using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Bildirim alanında (saatin yanında) simge; tıklayınca küçük panel açılır (KlavyePaneli).
    /// Yarım saniyede bir çalan medyayı ve ses seviyesini okur, değişince klavyeye gönderir.
    /// </summary>
    public sealed class TepsiUygulamasi : ApplicationContext
    {
        const string BaslangicAnahtari = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string BaslangicAdi = "MakroKeyboardDisplay";

        readonly NotifyIcon simge;
        readonly ContextMenuStrip menu;
        readonly ToolStripMenuItem mDurum, mCalan, mModlar, mBaslangic;
        readonly System.Windows.Forms.Timer zamanlayici;
        readonly KlavyeBaglantisi klavye = new KlavyeBaglantisi(Ayarlar.Oku("SonPort"));
        readonly UygulamaSesi uygulamaSesi = new UygulamaSesi();
        readonly MedyaIzleyici medya = new MedyaIzleyici();
        readonly SesSeviyesi ses = new SesSeviyesi();
        readonly Control arayuz;          // arka plan olaylarını arayüz iş parçacığına taşımak için

        // klavyeye son gönderilenler (değişmeyeni tekrar göndermemek için)
        string sonAnahtar;
        char sonDurum;
        double sonKonum;
        DateTime sonKonumZamani;
        int sonSes = -1;
        bool sonSessiz;
        bool okuyor;
        int kapakDenemesi;
        // Windows kapağı başlıktan biraz sonra günceller: şarkı değişince bu anlarda (sn) kapak yeniden okunur,
        // değiştiyse panel ve renkler yenilenir (önceki şarkının kapağı kalmasın)
        static readonly double[] KapakKontrolleri = { 0.5, 1.2, 2.5, 5, 10 };
        long gonderilenKapakOzeti, pencereKapakOzeti;
        int kapakKontrol, pencereKapakKontrol;
        DateTime sarkiBaslangic, pencereSarkiBaslangic;
        MedyaBilgisi simdiki;
        string[] modAdlari = new string[0];
        int aktifMod = -1;
        readonly KlavyePaneli pencere;
        string pencereKapakAnahtari;
        string kartSurum = "";
        int bosBellek = -1, enAzBellek = -1;   // kart 1.12+: boş bellek (bayt), PING cevabıyla gelir
        bool bellekIstendi;                    // bu bağlantıda açılış bellek dökümü istendi mi
        string ekranSayfasi = "";
        bool ilkBaglanti = true;

        // Mod düzenleme (kart 1.11+)
        System.Collections.Generic.List<ModTanimi> modTanimlari;
        readonly System.Text.StringBuilder modParcalari = new();
        int modParcaSirasi = -1;
        bool modOkunuyor;
        DateTime modOkumaZamani;
        string modKayit = "";

        // Işık (kart 1.9+)
        LedAyari ledAyari;                 // klavyedeki son ayar
        LedAyari bekleyenLed;              // panelde değişti, henüz gönderilmedi
        DateTime sonLedGonderimi = DateTime.MinValue;
        Color[] kapakRenkleri;             // çalan şarkının kapağından 2-4 renk
        string sonGidenKapakRenkleri;
        readonly System.Windows.Forms.Timer hizliZamanlayici;
        double solSeviye, sagSeviye;
        readonly SesAnalizi analiz = new SesAnalizi();     // uygulama 2.9: 8 bantlı gerçek frekans analizi (kart 2.7+)
        DateTime sonEq = DateTime.MinValue, sonAnalizDenemesi = DateTime.MinValue;
        bool eqSifirdi = true;
        readonly byte[] eqBantlar = new byte[8];
        bool kapakGidiyor;                 // kapak satırları gönderilirken ses seviyesi (LV) gönderilmez

        // Şarkı değişince hemen okuma ve hız ölçümü (hiz.txt)
        bool tekrarOku;                    // okurken yeni değişiklik geldi: bitince bir daha oku
        DateTime sonMedyaOlayi = DateTime.MinValue;
        System.Diagnostics.Stopwatch sarkiSaati;
        string hizSarki = "";
        long hizFarkEtme = -1, hizKapakAlindi = -1, hizHazir = -1, hizGonderildi = -1;
        bool hizBekliyor;
        DateTime sonOlcum = DateTime.MinValue;

        // Klavye ekranı ayarları, saat ve sistem bilgisi (kart 1.13+)
        int[] ekranAyari;                  // parlaklık, bayraklar, ses ışığı (klavyedeki son ayar)
        int[] bekleyenEkran;               // panelde değişti, henüz gönderilmedi
        DateTime sonEkranGonderimi = DateTime.MinValue, ekranIstemeZamani = DateTime.MinValue;
        readonly SistemBilgisi sistem = new SistemBilgisi();
        DateTime sonSis = DateTime.MinValue;
        bool sisOkunuyor;
        int sonSaatDakika = -1;
        static readonly System.Globalization.CultureInfo Turkce = new System.Globalization.CultureInfo("tr-TR");

        // Panel kısayolu (uygulama 2.3)
        PanelKisayolu kisayol;
        int sonMik = -1;
        bool modlarTazelendi;              // bu bağlantıda modlar kendiliğinden yeniden yazıldı mı (kart 1.19 uzun adları)
        DateTime sonLvGonderimi = DateTime.MinValue;   // kart 1.17+: ses çubukları için seviye                   // uygulama 2.4: klavyeye son bildirilen mikrofon durumu (1 kapalı, 0 açık)

        // Kart 1.6'dan önce modların tuş adları gelmez: modlar.py'deki varsayılanlar gösterilir
        System.Collections.Generic.Dictionary<int, string[]> modTuslari = new();
        System.Collections.Generic.Dictionary<int, Color> modRenkleri = new();
        static readonly string[][] VarsayilanTuslar =
        {
            new[] { "Önceki", "Oynat / Dur", "Sonraki", "Sessiz", "Durdur", "Oynatıcı" },
            new[] { "Kopyala", "Yapıştır", "Kes", "Geri al", "Yinele", "Kaydet" },
            new[] { "Masaüstü", "Dosya gezgini", "Ekran al", "Görev yön.", "Pencere", "Kilitle" },
            new[] { "F13", "F14", "F15", "F16", "F17", "F18" },
        };
        static readonly Color[] VarsayilanRenkler =
            { Color.FromArgb(255, 40, 120), Color.FromArgb(0, 160, 255), Color.FromArgb(255, 140, 0), Color.FromArgb(90, 230, 60) };

        public TepsiUygulamasi()
        {
            arayuz = new Control();
            _ = arayuz.Handle;            // pencere tutamağını şimdi oluştur (BeginInvoke için gerekli)

            menu = new ContextMenuStrip();
            mDurum = new ToolStripMenuItem("Klavye aranıyor…") { Enabled = false };
            mCalan = new ToolStripMenuItem("Çalan bir şey yok") { Enabled = false };
            mModlar = new ToolStripMenuItem("Klavye modu");
            mBaslangic = new ToolStripMenuItem("Windows açılınca başlat", null, (s, e) => BaslangicDegistir()) { Checked = BaslangictaMi() };
            menu.Items.Add(new ToolStripMenuItem("Paneli aç", null, (s, e) => pencere.Goster()) { Font = new Font(menu.Font, FontStyle.Bold) });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(mDurum);
            menu.Items.Add(mCalan);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(mModlar);
            menu.Items.Add(new ToolStripMenuItem("Klavyeyi yeniden ara", null, (s, e) => klavye.YenidenAra()));
            menu.Items.Add(mBaslangic);
            menu.Items.Add(new ToolStripMenuItem("Hata kayıtlarını aç", null, (s, e) => HataKlasoru()));
            menu.Items.Add(new ToolStripMenuItem("Sürüm " + Program.Surum + " ") { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Çıkış", null, (s, e) => Cik()));
            ModMenusunuKur();

            simge = new NotifyIcon
            {
                Icon = UygulamaSimgesi(),
                Text = "Makro Klavye: aranıyor…",
                ContextMenuStrip = menu,
                Visible = true,
            };

            simge.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) pencere.AcKapa(); };
            pencere = new KlavyePaneli(simge.Icon);
            pencere.YenidenAraIstendi += () => klavye.YenidenAra();
            pencere.HataKayitlariIstendi += HataKlasoru;
            pencere.YazilimGuncelleIstendi += YazilimGuncelle;        // uygulama 3.0
            pencere.HazirSurumuYukleIstendi += () =>                  // uygulama 3.2 (CM)
            {
                if (!KartGuncelleme.Calisiyor && KartGuncelleme.HazirDosya != null)
                    _ = KartGuncelleme.Yukle(KartGuncelleme.HazirDosya, klavye);
            };
            pencere.OzelSimgelerDegisti += () => _ = OzelSimgeleriGonder();   // uygulama 3.1
            KartGuncelleme.Degisti += () => { try { pencere?.BeginInvoke((Action)pencere.Invalidate); } catch { } };
            pencere.CikisIstendi += Cik;
            pencere.BaslangictaMi = BaslangictaMi;
            pencere.BaslangicDegistir = BaslangicDegistir;
            pencere.MedyaKomutu += async k =>
            {
                try { await medya.KomutAsync(k); }
                catch (Exception ex) { Gunluk.Hata("medya komutu", ex); }
            };
            pencere.KonumIstendi += async oran =>
            {
                try { await medya.KonumaGitAsync(oran); }
                catch (Exception ex) { Gunluk.Hata("konum", ex); }
            };
            pencere.SesIstendi += seviye => ses.Ayarla(seviye);
            pencere.SessizIstendi += () => ses.SessizDegistir();
            pencere.ModIstendi += no =>
            {
                if (!klavye.Bagli) return;
                klavye.Gonder("MOD", no);
                aktifMod = no;
                ModMenusunuKur();
            };
            pencere.LedAyarIstendi += l =>
            {
                ledAyari = l;
                bekleyenLed = l;            // hızlı zamanlayıcı en çok 8 kez/sn gönderir (kaydırıcı sürüklenirken)
            };
            pencere.EkranAyarIstendi += v =>
            {
                ekranAyari = v;
                bekleyenEkran = v;          // hızlı zamanlayıcı gönderir (kaydırıcı sürüklenirken en çok ~7 kez/sn)
            };
            pencere.SesOlcumu = () => ((DateTime.UtcNow - sonOlcum).TotalSeconds < 0.5, solSeviye, sagSeviye);
            pencere.ModlarKaydet += liste =>
            {
                if (!klavye.Bagli || !KartEnAz(1, 11))
                {
                    modKayit = "hata:Klavye bağlı değil";
                    return;
                }
                ModlariYaz(liste);
            };
            pencere.BellekSifirlaIstendi += () =>
            {
                if (klavye.Bagli) klavye.Gonder("BELLEKSIFIRLA");
            };
            pencere.ModlarSifirla += () =>
            {
                if (!klavye.Bagli || !KartEnAz(1, 11)) return;
                klavye.Gonder("MODSIFIRLA");
                modKayit = "gidiyor";
            };
            // genel kısayol: paneli her yerden aç / kapat (varsayılan Ctrl + Alt + M)
            kisayol = new PanelKisayolu();
            kisayol.Basildi += () => pencere.AcKapa();
            pencere.PanelKisayoluKullanimda = !kisayol.Ayarla(pencere.PanelKisayoluAyari);
            pencere.PanelKisayoluIstendi += ayar =>
            {
                pencere.PanelKisayoluKullanimda = !kisayol.Ayarla(ayar);
                pencere.Invalidate();
            };

            // uygulama 2.5: tuş testi, ayar geçmişi
            pencere.TusTestiIstendi += acik =>
            {
                if (klavye.Bagli && KartEnAz(1, 20)) klavye.Gonder("TEST", acik ? 1 : 0);
            };
            pencere.GecmisYukleIstendi += liste =>
            {
                if (!klavye.Bagli || !KartEnAz(1, 11)) return;
                ModlariYaz(liste);
            };

            pencere.SayfaIstendi += sayfa =>
            {
                if (!klavye.Bagli) return;
                klavye.Gonder("SAYFA", sayfa);
                ekranSayfasi = sayfa;          // kart 1.6 onaylar; eskisi bilmez
            };

            // ikinci kez çalıştırılırsa bu pencere öne gelsin
            var sinyal = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Program.GosterSinyali);
            new System.Threading.Thread(() =>
            {
                while (sinyal.WaitOne())
                    Arayuzde(() => pencere.Goster());
            })
            { IsBackground = true }.Start();

            // yeni bir kopya açılırsa (ör. yeniden derlendi) bu kopya kapanıp yerini ona bırakır
            var kapat = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Program.KapatSinyali);
            new System.Threading.Thread(() =>
            {
                if (kapat.WaitOne())
                    Arayuzde(Cik);
            })
            { IsBackground = true }.Start();

            klavye.BaglantiDegisti += b => Arayuzde(() => BaglantiDegisti(b));
            klavye.MesajGeldi += a => Arayuzde(() => MesajGeldi(a));
            medya.Degisti += () =>
            {
                sonMedyaOlayi = DateTime.UtcNow;
                Arayuzde(HemenOku);                        // yarım saniyelik yoklamayı beklemeden
            };

            zamanlayici = new System.Windows.Forms.Timer { Interval = 500 };
            zamanlayici.Tick += async (s, e) => await Tik();
            zamanlayici.Start();
            hizliZamanlayici = new System.Windows.Forms.Timer { Interval = 50 };
            hizliZamanlayici.Tick += (s, e) => HizliTik();
            hizliZamanlayici.Start();
            klavye.Baslat();

            simge.ShowBalloonTip(4000, "Makro Klavye Ekran çalışıyor",
                "Simgesi saatin yanında (gizli simgeler ^). Simgeye tıklayınca panel açılır.", ToolTipIcon.Info);
        }

        void Arayuzde(Action is_)
        {
            try
            {
                if (arayuz.IsHandleCreated && !arayuz.IsDisposed)
                    arayuz.BeginInvoke(is_);
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------ klavye olayları
        void BaglantiDegisti(bool bagli)
        {
            // yeniden bağlanınca her şeyi baştan gönder
            sonAnahtar = null;
            sonSes = -1;
            sonDurum = '\0';
            kapakDenemesi = 0;
            sonGidenKapakRenkleri = null;
            modTanimlari = null;
            modOkunuyor = false;
            simgelerGonderildi = false;          // uygulama 3.1
            bellekIstendi = false;
            modParcaSirasi = -1;
            ekranAyari = null;
            bekleyenEkran = null;
            sonSaatDakika = -1;
            sonMik = -1;
            modlarTazelendi = false;
            if (modKayit == "gidiyor") modKayit = "hata:Bağlantı koptu";
            if (bagli)
            {
                klavye.Gonder("MODLAR");
                klavye.Gonder("LEDOKU");                     // kart 1.9+ "LED ..." ile cevap verir
                Ayarlar.Yaz("SonPort", klavye.PortAdi);      // bir dahaki açılışta önce bu port denenir
            }
            else
            {
                aktifMod = -1;
                ekranSayfasi = "";
            }
            DurumuYaz();

            // Bağlantı bildirimi (ayarlardan kapatılabilir); uygulama açılırken ilk bağlantıda değil
            if (!ilkBaglanti && pencere.BaglantiBildirimi)
                simge.ShowBalloonTip(2500, "Makro Klavye",
                    bagli ? $"Klavye bağlandı ({klavye.PortAdi})." : "Klavye bağlantısı koptu.",
                    bagli ? ToolTipIcon.Info : ToolTipIcon.Warning);
            ilkBaglanti = false;
        }

        void MesajGeldi(string[] a)
        {
            switch (a[0])
            {
                case "MKD2":
                    if (a.Length >= 2) kartSurum = a[1];
                    if (a.Length >= 4) ekranSayfasi = a[3];        // kart 1.6+
                    if (a.Length >= 6 && int.TryParse(a[4], out int bb) && int.TryParse(a[5], out int eb))
                    {
                        bosBellek = bb;                            // kart 1.12+
                        enAzBellek = eb;
                        if (!bellekIstendi)
                        {
                            bellekIstendi = true;
                            Gunluk.Bellek($"--- {DateTime.Now:yyyy-MM-dd HH:mm:ss} kart {kartSurum} ---");
                            klavye.Gonder("BELLEK");               // açılış adımlarının bellek dökümü -> bellek.txt
                        }
                    }
                    if (!simgelerGonderildi && KartEnAz(3, 1))      // uygulama 3.1: çizdiğin simgeler karta
                    {
                        simgelerGonderildi = true;
                        _ = OzelSimgeleriGonder();
                    }
                    if (modTanimlari == null && (!modOkunuyor || (DateTime.UtcNow - modOkumaZamani).TotalSeconds > 6) && KartEnAz(1, 11))
                    {
                        modOkunuyor = true;
                        modOkumaZamani = DateTime.UtcNow;
                        klavye.Gonder("MODOKU");                   // kart 1.11+: modların tamamı (MODJ parçaları)
                    }
                    if (ekranAyari == null && KartEnAz(1, 13) && (DateTime.UtcNow - ekranIstemeZamani).TotalSeconds > 3)
                    {
                        ekranIstemeZamani = DateTime.UtcNow;
                        klavye.Gonder("EKRANOKU");                 // kart 1.13+: "EKRANAYAR parlaklık bayraklar ses_ışığı"
                    }
                    if (a.Length >= 3 && int.TryParse(a[2], out int m) && m != aktifMod)
                    {
                        aktifMod = m;
                        ModMenusunuKur();
                    }
                    break;
                case "MODTUS":                                     // kart 1.6+: no, renk, 6 tuş adı
                    if (a.Length >= 3 && int.TryParse(a[1], out int mt))
                    {
                        modTuslari[mt] = a.Skip(3).ToArray();
                        if (a[2].Length == 6 && int.TryParse(a[2], System.Globalization.NumberStyles.HexNumber, null, out int rgb))
                            modRenkleri[mt] = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
                    }
                    break;
                case "SAYFA":
                    if (a.Length >= 2) ekranSayfasi = a[1];
                    break;
                case "MODLAR":
                    modAdlari = a.Skip(1).ToArray();
                    ModMenusunuKur();
                    break;
                case "MOD":
                    if (a.Length >= 2 && int.TryParse(a[1], out int y))
                    {
                        aktifMod = y;
                        ModMenusunuKur();
                    }
                    break;
                case "SES":
                    UygulamaSesiDegistir(a);
                    break;
                case "MODJ":                                       // kart 1.11+: sıra, toplam, JSON parçası
                    if (a.Length >= 4 && int.TryParse(a[1], out int ji) && int.TryParse(a[2], out int jn))
                    {
                        if (ji == 0) { modParcalari.Clear(); modParcaSirasi = 0; }
                        if (ji != modParcaSirasi) { modParcaSirasi = -1; modOkunuyor = false; break; }
                        modParcalari.Append(string.Join("\t", a.Skip(3)));
                        modParcaSirasi++;
                        if (ji == jn - 1)
                        {
                            try
                            {
                                var ham = modParcalari.ToString();
                                modTanimlari = ModTanimi.Coz(ham);
                                // kart 1.19+: uzun basma komutlarının ekrandaki adları eski kayıtlarda yok;
                                // bağlantı başına bir kez, kullanıcı bir şey yapmadan yeniden yazılır
                                if (!modlarTazelendi && KartEnAz(1, 19)
                                    && modTanimlari.Exists(m => Array.Exists(m.Tuslar, t => !string.IsNullOrEmpty(t.UzunKod)))
                                    && ModTanimi.Json(modTanimlari) != ham)
                                {
                                    modlarTazelendi = true;
                                    ModlariYaz(modTanimlari);
                                }
                            }
                            catch (Exception ex) { Gunluk.Hata("modlar okunamadı", ex); }
                            modParcaSirasi = -1;
                            modOkunuyor = false;
                        }
                    }
                    break;
                case "MODYAZ":                                     // kart 1.11+: TAMAM n / HATA mesaj
                    if (a.Length >= 2 && a[1] == "TAMAM")
                    {
                        modKayit = "tamam";
                        modTanimlari = null;                       // yeniden oku: kartta ne varsa o görünsün
                        modOkunuyor = true;
                        modOkumaZamani = DateTime.UtcNow;
                        klavye.Gonder("MODOKU");
                    }
                    else
                        modKayit = "hata:" + (a.Length >= 3 ? a[2] : "bilinmeyen hata");
                    break;
                case "LED":                                        // kart 1.9+: efekt, renk, parlaklık, hız, akış renkleri
                    if (bekleyenLed == null)
                        ledAyari = LedAyari.Coz(a);
                    break;
                case "EKRANAYAR":                                  // kart 1.13+: parlaklık, bayraklar, ses ışığı
                    if (a.Length >= 4 && bekleyenEkran == null && int.TryParse(a[1], out int ep)
                        && int.TryParse(a[2], out int eb2) && int.TryParse(a[3], out int es))
                    {
                        // kart 1.20+: gece başı, gece sonu, gece parlaklığı
                        if (a.Length >= 8 && int.TryParse(a[4], out int h1) && int.TryParse(a[5], out int h2)
                            && int.TryParse(a[6], out int h3) && int.TryParse(a[7], out int h4))
                            ekranAyari = new[] { ep, eb2, es, h1, h2, h3, h4 };   // kart 3.1+: uzun basış
                        else if (a.Length >= 7 && int.TryParse(a[4], out int g1) && int.TryParse(a[5], out int g2) && int.TryParse(a[6], out int g3))
                            ekranAyari = new[] { ep, eb2, es, g1, g2, g3 };
                        else
                            ekranAyari = new[] { ep, eb2, es };
                    }
                    break;
                case "KARTHATA":                                   // kart 1.21+: klavyenin döngüsünde yakalanan son hata (sayı|metin)
                    if (a.Length >= 2)
                        Gunluk.Bellek("KART HATASI: " + string.Join(" ", a.Skip(1)));
                    break;
                case "BELLEK":                                     // kart 1.12+: adım, boş bayt
                    if (a.Length >= 3)
                        Gunluk.Bellek($"{a[1],-40} {a[2],8} bayt boş");
                    break;
                case "ARTOK":                                      // kart 1.12+: kapak çizildi (ilk satırdan beri ms)
                    if (hizBekliyor && sarkiSaati != null && a.Length >= 2)
                    {
                        hizBekliyor = false;
                        string Ms(long v) => v < 0 ? "-" : v + " ms";
                        Gunluk.Hiz($"{DateTime.Now:HH:mm:ss}  {hizSarki,-40}  fark etme {(hizFarkEtme < 0 ? "yoklamayla" : hizFarkEtme + " ms")} · " +
                                   $"kapak alındı {Ms(hizKapakAlindi)} · hazır {Ms(hizHazir)} · gönderildi {Ms(hizGonderildi)} · " +
                                   $"klavyede çizildi {sarkiSaati.ElapsedMilliseconds} ms (klavye {a[1]} ms)");
                    }
                    break;
                case "TUS":                                        // tuşa basıldı: no (1-6), mod
                    if (a.Length >= 3 && int.TryParse(a[1], out int tusNo) && int.TryParse(a[2], out int tusModu))
                    {
                        bool tekrarBasis = a.Length >= 4 && a[3] == "R";             // kart 1.22+: "R" uzun komutun tekrarı
                        bool uzunBasis = a.Length >= 4 && (a[3] == "U" || tekrarBasis);  // kart 1.14+: "U" uzun basış
                        if (!tekrarBasis) IstatistigeYaz(tusModu, tusNo - 1, uzunBasis);
                        TusaBasildi(tusModu, tusNo - 1, uzunBasis, tekrarBasis);
                    }
                    break;
                case "BAS":                                        // kart 1.20+: tuş testi: tuş (0-5, 6 sol, 7 sağ tekerlek), 1/0
                    if (a.Length >= 3 && int.TryParse(a[1], out int basNo) && int.TryParse(a[2], out int basD))
                        pencere.TestOlayi("BAS", basNo, basD);
                    break;
                case "DIZIADIM":                                   // kart 2.7+: makroda uygulamanın yapacağı adım (metin, uygulama aç...)
                    if (a.Length >= 2)
                    {
                        try { KodCalistir(string.Join("\t", a.Skip(1))); }
                        catch (Exception ex) { Gunluk.Hata("makro adımı", ex); }
                        klavye.Gonder("DIZIOK");
                    }
                    break;
                case "ENC":                                        // tekerlek çevrildi: L/R, +1/-1
                    if (a.Length >= 3 && int.TryParse(a[2], out int encD))
                    {
                        pencere.TestOlayi("ENC", a[1] == "L" ? 0 : 1, encD);
                        Istatistik.Teker(a[1] == "L");
                    }
                    break;
            }
        }

        /// <summary>Tuşa "Uygulama aç" atanmışsa (kod "APP:yol") programı / klasörü / siteyi açar.</summary>
        void TusaBasildi(int modNo, int tus, bool uzun = false, bool tekrar = false)
        {
            if (modTanimlari == null || modNo < 0 || modNo >= modTanimlari.Count || tus < 0 || tus > 5)
                return;
            var kod = uzun ? modTanimlari[modNo].Tuslar[tus].UzunKod : modTanimlari[modNo].Tuslar[tus].Kod;
            if (string.IsNullOrEmpty(kod))
                return;
            KodCalistir(kod, tekrar);
        }

        /// <summary>Uygulamanın yaptığı komutlar: mikrofon, bilgisayar komutları, renk seçici, metin yaz, uygulama aç. Makro adımları da buradan geçer.</summary>
        void KodCalistir(string kod, bool tekrar = false)
        {
            if (KomutKatalogu.PencereMi(kod))                   // 3.2 (CD): pencere yerleşimi
            {
                PencereIslemleri.Yerlestir(KomutKatalogu.PencereKodu(kod));
                return;
            }
            if (kod == "MIC_MUTE")                              // uygulama 2.4: mikrofonu kapat / aç
            {
                if (ses.MikrofonDegistir(out bool kapali))
                    MikrofonuBildir(kapali, true);
                else
                    simge.ShowBalloonTip(2500, "Makro Klavye", "Mikrofon bulunamadı.", ToolTipIcon.Warning);
                return;
            }
            if (kod.StartsWith("PC_"))                          // uygulama 2.7: kapat / yeniden başlat / uyku / ekranı kapat
            {
                if (!tekrar) BilgisayarKomutu(kod);
                return;
            }
            if (kod == "COLOR_PICK")                            // uygulama 2.4: imlecin altındaki renk
            {
                RenkSec();
                return;
            }
            if (KomutKatalogu.MetinMi(kod))                     // uygulama 2.3: "Metin yaz"
            {
                var metin = KomutKatalogu.MetinCoz(kod, out bool enter);
                MetinYazici.Yaz(metin, enter);
                return;
            }
            if (!KomutKatalogu.UygulamaMi(kod))
                return;
            var hedef = KomutKatalogu.UygulamaHedefi(kod);
            try
            {
                if (KomutKatalogu.OneGetirMi(kod))          // 3.2 (BT): açıksa öne getir
                {
                    var pencere = PencereIslemleri.PencereBul(hedef);
                    if (pencere != IntPtr.Zero && PencereIslemleri.OneGetir(pencere)) return;
                }
                if (hedef.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                    System.Diagnostics.Process.Start("explorer.exe", hedef);          // Store uygulamaları (shell:AppsFolder\...)
                else
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(hedef) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Gunluk.Hata("uygulama aç: " + hedef, ex);
                simge.ShowBalloonTip(3000, "Makro Klavye", "Açılamadı: " + hedef, ToolTipIcon.Warning);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr pencere, int mesaj, IntPtr w, IntPtr l);

        bool kapanisBekliyor;
        DateTime kapanisZamani;

        static void Calistir(string dosya, string arg) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dosya, arg)
            { UseShellExecute = false, CreateNoWindow = true });

        /// <summary>
        /// Uygulama 2.7: bilgisayar komutları. Kapat / yeniden başlat 10 sn sonra olur; bu sürede
        /// aynı tuşa (ya da diğerine) tekrar basılırsa iptal edilir. Uyku ve ekranı kapatma yarım saniye
        /// bekler ki tuşu bırakmak bilgisayarı hemen geri uyandırmasın.
        /// </summary>
        void BilgisayarKomutu(string kod)
        {
            try
            {
                switch (kod)
                {
                    case "PC_SHUTDOWN":
                    case "PC_RESTART":
                        if (kapanisBekliyor && (DateTime.UtcNow - kapanisZamani).TotalSeconds < 10)
                        {
                            Calistir("shutdown.exe", "/a");
                            kapanisBekliyor = false;
                            simge.ShowBalloonTip(2500, "Makro Klavye", "İptal edildi.", ToolTipIcon.Info);
                            return;
                        }
                        bool yeniden = kod == "PC_RESTART";
                        Calistir("shutdown.exe", (yeniden ? "/r" : "/s") + " /t 10");
                        kapanisBekliyor = true;
                        kapanisZamani = DateTime.UtcNow;
                        simge.ShowBalloonTip(5000, "Makro Klavye",
                            (yeniden ? "Bilgisayar 10 sn içinde yeniden başlayacak." : "Bilgisayar 10 sn içinde kapanacak.")
                            + " Vazgeçmek için tuşa tekrar bas.", ToolTipIcon.Warning);
                        break;
                    case "PC_SLEEP":
                        Task.Delay(600).ContinueWith(_ => Application.SetSuspendState(PowerState.Suspend, false, false));
                        break;
                    case "PC_MONITOR_OFF":
                        Task.Delay(600).ContinueWith(_ => PostMessage((IntPtr)0xFFFF, 0x0112, (IntPtr)0xF170, (IntPtr)2));
                        break;
                }
            }
            catch (Exception ex)
            {
                Gunluk.Hata("bilgisayar komutu: " + kod, ex);
                simge.ShowBalloonTip(3000, "Makro Klavye", "Yapılamadı: " + KomutKatalogu.Ad(kod), ToolTipIcon.Warning);
            }
        }

        /// <summary>Klavye "SES +1 n" / "SES -1 n" / "SES 0" / "SES MUTE" gönderir; çalan uygulamanın sesi değişir.</summary>
        /// Ses oturumu arayüzleri MTA iş parçacığında kullanılmalı (arayüz iş parçacığı STA'dır).
        /// Bu yüzden istekler sırayla, ayrı bir MTA iş parçacığında işlenir.
        readonly System.Collections.Concurrent.BlockingCollection<Action> sesIsleri = new();
        System.Threading.Thread sesIsParcacigi;

        void SesIsiEkle(Action is_)
        {
            if (sesIsParcacigi == null)
            {
                sesIsParcacigi = new System.Threading.Thread(() =>
                {
                    foreach (var i in sesIsleri.GetConsumingEnumerable())
                    {
                        try { i(); }
                        catch (Exception ex) { Gunluk.Hata("uygulama sesi", ex); }
                    }
                })
                { IsBackground = true, Name = "Uygulama sesi" };
                sesIsParcacigi.SetApartmentState(System.Threading.ApartmentState.MTA);
                sesIsParcacigi.Start();
            }
            sesIsleri.Add(is_);
        }

        void UygulamaSesiDegistir(string[] a)
        {
            if (a.Length < 2)
                return;
            int adim = 0;
            bool mute = a[1] == "MUTE";
            if (!mute)
            {
                int.TryParse(a[1], out int yon);
                int n = 1;
                if (a.Length >= 3) int.TryParse(a[2], out n);
                adim = Math.Sign(yon) * Math.Clamp(n, 1, 10);
            }
            var kimlik = medya.SonKaynakKimligi;
            SesIsiEkle(() =>
            {
                int seviye = uygulamaSesi.Degistir(kimlik, adim, mute, out bool sessiz, out string ad);
                klavye.Gonder("AVOL", seviye, sessiz ? 1 : 0, string.IsNullOrEmpty(ad) ? "Uygulama" : ad);
            });
        }

        // ------------------------------------------------------------------ ışık
        static bool SesliEfekt(string e) => e == "ritim" || e == "stereo";

        /// <summary>20 kez/sn: bekleyen ışık ayarını gönderir; Ritim/Stereo efektinde ses seviyesini ölçüp yollar.</summary>
        void HizliTik()
        {
            var simdi = DateTime.UtcNow;
            bool kart19 = klavye.Bagli && KartEnAz(1, 9);
            if (bekleyenLed != null && kart19 && (simdi - sonLedGonderimi).TotalMilliseconds >= 120)
            {
                klavye.Gonder(bekleyenLed.Alanlar());
                if (KartEnAz(1, 10))
                    klavye.Gonder(bekleyenLed.TusAlanlari());     // tuşların kendi efekti
                bekleyenLed = null;
                sonLedGonderimi = simdi;
            }
            if (bekleyenEkran != null && klavye.Bagli && KartEnAz(1, 13) && (simdi - sonEkranGonderimi).TotalMilliseconds >= 150)
            {
                if (bekleyenEkran.Length >= 7)
                    klavye.Gonder("EKRANAYAR", bekleyenEkran[0], bekleyenEkran[1], bekleyenEkran[2], bekleyenEkran[3],
                                  bekleyenEkran[4], bekleyenEkran[5], bekleyenEkran[6]);
                else if (bekleyenEkran.Length >= 6)
                    klavye.Gonder("EKRANAYAR", bekleyenEkran[0], bekleyenEkran[1], bekleyenEkran[2], bekleyenEkran[3], bekleyenEkran[4], bekleyenEkran[5]);
                else
                    klavye.Gonder("EKRANAYAR", bekleyenEkran[0], bekleyenEkran[1], bekleyenEkran[2]);
                bekleyenEkran = null;
                sonEkranGonderimi = simdi;
            }

            // kart 2.7+: müzik çalarken gerçek frekans bantları (saniyede ~18 kez)
            if (kart19 && KartEnAz(2, 7) && simdiki?.Durum == 'P' && !kapakGidiyor)
            {
                if (!analiz.Calisiyor && (simdi - sonAnalizDenemesi).TotalSeconds > 4)
                {
                    sonAnalizDenemesi = simdi;
                    analiz.Baslat();
                }
                if (analiz.Calisiyor && (simdi - sonEq).TotalMilliseconds >= 55)
                {
                    sonEq = simdi;
                    bool taze = analiz.Oku(eqBantlar);
                    bool hepsiSifir = true;
                    foreach (var b in eqBantlar) if (b > 0) { hepsiSifir = false; break; }
                    if (!(hepsiSifir && eqSifirdi))
                        klavye.Gonder("EQ", eqBantlar[0], eqBantlar[1], eqBantlar[2], eqBantlar[3], eqBantlar[4], eqBantlar[5], eqBantlar[6], eqBantlar[7]);
                    eqSifirdi = hepsiSifir;
                    _ = taze;
                }
            }
            else if (analiz.Calisiyor)
            {
                analiz.Durdur();                        // durunca ya da klavye gidince dinlemeyi bırak (sonraki çalışta yeniden başlar)
                eqSifirdi = true;
            }

            // kart 1.17+: ses çubukları için seviye her zaman gider; eskilerde yalnızca müziğe göre ışık efektinde
            bool cubuklar = kart19 && KartEnAz(1, 17);
            if (!cubuklar && (ledAyari == null || !SesliEfekt(ledAyari.Efekt)))
                return;
            if (!ses.Tepe(out float sol, out float sag))
                return;
            // tepe değeri desibele çevrilir (kulağın duyduğuna daha yakın); düşüş yavaş, yükseliş anında
            solSeviye = Math.Max(Desibel(sol), solSeviye * 0.82);
            sagSeviye = Math.Max(Desibel(sag), sagSeviye * 0.82);
            sonOlcum = simdi;
            // saniyede en çok 10 kez (klavyeyi yormasın)
            if (kart19 && simdiki?.Durum == 'P' && !kapakGidiyor && (simdi - sonLvGonderimi).TotalMilliseconds >= 95)
            {
                klavye.Gonder("LV", (int)Math.Round(solSeviye * 100), (int)Math.Round(sagSeviye * 100));
                sonLvGonderimi = simdi;
            }
        }

        static double Desibel(float tepe)
        {
            if (tepe <= 0.0001f) return 0;
            return Math.Clamp((20 * Math.Log10(tepe) + 42) / 42, 0, 1);
        }

        /// <summary>Kapak renklerini (ışıklar için) bul; kart 1.9+ ise gönder.</summary>
        async Task KapakRenkleriniAyarla(System.Drawing.Image resim)
        {
            Color[] renkler = null;
            if (resim != null)
            {
                try
                {
                    using var kopya = new Bitmap(resim);
                    renkler = await Task.Run(() => KapakResmi.BaskinRenkler(kopya));
                }
                catch (Exception ex) { Gunluk.Hata("kapak renkleri", ex); }
            }
            kapakRenkleri = renkler;
            KapakRenkleriniGonder();
        }

        void KapakRenkleriniGonder()
        {
            if (!klavye.Bagli || !KartEnAz(1, 9))
                return;
            var alanlar = new System.Collections.Generic.List<object> { "LP" };
            if (kapakRenkleri != null)
                foreach (var c in kapakRenkleri) alanlar.Add($"{c.R:X2}{c.G:X2}{c.B:X2}");
            var metin = string.Join(",", alanlar);
            if (metin == sonGidenKapakRenkleri)
                return;
            klavye.Gonder(alanlar.ToArray());
            sonGidenKapakRenkleri = metin;
        }

        // ------------------------------------------------------------------ ana döngü
        async Task Tik()
        {
            if (okuyor)
                return;
            okuyor = true;
            try
            {
                MedyaBilgisi b = null;
                try { b = await medya.OkuAsync(); }
                catch (Exception ex) { Gunluk.Hata("medya okuma", ex); }
                simdiki = b;

                if (klavye.Bagli)
                {
                    await MedyayiGonder(b);
                    SesiGonder();
                    MikrofonuKontrolEt();
                    await SaatVeSistemGonder();
                }
                DurumuYaz();
                ModSuresiniYaz();
                Istatistik.Kaydet();
                await PencereyiGuncelle(b);
            }
            finally
            {
                okuyor = false;
                if (tekrarOku)
                {
                    tekrarOku = false;
                    Arayuzde(HemenOku);
                }
            }
        }

        void HemenOku()
        {
            if (okuyor)
            {
                tekrarOku = true;
                return;
            }
            _ = Tik();
        }

        async Task MedyayiGonder(MedyaBilgisi b)
        {
            if (b == null)
            {
                if (sonAnahtar != "")
                {
                    klavye.Gonder("N");
                    klavye.Gonder("ARTYOK");
                    sonAnahtar = "";
                    kapakRenkleri = null;
                    KapakRenkleriniGonder();
                }
                return;
            }

            var simdi = DateTime.UtcNow;
            if (b.Anahtar != sonAnahtar)
            {
                klavye.Gonder("M", Kisa(b.Baslik, 80), Kisa(b.Sanatci, 60), Kisa(b.Album, 60), b.Kaynak, b.Durum,
                              Sayi(b.Konum), Sayi(b.Sure));
                sonAnahtar = b.Anahtar;
                sonDurum = b.Durum;
                sonKonum = b.Konum;
                sonKonumZamani = simdi;
                kapakDenemesi = 0;
                kapakKontrol = 0;
                gonderilenKapakOzeti = 0;
                sarkiBaslangic = simdi;
                // hız ölçümü: şarkı değişiminden kapağın klavyede çizilmesine kadar
                sarkiSaati = System.Diagnostics.Stopwatch.StartNew();
                hizSarki = Kisa(b.Baslik, 40);
                var olaydan = (simdi - sonMedyaOlayi).TotalMilliseconds;
                hizFarkEtme = olaydan >= 0 && olaydan < 3000 ? (long)olaydan : -1;
                hizKapakAlindi = hizHazir = hizGonderildi = -1;
                hizBekliyor = KartEnAz(1, 12);
                await KapagiGonder();
                return;
            }

            // kapak bazı oynatıcılarda şarkıdan biraz sonra gelir: birkaç kez daha dene
            if (kapakDenemesi > 0 && kapakDenemesi < 4)
                await KapagiGonder();
            else if (kapakDenemesi == 99 && kapakKontrol < KapakKontrolleri.Length
                     && (simdi - sarkiBaslangic).TotalSeconds >= KapakKontrolleri[kapakKontrol])
            {
                kapakKontrol++;
                await KapagiGonder(true);      // kapak sonradan değiştiyse yenile
            }

            // ilerleme: durum değiştiyse, atlama yapıldıysa ya da 5 sn'de bir
            double bekleneni = sonKonum + (sonDurum == 'P' ? (simdi - sonKonumZamani).TotalSeconds : 0);
            if (b.Durum != sonDurum || Math.Abs(b.Konum - bekleneni) > 2 || (simdi - sonKonumZamani).TotalSeconds > 5)
            {
                klavye.Gonder("POS", Sayi(b.Konum), Sayi(b.Sure), b.Durum);
                sonDurum = b.Durum;
                sonKonum = b.Konum;
                sonKonumZamani = simdi;
            }
        }

        /// <summary>kontrol: şarkı aynı, kapak sonradan değişti mi diye bakılıyor (değişmediyse hiçbir şey yapmaz)</summary>
        async Task KapagiGonder(bool kontrol = false)
        {
            if (!kontrol) kapakDenemesi++;
            System.Drawing.Image resim = null;
            long ozet = 0;
            try { (resim, ozet) = await medya.KapakOzetliAsync(); }
            catch (Exception ex) { Gunluk.Hata("kapak", ex); }
            if (kontrol && (resim == null || ozet == gonderilenKapakOzeti))
            {
                resim?.Dispose();
                return;
            }
            if (resim == null)
            {
                if (kapakDenemesi == 1)
                {
                    klavye.Gonder("ARTYOK");
                    kapakRenkleri = null;
                    KapakRenkleriniGonder();
                }
                // bazı oynatıcılar kapağı şarkıdan biraz sonra verir: yarım saniyelik yoklamayı beklemeden yeniden dene
                if (kapakDenemesi < 4)
                    _ = Task.Delay(300).ContinueWith(_ => Arayuzde(HemenOku));
                return;
            }
            if (sarkiSaati != null && !kontrol) hizKapakAlindi = sarkiSaati.ElapsedMilliseconds;
            gonderilenKapakOzeti = ozet;
            using (resim)
            {
                pencere.KapakAyarla(resim);
                pencereKapakAnahtari = sonAnahtar;
                if (KartEnAz(2, 4))
                {
                    // kart 2.4+ (C++): bellek bol; kapak tam renkli (RGB565) gönderilir, klavyede ses çubuklarıyla birlikte görünür
                    var satirlar = await Task.Run(() => KapakResmi.Satirlar(resim));
                    if (sarkiSaati != null) hizHazir = sarkiSaati.ElapsedMilliseconds;
                    klavye.Gonder("ART", KapakResmi.Boyut, KapakResmi.Boyut);
                    kapakGidiyor = true;
                    try
                    {
                        for (int y = 0; y < satirlar.Count; y++)
                        {
                            klavye.Gonder("AR", y, satirlar[y]);
                            if (y % 24 == 23) await Task.Delay(10);
                        }
                    }
                    finally
                    {
                        kapakGidiyor = false;
                    }
                    if (sarkiSaati != null) hizGonderildi = sarkiSaati.ElapsedMilliseconds;
                }
                else if (KartEnAz(1, 17))
                {
                    // kart 1.17+: kapak resmi gönderilmez (klavyenin belleği); yalnızca renkleri gider (ses çubukları ve ışıklar)
                }
                else if (KartEnAz(1, 7))
                {
                    // kart 1.7+: 256 renkli paletle yarı boyutta gönderilir (görünüş aynı, daha hızlı)
                    string palet = null;
                    var satirlar = await Task.Run(() => KapakResmi.PaletliSatirlar(resim, out palet));
                    if (sarkiSaati != null) hizHazir = sarkiSaati.ElapsedMilliseconds;
                    klavye.Gonder("ART", KapakResmi.Boyut, KapakResmi.Boyut);
                    if (KartEnAz(1, 12))
                    {
                        // kart 1.12+: palet 64 renklik 4 parça (tek uzun satır kartın belleğini zorluyordu)
                        var pb = Convert.FromBase64String(palet);
                        for (int i = 0; i < pb.Length; i += 192)
                            klavye.Gonder("ARP", i / 3, Convert.ToBase64String(pb, i, Math.Min(192, pb.Length - i)));
                    }
                    else
                        klavye.Gonder("ARP", palet);
                    kapakGidiyor = true;
                    try
                    {
                        // kart satırları işleyebilsin diye 12 satırda bir kısa ara (hepsi birden gidince kart yetişemiyordu)
                        for (int y = 0; y < satirlar.Count; y++)
                        {
                            klavye.Gonder("AR8", y, satirlar[y]);
                            if (y % 12 == 11) await Task.Delay(25);
                        }
                    }
                    finally
                    {
                        kapakGidiyor = false;
                    }
                    if (sarkiSaati != null) hizGonderildi = sarkiSaati.ElapsedMilliseconds;
                }
                else
                {
                    var satirlar = KapakResmi.Satirlar(resim);
                    klavye.Gonder("ART", KapakResmi.Boyut, KapakResmi.Boyut);
                    for (int y = 0; y < satirlar.Count; y++)
                        klavye.Gonder("AR", y, satirlar[y]);
                }
                // ışıklar için kapak renkleri: kapak gittikten sonra (kapağı geciktirmesin)
                await KapakRenkleriniAyarla(resim);
            }
            kapakDenemesi = 99;      // gönderildi, tekrar deneme
        }

        async Task PencereyiGuncelle(MedyaBilgisi b)
        {
            if (!pencere.Visible)
                return;
            int seviye = -1;
            bool sessiz = false;
            if (!ses.Oku(out seviye, out sessiz))
                seviye = -1;
            var d = new PanelDurumu
            {
                Bagli = klavye.Bagli,
                Port = klavye.Bagli ? klavye.PortAdi : (klavye.SonPort ?? ""),
                KartSurum = kartSurum ?? "",
                Medya = b,
                Ses = seviye,
                Sessiz = sessiz,
                ModAdlari = modAdlari,
                AktifMod = aktifMod,
                Sayfa = ekranSayfasi ?? "",
                Led = ledAyari,
                LedDestek = klavye.Bagli && KartEnAz(1, 9),
                LedTusDestek = klavye.Bagli && KartEnAz(1, 10),
                ModTanimlari = modTanimlari,
                ModDuzenDestek = klavye.Bagli && KartEnAz(1, 11),
                ModKayit = modKayit,
                EkranAyar = klavye.Bagli ? ekranAyari : null,
                MikKapali = klavye.Bagli && sonMik == 1,
                EkranDestek = klavye.Bagli && KartEnAz(1, 13),
                BosBellek = klavye.Bagli ? bosBellek : -1,
                EnAzBellek = klavye.Bagli ? enAzBellek : -1,
                KapakRenkleri = kapakRenkleri,
                ModTuslari = new string[modAdlari.Length][],
                ModRenkleri = new Color[modAdlari.Length],
            };
            for (int i = 0; i < modAdlari.Length; i++)
            {
                d.ModTuslari[i] = modTuslari.TryGetValue(i, out var t) ? t : (i < VarsayilanTuslar.Length ? VarsayilanTuslar[i] : new string[0]);
                d.ModRenkleri[i] = modRenkleri.TryGetValue(i, out var r) ? r : VarsayilanRenkler[i % VarsayilanRenkler.Length];
            }
            pencere.Guncelle(d);
            // klavye bağlıyken kapağı KapagiGonder koyar; bağlı değilken burada (o da sonradan değişirse yenilenir)
            var anahtar = b?.Anahtar ?? "";
            bool yeni = anahtar != pencereKapakAnahtari;
            if (yeni)
            {
                pencereKapakAnahtari = anahtar;
                pencereSarkiBaslangic = DateTime.UtcNow;
                pencereKapakKontrol = 0;
            }
            else if (klavye.Bagli || b == null || pencereKapakKontrol >= KapakKontrolleri.Length
                     || (DateTime.UtcNow - pencereSarkiBaslangic).TotalSeconds < KapakKontrolleri[pencereKapakKontrol])
                return;
            else
                pencereKapakKontrol++;
            System.Drawing.Image resim = null;
            long ozet = 0;
            if (b != null)
            {
                try { (resim, ozet) = await medya.KapakOzetliAsync(); } catch (Exception) { }
            }
            if (!yeni && ozet == pencereKapakOzeti)
            {
                resim?.Dispose();
                return;
            }
            pencereKapakOzeti = ozet;
            pencere.KapakAyarla(resim);
            if (!klavye.Bagli) await KapakRenkleriniAyarla(resim);
            resim?.Dispose();
        }

        void SesiGonder()
        {
            if (!ses.Oku(out int seviye, out bool sessiz))
                return;
            if (seviye != sonSes || sessiz != sonSessiz)
            {
                klavye.Gonder("VOL", seviye, sessiz ? 1 : 0);
                sonSes = seviye;
                sonSessiz = sessiz;
            }
        }

        DateTime sonModOlcumu = DateTime.UtcNow;

        /// <summary>uygulama 2.5: hangi modda ne kadar kalındı (klavye bağlıyken, yarım saniyede bir)</summary>
        void ModSuresiniYaz()
        {
            var simdi = DateTime.UtcNow;
            double sn = (simdi - sonModOlcumu).TotalSeconds;
            sonModOlcumu = simdi;
            if (klavye.Bagli && aktifMod >= 0 && aktifMod < modAdlari.Length)
                Istatistik.ModSuresi(modAdlari[aktifMod], sn);
        }

        /// <summary>uygulama 2.5: kullanım istatistiği için basılan tuşun adı.</summary>
        void IstatistigeYaz(int modNo, int tus, bool uzun)
        {
            if (tus < 0 || tus > 5) return;
            string mod = modNo >= 0 && modNo < modAdlari.Length ? modAdlari[modNo] : "?";
            string ad = "";
            if (modTanimlari != null && modNo >= 0 && modNo < modTanimlari.Count)
            {
                var t = modTanimlari[modNo].Tuslar[tus];
                ad = uzun ? KomutKatalogu.KisaAd(t.UzunKod) : (string.IsNullOrWhiteSpace(t.Ad) ? KomutKatalogu.Ad(t.Kod) : t.Ad);
            }
            else if (modTuslari.TryGetValue(modNo, out var adlar) && tus < adlar.Length)
                ad = adlar[tus];
            Istatistik.Tus(mod, ad, uzun);
        }

        /// <summary>Modların tamamını klavyeye yazar: JSON 160 harflik parçalar (MODYAZ sıra toplam parça); kart son parçada doğrulayıp kaydeder.</summary>
        void ModlariYaz(System.Collections.Generic.List<ModTanimi> liste)
        {
            AyarGecmisi.Sakla(modTanimlari);       // uygulama 2.5: klavyedeki önceki hâl geçmişe
            var json = ModTanimi.Json(liste);
            const int Parca = 160;
            int n = Math.Max(1, (json.Length + Parca - 1) / Parca);
            for (int i = 0; i < n; i++)
                klavye.Gonder("MODYAZ", i, n, json.Substring(i * Parca, Math.Min(Parca, json.Length - i * Parca)));
            modKayit = "gidiyor";
        }

        /// <summary>Kart 1.14+: mikrofon durumu (tuşu kırmızı yanar, üst şeritte "MİK"). goster: altta şerit çıksın.</summary>
        void MikrofonuBildir(bool kapali, bool goster)
        {
            int v = kapali ? 1 : 0;
            if (!klavye.Bagli || !KartEnAz(1, 14))
                return;
            if (v == sonMik && !goster)
                return;
            klavye.Gonder("MIC", v, goster ? 1 : 0);
            sonMik = v;
        }

        void MikrofonuKontrolEt()
        {
            if (!klavye.Bagli || !KartEnAz(1, 14))
                return;
            if (!ses.MikrofonOku(out bool kapali))
                return;
            int v = kapali ? 1 : 0;
            if (v == sonMik)
                return;
            // ilk bildirimde şerit yok; sonra başka yerden (Discord, Windows) değişince şerit çıksın
            klavye.Gonder("MIC", v, sonMik < 0 ? 0 : 1);
            sonMik = v;
        }

        /// <summary>İmlecin altındaki rengi "#RRGGBB" olarak panoya kopyalar; kart 1.14+ ekranda ve ışıkta gösterir.</summary>
        void RenkSec()
        {
            try
            {
                var p = Cursor.Position;
                using var bmp = new Bitmap(1, 1);
                using (var g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(p.X, p.Y, 0, 0, new Size(1, 1));
                var c = bmp.GetPixel(0, 0);
                var hex = $"{c.R:X2}{c.G:X2}{c.B:X2}";
                Clipboard.SetText("#" + hex);
                if (klavye.Bagli && KartEnAz(1, 14))
                    klavye.Gonder("RENK", hex);
                else
                    simge.ShowBalloonTip(2000, "Renk kopyalandı", "#" + hex, ToolTipIcon.Info);
            }
            catch (Exception ex) { Gunluk.Hata("renk seçici", ex); }
        }

        /// <summary>Kart 1.13+: dakika değişince saat (boştayken ekranda), tuş sayfası açıkken 2 sn'de bir sistem bilgisi.</summary>
        async Task SaatVeSistemGonder()
        {
            if (!KartEnAz(1, 13))
                return;
            var simdi = DateTime.Now;
            int dakika = simdi.Hour * 60 + simdi.Minute;
            if (dakika != sonSaatDakika)
            {
                sonSaatDakika = dakika;
                klavye.Gonder("SAAT", simdi.Hour, simdi.Minute, simdi.Second, ((int)simdi.DayOfWeek + 6) % 7,
                              simdi.ToString("d MMMM", Turkce));
            }
            bool sistemAcik = ekranAyari == null || (ekranAyari[1] & 8) != 0;
            if ((!sistemAcik && ekranSayfasi != "sistem") || (ekranSayfasi != "tuslar" && ekranSayfasi != "sistem") || kapakGidiyor || sisOkunuyor
                || (DateTime.UtcNow - sonSis).TotalSeconds < 1.9)
                return;
            sonSis = DateTime.UtcNow;
            sisOkunuyor = true;
            try
            {
                var (cpu, ram, gpu, ind, yuk, pil) = await Task.Run(() => sistem.Oku());
                if (klavye.Bagli && ram >= 0)
                {
                    if (KartEnAz(1, 20))       // kart 1.20+: ikinci satır: internet (kb/s) ve pil (%)
                        klavye.Gonder("SIS", Math.Max(0, cpu), ram, gpu, ind, yuk, pil);
                    else
                        klavye.Gonder("SIS", Math.Max(0, cpu), ram, gpu);
                }
            }
            catch (Exception ex) { Gunluk.Hata("sistem bilgisi", ex); }
            finally { sisOkunuyor = false; }
        }

        /// <summary>Bağlı kartın yazılımı en az bu sürüm mü (MKD2 cevabındaki sürüm).</summary>
        bool KartEnAz(int ana, int alt)
        {
            var p = (kartSurum ?? "").Split('.');
            if (p.Length < 2 || !int.TryParse(p[0], out int a) || !int.TryParse(p[1], out int b))
                return false;
            return a > ana || (a == ana && b >= alt);
        }

        static string Sayi(double d) => Math.Round(d, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        static string Kisa(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? (s ?? "") : s.Substring(0, n);

        // ------------------------------------------------------------------ menü ve simge
        void ModMenusunuKur()
        {
            mModlar.DropDownItems.Clear();
            if (modAdlari.Length == 0)
            {
                mModlar.DropDownItems.Add(new ToolStripMenuItem(klavye.Bagli ? "okunuyor…" : "klavye bağlı değil") { Enabled = false });
                return;
            }
            for (int i = 0; i < modAdlari.Length; i++)
            {
                int no = i;
                var oge = new ToolStripMenuItem($"{i + 1}. {modAdlari[i]}", null, (s, e) => klavye.Gonder("MOD", no))
                {
                    Checked = i == aktifMod,
                };
                mModlar.DropDownItems.Add(oge);
            }
        }

        void DurumuYaz()
        {
            string ust = klavye.Bagli ? $"Klavye bağlı ({klavye.PortAdi})"
                : "Klavye aranıyor…" + (klavye.SonPort != null ? $" (önce {klavye.SonPort} deneniyor)" : "");
            mDurum.Text = ust;
            mModlar.Enabled = klavye.Bagli;
            string calan = simdiki == null ? "Çalan bir şey yok"
                : $"{(simdiki.Durum == 'P' ? "▶" : "❚❚")} {simdiki.Baslik}" + (simdiki.Sanatci != "" ? $" – {simdiki.Sanatci}" : "")
                  + (simdiki.Kaynak != "" ? $"  ({simdiki.Kaynak})" : "");
            mCalan.Text = Kisa(calan, 90);
            var ipucu = "Makro Klavye: " + (klavye.Bagli ? "bağlı" : "aranıyor…");
            if (simdiki != null)
                ipucu += "\n" + simdiki.Baslik + (simdiki.Sanatci != "" ? " – " + simdiki.Sanatci : "");
            simge.Text = Kisa(ipucu, 120);
        }

        static Icon UygulamaSimgesi()
        {
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; }
            catch { return SystemIcons.Application; }
        }

        static bool BaslangictaMi()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(BaslangicAnahtari);
                return k?.GetValue(BaslangicAdi) is string;
            }
            catch { return false; }
        }

        void BaslangicDegistir()
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(BaslangicAnahtari);
                if (BaslangictaMi())
                    k.DeleteValue(BaslangicAdi, false);
                else
                    k.SetValue(BaslangicAdi, "\"" + Application.ExecutablePath + "\"");
            }
            catch (Exception ex)
            {
                Gunluk.Hata("başlangıç", ex);
            }
            mBaslangic.Checked = BaslangictaMi();
        }

        bool simgeGidiyor, simgelerGonderildi;
        /// <summary>Uygulama 3.1: kullanıcının çizdiği simgeleri karta yazar (kart 3.1+).</summary>
        async Task OzelSimgeleriGonder()
        {
            if (simgeGidiyor || !klavye.Bagli || !KartEnAz(3, 1)) return;
            simgeGidiyor = true;
            try
            {
                var liste = OzelSimge.Hepsi.ToList();
                klavye.Gonder("SIMBAS", liste.Count);
                await Task.Delay(120);
                foreach (var s in liste)
                {
                    klavye.Gonder("SIMAD", s.Ad);
                    var veri = s.Paketle();
                    for (int ofs = 0; ofs < veri.Length; ofs += 96)      // 96 bayt -> 128 harf base64
                    {
                        int k = Math.Min(96, veri.Length - ofs);
                        klavye.Gonder("SIMP", ofs, Convert.ToBase64String(veri, ofs, k));
                        await Task.Delay(8);
                    }
                    klavye.Gonder("SIMTAM");
                    await Task.Delay(20);
                }
                klavye.Gonder("SIMBIT");
                Gunluk.Baglanti($"özel simgeler gönderildi: {liste.Count}");
            }
            catch (Exception ex) { Gunluk.Hata("özel simgeler gönderilemedi", ex); }
            finally { simgeGidiyor = false; }
        }

        /// <summary>Uygulama 3.0: .uf2 seç, kartı güncelleme moduna al, kopyala.</summary>
        void YazilimGuncelle()
        {
            if (KartGuncelleme.Calisiyor) return;
            using var d = new OpenFileDialog
            {
                Title = "Kart yazılımı (.uf2) seç",
                Filter = "Kart yazılımı (*.uf2)|*.uf2",
                CheckFileExists = true,
            };
            var son = Ayarlar.Oku("SonUf2", "") ?? "";
            try { if (son != "" && Directory.Exists(Path.GetDirectoryName(son))) d.InitialDirectory = Path.GetDirectoryName(son); } catch { }
            if (d.ShowDialog() != DialogResult.OK) return;
            Ayarlar.Yaz("SonUf2", d.FileName);
            _ = KartGuncelleme.Yukle(d.FileName, klavye);
        }

        static void HataKlasoru()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Gunluk.Klasor);
                System.Diagnostics.Process.Start("explorer.exe", Gunluk.Klasor);
            }
            catch { }
        }

        void Cik()
        {
            zamanlayici.Stop();
            hizliZamanlayici.Stop();
            simge.Visible = false;
            pencere.Dispose();
            Istatistik.Kaydet(true);
            analiz.Dispose();
            klavye.Dispose();
            sistem.Dispose();
            kisayol?.Dispose();
            simge.Dispose();
            ExitThread();
        }
    }
}