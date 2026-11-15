using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Kullanım istatistiği (uygulama 2.5): hangi tuşa kaç kez basıldı, günlük basış sayısı, modlarda geçen süre.
    /// %APPDATA%\MakroKeyboardDisplay\istatistik.json; dakikada bir ve çıkarken yazılır.
    /// </summary>
    public sealed class IstatistikVerisi
    {
        public Dictionary<string, int> Tuslar { get; set; } = new();       // "MOD\tTuş adı\tk|u" -> basış
        public Dictionary<string, int> Gunler { get; set; } = new();       // "yyyy-MM-dd" -> basış
        public Dictionary<string, double> ModSure { get; set; } = new();   // mod adı -> saniye
        public int SolTeker { get; set; }
        public int SagTeker { get; set; }
        public DateTime Baslangic { get; set; } = DateTime.Now;
    }

    public static class Istatistik
    {
        static readonly string Dosya = Path.Combine(Gunluk.Klasor, "istatistik.json");
        static IstatistikVerisi veri;
        static bool kirli;
        static DateTime sonYazma = DateTime.UtcNow;

        public static IstatistikVerisi Veri => veri ??= Yukle();

        static IstatistikVerisi Yukle()
        {
            try
            {
                if (File.Exists(Dosya))
                    return JsonSerializer.Deserialize<IstatistikVerisi>(File.ReadAllText(Dosya)) ?? new IstatistikVerisi();
            }
            catch (Exception ex) { Gunluk.Hata("istatistik okunamadı", ex); }
            return new IstatistikVerisi();
        }

        public static void Tus(string mod, string tus, bool uzun)
        {
            var v = Veri;
            var anahtar = (mod ?? "?") + "\t" + (string.IsNullOrWhiteSpace(tus) ? "—" : tus) + "\t" + (uzun ? "u" : "k");
            v.Tuslar[anahtar] = v.Tuslar.TryGetValue(anahtar, out int n) ? n + 1 : 1;
            var gun = DateTime.Today.ToString("yyyy-MM-dd");
            v.Gunler[gun] = v.Gunler.TryGetValue(gun, out int g) ? g + 1 : 1;
            kirli = true;
        }

        public static void Teker(bool sol)
        {
            if (sol) Veri.SolTeker++;
            else Veri.SagTeker++;
            kirli = true;
        }

        public static void ModSuresi(string mod, double saniye)
        {
            if (string.IsNullOrEmpty(mod) || saniye <= 0 || saniye > 10) return;
            var v = Veri;
            v.ModSure[mod] = (v.ModSure.TryGetValue(mod, out double s) ? s : 0) + saniye;
            kirli = true;
        }

        public static int Bugun => Veri.Gunler.TryGetValue(DateTime.Today.ToString("yyyy-MM-dd"), out int n) ? n : 0;
        public static int Toplam => Veri.Gunler.Values.Sum();

        /// <summary>Son "gun" günün basışları (eskiden yeniye).</summary>
        public static int[] SonGunler(int gun)
        {
            var d = new int[gun];
            for (int i = 0; i < gun; i++)
                d[i] = Veri.Gunler.TryGetValue(DateTime.Today.AddDays(i - gun + 1).ToString("yyyy-MM-dd"), out int n) ? n : 0;
            return d;
        }

        /// <summary>En çok basılan tuşlar: (mod, tuş, uzun mu, sayı).</summary>
        public static List<(string mod, string tus, bool uzun, int sayi)> EnCok(int adet) =>
            Veri.Tuslar.OrderByDescending(kv => kv.Value).Take(adet).Select(kv =>
            {
                var p = kv.Key.Split('\t');
                return (p[0], p.Length > 1 ? p[1] : "", p.Length > 2 && p[2] == "u", kv.Value);
            }).ToList();

        public static void Kaydet(bool zorla = false)
        {
            if (!kirli || (!zorla && (DateTime.UtcNow - sonYazma).TotalSeconds < 60)) return;
            try
            {
                // 60 günden eski günler silinir
                var sinir = DateTime.Today.AddDays(-60).ToString("yyyy-MM-dd");
                foreach (var k in Veri.Gunler.Keys.Where(k => string.CompareOrdinal(k, sinir) < 0).ToList())
                    Veri.Gunler.Remove(k);
                Directory.CreateDirectory(Gunluk.Klasor);
                File.WriteAllText(Dosya, JsonSerializer.Serialize(Veri));
                kirli = false;
                sonYazma = DateTime.UtcNow;
            }
            catch (Exception ex) { Gunluk.Hata("istatistik yazılamadı", ex); }
        }

        public static void Sifirla()
        {
            veri = new IstatistikVerisi();
            kirli = true;
            Kaydet(true);
        }
    }

    /// <summary>
    /// Ayar geçmişi (uygulama 2.5): modlar klavyeye her yazılmadan önce klavyedeki hâli saklanır (son 20).
    /// %APPDATA%\MakroKeyboardDisplay\gecmis\yyyyMMdd-HHmmss.json
    /// </summary>
    public static class AyarGecmisi
    {
        public const int EnFazla = 20;
        static readonly string Klasor = Path.Combine(Gunluk.Klasor, "gecmis");

        public sealed class Kayit
        {
            public string Dosya;
            public DateTime Zaman;
            public List<ModTanimi> Modlar;
        }

        public static void Sakla(List<ModTanimi> modlar)
        {
            if (modlar == null || modlar.Count == 0) return;
            try
            {
                var json = ModTanimi.Json(modlar);
                Directory.CreateDirectory(Klasor);
                var dosyalar = Directory.GetFiles(Klasor, "*.json").OrderByDescending(f => f).ToList();
                if (dosyalar.Count > 0 && File.ReadAllText(dosyalar[0]) == json)
                    return;                                   // son kayıtla aynı: tekrar saklama
                File.WriteAllText(Path.Combine(Klasor, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"), json);
                foreach (var eski in Directory.GetFiles(Klasor, "*.json").OrderByDescending(f => f).Skip(EnFazla))
                    File.Delete(eski);
            }
            catch (Exception ex) { Gunluk.Hata("ayar geçmişi", ex); }
        }

        public static List<Kayit> Liste(int adet)
        {
            var l = new List<Kayit>();
            try
            {
                if (!Directory.Exists(Klasor)) return l;
                foreach (var f in Directory.GetFiles(Klasor, "*.json").OrderByDescending(f => f).Take(adet))
                {
                    if (!DateTime.TryParseExact(Path.GetFileNameWithoutExtension(f), "yyyyMMdd-HHmmss",
                            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var z))
                        continue;
                    try { l.Add(new Kayit { Dosya = f, Zaman = z, Modlar = ModTanimi.Coz(File.ReadAllText(f)) }); }
                    catch (Exception) { }
                }
            }
            catch (Exception ex) { Gunluk.Hata("ayar geçmişi", ex); }
            return l;
        }
    }
}
