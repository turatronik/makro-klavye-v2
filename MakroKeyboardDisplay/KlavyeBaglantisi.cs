using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Makro Klavye v2'nin USB veri portunu bulur, bağlı tutar, satır satır mesaj alıp gönderir.
    /// Klavye iki seri port açar (konsol + veri); "PING" sorusuna "MKD2" diye cevap veren veri portudur.
    /// Tüm işler arka plan iş parçacıklarında yapılır; olaylar da oradan gelir (arayüzde Invoke gerekir).
    /// </summary>
    public sealed class KlavyeBaglantisi : IDisposable
    {
        public event Action<bool> BaglantiDegisti;      // true: bağlandı, false: koptu
        public event Action<string[]> MesajGeldi;      // alanlar (TAB ile ayrılmış)

        public string PortAdi { get; private set; }
        // Kart kapak resmini çizerken birkaç saniye cevap veremeyebilir: 12 sn sessizlik "koptu" sayılır
        const double ZamanAsimi = 12;
        public bool Bagli => port != null && port.IsOpen && (DateTime.UtcNow - sonCevap).TotalSeconds < ZamanAsimi;

        SerialPort port;
        DateTime sonCevap = DateTime.MinValue;
        string sonBasariliPort;
        readonly BlockingCollection<string> gidenler = new BlockingCollection<string>(new ConcurrentQueue<string>());
        readonly Thread yazici, bekci;
        Thread okuyucu;
        volatile bool kapaniyor;
        readonly object kilit = new object();
        static readonly Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>Son bağlanılan port (kaydedilir; açılışta önce o denenir).</summary>
        public string SonPort => sonBasariliPort;

        public KlavyeBaglantisi(string sonPort = null)
        {
            sonBasariliPort = string.IsNullOrWhiteSpace(sonPort) ? null : sonPort;
            yazici = new Thread(YaziciDongusu) { IsBackground = true, Name = "Klavye yazıcı" };
            bekci = new Thread(BekciDongusu) { IsBackground = true, Name = "Klavye bekçi" };
        }

        /// <summary>Olaylara abone olduktan sonra çağır: klavyeyi aramaya başlar.</summary>
        public void Baslat()
        {
            yazici.Start();
            bekci.Start();
        }

        /// <summary>Bir satır kuyruğa ekler. Alanlardaki TAB ve satır sonları boşluğa çevrilir.</summary>
        public void Gonder(params object[] alanlar)
        {
            if (!Bagli)
                return;
            var temiz = alanlar.Select(a => (a?.ToString() ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' '));
            try { gidenler.Add(string.Join("\t", temiz) + "\n"); }
            catch (InvalidOperationException) { }     // kapanıyor
        }

        /// <summary>Hemen yeniden aramaya zorlar (menüden).</summary>
        public void YenidenAra()
        {
            Kapat("yeniden ara");
        }

        int yazmaZamanAsimi;
        DateTime sonTarama = DateTime.MinValue;

        // ------------------------------------------------------------------ iş parçacıkları
        void BekciDongusu()
        {
            while (!kapaniyor)
            {
                try
                {
                    if (port == null || !port.IsOpen)
                    {
                        // 2.8: klavye takılınca hızlı bağlan: son portu yarım saniyede bir dene,
                        // bütün portları (yavaş açılan Bluetooth portları da olabilir) 2 sn'de bir tara
                        if ((DateTime.UtcNow - sonTarama).TotalSeconds >= 2)
                        {
                            sonTarama = DateTime.UtcNow;
                            Ara();
                        }
                        else if (sonBasariliPort != null && SerialPort.GetPortNames().Contains(sonBasariliPort))
                        {
                            ArkaplandaDene(sonBasariliPort);
                        }
                        Thread.Sleep(500);
                        continue;
                    }
                    else
                    {
                        if (gidenler.IsAddingCompleted)
                            return;
                        gidenler.Add("PING\n");
                        if ((DateTime.UtcNow - sonCevap).TotalSeconds > ZamanAsimi)
                            Kapat("klavye 12 sn cevap vermedi");      // çıkarılmış ya da yeniden başlıyor
                    }
                }
                catch (Exception ex)
                {
                    Gunluk.Hata("bekçi", ex);
                }
                Thread.Sleep(2000);
            }
        }

        // 2.8: portlar ayrı ayrı, arka planda denenir. Bluetooth seri portları gibi bazı portlar açılırken
        // 20-30 sn takılıyor; eskiden bütün arama onları bekliyordu (klavye takılınca ~30 sn gecikme buydu).
        readonly ConcurrentDictionary<string, bool> deneniyor = new ConcurrentDictionary<string, bool>();

        void ArkaplandaDene(string ad)
        {
            if (kapaniyor || !deneniyor.TryAdd(ad, true))
                return;                          // bu port zaten deneniyor (takılmış olabilir)
            Task.Run(() =>
            {
                try { if (port == null) Dene(ad); }
                catch (Exception ex) { Gunluk.Hata("port dene", ex); }
                finally { deneniyor.TryRemove(ad, out _); }
            });
        }

        void Ara()
        {
            foreach (var ad in SerialPort.GetPortNames().Distinct())
            {
                if (kapaniyor || port != null)
                    return;
                ArkaplandaDene(ad);
            }
        }

        bool Dene(string ad)
        {
            SerialPort p = null;
            bool istisna = false;
            try
            {
                p = new SerialPort(ad, 115200)
                {
                    DtrEnable = true,           // CircuitPython veri portu DTR olmadan yazmaz
                    RtsEnable = true,
                    ReadTimeout = 300,
                    WriteTimeout = 3000,
                    NewLine = "\n",
                    Encoding = Utf8,
                };
                p.Open();
                p.DiscardInBuffer();
                p.Write("PING\n");
                var bitis = DateTime.UtcNow.AddMilliseconds(1200);
                while (DateTime.UtcNow < bitis)
                {
                    string satir;
                    try { satir = p.ReadLine(); }
                    catch (TimeoutException) { p.Write("PING\n"); continue; }
                    if (satir.TrimEnd('\r').StartsWith("MKD2"))
                    {
                        lock (kilit)
                        {
                            if (port != null)
                            {
                                Birak(p);            // başka bir deneme önce bağlandı
                                return true;
                            }
                            try { portAkisi = p.BaseStream; } catch { portAkisi = null; }
                            cevapsizBas = DateTime.MinValue;
                            port = p;
                            PortAdi = ad;
                            sonBasariliPort = ad;
                            sonCevap = DateTime.UtcNow;
                        }
                        p.ReadTimeout = SerialPort.InfiniteTimeout;
                        okuyucu = new Thread(() => OkuyucuDongusu(p)) { IsBackground = true, Name = "Klavye okuyucu" };
                        okuyucu.Start();
                        Ilet(satir.TrimEnd('\r'));
                        Gunluk.Baglanti($"bağlandı ({ad})");
                        sonDenemeHatasi = null;
                        BaglantiDegisti?.Invoke(true);
                        return true;
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                // 2.8: klavye çıkarılıp takılınca Windows eski (kapanmış) bağlantının tutamacını ancak çöp toplayıcı
                // çalışınca bırakıyor; o zamana kadar port "erişim engellendi" diyor (30-75 sn bekleme buydu)
                istisna = true;
                if (ad == sonBasariliPort) DenemeHatasi(ad, "erişim engellendi (eski bağlantı bırakılıyor)", ex);
                Birak(p);
                p = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                // bu port bizim klavye değil, meşgul ya da açılamadı
                istisna = true;
                if (ad == sonBasariliPort) DenemeHatasi(ad, ex.GetType().Name, ex);
            }
            if (!istisna && ad == sonBasariliPort)
            {
                DenemeHatasi(ad, "cevap yok", null);
                CevapsizKaldi();
            }
            Birak(p);
            return false;
        }

        Stream portAkisi;                    // açık bağlantının alt akışı (klavye çıkarılınca bunu da kapatmak gerekiyor)
        DateTime cevapsizBas = DateTime.MinValue;

        /// <summary>
        /// Klavye çıkarılıp takılınca Windows bazen eski bağlantıyı bırakmıyor: port açılıyor ama cevap gelmiyor;
        /// uygulama yeniden açılınca hemen bağlanıyor. Port 6 sn boyunca açılıp cevap vermezse uygulama kendini
        /// yeniden başlatır (yeni kopya eskisini kapatıp yerine geçer). 2 dakikada en fazla bir kez.
        /// </summary>
        void CevapsizKaldi()
        {
            var simdi = DateTime.UtcNow;
            if (cevapsizBas == DateTime.MinValue) { cevapsizBas = simdi; return; }
            if ((simdi - cevapsizBas).TotalSeconds < 6) return;
            cevapsizBas = DateTime.MinValue;
            try
            {
                var dosya = Path.Combine(Gunluk.Klasor, "son_yeniden_baslatma.txt");
                if (File.Exists(dosya) && (DateTime.Now - File.GetLastWriteTime(dosya)).TotalMinutes < 2)
                    return;
                File.WriteAllText(dosya, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Gunluk.Baglanti($"port açılıyor ama klavye cevap vermiyor: uygulama yeniden başlatılıyor");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false });
            }
            catch (Exception ex) { Gunluk.Hata("yeniden başlatma", ex); }
        }

        string sonDenemeHatasi;

        /// <summary>Son portun neden bağlanamadığı (aynı neden tekrar tekrar yazılmaz).</summary>
        void DenemeHatasi(string ad, string neden, Exception ex)
        {
            if (neden == sonDenemeHatasi) return;
            sonDenemeHatasi = neden;
            Gunluk.Baglanti($"bağlanamadı ({ad}): {neden}" + (ex != null ? " · " + ex.Message : ""));
        }

        static void Birak(SerialPort p, Stream akis = null)
        {
            if (p == null) return;
            try { p.Close(); } catch { }
            try { akis?.Dispose(); } catch { }
            // klavye çıkarılınca SerialPort.Close hata verip Windows tutamacını açık bırakabiliyor: elle kapat
            try
            {
                var f = akis?.GetType().GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance);
                (f?.GetValue(akis) as SafeHandle)?.Dispose();
            }
            catch { }
            try { p.Dispose(); } catch { }
        }

        void OkuyucuDongusu(SerialPort p)
        {
            try
            {
                while (!kapaniyor && p.IsOpen)
                {
                    var satir = p.ReadLine().TrimEnd('\r');
                    if (satir.Length == 0)
                        continue;
                    sonCevap = DateTime.UtcNow;
                    Ilet(satir);
                }
            }
            catch (Exception ex)
            {
                // port kapandı ya da klavye çıkarıldı
                if (ReferenceEquals(p, port))
                    Kapat("okuma: " + ex.GetType().Name + " " + ex.Message);
                return;
            }
            if (ReferenceEquals(p, port))
                Kapat("okuma bitti");
        }

        void Ilet(string satir)
        {
            try { MesajGeldi?.Invoke(satir.Split('\t')); }
            catch (Exception ex) { Gunluk.Hata("mesaj", ex); }
        }

        void YaziciDongusu()
        {
            foreach (var satir in gidenler.GetConsumingEnumerable())
            {
                var p = port;
                if (p == null || !p.IsOpen)
                    continue;
                try
                {
                    var b = Utf8.GetBytes(satir);
                    p.Write(b, 0, b.Length);
                }
                catch (TimeoutException)
                {
                    // kart meşgul (ör. kapak çiziyor): bu satır atlanır, bağlantı kesilmez
                    yazmaZamanAsimi++;
                }
                catch (System.IO.IOException ex) when (ex.HResult == unchecked((int)0x80070079))
                {
                    // "Semafor zaman aşımı" (ERROR_SEM_TIMEOUT): Windows'un zaman aşımı bu şekilde de gelir; kopma değil
                    yazmaZamanAsimi++;
                }
                catch (Exception ex)
                {
                    Kapat("yazma: " + ex.GetType().Name + " " + ex.Message);
                }
            }
        }

        /// <summary>Bağlantıyı kapatır; neden "baglanti.txt"ye yazılır (kopmaları anlamak için).</summary>
        void Kapat(string neden = "")
        {
            SerialPort p;
            lock (kilit)
            {
                p = port;
                port = null;
                PortAdi = null;
            }
            if (p == null)
                return;
            Gunluk.Baglanti($"koptu ({p.PortName}): {neden}" + (yazmaZamanAsimi > 0 ? $" · yazma zaman aşımı {yazmaZamanAsimi}" : ""));
            yazmaZamanAsimi = 0;
            Birak(p, portAkisi);
            portAkisi = null;
            sonDenemeHatasi = null;
            // eski bağlantının Windows tutamacı hemen bırakılsın (yoksa yeniden takınca port bir süre açılmıyor)
            GC.Collect();
            GC.WaitForPendingFinalizers();
            while (gidenler.TryTake(out _)) { }
            BaglantiDegisti?.Invoke(false);
        }

        public void Dispose()
        {
            kapaniyor = true;
            Kapat("uygulama kapanıyor");
            gidenler.CompleteAdding();
        }
    }
}
