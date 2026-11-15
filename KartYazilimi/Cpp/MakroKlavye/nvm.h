// MAKRO KLAVYE v2 (C++) - kalıcı bellek (4 KB)
// CircuitPython'un microcontroller.nvm alanıyla AYNI yer (flash 0xFF000): modlar, ışık ve ekran ayarları
// yeni yazılıma geçince kaybolmaz; CircuitPython'a geri dönülürse de orada kalır.
// Düzen (CircuitPython sürümüyle aynı):
//   0: mod   1-16: ışık   17-32: tuş ışıkları   40-46: ekran ayarları   64-3967: modlar (JSON)   3968-4095: son hata
#pragma once
#include <stdint.h>
#include <stddef.h>

const int NVM_BOYUT = 4096;

void nvm_baslat();
uint8_t nvm_oku(int i);
const uint8_t *nvm_veri();
// değiştirir; flash'a hemen değil, kisa süre sonra (nvm_tik) toplu yazılır
void nvm_yaz(int i, const uint8_t *veri, int n);
void nvm_yaz1(int i, uint8_t v);
// bekleyen yazma varsa ve vakti geldiyse flash'a yazar (ana döngüden)
void nvm_tik(uint32_t ms);
// hemen yaz
void nvm_kaydet();
