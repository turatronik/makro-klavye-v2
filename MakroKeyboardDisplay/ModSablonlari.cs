using System;
using System.Collections.Generic;
using System.Drawing;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Hazır modlar (uygulama 2.5): Modlar sekmesinde "+ Yeni mod" menüsünden tek tıkla eklenir, sonra düzenlenebilir.
    /// Kısayollar programların varsayılanlarıdır (Teams, Photoshop, Visual Studio, PowerPoint...).
    /// </summary>
    public static class ModSablonlari
    {
        public sealed class Sablon
        {
            public string Baslik, Aciklama;
            public Func<ModTanimi> Yap;
        }

        static ModTanimi M(string ad, int renk, string ekran, string teker, params (string ad, string kod, string uzun)[] tuslar)
        {
            var m = new ModTanimi
            {
                Ad = ad,
                Renk = Color.FromArgb((renk >> 16) & 255, (renk >> 8) & 255, renk & 255),
                Ekran = ekran,
                Tekerlek = teker,
            };
            for (int i = 0; i < 6 && i < tuslar.Length; i++)
                m.Tuslar[i] = new TusTanimi { Ad = tuslar[i].ad, Kod = tuslar[i].kod, UzunKod = tuslar[i].uzun ?? "" };
            return m;
        }

        public static readonly Sablon[] Liste =
        {
            new Sablon
            {
                Baslik = "Ofis", Aciklama = "kopyala, yapıştır, geri al, kaydet, bul, ekran al",
                Yap = () => M("OFİS", 0x3B82F6, "tuslar", "k",
                    ("Kopyala", "CTRL+C", "WINDOWS+V"), ("Yapıştır", "CTRL+V", "CTRL+SHIFT+V"), ("Geri al", "CTRL+Z", "CTRL+Y"),
                    ("Kaydet", "CTRL+S", null), ("Bul", "CTRL+F", null), ("Ekran al", "SHIFT+WINDOWS+S", null)),
            },
            new Sablon
            {
                Baslik = "Toplantı (Teams)", Aciklama = "mikrofon, kamera, el kaldır, paylaş, ayrıl",
                Yap = () => M("TOPLANTI", 0xA855F7, "tuslar", "s",
                    ("Mikrofon", "MIC_MUTE", null), ("Kamera", "CTRL+SHIFT+O", null), ("El kaldır", "CTRL+SHIFT+K", null),
                    ("Paylaş", "CTRL+SHIFT+E", null), ("Ayrıl", "CTRL+SHIFT+H", null), ("Sustur", "MUTE", null)),
            },
            new Sablon
            {
                Baslik = "OBS Studio", Aciklama = "F13–F18: OBS'te Ayarlar > Kısayollar'dan ata",
                Yap = () => M("OBS", 0x30D158, "tuslar", "m",
                    ("Sahne 1", "F13", null), ("Sahne 2", "F14", null), ("Sahne 3", "F15", null),
                    ("Kayıt", "F16", null), ("Yayın", "F17", null), ("Mikrofon", "F18", null)),
            },
            new Sablon
            {
                Baslik = "Photoshop", Aciklama = "fırça, silgi, taşı, geri al; tekerlek yakınlaştırır",
                Yap = () => M("PHOTOSHOP", 0x22D3EE, "tuslar", "z",
                    ("Fırça", "B", null), ("Silgi", "E", null), ("Taşı", "V", null),
                    ("Geri al", "CTRL+Z", "CTRL+SHIFT+Z"), ("Seçimi kaldır", "CTRL+D", null), ("Kaydet", "CTRL+S", "CTRL+SHIFT+S")),
            },
            new Sablon
            {
                Baslik = "Visual Studio", Aciklama = "derle, başlat, durdur, adım adım, tanıma git",
                Yap = () => M("GELİŞTİRME", 0xA855F7, "tuslar", "k",
                    ("Derle", "CTRL+SHIFT+B", null), ("Başlat", "F5", "CTRL+F5"), ("Durdur", "SHIFT+F5", null),
                    ("Adım", "F10", null), ("İçine", "F11", "SHIFT+F11"), ("Tanıma git", "F12", null)),
            },
            new Sablon
            {
                Baslik = "Sunum (PowerPoint)", Aciklama = "başlat, önceki, sonraki, siyah / beyaz ekran, bitir",
                Yap = () => M("SUNUM", 0xFF8A00, "tuslar", "m",
                    ("Başlat", "F5", "SHIFT+F5"), ("Önceki", "LEFT", null), ("Sonraki", "RIGHT", null),
                    ("Siyah ekran", "B", null), ("Beyaz ekran", "W", null), ("Bitir", "ESC", null)),
            },
            new Sablon
            {
                Baslik = "Tarayıcı", Aciklama = "sekme aç / kapat, geri, ileri; tekerlek sekmeler arası",
                Yap = () => M("TARAYICI", 0xFF2D55, "tuslar", "b",
                    ("Yeni sekme", "CTRL+T", null), ("Sekmeyi kapat", "CTRL+W", "CTRL+SHIFT+T"), ("Yenile", "F5", null),
                    ("Geri", "ALT+LEFT", null), ("İleri", "ALT+RIGHT", null), ("Adres", "CTRL+L", null)),
            },
            new Sablon
            {
                Baslik = "Medya", Aciklama = "önceki, oynat, sonraki, sustur, mikrofon",
                Yap = () => M("MEDYA", 0xFF2878, "medya", "m",
                    ("Önceki", "MEDIA_PREVIOUS", null), ("Oynat / Dur", "MEDIA_PLAY_PAUSE", null), ("Sonraki", "MEDIA_NEXT", null),
                    ("Sustur", "MUTE", null), ("Mikrofon", "MIC_MUTE", null), ("Oynatıcı", "MEDIA_PLAYER", null)),
            },
        };
    }
}
