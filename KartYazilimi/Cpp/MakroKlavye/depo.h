// MAKRO KLAVYE v2 (C++) - mod deposu (2.7)
// Modların JSON'u artık 4 KB'lık kalıcı bellek yerine flash'ta 124 KB'lık ayrı bir bölgede durur
// (896K-1020K; program ~130K'da bittiği, CircuitPython'un eski dosya sistemi de 1024K'da başladığı
// için ikisine de dokunmaz). Başlık: imza + uzunluk + CRC32; bozuksa yok sayılır.
#pragma once
#include <stdint.h>
#include <stddef.h>

// kayıtlı mod metni (0 ile bitmez); yoksa/bozuksa nullptr
const char *depo_oku(int &n);
bool depo_yaz(const char *veri, size_t n);
void depo_sil();
uint32_t crc32_hesapla(const void *veri, size_t n);
uint32_t crc32_devam(uint32_t c, const void *veri, size_t n);   // parça parça CRC (0xFFFFFFFF ile başlar)
