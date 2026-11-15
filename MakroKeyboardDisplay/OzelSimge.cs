using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Uygulama 3.1: kullanıcının çizdiği 24x24 renkli tuş simgesi.
    /// 16 renklik palet (0. renk saydam) + her piksel için 4 bitlik palet sırası.
    /// Kartta flash'ta durur; buradaki kopya ana kopyadır (uygulama klasöründe JSON).
    /// </summary>
    public sealed class OzelSimge
    {
        public const int Boy = 24, PaletBoy = 16, AdUzunlugu = 15;

        public string Ad = "";
        public Color[] Palet = VarsayilanPalet();
        public byte[] Px = new byte[Boy * Boy];     // palet sırası (0: saydam)

        public static Color[] VarsayilanPalet() => new[]
        {
            Color.Transparent,
            Color.FromArgb(255, 255, 255), Color.FromArgb(148, 163, 184), Color.FromArgb(71, 85, 105), Color.FromArgb(15, 23, 42),
            Color.FromArgb(248, 113, 113), Color.FromArgb(239, 68, 68),  Color.FromArgb(251, 146, 60), Color.FromArgb(250, 204, 21),
            Color.FromArgb(74, 222, 128), Color.FromArgb(34, 197, 94),  Color.FromArgb(45, 212, 191), Color.FromArgb(56, 189, 248),
            Color.FromArgb(96, 165, 250), Color.FromArgb(168, 85, 247), Color.FromArgb(244, 114, 182),
        };

        public OzelSimge Kopya() => new OzelSimge
        {
            Ad = Ad,
            Palet = (Color[])Palet.Clone(),
            Px = (byte[])Px.Clone(),
        };

        public bool Bos => Px.All(p => p == 0);

        /// <summary>Karta giden 320 bayt: 16 renk (RGB565, kartın bayt sırası) + 288 bayt piksel.</summary>
        public byte[] Paketle()
        {
            var b = new byte[PaletBoy * 2 + Boy * Boy / 2];
            for (int i = 0; i < PaletBoy; i++)
            {
                ushort c = Renk565(Palet[i]);
                b[i * 2] = (byte)(c >> 8);              // kartta büyük uçlu duruyor
                b[i * 2 + 1] = (byte)(c & 0xFF);
            }
            for (int y = 0; y < Boy; y++)
                for (int x = 0; x < Boy; x++)
                {
                    int d = Px[y * Boy + x] & 15;
                    int p = PaletBoy * 2 + y * (Boy / 2) + x / 2;
                    b[p] |= (byte)((x & 1) == 0 ? d << 4 : d);
                }
            return b;
        }

        public static ushort Renk565(Color c) =>
            (ushort)(((c.R & 0xF8) << 8) | ((c.G & 0xFC) << 3) | (c.B >> 3));

        /// <summary>Önizleme resmi (24x24, saydam zeminli).</summary>
        public Bitmap Resim()
        {
            var b = new Bitmap(Boy, Boy, PixelFormat.Format32bppArgb);
            for (int y = 0; y < Boy; y++)
                for (int x = 0; x < Boy; x++)
                {
                    int d = Px[y * Boy + x] & 15;
                    b.SetPixel(x, y, d == 0 ? Color.Transparent : Color.FromArgb(255, Palet[d]));
                }
            return b;
        }

        // ------------------------------------------------------------------ 3.1 (BG): programın kendi simgesi
        /// <summary>Bir .exe / kısayol dosyasının simgesini 24x24 ve 15 renge indirip simge yapar.</summary>
        public static OzelSimge UygulamadanAl(string yol, string istenenAd)
        {
            try
            {
                using var ham = WindowsSimgesi.Al(yol);
                if (ham == null) return null;
                using var kucuk = new Bitmap(Boy, Boy, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(kucuk))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(ham, new Rectangle(0, 0, Boy, Boy));
                }
                // renkleri 4-4-4'e yuvarlayıp en sık 15 tanesini palet yap
                var sayac = new Dictionary<int, int>();
                var pikseller = new Color[Boy * Boy];
                for (int y = 0; y < Boy; y++)
                    for (int x = 0; x < Boy; x++)
                    {
                        var c = kucuk.GetPixel(x, y);
                        pikseller[y * Boy + x] = c;
                        if (c.A < 128) continue;
                        int anahtar = ((c.R >> 4) << 8) | ((c.G >> 4) << 4) | (c.B >> 4);
                        sayac[anahtar] = sayac.TryGetValue(anahtar, out int k) ? k + 1 : 1;
                    }
                if (sayac.Count == 0) return null;
                var secilen = sayac.OrderByDescending(e => e.Value).Take(PaletBoy - 1)
                                   .Select(e => Color.FromArgb(255, ((e.Key >> 8) & 15) * 17, ((e.Key >> 4) & 15) * 17, (e.Key & 15) * 17))
                                   .ToList();
                var s = new OzelSimge { Ad = BenzersizAd(istenenAd) };
                s.Palet[0] = Color.Transparent;
                for (int i = 0; i < PaletBoy - 1; i++)
                    s.Palet[i + 1] = i < secilen.Count ? secilen[i] : Color.Black;
                for (int i = 0; i < pikseller.Length; i++)
                {
                    var c = pikseller[i];
                    if (c.A < 128) { s.Px[i] = 0; continue; }
                    int eniyi = 1, enyakin = int.MaxValue;
                    for (int k = 0; k < secilen.Count; k++)
                    {
                        int dr = c.R - secilen[k].R, dg = c.G - secilen[k].G, db = c.B - secilen[k].B;
                        int d = dr * dr + dg * dg + db * db;
                        if (d < enyakin) { enyakin = d; eniyi = k + 1; }
                    }
                    s.Px[i] = (byte)eniyi;
                }
                return s.Bos ? null : s;
            }
            catch (Exception ex) { Gunluk.Hata("uygulama simgesi alınamadı", ex); return null; }
        }

        // ------------------------------------------------------------------ kayıt
        static string Dosya => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MakroKeyboardDisplay", "ozelsimgeler.json");

        public static List<OzelSimge> Hepsi { get; private set; } = new List<OzelSimge>();

        public static void Yukle()
        {
            Hepsi = new List<OzelSimge>();
            try
            {
                if (!File.Exists(Dosya)) return;
                using var belge = JsonDocument.Parse(File.ReadAllText(Dosya, Encoding.UTF8));
                foreach (var e in belge.RootElement.EnumerateArray())
                {
                    var s = new OzelSimge { Ad = e.GetProperty("ad").GetString() ?? "" };
                    int i = 0;
                    foreach (var r in e.GetProperty("palet").EnumerateArray())
                    {
                        if (i >= PaletBoy) break;
                        s.Palet[i] = i == 0 ? Color.Transparent : ColorTranslator.FromHtml(r.GetString());
                        i++;
                    }
                    var px = Convert.FromBase64String(e.GetProperty("px").GetString() ?? "");
                    for (int k = 0; k < Math.Min(px.Length, s.Px.Length); k++) s.Px[k] = (byte)(px[k] & 15);
                    s.Palet[0] = Color.Transparent;
                    if (s.Ad != "") Hepsi.Add(s);
                }
            }
            catch (Exception ex) { Gunluk.Hata("özel simgeler okunamadı", ex); }
        }

        public static void Kaydet()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Dosya));
                var sb = new StringBuilder("[");
                for (int i = 0; i < Hepsi.Count; i++)
                {
                    var s = Hepsi[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"ad\":").Append(JsonSerializer.Serialize(s.Ad)).Append(",\"palet\":[");
                    for (int k = 0; k < PaletBoy; k++)
                        sb.Append(k > 0 ? "," : "").Append('"').Append(ColorTranslator.ToHtml(Color.FromArgb(255, s.Palet[k]))).Append('"');
                    sb.Append("],\"px\":\"").Append(Convert.ToBase64String(s.Px)).Append("\"}");
                }
                sb.Append(']');
                File.WriteAllText(Dosya, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex) { Gunluk.Hata("özel simgeler yazılamadı", ex); }
        }

        /// <summary>Ad temizler: küçük harf, boşluksuz, en çok 15 karakter, kartın anlayacağı hâlde.</summary>
        public static string AdTemizle(string ad)
        {
            var sb = new StringBuilder();
            foreach (var c in (ad ?? "").ToLowerInvariant().Trim())
            {
                char d = c switch { 'ı' => 'i', 'ğ' => 'g', 'ü' => 'u', 'ş' => 's', 'ö' => 'o', 'ç' => 'c', ' ' => '_', _ => c };
                if ((d >= 'a' && d <= 'z') || (d >= '0' && d <= '9') || d == '_' || d == '-') sb.Append(d);
                if (sb.Length >= AdUzunlugu) break;
            }
            return sb.ToString();
        }

        public static string BenzersizAd(string istek, OzelSimge haric = null)
        {
            string taban = AdTemizle(istek);
            if (taban == "") taban = "simge";
            string ad = taban;
            for (int i = 2; Hepsi.Any(s => s != haric && s.Ad == ad); i++)
                ad = (taban.Length > AdUzunlugu - 2 ? taban.Substring(0, AdUzunlugu - 2) : taban) + i;
            return ad;
        }
    }
}
