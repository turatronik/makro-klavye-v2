using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Ayarlar > Yedek (uygulama 2.3): modlar, simgeler, ışık ve ekran ayarları ve uygulama ayarları tek dosyada (.tmk, JSON).
    /// Yüklemeden önce şimdiki hâl %APPDATA%\MakroKeyboardDisplay\yedekler klasörüne otomatik yedeklenir.
    /// </summary>
    public sealed partial class KlavyePaneli
    {
        // 3.2: Turatronik Makro Klavye yedeği. Eski ".mkyedek" dosyaları da açılabilir.
        const string YedekUzantisi = ".tmk";
        string yedekDurum = "";            // son işlemin sonucu ("" / "tamam:..." / "hata:...")

        static readonly HashSet<string> YedeklenmeyenAyarlar = new(StringComparer.OrdinalIgnoreCase)
            { "SonPort", "SonYedek", "OtoYedek", "YukModlar", "YukIsik", "YukUygulama" };

        static bool Secili(string anahtar) => Ayarlar.Oku(anahtar, "1") == "1";

        void YedekCiz(Graphics g, float y, float w, float h)
        {
            Grup(g, "YEDEKLE", y);
            y += 20;
            Yaz(g, "Tek dosya (.tmk): bütün modlar ve tuşları, çizdiğin simgeler, ışık ve klavye ekranı ayarları, uygulama ayarları.",
                8f, FontStyle.Regular, T.Soluk, new RectangleF(P, y, w - 2 * P, 36), StringAlignment.Near, true, StringAlignment.Near);
            y += 40;
            KucukDugme(g, new RectangleF(P, y, 100, 30), "Yedek al…", "yedekal", YedekAl, 1);
            KucukDugme(g, new RectangleF(P + 108, y, 136, 30), "Yedekten yükle…", "yedekyukle", () => YedektenYukle(null), 0);
            y += 38;
            var son = Ayarlar.Oku("SonYedek", "");
            Yaz(g, son != "" ? "Son yedek: " + son : "Henüz yedek alınmadı", 7.5f, FontStyle.Regular, T.Soluk,
                new RectangleF(P, y, w - 2 * P, 30), StringAlignment.Near, true, StringAlignment.Near);
            y += 34;

            Grup(g, "YÜKLERKEN", y);
            y += 20;
            float x = P;
            x = Kutucuk(g, x, y, "Modlar", "YukModlar");
            x = Kutucuk(g, x + 14, y, "Işık ve ekran", "YukIsik");
            Kutucuk(g, x + 14, y, "Uygulama", "YukUygulama");
            y += 28;
            Yaz(g, "Yüklemeden önce şimdiki hâlin otomatik yedeği alınır; beğenmezsen geri dönersin.",
                7.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, y, w - 2 * P, 30), StringAlignment.Near, true, StringAlignment.Near);
            y += 34;

            var oto = Ayarlar.Oku("OtoYedek", "");
            if (oto != "" && File.Exists(oto))
            {
                KucukDugme(g, new RectangleF(P, y, 150, 28), "Önceki hâline dön", "yedekgeri", () => YedektenYukle(oto, true), 0);
                y += 36;
            }

            if (yedekDurum != "")
            {
                bool hata = yedekDurum.StartsWith("hata:");
                Yaz(g, (hata ? "" : "✓ ") + yedekDurum.Substring(yedekDurum.IndexOf(':') + 1), 8f, FontStyle.Bold,
                    hata ? R(248, 113, 113) : R(74, 222, 128), new RectangleF(P, y, w - 2 * P, 34), StringAlignment.Near, true, StringAlignment.Near);
            }
        }

        float Kutucuk(Graphics g, float x, float y, string ad, string anahtar)
        {
            bool acik = Secili(anahtar);
            var k = new RectangleF(x, y + 3, 15, 15);
            Doldur(g, acik ? T.Vurgu : T.Iz, k, 4);
            if (acik)
                using (var kalem = new Pen(T.Vurgu == Color.FromArgb(255, 255, 255) ? R(27, 20, 70) : Color.White, 2f))
                    g.DrawLines(kalem, new[] { new PointF(k.X + 3.5f, k.Y + 7.5f), new PointF(k.X + 6.5f, k.Y + 10.5f), new PointF(k.X + 11.5f, k.Y + 4.5f) });
            float gen = g.MeasureString(ad, YaziTipi("Segoe UI", 9f, FontStyle.Regular)).Width;
            Yaz(g, ad, 9f, FontStyle.Regular, T.Yazi, new RectangleF(x + 20, y, gen + 4, 21));
            var alan = new RectangleF(x - 2, y, gen + 26, 21);
            if (uzerindeki == "kut" + anahtar) Doldur(g, T.Uzerinde, alan, 5);
            Ekle(alan, () => Ayarlar.Yaz(anahtar, acik ? "0" : "1"), "kut" + anahtar);
            return x + 20 + gen;
        }

        // ------------------------------------------------------------------ dosya
        string YedekMetni(out List<string> eksik)
        {
            eksik = new List<string>();
            using var akis = new MemoryStream();
            using (var j = new Utf8JsonWriter(akis, new JsonWriterOptions { Indented = true }))
            {
                j.WriteStartObject();
                j.WriteNumber("surum", 2);                  // 3.2 (BM): simgeler de yedekte
                j.WriteString("uygulama_surumu", Program.Surum);
                j.WriteString("kart_surumu", durum.KartSurum ?? "");
                j.WriteString("tarih", DateTime.Now.ToString("s"));
                if (durum.ModTanimlari != null)
                    j.WriteString("modlar", ModTanimi.Json(durum.ModTanimlari));
                else eksik.Add("modlar");
                if (led != null)
                {
                    var alanlar = new List<string>();
                    foreach (var o in led.Alanlar()) alanlar.Add(Convert.ToString(o, System.Globalization.CultureInfo.InvariantCulture));
                    var t = led.TusAlanlari();
                    for (int i = 1; i < t.Length; i++) alanlar.Add(Convert.ToString(t[i], System.Globalization.CultureInfo.InvariantCulture));
                    j.WriteString("isik", string.Join("\t", alanlar));
                }
                else eksik.Add("ışık");
                if (ekranAyar != null)
                {
                    j.WriteStartArray("ekran");             // parlaklık, bayraklar, ses ışığı, gece, uzun basış
                    foreach (var v in ekranAyar) j.WriteNumberValue(v);
                    j.WriteEndArray();
                }
                else eksik.Add("ekran ayarları");
                // 3.2 (BM): kullanıcının çizdiği simgeler
                j.WriteStartArray("simgeler");
                foreach (var sim in OzelSimge.Hepsi)
                {
                    j.WriteStartObject();
                    j.WriteString("ad", sim.Ad);
                    j.WriteStartArray("palet");
                    foreach (var c in sim.Palet) j.WriteStringValue(ColorTranslator.ToHtml(Color.FromArgb(255, c)));
                    j.WriteEndArray();
                    j.WriteString("px", Convert.ToBase64String(sim.Px));
                    j.WriteEndObject();
                }
                j.WriteEndArray();
                j.WriteStartObject("ayarlar");
                foreach (var kv in Ayarlar.Hepsi())
                    if (!YedeklenmeyenAyarlar.Contains(kv.Key))
                        j.WriteString(kv.Key, kv.Value);
                j.WriteEndObject();
                j.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(akis.ToArray());
        }

        void YedekAl()
        {
            renkPenceresiAcik = true;          // pencere açıkken panel kapanmasın
            try
            {
                using var d = new SaveFileDialog
                {
                    Title = "Yedeği kaydet",
                    Filter = "Turatronik Makro Klavye yedeği (*.tmk)|*.tmk",
                    FileName = "turatronik-makro-klavye-" + DateTime.Now.ToString("yyyy-MM-dd") + YedekUzantisi,
                    DefaultExt = "tmk",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                };
                if (d.ShowDialog(this) != DialogResult.OK)
                    return;
                var metin = YedekMetni(out var eksik);
                File.WriteAllText(d.FileName, metin, new System.Text.UTF8Encoding(false));
                Ayarlar.Yaz("SonYedek", DateTime.Now.ToString("d MMMM HH:mm", new System.Globalization.CultureInfo("tr-TR")) + " · " + KisaYol(d.FileName));
                yedekDurum = eksik.Count == 0 ? "tamam:Yedek alındı"
                    : "tamam:Yedek alındı (" + string.Join(", ", eksik) + " klavye bağlı olmadığı için girmedi)";
            }
            catch (Exception ex)
            {
                Gunluk.Hata("yedek alma", ex);
                yedekDurum = "hata:Yedek kaydedilemedi: " + ex.Message;
            }
            finally
            {
                renkPenceresiAcik = false;
                Activate();
                Invalidate();
            }
        }

        static string KisaYol(string yol)
        {
            var masaustu = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            return yol.StartsWith(masaustu, StringComparison.OrdinalIgnoreCase) ? "Masaüstü" + yol.Substring(masaustu.Length) : yol;
        }

        /// <summary>dosya null ise dosya seçtirir. geriDonus: otomatik yedekten (her şey yüklenir, yeni otomatik yedek alınmaz).</summary>
        void YedektenYukle(string dosya, bool geriDonus = false)
        {
            if (dosya == null)
            {
                renkPenceresiAcik = true;
                try
                {
                    using var d = new OpenFileDialog
                    {
                        Title = "Yedek seç",
                        Filter = "Yedek dosyası (*.tmk, *.mkyedek)|*.tmk;*.mkyedek|Bütün dosyalar|*.*",
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    };
                    if (d.ShowDialog(this) != DialogResult.OK)
                        return;
                    dosya = d.FileName;
                }
                finally
                {
                    renkPenceresiAcik = false;
                    Activate();
                }
            }

            try
            {
                using var belge = JsonDocument.Parse(File.ReadAllText(dosya));
                var kok = belge.RootElement;
                bool modlar = geriDonus || Secili("YukModlar");
                bool isik = geriDonus || Secili("YukIsik");
                bool uygulama = geriDonus || Secili("YukUygulama");

                // önce dosyanın tamamını çöz (bozuksa hiçbir şey değişmesin)
                List<ModTanimi> yeniModlar = null;
                if (modlar && kok.TryGetProperty("modlar", out var jm) && jm.ValueKind == JsonValueKind.String)
                    yeniModlar = ModTanimi.Coz(jm.GetString());
                LedAyari yeniLed = null;
                if (isik && kok.TryGetProperty("isik", out var ji) && ji.ValueKind == JsonValueKind.String)
                    yeniLed = LedAyari.Coz(ji.GetString().Split('\t'));
                int[] yeniEkran = null;
                if (isik && kok.TryGetProperty("ekran", out var je) && je.ValueKind == JsonValueKind.Array && je.GetArrayLength() >= 3)
                {
                    yeniEkran = new int[je.GetArrayLength()];          // 3.2: gece modu ve uzun basış da gelsin
                    for (int i = 0; i < yeniEkran.Length; i++) yeniEkran[i] = je[i].GetInt32();
                }
                // 3.2 (BM): çizilen simgeler
                List<OzelSimge> yeniSimgeler = null;
                if (modlar && kok.TryGetProperty("simgeler", out var js) && js.ValueKind == JsonValueKind.Array)
                {
                    yeniSimgeler = new List<OzelSimge>();
                    foreach (var e in js.EnumerateArray())
                    {
                        var sim = new OzelSimge { Ad = e.GetProperty("ad").GetString() ?? "" };
                        int pi = 0;
                        foreach (var r in e.GetProperty("palet").EnumerateArray())
                        {
                            if (pi >= OzelSimge.PaletBoy) break;
                            sim.Palet[pi] = pi == 0 ? Color.Transparent : ColorTranslator.FromHtml(r.GetString());
                            pi++;
                        }
                        var px = Convert.FromBase64String(e.GetProperty("px").GetString() ?? "");
                        for (int k = 0; k < Math.Min(px.Length, sim.Px.Length); k++) sim.Px[k] = (byte)(px[k] & 15);
                        if (sim.Ad != "") yeniSimgeler.Add(sim);
                    }
                }
                var yeniAyarlar = new List<KeyValuePair<string, string>>();
                if (uygulama && kok.TryGetProperty("ayarlar", out var ja) && ja.ValueKind == JsonValueKind.Object)
                    foreach (var p in ja.EnumerateObject())
                        if (!YedeklenmeyenAyarlar.Contains(p.Name) && p.Value.ValueKind == JsonValueKind.String)
                            yeniAyarlar.Add(new KeyValuePair<string, string>(p.Name, p.Value.GetString()));

                bool klavyede = yeniModlar != null || yeniLed != null || yeniEkran != null;
                if (klavyede && !durum.Bagli)
                {
                    yedekDurum = "hata:Klavye bağlı değil; modlar ve ışık yüklenemez. Klavyeyi takıp tekrar dene.";
                    return;
                }
                if (yeniModlar != null && !durum.ModDuzenDestek) yeniModlar = null;
                if (yeniEkran != null && !durum.EkranDestek) yeniEkran = null;

                // şimdiki hâlin otomatik yedeği
                if (!geriDonus)
                {
                    var klasor = Path.Combine(Gunluk.Klasor, "yedekler");
                    Directory.CreateDirectory(klasor);
                    var oto = Path.Combine(klasor, "once-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + YedekUzantisi);
                    File.WriteAllText(oto, YedekMetni(out _), new System.Text.UTF8Encoding(false));
                    Ayarlar.Yaz("OtoYedek", oto);
                }

                var yuklenen = new List<string>();
                if (yeniModlar != null && yeniModlar.Count > 0)
                {
                    ModlarKaydet?.Invoke(yeniModlar);
                    yuklenen.Add("modlar");
                }
                if (yeniLed != null)
                {
                    led = yeniLed;
                    sonLedDegisimi = DateTime.UtcNow;
                    LedAyarIstendi?.Invoke(led.Kopya());
                    yuklenen.Add("ışık");
                }
                if (yeniEkran != null)
                {
                    ekranAyar = yeniEkran;
                    sonEkranDegisimi = DateTime.UtcNow;
                    EkranAyarIstendi?.Invoke((int[])ekranAyar.Clone());
                    yuklenen.Add("ekran");
                }
                if (yeniSimgeler != null)
                {
                    OzelSimge.Hepsi.Clear();
                    OzelSimge.Hepsi.AddRange(yeniSimgeler);
                    OzelSimge.Kaydet();
                    OzelResimleriUnut();
                    OzelSimgelerDegisti?.Invoke();
                    yuklenen.Add($"{yeniSimgeler.Count} simge");
                }
                if (yeniAyarlar.Count > 0)
                {
                    foreach (var kv in yeniAyarlar) Ayarlar.Yaz(kv.Key, kv.Value);
                    temaAdi = Ayarlar.Oku("Tema", temaAdi);
                    if (!Temalar.ContainsKey(temaAdi)) temaAdi = "renkli";
                    panelTuru = Ayarlar.Oku("PanelTuru", panelTuru);
                    KutulariYerlestir();
                    yuklenen.Add("uygulama ayarları");
                }
                yedekDurum = yuklenen.Count == 0 ? "hata:Yedekte seçtiğin bölümlerden hiçbiri yok"
                    : (geriDonus ? "tamam:Önceki hâline dönüldü (" : "tamam:Yüklendi (") + string.Join(", ", yuklenen) + ")";
            }
            catch (Exception ex)
            {
                Gunluk.Hata("yedek yükleme", ex);
                yedekDurum = "hata:Yedek okunamadı: " + ex.Message;
            }
            finally
            {
                Invalidate();
            }
        }
    }
}
