using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Uygulama 3.1: bir yolun (program, kısayol, klasör ya da "shell:AppsFolder\..." girdisi)
    /// Windows'taki simgesini resim olarak verir. Mağaza uygulamaları ve klasörler de çalışır.
    /// </summary>
    static class WindowsSimgesi
    {
        const uint SHGFI_ICON = 0x100, SHGFI_LARGEICON = 0, SHGFI_PIDL = 8;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SHGetFileInfo(string yol, uint oznitelik, ref SHFILEINFO bilgi, uint boy, uint bayrak);

        [DllImport("shell32.dll")]
        static extern IntPtr SHGetFileInfo(IntPtr pidl, uint oznitelik, ref SHFILEINFO bilgi, uint boy, uint bayrak);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHParseDisplayName(string ad, IntPtr baglam, out IntPtr pidl, uint istenen, out uint oznitelik);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        static Bitmap Cevir(IntPtr hIcon)
        {
            if (hIcon == IntPtr.Zero) return null;
            try
            {
                using var ikon = Icon.FromHandle(hIcon);
                return ikon.ToBitmap();
            }
            finally { DestroyIcon(hIcon); }
        }

        public static Bitmap Al(string hedef)
        {
            string yol = (hedef ?? "").Trim().Trim('"');
            if (yol == "") return null;
            try { yol = Environment.ExpandEnvironmentVariables(yol); } catch { }
            if (yol.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                yol.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return null;                       // site: simgesi yok

            try
            {
                if (File.Exists(yol) || Directory.Exists(yol))
                {
                    var bilgi = new SHFILEINFO();
                    if (SHGetFileInfo(yol, 0, ref bilgi, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON) != IntPtr.Zero)
                    {
                        var b = Cevir(bilgi.hIcon);
                        if (b != null) return b;
                    }
                    if (File.Exists(yol))
                        using (var ik = Icon.ExtractAssociatedIcon(yol))
                            if (ik != null) return ik.ToBitmap();
                    return null;
                }
                // "shell:AppsFolder\..." ve diğer kabuk ögeleri
                if (SHParseDisplayName(yol, IntPtr.Zero, out IntPtr pidl, 0, out _) == 0 && pidl != IntPtr.Zero)
                {
                    try
                    {
                        var bilgi = new SHFILEINFO();
                        if (SHGetFileInfo(pidl, 0, ref bilgi, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_PIDL | SHGFI_ICON | SHGFI_LARGEICON) != IntPtr.Zero)
                            return Cevir(bilgi.hIcon);
                    }
                    finally { Marshal.FreeCoTaskMem(pidl); }
                }
            }
            catch (Exception ex) { Gunluk.Hata("windows simgesi", ex); }
            return null;
        }
    }
}
