using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// uygulama 2.5: Ayarlar > Geçmiş (modların önceki hâlleri), Ayarlar > İstatistik (kullanım) ve Tuş testi ekranı.
    /// </summary>
    public sealed partial class KlavyePaneli
    {
        public event Action<List<ModTanimi>> GecmisYukleIstendi;     // bu hâle dön
        public event Action<bool> TusTestiIstendi;                   // aç / kapa (kart TEST 1/0)

        // ------------------------------------------------------------------ geçmiş
        string gecmisOnay;                 // "geri dön" onayı beklenen kaydın dosyası

        void GecmisCiz(Graphics g, float y, float w, float h)
        {
            Grup(g, "MODLARIN ÖNCEKİ HÂLLERİ", y);
            y += 18;
            Yaz(g, "Modları klavyeye her kaydedişte önceki hâl saklanır (son " + AyarGecmisi.EnFazla + ").",
                7.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, y, w - 2 * P, 18));
            y += 24;
            var liste = AyarGecmisi.Liste(8);
            if (liste.Count == 0)
            {
                Yaz(g, "Henüz kayıt yok. Bir modu düzenleyip kaydedince burada görünür.", 8.5f, FontStyle.Regular, T.Soluk,
                    new RectangleF(P, y, w - 2 * P, 36), StringAlignment.Near, true, StringAlignment.Near);
                return;
            }
            var tr = new System.Globalization.CultureInfo("tr-TR");
            foreach (var k in liste)
            {
                if (y + 40 > h - 44) break;
                var r = new RectangleF(P, y, w - 2 * P, 38);
                Doldur(g, T.Kart, r, 8);
                string zaman = k.Zaman.Date == DateTime.Today ? "Bugün " + k.Zaman.ToString("HH:mm")
                    : k.Zaman.Date == DateTime.Today.AddDays(-1) ? "Dün " + k.Zaman.ToString("HH:mm")
                    : k.Zaman.ToString("d MMMM HH:mm", tr);
                Yaz(g, zaman, 8.5f, FontStyle.Bold, T.Yazi, new RectangleF(r.X + 10, r.Y + 2, r.Width - 110, 18));
                Yaz(g, $"{k.Modlar.Count} mod: " + string.Join(", ", k.Modlar.Select(m => m.Ad)), 7.5f, FontStyle.Regular, T.Soluk,
                    new RectangleF(r.X + 10, r.Y + 19, r.Width - 110, 16));
                var kayit = k;
                bool onay = gecmisOnay == k.Dosya;
                var d = new RectangleF(r.Right - 92, r.Y + 6, 84, 26);
                KucukDugme(g, d, onay ? "Emin misin?" : "Bu hâle dön", "gec" + k.Dosya.GetHashCode(), () =>
                {
                    if (gecmisOnay != kayit.Dosya) { gecmisOnay = kayit.Dosya; return; }
                    gecmisOnay = null;
                    GecmisYukleIstendi?.Invoke(kayit.Modlar);
                }, onay ? 2 : 0, durum.Bagli && durum.ModDuzenDestek);
                y += 44;
            }
        }

        // ------------------------------------------------------------------ istatistik
        bool istatistikSifirOnay;

        void IstatistikCiz(Graphics g, float y, float w, float h)
        {
            Yaz(g, $"Bugün {Istatistik.Bugun} basış", 11f, FontStyle.Bold, T.Yazi, new RectangleF(P, y, w - 2 * P, 22));
            var v = Istatistik.Veri;
            Yaz(g, $"Toplam {Istatistik.Toplam} · tekerlek sol {v.SolTeker}, sağ {v.SagTeker} adım · {v.Baslangic:d MMM}'den beri",
                7.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, y + 22, w - 2 * P, 16));
            y += 44;

            // son 14 gün
            var gunler = Istatistik.SonGunler(14);
            int enCok = Math.Max(1, gunler.Max());
            float bw = (w - 2 * P) / 14f;
            var vurgu = T.Vurgu;
            for (int i = 0; i < 14; i++)
            {
                float bh = Math.Max(2, 44f * gunler[i] / enCok);
                using var f = new SolidBrush(i == 13 ? vurgu : Color.FromArgb(120, vurgu));
                g.FillRectangle(f, P + i * bw + 2, y + 44 - bh, bw - 4, bh);
            }
            Yaz(g, "14 gün önce", 7f, FontStyle.Regular, T.Soluk, new RectangleF(P, y + 46, 80, 14));
            Yaz(g, "bugün", 7f, FontStyle.Regular, T.Soluk, new RectangleF(w - P - 60, y + 46, 60, 14), StringAlignment.Far);
            y += 66;

            Grup(g, "EN ÇOK BASILANLAR", y);
            y += 18;
            var en = Istatistik.EnCok(6);
            if (en.Count == 0)
                Yaz(g, "Henüz basış yok.", 8f, FontStyle.Regular, T.Soluk, new RectangleF(P, y, w - 2 * P, 18));
            int ust = en.Count > 0 ? en[0].sayi : 1;
            foreach (var (mod, tus, uzun, sayi) in en)
            {
                var r = new RectangleF(P, y, w - 2 * P, 19);
                float oran = sayi / (float)ust;
                using (var f = new SolidBrush(Color.FromArgb(50, vurgu)))
                    g.FillRectangle(f, r.X, r.Y + 1, (r.Width - 50) * oran, r.Height - 2);
                Yaz(g, $"{tus}{(uzun ? " (uzun)" : "")}", 8.5f, FontStyle.Bold, T.Yazi, new RectangleF(r.X + 6, r.Y, r.Width - 140, r.Height));
                Yaz(g, mod, 7.5f, FontStyle.Regular, T.Soluk, new RectangleF(r.Right - 140, r.Y, 86, r.Height), StringAlignment.Far);
                Yaz(g, sayi.ToString(), 8.5f, FontStyle.Bold, T.Yazi, new RectangleF(r.Right - 46, r.Y, 46, r.Height), StringAlignment.Far);
                y += 21;
            }
            y += 8;

            Grup(g, "MODLARDA GEÇEN SÜRE", y);
            y += 18;
            var modlar = v.ModSure.OrderByDescending(kv => kv.Value).Take(4).ToList();
            double enUzun = modlar.Count > 0 ? modlar[0].Value : 1;
            foreach (var kv in modlar)
            {
                var r = new RectangleF(P, y, w - 2 * P, 19);
                using (var f = new SolidBrush(Color.FromArgb(50, vurgu)))
                    g.FillRectangle(f, r.X, r.Y + 1, (float)((r.Width - 70) * kv.Value / enUzun), r.Height - 2);
                Yaz(g, kv.Key, 8.5f, FontStyle.Bold, T.Yazi, new RectangleF(r.X + 6, r.Y, r.Width - 80, r.Height));
                var t = TimeSpan.FromSeconds(kv.Value);
                Yaz(g, t.TotalHours >= 1 ? $"{(int)t.TotalHours} sa {t.Minutes} dk" : $"{t.Minutes} dk", 8f, FontStyle.Regular, T.Soluk,
                    new RectangleF(r.Right - 70, r.Y, 70, r.Height), StringAlignment.Far);
                y += 21;
            }
            if (modlar.Count == 0)
                Yaz(g, "Klavye bağlıyken ölçülür.", 8f, FontStyle.Regular, T.Soluk, new RectangleF(P, y, w - 2 * P, 18));

            var sf = new RectangleF(w - P - 140, h - 66, 140, 18);
            Yaz(g, istatistikSifirOnay ? "Sıfırlansın mı? Tıkla" : "İstatistiği sıfırla", 8f, FontStyle.Regular,
                istatistikSifirOnay ? R(248, 113, 113) : (uzerindeki == "istsifir" ? T.Yazi : T.Soluk), sf, StringAlignment.Far);
            Ekle(sf, () =>
            {
                if (!istatistikSifirOnay) { istatistikSifirOnay = true; return; }
                istatistikSifirOnay = false;
                Istatistik.Sifirla();
            }, "istsifir");
        }

        // ------------------------------------------------------------------ tuş testi
        readonly bool[] testBasili = new bool[8];          // 0-5 tuşlar, 6 sol tekerlek, 7 sağ tekerlek düğmesi
        readonly int[] testSayac = new int[8];
        readonly int[] testTeker = new int[2];            // sol, sağ: toplam adım
        readonly DateTime[] testTekerZaman = new DateTime[2];
        readonly List<string> testOlaylar = new();
        Gorunum testOncesi;

        /// <summary>TepsiUygulamasi çağırır: kart "BAS n 1/0" ya da "ENC L/R ±1" gönderdi.</summary>
        public void TestOlayi(string tur, int no, int deger)
        {
            if (gorunum != Gorunum.TusTesti) return;
            string ad;
            if (tur == "BAS" && no >= 0 && no < 8)
            {
                testBasili[no] = deger == 1;
                if (deger == 1) testSayac[no]++;
                ad = no < 6 ? $"{no + 1}. tuş" : no == 6 ? "Sol tekerlek düğmesi" : "Sağ tekerlek düğmesi";
                ad += deger == 1 ? " basıldı" : " bırakıldı";
            }
            else if (tur == "ENC")
            {
                int i = no == 0 ? 0 : 1;
                testTeker[i] += deger;
                testTekerZaman[i] = DateTime.UtcNow;
                ad = (i == 0 ? "Sol" : "Sağ") + " tekerlek " + (deger > 0 ? "sağa" : "sola");
            }
            else return;
            testOlaylar.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + ad);
            if (testOlaylar.Count > 6) testOlaylar.RemoveAt(testOlaylar.Count - 1);
            Invalidate();
        }

        void TusTestineGir()
        {
            if (gorunum == Gorunum.TusTesti) return;
            testOncesi = gorunum;
            Array.Clear(testBasili);
            Array.Clear(testSayac);
            Array.Clear(testTeker);
            testOlaylar.Clear();
            gorunum = Gorunum.TusTesti;
            TusTestiIstendi?.Invoke(true);
            Boyutla();
        }

        void TusTestindenCik()
        {
            if (gorunum != Gorunum.TusTesti) return;
            TusTestiIstendi?.Invoke(false);
            gorunum = testOncesi;
            Boyutla();
        }

        void TusTestiCiz(Graphics g, float w, float h)
        {
            IkonDugme(g, "geri", GlifGeri, new RectangleF(P - 4, P - 2, 26, 26), 11f, TusTestindenCik);
            Yaz(g, "Tuş testi", 12f, FontStyle.Bold, T.Yazi, new RectangleF(P + 28, P - 1, 180, 24));
            float y = 48;
            if (!durum.Bagli || !KartEnAzPanel(1, 20))
            {
                Yaz(g, !durum.Bagli ? "Klavye bağlı değil." : "Tuş testi için kart yazılımı 1.20 gerekli.", 9f, FontStyle.Regular, T.Soluk,
                    new RectangleF(P, y, w - 2 * P, 20));
                return;
            }
            Yaz(g, "Bu ekran açıkken tuşlar komut çalıştırmaz. Bastığın tuş, çevirdiğin tekerlek burada yanar.", 8f, FontStyle.Regular, T.Soluk,
                new RectangleF(P, y, w - 2 * P, 32), StringAlignment.Near, true, StringAlignment.Near);
            y += 40;

            // klavye: LED haritasındaki konumlarla aynı yerleşim (tuşların altındaki LED'ler)
            var alan = new RectangleF(P + 10, y, w - 2 * P - 20, 210);
            const float x0 = -48, x1 = 48, y0 = -65, y1 = 36, pad = 6;
            float s = Math.Min((alan.Width - 2 * pad) / (x1 - x0), (alan.Height - 2 * pad) / (y1 - y0));
            float ox = alan.X + (alan.Width - (x1 - x0) * s) / 2, oy = alan.Y + (alan.Height - (y1 - y0) * s) / 2;
            PointF N(float px, float py) => new(ox + (px - x0) * s, oy + (y1 - py) * s);
            var govde = new RectangleF(ox, oy, (x1 - x0) * s, (y1 - y0) * s);
            Doldur(g, T.Kart, govde, 10);
            using (var kalem = new Pen(T.Kenar, 1.5f))
            using (var yol = Yuvarlak(govde, 10))
                g.DrawPath(kalem, yol);
            var renk = durum.AktifMod >= 0 && durum.AktifMod < durum.ModRenkleri.Length ? durum.ModRenkleri[durum.AktifMod] : T.Vurgu;

            // ekran (üstte ortada)
            var ekranMerkez = N(0, 22);
            var ekran = new RectangleF(ekranMerkez.X - 15 * s, ekranMerkez.Y - 11 * s, 30 * s, 22 * s);
            Doldur(g, R(11, 16, 24), ekran, 3);

            // tekerlekler (ekranın iki yanında)
            for (int i = 0; i < 2; i++)
            {
                var m = N(i == 0 ? -31 : 31, 22);
                float yr = 9 * s;
                bool bas = testBasili[6 + i];
                bool doner = (DateTime.UtcNow - testTekerZaman[i]).TotalMilliseconds < 300;
                using (var f = new SolidBrush(bas ? renk : doner ? Color.FromArgb(140, renk) : T.Sek))
                    g.FillEllipse(f, m.X - yr, m.Y - yr, 2 * yr, 2 * yr);
                using (var kalem = new Pen(renk, 2f))
                    g.DrawEllipse(kalem, m.X - yr, m.Y - yr, 2 * yr, 2 * yr);
                // dönüş çizgisi: toplam adıma göre döner
                double aci = testTeker[i] * Math.PI / 10;
                using (var kalem = new Pen(bas ? Color.White : T.Yazi, 2f))
                    g.DrawLine(kalem, m.X, m.Y, m.X + (float)Math.Sin(aci) * yr * 0.8f, m.Y - (float)Math.Cos(aci) * yr * 0.8f);
                Yaz(g, (i == 0 ? "Sol " : "Sağ ") + testTeker[i].ToString("+0;-0;0"), 7.5f, FontStyle.Regular, T.Soluk,
                    new RectangleF(m.X - 30, m.Y + yr + 2, 60, 14), StringAlignment.Center);
            }

            // tuşlar
            for (int t = 0; t < 6; t++)
            {
                var k = LedEfektleri.Konum[LedEfektleri.TusLed[t]];
                var m = N(k.X, k.Y);
                float yr = 10 * s;
                var r = new RectangleF(m.X - yr, m.Y - yr, 2 * yr, 2 * yr);
                Doldur(g, testBasili[t] ? renk : T.Sek, r, 5);
                using (var kalem = new Pen(testSayac[t] > 0 ? renk : T.Kenar, testSayac[t] > 0 ? 2f : 1f))
                using (var yol = Yuvarlak(r, 5))
                    g.DrawPath(kalem, yol);
                Yaz(g, (t + 1).ToString(), 9f, FontStyle.Bold, testBasili[t] ? Color.White : T.Yazi, r, StringAlignment.Center);
                if (testSayac[t] > 0)
                    Yaz(g, "×" + testSayac[t], 7f, FontStyle.Regular, T.Soluk, new RectangleF(r.X, r.Bottom, r.Width, 12), StringAlignment.Center);
            }
            y = alan.Bottom + 8;

            Grup(g, "SON OLAYLAR", y);
            y += 18;
            if (testOlaylar.Count == 0)
                Yaz(g, "Bir tuşa bas ya da tekerleği çevir.", 8.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, y, w - 2 * P, 18));
            foreach (var o in testOlaylar)
            {
                if (y + 18 > h - 12) break;
                Yaz(g, o, 8.5f, FontStyle.Regular, T.Yazi, new RectangleF(P, y, w - 2 * P, 18));
                y += 18;
            }
        }
    }
}
