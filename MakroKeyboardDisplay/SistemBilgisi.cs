using System;
using System.Runtime.InteropServices;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// İşlemci, bellek ve ekran kartı kullanımı (%). Klavyenin tuş sayfasında alt satırda gösterilir (kart 1.13+).
    /// İşlemci: GetSystemTimes (iki ölçüm arasındaki fark). Bellek: GlobalMemoryStatusEx.
    /// Ekran kartı: Görev Yöneticisi'nin kullandığı "GPU Engine" sayacı (3D motorlarının toplamı); okunamazsa -1.
    /// </summary>
    public sealed class SistemBilgisi : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct FILETIME { public uint Dusuk, Yuksek; public ulong Deger => ((ulong)Yuksek << 32) | Dusuk; }

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetSystemTimes(out FILETIME bos, out FILETIME cekirdek, out FILETIME kullanici);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX durum);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        static extern uint PdhOpenQueryW(string kaynak, IntPtr kullanici, out IntPtr sorgu);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        static extern uint PdhAddEnglishCounterW(IntPtr sorgu, string yol, IntPtr kullanici, out IntPtr sayac);

        [DllImport("pdh.dll")]
        static extern uint PdhCollectQueryData(IntPtr sorgu);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        static extern uint PdhGetFormattedCounterArrayW(IntPtr sayac, uint bicim, ref uint boyut, out uint adet, IntPtr tampon);

        [DllImport("pdh.dll")]
        static extern uint PdhCloseQuery(IntPtr sorgu);

        const uint PDH_FMT_DOUBLE = 0x00000200;
        const uint PDH_FMT_NOCAP100 = 0x00008000;
        const uint PDH_MORE_DATA = 0x800007D2;

        ulong oncekiBos, oncekiToplam;
        long oncekiAlinan = -1, oncekiGonderilen;
        DateTime oncekiAgZamani;
        IntPtr gpuSorgu, gpuSayac;
        bool gpuYok;

        public SistemBilgisi()
        {
            try
            {
                if (PdhOpenQueryW(null, IntPtr.Zero, out gpuSorgu) != 0 ||
                    PdhAddEnglishCounterW(gpuSorgu, @"\GPU Engine(*engtype_3D)\Utilization Percentage", IntPtr.Zero, out gpuSayac) != 0)
                    gpuYok = true;
                else
                    PdhCollectQueryData(gpuSorgu);      // ilk örnek (yüzde iki örnekten hesaplanır)
            }
            catch (Exception) { gpuYok = true; }
        }

        /// <summary>
        /// (işlemci, bellek, ekran kartı) yüzde; indirme, yükleme kilobit/sn; pil yüzde (pil yoksa -1).
        /// Okunamayan -1. İlk çağrıda işlemci 0, internet -1 olabilir.
        /// </summary>
        public (int cpu, int ram, int gpu, int ind, int yuk, int pil) Oku()
        {
            var (ind, yuk) = AgOku();
            int pil = -1;
            try
            {
                var g = System.Windows.Forms.SystemInformation.PowerStatus;
                if ((g.BatteryChargeStatus & System.Windows.Forms.BatteryChargeStatus.NoSystemBattery) == 0
                    && g.BatteryChargeStatus != System.Windows.Forms.BatteryChargeStatus.Unknown && g.BatteryLifePercent <= 1f)
                    pil = (int)Math.Round(g.BatteryLifePercent * 100);
            }
            catch (Exception) { }
            var (cpu, ram, gpu) = Temel();
            return (cpu, ram, gpu, ind, yuk, pil);
        }

        /// <summary>Bütün açık ağ bağlantılarının toplam indirme / yükleme hızı (kilobit/sn).</summary>
        (int ind, int yuk) AgOku()
        {
            try
            {
                long al = 0, gon = 0;
                foreach (var n in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (n.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up
                        || n.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback
                        || n.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                        continue;
                    var st = n.GetIPStatistics();
                    al += st.BytesReceived;
                    gon += st.BytesSent;
                }
                var simdi = DateTime.UtcNow;
                (int, int) sonuc = (-1, -1);
                if (oncekiAlinan >= 0)
                {
                    double sn = (simdi - oncekiAgZamani).TotalSeconds;
                    if (sn > 0.2)
                        sonuc = ((int)Math.Max(0, (al - oncekiAlinan) * 8 / 1000.0 / sn), (int)Math.Max(0, (gon - oncekiGonderilen) * 8 / 1000.0 / sn));
                }
                oncekiAlinan = al;
                oncekiGonderilen = gon;
                oncekiAgZamani = simdi;
                return sonuc;
            }
            catch (Exception) { return (-1, -1); }
        }

        (int cpu, int ram, int gpu) Temel()
        {
            int cpu = -1, ram = -1, gpu = -1;
            try
            {
                if (GetSystemTimes(out var bos, out var cekirdek, out var kullanici))
                {
                    ulong b = bos.Deger, toplam = cekirdek.Deger + kullanici.Deger;     // çekirdek süresi boşta geçeni de içerir
                    ulong db = b - oncekiBos, dt = toplam - oncekiToplam;
                    if (oncekiToplam != 0 && dt > 0)
                        cpu = (int)Math.Round(Math.Clamp(100.0 * (dt - Math.Min(db, dt)) / dt, 0, 100));
                    oncekiBos = b;
                    oncekiToplam = toplam;
                    if (cpu < 0) cpu = 0;
                }
            }
            catch (Exception) { }
            try
            {
                var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (GlobalMemoryStatusEx(ref m)) ram = (int)m.dwMemoryLoad;
            }
            catch (Exception) { }
            if (!gpuYok)
            {
                try { gpu = GpuOku(); }
                catch (Exception) { gpuYok = true; }
            }
            return (cpu, ram, gpu);
        }

        int GpuOku()
        {
            if (PdhCollectQueryData(gpuSorgu) != 0)
                return -1;
            uint boyut = 0;
            uint s = PdhGetFormattedCounterArrayW(gpuSayac, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref boyut, out uint adet, IntPtr.Zero);
            if (s != PDH_MORE_DATA || boyut == 0)
                return adet == 0 && s == 0 ? 0 : -1;
            var tampon = Marshal.AllocHGlobal((int)boyut);
            try
            {
                s = PdhGetFormattedCounterArrayW(gpuSayac, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref boyut, out adet, tampon);
                if (s != 0)
                    return -1;
                // PDH_FMT_COUNTERVALUE_ITEM_W: ad işaretçisi, CStatus (uint), [hizalama], double değer
                int adim = IntPtr.Size == 8 ? 24 : 16;
                int degerYeri = IntPtr.Size == 8 ? 16 : 8;
                double toplam = 0;
                for (int i = 0; i < adet; i++)
                {
                    var p = tampon + i * adim;
                    uint durum = (uint)Marshal.ReadInt32(p + IntPtr.Size);
                    if (durum != 0 && durum != 1) continue;            // PDH_CSTATUS_VALID_DATA / NEW_DATA
                    toplam += BitConverter.Int64BitsToDouble(Marshal.ReadInt64(p + degerYeri));
                }
                return (int)Math.Round(Math.Clamp(toplam, 0, 100));
            }
            finally { Marshal.FreeHGlobal(tampon); }
        }

        public void Dispose()
        {
            if (gpuSorgu != IntPtr.Zero)
            {
                try { PdhCloseQuery(gpuSorgu); } catch (Exception) { }
                gpuSorgu = IntPtr.Zero;
            }
        }
    }
}
