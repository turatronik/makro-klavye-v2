using System;
using System.Runtime.InteropServices;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Windows'un varsayılan hoparlörünün ses seviyesi (0-100) ve sessiz durumu (Core Audio).
    /// Panelden ses çubuğuyla da değiştirilebilir.
    /// </summary>
    public sealed class SesSeviyesi
    {
        IAudioEndpointVolume hacim;
        DateTime sonAlinma = DateTime.MinValue;

        public bool Oku(out int seviye, out bool sessiz)
        {
            seviye = 0;
            sessiz = false;
            try
            {
                // Varsayılan cihaz değişebilir (kulaklık takıldı vs.): birkaç saniyede bir yeniden al
                if (hacim == null || (DateTime.UtcNow - sonAlinma).TotalSeconds > 5)
                {
                    hacim = Al();
                    sonAlinma = DateTime.UtcNow;
                }
                if (hacim == null)
                    return false;
                if (hacim.GetMasterVolumeLevelScalar(out float s) != 0)
                {
                    hacim = null;
                    return false;
                }
                hacim.GetMute(out sessiz);
                seviye = (int)Math.Round(s * 100);
                return true;
            }
            catch (Exception)
            {
                hacim = null;
                return false;
            }
        }

        /// <summary>Ses seviyesini ayarla (0-100). Sessizdeyse sesi açar.</summary>
        public void Ayarla(int seviye)
        {
            try
            {
                hacim ??= Al();
                if (hacim == null)
                    return;
                var bos = Guid.Empty;
                hacim.SetMasterVolumeLevelScalar(Math.Clamp(seviye, 0, 100) / 100f, ref bos);
                if (seviye > 0 && hacim.GetMute(out bool sessiz) == 0 && sessiz)
                    hacim.SetMute(false, ref bos);
            }
            catch (Exception) { hacim = null; }
        }

        public void SessizDegistir()
        {
            try
            {
                hacim ??= Al();
                if (hacim == null || hacim.GetMute(out bool sessiz) != 0)
                    return;
                var bos = Guid.Empty;
                hacim.SetMute(!sessiz, ref bos);
            }
            catch (Exception) { hacim = null; }
        }

        // ---- ses ölçer (Işık sekmesinin Ritim ve Stereo efektleri için) ----
        IAudioMeterInformation olcer;
        DateTime olcerAlinma = DateTime.MinValue;
        readonly float[] kanallar = new float[8];

        /// <summary>Şu an çalan sesin sol/sağ tepe değeri (0-1, doğrusal). Ses yoksa 0.</summary>
        public bool Tepe(out float sol, out float sag)
        {
            sol = sag = 0;
            try
            {
                if (olcer == null || (DateTime.UtcNow - olcerAlinma).TotalSeconds > 5)
                {
                    if (olcer != null) Marshal.ReleaseComObject(olcer);
                    olcer = Al<IAudioMeterInformation>();
                    olcerAlinma = DateTime.UtcNow;
                }
                if (olcer == null)
                    return false;
                if (olcer.GetMeteringChannelCount(out uint n) != 0 || n == 0)
                {
                    if (olcer.GetPeakValue(out float t) != 0) { olcer = null; return false; }
                    sol = sag = t;
                    return true;
                }
                n = Math.Min(n, (uint)kanallar.Length);
                if (olcer.GetChannelsPeakValues(n, kanallar) != 0) { olcer = null; return false; }
                sol = kanallar[0];
                sag = n > 1 ? kanallar[1] : kanallar[0];
                return true;
            }
            catch (Exception)
            {
                olcer = null;
                return false;
            }
        }

        // ---- mikrofon (uygulama 2.4): Windows'un varsayılan kayıt cihazı; Discord, Teams, Zoom hepsi susar ----
        IAudioEndpointVolume mik;
        DateTime mikAlinma = DateTime.MinValue;

        /// <summary>Mikrofon kapalı mı. Mikrofon yoksa false döner.</summary>
        public bool MikrofonOku(out bool kapali)
        {
            kapali = false;
            try
            {
                if (mik == null || (DateTime.UtcNow - mikAlinma).TotalSeconds > 5)
                {
                    mik = Al<IAudioEndpointVolume>(1 /* eCapture */, 0 /* eConsole */);
                    mikAlinma = DateTime.UtcNow;
                }
                if (mik == null || mik.GetMute(out kapali) != 0)
                {
                    mik = null;
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                mik = null;
                return false;
            }
        }

        /// <summary>Mikrofonu kapatır / açar (varsayılan ve iletişim kayıt cihazının ikisi de). Yeni durum: kapalı mı.</summary>
        public bool MikrofonDegistir(out bool kapali)
        {
            kapali = false;
            if (!MikrofonOku(out bool simdi))
                return false;
            kapali = !simdi;
            var bos = Guid.Empty;
            try
            {
                mik.SetMute(kapali, ref bos);
                var iletisim = Al<IAudioEndpointVolume>(1, 2 /* eCommunications */);
                if (iletisim != null)
                {
                    iletisim.SetMute(kapali, ref bos);
                    Marshal.ReleaseComObject(iletisim);
                }
                return true;
            }
            catch (Exception)
            {
                mik = null;
                return false;
            }
        }

        static IAudioEndpointVolume Al() => Al<IAudioEndpointVolume>();

        static T Al<T>(int akis = 0, int rol = 1) where T : class
        {
            var sayac = (IMMDeviceEnumerator)new MMDeviceEnumeratorSinifi();
            try
            {
                if (sayac.GetDefaultAudioEndpoint(akis /* 0 eRender, 1 eCapture */, rol /* 0 eConsole, 1 eMultimedia, 2 eCommunications */, out IMMDevice cihaz) != 0 || cihaz == null)
                    return null;
                var iid = typeof(T).GUID;
                if (cihaz.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out object nesne) != 0)
                    return null;
                return nesne as T;
            }
            finally
            {
                Marshal.ReleaseComObject(sayac);
            }
        }

        // ---- Core Audio COM arayüzleri (yalnızca kullanılan kısımlar; sıra vtable sırasıdır) ----
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorSinifi { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
            [PreserveSig] int GetMasterVolumeLevel(out float level);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioMeterInformation
        {
            [PreserveSig] int GetPeakValue(out float peak);
            [PreserveSig] int GetMeteringChannelCount(out uint count);
            [PreserveSig] int GetChannelsPeakValues(uint count, [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] float[] peaks);
            [PreserveSig] int QueryHardwareSupport(out uint mask);
        }
    }
}
