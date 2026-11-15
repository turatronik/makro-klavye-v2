using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MakroKeyboardDisplay
{
    // 2.4: Hatırlatıcılar ve Windows bildirimleri klavyenin belleği için kaldırıldı; bu dosyada yalnızca panel kısayolu kaldı.

    /// <summary>Paneli her yerden açıp kapatan genel kısayol (RegisterHotKey). Varsayılan Ctrl + Alt + M.</summary>
    public sealed class PanelKisayolu : NativeWindow, IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr pencere, int no, uint degistirici, uint tus);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr pencere, int no);

        public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;
        const int WM_HOTKEY = 0x0312, No = 0x4D4B;
        public const string Varsayilan = "3|77";        // Ctrl + Alt + M
        bool kayitli;
        public event Action Basildi;

        public PanelKisayolu() => CreateHandle(new CreateParams());

        /// <summary>Ayar: "değiştiriciler|tuş" ya da "" (kapalı). Kaydedilemezse (başka program kullanıyor) false.</summary>
        public bool Ayarla(string ayar)
        {
            if (kayitli)
            {
                UnregisterHotKey(Handle, No);
                kayitli = false;
            }
            if (!Coz(ayar, out uint mod, out uint tus))
                return true;
            kayitli = RegisterHotKey(Handle, No, mod | MOD_NOREPEAT, tus);
            return kayitli;
        }

        public static bool Coz(string ayar, out uint mod, out uint tus)
        {
            mod = tus = 0;
            var p = (ayar ?? "").Split('|');
            return p.Length == 2 && uint.TryParse(p[0], out mod) && uint.TryParse(p[1], out tus) && tus != 0 && mod != 0;
        }

        public static string Yazisi(string ayar)
        {
            if (!Coz(ayar, out uint mod, out uint tus)) return "kapalı";
            var p = new List<string>();
            if ((mod & MOD_CONTROL) != 0) p.Add("Ctrl");
            if ((mod & MOD_ALT) != 0) p.Add("Alt");
            if ((mod & MOD_SHIFT) != 0) p.Add("Shift");
            if ((mod & MOD_WIN) != 0) p.Add("Win");
            p.Add(KomutKatalogu.TusAdi((Keys)tus));
            return string.Join(" + ", p);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && (int)m.WParam == No)
                Basildi?.Invoke();
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (kayitli) UnregisterHotKey(Handle, No);
            kayitli = false;
            DestroyHandle();
        }
    }
}
