// MAKRO KLAVYE v2 (C++) - ST7789 240x240 sürücüsü
#include "lcd.h"
#include <Arduino.h>
#include <SPI.h>

namespace {
const int P_SCK = 10, P_MOSI = 11, P_DC = 9, P_RST = 12, P_ISIK = 13;
const int ROWSTART = 80, COLSTART = 0;
SPISettings ayar(40000000, MSBFIRST, SPI_MODE3);   // CircuitPython: polarity=1 phase=1 (CS yok)
uint16_t satir[240];

void komut(uint8_t k, const uint8_t *veri = nullptr, int n = 0) {
  digitalWrite(P_DC, LOW);
  SPI1.transfer(k);
  if (n) {
    digitalWrite(P_DC, HIGH);
    for (int i = 0; i < n; i++) SPI1.transfer(veri[i]);
  }
}

void pencere(int x0, int y0, int x1, int y1) {
  uint8_t c[4] = {(uint8_t)(x0 >> 8), (uint8_t)x0, (uint8_t)(x1 >> 8), (uint8_t)x1};
  uint8_t r[4] = {(uint8_t)(y0 >> 8), (uint8_t)y0, (uint8_t)(y1 >> 8), (uint8_t)y1};
  komut(0x2A, c, 4);
  komut(0x2B, r, 4);
  komut(0x2C);
  digitalWrite(P_DC, HIGH);
}
}  // namespace

namespace lcd {

void baslat() {
  pinMode(P_DC, OUTPUT);
  pinMode(P_RST, OUTPUT);
  analogWriteFreq(50000);
  analogWriteRange(1000);
  analogWrite(P_ISIK, 0);
  SPI1.setSCK(P_SCK);
  SPI1.setTX(P_MOSI);
  SPI1.begin();
  SPI1.beginTransaction(ayar);         // ekran SPI'ı yalnız kullanır: işlem hiç bitmez
  digitalWrite(P_RST, HIGH);
  delay(5);
  digitalWrite(P_RST, LOW);
  delay(5);
  digitalWrite(P_RST, HIGH);
  delay(120);
  komut(0x01); delay(150);             // SWRESET
  komut(0x11); delay(500);             // SLPOUT
  uint8_t colmod = 0x55;
  komut(0x3A, &colmod, 1); delay(10);  // 16 bit renk
  uint8_t mad = 0x08;
  komut(0x36, &mad, 1);
  komut(0x13); delay(10);              // NORON
  komut(0x29); delay(50);              // DISPON
  mad = 0xC0;
  komut(0x36, &mad, 1);                // adafruit_st7789 bgr=True
  komut(0x21);                         // INVON (invert=True)
}

void parlaklik(float p) {
  if (p < 0) p = 0;
  if (p > 1) p = 1;
  analogWrite(P_ISIK, (int)(p * 1000));
}

void bant(const uint16_t *fb, int y0, int y1) {
  // mantıksal (x, y) -> ekranın kendi koordinatı (sütun = y, satır = 239 - x)
  int n = y1 - y0 + 1;
  pencere(COLSTART + y0, ROWSTART, COLSTART + y1, ROWSTART + 239);
  for (int ny = 0; ny < 240; ny++) {
    const uint16_t *p = fb + y0 * 240 + (239 - ny);
    for (int i = 0; i < n; i++) satir[i] = p[i * 240];
    SPI1.transfer(satir, nullptr, n * 2);
  }
}

}  // namespace lcd
