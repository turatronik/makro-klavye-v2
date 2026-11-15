using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace MakroKeyboardDisplay
{
    // KlavyePaneli parçası (uygulama 2.9 / kart 2.7): tuş simgesi seçici sayfası.
    // Simgeler klavyedeki 24x24 çizimlerin aynısıdır; üstte tuşun klavye ekranındaki önizlemesi durur.
    public sealed partial class KlavyePaneli
    {
        const int SimgeSutun = 7, SimgeKare = 42, SimgeBosluk = 3;
        const string OzelGrup = "Benim";        // 3.1: kullanıcının çizdiği simgeler

        float simgeIzgaraY;
        RectangleF SimgeHucresi(int i) =>
            new(P + (i % SimgeSutun) * (SimgeKare + SimgeBosluk),
                simgeIzgaraY + (i / SimgeSutun) * (SimgeKare + SimgeBosluk), SimgeKare, SimgeKare);

        readonly Dictionary<(string, int), Bitmap> simgeResimleri = new();
        string simgeGrubu;

        readonly Dictionary<string, Bitmap> ozelResimleri = new();

        /// <summary>3.1: kullanıcının çizdiği simgelerin resim önbelleğini boşaltır (çizim değişince).</summary>
        void OzelResimleriUnut()
        {
            foreach (var b in ozelResimleri.Values) b.Dispose();
            ozelResimleri.Clear();
        }

        /// <summary>Simgenin istenen renkte küçük resmi (klavyedeki bit deseninin aynısı). "*ad": kullanıcının renkli simgesi.</summary>
        Bitmap SimgeResmi(string ad, Color renk)
        {
            if (!string.IsNullOrEmpty(ad) && ad[0] == '*')
            {
                string sade = ad.Substring(1);
                if (ozelResimleri.TryGetValue(sade, out var o)) return o;
                var kayit = OzelSimge.Hepsi.FirstOrDefault(x => x.Ad == sade);
                if (kayit == null) return BosResim();
                o = kayit.Resim();
                ozelResimleri[sade] = o;
                return o;
            }
            var anahtar = (ad, renk.ToArgb());
            if (simgeResimleri.TryGetValue(anahtar, out var b)) return b;
            b = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            var saydam = Color.FromArgb(0, renk);       // boş pikseller aynı renkte saydam: büyütünce koyu kenar çıkmaz
            if (SimgeVerisi.Bit.TryGetValue(ad, out var satirlar))
                for (int y = 0; y < 24; y++)
                    for (int x = 0; x < 24; x++)
                        b.SetPixel(x, y, (satirlar[y] >> (23 - x) & 1) != 0 ? renk : saydam);
            simgeResimleri[anahtar] = b;
            return b;
        }

        Bitmap bosResim;
        Bitmap BosResim() => bosResim ??= new Bitmap(24, 24, PixelFormat.Format32bppArgb);

        void SimgeSecAc()
        {
            var tus = taslak[duzenNo].Tuslar[duzenTus];
            simgeGrubu = KomutKatalogu.TusSimgeleri.FirstOrDefault(x => x.ad == tus.Simge).grup
                         ?? KomutKatalogu.TusSimgeleri[0].grup;
            gorunum = Gorunum.SimgeSec;
            Boyutla();
        }

        void SimgeSecdenCik()
        {
            gorunum = Gorunum.ModDuzen;
            Boyutla();
        }

        /// <summary>Tuşun klavye ekranındaki karosunun önizlemesi (ekran.cpp'deki düzenin aynısı).</summary>
        void SimgeOnizleme(Graphics g, RectangleF r, ModTanimi m, TusTanimi tus)
        {
            float s = r.Width / 72f;                       // klavyede karo 72x76
            var arka = R(11, 16, 24);
            var kartRenk = R(17, 24, 39);
            using (var f = new SolidBrush(arka)) g.FillRectangle(f, r);
            using (var f = new SolidBrush(m.Renk)) g.FillRectangle(f, r.X + 2 * s, r.Y + 2 * s, 68 * s, 72 * s);
            using (var f = new SolidBrush(kartRenk)) g.FillRectangle(f, r.X + 4 * s, r.Y + 4 * s, 64 * s, 68 * s);
            Yaz(g, (duzenTus + 1).ToString(), 7f, FontStyle.Regular, m.Renk, new RectangleF(r.X + 7 * s, r.Y + 5 * s, 14 * s, 12 * s));
            string ad = string.IsNullOrEmpty(tus.Ad) ? KomutKatalogu.KisaAd(tus.Kod) : tus.Ad;
            bool simgeVar = !string.IsNullOrEmpty(tus.Simge) && SimgeVerisi.Bit.ContainsKey(tus.Simge);
            bool uzunVar = !string.IsNullOrEmpty(tus.UzunKod) && tus.UzunKod != "NONE";
            var beyaz = R(226, 232, 240);
            if (simgeVar)
            {
                float boy = 24 * s;
                g.DrawImage(SimgeResmi(tus.Simge, beyaz), r.X + 24 * s, r.Y + (uzunVar ? 8 : 12) * s, boy, boy);
                Yaz(g, ad, 8f, FontStyle.Bold, beyaz, new RectangleF(r.X + 4 * s, r.Y + (uzunVar ? 40 : 46) * s, 64 * s, 13 * s), StringAlignment.Center);
            }
            else
                Yaz(g, ad, 8.5f, FontStyle.Bold, beyaz, new RectangleF(r.X + 4 * s, r.Y + (uzunVar ? 28 : 36) * s, 64 * s, 16 * s), StringAlignment.Center);
            if (uzunVar)
                Yaz(g, KomutKatalogu.KisaAd(tus.UzunKod), 7f, FontStyle.Regular, m.Renk,
                    new RectangleF(r.X + 4 * s, r.Y + 58 * s, 64 * s, 12 * s), StringAlignment.Center);
        }

        void SimgeSecCiz(Graphics g, float w, float h)
        {
            if (taslak == null || duzenNo >= taslak.Count)
            {
                DuzendenCik(true);
                return;
            }
            var m = taslak[duzenNo];
            var tus = m.Tuslar[duzenTus];
            IkonDugme(g, "geri", GlifGeri, new RectangleF(P - 4, P - 2, 26, 26), 11f, SimgeSecdenCik);
            Yaz(g, $"{duzenTus + 1}. tuş · simge", 12f, FontStyle.Bold, T.Yazi, new RectangleF(P + 28, P - 1, w - 2 * P - 28, 24));

            // klavye önizlemesi
            var on = new RectangleF(P, 46, 90, 95);
            SimgeOnizleme(g, on, m, tus);
            Yaz(g, "KLAVYE EKRANINDA", 7.5f, FontStyle.Bold, T.Soluk, new RectangleF(on.Right + 14, 48, w - P - on.Right - 14, 16));
            Yaz(g, "Simge tuş adının üstünde görünür. Uzun adlar simgeyle birlikte küçülür, " +
                   "o yüzden simge kullanacağın tuşlarda kısa ad daha iyi durur.",
                8f, FontStyle.Regular, T.Soluk, new RectangleF(on.Right + 14, 66, w - P - on.Right - 14, 54),
                StringAlignment.Near, true, StringAlignment.Near);
            KucukDugme(g, new RectangleF(on.Right + 14, 122, 104, 28), "Simge yok", "simgeyok",
                       () => { tus.Simge = ""; Invalidate(); }, string.IsNullOrEmpty(tus.Simge) ? 1 : 0);

            // kategoriler ("Benim" = 3.1'de çizdiklerin)
            float x = P, y = 158;
            var yaziTipi = YaziTipi("Segoe UI", 8f, FontStyle.Bold);
            var gruplar = new List<string> { OzelGrup };
            gruplar.AddRange(KomutKatalogu.TusSimgeleri.Select(t => t.grup).Distinct());
            foreach (var grup in gruplar)
            {
                float kw = g.MeasureString(grup, yaziTipi).Width + 16;
                if (x + kw > w - P + 0.5f) { x = P; y += 27; }
                var r = new RectangleF(x, y, kw, 22);
                bool secili = grup == simgeGrubu;
                Doldur(g, secili ? T.SekAcik : (uzerindeki == "sg" + grup ? Karistir(T.Sek, T.Uzerinde) : T.Sek), r, 11);
                Yaz(g, grup, 8f, FontStyle.Bold, secili ? T.Yazi : T.Soluk, r, StringAlignment.Center);
                var secilen = grup;
                Ekle(r, () => { simgeGrubu = secilen; Invalidate(); }, "sg" + grup);
                x += kw + 5;
            }

            // simge ızgarası
            float gy = y + 32;
            simgeIzgaraY = gy;
            int i = 0;
            var adlar = simgeGrubu == OzelGrup
                ? OzelSimge.Hepsi.Select(o => "*" + o.Ad).ToList()
                : KomutKatalogu.TusSimgeleri.Where(t => t.grup == simgeGrubu).Select(t => t.ad).ToList();
            foreach (var ad in adlar)
            {
                var r = SimgeHucresi(i);
                bool seciliMi = ad == tus.Simge;
                Doldur(g, seciliMi ? T.SekAcik : (uzerindeki == "sim" + ad ? Karistir(T.Kart, T.Uzerinde) : T.Kart), r, 10);
                if (seciliMi)
                    using (var kalem = new Pen(m.Renk, 2f))
                    using (var yol = Yuvarlak(RectangleF.Inflate(r, -1, -1), 10))
                        g.DrawPath(kalem, yol);
                g.DrawImage(SimgeResmi(ad, T.Yazi), r.X + (SimgeKare - 26) / 2f, r.Y + (SimgeKare - 26) / 2f, 26, 26);
                var secilen = ad;
                Ekle(r, () => { tus.Simge = tus.Simge == secilen ? "" : secilen; Invalidate(); }, "sim" + ad);
                i++;
            }
            if (simgeGrubu == OzelGrup)                     // 3.1: yeni simge çiz
            {
                var r = SimgeHucresi(i);
                Doldur(g, uzerindeki == "simyeni" ? Karistir(T.Kart, T.Uzerinde) : T.Kart, r, 10);
                using (var kalem = new Pen(T.Soluk, 2f))
                {
                    g.DrawLine(kalem, r.X + r.Width / 2, r.Y + 12, r.X + r.Width / 2, r.Bottom - 12);
                    g.DrawLine(kalem, r.X + 12, r.Y + r.Height / 2, r.Right - 12, r.Y + r.Height / 2);
                }
                Ekle(r, () => SimgeCizAc(-1), "simyeni");
                if (OzelSimge.Hepsi.Count == 0)
                    Yaz(g, "Kendi simgeni çizmek için artıya bas", 8f, FontStyle.Regular, T.Soluk,
                        new RectangleF(P, gy + SimgeKare + 10, w - 2 * P, 18));
            }

            // alt satır
            using (var kalem = new Pen(T.Kenar, 1f))
                g.DrawLine(kalem, P, h - 48, w - P, h - 48);
            string ustundeki = uzerindeki != null && uzerindeki.StartsWith("sim") ? uzerindeki.Substring(3) : null;
            string alt = ustundeki != null ? KomutKatalogu.SimgeYazisi(ustundeki)
                       : string.IsNullOrEmpty(tus.Simge) ? "Simge seçili değil"
                       : "Seçili: " + KomutKatalogu.SimgeYazisi(tus.Simge);
            Yaz(g, alt, 8.5f, FontStyle.Bold, T.Soluk, new RectangleF(P, h - 38, w - 2 * P - 180, 28));
            int duzenSira = !string.IsNullOrEmpty(tus.Simge) && tus.Simge[0] == '*'
                            ? OzelSimge.Hepsi.FindIndex(o => o.Ad == tus.Simge.Substring(1)) : -1;
            bool uygulamaTusu = KomutKatalogu.UygulamaMi(tus.Kod);
            if (duzenSira >= 0)
                KucukDugme(g, new RectangleF(w - P - 174, h - 38, 70, 28), "Düzenle", "simduzen", () => SimgeCizAc(duzenSira), 0);
            else if (uygulamaTusu)                        // 3.1 (BG): programın kendi simgesini al
                KucukDugme(g, new RectangleF(w - P - 174, h - 38, 70, 28), "Programdan", "simprog", () => ProgramdanSimge(tus), 0);
            KucukDugme(g, new RectangleF(w - P - 100, h - 38, 100, 28), "Bitti", "simgebitti", SimgeSecdenCik, 1);
        }
    
        // ================================================================== 3.1: simge çizme
        OzelSimge cizilen;                 // düzenlenen kopya
        int cizilenSira = -1;              // OzelSimge.Hepsi içindeki yeri, -1: yeni
        int paletSecili = 1;
        bool silgi;
        RectangleF cizimAlani;
        bool cizimSuruyor;

        /// <summary>Çizim değişince karta gönderilmesi için.</summary>
        public event Action OzelSimgelerDegisti;

        void SimgeCizAc(int sira)
        {
            cizilenSira = sira;
            cizilen = sira >= 0 && sira < OzelSimge.Hepsi.Count ? OzelSimge.Hepsi[sira].Kopya() : new OzelSimge();
            paletSecili = 1;
            silgi = false;
            gorunum = Gorunum.SimgeCiz;
            Boyutla();
        }

        void SimgeCizKapat(bool kaydet)
        {
            if (kaydet && cizilen != null && !cizilen.Bos)
            {
                if (cizilenSira >= 0 && cizilenSira < OzelSimge.Hepsi.Count)
                {
                    cizilen.Ad = OzelSimge.Hepsi[cizilenSira].Ad;
                    OzelSimge.Hepsi[cizilenSira] = cizilen;
                }
                else
                {
                    cizilen.Ad = OzelSimge.BenzersizAd("simge");
                    OzelSimge.Hepsi.Add(cizilen);
                    var t = taslak != null && duzenNo < taslak.Count ? taslak[duzenNo].Tuslar[duzenTus] : null;
                    if (t != null && string.IsNullOrEmpty(t.Simge)) t.Simge = "*" + cizilen.Ad;   // yeni çizim hemen tuşa gelsin
                }
                OzelSimge.Kaydet();
                OzelResimleriUnut();
                OzelSimgelerDegisti?.Invoke();
            }
            cizilen = null;
            simgeGrubu = OzelGrup;
            gorunum = Gorunum.SimgeSec;
            Boyutla();
        }

        void CiziliSimgeyiSil()
        {
            if (cizilenSira >= 0 && cizilenSira < OzelSimge.Hepsi.Count)
            {
                string ad = "*" + OzelSimge.Hepsi[cizilenSira].Ad;
                OzelSimge.Hepsi.RemoveAt(cizilenSira);
                OzelSimge.Kaydet();
                if (taslak != null)
                    foreach (var md in taslak)
                        foreach (var t in md.Tuslar)
                            if (t.Simge == ad) t.Simge = "";
                OzelResimleriUnut();
                OzelSimgelerDegisti?.Invoke();
            }
            cizilen = null;
            gorunum = Gorunum.SimgeSec;
            Boyutla();
        }

        /// <summary>3.1 (BG): tuşa atanmış programın simgesini alıp özel simge yapar.</summary>
        void ProgramdanSimge(TusTanimi tus)
        {
            string yol = KomutKatalogu.UygulamaHedefi(tus.Kod);
            var yeni = OzelSimge.UygulamadanAl(yol, System.IO.Path.GetFileNameWithoutExtension(yol));
            if (yeni == null)
            {
                MessageBox.Show(this, "Bu programın simgesi alınamadı.", "Makro Klavye",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            OzelSimge.Hepsi.Add(yeni);
            OzelSimge.Kaydet();
            OzelResimleriUnut();
            tus.Simge = "*" + yeni.Ad;
            simgeGrubu = OzelGrup;
            OzelSimgelerDegisti?.Invoke();
            Invalidate();
        }

        void PaletRengiDegistir()
        {
            if (cizilen == null || paletSecili <= 0) return;
            renkPenceresiAcik = true;
            try
            {
                using var d = new ColorDialog { Color = cizilen.Palet[paletSecili], FullOpen = true, AnyColor = true };
                if (d.ShowDialog(this) == DialogResult.OK) cizilen.Palet[paletSecili] = d.Color;
            }
            finally { renkPenceresiAcik = false; }
            Invalidate();
        }

        /// <summary>Fare noktasındaki hücreyi boyar (ya da siler).</summary>
        void CizimBoya(Point p)
        {
            if (cizilen == null || cizimAlani.Width <= 0) return;
            float hucre = cizimAlani.Width / OzelSimge.Boy;
            int x = (int)((p.X / O - cizimAlani.X) / hucre), y = (int)((p.Y / O - cizimAlani.Y) / hucre);
            if (x < 0 || y < 0 || x >= OzelSimge.Boy || y >= OzelSimge.Boy) return;
            byte deger = silgi ? (byte)0 : (byte)paletSecili;
            if (cizilen.Px[y * OzelSimge.Boy + x] == deger) return;
            cizilen.Px[y * OzelSimge.Boy + x] = deger;
            Invalidate();
        }

        void SimgeCizCiz(Graphics g, float w, float h)
        {
            if (cizilen == null) { SimgeCizKapat(false); return; }
            IkonDugme(g, "geri", GlifGeri, new RectangleF(P - 4, P - 2, 26, 26), 11f, () => SimgeCizKapat(false));
            Yaz(g, cizilenSira >= 0 ? "Simgeyi düzenle" : "Yeni simge", 12f, FontStyle.Bold, T.Yazi,
                new RectangleF(P + 28, P - 1, w - 2 * P - 80, 24));
            // sağ üstte gerçek boyutta önizleme (klavyede bu kadar görünür)
            using (var on = cizilen.Resim())
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                var kutu = new RectangleF(w - P - 30, P - 3, 28, 28);
                Doldur(g, Color.FromArgb(255, 17, 24, 39), kutu, 6);      // klavye ekranının zemini
                g.DrawImage(on, kutu.X + 2, kutu.Y + 2, 24, 24);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            }

            float hucre = 10f;
            float cw = OzelSimge.Boy * hucre;
            cizimAlani = new RectangleF((float)Math.Round((w - cw) / 2), 46, cw, cw);
            for (int y = 0; y < OzelSimge.Boy; y++)
                for (int x = 0; x < OzelSimge.Boy; x++)
                {
                    int d = cizilen.Px[y * OzelSimge.Boy + x] & 15;
                    // boş kareler orta gri dama: hem beyaz hem siyah boya üstünde belli olsun
                    Color c = d == 0 ? (((x / 2 + y / 2) & 1) == 0 ? Color.FromArgb(104, 110, 124) : Color.FromArgb(78, 84, 98))
                                     : cizilen.Palet[d];
                    using var f = new SolidBrush(Color.FromArgb(255, c));
                    g.FillRectangle(f, cizimAlani.X + x * hucre, cizimAlani.Y + y * hucre, hucre, hucre);
                }
            using (var kalem = new Pen(Color.FromArgb(70, 0, 0, 0), 1f))
                for (int i = 0; i <= OzelSimge.Boy; i += 4)
                {
                    g.DrawLine(kalem, cizimAlani.X + i * hucre, cizimAlani.Y, cizimAlani.X + i * hucre, cizimAlani.Bottom);
                    g.DrawLine(kalem, cizimAlani.X, cizimAlani.Y + i * hucre, cizimAlani.Right, cizimAlani.Y + i * hucre);
                }

            // palet (0: silgi)
            float py = cizimAlani.Bottom + 10, sw = 30, sh = 24, ara = 2;
            float px0 = (float)Math.Round((w - (8 * (sw + ara) - ara)) / 2);
            for (int i = 0; i < OzelSimge.PaletBoy; i++)
            {
                var r = new RectangleF(px0 + (i % 8) * (sw + ara), py + (i / 8) * (sh + ara), sw, sh);
                if (i == 0)
                {
                    Doldur(g, uzerindeki == "pal0" ? Karistir(T.Kart, T.Uzerinde) : T.Kart, r, 5);
                    Yaz(g, "silgi", 7.5f, FontStyle.Bold, T.Soluk, r, StringAlignment.Center);
                }
                else
                {
                    using var f = new SolidBrush(Color.FromArgb(255, cizilen.Palet[i]));
                    g.FillRectangle(f, r);
                }
                bool secili = i == 0 ? silgi : (!silgi && i == paletSecili);
                if (secili)
                    using (var kalem = new Pen(T.Yazi, 2f))
                        g.DrawRectangle(kalem, r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
                int no = i;
                Ekle(r, () => { if (no == 0) silgi = true; else { silgi = false; paletSecili = no; } Invalidate(); }, "pal" + i);
            }

            float by = py + 2 * (sh + ara) + 12;
            KucukDugme(g, new RectangleF(P, by, 104, 28), "Rengi değiştir", "renkdeg", PaletRengiDegistir, 0, !silgi);
            KucukDugme(g, new RectangleF(P + 110, by, 72, 28), "Temizle", "temizle",
                       () => { Array.Clear(cizilen.Px, 0, cizilen.Px.Length); Invalidate(); }, 0);
            if (cizilenSira >= 0)
                KucukDugme(g, new RectangleF(w - P - 54, by, 54, 28), "Sil", "cizsil", CiziliSimgeyiSil, 0);
            by += 36;
            float dw = (w - 2 * P - 8) / 2;
            KucukDugme(g, new RectangleF(P, by, dw, 32), "Vazgeç", "cizvazgec", () => SimgeCizKapat(false), 0);
            KucukDugme(g, new RectangleF(P + dw + 8, by, dw, 32), "Kaydet", "cizkaydet", () => SimgeCizKapat(true), 1, !cizilen.Bos);
            Yaz(g, "Renk seç, kareleri boya. Karta kaydedilince tuş karosunda görünür.",
                8f, FontStyle.Regular, T.Soluk, new RectangleF(P, by + 38, w - 2 * P, 32), StringAlignment.Near, true);
        }
    }
}
