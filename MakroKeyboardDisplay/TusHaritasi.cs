using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Uygulama 3.2 (AD): bütün modların tuş haritasını tek bir resme çizer.
    /// Duvara asmak ya da birine göndermek için.
    /// </summary>
    static class TusHaritasi
    {
        const int Kenar = 36, KartEn = 300, KartBoy = 200, Ara = 18, Sutun = 3;

        public static Bitmap Ciz(List<ModTanimi> modlar, Func<string, Bitmap> simgeBul)
        {
            modlar ??= new List<ModTanimi>();
            int satir = Math.Max(1, (modlar.Count + Sutun - 1) / Sutun);
            int w = Kenar * 2 + Sutun * KartEn + (Sutun - 1) * Ara;
            int h = Kenar * 2 + 70 + satir * (KartBoy + Ara);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.FromArgb(11, 16, 24));

            using var baslikF = new Font("Segoe UI", 22, FontStyle.Bold);
            using var modF = new Font("Segoe UI", 13, FontStyle.Bold);
            using var tusF = new Font("Segoe UI", 11, FontStyle.Bold);
            using var altF = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            using var beyaz = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var soluk = new SolidBrush(Color.FromArgb(138, 148, 166));

            g.DrawString("Makro Klavye · tuş haritası", baslikF, beyaz, Kenar, Kenar);
            g.DrawString(DateTime.Now.ToString("d MMMM yyyy"), altF, soluk, Kenar + 2, Kenar + 42);

            for (int i = 0; i < modlar.Count; i++)
            {
                var m = modlar[i];
                float x = Kenar + (i % Sutun) * (KartEn + Ara);
                float y = Kenar + 70 + (i / Sutun) * (KartBoy + Ara);
                var kart = new RectangleF(x, y, KartEn, KartBoy);
                using (var f = new SolidBrush(m.Zemin ?? Color.FromArgb(22, 30, 43)))
                using (var yol = Yuvarlak(kart, 14))
                    g.FillPath(f, yol);
                using (var kalem = new Pen(m.Renk, 2f))
                using (var yol = Yuvarlak(kart, 14))
                    g.DrawPath(kalem, yol);
                using (var f = new SolidBrush(m.Renk))
                    g.FillEllipse(f, x + 14, y + 16, 10, 10);
                g.DrawString(m.Ad, modF, beyaz, x + 32, y + 10);
                if (!string.IsNullOrEmpty(m.Grup))
                    using (var f = new SolidBrush(m.Renk))
                        g.DrawString(m.Grup, altF, f, x + KartEn - 70, y + 14);

                float tw = (KartEn - 28 - 2 * 8) / 3f, th = 56;
                for (int t = 0; t < 6; t++)
                {
                    var tus = m.Tuslar[t];
                    var r = new RectangleF(x + 14 + (t % 3) * (tw + 8), y + 42 + (t / 3) * (th + 8), tw, th);
                    using (var f = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    using (var yol = Yuvarlak(r, 8))
                        g.FillPath(f, yol);
                    var resim = simgeBul?.Invoke(tus.Simge);
                    float yaziY = r.Y + 8;
                    if (resim != null)
                    {
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.DrawImage(resim, r.X + r.Width / 2 - 10, r.Y + 6, 20, 20);
                        yaziY = r.Y + 28;
                    }
                    string ad = string.IsNullOrEmpty(tus.Ad) ? KomutKatalogu.KisaAd(tus.Kod) : tus.Ad;
                    using var bicim = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                    g.DrawString(ad, tusF, beyaz, new RectangleF(r.X + 2, yaziY, r.Width - 4, 18), bicim);
                    if (!string.IsNullOrEmpty(tus.UzunKod) && tus.UzunKod != "NONE")
                        g.DrawString("uzun: " + KomutKatalogu.KisaAd(tus.UzunKod), altF, soluk,
                                     new RectangleF(r.X + 2, r.Bottom - 18, r.Width - 4, 16), bicim);
                }
            }
            return bmp;
        }

        static GraphicsPath Yuvarlak(RectangleF r, float yaricap)
        {
            float d = yaricap * 2;
            var yol = new GraphicsPath();
            yol.AddArc(r.X, r.Y, d, d, 180, 90);
            yol.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            yol.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            yol.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            yol.CloseFigure();
            return yol;
        }
    }
}
