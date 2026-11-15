// MAKRO KLAVYE v2 (C++) - ekran arayüzü (240x240 ST7789)
// CircuitPython sürümündeki ekran.py ile aynı görünüm. Bütün ekran bellekte (240x240x2 = 112,5 KB) çizilir;
// yalnızca değişen satırlar ekrana gönderilir.
#pragma once
#include <stdint.h>
#include "yazi.h"

uint32_t ms_simdi();   // platform (Arduino: millis)

const int EN = 240, BOY = 240;

class Ekran {
 public:
  uint16_t fb[EN * BOY];            // RGB565, ekrana gidecek bayt sırasında (büyük uçlu)

  void baslat();
  // değişen satır aralıklarını gonder(y0, y1) ile verir
  template <typename F> void gonder(F &&gonder_fn) { if (ciz_gerekli()) bantlari_gonder(gonder_fn); }
  // iki çekirdek: ciz_gerekli() kilit altında (durumu okur, fb'ye çizer),
  // bantlari_gonder() kilitsiz (yalnızca fb'yi okur; fb'ye yalnız çizen çekirdek dokunur)
  bool ciz_gerekli() { if (!kirli) return false; ciz(); kirli = false; return true; }
  template <typename F> void bantlari_gonder(F &&gonder_fn);
  void hepsini_kirlet() { tumu_kirli = true; kirli = true; }

  // ---- code.py'nin kullandığı arayüz (ekran.py ile aynı adlar)
  enum Sayfa : uint8_t { YOK = 0, TUSLAR, MEDYA, SISTEM };   // 2.7: SISTEM = canlı grafikler
  Sayfa sayfa = YOK;
  int mod = 0;
  bool mod_bildir = true;     // mod değişince altta şerit
  bool sistem_acik = true;    // tuş sayfasında işlemci / bellek
  bool bagli = false;
  bool saat_var = false;
  bool ses_acik() const { return ses_gorunur; }
  bool ses_cubugu_acik() const { return ses_gorunur && cubuk_gorunur; }

  void mod_ayarla(int m);
  void mod_kilidi(bool k);    // 2.9: mod kilidi (mod şeridi görünürken sağ düğme ile)
  // 3.2 (AC): klavye üstündeki ayar menüsü
  void menu_ac(bool a);
  bool menu_acik() const { return menu_var; }
  void menu_ayarla(int secim, bool duzenle, const char *const *satirlar, const char *const *degerler, int n);
  bool mod_kilidi_var() const { return mod_kilit; }
  void sayfa_ayarla(Sayfa s);
  void sayfa_yenile();
  void tus_basildi(int i, bool basili);
  void tus_ilerleme(int i, float t);   // 2.8: uzun komut beklerken karonun çevresinde dolum halkası (0 kapalı, 0-1 ilerleme)
  void baglanti(bool b);
  void medya_ayarla(const char *baslik, const char *sanatci, const char *kaynak, char durum, float konum, float sure);
  void medya_yok();
  // 2.4: kapak resmi (96x96, uygulama "ART" / "AR y <base64 RGB565 küçük uçlu>" / "ARTYOK" gönderir)
  void kapak_basla();
  void kapak_satiri(int y, const uint8_t *le565, int n);
  void kapak_yok();
  bool medya_var() const { return m_var; }
  char medya_durum() const { return m_durum; }
  float medya_ilerleme() const;   // 0-1 (ışıklar için)
  void konum_ayarla(float konum, float sure, char durum);
  void saat_ayarla(int32_t sn, int gun, const char *tarih);
  int32_t simdi_sn() const;       // gün içindeki saniye (saat geldiyse)
  void sistem_ayarla(int cpu, int ram, int gpu, int ind, int yuk, int pil);
  void ipucu_yaz();
  void kapak_renkleri(const uint8_t (*renk)[3], int n);
  void seviye(float sol, float sag);
  void bantlar(const uint8_t b[8]);          // 2.7: uygulamadan gerçek frekans bantları (0-100)
  void ses_goster(int ses, bool sessiz, int yon, const char *ad, float sure = 2.0f);   // ses: 0-100, -2 bilinmiyor, -1 yok
  void ses_metni(int ses, bool sessiz);     // medya sayfasındaki "Ses %50"
  void serit(const char *metin, uint8_t r, uint8_t g, uint8_t b, const char *sag, float sure);
  void mod_goster(float sure = 1.5f);
  void mik_ayarla(bool kapali);
  void kilit_ayarla(bool kilitli);
  // tam ekran uyarı (güncelleme modu); başlık nullptr ise kapanır
  void uyari(const char *baslik, const char *metin, const char *alt, uint32_t renk);
  void uyari_sureli(const char *baslik, const char *metin, const char *alt, uint32_t renk, float sn);   // 3.1: kendi kapanan (açılış ekranı)
  // 3.1: tuşa basınca komut adı tam ekran (F)
  void tus_bildir(const char *ad, float sn = 0.45f);
  bool tus_bildirimi_acik = false;      // ayarla açılıp kapanır
  bool gecis_animasyonu = true;         // 3.1: mod değişince karolar kayarak gelir (AH)
  void tik();

 private:
  bool kirli = true, tumu_kirli = true;
  uint32_t satir_ozet[BOY];

  // tuşlar
  bool basili[6] = {};
  float tus_ilerlemesi[6] = {};
  char tus_ad[6][110];
  const YaziTipi *tus_font[6];
  char tus_uz[6][60];
  char ipucu[140];
  // üst şerit
  char ust_sol[48];
  char ust_tarih[48];
  char sira[12];
  int32_t ust_dk = -1;
  int32_t saat_sn = 0;
  uint32_t saat_ms = 0;
  int saat_gun = -1;
  char saat_tarih[32];
  bool mik_kapali = false, kilitli = false;
  bool mod_kilit = false;
  bool menu_var = false, menu_duzenle = false;
  int menu_secim = 0, menu_n = 0;
  char menu_ad[6][18], menu_deger[6][14];
  bool uyari_var = false;
  uint32_t uyari_bitis = 0;
  char bildirim[64];
  uint32_t bildirim_bitis = 0;
  bool bildirim_var = false;
  uint32_t gecis_bas = 0;
  char uyari_baslik[64], uyari_metin[200], uyari_alt[80];
  uint16_t uyari_renk;
  // sistem
  bool sis_var = false;
  // 2.7: sistem grafiği (her SIS mesajı bir örnek; en yenisi sonda)
  static const int GECMIS = 224;
  uint8_t g_cpu[GECMIS], g_ram[GECMIS], g_gpu[GECMIS];
  uint16_t g_ind[GECMIS], g_yuk[GECMIS];      // kb/s / 8 (65535*8 kb/s'ye kadar)
  int g_n = 0, g_bas = 0;
  int s_cpu = 0, s_ram = 0, s_gpu = -1, s_ind = -1, s_yuk = -1;
  void sistem_sayfasi();
  void grafik(int x, int y, int w, int h, const uint16_t *v, int olcek, uint16_t renk, bool doldur);
  uint32_t sis_ms = 0;
  char sis[140];
  // medya
  bool m_var = false;
  char m_baslik[200];
  char m_sanatci[200];
  char m_kaynak[100];
  char m_durum = 0;
  float m_sure = 0, konum = 0;
  uint32_t konum_ms = 0;
  char l_kaynak[100], l_durum[40], l_ses[40], l_baslik[200], l_sanatci[200], l_gecen[16], l_sure[16];
  int ilerleme_w = 1;
  int son_ilerleme = -1;
  bool kare_cubuk = false;
  // kapak resmi
  uint16_t kapak_px[96 * 96];
  // 2.7b: medya sayfasının bulanık zemini (kapağın 12x12 blok ortalaması, koyulaştırılmış)
  uint8_t zg[12][12][3];
  bool zemin_gecerli = false;
  void zemin_kur();
  void zemini_ciz();
  uint8_t kapak_gelen[12];        // gelen satırlar (bit)
  bool kapak_var = false;         // tamamı geldi, gösteriliyor
  // kayan şarkı adı (2.4)
  bool kayan = false;
  int kayan_gen = 0;
  uint32_t kayan_bas = 0;
  int kayan_ofs = 0;
  // ses çubukları
  float eq_h[8];
  int eq_ih[8];
  uint16_t eq_renk[8];
  uint8_t kapak[4][3];
  int kapak_n = 0;
  uint32_t eq_ms = 0;
  float sol = 0, sag = 0;
  uint32_t seviye_ms = 0;
  bool seviye_var = false;
  uint8_t bant[8] = {};
  uint32_t bant_ms = 0;
  bool bant_var = false;
  // alt şerit
  bool ses_gorunur = false, cubuk_gorunur = false;
  uint32_t ses_bitis = 0;
  uint16_t serit_renk;
  uint16_t ses_yazi_renk;
  char ses_bas[80], ses_deger[24];
  int ses_bar_w = 1;
  uint16_t ses_bar_renk;

  void tuslari_yaz();
  void medyayi_yaz();
  void ust_saat_yaz();
  void ilerleme_sifirla();
  void ilerleme_guncelle(bool zorla = false);
  void cubuk_renkleri();
  void cubuklari_oynat(uint32_t ms);
  uint16_t mod_rengi() const;

  // çizim
  void ciz();
  void dikdortgen(int x, int y, int w, int h, uint16_t c);
  void cerceve_ilerleme(int x, int y, int w, int h, int kalinlik, float t, uint16_t c);   // 2.8: çevresi boyunca dolum
  void daire(int cx, int cy, int r, uint16_t c);
  void cokgen(int ox, int oy, const int8_t (*p)[2], int n, uint16_t c);
  void yazi(const YaziTipi &f, const char *s, int x, int y, float ax, float ay, uint16_t c, float aralik = 1.25f, bool ortala = false);
  int kesme_x0 = 0, kesme_x1 = EN;   // yazının çizilebileceği x aralığı (kayan yazı için)
  void kapagi_ciz();
  void mik_simgesi(int x, int y);
  void kilit_simgesi();
  void simge_ciz(int no, int x, int y, uint16_t c);
};

uint16_t renk565(uint32_t rgb);
uint16_t renk565(uint8_t r, uint8_t g, uint8_t b);

// ---- şablon: değişen satırları bul (satır özeti) ve gönder
template <typename F> void Ekran::bantlari_gonder(F &&gonder_fn) {
  int bas = -1;
  for (int y = 0; y <= BOY; y++) {
    bool degisti = false;
    if (y < BOY) {
      // FNV-1a (32 bit, kelime kelime)
      const uint32_t *p = (const uint32_t *)&fb[y * EN];
      uint32_t h = 2166136261u;
      for (int i = 0; i < EN / 2; i++) { h ^= p[i]; h *= 16777619u; }
      degisti = tumu_kirli || h != satir_ozet[y];
      satir_ozet[y] = h;
    }
    if (degisti && bas < 0) bas = y;
    if (!degisti && bas >= 0) {
      gonder_fn(bas, y - 1);
      bas = -1;
    }
  }
  tumu_kirli = false;
}
