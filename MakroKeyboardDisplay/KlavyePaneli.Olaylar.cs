using System;

namespace MakroKeyboardDisplay
{
    // KlavyePaneli parçası: panelin dışarı bildirdiği olaylardan biri burada durur.
    // (Visual Studio açık dosyaları kendi kopyasıyla kaydettiği için ayrı dosyada.)
    public sealed partial class KlavyePaneli
    {
        /// <summary>Işık sekmesinde her değişiklikte.</summary>
        public event Action<LedAyari> LedAyarIstendi;

        /// <summary>3.2 (CM): klasörde bulunan yeni kart yazılımını yükle.</summary>
        public event Action HazirSurumuYukleIstendi;
    }
}
