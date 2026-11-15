// MAKRO KLAVYE v2 (C++) - ekran arayüzü (240x240 ST7789)
// Sayfalar: tuşlar (modun tuş haritası) ve medya (çalan şarkı, ses çubukları, ilerleme)
// Alt şerit: ses seviyesi (sol tekerlek çevrilince birkaç saniye) ya da mod / mikrofon / renk şeridi
#include "ekran.h"
#include "komutlar.h"
#include "simgeler.h"
#include "simgedepo.h"
#include <string.h>
#include <stdio.h>
#include <math.h>

static const uint32_t ARKA = 0x0B1018, KART = 0x161E2B, YAZI = 0xF1F5F9, SOLUK = 0x8A94A6, KOYU = 0x0B1018;
static const int ART = 96, CUBUK = 8, CUBUK_ALT = 126, CUBUK_EN = 80;
// RGB565 (büyük uçlu) aç / kapa
static inline void ac(uint16_t c, int &r, int &g, int &b) {
  c = (c >> 8) | (c << 8);
  r = (c >> 11) & 31; g = (c >> 5) & 63; b = c & 31;
}
static inline uint16_t kapa(int r, int g, int b) {
  uint16_t c = (r << 11) | (g << 5) | b;
  return (c >> 8) | (c << 8);
}

static const uint32_t GECIS_SURE = 220;   // 3.1: mod geçiş animasyonu (ms)
static const int KAYAN_ARA = 48;          // kayan adın iki kopyası arası (piksel)
static const int KAYAN_HIZ = 32;          // piksel / sn
static const uint32_t KAYAN_BEKLE = 2500; // her turun başında bekleme (ms)
static const char IPUCU[] = "Sol bas: sessiz · uzun: oynat/dur";
static const char *const KISA_GUN[] = {"Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"};
static const char NOKTA[] = " · ";   // " · "

uint16_t renk565(uint8_t r, uint8_t g, uint8_t b) {
  uint16_t c = ((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3);
  return (uint16_t)((c >> 8) | (c << 8));          // ekrana büyük uçlu gider
}
uint16_t renk565(uint32_t rgb) { return renk565((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

uint16_t Ekran::mod_rengi() const {
  const Mod &m = MODLAR[mod];
  return renk565(m.r, m.g, m.b);
}

// ======================================================================= çizim
void Ekran::dikdortgen(int x, int y, int w, int h, uint16_t c) {
  if (w < 1) w = 1;
  if (h < 1) h = 1;
  int x0 = x < 0 ? 0 : x, y0 = y < 0 ? 0 : y;
  int x1 = x + w > EN ? EN : x + w, y1 = y + h > BOY ? BOY : y + h;
  if (x0 >= x1 || y0 >= y1) return;
  for (int yy = y0; yy < y1; yy++) {
    uint16_t *p = &fb[yy * EN + x0];
    for (int xx = x0; xx < x1; xx++) *p++ = c;
  }
}

// 2.8: karonun çevresi boyunca, üst kenarın ortasından başlayıp saat yönünde t (0-1) kadar dolum çizer
void Ekran::cerceve_ilerleme(int x, int y, int w, int h, int kalinlik, float t, uint16_t c) {
  if (t <= 0) return;
  if (t > 1) t = 1;
  int cx = x + w / 2;
  float u0 = (w - w / 2), u1 = h, u2 = w, u3 = h, u4 = (w / 2);
  float kalan = t * (u0 + u1 + u2 + u3 + u4);
  float k;
  k = kalan < u0 ? kalan : u0; kalan -= k;                                     // üst: ortadan sağa
  dikdortgen(cx, y, (int)k > 0 ? (int)k : 1, kalinlik, c);
  if (kalan <= 0) return;
  k = kalan < u1 ? kalan : u1; kalan -= k;                                     // sağ kenar: yukarıdan aşağı
  dikdortgen(x + w - kalinlik, y, kalinlik, (int)k > 0 ? (int)k : 1, c);
  if (kalan <= 0) return;
  k = kalan < u2 ? kalan : u2; kalan -= k;                                     // alt: sağdan sola
  dikdortgen(x + w - (int)k, y + h - kalinlik, (int)k > 0 ? (int)k : 1, kalinlik, c);
  if (kalan <= 0) return;
  k = kalan < u3 ? kalan : u3; kalan -= k;                                     // sol kenar: aşağıdan yukarı
  dikdortgen(x, y + h - (int)k, kalinlik, (int)k > 0 ? (int)k : 1, c);
  if (kalan <= 0) return;
  k = kalan < u4 ? kalan : u4; kalan -= k;                                     // üst: soldan ortaya
  dikdortgen(x, y, (int)k > 0 ? (int)k : 1, kalinlik, c);
}

void Ekran::daire(int cx, int cy, int r, uint16_t c) {
  for (int dy = -r; dy <= r; dy++) {
    int y = cy + dy;
    if (y < 0 || y >= BOY) continue;
    for (int dx = -r; dx <= r; dx++) {
      if (dx * dx + dy * dy > r * r) continue;
      int x = cx + dx;
      if (x >= 0 && x < EN) fb[y * EN + x] = c;
    }
  }
}

void Ekran::cokgen(int ox, int oy, const int8_t (*p)[2], int n, uint16_t c) {
  int ymin = 127, ymax = -128;
  for (int i = 0; i < n; i++) { if (p[i][1] < ymin) ymin = p[i][1]; if (p[i][1] > ymax) ymax = p[i][1]; }
  for (int y = ymin; y <= ymax; y++) {
    float yc = y + 0.5f;
    float xs[8];
    int k = 0;
    for (int i = 0; i < n && k < 8; i++) {
      float x1 = p[i][0], y1 = p[i][1], x2 = p[(i + 1) % n][0], y2 = p[(i + 1) % n][1];
      if ((y1 <= yc && y2 > yc) || (y2 <= yc && y1 > yc)) xs[k++] = x1 + (yc - y1) * (x2 - x1) / (y2 - y1);
    }
    for (int a = 0; a + 1 < k; a += 2) {
      float l = xs[a] < xs[a + 1] ? xs[a] : xs[a + 1], r = xs[a] < xs[a + 1] ? xs[a + 1] : xs[a];
      for (int x = (int)ceilf(l - 0.5f); x < (int)ceilf(r - 0.5f); x++) {
        int px = ox + x, py = oy + y;
        if (px >= 0 && px < EN && py >= 0 && py < BOY) fb[py * EN + px] = c;
      }
    }
  }
}

// adafruit_display_text Label gibi: (x, y) noktası, metin kutusunun (ax, ay) oranındaki yerine gelir
void Ekran::yazi(const YaziTipi &f, const char *s, int x, int y, float ax, float ay, uint16_t c, float aralik, bool ortala) {
  if (!s || !*s) return;
  int satir = 1, gen = 0;
  for (const char *p = s;;) {
    int w = metin_gen(f, p);
    if (w > gen) gen = w;
    const char *nl = strchr(p, '\n');
    if (!nl) break;
    satir++;
    p = nl + 1;
  }
  int lh = (int)(aralik * f.bbh);
  int kutu_h = f.asc + f.desc + (satir - 1) * lh;
  int sol = x - (int)(ax * gen + 0.5f);
  int ust = y - (int)(ay * kutu_h + 0.5f);
  int taban = ust + f.asc;
  // ortala: her satır kendi içinde ortalanır (Label gibi sola dayalı değil)
  auto satir_bas = [&](const char *q) { return ortala ? sol + (gen - metin_gen(f, q)) / 2 : sol; };
  int cx = satir_bas(s);
  const char *p = s;
  while (*p) {
    if (*p == '\n') { p++; taban += lh; cx = satir_bas(p); continue; }
    const Harf *h = harf_bul(f, utf8_oku(p));
    if (!h) continue;
    int nb = (h->w + 7) >> 3;
    int gx = cx + h->xo, gy = taban - (h->h + h->yo);
    const uint8_t *b = f.bit + h->ofs;
    for (int r = 0; r < h->h; r++) {
      int py = gy + r;
      if (py < 0 || py >= BOY) continue;
      for (int k = 0; k < h->w; k++) {
        if (b[r * nb + (k >> 3)] & (0x80 >> (k & 7))) {
          int px = gx + k;
          if (px >= kesme_x0 && px < kesme_x1) fb[py * EN + px] = c;
        }
      }
    }
    cx += h->dx;
  }
}

void Ekran::mik_simgesi(int x, int y) {
  uint16_t k = renk565(0xF87171);
  daire(x, y - 6, 4, k);
  dikdortgen(x - 4, y - 6, 9, 6, k);
  daire(x, y, 4, k);
  dikdortgen(x - 8, y - 2, 2, 5, k);
  dikdortgen(x + 7, y - 2, 2, 5, k);
  dikdortgen(x - 7, y + 3, 15, 2, k);
  dikdortgen(x - 1, y + 5, 3, 4, k);
  dikdortgen(x - 4, y + 8, 9, 2, k);
  static const int8_t P1[4][2] = {{-9, -11}, {-6, -11}, {10, 10}, {7, 10}};
  static const int8_t P2[4][2] = {{-8, -11}, {-7, -11}, {9, 10}, {8, 10}};
  cokgen(x, y, P1, 4, renk565(KART));
  cokgen(x, y, P2, 4, k);
}

void Ekran::simge_ciz(int no, int x, int y, uint16_t c) {
  if (no >= OZEL_BAS) {                       // 3.1: kullanıcının çizdiği renkli simge
    const OzelSimge *s = ozel_simge(no - OZEL_BAS);
    if (!s) return;
    for (int r = 0; r < 24; r++) {
      int py = y + r;
      if (py < 0 || py >= BOY) continue;
      const uint8_t *sat = &s->px[r * 12];
      for (int k = 0; k < 24; k++) {
        int d = (k & 1) ? (sat[k >> 1] & 15) : (sat[k >> 1] >> 4);
        if (d == 0) continue;                 // 0. renk saydam
        int px = x + k;
        if (px >= 0 && px < EN) fb[py * EN + px] = s->palet[d];
      }
    }
    return;
  }
  if (no < 0 || no >= SIMGE_SAYISI) return;
  const uint32_t *b = SIMGELER[no].bit;
  for (int r = 0; r < 24; r++) {
    int py = y + r;
    if (py < 0 || py >= BOY) continue;
    uint32_t v = b[r];
    for (int k = 0; k < 24; k++)
      if (v & (1u << (23 - k))) {
        int px = x + k;
        if (px >= 0 && px < EN) fb[py * EN + px] = c;
      }
  }
}

void Ekran::kilit_simgesi() {
  uint16_t k = renk565(0xFBBF24);
  daire(158, 12, 5, k);
  daire(158, 12, 3, renk565(KART));
  dikdortgen(152, 13, 12, 9, k);
}

void Ekran::uyari(const char *baslik, const char *metin, const char *alt, uint32_t renk) {
  uyari_var = baslik != nullptr;
  if (uyari_var) {
    kopyala(uyari_baslik, baslik, sizeof(uyari_baslik));
    kopyala(uyari_metin, metin ? metin : "", sizeof(uyari_metin));
    kopyala(uyari_alt, alt ? alt : "", sizeof(uyari_alt));
    uyari_renk = renk565(renk);
  }
  kirli = true;
}

void Ekran::ciz() {
  if (uyari_var) {                  // tam ekran uyarı: aşağı ok (indirme) simgesi, başlık, açıklama
    dikdortgen(0, 0, EN, BOY, renk565(ARKA));
    dikdortgen(0, 0, EN, 6, uyari_renk);
    if (!uyari_metin[0]) {          // 3.1: açıklamasız uyarı = açılış ekranı; ok yerine klavye çizimi
      dikdortgen(52, 54, 136, 68, uyari_renk);
      uint16_t ic = renk565(ARKA);
      for (int i = 0; i < 6; i++) dikdortgen(62 + (i % 3) * 42, 64 + (i / 3) * 30, 34, 22, ic);
      yazi(YT_baslik, uyari_baslik, 120, 150, 0.5f, 0.5f, renk565(YAZI));
      yazi(YT_orta, uyari_alt, 120, 190, 0.5f, 0.5f, uyari_renk);
      return;
    }
    daire(120, 62, 32, uyari_renk);
    uint16_t k = renk565(ARKA);
    dikdortgen(114, 40, 12, 24, k);
    static const int8_t OK[3][2] = {{-15, 0}, {15, 0}, {0, 16}};
    cokgen(120, 62, OK, 3, k);
    dikdortgen(103, 82, 34, 4, k);
    yazi(YT_baslik, uyari_baslik, 120, 118, 0.5f, 0.5f, renk565(YAZI));
    yazi(YT_kucuk, uyari_metin, 120, 168, 0.5f, 0.5f, renk565(SOLUK), 1.15f, true);
    yazi(YT_orta, uyari_alt, 120, 218, 0.5f, 0.5f, uyari_renk);
    return;
  }
  const uint16_t c_kart = renk565(KART), c_yazi = renk565(YAZI), c_soluk = renk565(SOLUK), c_koyu = renk565(KOYU);
  const uint16_t mr = mod_rengi();
  if (bildirim_var) {                     // 3.1 (F): basılan tuşun komutu tam ekran
    const Mod &mm = MODLAR[mod];
    bool koyu_yazi = mm.r * 3 + mm.g * 6 + mm.b > 1100;
    dikdortgen(0, 0, EN, BOY, mr);
    dikdortgen(10, 10, EN - 20, BOY - 20, koyu_yazi ? renk565(0x101828) : c_kart);
    yazi(YT_baslik, bildirim, 120, 120, 0.5f, 0.5f, koyu_yazi ? c_yazi : mr, 1.2f, true);
    return;
  }
  // 3.2 (5): modun kendi zemini varsa tuş sayfası onunla boyanır
  const Mod &mz = MODLAR[mod];
  const uint16_t c_zemin = mz.zemin_var ? renk565(mz.zr, mz.zg, mz.zb) : renk565(ARKA);
  const uint16_t c_karo = mz.zemin_var ? renk565(mz.zr / 2 + 20, mz.zg / 2 + 24, mz.zb / 2 + 32) : c_kart;
  dikdortgen(0, 0, EN, BOY, sayfa == TUSLAR ? c_zemin : renk565(ARKA));
  // ---- üst şerit
  dikdortgen(0, 0, 240, 30, c_kart);
  dikdortgen(0, 28, 240, 2, mr);
  yazi(YT_kucuk, ust_tarih, 62, 16, 0, 0.5f, c_soluk);
  yazi(YT_orta, ust_sol, 10, 15, 0, 0.5f, c_yazi);
  yazi(YT_kucuk, sira, 232, 15, 1, 0.5f, mr);
  daire(196, 15, 4, renk565(bagli ? 0x22C55E : 0x334155));

  if (sayfa == TUSLAR) {
    // 3.1 (AH): mod değişince karolar sağdan kayarak gelir
    int kay = 0;
    if (gecis_animasyonu && gecis_bas) {
      uint32_t ge = ms_simdi() - gecis_bas;
      if (ge >= GECIS_SURE) gecis_bas = 0;
      else {
        float t = 1.0f - (float)ge / GECIS_SURE;
        kay = (int)(58 * t * t);
      }
    }
    if (mz.duzen == 1) {                     // 3.2 (BK): liste düzeni
      for (int i = 0; i < 6; i++) {
        int x = 6 + (kay ? kay * (i + 1) / 6 : 0), y = 36 + i * 30;
        bool b = basili[i];
        dikdortgen(x, y, 228, 28, b ? mr : c_karo);
        dikdortgen(x, y, 4, 28, mr);
        char no[2] = {(char)('1' + i), 0};
        yazi(YT_kucuk, no, x + 10, y + 14, 0, 0.5f, b ? c_koyu : mr);
        int sm2 = MODLAR[mod].tuslar[i].simge;
        if (sm2 >= 0) simge_ciz(sm2, x + 22, y + 2, b ? c_koyu : c_yazi);
        yazi(YT_orta, tus_ad[i], x + (sm2 >= 0 ? 52 : 26), y + 14, 0, 0.5f, b ? c_koyu : c_yazi, 0.95f);
        if (tus_uz[i][0]) yazi(YT_kucuk, tus_uz[i], x + 222, y + 14, 1, 0.5f, b ? c_koyu : mr);
        if (tus_ilerlemesi[i] > 0) cerceve_ilerleme(x, y, 228, 28, 2, tus_ilerlemesi[i], c_yazi);
      }
    } else
    for (int i = 0; i < 6; i++) {
      int x = 6 + (i % 3) * 78 + (kay ? (i % 3 == 0 ? kay : (i % 3 == 1 ? kay * 2 / 3 : kay / 3)) : 0);
      int y = 38 + (i / 3) * 82;
      bool b = basili[i];
      dikdortgen(x, y, 72, 76, mr);
      dikdortgen(x + 2, y + 2, 68, 72, b ? mr : c_karo);
      char no[2] = {(char)('1' + i), 0};
      yazi(YT_kucuk, no, x + 6, y + 5, 0, 0, b ? c_koyu : mr);
      int sm = MODLAR[mod].tuslar[i].simge;
      if (sm >= 0) {                           // 2.7: simge üstte, ad altında (3.1: özel simgeler de)
        simge_ciz(sm, x + 24, y + (tus_uz[i][0] ? 8 : 12), b ? c_koyu : c_yazi);
        yazi(*tus_font[i], tus_ad[i], x + 36, y + (tus_uz[i][0] ? 46 : 52), 0.5f, 0.5f, b ? c_koyu : c_yazi, 0.95f, true);
      } else {
        yazi(*tus_font[i], tus_ad[i], x + 36, y + (tus_uz[i][0] ? 36 : 44), 0.5f, 0.5f, b ? c_koyu : c_yazi, 0.95f);
      }
      if (tus_uz[i][0]) yazi(YT_kucuk, tus_uz[i], x + 36, y + 64, 0.5f, 0.5f, b ? c_koyu : mr);
      if (tus_ilerlemesi[i] > 0)             // 2.8: uzun basışın ne kadar dolduğunu halka gösterir
        cerceve_ilerleme(x, y, 72, 76, 3, tus_ilerlemesi[i], c_yazi);
    }
    yazi(YT_kucuk, ipucu, 120, 218, 0.5f, 0.5f, c_soluk, 1.0f);
  } else if (sayfa == MEDYA) {
    // 2.7b: kapak bulanıklaştırılıp koyulaştırılarak zemin olur, çubuklar altta tam genişlikte
    if (!zemin_gecerli) zemin_kur();
    zemini_ciz();
    const int KB = 72, KX = 10, KY = 38;         // küçük kapak (96 -> 72)
    if (kapak_var && m_var) {
      for (int y = 0; y < KB; y++) {
        const uint16_t *s = &kapak_px[(y * 4 / 3) * ART];
        uint16_t *p = &fb[(KY + y) * EN + KX];
        for (int x = 0; x < KB; x++) p[x] = s[x * 4 / 3];
      }
    } else {
      dikdortgen(KX, KY, KB, KB, c_kart);
      uint16_t n = renk565(0x475569);
      daire(KX + 30, KY + 50, 9, n);
      dikdortgen(KX + 35, KY + 16, 4, 36, n);
      dikdortgen(KX + 35, KY + 16, 16, 5, n);
    }
    int x = 92;
    if (kayan) {                    // 2.4: sığmayan şarkı adı kayar (iki kopya arka arkaya)
      kesme_x0 = x; kesme_x1 = 232;
      yazi(YT_baslik, m_baslik, x - kayan_ofs, 54, 0, 0.5f, c_yazi);
      yazi(YT_baslik, m_baslik, x - kayan_ofs + kayan_gen + KAYAN_ARA, 54, 0, 0.5f, c_yazi);
      kesme_x0 = 0; kesme_x1 = EN;
    } else {
      yazi(YT_baslik, l_baslik, x, 54, 0, 0.5f, c_yazi);
    }
    yazi(YT_orta, l_sanatci, x, 78, 0, 0.5f, c_soluk);
    yazi(YT_kucuk, l_durum, x, 98, 0, 0.5f, mr);
    if (l_kaynak[0]) yazi(YT_kucuk, l_kaynak, x + metin_gen(YT_kucuk, l_durum) + 10, 98, 0, 0.5f, c_soluk);
    // 8 bant -> 16 çubuk (aradakiler yumuşatılır)
    for (int j = 0; j < 16; j++) {
      float u = j * (CUBUK - 1) / 15.0f;
      int k = (int)u;
      if (k > CUBUK - 2) k = CUBUK - 2;
      float t = u - k;
      int h = (int)(eq_h[k] + (eq_h[k + 1] - eq_h[k]) * t);
      if (h < 3) h = 3;
      int r1, g1, b1, r2, g2, b2;
      ac(eq_renk[k], r1, g1, b1);
      ac(eq_renk[k + 1], r2, g2, b2);
      dikdortgen(9 + j * 14, 210 - h, 12, h,
                 kapa((int)(r1 + (r2 - r1) * t), (int)(g1 + (g2 - g1) * t), (int)(b1 + (b2 - b1) * t)));
    }
    yazi(YT_kucuk, l_gecen, 8, 220, 0, 0.5f, c_soluk);
    yazi(YT_kucuk, l_ses, 120, 220, 0.5f, 0.5f, c_soluk);
    yazi(YT_kucuk, l_sure, 232, 220, 1, 0.5f, c_soluk);
    dikdortgen(8, 230, 224, 4, renk565(0x273244));
    dikdortgen(8, 230, ilerleme_w, 4, mr);
  }
  else if (sayfa == SISTEM) {
    sistem_sayfasi();
  }
  // 3.2 (AC): klavye üstündeki ayar menüsü
  if (menu_var) {
    const int PX = 16, PY = 38, PW = 208, PH = 170, SATIR = 25;
    dikdortgen(PX, PY, PW, PH, c_kart);
    dikdortgen(PX, PY, PW, 2, mr);
    dikdortgen(PX, PY + PH - 2, PW, 2, mr);
    dikdortgen(PX, PY, 2, PH, mr);
    dikdortgen(PX + PW - 2, PY, 2, PH, mr);
    yazi(YT_kucuk, "AYARLAR", PX + 10, PY + 12, 0, 0.5f, c_soluk);
    for (int i = 0; i < menu_n && i < 6; i++) {
      int ry = PY + 22 + i * SATIR;
      bool sec = i == menu_secim;
      if (sec) dikdortgen(PX + 4, ry, PW - 8, SATIR - 2, menu_duzenle ? mr : renk565(0x1E293B));
      uint16_t yr = sec && menu_duzenle ? c_koyu : c_yazi;
      yazi(YT_kucuk, menu_ad[i], PX + 12, ry + SATIR / 2 - 1, 0, 0.5f, sec ? yr : c_soluk);
      yazi(YT_kucuk, menu_deger[i], PX + PW - 12, ry + SATIR / 2 - 1, 1, 0.5f, sec && menu_duzenle ? yr : mr);
    }
    yazi(YT_kucuk, menu_duzenle ? "çevir: değiştir · bas: bitir" : "çevir: seç · bas: değiştir",
         120, PY + PH - 13, 0.5f, 0.5f, c_soluk);
  }
  // simgeler (sayfaların üstünde, alt şeridin altında)
  if (mik_kapali) mik_simgesi(179, 15);
  if (kilitli) kilit_simgesi();
  // ---- alt şerit
  if (ses_gorunur) {
    dikdortgen(0, 196, 240, 44, renk565(ARKA));
    dikdortgen(8, 200, 224, 36, serit_renk);
    if (cubuk_gorunur) {
      dikdortgen(100, 214, 72, 8, renk565(0x273244));
      dikdortgen(100, 214, ses_bar_w, 8, ses_bar_renk);
    }
    yazi(YT_orta, ses_bas, 16, 218, 0, 0.5f, ses_yazi_renk);
    yazi(YT_orta, ses_deger, 226, 218, 1, 0.5f, ses_yazi_renk);
  }
}

// ======================================================================= kurulum
void Ekran::baslat() {
  for (int i = 0; i < 6; i++) { tus_font[i] = &YT_orta; tus_ad[i][0] = tus_uz[i][0] = 0; }
  kopyala(ipucu, IPUCU, sizeof(ipucu));
  ust_sol[0] = ust_tarih[0] = sira[0] = 0;
  l_kaynak[0] = l_durum[0] = l_ses[0] = l_baslik[0] = l_sanatci[0] = l_gecen[0] = l_sure[0] = 0;
  for (int i = 0; i < CUBUK; i++) { eq_h[i] = 2; eq_ih[i] = 2; eq_renk[i] = 0xFFFF; }
  konum_ms = ms_simdi();
  seviye_ms = 0;
  sis[0] = 0;
  hepsini_kirlet();
}

// ======================================================================= tuş sayfası
void Ekran::tuslari_yaz() {
  const Mod &m = MODLAR[mod];
  for (int i = 0; i < 6; i++) {
    const Tus &t = m.tuslar[i];
    if (t.uzun_var && t.uzun_ad[0]) kisalt(YT_kucuk, t.uzun_ad, 66, tus_uz[i], sizeof(tus_uz[i]));
    else tus_uz[i][0] = 0;
    tus_font[i] = &YT_orta;
    if (t.simge >= 0) {
      // simgeli tuşta ada daha az yer var: tek satır orta sığmazsa küçük yazıyla (en çok 2 satır)
      if (metin_gen(YT_orta, t.ad) <= 64) kopyala(tus_ad[i], t.ad, sizeof(tus_ad[i]));
      else { tus_font[i] = &YT_kucuk; iki_satir(YT_kucuk, t.ad, 64, tus_ad[i], sizeof(tus_ad[i])); }
      if (tus_uz[i][0] && strchr(tus_ad[i], '\n')) {   // uzun adı da varsa ad tek satıra kısalır
        tus_font[i] = &YT_kucuk;
        kisalt(YT_kucuk, t.ad, 64, tus_ad[i], sizeof(tus_ad[i]));
      }
    } else if (iki_satir(YT_orta, t.ad, 64, tus_ad[i], sizeof(tus_ad[i]))) {   // sığmıyorsa küçük yazı tipi
      tus_font[i] = &YT_kucuk;
      iki_satir(YT_kucuk, t.ad, 64, tus_ad[i], sizeof(tus_ad[i]));
    }
  }
  kirli = true;
}

void Ekran::tus_basildi(int i, bool b) {
  basili[i] = b;
  if (sayfa == TUSLAR) kirli = true;
}

void Ekran::tus_ilerleme(int i, float t) {
  if (i < 0 || i >= 6) return;
  if (t < 0) t = 0;
  if (t > 1) t = 1;
  if (t == tus_ilerlemesi[i]) return;
  tus_ilerlemesi[i] = t;
  if (sayfa == TUSLAR) kirli = true;
}

// ======================================================================= medya sayfası
static const char *durum_adi(char d) {
  switch (d) {
    case 'P': return "Çalıyor";          // Çalıyor
    case 'D': return "Duraklatıldı";        // Duraklatıldı
    case 'S': return "Durdu";
  }
  return "";
}

void Ekran::medyayi_yaz() {
  kirli = true;
  if (!bagli) {
    strcpy(l_kaynak, "Bilgisayar");
    strcpy(l_durum, "Bağlı değil");                      // Bağlı değil
    strcpy(l_ses, "Uygulamayı aç");                            // Uygulamayı aç
    strcpy(l_baslik, "Uygulama bekleniyor");
    kayan = false;
    strcpy(l_sanatci, "Tuşlar yine de çalışır");   // Tuşlar yine de çalışır
    ilerleme_sifirla();
    kare_cubuk = false;
    return;
  }
  if (!strcmp(l_ses, "Uygulamayı aç")) l_ses[0] = 0;   // bağlandı; ses seviyesi birazdan gelir
  if (!m_var) {
    l_kaynak[0] = 0;
    strcpy(l_durum, "Sessizlik");
    ilerleme_sifirla();
    kare_cubuk = false;
    strcpy(l_baslik, "Çalan bir şey yok");                 // Çalan bir şey yok
    kayan = false;
    strcpy(l_sanatci, "Bir oynatıcıda şarkı başlat");  // Bir oynatıcıda şarkı başlat
    return;
  }
  kisalt(YT_kucuk, m_kaynak, 90, l_kaynak, sizeof(l_kaynak));
  kopyala(l_durum, durum_adi(m_durum), sizeof(l_durum));
  kisalt(YT_baslik, m_baslik, 140, l_baslik, sizeof(l_baslik));
  kayan_gen = metin_gen(YT_baslik, m_baslik);
  kayan = kayan_gen > 140;
  kisalt(YT_orta, m_sanatci, 140, l_sanatci, sizeof(l_sanatci));
  son_ilerleme = -1;
  ilerleme_guncelle(true);
  kare_cubuk = true;
  cubuk_renkleri();
}

void Ekran::ilerleme_sifirla() {
  ilerleme_w = 1;
  l_gecen[0] = l_sure[0] = 0;
  kirli = true;
}

static void sure_yaz(char *out, float sn) {
  int s = sn > 0 ? (int)sn : 0;
  if (s >= 3600) sprintf(out, "%d:%02d:%02d", s / 3600, (s / 60) % 60, s % 60);
  else sprintf(out, "%d:%02d", s / 60, s % 60);
}

void Ekran::ilerleme_guncelle(bool zorla) {
  if (sayfa != MEDYA || !m_var || !bagli) return;
  float k = konum;
  if (m_durum == 'P') k += (ms_simdi() - konum_ms) / 1000.0f;
  if (m_sure > 0 && k > m_sure) k = m_sure;
  int sn = (int)k;
  if (sn == son_ilerleme && !zorla) return;
  son_ilerleme = sn;
  if (m_sure > 0) {
    int w = (int)(224 * k / m_sure);
    ilerleme_w = w < 1 ? 1 : w;
    sure_yaz(l_gecen, k);
    sure_yaz(l_sure, m_sure);
    kirli = true;
  } else {
    ilerleme_sifirla();
  }
}

float Ekran::medya_ilerleme() const {
  if (!m_var || m_sure <= 0) return 0;
  float k = konum + (m_durum == 'P' ? (ms_simdi() - konum_ms) / 1000.0f : 0);
  return k >= m_sure ? 1.0f : k / m_sure;
}

// ======================================================================= kapak resmi (2.4)

void Ekran::kapagi_ciz() {
  for (int y = 0; y < ART; y++) memcpy(&fb[(38 + y) * EN + 8], &kapak_px[y * ART], ART * 2);
  // ses çubukları kapağın altında, yarı saydam (yükseklik kapağın ~%40'ı)
  for (int i = 0; i < CUBUK; i++) {
    int h = (eq_ih[i] * 38) / CUBUK_EN;
    if (h < 2) h = 2;
    int br, bg, bb;
    ac(eq_renk[i], br, bg, bb);
    for (int yy = CUBUK_ALT + 6 - h; yy < CUBUK_ALT + 6; yy++)
      for (int xx = 13 + i * 11; xx < 13 + i * 11 + 9; xx++) {
        uint16_t *p = &fb[yy * EN + xx];
        int r, g, b;
        ac(*p, r, g, b);
        *p = kapa((r * 35 + br * 65) / 100, (g * 35 + bg * 65) / 100, (b * 35 + bb * 65) / 100);
      }
  }
}

// 2.7b: zemin için kapağın 12x12 blok ortalaması; kapak yoksa mod renginden koyu bir geçiş
void Ekran::zemin_kur() {
  zemin_gecerli = true;
  if (kapak_var && m_var) {
    for (int by = 0; by < 12; by++)
      for (int bx = 0; bx < 12; bx++) {
        int r = 0, g = 0, b = 0;
        for (int y = 0; y < 8; y++)
          for (int x = 0; x < 8; x++) {
            int rr, gg, bb;
            ac(kapak_px[(by * 8 + y) * ART + bx * 8 + x], rr, gg, bb);
            r += rr; g += gg; b += bb;
          }
        zg[by][bx][0] = (uint8_t)(r * 255 / (64 * 31) * 38 / 100);     // 5/6/5 -> 0-255, sonra %38
        zg[by][bx][1] = (uint8_t)(g * 255 / (64 * 63) * 38 / 100);
        zg[by][bx][2] = (uint8_t)(b * 255 / (64 * 31) * 38 / 100);
      }
    return;
  }
  const Mod &md = MODLAR[mod];
  for (int by = 0; by < 12; by++)
    for (int bx = 0; bx < 12; bx++) {
      int f = 6 + by + bx;
      zg[by][bx][0] = (uint8_t)(((ARKA >> 16) & 255) + md.r * f / 180);
      zg[by][bx][1] = (uint8_t)(((ARKA >> 8) & 255) + md.g * f / 180);
      zg[by][bx][2] = (uint8_t)((ARKA & 255) + md.b * f / 180);
    }
}

// 12x12 ızgarayı 240x210'a iki yönlü (bilinear) büyütür: bulanık bir zemin çıkar
void Ekran::zemini_ciz() {
  const int Y0 = 30, YB = BOY - Y0;
  static int16_t sx[EN], tx[EN];
  static bool hazir = false;
  if (!hazir) {
    for (int x = 0; x < EN; x++) {
      int u = (x * 12 * 256) / EN - 128;
      if (u < 0) u = 0;
      int i = u >> 8, t = u & 255;
      if (i > 10) { i = 10; t = 255; }
      sx[x] = (int16_t)i; tx[x] = (int16_t)t;
    }
    hazir = true;
  }
  for (int y = 0; y < YB; y++) {
    int v = (y * 12 * 256) / YB - 128;
    if (v < 0) v = 0;
    int sy = v >> 8, ty = v & 255;
    if (sy > 10) { sy = 10; ty = 255; }
    uint8_t satir[12][3];
    for (int i = 0; i < 12; i++)
      for (int j = 0; j < 3; j++)
        satir[i][j] = (uint8_t)((zg[sy][i][j] * (256 - ty) + zg[sy + 1][i][j] * ty) >> 8);
    uint16_t *p = &fb[(Y0 + y) * EN];
    for (int x = 0; x < EN; x++) {
      int i = sx[x], t = tx[x];
      p[x] = renk565((uint8_t)((satir[i][0] * (256 - t) + satir[i + 1][0] * t) >> 8),
                     (uint8_t)((satir[i][1] * (256 - t) + satir[i + 1][1] * t) >> 8),
                     (uint8_t)((satir[i][2] * (256 - t) + satir[i + 1][2] * t) >> 8));
    }
  }
}

void Ekran::kapak_basla() {
  memset(kapak_gelen, 0, sizeof(kapak_gelen));
}

void Ekran::kapak_satiri(int y, const uint8_t *le, int n) {
  if (y < 0 || y >= ART || n < ART * 2) return;
  uint16_t *s = &kapak_px[y * ART];
  for (int x = 0; x < ART; x++) s[x] = (uint16_t)((le[x * 2] << 8) | le[x * 2 + 1]);   // küçük uçlu -> ekrana büyük uçlu
  kapak_gelen[y >> 3] |= 1 << (y & 7);
  bool tamam = true;
  for (int i = 0; i < 12; i++) if (kapak_gelen[i] != 0xFF) { tamam = false; break; }
  if (tamam || kapak_var) {           // ilk kapak tamamlanınca görünür; sonrakiler satır satır yenilenir
    kapak_var = true;
    zemin_gecerli = false;
    if (sayfa == MEDYA) kirli = true;
  }
}

void Ekran::kapak_yok() {
  kapak_var = false;
  zemin_gecerli = false;
  memset(kapak_gelen, 0, sizeof(kapak_gelen));
  if (sayfa == MEDYA) kirli = true;
}

void Ekran::medya_ayarla(const char *baslik, const char *sanatci, const char *kaynak, char durum, float k, float sure) {
  if (strcmp(baslik, m_baslik) != 0 || !m_var) {   // yeni şarkı: eski kapak gitsin, ad baştan kaysın
    kapak_var = false;
    zemin_gecerli = false;
    memset(kapak_gelen, 0, sizeof(kapak_gelen));
    kayan_bas = ms_simdi();
  }
  m_var = true;
  kopyala(m_baslik, baslik, sizeof(m_baslik));
  kopyala(m_sanatci, sanatci, sizeof(m_sanatci));
  kopyala(m_kaynak, kaynak, sizeof(m_kaynak));
  m_durum = durum;
  m_sure = sure;
  konum = k;
  konum_ms = ms_simdi();
  if (sayfa == MEDYA) medyayi_yaz();
}

void Ekran::medya_yok() {
  m_var = false;
  zemin_gecerli = false;
  if (sayfa == MEDYA) medyayi_yaz();
}

void Ekran::konum_ayarla(float k, float sure, char durum) {
  if (!m_var) return;
  m_sure = sure;
  bool degisti = m_durum != durum;
  m_durum = durum;
  konum = k;
  konum_ms = ms_simdi();
  if (degisti && sayfa == MEDYA) { kopyala(l_durum, durum_adi(durum), sizeof(l_durum)); kirli = true; }
  ilerleme_guncelle(true);
}

// ======================================================================= saat
void Ekran::saat_ayarla(int32_t sn, int gun, const char *tarih) {
  saat_var = true;
  saat_sn = sn;
  saat_ms = ms_simdi();
  saat_gun = gun;
  kopyala(saat_tarih, tarih, sizeof(saat_tarih));
  ust_saat_yaz();
}

int32_t Ekran::simdi_sn() const { return saat_sn + (int32_t)((ms_simdi() - saat_ms) / 1000); }

void Ekran::ust_saat_yaz() {
  int32_t s = simdi_sn();
  ust_dk = s / 60;
  int32_t g = s % 86400;
  sprintf(ust_sol, "%02d:%02d", (int)(g / 3600), (int)((g / 60) % 60));
  char t[64];
  if (saat_gun >= 0) {
    snprintf(t, sizeof(t), "%s%s%s", KISA_GUN[(saat_gun + s / 86400) % 7],
             (saat_tarih[0] && s < 86400) ? " " : "", (saat_tarih[0] && s < 86400) ? saat_tarih : "");
  } else {
    kopyala(t, saat_tarih, sizeof(t));
  }
  kisalt(YT_kucuk, t, 120, ust_tarih, sizeof(ust_tarih));
  kirli = true;
}

// ======================================================================= sistem bilgisi
void Ekran::sistem_ayarla(int cpu, int ram, int gpu, int ind, int yuk, int pil) {
  char s[140];
  if (gpu >= 0) snprintf(s, sizeof(s), "İşlemci %%%d%sBellek %%%d%sEkran kartı %%%d", cpu, NOKTA, ram, NOKTA, gpu);
  else snprintf(s, sizeof(s), "İşlemci %%%d%sBellek %%%d", cpu, NOKTA, ram);
  if (metin_gen(YT_kucuk, s) > 228) {
    if (gpu >= 0) snprintf(s, sizeof(s), "CPU %%%d%sRAM %%%d%sGPU %%%d", cpu, NOKTA, ram, NOKTA, gpu);
    else snprintf(s, sizeof(s), "CPU %%%d%sRAM %%%d", cpu, NOKTA, ram);
  }
  char p[100];
  p[0] = 0;
  if (ind >= 0) {
    int mx = ind > yuk ? ind : yuk;
    if (mx >= 1000) snprintf(p, sizeof(p), "İnd %.1f%sYük %.1f Mb/s", ind / 1000.0, NOKTA, yuk / 1000.0);
    else snprintf(p, sizeof(p), "İnd %d%sYük %d kb/s", ind, NOKTA, yuk);
  }
  if (pil >= 0) {
    size_t l = strlen(p);
    snprintf(p + l, sizeof(p) - l, "%sPil %%%d", l ? NOKTA : "", pil);
  }
  if (p[0]) {
    char k[100];
    kisalt(YT_kucuk, p, 228, k, sizeof(k));
    size_t l = strlen(s);
    snprintf(s + l, sizeof(s) - l, "\n%s", k);
  }
  kopyala(sis, s, sizeof(sis));
  // 2.7: grafik geçmişi
  {
    int k = (g_bas + g_n) % GECMIS;
    if (g_n == GECMIS) g_bas = (g_bas + 1) % GECMIS; else g_n++;
    auto yuzde = [](int v) { return (uint8_t)(v < 0 ? 0 : (v > 100 ? 100 : v)); };
    g_cpu[k] = yuzde(cpu); g_ram[k] = yuzde(ram); g_gpu[k] = yuzde(gpu);
    auto hz = [](int v) { long x = v < 0 ? 0 : v / 8; return (uint16_t)(x > 65535 ? 65535 : x); };
    g_ind[k] = hz(ind); g_yuk[k] = hz(yuk);
    s_cpu = cpu; s_ram = ram; s_gpu = gpu; s_ind = ind; s_yuk = yuk;
    if (sayfa == SISTEM) kirli = true;
  }
  sis_var = true;
  sis_ms = ms_simdi();
  ipucu_yaz();
}

void Ekran::ipucu_yaz() {
  const char *m = (sis_var && sistem_acik && bagli) ? sis : IPUCU;
  if (strcmp(ipucu, m) != 0) {
    kopyala(ipucu, m, sizeof(ipucu));
    if (sayfa == TUSLAR) kirli = true;
  }
}

// ======================================================================= ses çubukları
void Ekran::kapak_renkleri(const uint8_t (*renk)[3], int n) {
  kapak_n = n > 4 ? 4 : n;
  for (int i = 0; i < kapak_n; i++) memcpy(kapak[i], renk[i], 3);
  cubuk_renkleri();
}

void Ekran::cubuk_renkleri() {
  uint8_t pal[4][3];
  int n = kapak_n;
  if (n) {
    memcpy(pal, kapak, sizeof(pal));
  } else {
    const Mod &m = MODLAR[mod];
    pal[0][0] = m.r; pal[0][1] = m.g; pal[0][2] = m.b;
    pal[1][0] = m.r + 90 > 255 ? 255 : m.r + 90;
    pal[1][1] = m.g + 90 > 255 ? 255 : m.g + 90;
    pal[1][2] = m.b + 90 > 255 ? 255 : m.b + 90;
    n = 2;
  }
  for (int i = 0; i < CUBUK; i++) {
    uint8_t c[3];
    if (n == 1) {
      memcpy(c, pal[0], 3);
    } else {
      float u = (float)i / (CUBUK - 1) * (n - 1);
      int k = (int)u;
      if (k > n - 2) k = n - 2;
      float t = u - k;
      for (int j = 0; j < 3; j++) c[j] = (uint8_t)(pal[k][j] + (pal[k + 1][j] - pal[k][j]) * t);
    }
    eq_renk[i] = renk565(c[0], c[1], c[2]);
  }
  if (sayfa == MEDYA) kirli = true;
}

void Ekran::seviye(float s1, float s2) {
  sol = s1;
  sag = s2;
  seviye_ms = ms_simdi();
  seviye_var = true;
}

void Ekran::bantlar(const uint8_t b[8]) {
  memcpy(bant, b, 8);
  bant_ms = ms_simdi();
  bant_var = true;
}

void Ekran::cubuklari_oynat(uint32_t ms) {
  if (ms - eq_ms < 25) return;           // en çok 40 kare/sn (2.3: ekran ikinci çekirdekte)
  eq_ms = ms;
  double t = ms / 1000.0;
  bool caliyor = m_var && m_durum == 'P';
  bool taze = seviye_var && ms - seviye_ms < 600;
  bool gercek = bant_var && ms - bant_ms < 600;        // 2.7: uygulama gerçek frekansları gönderiyor
  for (int i = 0; i < CUBUK; i++) {
    float hedef;
    if (caliyor && gercek) {
      hedef = 3 + CUBUK_EN * (bant[i] / 100.0f);
    } else if (caliyor && taze) {
      float sv = i < CUBUK / 2 ? sol : sag;
      float dalga = 0.5f + 0.5f * (float)sin(t * (5.0 + i * 0.9) + i * 1.7);
      hedef = 3 + CUBUK_EN * sv * (0.45f + 0.55f * dalga);
    } else if (caliyor) {
      hedef = 8 + 22 * (0.5f + 0.5f * (float)sin(t * (1.6 + i * 0.3) + i * 1.1));
    } else {
      hedef = 3;
    }
    float h = eq_h[i];
    h += (hedef - h) * (hedef > h ? 0.6f : 0.25f);
    eq_h[i] = h;
    int ih = (int)h;
    if (ih < 2) ih = 2;
    if (ih != eq_ih[i]) { eq_ih[i] = ih; kirli = true; }
  }
}

// ======================================================================= alt şerit
void Ekran::mod_goster(float sure) {
  const Mod &m = MODLAR[mod];
  char s[16];
  if (mod_kilit) snprintf(s, sizeof(s), "kilit");
  else if (m.grup[0]) snprintf(s, sizeof(s), "%s", m.grup);    // 3.2 (AQ): grup adı
  else snprintf(s, sizeof(s), "%d/%d", mod + 1, MOD_SAYISI);
  serit(m.ad, m.r, m.g, m.b, s, sure);
}

void Ekran::serit(const char *metin, uint8_t r, uint8_t g, uint8_t b, const char *sag_metin, float sure) {
  serit_renk = renk565(r, g, b);
  bool koyu = r * 3 + g * 6 + b > 1100;            // açık renk üstüne koyu yazı
  cubuk_gorunur = false;
  ses_yazi_renk = renk565(koyu ? KOYU : YAZI);
  // sağdaki yazı varsa soldaki onunla çakışmayacak kadar kısalır (sol 16'dan, sağ 226'ya kadar)
  int sinir = (sag_metin && *sag_metin) ? 226 - metin_gen(YT_orta, sag_metin) - 12 - 16 : 210;
  kisalt(YT_orta, metin, sinir, ses_bas, sizeof(ses_bas));
  kopyala(ses_deger, sag_metin ? sag_metin : "", sizeof(ses_deger));
  ses_gorunur = true;
  ses_bitis = ms_simdi() + (uint32_t)(sure * 1000);
  kirli = true;
}

void Ekran::ses_goster(int ses, bool sessiz, int yon, const char *ad, float sure) {
  serit_renk = renk565(KART);
  cubuk_gorunur = true;
  ses_yazi_renk = renk565(YAZI);
  kisalt(YT_orta, ad, 80, ses_bas, sizeof(ses_bas));
  ses_bar_renk = sessiz ? renk565(0x64748B) : mod_rengi();
  if (ses == -2) {
    ses_bar_w = 36;
    strcpy(ses_deger, yon > 0 ? "+" : (yon < 0 ? "-" : ""));
  } else if (ses < 0) {
    ses_bar_w = 1;
    strcpy(ses_deger, "yok");
  } else {
    int w = 72 * ses / 100;
    ses_bar_w = w < 1 ? 1 : w;
    if (sessiz) strcpy(ses_deger, "Sessiz");
    else sprintf(ses_deger, "%%%d", ses);
  }
  ses_gorunur = true;
  ses_bitis = ms_simdi() + (uint32_t)(sure * 1000);
  kirli = true;
}

// ======================================================================= sistem grafiği (2.7)
// v: GECMIS örnek (halka); olcek: tam yükseklik değeri. Doldur: altı koyu, üst çizgi parlak.
void Ekran::grafik(int x, int y, int w, int h, const uint16_t *v, int olcek, uint16_t renk, bool doldur) {
  if (olcek < 1) olcek = 1;
  int r, g, b;
  {
    uint16_t c = (renk >> 8) | (renk << 8);
    r = (c >> 11) & 31; g = (c >> 5) & 63; b = c & 31;
  }
  uint16_t koyu = (uint16_t)(((r * 3 / 10) << 11) | ((g * 3 / 10) << 5) | (b * 3 / 10));
  koyu = (koyu >> 8) | (koyu << 8);
  int n = g_n < w ? g_n : w;
  int onceki = -1;
  for (int i = 0; i < n; i++) {
    int k = (g_bas + g_n - n + i) % GECMIS;     // eskiden yeniye; en yenisi sağ kenarda
    int px = x + w - n + i;
    long d = v[k];
    int yy = (int)(h - 1 - (d * (h - 1)) / olcek);
    if (yy < 0) yy = 0;
    if (doldur) for (int q = yy + 1; q < h; q++) fb[(y + q) * EN + px] = koyu;
    // dik çizgiyle bir önceki noktaya bağla (kırık çizgi görünmesin)
    int a = onceki < 0 ? yy : onceki, c = yy;
    if (a > c) { int t = a; a = c; c = t; }
    for (int q = a; q <= c; q++) fb[(y + q) * EN + px] = renk;
    onceki = yy;
  }
}

static void hiz_yaz(char *o, size_t boy, const char *ad, int kbps) {
  if (kbps < 0) snprintf(o, boy, "%s —", ad);
  else if (kbps >= 1000) snprintf(o, boy, "%s %.1f Mb/s", ad, kbps / 1000.0);
  else snprintf(o, boy, "%s %d kb/s", ad, kbps);
}

void Ekran::sistem_sayfasi() {
  const uint16_t c_kart = renk565(KART), c_soluk = renk565(SOLUK), c_yazi = renk565(YAZI);
  const uint16_t mr = mod_rengi(), mavi = renk565(0x38BDF8), yesil = renk565(0x22C55E), turuncu = renk565(0xFB923C);
  if (!sis_var && g_n == 0) {
    yazi(YT_orta, bagli ? "Sistem bilgisi bekleniyor" : "Uygulama bağlı değil", 120, 130, 0.5f, 0.5f, c_soluk);
    return;
  }
  static uint16_t tmp[GECMIS];
  char s[48];
  // --- işlemci + ekran kartı
  dikdortgen(6, 36, 228, 70, c_kart);
  snprintf(s, sizeof(s), "İşlemci %%%d", s_cpu);
  yazi(YT_kucuk, s, 12, 46, 0, 0.5f, mr);
  if (s_gpu >= 0) { snprintf(s, sizeof(s), "Ekran kartı %%%d", s_gpu); yazi(YT_kucuk, s, 228, 46, 1, 0.5f, mavi); }
  if (s_gpu >= 0) { for (int i = 0; i < GECMIS; i++) tmp[i] = g_gpu[i]; grafik(8, 56, 224, 48, tmp, 100, mavi, false); }
  for (int i = 0; i < GECMIS; i++) tmp[i] = g_cpu[i];
  grafik(8, 56, 224, 48, tmp, 100, mr, true);
  // --- bellek
  dikdortgen(6, 110, 228, 56, c_kart);
  snprintf(s, sizeof(s), "Bellek %%%d", s_ram);
  yazi(YT_kucuk, s, 12, 120, 0, 0.5f, c_yazi);
  for (int i = 0; i < GECMIS; i++) tmp[i] = g_ram[i];
  grafik(8, 130, 224, 34, tmp, 100, renk565(0xA78BFA), true);
  // --- internet (iki çizgi, ortak ölçek: son ekrandaki en yüksek değer)
  dikdortgen(6, 170, 228, 66, c_kart);
  hiz_yaz(s, sizeof(s), "İnd", s_ind);
  yazi(YT_kucuk, s, 12, 180, 0, 0.5f, yesil);
  hiz_yaz(s, sizeof(s), "Yük", s_yuk);
  yazi(YT_kucuk, s, 228, 180, 1, 0.5f, turuncu);
  int en = 64;          // en az 512 kb/s ölçek (örnekler /8 saklı)
  for (int i = 0; i < GECMIS; i++) { if (g_ind[i] > en) en = g_ind[i]; if (g_yuk[i] > en) en = g_yuk[i]; }
  grafik(8, 190, 224, 44, g_ind, en, yesil, true);
  grafik(8, 190, 224, 44, g_yuk, en, turuncu, false);
}

void Ekran::ses_metni(int ses, bool sessiz) {
  if (sessiz) strcpy(l_ses, "Ses: sessiz");
  else sprintf(l_ses, "Ses %%%d", ses);
  if (sayfa == MEDYA) kirli = true;
}

void Ekran::mik_ayarla(bool k) { mik_kapali = k; kirli = true; }
void Ekran::kilit_ayarla(bool k) { kilitli = k; kirli = true; }

// ======================================================================= genel
void Ekran::uyari_sureli(const char *baslik, const char *metin, const char *alt, uint32_t renk, float sn) {
  uyari(baslik, metin, alt, renk);
  uyari_bitis = ms_simdi() + (uint32_t)(sn * 1000);
}

void Ekran::tus_bildir(const char *ad, float sn) {
  if (!tus_bildirimi_acik || !ad || !*ad) return;
  kisalt(YT_baslik, ad, 200, bildirim, sizeof(bildirim));
  bildirim_bitis = ms_simdi() + (uint32_t)(sn * 1000);
  bildirim_var = true;
  kirli = true;
}

void Ekran::menu_ac(bool a) {
  menu_var = a;
  menu_duzenle = false;
  kirli = true;
}

void Ekran::menu_ayarla(int secim, bool duzenle, const char *const *satirlar, const char *const *degerler, int n) {
  menu_secim = secim;
  menu_duzenle = duzenle;
  menu_n = n > 6 ? 6 : n;
  for (int i = 0; i < menu_n; i++) {
    kopyala(menu_ad[i], satirlar[i], sizeof(menu_ad[i]));
    kopyala(menu_deger[i], degerler[i], sizeof(menu_deger[i]));
  }
  kirli = true;
}

void Ekran::mod_kilidi(bool k) {
  mod_kilit = k;
  kirli = true;
}

void Ekran::mod_ayarla(int m) {
  if (mod != m) gecis_bas = ms_simdi();      // 3.1 (AH)
  mod = m;
  zemin_gecerli = false;
  if (!kapak_n) cubuk_renkleri();
  if (!saat_var) kisalt(YT_orta, MODLAR[m].ad, 170, ust_sol, sizeof(ust_sol));
  snprintf(sira, sizeof(sira), "%d/%d", m + 1, MOD_SAYISI);
  for (int i = 0; i < 6; i++) basili[i] = false;
  sayfa_yenile();
  kirli = true;
}

void Ekran::sayfa_ayarla(Sayfa s) {
  sayfa = s;
  sayfa_yenile();
}

void Ekran::sayfa_yenile() {
  if (sayfa == TUSLAR) tuslari_yaz();
  else if (sayfa == MEDYA) medyayi_yaz();
  kirli = true;
}

void Ekran::baglanti(bool b) {
  if (b == bagli) return;
  bagli = b;
  if (!b) l_ses[0] = 0;
  ipucu_yaz();
  if (sayfa == MEDYA) medyayi_yaz();
  kirli = true;
}

void Ekran::tik() {
  uint32_t ms = ms_simdi();
  if (ses_gorunur && (int32_t)(ms - ses_bitis) > 0) { ses_gorunur = false; kirli = true; }
  if (saat_var && simdi_sn() / 60 != ust_dk) ust_saat_yaz();
  if (sayfa == MEDYA) {
    if (kare_cubuk) cubuklari_oynat(ms);
    if (kayan && m_var && bagli) {
      int dongu = kayan_gen + KAYAN_ARA;
      uint32_t tur = KAYAN_BEKLE + (uint32_t)dongu * 1000 / KAYAN_HIZ;
      uint32_t t = (ms - kayan_bas) % tur;
      int ofs = t < KAYAN_BEKLE ? 0 : (int)((t - KAYAN_BEKLE) * KAYAN_HIZ / 1000);
      if (ofs != kayan_ofs) { kayan_ofs = ofs; kirli = true; }
    }
  } else if (sis_var && ms - sis_ms > 8000) {      // uygulama sistem bilgisi göndermeyi bıraktı
    sis_var = false;
    ipucu_yaz();
  }
  if (uyari_var && uyari_bitis && (int32_t)(ms - uyari_bitis) > 0) { uyari_bitis = 0; uyari(nullptr, nullptr, nullptr, 0); }
  if (bildirim_var && (int32_t)(ms - bildirim_bitis) > 0) { bildirim_var = false; kirli = true; }
  if (gecis_bas && sayfa == TUSLAR) kirli = true;
  ilerleme_guncelle();
}
