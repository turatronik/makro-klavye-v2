namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Uygulama (oynatıcı) sesi özelliği kaldırıldı: Windows bu bilgisayarda karıştırıcı kaydırıcısına
    /// erişim vermiyor, oynatıcının kendi çubuğunu kullanmak da güvenilir olmadı.
    /// Klavye artık "SES" isteği göndermiyor; eski kart yazılımı gönderirse "yok" cevabı döner.
    /// </summary>
    public sealed class UygulamaSesi
    {
        public int Degistir(string kaynakKimligi, int adim, bool sessizDegistir, out bool sessiz, out string ad)
        {
            sessiz = false;
            ad = MedyaIzleyici.KaynakAdi(kaynakKimligi);
            return -1;
        }
    }
}
