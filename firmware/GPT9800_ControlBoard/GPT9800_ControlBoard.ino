/***************************************************************************************************
 * GPT-9000 Safety Test System - CONTROL BOARD FIRMWARE
 * Board  : Arduino Uno / Nano (ATmega328P, 16 MHz, 5 V)
 * Link   : USB CDC -> Hardware Serial, 115200 8N1
 * Version: 1.4.0  - thời gian chu trình cài từ PC bằng TIMING? / TIMING SET
 *                    + lệnh CHẨN ĐOÁN từng van / tiếp điểm để đấu dây (VALVE, ILK)
 *
 * PROTOCOL 1 - "NEW" ASCII, 1 command / 1 line, terminated by '\n'
 *   Host -> Board :  "PING"
 *   Board -> Host :  "OK PONG GPT9800-CB FW=1.3.1"
 *   Every reply starts with "OK " or "ERR ". Unsolicited events start with "EVT ".
 *
 * PROTOCOL 2 - "LEGACY" (compatible with the old Hipot_GPT9801 control box)
 *   Board -> Host :  "*TESTON#"   fixture đã đóng + pogo đã chạm + ĐÃ KHÓA LIÊN ĐỘNG
 *   Board -> Host :  "*CANCEL#"   ABORT pressed / safety cut
 *   Host -> Board :  "*R"         reset to ready (all outputs OFF)
 *   Host -> Board :  "*PASS#"     show PASS lamp, retract both cylinders
 *   Host -> Board :  "*FAIL#"     show FAIL lamp, retract both cylinders
 *
 * -------------------------------------------------------------------------------------------------
 * OPERATOR FLOW (FIXTURE_MODE = 1) - quy trình 2 xi-lanh + interlock GPT-9800:
 *
 *   operator puts the unit in the fixture and presses START (D7, or the PC sends "FIX START")
 *        |
 *        v
 *   [ST_FIX_CLOSING]  van FIXTURE (D10) ON -> clamp đóng xuống
 *                     chờ FIX_CLOSING_MS (hoặc cảm biến vị trí D2 nếu USE_POS_SENSOR=1)
 *        |
 *        v
 *   [ST_POGO_OUT]     van POGO (D11) ON -> pogo pin đẩy TỪ SAU RA TRƯỚC chạm unit
 *                     van fixture vẫn ON (không được nhả), chờ POGO_OUT_MS
 *        |
 *        v
 *   [ST_ARMED]        tiếp điểm D13 NGẮN INTERLOCK1(1) - INTERLOCK2(2) trên DB-9 SIGNAL I/O
 *                     của GPT-9800  ==> "KHÓA LIÊN ĐỘNG"
 *                     board gửi "*TESTON#" + "EVT HV_ARMED" cho PC
 *                     ... CHỜ PC gửi "HV ARM" (PC xác nhận máy sẵn sàng, không báo INTERLOCK OPEN)
 *        |
 *        v
 *   [ST_TESTING]      relay D4 ON -> đường phóng điện tới unit được nối
 *                     PC gửi FUNC:TEST ON -> GPT-9800 PHÓNG ĐIỆN -> MEAS? -> kết quả
 *        |
 *        v
 *   [ST_POGO_BACK]    PC gửi "*PASS#" / "*FAIL#"
 *                     THỨ TỰ NGẮN AN TOÀN: relay D4 OFF -> mở liên động D13 OFF
 *                     -> van pogo D11 OFF (pogo rút về) -> chờ POGO_BACK_MS
 *        |
 *        v
 *   [ST_FIX_OPEN]     van fixture D10 OFF (clamp mở) -> chờ FIX_OPEN_MS
 *        |
 *        v
 *   [ST_IDLE]         sẵn sàng cho unit tiếp theo (đèn PASS/FAIL giữ sáng tới START kế)
 *
 *   Vì sao pogo PHẢI rút TRƯỚC khi mở fixture: pogo đang chạm unit mà mở clamp
 *   sẽ kéo/móp pogo pin và làm xước mặt tiếp xúc unit.
 *
 * -------------------------------------------------------------------------------------------------
 * INTERLOCK CỦA GPT-9800 LÀ TIẾP ĐIỂM CỨNG, KHÔNG CÓ LỆNH SCPI
 *
 *   Manual GPT-9000/9800 (chương REMOTE CONTROL) KHÔNG có lệnh SCPI nào điều khiển interlock -
 *   cài ON/OFF nằm trong UTILITY -> CTRL (bấm tay 1 lần). Khi INTERLOCK = ON, máy chỉ cho
 *   phóng áp khi PIN 1 (INTERLOCK1) và PIN 2 (INTERLOCK2) trên DB-9 SIGNAL I/O bị SHORT.
 *   => "gửi lệnh khóa liên động" = board short 2 chân đó bằng tiếp điểm tại D13.
 *   Board CHỈ short khi fixture đã đóng VÀ pogo đã chạm unit -> máy không thể phóng áp khi fixture hở.
 *
 * -------------------------------------------------------------------------------------------------
 * STATE MACHINE - hai biến (bài học từ bug v1.2.0):
 *
 *   "state"     = VỊ TRÍ vật lý (đóng / đẩy ra / khóa / phát áp / rút / mở / lỗi)
 *   "fixPhase"  = KẾT QUẢ chu trình (none / pass / fail / aborted)
 *   Giữ riêng 2 biến nên một lần ghi đè không bao giờ làm van kẹt ON.
 *
 * -------------------------------------------------------------------------------------------------
 * SAFETY RULES ENFORCED BY THE FIRMWARE (cannot be bypassed from the PC):
 *
 *   1. THỨ TỰ NGẮN: relay D4 tắt TRƯỚC, tắt "cho phép đo" (D8 + D13) SAU, rồi mới di chuyển xi-lanh.
 *   2. POWER-ON / RESET STATE = mọi output LOW: không bao giờ cử động khi reset.
 *   3. ABORT ("TEST ABORT" / "*CANCEL#") luôn đi qua rút pogo -> mở fixture.
 *   4. Relay D4 CHỈ đóng được khi state == ST_TESTING (PC đã gửi "HV ARM" sau ST_ARMED)
 *      VÀ ngõ ra "cho phép đo" D8 đang bật.
 *      Lệnh "RELAY ON" tự phát bị từ chối: ERR RELAY_NOT_ARMED.
 *   5. Watchdog: relay D4 ON quá RELAY_MAX_ON_MS -> cắt + tắt "cho phép đo" + rút về + "*CANCEL#".
 *   6. Chờ "HV ARM" quá ARM_WAIT_MS (PC chết / hipot mất) -> tự nhả toàn bộ + "*CANCEL#".
 *   7. Hành trình vượt FIX_MAX_STROKE_MS (kẹt khí / hết khí) -> ST_FAULT: mọi thứ OFF,
 *      không chạy tiếp cho tới khi "*R" / "RESET".
 *   8. Unit sensor (D3) vẫn được kiểm khi USE_UNIT_SENSE = 1.
 *
 * PIN MAP (edit here if your wiring differs):
 *   D2  LAMP_FAIL       OUTPUT, đèn FAIL  (đã dời khỏi D8 - D8 là ngõ ra cho phép đo)
 *   D3  POS_POGO_OUT    INPUT_PULLUP, công tắc "pogo đã chạm" (chỉ khi USE_POS_SENSOR=1)
 *   D4  TEST_RELAY      OUTPUT, opto -> relay đường phóng điện : HIGH = NGẮN
 *   D5  BEACON          OUTPUT, đèn cảnh báo
 *   D6  BUZZER          OUTPUT, còi piezo
 *   D7  BTN_START       INPUT_PULLUP, nút START trên fixture (kèm trở kéo NGOÀI lên 5V -> song song, OK)
 *   D8  INTERLOCK       OUTPUT !! 1 = CHO PHÉP ĐO - điều khiển thiết bị cho phép đo
 *   D9  LAMP_PASS       OUTPUT, đèn PASS
 *   D10 SOL_FIXTURE     OUTPUT -> van xi-lanh CLAMP FIXTURE : ON = đóng xuống (hết điện lò xo rút về)
 *   D11 SOL_POGO        OUTPUT -> van xi-lanh POGO          : ON = đẩy pogo ra (hết điện lò xo rút về)
 *   D12 BTN_ABORT       INPUT_PULLUP, nút ABORT (chỉ khi USE_ABORT_BTN=1)
 *   D13 ILK_LOCK        OUTPUT -> opto/relay KHÔNG (NO) nối DB-9 pin1 - pin2 của GPT-9800
 *   A4  POS_FIX_CLOSED  INPUT_PULLUP, công tắc "fixture đã kín" (chỉ khi USE_POS_SENSOR=1)
 *
 *   SƠ ĐỒ ĐẤU DÂY ĐÃ KIỂM TRA THỰC TẾ BẰNG LỆNH "OUT <pin> ON <ms>":
 *     D7  = nút START            D10 = van clamp (đóng xuống)
 *     D8  = NGÕ RA cho phép đo   D11 = van pogo  (đẩy ra)
 *     D9  = đèn PASS             D13 = tiếp điểm interlock DB-9 (nếu có đấu)
 *
 *   D8 và D13 là 2 ngõ ra ĐỘC LẬP nhưng bật/tắt CÙNG LÚC:
 *     D8  -> thiết bị cho phép đo (PLC / relay / contactor enable)
 *     D13 -> tiếp điểm khô short DB-9 pin1-pin2 của GPT-9800
 *   Tắt `USE_ILK_DB9` = 0 nếu máy không dùng tiếp điểm D13.
 *
 * ANALOG INPUTS (ADC 10-bit, 0..1023) - read for real, never faked:
 *   A0  ACW voltage sense / A1 Leakage / A2 Insulation resistance / A3 Ground bond
 ****************************************************************************************************/

#include <Arduino.h>

/* ======================= USER CONFIGURATION - EDIT HERE ======================= */
#define FW_NAME        "GPT9800-CB"
#define FW_VERSION     "1.4.0"
#define SERIAL_BAUD    115200UL

/* ---- operating mode ---- */
#define FIXTURE_MODE     1     /* 1 = tự động 2 xi-lanh + khóa liên động; 0 = legacy thủ công */

/* Per-feature safety gates. Set to 0 only if that feature is physically not fitted. */
#define USE_UNIT_SENSE   0     /* D3 unit sensor      : 1 = required, 0 = not fitted */
#define USE_POS_SENSOR   0     /* A4/D3 công tắc hành trình: 1 = có dây, 0 = canh giờ mù */
#define USE_ABORT_BTN    0     /* nút ABORT (D12)     : 0 = KHÔNG có nút, bỏ qua */
#define USE_ILK_DB9      1     /* 1 = D13 đóng tiếp điểm DB-9 pin1-2 của GPT-9800 */

/* ---- pins ---- */
#define PIN_LAMP_FAIL      2    /* đèn FAIL (rời khỏi D8)                                    */
#define PIN_POS_POGO_OUT   3    /* công tắc "pogo đã chạm"  (chỉ khi USE_POS_SENSOR=1)      */
#define PIN_TEST_RELAY     4
#define PIN_BEACON         5
#define PIN_BUZZER         6
#define PIN_BTN_START      7
#define PIN_INTERLOCK      8    /* OUTPUT: 1 = CHO PHÉP ĐO (thiết bị cho phép đo)            */
#define PIN_LAMP_PASS      9    /* đèn PASS                                                  */
#define PIN_SOL_FIXTURE    10   /* van xi-lanh clamp fixture: ON = đóng xuống (đã kiểm tra) */
#define PIN_SOL_POGO       11   /* van xi-lanh pogo        : ON = đẩy ra     (đã kiểm tra) */
#define PIN_BTN_ABORT      12   /* nút ABORT: CHỈ dùng khi USE_ABORT_BTN=1                   */
#define PIN_ILK_LOCK       13   /* tiếp điểm interlock GPT-9800 (DB-9 pin1-pin2)             */
#define PIN_POS_FIX_CLOSED A4   /* công tắc "fixture đã kín" (chỉ khi USE_POS_SENSOR=1)      */

#define PIN_AIN_ACW        A0
#define PIN_AIN_LEAK       A1
#define PIN_AIN_IR         A2
#define PIN_AIN_GB         A3

/* 1 = HIGH means "active", 0 = LOW means "active" (for active-low relay/lamps/valves) */
#define RELAY_ACTIVE_HIGH  1
#define LAMP_ACTIVE_HIGH   1
#define BEACON_ACTIVE_HIGH 1
#define BUZZER_ACTIVE_HIGH 1
#define SOL_ACTIVE_HIGH    1
#define INTERLOCK_ACTIVE_HIGH 1 /* 1 = HIGH cho phép đo (D8) */
#define ILK_ACTIVE_HIGH    1    /* 1 = HIGH đóng tiếp điểm (opto/relay transistor) */
#define BTN_ACTIVE_LOW     1    /* 1 = button to GND with INPUT_PULLUP */

/* ---- fixture timing (đổi cho khớp hành trình THẬT của xi-lanh bạn) ---- */
uint32_t FIX_CLOSING_MS   = 2500UL;  /* đổi bằng TIMING SET khi board IDLE */
uint32_t POGO_OUT_MS      = 1200UL;
uint32_t POGO_BACK_MS     = 1200UL;
uint32_t FIX_OPEN_MS      = 2500UL;
const uint32_t FIX_MAX_STROKE_MS = 8000UL; /* một hành trình dài hơn này = FAULT      */

/* ---- general safety ---- */
const uint32_t RELAY_MAX_ON_MS = 30000UL;  /* watchdog relay: auto-cut sau 30 s        */
uint32_t ARM_WAIT_MS     = 30000UL;  /* TIMING SET chỉ cho phép 1000..30000 ms */
const uint32_t HEARTBEAT_MS    = 2000UL;
const uint32_t BTN_DEBOUNCE_MS = 40UL;
const uint16_t LINE_MAX        = 80;
/* =========================================================================== */

/* Vị trí vật lý hiện tại của fixture. */
enum BoardState : uint8_t {
  ST_IDLE = 0,     /* mọi thứ OFF, chờ unit + START                        */
  ST_FIX_CLOSING,  /* van fixture ON - clamp đang đóng xuống               */
  ST_POGO_OUT,     /* fixture đóng + van pogo ON - pogo đang đẩy ra         */
  ST_ARMED,        /* fixture đóng + pogo chạm + LIÊN ĐỘNG ĐANG KHÓA - chờ PC */
  ST_TESTING,      /* relay D4 ON - PC đang phóng áp                        */
  ST_POGO_BACK,    /* relay OFF, liên động mở, pogo đang rút về             */
  ST_FIX_OPEN,     /* pogo đã về, fixture đang mở ra                        */
  ST_FAULT         /* kẹt / quá thời gian - mọi thứ OFF, cần RESET         */
};

/* KẾT QUẢ chu trình - độc lập với "state" nên không bao giờ ghi đè hành trình. */
enum FixPhase : uint8_t {
  FX_NONE = 0,
  FX_PASS,
  FX_FAIL,
  FX_ABORTED
};

static char      lineBuf[LINE_MAX];
static uint16_t  lineLen = 0;
static bool      lineOverflow = false;

static BoardState state        = ST_IDLE;
static FixPhase   fixPhase     = FX_NONE;
static bool       relayOn      = false;
static bool       beaconOn     = false;
static bool       eventsEnabled = false;
static uint32_t   relaySinceMs  = 0;
static uint32_t   strokeSinceMs = 0;   /* mốc thời gian của hành trình hiện tại */
static uint32_t   armSinceMs    = 0;   /* mốc vào ST_ARMED - chờ PC "HV ARM"    */
static uint32_t   beepUntilMs   = 0;
static uint32_t   lastBeatMs    = 0;
static uint32_t   cmdCount      = 0;

/* Hẹn giờ TỰ TẮT cho chế độ chẩn đoán đấu dây ("VALVE ... ON <ms>" / "ILK ON <ms>"). */
static uint32_t   valveFixOffAt  = 0;   /* 0 = không hẹn giờ */
static uint32_t   valvePogoOffAt = 0;
static uint32_t   ilkOffAt       = 0;

static bool      lastStartLevel = false;
static bool      lastAbortLevel = false;
static uint32_t  startEdgeMs    = 0;
static uint32_t  abortEdgeMs    = 0;

/* ------------------------------ pin helpers --------------------------------- */
static inline void outWrite(uint8_t pin, bool on, bool activeHigh)
{
  digitalWrite(pin, (on == activeHigh) ? HIGH : LOW);
}
static inline bool readActiveLow(uint8_t pin)
{
  return digitalRead(pin) == LOW;   /* INPUT_PULLUP + switch to GND */
}
static inline bool readButton(uint8_t pin)
{
#if BTN_ACTIVE_LOW
  return digitalRead(pin) == LOW;
#else
  return digitalRead(pin) == HIGH;
#endif
}
static inline const char* onOff(bool v) { return v ? "ON" : "OFF"; }

/* Đọc trạng thái THẬT từ chân (không tin vào biến nhớ). */
static bool solIsOn(uint8_t pin)
{
  return digitalRead(pin) == (SOL_ACTIVE_HIGH ? HIGH : LOW);
}
static bool relayIsOn()
{
  return digitalRead(PIN_TEST_RELAY) == (RELAY_ACTIVE_HIGH ? HIGH : LOW);
}
static bool ilkIsLocked()
{
  /* D8 = ngõ ra "CHO PHÉP ĐO" - đọc lại chính mức đã ghi. */
  return digitalRead(PIN_INTERLOCK) == (INTERLOCK_ACTIVE_HIGH ? HIGH : LOW);
}

/* --------------------- real ADC reads (never hardcoded) --------------------- */
static uint16_t readAvg(uint8_t ch)
{
  uint32_t sum = 0;
  for (uint8_t i = 0; i < 8; i++) sum += analogRead(ch);
  return (uint16_t)(sum >> 3);
}

static uint16_t readVccMv()
{
#if defined(__AVR_ATmega328P__) || defined(__AVR_ATmega168__)
  ADMUX  = _BV(REFS0) | _BV(MUX3) | _BV(MUX2) | _BV(MUX1);   /* 1.1V / AVcc */
  ADCSRA |= _BV(ADSC);
  while (bit_is_set(ADCSRA, ADSC)) { }
  uint16_t raw = ADC;
  if (raw == 0) return 0;
  return (uint16_t)((1125300UL) / raw);
#else
  return 0;
#endif
}

static const char* stateName(BoardState s)
{
  switch (s) {
    case ST_IDLE:        return "IDLE";
    case ST_FIX_CLOSING: return "CLOSING";
    case ST_POGO_OUT:    return "POGO_OUT";
    case ST_ARMED:       return "ARMED";
    case ST_TESTING:     return "TESTING";
    case ST_POGO_BACK:   return "POGO_BACK";
    case ST_FIX_OPEN:    return "FIX_OPEN";
    case ST_FAULT:       return "FAULT";
  }
  return "?";
}

static const char* phaseName(FixPhase p)
{
  switch (p) {
    case FX_NONE:    return "-";
    case FX_PASS:    return "P";
    case FX_FAIL:    return "F";
    case FX_ABORTED: return "A";
  }
  return "?";
}

/* --------------------------- valves (2 cylinders) --------------------------- */
/* MỖI xi-lanh có 1 điện từ riêng: cấp điện = hành trình, hết điện = lò xo tự rút.
 * Vì 2 xi-lanh KHÁC nhau nên D10 và D11 ĐƯỢC phép ON đồng thời
 * (fixture phải giữ KÍN trong lúc pogo đang đẩy ra) - không áp dụng break-before-make. */

static void valveFix(bool on)  { outWrite(PIN_SOL_FIXTURE, on, SOL_ACTIVE_HIGH); }
static void valvePogo(bool on) { outWrite(PIN_SOL_POGO,    on, SOL_ACTIVE_HIGH); }
static void valvesOff()        { valveFix(false); valvePogo(false); }

/* ------------------- ngõ ra "CHO PHÉP ĐO" (interlock) ----------------------- */
/* D8  -> thiết bị cho phép đo (PLC / relay / contactor enable)   [luôn có]
 * D13 -> tiếp điểm khô short DB-9 pin1-pin2 của GPT-9800         [USE_ILK_DB9]
 * Hai ngõ ra ĐỘC LẬP nhưng bật/tắt CÙNG LÚC để máy đo chỉ được phép
 * phóng áp khi fixture đã kín + pogo đã chạm unit. */
static void ilkLock(bool on)
{
  outWrite(PIN_INTERLOCK, on, INTERLOCK_ACTIVE_HIGH);
#if USE_ILK_DB9
  outWrite(PIN_ILK_LOCK,  on, ILK_ACTIVE_HIGH);
#endif
}

/* ------------------ chẩn đoán đấu dây (chỉ khi IDLE, relay OFF) ------------------ */
/* Dùng để xác định ĐÚNG van nào nối vào chân nào, và ngõ ra "cho phép đo" đã ăn chưa:
 *     VALVE FIXTURE ON 3000     -> bật van D10 3 s rồi tự tắt (quan sát xi-lanh nào chạy)
 *     VALVE POGO    ON 3000     -> bật van D11 3 s rồi tự tắt
 *     VALVE STATUS / ILK ON 3000 / ILK OFF     (ILK bật cả D8 + D13)
 * Tự tắt theo thời gian nên KHÔNG BAO GIỜ kẹt van ON nếu mất kết nối serial. */
static bool manualOutputAllowed()
{
  return !relayOn && state == ST_IDLE;
}

static void diagTimers()
{
  uint32_t now = millis();
  if (valveFixOffAt  && (int32_t)(now - valveFixOffAt)  >= 0) { valveFix(false);  valveFixOffAt  = 0; }
  if (valvePogoOffAt && (int32_t)(now - valvePogoOffAt) >= 0) { valvePogo(false); valvePogoOffAt = 0; }
  if (ilkOffAt       && (int32_t)(now - ilkOffAt)       >= 0) { ilkLock(false);   ilkOffAt       = 0; }
}

static void diagCancel()
{
  valveFixOffAt = 0;
  valvePogoOffAt = 0;
  ilkOffAt = 0;
}

/* ---------------- dò đấu dây: bật HIGH từng chân D2..D13 ---------------- */
/* Dùng khi đang đấu dây/commissioning: cho phép xuất HIGH trực tiếp lên một chân
 * bất kỳ để xem thiết bị nào chạy, KHÔNG cần biết trước chân đó là van hay đèn.
 * Chỉ cho phép khi STATE=IDLE và relay HV đang OFF -> không thể phóng áp. */
static bool diagPinAllowed(uint8_t p)
{
  if (p == PIN_BTN_START) return false;            /* D7 = nút START, không đụng */
  if (p == PIN_BTN_ABORT && USE_ABORT_BTN) return false;
  return p >= 2 && p <= 13;
}

static void diagRestorePin(uint8_t p)
{
  digitalWrite(p, LOW);
#if USE_ABORT_BTN
  if (p == PIN_BTN_ABORT) { pinMode(p, INPUT_PULLUP); return; }  /* trả lại nút ABORT */
#endif
  pinMode(p, OUTPUT);          /* các chân còn lại đều là OUTPUT */
}

static void diagOutPin(uint8_t p, bool on, uint32_t ms)
{
  pinMode(p, OUTPUT);
  digitalWrite(p, on ? HIGH : LOW);
  Serial.print(F("OK OUT D")); Serial.print(p);
  Serial.print(' ');           Serial.println(on ? F("ON") : F("OFF"));

  if (on && ms) {
    delay(ms);                    /* chặn trong lúc dò - không có relay HV nào bật */
    diagRestorePin(p);
    Serial.print(F("OK OUT D")); Serial.print(p);
    Serial.print(F(" OFF (auto ")); Serial.print(ms); Serial.println(F(" ms)"));
  }
}

static void diagOutScan(uint32_t ms)
{
  const uint8_t pins[] = { PIN_LAMP_FAIL, PIN_LAMP_PASS, PIN_INTERLOCK,
                           PIN_SOL_FIXTURE, PIN_SOL_POGO, PIN_ILK_LOCK };
  const char* names[] = { "DEN FAIL (D2)", "DEN PASS (D9)", "CHO PHEP DO (D8)",
                          "VAN CLAMP FIXTURE (D10)", "VAN POGO (D11)", "TIEP DIEM DB-9 (D13)" };

  /* relay HV phải OFF trước khi dò - relayApply() định nghĩa ở dưới nên ghi trực tiếp */
  digitalWrite(PIN_TEST_RELAY, LOW);
  relayOn = false;
  ilkLock(false);
  valvesOff();
  diagCancel();
  Serial.print(F("OK OUT SCAN START ")); Serial.print(ms); Serial.println(F(" ms/chan"));

  for (uint8_t i = 0; i < 6; i++) {
    uint8_t p = pins[i];
    Serial.print(F(">>> BAT ")); Serial.println(names[i]);
    pinMode(p, OUTPUT);
    digitalWrite(p, HIGH);
    delay(ms);
    digitalWrite(p, LOW);
    diagRestorePin(p);
    Serial.print(F("<<< TAT ")); Serial.println(names[i]);
    delay(400);
  }
  Serial.println(F("OK OUT SCAN DONE"));
}

/* ------------------------------ outputs ------------------------------------ */
static void relayApply(bool on)
{
  /* Relay HV chỉ được đóng khi ngõ ra "CHO PHÉP ĐO" (D8) đang bật. */
  if (on && !ilkIsLocked())                   on = false;
#if USE_UNIT_SENSE
  if (on && !readActiveLow(PIN_POS_POGO_OUT)) on = false;
#endif
#if FIXTURE_MODE
  /* Chỉ đóng được khi ST_TESTING - tức PC đã qua ST_ARMED và gửi "HV ARM". */
  if (on && state != ST_TESTING)              on = false;
#endif

  if (on == relayOn) return;
  relayOn = on;
  outWrite(PIN_TEST_RELAY, on, RELAY_ACTIVE_HIGH);
  if (on) relaySinceMs = millis();
}

static void lampsOff()
{
  outWrite(PIN_LAMP_PASS, false, LAMP_ACTIVE_HIGH);
  outWrite(PIN_LAMP_FAIL, false, LAMP_ACTIVE_HIGH);
}
static void lampPass() { lampsOff(); outWrite(PIN_LAMP_PASS, true, LAMP_ACTIVE_HIGH); }
static void lampFail() { lampsOff(); outWrite(PIN_LAMP_FAIL, true, LAMP_ACTIVE_HIGH); }
static void beep(uint16_t ms) { beepUntilMs = millis() + ms; }
static void beaconSet(bool on) { beaconOn = on; outWrite(PIN_BEACON, on, BEACON_ACTIVE_HIGH); }

/* --------------------------------- events --------------------------------- */
static void emitEvent(const char* ev)
{
  if (!eventsEnabled) return;
  Serial.print(F("EVT "));
  Serial.println(ev);
}

/* ------------------------------- status line -------------------------------- */
static void printStatus()
{
  Serial.print(F("OK STATUS STATE="));
  Serial.print(stateName(state));
  Serial.print(F(" RESULT="));
  Serial.print(phaseName(fixPhase));
  Serial.print(F(" INTERLOCK="));
  /* D8 là ngõ ra: CLOSED = đang cho phép đo */
  Serial.print(ilkIsLocked() ? F("CLOSED") : F("OPEN"));
  Serial.print(F(" UNIT="));
#if USE_UNIT_SENSE
  Serial.print(readActiveLow(PIN_POS_POGO_OUT) ? F("PRESENT") : F("ABSENT"));
#else
  Serial.print(F("BYPASS"));
#endif
  Serial.print(F(" FIXTURE=")); Serial.print(onOff(solIsOn(PIN_SOL_FIXTURE)));
  Serial.print(F(" POGO="));    Serial.print(onOff(solIsOn(PIN_SOL_POGO)));
  Serial.print(F(" LOCK="));    Serial.print(onOff(ilkIsLocked()));
  Serial.print(F(" RELAY="));   Serial.print(onOff(relayIsOn()));
  Serial.print(F(" BEACON="));  Serial.print(onOff(beaconOn));
  Serial.print(F(" UPTIME_MS="));
  Serial.print(millis());
  Serial.print(F(" CMDS="));
  Serial.println(cmdCount);
}

/* ------------------------- fixture cycle (2 cylinders) ---------------------- */

/* THỨ TỰ NGẮN AN TOÀN - dùng cho cả PASS, FAIL lẫn ABORT:
 *   1. relay D4 OFF      -> ngắt đường phóng điện TRƯỚC tiên
 *   2. liên động D13 OFF -> GPT-9800 không thể phóng áp được nữa
 *   3. van pogo OFF      -> pogo rút về (chờ POGO_BACK_MS)
 *   4. van fixture OFF   -> clamp mở (chờ FIX_OPEN_MS)   [trong fixtureStep]
 * Không bao giờ mở fixture khi pogo còn đang chạm unit. */
static void retract(FixPhase phase)
{
  relayApply(false);
  ilkLock(false);
  beaconSet(false);
  valvePogo(false);
  fixPhase = phase;
  state = ST_POGO_BACK;
  strokeSinceMs = millis();
  emitEvent("HV_RELEASED");
}

/* Bắt đầu chu trình: đóng fixture. Chỉ hợp lệ từ IDLE. */
static bool fixtureClose()
{
#if FIXTURE_MODE
  if (state != ST_IDLE) { Serial.println(F("ERR FIX_NOT_IDLE")); return false; }

  diagCancel();               /* bỏ mọi hẹn giờ chẩn đoán còn treo */
  lampsOff();                 /* xóa đèn kết quả chu trình trước */
  fixPhase = FX_NONE;
  beaconSet(true);
  ilkLock(false);             /* chắc chắn liên động đang MỞ ở lúc bắt đầu */
  relayApply(false);
  valveFix(true);
  state = ST_FIX_CLOSING;
  strokeSinceMs = millis();
  beep(100);
  emitEvent("CYCLE_CLOSING");
  return true;
#else
  Serial.println(F("ERR FIX_DISABLED"));
  return false;
#endif
}

/* Xi-lanh fixture đã đi hết hành trình -> pogo pin đẩy ra. */
static void fixtureClosed()
{
  state = ST_POGO_OUT;
  strokeSinceMs = millis();
  valvePogo(true);
  beep(80);
  emitEvent("POGO_OUT");
}

/* Pogo pin đã chạm unit -> KHÓA LIÊN ĐỘNG cho GPT-9800 rồi báo PC. */
static void fixtureArmed()
{
  ilkLock(true);                     /* short DB-9 pin1-pin2 */
  state = ST_ARMED;
  armSinceMs = millis();
  strokeSinceMs = millis();
  beep(150);
  emitEvent("HV_ARMED");
  Serial.println(F("*TESTON#"));     /* legacy: PC bắt đầu phép thử */
}

/* PC xác nhận sẵn sàng -> đóng relay D4, cho phép phóng áp. */
static void hvArm()
{
  if (state != ST_ARMED) {
    Serial.println(relayOn ? F("ERR HV_ALREADY") : F("ERR HV_NOT_ARMED"));
    return;
  }
  if (!ilkIsLocked()) { Serial.println(F("ERR HV_INTERLOCK_NOT_LOCKED")); return; }
  state = ST_TESTING;                /* phải đặt TRƯỚC relayApply(): relay gate trên state */
  relayApply(true);
  emitEvent("HV_RELAY_ON");
  Serial.print(F("OK HV ARMED RELAY="));
  Serial.println(onOff(relayIsOn()));
}

/* PC/người vận hành nhả HV mà không retract (dừng giữa chừng). */
static void hvDisarm()
{
  relayApply(false);
  emitEvent("HV_RELEASED");
  Serial.print(F("OK HV DISARMED RELAY="));
  Serial.println(onOff(relayIsOn()));
}

/* Kết quả từ PC: đèn + còi, rồi rút về theo thứ tự an toàn. */
static void fixtureResult(bool pass)
{
  if (pass) lampPass(); else lampFail();
  beep(pass ? 300 : 900);
  emitEvent(pass ? "RESULT_PASS" : "RESULT_FAIL");
  retract(pass ? FX_PASS : FX_FAIL);
}

/* Mọi thứ OFF ngay lập tức (dùng cho *R / RESET và ST_FAULT). */
static void allOff(const char* why)
{
  relayApply(false);
  ilkLock(false);
  valvesOff();
  beaconSet(false);
  diagCancel();
  if (why) emitEvent(why);
}

/* Advance the fixture state machine. Called every loop(). */
static void fixtureStep()
{
#if FIXTURE_MODE
  uint32_t now = millis();

  switch (state) {
    case ST_FIX_CLOSING:
#if USE_POS_SENSOR
      /* D2 = công tắc "fixture đã kín". */
      if (readActiveLow(PIN_POS_FIX_CLOSED)) { fixtureClosed(); break; }
#else
      if (now - strokeSinceMs >= FIX_CLOSING_MS) { fixtureClosed(); break; }
#endif
      if (now - strokeSinceMs > FIX_MAX_STROKE_MS) goto strokeFault;
      break;

    case ST_POGO_OUT:
#if USE_POS_SENSOR
      /* D3 = công tắc "pogo đã chạm unit". */
      if (readActiveLow(PIN_POS_POGO_OUT)) { fixtureArmed(); break; }
#else
      if (now - strokeSinceMs >= POGO_OUT_MS) { fixtureArmed(); break; }
#endif
      if (now - strokeSinceMs > FIX_MAX_STROKE_MS) goto strokeFault;
      break;

    case ST_ARMED:
      /* PC không bao giờ "HV ARM" (mất hipot / app bị đóng) -> tự nhả, không kẹt fixture. */
      if (now - armSinceMs >= ARM_WAIT_MS) {
        fixPhase = FX_ABORTED;
        beep(500);
        emitEvent("ARM_TIMEOUT");
        retract(FX_ABORTED);
        Serial.println(F("*CANCEL#"));
      }
      break;

    case ST_TESTING:
      break;   /* watchdog lo phần relay */

    case ST_POGO_BACK:
      if (now - strokeSinceMs >= POGO_BACK_MS) {
        valveFix(false);                 /* pogo đã về -> mở fixture */
        state = ST_FIX_OPEN;
        strokeSinceMs = now;
        emitEvent("CYCLE_FIXTURE_OPEN");
        break;
      }
      if (now - strokeSinceMs > FIX_MAX_STROKE_MS) goto strokeFault;
      break;

    case ST_FIX_OPEN:
      if (now - strokeSinceMs >= FIX_OPEN_MS) {
        valvesOff();
        state = ST_IDLE;
        beaconSet(false);
        emitEvent("CYCLE_IDLE");
        break;
      }
      if (now - strokeSinceMs > FIX_MAX_STROKE_MS) goto strokeFault;
      break;

    default:
      break;
  }
  return;

strokeFault:
  /* Kẹt / hết khí. Cắt hết và không chạy tiếp tới khi RESET - không bao giờ
   * để fixture/hành trình nửa vời khi đã có áp cao. */
  relayApply(false);
  ilkLock(false);
  valvesOff();
  beaconSet(false);
  state = ST_FAULT;
  beep(1000);
  emitEvent("STROKE_TIMEOUT");
  Serial.println(F("*CANCEL#"));
#endif
}

/* ------------------------------- command parser ----------------------------- */
static bool eq(const char* a, const char* b) { return strcasecmp(a, b) == 0; }

/* One atomic update; no outputs change. Timings live in RAM and the PC restores
 * its persisted configuration after connecting/resetting the board. The fixed
 * 8 s stroke watchdog and 30 s relay watchdog cannot be relaxed by this command. */
static void printTiming()
{
  Serial.print(F("OK TIMING V=1 CLOSE=")); Serial.print((unsigned long)FIX_CLOSING_MS);
  Serial.print(F(" OUT=")); Serial.print((unsigned long)POGO_OUT_MS);
  Serial.print(F(" BACK=")); Serial.print((unsigned long)POGO_BACK_MS);
  Serial.print(F(" OPEN=")); Serial.print((unsigned long)FIX_OPEN_MS);
  Serial.print(F(" ARM=")); Serial.println((unsigned long)ARM_WAIT_MS);
}

static bool parseTimingValue(const char* text, uint32_t maxValue, uint32_t* value)
{
  if (!text || !*text) return false;
  uint32_t n = 0;
  for (const char* p = text; *p; ++p) {
    if (*p < '0' || *p > '9') return false;
    uint8_t digit = (uint8_t)(*p - '0');
    if (n > (maxValue - digit) / 10UL) return false;
    n = n * 10UL + digit;
  }
  *value = n;
  return true;
}

static void setTiming(int argc, char** argv)
{
  if (argc != 7 || !eq(argv[1], "SET")) { Serial.println(F("ERR TIMING_ARGS")); return; }
  if (state != ST_IDLE || relayOn || relayIsOn() || ilkIsLocked() ||
      solIsOn(PIN_SOL_FIXTURE) || solIsOn(PIN_SOL_POGO)) {
    Serial.println(F("ERR TIMING_BUSY")); return;
  }
  uint32_t values[5];
  for (uint8_t i = 0; i < 5; ++i) {
    uint32_t minValue = i == 4 ? 1000UL : 100UL;
    uint32_t maxValue = i == 4 ? 30000UL : 7500UL;
    if (!parseTimingValue(argv[i + 2], maxValue, &values[i]) || values[i] < minValue) {
      Serial.println(F("ERR TIMING_RANGE")); return;
    }
  }
  FIX_CLOSING_MS = values[0]; POGO_OUT_MS = values[1]; POGO_BACK_MS = values[2];
  FIX_OPEN_MS = values[3]; ARM_WAIT_MS = values[4];
  printTiming();
}

/* START vật lý hoặc từ PC - dùng chung. */
static void requestStart()
{
  if (relayOn || state == ST_ARMED || state == ST_TESTING) {
    Serial.println(F("ERR TEST_ALREADY_RUNNING"));
    return;
  }

#if FIXTURE_MODE
  if (!fixtureClose()) return;
  Serial.println(F("OK FIX CLOSING"));
#else
  if (state == ST_POGO_OUT || state == ST_POGO_BACK || state == ST_FIX_OPEN) {
    Serial.println(F("ERR FIX_BUSY"));
    return;
  }
#if USE_UNIT_SENSE
  if (!readActiveLow(PIN_POS_POGO_OUT)) { Serial.println(F("ERR TEST_NO_UNIT"));       return; }
#endif
  lampsOff();
  beaconSet(true);
  ilkLock(true);              /* legacy mode: bật "cho phép đo" khi bắt đầu test */
  state = ST_TESTING;
  relayApply(true);
  beep(150);
  emitEvent("TEST_STARTED");
  Serial.println(F("OK TEST STARTED"));
#endif
}

/* ABORT: cắt relay + mở liên động trước, rồi rút pogo -> mở fixture. */
static void requestAbort()
{
  beep(400);
  emitEvent("TEST_ABORTED");

  if (state == ST_FAULT) { Serial.println(F("OK TEST ABORTED")); return; }  /* đã an toàn */

#if FIXTURE_MODE
  if (state == ST_IDLE) {
    allOff(NULL);                 /* không có gì để rút - về sạch */
    fixPhase = FX_ABORTED;
  } else {
    retract(FX_ABORTED);          /* pogo BACK -> FIX_OPEN chạy tiếp trong fixtureStep */
  }
#else
  relayApply(false);
  ilkLock(false);
  beaconSet(false);
  fixPhase = FX_ABORTED;
  state = ST_IDLE;
#endif
  Serial.println(F("OK TEST ABORTED"));
}

/* LEGACY trigger: báo PC chu trình đã sẵn sàng. */
static void legacyStartTrigger()
{
#if FIXTURE_MODE
  if (fixtureClose()) Serial.println(F("OK FIX CLOSING"));
#else
  requestStart();
  if (state == ST_TESTING) Serial.println(F("*TESTON#"));
#endif
}

static void legacyAbortTrigger()
{
  requestAbort();
  Serial.println(F("*CANCEL#"));
}

static void printFixStatus()
{
  Serial.print(F("OK FIX STATE="));   Serial.print(stateName(state));
  Serial.print(F(" RESULT="));        Serial.print(phaseName(fixPhase));
  Serial.print(F(" FIXTURE="));       Serial.print(onOff(solIsOn(PIN_SOL_FIXTURE)));
  Serial.print(F(" POGO="));          Serial.print(onOff(solIsOn(PIN_SOL_POGO)));
  Serial.print(F(" LOCK="));          Serial.print(onOff(ilkIsLocked()));
  Serial.print(F(" RELAY="));         Serial.println(onOff(relayIsOn()));
}

static void handleCommand(char* cmd)
{
  cmdCount++;
  char* argv[8];
  int   argc = 0;
  char* p = strtok(cmd, " \t");
  while (p && argc < 8) { argv[argc++] = p; p = strtok(NULL, " \t"); }
  if (argc == 0) { Serial.println(F("ERR EMPTY")); return; }

  if (eq(argv[0], "TIMING?")) {
    if (argc != 1) { Serial.println(F("ERR TIMING_ARGS")); return; }
    printTiming(); return;
  }
  if (eq(argv[0], "TIMING")) { setTiming(argc, argv); return; }

  /* ---------- LEGACY protocol (old control box) ---------- */
  if (eq(argv[0], "*R")) {
    allOff(NULL);
    lampsOff();
    fixPhase = FX_NONE;
    state = ST_IDLE;
    cmdCount = 0;
    Serial.println(F("OK RESET"));
    return;
  }
  if (eq(argv[0], "*PASS#")) { fixtureResult(true);  Serial.println(F("OK RESULT PASS"));  return; }
  if (eq(argv[0], "*FAIL#")) { fixtureResult(false); Serial.println(F("OK RESULT FAIL"));  return; }
  if (eq(argv[0], "*CLS"))   { Serial.println(F("OK CLS")); return; }

  /* ---------- administration / diagnostics ---------- */
  if (eq(argv[0], "PING")) {
    Serial.print(F("OK PONG ")); Serial.print(F(FW_NAME));
    Serial.print(F(" FW=")); Serial.println(F(FW_VERSION));
    return;
  }
  if (eq(argv[0], "ID?")) {
    Serial.print(F("OK ID NAME=")); Serial.print(F(FW_NAME));
    Serial.print(F(" FW=")); Serial.print(F(FW_VERSION));
    Serial.print(F(" BOARD=UNO328 BAUD=")); Serial.println((unsigned long)SERIAL_BAUD);
    return;
  }
  if (eq(argv[0], "HELP")) {
    Serial.println(F("OK CMD TIMING? | TIMING SET <close> <out> <back> <open> <arm> (ms) | PING | ID? | STATUS? | INTERLOCK? | UNIT? | MEAS? | HV ARM|DISARM | RELAY ON|OFF | BEACON ON|OFF | BEEP <ms> | LAMP PASS|FAIL|OFF | TEST START|ABORT | FIX START|OPEN|STATUS | RESULT PASS|FAIL | VALVE FIXTURE|POGO|STATUS [ON|OFF [ms]] | ILK ON|OFF [ms] | OUT <4..13>|SCAN|STATUS [ON|OFF [ms]] | EVENTS [ON|OFF] | RESET  ||  legacy: *R *PASS# *FAIL# *CLS"));
    return;
  }
  if (eq(argv[0], "STATUS?"))   { printStatus(); return; }
  if (eq(argv[0], "INTERLOCK?")){
    Serial.print(F("OK INTERLOCK "));
    Serial.println(ilkIsLocked() ? F("CLOSED") : F("OPEN"));   /* CLOSED = đang cho phép đo */
    return;
  }
  if (eq(argv[0], "UNIT?")){
    Serial.print(F("OK UNIT "));
#if USE_UNIT_SENSE
    Serial.println(readActiveLow(PIN_POS_POGO_OUT) ? F("PRESENT") : F("ABSENT"));
#else
    Serial.println(F("BYPASS"));
#endif
    return;
  }

  /* ---------- REAL measured values from the sensors (ADC), never faked ---------- */
  if (eq(argv[0], "MEAS?")) {
    uint16_t a0 = readAvg(PIN_AIN_ACW), a1 = readAvg(PIN_AIN_LEAK);
    uint16_t a2 = readAvg(PIN_AIN_IR),  a3 = readAvg(PIN_AIN_GB);
    Serial.print(F("OK MEAS ACW="));     Serial.print(a0);
    Serial.print(F(" LEAK="));           Serial.print(a1);
    Serial.print(F(" IR="));             Serial.print(a2);
    Serial.print(F(" GB="));             Serial.print(a3);
    Serial.print(F(" VCC_MV="));         Serial.println(readVccMv());
    return;
  }

  /* ---------- HIGH VOLTAGE gate: PC xác nhận rồi board mới cho phép phóng áp ---------- */
  if (eq(argv[0], "HV")) {
    if (argc < 2) { Serial.println(F("ERR HV_NEED_ARM_OR_DISARM")); return; }
    if      (eq(argv[1], "ARM"))   hvArm();
    else if (eq(argv[1], "DISARM")) hvDisarm();
    else Serial.println(F("ERR HV_BAD_ARG"));
    return;
  }

  /* ---------- CHẨN ĐOÁN ĐẤU DÂY: bật riêng từng van / tiếp điểm interlock ---------- */
  if (eq(argv[0], "VALVE")) {
    if (argc < 2) { Serial.println(F("ERR VALVE_NEED_ARG")); return; }
    if (eq(argv[1], "STATUS")) {
      Serial.print(F("OK VALVE FIXTURE=")); Serial.print(onOff(solIsOn(PIN_SOL_FIXTURE)));
      Serial.print(F(" POGO="));            Serial.print(onOff(solIsOn(PIN_SOL_POGO)));
      Serial.print(F(" LOCK="));            Serial.println(onOff(ilkIsLocked()));
      return;
    }
    bool isFix  = eq(argv[1], "FIXTURE");
    bool isPogo = eq(argv[1], "POGO");
    if (!isFix && !isPogo) { Serial.println(F("ERR VALVE_BAD_ARG")); return; }
    if (argc < 3)          { Serial.println(F("ERR VALVE_NEED_ON_OFF")); return; }
    if (!manualOutputAllowed()) { Serial.println(F("ERR VALVE_BUSY")); return; }

    bool on = eq(argv[2], "ON");
    uint32_t ms = 0;
    if (on) ms = (argc >= 4) ? (uint32_t)constrain(atol(argv[3]), 0L, 60000L) : 2000UL;
    if (isFix) { valveFix(on);  valveFixOffAt  = (ms ? millis() + ms : 0); }
    else       { valvePogo(on); valvePogoOffAt = (ms ? millis() + ms : 0); }

    Serial.print(F("OK VALVE ")); Serial.print(isFix ? F("FIXTURE") : F("POGO"));
    Serial.print(' ');
    Serial.print(onOff(isFix ? solIsOn(PIN_SOL_FIXTURE) : solIsOn(PIN_SOL_POGO)));
    Serial.print(F(" AUTO_OFF_MS=")); Serial.println(ms);
    return;
  }

  if (eq(argv[0], "ILK")) {
    if (argc < 2) { Serial.println(F("ERR ILK_NEED_ON_OFF")); return; }
    if (!manualOutputAllowed()) { Serial.println(F("ERR ILK_BUSY")); return; }

    bool on = eq(argv[1], "ON");
    uint32_t ms = on ? ((argc >= 3) ? (uint32_t)constrain(atol(argv[2]), 0L, 60000L) : 2000UL) : 0UL;
    ilkLock(on);
    ilkOffAt = (on && ms) ? millis() + ms : 0;

    Serial.print(F("OK ILK ")); Serial.print(onOff(ilkIsLocked()));
    Serial.print(F(" AUTO_OFF_MS=")); Serial.println(ms);
    return;
  }

  /* ---------- DÒ ĐẤU DÂY: bật HIGH từng chân để xem thiết bị nào chạy ---------- */
  if (eq(argv[0], "OUT")) {
    if (argc < 2) { Serial.println(F("ERR OUT_NEED_ARG")); return; }

    if (eq(argv[1], "SCAN")) {
      if (!manualOutputAllowed()) { Serial.println(F("ERR OUT_BUSY")); return; }
      uint32_t ms = (argc >= 3) ? (uint32_t)constrain(atol(argv[2]), 200L, 20000L) : 2500UL;
      diagOutScan(ms);
      return;
    }

    if (eq(argv[1], "STATUS")) {
      const uint8_t pins[] = { PIN_LAMP_FAIL, PIN_LAMP_PASS, PIN_INTERLOCK,
                               PIN_SOL_FIXTURE, PIN_SOL_POGO, PIN_ILK_LOCK };
      Serial.print(F("OK OUT"));
      for (uint8_t i = 0; i < 6; i++) {
        Serial.print(F(" D")); Serial.print(pins[i]);
        Serial.print('=');     Serial.print(digitalRead(pins[i]) == HIGH ? 1 : 0);
      }
      Serial.println();
      return;
    }

    int pinNo = atoi(argv[1]);
    if (!diagPinAllowed((uint8_t)pinNo)) { Serial.println(F("ERR OUT_BAD_PIN")); return; }
    if (argc < 3)                        { Serial.println(F("ERR OUT_NEED_ON_OFF")); return; }
    if (!manualOutputAllowed())          { Serial.println(F("ERR OUT_BUSY")); return; }

    bool on = eq(argv[2], "ON");
    uint32_t ms = on ? ((argc >= 4) ? (uint32_t)constrain(atol(argv[3]), 0L, 20000L) : 0UL) : 0UL;
    diagOutPin((uint8_t)pinNo, on, ms);
    return;
  }

  /* ---------- output control ---------- */
  if (eq(argv[0], "RELAY")) {
    if (argc < 2) { Serial.println(F("ERR RELAY_NEED_ON_OFF")); return; }
    if (eq(argv[1], "ON")) {
      if (!ilkIsLocked())                   { Serial.println(F("ERR RELAY_INTERLOCK_OPEN")); return; }
#if USE_UNIT_SENSE
      if (!readActiveLow(PIN_POS_POGO_OUT)) { Serial.println(F("ERR RELAY_NO_UNIT"));       return; }
#endif
#if FIXTURE_MODE
      if (state != ST_TESTING)              { Serial.println(F("ERR RELAY_NOT_ARMED"));     return; }
#endif
      relayApply(true);
    } else if (eq(argv[1], "OFF")) {
      relayApply(false);
    } else { Serial.println(F("ERR RELAY_BAD_ARG")); return; }
    Serial.print(F("OK RELAY ")); Serial.println(onOff(relayIsOn()));
    return;
  }
  if (eq(argv[0], "BEACON")) {
    if (argc < 2) { Serial.println(F("ERR BEACON_NEED_ON_OFF")); return; }
    beaconSet(eq(argv[1], "ON"));
    Serial.print(F("OK BEACON ")); Serial.println(onOff(beaconOn));
    return;
  }
  if (eq(argv[0], "BEEP")) {
    uint16_t ms = (argc >= 2) ? (uint16_t)constrain(atol(argv[1]), 0, 10000) : 120;
    beep(ms);
    Serial.print(F("OK BEEP ")); Serial.println(ms);
    return;
  }
  if (eq(argv[0], "LAMP")) {
    if (argc < 2) { Serial.println(F("ERR LAMP_NEED_ARG")); return; }
    if      (eq(argv[1], "PASS")) lampPass();
    else if (eq(argv[1], "FAIL")) lampFail();
    else if (eq(argv[1], "OFF"))  lampsOff();
    else { Serial.println(F("ERR LAMP_BAD_ARG")); return; }
    Serial.print(F("OK LAMP ")); Serial.println(argv[1]);
    return;
  }

  /* ---------- fixture control ---------- */
  if (eq(argv[0], "FIX")) {
#if FIXTURE_MODE
    if (argc < 2) { Serial.println(F("ERR FIX_NEED_ARG")); return; }
    if      (eq(argv[1], "START")) { if (fixtureClose()) Serial.println(F("OK FIX CLOSING")); }
    else if (eq(argv[1], "OPEN"))  {
      beep(400);
      emitEvent("TEST_ABORTED");
      if (state == ST_IDLE) { allOff(NULL); fixPhase = FX_NONE; Serial.println(F("OK FIX IDLE")); }
      else                  { retract(FX_ABORTED); Serial.println(F("OK FIX OPENING")); }
    }
    else if (eq(argv[1], "STATUS")) { printFixStatus(); }
    else Serial.println(F("ERR FIX_BAD_ARG"));
#else
    Serial.println(F("ERR FIX_DISABLED"));
#endif
    return;
  }

  /* ---------- test life-cycle ---------- */
  if (eq(argv[0], "TEST")) {
    if (argc < 2) { Serial.println(F("ERR TEST_NEED_ARG")); return; }
    if      (eq(argv[1], "START")) requestStart();
    else if (eq(argv[1], "ABORT")) requestAbort();
    else Serial.println(F("ERR TEST_BAD_ARG"));
    return;
  }

  if (eq(argv[0], "RESULT")) {
    if (argc < 2) { Serial.println(F("ERR RESULT_NEED_ARG")); return; }
    if      (eq(argv[1], "PASS")) fixtureResult(true);
    else if (eq(argv[1], "FAIL")) fixtureResult(false);
    else { Serial.println(F("ERR RESULT_BAD_ARG")); return; }
    Serial.print(F("OK RESULT ")); Serial.println(argv[1]);
    return;
  }

  if (eq(argv[0], "EVENTS")) {
    if (argc < 2) { Serial.print(F("OK EVENTS ")); Serial.println(onOff(eventsEnabled)); return; }
    eventsEnabled = eq(argv[1], "ON");
    Serial.print(F("OK EVENTS ")); Serial.println(onOff(eventsEnabled));
    return;
  }

  if (eq(argv[0], "RESET")) {
    allOff(NULL);
    lampsOff();
    fixPhase = FX_NONE;
    cmdCount = 0;
    state = ST_IDLE;
    Serial.println(F("OK RESET"));
    return;
  }

  Serial.print(F("ERR UNKNOWN_CMD ")); Serial.println(argv[0]);
}

/* ------------------------------- serial read --------------------------------- */
static void pollSerial()
{
  while (Serial.available() > 0) {
    char c = (char)Serial.read();

    if (c == '\r') continue;
    if (c == '\n') {
      if (lineOverflow) { Serial.println(F("ERR LINE_TOO_LONG")); lineOverflow = false; lineLen = 0; return; }
      lineBuf[lineLen] = '\0';
      if (lineLen > 0) handleCommand(lineBuf);
      lineLen = 0;
      continue;
    }
    if (lineLen < (LINE_MAX - 1)) lineBuf[lineLen++] = c;
    else lineOverflow = true;
  }
}

/* --------------------- physical START / ABORT buttons ----------------------- */
static void pollButtons()
{
  uint32_t now = millis();

  bool startLevel = readButton(PIN_BTN_START);
  if (startLevel != lastStartLevel) { lastStartLevel = startLevel; startEdgeMs = now; }
  if (startLevel && startEdgeMs != 0 && (now - startEdgeMs) >= BTN_DEBOUNCE_MS) {
    startEdgeMs = 0;
    legacyStartTrigger();
  }

#if USE_ABORT_BTN
  bool abortLevel = readButton(PIN_BTN_ABORT);
  if (abortLevel != lastAbortLevel) { lastAbortLevel = abortLevel; abortEdgeMs = now; }
  if (abortLevel && abortEdgeMs != 0 && (now - abortEdgeMs) >= BTN_DEBOUNCE_MS) {
    abortEdgeMs = 0;
    legacyAbortTrigger();
  }
#endif
}

/* ------------------------- background safety watchdog ----------------------- */
static void safetyWatchdog()
{
  uint32_t now = millis();

  /* Ngõ ra "cho phép đo" (D8) bị tắt bất ngờ trong lúc đang phóng áp -> cắt ngay. */
  if (relayOn && !ilkIsLocked()) {
    beep(600);
    emitEvent("INTERLOCK_OPEN");
    if (state != ST_FAULT) retract(FX_ABORTED);
    Serial.println(F("*CANCEL#"));
  }
#if USE_UNIT_SENSE
  if (relayOn && !readActiveLow(PIN_POS_POGO_OUT)) {
    emitEvent("UNIT_LOST");
    if (state != ST_FAULT) retract(FX_ABORTED);
    Serial.println(F("*CANCEL#"));
  }
#endif

  /* Relay D4 đóng quá lâu -> cắt + mở liên động + rút về. */
  if (relayOn && (now - relaySinceMs) > RELAY_MAX_ON_MS) {
    beep(1000);
    emitEvent("RELAY_TIMEOUT");
    if (state != ST_FAULT) retract(FX_ABORTED);
    Serial.println(F("*CANCEL#"));
  }
}

static void heartbeat()
{
  if (!eventsEnabled) return;
  if (millis() - lastBeatMs < HEARTBEAT_MS) return;
  lastBeatMs = millis();
  emitEvent("HB");
}

/* ------------------------------- lifecycle --------------------------------- */
static void hardwareInit()
{
  /* INPUTS first, so nothing floats while we bring the outputs up. */
  pinMode(PIN_POS_POGO_OUT,   INPUT_PULLUP);
  pinMode(PIN_POS_FIX_CLOSED, INPUT_PULLUP);
  pinMode(PIN_BTN_START,      INPUT_PULLUP);
#if USE_ABORT_BTN
  pinMode(PIN_BTN_ABORT,      INPUT_PULLUP);
#endif

  /* Outputs are parked LOW before the pinMode, so the very first edge is already safe.
     A reset or power-up therefore NEVER moves a cylinder and NEVER locks the interlock. */
  digitalWrite(PIN_TEST_RELAY,  LOW);
  digitalWrite(PIN_BEACON,      LOW);
  digitalWrite(PIN_BUZZER,      LOW);
  digitalWrite(PIN_LAMP_PASS,   LOW);
  digitalWrite(PIN_LAMP_FAIL,   LOW);
  digitalWrite(PIN_INTERLOCK,   LOW);   /* D8 = "cho phép đo" - mặc định TẮT */
  digitalWrite(PIN_SOL_FIXTURE, LOW);
  digitalWrite(PIN_SOL_POGO,    LOW);
  digitalWrite(PIN_ILK_LOCK,    LOW);

  pinMode(PIN_TEST_RELAY,   OUTPUT);
  pinMode(PIN_BEACON,       OUTPUT);
  pinMode(PIN_BUZZER,       OUTPUT);
  pinMode(PIN_LAMP_PASS,    OUTPUT);
  pinMode(PIN_LAMP_FAIL,    OUTPUT);
  pinMode(PIN_INTERLOCK,    OUTPUT);
  pinMode(PIN_SOL_FIXTURE,  OUTPUT);
  pinMode(PIN_SOL_POGO,     OUTPUT);
  pinMode(PIN_ILK_LOCK,     OUTPUT);

  relayOn = false;
  beaconSet(false);
  lampsOff();
  valvesOff();
  ilkLock(false);
  state    = ST_IDLE;
  fixPhase = FX_NONE;
  diagCancel();

  lastStartLevel = readButton(PIN_BTN_START);
#if USE_ABORT_BTN
  lastAbortLevel = readButton(PIN_BTN_ABORT);
#else
  lastAbortLevel = false;
#endif
}

void setup()
{
  hardwareInit();
  Serial.begin(SERIAL_BAUD);
  while (!Serial) { ; }          /* Leonardo/USB; Uno returns immediately */
  Serial.print(F("OK BOOT "));
  Serial.print(F(FW_NAME));
  Serial.print(F(" FW="));
  Serial.print(F(FW_VERSION));
#if FIXTURE_MODE
  Serial.print(F(" FIXTURE=ON"));
#else
  Serial.print(F(" FIXTURE=OFF"));
#endif
  Serial.println();
}

void loop()
{
  pollSerial();
  pollButtons();
  diagTimers();
  fixtureStep();
  safetyWatchdog();
  heartbeat();

  if (beepUntilMs != 0 && (int32_t)(millis() - beepUntilMs) >= 0) {
    beepUntilMs = 0;
    outWrite(PIN_BUZZER, false, BUZZER_ACTIVE_HIGH);
  } else if (beepUntilMs != 0) {
    outWrite(PIN_BUZZER, true, BUZZER_ACTIVE_HIGH);
  }
}
