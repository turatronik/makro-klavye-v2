// MAKRO KLAVYE v2 (C++) - bitmap yazı tipleri ve UTF-8
#include "yazi.h"
#include <string.h>

uint32_t utf8_oku(const char *&s) {
  const uint8_t *p = (const uint8_t *)s;
  uint32_t c = p[0];
  int n = 0;
  if (c < 0x80) n = 0;
  else if ((c & 0xE0) == 0xC0) { c &= 0x1F; n = 1; }
  else if ((c & 0xF0) == 0xE0) { c &= 0x0F; n = 2; }
  else if ((c & 0xF8) == 0xF0) { c &= 0x07; n = 3; }
  else { s += 1; return '?'; }
  for (int i = 1; i <= n; i++) {
    if ((p[i] & 0xC0) != 0x80) { s += i; return '?'; }
    c = (c << 6) | (p[i] & 0x3F);
  }
  s += n + 1;
  return c;
}

const Harf *harf_bul(const YaziTipi &f, uint32_t kod) {
  int a = 0, b = f.n - 1;
  while (a <= b) {
    int m = (a + b) >> 1;
    uint16_t k = f.harfler[m].kod;
    if (k == kod) return &f.harfler[m];
    if (k < kod) a = m + 1; else b = m - 1;
  }
  return nullptr;
}

int metin_gen(const YaziTipi &f, const char *s, int n) {
  const char *son = n < 0 ? s + strlen(s) : s + n;
  int w = 0;
  while (s < son && *s && *s != '\n') {
    const Harf *h = harf_bul(f, utf8_oku(s));
    if (h) w += h->dx;
  }
  return w;
}

void kopyala(char *out, const char *s, size_t boyut) {
  if (!boyut) return;
  size_t n = strlen(s);
  if (n >= boyut) {
    n = boyut - 1;
    while (n > 0 && (((uint8_t)s[n]) & 0xC0) == 0x80) n--;   // harfi yarıda kesme
  }
  memcpy(out, s, n);
  out[n] = 0;
}

static const char UC_NOKTA[] = "\xE2\x80\xA6";   // …

void kisalt(const YaziTipi &f, const char *s, int genislik, char *out, size_t boyut) {
  if (metin_gen(f, s) <= genislik) { kopyala(out, s, boyut); return; }
  int nokta = metin_gen(f, UC_NOKTA);
  // baştan, sığdığı kadar harf
  const char *p = s, *son = s;
  int w = 0;
  while (*p) {
    const char *q = p;
    const Harf *h = harf_bul(f, utf8_oku(q));
    int d = h ? h->dx : 0;
    if (w + d + nokta > genislik) break;
    w += d;
    p = q;
    son = p;
  }
  size_t n = son - s;
  while (n > 0 && s[n - 1] == ' ') n--;             // rstrip
  if (n + 4 > boyut) n = boyut > 4 ? boyut - 4 : 0;
  memcpy(out, s, n);
  memcpy(out + n, UC_NOKTA, 4);                    // 3 bayt + 0
}

bool iki_satir(const YaziTipi &f, const char *s, int genislik, char *out, size_t boyut) {
  // kelimelere böl
  char satir[3][96];
  int ns = 0;
  satir[0][0] = 0;
  char kelime[96];
  const char *p = s;
  while (*p) {
    while (*p == ' ' || *p == '\n' || *p == '\t') p++;
    if (!*p) break;
    const char *b = p;
    while (*p && *p != ' ' && *p != '\n' && *p != '\t') p++;
    size_t n = p - b;
    if (n >= sizeof(kelime)) n = sizeof(kelime) - 1;
    memcpy(kelime, b, n);
    kelime[n] = 0;
    char dene[200];
    if (ns < 3 && satir[ns][0]) {
      strcpy(dene, satir[ns]);
      strcat(dene, " ");
      strcat(dene, kelime);
    } else {
      strcpy(dene, kelime);
    }
    if (ns < 3 && metin_gen(f, dene) <= genislik) {
      kopyala(satir[ns], dene, sizeof(satir[0]));
    } else {
      if (ns < 3 && satir[ns][0]) ns++;
      if (ns >= 3) {          // üçüncü satırdan sonrası: sonuna ekle (aşağıda kısaltılır)
        size_t l = strlen(satir[2]);
        if (l + n + 2 < sizeof(satir[2])) { satir[2][l] = ' '; memcpy(satir[2] + l + 1, kelime, n + 1); }
        continue;
      }
      kopyala(satir[ns], kelime, sizeof(satir[0]));
    }
  }
  if (ns < 3 && satir[ns][0]) ns++;
  if (ns > 2) {               // ikinci satır ve sonrası birleşir
    size_t l = strlen(satir[1]);
    size_t m = strlen(satir[2]);
    if (l + m + 2 < sizeof(satir[1])) { satir[1][l] = ' '; memcpy(satir[1] + l + 1, satir[2], m + 1); }
    ns = 2;
  }
  bool kisa = false;
  out[0] = 0;
  for (int i = 0; i < ns; i++) {
    char k[100];
    kisalt(f, satir[i], genislik, k, sizeof(k));
    if (strcmp(k, satir[i]) != 0) kisa = true;
    size_t l = strlen(out);
    if (i) { if (l + 1 < boyut) { out[l++] = '\n'; out[l] = 0; } }
    kopyala(out + l, k, boyut - l);
  }
  return kisa;
}
