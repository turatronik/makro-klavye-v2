using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Uygulama 2.9 / kart 2.7: bilgisayardan çıkan sesi (hoparlör "loopback") dinler ve 8 frekans bandına böler.
    /// Klavye ekranındaki ses çubukları ve ritim ışıkları gerçek bas / tiz seviyesini bununla gösterir.
    /// Yalnızca ölçüm yapılır; ses kaydedilmez, saklanmaz ve hiçbir yere gönderilmez.
    /// </summary>
    public sealed class SesAnalizi : IDisposable
    {
        const int N = 2048;                 // FFT boyu (48 kHz'de ~23 Hz çözünürlük)
        static readonly double[] Kenarlar = { 40, 80, 160, 320, 640, 1280, 2560, 5120, 12000 };

        readonly object kilit = new();
        readonly float[] halka = new float[N];
        int yaz;                            // halkaya sıradaki yazılacak yer
        long toplamOrnek;
        long sonVeri;                       // Environment.TickCount64
        WasapiLoopbackCapture yakala;
        int ornekHizi = 48000;

        // FFT için hazır tablolar
        readonly double[] pencere = new double[N];
        readonly double[] re = new double[N], im = new double[N];
        readonly double[] cosT = new double[N / 2], sinT = new double[N / 2];
        readonly float[] yumusak = new float[8];

        public bool Calisiyor => yakala != null;

        public SesAnalizi()
        {
            for (int i = 0; i < N; i++) pencere[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1));
            for (int i = 0; i < N / 2; i++)
            {
                cosT[i] = Math.Cos(-2 * Math.PI * i / N);
                sinT[i] = Math.Sin(-2 * Math.PI * i / N);
            }
        }

        public bool Baslat()
        {
            if (yakala != null) return true;
            try
            {
                var y = new WasapiLoopbackCapture();
                ornekHizi = y.WaveFormat.SampleRate;
                y.DataAvailable += Geldi;
                y.RecordingStopped += (s, e) => { };
                y.StartRecording();
                yakala = y;
                return true;
            }
            catch (Exception ex)
            {
                Gunluk.Hata("ses analizi başlatılamadı", ex);
                yakala = null;
                return false;
            }
        }

        public void Durdur()
        {
            var y = yakala;
            yakala = null;
            if (y == null) return;
            try { y.DataAvailable -= Geldi; y.StopRecording(); } catch { }
            try { y.Dispose(); } catch { }
            lock (kilit) { Array.Clear(halka); yaz = 0; toplamOrnek = 0; }
            Array.Clear(yumusak);
        }

        public void Dispose() => Durdur();

        void Geldi(object s, WaveInEventArgs e)
        {
            var f = (s as WasapiLoopbackCapture)?.WaveFormat;
            if (f == null || e.BytesRecorded <= 0) return;
            int kanal = Math.Max(1, f.Channels);
            int bayt = f.BitsPerSample / 8;
            int cerceve = bayt * kanal;
            bool kayan = f.Encoding == WaveFormatEncoding.IeeeFloat
                         || (f.Encoding == WaveFormatEncoding.Extensible && bayt == 4 && f.BitsPerSample == 32 && KayanMi(f));
            int adet = e.BytesRecorded / cerceve;
            lock (kilit)
            {
                for (int i = 0; i < adet; i++)
                {
                    float toplam = 0;
                    int o = i * cerceve;
                    for (int c = 0; c < kanal; c++)
                    {
                        int p = o + c * bayt;
                        float v;
                        if (kayan && bayt == 4) v = BitConverter.ToSingle(e.Buffer, p);
                        else if (bayt == 2) v = BitConverter.ToInt16(e.Buffer, p) / 32768f;
                        else if (bayt == 3) v = ((e.Buffer[p] << 8 | e.Buffer[p + 1] << 16 | e.Buffer[p + 2] << 24) >> 8) / 8388608f;
                        else if (bayt == 4) v = BitConverter.ToInt32(e.Buffer, p) / 2147483648f;
                        else v = 0;
                        toplam += v;
                    }
                    halka[yaz] = toplam / kanal;
                    yaz = (yaz + 1) % N;
                }
                toplamOrnek += adet;
                sonVeri = Environment.TickCount64;
            }
        }

        static bool KayanMi(WaveFormat f) =>
            f is WaveFormatExtensible x && x.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");

        /// <summary>
        /// 8 bandın seviyesi (0-100). Yükseliş anında, düşüş yavaş. Son birkaç yüz ms ses gelmediyse hepsi sıfıra iner.
        /// Dönüş: ses akışı var mı.
        /// </summary>
        public bool Oku(byte[] bantlar)
        {
            bool taze;
            lock (kilit)
            {
                taze = toplamOrnek >= N && Environment.TickCount64 - sonVeri < 400;
                if (taze)
                    for (int i = 0; i < N; i++) re[i] = halka[(yaz + i) % N] * pencere[i];
            }
            var ham = new float[8];
            if (taze)
            {
                Array.Clear(im);
                Fft();
                double kutuGenis = (double)ornekHizi / N;
                for (int b = 0; b < 8; b++)
                {
                    int k0 = Math.Max(1, (int)Math.Ceiling(Kenarlar[b] / kutuGenis));
                    int k1 = Math.Min(N / 2 - 1, Math.Max(k0, (int)Math.Floor(Kenarlar[b + 1] / kutuGenis)));
                    double en = 0;
                    for (int k = k0; k <= k1; k++) en = Math.Max(en, Math.Sqrt(re[k] * re[k] + im[k] * im[k]));
                    double genlik = en / (N / 4.0);                  // Hann penceresi: tam ölçekli sinüs ~ 1
                    double db = genlik <= 1e-7 ? -140 : 20 * Math.Log10(genlik);
                    db += b * 2.5;                                   // tizler doğal olarak daha zayıf: hafif yükselt
                    ham[b] = (float)Math.Clamp((db + 62) / 56 * 100, 0, 100);
                }
            }
            for (int b = 0; b < 8; b++)
            {
                yumusak[b] = ham[b] >= yumusak[b] ? ham[b] : yumusak[b] * 0.82f + ham[b] * 0.18f;
                bantlar[b] = (byte)Math.Clamp((int)Math.Round(yumusak[b]), 0, 100);
            }
            return taze;
        }

        void Fft()
        {
            // bit çevirmeli yerinde radix-2
            for (int i = 1, j = 0; i < N; i++)
            {
                int bit = N >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }
            for (int uzun = 2; uzun <= N; uzun <<= 1)
            {
                int yari = uzun / 2, adim = N / uzun;
                for (int i = 0; i < N; i += uzun)
                    for (int k = 0; k < yari; k++)
                    {
                        double wr = cosT[k * adim], wi = sinT[k * adim];
                        int a = i + k, b = a + yari;
                        double tr = re[b] * wr - im[b] * wi, ti = re[b] * wi + im[b] * wr;
                        re[b] = re[a] - tr; im[b] = im[a] - ti;
                        re[a] += tr; im[a] += ti;
                    }
            }
        }
    }
}
