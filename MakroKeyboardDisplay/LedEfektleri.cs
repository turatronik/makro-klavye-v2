using System;
using System.Collections.Generic;
using System.Drawing;

namespace MakroKeyboardDisplay
{
    /// <summary>Işık sekmesinin seçimleri (kart 1.9 "LED" satırıyla aynı alanlar).</summary>
    public sealed class LedAyari
    {
        public string Efekt = "modrengi";
        public Color Renk = Color.FromArgb(168, 85, 247);
        public int Parlaklik = 35;          // 3-100
        public int Hiz = 100;               // 20-200
        public Color[] Akis = { Color.FromArgb(255, 45, 85), Color.FromArgb(168, 85, 247), Color.White };
        // kart 1.10+: tuşlar (6 LED) kendi efekt, renk, hız ve akış renkleriyle çalışabilir.
        // TusAyri kapalıyken tuşlar yukarıdaki (zemin) ayarını kullanır. Parlaklık ortaktır.
        public bool TusAyri;
        public LedAyari Tus;                // null değil: Efekt, Renk, Hiz, Akis kullanılır

        public LedAyari Kopya() => new LedAyari
        {
            Efekt = Efekt, Renk = Renk, Parlaklik = Parlaklik, Hiz = Hiz, Akis = (Color[])Akis.Clone(),
            TusAyri = TusAyri, Tus = Tus?.Kopya(),
        };

        static string Hex(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";

        public object[] TusAlanlari()
        {
            var b = Tus ?? this;
            return new object[] { "LEDTUS", TusAyri ? "1" : "0", b.Efekt, Hex(b.Renk), b.Hiz, string.Join(",", Array.ConvertAll(b.Akis, c => Hex(c))) };
        }

        public object[] Alanlar() => new object[] { "LED", Efekt, Hex(Renk), Parlaklik, Hiz, string.Join(",", Array.ConvertAll(Akis, c => Hex(c))) };

        /// <summary>Karttan gelen "LED efekt renk parlaklık hız akış" satırı.</summary>
        public static LedAyari Coz(string[] a)
        {
            var l = new LedAyari();
            if (a.Length >= 2) l.Efekt = a[1];
            if (a.Length >= 3) l.Renk = RenkCoz(a[2], l.Renk);
            if (a.Length >= 4 && int.TryParse(a[3], out int p)) l.Parlaklik = Math.Clamp(p, 3, 100);
            if (a.Length >= 5 && int.TryParse(a[4], out int h)) l.Hiz = Math.Clamp(h, 20, 200);
            if (a.Length >= 6)
            {
                var r = a[5].Split(',');
                if (r.Length == 3) l.Akis = new[] { RenkCoz(r[0], l.Akis[0]), RenkCoz(r[1], l.Akis[1]), RenkCoz(r[2], l.Akis[2]) };
            }
            // kart 1.10+: ayrı mı, tuş efekti, renk, hız, akış
            if (a.Length >= 11)
            {
                l.TusAyri = a[6] == "1";
                var t = new LedAyari { Efekt = a[7], Renk = RenkCoz(a[8], l.Renk) };
                if (int.TryParse(a[9], out int th)) t.Hiz = Math.Clamp(th, 20, 200);
                var r = a[10].Split(',');
                if (r.Length == 3) t.Akis = new[] { RenkCoz(r[0], t.Akis[0]), RenkCoz(r[1], t.Akis[1]), RenkCoz(r[2], t.Akis[2]) };
                l.Tus = t;
            }
            return l;
        }

        public static Color RenkCoz(string s, Color varsayilan)
        {
            if (s != null && s.Length == 6 && int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out int v))
                return Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255);
            return varsayilan;
        }
    }

    /// <summary>Efekt listesi ve panelin LED haritası için önizleme (kartın isiklar.py'siyle aynı hesap).</summary>
    public static class LedEfektleri
    {
        public sealed class Bilgi
        {
            public string Kod, Ad, Aciklama;
            public bool Muzik;
            public int RenkKullanimi;         // 0: kendi renkleri, 1: tek renk, 3: üç renk (akış)
            public Color[] Serit;            // çipteki küçük şerit (null: seçili renkten)
        }

        static Color H(int r, int g, int b) => Color.FromArgb(r, g, b);

        public static readonly Bilgi[] Liste =
        {
            new Bilgi { Kod = "kapak", Ad = "Kapak renkleri", Muzik = true, Aciklama = "Çalan şarkının kapağındaki renkler çevrede yavaşça akar.", Serit = new[] { H(255, 95, 109), H(226, 54, 110), H(91, 42, 134), H(255, 179, 107) } },
            new Bilgi { Kod = "ritim", Ad = "Ritim", Muzik = true, Aciklama = "Taban her vuruşta parlar, renk kapaktan sırayla seçilir.", Serit = new[] { H(255, 255, 255), H(226, 54, 110), H(255, 255, 255), H(226, 54, 110) } },
            new Bilgi { Kod = "stereo", Ad = "Stereo VU", Muzik = true, Aciklama = "Sol taraf sol kanalı, sağ taraf sağ kanalı gösterir.", Serit = new[] { H(48, 209, 88), H(255, 214, 10), H(255, 45, 85) } },
            new Bilgi { Kod = "ilerleme", Ad = "Şarkı ilerlemesi", Muzik = true, Aciklama = "Taban halkası şarkı ilerledikçe dolar.", Serit = new[] { H(226, 54, 110), H(226, 54, 110), H(58, 36, 64) } },
            new Bilgi { Kod = "modrengi", Ad = "Mod rengi", Aciklama = "Taban mod renginde nefes alır, tuşlar sönük yanar.", Serit = new[] { H(255, 40, 120), H(0, 160, 255), H(255, 140, 0), H(90, 230, 60) } },
            new Bilgi { Kod = "sabit", Ad = "Sabit renk", RenkKullanimi = 1, Aciklama = "Bütün LED'ler seçtiğin renkte." },
            new Bilgi { Kod = "nefes", Ad = "Nefes", RenkKullanimi = 1, Aciklama = "Seçtiğin renk yavaşça söner ve yanar." },
            new Bilgi { Kod = "kalp", Ad = "Kalp atışı", RenkKullanimi = 1, Aciklama = "İki kısa vuruş, bir bekleme." },
            new Bilgi { Kod = "kuyruk", Ad = "Kuyruklu yıldız", RenkKullanimi = 1, Aciklama = "Bir ışık izi çevrede döner." },
            new Bilgi { Kod = "tarayici", Ad = "Tarayıcı", RenkKullanimi = 1, Aciklama = "Işık sağa sola gidip gelir." },
            new Bilgi { Kod = "takip", Ad = "Takip", RenkKullanimi = 1, Aciklama = "Her üç LED'den biri yanar ve kayar." },
            new Bilgi { Kod = "doldur", Ad = "Doldur", RenkKullanimi = 1, Aciklama = "Renk çevreyi doldurur, sonra boşaltır." },
            new Bilgi { Kod = "pirilti", Ad = "Pırıltı", RenkKullanimi = 1, Aciklama = "Rastgele LED'ler beyaz parlar." },
            new Bilgi { Kod = "akis", Ad = "Akış", RenkKullanimi = 3, Aciklama = "Üç rengin arasında akış." },
            new Bilgi { Kod = "gokkusagi", Ad = "Gökkuşağı", Aciklama = "Renkler klavyenin çevresinde döner.", Serit = new[] { H(255, 0, 0), H(255, 200, 0), H(0, 255, 80), H(0, 160, 255), H(200, 0, 255) } },
            new Bilgi { Kod = "gecis", Ad = "Renk geçişi", Aciklama = "Bütün LED'ler birlikte renk değiştirir.", Serit = new[] { H(255, 45, 85), H(168, 85, 247), H(59, 130, 246) } },
            new Bilgi { Kod = "dalga", Ad = "Dalga", RenkKullanimi = 1, Aciklama = "Parlaklık dalgası soldan sağa geçer." },
            new Bilgi { Kod = "ates", Ad = "Ateş", Aciklama = "Titreyen kor rengi.", Serit = new[] { H(124, 29, 6), H(249, 115, 22), H(250, 204, 21), H(249, 115, 22) } },
            new Bilgi { Kod = "konfeti", Ad = "Konfeti", Aciklama = "Rastgele renkli kıvılcımlar.", Serit = new[] { H(255, 45, 85), H(255, 214, 10), H(34, 211, 238), H(168, 85, 247) } },
            new Bilgi { Kod = "polis", Ad = "Polis", Aciklama = "Sol kırmızı, sağ mavi çakar.", Serit = new[] { H(255, 45, 85), H(255, 45, 85), H(59, 130, 246), H(59, 130, 246) } },
            new Bilgi { Kod = "kapali", Ad = "Kapalı", Aciklama = "Işıklar kapalı.", Serit = new[] { H(60, 60, 60), H(40, 40, 40) } },
        };

        /// <summary>T1..T6 tuşunun zincirdeki LED'i (kartın TUS_LED listesi).</summary>
        public static readonly int[] TusLed = { 14, 13, 12, 15, 16, 17 };

        public static Bilgi Bul(string kod)
        {
            foreach (var b in Liste) if (b.Kod == kod) return b;
            return Liste[4];
        }

        public static bool MuzikMi(string kod) => Bul(kod).Muzik;

        // ------------------------------------------------------------ önizleme
        // LED'lerin klavyedeki konumu (mm): 0-11 taban (zincir sırası), 12-17 tuş LED'leri
        public static readonly PointF[] Konum =
        {
            new(-24, 28.09f), new(-32, 14), new(-31, -22), new(-31, -35.5f), new(-25.9f, -46.5f), new(-8.6f, -46.5f),
            new(8.6f, -46.5f), new(25.9f, -46.5f), new(40, -40), new(40, -22), new(32, 14), new(24, 28.09f),
            new(25, -8.1f), new(0, -8.1f), new(-25, -8.1f), new(-25, -56.9f), new(0, -56.9f), new(25, -56.9f),
        };
        static readonly float[] UX = Array.ConvertAll(Konum, p => (p.X + 32) / 72f);
        static readonly double[] Halka = Olustur();
        static double[] Olustur()
        {
            var h = new double[18];
            for (int i = 0; i < 18; i++) h[i] = i < 12 ? i / 12.0 : UX[i] * 0.999;
            return h;
        }

        static readonly Random Rast = new Random();
        static readonly double[] Tohum = Tohumla();
        static double[] Tohumla() { var t = new double[18]; for (int i = 0; i < 18; i++) t[i] = Rast.NextDouble() * 100 + i; return t; }
        static readonly double[] KonfetiH = new double[18], KonfetiK = new double[18];
        static double ort = 0.2, zarf, sonVurus = -10;
        static int vurus;

        /// <summary>18 LED'in rengi. t: saniye (hız ölçeksiz), modRengi: klavyenin etkin modunun rengi.</summary>
        /// <summary>zemin: 12 taban LED'inin ayarı; tus: 6 tuş LED'inin ayarı (null: zeminle aynı).</summary>
        public static Color[] Hesapla(LedAyari zemin, LedAyari tusBolge, double t, double dt, Color modRengi, Color[] kapak, bool caliyor, double oran, double sol, double sag, bool sesVar)
        {
            var sonuc = new Color[18];
            tusBolge ??= zemin;
            if (zemin.Efekt == "ritim" || tusBolge.Efekt == "ritim")
            {
                double lv = (sol + sag) / 2;
                ort += (lv - ort) * 0.05;
                if (lv > ort * 1.25 + 0.04 && t - sonVurus > 0.22) { sonVurus = t; vurus++; zarf = 1; }
                zarf = Math.Max(0, zarf - dt * 4.5);
            }
            for (int i = 0; i < 18; i++)
            {
                double u = Halka[i], x = UX[i];
                bool tus = i >= 12;
                var a = tus ? tusBolge : zemin;
                double T = t * a.Hiz / 100.0;
                var r = a.Renk;
                var pal = kapak != null && kapak.Length > 0 ? kapak : new[] { modRengi, a.Renk, Color.White };
                Color c;
                switch (a.Efekt)
                {
                    case "modrengi": c = Olc(modRengi, tus ? 0.35 : 0.25 + 0.2 * (0.5 + 0.5 * Math.Sin(t * 1.6))); break;
                    case "sabit": c = r; break;
                    case "nefes": c = Olc(r, 0.12 + 0.88 * (0.5 + 0.5 * Math.Sin(T * 2))); break;
                    case "kalp":
                        {
                            double f = (T * 0.9) % 1;
                            double k = Math.Exp(-Math.Pow((f - 0.08) / 0.04, 2)) + 0.7 * Math.Exp(-Math.Pow((f - 0.26) / 0.05, 2));
                            c = Olc(r, 0.08 + 0.92 * Math.Min(1, k)); break;
                        }
                    case "kuyruk":
                        {
                            double bas = (T * 0.45) % 1;
                            c = tus ? Olc(r, 0.15 + 0.85 * Math.Max(0, 1 - Math.Abs(x - bas) * 3))
                                    : Olc(r, Math.Max(0.03, Math.Pow(1 - Mod(bas - u), 6)));
                            break;
                        }
                    case "tarayici": { double p = 0.5 + 0.5 * Math.Sin(T * 2.2); c = Olc(r, Math.Max(0.03, 1 - Math.Abs(x - p) * 3.2)); break; }
                    case "takip": c = Olc(r, (i + (int)(T * 6)) % 3 == 0 ? 1 : 0.06); break;
                    case "doldur": { double f = T * 0.35 % 2; bool d = f < 1 ? u < f : u >= f - 1; c = Olc(r, d ? 1 : 0.04); break; }
                    case "pirilti":
                        {
                            double n = Math.Sin(T * 7 + Tohum[i] * 13.1) * Math.Sin(T * 3.3 + Tohum[i]);
                            c = n > 0.82 ? Color.White : Olc(r, 0.25); break;
                        }
                    case "akis": c = Paletten(a.Akis, u - T * 0.18); break;
                    case "gokkusagi": c = Hsv(u - T * 0.15, 1); break;
                    case "gecis": c = Hsv(T * 0.08, 1); break;
                    case "dalga": { double v = 0.5 + 0.5 * Math.Sin((x * 2 - T * 1.2) * Math.PI); c = Olc(r, 0.1 + 0.9 * v * v); break; }
                    case "ates":
                        {
                            double n = 0.55 + 0.45 * Math.Sin(T * 9 + Tohum[i] * 5) * Math.Sin(T * 4.1 + Tohum[i] * 2);
                            n = Math.Clamp(n + (Rast.NextDouble() - 0.5) * 0.25, 0, 1);
                            c = Karistir(Color.FromArgb(140, 20, 0), Color.FromArgb(255, 190, 40), n); break;
                        }
                    case "konfeti":
                        {
                            KonfetiK[i] *= 0.94;
                            if (Rast.NextDouble() < 0.02 * a.Hiz / 100.0) { KonfetiK[i] = 1; KonfetiH[i] = Rast.NextDouble(); }
                            c = Hsv(KonfetiH[i], KonfetiK[i]); break;
                        }
                    case "polis":
                        {
                            double f = T * 2 % 1; bool solYan = x < 0.5;
                            bool yan = ((int)(T * 2) % 2 == 0) ? solYan : !solYan;
                            bool strob = f < 0.15 || (f > 0.25 && f < 0.4);
                            c = yan && strob ? (solYan ? Color.FromArgb(255, 30, 60) : Color.FromArgb(40, 90, 255)) : Color.Black; break;
                        }
                    case "kapali": c = Color.Black; break;
                    case "kapak": c = Olc(Paletten(pal, u + (caliyor ? t * 0.06 : 0)), caliyor ? 0.75 + 0.25 * Math.Sin(t * 1.3 + u * 6) : 0.25); break;
                    case "ritim":
                        {
                            if (!caliyor || !sesVar) goto case "kapak";
                            var ana = pal[vurus % pal.Length];
                            c = tus ? Karistir(Olc(ana, 0.25), Color.White, zarf * 0.85) : Olc(ana, 0.12 + 0.88 * zarf); break;
                        }
                    case "stereo":
                        {
                            if (!caliyor || !sesVar) goto case "kapak";
                            if (tus) { c = Olc(pal[0], 0.15 + 0.85 * (x < 0.5 ? sol : sag)); break; }
                            bool s = i < 6; int sira = s ? i : 11 - i;
                            double kademe = (5 - sira) / 5.0, lv = s ? sol : sag;
                            var g = Karistir(Color.FromArgb(48, 209, 88), Color.FromArgb(255, 214, 10), kademe);
                            c = kademe <= lv ? (kademe > 0.85 ? Color.FromArgb(255, 45, 85) : g) : Olc(g, 0.05); break;
                        }
                    case "ilerleme":
                        c = tus ? Olc(pal[Math.Min(2, pal.Length - 1)], caliyor ? 0.3 + 0.2 * Math.Sin(t * 3) : 0.15)
                                : (u <= oran ? pal[0] : Olc(pal[0], 0.06));
                        break;
                    default: c = Color.Black; break;
                }
                sonuc[i] = c;
            }
            return sonuc;
        }

        static double Mod(double v) => ((v % 1) + 1) % 1;

        public static Color Olc(Color c, double k)
        {
            k = Math.Clamp(k, 0, 1);
            return Color.FromArgb((int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
        }

        public static Color Karistir(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static Color Paletten(Color[] pal, double u)
        {
            int n = pal.Length;
            u = Mod(u) * n;
            int i = (int)u;
            double f = u - i;
            f = f * f * (3 - 2 * f);
            return Karistir(pal[i % n], pal[(i + 1) % n], f);
        }

        static Color Hsv(double h, double v)
        {
            h = Mod(h) * 6;
            int i = (int)h;
            double f = h - i, q = 1 - f;
            double[] r = (i % 6) switch
            {
                0 => new[] { 1, f, 0 },
                1 => new[] { q, 1, 0 },
                2 => new[] { 0, 1, f },
                3 => new[] { 0, q, 1 },
                4 => new[] { f, 0, 1 },
                _ => new[] { 1, 0, q },
            };
            v = Math.Clamp(v, 0, 1);
            return Color.FromArgb((int)(r[0] * 255 * v), (int)(r[1] * 255 * v), (int)(r[2] * 255 * v));
        }
    }
}
