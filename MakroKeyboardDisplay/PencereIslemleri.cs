using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Uygulama 3.2: pencere işleri.
    /// BT: bir program zaten açıksa penceresini öne getirir (yoksa açılır).
    /// CD: öndeki pencereyi ekranın yarısına, üçte birine ya da tamamına yerleştirir.
    /// </summary>
    static class PencereIslemleri
    {
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int komut);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int en, int boy, bool yenile);
        [DllImport("user32.dll")] static extern IntPtr GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint konuk, uint hedef, bool bagla);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);

        const int SW_RESTORE = 9, SW_MAXIMIZE = 3;

        /// <summary>Pencereyi öne getirir (küçültülmüşse büyütür). Windows'un odak kilidini aşmak için iş parçacığı bağlar.</summary>
        public static bool OneGetir(IntPtr pencere)
        {
            if (pencere == IntPtr.Zero) return false;
            try
            {
                if (IsIconic(pencere)) ShowWindow(pencere, SW_RESTORE);
                uint bizim = GetCurrentThreadId();
                uint onkiler = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
                if (onkiler != 0 && onkiler != bizim) AttachThreadInput(bizim, onkiler, true);
                bool oldu = SetForegroundWindow(pencere);
                if (onkiler != 0 && onkiler != bizim) AttachThreadInput(bizim, onkiler, false);
                return oldu;
            }
            catch { return false; }
        }

        /// <summary>Verilen .exe yolu için açık bir ana pencere bulur; yoksa IntPtr.Zero.</summary>
        public static IntPtr PencereBul(string yol)
        {
            try
            {
                string ad = System.IO.Path.GetFileNameWithoutExtension(yol ?? "");
                if (ad == "") return IntPtr.Zero;
                foreach (var p in Process.GetProcessesByName(ad))
                {
                    try
                    {
                        if (p.MainWindowHandle != IntPtr.Zero && IsWindowVisible(p.MainWindowHandle))
                            return p.MainWindowHandle;
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch (Exception ex) { Gunluk.Hata("pencere bul", ex); }
            return IntPtr.Zero;
        }

        // ------------------------------------------------------------------ CD: yerleşim
        public static readonly (string kod, string ad)[] Yerlesimler =
        {
            ("sol", "Sol yarı"), ("sag", "Sağ yarı"), ("ust", "Üst yarı"), ("alt", "Alt yarı"),
            ("sol3", "Sol üçte bir"), ("orta3", "Orta üçte bir"), ("sag3", "Sağ üçte bir"),
            ("sol23", "Sol üçte iki"), ("sag23", "Sağ üçte iki"),
            ("tam", "Tam ekran"), ("orta", "Ortala"),
        };

        public static string YerlesimAdi(string kod)
        {
            foreach (var (k, ad) in Yerlesimler) if (k == kod) return ad;
            return "Yerleşim";
        }

        /// <summary>Öndeki pencereyi verilen yerleşime taşır.</summary>
        public static bool Yerlestir(string kod)
        {
            var p = GetForegroundWindow();
            if (p == IntPtr.Zero) return false;
            try
            {
                var sinif = new StringBuilder(64);
                GetClassName(p, sinif, sinif.Capacity);
                string sn = sinif.ToString();
                if (sn == "Progman" || sn == "WorkerW" || sn == "Shell_TrayWnd") return false;   // masaüstü / görev çubuğu
                var alan = Screen.FromHandle(p).WorkingArea;
                if (IsIconic(p)) ShowWindow(p, SW_RESTORE);
                if (kod == "tam") { ShowWindow(p, SW_MAXIMIZE); return true; }
                ShowWindow(p, SW_RESTORE);
                var r = Hesapla(kod, alan);
                return MoveWindow(p, r.X, r.Y, r.Width, r.Height, true);
            }
            catch (Exception ex) { Gunluk.Hata("pencere yerleştir", ex); return false; }
        }

        static Rectangle Hesapla(string kod, Rectangle a)
        {
            int ucte = a.Width / 3;
            switch (kod)
            {
                case "sol": return new Rectangle(a.X, a.Y, a.Width / 2, a.Height);
                case "sag": return new Rectangle(a.X + a.Width / 2, a.Y, a.Width - a.Width / 2, a.Height);
                case "ust": return new Rectangle(a.X, a.Y, a.Width, a.Height / 2);
                case "alt": return new Rectangle(a.X, a.Y + a.Height / 2, a.Width, a.Height - a.Height / 2);
                case "sol3": return new Rectangle(a.X, a.Y, ucte, a.Height);
                case "orta3": return new Rectangle(a.X + ucte, a.Y, ucte, a.Height);
                case "sag3": return new Rectangle(a.X + 2 * ucte, a.Y, a.Width - 2 * ucte, a.Height);
                case "sol23": return new Rectangle(a.X, a.Y, 2 * ucte, a.Height);
                case "sag23": return new Rectangle(a.X + ucte, a.Y, a.Width - ucte, a.Height);
                case "orta":
                    int en = (int)(a.Width * 0.62), boy = (int)(a.Height * 0.82);
                    return new Rectangle(a.X + (a.Width - en) / 2, a.Y + (a.Height - boy) / 2, en, boy);
                default: return a;
            }
        }
    }
}
