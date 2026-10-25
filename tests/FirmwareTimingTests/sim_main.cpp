/* =====================================================================================
 *  sim_main.cpp  -  Mô phỏng firmware GPT9800_ControlBoard trên PC
 *
 *  Nhúng THẲNG file .ino (không copy logic) rồi chạy với:
 *    - đồng hồ ảo (millis/delay)
 *    - chân GPIO ảo: ghi lại mọi sườn lên/xuống + mốc thời gian
 *    - Serial ảo: bơm lệnh vào, thu toàn bộ phản hồi
 *
 *  Kiểm tra đúng thứ tự an toàn:
 *      D10 clamp đóng  ->  D11 pogo đẩy  ->  D13 khóa interlock  ->  D4 relay HV
 *  và khi nhả:
 *      D4 tắt  ->  D13 mở  ->  D11 rút  ->  (POGO_BACK_MS)  ->  D10 mở clamp
 *
 *  Build + chạy:  powershell -File arduino\sim\run-sim.ps1
 * ===================================================================================== */

#include "Arduino.h"

#include <vector>
#include <string>
#include <algorithm>

/* ============================ đồng hồ ảo ============================ */
static uint32_t g_now = 0;
uint32_t millis() { return g_now; }
void     delay(uint32_t ms) { g_now += ms; }

/* ============================== GPIO ảo ============================= */
struct PinInfo { int mode; int value; };
static PinInfo g_pin[32];

struct Edge { uint32_t t; int pin; int value; };
static std::vector<Edge> g_edges;

static bool g_btnStart = false;    /* true = đang bấm nút START (kéo xuống GND) */

void pinMode(uint8_t p, uint8_t m)
{
  if (p < 32) g_pin[p].mode = m;
}

void digitalWrite(uint8_t p, uint8_t v)
{
  if (p >= 32) return;
  v = (v == HIGH) ? 1 : 0;
  if (g_pin[p].value != v) {
    g_pin[p].value = v;
    Edge e{ g_now, (int)p, v };
    g_edges.push_back(e);
  }
}

int digitalRead(uint8_t p)
{
  if (p == 7)  return g_btnStart ? LOW : HIGH;   /* D7 = nút START, pull-up ngoài */
  if (p == A4) return HIGH;                      /* A4 = nút ABORT (không bấm)  */
  if (p >= 32) return LOW;
  if (g_pin[p].mode == INPUT_PULLUP && g_pin[p].value == 0) return HIGH;
  return g_pin[p].value;
}

int analogRead(uint8_t p)
{
  /* giá trị giả lập ổn định, chỉ để firmware có số mà in ra */
  switch (p) {
    case A0: return 398;   /* ACW  */
    case A1: return 330;   /* Leak */
    case A2: return 281;   /* IR   */
    case A3: return 274;   /* GB   */
    default: return 0;
  }
}

/* ============================= Serial ảo ============================ */
static std::deque<char> g_rx;
static std::string      g_tx;

int SimSerial::available() { return (int)g_rx.size(); }
int SimSerial::read()      { if (g_rx.empty()) return -1; char c = g_rx.front(); g_rx.pop_front(); return (unsigned char)c; }
int SimSerial::peek()      { if (g_rx.empty()) return -1; return (unsigned char)g_rx.front(); }
void SimSerial::write(uint8_t b) { g_tx.push_back((char)b); }

size_t SimSerial::print(const char* s) { if (s) g_tx += s; return s ? strlen(s) : 0; }
size_t SimSerial::print(char c)         { g_tx.push_back(c); return 1; }
size_t SimSerial::print(int v)          { char b[32]; snprintf(b, sizeof(b), "%d", v);  g_tx += b; return strlen(b); }
size_t SimSerial::print(unsigned int v) { char b[32]; snprintf(b, sizeof(b), "%u", v);  g_tx += b; return strlen(b); }
size_t SimSerial::print(long v)         { char b[32]; snprintf(b, sizeof(b), "%ld", v); g_tx += b; return strlen(b); }
size_t SimSerial::print(unsigned long v){ char b[32]; snprintf(b, sizeof(b), "%lu", v); g_tx += b; return strlen(b); }
size_t SimSerial::print(double v)       { char b[32]; snprintf(b, sizeof(b), "%.2f", v); g_tx += b; return strlen(b); }

size_t SimSerial::println() { g_tx += "\r\n"; return 2; }
size_t SimSerial::println(const char* s) { size_t n = print(s); n += println(); return n; }
size_t SimSerial::println(char c)        { size_t n = print(c); n += println(); return n; }
size_t SimSerial::println(int v)         { size_t n = print(v); n += println(); return n; }
size_t SimSerial::println(unsigned int v){ size_t n = print(v); n += println(); return n; }
size_t SimSerial::println(long v)        { size_t n = print(v); n += println(); return n; }
size_t SimSerial::println(unsigned long v){ size_t n = print(v); n += println(); return n; }
size_t SimSerial::println(double v)      { size_t n = print(v); n += println(); return n; }

SimSerial Serial;

/* ==================== nhúng firmware THẬT ==================== */
#include "../../firmware/GPT9800_ControlBoard/GPT9800_ControlBoard.ino"

/* ========================== khung test ========================== */
static int  g_checks = 0;
static int  g_fails  = 0;
static const char* g_scenario = "";

static void check(bool ok, const char* what)
{
  g_checks++;
  if (!ok) {
    g_fails++;
    printf("    [FAIL] %s\n", what);
  }
}

static void scenario(const char* name)
{
  g_scenario = name;
  printf("\n== %s ==\n", name);
}

static void runFor(uint32_t ms);

static void resetBoard()
{
  g_rx.clear();
  g_tx.clear();
  g_edges.clear();
  g_btnStart = false;
  for (int i = 0; i < 32; i++) { g_pin[i].mode = INPUT; g_pin[i].value = 0; }

  hardwareInit();          /* đúng những gì setup() gọi */
  eventsEnabled = true;    /* bật EVT ... để test bắt được sự kiện */
  g_now += 5;
  runFor(5);
  g_tx.clear();
  g_edges.clear();
}

static void runFor(uint32_t ms)
{
  uint32_t start = g_now;
  while ((uint32_t)(g_now - start) < ms) {
    loop();
    g_now += 1;
  }
}

static void sendCmd(const std::string& cmd)
{
  for (char c : cmd) g_rx.push_back(c);
  g_rx.push_back('\n');
  runFor(3);
}

static bool txHas(const char* needle) { return g_tx.find(needle) != std::string::npos; }

static int  pinState(int p) { return g_pin[p].value; }

/* mốc thời gian của sườn CUỐI CÙNG có (pin, value) */
static uint32_t lastEdge(int p, int v)
{
  for (size_t i = g_edges.size(); i-- > 0; )
    if (g_edges[i].pin == p && g_edges[i].value == v) return g_edges[i].t;
  return 0xFFFFFFFFu;
}

/* mốc thời gian của sườn ĐẦU TIÊN sau thời điểm after */
static uint32_t firstEdgeAfter(int p, int v, uint32_t after)
{
  for (size_t i = 0; i < g_edges.size(); i++)
    if (g_edges[i].pin == p && g_edges[i].value == v && g_edges[i].t >= after) return g_edges[i].t;
  return 0xFFFFFFFFu;
}

/* ============================ kịch bản ============================ */

/* 1. Sau khi khởi động: mọi thứ OFF. */
static void test_boot()
{
  scenario("1. Khoi dong - moi output OFF");
  resetBoard();

  check(state == ST_IDLE, "state = IDLE");
  check(pinState(PIN_SOL_FIXTURE) == 0, "D10 clamp OFF khi khoi dong");
  check(pinState(PIN_SOL_POGO) == 0,    "D11 pogo OFF khi khoi dong");
  check(pinState(PIN_INTERLOCK) == 0,   "D8 CHO PHEP DO OFF khi khoi dong");
  check(pinState(PIN_ILK_LOCK) == 0,    "D13 tiep diem DB-9 OFF khi khoi dong");
  check(pinState(PIN_TEST_RELAY) == 0,  "D4 relay HV OFF khi khoi dong");
  check(pinState(PIN_LAMP_PASS) == 0 && pinState(PIN_LAMP_FAIL) == 0, "2 den tat");
}

/* 2. Chu trình đầy đủ PASS - kiểm tra đúng thứ tự. */
static void test_full_pass_cycle()
{
  scenario("2. Chu trinh PASS - thu tu clamp -> pogo -> interlock -> HV");
  resetBoard();

  sendCmd("FIX START");

  check(state == ST_FIX_CLOSING, "vao CLOSING");
  check(pinState(PIN_SOL_FIXTURE) == HIGH, "D10 clamp BAT ngay");
  check(pinState(PIN_SOL_POGO) == LOW,    "D11 pogo CHUA bat");
  check(pinState(PIN_INTERLOCK) == LOW,   "D8 cho phep do CHUA bat");
  check(pinState(PIN_ILK_LOCK) == LOW,    "D13 tiep diem DB-9 CHUA dong");
  check(pinState(PIN_TEST_RELAY) == LOW,  "D4 relay CHUA dong");

  /* chờ hết hành trình clamp */
  runFor(FIX_CLOSING_MS + 20);
  check(state == ST_POGO_OUT, "sau FIX_CLOSING_MS -> POGO_OUT");
  check(pinState(PIN_SOL_FIXTURE) == HIGH, "D10 van clamp van giu");
  check(pinState(PIN_SOL_POGO) == HIGH,    "D11 pogo BAT");
  check(pinState(PIN_INTERLOCK) == LOW,    "D8 van CHUA cho phep do");

  /* chờ pogo chạm */
  runFor(POGO_OUT_MS + 20);
  check(state == ST_ARMED, "sau POGO_OUT_MS -> ARMED");
  check(pinState(PIN_INTERLOCK) == HIGH,  "D8 CHO PHEP DO bat");
  check(pinState(PIN_ILK_LOCK) == HIGH,   "D13 tiep diem DB-9 dong");
  check(pinState(PIN_TEST_RELAY) == LOW,  "D4 relay VAN CHUA dong khi chua co HV ARM");
  check(txHas("*TESTON#"), "gui *TESTON# cho PC");
  check(txHas("HV_ARMED"), "evt HV_ARMED");

  /* PC xác nhận */
  sendCmd("HV ARM");
  check(state == ST_TESTING, "HV ARM -> TESTING");
  check(pinState(PIN_TEST_RELAY) == HIGH, "D4 relay HV DONG");
  check(txHas("OK HV ARMED RELAY=ON"), "tra loi OK HV ARMED");

  /* kết quả */
  uint32_t tRes = g_now;
  sendCmd("RESULT PASS");

  check(state == ST_POGO_BACK, "RESULT -> POGO_BACK");
  check(pinState(PIN_TEST_RELAY) == LOW, "D4 relay TAT truoc tien");
  check(pinState(PIN_INTERLOCK) == LOW,  "D8 cho phep do TAT");
  check(pinState(PIN_ILK_LOCK) == LOW,   "D13 tiep diem DB-9 MO");
  check(pinState(PIN_SOL_POGO) == LOW,   "D11 pogo RUT");
  check(pinState(PIN_SOL_FIXTURE) == HIGH, "D10 clamp VAN GIU (chua mo)");
  check(pinState(PIN_LAMP_PASS) == HIGH, "den PASS (D9) sang");
  check(pinState(PIN_LAMP_FAIL) == LOW,  "den FAIL (D2) tat");

  uint32_t tRelayOff = firstEdgeAfter(PIN_TEST_RELAY, LOW, tRes);
  uint32_t tIlkOff   = firstEdgeAfter(PIN_INTERLOCK,  LOW, tRes);
  uint32_t tPogoOff  = firstEdgeAfter(PIN_SOL_POGO,   LOW, tRes);
  check(tRelayOff <= tIlkOff, "thu tu: relay D4 tat TRUOC cho-phep-do D8");
  check(tIlkOff   <= tPogoOff, "thu tu: cho-phep-do D8 tat TRUOC pogo D11");

  /* chờ pogo rút về rồi mới mở clamp */
  runFor(POGO_BACK_MS + 10);
  check(state == ST_FIX_OPEN, "sau POGO_BACK_MS -> FIX_OPEN");
  check(pinState(PIN_SOL_FIXTURE) == LOW, "D10 clamp MO");
  uint32_t tFixOff = lastEdge(PIN_SOL_FIXTURE, LOW);
  check(tFixOff >= tPogoOff + POGO_BACK_MS, "clamp chi mo sau khi pogo da rut (POGO_BACK_MS)");

  runFor(FIX_OPEN_MS + 60);
  check(state == ST_IDLE, "ve IDLE");
  check(pinState(PIN_SOL_FIXTURE) == LOW && pinState(PIN_SOL_POGO) == LOW, "2 van OFF");
  check(pinState(PIN_INTERLOCK) == LOW && pinState(PIN_TEST_RELAY) == LOW, "cho-phep-do + relay OFF");
  check(fixPhase == FX_PASS, "ket qua ghi nhan PASS");
}

/* 3. Nút START vật lý (D7) - không cần PC. */
static void test_physical_start_button()
{
  scenario("3. Bam nut START vat ly tren D7");
  resetBoard();

  g_btnStart = true;
  runFor(BTN_DEBOUNCE_MS + 30);
  check(state == ST_FIX_CLOSING, "nut D7 bat dau chu trinh");
  check(pinState(PIN_SOL_FIXTURE) == HIGH, "D10 clamp BAT (khong phai pogo)");
  check(pinState(PIN_SOL_POGO) == LOW, "D11 pogo CHUA bat");

  g_btnStart = false;
  runFor(50);
  check(state == ST_FIX_CLOSING || state == ST_POGO_OUT, "giu nguyen chu trinh, khong lap lai");
}

/* 4. PC không gửi HV ARM -> tự nhả sau ARM_WAIT_MS. */
static void test_arm_timeout()
{
  scenario("4. Khong co HV ARM -> tu nha sau ARM_WAIT_MS");
  resetBoard();

  sendCmd("FIX START");
  runFor(FIX_CLOSING_MS + POGO_OUT_MS + 50);
  check(state == ST_ARMED, "dang ARMED");
  check(pinState(PIN_INTERLOCK) == HIGH, "D8 dang cho phep do");
  check(pinState(PIN_ILK_LOCK) == HIGH, "D13 tiep diem dang dong");

  runFor(ARM_WAIT_MS + 100);
  check(pinState(PIN_TEST_RELAY) == LOW, "relay HV khong bao gio dong");
  check(pinState(PIN_INTERLOCK) == LOW, "D8 da tat cho phep do");
  check(pinState(PIN_ILK_LOCK) == LOW, "D13 tiep diem da mo");
  check(pinState(PIN_SOL_POGO) == LOW, "pogo da rut");
  check(txHas("*CANCEL#"), "bao *CANCEL# cho PC");

  runFor(POGO_BACK_MS + FIX_OPEN_MS + 100);
  check(state == ST_IDLE, "tu dong ve IDLE (khong ket fixture)");
  check(pinState(PIN_SOL_FIXTURE) == LOW, "clamp da mo");
  check(fixPhase == FX_ABORTED, "ket qua = ABORTED");
}

/* 5. ABORT giữa lúc đang phóng áp. */
static void test_abort_during_test()
{
  scenario("5. TEST ABORT khi dang phong ap");
  resetBoard();

  sendCmd("FIX START");
  runFor(FIX_CLOSING_MS + POGO_OUT_MS + 50);
  sendCmd("HV ARM");
  check(pinState(PIN_TEST_RELAY) == HIGH, "relay dang dong");

  uint32_t tAbort = g_now;
  sendCmd("TEST ABORT");

  check(pinState(PIN_TEST_RELAY) == LOW, "relay TAT ngay");
  check(pinState(PIN_INTERLOCK) == LOW,  "D8 cho phep do TAT ngay");
  check(pinState(PIN_ILK_LOCK) == LOW,   "D13 tiep diem MO ngay");
  check(pinState(PIN_SOL_POGO) == LOW,   "pogo RUT ngay");
  check(pinState(PIN_SOL_FIXTURE) == HIGH, "clamp con giu trong luc pogo rut");
  check(txHas("TEST_ABORTED"), "evt TEST_ABORTED");

  uint32_t tRelayOff = firstEdgeAfter(PIN_TEST_RELAY, LOW, tAbort);
  uint32_t tIlkOff   = firstEdgeAfter(PIN_INTERLOCK,  LOW, tAbort);
  uint32_t tPogoOff  = firstEdgeAfter(PIN_SOL_POGO,   LOW, tAbort);
  check(tRelayOff <= tIlkOff && tIlkOff <= tPogoOff, "dung thu tu nha an toan");

  runFor(POGO_BACK_MS + FIX_OPEN_MS + 100);
  check(state == ST_IDLE, "ve IDLE");
}

/* 6. HV ARM khi chưa ARMED -> phải từ chối. */
static void test_hv_arm_rejected()
{
  scenario("6. HV ARM khi IDLE -> tu choi");
  resetBoard();

  sendCmd("HV ARM");
  check(pinState(PIN_TEST_RELAY) == LOW, "relay KHONG dong");
  check(txHas("ERR HV_NOT_ARMED"), "tra loi ERR HV_NOT_ARMED");
  check(state == ST_IDLE, "van IDLE");
}

/* 7. Watchdog relay 30 s. */
static void test_relay_watchdog()
{
  scenario("7. Watchdog relay: tu cat sau RELAY_MAX_ON_MS");
  resetBoard();

  sendCmd("FIX START");
  runFor(FIX_CLOSING_MS + POGO_OUT_MS + 50);
  sendCmd("HV ARM");
  check(pinState(PIN_TEST_RELAY) == HIGH, "relay dong");

  runFor(RELAY_MAX_ON_MS + 200);
  check(pinState(PIN_TEST_RELAY) == LOW, "relay tu cat");
  check(txHas("RELAY_TIMEOUT"), "evt RELAY_TIMEOUT");
  check(pinState(PIN_INTERLOCK) == LOW, "D8 cho phep do tat");
  check(pinState(PIN_ILK_LOCK) == LOW, "D13 tiep diem mo");

  runFor(POGO_BACK_MS + FIX_OPEN_MS + 100);
  check(state == ST_IDLE, "ve IDLE");
}

/* 8. Lệnh chẩn đoán OUT / VALVE / ILK. */
static void test_diag_commands()
{
  scenario("8. Lenh chan doan OUT / VALVE / ILK");
  resetBoard();

  sendCmd("OUT 10 ON 300");      /* hàm này delay() bên trong firmware */
  check(txHas("OK OUT D10 ON"), "OUT 10 ON tra loi OK");
  check(txHas("OFF (auto 300 ms)"), "OUT 10 tu tat sau 300 ms");
  check(pinState(PIN_SOL_FIXTURE) == LOW, "D10 da tat lai");

  uint32_t tOut = g_now;
  sendCmd("OUT 11 ON 200");
  check(pinState(PIN_SOL_POGO) == LOW, "D11 da tat lai");
  uint32_t tOn  = firstEdgeAfter(PIN_SOL_POGO, HIGH, tOut);
  uint32_t tOff = firstEdgeAfter(PIN_SOL_POGO, LOW,  tOut);
  check(tOn != 0xFFFFFFFFu && tOff != 0xFFFFFFFFu, "D11 co bat roi tat");
  check(tOff - tOn >= 190 && tOff - tOn <= 260, "D11 bat dung ~200 ms");

  sendCmd("VALVE FIXTURE ON 200");
  check(txHas("OK VALVE FIXTURE ON AUTO_OFF_MS=200"), "VALVE FIXTURE ON 200 OK");
  runFor(260);                   /* hẹn giờ tự tắt là non-blocking */
  check(pinState(PIN_SOL_FIXTURE) == LOW, "clamp da tat lai");

  sendCmd("ILK ON 200");
  check(txHas("OK ILK ON"), "ILK ON 200 OK");
  check(pinState(PIN_INTERLOCK) == HIGH, "D8 cho phep do BAT");
  check(pinState(PIN_ILK_LOCK) == HIGH, "D13 tiep diem DONG");
  runFor(260);
  check(pinState(PIN_INTERLOCK) == LOW, "D8 cho phep do da tat lai");
  check(pinState(PIN_ILK_LOCK) == LOW, "D13 tiep diem da mo lai");

  sendCmd("OUT 13 ON 100");
  check(txHas("OK OUT D13 ON"), "OUT 13 ON OK");
  check(pinState(PIN_ILK_LOCK) == LOW, "D13 da tat lai");
}

/* 9. OUT bị chặn khi đang phóng áp. */
static void test_diag_blocked_while_testing()
{
  scenario("9. OUT / VALVE bi chan khi dang TEST");
  resetBoard();

  sendCmd("FIX START");
  runFor(FIX_CLOSING_MS + POGO_OUT_MS + 50);
  sendCmd("HV ARM");
  check(pinState(PIN_TEST_RELAY) == HIGH, "relay dong");

  sendCmd("OUT 10 ON 500");
  check(txHas("ERR OUT_BUSY"), "OUT bi tu choi");
  check(pinState(PIN_SOL_FIXTURE) == HIGH, "clamp van giu, khong bi doi trang thai");

  sendCmd("VALVE POGO OFF");
  check(txHas("ERR VALVE_BUSY"), "VALVE bi tu choi khi dang TEST");
  check(pinState(PIN_SOL_POGO) == HIGH, "pogo van giu");
}

/* 10. Hai chu trình liên tiếp - trạng thái phải sạch. */
static void test_two_cycles()
{
  scenario("10. Hai chu trinh lien tiep");
  resetBoard();

  for (int i = 0; i < 2; i++) {
    sendCmd("FIX START");
    runFor(FIX_CLOSING_MS + POGO_OUT_MS + 50);
    check(state == ST_ARMED, "chu trinh vao ARMED");
    sendCmd("HV ARM");
    sendCmd("RESULT PASS");
    runFor(POGO_BACK_MS + FIX_OPEN_MS + 100);
    check(state == ST_IDLE, "ve IDLE sau chu trinh");
    check(pinState(PIN_SOL_FIXTURE) == LOW && pinState(PIN_SOL_POGO) == LOW, "2 van OFF");
  }
}

/* 11. RESET giữa chu trình. */
static void test_reset_mid_cycle()
{
  scenario("11. RESET giua chu trinh");
  resetBoard();

  sendCmd("FIX START");
  runFor(FIX_CLOSING_MS + POGO_OUT_MS + 50);
  sendCmd("HV ARM");
  check(pinState(PIN_TEST_RELAY) == HIGH, "relay dong");

  sendCmd("RESET");
  check(pinState(PIN_TEST_RELAY) == LOW, "relay tat");
  check(pinState(PIN_INTERLOCK) == LOW,  "D8 cho phep do tat");
  check(pinState(PIN_ILK_LOCK) == LOW,   "D13 tiep diem mo");
  check(pinState(PIN_SOL_FIXTURE) == LOW && pinState(PIN_SOL_POGO) == LOW, "2 van tat");
  check(state == ST_IDLE, "ve IDLE ngay");
}

static void test_configurable_timing()
{
  scenario("12. Cai thoi gian - kiem tra du lieu, cap nhat nguyen bo va dung hanh trinh");
  resetBoard();
  sendCmd("TIMING?");
  check(txHas("OK TIMING V=1 CLOSE=2500 OUT=1200 BACK=1200 OPEN=2500 ARM=30000"), "doc gia tri mac dinh");
  sendCmd("TIMING SET 3500 600 900 1800 2000");
  check(txHas("OK TIMING V=1 CLOSE=3500 OUT=600 BACK=900 OPEN=1800 ARM=2000"), "xac nhan ca bo gia tri moi");
  check(g_edges.empty(), "cau hinh khong bat van, lien dong hoac relay");
  const char* invalid[] = {
    "TIMING SET 0 600 900 1800 2000", "TIMING SET 7501 600 900 1800 2000",
    "TIMING SET 3500 600 900 1800 999", "TIMING SET 3500 600 900 1800 30001",
    "TIMING SET 3500 -1 900 1800 2000", "TIMING SET 3500 6x0 900 1800 2000",
    "TIMING SET 3500 600 900 1800 99999999999999999999",
    "TIMING SET 3500 600 900 1800", "TIMING SET 3500 600 900 1800 2000 extra"
  };
  for (const char* cmd : invalid) {
    g_tx.clear(); sendCmd(cmd);
    check(txHas("ERR TIMING_"), "du lieu sai bi tu choi");
    check(FIX_CLOSING_MS == 3500 && POGO_OUT_MS == 600 && POGO_BACK_MS == 900 &&
          FIX_OPEN_MS == 1800 && ARM_WAIT_MS == 2000, "loi khong thay doi mot phan cau hinh");
  }
  sendCmd("RESET");
  check(FIX_CLOSING_MS == 3500, "RESET giu thoi gian da cai trong phien");
  sendCmd("FIX START");
  uint32_t closedAt = strokeSinceMs;
  g_tx.clear(); sendCmd("TIMING SET 2500 1200 1200 2500 30000");
  check(txHas("ERR TIMING_BUSY"), "khong doi thoi gian khi clamp dang dong");
  while ((uint32_t)(g_now - closedAt) < 3500) runFor(1);
  check(state == ST_FIX_CLOSING && pinState(PIN_SOL_POGO) == LOW, "pogo chua ra truoc moc da cai");
  runFor(1);
  check(state == ST_POGO_OUT && lastEdge(PIN_SOL_POGO, HIGH) - closedAt == 3500, "pogo ra dung 3500 ms");
  uint32_t pogoAt = strokeSinceMs;
  g_tx.clear(); sendCmd("TIMING SET 2500 1200 1200 2500 30000");
  check(txHas("ERR TIMING_BUSY"), "khong doi thoi gian khi pogo dang ra");
  while ((uint32_t)(g_now - pogoAt) < 600) runFor(1);
  check(state == ST_POGO_OUT && pinState(PIN_INTERLOCK) == LOW, "chua cho phep do truoc khi pogo di du thoi gian");
  runFor(1);
  check(state == ST_ARMED && lastEdge(PIN_INTERLOCK, HIGH) - pogoAt == 600, "cho phep do dung 600 ms");
  g_tx.clear(); sendCmd("TIMING SET 2500 1200 1200 2500 30000");
  check(txHas("ERR TIMING_BUSY"), "khong doi thoi gian khi dang cho HV ARM");
  sendCmd("HV ARM");
  g_tx.clear(); sendCmd("TIMING SET 2500 1200 1200 2500 30000");
  check(txHas("ERR TIMING_BUSY"), "khong doi thoi gian khi relay dang bat");
  sendCmd("RESULT PASS");
  uint32_t backAt = strokeSinceMs;
  check(pinState(PIN_TEST_RELAY) == LOW && pinState(PIN_INTERLOCK) == LOW, "cat relay va lien dong truoc khi rut kim");
  g_tx.clear(); sendCmd("TIMING SET 2500 1200 1200 2500 30000");
  check(txHas("ERR TIMING_BUSY"), "khong doi thoi gian khi kim dang rut");
  while ((uint32_t)(g_now - backAt) < 900) runFor(1);
  check(pinState(PIN_SOL_FIXTURE) == HIGH, "giu clamp cho den het thoi gian rut kim");
  runFor(1);
  check(state == ST_FIX_OPEN && lastEdge(PIN_SOL_FIXTURE, LOW) - backAt == 900, "chi mo clamp sau 900 ms");
  uint32_t openAt = strokeSinceMs;
  g_tx.clear(); sendCmd("TIMING SET 2500 1200 1200 2500 30000");
  check(txHas("ERR TIMING_BUSY"), "khong doi thoi gian khi clamp dang mo");
  while ((uint32_t)(g_now - openAt) < 1800) runFor(1);
  check(state == ST_FIX_OPEN, "chua san sang truoc 1800 ms");
  runFor(1);
  check(state == ST_IDLE, "san sang sau dung thoi gian mo clamp");
  sendCmd("FIX START"); runFor(FIX_CLOSING_MS + POGO_OUT_MS + 5);
  g_tx.clear(); runFor(ARM_WAIT_MS + 1);
  check(txHas("EVT ARM_TIMEOUT") && pinState(PIN_TEST_RELAY) == LOW && state == ST_POGO_BACK,
        "thoi gian cho PC moi van tu dong cat va rut ve");
  runFor(POGO_BACK_MS + FIX_OPEN_MS + 5);
  sendCmd("VALVE FIXTURE ON 200");
  g_tx.clear(); sendCmd("TIMING SET 2500 1200 1200 2500 30000");
  check(txHas("ERR TIMING_BUSY"), "khong doi thoi gian trong khi thu van thu cong");
  runFor(210);
  sendCmd("TIMING SET 100 100 100 100 1000");
  check(FIX_CLOSING_MS == 100 && ARM_WAIT_MS == 1000, "nhan gia tri nho nhat hop le");
  sendCmd("TIMING SET 7500 7500 7500 7500 30000");
  check(FIX_OPEN_MS == 7500 && FIX_MAX_STROKE_MS == 8000 && RELAY_MAX_ON_MS == 30000,
        "nhan gioi han tren nhung khong tang watchdog an toan");
  sendCmd("TIMING SET 2500 1200 1200 2500 30000");
}

/* ============================== main ============================== */
int main(int argc, char** argv)
{
  if (argc == 2 && strcmp(argv[1], "--protocol") == 0) {
    setup(); g_tx.clear();
    printf("SIM READY\n"); fflush(stdout);
    char input[256];
    while (fgets(input, sizeof(input), stdin)) {
      input[strcspn(input, "\r\n")] = 0;
      sendCmd(input);
      /* Windows stdout adds CR before LF; avoid emitting CR CR LF. */
      for (char c : g_tx) if (c != '\r') putchar(c);
      fflush(stdout); g_tx.clear();
    }
    return 0;
  }
  printf("=== MO PHONG FIRMWARE %s (%s) ===\n", FW_VERSION, FW_NAME);
  printf("FIX_CLOSING_MS=%lu POGO_OUT_MS=%lu POGO_BACK_MS=%lu FIX_OPEN_MS=%lu\n",
         (unsigned long)FIX_CLOSING_MS, (unsigned long)POGO_OUT_MS,
         (unsigned long)POGO_BACK_MS,  (unsigned long)FIX_OPEN_MS);

  setup();          /* in OK BOOT, khởi tạo hardware */
  g_tx.clear();
  g_edges.clear();
  g_now += 5;
  runFor(5);

  test_boot();
  test_full_pass_cycle();
  test_physical_start_button();
  test_arm_timeout();
  test_abort_during_test();
  test_hv_arm_rejected();
  test_relay_watchdog();
  test_diag_commands();
  test_diag_blocked_while_testing();
  test_two_cycles();
  test_reset_mid_cycle();
  test_configurable_timing();

  printf("\n--------------------------------------------------\n");
  printf("TONG: %d phep kiem tra, %d loi\n", g_checks, g_fails);
  printf("%s\n", g_fails == 0 ? "KET QUA: PASS" : "KET QUA: FAIL");
  printf("--------------------------------------------------\n");
  return g_fails == 0 ? 0 : 1;
}
