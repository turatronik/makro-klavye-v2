// MAKRO KLAVYE v2 (C++) - tuş simgeleri (2.7): 24x24, tek renk; araclar/simgeler.py üretir
#pragma once
#include <stdint.h>

struct Simge {
  const char *ad;
  const uint32_t *bit;     // 24 satır, her satırın üst 24 biti (soldan sağa)
};
extern const Simge SIMGELER[];
extern const int SIMGE_SAYISI;
// 3.1: "*ad" ile başlayan adlar kullanıcının çizdiği özel simgelerdir (simgedepo.h);
// numaraları OZEL_BAS + sıra olur, gömülü simgeler 0..SIMGE_SAYISI-1 arasında kalır.
const int OZEL_BAS = 1000;
int simge_bul(const char *ad);   // yoksa -1
