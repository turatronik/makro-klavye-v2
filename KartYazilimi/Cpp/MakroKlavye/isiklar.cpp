// MAKRO KLAVYE v2 (C++) - RGB LED'ler (18x WS2812B)
// Efektler uygulamanın Işık sekmesinden seçilir, kalıcı bellekte saklanır; uygulama kapalıyken de son efekt çalışır.
// Müziğe göre efektler (kapak, ritim, stereo, ilerleme) uygulamadan gelen kapak renklerini (LP) ve ses seviyesini (LV)
// kullanır. Her efektin üstüne: tuşa basınca tuşun ışığı beyaz parlar, ses değişince taban seviyeyi gösterir.
// İki bölge: zemin (12 taban LED'i) ve tuşlar (6 LED) kendi efekt, renk ve hızıyla çalışabilir.
#include "isiklar.h"
#include "nvm.h"
#include "ekran.h"     // ms_simdi
#include <math.h>
#include <string.h>
#include <stdio.h>
#include <stdlib.h>

static const char *const EFEKTLER[] = {
  "modrengi", "sabit", "nefes", "kalp", "kuyruk", "tarayici", "takip", "doldur", "pirilti", "akis",
  "gokkusagi", "gecis", "dalga", "ates", "konfeti", "polis", "kapali", "kapak", "ritim", "stereo", "ilerleme"};
enum { MODRENGI, SABIT, NEFES, KALP, KUYRUK, TARAYICI, TAKIP, DOLDUR, PIRILTI, AKIS, GOKKUSAGI, GECIS, DALGA,
       ATES, KONFETI, POLIS, KAPALI, KAPAK, RITIM, STEREO, ILERLEME, EFEKT_SAYISI };

int efekt_no(const char *ad) {
  if (!ad) return -1;
  for (int i = 0; i < EFEKT_SAYISI; i++) if (!strcmp(ad, EFEKTLER[i])) return i;
  return -1;
}
const char *efekt_adi(int i) { return (i >= 0 && i < EFEKT_SAYISI) ? EFEKTLER[i] : "sabit"; }

static const int TUS_LED[6] = {14, 13, 12, 15, 16, 17};   // T1..T6 -> zincirdeki LED
// LED'lerin klavyedeki yatay konumu (mm): efektlerin yönü için
static const float X_MM[LED_SAYISI] = {-24, -32, -31, -31, -25.9f, -8.6f, 8.6f, 25.9f, 40, 40, 32, 24, 25, 0, -25, -25, 0, 25};
static float UX[LED_SAYISI], HALKA[LED_SAYISI];

const int NVM_BAS = 1, NVM_TUS = 17;
const uint8_t NVM_IMZA = 0xA7, NVM_TUS_IMZA = 0xB4;

static inline double kesir(double x) { return x - floor(x); }
static inline float sinir(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
static float rastgele() { return (float)rand() / (float)RAND_MAX; }

static Renk karistir(Renk a, Renk b, float t) {
  return {(uint8_t)(a.r + (b.r - a.r) * t), (uint8_t)(a.g + (b.g - a.g) * t), (uint8_t)(a.b + (b.b - a.b) * t)};
}
static Renk olcek(Renk c, float k) { return {(uint8_t)(c.r * k), (uint8_t)(c.g * k), (uint8_t)(c.b * k)}; }

static Renk hsv(double h, float v = 1.0f) {
  double hh = kesir(h) * 6;
  int i = (int)hh;
  float f = (float)(hh - i), q = 1 - f;
  float r, g, b;
  switch (i % 6) {
    case 0: r = 1; g = f; b = 0; break;
    case 1: r = q; g = 1; b = 0; break;
    case 2: r = 0; g = 1; b = f; break;
    case 3: r = 0; g = q; b = 1; break;
    case 4: r = f; g = 0; b = 1; break;
    default: r = 1; g = 0; b = q; break;
  }
  return {(uint8_t)(r * 255 * v), (uint8_t)(g * 255 * v), (uint8_t)(b * 255 * v)};
}

static Renk paletten(const Renk *pal, int n, double u) {
  double uu = kesir(u) * n;
  int i = (int)uu;
  float f = (float)(uu - i);
  f = f * f * (3 - 2 * f);
  return karistir(pal[i % n], pal[(i + 1) % n], f);
}

bool Isiklar::renk_coz(const char *s, Renk &r) {
  if (!s || !*s) return false;
  char *son;
  long v = strtol(s, &son, 16);
  if (son == s) return false;
  r = {(uint8_t)((v >> 16) & 255), (uint8_t)((v >> 8) & 255), (uint8_t)(v & 255)};
  return true;
}

void Isiklar::baslat() {
  for (int i = 0; i < LED_SAYISI; i++) {
    UX[i] = (X_MM[i] + 32) / 72;
    HALKA[i] = i < TABAN ? (float)i / 12 : UX[i] * 0.999f;
    konfeti[i][0] = konfeti[i][1] = 0;
    tohum[i] = rastgele() * 100 + i;
    px[i] = {0, 0, 0};
  }
  yukle();
}

// ------------------------------------------------------------ ayarlar
bool Isiklar::ayarla(const char *ef, Renk r, float p, float h, const Renk *ak, bool kayit_et) {
  int e = efekt_no(ef);
  if (e >= 0) efekt = e;
  renk = r;
  parlaklik = sinir(p, 0.03f, 1.0f);
  genel_var = false;           // Işık sekmesinden değiştirildi: modun ışığı bir sonraki mod değişimine kadar bırakılır
  hiz = sinir(h, 0.2f, 2.0f);
  if (ak) memcpy(akis, ak, sizeof(akis));
  if (kayit_et) { kayit = ms_simdi() + 2000; kayit_var = true; }
  return true;
}

void Isiklar::tus_ayarla(bool ayri, const char *ef, Renk r, float h, const Renk *ak, bool kayit_et) {
  tus_ayri = ayri;
  int e = efekt_no(ef);
  if (e >= 0) tb.efekt = e;
  tb.renk = r;
  tb.hiz = sinir(h, 0.2f, 2.0f);
  if (ak) memcpy(tb.akis, ak, sizeof(tb.akis));
  if (kayit_et) { kayit = ms_simdi() + 2000; kayit_var = true; }
}

void Isiklar::mod_isigi(const char *ef, const Renk *r) {
  int e = efekt_no(ef);
  if (e >= 0) {
    if (!genel_var) { genel_var = true; genel_efekt = efekt; genel_renk = renk; }
    efekt = e;
    renk = r ? *r : genel_renk;
  } else if (genel_var) {
    efekt = genel_efekt;
    renk = genel_renk;
    genel_var = false;
  }
}

static void hex(char *o, Renk c) { sprintf(o, "%02X%02X%02X", c.r, c.g, c.b); }

void Isiklar::ayar_metni(char *out, int boy) {
  uint8_t ef = genel_var ? genel_efekt : efekt;
  Renk rk = genel_var ? genel_renk : renk;
  char h1[8], a[3][8], h2[8], b[3][8];
  hex(h1, rk);
  hex(h2, tb.renk);
  for (int i = 0; i < 3; i++) { hex(a[i], akis[i]); hex(b[i], tb.akis[i]); }
  snprintf(out, boy, "%s\t%s\t%d\t%d\t%s,%s,%s\t%s\t%s\t%s\t%d\t%s,%s,%s", EFEKTLER[ef], h1,
           (int)(parlaklik * 100 + 0.5f), (int)(hiz * 100 + 0.5f), a[0], a[1], a[2], tus_ayri ? "1" : "0",
           EFEKTLER[tb.efekt], h2, (int)(tb.hiz * 100 + 0.5f), b[0], b[1], b[2]);
}

void Isiklar::kaydet() {
  uint8_t ef = genel_var ? genel_efekt : efekt;
  Renk rk = genel_var ? genel_renk : renk;
  uint8_t b[16] = {NVM_IMZA, ef, rk.r, rk.g, rk.b, (uint8_t)(int)(parlaklik * 100), (uint8_t)((int)(hiz * 100) / 2)};
  for (int i = 0; i < 3; i++) { b[7 + 3 * i] = akis[i].r; b[8 + 3 * i] = akis[i].g; b[9 + 3 * i] = akis[i].b; }
  nvm_yaz(NVM_BAS, b, 16);
  uint8_t t[16] = {NVM_TUS_IMZA, (uint8_t)(tus_ayri ? 1 : 0), tb.efekt, tb.renk.r, tb.renk.g, tb.renk.b,
                   (uint8_t)((int)(tb.hiz * 100) / 2)};
  for (int i = 0; i < 3; i++) { t[7 + 3 * i] = tb.akis[i].r; t[8 + 3 * i] = tb.akis[i].g; t[9 + 3 * i] = tb.akis[i].b; }
  nvm_yaz(NVM_TUS, t, 16);
}

void Isiklar::yukle() {
  const uint8_t *b = nvm_veri() + NVM_BAS;
  if (b[0] != NVM_IMZA || b[1] >= EFEKT_SAYISI) return;
  Renk ak[3];
  for (int i = 0; i < 3; i++) ak[i] = {b[7 + 3 * i], b[8 + 3 * i], b[9 + 3 * i]};
  ayarla(EFEKTLER[b[1]], {b[2], b[3], b[4]}, b[5] / 100.0f, b[6] * 2 / 100.0f, ak, false);
  const uint8_t *t = nvm_veri() + NVM_TUS;
  if (t[0] == NVM_TUS_IMZA && t[2] < EFEKT_SAYISI) {
    for (int i = 0; i < 3; i++) ak[i] = {t[7 + 3 * i], t[8 + 3 * i], t[9 + 3 * i]};
    tus_ayarla(t[1] == 1, EFEKTLER[t[2]], {t[3], t[4], t[5]}, t[6] * 2 / 100.0f, ak, false);
  }
}

// ------------------------------------------------------------ olaylar
void Isiklar::mod_rengi(Renk r) {
  renk_mod = r;
  akis_bas = ms_simdi();
  akis_var = true;
}

void Isiklar::ses_goster(int s, bool sessiz) {
  ses_sev = s;
  ses_sessiz = sessiz;
  ses_bitis = ms_simdi() + 2000;
}

void Isiklar::flas_yap(Renk r, float sure) {
  flas_var = true;
  flas_renk = r;
  flas_bitis = ms_simdi() + (uint32_t)(sure * 1000);
}

void Isiklar::kapak_renkleri(const Renk *r, int n) {
  kapak_n = n > 4 ? 4 : n;
  for (int i = 0; i < kapak_n; i++) kapak[i] = r[i];
}

void Isiklar::seviye(float s1, float s2) {
  sol = sinir(s1, 0, 1);
  sag = sinir(s2, 0, 1);
  seviye_ms = ms_simdi();
  seviye_var = true;
}

void Isiklar::bantlar(const uint8_t b[8]) {
  // vuruş yakalama bas bantlarından (ilk iki bant): ritim efekti davula göre atar
  float bas = (b[0] + b[1]) / 200.0f;
  bas_ort += (bas - bas_ort) * 0.06f;
  uint32_t ms = ms_simdi();
  if (bas > bas_ort * 1.3f + 0.05f && ms - son_vurus > 200) {
    son_vurus = ms;
    vurus++;
    zarf = 1.0f;
  }
  bant_ms = ms;
  bant_var = true;
}

bool Isiklar::ac_kapa() {
  acik = !acik;
  if (!acik) sondur = true;
  return acik;
}

// ------------------------------------------------------------ çizim
void Isiklar::tik() {
  uint32_t ms = ms_simdi();
  if (!acik || (son_var && ms - son < 20)) return;     // en çok 50 kare/sn
  if (kayit_var && (int32_t)(ms - kayit) >= 0) { kayit_var = false; kaydet(); }
  float dt = son_var ? (ms - son) / 1000.0f : 0.03f;
  if (dt > 0.1f) dt = 0.1f;
  son = ms;
  son_var = true;
  t += dt * hiz;
  tb.t += dt * tb.hiz;
  if (t > 1e6) t = 0;          // günlerce açık kalırsa hassasiyet kaybolmasın
  if (tb.t > 1e6) tb.t = 0;
  double simdi = ms / 1000.0;
  uint8_t tus_ef = tus_ayri ? tb.efekt : efekt;
  if (efekt == RITIM || tus_ef == RITIM) vurus_bul(ms, dt);

  for (int i = 0; i < LED_SAYISI; i++) {
    if (i < TABAN || !tus_ayri) px[i] = hesapla(i, simdi, efekt, renk, akis, t, hiz);
    else px[i] = hesapla(i, simdi, tb.efekt, tb.renk, tb.akis, tb.t, tb.hiz);
  }

  // 3.1 (AN): basılan tuştan taban LED'lerine yayılan dalga
  if (dalga_var) {
    dalga_t += dt;
    float on = dalga_t * 16.0f;                 // dalganın gittiği LED sayısı
    if (on > TABAN / 2 + 3) dalga_var = false;
    else
      for (int i = 0; i < TABAN; i++) {
        int d = i - dalga_kaynak;
        if (d < 0) d = -d;
        if (d > TABAN / 2) d = TABAN - d;       // zincir halka: kısa yoldan
        float fark = on - d;
        if (fark < -1.2f || fark > 1.2f) continue;
        float k = (1.2f - (fark < 0 ? -fark : fark)) / 1.2f;
        k *= 0.85f * (1.0f - dalga_t * 1.6f);
        if (k <= 0) continue;
        px[i].r = (uint8_t)sinir(px[i].r + (255 - px[i].r) * k, 0, 255);
        px[i].g = (uint8_t)sinir(px[i].g + (255 - px[i].g) * k, 0, 255);
        px[i].b = (uint8_t)sinir(px[i].b + (255 - px[i].b) * k, 0, 255);
      }
  }

  // ses değişince taban seviyeyi gösterir (her efektin üstünde, 2 sn)
  if (ses_isik && ses_sev >= 0 && (int32_t)(ses_bitis - ms) > 0) {
    float dolu = ses_sev / 100.0f * TABAN;
    Renk rk = ses_sessiz ? Renk{60, 60, 60} : (ses_isik == 2 ? Renk{255, 255, 255} : renk_mod);
    for (int i = 0; i < TABAN; i++) {
      float k = sinir(dolu - i, 0, 1);
      if (ses_isik == 3 && !ses_sessiz) {        // yeşil -> sarı -> kırmızı
        float u = (float)i / (TABAN - 1);
        rk = u < 0.5f ? karistir({0, 255, 60}, {255, 190, 0}, u * 2) : karistir({255, 190, 0}, {255, 20, 20}, u * 2 - 1);
      }
      px[i] = olcek(rk, 0.08f + 0.92f * k);
    }
  }

  // renk seçilince bütün ışıklar o renkte yanıp söner
  if (flas_var) {
    int32_t kalan = (int32_t)(flas_bitis - ms);
    if (kalan <= 0) {
      flas_var = false;
    } else if (((int)(kalan / 1000.0f * 4.4f)) % 2 == 0) {
      for (int i = 0; i < LED_SAYISI; i++) px[i] = karistir(px[i], flas_renk, 0.9f);
    }
  }

  // mikrofon kapalı: o tuş kırmızı, yavaşça nefes alır
  if (mik_kapali) {
    float k = 0.55f + 0.45f * (float)fabs(fmod(simdi * 0.8, 2.0) - 1.0);
    for (int i = 0; i < 6; i++)
      if (mik_tuslari & (1 << i)) px[TUS_LED[i]] = {(uint8_t)(255 * k), 0, 0};
  }

  // tuşa basınca beyaz parlama
  for (int i = 0; i < 6; i++) {
    float p = parla[i];
    if (p > 0) {
      parla[i] = p - dt * 3.0f > 0 ? p - dt * 3.0f : 0;
      px[TUS_LED[i]] = karistir(px[TUS_LED[i]], {255, 255, 255}, p);
    }
  }
  degisti = true;
}

void Isiklar::vurus_bul(uint32_t ms, float dt) {
  if (!(bant_var && ms - bant_ms < 600)) {           // gerçek bantlar varsa vuruş bantlar()'da bulunur
    float lv = (sol + sag) / 2;
    ort += (lv - ort) * 0.05f;
    if (lv > ort * 1.25f + 0.04f && ms - son_vurus > 220) {
      son_vurus = ms;
      vurus++;
      zarf = 1.0f;
    }
  }
  zarf = zarf - dt * 4.5f > 0 ? zarf - dt * 4.5f : 0;
}

Renk Isiklar::hesapla(int i, double simdi, uint8_t e, Renk r, const Renk *ak, double T, float zhiz) {
  float u = HALKA[i];
  float x = UX[i];
  bool tus = i >= TABAN;
  switch (e) {
    case MODRENGI: {
      Renk rm = renk_mod;
      if (tus) return olcek(rm, 0.35f);
      float k = 0.25f + 0.2f * (0.5f + 0.5f * (float)sin(simdi * 1.6));
      if (akis_var) {
        float a = (ms_simdi() - akis_bas) / 600.0f;
        if (a >= 0 && a <= 1.3f) {
          float d = a * TABAN - i;
          if (d >= 0 && d < 4) k = k > 1.0f - d / 4 ? k : 1.0f - d / 4;
        }
      }
      return olcek(rm, k);
    }
    case SABIT: return r;
    case NEFES: return olcek(r, 0.12f + 0.88f * (0.5f + 0.5f * (float)sin(T * 2)));
    case KALP: {
      double f = kesir(T * 0.9);
      double k = exp(-pow((f - 0.08) / 0.04, 2)) + 0.7 * exp(-pow((f - 0.26) / 0.05, 2));
      return olcek(r, 0.08f + 0.92f * (float)(k < 1 ? k : 1));
    }
    case KUYRUK: {
      double bas = kesir(T * 0.45);
      if (tus) { float v = 1 - (float)fabs(x - bas) * 3; return olcek(r, 0.15f + 0.85f * (v > 0 ? v : 0)); }
      double d = kesir(bas - u);
      float v = (float)pow(1 - d, 6);
      return olcek(r, v > 0.03f ? v : 0.03f);
    }
    case TARAYICI: {
      double p = 0.5 + 0.5 * sin(T * 2.2);
      float v = 1 - (float)fabs(x - p) * 3.2f;
      return olcek(r, v > 0.03f ? v : 0.03f);
    }
    case TAKIP: return olcek(r, (i + (int)(T * 6)) % 3 == 0 ? 1.0f : 0.06f);
    case DOLDUR: {
      double f = fmod(T * 0.35, 2.0);
      bool dolu = f < 1 ? u < f : u >= f - 1;
      return olcek(r, dolu ? 1.0f : 0.04f);
    }
    case PIRILTI: {
      double n = sin(T * 7 + tohum[i] * 13.1) * sin(T * 3.3 + tohum[i]);
      return n > 0.82 ? Renk{255, 255, 255} : olcek(r, 0.25f);
    }
    case AKIS: return paletten(ak, 3, u - T * 0.18);
    case GOKKUSAGI: return hsv(u - T * 0.15);
    case GECIS: return hsv(T * 0.08);
    case DALGA: {
      float v = 0.5f + 0.5f * (float)sin((x * 2 - T * 1.2) * M_PI);
      return olcek(r, 0.1f + 0.9f * v * v);
    }
    case ATES: {
      float n = 0.55f + 0.45f * (float)(sin(T * 9 + tohum[i] * 5) * sin(T * 4.1 + tohum[i] * 2));
      n = sinir(n + (rastgele() - 0.5f) * 0.25f, 0, 1);
      return karistir({140, 20, 0}, {255, 190, 40}, n);
    }
    case KONFETI: {
      float *k = konfeti[i];
      k[1] *= 0.94f;
      if (rastgele() < 0.02f * zhiz) { k[1] = 1.0f; k[0] = rastgele(); }
      return hsv(k[0], k[1]);
    }
    case POLIS: {
      double f = kesir(T * 2);
      bool solda = x < 0.5f;
      bool yan = ((int)(T * 2)) % 2 == 0 ? solda : !solda;
      bool strob = f < 0.15 || (f > 0.25 && f < 0.4);
      if (yan && strob) return solda ? Renk{255, 30, 60} : Renk{40, 90, 255};
      return {0, 0, 0};
    }
    case KAPALI: return {0, 0, 0};
  }
  // ---- müziğe göre
  Renk pal3[3] = {renk_mod, r, {255, 255, 255}};
  const Renk *pal = kapak_n ? kapak : pal3;
  int pn = kapak_n ? kapak_n : 3;
  float oran = 0;
  bool caliyor = medya ? medya(oran) : false;
  bool taze = seviye_var && ms_simdi() - seviye_ms < 1000;
  if (e == KAPAK || ((e == RITIM || e == STEREO) && !(caliyor && taze))) {
    float k = caliyor ? 0.75f + 0.25f * (float)sin(simdi * 1.3 + u * 6) : 0.25f;
    return olcek(paletten(pal, pn, u + (caliyor ? simdi * 0.06 : 0)), k);
  }
  if (e == RITIM) {
    Renk ana = pal[vurus % pn];
    if (tus) return karistir(olcek(ana, 0.25f), {255, 255, 255}, zarf * 0.85f);
    return olcek(ana, 0.12f + 0.88f * zarf);
  }
  if (e == STEREO) {
    if (tus) { float lv = x < 0.5f ? sol : sag; return olcek(pal[0], 0.15f + 0.85f * lv); }
    bool solda = i < 6;
    int sira = solda ? i : 11 - i;
    float kademe = (5 - sira) / 5.0f;
    float lv = solda ? sol : sag;
    Renk g = karistir({48, 209, 88}, {255, 214, 10}, kademe);
    if (kademe <= lv) return kademe > 0.85f ? Renk{255, 45, 85} : g;
    return olcek(g, 0.05f);
  }
  if (e == ILERLEME) {
    if (tus) return olcek(pal[pn - 1 < 2 ? pn - 1 : 2], caliyor ? 0.3f + 0.2f * (float)sin(simdi * 3) : 0.15f);
    return u <= oran ? pal[0] : olcek(pal[0], 0.06f);
  }
  return {0, 0, 0};
}
