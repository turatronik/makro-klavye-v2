// MAKRO KLAVYE v2 (C++) - modlar ve tuş komutları
#include "komutlar.h"
#include "nvm.h"
#include "depo.h"
#include "simgeler.h"
#include <ArduinoJson.h>
#include <string.h>
#include <stdlib.h>
#include <ctype.h>
#include <stdio.h>

Mod MODLAR[EN_FAZLA_MOD];
int MOD_SAYISI = 0;

// modlar.py'deki VARSAYILAN (ilk açılışta ve "Varsayılan modlara dön" deyince)
const char VARSAYILAN_MODLAR[] = R"JSON([{"a": "MEDYA", "r": "FF2878", "e": "m", "k": [["Önceki", "MEDIA_PREVIOUS"], ["Oynat / Dur", "MEDIA_PLAY_PAUSE"], ["Sonraki", "MEDIA_NEXT"], ["Sessiz", "MUTE"], ["Durdur", "MEDIA_STOP"], ["Oynatıcı", "MEDIA_PLAYER"]]}, {"a": "KISAYOL", "r": "00A0FF", "e": "t", "k": [["Kopyala", "CTRL+C"], ["Yapıştır", "CTRL+V"], ["Kes", "CTRL+X"], ["Geri al", "CTRL+Z"], ["Yinele", "CTRL+Y"], ["Kaydet", "CTRL+S"]]}, {"a": "SİSTEM", "r": "FF8C00", "e": "t", "k": [["Masaüstü", "WINDOWS+D"], ["Dosya gezgini", "WINDOWS+E"], ["Ekran al", "SHIFT+WINDOWS+S"], ["Görev yön.", "CTRL+SHIFT+ESC"], ["Pencere", "ALT+TAB"], ["Kilitle", "WINDOWS+L"]]}, {"a": "F13-F18", "r": "5AE63C", "e": "t", "k": [["F13", "F13"], ["F14", "F14"], ["F15", "F15"], ["F16", "F16"], ["F17", "F17"], ["F18", "F18"]]}])JSON";

// ---------------------------------------------------------------- HID tuş adları
// Tüketici (medya / özel) tuşları: HID Consumer Page kodları
struct AdKod { const char *ad; int16_t kod; };
static const AdKod TUKETICI[] = {
  {"MEDIA_PLAY_PAUSE", 0xCD}, {"MEDIA_PREVIOUS", 0xB6}, {"MEDIA_NEXT", 0xB5}, {"MEDIA_STOP", 0xB7},
  {"MEDIA_FAST_FORWARD", 0xB3}, {"MEDIA_REWIND", 0xB4}, {"MUTE", 0xE2}, {"VOLUME_UP", 0xE9}, {"VOLUME_DOWN", 0xEA},
  {"BRIGHTNESS_UP", 0x6F}, {"BRIGHTNESS_DOWN", 0x70}, {"CALCULATOR", 0x192}, {"EMAIL", 0x18A}, {"MY_COMPUTER", 0x194},
  {"WINDOWS_SEARCH", 0x221}, {"MEDIA_PLAYER", 0x183}, {"BROWSER_HOME", 0x223}, {"BROWSER_BACK", 0x224},
  {"BROWSER_FORWARD", 0x225}, {"BROWSER_STOP", 0x226}, {"BROWSER_REFRESH", 0x227}, {"BROWSER_FAVORITES", 0x22A},
};

// uygulamanın kısa adları -> Keycode adları
static const char *const TAKMA[][2] = {
  {"CTRL", "CONTROL"}, {"WINDOWS", "GUI"}, {"WIN", "GUI"}, {"ESC", "ESCAPE"}, {"DEL", "DELETE"},
  {"LEFT", "LEFT_ARROW"}, {"RIGHT", "RIGHT_ARROW"}, {"UP", "UP_ARROW"}, {"DOWN", "DOWN_ARROW"},
  {"PAGEUP", "PAGE_UP"}, {"PAGEDOWN", "PAGE_DOWN"}, {"RETURN", "ENTER"},
  {"0", "ZERO"}, {"1", "ONE"}, {"2", "TWO"}, {"3", "THREE"}, {"4", "FOUR"},
  {"5", "FIVE"}, {"6", "SIX"}, {"7", "SEVEN"}, {"8", "EIGHT"}, {"9", "NINE"},
};

static const char *const RAKAM[] = {"ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "ZERO"};

static const AdKod OZEL[] = {
  {"ENTER", 0x28}, {"RETURN", 0x28}, {"ESCAPE", 0x29}, {"BACKSPACE", 0x2A}, {"TAB", 0x2B}, {"SPACEBAR", 0x2C}, {"SPACE", 0x2C},
  {"MINUS", 0x2D}, {"EQUALS", 0x2E}, {"LEFT_BRACKET", 0x2F}, {"RIGHT_BRACKET", 0x30}, {"BACKSLASH", 0x31}, {"POUND", 0x32},
  {"SEMICOLON", 0x33}, {"QUOTE", 0x34}, {"GRAVE_ACCENT", 0x35}, {"COMMA", 0x36}, {"PERIOD", 0x37}, {"FORWARD_SLASH", 0x38},
  {"CAPS_LOCK", 0x39}, {"PRINT_SCREEN", 0x46}, {"SCROLL_LOCK", 0x47}, {"PAUSE", 0x48}, {"INSERT", 0x49}, {"HOME", 0x4A},
  {"PAGE_UP", 0x4B}, {"DELETE", 0x4C}, {"END", 0x4D}, {"PAGE_DOWN", 0x4E}, {"RIGHT_ARROW", 0x4F}, {"LEFT_ARROW", 0x50},
  {"DOWN_ARROW", 0x51}, {"UP_ARROW", 0x52}, {"KEYPAD_NUMLOCK", 0x53}, {"KEYPAD_FORWARD_SLASH", 0x54},
  {"KEYPAD_ASTERISK", 0x55}, {"KEYPAD_MINUS", 0x56}, {"KEYPAD_PLUS", 0x57}, {"KEYPAD_ENTER", 0x58}, {"KEYPAD_PERIOD", 0x63},
  {"KEYPAD_BACKSLASH", 0x64}, {"APPLICATION", 0x65}, {"POWER", 0x66}, {"KEYPAD_EQUALS", 0x67},
  {"LEFT_CONTROL", 0xE0}, {"CONTROL", 0xE0}, {"LEFT_SHIFT", 0xE1}, {"SHIFT", 0xE1}, {"LEFT_ALT", 0xE2}, {"ALT", 0xE2},
  {"OPTION", 0xE2}, {"LEFT_GUI", 0xE3}, {"GUI", 0xE3}, {"WINDOWS", 0xE3}, {"COMMAND", 0xE3},
  {"RIGHT_CONTROL", 0xE4}, {"RIGHT_SHIFT", 0xE5}, {"RIGHT_ALT", 0xE6}, {"RIGHT_GUI", 0xE7},
};

// Keycode adı -> HID kodu (bilinmiyorsa -1)
static int tus_kodu(const char *ad) {
  size_t n = strlen(ad);
  if (n == 1) {
    char c = ad[0];
    if (c >= 'A' && c <= 'Z') return c - 61;
    if (c >= '0' && c <= '9') return c == '0' ? 0x27 : c - 19;
    return -1;
  }
  if (ad[0] == 'F' && n <= 3 && isdigit((unsigned char)ad[1]) && (n == 2 || isdigit((unsigned char)ad[2]))) {
    int k = atoi(ad + 1);
    if (k >= 1 && k <= 12) return 0x39 + k;
    if (k >= 13 && k <= 24) return 0x5B + k;
    return -1;
  }
  for (int i = 0; i < 10; i++) if (!strcmp(ad, RAKAM[i])) return 0x1E + i;
  if (!strncmp(ad, "KEYPAD_", 7))
    for (int i = 0; i < 10; i++) if (!strcmp(ad + 7, RAKAM[i])) return 0x59 + i;
  for (auto &o : OZEL) if (!strcmp(ad, o.ad)) return o.kod;
  return -1;
}

Eylem cevir(const char *ham) {
  Eylem e;
  char kod[64];
  // kırp ve büyük harf
  while (*ham == ' ') ham++;
  size_t n = 0;
  for (; ham[n] && n < sizeof(kod) - 1; n++) kod[n] = toupper((unsigned char)ham[n]);
  while (n && kod[n - 1] == ' ') n--;
  kod[n] = 0;
  if (!n || !strcmp(kod, "NONE") || !strncmp(kod, "APP:", 4) || !strncmp(kod, "APPF:", 5) ||
      !strncmp(kod, "TXT:", 4) || !strncmp(kod, "TXE:", 4) || !strcmp(kod, "COLOR_PICK") ||
      !strncmp(kod, "PC_", 3) || !strncmp(kod, "PEN:", 4))
    return e;                                   // bunları bilgisayardaki uygulama yapar (3.2: APPF ve PEN de)
  if (!strcmp(kod, "MIC_MUTE")) { e.tur = E_MIK; return e; }
  for (auto &t : TUKETICI)
    if (!strcmp(kod, t.ad)) { e.tur = E_MEDYA; e.n = 1; e.kod[0] = t.kod; return e; }
  if (!strcmp(kod, "SCROLL_UP")) { e.tur = E_FARE_TEKER; e.n = 1; e.kod[0] = 1; return e; }
  if (!strcmp(kod, "SCROLL_DOWN")) { e.tur = E_FARE_TEKER; e.n = 1; e.kod[0] = -1; return e; }
  if (!strcmp(kod, "ZOOM_IN")) { e.tur = E_FARE_ZOOM; e.n = 1; e.kod[0] = 1; return e; }
  if (!strcmp(kod, "ZOOM_OUT")) { e.tur = E_FARE_ZOOM; e.n = 1; e.kod[0] = -1; return e; }
  if (!strcmp(kod, "MOUSE_LEFT")) { e.tur = E_FARE_TIK; e.n = 1; e.kod[0] = 1; return e; }
  if (!strcmp(kod, "MOUSE_RIGHT")) { e.tur = E_FARE_TIK; e.n = 1; e.kod[0] = 2; return e; }
  if (!strcmp(kod, "MOUSE_MIDDLE")) { e.tur = E_FARE_TIK; e.n = 1; e.kod[0] = 4; return e; }
  if (!strcmp(kod, "PIXEL_MODE")) { e.tur = E_LED; return e; }
  if (!strcmp(kod, "NOSLEEP")) { e.tur = E_UYUTMA; return e; }
  if (!strcmp(kod, "DISPLAY")) strcpy(kod, "WINDOWS+P");
  Eylem t;
  t.tur = E_TUS;
  char *p = kod;
  while (p && *p) {
    char *arti = strchr(p, '+');
    if (arti) *arti = 0;
    const char *ad = p;
    for (auto &k : TAKMA) if (!strcmp(p, k[0])) { ad = k[1]; break; }
    int c = tus_kodu(ad);
    if (c < 0 || t.n >= 6) return e;             // bilinmeyen tuş: hiçbir şey yapma
    t.kod[t.n++] = c;
    p = arti ? arti + 1 : nullptr;
  }
  return t.n ? t : e;
}

static bool renk_coz(const char *s, uint8_t &r, uint8_t &g, uint8_t &b) {
  char *son;
  long v = strtol(s ? s : "", &son, 16);
  bool oldu = s && son != s;
  if (!oldu) v = 0xFFFFFF;
  r = (v >> 16) & 255; g = (v >> 8) & 255; b = v & 255;
  return oldu;
}

// en çok n harf (UTF-8)
static void harf_kirp(char *out, const char *s, int harf, size_t boyut) {
  size_t i = 0;
  int h = 0;
  while (s[i] && h < harf) {
    size_t j = i + 1;
    while (s[j] && (((uint8_t)s[j]) & 0xC0) == 0x80) j++;
    if (j >= boyut) break;
    i = j;
    h++;
  }
  memcpy(out, s, i);
  out[i] = 0;
}

static const char *const TEKER[][3] = {
  {"k", "SCROLL_DOWN", "SCROLL_UP"}, {"z", "ZOOM_IN", "ZOOM_OUT"}, {"s", "VOLUME_UP", "VOLUME_DOWN"},
  {"b", "CTRL+TAB", "CTRL+SHIFT+TAB"}, {"g", "CTRL+Y", "CTRL+Z"},
};

char DIZI_HAVUZ[DIZI_HAVUZ_BOY];
static int havuz_n = 0;

// "DIZI:" ile başlıyorsa metni havuza koyar (büyük harfe çevirmeden: içinde yazılacak metin olabilir)
static Eylem eylem_coz(const char *ham) {
  if (!strncmp(ham, "DIZI:", 5)) {
    Eylem e;
    const char *d = ham + 5;
    int n = strlen(d);
    if (n == 0 || havuz_n + n + 1 > DIZI_HAVUZ_BOY) return e;       // yer yok: hiçbir şey yapmaz
    memcpy(DIZI_HAVUZ + havuz_n, d, n + 1);
    e.tur = E_DIZI;
    e.n = 2;
    e.kod[0] = havuz_n;
    e.kod[1] = n;
    havuz_n += n + 1;
    return e;
  }
  return cevir(ham);
}

static const char *metin(JsonVariantConst v, char *tmp, size_t n) {
  if (v.is<const char *>()) return v.as<const char *>();
  if (v.isNull()) return "";
  serializeJson(v, tmp, n);       // sayı vb.
  return tmp;
}

bool modlari_kur(char *json, char *hata, size_t hboy) {
  JsonDocument d;
  DeserializationError err = deserializeJson(d, json);   // char*: yerinde çözüm, kopya yok
  if (err) { snprintf(hata, hboy, "%s", err.c_str()); return false; }
  JsonArrayConst liste = d.as<JsonArrayConst>();
  if (liste.isNull() || liste.size() == 0) { snprintf(hata, hboy, "en az bir mod olmalı"); return false; }
  havuz_n = 0;
  // JSON bu noktadan sonra hatasız işlenir: doğrudan MODLAR'a yazılabilir (ayrı kopya 15 KB tutuyordu)
  int sayi = 0;
  char tmp[32];
  for (JsonVariantConst mv : liste) {
    if (sayi >= EN_FAZLA_MOD) break;
    JsonObjectConst m = mv.as<JsonObjectConst>();
    Mod &md = MODLAR[sayi];
    memset(&md, 0, sizeof(md));
    const char *a = m["a"].is<const char *>() ? m["a"].as<const char *>() : "";
    harf_kirp(md.ad, *a ? a : "MOD", 12, sizeof(md.ad));
    renk_coz(m["r"].is<const char *>() ? m["r"].as<const char *>() : "FFFFFF", md.r, md.g, md.b);
    md.medya_ekrani = m["e"].is<const char *>() && !strcmp(m["e"].as<const char *>(), "m");
    // 3.2: modun kendi zemini (ze), tuş sayfası düzeni (dz) ve grubu (g)
    if (m["ze"].is<const char *>()) md.zemin_var = renk_coz(m["ze"].as<const char *>(), md.zr, md.zg, md.zb);
    if (m["dz"].is<int>()) { int dz = m["dz"].as<int>(); md.duzen = (uint8_t)(dz == 1 ? 1 : 0); }
    if (m["g"].is<const char *>()) harf_kirp(md.grup, m["g"].as<const char *>(), 12, sizeof(md.grup));
    JsonArrayConst k = m["k"].as<JsonArrayConst>();
    for (int i = 0; i < 6; i++) {
      Tus &t = md.tuslar[i];
      JsonArrayConst ti;
      if (!k.isNull() && (int)k.size() > i) ti = k[i].as<JsonArrayConst>();
      t.simge = -1;
      if (ti.isNull() || ti.size() < 2) {
        t.ad[0] = 0;
        t.kisa = Eylem();
        continue;
      }
      // 2.7: 6. alan simge adı ("oynat", "mikrofon"...); [ad, kod, uzunKod, uzunAd, tekrar, simge]
      if (ti.size() >= 6) t.simge = simge_bul(metin(ti[5], tmp, sizeof(tmp)));
      harf_kirp(t.ad, metin(ti[0], tmp, sizeof(tmp)), 16, sizeof(t.ad));
      t.kisa = eylem_coz(metin(ti[1], tmp, sizeof(tmp)));
      const char *uz = ti.size() >= 3 ? metin(ti[2], tmp, sizeof(tmp)) : "";
      t.uzun_var = *uz && strcmp(uz, "NONE") != 0;
      if (t.uzun_var) {
        t.uzun = eylem_coz(uz);
        if (ti.size() >= 4) harf_kirp(t.uzun_ad, metin(ti[3], tmp, sizeof(tmp)), 16, sizeof(t.uzun_ad));
        else strcpy(t.uzun_ad, "Uzun");
        t.tekrar = ti.size() >= 5 && ti[4].is<int>() && ti[4].as<int>() == 1;
      }
    }
    const char *tg = m["t"].is<const char *>() ? m["t"].as<const char *>() : "m";
    md.tg = *tg ? tg[0] : 'm';
    md.teker_var = false;
    for (auto &tk : TEKER)
      if (md.tg == tk[0][0]) { md.teker_var = true; md.teker_sag = cevir(tk[1]); md.teker_sol = cevir(tk[2]); }
    if (md.tg == 'o') {
      md.teker_var = true;
      md.teker_sag = cevir(m["tr"].is<const char *>() ? m["tr"].as<const char *>() : "");
      md.teker_sol = cevir(m["tl"].is<const char *>() ? m["tl"].as<const char *>() : "");
    }
    if (m["l"].is<const char *>() && *m["l"].as<const char *>()) {
      md.isik_var = true;
      strncpy(md.isik_efekt, m["l"].as<const char *>(), sizeof(md.isik_efekt) - 1);
      if (m["lr"].is<const char *>() && *m["lr"].as<const char *>()) {
        md.isik_renk_var = true;
        renk_coz(m["lr"].as<const char *>(), md.ir, md.ig, md.ib);
      }
    }
    sayi++;
  }
  MOD_SAYISI = sayi;
  return true;
}

char MOD_TAMPON[MODLAR_EN_COK + 4];

// 2.7: modlar önce depodan (flash bölgesi) okunur; yoksa eski yerden (4 KB kalıcı bellek, CircuitPython biçimi)
const char *kayitli_mod_metni(int &n) {
  const char *d = depo_oku(n);
  if (d) return d;
  const uint8_t *b = nvm_veri();
  if (b[NVM_MOD] != NVM_MOD_IMZA) return nullptr;
  n = (b[NVM_MOD + 1] << 8) | b[NVM_MOD + 2];
  if (n == 0 || NVM_MOD + 3 + n > NVM_HATA) return nullptr;
  return (const char *)(b + NVM_MOD + 3);
}

bool modlari_yaz(const char *json, size_t n, char *hata, size_t hboy) {
  if (n > (size_t)MODLAR_EN_COK) { snprintf(hata, hboy, "modlar çok uzun"); return false; }
  if (!depo_yaz(json, n)) { snprintf(hata, hboy, "depoya yazılamadı"); return false; }
  if (NVM_MOD + 3 + n <= (size_t)NVM_HATA) {
    // küçükse eski yere de yazılır: CircuitPython'a geri dönülürse modlar orada da bulunur
    uint8_t bas[3] = {0, (uint8_t)(n >> 8), (uint8_t)(n & 255)};
    nvm_yaz(NVM_MOD, bas, 3);
    nvm_yaz(NVM_MOD + 3, (const uint8_t *)json, n);
    nvm_yaz1(NVM_MOD, NVM_MOD_IMZA);
  } else if (nvm_oku(NVM_MOD) == NVM_MOD_IMZA) {
    nvm_yaz1(NVM_MOD, 0);              // eski yerdeki kopya artık bayat: geçersiz kıl
  }
  return true;
}

void modlari_sil() {
  depo_sil();
  if (nvm_oku(NVM_MOD) != 0) nvm_yaz1(NVM_MOD, 0);
}

void modlari_varsayilan_kur() {
  char hata[48];
  strcpy(MOD_TAMPON, VARSAYILAN_MODLAR);
  modlari_kur(MOD_TAMPON, hata, sizeof(hata));
}

void modlari_yukle() {
  int n = 0;
  const char *m = kayitli_mod_metni(n);
  char hata[48];
  if (m) {
    // flash'taki metin 0 ile bitmez: kopyala
    memcpy(MOD_TAMPON, m, n);
    MOD_TAMPON[n] = 0;
    if (modlari_kur(MOD_TAMPON, hata, sizeof(hata))) return;
  }
  modlari_varsayilan_kur();
}
