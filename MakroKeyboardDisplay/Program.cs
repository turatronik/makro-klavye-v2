using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MakroKeyboardDisplay
{
    internal static class Program
    {
        public const string GosterSinyali = "MakroKeyboardDisplay_Goster";
        public const string KapatSinyali = "MakroKeyboardDisplay_Kapat";
        /// <summary>Her değişiklikte artır: pencere başlığında ve tepsi menüsünde görünür.</summary>
        public const string Surum = "3.2";

        [STAThread]
        static void Main()
        {
            // Tek kopya: ikinci kez açılırsa sessizce çıkar (seri port zaten ilkinde)
            using var kilit = new Mutex(true, "MakroKeyboardDisplay_TekKopya", out bool ilk);
            if (!ilk)
            {
                // Zaten çalışan bir kopya var. Yeni derlenmiş sürüm olabilir: eskisini kapatıp yerine geç.
                try { using var sinyal = EventWaitHandle.OpenExisting(KapatSinyali); sinyal.Set(); } catch { }
                bool alindi;
                try { alindi = kilit.WaitOne(6000); }
                catch (AbandonedMutexException) { alindi = true; }
                if (!alindi)
                {
                    // eski sürüm kapanmadı (bu özelliği bilmiyor): onun penceresini öne getir
                    try { using var sinyal = EventWaitHandle.OpenExisting(GosterSinyali); sinyal.Set(); } catch { }
                    MessageBox.Show("Makro Klavye Ekran'ın eski bir kopyası hâlâ çalışıyor.\n\n" +
                                    "Saatin yanındaki simgeye sağ tıklayıp Çıkış de (ya da Görev Yöneticisi'nden " +
                                    "MakroKeyboardDisplay'i sonlandır), sonra yeniden aç.",
                                    "Makro Klavye Ekran", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => Gunluk.Hata("arayüz", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Gunluk.Hata("genel", e.ExceptionObject as Exception);
            OzelSimge.Yukle();                 // uygulama 3.1: kullanıcının çizdiği simgeler
            Application.Run(new TepsiUygulamasi());
            try { kilit.ReleaseMutex(); } catch { }
        }
    }

    /// <summary>Hataları %APPDATA%\MakroKeyboardDisplay\hatalar.txt dosyasına yazar.</summary>
    internal static class Gunluk
    {
        static readonly object Kilit = new object();
        public static string Klasor => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MakroKeyboardDisplay");

        /// <summary>Şarkı değişince kapağın klavyeye ulaşma süresi: %APPDATA%\MakroKeyboardDisplay\hiz.txt</summary>
        public static void Hiz(string satir)
        {
            try
            {
                lock (Kilit)
                {
                    Directory.CreateDirectory(Klasor);
                    File.AppendAllText(Path.Combine(Klasor, "hiz.txt"), satir + Environment.NewLine);
                }
            }
            catch { }
        }

        /// <summary>Klavyenin bellek dökümü: %APPDATA%\MakroKeyboardDisplay\bellek.txt</summary>
        public static void Bellek(string satir)
        {
            try
            {
                lock (Kilit)
                {
                    Directory.CreateDirectory(Klasor);
                    File.AppendAllText(Path.Combine(Klasor, "bellek.txt"), satir + Environment.NewLine);
                }
            }
            catch { }
        }

        /// <summary>Bağlantı olayları: %APPDATA%\MakroKeyboardDisplay\baglanti.txt</summary>
        public static void Baglanti(string olay)
        {
            try
            {
                lock (Kilit)
                {
                    Directory.CreateDirectory(Klasor);
                    var dosya = Path.Combine(Klasor, "baglanti.txt");
                    if (File.Exists(dosya) && new FileInfo(dosya).Length > 256 * 1024)
                        File.Delete(dosya);
                    File.AppendAllText(dosya, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {olay}\r\n");
                }
            }
            catch { }
        }

        public static void Hata(string yer, Exception ex)
        {
            try
            {
                lock (Kilit)
                {
                    Directory.CreateDirectory(Klasor);
                    var dosya = Path.Combine(Klasor, "hatalar.txt");
                    if (File.Exists(dosya) && new FileInfo(dosya).Length > 512 * 1024)
                        File.Delete(dosya);
                    File.AppendAllText(dosya, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {yer}: {ex}\r\n\r\n");
                }
            }
            catch { }
        }
    }
}