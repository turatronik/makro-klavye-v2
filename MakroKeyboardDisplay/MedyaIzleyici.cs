using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace MakroKeyboardDisplay
{
    public sealed class MedyaBilgisi
    {
        public string Baslik = "", Sanatci = "", Album = "", Kaynak = "";
        public char Durum = 'S';        // P: çalıyor, D: duraklatıldı, S: durdu
        public double Konum;            // saniye (okunduğu andaki)
        public double Sure;             // saniye (0: bilinmiyor)

        /// <summary>Şarkı değişti mi anlamak için.</summary>
        public string Anahtar => Kaynak + "|" + Baslik + "|" + Sanatci + "|" + Album;
    }

    /// <summary>
    /// Windows'un medya denetimlerini (SMTC) okur: Spotify, Apple Music, iTunes, YouTube (tarayıcı),
    /// VLC, Medya Oynatıcı... Windows'un ses düğmesi açılır penceresinde görünen her oynatıcı.
    /// </summary>
    public sealed class MedyaIzleyici
    {
        GlobalSystemMediaTransportControlsSessionManager yonetici;
        GlobalSystemMediaTransportControlsSession oturum;
        GlobalSystemMediaTransportControlsSessionMediaProperties sonOzellik;

        /// <summary>
        /// Windows şarkı / oynatıcı / çalma durumu değişince haber verir (arka plan iş parçacığında).
        /// Böylece yarım saniyelik yoklamayı beklemeden hemen okunur.
        /// </summary>
        public event Action Degisti;
        readonly System.Collections.Generic.HashSet<GlobalSystemMediaTransportControlsSession> dinlenenler = new();

        void Dinle()
        {
            try
            {
                foreach (var o in yonetici.GetSessions())
                {
                    lock (dinlenenler)
                    {
                        if (!dinlenenler.Add(o)) continue;
                    }
                    o.MediaPropertiesChanged += (s, e) => Degisti?.Invoke();
                    o.PlaybackInfoChanged += (s, e) => Degisti?.Invoke();
                }
            }
            catch (Exception) { }
        }

        /// <summary>Son okunan oynatıcının kimliği (AUMID), uygulama sesini bulmak için.</summary>
        public string SonKaynakKimligi { get; private set; }

        public async Task<MedyaBilgisi> OkuAsync()
        {
            if (yonetici == null)
            {
                yonetici = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                yonetici.CurrentSessionChanged += (s, e) => { Dinle(); Degisti?.Invoke(); };
                yonetici.SessionsChanged += (s, e) => { Dinle(); Degisti?.Invoke(); };
                Dinle();
            }
            oturum = SecOturum();
            if (oturum == null)
                return null;
            SonKaynakKimligi = oturum.SourceAppUserModelId;

            var ozellik = await oturum.TryGetMediaPropertiesAsync();
            sonOzellik = ozellik;
            if (ozellik == null || (string.IsNullOrWhiteSpace(ozellik.Title) && string.IsNullOrWhiteSpace(ozellik.Artist)))
                return null;

            var oynatma = oturum.GetPlaybackInfo();
            var zaman = oturum.GetTimelineProperties();
            var b = new MedyaBilgisi
            {
                Baslik = ozellik.Title ?? "",
                Sanatci = !string.IsNullOrWhiteSpace(ozellik.Artist) ? ozellik.Artist : (ozellik.AlbumArtist ?? ""),
                Album = ozellik.AlbumTitle ?? "",
                Kaynak = KaynakAdi(oturum.SourceAppUserModelId),
            };
            switch (oynatma?.PlaybackStatus)
            {
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing: b.Durum = 'P'; break;
                case GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused: b.Durum = 'D'; break;
                default: b.Durum = 'S'; break;
            }
            if (zaman != null)
            {
                b.Sure = Math.Max(0, (zaman.EndTime - zaman.StartTime).TotalSeconds);
                var konum = zaman.Position.TotalSeconds;
                if (b.Durum == 'P' && zaman.LastUpdatedTime.Year > 2000)
                    konum += (DateTimeOffset.Now - zaman.LastUpdatedTime).TotalSeconds;
                b.Konum = b.Sure > 0 ? Math.Clamp(konum, 0, b.Sure) : Math.Max(0, konum);
            }
            // Bazı oynatıcılar şarkı adına sanatçıyı da koyar ("Sanatçı - Şarkı"); sanatçı boşsa ayır
            if (b.Sanatci == "" && b.Baslik.Contains(" - "))
            {
                var i = b.Baslik.IndexOf(" - ", StringComparison.Ordinal);
                b.Sanatci = b.Baslik.Substring(0, i);
                b.Baslik = b.Baslik.Substring(i + 3);
            }
            return b;
        }

        /// <summary>Çalan oturumu tercih et; yoksa Windows'un "geçerli" oturumu.</summary>
        GlobalSystemMediaTransportControlsSession SecOturum()
        {
            var gecerli = yonetici.GetCurrentSession();
            if (gecerli?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                return gecerli;
            foreach (var o in yonetici.GetSessions())
            {
                if (o.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    return o;
            }
            return gecerli;
        }

        /// <summary>Panelden oynatma komutu: "onceki", "oynat" (oynat / duraklat), "sonraki".</summary>
        public async Task KomutAsync(string komut)
        {
            var o = oturum;
            if (o == null)
                return;
            switch (komut)
            {
                case "onceki": await o.TrySkipPreviousAsync(); break;
                case "sonraki": await o.TrySkipNextAsync(); break;
                default: await o.TryTogglePlayPauseAsync(); break;
            }
        }

        /// <summary>Şarkıda konuma git (0-1 arası oran).</summary>
        public async Task KonumaGitAsync(double oran)
        {
            var o = oturum;
            if (o == null)
                return;
            var zaman = o.GetTimelineProperties();
            if (zaman == null)
                return;
            var hedef = zaman.StartTime + TimeSpan.FromTicks((long)((zaman.EndTime - zaman.StartTime).Ticks * Math.Clamp(oran, 0, 1)));
            await o.TryChangePlaybackPositionAsync(hedef.Ticks);
        }

        /// <summary>Son okunan şarkının kapak resmi (yoksa null).</summary>
        public async Task<Image> KapakAsync() => (await KapakOzetliAsync()).resim;

        /// <summary>
        /// Kapak ve özeti (baytlarından hesaplanır). Şarkı değişince Windows kapağı başlıktan biraz sonra günceller;
        /// özet sayesinde birkaç kez yeniden okunup yalnızca gerçekten değiştiyse yenilenir.
        /// </summary>
        public async Task<(Image resim, long ozet)> KapakOzetliAsync()
        {
            var kucukResim = sonOzellik?.Thumbnail;
            if (kucukResim == null)
                return (null, 0);
            using var akis = await kucukResim.OpenReadAsync();
            if (akis == null || akis.Size == 0 || akis.Size > 20_000_000)
                return (null, 0);
            var bayt = new byte[akis.Size];
            using (var okuyucu = new DataReader(akis.GetInputStreamAt(0)))
            {
                await okuyucu.LoadAsync((uint)akis.Size);
                okuyucu.ReadBytes(bayt);
            }
            unchecked
            {
                long ozet = (long)14695981039346656037UL;          // FNV-1a
                foreach (var x in bayt) ozet = (ozet ^ x) * 1099511628211L;
                using var ms = new MemoryStream(bayt);
                using var resim = Image.FromStream(ms);
                return (new Bitmap(resim), ozet);                  // akıştan bağımsız kopya
            }
        }

        public static string KaynakAdi(string kimlik)
        {
            if (string.IsNullOrEmpty(kimlik))
                return "";
            var k = kimlik.ToLowerInvariant();
            if (k.Contains("spotify")) return "Spotify";
            if (k.Contains("applemusic") || k.Contains("apple music")) return "Apple Music";
            if (k.Contains("itunes")) return "iTunes";
            if (k.Contains("zunemusic") || k.Contains("zunevideo") || k.Contains("mediaplayer")) return "Medya Oynatıcı";
            if (k.Contains("chrome")) return "Chrome";
            if (k.Contains("msedge") || k.Contains("microsoftedge")) return "Edge";
            if (k.Contains("firefox")) return "Firefox";
            if (k.Contains("opera")) return "Opera";
            if (k.Contains("brave")) return "Brave";
            if (k.Contains("vlc")) return "VLC";
            if (k.Contains("foobar")) return "foobar2000";
            if (k.Contains("musicbee")) return "MusicBee";
            if (k.Contains("aimp")) return "AIMP";
            if (k.Contains("tidal")) return "TIDAL";
            if (k.Contains("deezer")) return "Deezer";
            if (k.Contains("youtube")) return "YouTube Music";
            if (k.Contains("amazon")) return "Amazon Music";
            if (k.Contains("discord")) return "Discord";
            // "Firma.Uygulama_xxx!App" ya da "...\program.exe"
            var ad = kimlik;
            var u = ad.LastIndexOf('!');
            if (u > 0) ad = ad.Substring(0, u);
            ad = Path.GetFileNameWithoutExtension(ad);
            var n = ad.IndexOf('_');
            if (n > 0) ad = ad.Substring(0, n);
            var nokta = ad.LastIndexOf('.');
            if (nokta >= 0 && nokta < ad.Length - 1) ad = ad.Substring(nokta + 1);
            return ad.Length > 0 ? char.ToUpper(ad[0]) + ad.Substring(1) : kimlik;
        }
    }
}
