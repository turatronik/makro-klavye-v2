using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Uygulama 3.0: kart yazılımını uygulamadan yükler.
    /// Klavyeye "BOOTSEL EVET" gönderilir, kart RPI-RP2 adlı USB bellek olarak açılır,
    /// .uf2 dosyası oraya kopyalanır ve kart kendiliğinden yeniden başlar.
    /// Elle güncelleme moduna almaya (kilit + 1 ve 6. tuş) gerek kalmaz.
    /// </summary>
    public static class KartGuncelleme
    {
        public static string Durum { get; private set; } = "";
        public static bool Calisiyor { get; private set; }
        public static event Action Degisti;

        // UF2 dosyasının ilk iki sihirli sayısı
        const uint Sihir1 = 0x0A324655, Sihir2 = 0x9E5D5157;

        static void Bildir(string s)
        {
            Durum = s;
            try { Degisti?.Invoke(); } catch { }
        }

        // ------------------------------------------------------------------ 3.2 (CM): yeni sürüm var mı
        public static string HazirDosya { get; private set; }     // bulunan .uf2
        public static string HazirSurum { get; private set; } = "";

        /// <summary>Bilinen klasörlerde "MakroKlavye_x.y.uf2" arar; karttakinden yeniyse HazirSurum dolar.</summary>
        public static bool YeniSurumAra(string kartSurumu)
        {
            HazirDosya = null;
            HazirSurum = "";
            try
            {
                var klasorler = new List<string>();
                void Ekle(string k) { if (!string.IsNullOrEmpty(k) && Directory.Exists(k) && !klasorler.Contains(k)) klasorler.Add(k); }
                Ekle(AppDomain.CurrentDomain.BaseDirectory);
                Ekle(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KartYazilimi"));
                var son = Ayarlar.Oku("SonUf2", "");
                if (son != "") { try { Ekle(Path.GetDirectoryName(son)); } catch { } }
                // kaynak klasörü: ...\MakroKeyboardDisplay\bin\... -> ...\KartYazilimi
                var d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                for (int i = 0; i < 5 && d != null; i++, d = d.Parent)
                    Ekle(Path.Combine(d.FullName, "KartYazilimi"));

                foreach (var k in klasorler)
                    foreach (var dosya in Directory.GetFiles(k, "MakroKlavye_*.uf2"))
                    {
                        var s2 = SurumCoz(Path.GetFileNameWithoutExtension(dosya));
                        if (s2 == "" || !Uf2Mi(dosya)) continue;
                        if (Karsilastir(s2, HazirSurum) > 0) { HazirSurum = s2; HazirDosya = dosya; }
                    }
                if (HazirDosya == null) return false;
                return Karsilastir(HazirSurum, kartSurumu ?? "") > 0;
            }
            catch (Exception ex) { Gunluk.Hata("yeni sürüm arama", ex); return false; }
        }

        static string SurumCoz(string ad)
        {
            int i = ad.LastIndexOf('_');
            if (i < 0 || i + 1 >= ad.Length) return "";
            var s2 = ad.Substring(i + 1);
            foreach (var c in s2) if (!char.IsDigit(c) && c != '.') return "";
            return s2;
        }

        static int Karsilastir(string a, string b)
        {
            var pa = (a ?? "").Split('.');
            var pb = (b ?? "").Split('.');
            for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                int va = i < pa.Length && int.TryParse(pa[i], out int x) ? x : 0;
                int vb = i < pb.Length && int.TryParse(pb[i], out int y) ? y : 0;
                if (va != vb) return va - vb;
            }
            return 0;
        }

        public static bool Uf2Mi(string yol)
        {
            try
            {
                using var f = File.OpenRead(yol);
                var b = new byte[8];
                if (f.Read(b, 0, 8) != 8) return false;
                return BitConverter.ToUInt32(b, 0) == Sihir1 && BitConverter.ToUInt32(b, 4) == Sihir2;
            }
            catch { return false; }
        }

        /// <summary>Bootloader'ın USB belleği (RPI-RP2); yoksa null.</summary>
        static string BootSurucusu()
        {
            try
            {
                foreach (var s in DriveInfo.GetDrives())
                {
                    if (s.DriveType != DriveType.Removable) continue;
                    try
                    {
                        if (!s.IsReady) continue;
                        if (File.Exists(Path.Combine(s.RootDirectory.FullName, "INFO_UF2.TXT"))) return s.RootDirectory.FullName;
                    }
                    catch { }      // hazır olmayan / erişilemeyen sürücüler
                }
            }
            catch { }
            return null;
        }

        /// <summary>Kartı güncelleme moduna alır, dosyayı kopyalar. Klavye yeniden başlayınca bağlantı kendiliğinden kurulur.</summary>
        public static async Task<bool> Yukle(string uf2, KlavyeBaglantisi klavye)
        {
            if (Calisiyor) return false;
            Calisiyor = true;
            try
            {
                if (!File.Exists(uf2) || !Uf2Mi(uf2))
                {
                    Bildir("Bu dosya kart yazılımı değil");
                    return false;
                }
                string surucu = BootSurucusu();
                if (surucu == null)
                {
                    if (klavye == null || !klavye.Bagli)
                    {
                        Bildir("Klavye bağlı değil");
                        return false;
                    }
                    Bildir("Klavye güncelleme moduna alınıyor…");
                    klavye.Gonder("BOOTSEL", "EVET");
                    await Task.Delay(1500);
                    klavye.YenidenAra();                     // kopan portun tutamacını bırak
                    var bitis = DateTime.UtcNow.AddSeconds(25);
                    while (DateTime.UtcNow < bitis && surucu == null)
                    {
                        await Task.Delay(400);
                        surucu = BootSurucusu();
                    }
                }
                if (surucu == null)
                {
                    Bildir("Kart güncelleme moduna geçmedi");
                    return false;
                }
                Bildir("Yazılım kopyalanıyor…");
                string hedef = Path.Combine(surucu, Path.GetFileName(uf2));
                try
                {
                    await Task.Run(() => File.Copy(uf2, hedef, true));
                }
                catch (IOException)
                {
                    // son bloklar yazılınca kart hemen yeniden başlar, sürücü kaybolur: bu normal
                }
                Bildir("Yüklendi, klavye yeniden başlıyor…");
                await Task.Delay(5000);
                Bildir("");
                return true;
            }
            catch (Exception ex)
            {
                Gunluk.Hata("kart güncelleme", ex);
                Bildir("Hata: " + ex.Message);
                return false;
            }
            finally
            {
                Calisiyor = false;
                try { Degisti?.Invoke(); } catch { }
            }
        }
    }
}
