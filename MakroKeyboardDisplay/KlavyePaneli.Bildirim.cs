using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Ayarlar > Genel'deki panel kısayolu satırı ve ortak küçük parçalar.
    /// (2.3'teki Bildirim sekmesi, Windows bildirimleri ve hatırlatıcılar 2.4'te klavyenin belleği için kaldırıldı.)
    /// </summary>
    public sealed partial class KlavyePaneli
    {
        public event Action<string> PanelKisayoluIstendi;      // "değiştiriciler|tuş" ya da ""
        public string PanelKisayoluAyari => Ayarlar.Oku("PanelKisayolu", PanelKisayolu.Varsayilan);
        public bool PanelKisayoluKullanimda;                    // başka program aynı kısayolu almış
        bool kisayolKaydi;                 // panel kısayolu için tuş bekleniyor

        /// <summary>Bağlı kartın yazılımı en az bu sürüm mü.</summary>
        bool KartEnAzPanel(int ana, int alt)
        {
            var p = (durum.KartSurum ?? "").Split('.');
            if (!durum.Bagli || p.Length < 2 || !int.TryParse(p[0], out int a) || !int.TryParse(p[1], out int b))
                return false;
            return a > ana || (a == ana && b >= alt);
        }

        void Kutucuk2(Graphics g, float x, float y, string ad, bool acik, Action degistir, string kimlik)
        {
            float gen = g.MeasureString(ad, YaziTipi("Segoe UI", 9f, FontStyle.Regular)).Width;
            var alan = new RectangleF(x - 2, y, gen + 26, 21);
            if (uzerindeki == kimlik) Doldur(g, T.Uzerinde, alan, 5);
            var k = new RectangleF(x, y + 3, 15, 15);
            Doldur(g, acik ? T.Vurgu : T.Iz, k, 4);
            if (acik)
                using (var kalem = new Pen(T.Vurgu == Color.FromArgb(255, 255, 255) ? R(27, 20, 70) : Color.White, 2f))
                    g.DrawLines(kalem, new[] { new PointF(k.X + 3.5f, k.Y + 7.5f), new PointF(k.X + 6.5f, k.Y + 10.5f), new PointF(k.X + 11.5f, k.Y + 4.5f) });
            Yaz(g, ad, 9f, FontStyle.Regular, T.Yazi, new RectangleF(x + 20, y, gen + 4, 21));
            Ekle(alan, degistir, kimlik);
        }

        // ---- panel kısayolu (Ayarlar > Genel)
        float KisayolSatiri(Graphics g, float y, float w)
        {
            var ayar = PanelKisayoluAyari;
            bool acik = PanelKisayolu.Coz(ayar, out _, out _);
            string alt = kisayolKaydi ? "Tuş birleşimine bas… (Esc: vazgeç)"
                : !acik ? "Kapalı · açınca Ctrl + Alt + M"
                : PanelKisayoluKullanimda ? PanelKisayolu.Yazisi(ayar) + " başka programda kullanılıyor · tıkla, değiştir"
                : PanelKisayolu.Yazisi(ayar) + " · değiştirmek için tıkla";
            float sonraki = AnahtarSatiri(g, y, w, "Panel kısayolu", alt, acik, () =>
            {
                kisayolKaydi = false;
                var yeni = acik ? "" : PanelKisayolu.Varsayilan;
                Ayarlar.Yaz("PanelKisayolu", yeni);
                PanelKisayoluIstendi?.Invoke(yeni);
            }, "pkis");
            var yazi = new RectangleF(P, y, w - 2 * P - 50, 42);
            Ekle(yazi, () =>
            {
                kisayolKaydi = !kisayolKaydi;
                ActiveControl = null;
                Focus();
            }, "pkis");
            return sonraki;
        }

        /// <summary>Panel kısayolu kaydedilirken basılan tuş (ProcessCmdKey'den). true: işlendi.</summary>
        bool KisayolTusu(Keys keyData, bool win)
        {
            if (!kisayolKaydi) return false;
            var tus = keyData & Keys.KeyCode;
            if (tus == Keys.Escape)
            {
                kisayolKaydi = false;
                Invalidate();
                return true;
            }
            if (tus == Keys.ControlKey || tus == Keys.ShiftKey || tus == Keys.Menu || tus == Keys.LWin || tus == Keys.RWin
                || tus == Keys.LControlKey || tus == Keys.RControlKey || tus == Keys.LShiftKey || tus == Keys.RShiftKey
                || tus == Keys.LMenu || tus == Keys.RMenu)
                return true;                    // değiştirici tek başına: asıl tuşu bekle
            uint mod = 0;
            if ((keyData & Keys.Control) != 0) mod |= PanelKisayolu.MOD_CONTROL;
            if ((keyData & Keys.Alt) != 0) mod |= PanelKisayolu.MOD_ALT;
            if ((keyData & Keys.Shift) != 0) mod |= PanelKisayolu.MOD_SHIFT;
            if (win) mod |= PanelKisayolu.MOD_WIN;
            if (mod == 0 || mod == PanelKisayolu.MOD_SHIFT)
                return true;                    // tek tuş ya da yalnızca Shift ile olmaz (yazı yazarken tetiklenir)
            var ayar = mod + "|" + (uint)tus;
            kisayolKaydi = false;
            Ayarlar.Yaz("PanelKisayolu", ayar);
            PanelKisayoluIstendi?.Invoke(ayar);
            Invalidate();
            return true;
        }
    }
}
