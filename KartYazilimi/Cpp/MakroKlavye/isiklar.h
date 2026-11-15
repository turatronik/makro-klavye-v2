// MAKRO KLAVYE v2 (C++) - RGB LED'ler (18x WS2812B)
// 0-11: taban LED'leri (zincir sırası, klavyenin çevresinde), 12-17: tuş LED'leri
// CircuitPython sürümündeki isiklar.py ile aynı efektler ve aynı kalıcı bellek düzeni.
#pragma once
#include <stdint.h>

const int LED_SAYISI = 18;
const int TABAN = 12;

struct Renk { uint8_t r, g, b; };

struct Bolge {
  uint8_t efekt = 1;                 // "sabit"
  Renk renk = {168, 85, 247};
  float hiz = 1.0f;
  Renk akis[3] = {{255, 45, 85}, {168, 85, 247}, {255, 255, 255}};
  double t = 0;
};

class Isiklar {
 public:
  Renk px[LED_SAYISI];               // tik() sonrası gösterilecek renkler (parlaklık uygulanmamış)
  float parlaklik = 0.35f;
  float kisma = 1.0f;                // gece modu
  bool acik = true;
  int ses_isik = 1;                  // ses değişince: 0 gösterme, 1 mod rengi, 2 beyaz, 3 seviyeye göre
  bool mik_kapali = false;
  uint8_t mik_tuslari = 0;           // bit maskesi
  bool degisti = false;              // tik() yeni kare üretti (gösterilmeli)
  bool sondur = false;               // kapatıldı: bir kez siyah gösterilmeli

  // müzik bilgisi (code.py verir)
  bool (*medya)(float &ilerleme) = nullptr;

  void baslat();
  bool ayarla(const char *efekt, Renk renk, float parlaklik, float hiz, const Renk *akis, bool kaydet = true);
  void tus_ayarla(bool ayri, const char *efekt, Renk renk, float hiz, const Renk *akis, bool kaydet = true);
  void mod_isigi(const char *efekt, const Renk *renk);    // efekt nullptr: genel ayara dön
  void kis(float k) { kisma = k; }
  // "LED" cevabının alanları: efekt renk parlaklık hız akış ayrı tefekt trenk thız takış
  void ayar_metni(char *out, int boy);
  void mod_rengi(Renk r);
  bool dalga_acik = true;              // 3.1 (AN): basınca taban LED'lerinde yayılan dalga
  void tus(int i) {
    if (i < 0 || i >= 6) return;
    parla[i] = 1.0f;
    if (dalga_acik) { dalga_t = 0; dalga_var = true; dalga_kaynak = i * 2; }
  }
  void ses_goster(int seviye, bool sessiz);
  void flas_yap(Renk r, float sure);
  void kapak_renkleri(const Renk *r, int n);
  void seviye(float sol, float sag);
  void bantlar(const uint8_t b[8]);   // 2.7: ritim efekti vuruşu gerçek bas bandından yakalar
  bool ac_kapa();
  void tik();
  static bool renk_coz(const char *s, Renk &r);

 private:
  Renk renk_mod = {255, 255, 255};
  float parla[6] = {};
  uint32_t akis_bas = 0;
  bool akis_var = false;
  int ses_sev = -1;
  bool ses_sessiz = false;
  uint32_t ses_bitis = 0;
  bool flas_var = false;
  Renk flas_renk;
  uint32_t flas_bitis = 0;
  bool genel_var = false;
  uint8_t genel_efekt;
  Renk genel_renk;
  uint32_t son = 0;
  bool son_var = false;
  // ayarlar
  uint8_t efekt = 0;                 // "modrengi"
  Renk renk = {168, 85, 247};
  float hiz = 1.0f;
  double t = 0;
  Renk akis[3] = {{255, 45, 85}, {168, 85, 247}, {255, 255, 255}};
  bool tus_ayri = false;
  Bolge tb;
  // müzik
  Renk kapak[4];
  int kapak_n = 0;
  float sol = 0, sag = 0;
  uint32_t seviye_ms = 0;
  bool seviye_var = false;
  float bas_ort = 0.12f;
  uint32_t bant_ms = 0;
  bool bant_var = false;
  float ort = 0.2f, zarf = 0;
  int vurus = 0;
  uint32_t son_vurus = 0;
  uint32_t kayit = 0;
  bool kayit_var = false;
  float konfeti[LED_SAYISI][2];
  float dalga_t = 0;
  bool dalga_var = false;
  int dalga_kaynak = 0;
  float tohum[LED_SAYISI];

  void kaydet();
  void yukle();
  void vurus_bul(uint32_t ms, float dt);
  Renk hesapla(int i, double simdi, uint8_t e, Renk r, const Renk *ak, double T, float zhiz);
};

int efekt_no(const char *ad);    // bilinmiyorsa -1
const char *efekt_adi(int i);
