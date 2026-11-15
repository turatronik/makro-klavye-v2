// MAKRO KLAVYE v2 (C++) - özel (kullanıcının çizdiği) simgeler, 3.1
// Uygulamada çizilen 24x24 renkli simgeler flash'ta 512K-768K arasında durur (256 KB).
// Her kayıt sabit boyda olduğu için sıradan doğrudan okunur; RAM'de kopya tutulmaz,
// çizim doğrudan flash'tan (XIP) okunarak yapılır.
#pragma once
#include <stdint.h>
#include <stddef.h>

// bir simge: ad + 16 renklik palet (0. renk saydam) + 24x24 piksel (her piksel 4 bit)
const int OZEL_AD = 16;
const int OZEL_PALET = 16;
const int OZEL_PIKSEL = 24 * 24 / 2;         // 288 bayt
struct OzelSimge {
  char ad[OZEL_AD];                          // 0 ile biter
  uint16_t palet[OZEL_PALET];                // RGB565, ekrana gidecek bayt sırasında
  uint8_t px[OZEL_PIKSEL];                   // üst yarı bayt soldaki piksel
};
const int OZEL_EN_COK = 512;

int ozel_sayisi();                           // kayıtlı özel simge sayısı
const OzelSimge *ozel_simge(int i);          // flash'taki kayıt (yoksa nullptr)
int ozel_bul(const char *ad);                // ada göre sıra (yoksa -1)
void ozel_sil();

// Akış hâlinde yazma: simgeler tek tek gelir, RAM'de yalnızca bir sektör tutulur
// (512 simge = 172 KB; RAM'e sığmaz, bu yüzden geldikçe flash'a yazılır).
void ozel_yazim_basla();
bool ozel_yazim_ekle(const OzelSimge *s);
bool ozel_yazim_bitir();
