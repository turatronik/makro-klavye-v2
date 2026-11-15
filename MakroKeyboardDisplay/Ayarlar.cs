using System;
using System.Collections.Generic;
using System.IO;

namespace MakroKeyboardDisplay
{
    /// <summary>Basit ayar dosyası: %APPDATA%\MakroKeyboardDisplay\ayarlar.txt (anahtar=değer satırları).</summary>
    public static class Ayarlar
    {
        static readonly string Dosya = Path.Combine(Gunluk.Klasor, "ayarlar.txt");
        static readonly Dictionary<string, string> Degerler = Yukle();

        static Dictionary<string, string> Yukle()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(Dosya))
                    foreach (var satir in File.ReadAllLines(Dosya))
                    {
                        var i = satir.IndexOf('=');
                        if (i > 0) d[satir.Substring(0, i).Trim()] = satir.Substring(i + 1).Trim();
                    }
            }
            catch (Exception ex) { Gunluk.Hata("ayar okuma", ex); }
            return d;
        }

        public static string Oku(string anahtar, string varsayilan = null)
        {
            lock (Degerler)
                return Degerler.TryGetValue(anahtar, out var v) ? v : varsayilan;
        }

        /// <summary>Bütün ayarların kopyası (yedek için).</summary>
        public static List<KeyValuePair<string, string>> Hepsi()
        {
            lock (Degerler)
                return new List<KeyValuePair<string, string>>(Degerler);
        }

        public static void Yaz(string anahtar, string deger)
        {
            try
            {
                lock (Degerler)
                {
                    if (Degerler.TryGetValue(anahtar, out var eski) && eski == deger)
                        return;
                    Degerler[anahtar] = deger;
                    Directory.CreateDirectory(Gunluk.Klasor);
                    var satirlar = new List<string>();
                    foreach (var kv in Degerler) satirlar.Add(kv.Key + "=" + kv.Value);
                    File.WriteAllLines(Dosya, satirlar);
                }
            }
            catch (Exception ex) { Gunluk.Hata("ayar yazma", ex); }
        }
    }
}
