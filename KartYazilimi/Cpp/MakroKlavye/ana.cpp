// =========================================================
// MAKRO KLAVYE v2 - ana program (C++, Arduino, RP2040-Zero)
// =========================================================
// CircuitPython 1.24 sürümünün C++'a çevrilmiş hâli: aynı özellikler, aynı ekran, aynı ışıklar,
// MakroKeyboardDisplay uygulamasıyla aynı haberleşme (uygulamada değişiklik gerekmez).
// Modlar ve ayarlar CircuitPython'un kalıcı belleğiyle aynı yerde durur (geçişte kaybolmaz).
//
// Sağ tekerlek : çevir = mod değiştir (ya da modun tekerlek görevi), bas = ekran sayfası, uzun bas = ışık aç/kapa
// Sol tekerlek : çevir = ses, bas = sessiz, uzun bas = oynat / duraklat, basılı tutup çevir = sonraki / önceki şarkı
// İki tekerleğe birlikte 1 sn bas: tuş kilidi
//
// Derleme (Arduino IDE): Kart "Waveshare RP2040 Zero" (Earle Philhower arduino-pico), USB Stack "Pico SDK",
// Flash Size "2MB (no FS)". Kütüphaneler: Adafruit NeoPixel, ArduinoJson (7.x).

#include <Arduino.h>
#include <Keyboard.h>
#include <Mouse.h>
#include <Adafruit_NeoPixel.h>
#include <hardware/watchdog.h>
#include <tusb.h>
#include <pico/mutex.h>
#include "ekran.h"
#include "lcd.h"
#include "isiklar.h"
#include "komutlar.h"
#include "nvm.h"
#include "simgedepo.h"
#include "simgeler.h"

#define SURUM "3.2"

// ---------------- AYARLAR ----------------
const int SOL_YON = 1, SAG_YON = 1;          // tekerlek ters çalışıyorsa -1
const uint32_t UZUN_BASIS = 600;             // ms (tekerlek düğmeleri)
// 3.1: tuşun ikinci komutuna geçme süresi artık ayardan gelir (ayar[6] * 50 ms)
#define TUS_UZUN ((uint32_t)(ayar[6] * 50))
const uint32_t TEKRAR_ILK = 300, TEKRAR_ARA = 150;
const uint32_t ZAMAN_ASIMI = 6000;           // bu kadar mesaj gelmezse uygulama bağlı değil

// ---------------- PİNLER ----------------
const uint8_t TUS_PINLERI[8] = {6, 7, 8, 14, 15, 26, 2, 5};   // 6 tuş + sol, sağ tekerlek düğmesi
const int SOL_TUS = 6, SAG_TUS = 7;
const int ENC_SOL_A = 0, ENC_SOL_B = 1, ENC_SAG_A = 3, ENC_SAG_B = 4;
const int LED_PIN = 27;

Ekran ekran;
Isiklar isik;
Adafruit_NeoPixel serit_led(LED_SAYISI, LED_PIN, NEO_GRB + NEO_KHZ800);

uint32_t ms_simdi() { return millis(); }

// ============================================================ TEKERLEKLER (kesmeyle)
struct Teker {
  uint8_t a, b;
  volatile uint8_t durum;
  volatile int8_t alt;
  volatile int32_t konum;
};
static Teker teker[2] = {{ENC_SOL_A, ENC_SOL_B, 0, 0, 0}, {ENC_SAG_A, ENC_SAG_B, 0, 0, 0}};

static void teker_guncelle(Teker &t) {
  // CircuitPython rotaryio ile aynı tablo (bölen 4)
  static const int8_t GECIS[16] = {0, -1, +1, 0, +1, 0, 0, -1, -1, 0, 0, +1, 0, +1, -1, 0};
  uint8_t yeni = (digitalRead(t.a) << 1) | digitalRead(t.b);
  int idx = (t.durum << 2) | yeni;
  t.durum = yeni;
  t.alt += GECIS[idx];
  if (t.alt >= 4) { t.konum++; t.alt = 0; }
  else if (t.alt <= -4) { t.konum--; t.alt = 0; }
}
static void sol_kesme() { teker_guncelle(teker[0]); }
static void sag_kesme() { teker_guncelle(teker[1]); }

// ============================================================ TUŞLAR (titreşim süzgeçli)
struct TusOlayi { uint8_t no; bool basildi; };
static TusOlayi olaylar[16];
static uint8_t olay_bas = 0, olay_son = 0;
static bool tus_durum[8], tus_ham[8];
static uint32_t tus_degisim[8];
static uint32_t son_tarama = 0;

static void tuslari_tara(uint32_t ms) {
  if (ms - son_tarama < 2) return;
  son_tarama = ms;
  for (int i = 0; i < 8; i++) {
    bool b = digitalRead(TUS_PINLERI[i]) == LOW;
    if (b != tus_ham[i]) { tus_ham[i] = b; tus_degisim[i] = ms; }
    if (b != tus_durum[i] && ms - tus_degisim[i] >= 15) {     // 15 ms kararlı
      tus_durum[i] = b;
      uint8_t s = (olay_son + 1) & 15;
      if (s != olay_bas) { olaylar[olay_son] = {(uint8_t)i, b}; olay_son = s; }
    }
  }
}
static bool olay_al(TusOlayi &o) {
  if (olay_bas == olay_son) return false;
  o = olaylar[olay_bas];
  olay_bas = (olay_bas + 1) & 15;
  return true;
}

// ============================================================ UYGULAMA BAĞLANTISI (USB seri)
static char gelen[2100];
static int gelen_n = 0;
static uint32_t son_mesaj = 0;
static bool hic_mesaj = true;

static bool bagli() { return !hic_mesaj && millis() - son_mesaj < ZAMAN_ASIMI; }

// "A\tB\tC\n" yazar; alanlar sayı ya da metin
static char cikis[600];
static int cikis_n = 0;
static void y_bas(const char *k) { cikis_n = snprintf(cikis, sizeof(cikis), "%s", k); }
static void y_ekle(const char *s) {
  if (cikis_n < (int)sizeof(cikis) - 2) cikis_n += snprintf(cikis + cikis_n, sizeof(cikis) - cikis_n, "\t%s", s);
}
static void y_ekle(long v) { char b[16]; snprintf(b, sizeof(b), "%ld", v); y_ekle(b); }
static void y_ekle(int v) { y_ekle((long)v); }
static void y_gonder() {
  if (!bagli()) return;
  if (cikis_n > (int)sizeof(cikis) - 2) cikis_n = sizeof(cikis) - 2;
  cikis[cikis_n++] = '\n';
  Serial.write((const uint8_t *)cikis, cikis_n);
}
static void yaz1(const char *k) { y_bas(k); y_gonder(); }
template <typename A> static void yaz2(const char *k, A a) { y_bas(k); y_ekle(a); y_gonder(); }
template <typename A, typename B> static void yaz3(const char *k, A a, B b) { y_bas(k); y_ekle(a); y_ekle(b); y_gonder(); }
template <typename A, typename B, typename C> static void yaz4(const char *k, A a, B b, C c) {
  y_bas(k); y_ekle(a); y_ekle(b); y_ekle(c); y_gonder();
}

// ============================================================ HID
static bool uyutma = false;
static uint32_t son_kipirdama = 0;

static void tus_bas(int k) {
  if (k >= 0xE0 && k <= 0xE7) Keyboard.press(0x80 + (k - 0xE0));
  else if (k > 0 && k + 136 <= 255) Keyboard.press(k + 136);
}

static void medya_tusu(uint16_t k) {
  Keyboard.consumerPress(k);
  Keyboard.consumerRelease();
}

static void eylem_calistir(const Eylem &e) {
  switch (e.tur) {
    case E_TUS:
      for (int i = 0; i < e.n; i++) tus_bas(e.kod[i]);
      Keyboard.releaseAll();
      break;
    case E_MEDYA: medya_tusu(e.kod[0]); break;
    case E_FARE_TIK: Mouse.click(e.kod[0]); break;
    case E_FARE_TEKER: Mouse.move(0, 0, e.kod[0]); break;
    case E_FARE_ZOOM:
      Keyboard.press(0x80);                // Ctrl + tekerlek
      Mouse.move(0, 0, e.kod[0]);
      Keyboard.releaseAll();
      break;
    case E_LED: isik.ac_kapa(); break;
    case E_UYUTMA:
      uyutma = !uyutma;
      ekran.ses_goster(-2, false, 0, uyutma ? "Uyku engel açık" : "Uyku engel kapalı", 1.5f);
      break;
  }
}

// ============================================================ MOD
static int mod = 0;

static int kayitli_mod() {
  int m = nvm_oku(0);
  return m < MOD_SAYISI ? m : 0;
}

static uint8_t mod_sayfa[EN_FAZLA_MOD] = {};   // 2.9: her modun son sayfası (0 yok, yoksa Sayfa + 1)
static bool mod_kilitli = false;               // 2.9: sağ tekerlek modu değiştirmesin

// 3.2 (AQ): mod grupları. Tekerlek aynı gruptaki modlar arasında döner;
// tekerleğin düğmesi basılıyken çevirince sıradaki grubun ilk moduna atlanır.
static int grup_icinde(int bas, int yon) {
  const char *g = MODLAR[bas].grup;
  if (!g[0]) {                                  // grupsuz modlar kendi aralarında
    for (int i = 1; i <= MOD_SAYISI; i++) {
      int k = ((bas + yon * i) % MOD_SAYISI + MOD_SAYISI) % MOD_SAYISI;
      if (!MODLAR[k].grup[0]) return k;
    }
    return bas;
  }
  for (int i = 1; i <= MOD_SAYISI; i++) {
    int k = ((bas + yon * i) % MOD_SAYISI + MOD_SAYISI) % MOD_SAYISI;
    if (!strcmp(MODLAR[k].grup, g)) return k;
  }
  return bas;
}

static int sonraki_grup(int bas, int yon) {
  const char *g = MODLAR[bas].grup;
  for (int i = 1; i <= MOD_SAYISI; i++) {
    int k = ((bas + yon * i) % MOD_SAYISI + MOD_SAYISI) % MOD_SAYISI;
    if (strcmp(MODLAR[k].grup, g)) return k;
  }
  return bas;
}

static void mod_ayarla(int yeni, bool bildir = true, bool goster = true) {
  int eski = mod;
  mod = ((yeni % MOD_SAYISI) + MOD_SAYISI) % MOD_SAYISI;
  const Mod &m = MODLAR[mod];
  if (ekran.sayfa == Ekran::YOK) ekran.sayfa = m.medya_ekrani ? Ekran::MEDYA : Ekran::TUSLAR;
  ekran.mod_ayarla(mod);
  isik.mod_rengi({m.r, m.g, m.b});
  Renk ir = {m.ir, m.ig, m.ib};
  isik.mod_isigi(m.isik_var ? m.isik_efekt : nullptr, m.isik_renk_var ? &ir : nullptr);
  uint8_t mik = 0;
  for (int i = 0; i < 6; i++)
    if (m.tuslar[i].kisa.tur == E_MIK || (m.tuslar[i].uzun_var && m.tuslar[i].uzun.tur == E_MIK)) mik |= 1 << i;
  isik.mik_tuslari = mik;
  if (nvm_oku(0) != mod) nvm_yaz1(0, mod);
  if (mod != eski) {                           // 2.9: her mod en son hangi sayfadaysa oraya döner
    Ekran::Sayfa hedef = mod_sayfa[mod] ? (Ekran::Sayfa)(mod_sayfa[mod] - 1)
                                        : (m.medya_ekrani ? Ekran::MEDYA : Ekran::TUSLAR);
    if (!bagli() && hedef != Ekran::TUSLAR) hedef = Ekran::TUSLAR;
    if (ekran.sayfa != hedef) ekran.sayfa_ayarla(hedef);
  }
  if (goster && mod != eski && ekran.mod_bildir) ekran.mod_goster();
  if (bildir) {
    yaz3("MOD", mod, m.ad);
    yaz2("SAYFA", ekran.sayfa == Ekran::MEDYA ? "medya" : (ekran.sayfa == Ekran::SISTEM ? "sistem" : "tuslar"));
  }
}

static bool medya_durumu(float &oran) {
  if (!ekran.medya_var() || !bagli()) return false;
  oran = ekran.medya_ilerleme();
  return ekran.medya_durum() == 'P';
}

// ============================================================ EKRAN AYARLARI
// nvm 40: imza, parlaklık (10-100), bayraklar, ses ışığı, gece başı (saat, 255 kapalı), gece sonu, gece parlaklığı (%)
const int NVM_AYAR = 40;
const uint8_t NVM_AYAR_IMZA = 0xD3, NVM_AYAR_IMZA2 = 0xD4, NVM_AYAR_IMZA3 = 0xD5;
// bayraklar: 1 mod şeridi, 2 tam ekran geri bildirim, 4 LED dalgası, 8 sistem bilgisi,
// 16 mikrofon/kamera uyarısı, 32 mod geçiş animasyonu, 64 açılış ekranı
const int B_MOD = 1, B_BILDIRIM = 2, B_DALGA = 4, B_SISTEM = 8, B_AVUYARI = 16, B_ANIM = 32, B_ACILIS = 64;
// 3.1: ayar[6] = uzun basış süresi (50 ms'nin katı, 4-30 => 200-1500 ms)
static int ayar[7] = {100, 127, 1, 255, 7, 20, 10};
static bool gece = false;

static bool gece_mi() {
  int bas = ayar[3], bit = ayar[4];
  if (bas > 23 || !ekran.saat_var) return false;
  int h = (ekran.simdi_sn() / 3600) % 24;
  return bas < bit ? (bas <= h && h < bit) : (h >= bas || h < bit);
}

static volatile bool pc_kapali = false;
// 2.6: ekranı çekirdek 1 açar (ekranın açılışı ~1 sn sürer; bu sırada çekirdek 0 uygulamaya cevap verebilsin)
static volatile bool lcd_hazir = false;
static volatile float istenen_parlaklik = 1.0f;

static void parlaklik_uygula() {
  gece = gece_mi();
  int p = gece ? (ayar[0] < ayar[5] ? ayar[0] : ayar[5]) : ayar[0];
  istenen_parlaklik = p / 100.0f;
  if (!pc_kapali && lcd_hazir) lcd::parlaklik(p / 100.0f);
  isik.kis(gece ? ayar[5] / 100.0f : 1.0f);
}

static void ayar_uygula() {
  parlaklik_uygula();
  ekran.mod_bildir = ayar[1] & B_MOD;
  ekran.sistem_acik = ayar[1] & B_SISTEM;
  ekran.tus_bildirimi_acik = ayar[1] & B_BILDIRIM;      // 3.1 (F)
  ekran.gecis_animasyonu = ayar[1] & B_ANIM;            // 3.1 (AH)
  isik.dalga_acik = ayar[1] & B_DALGA;                  // 3.1 (AN)
  isik.ses_isik = ayar[2];
  ekran.sayfa_yenile();
  ekran.ipucu_yaz();
}

static void ayar_yukle() {
  const uint8_t *b = nvm_veri() + NVM_AYAR;
  if ((b[0] == NVM_AYAR_IMZA || b[0] == NVM_AYAR_IMZA2) && b[1] >= 10 && b[1] <= 100 && b[3] <= 3) {
    ayar[0] = b[1]; ayar[1] = b[2]; ayar[2] = b[3];
    if (b[0] != NVM_AYAR_IMZA && b[5] <= 23 && b[6] >= 5 && b[6] <= 100) { ayar[3] = b[4]; ayar[4] = b[5]; ayar[5] = b[6]; }
    if (b[0] == NVM_AYAR_IMZA3 && b[7] >= 4 && b[7] <= 30) ayar[6] = b[7];
    else ayar[1] |= B_ANIM | B_ACILIS;      // eski kayıt: yeni görsellikler açık gelsin
  }
}

static void ayar_kaydet() {
  uint8_t b[8] = {NVM_AYAR_IMZA3};
  for (int i = 0; i < 7; i++) b[i + 1] = ayar[i];
  nvm_yaz(NVM_AYAR, b, 8);
}

// ============================================================ HATA KAYDI
static void nvm_not(const char *metin) {
  uint8_t b[128];
  int n = strlen(metin);
  if (n > 126) n = 126;
  b[0] = 0xE7;
  b[1] = n;
  memcpy(b + 2, metin, n);
  nvm_yaz(NVM_HATA, b, n + 2);
}

static bool son_hata(char *out, int boy) {
  const uint8_t *b = nvm_veri() + NVM_HATA;
  if (b[0] != 0xE7 || b[1] == 0 || b[1] > 126) return false;
  int n = b[1] < boy - 1 ? b[1] : boy - 1;
  memcpy(out, b + 2, n);
  out[n] = 0;
  return true;
}

// ============================================================ UYGULAMA VERİSİ
static bool acilis_watchdog = false;
static uint32_t pc_kapali_sayisi_on();
static bool test = false;      // tuş testi
static int ses = -2;           // Windows ses seviyesi (-2: bilinmiyor)
static bool sessiz = false;
static uint32_t en_az_bellek = 0xFFFFFFFF;

static float sayi(const char *s, float v = 0) {
  if (!s || !*s) return v;
  char *son;
  double d = strtod(s, &son);
  return son == s ? v : (float)d;
}

static void modlari_bildir() {
  y_bas("MODLAR");
  for (int i = 0; i < MOD_SAYISI; i++) y_ekle(MODLAR[i].ad);
  y_gonder();
  for (int i = 0; i < MOD_SAYISI; i++) {
    const Mod &m = MODLAR[i];
    char r[8];
    snprintf(r, sizeof(r), "%02X%02X%02X", m.r, m.g, m.b);
    y_bas("MODTUS");
    y_ekle(i);
    y_ekle(r);
    for (int k = 0; k < 6; k++) y_ekle(m.tuslar[k].ad);
    y_gonder();
  }
}

static void modlari_gonder() {
  int n = 0;
  const char *m = kayitli_mod_metni(n);
  if (!m) { m = VARSAYILAN_MODLAR; n = strlen(m); }
  const int PARCA = 160;
  int adet = n ? (n + PARCA - 1) / PARCA : 1;
  for (int i = 0; i < adet; i++) {
    char parca[PARCA * 4 + 1];
    // UTF-8 harfini bölmemek için bayt sınırını harf başına çek (Python harf sayardı; uygulama birleştirir)
    int bas = i * PARCA, son = (i + 1) * PARCA < n ? (i + 1) * PARCA : n;
    while (bas > 0 && bas < n && (((uint8_t)m[bas]) & 0xC0) == 0x80) bas--;
    while (son < n && (((uint8_t)m[son]) & 0xC0) == 0x80) son--;
    int l = son - bas;
    if (l < 0) l = 0;
    memcpy(parca, m + bas, l);
    parca[l] = 0;
    y_bas("MODJ");
    y_ekle(i);
    y_ekle(adet);
    y_ekle(parca);
    y_gonder();
  }
}

static void modlari_degistir() {
  mod_ayarla(mod < MOD_SAYISI ? mod : MOD_SAYISI - 1, true, false);
  modlari_bildir();
}

static char *const mod_parca = MOD_TAMPON;   // 2.7: 20 KB ortak tampon (komutlar.cpp)
static int mod_parca_n = 0, mod_parca_sayi = 0;

static void modlari_al(char **a, int n) {
  int i = (int)sayi(a[1], -1), toplam = (int)sayi(a[2], 0);
  if (i == 0) { mod_parca_n = 0; mod_parca_sayi = 0; }
  if (i != mod_parca_sayi || toplam < 1 || toplam > 160) {
    mod_parca_n = mod_parca_sayi = 0;
    yaz3("MODYAZ", "HATA", "parçalar karıştı");
    return;
  }
  const char *p = n >= 4 ? a[3] : "";
  int l = strlen(p);
  if (mod_parca_n + l >= MODLAR_EN_COK) {
    mod_parca_n = mod_parca_sayi = 0;
    yaz3("MODYAZ", "HATA", "modlar çok uzun");
    return;
  }
  memcpy(mod_parca + mod_parca_n, p, l);
  mod_parca_n += l;
  mod_parca_sayi++;
  if (i < toplam - 1) return;
  mod_parca[mod_parca_n] = 0;
  int uzunluk = mod_parca_n;
  mod_parca_n = mod_parca_sayi = 0;
  char hata[64];
  // önce kaydet (çözüm tamponu yerinde değiştirir), sonra kur; kurulamazsa kayıt geri silinir
  if (!modlari_yaz(mod_parca, uzunluk, hata, sizeof(hata))) {
    yaz3("MODYAZ", "HATA", hata);
    return;
  }
  if (!modlari_kur(mod_parca, hata, sizeof(hata))) {
    modlari_sil();
    modlari_varsayilan_kur();
    modlari_degistir();
    yaz3("MODYAZ", "HATA", hata);
    return;
  }
  modlari_degistir();
  yaz3("MODYAZ", "TAMAM", MOD_SAYISI);
}

static void sistem_sesi(int fark) {
  int adim = fark > 0 ? 1 : -1;
  int n = abs(fark) < 12 ? abs(fark) : 12;     // 2.9: ivmeyle 12 adıma kadar
  for (int i = 0; i < n; i++) medya_tusu(adim > 0 ? 0xE9 : 0xEA);
  if (ses >= 0) {                              // uygulama gerçek değeri birazdan gönderir
    ses += 2 * adim * n;
    if (ses < 0) ses = 0;
    if (ses > 100) ses = 100;
    sessiz = false;
  }
  ekran.ses_goster(ses >= 0 ? ses : -2, sessiz, adim, "Sistem");
  if (ses >= 0) isik.ses_goster(ses, sessiz);
}

static void led_cevap() {
  char m[200];
  isik.ayar_metni(m, sizeof(m));
  y_bas("LED");
  y_ekle(m);
  y_gonder();
}

static const char *sayfa_adi() {
  return ekran.sayfa == Ekran::MEDYA ? "medya" : (ekran.sayfa == Ekran::SISTEM ? "sistem" : "tuslar");
}

// ============================================================ İKİ ÇEKİRDEK (2.3)
// Çekirdek 0: tuşlar, tekerlekler, USB (klavye + uygulama), ışıklar.  Çekirdek 1: ekranı çizip gönderir.
// Ekranın durumu (yazılar, sayfa...) kilitle korunur: çekirdek 0 döngüsünün gövdesini kilit altında çalıştırır,
// çekirdek 1 yalnızca çizerken (~2 ms) kilidi alır; asıl uzun iş olan SPI gönderimi kilitsiz yapılır.
static mutex_t ekran_kilidi;
static volatile bool cekirdek2_hazir = false;
static volatile uint32_t c1_nabiz = 0, c1_cizim = 0;
static volatile bool guncelleme_istek = false;

// Yazılım güncellemesi: kart "RPI-RP2" adlı USB bellek olarak açılır; yeni .uf2 dosyası içine kopyalanır.
static void guncelleme_moduna_gec() { guncelleme_istek = true; }   // kilit dışında guncelleme_uygula yapar

static void guncelleme_uygula() {
  guncelleme_istek = false;
  mutex_enter_blocking(&ekran_kilidi);
  ekran.uyari("Güncelleme modu", "Klavye birazdan \"RPI-RP2\" adlı\nUSB bellek olarak açılacak.\nYeni yazılım kopyalanınca\nkendiliğinden yeniden başlar.",
              "Tuşlar şu an çalışmaz", 0x38BDF8);
  mutex_exit(&ekran_kilidi);
  uint32_t c = c1_cizim, t0 = millis();
  while (c1_cizim == c && millis() - t0 < 1000) { rp2040.wdt_reset(); delay(5); }   // çekirdek 1 çizsin
  lcd::parlaklik(ayar[0] / 100.0f);
  nvm_kaydet();
  for (int i = 0; i < LED_SAYISI; i++) serit_led.setPixelColor(i, 0, 40, 60);   // ışıklar mavi
  serit_led.show();
  for (int i = 0; i < 25; i++) { rp2040.wdt_reset(); delay(100); }   // uyarı 2,5 sn görünsün
  rp2040.rebootToBootloader();
}

// makro dizisinin durumu (dizi_tik aşağıda)
static struct {
  bool aktif = false;
  const char *p = nullptr, *son = nullptr;
  uint32_t sonraki = 0;
  bool uygulama_bekliyor = false;
  uint32_t bekleme_bitis = 0;
} dz;

static OzelSimge sim_gecici;     // 3.1: uygulamadan gelen özel simge (tek tek yazılır)
static int sim_alinan = 0;

static int base64_coz(const char *s, uint8_t *out, int boy) {
  uint32_t v = 0;
  int bit = 0, n = 0;
  for (; *s && *s != '='; s++) {
    char c = *s;
    int d;
    if (c >= 'A' && c <= 'Z') d = c - 'A';
    else if (c >= 'a' && c <= 'z') d = c - 'a' + 26;
    else if (c >= '0' && c <= '9') d = c - '0' + 52;
    else if (c == '+') d = 62;
    else if (c == '/') d = 63;
    else continue;
    v = (v << 6) | d;
    bit += 6;
    if (bit >= 8) {
      bit -= 8;
      if (n < boy) out[n++] = (v >> bit) & 0xFF;
    }
  }
  return n;
}

static void mesaj_isle(char **a, int n) {
  const char *k = a[0];
  if (!strcmp(k, "PING")) {
    uint32_t bos = rp2040.getFreeHeap();
    y_bas("MKD2"); y_ekle(SURUM); y_ekle(mod); y_ekle(sayfa_adi()); y_ekle((long)bos); y_ekle((long)en_az_bellek);
    y_gonder();
  } else if (!strcmp(k, "MODLAR")) {
    modlari_bildir();
  } else if (!strcmp(k, "EKRANAYAR") && n >= 4) {
    int p = (int)sayi(a[1], 100);
    ayar[0] = p < 10 ? 10 : (p > 100 ? 100 : p);
    ayar[1] = ((int)sayi(a[2], 31)) & 255;
    int s = (int)sayi(a[3], 1);
    ayar[2] = s < 0 ? 0 : (s > 3 ? 3 : s);
    if (n >= 7) {
      int g = (int)sayi(a[4], 255);
      ayar[3] = (g >= 0 && g <= 23) ? g : 255;
      int b = (int)sayi(a[5], 7);
      ayar[4] = b < 0 ? 0 : (b > 23 ? 23 : b);
      int y = (int)sayi(a[6], 20);
      ayar[5] = y < 5 ? 5 : (y > 100 ? 100 : y);
    }
    if (n >= 8) {                              // 3.1: uzun basış süresi
      int u = (int)sayi(a[7], 10);
      ayar[6] = u < 4 ? 4 : (u > 30 ? 30 : u);
    }
    ayar_uygula();
    ayar_kaydet();
    y_bas("EKRANAYAR"); for (int i = 0; i < 7; i++) y_ekle(ayar[i]); y_gonder();
  } else if (!strcmp(k, "EKRANOKU")) {
    y_bas("EKRANAYAR"); for (int i = 0; i < 7; i++) y_ekle(ayar[i]); y_gonder();
  } else if (!strcmp(k, "SIS") && n >= 3) {
    int e[4];
    for (int i = 0; i < 4; i++) e[i] = n > 3 + i ? (int)sayi(a[3 + i], -1) : -1;
    ekran.sistem_ayarla((int)sayi(a[1]), (int)sayi(a[2]), e[0], e[1], e[2], e[3]);
  } else if (!strcmp(k, "TEST") && n >= 2) {
    test = !strcmp(a[1], "1");
    ekran.serit(test ? "Tuş testi" : "Tuş testi bitti", 100, 116, 139, "", 1.5f);
  } else if (!strcmp(k, "SAAT") && n >= 4) {
    ekran.saat_ayarla((int)sayi(a[1]) * 3600 + (int)sayi(a[2]) * 60 + (int)sayi(a[3]),
                      n >= 5 ? (int)sayi(a[4], -1) : -1, n >= 6 ? a[5] : "");
  } else if (!strcmp(k, "BELLEKSIFIRLA")) {
    en_az_bellek = rp2040.getFreeHeap();
  } else if (!strcmp(k, "BELLEK")) {
    yaz3("BELLEK", "açılış", acilis_watchdog ? "watchdog (takılma)" : "normal");
    yaz3("BELLEK", "çalışma süresi (sn)", (long)(millis() / 1000));
    yaz3("BELLEK", "bilgisayar kapalı sayıldı", (long)pc_kapali_sayisi_on());
    yaz3("BELLEK", "toplam (C++)", (long)rp2040.getTotalHeap());
    yaz3("BELLEK", "şu an", (long)rp2040.getFreeHeap());
    yaz3("BELLEK", "en düşük", (long)en_az_bellek);
    char h[130];
    if (son_hata(h, sizeof(h))) yaz2("KARTHATA", h);
  } else if (!strcmp(k, "MODOKU")) {
    modlari_gonder();
  } else if (!strcmp(k, "MODYAZ") && n >= 3) {
    modlari_al(a, n);
  } else if (!strcmp(k, "MODSIFIRLA")) {
    modlari_sil();
    modlari_varsayilan_kur();
    modlari_degistir();
    yaz3("MODYAZ", "TAMAM", MOD_SAYISI);
  } else if (!strcmp(k, "SAYFA") && n >= 2) {
    if (!strcmp(a[1], "medya")) ekran.sayfa_ayarla(Ekran::MEDYA);
    else if (!strcmp(a[1], "tuslar")) ekran.sayfa_ayarla(Ekran::TUSLAR);
    else if (!strcmp(a[1], "sistem")) ekran.sayfa_ayarla(Ekran::SISTEM);
    mod_sayfa[mod] = (uint8_t)(ekran.sayfa + 1);
    yaz2("SAYFA", sayfa_adi());
  } else if (!strcmp(k, "M") && n >= 8) {
    ekran.medya_ayarla(a[1], a[2], a[4], a[5][0], sayi(a[6]), sayi(a[7]));
  } else if (!strcmp(k, "ART")) {               // 2.4: kapak resmi başlıyor (96x96)
    ekran.kapak_basla();
  } else if (!strcmp(k, "AR") && n >= 3) {       // satır: y, base64 RGB565 (küçük uçlu)
    static uint8_t satir[200];
    int b = base64_coz(a[2], satir, sizeof(satir));
    ekran.kapak_satiri((int)sayi(a[1], -1), satir, b);
  } else if (!strcmp(k, "ARTYOK")) {
    ekran.kapak_yok();
  } else if (!strcmp(k, "N")) {
    ekran.medya_yok();
  } else if (!strcmp(k, "POS") && n >= 4) {
    ekran.konum_ayarla(sayi(a[1]), sayi(a[2]), a[3][0]);
  } else if (!strcmp(k, "LED") && n >= 5) {
    Renk r = {255, 255, 255}, ak[3];
    Isiklar::renk_coz(a[2], r);
    bool akis = false;
    if (n >= 6 && a[5][0]) {
      char tmp[40];
      strncpy(tmp, a[5], sizeof(tmp) - 1);
      tmp[sizeof(tmp) - 1] = 0;
      int c = 0;
      for (char *p = strtok(tmp, ","); p && c < 3; p = strtok(nullptr, ",")) {
        ak[c] = {255, 255, 255};
        Isiklar::renk_coz(p, ak[c]);
        c++;
      }
      akis = c == 3;
    }
    isik.ayarla(a[1], r, sayi(a[3], 60) / 100, sayi(a[4], 100) / 100, akis ? ak : nullptr);
    led_cevap();
  } else if (!strcmp(k, "LEDTUS") && n >= 5) {
    Renk r = {255, 255, 255}, ak[3];
    Isiklar::renk_coz(a[3], r);
    bool akis = false;
    if (n >= 6 && a[5][0]) {
      char tmp[40];
      strncpy(tmp, a[5], sizeof(tmp) - 1);
      tmp[sizeof(tmp) - 1] = 0;
      int c = 0;
      for (char *p = strtok(tmp, ","); p && c < 3; p = strtok(nullptr, ",")) {
        ak[c] = {255, 255, 255};
        Isiklar::renk_coz(p, ak[c]);
        c++;
      }
      akis = c == 3;
    }
    isik.tus_ayarla(!strcmp(a[1], "1"), a[2], r, sayi(a[4], 100) / 100, akis ? ak : nullptr);
    led_cevap();
  } else if (!strcmp(k, "LEDOKU")) {
    led_cevap();
  } else if (!strcmp(k, "LP")) {
    Renk r[4];
    uint8_t e[4][3];
    int c = 0;
    for (int i = 1; i < n && c < 4; i++) {
      if (!a[i][0]) continue;
      r[c] = {255, 255, 255};
      Isiklar::renk_coz(a[i], r[c]);
      e[c][0] = r[c].r; e[c][1] = r[c].g; e[c][2] = r[c].b;
      c++;
    }
    isik.kapak_renkleri(r, c);
    ekran.kapak_renkleri(e, c);
  } else if (!strcmp(k, "DIZIOK")) {             // 2.7: uygulama dizinin adımını bitirdi
    if (dz.uygulama_bekliyor) { dz.uygulama_bekliyor = false; dz.sonraki = millis() + 30; }
  } else if (!strcmp(k, "EQ") && n >= 9) {           // 2.7: 8 frekans bandı (0-100), bas -> tiz
    uint8_t b[8];
    for (int i = 0; i < 8; i++) {
      int v = (int)sayi(a[1 + i]);
      b[i] = v < 0 ? 0 : (v > 100 ? 100 : v);
    }
    ekran.bantlar(b);
    isik.bantlar(b);
  } else if (!strcmp(k, "LV") && n >= 3) {
    isik.seviye(sayi(a[1]) / 100, sayi(a[2]) / 100);
    ekran.seviye(sayi(a[1]) / 100, sayi(a[2]) / 100);
  } else if (!strcmp(k, "VOL") && n >= 3) {
    ses = (int)sayi(a[1]);
    sessiz = !strcmp(a[2], "1");
    if (ekran.ses_cubugu_acik()) {             // ses şeridi açıkken gerçek değeri göster
      ekran.ses_goster(ses, sessiz, 0, "Sistem");
      isik.ses_goster(ses, sessiz);
    } else {
      ekran.ses_metni(ses, sessiz);
    }
  } else if (!strcmp(k, "MIC") && n >= 2) {
    bool kapali = !strcmp(a[1], "1");
    if (kapali != isik.mik_kapali) {
      isik.mik_kapali = kapali;
      ekran.mik_ayarla(kapali);
      if (n < 3 || strcmp(a[2], "0") != 0) {     // ilk durum bildirilirken şerit çıkmasın
        if (kapali) ekran.serit("Mikrofon kapalı", 220, 38, 38, "", 1.5f);
        else ekran.serit("Mikrofon açık", 34, 197, 94, "", 1.5f);
      }
    }
  } else if (!strcmp(k, "RENK") && n >= 2) {
    Renk r = {0, 0, 0};
    Isiklar::renk_coz(a[1], r);
    char m[16] = "#";
    for (int i = 0; a[1][i] && i < 12; i++) m[i + 1] = toupper(a[1][i]), m[i + 2] = 0;
    ekran.serit(m, r.r, r.g, r.b, "kopyalandı", 3.0f);
    isik.flas_yap(r, 1.6f);
  } else if (!strcmp(k, "SIMBAS")) {          // 3.1: özel simgeler geliyor
    ozel_yazim_basla();
    memset(&sim_gecici, 0, sizeof(sim_gecici));
    sim_alinan = 0;
    yaz1("SIMHAZIR");
  } else if (!strcmp(k, "SIMAD") && n >= 2) {  // yeni simge: adı (en çok 15 harf)
    memset(&sim_gecici, 0, sizeof(sim_gecici));
    strncpy(sim_gecici.ad, a[1], OZEL_AD - 1);
    sim_alinan = 0;
  } else if (!strcmp(k, "SIMP") && n >= 3) {   // palet + piksel verisi, parça parça (base64)
    int ofs = (int)sayi(a[1]);
    uint8_t *hedef = (uint8_t *)sim_gecici.palet;
    int boy = (int)sizeof(sim_gecici) - OZEL_AD;
    if (ofs >= 0 && ofs < boy) sim_alinan = ofs + base64_coz(a[2], hedef + ofs, boy - ofs);
  } else if (!strcmp(k, "SIMTAM")) {           // bu simge bitti, sıradakine geç
    yaz3("SIMTAM", ozel_yazim_ekle(&sim_gecici) ? 1 : 0, sim_alinan);
  } else if (!strcmp(k, "SIMBIT")) {           // hepsi bitti: flash'a yaz
    bool ok = ozel_yazim_bitir();
    modlari_yukle();                           // tuşların simge numaraları yeniden çözülsün
    ekran.sayfa_yenile();
    yaz3("SIMOK", ok ? 1 : 0, ozel_sayisi());
  } else if (!strcmp(k, "SIMSIL")) {
    ozel_sil();
    modlari_yukle();
    ekran.sayfa_yenile();
    yaz3("SIMOK", 1, 0);
  } else if (!strcmp(k, "SIMOKU")) {           // karttaki özel simgelerin adları
    int sn = ozel_sayisi();
    for (int i = 0; i < sn; i++) {
      const OzelSimge *os = ozel_simge(i);
      if (os) yaz3("SIML", i, os->ad);
    }
    yaz2("SIMSON", sn);
  } else if (!strcmp(k, "BOOTSEL") && n >= 2 && !strcmp(a[1], "EVET")) {
    guncelleme_moduna_gec();                   // yazılım güncellemesi: kart USB bellek (RPI-RP2) olarak açılır
  } else if (!strcmp(k, "MOD") && n >= 2) {
    int m = (int)sayi(a[1]);
    if (m >= 0 && m < MOD_SAYISI && m != mod) mod_ayarla(m, false);
  }
}

// gelen baytları satırlara böler; tam satırları işler (en fazla sure_ms)
static int seri_oku(uint32_t sure_ms) {
  uint32_t bas = millis();
  int islenen = 0;
  while (Serial.available() > 0) {
    int c = Serial.read();
    if (c < 0) break;
    if (c == '\n') {
      if (gelen_n && gelen[gelen_n - 1] == '\r') gelen_n--;
      gelen[gelen_n] = 0;
      if (gelen_n) {
        son_mesaj = millis();
        hic_mesaj = false;
        char *alan[20];
        int n = 0;
        char *p = gelen;
        alan[n++] = p;
        while (*p && n < 20) {
          if (*p == '\t') { *p = 0; alan[n++] = p + 1; }
          p++;
        }
        mesaj_isle(alan, n);
        islenen++;
      }
      gelen_n = 0;
      if (millis() - bas > sure_ms) break;
    } else if (gelen_n < (int)sizeof(gelen) - 1) {
      gelen[gelen_n++] = (char)c;
    } else {
      gelen_n = 0;                 // satır sonu olmayan bozuk veri: at
    }
  }
  return islenen;
}

// ============================================================ TUŞ EYLEMİ
// ============================================================ MAKRO DİZİLERİ (2.7)
// "DIZI:WINDOWS+R|BEKLE:400|TXT:notepad|ENTER": adımlar sırayla, ana döngüyü bekletmeden çalışır.
// Tuş / medya / fare adımlarını kart yapar; metin yazma ve program açma (TXT: TXE: APP: ...) adımlarını
// uygulamaya "DIZIADIM" diye gönderir ve "DIZIOK" cevabını bekler (en çok 5 sn; uygulama yoksa atlanır).

static void dizi_basla(const Eylem &e) {
  if (dz.aktif) { dz.aktif = false; ekran.serit("Makro durduruldu", 100, 116, 139, "", 1.0f); return; }
  dz.p = DIZI_HAVUZ + e.kod[0];
  dz.son = dz.p + e.kod[1];
  dz.aktif = true;
  dz.uygulama_bekliyor = false;
  dz.sonraki = millis();
}

static void dizi_tik(uint32_t simdi) {
  int adim_sayisi = 0;
  while (dz.aktif && adim_sayisi++ < 8) {
    if (dz.uygulama_bekliyor) {
      if ((int32_t)(simdi - dz.bekleme_bitis) < 0) return;
      dz.uygulama_bekliyor = false;           // cevap gelmedi: devam
    }
    if ((int32_t)(simdi - dz.sonraki) < 0) return;
    if (dz.p >= dz.son) { dz.aktif = false; return; }
    const char *b = dz.p;
    const char *e = (const char *)memchr(b, '|', dz.son - b);
    if (!e) e = dz.son;
    dz.p = e < dz.son ? e + 1 : dz.son;
    char adim[220];
    int n = e - b;
    while (n > 0 && *b == ' ') { b++; n--; }
    if (n <= 0) continue;
    if (n >= (int)sizeof(adim)) n = sizeof(adim) - 1;
    memcpy(adim, b, n);
    adim[n] = 0;
    if (!strncmp(adim, "BEKLE:", 6)) {
      long ms = atol(adim + 6);
      dz.sonraki = simdi + (ms < 0 ? 0 : (ms > 10000 ? 10000 : ms));
      return;
    }
    Eylem ey = cevir(adim);
    if (ey.tur == E_YOK || ey.tur == E_MIK) {          // uygulamanın işi
      if (bagli()) {
        yaz2("DIZIADIM", adim);
        dz.uygulama_bekliyor = true;
        dz.bekleme_bitis = simdi + 5000;
        return;
      }
      continue;
    }
    if (ey.tur == E_MEDYA && (ey.kod[0] == 0xE9 || ey.kod[0] == 0xEA)) sistem_sesi(ey.kod[0] == 0xE9 ? 1 : -1);
    else eylem_calistir(ey);
    dz.sonraki = simdi + 30;                            // adımlar arası kısa ara (programlar yetişsin)
  }
}

static void tus_eylemi(int n, bool uzun, bool tekrar = false) {
  const Tus &t = MODLAR[mod].tuslar[n];
  const Eylem &e = uzun ? t.uzun : t.kisa;
  if (e.tur == E_DIZI) { if (!tekrar) dizi_basla(e); }
  else if (e.tur == E_MEDYA && (e.kod[0] == 0xE9 || e.kod[0] == 0xEA)) sistem_sesi(e.kod[0] == 0xE9 ? 1 : -1);
  else eylem_calistir(e);
  if (tekrar) yaz4("TUS", n + 1, mod, "R");
  else if (uzun) yaz4("TUS", n + 1, mod, "U");
  else yaz3("TUS", n + 1, mod);
}

// ============================================================ UYGULAMA YOKKEN TUŞ HARİTASI
// Uygulama bağlı değilken "çalan şarkı" sayfasında gösterilecek bir şey yok: tuş haritası gösterilir.
// 2.5: uygulama bağlanınca şarkı sayfasına geçilir; ya bağlantı kopmadan önce o sayfa açıktıysa ya da
// o anki modun açılış sayfası şarkı sayfasıysa. Bağlı değilken sayfayı kendin değiştirdiysen dokunulmaz.
// 2.9: tekerlek ivmesi - tıklar sıklaştıkça adım büyür
static uint32_t ivme_sol = 0, ivme_sag = 0;
static int ivmele(uint32_t &son, uint32_t simdi, int fark) {
  uint32_t dt = simdi - son;
  son = simdi;
  int kat = dt < 35 ? 5 : (dt < 70 ? 3 : (dt < 130 ? 2 : 1));
  return fark * kat;
}

// ============================================================ 3.2 (AC): KLAVYE ÜSTÜ AYAR MENÜSÜ
// Sağ tekerleğin düğmesini uzun tutunca açılır: çevirerek seç, basarak değiştir, uzun basarak kapat.
static bool menu_acik = false, menu_duzenle = false;
static int menu_secim = 0;
static uint32_t menu_son = 0;
const int MENU_SATIR = 5;

static void menu_ciz() {
  static char d[MENU_SATIR][14];
  snprintf(d[0], sizeof(d[0]), "%%%d", ayar[0]);
  snprintf(d[1], sizeof(d[1]), "%s", isik.acik ? "açık" : "kapalı");
  snprintf(d[2], sizeof(d[2]), "%s", ayar[2] == 0 ? "kapalı" : (ayar[2] == 1 ? "mod rengi" : (ayar[2] == 2 ? "beyaz" : "seviye")));
  snprintf(d[3], sizeof(d[3]), "%s", ayar[3] > 23 ? "kapalı" : "açık");
  snprintf(d[4], sizeof(d[4]), "%s", mod_kilitli ? "kilitli" : "serbest");
  static const char *adlar[MENU_SATIR] = {"Parlaklık", "Işıklar", "Ses ışığı", "Gece modu", "Mod"};
  const char *degerler[MENU_SATIR] = {d[0], d[1], d[2], d[3], d[4]};
  ekran.menu_ayarla(menu_secim, menu_duzenle, adlar, degerler, MENU_SATIR);
}

static void menu_ac(bool a) {
  menu_acik = a;
  menu_duzenle = false;
  menu_son = millis();
  ekran.menu_ac(a);
  if (a) menu_ciz();
}

// menüde çevirme: seçim ya da değer değişir
static void menu_cevir(int yon) {
  menu_son = millis();
  if (!menu_duzenle) {
    menu_secim = (menu_secim + yon + MENU_SATIR) % MENU_SATIR;
  } else {
    switch (menu_secim) {
      case 0: ayar[0] = ayar[0] + yon * 5 < 10 ? 10 : (ayar[0] + yon * 5 > 100 ? 100 : ayar[0] + yon * 5);
              parlaklik_uygula(); ayar_kaydet(); break;
      case 1: isik.ac_kapa(); break;
      case 2: ayar[2] = (ayar[2] + yon + 4) % 4; isik.ses_isik = ayar[2]; ayar_kaydet(); break;
      case 3: ayar[3] = ayar[3] > 23 ? 23 : 255; parlaklik_uygula(); ayar_kaydet(); break;
      case 4: mod_kilitli = !mod_kilitli; ekran.mod_kilidi(mod_kilitli); break;
    }
  }
  menu_ciz();
}

static bool oto_medya = false;
static Ekran::Sayfa oto_sayfa = Ekran::MEDYA;        // 2.7: bağlantı kopunca hangi sayfadan çıkıldı
static bool elle_secildi = false;

static void sayfa_baglantiya_gore(bool b) {
  if (!b) {
    if (ekran.sayfa != Ekran::TUSLAR) { oto_medya = true; oto_sayfa = ekran.sayfa; ekran.sayfa_ayarla(Ekran::TUSLAR); }
  } else {
    if ((oto_medya || MODLAR[mod].medya_ekrani) && !elle_secildi && ekran.sayfa == Ekran::TUSLAR) {
      ekran.sayfa_ayarla(oto_medya ? oto_sayfa : Ekran::MEDYA);
      yaz2("SAYFA", sayfa_adi());
    }
    oto_medya = false;
    elle_secildi = false;
  }
}

// ============================================================ BİLGİSAYAR KAPANINCA
// USB'de bilgisayar yoksa ya da uykudaysa ekran ve ışıklar söner, bilgisayar açılınca yanar.
static uint32_t usb_yok = 0;
static bool usb_yok_var = false;
static uint32_t son_usb = 0;

// ============================================================ ANA DÖNGÜ DURUMU
static uint32_t basis[8];
static bool basili[8];
static bool sol_dondu = false, sag_dondu = false;
static bool bekliyor[6];
static uint32_t bekleme[6];
static bool tekrar_var[6];
static uint32_t tekrar_z[6];
static bool kilitli = false;
static bool kilit_bas_var = false;
static uint32_t kilit_bas = 0;
static uint32_t son_gece = 0, son_olcum = 0;
static bool guncelleme_var = false;
static uint32_t guncelleme_bas = 0;
static int guncelleme_sn = -1;
static int32_t son_sol = 0, son_sag = 0;

static uint32_t pc_kapali_sayisi = 0;
static uint32_t pc_kapali_sayisi_on() { return pc_kapali_sayisi; }

static void pc_kapandi() {
  dz.aktif = false;
  pc_kapali = true;
  pc_kapali_sayisi++;
  for (int i = 0; i < 8; i++) basili[i] = false;
  for (int i = 0; i < 6; i++) { bekliyor[i] = false; tekrar_var[i] = false; }
  kilit_bas_var = false;
  if (lcd_hazir) lcd::parlaklik(0);
  serit_led.clear();
  serit_led.show();
}

static void pc_acildi() {
  pc_kapali = false;
  son_sol = teker[0].konum;
  son_sag = teker[1].konum;
  parlaklik_uygula();
  ekran.hepsini_kirlet();
}

static void ledleri_goster() {
  if (isik.sondur) {
    isik.sondur = false;
    serit_led.clear();
    serit_led.show();
  }
  if (!isik.degisti || !isik.acik) return;
  isik.degisti = false;
  float b = isik.parlaklik * isik.kisma;
  for (int i = 0; i < LED_SAYISI; i++)
    serit_led.setPixelColor(i, (uint8_t)(isik.px[i].r * b), (uint8_t)(isik.px[i].g * b), (uint8_t)(isik.px[i].b * b));
  serit_led.show();
}

// ============================================================ KURULUM
void setup() {
  mutex_init(&ekran_kilidi);
  bool wd = watchdog_caused_reboot() && watchdog_enable_caused_reboot();   // yalnızca bizim watchdog (güncelleme sonrası değil)
  Serial.begin(115200);
  Keyboard.begin();
  Mouse.begin();
  for (int i = 0; i < 8; i++) pinMode(TUS_PINLERI[i], INPUT_PULLUP);
  for (auto &t : teker) {
    pinMode(t.a, INPUT_PULLUP);
    pinMode(t.b, INPUT_PULLUP);
    t.durum = (digitalRead(t.a) << 1) | digitalRead(t.b);
  }
  attachInterrupt(digitalPinToInterrupt(ENC_SOL_A), sol_kesme, CHANGE);
  attachInterrupt(digitalPinToInterrupt(ENC_SOL_B), sol_kesme, CHANGE);
  attachInterrupt(digitalPinToInterrupt(ENC_SAG_A), sag_kesme, CHANGE);
  attachInterrupt(digitalPinToInterrupt(ENC_SAG_B), sag_kesme, CHANGE);

  serit_led.begin();
  serit_led.clear();
  serit_led.show();

  nvm_baslat();
  acilis_watchdog = wd;
  // 2.5: 2.0'ın yanlışlıkla yazdığı "watchdog" kaydını bir kez sil; artık açılış nedeni BELLEK'te gider
  { char h[130]; if (son_hata(h, sizeof(h)) && !strncmp(h, "0|watchdog", 10)) { uint8_t z[2] = {0, 0}; nvm_yaz(NVM_HATA, z, 2); } }
  if (wd) nvm_not("0|watchdog: kart takıldı, kendini yeniden başlattı");
  modlari_yukle();
  mod = kayitli_mod();

  ekran.baslat();
  isik.baslat();
  isik.medya = medya_durumu;
  mod_ayarla(mod, false, false);
  ayar_yukle();
  ayar_uygula();
  sayfa_baglantiya_gore(false);
  if (ayar[1] & B_ACILIS)           // 3.1 (AB): açılış ekranı
    ekran.uyari_sureli("Makro Klavye", "", "v" SURUM, 0xA855F7, 1.1f);
  cekirdek2_hazir = true;           // ekranı çekirdek 1 açar ve çizer
  son_sol = teker[0].konum;
  son_sag = teker[1].konum;
  en_az_bellek = rp2040.getFreeHeap();
  rp2040.wdt_begin(8000);          // 8 sn takılırsa kart kendini yeniden başlatır
}

// ============================================================ DÖNGÜ
static int govde();
static uint32_t son_nabiz = 0, nabiz_ms = 0;

void loop() {
  // watchdog: çekirdek 1 de çalışıyorsa beslenir (ekran takılırsa da kart yeniden başlar)
  uint32_t nb = c1_nabiz;
  if (nb != son_nabiz) { son_nabiz = nb; nabiz_ms = millis(); }
  if (millis() - nabiz_ms < 4000) rp2040.wdt_reset();
  mutex_enter_blocking(&ekran_kilidi);
  int bekle = govde();
  mutex_exit(&ekran_kilidi);
  if (guncelleme_istek) guncelleme_uygula();
  delay(bekle);
}

// ---- çekirdek 1: ekran
static uint32_t son_cizim1 = 0;
void setup1() {
  lcd::baslat();                    // ~1 sn (ekranın kendi bekleme süreleri); çekirdek 0 bu sırada çalışır
  while (!cekirdek2_hazir) delay(1);
  mutex_enter_blocking(&ekran_kilidi);
  ekran.hepsini_kirlet();
  bool ciz = ekran.ciz_gerekli();
  mutex_exit(&ekran_kilidi);
  if (ciz) ekran.bantlari_gonder([](int a, int b) { lcd::bant(ekran.fb, a, b); });
  lcd::parlaklik(pc_kapali ? 0.0f : istenen_parlaklik);   // ilk görüntü hazır olunca ışık yansın
  lcd_hazir = true;
}

void loop1() {
  c1_nabiz++;
  uint32_t t = millis();
  if (pc_kapali || t - son_cizim1 < 16) { delay(1); return; }     // en çok ~60 kare/sn
  mutex_enter_blocking(&ekran_kilidi);
  bool ciz = ekran.ciz_gerekli();
  mutex_exit(&ekran_kilidi);
  if (!ciz) { delay(1); return; }
  son_cizim1 = t;
  ekran.bantlari_gonder([](int a, int b) { lcd::bant(ekran.fb, a, b); });
  c1_cizim++;
}

static int govde() {
  uint32_t simdi = millis();
  nvm_tik(simdi);

  // bilgisayar açık mı (yarım saniyede bir bak; 3 sn görünmezse kapandı say)
  if (simdi - son_usb >= 500) {
    son_usb = simdi;
    if (tud_ready()) {
      usb_yok_var = false;
      if (pc_kapali) pc_acildi();
    } else if (!usb_yok_var) {
      usb_yok_var = true;
      usb_yok = simdi;
    } else if (!pc_kapali && simdi - usb_yok >= 3000) {
      pc_kapandi();
    }
  }
  tuslari_tara(simdi);
  if (pc_kapali) {
    // 2.5: uygulamadan mesaj geliyorsa bilgisayar açıktır (USB kısa süre askıda görünmüş olabilir): hemen aç
    if (Serial.available() > 0) {
      usb_yok_var = false;
      pc_acildi();
    } else {
      TusOlayi o;
      while (olay_al(o))                      // kapalıyken basılanları yok say; uykudaysa uyandır
        if (o.basildi && tud_suspended()) tud_remote_wakeup();
      return 20;
    }
  }

  // --- tuşlar ---
  TusOlayi o;
  while (olay_al(o)) {
    int n = o.no;
    if (test) {                                 // tuş testi: yalnızca bildir
      yaz3("BAS", n, o.basildi ? 1 : 0);
      if (n < 6) {
        ekran.tus_basildi(n, o.basildi);
        if (o.basildi) isik.tus(n);
      }
      continue;
    }
    if (o.basildi) {
      basis[n] = simdi;
      basili[n] = true;
      if (n < 6 && kilitli) {
        ekran.serit("Tuşlar kilitli", 100, 116, 139, "", 1.2f);
      } else if (n < 6) {
        if (!MODLAR[mod].tuslar[n].uzun_var) {                       // ikinci komut yok: hemen
          tus_eylemi(n, false);
          ekran.tus_bildir(MODLAR[mod].tuslar[n].ad);                // 3.1 (F)
        }
        else { bekliyor[n] = true; bekleme[n] = simdi; }               // kısa mı uzun mu: bırakınca ya da süre dolunca
        ekran.tus_basildi(n, true);
        isik.tus(n);
      } else if (n == SOL_TUS) {
        sol_dondu = false;
      } else if (n == SAG_TUS) {
        sag_dondu = false;
      }
      if (n >= 6 && basili[SOL_TUS] && basili[SAG_TUS]) {
        kilit_bas_var = true;                   // iki tekerlek birlikte: kilit için say
        kilit_bas = simdi;
        sol_dondu = sag_dondu = true;           // bırakınca sessiz / sayfa değişmesin
      }
    } else {
      uint32_t sure = basili[n] ? simdi - basis[n] : 0;
      basili[n] = false;
      if (n >= 6) kilit_bas_var = false;
      if (kilitli && n >= 6) {
        // kilitliyken tekerlek düğmeleri bir şey yapmaz
      } else if (n < 6) {
        tekrar_var[n] = false;
        if (bekliyor[n]) {
          bekliyor[n] = false;
          tus_eylemi(n, false);
          ekran.tus_bildir(MODLAR[mod].tuslar[n].ad);
        }
        ekran.tus_basildi(n, false);
        ekran.tus_ilerleme(n, 0);           // 2.8: bırakınca halka kapanır
      } else if (n == SOL_TUS && !sol_dondu) {
        if (sure < UZUN_BASIS) {
          medya_tusu(0xE2);                     // sessiz
          if (ses >= 0) {
            sessiz = !sessiz;                   // uygulama gerçek durumu birazdan gönderir
            ekran.ses_goster(ses, sessiz, 0, "Sistem");
            isik.ses_goster(ses, sessiz);
          }
        } else {
          medya_tusu(0xCD);                     // oynat / duraklat
        }
      } else if (n == SAG_TUS && !sag_dondu) {
        if (sure >= UZUN_BASIS) {
          menu_ac(!menu_acik);                   // 3.2 (AC): ayar menüsünü aç / kapa
        } else if (menu_acik) {
          menu_duzenle = !menu_duzenle;
          menu_son = simdi;
          menu_ciz();
        } else if (ekran.ses_acik()) {          // 3.1: mod şeridi dururken düğme modu kilitler / açar
          mod_kilitli = !mod_kilitli;
          ekran.mod_kilidi(mod_kilitli);
          ekran.serit(mod_kilitli ? "Mod kilitlendi" : "Mod kilidi açıldı", 100, 116, 139, "", 1.4f);
        } else {
          // 2.7: tuşlar -> şarkı -> sistem -> tuşlar
          ekran.sayfa_ayarla(ekran.sayfa == Ekran::TUSLAR ? Ekran::MEDYA
                             : (ekran.sayfa == Ekran::MEDYA ? Ekran::SISTEM : Ekran::TUSLAR));
          oto_medya = false;                    // sayfayı kendin seçtin: bağlantı değişince dokunulmaz
          if (!bagli()) elle_secildi = true;
          mod_sayfa[mod] = (uint8_t)(ekran.sayfa + 1);   // 2.9: bu mod bu sayfayı hatırlasın
          yaz2("SAYFA", sayfa_adi());
        }
      }
    }
  }

  // iki tekerleğe birlikte 1 sn basılı: tuş kilidi aç / kapa
  if (kilit_bas_var && simdi - kilit_bas >= 1000) {
    kilit_bas_var = false;
    kilitli = !kilitli;
    ekran.kilit_ayarla(kilitli);
    if (kilitli) ekran.serit("Tuşlar kilitlendi", 251, 191, 36, "", 1.5f);
    else ekran.serit("Kilit açıldı", 34, 197, 94, "", 1.5f);
    yaz2("KILIT", kilitli ? 1 : 0);
  }

  // tuşlar kilitliyken 1. ve 6. tuşa birlikte 3 sn basılı: yazılım güncelleme modu
  if (kilitli && basili[0] && basili[5]) {
    if (!guncelleme_var) { guncelleme_var = true; guncelleme_bas = simdi; guncelleme_sn = -1; }
    int kalan = 3 - (int)((simdi - guncelleme_bas) / 1000);
    if (kalan <= 0) {
      guncelleme_moduna_gec();
    } else if (kalan != guncelleme_sn) {       // geri sayım: bırakırsan iptal
      guncelleme_sn = kalan;
      char m[16];
      snprintf(m, sizeof(m), "%d sn", kalan);
      ekran.serit("Güncelleme modu", 56, 189, 248, m, 1.2f);
    }
  } else if (guncelleme_var) {                  // süre dolmadan bırakıldı
    guncelleme_var = false;
    ekran.serit("Güncelleme iptal", 100, 116, 139, "", 1.2f);
  }

  if (menu_acik && simdi - menu_son > 9000) menu_ac(false);   // 3.2 (AC): dokunulmazsa kapanır

  // ikinci komutu olan tuş yeterince basılı tutuldu: uzun komut (bir kez)
  for (int n = 0; n < 6; n++) {
    if (bekliyor[n]) ekran.tus_ilerleme(n, (float)(simdi - bekleme[n]) / TUS_UZUN);   // 2.8: halka dolumu
    if (bekliyor[n] && simdi - bekleme[n] >= TUS_UZUN) {
      bekliyor[n] = false;
      ekran.tus_ilerleme(n, 0);
      tus_eylemi(n, true);
      ekran.tus_bildir(MODLAR[mod].tuslar[n].uzun_ad[0] ? MODLAR[mod].tuslar[n].uzun_ad : MODLAR[mod].tuslar[n].ad);
      isik.tus(n);
      if (MODLAR[mod].tuslar[n].tekrar) { tekrar_var[n] = true; tekrar_z[n] = simdi + TEKRAR_ILK; }
    }
    if (tekrar_var[n] && (int32_t)(simdi - tekrar_z[n]) >= 0) {       // basılı tutuldukça tekrarla
      tekrar_z[n] = simdi + TEKRAR_ARA;
      tus_eylemi(n, true, true);
    }
  }

  // --- tekerlekler ---
  int32_t p = teker[0].konum;
  int fark = (p - son_sol) * SOL_YON;
  if (fark && (kilitli || test)) {
    son_sol = p;
    if (test) yaz3("ENC", "L", fark > 0 ? "+1" : "-1");
  } else if (fark) {
    son_sol = p;
    int adim = fark > 0 ? 1 : -1;
    if (basili[SOL_TUS]) {
      sol_dondu = true;
      medya_tusu(adim > 0 ? 0xB5 : 0xB6);      // şarkı atlarken ivme yok
    } else {
      sistem_sesi(ivmele(ivme_sol, simdi, fark));
    }
    yaz3("ENC", "L", adim > 0 ? "+1" : "-1");
  }

  // --- sağ tekerlek: mod ---
  p = teker[1].konum;
  fark = (p - son_sag) * SAG_YON;
  if (fark && (kilitli || test)) {
    son_sag = p;
    if (test) yaz3("ENC", "R", fark > 0 ? "+1" : "-1");
  } else if (fark) {
    son_sag = p;
    const Mod &m = MODLAR[mod];
    int hizli = ivmele(ivme_sag, simdi, fark);               // 2.9: hızlı çevirince adım büyür
    if (menu_acik) {                           // 3.2 (AC): menü açıkken tekerlek menüyü sürer
      menu_cevir(fark > 0 ? 1 : -1);
    } else if (basili[SAG_TUS] || !m.teker_var) {     // basılıyken çevirince her zaman mod değişir
      if (basili[SAG_TUS]) sag_dondu = true;
      int ad = hizli > 8 ? 8 : (hizli < -8 ? -8 : hizli);
      if (mod_kilitli) {
        ekran.serit("Mod kilitli", 100, 116, 139, "düğme: aç", 1.5f);
      } else if (basili[SAG_TUS]) {              // 3.2 (AQ): düğme basılıyken sıradaki gruba atla
        mod_ayarla(sonraki_grup(mod, ad > 0 ? 1 : -1));
      } else {
        int hedef = mod;
        for (int i = 0; i < (ad > 0 ? ad : -ad); i++) hedef = grup_icinde(hedef, ad > 0 ? 1 : -1);
        mod_ayarla(hedef);                       // 3.1: alttaki mod şeridi (liste kaldırıldı)
      }
    } else if (m.tg == 's') {
      sistem_sesi(hizli);
    } else {
      const Eylem &e = fark > 0 ? m.teker_sag : m.teker_sol;
      int k = abs(hizli) < 10 ? abs(hizli) : 10;
      for (int i = 0; i < k; i++) eylem_calistir(e);
    }
    yaz3("ENC", "R", fark > 0 ? "+1" : "-1");
  }

  // --- uygulama ---
  int islenen = seri_oku(40);
  bool yogun = islenen >= 24;
  bool b = bagli();
  if (b != ekran.bagli) {
    ekran.baglanti(b);
    sayfa_baglantiya_gore(b);
    if (!b) test = false;
  }

  // gece modu: yarım dakikada bir saate bak
  if (simdi - son_gece >= 30000) {
    son_gece = simdi;
    if (gece_mi() != gece) parlaklik_uygula();
  }

  if (simdi - son_olcum >= 2000) {
    son_olcum = simdi;
    uint32_t bos = rp2040.getFreeHeap();
    if (bos < en_az_bellek) en_az_bellek = bos;
  }

  // uyku engelleyici: dakikada bir fareyi 1 piksel oynatıp geri al
  if (uyutma && simdi - son_kipirdama > 50000) {
    son_kipirdama = simdi;
    Mouse.move(0, 1, 0);
    Mouse.move(0, -1, 0);
  }

  dizi_tik(simdi);
  ekran.tik();
  if (!yogun) {
    isik.tik();
    ledleri_goster();
  }
  return 1;
}
