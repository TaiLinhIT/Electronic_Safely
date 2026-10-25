/* =====================================================================================
 *  Arduino.h  -  Shim để biên dịch firmware GPT9800_ControlBoard.ino trên PC (MSVC)
 *
 *  Mục đích: chạy THẬT mã nguồn firmware (không viết lại logic) với:
 *    - đồng hồ ảo millis()/delay()
 *    - chân GPIO ảo (ghi lại mọi sườn lên/xuống kèm mốc thời gian)
 *    - Serial ảo (nhận lệnh từ kịch bản test, ghi lại toàn bộ phản hồi)
 *
 *  Chỉ dùng cho mô phỏng - KHÔNG nạp file này vào board.
 * ===================================================================================== */
#ifndef ARDUINO_H_SIM
#define ARDUINO_H_SIM

#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <deque>

#define HIGH 0x1
#define LOW  0x0

#define INPUT        0x0
#define OUTPUT       0x1
#define INPUT_PULLUP 0x2

/* Arduino Uno analog pin numbers (A0..A5) */
#define A0 14
#define A1 15
#define A2 16
#define A3 17
#define A4 18
#define A5 19

#define F(x)      (x)
#define PROGMEM
#define PSTR(x)   (x)
#define pgm_read_byte(a) (*(const uint8_t*)(a))

#define constrain(x, lo, hi) ((x) < (lo) ? (lo) : ((x) > (hi) ? (hi) : (x)))

/* MSVC không có strcasecmp (POSIX) */
#ifndef strcasecmp
#define strcasecmp _stricmp
#endif

typedef uint8_t byte;
typedef bool    boolean;

/* ------------------------------ thời gian ảo ------------------------------- */
uint32_t millis();
void     delay(uint32_t ms);

/* --------------------------------- GPIO ảo --------------------------------- */
void pinMode(uint8_t pin, uint8_t mode);
void digitalWrite(uint8_t pin, uint8_t value);
int  digitalRead(uint8_t pin);
int  analogRead(uint8_t pin);

/* -------------------------------- Serial ảo -------------------------------- */
class SimSerial {
public:
  void begin(unsigned long) {}
  void end() {}
  void flush() {}

  int available();
  int read();
  int peek();

  void write(uint8_t b);

  size_t print(const char* s);
  size_t print(char c);
  size_t print(int v);
  size_t print(unsigned int v);
  size_t print(long v);
  size_t print(unsigned long v);
  size_t print(double v);

  size_t println();
  size_t println(const char* s);
  size_t println(char c);
  size_t println(int v);
  size_t println(unsigned int v);
  size_t println(long v);
  size_t println(unsigned long v);
  size_t println(double v);

  operator bool() const { return true; }   /* while (!Serial) -> thoát ngay */
};

extern SimSerial Serial;

#endif /* ARDUINO_H_SIM */
