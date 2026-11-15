// MAKRO KLAVYE v2 (C++) - modlar ve tuş komutları
// Modlar kalıcı bellekte JSON olarak durur (CircuitPython sürümüyle aynı yer ve biçim);
// yoksa VARSAYILAN_MODLAR kullanılır. Komut adları uygulamadakiyle aynı.
#pragma once
#include <stdint.h>
#include <stddef.h>

enum EylemTuru : uint8_t {
  E_YOK = 0,      // klavye bir şey yapmaz (Uygulama aç, Metin yaz, Renk seçici: uygulama yapar)
  E_TUS,          // kodlar[0..n): HID tuş kodları (0xE0-0xE7 değiştiriciler)
  E_MEDYA,        // kodlar[0]: tüketici (Consumer) kodu
  E_FARE_TIK,     // kodlar[0]: düğme (1 sol, 2 sağ, 4 orta)
  E_FARE_TEKER,   // kodlar[0]: +1 / -1 (int16)
  E_FARE_ZOOM,    // Ctrl + tekerlek
  E_LED,          // ışıkları aç / kapa
  E_UYUTMA,       // uyku engelleyici aç / kapa
  E_MIK,          // mikrofon (uygulama yapar; ışık için işaret)
  E_DIZI,         // 2.7: makro dizisi; kod[0] = DIZI_HAVUZ içindeki yer, kod[1] = uzunluk
};

struct Eylem {
  uint8_t tur = E_YOK;
  uint8_t n = 0;
  int16_t kod[6] = {0, 0, 0, 0, 0, 0};
};

struct Tus {
  char ad[52];        // ekranda görünen ad (en çok 16 harf)
  Eylem kisa;
  bool uzun_var;
  Eylem uzun;         // basılı tutunca
  char uzun_ad[52];
  bool tekrar;        // uzun komut basılı tutuldukça tekrarlansın
  int16_t simge;      // 2.7: tuş simgesi (simgeler.h), -1 yok; 3.1: 1000+ özel simge
};

struct Mod {
  char ad[40];        // en çok 12 harf
  uint8_t r, g, b;
  bool medya_ekrani;  // açılışta çalan şarkı sayfası
  Tus tuslar[6];
  char tg;            // sağ tekerlek görevi: m k z s b g o
  bool teker_var;     // false: mod değiştirir
  Eylem teker_sag, teker_sol;
  bool isik_var;      // modun kendi ışığı
  char isik_efekt[16];
  bool isik_renk_var;
  uint8_t ir, ig, ib;
  // 3.2: modun kendi ekran görünümü
  bool zemin_var;     // modun kendi arka plan rengi
  uint8_t zr, zg, zb;
  uint8_t duzen;      // 0: 2x3 karo, 1: liste, 2: tek büyük karo
  char grup[14];      // mod grubu (boş: grupsuz)
};

const int EN_FAZLA_MOD = 16;           // 2.7: 10 -> 16
const int MODLAR_EN_COK = 20480;       // mod JSON'unun en çok boyutu (bayt); eskiden ~3,9 KB idi
extern char MOD_TAMPON[MODLAR_EN_COK + 4];   // ortak tampon: MODYAZ parçaları ve açılışta okuma
extern Mod MODLAR[EN_FAZLA_MOD];
extern int MOD_SAYISI;

const int NVM_MOD = 64;
const uint8_t NVM_MOD_IMZA = 0xC5;
const int NVM_HATA = 3968;

extern const char VARSAYILAN_MODLAR[];

Eylem cevir(const char *kod);
// 2.7: "DIZI:adım|adım|..." metinleri burada durur (her modlari_kur'da yeniden dolar)
const int DIZI_HAVUZ_BOY = 8192;
extern char DIZI_HAVUZ[DIZI_HAVUZ_BOY];
// JSON metninden modları kurar; hata varsa false (hata metni hata'ya).
// Belleği az kullanmak için JSON yerinde çözülür: json İÇERİĞİ DEĞİŞİR (bir daha kullanılamaz).
bool modlari_kur(char *json, char *hata, size_t hboy);
// VARSAYILAN_MODLAR'ı (MOD_TAMPON üzerinden) kurar
void modlari_varsayilan_kur();
// kalıcı bellekteki JSON metni (yoksa nullptr); uzunluk n'ye
const char *kayitli_mod_metni(int &n);
bool modlari_yaz(const char *json, size_t n, char *hata, size_t hboy);
void modlari_sil();
// açılışta: kayıtlıları yükler, olmazsa varsayılanlar
void modlari_yukle();
