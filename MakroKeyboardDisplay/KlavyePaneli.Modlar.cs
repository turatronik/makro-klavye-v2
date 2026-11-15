using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MakroKeyboardDisplay
{
    // KlavyePaneli'nin parçası: Modlar sekmesi, mod düzenleyici ve tuşa komut seçici (kart 1.11+).
    // Düzenleme bir taslak üzerinde yapılır; "Klavyeye kaydet" ile taslağın tamamı klavyeye gider.
    public sealed partial class KlavyePaneli
    {
        const string GlifGeri = "", GlifKalem = "", GlifEkle = "", GlifYukari = "",
                     GlifAsagi = "", GlifKlavye = "", GlifTamam = "";

        List<ModTanimi> taslak;            // düzenlenen modlar (kaydedilene kadar klavyeye gitmez)
        int duzenNo, duzenTus;
        bool silOnay, cikisOnay, sifirlaOnay;
        float modKaydirma, modIcerik, komutKaydirma, komutIcerik;
        RectangleF modListeAlani, komutAlani;
        string komutKategori = "Genel";
        bool kayitModu, kayitTamam;        // "Kendi kısayolun": tuş birleşimi bekleniyor
        string kayitKod = "";
        TextBox tbAd, tbTusAd, tbAra, tbGrup;
        TextBox tbMetin;                   // uygulama 2.3: "Metin yaz" komutunun metni (çok satırlı)
        bool metinEnter;
        string komutHedefi = "kisa";       // uygulama 2.4: komut seçici neyi atıyor: "kisa", "uzun" (basılı tutunca), "sag" / "sol" (tekerlek)
        RectangleF metinAlani = new(P, 176, G - 2 * P, 104);
        Font kutuYazisi;
        bool kutuYaziliyor;                // kutuların metni koddan değişirken TextChanged yok sayılsın

        public event Action<List<ModTanimi>> ModlarKaydet;
        public event Action ModlarSifirla;

        bool Kirli => taslak != null && !ModTanimi.Ayni(taslak, durum.ModTanimlari);
        bool Kaydediliyor => durum.ModKayit == "gidiyor";

        // ------------------------------------------------------------------ yazı kutuları
        void ModKutulariniKur()
        {
            // yüklü uygulamalar okununca (arka planda) liste yeniden çizilsin
            YukluUygulamalar.Okundu += () =>
            {
                try { if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(Invalidate)); } catch { }
            };
            tbAd = YeniKutu(ModTanimi.AdUzunlugu);
            tbAd.CharacterCasing = CharacterCasing.Upper;
            tbAd.TextChanged += (s, e) =>
            {
                if (kutuYaziliyor || taslak == null) return;
                taslak[duzenNo].Ad = tbAd.Text;
                Invalidate();
            };
            tbGrup = YeniKutu(12);                     // 3.2: mod grubu
            tbGrup.CharacterCasing = CharacterCasing.Upper;
            tbGrup.TextChanged += (s, e) =>
            {
                if (kutuYaziliyor || taslak == null) return;
                taslak[duzenNo].Grup = tbGrup.Text;
                Invalidate();
            };
            tbTusAd = YeniKutu(ModTanimi.TusAdiUzunlugu);
            tbTusAd.TextChanged += (s, e) =>
            {
                if (kutuYaziliyor || taslak == null) return;
                taslak[duzenNo].Tuslar[duzenTus].Ad = tbTusAd.Text;
                Invalidate();
            };
            tbMetin = new TextBox
            {
                BorderStyle = BorderStyle.None, Multiline = true, AcceptsReturn = true, WordWrap = true,
                MaxLength = KomutKatalogu.MetinUzunlugu, Visible = false,
            };
            tbMetin.GotFocus += (s, e) => Invalidate();
            tbMetin.LostFocus += (s, e) => Invalidate();
            tbMetin.TextChanged += (s, e) => { if (!kutuYaziliyor) Invalidate(); };
            Controls.Add(tbMetin);
            tbAra = YeniKutu(40);
            tbAra.PlaceholderText = "Ara: kopyala, ses, ekran…";
            tbAra.TextChanged += (s, e) =>
            {
                if (kutuYaziliyor) return;
                komutKaydirma = 0;
                Invalidate();
            };
        }

        TextBox YeniKutu(int uzunluk)
        {
            var t = new TextBox { BorderStyle = BorderStyle.None, MaxLength = uzunluk, Visible = false };
            t.KeyPress += (s, e) => { if (e.KeyChar == '\r' || e.KeyChar == '\t') e.Handled = true; };   // bip sesi olmasın
            t.GotFocus += (s, e) => Invalidate();
            t.LostFocus += (s, e) => Invalidate();
            Controls.Add(t);
            return t;
        }

        static RectangleF AdKutusu => new(P, 66, G - 2 * P, 28);
        static RectangleF GrupKutusu => new(P, 330, G - 2 * P, 28);
        static RectangleF TusAdKutusu => new(P, 354, G - 2 * P - 208, 32);
        static RectangleF AraKutusu => new(P, 48, G - 2 * P, 30);

        Color KutuRengi => T.Renkli ? R(62, 32, 76) : T.Kart;

        /// <summary>Görünüme göre yazı kutularını gösterir, yerleştirir ve renklendirir.</summary>
        void KutulariYerlestir()
        {
            if (tbAd == null) return;
            bool duzen = gorunum == Gorunum.ModDuzen && taslak != null;
            bool sec = gorunum == Gorunum.KomutSec && taslak != null;
            float boy = 13f * O;
            if (kutuYazisi == null || Math.Abs(kutuYazisi.Size - boy) > 0.1f)
            {
                kutuYazisi?.Dispose();
                kutuYazisi = new Font("Segoe UI", boy, GraphicsUnit.Pixel);
            }
            kutuYaziliyor = true;
            try
            {
                Yerlestir(tbAd, AdKutusu, duzen);
                Yerlestir(tbGrup, GrupKutusu, gorunum == Gorunum.ModGorunum && taslak != null);
                Yerlestir(tbTusAd, TusAdKutusu, duzen);
                bool metinKat = sec && MetinKutusuKategorisi(komutKategori);
                Yerlestir(tbAra, AraKutusu, sec && !metinKat);
                if (metinKat)
                {
                    tbMetin.MaxLength = komutKategori == KomutKatalogu.MakroKategorisi ? KomutKatalogu.MakroUzunlugu : KomutKatalogu.MetinUzunlugu;
                    tbMetin.Font = kutuYazisi;
                    tbMetin.BackColor = KutuRengi;
                    tbMetin.ForeColor = Color.FromArgb(255, T.Yazi);
                    tbMetin.Location = new Point((int)((metinAlani.X + 9) * O), (int)((metinAlani.Y + 7) * O));
                    tbMetin.Size = new Size((int)((metinAlani.Width - 18) * O), (int)((metinAlani.Height - 14) * O));
                    tbMetin.Visible = true;
                }
                else if (tbMetin.Visible)
                {
                    if (tbMetin.Focused) ActiveControl = null;
                    tbMetin.Visible = false;
                }
                if (duzen)
                {
                    var m = taslak[duzenNo];
                    if (tbAd.Text != m.Ad) tbAd.Text = m.Ad;
                    var ad = m.Tuslar[duzenTus].Ad ?? "";
                    if (tbTusAd.Text != ad) tbTusAd.Text = ad;
                    tbTusAd.PlaceholderText = KomutKatalogu.Ad(m.Tuslar[duzenTus].Kod);
                }
            }
            finally
            {
                kutuYaziliyor = false;
            }
        }

        static bool MetinKutusuKategorisi(string kat) => kat == KomutKatalogu.MetinKategorisi || kat == KomutKatalogu.MakroKategorisi;

        void Yerlestir(TextBox t, RectangleF r, bool goster)
        {
            if (!goster)
            {
                if (t.Visible)
                {
                    if (t.Focused) ActiveControl = null;
                    t.Visible = false;
                }
                return;
            }
            t.Font = kutuYazisi;
            t.BackColor = KutuRengi;
            t.ForeColor = Color.FromArgb(255, T.Yazi);
            t.Width = (int)((r.Width - 18) * O);
            t.Location = new Point((int)((r.X + 9) * O), (int)(r.Y * O + (r.Height * O - t.Height) / 2));
            t.Visible = true;
        }

        void KutuCiz(Graphics g, RectangleF r, TextBox t)
        {
            Doldur(g, KutuRengi, r, 8);
            bool odak = t != null && t.Focused;
            using var kalem = new Pen(odak ? T.Vurgu : T.Kenar, odak ? 2f : 1f);
            using var yol = Yuvarlak(odak ? RectangleF.Inflate(r, -0.5f, -0.5f) : r, 8);
            g.DrawPath(kalem, yol);
        }

        // ------------------------------------------------------------------ Modlar sekmesi
        void ModlarSekmesi(Graphics g, RectangleF a)
        {
            if (durum.ModAdlari.Length == 0)
            {
                Yaz(g, durum.Bagli ? "Modlar okunuyor…" : "Klavye bağlanınca modları burada görürsün.", 9.5f, FontStyle.Regular,
                    T.Soluk, new RectangleF(a.X, a.Y + 20, a.Width, 40), StringAlignment.Center);
                return;
            }
            bool duzenlenebilir = durum.ModDuzenDestek && durum.ModTanimlari != null;
            int n = durum.ModAdlari.Length;
            modListeAlani = new RectangleF(a.X, a.Y, a.Width, a.Height - 24);

            // içerik yüksekliği
            float icerik = 0;
            for (int i = 0; i < n; i++) icerik += (i == durum.AktifMod ? 98 : 50) + 8;
            if (duzenlenebilir) icerik += (n < ModTanimi.EnFazlaMod ? 46 : 0) + 26;
            modIcerik = icerik;
            modKaydirma = Math.Clamp(modKaydirma, 0, Math.Max(0, modIcerik - modListeAlani.Height));
            bool kaydirma = modIcerik > modListeAlani.Height;
            float gen = a.Width - (kaydirma ? 8 : 0);

            var onceki = g.Save();
            g.SetClip(modListeAlani);
            float y = a.Y - modKaydirma;
            for (int i = 0; i < n; i++)
            {
                bool etkin = i == durum.AktifMod;
                var renk = i < durum.ModRenkleri.Length ? durum.ModRenkleri[i] : T.Vurgu;
                var tuslar = i < durum.ModTuslari.Length && durum.ModTuslari[i] != null ? durum.ModTuslari[i] : new string[0];
                float kh = etkin ? 98 : 50;
                var kart = new RectangleF(a.X, y, gen, kh);
                y += kh + 8;
                if (kart.Bottom < modListeAlani.Y || kart.Y > modListeAlani.Bottom) continue;
                bool suruklenen = modSurukleniyor && i == modSurukle;
                bool birakmaYeriM = modSurukleniyor && i != modSurukle && uzerindeki == "mod" + i;
                Doldur(g, (uzerindeki == "mod" + i && !etkin) || birakmaYeriM ? Karistir(T.Kart, T.Uzerinde) : T.Kart, kart, 12);
                if (suruklenen)
                    using (var f = new SolidBrush(Color.FromArgb(60, renk))) g.FillRectangle(f, kart);
                if (birakmaYeriM)
                    using (var kalem = new Pen(renk, 2f) { DashStyle = DashStyle.Dash })
                    using (var yol = Yuvarlak(kart, 12))
                        g.DrawPath(kalem, yol);
                using (var kalem = new Pen(etkin ? renk : T.Kenar, etkin ? 2f : 1f))
                using (var yol = Yuvarlak(kart, 12))
                    g.DrawPath(kalem, yol);
                using (var f = new SolidBrush(renk))
                    g.FillEllipse(f, kart.X + 12, kart.Y + 15, 9, 9);
                float sagBosluk = duzenlenebilir ? 34 : 12;
                Yaz(g, durum.ModAdlari[i], 10f, FontStyle.Bold, T.Yazi, new RectangleF(kart.X + 28, kart.Y + 9, kart.Width - 120, 20));
                Yaz(g, etkin ? "ETKİN" : $"{i + 1}/{n}", 7.5f, FontStyle.Bold, etkin ? renk : T.Soluk,
                    new RectangleF(kart.Right - sagBosluk - 68, kart.Y + 11, 68, 16), StringAlignment.Far);
                if (etkin)
                {
                    int akt = i;
                    var gorunenE = RectangleF.Intersect(kart, modListeAlani);
                    if (gorunenE.Height > 6) Ekle(gorunenE, () => { }, "mod" + akt);
                    float hw = (kart.Width - 24 - 10) / 3;
                    for (int t = 0; t < 6; t++)
                    {
                        var r = new RectangleF(kart.X + 12 + (t % 3) * (hw + 5), kart.Y + 36 + (t / 3) * 30, hw, 25);
                        Doldur(g, T.Sek, r, 6);
                        Yaz(g, t < tuslar.Length ? tuslar[t] : "", 8f, FontStyle.Regular, T.Yazi, r, StringAlignment.Center);
                    }
                }
                else
                {
                    Yaz(g, string.Join(" · ", tuslar), 8f, FontStyle.Regular, T.Soluk, new RectangleF(kart.X + 12, kart.Y + 28, kart.Width - 24, 16));
                    int no = i;
                    var gorunen = RectangleF.Intersect(kart, modListeAlani);
                    if (gorunen.Height > 6) Ekle(gorunen, () => ModIstendi?.Invoke(no), "mod" + i);
                }
                if (duzenlenebilir)
                {
                    var kr = new RectangleF(kart.Right - 32, kart.Y + 7, 24, 24);
                    if (kr.Y >= modListeAlani.Y - 4 && kr.Bottom <= modListeAlani.Bottom + 4)
                    {
                        int no = i;
                        IkonDugme(g, "kalem" + i, GlifKalem, kr, 9.5f, () => DuzenAc(no, false));
                    }
                }
            }
            if (duzenlenebilir)
            {
                if (n < ModTanimi.EnFazlaMod)
                {
                    var ek = new RectangleF(a.X, y, gen, 38);
                    using (var kalem = new Pen(uzerindeki == "yenimod" ? T.Soluk : T.Kenar, 1.5f) { DashStyle = DashStyle.Dash })
                    using (var yol = Yuvarlak(ek, 12))
                        g.DrawPath(kalem, yol);
                    Yaz(g, "+  Yeni mod", 9f, FontStyle.Bold, uzerindeki == "yenimod" ? T.Yazi : T.Soluk, ek, StringAlignment.Center);
                    var gorunen = RectangleF.Intersect(ek, modListeAlani);
                    var ekAlani = ek;
                    if (gorunen.Height > 6) Ekle(gorunen, () => YeniModMenusu(ekAlani), "yenimod");
                    y += 46;
                }
                var sf = new RectangleF(a.X, y, gen, 22);
                if (sifirlaOnay)
                {
                    Yaz(g, "Fabrika modları geri gelsin mi?", 8f, FontStyle.Regular, R(248, 113, 113), new RectangleF(sf.X, sf.Y, 170, 22));
                    KucukDugme(g, new RectangleF(sf.Right - 128, sf.Y, 60, 22), "Evet", "sifevet", () => { sifirlaOnay = false; ModlarSifirla?.Invoke(); }, 2);
                    KucukDugme(g, new RectangleF(sf.Right - 64, sf.Y, 64, 22), "Vazgeç", "sifhayir", () => sifirlaOnay = false, 0);
                }
                else
                {
                    Yaz(g, "Varsayılan modlara dön", 8f, FontStyle.Regular, uzerindeki == "sifirla" ? T.Yazi : T.Soluk, sf, StringAlignment.Center);
                    var gorunen = RectangleF.Intersect(sf, modListeAlani);
                    if (gorunen.Height > 6) Ekle(gorunen, () => sifirlaOnay = true, "sifirla");
                }
            }
            g.Restore(onceki);
            if (kaydirma) KaydirmaCubugu(g, modListeAlani, modKaydirma, modIcerik);

            Yaz(g, !durum.Bagli ? "Mod değiştirmek için klavye bağlı olmalı"
                   : duzenlenebilir ? "Tıkla: klavyede aç · sürükle: sırala · kalem: düzenle"
                   : durum.ModDuzenDestek ? "Modlar okunuyor…"
                   : "Mod düzenlemek için klavyenin yazılımı 1.11 olmalı", 8f,
                FontStyle.Regular, T.Soluk, new RectangleF(a.X, a.Bottom - 20, a.Width - 80, 18), StringAlignment.Near);
            if (duzenlenebilir)       // 3.2 (AD): tuş haritasını resim olarak kaydet
            {
                var hr = new RectangleF(a.Right - 76, a.Bottom - 22, 76, 20);
                Yaz(g, "Harita kaydet", 8f, FontStyle.Regular, uzerindeki == "harita" ? T.Yazi : T.Soluk, hr, StringAlignment.Far);
                Ekle(hr, HaritayiKaydet, "harita");
            }
        }

        void KaydirmaCubugu(Graphics g, RectangleF alan, float kaydirma, float icerik)
        {
            var iz = new RectangleF(alan.Right - 4, alan.Y, 4, alan.Height);
            Doldur(g, T.Iz, iz, 2);
            float boy = Math.Max(20, iz.Height * alan.Height / icerik);
            float y = iz.Y + (iz.Height - boy) * (kaydirma / (icerik - alan.Height));
            Doldur(g, T.Soluk, new RectangleF(iz.X, y, iz.Width, boy), 2);
        }

        /// <summary>stil: 0 normal, 1 ana (vurgu), 2 tehlike (kırmızı yazı)</summary>
        void KucukDugme(Graphics g, RectangleF r, string metin, string kimlik, Action tik, int stil, bool etkin = true)
        {
            if (!etkin && stil == 1) stil = 0;     // pasif ana düğme: beyaz üstüne soluk yazı görünmüyordu
            var arka = stil == 1 ? T.Vurgu : T.Sek;
            if (etkin && uzerindeki == kimlik) arka = stil == 1 ? Karistir(T.Vurgu, R(255, 255, 255, 40)) : Karistir(T.Sek, T.Uzerinde);
            Doldur(g, arka, r, 8);
            if (stil != 1)
                using (var kalem = new Pen(T.Kenar, 1f))
                using (var yol = Yuvarlak(r, 8))
                    g.DrawPath(kalem, yol);
            var yazi = stil == 1 ? (T.Vurgu == Color.FromArgb(255, 255, 255) ? R(27, 20, 70) : Color.White)
                     : stil == 2 ? R(248, 113, 113) : T.Yazi;
            if (!etkin) yazi = T.Soluk;
            Yaz(g, metin, 8.5f, FontStyle.Bold, yazi, r, StringAlignment.Center);
            if (etkin) Ekle(r, tik, kimlik);
        }

        /// <summary>3.2 (AD): bütün modların tuş haritasını PNG olarak kaydeder.</summary>
        void HaritayiKaydet()
        {
            var liste = taslak ?? durum.ModTanimlari;
            if (liste == null || liste.Count == 0) return;
            renkPenceresiAcik = true;
            try
            {
                using var d = new SaveFileDialog
                {
                    Title = "Tuş haritasını kaydet",
                    Filter = "PNG resmi (*.png)|*.png",
                    FileName = "makro-klavye-tus-haritasi.png",
                };
                if (d.ShowDialog(this) != DialogResult.OK) return;
                using var resim = TusHaritasi.Ciz(liste, ad => string.IsNullOrEmpty(ad) ? null : SimgeResmi(ad, Color.White));
                resim.Save(d.FileName, System.Drawing.Imaging.ImageFormat.Png);
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(d.FileName) { UseShellExecute = true }); } catch { }
            }
            catch (Exception ex) { Gunluk.Hata("tuş haritası", ex); }
            finally { renkPenceresiAcik = false; }
        }

        // ------------------------------------------------------------------ düzenleyiciye giriş / çıkış
        void DuzenAc(int no, bool yeni)
        {
            if (durum.ModTanimlari == null) return;
            taslak = ModTanimi.Kopya(durum.ModTanimlari);
            if (yeni)
            {
                if (taslak.Count >= ModTanimi.EnFazlaMod) return;
                taslak.Add(new ModTanimi
                {
                    Ad = "MOD " + (taslak.Count + 1),
                    Renk = HazirRenkler[(taslak.Count + 1) % HazirRenkler.Length],
                    Ekran = "tuslar",
                });
                no = taslak.Count - 1;
            }
            duzenNo = Math.Clamp(no, 0, taslak.Count - 1);
            duzenTus = 0;
            silOnay = cikisOnay = false;
            gorunum = Gorunum.ModDuzen;
            Boyutla();
        }

        /// <summary>uygulama 2.5: "+ Yeni mod": boş mod, hazır modlar ya da dosyadan.</summary>
        void YeniModMenusu(RectangleF r)
        {
            var menu = new ContextMenuStrip();
            var bos = new ToolStripMenuItem("Boş mod") { Font = new Font(menu.Font, FontStyle.Bold) };
            bos.Click += (s, e) => DuzenAc(-1, true);
            menu.Items.Add(bos);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("HAZIR MODLAR") { Enabled = false });
            foreach (var sb in ModSablonlari.Liste)
            {
                var sablon = sb;
                var oge = new ToolStripMenuItem(sablon.Baslik) { ToolTipText = sablon.Aciklama };
                oge.Click += (s, e) => DuzenAcYeni(new List<ModTanimi> { sablon.Yap() });
                menu.Items.Add(oge);
            }
            menu.Items.Add(new ToolStripSeparator());
            var dosya = new ToolStripMenuItem("Dosyadan mod ekle…");
            dosya.Click += (s, e) => ModDosyadanEkle();
            menu.Items.Add(dosya);
            menu.ShowItemToolTips = true;
            renkPenceresiAcik = true;
            menu.Closed += (s, e) =>
            {
                renkPenceresiAcik = false;
                BeginInvoke(new Action(menu.Dispose));
            };
            menu.Show(this, new Point((int)(r.X * O), (int)(r.Bottom * O)));
        }

        /// <summary>Verilen modları (şablon ya da dosyadan) taslağın sonuna ekler ve ilkini düzenleyicide açar.</summary>
        void DuzenAcYeni(List<ModTanimi> yeniler)
        {
            if (durum.ModTanimlari == null || yeniler == null || yeniler.Count == 0) return;
            taslak = ModTanimi.Kopya(durum.ModTanimlari);
            int ilk = taslak.Count;
            foreach (var y in yeniler)
            {
                if (taslak.Count >= ModTanimi.EnFazlaMod) break;
                // aynı adda mod varsa sonuna sayı
                var kok = ModTanimi.Temizle(y.Ad, ModTanimi.AdUzunlugu - 2);
                for (int i = 2; taslak.Exists(t => t.Ad == y.Ad) && i < 10; i++) y.Ad = kok + " " + i;
                taslak.Add(y);
            }
            if (taslak.Count == ilk)
            {
                taslak = null;
                return;
            }
            duzenNo = ilk;
            duzenTus = 0;
            silOnay = cikisOnay = false;
            gorunum = Gorunum.ModDuzen;
            Boyutla();
        }

        const string ModUzantisi = ".mkmod";

        void ModDosyadanEkle()
        {
            renkPenceresiAcik = true;
            try
            {
                using var d = new OpenFileDialog
                {
                    Title = "Mod dosyası seç",
                    Filter = "Makro Klavye modu (*.mkmod)|*.mkmod|Bütün dosyalar|*.*",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                };
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var liste = ModTanimi.Coz(System.IO.File.ReadAllText(d.FileName));
                DuzenAcYeni(liste);
            }
            catch (Exception ex)
            {
                Gunluk.Hata("mod dosyası", ex);
                MessageBox.Show(this, "Mod dosyası okunamadı: " + ex.Message, "Makro Klavye", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                renkPenceresiAcik = false;
                Activate();
                Invalidate();
            }
        }

        void ModuDosyayaKaydet(ModTanimi m)
        {
            renkPenceresiAcik = true;
            try
            {
                var ad = new string(m.Ad.Where(c => !System.IO.Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
                using var d = new SaveFileDialog
                {
                    Title = "Modu dosyaya kaydet",
                    Filter = "Makro Klavye modu (*.mkmod)|*.mkmod",
                    FileName = (ad == "" ? "mod" : ad.ToLowerInvariant()) + ModUzantisi,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                };
                if (d.ShowDialog(this) != DialogResult.OK) return;
                System.IO.File.WriteAllText(d.FileName, ModTanimi.Json(new List<ModTanimi> { m }), new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Gunluk.Hata("mod kaydetme", ex);
                MessageBox.Show(this, "Kaydedilemedi: " + ex.Message, "Makro Klavye", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                renkPenceresiAcik = false;
                Activate();
                Invalidate();
            }
        }

        // ------------------------------------------------------------------ tuşlar: sürükle-bırak, kopyala / yapıştır (uygulama 2.5)
        int tusSurukle = -1;               // sürüklenmeye başlanan tuş (sol tuş basılı)
        bool tusSurukleniyor;              // gerçekten sürükleniyor (birkaç piksel kıpırdadı)
        Point surukleBaslangic;
        TusTanimi panoTus;                 // kopyalanan tuş

        // ------------------------------------------------------------------ 3.2 (AX): modları sürükleyerek sırala
        int modSurukle = -1;
        bool modSurukleniyor;
        Point modSurukleBas;

        bool ModSurukleBasla(Hedef h, Point p)
        {
            modSurukle = -1;
            modSurukleniyor = false;
            if (gorunum != Gorunum.Modlar || durum.ModTanimlari == null || h?.Kimlik == null || !h.Kimlik.StartsWith("mod")) return false;
            if (!int.TryParse(h.Kimlik.Substring(3), out int no) || no >= durum.ModTanimlari.Count) return false;
            modSurukle = no;
            modSurukleBas = p;
            return true;
        }

        void ModSurukleHareket(Point p)
        {
            if (modSurukle < 0 || modSurukleniyor) return;
            if (Math.Abs(p.X - modSurukleBas.X) + Math.Abs(p.Y - modSurukleBas.Y) > 8 * O)
            {
                modSurukleniyor = true;
                Invalidate();
            }
        }

        /// <summary>Bırakıldı: başka bir modun üstündeyse sıraya oraya taşınır. true: işlendi.</summary>
        bool ModSurukleBitir(Hedef h)
        {
            int kaynak = modSurukle;
            bool suruklendi = modSurukleniyor;
            modSurukle = -1;
            modSurukleniyor = false;
            if (kaynak < 0 || !suruklendi) return false;
            var liste = durum.ModTanimlari;
            if (liste != null && h?.Kimlik != null && h.Kimlik.StartsWith("mod")
                && int.TryParse(h.Kimlik.Substring(3), out int hedef) && hedef != kaynak
                && kaynak < liste.Count && hedef < liste.Count)
            {
                var yeni = ModTanimi.Kopya(liste);
                var tasinan = yeni[kaynak];
                yeni.RemoveAt(kaynak);
                yeni.Insert(hedef, tasinan);
                ModlarKaydet?.Invoke(yeni);
                durum.ModKayit = "gidiyor";
            }
            Invalidate();
            return true;
        }

        /// <summary>Modu düzenle'de bir tuş karosuna basıldı: sürükleme başlayabilir.</summary>
        bool TusSurukleBasla(Hedef h, Point p)
        {
            if (gorunum != Gorunum.ModDuzen || taslak == null || h?.Kimlik == null || !h.Kimlik.StartsWith("mtus")) return false;
            if (!int.TryParse(h.Kimlik.Substring(4), out int no)) return false;
            tusSurukle = no;
            tusSurukleniyor = false;
            surukleBaslangic = p;
            return true;
        }

        void TusSurukleHareket(Point p)
        {
            if (tusSurukle < 0 || tusSurukleniyor) return;
            if (Math.Abs(p.X - surukleBaslangic.X) + Math.Abs(p.Y - surukleBaslangic.Y) > 6 * O)
            {
                tusSurukleniyor = true;
                Invalidate();
            }
        }

        /// <summary>Bırakıldı: başka bir tuşun üstündeyse iki tuş yer değiştirir. true: işlendi (normal tıklama değil).</summary>
        bool TusSurukleBitir(Hedef h)
        {
            int kaynak = tusSurukle;
            bool suruklendi = tusSurukleniyor;
            tusSurukle = -1;
            tusSurukleniyor = false;
            if (kaynak < 0 || !suruklendi) return false;
            if (taslak != null && h?.Kimlik != null && h.Kimlik.StartsWith("mtus") && int.TryParse(h.Kimlik.Substring(4), out int hedef) && hedef != kaynak)
            {
                var t = taslak[duzenNo].Tuslar;
                (t[kaynak], t[hedef]) = (t[hedef], t[kaynak]);
                duzenTus = hedef;
                KutulariYerlestir();
            }
            Invalidate();
            return true;
        }

        TusTanimi TusKopyasi(TusTanimi t) => new TusTanimi { Ad = t.Ad, Kod = t.Kod, UzunKod = t.UzunKod, Tekrar = t.Tekrar, Simge = t.Simge };

        // ------------------------------------------------------------------ 3.1 (AE): dosyayı tuşun üstüne bırak
        string birakmaHedefi;              // üstünde durulan tuş karosu ("mtus3")

        void SurukleGirdi(object g, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (gorunum != Gorunum.ModDuzen || taslak == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var h = Bul(PointToClient(new Point(e.X, e.Y)));
            if (h?.Kimlik == null || !h.Kimlik.StartsWith("mtus")) { BirakmaHedefi(null); return; }
            e.Effect = DragDropEffects.Copy;
            BirakmaHedefi(h.Kimlik);
        }

        void BirakmaHedefi(string k)
        {
            if (birakmaHedefi == k) return;
            birakmaHedefi = k;
            uzerindeki = k;                 // karo vurgulansın
            Invalidate();
        }

        void SurukleBirakildi(object g, DragEventArgs e)
        {
            string kimlik = birakmaHedefi;
            BirakmaHedefi(null);
            if (gorunum != Gorunum.ModDuzen || taslak == null || kimlik == null) return;
            if (!int.TryParse(kimlik.Substring(4), out int no) || no < 0 || no > 5) return;
            if (!(e.Data.GetData(DataFormats.FileDrop) is string[] dosyalar) || dosyalar.Length == 0) return;
            string yol = dosyalar[0];
            var t = taslak[duzenNo].Tuslar[no];
            t.Kod = "APP:" + yol;
            t.Ad = ModTanimi.Temizle(System.IO.Path.GetFileNameWithoutExtension(yol.TrimEnd('\\', '/')), ModTanimi.TusAdiUzunlugu);
            if (t.Ad == "") t.Ad = "Aç";
            duzenTus = no;
            var simge = OzelSimge.UygulamadanAl(yol, System.IO.Path.GetFileNameWithoutExtension(yol));
            if (simge != null)              // programın kendi simgesi de gelsin (BG)
            {
                OzelSimge.Hepsi.Add(simge);
                OzelSimge.Kaydet();
                OzelResimleriUnut();
                t.Simge = "*" + simge.Ad;
                OzelSimgelerDegisti?.Invoke();
            }
            KutulariYerlestir();
            Invalidate();
        }

        /// <summary>Tuş karosuna sağ tık: kopyala, yapıştır, boşalt.</summary>
        void TusMenusu(int no, Point p)
        {
            if (taslak == null) return;
            var t = taslak[duzenNo].Tuslar;
            var menu = new ContextMenuStrip();
            var kop = new ToolStripMenuItem("Kopyala");
            kop.Click += (s, e) => panoTus = TusKopyasi(t[no]);
            var yap = new ToolStripMenuItem("Yapıştır" + (panoTus != null && !string.IsNullOrEmpty(panoTus.Ad) ? $" ({panoTus.Ad})" : "")) { Enabled = panoTus != null };
            yap.Click += (s, e) => { t[no] = TusKopyasi(panoTus); duzenTus = no; KutulariYerlestir(); Invalidate(); };
            var bos = new ToolStripMenuItem("Boşalt");
            bos.Click += (s, e) => { t[no] = new TusTanimi(); duzenTus = no; KutulariYerlestir(); Invalidate(); };
            menu.Items.Add(kop);
            menu.Items.Add(yap);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(bos);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("İpucu: tuşu sürükleyip başka tuşun üstüne bırakınca yer değiştirir") { Enabled = false });
            renkPenceresiAcik = true;
            menu.Closed += (s, e) =>
            {
                renkPenceresiAcik = false;
                BeginInvoke(new Action(menu.Dispose));
            };
            menu.Show(this, p);
        }

        static readonly (string ad, Color? renk)[] ZeminRenkleri =
        {
            ("Varsayılan", null),
            ("Lacivert", Color.FromArgb(16, 28, 46)),
            ("Mor", Color.FromArgb(32, 18, 48)),
            ("Yeşil", Color.FromArgb(12, 36, 28)),
            ("Bordo", Color.FromArgb(44, 14, 22)),
            ("Kahve", Color.FromArgb(42, 26, 5)),
            ("Gri", Color.FromArgb(28, 30, 34)),
        };
        static readonly string[] DuzenAdlari = { "2x3 karo", "Liste" };

        /// <summary>Uygulama 3.2: modun zemini, tuş düzeni ve grubu (kart 3.2).</summary>
        void ModGorunumCiz(Graphics g, float w, float h)
        {
            if (taslak == null || duzenNo >= taslak.Count)
            {
                gorunum = Gorunum.ModDuzen;
                Boyutla();
                return;
            }
            var m = taslak[duzenNo];
            IkonDugme(g, "geri", GlifGeri, new RectangleF(P - 4, P - 2, 26, 26), 11f,
                      () => { gorunum = Gorunum.ModDuzen; Boyutla(); KutulariYerlestir(); });
            Yaz(g, "Modun görünümü", 12f, FontStyle.Bold, T.Yazi, new RectangleF(P + 28, P - 1, w - 2 * P - 28, 24));
            bool destek = KartEnAzPanel(3, 2);
            if (!destek)
                Yaz(g, "Bunlar için kart yazılımı 3.2 gerekli", 8f, FontStyle.Regular, R(248, 113, 113),
                    new RectangleF(P, 42, w - 2 * P, 16));

            Grup(g, "ZEMİN RENGİ", 62);
            float x = P, cy = 96;
            for (int i = 0; i < ZeminRenkleri.Length; i++)
            {
                var (ad, renk) = ZeminRenkleri[i];
                bool secili = (m.Zemin?.ToArgb() ?? 0) == (renk?.ToArgb() ?? 0);
                var d = new RectangleF(x, cy - 13, 26, 26);
                if (renk == null)
                {
                    Doldur(g, R(11, 16, 24), d, 13);
                    using (var kalem = new Pen(secili ? T.Yazi : T.Kenar, secili ? 2f : 1f))
                        g.DrawEllipse(kalem, d.X, d.Y, d.Width, d.Height);
                    Yaz(g, "—", 9f, FontStyle.Bold, T.Soluk, d, StringAlignment.Center);
                }
                else RenkDairesi(g, d, renk.Value, secili, "zem" + i);
                var secilen = renk;
                if (destek) Ekle(RectangleF.Inflate(d, 2, 2), () => { m.Zemin = secilen; Invalidate(); }, "zem" + i);
                x += 32;
            }
            var ozel = new RectangleF(x, cy - 13, 26, 26);
            OzelDugme(g, ozel, null);
            if (destek) Ekle(RectangleF.Inflate(ozel, 2, 2),
                             () => OzelRenkSec(m.Zemin ?? R(16, 28, 46), c => m.Zemin = c), "zemozel");
            Yaz(g, "Modun tuş sayfasının arka planı", 7.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, cy + 18, w - 2 * P, 16));

            Grup(g, "TUŞ DÜZENİ", 146);
            Segment(g, new RectangleF(P, 164, w - 2 * P, 30), DuzenAdlari, m.Duzen, "mduzen",
                    i => { if (destek) m.Duzen = i; });
            Yaz(g, m.Duzen == 1 ? "Altı tuş alt alta liste olarak görünür · simge solda, uzun komut sağda"
                  : "Altı tuş 2x3 karo olarak görünür",
                7.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, 198, w - 2 * P, 16));

            Grup(g, "GRUP", 224);
            float gw = (w - 2 * P - 12) / 3;
            var gruplar = (taslak ?? new List<ModTanimi>()).Select(z => z.Grup).Where(z => !string.IsNullOrEmpty(z))
                          .Distinct().OrderBy(z => z).Take(3).ToList();
            for (int i = 0; i < 3; i++)
            {
                var r = new RectangleF(P + i * (gw + 6), 242, gw, 28);
                string ad = i < gruplar.Count ? gruplar[i] : (i == 0 ? "Grupsuz" : "");
                if (ad == "") continue;
                bool secili = i < gruplar.Count ? m.Grup == ad : string.IsNullOrEmpty(m.Grup);
                string secilen = i < gruplar.Count ? ad : "";
                KucukDugme(g, r, ad, "grup" + i, () => { if (destek) { m.Grup = secilen; kutuYaziliyor = true; tbGrup.Text = secilen; kutuYaziliyor = false; Invalidate(); } },
                           secili ? 1 : 0, destek);
            }
            Yaz(g, "Tekerlek yalnızca aynı gruptaki modlar arasında döner. Düğmeye basılı tutup çevirince sıradaki gruba atlar.",
                7.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, 276, w - 2 * P, 32), StringAlignment.Near, true);
            Yaz(g, "GRUP ADI", 7.5f, FontStyle.Bold, T.Soluk, new RectangleF(P, 312, w - 2 * P, 16));
            KutuCiz(g, GrupKutusu, tbGrup);
            Yaz(g, "Boş bırakırsan mod grupsuz olur", 7.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, 364, w - 2 * P, 16));

            using (var kalem = new Pen(T.Kenar, 1f))
                g.DrawLine(kalem, P, h - 48, w - P, h - 48);
            Yaz(g, m.Ad, 8.5f, FontStyle.Bold, m.Renk, new RectangleF(P, h - 38, w - 2 * P - 110, 28));
            KucukDugme(g, new RectangleF(w - P - 100, h - 38, 100, 28), "Bitti", "gorunumbitti",
                       () => { gorunum = Gorunum.ModDuzen; Boyutla(); KutulariYerlestir(); }, 1);
        }

        void DuzendenCik(bool zorla = false)
        {
            if (!zorla && Kirli)
            {
                cikisOnay = true;
                Invalidate();
                return;
            }
            taslak = null;
            kayitModu = false;
            silOnay = cikisOnay = false;
            gorunum = Gorunum.Modlar;
            Boyutla();
        }

        void TaslagiKaydet()
        {
            if (taslak == null || !durum.Bagli) return;
            ModlarKaydet?.Invoke(ModTanimi.Kopya(taslak));
            durum.ModKayit = "gidiyor";
            Invalidate();
        }

        // ------------------------------------------------------------------ mod düzenleyici
        void ModDuzenCiz(Graphics g, float w, float h)
        {
            if (taslak == null || duzenNo >= taslak.Count)
            {
                DuzendenCik(true);
                return;
            }
            var m = taslak[duzenNo];
            IkonDugme(g, "geri", GlifGeri, new RectangleF(P - 4, P - 2, 26, 26), 11f, () => DuzendenCik());
            Yaz(g, "Modu düzenle", 12f, FontStyle.Bold, T.Yazi, new RectangleF(P + 28, P - 1, 180, 24));
            Yaz(g, $"{duzenNo + 1}/{taslak.Count}", 8f, FontStyle.Regular, T.Soluk, new RectangleF(w - P - 40, P + 2, 40, 18), StringAlignment.Far);
            IkonDugme(g, "moddosya", "\uE792", new RectangleF(w - P - 70, P - 2, 26, 26), 10f, () => ModuDosyayaKaydet(m));   // Segoe MDL2: kaydet
            // uygulama 3.2: modun görünümü (zemin, tuş düzeni) ve grubu
            var gk = new RectangleF(w - P - 100, P - 2, 26, 26);
            IkonDugme(g, "modgorunum", "\uE790", gk, 10f, () => { gorunum = Gorunum.ModGorunum; Boyutla(); KutulariYerlestir(); });
            if (!string.IsNullOrEmpty(m.Grup))
                Yaz(g, m.Grup, 7.5f, FontStyle.Bold, m.Renk, new RectangleF(P + 130, P + 3, 100, 16));

            Grup(g, "AD", 50);
            KutuCiz(g, AdKutusu, tbAd);

            Grup(g, "RENK", 102);
            float x = P, cy = 129;
            bool hazirda = false;
            for (int i = 0; i < HazirRenkler.Length; i++)
            {
                var c = HazirRenkler[i];
                bool secili = c.ToArgb() == m.Renk.ToArgb();
                hazirda |= secili;
                var d = new RectangleF(x, cy - 11, 22, 22);
                RenkDairesi(g, d, c, secili, "mrenk" + i);
                Ekle(RectangleF.Inflate(d, 2, 2), () => { m.Renk = c; Invalidate(); }, "mrenk" + i);
                x += 28;
            }
            var oz = new RectangleF(x, cy - 11, 22, 22);
            if (!hazirda) RenkDairesi(g, oz, m.Renk, true, "ozel");
            else OzelDugme(g, oz, null);
            Ekle(RectangleF.Inflate(oz, 2, 2), () => OzelRenkSec(m.Renk, c => m.Renk = c), "ozel");

            // uygulama 2.5: üç sütun: açılışta ekran · sağ tekerlek · modun ışığı
            float uc = (w - 2 * P - 12) / 3;
            Yaz(g, "AÇILIŞTA", 7.5f, FontStyle.Bold, T.Soluk, new RectangleF(P, 150, uc, 16));
            Segment(g, new RectangleF(P, 166, uc, 28), new[] { "Şarkı", "Tuşlar" }, m.Ekran == "medya" ? 0 : 1, "mekran",
                    i => m.Ekran = i == 0 ? "medya" : "tuslar");
            // uygulama 2.4 / kart 1.14: sağ tekerleğin bu moddaki görevi
            Yaz(g, "SAĞ TEKERLEK", 7.5f, FontStyle.Bold, T.Soluk, new RectangleF(P + uc + 6, 150, uc, 16));
            var tk = new RectangleF(P + uc + 6, 166, uc, 28);
            bool tekerDestek = KartEnAzPanel(1, 14);
            MenuDugmesi(g, tk, tekerDestek ? KomutKatalogu.TekerAdi(m.Tekerlek) : "Kart 1.14", "teker", tekerDestek, () => TekerMenusu(m, tk));
            // uygulama 2.5 / kart 1.20: modun kendi ışığı
            Yaz(g, "IŞIK", 7.5f, FontStyle.Bold, T.Soluk, new RectangleF(P + 2 * (uc + 6), 150, uc, 16));
            var ik = new RectangleF(P + 2 * (uc + 6), 166, uc, 28);
            bool isikDestek = KartEnAzPanel(1, 20);
            MenuDugmesi(g, ik, !isikDestek ? "Kart 1.20" : string.IsNullOrEmpty(m.Isik) ? "Genel" : LedEfektleri.Bul(m.Isik).Ad,
                        "misik", isikDestek, () => IsikMenusu(m, ik),
                        isikDestek && !string.IsNullOrEmpty(m.Isik) && LedEfektleri.Bul(m.Isik).RenkKullanimi > 0 ? m.IsikRenk : (Color?)null);

            Grup(g, "TUŞLAR", 204);
            float tw = (w - 2 * P - 12) / 3, th = 52;
            for (int t = 0; t < 6; t++)
            {
                var r = new RectangleF(P + (t % 3) * (tw + 6), 220 + (t / 3) * (th + 6), tw, th);
                bool secili = t == duzenTus;
                var tus = m.Tuslar[t];
                bool birakmaYeri = tusSurukleniyor && t != tusSurukle && uzerindeki == "mtus" + t;
                Doldur(g, (!secili && uzerindeki == "mtus" + t) || birakmaYeri ? Karistir(T.Kart, T.Uzerinde) : T.Kart, r, 9);
                if (tusSurukleniyor && t == tusSurukle)
                    using (var f = new SolidBrush(Color.FromArgb(70, m.Renk))) g.FillRectangle(f, r);
                using (var kalem = new Pen(secili || birakmaYeri ? m.Renk : T.Kenar, secili || birakmaYeri ? 2f : 1f) { DashStyle = birakmaYeri ? DashStyle.Dash : DashStyle.Solid })
                using (var yol = Yuvarlak(secili ? RectangleF.Inflate(r, -1, -1) : r, 9))
                    g.DrawPath(kalem, yol);
                Yaz(g, (t + 1).ToString(), 7.5f, FontStyle.Regular, T.Soluk, new RectangleF(r.X + 8, r.Y + 4, 20, 14));
                Yaz(g, string.IsNullOrEmpty(tus.Ad) ? "—" : tus.Ad, 9f, FontStyle.Bold, T.Yazi, new RectangleF(r.X + 8, r.Y + 17, r.Width - 12, 18));
                Yaz(g, KomutKatalogu.Tur(tus.Kod) + (string.IsNullOrEmpty(tus.UzunKod) ? "" : " · +uzun"), 7.5f, FontStyle.Regular, T.Soluk,
                    new RectangleF(r.X + 8, r.Y + 33, r.Width - 12, 15));
                int no = t;
                Ekle(r, () => { duzenTus = no; KutulariYerlestir(); }, "mtus" + t);
            }

            var secilen = m.Tuslar[duzenTus];
            Grup(g, $"{duzenTus + 1}. TUŞ: EKRANDAKİ ADI VE KOMUTU", 338);
            KutuCiz(g, TusAdKutusu, tbTusAd);
            var sk = new RectangleF(w - P - 202, 354, 56, 32);
            bool simgeDestek = KartEnAzPanel(2, 7);
            KucukDugme(g, sk, (string.IsNullOrEmpty(secilen.Simge) ? "" : "✓ ") + "Simge", "simge", SimgeSecAc, string.IsNullOrEmpty(secilen.Simge) ? 0 : 1, simgeDestek);
            if (uzerindeki == "simge")
                Yaz(g, simgeDestek ? "Tuşun ekrandaki simgesi: " + KomutKatalogu.SimgeYazisi(secilen.Simge) : "Simge için kart yazılımı 2.7 gerekli", 7.5f, FontStyle.Regular, T.Soluk,
                    new RectangleF(P, 387, w - 2 * P, 14));
            var kd = new RectangleF(w - P - 140, 354, 140, 32);
            Doldur(g, uzerindeki == "komut" ? Karistir(T.Sek, T.Uzerinde) : T.Sek, kd, 8);
            using (var kalem = new Pen(T.Kenar, 1f))
            using (var yol = Yuvarlak(kd, 8))
                g.DrawPath(kalem, yol);
            Yaz(g, KomutKatalogu.Ad(secilen.Kod) + "  ›", 8.5f, FontStyle.Bold, T.Yazi, new RectangleF(kd.X + 10, kd.Y + 2, kd.Width - 16, 16));
            Yaz(g, KomutKatalogu.Tur(secilen.Kod), 7.5f, FontStyle.Regular, T.Soluk, new RectangleF(kd.X + 10, kd.Y + 16, kd.Width - 16, 14));
            Ekle(kd, () => KomutSecAc("kisa"), "komut");

            // uygulama 2.4 / kart 1.14: basılı tutunca ikinci komut
            Yaz(g, "Basılı tutunca", 8.5f, FontStyle.Bold, T.Soluk, new RectangleF(P, 392, 100, 28));
            bool uzunDestek = KartEnAzPanel(1, 14);
            bool uzunVar = !string.IsNullOrEmpty(secilen.UzunKod);
            bool tekrarDestek = uzunVar && KartEnAzPanel(1, 22);
            var uk = new RectangleF(P + 104, 392, w - 2 * P - 104 - (tekrarDestek ? 84 : 0), 28);
            if (tekrarDestek)
            {
                // uygulama 2.6 / kart 1.22: basılı tutuldukça tekrarla (ör. ses yavaş yavaş açılır)
                KucukDugme(g, new RectangleF(w - P - 78, 392, 78, 28), (secilen.Tekrar ? "✓ " : "") + "Tekrarla", "uzuntekrar",
                           () => secilen.Tekrar = !secilen.Tekrar, secilen.Tekrar ? 1 : 0);
                if (uzerindeki == "uzuntekrar")
                    Yaz(g, "Açıksa tuşu bırakana kadar bu komut tekrar tekrar çalışır", 7.5f, FontStyle.Regular, T.Soluk,
                        new RectangleF(P, 421, w - 2 * P, 14));
            }
            Doldur(g, uzunDestek && uzerindeki == "uzunkomut" ? Karistir(T.Sek, T.Uzerinde) : T.Sek, uk, 8);
            using (var kalem = new Pen(T.Kenar, 1f))
            using (var yol = Yuvarlak(uk, 8))
                g.DrawPath(kalem, yol);
            string uzunYazi = !uzunDestek ? "Kart yazılımı 1.14 gerekli" : uzunVar ? KomutKatalogu.Ad(secilen.UzunKod) + "  ›" : "Yok · eklemek için tıkla";
            Yaz(g, uzunYazi, 8.5f, uzunVar ? FontStyle.Bold : FontStyle.Regular, uzunVar ? T.Yazi : T.Soluk,
                new RectangleF(uk.X + 10, uk.Y, uk.Width - (uzunVar ? 40 : 14), uk.Height));
            if (uzunDestek) Ekle(uk, () => KomutSecAc("uzun"), "uzunkomut");
            if (uzunDestek && uzunVar)
            {
                var sil = new RectangleF(uk.Right - 28, uk.Y + 2, 24, 24);
                if (uzerindeki == "uzunsil") Doldur(g, T.Uzerinde, sil, 6);
                Yaz(g, "×", 11f, FontStyle.Regular, uzerindeki == "uzunsil" ? R(248, 113, 113) : T.Soluk, sil, StringAlignment.Center);
                Ekle(sil, () => { secilen.UzunKod = ""; secilen.Tekrar = false; }, "uzunsil");
            }

            // hata satırı
            if (durum.ModKayit != null && durum.ModKayit.StartsWith("hata"))
                Yaz(g, "Kaydedilemedi: " + durum.ModKayit.Substring(Math.Min(5, durum.ModKayit.Length)), 8f, FontStyle.Regular,
                    R(248, 113, 113), new RectangleF(P, h - 70, w - 2 * P, 18));

            // alt satır
            using (var kalem = new Pen(T.Kenar, 1f))
                g.DrawLine(kalem, P, h - 48, w - P, h - 48);
            var ay = h - 38;
            if (silOnay)
            {
                Yaz(g, "Bu mod silinsin mi?", 8.5f, FontStyle.Bold, R(248, 113, 113), new RectangleF(P, ay, 130, 28));
                KucukDugme(g, new RectangleF(w - P - 150, ay, 70, 28), "Sil", "silevet", () =>
                {
                    taslak.RemoveAt(duzenNo);
                    TaslagiKaydet();
                    DuzendenCik(true);
                }, 2, durum.Bagli);
                KucukDugme(g, new RectangleF(w - P - 74, ay, 74, 28), "Vazgeç", "silhayir", () => silOnay = false, 0);
            }
            else if (cikisOnay)
            {
                Yaz(g, "Değişiklikler kaydedilmedi.", 8.5f, FontStyle.Bold, T.Yazi, new RectangleF(P, ay, 150, 28));
                KucukDugme(g, new RectangleF(w - P - 156, ay, 72, 28), "Kaydet", "cikkaydet", () => { TaslagiKaydet(); DuzendenCik(true); }, 1, durum.Bagli);
                KucukDugme(g, new RectangleF(w - P - 78, ay, 78, 28), "Kaydetme", "cikat", () => DuzendenCik(true), 0);
            }
            else
            {
                IkonDugme(g, "yukari", GlifYukari, new RectangleF(P - 2, ay, 28, 28), 9.5f, () => Tasi(-1));
                IkonDugme(g, "asagi", GlifAsagi, new RectangleF(P + 28, ay, 28, 28), 9.5f, () => Tasi(1));
                KucukDugme(g, new RectangleF(P + 62, ay, 46, 28), "Sil", "sil", () => silOnay = true, 2, taslak.Count > 1);
                KucukDugme(g, new RectangleF(P + 112, ay, 70, 28), "Kopyala", "kopyala", ModuKopyala, 0, taslak.Count < ModTanimi.EnFazlaMod);
                string metin = !durum.Bagli ? "Klavye bağlı değil"
                    : Kaydediliyor ? "Kaydediliyor…"
                    : !Kirli && durum.ModKayit == "tamam" ? "✓ Kaydedildi"
                    : !Kirli ? "✓ Klavyede kayıtlı"
                    : "Klavyeye kaydet";
                KucukDugme(g, new RectangleF(w - P - 132, ay, 132, 28), metin, "kaydet", TaslagiKaydet, 1, durum.Bagli && !Kaydediliyor && Kirli);
            }
        }

        /// <summary>uygulama 2.3: düzenlenen modun kopyası hemen arkasına eklenir ve açılır (kaydedince klavyeye gider).</summary>
        void ModuKopyala()
        {
            if (taslak == null || taslak.Count >= ModTanimi.EnFazlaMod) return;
            var kopya = taslak[duzenNo].Kopya();
            var kok = ModTanimi.Temizle(kopya.Ad, ModTanimi.AdUzunlugu - 3);
            int no = 2;
            kopya.Ad = kok + " " + no;
            while (taslak.Exists(t => t.Ad == kopya.Ad) && no < 99)
                kopya.Ad = kok + " " + (++no);
            taslak.Insert(duzenNo + 1, kopya);
            duzenNo++;
            silOnay = false;
            KutulariYerlestir();
        }

        /// <summary>Menü açan küçük düğme (ad + ›); renk verilirse solunda küçük bir renk noktası.</summary>
        void MenuDugmesi(Graphics g, RectangleF r, string metin, string kimlik, bool etkin, Action tik, Color? renk = null)
        {
            Doldur(g, etkin && uzerindeki == kimlik ? Karistir(T.Sek, T.Uzerinde) : T.Sek, r, 8);
            using (var kalem = new Pen(T.Kenar, 1f))
            using (var yol = Yuvarlak(r, 8))
                g.DrawPath(kalem, yol);
            float x = r.X + 8;
            if (renk.HasValue)
            {
                using (var f = new SolidBrush(renk.Value))
                    g.FillEllipse(f, x, r.Y + r.Height / 2 - 4, 8, 8);
                x += 12;
            }
            Yaz(g, metin + (etkin ? " ›" : ""), 8f, FontStyle.Bold, etkin ? T.Yazi : T.Soluk, new RectangleF(x, r.Y, r.Right - x - 4, r.Height));
            if (etkin) Ekle(r, tik, kimlik);
        }

        /// <summary>uygulama 2.5: modun ışığı menüsü: genel ayar ya da bir efekt (+ renk).</summary>
        void IsikMenusu(ModTanimi m, RectangleF r)
        {
            var menu = new ContextMenuStrip { ShowCheckMargin = true, ShowImageMargin = false };
            var genel = new ToolStripMenuItem("Genel ışık (Işık sekmesindeki)") { Checked = string.IsNullOrEmpty(m.Isik) };
            genel.Click += (s, e) => { m.Isik = ""; Invalidate(); };
            menu.Items.Add(genel);
            menu.Items.Add(new ToolStripSeparator());
            foreach (var b in LedEfektleri.Liste)
            {
                if (b.Muzik && b.Kod != "ritim" && b.Kod != "stereo") continue;      // kapak / ilerleme modda anlamsız
                var kod = b.Kod;
                var oge = new ToolStripMenuItem(b.Ad + (b.RenkKullanimi > 0 ? "  (renkli)" : "")) { Checked = m.Isik == kod };
                oge.Click += (s, e) =>
                {
                    m.Isik = kod;
                    if (LedEfektleri.Bul(kod).RenkKullanimi > 0)
                        OzelRenkSec(m.IsikRenk, c => m.IsikRenk = c);
                    Invalidate();
                };
                menu.Items.Add(oge);
            }
            if (!string.IsNullOrEmpty(m.Isik) && LedEfektleri.Bul(m.Isik).RenkKullanimi > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
                var renk = new ToolStripMenuItem("Rengi değiştir…");
                renk.Click += (s, e) => OzelRenkSec(m.IsikRenk, c => m.IsikRenk = c);
                menu.Items.Add(renk);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Bu moda geçince ışık bu olur; parlaklık ve hız genel ayardan") { Enabled = false });
            renkPenceresiAcik = true;
            menu.Closed += (s, e) =>
            {
                renkPenceresiAcik = false;
                BeginInvoke(new Action(menu.Dispose));
            };
            menu.Show(this, new Point((int)(r.X * O), (int)(r.Bottom * O)));
        }

        /// <summary>uygulama 2.4: sağ tekerlek görevi menüsü (Windows menüsü olarak açılır).</summary>
        void TekerMenusu(ModTanimi m, RectangleF r)
        {
            var menu = new ContextMenuStrip { ShowCheckMargin = true, ShowImageMargin = false };
            foreach (var t in KomutKatalogu.TekerGorevleri)
            {
                var kod = t.kod;
                var oge = new ToolStripMenuItem(t.ad + (t.sag != "" ? "   (sağa " + t.sag + " · sola " + t.sol + ")" : ""))
                {
                    Checked = m.Tekerlek == kod,
                };
                oge.Click += (s, e) =>
                {
                    m.Tekerlek = kod;
                    if (kod == "o" && string.IsNullOrEmpty(m.TekerSag)) KomutSecAc("sag");
                    Invalidate();
                };
                menu.Items.Add(oge);
            }
            if (m.Tekerlek == "o")
            {
                menu.Items.Add(new ToolStripSeparator());
                var sag = new ToolStripMenuItem("Sağa çevirince: " + (string.IsNullOrEmpty(m.TekerSag) ? "—" : KomutKatalogu.Ad(m.TekerSag)) + "…");
                sag.Click += (s, e) => KomutSecAc("sag");
                var sol = new ToolStripMenuItem("Sola çevirince: " + (string.IsNullOrEmpty(m.TekerSol) ? "—" : KomutKatalogu.Ad(m.TekerSol)) + "…");
                sol.Click += (s, e) => KomutSecAc("sol");
                menu.Items.Add(sag);
                menu.Items.Add(sol);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Basılı tutup çevirince her zaman mod değişir") { Enabled = false });
            renkPenceresiAcik = true;
            menu.Closed += (s, e) =>
            {
                renkPenceresiAcik = false;
                BeginInvoke(new Action(menu.Dispose));
            };
            menu.Show(this, new Point((int)(r.X * O), (int)(r.Bottom * O)));
        }

        void Tasi(int yon)
        {
            int j = duzenNo + yon;
            if (taslak == null || j < 0 || j >= taslak.Count) return;
            (taslak[duzenNo], taslak[j]) = (taslak[j], taslak[duzenNo]);
            duzenNo = j;
        }

        void OzelRenkSec(Color mevcut, Action<Color> sec)
        {
            renkPenceresiAcik = true;
            try
            {
                using var d = new ColorDialog { Color = mevcut, FullOpen = true, AnyColor = true };
                if (d.ShowDialog(this) == DialogResult.OK)
                    sec(Color.FromArgb(d.Color.R, d.Color.G, d.Color.B));
            }
            finally
            {
                renkPenceresiAcik = false;
                Activate();
                Invalidate();
            }
        }

        // ------------------------------------------------------------------ komut seçici
        /// <summary>Komut seçicide şu an seçili olan (hedefe göre).</summary>
        string HedefKodu()
        {
            var m = taslak[duzenNo];
            var t = m.Tuslar[duzenTus];
            string k = komutHedefi switch { "uzun" => t.UzunKod, "sag" => m.TekerSag, "sol" => m.TekerSol, _ => t.Kod };
            return string.IsNullOrEmpty(k) ? "NONE" : k;
        }

        void KomutSecAc(string hedef)
        {
            komutHedefi = hedef;
            kayitModu = false;
            kayitKod = "";
            komutKaydirma = 0;
            var kod = HedefKodu();
            var kat = KomutKatalogu.Kategoriler.FirstOrDefault(k => k.Skip(1).Contains(kod));
            if (kat != null) komutKategori = kat[0];
            else if (KomutKatalogu.UygulamaMi(kod)) komutKategori = KomutKatalogu.UygulamaKategorisi;
            else if (KomutKatalogu.MetinMi(kod)) komutKategori = KomutKatalogu.MetinKategorisi;
            else if (KomutKatalogu.DiziMi(kod)) komutKategori = KomutKatalogu.MakroKategorisi;
            kutuYaziliyor = true;
            tbMetin.MaxLength = KomutKatalogu.DiziMi(kod) ? KomutKatalogu.MakroUzunlugu : KomutKatalogu.MetinUzunlugu;
            tbMetin.Text = KomutKatalogu.MetinMi(kod) ? KomutKatalogu.MetinCoz(kod, out metinEnter).Replace("\n", "\r\n")
                         : KomutKatalogu.DiziMi(kod) ? KomutKatalogu.DiziMetni(kod) : "";
            if (!KomutKatalogu.MetinMi(kod)) metinEnter = false;
            tbAra.Text = "";
            kutuYaziliyor = false;
            gorunum = Gorunum.KomutSec;
            Boyutla();
        }

        void KomutSecdenCik()
        {
            kayitModu = false;
            gorunum = Gorunum.ModDuzen;
            Boyutla();
        }

        /// <summary>Tuşa komutu atar; ekrandaki adı boşsa ya da eski komutun adıysa yeni komutun adını alır.</summary>
        void KomutAta(string kod, string ad)
        {
            var mt = taslak[duzenNo];
            if (komutHedefi != "kisa")
            {
                var k = kod == "NONE" ? "" : kod;
                if (komutHedefi == "uzun") mt.Tuslar[duzenTus].UzunKod = k;
                else if (komutHedefi == "sag") mt.TekerSag = k;
                else mt.TekerSol = k;
                KomutSecdenCik();
                return;
            }
            var tus = mt.Tuslar[duzenTus];
            var eski = KomutKatalogu.Ad(tus.Kod);
            if (string.IsNullOrWhiteSpace(tus.Ad) || tus.Ad == eski || tus.Ad == ModTanimi.Temizle(eski, ModTanimi.TusAdiUzunlugu))
                tus.Ad = kod == "NONE" ? "" : ModTanimi.Temizle(ad, ModTanimi.TusAdiUzunlugu);
            tus.Kod = kod;
            KomutSecdenCik();
        }

        static string KisayolYazisi(string kod)
        {
            var ip = KomutKatalogu.Ipucu(kod);
            return ip != "" ? ip : KomutKatalogu.Ad(kod);
        }

        void KomutSecCiz(Graphics g, float w, float h)
        {
            if (taslak == null || duzenNo >= taslak.Count)
            {
                DuzendenCik(true);
                return;
            }
            var m = taslak[duzenNo];
            var mevcutKod = HedefKodu();
            IkonDugme(g, "geri", GlifGeri, new RectangleF(P - 4, P - 2, 26, 26), 11f, KomutSecdenCik);
            string baslik = komutHedefi switch
            {
                "uzun" => $"{duzenTus + 1}. tuş · basılı tutunca",
                "sag" => "Sağ tekerlek · sağa çevirince",
                "sol" => "Sağ tekerlek · sola çevirince",
                _ => $"{duzenTus + 1}. tuş için komut",
            };
            Yaz(g, baslik, 12f, FontStyle.Bold, T.Yazi, new RectangleF(P + 28, P - 1, w - 2 * P - 28, 24));

            bool metinKategorisi = MetinKutusuKategorisi(komutKategori);
            if (metinKategorisi)
                Yaz(g, komutKategori == KomutKatalogu.MakroKategorisi ? "Tuşa basınca satırlar sırayla yapılır" : "Tuşa basınca imlecin olduğu yere yazılır", 8.5f, FontStyle.Regular, T.Soluk, AraKutusu);
            else
                KutuCiz(g, AraKutusu, tbAra);

            // kendi kısayolun / boş
            float cw = (w - 2 * P - 8) / 2;
            var k1 = new RectangleF(P, 86, cw, 44);
            var k2 = new RectangleF(P + cw + 8, 86, cw, 44);
            Doldur(g, uzerindeki == "kendi" && !kayitModu ? Karistir(T.Kart, T.Uzerinde) : T.Kart, k1, 10);
            using (var kalem = new Pen(kayitModu ? T.Vurgu : T.Kenar, kayitModu ? 2f : 1f))
            using (var yol = Yuvarlak(kayitModu ? RectangleF.Inflate(k1, -1, -1) : k1, 10))
                g.DrawPath(kalem, yol);
            if (kayitModu)
            {
                Yaz(g, kayitKod == "" ? "Tuşlara bas…" : KisayolYazisi(kayitKod), 9f, FontStyle.Bold, T.Yazi, new RectangleF(k1.X + 10, k1.Y + 5, k1.Width - 16, 18));
                Yaz(g, kayitTamam ? "Atamak için tıkla" : "Örn. Ctrl + Shift + K", 7.5f, FontStyle.Regular, T.Soluk,
                    new RectangleF(k1.X + 10, k1.Y + 23, k1.Width - 16, 15));
            }
            else
            {
                Ikon(g, GlifKlavye, 9f, T.Yazi, new RectangleF(k1.X + 8, k1.Y + 5, 18, 18));
                Yaz(g, "Kendi kısayolun", 9f, FontStyle.Bold, T.Yazi, new RectangleF(k1.X + 28, k1.Y + 5, k1.Width - 34, 18));
                Yaz(g, "Tuş birleşimine bas", 7.5f, FontStyle.Regular, T.Soluk, new RectangleF(k1.X + 10, k1.Y + 23, k1.Width - 16, 15));
            }
            Ekle(k1, () =>
            {
                if (kayitModu && kayitTamam)
                {
                    KomutAta(kayitKod, KisayolYazisi(kayitKod));
                    return;
                }
                kayitModu = !kayitModu;
                kayitKod = "";
                kayitTamam = false;
                ActiveControl = null;          // tuşlar arama kutusuna değil bize gelsin
                Focus();
            }, "kendi");
            bool bos = mevcutKod == "NONE" || string.IsNullOrEmpty(mevcutKod);
            Doldur(g, uzerindeki == "bos" ? Karistir(T.Kart, T.Uzerinde) : T.Kart, k2, 10);
            using (var kalem = new Pen(bos ? T.Vurgu : T.Kenar, bos ? 2f : 1f))
            using (var yol = Yuvarlak(bos ? RectangleF.Inflate(k2, -1, -1) : k2, 10))
                g.DrawPath(kalem, yol);
            Yaz(g, "Boş", 9f, FontStyle.Bold, T.Yazi, new RectangleF(k2.X + 10, k2.Y + 5, k2.Width - 16, 18));
            Yaz(g, komutHedefi == "uzun" ? "Basılı tutmak bir şey yapmasın" : komutHedefi == "kisa" ? "Tuş bir şey yapmasın" : "Bu yönde bir şey yapmasın", 7.5f, FontStyle.Regular, T.Soluk, new RectangleF(k2.X + 10, k2.Y + 23, k2.Width - 16, 15));
            Ekle(k2, () => KomutAta("NONE", ""), "bos");

            // kategoriler (sığmayan alt satıra)
            string ara = tbAra.Text.Trim();
            var yerTutucu = komutKategori == KomutKatalogu.UygulamaKategorisi ? "Ara: uygulama adı ya da site adresi" : "Ara: kopyala, ses, ekran…";
            if (tbAra.PlaceholderText != yerTutucu) tbAra.PlaceholderText = yerTutucu;
            float x = P, y = 140;
            var yazi = YaziTipi("Segoe UI", 8f, FontStyle.Bold);
            var katAdlari = KomutKatalogu.Kategoriler.Select(k => k[0]).Append(KomutKatalogu.UygulamaKategorisi)
                            .Append(KomutKatalogu.PencereKategorisi).Append(KomutKatalogu.MetinKategorisi).Append(KomutKatalogu.MakroKategorisi);
            foreach (var katAdi in katAdlari)
            {
                var kat = new[] { katAdi };
                float kw = g.MeasureString(kat[0], yazi).Width + 16;
                if (x + kw > w - P + 0.5f)
                {
                    x = P;
                    y += 27;
                }
                var r = new RectangleF(x, y, kw, 22);
                bool secili = (ara == "" || komutKategori == KomutKatalogu.UygulamaKategorisi || metinKategorisi) && kat[0] == komutKategori;
                Doldur(g, secili ? T.SekAcik : (uzerindeki == "kat" + kat[0] ? Karistir(T.Sek, T.Uzerinde) : T.Sek), r, 11);
                Yaz(g, kat[0], 8f, FontStyle.Bold, secili ? T.Yazi : T.Soluk, r, StringAlignment.Center);
                var ad = kat[0];
                Ekle(r, () =>
                {
                    komutKategori = ad;
                    komutKaydirma = 0;
                    kutuYaziliyor = true;
                    tbAra.Text = "";
                    kutuYaziliyor = false;
                    KutulariYerlestir();
                }, "kat" + kat[0]);
                x += kw + 5;
            }

            if (metinKategorisi)
            {
                if (komutKategori == KomutKatalogu.MakroKategorisi) MakroCiz(g, w, h, y + 32, m);
                else MetinYazCiz(g, w, h, y + 32, m);
                return;
            }

            // komutlar
            komutAlani = new RectangleF(P, y + 30, w - 2 * P, h - 46 - (y + 30));
            var satirlar = new List<(string kimlik, string ad, string ipucu, bool secili, Action tik)>();
            void KodEkle(string kod) =>
                satirlar.Add((kod, KomutKatalogu.Ad(kod), KomutKatalogu.Ipucu(kod), mevcutKod == kod, new Action(() => KomutAta(kod, KomutKatalogu.Ad(kod)))));
            bool uygulamaKat = komutKategori == KomutKatalogu.UygulamaKategorisi;
            if (komutKategori == KomutKatalogu.PencereKategorisi)       // 3.2 (CD)
            {
                foreach (var (kodu, adi) in PencereIslemleri.Yerlesimler)
                {
                    var tam = "PEN:" + kodu;
                    satirlar.Add((tam, adi, "öndeki pencereyi taşı", mevcutKod == tam,
                                  new Action(() => KomutAta(tam, adi))));
                }
            }
            else if (uygulamaKat)
            {
                // arama kutusu burada uygulama adlarında arar; adres yazılırsa site olarak da eklenir
                var site = KomutKatalogu.SiteAdresi(ara);
                if (site != null)
                {
                    var sk = "APP:" + site;
                    satirlar.Add(("@site", "Siteyi aç: " + KomutKatalogu.UygulamaAdi(sk), "site", mevcutKod == sk, new Action(() => KomutAta(sk, KomutKatalogu.UygulamaAdi(sk)))));
                }
                if (ara == "")
                {
                    if (KomutKatalogu.UygulamaMi(mevcutKod))
                    {
                        satirlar.Add(("@simdiki", KomutKatalogu.Ad(mevcutKod), KomutKatalogu.UygulamaTuru(mevcutKod), true, new Action(() => { })));
                        // 3.2 (BT): aynı program için "açıksa öne getir" seçeneği
                        bool one = KomutKatalogu.OneGetirMi(mevcutKod);
                        string hedefKod = (one ? "APP:" : "APPF:") + KomutKatalogu.UygulamaHedefi(mevcutKod);
                        satirlar.Add(("@onegetir", one ? "✓ Açıksa öne getir" : "Açıksa öne getir",
                                      one ? "şu an açıksa pencereyi öne alır" : "ikinci kopya açmaz",
                                      false, new Action(() => KomutAta(hedefKod, KomutKatalogu.Ad(hedefKod)))));
                    }
                    satirlar.Add(("@dosya", "Program ya da dosya seç…", "exe, kısayol", false, new Action(DosyaSec)));
                    satirlar.Add(("@klasor", "Klasör seç…", "", false, new Action(KlasorSec)));
                    satirlar.Add(("@siteipucu", "Web sitesi: adresi yukarıya yaz", "örn. youtube.com", false, new Action(() => { tbAra.Focus(); })));
                }
                if (YukluUygulamalar.Liste == null)
                {
                    YukluUygulamalar.Oku();
                    satirlar.Add(("@okunuyor", "Yüklü uygulamalar okunuyor…", "", false, new Action(() => { })));
                }
                else
                {
                    var q = KomutKatalogu.Norm(ara);
                    foreach (var u in YukluUygulamalar.Liste)
                    {
                        if (q != "" && !KomutKatalogu.Norm(u.Ad).Contains(q)) continue;
                        var uk = "APP:" + u.Hedef;
                        var uad = u.Ad;
                        satirlar.Add(("u:" + u.Hedef, u.Ad, u.Store ? "Store" : "", mevcutKod == uk, new Action(() => KomutAta(uk, uad))));
                    }
                }
            }
            else if (ara != "")
                foreach (var kod in KomutKatalogu.Ara(ara)) KodEkle(kod);
            else
                foreach (var kod in KomutKatalogu.Kategoriler.First(k => k[0] == komutKategori).Skip(1)) KodEkle(kod);

            const float sh = 30;
            komutIcerik = satirlar.Count * sh;
            komutKaydirma = Math.Clamp(komutKaydirma, 0, Math.Max(0, komutIcerik - komutAlani.Height));
            bool kaydirma = komutIcerik > komutAlani.Height;
            if (satirlar.Count == 0)
                Yaz(g, "Bulunamadı. \"Kendi kısayolun\" ile istediğin tuş birleşimini kaydedebilirsin.", 8.5f, FontStyle.Regular, T.Soluk,
                    new RectangleF(komutAlani.X + 6, komutAlani.Y + 6, komutAlani.Width - 12, 48), StringAlignment.Near, true, StringAlignment.Near);
            var onceki = g.Save();
            g.SetClip(komutAlani);
            for (int i = 0; i < satirlar.Count; i++)
            {
                var st = satirlar[i];
                var r = new RectangleF(komutAlani.X, komutAlani.Y + i * sh - komutKaydirma, komutAlani.Width - (kaydirma ? 8 : 0), sh - 2);
                if (r.Bottom < komutAlani.Y || r.Y > komutAlani.Bottom) continue;
                if (st.secili) Doldur(g, T.SekAcik, r, 8);
                else if (uzerindeki == "k:" + st.kimlik) Doldur(g, T.Uzerinde, r, 8);
                float ix = r.X + 8;
                if (st.secili)
                {
                    Ikon(g, GlifTamam, 8f, T.Yazi, new RectangleF(ix, r.Y, 14, r.Height));
                    ix += 18;
                }
                float iw = st.ipucu == "" ? 0 : Math.Min(120, g.MeasureString(st.ipucu, YaziTipi("Segoe UI", 8f, FontStyle.Regular)).Width + 6);
                Yaz(g, st.ad, 9f, FontStyle.Bold, T.Yazi, new RectangleF(ix, r.Y, r.Right - ix - iw - 12, r.Height));
                if (iw > 0)
                    Yaz(g, st.ipucu, 8f, FontStyle.Regular, T.Soluk, new RectangleF(r.Right - 8 - iw, r.Y, iw, r.Height), StringAlignment.Far);
                var gorunen = RectangleF.Intersect(r, komutAlani);
                if (gorunen.Height > 6) Ekle(gorunen, st.tik, "k:" + st.kimlik);
            }
            g.Restore(onceki);
            if (kaydirma) KaydirmaCubugu(g, komutAlani, komutKaydirma, komutIcerik);

            using (var kalem = new Pen(T.Kenar, 1f))
                g.DrawLine(kalem, P, h - 40, w - P, h - 40);
            Yaz(g, uygulamaKat ? "Uygulama açıkken çalışır" : "Seçince tuşa atanır", 8f, FontStyle.Regular, T.Soluk, new RectangleF(P, h - 32, 150, 18));
            Yaz(g, $"{m.Ad} · {duzenTus + 1}. tuş", 8f, FontStyle.Regular, T.Soluk, new RectangleF(w - P - 160, h - 32, 160, 18), StringAlignment.Far);
        }

        /// <summary>uygulama 2.3: "Metin yaz" kategorisi: çok satırlı kutu, sonunda Enter, Tuşa ata.</summary>
        void MetinYazCiz(Graphics g, float w, float h, float y, ModTanimi m)
        {
            var alan = new RectangleF(P, y, w - 2 * P, 104);
            if (alan != metinAlani)
            {
                metinAlani = alan;
                KutulariYerlestir();
            }
            KutuCiz(g, alan, tbMetin);
            y = alan.Bottom + 6;
            Kutucuk2(g, P, y, "Sonunda Enter'a bas", metinEnter, () => metinEnter = !metinEnter, "metenter");
            Yaz(g, $"{tbMetin.TextLength}/{KomutKatalogu.MetinUzunlugu}", 7.5f, FontStyle.Regular, T.Soluk,
                new RectangleF(w - P - 60, y, 60, 21), StringAlignment.Far);
            y += 30;
            Yaz(g, "Türkçe harfler ve satır sonları olduğu gibi yazılır. Örn. e-posta adresin, imzan, sık yazdığın bir cevap.",
                7.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, y, w - 2 * P, 32), StringAlignment.Near, true, StringAlignment.Near);
            y += 38;
            string metin = tbMetin.Text;
            KucukDugme(g, new RectangleF(w - P - 110, y, 110, 30), "Tuşa ata", "metinata", () =>
            {
                var kod = KomutKatalogu.MetinKodu(metin, metinEnter);
                var ilk = metin.Replace("\r", "").Split('\n')[0].Trim();
                KomutAta(kod, ilk == "" ? "Metin" : ilk);
            }, 1, metin.Trim().Length > 0);

            using (var kalem = new Pen(T.Kenar, 1f))
                g.DrawLine(kalem, P, h - 40, w - P, h - 40);
            Yaz(g, "Uygulama açıkken çalışır", 8f, FontStyle.Regular, T.Soluk, new RectangleF(P, h - 32, 150, 18));
            Yaz(g, $"{m.Ad} · {duzenTus + 1}. tuş", 8f, FontStyle.Regular, T.Soluk, new RectangleF(w - P - 160, h - 32, 160, 18), StringAlignment.Far);
        }

        /// <summary>uygulama 2.9 / kart 2.7: "Makro" kategorisi: her satır bir adım; tuşa basınca sırayla yapılır.</summary>
        void MakroCiz(Graphics g, float w, float h, float y, ModTanimi m)
        {
            var alan = new RectangleF(P, y, w - 2 * P, 104);
            if (alan != metinAlani)
            {
                metinAlani = alan;
                KutulariYerlestir();
            }
            KutuCiz(g, alan, tbMetin);
            y = alan.Bottom + 6;
            int adim = KomutKatalogu.DiziAdimlari(KomutKatalogu.DiziKodu(tbMetin.Text)).Count;
            Yaz(g, $"{adim} adım · {tbMetin.TextLength}/{KomutKatalogu.MakroUzunlugu}", 7.5f, FontStyle.Regular, T.Soluk,
                new RectangleF(P, y, 160, 16));
            y += 18;
            Yaz(g, "Her satır bir adım. Kısayol: CTRL+C, ALT+TAB, ENTER…  ·  BEKLE 500 (ms)  ·  YAZ merhaba  ·  YAZ+ merhaba (sonunda Enter)  ·  AC notepad",
                7.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, y, w - 2 * P, 44), StringAlignment.Near, true, StringAlignment.Near);
            y += 48;
            bool destek = KartEnAzPanel(2, 7);
            if (!destek)
                Yaz(g, "Makro için kart yazılımı 2.7 gerekli", 8f, FontStyle.Regular, R(248, 113, 113), new RectangleF(P, y + 6, w - 2 * P - 120, 18));
            string metin = tbMetin.Text;
            KucukDugme(g, new RectangleF(w - P - 110, y, 110, 30), "Tuşa ata", "makroata", () =>
            {
                var kod = KomutKatalogu.DiziKodu(metin);
                if (kod == "NONE") return;
                KomutAta(kod, "Makro");
            }, 1, destek && KomutKatalogu.DiziKodu(metin) != "NONE");

            using (var kalem = new Pen(T.Kenar, 1f))
                g.DrawLine(kalem, P, h - 40, w - P, h - 40);
            Yaz(g, "Adımlar arası bekleme için BEKLE kullan", 8f, FontStyle.Regular, T.Soluk, new RectangleF(P, h - 32, 220, 18));
            Yaz(g, $"{m.Ad} · {duzenTus + 1}. tuş", 8f, FontStyle.Regular, T.Soluk, new RectangleF(w - P - 160, h - 32, 160, 18), StringAlignment.Far);
        }

        // ------------------------------------------------------------------ uygulama / klasör seç
        void DosyaSec()
        {
            renkPenceresiAcik = true;          // pencere açıkken panel kapanmasın
            try
            {
                using var d = new OpenFileDialog
                {
                    Title = "Tuşa basınca açılacak program ya da dosya",
                    Filter = "Programlar ve kısayollar|*.exe;*.lnk;*.url;*.bat;*.cmd|Tüm dosyalar|*.*",
                    DereferenceLinks = false,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                };
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    var kod = "APP:" + d.FileName;
                    KomutAta(kod, KomutKatalogu.UygulamaAdi(kod));
                }
            }
            finally
            {
                renkPenceresiAcik = false;
                Activate();
                Invalidate();
            }
        }

        void KlasorSec()
        {
            renkPenceresiAcik = true;
            try
            {
                using var d = new FolderBrowserDialog { Description = "Tuşa basınca açılacak klasör", UseDescriptionForTitle = true };
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    var kod = "APP:" + d.SelectedPath;
                    KomutAta(kod, KomutKatalogu.UygulamaAdi(kod));
                }
            }
            finally
            {
                renkPenceresiAcik = false;
                Activate();
                Invalidate();
            }
        }

        // ------------------------------------------------------------------ kendi kısayolun: tuşları yakala
        [DllImport("user32.dll")]
        static extern short GetKeyState(int tus);

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (kisayolKaydi && gorunum == Gorunum.Ayarlar)
            {
                bool winBasili = (GetKeyState(0x5B) & 0x8000) != 0 || (GetKeyState(0x5C) & 0x8000) != 0;
                if (KisayolTusu(keyData, winBasili)) return true;
            }
            if (gorunum == Gorunum.KomutSec && kayitModu)
            {
                var tus = keyData & Keys.KeyCode;
                bool win = (GetKeyState(0x5B) & 0x8000) != 0 || (GetKeyState(0x5C) & 0x8000) != 0 || tus == Keys.LWin || tus == Keys.RWin;
                kayitKod = KomutKatalogu.Birlesim(tus, keyData & Keys.Modifiers, win, out kayitTamam);
                Invalidate();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>Esc: düzenleyicide bir geri gider. true: işlendi.</summary>
        bool ModEsc()
        {
            if (gorunum == Gorunum.KomutSec)
            {
                KomutSecdenCik();
                return true;
            }
            if (gorunum == Gorunum.ModGorunum)
            {
                gorunum = Gorunum.ModDuzen;
                Boyutla();
                KutulariYerlestir();
                return true;
            }
            if (gorunum == Gorunum.SimgeCiz)
            {
                SimgeCizKapat(false);
                return true;
            }
            if (gorunum == Gorunum.SimgeSec)
            {
                SimgeSecdenCik();
                return true;
            }
            if (gorunum == Gorunum.ModDuzen)
            {
                if (silOnay || cikisOnay) { silOnay = cikisOnay = false; Invalidate(); }
                else DuzendenCik();
                return true;
            }
            return false;
        }

        /// <summary>Fare tekerleği: mod listesi ya da komut listesi kayar. true: işlendi.</summary>
        bool ModTekerlek(PointF q, int delta)
        {
            if (gorunum == Gorunum.Modlar && modListeAlani.Contains(q) && modIcerik > modListeAlani.Height)
            {
                modKaydirma = Math.Clamp(modKaydirma - Math.Sign(delta) * 40, 0, modIcerik - modListeAlani.Height);
                Invalidate();
                return true;
            }
            if (gorunum == Gorunum.KomutSec && komutAlani.Contains(q) && komutIcerik > komutAlani.Height)
            {
                komutKaydirma = Math.Clamp(komutKaydirma - Math.Sign(delta) * 60, 0, komutIcerik - komutAlani.Height);
                Invalidate();
                return true;
            }
            return false;
        }
    }
}
