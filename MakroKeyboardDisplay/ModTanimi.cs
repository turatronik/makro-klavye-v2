using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace MakroKeyboardDisplay
{
    /// <summary>Klavyenin bir modu (kart 1.11+ "MODJ" / "MODYAZ" ile gelip giden JSON'un karşılığı).</summary>
    public sealed class ModTanimi
    {
        public const int EnFazlaMod = 16;          // kart 2.7+: 16 (eski kartlar 10)
        public const int AdUzunlugu = 12;
        public const int TusAdiUzunlugu = 14;

        public string Ad = "MOD";
        public Color Renk = Color.FromArgb(168, 85, 247);
        public string Ekran = "tuslar";                     // açılışta ekranda: "medya" / "tuslar"
        // uygulama 3.2 / kart 3.2: modun kendi ekran görünümü ve grubu
        public Color? Zemin;                                // null: varsayılan koyu zemin
        public int Duzen;                                   // 0: 2x3 karo, 1: liste, 2: tek büyük
        public string Grup = "";                            // mod grubu (boş: grupsuz)
        // uygulama 2.4 / kart 1.14: sağ tekerlek görevi. "m" mod değiştir, "k" kaydır, "z" yakınlaştır, "s" ses,
        // "b" sekmeler, "g" geri al / yinele, "o" özel (TekerSag / TekerSol kısayolları)
        public string Tekerlek = "m";
        public string TekerSag = "", TekerSol = "";
        // uygulama 2.5 / kart 1.20: modun kendi ışığı ("" = Işık sekmesindeki genel ayar)
        public string Isik = "";
        public Color IsikRenk = Color.FromArgb(255, 140, 0);
        public TusTanimi[] Tuslar = Enumerable.Range(0, 6).Select(_ => new TusTanimi()).ToArray();

        public ModTanimi Kopya() => new ModTanimi
        {
            Ad = Ad, Renk = Renk, Ekran = Ekran, Tekerlek = Tekerlek, TekerSag = TekerSag, TekerSol = TekerSol,
            Isik = Isik, IsikRenk = IsikRenk,
            Tuslar = Tuslar.Select(t => new TusTanimi { Ad = t.Ad, Kod = t.Kod, UzunKod = t.UzunKod, Tekrar = t.Tekrar, Simge = t.Simge }).ToArray(),
            Zemin = Zemin, Duzen = Duzen, Grup = Grup,
        };

        public static List<ModTanimi> Kopya(List<ModTanimi> liste) => liste?.Select(m => m.Kopya()).ToList();

        /// <summary>İki liste aynı mı (kaydedilmemiş değişiklik var mı diye bakmak için).</summary>
        public static bool Ayni(List<ModTanimi> a, List<ModTanimi> b) => a != null && b != null && Json(a) == Json(b);

        // ------------------------------------------------------------ JSON
        // [{"a":"MEDYA","r":"FF2878","e":"m","k":[["Önceki","MEDIA_PREVIOUS"], ...6]}, ...]
        public static string Json(List<ModTanimi> liste)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < liste.Count; i++)
            {
                var m = liste[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"a\":").Append(Metin(Temizle(m.Ad, AdUzunlugu)))
                  .Append(",\"r\":\"").Append($"{m.Renk.R:X2}{m.Renk.G:X2}{m.Renk.B:X2}")
                  .Append("\",\"e\":\"").Append(m.Ekran == "medya" ? "m" : "t").Append('"');
                if (!string.IsNullOrEmpty(m.Tekerlek) && m.Tekerlek != "m")
                {
                    sb.Append(",\"t\":").Append(Metin(m.Tekerlek));
                    if (m.Tekerlek == "o")
                        sb.Append(",\"tr\":").Append(Metin(m.TekerSag ?? "")).Append(",\"tl\":").Append(Metin(m.TekerSol ?? ""));
                }
                if (!string.IsNullOrEmpty(m.Isik))
                    sb.Append(",\"l\":").Append(Metin(m.Isik)).Append(",\"lr\":\"").Append($"{m.IsikRenk.R:X2}{m.IsikRenk.G:X2}{m.IsikRenk.B:X2}").Append('"');
                if (m.Zemin.HasValue)                        // kart 3.2
                    sb.Append(",\"ze\":\"").Append($"{m.Zemin.Value.R:X2}{m.Zemin.Value.G:X2}{m.Zemin.Value.B:X2}").Append('"');
                if (m.Duzen > 0) sb.Append(",\"dz\":").Append(m.Duzen);
                if (!string.IsNullOrEmpty(m.Grup)) sb.Append(",\"g\":").Append(Metin(Temizle(m.Grup, 12)));
                sb.Append(",\"k\":[");
                for (int t = 0; t < 6; t++)
                {
                    if (t > 0) sb.Append(',');
                    var tus = m.Tuslar[t];
                    sb.Append('[').Append(Metin(Temizle(tus.Ad, TusAdiUzunlugu))).Append(',')
                      .Append(Metin(string.IsNullOrEmpty(tus.Kod) ? "NONE" : tus.Kod));
                    bool uzunVar = !string.IsNullOrEmpty(tus.UzunKod) && tus.UzunKod != "NONE";
                    bool simgeVar = !string.IsNullOrEmpty(tus.Simge);
                    if (uzunVar)
                    {
                        sb.Append(',').Append(Metin(tus.UzunKod));             // kart 1.14+: uzun basınca
                        sb.Append(',').Append(Metin(Temizle(KomutKatalogu.KisaAd(tus.UzunKod), TusAdiUzunlugu)));   // kart 1.19+: ekranda adı
                        if (tus.Tekrar || simgeVar) sb.Append(tus.Tekrar ? ",1" : ",0");   // kart 1.22+: basılı tutuldukça tekrarla
                    }
                    else if (simgeVar)
                        sb.Append(",\"\",\"\",0");                              // uzun komut yok; simge 6. alanda durur
                    if (simgeVar) sb.Append(',').Append(Metin(tus.Simge));       // kart 2.7+: tuş simgesi
                    sb.Append(']');
                }
                sb.Append("]}");
            }
            return sb.Append(']').ToString();
        }

        /// <summary>JSON metni; Türkçe harfler olduğu gibi kalır (kart UTF-8 okur), tırnak ve ters bölü kaçırılır.</summary>
        static string Metin(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 32) sb.Append(' ');
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        public static string Temizle(string s, int uzunluk)
        {
            s = (s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
            return s.Length > uzunluk ? s.Substring(0, uzunluk) : s;
        }

        public static List<ModTanimi> Coz(string json)
        {
            var liste = new List<ModTanimi>();
            using var belge = JsonDocument.Parse(json);
            foreach (var e in belge.RootElement.EnumerateArray())
            {
                var m = new ModTanimi();
                if (e.TryGetProperty("a", out var a)) m.Ad = a.GetString() ?? "MOD";
                if (e.TryGetProperty("r", out var r)) m.Renk = LedAyari.RenkCoz(r.GetString(), m.Renk);
                if (e.TryGetProperty("e", out var ek)) m.Ekran = ek.GetString() == "m" ? "medya" : "tuslar";
                if (e.TryGetProperty("t", out var tg) && tg.ValueKind == JsonValueKind.String) m.Tekerlek = tg.GetString() ?? "m";
                if (e.TryGetProperty("tr", out var tr) && tr.ValueKind == JsonValueKind.String) m.TekerSag = tr.GetString() ?? "";
                if (e.TryGetProperty("tl", out var tl) && tl.ValueKind == JsonValueKind.String) m.TekerSol = tl.GetString() ?? "";
                if (e.TryGetProperty("l", out var li) && li.ValueKind == JsonValueKind.String) m.Isik = li.GetString() ?? "";
                if (e.TryGetProperty("lr", out var lr) && lr.ValueKind == JsonValueKind.String) m.IsikRenk = LedAyari.RenkCoz(lr.GetString(), m.IsikRenk);
                if (e.TryGetProperty("ze", out var ze) && ze.ValueKind == JsonValueKind.String) m.Zemin = LedAyari.RenkCoz(ze.GetString(), Color.Black);
                if (e.TryGetProperty("dz", out var dz) && dz.ValueKind == JsonValueKind.Number) m.Duzen = Math.Clamp(dz.GetInt32(), 0, 1);
                if (e.TryGetProperty("g", out var gr) && gr.ValueKind == JsonValueKind.String) m.Grup = gr.GetString() ?? "";
                if (e.TryGetProperty("k", out var k) && k.ValueKind == JsonValueKind.Array)
                {
                    int i = 0;
                    foreach (var t in k.EnumerateArray())
                    {
                        if (i >= 6) break;
                        if (t.ValueKind == JsonValueKind.Array && t.GetArrayLength() >= 2)
                            m.Tuslar[i] = new TusTanimi
                            {
                                Ad = t[0].GetString() ?? "", Kod = t[1].GetString() ?? "NONE",
                                UzunKod = t.GetArrayLength() >= 3 ? t[2].GetString() ?? "" : "",
                                Tekrar = t.GetArrayLength() >= 5 && t[4].ValueKind == JsonValueKind.Number && t[4].GetInt32() == 1,
                                Simge = t.GetArrayLength() >= 6 && t[5].ValueKind == JsonValueKind.String ? t[5].GetString() ?? "" : "",
                            };
                        i++;
                    }
                }
                liste.Add(m);
            }
            return liste;
        }
    }

    /// <summary>
    /// Bilgisayarda yüklü uygulamalar (Microsoft Store uygulamaları dahil): Başlat menüsünün kullandığı
    /// "shell:AppsFolder" klasöründen okunur (MakroKeyboard v1'deki uygulama seçiciyle aynı yol).
    /// </summary>
    public static class YukluUygulamalar
    {
        public sealed class Uygulama
        {
            public string Ad, Hedef;
            public bool Store;
        }

        public static List<Uygulama> Liste;          // null: okunmadı
        public static bool Okunuyor;
        public static event Action Okundu;

        public static string AdBul(string hedef) =>
            Liste?.FirstOrDefault(u => string.Equals(u.Hedef, hedef, StringComparison.OrdinalIgnoreCase))?.Ad;

        /// <summary>Arka planda okur; bitince Okundu olayı (arka plan iş parçacığında) çağrılır.</summary>
        public static void Oku()
        {
            if (Okunuyor || Liste != null) return;
            Okunuyor = true;
            var t = new System.Threading.Thread(() =>
            {
                List<Uygulama> sonuc;
                try { sonuc = Tara(); }
                catch (Exception ex)
                {
                    Gunluk.Hata("uygulama listesi", ex);
                    sonuc = new List<Uygulama>();
                }
                Liste = sonuc;
                Okunuyor = false;
                try { Okundu?.Invoke(); } catch { }
            })
            { IsBackground = true, Name = "Uygulama listesi" };
            t.SetApartmentState(System.Threading.ApartmentState.STA);     // Shell COM nesneleri STA ister
            t.Start();
        }

        static List<Uygulama> Tara()
        {
            var sonuc = new List<Uygulama>();
            var bf = System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.GetProperty;
            var shellTipi = Type.GetTypeFromProgID("Shell.Application");
            if (shellTipi == null) return sonuc;
            object shell = Activator.CreateInstance(shellTipi);
            object klasor = shellTipi.InvokeMember("NameSpace", bf, null, shell, new object[] { "shell:AppsFolder" });
            if (klasor == null) return sonuc;
            object ogeler = klasor.GetType().InvokeMember("Items", bf, null, klasor, null);
            int sayi = (int)ogeler.GetType().InvokeMember("Count", bf, null, ogeler, null);
            var goruldu = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sayi; i++)
            {
                object oge = ogeler.GetType().InvokeMember("Item", bf, null, ogeler, new object[] { i });
                var ad = (oge.GetType().InvokeMember("Name", bf, null, oge, null) as string ?? "").Trim();
                var yol = (oge.GetType().InvokeMember("Path", bf, null, oge, null) as string ?? "").Trim();
                if (ad == "" || yol == "") continue;
                bool dosya = false;
                try { dosya = System.IO.File.Exists(yol); } catch { }
                var hedef = dosya ? yol : @"shell:AppsFolder\" + yol;
                if (!goruldu.Add(ad + "|" + hedef)) continue;
                sonuc.Add(new Uygulama { Ad = ad, Hedef = hedef, Store = !dosya && yol.Contains('!') });
            }
            sonuc.Sort((a, b) => string.Compare(a.Ad, b.Ad, StringComparison.CurrentCultureIgnoreCase));
            return sonuc;
        }
    }

    public sealed class TusTanimi
    {
        public string Ad = "";              // klavye ekranında görünen
        public string Kod = "NONE";         // KomutKatalogu kodu ya da "CTRL+SHIFT+K" gibi kısayol
        public string UzunKod = "";         // uygulama 2.4 / kart 1.14: basılı tutunca (boş: yok, tuş beklemeden çalışır)
        public string Simge = "";           // uygulama 2.9 / kart 2.7: tuş simgesi (TusSimgeleri adı; boş: yok)
        public bool Tekrar;                 // uygulama 2.6 / kart 1.22: uzun komut basılı tutuldukça tekrarlansın (ör. ses açma)
    }

    /// <summary>
    /// Tuşlara atanabilen komutlar: MakroKeyboard (v1) uygulamasındaki kategoriler ve adlar.
    /// Makro, metin yazma ve uygulama açma yok (klavye kendi başına çalışabilsin diye).
    /// </summary>
    public static class KomutKatalogu
    {
        public static readonly Dictionary<string, string> Adlar = new()
        {
            ["WINDOWS+D"] = "Masaüstü", ["CTRL+C"] = "Kopyala", ["CTRL+V"] = "Yapıştır", ["ALT+TAB"] = "Alt + Tab",
            ["DISPLAY"] = "Ekran Modu", ["PIXEL_MODE"] = "LED Aç / Kapat",
            ["CTRL+X"] = "Kes", ["CTRL+Z"] = "Geri Al", ["CTRL+Y"] = "Yinele", ["CTRL+A"] = "Tümünü Seç",
            ["CTRL+S"] = "Dosyayı Kaydet", ["CTRL+F"] = "Bul",
            ["MEDIA_PLAY_PAUSE"] = "Oynat/Durdur", ["MEDIA_PREVIOUS"] = "Önceki Şarkı", ["MEDIA_NEXT"] = "Sonraki Şarkı",
            ["MEDIA_STOP"] = "Durdur", ["MEDIA_FAST_FORWARD"] = "İleri Sar", ["MEDIA_REWIND"] = "Geri Sar",
            ["MUTE"] = "Sustur", ["VOLUME_UP"] = "Ses Artır", ["VOLUME_DOWN"] = "Ses Azalt",
            ["CTRL+T"] = "Yeni Sekme", ["CTRL+W"] = "Sekmeyi Kapat", ["CTRL+SHIFT+T"] = "Kapanan Sekme",
            ["F5"] = "Sayfayı Yenile", ["BROWSER_HOME"] = "Tarayıcı", ["BROWSER_BACK"] = "Tarayıcı Geri",
            ["BROWSER_FORWARD"] = "Tarayıcı İleri", ["BROWSER_REFRESH"] = "Tarayıcı Yenile",
            ["BROWSER_STOP"] = "Tarayıcı Durdur", ["BROWSER_FAVORITES"] = "Sık Kullanılanlar",
            ["SHIFT+WINDOWS+S"] = "Ekran Görüntüsü", ["CTRL+SHIFT+ESC"] = "Görev Yöneticisi", ["WINDOWS+E"] = "Dosya Gezgini",
            ["WINDOWS+LEFT"] = "Sola Yasla", ["WINDOWS+RIGHT"] = "Sağa Yasla", ["WINDOWS+V"] = "Pano Geçmişi",
            ["WINDOWS+L"] = "Ekranı Kilitle", ["NOSLEEP"] = "Uyku Engelleyici",
            ["BRIGHTNESS_UP"] = "Parlaklık Artır", ["BRIGHTNESS_DOWN"] = "Parlaklık Azalt", ["CALCULATOR"] = "Hesap Makinesi",
            ["EMAIL"] = "E-posta", ["MY_COMPUTER"] = "Bu Bilgisayar", ["WINDOWS_SEARCH"] = "Windows Arama",
            ["MEDIA_PLAYER"] = "Medya Oynatıcı",
            ["APPLICATION"] = "Menü Tuşu", ["PAUSE"] = "Pause", ["SCROLL_LOCK"] = "Scroll Lock", ["INSERT"] = "Insert",
            ["PRINT_SCREEN"] = "Print Screen", ["KEYPAD_EQUALS"] = "Tuş Takımı =",
            ["SCROLL_UP"] = "Kaydır Yukarı", ["SCROLL_DOWN"] = "Kaydır Aşağı", ["ZOOM_IN"] = "Yakınlaştır",
            ["ZOOM_OUT"] = "Uzaklaştır", ["MOUSE_LEFT"] = "Sol Tık", ["MOUSE_RIGHT"] = "Sağ Tık", ["MOUSE_MIDDLE"] = "Orta Tık",
            ["NONE"] = "Boş",
            ["MIC_MUTE"] = "Mikrofon Kapat / Aç", ["COLOR_PICK"] = "Renk Seçici",
            ["PC_SHUTDOWN"] = "Bilgisayarı Kapat", ["PC_RESTART"] = "Yeniden Başlat", ["PC_SLEEP"] = "Uyku Modu",
            ["PC_MONITOR_OFF"] = "Ekranı Kapat",
        };

        /// <summary>Sağ tekerleğin görevleri (kod, ad, sağa, sola).</summary>
        public static readonly (string kod, string ad, string sag, string sol)[] TekerGorevleri =
        {
            ("m", "Mod değiştir", "sonraki mod", "önceki mod"),
            ("k", "Sayfa kaydır", "aşağı", "yukarı"),
            ("z", "Yakınlaştır", "büyüt", "küçült"),
            ("s", "Ses", "aç", "kıs"),
            ("b", "Sekmeler arası", "sonraki sekme", "önceki sekme"),
            ("g", "Geri al / Yinele", "yinele", "geri al"),
            ("o", "Özel kısayol", "", ""),
        };

        /// <summary>Klavye ekranında karonun altına sığacak kısa ad (uzun basma komutu için).</summary>
        public static string KisaAd(string kod)
        {
            switch (kod)
            {
                case "MIC_MUTE": return "Mikrofon";
                case "COLOR_PICK": return "Renk seç";
                case "MEDIA_PLAY_PAUSE": return "Oynat/Dur";
                case "MEDIA_PREVIOUS": return "Önceki";
                case "MEDIA_NEXT": return "Sonraki";
                case "SHIFT+WINDOWS+S": return "Ekran al";
                case "CTRL+SHIFT+ESC": return "Görev yön.";
                case "NOSLEEP": return "Uyku engel";
                case "PIXEL_MODE": return "LED";
                case "PC_SHUTDOWN": return "Kapat";
                case "PC_RESTART": return "Yeniden";
                case "PC_SLEEP": return "Uyku";
                case "PC_MONITOR_OFF": return "Ekran kapat";
                case "WINDOWS+L": return "Kilitle";
            }
            if (MetinMi(kod))
            {
                var m = MetinCoz(kod, out _).Replace('\n', ' ').Trim();
                return "“" + (m.Length > 10 ? m.Substring(0, 10) : m) + "”";
            }
            return Ad(kod);
        }

        public static string TekerAdi(string kod)
        {
            foreach (var t in TekerGorevleri) if (t.kod == kod) return t.ad;
            return "Mod değiştir";
        }

        public static readonly string[][] Kategoriler =
        {
            new[] { "Genel", "WINDOWS+D", "CTRL+C", "CTRL+V", "ALT+TAB", "DISPLAY", "PIXEL_MODE" },
            new[] { "Düzenleme", "CTRL+X", "CTRL+Z", "CTRL+Y", "CTRL+A", "CTRL+S", "CTRL+F" },
            new[] { "Medya ve Ses", "MEDIA_PLAY_PAUSE", "MEDIA_PREVIOUS", "MEDIA_NEXT", "MEDIA_STOP",
                    "MEDIA_FAST_FORWARD", "MEDIA_REWIND", "MUTE", "VOLUME_UP", "VOLUME_DOWN", "MIC_MUTE" },
            new[] { "Tarayıcı", "CTRL+T", "CTRL+W", "CTRL+SHIFT+T", "F5", "BROWSER_HOME",
                    "BROWSER_BACK", "BROWSER_FORWARD", "BROWSER_REFRESH", "BROWSER_STOP", "BROWSER_FAVORITES" },
            new[] { "Pencere ve Sistem", "SHIFT+WINDOWS+S", "CTRL+SHIFT+ESC", "WINDOWS+E",
                    "WINDOWS+LEFT", "WINDOWS+RIGHT", "WINDOWS+V", "NOSLEEP", "COLOR_PICK" },
            new[] { "Bilgisayar", "PC_SHUTDOWN", "PC_RESTART", "PC_SLEEP", "WINDOWS+L", "PC_MONITOR_OFF" },
            new[] { "Özel Tuşlar", "BRIGHTNESS_UP", "BRIGHTNESS_DOWN", "CALCULATOR", "EMAIL", "MY_COMPUTER",
                    "WINDOWS_SEARCH", "MEDIA_PLAYER" },
            new[] { "Ek Tuşlar", "APPLICATION", "PAUSE", "SCROLL_LOCK", "INSERT", "PRINT_SCREEN", "KEYPAD_EQUALS" },
            new[] { "Fare", "SCROLL_UP", "SCROLL_DOWN", "ZOOM_IN", "ZOOM_OUT", "MOUSE_LEFT", "MOUSE_RIGHT", "MOUSE_MIDDLE" },
            new[] { "F13 – F24", "F13", "F14", "F15", "F16", "F17", "F18", "F19", "F20", "F21", "F22", "F23", "F24" },
        };

        static readonly Dictionary<string, string> AramaKelimeleri = new()
        {
            ["WINDOWS+D"] = "masaustu desktop pencereleri gizle", ["MEDIA_STOP"] = "muzik video medya durdur stop",
            ["MEDIA_FAST_FORWARD"] = "muzik video ileri sar hizli", ["MEDIA_REWIND"] = "muzik video geri sar",
            ["BRIGHTNESS_UP"] = "ekran parlaklik aydinlik isik", ["BRIGHTNESS_DOWN"] = "ekran parlaklik karart isik",
            ["CALCULATOR"] = "hesap makinesi calculator", ["BROWSER_HOME"] = "internet tarayici chrome edge web",
            ["EMAIL"] = "posta mail outlook", ["MY_COMPUTER"] = "bu bilgisayar dosya gezgini explorer",
            ["NOSLEEP"] = "uyku uyuma kilitlenme ekran koruyucu fare oynat kapanmasin acik tut",
            ["CTRL+C"] = "copy", ["CTRL+V"] = "paste", ["ALT+TAB"] = "pencere gecis degistir",
            ["DISPLAY"] = "ekran monitor projeksiyon yansit win p",
            ["MEDIA_PLAY_PAUSE"] = "muzik sarki medya baslat duraklat play pause", ["MEDIA_PREVIOUS"] = "muzik sarki medya geri",
            ["MEDIA_NEXT"] = "muzik sarki medya ileri", ["MUTE"] = "ses sessiz kapat", ["VOLUME_UP"] = "ses yukselt volume",
            ["VOLUME_DOWN"] = "ses dusur kis volume", ["SCROLL_UP"] = "fare tekerlek kaydir", ["SCROLL_DOWN"] = "fare tekerlek kaydir",
            ["ZOOM_IN"] = "buyut zoom", ["ZOOM_OUT"] = "kucult zoom", ["MOUSE_LEFT"] = "fare tikla mouse",
            ["MOUSE_RIGHT"] = "fare tikla mouse menu", ["MOUSE_MIDDLE"] = "fare tikla mouse tekerlek",
            ["CTRL+X"] = "cut", ["CTRL+Z"] = "undo", ["CTRL+Y"] = "redo ileri al", ["CTRL+A"] = "hepsini select all",
            ["CTRL+S"] = "save", ["CTRL+F"] = "ara find", ["SHIFT+WINDOWS+S"] = "ekran alintisi screenshot resim kirp",
            ["CTRL+SHIFT+ESC"] = "task manager islem program kapat", ["WINDOWS+E"] = "explorer klasor dosyalar",
            ["WINDOWS+LEFT"] = "pencere bol yarim ekran", ["WINDOWS+RIGHT"] = "pencere bol yarim ekran",
            ["WINDOWS+V"] = "pano kopyalananlar clipboard", ["WINDOWS+L"] = "kilit kilitle oturum lock",
            ["CTRL+T"] = "tarayici sekme chrome yeni", ["CTRL+W"] = "tarayici sekme kapat", ["CTRL+SHIFT+T"] = "tarayici sekme geri ac",
            ["F5"] = "tarayici yenile refresh", ["PIXEL_MODE"] = "led isik", ["APPLICATION"] = "menu tusu sag tik baglam context",
            ["PAUSE"] = "pause break duraklat", ["SCROLL_LOCK"] = "scroll lock kaydirma kilidi", ["INSERT"] = "insert ekle uzerine yaz",
            ["PRINT_SCREEN"] = "print screen prtsc ekran goruntusu", ["KEYPAD_EQUALS"] = "esittir tus takimi numpad",
            ["WINDOWS_SEARCH"] = "windows arama ara bul search", ["BROWSER_BACK"] = "tarayici geri back",
            ["BROWSER_FORWARD"] = "tarayici ileri forward", ["BROWSER_REFRESH"] = "tarayici yenile refresh",
            ["BROWSER_STOP"] = "tarayici durdur stop", ["BROWSER_FAVORITES"] = "tarayici sik kullanilanlar yer imi favori",
            ["MEDIA_PLAYER"] = "medya oynatici muzik video player",
            ["MIC_MUTE"] = "mikrofon mic sessiz sustur discord teams zoom toplanti",
            ["COLOR_PICK"] = "renk secici pipet hex kod renk kopyala tasarim",
            ["PC_SHUTDOWN"] = "bilgisayar kapat kapatma shutdown power guc",
            ["PC_RESTART"] = "bilgisayar yeniden baslat restart reset",
            ["PC_SLEEP"] = "bilgisayar uyku uyut sleep bekleme",
            ["PC_MONITOR_OFF"] = "monitor ekran kapat karart",
        };

        /// <summary>Kısayol parçalarının ekranda yazılışı (Türkçe Q klavyede o tuşun üstünde yazan).</summary>
        static readonly Dictionary<string, string> ParcaAdlari = new()
        {
            ["CTRL"] = "Ctrl", ["CONTROL"] = "Ctrl", ["SHIFT"] = "Shift", ["ALT"] = "Alt", ["WINDOWS"] = "Win", ["GUI"] = "Win",
            ["ESC"] = "Esc", ["ESCAPE"] = "Esc", ["TAB"] = "Tab", ["ENTER"] = "Enter", ["SPACE"] = "Boşluk",
            ["BACKSPACE"] = "Geri Sil", ["DELETE"] = "Delete", ["INSERT"] = "Insert", ["HOME"] = "Home", ["END"] = "End",
            ["PAGE_UP"] = "PgUp", ["PAGE_DOWN"] = "PgDn", ["CAPS_LOCK"] = "Caps Lock",
            ["LEFT"] = "←", ["RIGHT"] = "→", ["UP"] = "↑", ["DOWN"] = "↓",
            ["ONE"] = "1", ["TWO"] = "2", ["THREE"] = "3", ["FOUR"] = "4", ["FIVE"] = "5",
            ["SIX"] = "6", ["SEVEN"] = "7", ["EIGHT"] = "8", ["NINE"] = "9", ["ZERO"] = "0",
            ["MINUS"] = "*", ["EQUALS"] = "-", ["LEFT_BRACKET"] = "Ğ", ["RIGHT_BRACKET"] = "Ü", ["SEMICOLON"] = "Ş",
            ["QUOTE"] = "İ", ["COMMA"] = "Ö", ["PERIOD"] = "Ç", ["FORWARD_SLASH"] = ".", ["BACKSLASH"] = ",",
            ["GRAVE_ACCENT"] = "\"", ["KEYPAD_BACKSLASH"] = "<",
            ["PRINT_SCREEN"] = "PrtSc", ["SCROLL_LOCK"] = "ScrLk", ["PAUSE"] = "Pause", ["APPLICATION"] = "Menü",
            ["KEYPAD_PLUS"] = "Num +", ["KEYPAD_MINUS"] = "Num −", ["KEYPAD_ASTERISK"] = "Num *",
            ["KEYPAD_FORWARD_SLASH"] = "Num /", ["KEYPAD_PERIOD"] = "Num ,", ["KEYPAD_ENTER"] = "Num Enter",
        };

        public static IEnumerable<string> Tumu() => Kategoriler.SelectMany(k => k.Skip(1));

        /// <summary>Komutun adı: "CTRL+C" -> "Kopyala"; katalogda yoksa kısayolun yazılışı.</summary>
        public static string Ad(string kod)
        {
            if (string.IsNullOrEmpty(kod)) return "Boş";
            if (MetinMi(kod)) return MetinOnizleme(kod);
            if (DiziMi(kod)) return DiziOnizleme(kod);
            if (PencereMi(kod)) return "Pencere · " + PencereIslemleri.YerlesimAdi(PencereKodu(kod));
            if (Adlar.TryGetValue(kod, out var ad)) return ad;
            if (UygulamaMi(kod)) return UygulamaAdi(kod);
            if (kod.Length <= 3 && kod[0] == 'F' && int.TryParse(kod.Substring(1), out _)) return kod;      // F1-F24
            var ip = Ipucu(kod);
            return ip != "" ? ip : kod;
        }

        /// <summary>Komutun sağda soluk yazılan ipucu: "CTRL+SHIFT+T" -> "Ctrl + Shift + T".</summary>
        public static string Ipucu(string kod)
        {
            switch (kod)
            {
                case null: case "": case "NONE": return "";
                case "DISPLAY": return "Win + P";
                case "NOSLEEP": return "aç / kapat";
                case "PIXEL_MODE": return "klavyede";
                case "MIC_MUTE": return "aç / kapat";
                case "COLOR_PICK": return "imleçteki renk";
                case "PC_SHUTDOWN": case "PC_RESTART": return "10 sn sonra";
                case "PC_SLEEP": return "";
                case "PC_MONITOR_OFF": return "";
            }
            if (MetinMi(kod)) return kod.StartsWith("TXE:") ? "metin + Enter" : "metin";
            if (DiziMi(kod)) return "adım adım";
            if (PencereMi(kod)) return "pencere";
            if (UygulamaMi(kod)) return UygulamaTuru(kod);
            if (kod.StartsWith("F") && int.TryParse(kod.Substring(1), out int f))
                return f >= 13 ? "programda ata" : kod;
            if (!kod.Contains('+'))
                return Adlar.ContainsKey(kod) ? "" : ParcaAdi(kod);
            return string.Join(" + ", kod.Split('+').Select(ParcaAdi));
        }

        static readonly string[] Rakamlar = { "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE" };

        static string ParcaAdi(string p)
        {
            if (ParcaAdlari.TryGetValue(p, out var ad)) return ad;
            if (p.StartsWith("KEYPAD_"))
            {
                int r = Array.IndexOf(Rakamlar, p.Substring(7));
                return "Num " + (r >= 0 ? r.ToString() : p.Substring(7));
            }
            return p;
        }

        /// <summary>Medya / fare / özel komut mu (kısayol değil): ipucu yerine tür yazılır.</summary>
        public static string Tur(string kod)
        {
            if (string.IsNullOrEmpty(kod) || kod == "NONE") return "boş";
            var ip = Ipucu(kod);
            if (ip != "") return ip;
            if (kod.StartsWith("MOUSE_") || kod.StartsWith("SCROLL_") || kod.StartsWith("ZOOM_")) return "fare";
            if (kod.StartsWith("MEDIA_") || kod == "MUTE" || kod.StartsWith("VOLUME_")) return "medya tuşu";
            return "özel tuş";
        }

        // ------------------------------------------------------------ metin yaz (uygulama 2.3)
        // Kod: "TXT:" + metin (ya da sonunda Enter'a basılacaksa "TXE:"). Satır sonları "¶" olarak saklanır
        // (mod JSON'u tek satır). Klavye bunu kendisi yapamaz: tuşa basınca "TUS" bildirir, uygulama yazar.
        public const string MetinKategorisi = "Metin yaz";
        public const int MetinUzunlugu = 120;       // klavyenin kalıcı belleği küçük

        // ------------------------------------------------------------ tuş simgeleri (uygulama 2.9 / kart 2.7)
        // Ad, kartın simgeler.cpp'sindeki adla aynı olmalı.
        public static readonly (string ad, string yazi, string grup)[] TusSimgeleri =
        {
            ("oynat", "Oynat", "Medya"), ("duraklat", "Duraklat", "Medya"), ("dur", "Durdur", "Medya"), ("onceki", "Önceki", "Medya"),
            ("sonraki", "Sonraki", "Medya"), ("ileri", "İleri sar", "Medya"), ("geri_sar", "Geri sar", "Medya"),
            ("karistir", "Karıştır", "Medya"), ("tekrar", "Tekrarla", "Medya"), ("muzik", "Müzik", "Medya"),
            ("ses", "Ses", "Ses"), ("sessiz", "Sessiz", "Ses"), ("ses_artir", "Sesi artır", "Ses"), ("ses_azalt", "Sesi azalt", "Ses"),
            ("mikrofon", "Mikrofon", "Ses"), ("kulaklik", "Kulaklık", "Ses"),
            ("kopyala", "Kopyala", "Düzenleme"), ("yapistir", "Yapıştır", "Düzenleme"), ("kes", "Kes", "Düzenleme"),
            ("geri", "Geri al", "Düzenleme"), ("kaydet", "Kaydet", "Düzenleme"), ("kalem", "Kalem", "Düzenleme"),
            ("dosya", "Dosya", "Düzenleme"), ("klasor", "Klasör", "Düzenleme"), ("cop", "Çöp", "Düzenleme"),
            ("yazici", "Yazıcı", "Düzenleme"), ("ara", "Ara", "Düzenleme"),
            ("ekran", "Ekran", "Bilgisayar"), ("kamera", "Kamera", "Bilgisayar"), ("kilit", "Kilit", "Bilgisayar"),
            ("guc", "Güç", "Bilgisayar"), ("pencere", "Pencere", "Bilgisayar"), ("terminal", "Terminal", "Bilgisayar"),
            ("kod", "Kod", "Bilgisayar"), ("ayar", "Ayar", "Bilgisayar"), ("parlaklik", "Parlaklık", "Bilgisayar"),
            ("pil", "Pil", "Bilgisayar"), ("oyun", "Oyun", "Bilgisayar"),
            ("dunya", "Dünya / web", "İnternet"), ("wifi", "Wi-Fi", "İnternet"), ("bulut", "Bulut", "İnternet"),
            ("baglanti", "Bağlantı", "İnternet"), ("indir", "İndir", "İnternet"), ("yukle", "Yükle", "İnternet"),
            ("yenile", "Yenile", "İnternet"), ("posta", "Posta", "İnternet"), ("sohbet", "Sohbet", "İnternet"),
            ("ev", "Ev", "Genel"), ("yildiz", "Yıldız", "Genel"), ("kalp", "Kalp", "Genel"), ("simsek", "Şimşek", "Genel"),
            ("zaman", "Saat", "Genel"), ("takvim", "Takvim", "Genel"), ("ay", "Ay", "Genel"), ("zil", "Zil", "Genel"),
            ("kullanici", "Kişi", "Genel"), ("konum", "Konum", "Genel"), ("arti", "Artı", "Genel"), ("tamam", "Tamam", "Genel"),
            ("kapat", "Kapat", "Genel"), ("bilgi", "Bilgi", "Genel"), ("uyari", "Uyarı", "Genel"),
        };

        public static string SimgeYazisi(string ad)
        {
            foreach (var (a, y, _) in TusSimgeleri) if (a == ad) return y;
            return "Yok";
        }

        // ------------------------------------------------------------ makro dizisi (uygulama 2.9 / kart 2.7)
        // Kod: "DIZI:adım|adım|..." Adım: kısayol ("CTRL+C"), "BEKLE:300" (ms), "TXT:metin", "TXE:metin", "APP:yol", "MIC_MUTE"...
        // Klavye tuş kısayollarını kendisi yapar; metin, uygulama vb. adımlar için "DIZIADIM" yollar, uygulama yapınca "DIZIOK" der.
        public const string MakroKategorisi = "Makro";
        public const int MakroUzunlugu = 600;

        public static bool DiziMi(string kod) => kod != null && kod.StartsWith("DIZI:") && kod.Length > 5;

        public static List<string> DiziAdimlari(string kod) =>
            DiziMi(kod) ? kod.Substring(5).Split('|').Select(s => s.Trim()).Where(s => s != "").ToList() : new List<string>();

        /// <summary>Düzenleme kutusundaki satırlar -> kod. Satır biçimleri: "CTRL+C", "BEKLE 300", "YAZ metin", "YAZ+ metin" (sonunda Enter), "AC yol".</summary>
        public static string DiziKodu(string satirlar)
        {
            var adimlar = new List<string>();
            foreach (var ham in (satirlar ?? "").Replace("\r", "").Split('\n'))
            {
                var s = ham.Trim();
                if (s == "") continue;
                var buyuk = s.ToUpperInvariant();
                if (buyuk.StartsWith("BEKLE"))
                {
                    var sayi = new string(s.Substring(5).Where(char.IsDigit).ToArray());
                    int ms = sayi == "" ? 500 : (int)Math.Min(60000, long.Parse(sayi.Length > 6 ? sayi.Substring(0, 6) : sayi));
                    adimlar.Add("BEKLE:" + ms);
                }
                else if (buyuk.StartsWith("YAZ+ ") || buyuk == "YAZ+") adimlar.Add("TXE:" + s.Substring(Math.Min(5, s.Length)).Replace('|', '/'));
                else if (buyuk.StartsWith("YAZ ")) adimlar.Add("TXT:" + s.Substring(4).Replace('|', '/'));
                else if (buyuk.StartsWith("AC ")) adimlar.Add("APP:" + s.Substring(3).Replace('|', '/'));
                else adimlar.Add(s.Replace('|', '/').ToUpperInvariant().Replace(" ", ""));
            }
            if (adimlar.Count == 0) return "NONE";
            var kod = "DIZI:" + string.Join("|", adimlar);
            return kod.Length > MakroUzunlugu ? kod.Substring(0, MakroUzunlugu) : kod;
        }

        /// <summary>Koddan düzenleme kutusunun metni (DiziKodu'nun tersi).</summary>
        public static string DiziMetni(string kod)
        {
            var sb = new StringBuilder();
            foreach (var a in DiziAdimlari(kod))
            {
                if (a.StartsWith("BEKLE:")) sb.Append("BEKLE ").Append(a.Substring(6));
                else if (a.StartsWith("TXT:")) sb.Append("YAZ ").Append(a.Substring(4));
                else if (a.StartsWith("TXE:")) sb.Append("YAZ+ ").Append(a.Substring(4));
                else if (a.StartsWith("APP:")) sb.Append("AC ").Append(a.Substring(4));
                else sb.Append(a);
                sb.Append("\r\n");
            }
            return sb.ToString().TrimEnd();
        }

        public static string DiziOnizleme(string kod)
        {
            int n = DiziAdimlari(kod).Count;
            return "Makro · " + n + " adım";
        }

        public static bool MetinMi(string kod) => kod != null && kod.Length >= 4 && (kod.StartsWith("TXT:") || kod.StartsWith("TXE:"));

        public static string MetinCoz(string kod, out bool enter)
        {
            enter = kod != null && kod.StartsWith("TXE:");
            return MetinMi(kod) ? kod.Substring(4).Replace('¶', '\n') : "";
        }

        public static string MetinKodu(string metin, bool enter)
        {
            metin = (metin ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ');
            if (metin.Length > MetinUzunlugu) metin = metin.Substring(0, MetinUzunlugu);
            return (enter ? "TXE:" : "TXT:") + metin.Replace('\n', '¶');
        }

        static string MetinOnizleme(string kod)
        {
            var m = MetinCoz(kod, out _).Replace('\n', ' ').Trim();
            return "“" + (m.Length > 22 ? m.Substring(0, 21) + "…" : m) + "”";
        }

        // ------------------------------------------------------------ uygulama / klasör / site aç
        // Kod: "APP:" + yol ya da adres. Klavye bunu kendisi yapamaz: tuşa basınca "TUS" bildirir, uygulama açar.
        public const string UygulamaKategorisi = "Uygulama aç";

        public static bool UygulamaMi(string kod) =>
            kod != null && (kod.StartsWith("APP:", StringComparison.OrdinalIgnoreCase) || kod.StartsWith("APPF:", StringComparison.OrdinalIgnoreCase));

        /// <summary>Uygulama 3.2 (BT): açıksa yeni kopya açmak yerine penceresini öne getir.</summary>
        public static bool OneGetirMi(string kod) => kod != null && kod.StartsWith("APPF:", StringComparison.OrdinalIgnoreCase);

        public static string UygulamaHedefi(string kod) =>
            OneGetirMi(kod) ? kod.Substring(5) : (UygulamaMi(kod) ? kod.Substring(4) : "");

        // ------------------------------------------------------------ 3.2 (CD): pencere yerleşimi
        public const string PencereKategorisi = "Pencere";

        public static bool PencereMi(string kod) => kod != null && kod.StartsWith("PEN:", StringComparison.OrdinalIgnoreCase);

        public static string PencereKodu(string kod) => PencereMi(kod) ? kod.Substring(4) : "";

        public static bool SiteMi(string hedef) =>
            hedef.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || hedef.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        public static string UygulamaTuru(string kod)
        {
            var h = UygulamaHedefi(kod);
            if (SiteMi(h)) return "site";
            if (h.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return h.Contains('!') ? "Store uygulaması" : "uygulama";
            try { if (System.IO.Directory.Exists(h)) return "klasör"; } catch { }
            return "uygulama";
        }

        public static string UygulamaAdi(string kod)
        {
            var h = UygulamaHedefi(kod).TrimEnd('\\', '/');
            if (h == "") return "Uygulama";
            if (h.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                var bilinen = YukluUygulamalar.AdBul(h);
                if (bilinen != null) return bilinen;
                // "shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" -> "WindowsCalculator"
                var id = h.Substring(h.LastIndexOf('\\') + 1);
                id = id.Split('!')[0].Split('_')[0];
                return id.Substring(id.LastIndexOf('.') + 1);
            }
            if (SiteMi(h))
            {
                try { return new Uri(h).Host.Replace("www.", ""); } catch { return h; }
            }
            try
            {
                var ad = System.IO.Directory.Exists(h) ? System.IO.Path.GetFileName(h) : System.IO.Path.GetFileNameWithoutExtension(h);
                return string.IsNullOrEmpty(ad) ? h : ad;
            }
            catch { return h; }
        }

        /// <summary>Arama kutusuna yazılan bir web adresi mi ("youtube.com", "https://...")? Öyleyse tam adresi verir.</summary>
        public static string SiteAdresi(string metin)
        {
            metin = (metin ?? "").Trim();
            if (metin.Length < 4 || metin.Contains(' ')) return null;
            if (SiteMi(metin)) return metin;
            int nokta = metin.IndexOf('.');
            if (nokta > 0 && nokta < metin.Length - 2 && !metin.Contains('\\')) return "https://" + metin;
            return null;
        }

        public static string Norm(string s)
        {
            s = (s ?? "").ToLower(new System.Globalization.CultureInfo("tr-TR"));
            return s.Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g').Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c');
        }

        public static List<string> Ara(string metin)
        {
            var q = Norm(metin).Trim();
            if (q == "") return new List<string>();
            return Tumu().Where(k => Norm(Ad(k) + " " + Ipucu(k) + " " + (AramaKelimeleri.TryGetValue(k, out var a) ? a : "")).Contains(q))
                         .Distinct().ToList();
        }

        // ------------------------------------------------------------ kendi kısayolun: basılan tuşu karta uygun ada çevirir
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint code, uint mapType);

        /// <summary>
        /// Windows tuş kodunu klavyenin (US düzeninde) gönderdiği tuşun adına çevirir. Harf ve noktalama tuşları
        /// fiziksel konumuna göre (tarama koduyla) çevrilir: Türkçe Q'da Ş'ye basılınca kart da o tuşu gönderir.
        /// </summary>
        public static string TusAdi(Keys k)
        {
            switch (k)
            {
                case Keys.Left: return "LEFT";
                case Keys.Right: return "RIGHT";
                case Keys.Up: return "UP";
                case Keys.Down: return "DOWN";
                case Keys.Home: return "HOME";
                case Keys.End: return "END";
                case Keys.PageUp: return "PAGE_UP";
                case Keys.PageDown: return "PAGE_DOWN";
                case Keys.Insert: return "INSERT";
                case Keys.Delete: return "DELETE";
                case Keys.Escape: return "ESC";
                case Keys.PrintScreen: return "PRINT_SCREEN";
                case Keys.Pause: return "PAUSE";
                case Keys.Scroll: return "SCROLL_LOCK";
                case Keys.Apps: return "APPLICATION";
                case Keys.Multiply: return "KEYPAD_ASTERISK";
                case Keys.Add: return "KEYPAD_PLUS";
                case Keys.Subtract: return "KEYPAD_MINUS";
                case Keys.Divide: return "KEYPAD_FORWARD_SLASH";
                case Keys.Decimal: return "KEYPAD_PERIOD";
            }
            if (k >= Keys.F1 && k <= Keys.F24) return "F" + (k - Keys.F1 + 1);
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9)
                return "KEYPAD_" + Rakamlar[k - Keys.NumPad0];
            uint sc = 0;
            try { sc = MapVirtualKey((uint)k, 0); } catch { }
            return sc switch
            {
                >= 0x02 and <= 0x0B => new[] { "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "ZERO" }[sc - 0x02],
                0x0C => "MINUS", 0x0D => "EQUALS", 0x0E => "BACKSPACE", 0x0F => "TAB",
                >= 0x10 and <= 0x19 => "QWERTYUIOP"[(int)sc - 0x10].ToString(),
                0x1A => "LEFT_BRACKET", 0x1B => "RIGHT_BRACKET", 0x1C => "ENTER",
                >= 0x1E and <= 0x26 => "ASDFGHJKL"[(int)sc - 0x1E].ToString(),
                0x27 => "SEMICOLON", 0x28 => "QUOTE", 0x29 => "GRAVE_ACCENT", 0x2B => "BACKSLASH",
                >= 0x2C and <= 0x32 => "ZXCVBNM"[(int)sc - 0x2C].ToString(),
                0x33 => "COMMA", 0x34 => "PERIOD", 0x35 => "FORWARD_SLASH", 0x39 => "SPACE", 0x3A => "CAPS_LOCK",
                0x56 => "KEYPAD_BACKSLASH",
                _ => null,
            };
        }

        /// <summary>Basılan tuşlar -> "CTRL+SHIFT+K" (değiştirici tuşa tek başına basıldıysa sonu boş kalır).</summary>
        public static string Birlesim(Keys tus, Keys degistiriciler, bool win, out bool tamam)
        {
            var p = new List<string>();
            if ((degistiriciler & Keys.Control) != 0) p.Add("CTRL");
            if ((degistiriciler & Keys.Shift) != 0) p.Add("SHIFT");
            if ((degistiriciler & Keys.Alt) != 0) p.Add("ALT");
            if (win) p.Add("WINDOWS");
            tamam = false;
            if (tus is not (Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin or Keys.None))
            {
                var ad = TusAdi(tus);
                if (ad != null)
                {
                    p.Add(ad);
                    tamam = true;
                }
            }
            return string.Join("+", p);
        }
    }
}
