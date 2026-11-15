using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MakroKeyboardDisplay
{
    /// <summary>Panelde gösterilen her şey (TepsiUygulamasi yarım saniyede bir doldurur).</summary>
    public sealed class PanelDurumu
    {
        public bool Bagli;
        public string Port = "";
        public string KartSurum = "";
        public MedyaBilgisi Medya;
        public int Ses = -1;              // 0-100, -1: okunamadı
        public bool Sessiz;
        public string[] ModAdlari = new string[0];
        public Color[] ModRenkleri = new Color[0];
        public string[][] ModTuslari = new string[0][];
        public int AktifMod = -1;
        public string Sayfa = "";         // klavye ekranı: "medya" / "tuslar" (kart 1.6+)
        public LedAyari Led;              // klavyedeki ışık ayarı (kart 1.9+), okunamadıysa null
        public bool LedDestek;            // kart 1.9+
        public bool LedTusDestek;         // kart 1.10+: tuşlara ayrı efekt
        public Color[] KapakRenkleri;     // çalan şarkının kapağından (Işık sekmesi önizlemesi)
        public List<ModTanimi> ModTanimlari;   // kart 1.11+: modların tamamı (komutlarıyla); okunamadıysa null
        public bool ModDuzenDestek;            // kart 1.11+
        public string ModKayit = "";           // "", "gidiyor", "tamam", "hata:..."
        public int BosBellek = -1, EnAzBellek = -1;   // kart 1.12+: klavyenin boş belleği (bayt)
        public bool MikKapali;                 // kart 1.14+: mikrofon kapalı (Ekran sekmesindeki önizleme için)
        public int[] EkranAyar;                // kart 1.13+: parlaklık (10-100), bayraklar, ses ışığı (0-3); okunamadıysa null
        public bool EkranDestek;               // kart 1.13+
    }

    /// <summary>
    /// Saatin yanındaki simgeye tıklayınca açılan panel (Windows'un hızlı ayarları gibi).
    /// İki tür: "Sadece şarkı" (küçük) ve "Sekmeli" (Şarkı · Modlar · Ekran · Işık). Her sekmede boyut aynı.
    /// Üç tema: Açık, Koyu, Renkli (arka plan çalan şarkının kapağından bulanık).
    /// Her şey kodla çiziliyor; tasarımcı dosyası yok.
    /// </summary>
    [System.ComponentModel.DesignerCategory("Code")]
    public sealed partial class KlavyePaneli : Form
    {
        // ------------------------------------------------------------------ ölçüler (96 dpi birimi)
        const int G = 340;                // genişlik
        const int YSekmeli = 500;         // sekmeli panel ve ayarlar
        const int YKucuk = 214;           // sadece şarkı
        const int P = 14;                 // kenar boşluğu

        enum Gorunum { Sarki, Modlar, Ekran, Isik, Ayarlar, ModDuzen, KomutSec, TusTesti, SimgeSec, SimgeCiz, ModGorunum }

        // ------------------------------------------------------------------ tema
        sealed class Tema
        {
            public string Ad;
            public Color Arka, Yazi, Soluk, Kart, Kenar, Sek, SekAcik, Vurgu, Iz, Uzerinde;
            public bool Renkli;
        }

        static Color R(int r, int g, int b, int a = 255) => Color.FromArgb(a, r, g, b);

        static readonly Dictionary<string, Tema> Temalar = new()
        {
            ["acik"] = new Tema
            {
                Ad = "Açık", Arka = R(246, 247, 251), Yazi = R(15, 23, 42), Soluk = R(100, 116, 139), Kart = R(255, 255, 255),
                Kenar = R(226, 232, 240), Sek = R(233, 237, 243), SekAcik = R(255, 255, 255), Vurgu = R(124, 58, 237),
                Iz = R(226, 232, 240), Uzerinde = R(15, 23, 42, 14),
            },
            ["koyu"] = new Tema
            {
                Ad = "Koyu", Arka = R(22, 24, 30), Yazi = R(241, 245, 249), Soluk = R(148, 163, 184), Kart = R(32, 35, 43),
                Kenar = R(44, 48, 57), Sek = R(15, 17, 21), SekAcik = R(44, 48, 57), Vurgu = R(167, 139, 250),
                Iz = R(44, 48, 57), Uzerinde = R(255, 255, 255, 16),
            },
            ["renkli"] = new Tema
            {
                Ad = "Renkli", Renkli = true, Arka = R(40, 16, 48), Yazi = R(255, 255, 255), Soluk = R(255, 255, 255, 184),
                Kart = R(255, 255, 255, 26), Kenar = R(255, 255, 255, 40), Sek = R(0, 0, 0, 64), SekAcik = R(255, 255, 255, 56),
                Vurgu = R(255, 255, 255), Iz = R(255, 255, 255, 64), Uzerinde = R(255, 255, 255, 22),
            },
        };

        static readonly string[] TemaSirasi = { "acik", "koyu", "renkli" };

        // ------------------------------------------------------------------ durum
        PanelDurumu durum = new PanelDurumu();
        Image kapak;
        Bitmap bulanik;                    // Renkli tema arka planı (kapaktan)
        Gorunum gorunum = Gorunum.Sarki;
        Gorunum ayarOncesi = Gorunum.Sarki;
        string temaAdi = Ayarlar.Oku("Tema", "renkli");
        string panelTuru = Ayarlar.Oku("PanelTuru", "sekmeli");
        DateTime gizlenme = DateTime.MinValue;

        Tema T => Temalar.TryGetValue(temaAdi, out var t) ? t : Temalar["renkli"];
        bool Kucuk => panelTuru == "kucuk";
        public bool DisariTiklayincaKapan => Ayarlar.Oku("DisariKapan", "1") == "1";
        public bool BaglantiBildirimi => Ayarlar.Oku("BaglantiBildirimi", "1") == "1";

        // ------------------------------------------------------------------ dışarıya istekler
        public event Action<string> MedyaKomutu;        // "onceki" / "oynat" / "sonraki"
        public event Action<double> KonumIstendi;       // 0-1 arası
        public event Action<int> SesIstendi;            // 0-100
        public event Action SessizIstendi;
        public event Action<int> ModIstendi;
        public event Action<string> SayfaIstendi;       // "medya" / "tuslar"
        public event Action YenidenAraIstendi;
        public event Action HataKayitlariIstendi;
        public event Action CikisIstendi;
        public event Action BellekSifirlaIstendi;
        public event Action YazilimGuncelleIstendi;   // uygulama 3.0: kart yazılımını uygulamadan yükle
        public event Action<int[]> EkranAyarIstendi;    // Ayarlar > Klavye: parlaklık, bayraklar, ses ışığı
        /// <summary>Önizleme için çalan sesin seviyesi (sol, sağ 0-1); ölçülmüyorsa false.</summary>
        public Func<(bool olculuyor, double sol, double sag)> SesOlcumu = () => (false, 0, 0);
        public Func<bool> BaslangictaMi = () => false;
        public Action BaslangicDegistir = () => { };

        // ------------------------------------------------------------------ tıklanabilir alanlar
        sealed class Hedef
        {
            public RectangleF Alan;
            public Action Tik;
            public string Kimlik;
        }

        readonly List<Hedef> hedefler = new();
        string uzerindeki;                 // fare hangi hedefin üstünde
        RectangleF sesCubugu, konumCubugu, parCubugu, hizCubugu;
        string surukle;                    // sürüklenen çubuk: "ses", "par", "hiz"
        bool sesSurukleniyor => surukle == "ses";
        int? surukSes;                     // sürüklerken gösterilen değer

        // Işık sekmesi
        LedAyari led;                      // panelde gösterilen (tıklayınca hemen değişir)
        DateTime sonLedDegisimi = DateTime.MinValue;
        int[] ekranAyar;                   // Ayarlar > Klavye (panelde gösterilen; tıklayınca hemen değişir)
        DateTime sonEkranDegisimi = DateTime.MinValue;
        RectangleF ekParCubugu, uzunCubugu;
        string ayarSekmesi = "genel";      // "genel" / "klavye" / "yedek"
        string ledKategori;                // "muzik" / "efekt"; null: seçili efektinkine göre
        string isikBolge = "zemin";        // Işık sekmesinde düzenlenen bölge: "zemin" / "tuslar"
        int akisSecili;
        float cipKaydirma, cipIcerik;
        RectangleF cipAlani, haritaAlani;
        bool renkPenceresiAcik;
        readonly System.Windows.Forms.Timer animasyon = new() { Interval = 40 };
        readonly System.Diagnostics.Stopwatch saat = System.Diagnostics.Stopwatch.StartNew();
        double sonKare;

        static readonly Color[] HazirRenkler =
        {
            Color.FromArgb(255, 255, 255), Color.FromArgb(255, 45, 85), Color.FromArgb(255, 138, 0), Color.FromArgb(255, 214, 10),
            Color.FromArgb(48, 209, 88), Color.FromArgb(34, 211, 238), Color.FromArgb(59, 130, 246), Color.FromArgb(168, 85, 247),
        };

        float O => DeviceDpi / 96f;

        static readonly string IkonYaziTipi = YaziTipiVarMi("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

        public KlavyePaneli(Icon simge)
        {
            Text = "Makro Klavye";
            Icon = simge;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            _ = Handle;            // tutamak şimdi oluşsun: DeviceDpi doğru okunsun
            AllowDrop = true;      // 3.1 (AE): dosyayı tuş karosunun üstüne bırakınca o tuşa atanır
            DragEnter += SurukleGirdi;
            DragOver += SurukleGirdi;
            DragLeave += (s2, e2) => BirakmaHedefi(null);
            DragDrop += SurukleBirakildi;
            ModKutulariniKur();
            animasyon.Tick += (s, e) =>
            {
                // yalnız LED haritası (Işık sekmesi) ya da ses çubukları (Ekran sekmesi) yeniden çizilir
                if (Visible && gorunum == Gorunum.Isik && haritaAlani.Width > 0)
                    Invalidate(Rectangle.Inflate(Rectangle.Round(new RectangleF(haritaAlani.X * O, haritaAlani.Y * O,
                                                                               haritaAlani.Width * O, haritaAlani.Height * O)), 2, 2));
                else if (Visible && gorunum == Gorunum.Ekran && esitleyiciAlani.Width > 0)
                    Invalidate(Rectangle.Inflate(Rectangle.Round(new RectangleF(esitleyiciAlani.X * O, esitleyiciAlani.Y * O,
                                                                               esitleyiciAlani.Width * O, esitleyiciAlani.Height * O)), 2, 2));
            };
            Boyutla();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;      // CS_DROPSHADOW: gölge
                cp.ExStyle |= 0x80;            // WS_EX_TOOLWINDOW: Alt+Tab'da görünmesin
                return cp;
            }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Windows 11: köşeleri sistem yuvarlar (kenarları pürüzsüz). Windows 10'da bölge ile kesilir.
            int yuvarlak = 2;
            try { DwmSetWindowAttribute(Handle, 33, ref yuvarlak, sizeof(int)); } catch { }
        }

        static bool Windows11 => Environment.OSVersion.Version.Build >= 22000;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!Windows11 && Width > 0 && Height > 0)
            {
                using var yol = Yuvarlak(new RectangleF(0, 0, Width, Height), 12 * O);
                Region = new Region(yol);
            }
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            Boyutla();
        }

        void Boyutla()
        {
            int y = gorunum >= Gorunum.Ayarlar || !Kucuk ? YSekmeli : YKucuk;
            var yeni = new Size((int)(G * O), (int)(y * O));
            if (Size != yeni)
            {
                int alt = Bottom;
                Size = yeni;
                if (Visible) Top = alt - Height;      // alttan sabit kalsın (yukarı doğru büyür)
            }
            KutulariYerlestir();
            Invalidate();
        }

        // ------------------------------------------------------------------ gösterme / gizleme
        public void AcKapa()
        {
            if (Visible)
            {
                Gizle();
                return;
            }
            // panel dışarı tıklanınca kapandıysa ve o tıklama simgeye yapıldıysa yeniden açma
            if ((DateTime.UtcNow - gizlenme).TotalMilliseconds < 300)
                return;
            Goster();
        }

        public void Goster()
        {
            if (gorunum == Gorunum.Ayarlar)
                gorunum = ayarOncesi;
            if (gorunum == Gorunum.TusTesti)
                gorunum = testOncesi;
            if (Kucuk) gorunum = Gorunum.Sarki;
            Boyutla();
            var alan = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(alan.Right - Width - (int)(12 * O), alan.Bottom - Height - (int)(12 * O));
            Show();
            Activate();
            animasyon.Start();
        }

        void Gizle()
        {
            TusTestindenCik();                 // panel kapanınca tuşlar yine komut çalıştırsın
            gizlenme = DateTime.UtcNow;
            animasyon.Stop();
            Hide();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (DisariTiklayincaKapan && Visible && !renkPenceresiAcik)
                Gizle();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                if (ModEsc()) { }
                else if (gorunum == Gorunum.TusTesti) TusTestindenCik();
                else if (gorunum == Gorunum.Ayarlar) AyarlardanCik();
                else Gizle();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Gizle();
                return;
            }
            base.OnFormClosing(e);
        }

        // ------------------------------------------------------------------ veri
        public void Guncelle(PanelDurumu d)
        {
            durum = d ?? new PanelDurumu();
            // klavyedeki ayar, panelde yeni bir değişiklik yapılmadıysa alınır (gönderilen ayar geri dönene kadar beklenir)
            if (durum.Led != null && surukle == null && (led == null || (DateTime.UtcNow - sonLedDegisimi).TotalSeconds > 2))
                led = durum.Led.Kopya();
            if (durum.EkranAyar != null && surukle == null && (ekranAyar == null || (DateTime.UtcNow - sonEkranDegisimi).TotalSeconds > 2))
                ekranAyar = (int[])durum.EkranAyar.Clone();
            if (Visible && surukle == null)
                Invalidate();
        }

        public void KapakAyarla(Image resim)
        {
            var eski = kapak;
            kapak = resim == null ? null : new Bitmap(resim);
            eski?.Dispose();
            bulanik?.Dispose();
            bulanik = kapak == null ? null : Bulaniklastir(kapak);
            if (Visible) Invalidate();
        }

        /// <summary>Kapağı 12x12'ye küçültüp büyüterek yumuşak, bulanık bir arka plan yapar.</summary>
        static Bitmap Bulaniklastir(Image kaynak)
        {
            using var kucuk = new Bitmap(12, 12);
            using (var g = Graphics.FromImage(kucuk))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(kaynak, 0, 0, 12, 12);
            }
            var buyuk = new Bitmap(170, 250);
            using (var g = Graphics.FromImage(buyuk))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(kucuk, new Rectangle(-20, -20, 210, 290));
            }
            return buyuk;
        }

        // ------------------------------------------------------------------ çizim
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(O, O);
            hedefler.Clear();

            float w = G, h = Height / O;
            Arkaplan(g, w, h);

            if (gorunum == Gorunum.Ayarlar) AyarlarCiz(g, w, h);
            else if (gorunum == Gorunum.ModDuzen) ModDuzenCiz(g, w, h);
            else if (gorunum == Gorunum.KomutSec) KomutSecCiz(g, w, h);
            else if (gorunum == Gorunum.SimgeSec) SimgeSecCiz(g, w, h);
            else if (gorunum == Gorunum.SimgeCiz) SimgeCizCiz(g, w, h);
            else if (gorunum == Gorunum.ModGorunum) ModGorunumCiz(g, w, h);
            else if (gorunum == Gorunum.TusTesti) TusTestiCiz(g, w, h);
            else if (Kucuk) KucukCiz(g, w);
            else SekmeliCiz(g, w, h);

            // ince kenar
            using var kalem = new Pen(T.Kenar, 1f);
            using var yol = Yuvarlak(new RectangleF(0.5f, 0.5f, w - 1, h - 1), 12);
            g.DrawPath(kalem, yol);
        }

        void Arkaplan(Graphics g, float w, float h)
        {
            if (!T.Renkli)
            {
                g.Clear(T.Arka);
                return;
            }
            if (bulanik != null)
                g.DrawImage(bulanik, new RectangleF(0, 0, w, h));
            else
            {
                using var f = new LinearGradientBrush(new RectangleF(0, 0, w, h), R(59, 31, 79), R(122, 32, 72), 60f);
                g.FillRectangle(f, 0, 0, w, h);
            }
            using var karart = new SolidBrush(R(8, 6, 20, 118));
            g.FillRectangle(karart, 0, 0, w, h);
        }

        // ---- üst satır: ad, bağlantı, tema seçici, ayarlar
        void UstSatir(Graphics g, float w, bool temaSecici)
        {
            Yaz(g, "Makro Klavye", 10.5f, FontStyle.Bold, T.Yazi, new RectangleF(P, P, 140, 22));
            var bagli = durum.Bagli;
            using (var f = new SolidBrush(bagli ? R(34, 197, 94) : R(148, 163, 184)))
                g.FillEllipse(f, P + 112, P + 7, 7, 7);
            Yaz(g, bagli ? "bağlı" : "aranıyor…", 8f, FontStyle.Regular, bagli ? R(34, 197, 94) : T.Soluk,
                new RectangleF(P + 122, P + 2, 80, 18));

            float x = w - P - 24;
            IkonDugme(g, "ayar", "", new RectangleF(x, P - 1, 24, 24), 11f, () => AyarlaraGir());
            if (temaSecici)
            {
                var alan = new RectangleF(x - 6 - 3 * 25 - 4, P - 1, 3 * 25 + 4, 24);
                Doldur(g, T.Sek, alan, 8);
                for (int i = 0; i < 3; i++)
                {
                    var ad = TemaSirasi[i];
                    var r = new RectangleF(alan.X + 2 + i * 25, alan.Y + 2, 25, 20);
                    if (ad == temaAdi) Doldur(g, T.SekAcik, r, 6);
                    else if (uzerindeki == "tema" + ad) Doldur(g, T.Uzerinde, r, 6);
                    if (ad == "renkli")
                    {
                        using var f = new LinearGradientBrush(new RectangleF(r.X + 6, r.Y + 4, 12, 12), R(249, 115, 22), R(124, 58, 237), 45f);
                        g.FillEllipse(f, r.X + 6.5f, r.Y + 4, 12, 12);
                    }
                    else
                        Ikon(g, ad == "acik" ? "" : "", 9f, T.Yazi, r);
                    var tema = ad;
                    Ekle(r, () => TemaSec(tema), "tema" + ad);
                }
            }
        }

        // ---- sadece şarkı
        void KucukCiz(Graphics g, float w)
        {
            UstSatir(g, w, false);
            var m = durum.Medya;
            var kap = new RectangleF(P, 50, 76, 76);
            KapakCiz(g, kap, 12);
            float x = P + 76 + 14, sag = w - P;
            Yaz(g, m?.Baslik ?? "Çalan bir şey yok", 12f, FontStyle.Bold, T.Yazi, new RectangleF(x, 56, sag - x, 24));
            Yaz(g, m == null ? "Bir oynatıcıda şarkı başlat" : AltYazi(m), 8.5f, FontStyle.Regular, T.Soluk, new RectangleF(x, 80, sag - x, 18));
            KonumCubugu(g, new RectangleF(x, 106, sag - x, 4), false);
            OynatmaTuslari(g, w / 2, 150, 34);
            SesSatiri(g, new RectangleF(P, 178, w - 2 * P, 22));
        }

        // ---- sekmeli
        void SekmeliCiz(Graphics g, float w, float h)
        {
            UstSatir(g, w, true);
            var sekAlan = new RectangleF(P, 48, w - 2 * P, 34);
            Doldur(g, T.Sek, sekAlan, 11);
            string[] adlar = { "Şarkı", "Modlar", "Ekran", "Işık" };
            float sw = (sekAlan.Width - 6) / adlar.Length;
            for (int i = 0; i < adlar.Length; i++)
            {
                var r = new RectangleF(sekAlan.X + 3 + i * sw, sekAlan.Y + 3, sw, sekAlan.Height - 6);
                bool secili = (int)gorunum == i;
                if (secili) Doldur(g, T.SekAcik, r, 9);
                else if (uzerindeki == "sek" + i) Doldur(g, T.Uzerinde, r, 9);
                Yaz(g, adlar[i], 9f, FontStyle.Bold, secili ? T.Yazi : T.Soluk, r, StringAlignment.Center);
                var hedef = (Gorunum)i;
                Ekle(r, () => { gorunum = hedef; Invalidate(); }, "sek" + i);
            }

            var icerik = new RectangleF(P, 94, w - 2 * P, h - 94 - 46);
            switch (gorunum)
            {
                case Gorunum.Sarki: SarkiSekmesi(g, icerik, w); break;
                case Gorunum.Modlar: ModlarSekmesi(g, icerik); break;
                case Gorunum.Ekran: EkranSekmesi(g, icerik, w); break;
                case Gorunum.Isik: IsikSekmesi(g, icerik); break;
            }

            // alt durum satırı
            using (var kalem = new Pen(T.Kenar, 1f))
                g.DrawLine(kalem, P, h - 40, w - P, h - 40);
            string mod = durum.AktifMod >= 0 && durum.AktifMod < durum.ModAdlari.Length ? durum.ModAdlari[durum.AktifMod] + " modu" : "";
            Yaz(g, durum.Bagli ? mod : "Klavye bağlı değil", 8f, FontStyle.Regular, T.Soluk, new RectangleF(P, h - 32, 180, 18));
            string sag = durum.Bagli ? durum.Port + (durum.KartSurum != "" ? " · v" + durum.KartSurum : "") : "";
            Yaz(g, sag, 8f, FontStyle.Regular, T.Soluk, new RectangleF(w - P - 150, h - 32, 150, 18), StringAlignment.Far);
        }

        void SarkiSekmesi(Graphics g, RectangleF a, float w)
        {
            var m = durum.Medya;
            KapakCiz(g, new RectangleF(a.X, a.Y, a.Width, 170), 14);
            Yaz(g, m?.Baslik ?? "Çalan bir şey yok", 14f, FontStyle.Bold, T.Yazi, new RectangleF(a.X, a.Y + 180, a.Width, 28));
            Yaz(g, m == null ? "Bir oynatıcıda (Spotify, YouTube…) şarkı başlat" : AltYazi(m), 9.5f, FontStyle.Regular, T.Soluk,
                new RectangleF(a.X, a.Y + 208, a.Width, 20));
            KonumCubugu(g, new RectangleF(a.X, a.Y + 238, a.Width, 5), true);
            OynatmaTuslari(g, w / 2, a.Y + 290, 46);
            SesSatiri(g, new RectangleF(a.X, a.Y + 330, a.Width, 22));
        }

        void EkranSekmesi(Graphics g, RectangleF a, float w)
        {
            const float boy = 214;
            var ekran = new RectangleF((w - boy) / 2, a.Y + 4, boy, boy);
            KlavyeEkrani(g, ekran);

            float y = ekran.Bottom + 18;
            Yaz(g, "KLAVYE EKRANINDA", 7.5f, FontStyle.Bold, T.Soluk, new RectangleF(a.X, y, a.Width, 16));
            bool sistemSayfasi = KartEnAzPanel(2, 7);     // kart 2.7+: canlı sistem grafikleri sayfası
            string[] adlar = sistemSayfasi ? new[] { "Çalan şarkı", "Tuş haritası", "Sistem" } : new[] { "Çalan şarkı", "Tuş haritası" };
            string[] kodlar = sistemSayfasi ? new[] { "medya", "tuslar", "sistem" } : new[] { "medya", "tuslar" };
            int sayfaSayisi = adlar.Length;
            float bw = (a.Width - 8 * (sayfaSayisi - 1)) / sayfaSayisi;
            for (int i = 0; i < sayfaSayisi; i++)
            {
                var r = new RectangleF(a.X + i * (bw + 8), y + 20, bw, 36);
                bool secili = durum.Sayfa == kodlar[i];
                Doldur(g, secili ? T.SekAcik : (uzerindeki == "sayfa" + i ? Karistir(T.Sek, T.Uzerinde) : T.Sek), r, 9);
                using (var kalem = new Pen(T.Kenar, 1f))
                using (var yol = Yuvarlak(r, 9))
                    g.DrawPath(kalem, yol);
                Yaz(g, adlar[i], 9f, FontStyle.Bold, T.Yazi, r, StringAlignment.Center);
                var kod = kodlar[i];
                Ekle(r, () => SayfaIstendi?.Invoke(kod), "sayfa" + i);
            }
            Yaz(g, durum.Bagli ? "Sağ encoder'a basınca da değişir." : "Klavye bağlı değil.", 8f, FontStyle.Regular, T.Soluk,
                new RectangleF(a.X, y + 64, a.Width, 18));
            // uygulama 2.5 / kart 1.20: tuş testi
            KucukDugme(g, new RectangleF(a.X, y + 92, a.Width, 30), "Tuş testi", "tustesti", TusTestineGir, 0, durum.Bagli && KartEnAzPanel(1, 20));
        }

        /// <summary>Klavyenin 240x240 ekranının benzeri (gerçek görüntü değil, aynı bilgilerle çizilir; kart 1.17 düzeni).</summary>
        void KlavyeEkrani(Graphics g, RectangleF e)
        {
            float s = e.Width / 240f;
            var arka = R(11, 16, 24);
            var kart = R(22, 30, 43);
            var yazi = R(241, 245, 249);
            var soluk = R(138, 148, 166);
            Doldur(g, R(17, 24, 39), new RectangleF(e.X - 6, e.Y - 6, e.Width + 12, e.Height + 12), 10);
            Doldur(g, arka, e, 4);
            var renk = durum.AktifMod >= 0 && durum.AktifMod < durum.ModRenkleri.Length ? durum.ModRenkleri[durum.AktifMod] : R(236, 72, 153);
            RectangleF K(float px, float py, float pw, float ph) => new(e.X + px * s, e.Y + py * s, pw * s, ph * s);

            // üst şerit: saat, tarih, mikrofon, bağlantı, mod sırası; altında modun renginde çizgi
            using (var f = new SolidBrush(kart)) g.FillRectangle(f, K(0, 0, 240, 28));
            using (var f = new SolidBrush(renk)) g.FillRectangle(f, K(0, 28, 240, 2));
            var simdi = DateTime.Now;
            Yaz(g, simdi.ToString("HH:mm"), 10f * s, FontStyle.Bold, yazi, K(8, 0, 60, 30));
            Yaz(g, simdi.ToString("ddd d MMMM", new System.Globalization.CultureInfo("tr-TR")), 7.5f * s, FontStyle.Regular, soluk, K(60, 1, 110, 30));
            if (durum.MikKapali)
                Ikon(g, "\uEC54", 8f * s, R(248, 113, 113), K(166, 0, 24, 30));     // Segoe MDL2: mikrofon kapalı
            using (var f = new SolidBrush(durum.Bagli ? R(34, 197, 94) : R(51, 65, 85)))
                g.FillEllipse(f, e.X + 192 * s, e.Y + 11 * s, 8 * s, 8 * s);
            int n = durum.ModAdlari.Length;
            Yaz(g, n > 0 ? $"{durum.AktifMod + 1}/{n}" : "", 8f * s, FontStyle.Regular, renk, K(196, 0, 38, 30), StringAlignment.Far);

            if (durum.Sayfa == "sistem")
            {
                string[] ad = { "İŞLEMCİ", "BELLEK", "EKRAN KARTI", "İNTERNET" };
                for (int i = 0; i < 4; i++)
                {
                    var kutu = K(8 + (i % 2) * 116, 38 + (i / 2) * 98, 108, 90);
                    Doldur(g, kart, kutu, 2 * s);
                    Yaz(g, ad[i], 7.5f * s, FontStyle.Bold, soluk, K(14 + (i % 2) * 116, 42 + (i / 2) * 98, 98, 16));
                    using var kalem = new Pen(renk, 1.5f * s);
                    var nok = new PointF[8];
                    for (int j = 0; j < 8; j++)
                        nok[j] = new PointF(kutu.X + 6 * s + j * (kutu.Width - 12 * s) / 7, kutu.Bottom - 8 * s - (float)((Math.Sin(j * 0.9 + i * 1.7) * 0.5 + 0.5) * (kutu.Height - 36 * s)));
                    g.DrawLines(kalem, nok);
                }
            }
            else if (durum.Sayfa != "tuslar")
            {
                var m = durum.Medya;
                var kare = K(8, 38, 96, 96);
                Doldur(g, kart, kare, 2 * s);
                if (m != null && durum.Bagli) SesCubuklari(g, kare, s, renk, m.Durum == 'P');
                else NotaSimgesi(g, kare, s);
                float x = 114;
                string kaynak = !durum.Bagli ? "Bilgisayar" : m?.Kaynak ?? "";
                string dur = !durum.Bagli ? "Bağlı değil" : m == null ? "Sessizlik" : m.Durum == 'P' ? "Çalıyor" : m.Durum == 'S' ? "Durdu" : "Duraklatıldı";
                Yaz(g, kaynak, 8f * s, FontStyle.Regular, soluk, K(x, 42, 120, 20));
                Yaz(g, dur, 9.5f * s, FontStyle.Bold, renk, K(x, 66, 120, 24));
                Yaz(g, !durum.Bagli || durum.Ses < 0 ? "" : durum.Sessiz ? "Ses: sessiz" : $"Ses %{durum.Ses}", 8f * s, FontStyle.Regular, soluk,
                    K(x, 94, 120, 20));
                string baslik = !durum.Bagli ? "Uygulama bekleniyor" : m?.Baslik ?? "Çalan bir şey yok";
                string sanatci = !durum.Bagli ? "Tuşlar yine de çalışır" : m == null ? "Bir oynatıcıda şarkı başlat" : m.Sanatci ?? "";
                Yaz(g, baslik, 12.5f * s, FontStyle.Bold, yazi, K(6, 140, 228, 26));
                Yaz(g, sanatci, 9.5f * s, FontStyle.Bold, soluk, K(6, 165, 228, 22));
                var iz = K(8, 194, 224, 5);
                using (var f = new SolidBrush(R(39, 50, 68))) g.FillRectangle(f, iz);
                double oran = m != null && m.Sure > 0 ? Math.Clamp(m.Konum / m.Sure, 0, 1) : 0;
                using (var f = new SolidBrush(renk)) g.FillRectangle(f, iz.X, iz.Y, Math.Max(1, (float)(iz.Width * oran)), iz.Height);
                if (m != null && m.Sure > 0)
                {
                    Yaz(g, Sure(m.Konum), 7.5f * s, FontStyle.Regular, soluk, K(8, 204, 80, 20));
                    Yaz(g, Sure(m.Sure), 7.5f * s, FontStyle.Regular, soluk, K(152, 204, 80, 20), StringAlignment.Far);
                }
            }
            else
            {
                esitleyiciAlani = RectangleF.Empty;
                var tuslar = durum.AktifMod >= 0 && durum.AktifMod < durum.ModTuslari.Length ? durum.ModTuslari[durum.AktifMod] : null;
                var tanim = durum.ModTanimlari != null && durum.AktifMod >= 0 && durum.AktifMod < durum.ModTanimlari.Count
                    ? durum.ModTanimlari[durum.AktifMod] : null;
                for (int t = 0; t < 6; t++)
                {
                    var r = K(6 + (t % 3) * 78, 38 + (t / 3) * 82, 72, 76);
                    using (var f = new SolidBrush(kart)) g.FillRectangle(f, r);
                    using (var kalem = new Pen(renk, 2 * s)) g.DrawRectangle(kalem, r.X, r.Y, r.Width, r.Height);
                    Yaz(g, (t + 1).ToString(), 7.5f * s, FontStyle.Regular, renk, new RectangleF(r.X + 5 * s, r.Y + 3 * s, 20 * s, 14 * s));
                    // basılı tutunca çalışan ikinci komut (kart 1.14+): karonun altında, modun renginde
                    string uzun = tanim != null && !string.IsNullOrEmpty(tanim.Tuslar[t].UzunKod) ? KomutKatalogu.KisaAd(tanim.Tuslar[t].UzunKod) : "";
                    Yaz(g, tuslar != null && t < tuslar.Length ? tuslar[t] : "", 8.5f * s, FontStyle.Bold, yazi,
                        new RectangleF(r.X + 2 * s, r.Y + 16 * s, r.Width - 4 * s, (uzun != "" ? 40 : 56) * s), StringAlignment.Center, true);
                    if (uzun != "")
                        Yaz(g, uzun, 7.5f * s, FontStyle.Regular, renk, new RectangleF(r.X + 3 * s, r.Y + 56 * s, r.Width - 6 * s, 16 * s),
                            StringAlignment.Center);
                }
                Yaz(g, "Sol bas: sessiz · uzun: oynat/dur", 7.5f * s, FontStyle.Regular, soluk, K(0, 210, 240, 24), StringAlignment.Center);
            }
        }

        RectangleF esitleyiciAlani;
        readonly double[] eqYukseklik = new double[8];

        /// <summary>Klavyedeki ses çubuklarının benzeri (kart 1.17): renkler kapaktan, yükseklik çalan sesten.</summary>
        void SesCubuklari(Graphics g, RectangleF kare, float s, Color modRengi, bool caliyor)
        {
            esitleyiciAlani = kare;
            var renkler = durum.KapakRenkleri != null && durum.KapakRenkleri.Length > 0
                ? durum.KapakRenkleri
                : new[] { modRengi, Ara(modRengi, Color.White, 0.35f) };
            var (olculuyor, sol, sag) = SesOlcumu();
            double t = saat.Elapsed.TotalSeconds;
            for (int i = 0; i < 8; i++)
            {
                double hedef;
                if (caliyor && olculuyor)
                {
                    double sv = i < 4 ? sol : sag;
                    double dalga = 0.5 + 0.5 * Math.Sin(t * (5.0 + i * 0.9) + i * 1.7);
                    hedef = 3 + 80 * sv * (0.45 + 0.55 * dalga);
                }
                else if (caliyor)
                    hedef = 8 + 22 * (0.5 + 0.5 * Math.Sin(t * (1.6 + i * 0.3) + i * 1.1));
                else
                    hedef = 3;
                double h = eqYukseklik[i];
                h += (hedef - h) * (hedef > h ? 0.6 : 0.25);
                eqYukseklik[i] = h;
                float u = renkler.Length == 1 ? 0 : i / 7f * (renkler.Length - 1);
                int k = Math.Min(renkler.Length - 2, (int)u);
                var c = renkler.Length == 1 ? renkler[0] : Ara(renkler[Math.Max(0, k)], renkler[Math.Max(0, k) + 1], u - Math.Max(0, k));
                float hh = (float)Math.Max(2, h) * s;
                using var f = new SolidBrush(c);
                g.FillRectangle(f, kare.X + (5 + i * 11) * s, kare.Bottom - 8 * s - hh, 9 * s, hh);
            }
        }

        void NotaSimgesi(Graphics g, RectangleF kare, float s)
        {
            esitleyiciAlani = RectangleF.Empty;
            using var f = new SolidBrush(R(71, 85, 105));
            g.FillEllipse(f, kare.X + 29 * s, kare.Y + 55 * s, 22 * s, 22 * s);
            g.FillRectangle(f, kare.X + 46 * s, kare.Y + 22 * s, 5 * s, 44 * s);
            g.FillRectangle(f, kare.X + 46 * s, kare.Y + 22 * s, 20 * s, 6 * s);
        }

        // ---- ışık
        // İki bölge: Zemin (12 taban LED'i) ve Tuşlar (6 LED). Tuşlar ayrı değilse zeminin ayarını kullanır;
        // Tuşlar bölgesinde bir şey seçilince tuşlar kendi ayarına geçer (zeminin ayarı kopyalanarak başlar).
        bool TuslarBolgesi => isikBolge == "tuslar";

        /// <summary>Gösterilen bölgenin ayarı (tuşlar ayrı değilse zeminin ayarı).</summary>
        LedAyari Gorunen => TuslarBolgesi && led.TusAyri && led.Tus != null ? led.Tus : led;

        /// <summary>Değiştirilecek bölgenin ayarı; Tuşlar bölgesinde tuşları ayrı moda geçirir.</summary>
        LedAyari Duzenlenen()
        {
            if (!TuslarBolgesi) return led;
            if (!led.TusAyri || led.Tus == null)
            {
                led.Tus ??= new LedAyari();
                if (!led.TusAyri)
                {
                    led.Tus.Efekt = led.Efekt;
                    led.Tus.Renk = led.Renk;
                    led.Tus.Hiz = led.Hiz;
                    led.Tus.Akis = (Color[])led.Akis.Clone();
                }
                led.TusAyri = true;
            }
            return led.Tus;
        }

        void IsikSekmesi(Graphics g, RectangleF a)
        {
            led ??= durum.Led?.Kopya() ?? new LedAyari();
            var z = Gorunen;
            var bilgi = LedEfektleri.Bul(z.Efekt);
            string kategori = ledKategori ?? (bilgi.Muzik ? "muzik" : "efekt");
            bool tuslarAyni = TuslarBolgesi && !led.TusAyri;

            // harita + özet
            var kart = new RectangleF(a.X, a.Y, a.Width, 108);
            Doldur(g, T.Kart, kart, 10);
            using (var kalem = new Pen(T.Kenar, 1f))
            using (var yol = Yuvarlak(kart, 10))
                g.DrawPath(kalem, yol);
            haritaAlani = new RectangleF(kart.X + 6, kart.Y + 6, 96, 96);
            LedHaritasi(g, haritaAlani);
            float ox = haritaAlani.Right + 10, ow = kart.Right - ox - 10;
            Yaz(g, TuslarBolgesi ? "TUŞLARDA ŞU AN" : "ZEMİNDE ŞU AN", 7.5f, FontStyle.Bold, T.Soluk, new RectangleF(ox, kart.Y + 12, ow, 16));
            Yaz(g, tuslarAyni ? "Zeminle aynı" : bilgi.Ad, 12f, FontStyle.Bold, T.Yazi, new RectangleF(ox, kart.Y + 28, ow, 24));
            string aciklama = !durum.Bagli ? "Klavye bağlanınca uygulanır."
                : !durum.LedDestek ? "Işık ayarı için klavyenin yazılımı 1.9 olmalı."
                : TuslarBolgesi && !durum.LedTusDestek ? "Tuşlara ayrı efekt için klavyenin yazılımı 1.10 olmalı."
                : tuslarAyni ? "Tuşlar zeminin efektini oynatıyor. Bir efekt seçersen kendi efektine geçer."
                : bilgi.Muzik && durum.Medya == null ? bilgi.Aciklama + " Şarkı çalmıyorken sakin kalır."
                : bilgi.Aciklama;
            bool zeminleAyniDugmesi = TuslarBolgesi && led.TusAyri;
            Yaz(g, aciklama, 8f, FontStyle.Regular, T.Soluk, new RectangleF(ox, kart.Y + 52, ow, zeminleAyniDugmesi ? 30 : 52),
                StringAlignment.Near, true, StringAlignment.Near);
            if (zeminleAyniDugmesi)
            {
                var d = new RectangleF(ox - 4, kart.Bottom - 26, ow + 4, 20);
                if (uzerindeki == "zeminleayni") Doldur(g, T.Uzerinde, d, 6);
                Yaz(g, "↺ Zeminle aynı yap", 8f, FontStyle.Bold, T.Yazi, new RectangleF(d.X + 4, d.Y, d.Width - 4, d.Height));
                Ekle(d, () => { led.TusAyri = false; LedDegisti(); }, "zeminleayni");
            }

            // bölge (zemin / tuşlar) ve tür (müziğe göre / efektler)
            float y0 = kart.Bottom + 8;
            var bseg = new RectangleF(a.X, y0, 128, 30);
            var kseg = new RectangleF(bseg.Right + 8, y0, a.Right - bseg.Right - 8, 30);
            Segment(g, bseg, new[] { "Zemin", "Tuşlar" }, TuslarBolgesi ? 1 : 0, "lbol", i =>
            {
                isikBolge = i == 1 ? "tuslar" : "zemin";
                ledKategori = null;
                cipKaydirma = 0;
            });
            Segment(g, kseg, new[] { "Müziğe göre", "Efektler" }, kategori == "muzik" ? 0 : 1, "lkat", i =>
            {
                ledKategori = i == 0 ? "muzik" : "efekt";
                cipKaydirma = 0;
            });

            // efekt çipleri (kaydırılabilir)
            cipAlani = new RectangleF(a.X, y0 + 38, a.Width, 110);
            var liste = new List<LedEfektleri.Bilgi>();
            foreach (var b in LedEfektleri.Liste)
                if (b.Muzik == (kategori == "muzik")) liste.Add(b);
            const float ch = 32, ara = 6;
            int satir = (liste.Count + 2) / 3;
            cipIcerik = satir * ch + (satir - 1) * ara;
            cipKaydirma = Math.Clamp(cipKaydirma, 0, Math.Max(0, cipIcerik - cipAlani.Height));
            bool kaydirma = cipIcerik > cipAlani.Height;
            float cw = (cipAlani.Width - (kaydirma ? 8 : 0) - 2 * ara) / 3;
            var onceki = g.Save();
            g.SetClip(cipAlani);
            for (int i = 0; i < liste.Count; i++)
            {
                var b = liste[i];
                var r = new RectangleF(cipAlani.X + (i % 3) * (cw + ara), cipAlani.Y + (i / 3) * (ch + ara) - cipKaydirma, cw, ch);
                if (r.Bottom < cipAlani.Y || r.Y > cipAlani.Bottom) continue;
                bool secili = b.Kod == z.Efekt && !tuslarAyni;
                Doldur(g, uzerindeki == "cip" + b.Kod && !secili ? Karistir(T.Kart, T.Uzerinde) : T.Kart, r, 8);
                using (var kalem = new Pen(secili ? T.Vurgu : T.Kenar, secili ? 2f : 1f))
                using (var yol = Yuvarlak(secili ? RectangleF.Inflate(r, -1, -1) : r, 8))
                    g.DrawPath(kalem, yol);
                Yaz(g, b.Ad, 8f, FontStyle.Bold, T.Yazi, new RectangleF(r.X + 7, r.Y + 3, r.Width - 12, 17));
                Serit(g, new RectangleF(r.X + 7, r.Bottom - 10, r.Width - 14, 4), CipRenkleri(b, z));
                var kod = b.Kod;
                var gorunen = RectangleF.Intersect(r, cipAlani);
                if (gorunen.Height > 6) Ekle(gorunen, () => { Duzenlenen().Efekt = kod; LedDegisti(); }, "cip" + b.Kod);
            }
            g.Restore(onceki);
            if (kaydirma)
            {
                var iz = new RectangleF(cipAlani.Right - 4, cipAlani.Y, 4, cipAlani.Height);
                Doldur(g, T.Iz, iz, 2);
                float boy = iz.Height * cipAlani.Height / cipIcerik;
                float yk = iz.Y + (iz.Height - boy) * (cipKaydirma / (cipIcerik - cipAlani.Height));
                Doldur(g, T.Soluk, new RectangleF(iz.X, yk, iz.Width, boy), 2);
            }

            // renkler
            var rs = new RectangleF(a.X, cipAlani.Bottom + 8, a.Width, 24);
            RenkSatiri(g, rs, bilgi, z);

            // parlaklık (ortak) ve bölgenin hızı
            parCubugu = Kaydirici(g, new RectangleF(a.X, rs.Bottom + 6, a.Width, 22), "Parlaklık", led.Parlaklik, 5, 100, "par");
            hizCubugu = Kaydirici(g, new RectangleF(a.X, rs.Bottom + 30, a.Width, 22), "Hız", z.Hiz, 20, 200, "hiz");
        }

        void Segment(Graphics g, RectangleF alan, string[] adlar, int secili, string kimlik, Action<int> sec)
        {
            Doldur(g, T.Sek, alan, 10);
            float w = (alan.Width - 6) / adlar.Length;
            for (int i = 0; i < adlar.Length; i++)
            {
                var r = new RectangleF(alan.X + 3 + i * w, alan.Y + 3, w, alan.Height - 6);
                if (i == secili) Doldur(g, T.SekAcik, r, 8);
                else if (uzerindeki == kimlik + i) Doldur(g, T.Uzerinde, r, 8);
                Yaz(g, adlar[i], 8.5f, FontStyle.Bold, i == secili ? T.Yazi : T.Soluk, r, StringAlignment.Center);
                int no = i;
                Ekle(r, () => sec(no), kimlik + i);
            }
        }

        Color[] CipRenkleri(LedEfektleri.Bilgi b, LedAyari z)
        {
            if (b.Muzik && durum.KapakRenkleri != null && durum.KapakRenkleri.Length > 0 && b.Kod != "stereo")
                return durum.KapakRenkleri;
            if (b.Serit != null) return b.Serit;
            if (b.RenkKullanimi == 3) return z.Akis;
            return new[] { LedEfektleri.Olc(z.Renk, 0.25), z.Renk };
        }

        void RenkSatiri(Graphics g, RectangleF r, LedEfektleri.Bilgi b, LedAyari z)
        {
            Yaz(g, b.RenkKullanimi == 3 ? "Renkler" : "Renk", 8f, FontStyle.Bold, T.Soluk, new RectangleF(r.X, r.Y, 46, r.Height));
            float x = r.X + 50, cy = r.Y + r.Height / 2;
            if (b.Muzik || b.RenkKullanimi == 0)
            {
                Yaz(g, b.Muzik ? "Çalan şarkının kapağından alınır" : "Bu efektin kendi renkleri var", 8f, FontStyle.Regular, T.Soluk,
                    new RectangleF(x, r.Y, r.Right - x, r.Height));
                return;
            }
            if (b.RenkKullanimi == 3)
            {
                for (int i = 0; i < 3; i++)
                {
                    var d = new RectangleF(x, cy - 11, 22, 22);
                    RenkDairesi(g, d, z.Akis[i], i == akisSecili, "akis" + i);
                    int no = i;
                    Ekle(RectangleF.Inflate(d, 2, 2), () =>
                    {
                        if (akisSecili == no) OzelRenk(Gorunen.Akis[no], c => Duzenlenen().Akis[no] = c);
                        else akisSecili = no;
                    }, "akis" + i);
                    x += 27;
                }
                using (var kalem = new Pen(T.Kenar, 1f)) g.DrawLine(kalem, x + 2, cy - 9, x + 2, cy + 9);
                x += 9;
                for (int i = 1; i < 7; i++)
                {
                    var c = HazirRenkler[i];
                    var d = new RectangleF(x, cy - 9, 18, 18);
                    RenkDairesi(g, d, c, false, "hazir" + i);
                    Ekle(RectangleF.Inflate(d, 2, 2), () => { Duzenlenen().Akis[akisSecili] = c; LedDegisti(); }, "hazir" + i);
                    x += 22;
                }
                OzelDugme(g, new RectangleF(x, cy - 10, 20, 20), () => OzelRenk(Gorunen.Akis[akisSecili], c => Duzenlenen().Akis[akisSecili] = c));
                return;
            }
            bool hazirda = false;
            for (int i = 0; i < HazirRenkler.Length; i++)
            {
                var c = HazirRenkler[i];
                bool secili = c.ToArgb() == z.Renk.ToArgb();
                hazirda |= secili;
                var d = new RectangleF(x, cy - 11, 22, 22);
                RenkDairesi(g, d, c, secili, "hazir" + i);
                Ekle(RectangleF.Inflate(d, 2, 2), () => { Duzenlenen().Renk = c; LedDegisti(); }, "hazir" + i);
                x += 27;
            }
            var oz = new RectangleF(x, cy - 11, 22, 22);
            if (!hazirda) RenkDairesi(g, oz, z.Renk, true, "ozel");
            else OzelDugme(g, oz, null);
            Ekle(RectangleF.Inflate(oz, 2, 2), () => OzelRenk(Gorunen.Renk, c => Duzenlenen().Renk = c), "ozel");
        }

        void RenkDairesi(Graphics g, RectangleF d, Color c, bool secili, string kimlik)
        {
            if (uzerindeki == kimlik && !secili)
                using (var f = new SolidBrush(T.Uzerinde)) g.FillEllipse(f, RectangleF.Inflate(d, 3, 3));
            using (var f = new SolidBrush(c)) g.FillEllipse(f, d);
            using (var kalem = new Pen(T.Kenar, 1f)) g.DrawEllipse(kalem, d);
            if (secili)
                using (var kalem = new Pen(T.Yazi, 2f)) g.DrawEllipse(kalem, RectangleF.Inflate(d, 2.5f, 2.5f));
        }

        /// <summary>Renk seçici açan, gökkuşağı halkalı küçük düğme.</summary>
        void OzelDugme(Graphics g, RectangleF d, Action tik)
        {
            using (var yol = new GraphicsPath())
            {
                yol.AddEllipse(d);
                using var f = new PathGradientBrush(yol)
                {
                    CenterColor = Color.White,
                    SurroundColors = new[] { R(255, 45, 85), R(255, 214, 10), R(48, 209, 88), R(34, 211, 238), R(59, 130, 246), R(168, 85, 247) },
                };
                g.FillEllipse(f, d);
            }
            Ikon(g, "", d.Height * 0.32f, R(15, 23, 42), d);     // artı
            if (tik != null) Ekle(RectangleF.Inflate(d, 2, 2), tik, "ozel");
        }

        void OzelRenk(Color mevcut, Action<Color> sec)
        {
            renkPenceresiAcik = true;
            try
            {
                using var d = new ColorDialog { Color = mevcut, FullOpen = true, AnyColor = true };
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    sec(Color.FromArgb(d.Color.R, d.Color.G, d.Color.B));
                    LedDegisti();
                }
            }
            finally
            {
                renkPenceresiAcik = false;
                Activate();
            }
        }

        RectangleF Kaydirici(Graphics g, RectangleF r, string ad, int deger, int en, int enCok, string kimlik,
                             string gosterim = null, float adGenisligi = 60)
        {
            Yaz(g, ad, 8f, FontStyle.Bold, T.Soluk, new RectangleF(r.X, r.Y, adGenisligi, r.Height));
            var cubuk = new RectangleF(r.X + adGenisligi + 2, r.Y + r.Height / 2 - 2.5f, r.Width - adGenisligi - 2 - 54, 5);
            float oran = (deger - en) / (float)(enCok - en);
            Doldur(g, T.Iz, cubuk, 2.5f);
            Doldur(g, T.Vurgu, new RectangleF(cubuk.X, cubuk.Y, Math.Max(5, cubuk.Width * oran), cubuk.Height), 2.5f);
            float tx = cubuk.X + cubuk.Width * oran;
            bool etkin = uzerindeki == kimlik || surukle == kimlik;
            using (var f = new SolidBrush(T.Renkli ? Color.White : T.Vurgu))
                g.FillEllipse(f, tx - (etkin ? 7 : 5.5f), cubuk.Y + 2.5f - (etkin ? 7 : 5.5f), etkin ? 14 : 11, etkin ? 14 : 11);
            Yaz(g, gosterim ?? "%" + deger, 8.5f, FontStyle.Regular, T.Yazi, new RectangleF(r.Right - 54, r.Y, 54, r.Height), StringAlignment.Far);
            Ekle(new RectangleF(cubuk.X - 7, r.Y, cubuk.Width + 14, r.Height), null, kimlik);
            return cubuk;
        }

        void Serit(Graphics g, RectangleF r, Color[] renkler)
        {
            using var yol = Yuvarlak(r, r.Height / 2);
            if (renkler.Length == 1)
            {
                using var f = new SolidBrush(renkler[0]);
                g.FillPath(f, yol);
                return;
            }
            using var fr = new LinearGradientBrush(RectangleF.Inflate(r, 1, 0), renkler[0], renkler[^1], 0f);
            var blend = new ColorBlend(renkler.Length) { Colors = renkler, Positions = new float[renkler.Length] };
            for (int i = 0; i < renkler.Length; i++) blend.Positions[i] = i / (float)(renkler.Length - 1);
            fr.InterpolationColors = blend;
            g.FillPath(fr, yol);
        }

        void LedDegisti()
        {
            sonLedDegisimi = DateTime.UtcNow;
            LedAyarIstendi?.Invoke(led.Kopya());
            Invalidate();
        }

        /// <summary>18 LED'in üstten görünüşü; renkler kartın efekt hesabının aynısıyla canlı hesaplanır.</summary>
        void LedHaritasi(Graphics g, RectangleF a)
        {
            double t = saat.Elapsed.TotalSeconds;
            double dt = Math.Clamp(t - sonKare, 0.001, 0.2);
            sonKare = t;
            var m = durum.Medya;
            var olcum = SesOlcumu();
            var modRengi = durum.AktifMod >= 0 && durum.AktifMod < durum.ModRenkleri.Length ? durum.ModRenkleri[durum.AktifMod] : R(255, 40, 120);
            double oran = m != null && m.Sure > 0 ? Math.Clamp(m.Konum / m.Sure, 0, 1) : 0;
            var renk = LedEfektleri.Hesapla(led, led.TusAyri ? led.Tus : null, t, dt, modRengi, durum.KapakRenkleri, m?.Durum == 'P', oran, olcum.sol, olcum.sag, olcum.olculuyor);
            double parlak = 0.35 + 0.65 * led.Parlaklik / 100.0;

            const float x0 = -48, x1 = 48, y0 = -65, y1 = 36, pad = 6;
            float s = Math.Min((a.Width - 2 * pad) / (x1 - x0), (a.Height - 2 * pad) / (y1 - y0));
            float ox = a.X + (a.Width - (x1 - x0) * s) / 2, oy = a.Y + (a.Height - (y1 - y0) * s) / 2;
            PointF N(PointF p) => new(ox + (p.X - x0) * s, oy + (y1 - p.Y) * s);

            var govde = new RectangleF(ox, oy, (x1 - x0) * s, (y1 - y0) * s);
            using (var f = new SolidBrush(T.Renkli ? R(0, 0, 0, 70) : T.Sek))
            using (var yol = Yuvarlak(govde, 7))
                g.FillPath(f, yol);
            using (var kalem = new Pen(T.Kenar, 1.5f))
            using (var yol = Yuvarlak(govde, 7))
                g.DrawPath(kalem, yol);

            for (int i = 0; i < 18; i++)
            {
                var c = LedEfektleri.Olc(renk[i], parlak);
                var p = N(LedEfektleri.Konum[i]);
                int enCok = Math.Max(c.R, Math.Max(c.G, c.B));
                float yr = i < 12 ? 7.5f : 10.5f;
                if (enCok > 8)
                {
                    using var yol = new GraphicsPath();
                    yol.AddEllipse(p.X - yr, p.Y - yr, 2 * yr, 2 * yr);
                    using var f = new PathGradientBrush(yol)
                    {
                        CenterColor = Color.FromArgb(Math.Min(200, enCok), c),
                        SurroundColors = new[] { Color.FromArgb(0, c) },
                    };
                    g.FillPath(f, yol);
                }
                var dolgu = enCok < 10 ? T.Iz : c;
                using var fd = new SolidBrush(dolgu);
                if (i < 12) g.FillEllipse(fd, p.X - 2.4f, p.Y - 2.4f, 4.8f, 4.8f);
                else
                {
                    using var yol = Yuvarlak(new RectangleF(p.X - 6, p.Y - 3.5f, 12, 7), 2.5f);
                    g.FillPath(fd, yol);
                }
            }
        }

        // ---- ayarlar
        void AyarlarCiz(Graphics g, float w, float h)
        {
            IkonDugme(g, "geri", "", new RectangleF(P - 4, P - 2, 26, 26), 11f, AyarlardanCik);
            Yaz(g, "Ayarlar", 12f, FontStyle.Bold, T.Yazi, new RectangleF(P + 28, P - 1, 150, 24));
            Yaz(g, "v" + Program.Surum, 8f, FontStyle.Regular, T.Soluk, new RectangleF(w - P - 80, P + 2, 80, 18), StringAlignment.Far);

            // uygulama 2.3: Genel · Klavye sekmeleri
            float y = 46;
            var sekmeler = new RectangleF(P, y, w - 2 * P, 30);
            Doldur(g, T.Sek, sekmeler, 9);
            string[] sk = { "genel", "klavye", "yedek", "gecmis", "istatistik" }, sa = { "Genel", "Klavye", "Yedek", "Geçmiş", "İstatistik" };
            float sw = (sekmeler.Width - 6) / sk.Length;
            for (int i = 0; i < sk.Length; i++)
            {
                var r = new RectangleF(sekmeler.X + 3 + i * sw, sekmeler.Y + 3, sw, 24);
                bool secili = ayarSekmesi == sk[i];
                if (secili) Doldur(g, T.SekAcik, r, 7);
                else if (uzerindeki == "ayarsek" + i) Doldur(g, T.Uzerinde, r, 7);
                Yaz(g, sa[i], 8.5f, FontStyle.Bold, secili ? T.Yazi : T.Soluk, r, StringAlignment.Center);
                var ad = sk[i];
                Ekle(r, () => { ayarSekmesi = ad; kisayolKaydi = false; ayarKaydirma = 0; KutulariYerlestir(); }, "ayarsek" + i);
            }
            y += 42;

            if (ayarSekmesi == "genel")
            {
                Grup(g, "GÖRÜNÜM", y);
                y += 20;
                float kw = (w - 2 * P - 8) / 2;
                TurKarti(g, new RectangleF(P, y, kw, 74), "kucuk", "Sadece şarkı");
                TurKarti(g, new RectangleF(P + kw + 8, y, kw, 74), "sekmeli", "Sekmeli");
                y += 82;
                var segment = new RectangleF(P, y, w - 2 * P, 32);
                Doldur(g, T.Sek, segment, 10);
                float tw = (segment.Width - 6) / 3;
                for (int i = 0; i < 3; i++)
                {
                    var ad = TemaSirasi[i];
                    var r = new RectangleF(segment.X + 3 + i * tw, segment.Y + 3, tw, 26);
                    if (ad == temaAdi) Doldur(g, T.SekAcik, r, 8);
                    else if (uzerindeki == "ayartema" + i) Doldur(g, T.Uzerinde, r, 8);
                    Yaz(g, Temalar[ad].Ad, 9f, FontStyle.Bold, ad == temaAdi ? T.Yazi : T.Soluk, r, StringAlignment.Center);
                    var tema = ad;
                    Ekle(r, () => TemaSec(tema), "ayartema" + i);
                }
                y += 46;

                Grup(g, "DAVRANIŞ", y);
                y += 18;
                y = AnahtarSatiri(g, y, w, "Windows açılınca başlat", "Saatin yanında simge olarak açılır", BaslangictaMi(),
                                  () => BaslangicDegistir(), "a1");
                y = AnahtarSatiri(g, y, w, "Bağlantı bildirimi", "Klavye takılınca / çıkarılınca haber ver", BaglantiBildirimi,
                                  () => Ayarlar.Yaz("BaglantiBildirimi", BaglantiBildirimi ? "0" : "1"), "a2");
                y = AnahtarSatiri(g, y, w, "Dışarı tıklayınca kapan", "Kapalıysa panel, simgeye ya da Esc'ye basana kadar açık kalır",
                                  DisariTiklayincaKapan, () => Ayarlar.Yaz("DisariKapan", DisariTiklayincaKapan ? "0" : "1"), "a3");
                y += 6;
            }
            else if (ayarSekmesi == "yedek")
                YedekCiz(g, y, w, h);
            else if (ayarSekmesi == "gecmis")
                GecmisCiz(g, y, w, h);
            else if (ayarSekmesi == "istatistik")
                IstatistikCiz(g, y, w, h);
            else
            {
                // uygulama 3.1: içerik sığmıyorsa tekerlekle kaydırılır
                ayarAlani = new RectangleF(0, y - 4, w, h - 46 - (y - 4));
                int ilkHedef = hedefler.Count;
                var eskiKesme = g.Clip;
                g.SetClip(ayarAlani);
                float bas = y - ayarKaydirma;
                float sonY = KlavyeSekmesiCiz(g, bas, w, h);
                g.Clip = eskiKesme;
                ayarIcerik = sonY - bas;
                for (int i = hedefler.Count - 1; i >= ilkHedef; i--)      // görünmeyen satırlara tıklanmasın
                    if (hedefler[i].Alan.Bottom < ayarAlani.Y || hedefler[i].Alan.Y > ayarAlani.Bottom)
                        hedefler.RemoveAt(i);
                if (ayarIcerik > ayarAlani.Height) KaydirmaCubugu(g, ayarAlani, ayarKaydirma, ayarIcerik);
            }

            using (var kalem = new Pen(T.Kenar, 1f))
                g.DrawLine(kalem, P, h - 40, w - P, h - 40);
            var hata = new RectangleF(P, h - 34, 140, 22);
            Yaz(g, "Hata kayıtlarını aç", 8.5f, FontStyle.Regular, uzerindeki == "hata" ? T.Yazi : T.Soluk, hata);
            Ekle(hata, () => HataKayitlariIstendi?.Invoke(), "hata");
            var cik = new RectangleF(w - P - 60, h - 34, 60, 22);
            Yaz(g, "Çıkış", 8.5f, FontStyle.Regular, uzerindeki == "cik" ? R(239, 68, 68) : T.Soluk, cik, StringAlignment.Far);
            Ekle(cik, () => CikisIstendi?.Invoke(), "cik");
        }

        RectangleF ayarAlani;
        float ayarKaydirma, ayarIcerik;
        DateTime sonSurumBakisi = DateTime.MinValue;
        bool yeniSurumVar;

        /// <summary>Ayarlar > Klavye sekmesinin içeriği; alt kenarını döner.</summary>
        float KlavyeSekmesiCiz(Graphics g, float y, float w, float h)
        {
            {
                Yaz(g, durum.Bagli ? "Makro Klavye v2 · " + durum.Port : "Klavye bulunamadı", 10f, FontStyle.Bold, T.Yazi,
                    new RectangleF(P, y + 2, w - 2 * P - 100, 20));
                Yaz(g, durum.Bagli ? (durum.KartSurum != "" ? "Kart yazılımı " + durum.KartSurum : "Bağlı") : "USB'ye takılı olduğundan emin ol",
                    8f, FontStyle.Regular, T.Soluk, new RectangleF(P, y + 22, w - 2 * P - 100, 16));
                var ara = new RectangleF(w - P - 92, y + 6, 92, 28);
                Doldur(g, uzerindeki == "ara" ? Karistir(T.Sek, T.Uzerinde) : T.Sek, ara, 8);
                Yaz(g, "Yeniden ara", 8.5f, FontStyle.Bold, T.Yazi, ara, StringAlignment.Center);
                Ekle(ara, () => YenidenAraIstendi?.Invoke(), "ara");

                // klavyenin belleği (kart 1.12+): şu an boş olan ve açıldığından beri en düşük
                if (durum.Bagli && durum.BosBellek >= 0)
                {
                    var renk = durum.EnAzBellek < 8 * 1024 ? R(248, 113, 113) : durum.EnAzBellek < 16 * 1024 ? R(251, 191, 36) : T.Soluk;
                    Yaz(g, $"Boş bellek {durum.BosBellek / 1024.0:0.0} KB · en düşük {durum.EnAzBellek / 1024.0:0.0} KB", 8f, FontStyle.Regular, renk,
                        new RectangleF(P, y + 40, w - 2 * P - 70, 16));
                    var sf = new RectangleF(w - P - 64, y + 40, 64, 16);
                    Yaz(g, "sıfırla", 8f, FontStyle.Regular, uzerindeki == "bellek" ? T.Yazi : T.Soluk, sf, StringAlignment.Far);
                    Ekle(sf, () => BellekSifirlaIstendi?.Invoke(), "bellek");
                }
                y += 56;
                // uygulama 3.0: kart yazılımını uygulamadan güncelle (elle güncelleme moduna almaya gerek yok)
                {
                    bool calisiyor = KartGuncelleme.Calisiyor;
                    // 3.2 (CM): klasörde karttakinden yeni bir .uf2 var mı
                    if (!calisiyor && durum.Bagli && (DateTime.UtcNow - sonSurumBakisi).TotalSeconds > 20)
                    {
                        sonSurumBakisi = DateTime.UtcNow;
                        yeniSurumVar = KartGuncelleme.YeniSurumAra(durum.KartSurum);
                    }
                    bool hazir = yeniSurumVar && !calisiyor;
                    KucukDugme(g, new RectangleF(P, y, 150, 28),
                               calisiyor ? "Güncelleniyor…" : hazir ? $"{KartGuncelleme.HazirSurum} sürümünü yükle" : "Kart yazılımını yükle",
                               "kartguncelle", () => { if (hazir) HazirSurumuYukleIstendi?.Invoke(); else YazilimGuncelleIstendi?.Invoke(); },
                               hazir ? 1 : 0, durum.Bagli && !calisiyor);
                    string dr = KartGuncelleme.Durum;
                    Yaz(g, dr != "" ? dr
                           : hazir ? $"Kartta {durum.KartSurum} var, klasörde {KartGuncelleme.HazirSurum} hazır"
                           : "Hazırladığım .uf2 dosyasını seç, gerisini uygulama yapar",
                        8f, FontStyle.Regular, hazir ? T.Yazi : T.Soluk,
                        new RectangleF(P + 158, y, w - 2 * P - 158, 28), StringAlignment.Near, true);
                    y += 36;
                }
                return KlavyeEkraniCiz(g, y, w, h);
            }
        }

        // ---- Ayarlar > Klavye: klavyenin ekranı (kart 1.13+)
        static readonly (int bit, string ad, string alt)[] EkranBayraklari =
        {
            (1, "Mod şeridi", "Mod değişince altta modun renginde şerit"),
            (8, "Sistem bilgisi", "Tuş sayfasında işlemci, bellek, ekran kartı"),
            (2, "Tuş geri bildirimi", "Basınca komutun adı tam ekran görünür (kart 3.1)"),
            (32, "Geçiş animasyonu", "Mod değişince karolar kayarak gelir (kart 3.1)"),
            (64, "Açılış ekranı", "Klavye açılırken logo görünür (kart 3.1)"),
            (4, "Işık dalgası", "Tuşa basınca LED'lerde yayılan dalga (kart 3.1)"),
        };
        static readonly string[] SesIsikAdlari = { "Kapalı", "Mod rengi", "Beyaz", "Seviye" };

        float KlavyeEkraniCiz(Graphics g, float y, float w, float h)
        {
            Grup(g, "KLAVYE EKRANI", y);
            y += 20;
            if (!durum.EkranDestek || ekranAyar == null)
            {
                string m = !durum.Bagli ? "Klavye bağlı değil"
                    : !durum.EkranDestek ? "Bu ayarlar için kart yazılımı 1.13 gerekli" : "Klavyeden okunuyor…";
                Yaz(g, m, 8.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, y, w - 2 * P, 18));
                return y + 20;
            }
            ekParCubugu = Kaydirici(g, new RectangleF(P, y, w - 2 * P, 22), "Parlaklık", ekranAyar[0], 10, 100, "ekpar");
            y += 30;
            // uygulama 3.1: anahtarlar iki sütunda, açıklama altta imlecin üstündekine göre
            float kg = (w - 2 * P - 8) / 2;
            string aciklama = null;
            for (int i = 0; i < EkranBayraklari.Length; i++)
            {
                var (bit, ad, alt) = EkranBayraklari[i];
                bool yeni = bit != 1 && bit != 8;                 // kart 3.1 gerektirenler
                bool etkin = !yeni || KartEnAzPanel(3, 1);
                bool acik = (ekranAyar[1] & bit) != 0 && etkin;
                var satir = new RectangleF(P + (i % 2) * (kg + 8), y + (i / 2) * 30, kg, 28);
                string kimlik = "ekb" + i;
                if (uzerindeki == kimlik) { Doldur(g, T.Uzerinde, satir, 8); aciklama = etkin ? alt : "Bunun için kart yazılımı 3.1 gerekli"; }
                Yaz(g, ad, 8.5f, FontStyle.Bold, etkin ? T.Yazi : T.Soluk, new RectangleF(satir.X + 6, satir.Y, satir.Width - 42, satir.Height));
                var an = new RectangleF(satir.Right - 36, satir.Y + 6, 30, 16);
                Doldur(g, acik ? T.Vurgu : T.Iz, an, 8f);
                using (var f = new SolidBrush(acik && T.Vurgu == Color.FromArgb(255, 255, 255) ? R(27, 20, 70) : Color.White))
                    g.FillEllipse(f, acik ? an.Right - 14.5f : an.X + 2, an.Y + 2, 12, 12);
                if (etkin) Ekle(satir, () => { ekranAyar[1] ^= bit; EkranDegisti(); }, kimlik);
            }
            y += (EkranBayraklari.Length + 1) / 2 * 30;
            Yaz(g, aciklama ?? "Anahtarın üstüne gelince ne yaptığı yazar", 7.5f, FontStyle.Regular, T.Soluk,
                new RectangleF(P, y, w - 2 * P, 16));
            y += 22;

            // uygulama 3.1 (X): uzun basış süresi
            if (ekranAyar.Length >= 7 && KartEnAzPanel(3, 1))
            {
                uzunCubugu = Kaydirici(g, new RectangleF(P, y, w - 2 * P, 22), "Uzun basış", ekranAyar[6], 4, 30,
                                       "uzunbas", ekranAyar[6] * 50 + " ms", 66);
                y += 30;
            }
            Grup(g, "SES DEĞİŞİNCE IŞIK", y);
            y += 18;
            var seg = new RectangleF(P, y, w - 2 * P, 30);
            Doldur(g, T.Sek, seg, 9);
            float tw = (seg.Width - 6) / SesIsikAdlari.Length;
            for (int i = 0; i < SesIsikAdlari.Length; i++)
            {
                var r = new RectangleF(seg.X + 3 + i * tw, seg.Y + 3, tw, 24);
                bool secili = ekranAyar[2] == i;
                if (secili) Doldur(g, T.SekAcik, r, 7);
                else if (uzerindeki == "sesisik" + i) Doldur(g, T.Uzerinde, r, 7);
                Yaz(g, SesIsikAdlari[i], 8.5f, FontStyle.Bold, secili ? T.Yazi : T.Soluk, r, StringAlignment.Center);
                int deger = i;
                Ekle(r, () => { ekranAyar[2] = deger; EkranDegisti(); }, "sesisik" + i);
            }
            y += 38;

            // kart 1.20: gece modu (belirli saatlerde ekran ve ışıklar kısılır)
            if (ekranAyar.Length < 6)
                return y;
            Grup(g, "GECE MODU", y);
            y += 18;
            bool geceAcik = ekranAyar[3] <= 23;
            var gs = new RectangleF(P, y, w - 2 * P, 34);
            if (uzerindeki == "gece") Doldur(g, T.Uzerinde, new RectangleF(gs.X - 6, gs.Y, gs.Width + 12, gs.Height), 8);
            Yaz(g, "Gece kıs", 9f, FontStyle.Bold, T.Yazi, new RectangleF(P, y + 1, w - 2 * P - 46, 18));
            Yaz(g, "Bu saatler arasında ekran ve ışıklar kısılır", 7.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, y + 17, w - 2 * P - 46, 15));
            var ga = new RectangleF(w - P - 32, y + 9, 32, 17);
            Doldur(g, geceAcik ? T.Vurgu : T.Iz, ga, 8.5f);
            using (var f = new SolidBrush(geceAcik && T.Vurgu == Color.FromArgb(255, 255, 255) ? R(27, 20, 70) : Color.White))
                g.FillEllipse(f, geceAcik ? ga.Right - 15.5f : ga.X + 2, ga.Y + 2, 13, 13);
            Ekle(gs, () => { ekranAyar[3] = geceAcik ? 255 : 23; EkranDegisti(); }, "gece");
            y += 38;
            if (!geceAcik)
                return y;
            float gw = (w - 2 * P - 12) / 3;
            var gb = new RectangleF(P, y, gw, 28);
            var gbit = new RectangleF(P + gw + 6, y, gw, 28);
            var gp = new RectangleF(P + 2 * (gw + 6), y, gw, 28);
            MenuDugmesi(g, gb, $"{ekranAyar[3]:00}:00", "gecebas", true, () => GeceMenusu(gb, 3));
            MenuDugmesi(g, gbit, $"→ {ekranAyar[4]:00}:00", "gecebit", true, () => GeceMenusu(gbit, 4));
            MenuDugmesi(g, gp, $"%{ekranAyar[5]}", "gecepar", true, () => GeceMenusu(gp, 5));
            return y + 34;
        }

        /// <summary>Gece modu: başlangıç (3), bitiş (4) saati ya da parlaklık (5) seçimi.</summary>
        void GeceMenusu(RectangleF r, int alan)
        {
            var menu = new ContextMenuStrip { ShowCheckMargin = true, ShowImageMargin = false };
            int[] degerler = alan == 5 ? new[] { 5, 10, 20, 30, 50 } : Enumerable.Range(0, 24).ToArray();
            foreach (var v in degerler)
            {
                int d = v;
                var oge = new ToolStripMenuItem(alan == 5 ? $"%{d}" : $"{d:00}:00") { Checked = ekranAyar[alan] == d };
                oge.Click += (s, e) => { ekranAyar[alan] = d; EkranDegisti(); };
                menu.Items.Add(oge);
            }
            renkPenceresiAcik = true;
            menu.Closed += (s, e) =>
            {
                renkPenceresiAcik = false;
                BeginInvoke(new Action(menu.Dispose));
            };
            menu.Show(this, new Point((int)(r.X * O), (int)(r.Bottom * O)));
        }

        void EkranDegisti()
        {
            sonEkranDegisimi = DateTime.UtcNow;
            EkranAyarIstendi?.Invoke((int[])ekranAyar.Clone());
            Invalidate();
        }

        void Grup(Graphics g, string ad, float y) =>
            Yaz(g, ad, 7.5f, FontStyle.Bold, T.Soluk, new RectangleF(P, y, G - 2 * P, 16));

        void TurKarti(Graphics g, RectangleF r, string tur, string ad)
        {
            bool secili = panelTuru == tur;
            Doldur(g, uzerindeki == "tur" + tur && !secili ? Karistir(T.Kart, T.Uzerinde) : T.Kart, r, 10);
            using (var kalem = new Pen(secili ? T.Vurgu : T.Kenar, secili ? 2f : 1f))
            using (var yol = Yuvarlak(r, 10))
                g.DrawPath(kalem, yol);
            // küçük önizleme
            var on = new RectangleF(r.X + 8, r.Y + 8, r.Width - 16, 38);
            Doldur(g, T.Sek, on, 6);
            using var iz = new SolidBrush(T.Iz);
            if (tur == "kucuk")
            {
                KapakCiz(g, new RectangleF(on.X + 5, on.Y + 5, 18, 18), 4);
                g.FillRectangle(iz, on.X + 28, on.Y + 7, on.Width * 0.5f, 4);
                g.FillRectangle(iz, on.X + 28, on.Y + 15, on.Width * 0.3f, 4);
                g.FillRectangle(iz, on.X + 5, on.Y + 29, on.Width - 10, 3);
            }
            else
            {
                float bw = (on.Width - 14) / 3;
                for (int i = 0; i < 3; i++) g.FillRectangle(iz, on.X + 5 + i * (bw + 2), on.Y + 5, bw, 4);
                KapakCiz(g, new RectangleF(on.X + 5, on.Y + 13, on.Width - 10, 14), 3);
                g.FillRectangle(iz, on.X + 5, on.Y + 31, on.Width * 0.6f, 3);
            }
            Yaz(g, ad, 8.5f, FontStyle.Bold, T.Yazi, new RectangleF(r.X, r.Bottom - 26, r.Width, 22), StringAlignment.Center);
            Ekle(r, () => TurSec(tur), "tur" + tur);
        }

        float AnahtarSatiri(Graphics g, float y, float w, string baslik, string alt, bool acik, Action degistir, string kimlik)
        {
            var satir = new RectangleF(P, y, w - 2 * P, 42);
            if (uzerindeki == kimlik) Doldur(g, T.Uzerinde, new RectangleF(satir.X - 6, satir.Y, satir.Width + 12, satir.Height), 8);
            Yaz(g, baslik, 9.5f, FontStyle.Bold, T.Yazi, new RectangleF(P, y + 3, w - 2 * P - 50, 20));
            Yaz(g, alt, 7.5f, FontStyle.Regular, T.Soluk, new RectangleF(P, y + 22, w - 2 * P - 50, 16));
            var an = new RectangleF(w - P - 36, y + 11, 36, 20);
            Doldur(g, acik ? T.Vurgu : T.Iz, an, 10);
            using (var f = new SolidBrush(acik && T.Vurgu == Color.FromArgb(255, 255, 255) ? R(27, 20, 70) : Color.White))
                g.FillEllipse(f, acik ? an.Right - 18 : an.X + 2, an.Y + 2, 16, 16);
            Ekle(satir, () => { degistir(); Invalidate(); }, kimlik);
            return y + 44;
        }

        // ---- ortak parçalar
        void KapakCiz(Graphics g, RectangleF r, float yaricap)
        {
            using var yol = Yuvarlak(r, yaricap);
            if (kapak == null)
            {
                using var f = new LinearGradientBrush(r, R(236, 72, 153), R(91, 42, 134), 45f);
                g.FillPath(f, yol);
                if (r.Height > 40) Ikon(g, "", r.Height / 5f, R(255, 255, 255, 170), r);   // müzik notası
                return;
            }
            // kapağı alanı dolduracak şekilde ortadan kırp
            float oran = Math.Max(r.Width / kapak.Width, r.Height / kapak.Height);
            float kw = kapak.Width * oran, kh = kapak.Height * oran;
            var durumG = g.Save();
            g.SetClip(yol);
            g.DrawImage(kapak, r.X + (r.Width - kw) / 2, r.Y + (r.Height - kh) / 2, kw, kh);
            g.Restore(durumG);
        }

        void KonumCubugu(Graphics g, RectangleF r, bool sureler)
        {
            var m = durum.Medya;
            double oran = m != null && m.Sure > 0 ? Math.Clamp(m.Konum / m.Sure, 0, 1) : 0;
            Doldur(g, T.Iz, r, r.Height / 2);
            if (oran > 0) Doldur(g, T.Vurgu, new RectangleF(r.X, r.Y, (float)(r.Width * oran), r.Height), r.Height / 2);
            konumCubugu = new RectangleF(r.X, r.Y - 6, r.Width, r.Height + 12);
            if (m != null && m.Sure > 0)
                Ekle(konumCubugu, null, "konum");
            if (sureler)
            {
                Yaz(g, m == null ? "" : Sure(m.Konum), 8f, FontStyle.Regular, T.Soluk, new RectangleF(r.X, r.Bottom + 4, 60, 16));
                Yaz(g, m == null || m.Sure <= 0 ? "" : Sure(m.Sure), 8f, FontStyle.Regular, T.Soluk,
                    new RectangleF(r.Right - 60, r.Bottom + 4, 60, 16), StringAlignment.Far);
            }
        }

        void OynatmaTuslari(Graphics g, float cx, float cy, float buyuk)
        {
            bool caliyor = durum.Medya?.Durum == 'P';
            float ara = buyuk >= 40 ? 70 : 54;
            IkonDugme(g, "onceki", "", new RectangleF(cx - ara - 16, cy - 16, 32, 32), buyuk >= 40 ? 13f : 11f, () => MedyaKomutu?.Invoke("onceki"));
            IkonDugme(g, "sonraki", "", new RectangleF(cx + ara - 16, cy - 16, 32, 32), buyuk >= 40 ? 13f : 11f, () => MedyaKomutu?.Invoke("sonraki"));
            var r = new RectangleF(cx - buyuk / 2, cy - buyuk / 2, buyuk, buyuk);
            if (buyuk >= 40)
            {
                var renk = uzerindeki == "oynat" ? Karistir(T.Vurgu, R(255, 255, 255, 40)) : T.Vurgu;
                using (var f = new SolidBrush(renk)) g.FillEllipse(f, r);
                Ikon(g, caliyor ? "" : "", 13f, T.Renkli ? R(27, 20, 70) : Color.White, r);
                Ekle(r, () => MedyaKomutu?.Invoke("oynat"), "oynat");
            }
            else
                IkonDugme(g, "oynat", caliyor ? "" : "", r, 12f, () => MedyaKomutu?.Invoke("oynat"));
        }

        void SesSatiri(Graphics g, RectangleF r)
        {
            int ses = surukSes ?? durum.Ses;
            var ik = new RectangleF(r.X - 4, r.Y - 4, 28, r.Height + 8);
            IkonDugme(g, "sessiz", durum.Sessiz ? "" : "", ik, 10.5f, () => SessizIstendi?.Invoke());
            var cubuk = new RectangleF(r.X + 30, r.Y + r.Height / 2 - 2.5f, r.Width - 30 - 44, 5);
            Doldur(g, T.Iz, cubuk, 2.5f);
            if (ses > 0) Doldur(g, durum.Sessiz ? T.Soluk : T.Vurgu, new RectangleF(cubuk.X, cubuk.Y, cubuk.Width * ses / 100f, cubuk.Height), 2.5f);
            if (ses >= 0 && (uzerindeki == "ses" || sesSurukleniyor))
                using (var f = new SolidBrush(T.Renkli ? Color.White : T.Vurgu))
                    g.FillEllipse(f, cubuk.X + cubuk.Width * ses / 100f - 6, cubuk.Y - 3.5f, 12, 12);
            Yaz(g, ses < 0 ? "–" : durum.Sessiz ? "sessiz" : ses + "%", 8.5f, FontStyle.Regular, T.Yazi,
                new RectangleF(r.Right - 44, r.Y, 44, r.Height), StringAlignment.Far);
            sesCubugu = new RectangleF(cubuk.X - 6, r.Y - 4, cubuk.Width + 12, r.Height + 8);
            Ekle(sesCubugu, null, "ses");
        }

        void IkonDugme(Graphics g, string kimlik, string ikon, RectangleF r, float boyut, Action tik)
        {
            if (uzerindeki == kimlik) Doldur(g, T.Uzerinde, r, Math.Min(r.Width, r.Height) / 2);
            Ikon(g, ikon, boyut, T.Yazi, r);
            Ekle(r, tik, kimlik);
        }

        // ------------------------------------------------------------------ eylemler
        void AyarlaraGir()
        {
            ayarOncesi = gorunum;
            gorunum = Gorunum.Ayarlar;
            Boyutla();
        }

        void AyarlardanCik()
        {
            gorunum = Kucuk ? Gorunum.Sarki : ayarOncesi;
            Boyutla();
        }

        void TemaSec(string ad)
        {
            temaAdi = ad;
            Ayarlar.Yaz("Tema", ad);
            KutulariYerlestir();
            Invalidate();
        }

        void TurSec(string tur)
        {
            panelTuru = tur;
            Ayarlar.Yaz("PanelTuru", tur);
            ayarOncesi = Gorunum.Sarki;
            Invalidate();
        }

        // ------------------------------------------------------------------ fare
        Hedef Bul(Point p)
        {
            var q = new PointF(p.X / O, p.Y / O);
            for (int i = hedefler.Count - 1; i >= 0; i--)
                if (hedefler[i].Alan.Contains(q))
                    return hedefler[i];
            return null;
        }

        void Ekle(RectangleF alan, Action tik, string kimlik) =>
            hedefler.Add(new Hedef { Alan = alan, Tik = tik, Kimlik = kimlik });

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (cizimSuruyor)
            {
                CizimBoya(e.Location);
                return;
            }
            if (surukle != null)
            {
                CubuguFareyeGore(e.Location, false);
                return;
            }
            if (e.Button == MouseButtons.Left) { TusSurukleHareket(e.Location); ModSurukleHareket(e.Location); }
            var h = Bul(e.Location);
            var yeni = h?.Kimlik;
            Cursor = h != null ? Cursors.Hand : Cursors.Default;
            if (yeni != uzerindeki)
            {
                uzerindeki = yeni;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (uzerindeki != null)
            {
                uzerindeki = null;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;
            if (gorunum == Gorunum.SimgeCiz && cizimAlani.Contains(e.Location.X / O, e.Location.Y / O))
            {
                cizimSuruyor = true;                // 3.1: simge çizimi, sürükleyerek boyama
                Capture = true;
                CizimBoya(e.Location);
                return;
            }
            var h = Bul(e.Location);
            TusSurukleBasla(h, e.Location);         // Modu düzenle: tuş karosu sürüklenebilir
            ModSurukleBasla(h, e.Location);         // 3.2 (AX): mod listesinde sıralama
            if ((h?.Kimlik == "ses" && durum.Ses >= 0) || ((h?.Kimlik == "par" || h?.Kimlik == "hiz") && led != null)
                || ((h?.Kimlik == "ekpar" || h?.Kimlik == "uzunbas") && ekranAyar != null))
            {
                surukle = h.Kimlik;
                Capture = true;
                CubuguFareyeGore(e.Location, false);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                var sh = Bul(e.Location);
                if (gorunum == Gorunum.ModDuzen && sh?.Kimlik != null && sh.Kimlik.StartsWith("mtus") && int.TryParse(sh.Kimlik.Substring(4), out int sno))
                    TusMenusu(sno, e.Location);
                return;
            }
            if (e.Button != MouseButtons.Left)
                return;
            if (cizimSuruyor)
            {
                cizimSuruyor = false;
                Capture = false;
                return;
            }
            if (ModSurukleBitir(Bul(e.Location)))
                return;
            if (TusSurukleBitir(Bul(e.Location)))
                return;
            if (surukle != null)
            {
                CubuguFareyeGore(e.Location, true);
                surukle = null;
                Capture = false;
                Invalidate();
                return;
            }
            var h = Bul(e.Location);
            if (h == null)
                return;
            if (h.Kimlik == "konum")
            {
                float x = e.X / O;
                KonumIstendi?.Invoke(Math.Clamp((x - konumCubugu.X) / konumCubugu.Width, 0, 1));
                return;
            }
            h.Tik?.Invoke();
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            var q = new PointF(e.X / O, e.Y / O);
            if (ModTekerlek(q, e.Delta))
                return;
            if (gorunum == Gorunum.Ayarlar && ayarSekmesi == "klavye" && ayarIcerik > ayarAlani.Height && ayarAlani.Contains(q))
            {
                ayarKaydirma = Math.Clamp(ayarKaydirma - Math.Sign(e.Delta) * 48, 0, ayarIcerik - ayarAlani.Height);
                Invalidate();
                return;
            }
            if (gorunum == Gorunum.Isik && cipAlani.Contains(q) && cipIcerik > cipAlani.Height)
            {
                cipKaydirma = Math.Clamp(cipKaydirma - Math.Sign(e.Delta) * 40, 0, cipIcerik - cipAlani.Height);
                Invalidate();
            }
        }

        void CubuguFareyeGore(Point p, bool bitti)
        {
            if (surukle == "ses")
            {
                SesiFareyeGore(p, bitti);
                return;
            }
            if (surukle == "uzunbas" && ekranAyar.Length >= 7)
            {
                float ou = Math.Clamp((p.X / O - uzunCubugu.X) / uzunCubugu.Width, 0, 1);
                int ub = (int)Math.Round(4 + ou * 26);
                if (ub == ekranAyar[6] && !bitti) return;
                ekranAyar[6] = ub;
                EkranDegisti();
                return;
            }
            if (surukle == "ekpar")
            {
                float o = Math.Clamp((p.X / O - ekParCubugu.X) / ekParCubugu.Width, 0, 1);
                int ev = (int)Math.Round((10 + o * 90) / 5) * 5;
                if (ev == ekranAyar[0] && !bitti) return;
                ekranAyar[0] = ev;
                EkranDegisti();
                return;
            }
            var c = surukle == "par" ? parCubugu : hizCubugu;
            float oran = Math.Clamp((p.X / O - c.X) / c.Width, 0, 1);
            if (surukle == "par")
            {
                int v = (int)Math.Round(5 + oran * 95);
                if (v == led.Parlaklik && !bitti) return;
                led.Parlaklik = v;
            }
            else
            {
                int v = (int)Math.Round((20 + oran * 180) / 5) * 5;
                if (v == Gorunen.Hiz && !bitti) return;
                Duzenlenen().Hiz = v;
            }
            LedDegisti();
        }

        void SesiFareyeGore(Point p, bool bitti)
        {
            float x = p.X / O;
            var c = new RectangleF(sesCubugu.X + 6, 0, sesCubugu.Width - 12, 1);
            int ses = (int)Math.Round(Math.Clamp((x - c.X) / c.Width, 0, 1) * 100);
            surukSes = bitti ? null : ses;
            SesIstendi?.Invoke(ses);
            if (bitti) durum.Ses = ses;
            Invalidate();
        }

        // ------------------------------------------------------------------ yardımcılar
        static string AltYazi(MedyaBilgisi m)
        {
            var parca = new List<string>();
            if (!string.IsNullOrEmpty(m.Sanatci)) parca.Add(m.Sanatci);
            if (!string.IsNullOrEmpty(m.Kaynak)) parca.Add(m.Kaynak);
            if (m.Durum != 'P') parca.Add(m.Durum == 'D' ? "duraklatıldı" : "durdu");
            return string.Join(" · ", parca);
        }

        static Color Ara(Color a, Color b, float t) =>
            Color.FromArgb(a.R + (int)((b.R - a.R) * t), a.G + (int)((b.G - a.G) * t), a.B + (int)((b.B - a.B) * t));

        static string Sure(double sn)
        {
            if (sn < 0) sn = 0;
            var t = TimeSpan.FromSeconds(sn);
            return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
        }

        static Color Karistir(Color a, Color b)
        {
            float k = b.A / 255f;
            return Color.FromArgb(Math.Max(a.A, b.A),
                (int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));
        }

        static GraphicsPath Yuvarlak(RectangleF r, float y)
        {
            var yol = new GraphicsPath();
            float d = Math.Min(y * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f)
            {
                yol.AddRectangle(r);
                return yol;
            }
            yol.AddArc(r.X, r.Y, d, d, 180, 90);
            yol.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            yol.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            yol.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            yol.CloseFigure();
            return yol;
        }

        static void Doldur(Graphics g, Color renk, RectangleF r, float yaricap)
        {
            using var f = new SolidBrush(renk);
            using var yol = Yuvarlak(r, yaricap);
            g.FillPath(f, yol);
        }

        static readonly Dictionary<(string, float, FontStyle), Font> yaziTipleri = new();

        static Font YaziTipi(string ad, float boyut, FontStyle stil)
        {
            var k = (ad, (float)Math.Round(boyut, 2), stil);
            if (!yaziTipleri.TryGetValue(k, out var f))
            {
                // piksel birimi: DPI ölçeği ScaleTransform ile zaten uygulanıyor (punto iki kez ölçeklenirdi)
                f = new Font(ad, Math.Max(1f, boyut * 96f / 72f), stil, GraphicsUnit.Pixel);
                yaziTipleri[k] = f;
            }
            return f;
        }

        static void Yaz(Graphics g, string metin, float boyut, FontStyle stil, Color renk, RectangleF r,
                        StringAlignment yatay = StringAlignment.Near, bool satirKaydir = false,
                        StringAlignment dikey = StringAlignment.Center)
        {
            if (string.IsNullOrEmpty(metin))
                return;
            using var f = new SolidBrush(renk);
            using var bicim = new StringFormat
            {
                Alignment = yatay,
                LineAlignment = dikey,
                Trimming = satirKaydir ? StringTrimming.EllipsisWord : StringTrimming.EllipsisCharacter,
                FormatFlags = satirKaydir ? 0 : StringFormatFlags.NoWrap,
            };
            g.DrawString(metin, YaziTipi("Segoe UI", boyut, stil), f, r, bicim);
        }

        static void Ikon(Graphics g, string karakter, float boyut, Color renk, RectangleF r)
        {
            using var f = new SolidBrush(renk);
            using var bicim = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(karakter, YaziTipi(IkonYaziTipi, boyut, FontStyle.Regular), f, r, bicim);
        }

        static bool YaziTipiVarMi(string ad)
        {
            try
            {
                using var f = new Font(ad, 10f);
                return string.Equals(f.Name, ad, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                kapak?.Dispose();
                bulanik?.Dispose();
                animasyon.Dispose();
                kutuYazisi?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
