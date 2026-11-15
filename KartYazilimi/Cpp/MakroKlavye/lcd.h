// MAKRO KLAVYE v2 (C++) - ST7789 240x240 sürücüsü (SPI1: SCK GP10, MOSI GP11, DC GP9, RST GP12, ışık GP13)
// CircuitPython'daki adafruit_st7789 + displayio ile aynı ayarlar: MADCTL 0xC0, renk ters çevirme açık,
// rowstart 80, döndürme 270 (displayio'nun yaptığı gibi yazılımda döndürülür).
#pragma once
#include <stdint.h>

namespace lcd {
void baslat();
void parlaklik(float p);     // 0-1
// mantıksal (döndürülmüş) y0..y1 satırlarını gönderir; fb: 240x240, büyük uçlu RGB565
void bant(const uint16_t *fb, int y0, int y1);
}
