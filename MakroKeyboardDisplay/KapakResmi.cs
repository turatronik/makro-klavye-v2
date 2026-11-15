using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Kapak resmini klavyenin anladığı biçime çevirir: kare, 96x96, satır satır base64.
    ///   Kart 1.7+: bu kapağa özel 256 renk (palet) + piksel başına 1 bayt  -> RGB565'in yarısı kadar veri
    ///   Eski kart: RGB565 (little endian), piksel başına 2 bayt
    /// </summary>
    public static class KapakResmi
    {
        public const int Boyut = 96;

        /// <summary>Kartın 1.7+ biçimi: palet (768 bayt RGB, base64) ve 96 satır (her biri 96 bayt sıra numarası, base64).</summary>
        public static List<string> PaletliSatirlar(Image kaynak, out string palet)
        {
            using var hedef = KareYap(kaynak);
            var piksel = Pikseller(hedef);                 // r, g, b sırayla
            var renkler = MedyanKesim(piksel, 256);
            var pal = new byte[768];
            for (int i = 0; i < renkler.Count; i++)
            {
                pal[i * 3] = (byte)renkler[i][0];
                pal[i * 3 + 1] = (byte)renkler[i][1];
                pal[i * 3 + 2] = (byte)renkler[i][2];
            }
            palet = Convert.ToBase64String(pal);

            // Floyd–Steinberg titreşimi: 256 renkte geçişler bantlanmasın
            var hata = new float[Boyut * Boyut * 3];
            for (int i = 0; i < piksel.Length; i++) hata[i] = piksel[i];
            var onbellek = new Dictionary<int, byte>();
            var satirlar = new List<string>(Boyut);
            var satir = new byte[Boyut];
            for (int y = 0; y < Boyut; y++)
            {
                for (int x = 0; x < Boyut; x++)
                {
                    int i = (y * Boyut + x) * 3;
                    int r = Sinir(hata[i]), g = Sinir(hata[i + 1]), b = Sinir(hata[i + 2]);
                    int anahtar = (r >> 2 << 12) | (g >> 2 << 6) | (b >> 2);
                    if (!onbellek.TryGetValue(anahtar, out byte en))
                    {
                        en = EnYakin(renkler, r, g, b);
                        onbellek[anahtar] = en;
                    }
                    satir[x] = en;
                    var c = renkler[en];
                    Yay(hata, x, y, r - c[0], g - c[1], b - c[2]);
                }
                satirlar.Add(Convert.ToBase64String(satir));
            }
            return satirlar;
        }

        static int Sinir(float v) => v < 0 ? 0 : v > 255 ? 255 : (int)(v + 0.5f);

        static void Yay(float[] h, int x, int y, int er, int eg, int eb)
        {
            void Ekle(int xx, int yy, float k)
            {
                if (xx < 0 || xx >= Boyut || yy >= Boyut) return;
                int j = (yy * Boyut + xx) * 3;
                h[j] += er * k; h[j + 1] += eg * k; h[j + 2] += eb * k;
            }
            Ekle(x + 1, y, 7 / 16f);
            Ekle(x - 1, y + 1, 3 / 16f);
            Ekle(x, y + 1, 5 / 16f);
            Ekle(x + 1, y + 1, 1 / 16f);
        }

        static byte EnYakin(List<int[]> renkler, int r, int g, int b)
        {
            int en = 0, enUzaklik = int.MaxValue;
            for (int i = 0; i < renkler.Count; i++)
            {
                var c = renkler[i];
                int dr = r - c[0], dg = g - c[1], db = b - c[2];
                int u = dr * dr * 3 + dg * dg * 4 + db * db * 2;      // göz yeşile daha duyarlı
                if (u < enUzaklik) { enUzaklik = u; en = i; }
            }
            return (byte)en;
        }

        /// <summary>Medyan kesim: renk uzayını en geniş kenarından ikiye bölerek en fazla n renk seçer.</summary>
        static List<int[]> MedyanKesim(byte[] piksel, int n)
        {
            int adet = piksel.Length / 3;
            var kutular = new List<List<int>> { new List<int>(adet) };
            for (int i = 0; i < adet; i++) kutular[0].Add(i);

            while (kutular.Count < n)
            {
                // en geniş renk aralığı × piksel sayısı en büyük olan kutuyu böl
                int sec = -1, kanal = 0;
                long enPuan = 0;
                for (int k = 0; k < kutular.Count; k++)
                {
                    var kutu = kutular[k];
                    if (kutu.Count < 2) continue;
                    for (int c = 0; c < 3; c++)
                    {
                        int mn = 255, mx = 0;
                        foreach (var p in kutu) { int v = piksel[p * 3 + c]; if (v < mn) mn = v; if (v > mx) mx = v; }
                        long puan = (long)(mx - mn) * kutu.Count;
                        if (puan > enPuan) { enPuan = puan; sec = k; kanal = c; }
                    }
                }
                if (sec < 0) break;                      // bölünecek renk kalmadı (az renkli kapak)
                var b = kutular[sec];
                int kk = kanal;
                b.Sort((p, q) => piksel[p * 3 + kk].CompareTo(piksel[q * 3 + kk]));
                int orta = b.Count / 2;
                kutular[sec] = b.GetRange(0, orta);
                kutular.Add(b.GetRange(orta, b.Count - orta));
            }

            var renkler = new List<int[]>(kutular.Count);
            foreach (var kutu in kutular)
            {
                if (kutu.Count == 0) continue;
                long r = 0, g = 0, bl = 0;
                foreach (var p in kutu) { r += piksel[p * 3]; g += piksel[p * 3 + 1]; bl += piksel[p * 3 + 2]; }
                renkler.Add(new[] { (int)(r / kutu.Count), (int)(g / kutu.Count), (int)(bl / kutu.Count) });
            }
            return renkler;
        }

        /// <summary>
        /// Işıklar için kapağın 4 baskın, canlı rengi (kart 1.9 "LP"). Renk tonuna göre 12 kovaya ayrılır;
        /// doygun ve parlak pikseller daha çok sayılır. Gri bir kapakta en açık tonlar kullanılır.
        /// LED'de soluk renkler kötü göründüğü için renkler biraz canlandırılır.
        /// </summary>
        public static Color[] BaskinRenkler(Image kaynak, int adet = 4)
        {
            using var hedef = KareYap(kaynak);
            var p = Pikseller(hedef);
            const int K = 12;
            var agirlik = new double[K];
            var top = new double[K, 3];
            double griA = 0, griR = 0, griG = 0, griB = 0;
            for (int i = 0; i < p.Length; i += 9)               // her 3. piksel yeter
            {
                int r = p[i], g = p[i + 1], b = p[i + 2];
                var c = Color.FromArgb(r, g, b);
                float s = c.GetSaturation(), v = Math.Max(r, Math.Max(g, b)) / 255f;
                if (s < 0.2f || v < 0.18f)
                {
                    double ag = v * v;
                    griA += ag; griR += r * ag; griG += g * ag; griB += b * ag;
                    continue;
                }
                int k = (int)(c.GetHue() / 360f * K) % K;
                double w = s * s * v;
                agirlik[k] += w;
                top[k, 0] += r * w; top[k, 1] += g * w; top[k, 2] += b * w;
            }
            var sira = new List<int>();
            for (int k = 0; k < K; k++) if (agirlik[k] > 0) sira.Add(k);
            sira.Sort((a, b) => agirlik[b].CompareTo(agirlik[a]));
            var sonuc = new List<Color>();
            double enBuyuk = sira.Count > 0 ? agirlik[sira[0]] : 0;
            foreach (int k in sira)
            {
                if (sonuc.Count >= adet || agirlik[k] < enBuyuk * 0.04) break;
                sonuc.Add(Canlandir(top[k, 0] / agirlik[k], top[k, 1] / agirlik[k], top[k, 2] / agirlik[k]));
            }
            if (sonuc.Count == 0)
            {
                if (griA <= 0) return new[] { Color.White };
                int gr = (int)(griR / griA), gg = (int)(griG / griA), gb = (int)(griB / griA);
                sonuc.Add(Color.FromArgb(gr, gg, gb));
                sonuc.Add(Color.FromArgb(gr / 3, gg / 3, gb / 3));
            }
            // tek renkli kapakta akış olsun diye aynı tonun koyusu eklenir
            if (sonuc.Count == 1)
            {
                var c = sonuc[0];
                sonuc.Add(Color.FromArgb(c.R / 3, c.G / 3, c.B / 3));
            }
            return sonuc.ToArray();
        }

        /// <summary>Rengin en parlak kanalını 255'e çeker ve doygunluğu biraz artırır.</summary>
        static Color Canlandir(double r, double g, double b)
        {
            double m = Math.Max(r, Math.Max(g, b));
            if (m < 1) return Color.Black;
            double k = 255 / m;
            r *= k; g *= k; b *= k;
            double en = Math.Min(r, Math.Min(g, b));
            double azalt = 0.35;                            // en düşük kanalı azalt: daha doygun
            r -= en * azalt; g -= en * azalt; b -= en * azalt;
            return Color.FromArgb((int)Math.Clamp(r, 0, 255), (int)Math.Clamp(g, 0, 255), (int)Math.Clamp(b, 0, 255));
        }

        static Bitmap KareYap(Image kaynak)
        {
            var hedef = new Bitmap(Boyut, Boyut, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(hedef);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.Clear(Color.Black);
            int kenar = Math.Min(kaynak.Width, kaynak.Height);
            var kirp = new Rectangle((kaynak.Width - kenar) / 2, (kaynak.Height - kenar) / 2, kenar, kenar);
            using var ia = new ImageAttributes();
            ia.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(kaynak, new Rectangle(0, 0, Boyut, Boyut), kirp.X, kirp.Y, kirp.Width, kirp.Height, GraphicsUnit.Pixel, ia);
            return hedef;
        }

        static byte[] Pikseller(Bitmap bmp)
        {
            var veri = bmp.LockBits(new Rectangle(0, 0, Boyut, Boyut), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var sonuc = new byte[Boyut * Boyut * 3];
            try
            {
                var ham = new byte[veri.Stride];
                for (int y = 0; y < Boyut; y++)
                {
                    Marshal.Copy(veri.Scan0 + y * veri.Stride, ham, 0, veri.Stride);
                    for (int x = 0; x < Boyut; x++)
                    {
                        int j = (y * Boyut + x) * 3;
                        sonuc[j] = ham[x * 3 + 2];       // BGR -> RGB
                        sonuc[j + 1] = ham[x * 3 + 1];
                        sonuc[j + 2] = ham[x * 3];
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(veri);
            }
            return sonuc;
        }

        public static List<string> Satirlar(Image kaynak)
        {
            using var hedef = new Bitmap(Boyut, Boyut, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(hedef))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.Clear(Color.Black);
                // ortadan kare kırp (Spotify ve tarayıcılar bazen geniş resim verir)
                int kenar = Math.Min(kaynak.Width, kaynak.Height);
                var kirp = new Rectangle((kaynak.Width - kenar) / 2, (kaynak.Height - kenar) / 2, kenar, kenar);
                using var ia = new ImageAttributes();
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(kaynak, new Rectangle(0, 0, Boyut, Boyut), kirp.X, kirp.Y, kirp.Width, kirp.Height, GraphicsUnit.Pixel, ia);
            }

            var veri = hedef.LockBits(new Rectangle(0, 0, Boyut, Boyut), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var satirlar = new List<string>(Boyut);
            try
            {
                var ham = new byte[veri.Stride];
                var satir = new byte[Boyut * 2];
                for (int y = 0; y < Boyut; y++)
                {
                    Marshal.Copy(veri.Scan0 + y * veri.Stride, ham, 0, veri.Stride);
                    for (int x = 0; x < Boyut; x++)
                    {
                        int b = ham[x * 3], gr = ham[x * 3 + 1], r = ham[x * 3 + 2];
                        int v = ((r >> 3) << 11) | ((gr >> 2) << 5) | (b >> 3);
                        satir[x * 2] = (byte)(v & 0xFF);
                        satir[x * 2 + 1] = (byte)(v >> 8);
                    }
                    satirlar.Add(Convert.ToBase64String(satir));
                }
            }
            finally
            {
                hedef.UnlockBits(veri);
            }
            return satirlar;
        }
    }
}
