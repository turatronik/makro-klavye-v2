// MAKRO KLAVYE v2 (C++) - mod deposu (flash 896K-1020K)
#include "depo.h"
#include "komutlar.h"
#include <string.h>

static const uint32_t IMZA = 0x324D4B4D;       // "MKM2"
struct DepoBaslik { uint32_t imza, boy, crc; };

uint32_t crc32_devam(uint32_t c, const void *veri, size_t n) {
  const uint8_t *p = (const uint8_t *)veri;
  for (size_t i = 0; i < n; i++) {
    c ^= p[i];
    for (int b = 0; b < 8; b++) c = (c >> 1) ^ (0xEDB88320u & (0u - (c & 1)));
  }
  return c;
}

uint32_t crc32_hesapla(const void *veri, size_t n) {
  return ~crc32_devam(0xFFFFFFFF, veri, n);
}

#ifdef ARDUINO
#include <Arduino.h>
#include <hardware/flash.h>

static const uint32_t DEPO_FLASH = 896 * 1024;
static const uint32_t DEPO_BOYUT = 124 * 1024;
static_assert(DEPO_FLASH % FLASH_SECTOR_SIZE == 0, "sektör hizası");
static_assert(sizeof(DepoBaslik) + MODLAR_EN_COK <= DEPO_BOYUT, "depo küçük");

static const uint8_t *taban() { return (const uint8_t *)(XIP_BASE + DEPO_FLASH); }

const char *depo_oku(int &n) {
  DepoBaslik b;
  memcpy(&b, taban(), sizeof(b));
  if (b.imza != IMZA || b.boy == 0 || b.boy > MODLAR_EN_COK) return nullptr;
  const char *veri = (const char *)(taban() + sizeof(DepoBaslik));
  if (crc32_hesapla(veri, b.boy) != b.crc) return nullptr;
  n = b.boy;
  return veri;
}

// 4 KB'lık parçalarla, her parçada kesmeleri en çok ~50 ms kapatarak siler ve yazar
// (flash'a yazarken çipten kod çalıştırılamaz: kesmeler ve ikinci çekirdek o an durdurulur)
static void yaz_parca(uint32_t ofs, const uint8_t *veri) {
  noInterrupts();
  rp2040.idleOtherCore();
  flash_range_erase(DEPO_FLASH + ofs, FLASH_SECTOR_SIZE);
  flash_range_program(DEPO_FLASH + ofs, veri, FLASH_SECTOR_SIZE);
  rp2040.resumeOtherCore();
  interrupts();
}

bool depo_yaz(const char *veri, size_t n) {
  if (n == 0 || n > MODLAR_EN_COK) return false;
  DepoBaslik b = {IMZA, (uint32_t)n, crc32_hesapla(veri, n)};
  size_t toplam = sizeof(b) + n;
  static uint8_t parca[FLASH_SECTOR_SIZE];
  for (size_t ofs = 0; ofs < toplam; ofs += FLASH_SECTOR_SIZE) {
    memset(parca, 0xFF, sizeof(parca));
    for (size_t i = 0; i < FLASH_SECTOR_SIZE && ofs + i < toplam; i++)
      parca[i] = ofs + i < sizeof(b) ? ((const uint8_t *)&b)[ofs + i] : (uint8_t)veri[ofs + i - sizeof(b)];
    yaz_parca(ofs, parca);
  }
  int dn = 0;
  const char *d = depo_oku(dn);
  return d && dn == (int)n && memcmp(d, veri, n) == 0;    // geri okuyarak doğrula
}

void depo_sil() {
  if (((const DepoBaslik *)taban())->imza == IMZA) {
    static uint8_t bos[FLASH_SECTOR_SIZE];
    memset(bos, 0xFF, sizeof(bos));
    yaz_parca(0, bos);
  }
}

#else
// bilgisayarda deneme: depo.bin dosyası
#include <stdio.h>
#include <stdlib.h>

static uint8_t icerik[sizeof(DepoBaslik) + MODLAR_EN_COK];
static bool okundu = false;

static void yukle() {
  if (okundu) return;
  okundu = true;
  memset(icerik, 0xFF, sizeof(icerik));
  FILE *f = fopen("depo.bin", "rb");
  if (f) { fread(icerik, 1, sizeof(icerik), f); fclose(f); }
}

const char *depo_oku(int &n) {
  yukle();
  DepoBaslik b;
  memcpy(&b, icerik, sizeof(b));
  if (b.imza != IMZA || b.boy == 0 || b.boy > MODLAR_EN_COK) return nullptr;
  const char *veri = (const char *)(icerik + sizeof(DepoBaslik));
  if (crc32_hesapla(veri, b.boy) != b.crc) return nullptr;
  n = b.boy;
  return veri;
}

bool depo_yaz(const char *veri, size_t n) {
  yukle();
  if (n == 0 || n > MODLAR_EN_COK) return false;
  DepoBaslik b = {IMZA, (uint32_t)n, crc32_hesapla(veri, n)};
  memcpy(icerik, &b, sizeof(b));
  memcpy(icerik + sizeof(b), veri, n);
  FILE *f = fopen("depo.bin", "wb");
  if (f) { fwrite(icerik, 1, sizeof(b) + n, f); fclose(f); }
  return true;
}

void depo_sil() {
  yukle();
  memset(icerik, 0xFF, sizeof(DepoBaslik));
  remove("depo.bin");
}
#endif
