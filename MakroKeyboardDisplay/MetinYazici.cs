using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Tuşa atanmış metni (kod "TXT:..." / "TXE:...") odaktaki pencereye yazar (uygulama 2.3).
    /// Her harf Unicode olarak gönderilir; klavye düzeninden (Türkçe Q/F) bağımsız, Türkçe harfler de doğru çıkar.
    /// </summary>
    public static class MetinYazici
    {
        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Explicit)]
        struct GIRDI_BIRLIGI
        {
            [FieldOffset(0)] public MOUSEINPUT mi;      // en büyük üye: yapının boyu doğru olsun
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public GIRDI_BIRLIGI u; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint adet, INPUT[] girdiler, int boyut);

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_UNICODE = 0x0004;
        const ushort VK_RETURN = 0x0D;

        static INPUT Tus(ushort vk, ushort harf, uint bayrak) => new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new GIRDI_BIRLIGI { ki = new KEYBDINPUT { wVk = vk, wScan = harf, dwFlags = bayrak } },
        };

        /// <summary>Arka planda yazar (arayüzü bekletmez). Kısa aralıklarla gönderir: bazı programlar hızlı girdide harf kaçırır.</summary>
        public static void Yaz(string metin, bool enter)
        {
            var liste = new List<INPUT>();
            foreach (char c in metin ?? "")
            {
                if (c == '\n')
                {
                    liste.Add(Tus(VK_RETURN, 0, 0));
                    liste.Add(Tus(VK_RETURN, 0, KEYEVENTF_KEYUP));
                }
                else if (c != '\r')
                {
                    liste.Add(Tus(0, c, KEYEVENTF_UNICODE));
                    liste.Add(Tus(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
                }
            }
            if (enter)
            {
                liste.Add(Tus(VK_RETURN, 0, 0));
                liste.Add(Tus(VK_RETURN, 0, KEYEVENTF_KEYUP));
            }
            if (liste.Count == 0)
                return;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    System.Threading.Thread.Sleep(60);          // makro tuşu bırakılsın
                    int boyut = Marshal.SizeOf<INPUT>();
                    for (int i = 0; i < liste.Count; i += 40)
                    {
                        var parca = liste.GetRange(i, Math.Min(40, liste.Count - i)).ToArray();
                        SendInput((uint)parca.Length, parca, boyut);
                        System.Threading.Thread.Sleep(8);
                    }
                }
                catch (Exception ex) { Gunluk.Hata("metin yazma", ex); }
            });
        }
    }
}
