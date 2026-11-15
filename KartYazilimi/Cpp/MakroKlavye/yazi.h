// MAKRO KLAVYE v2 (C++) - bitmap yazı tipleri ve UTF-8
#pragma once
#include <stdint.h>
#include <stddef.h>

struct Harf {
  uint16_t kod;      // Unicode
  uint8_t w, h;      // bitmap boyutu
  int8_t xo, yo;     // BDF BBX ofseti (yo: taban çizgisinden yukarı değil, aşağıdan)
  uint8_t dx;        // ilerleme
  uint16_t ofs;      // bit dizisindeki yer (satır başına (w+7)/8 bayt)
};

struct YaziTipi {
  const Harf *harfler;
  uint16_t n;
  const uint8_t *bit;
  uint8_t asc, desc;   // adafruit_display_text'in kullandığı yükseklik ("Mj'" harflerinden)
  uint8_t bbh;         // FONTBOUNDINGBOX yüksekliği (satır aralığı bununla çarpılır)
};

extern const YaziTipi YT_baslik, YT_orta, YT_kucuk;

// UTF-8: s'den bir harf okur, s'yi ilerletir (bozuk bayt '?' sayılır)
uint32_t utf8_oku(const char *&s);
const Harf *harf_bul(const YaziTipi &f, uint32_t kod);
// metnin genişliği (piksel); n bayta kadar (n < 0 ise tamamı)
int metin_gen(const YaziTipi &f, const char *s, int n = -1);
// sığmazsa sonunu "…" ile kısaltır; out en az boyut kadar olmalı
void kisalt(const YaziTipi &f, const char *s, int genislik, char *out, size_t boyut);
// kelime kaydırma, en fazla 2 satır ("\n" ile); kısaltıldıysa true
bool iki_satir(const YaziTipi &f, const char *s, int genislik, char *out, size_t boyut);
// güvenli kopyalama (UTF-8 harfini yarıda kesmez)
void kopyala(char *out, const char *s, size_t boyut);
