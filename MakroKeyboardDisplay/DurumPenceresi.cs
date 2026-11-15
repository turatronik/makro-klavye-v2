using System;
using System.Drawing;
using System.Windows.Forms;

namespace MakroKeyboardDisplay
{
    /// <summary>
    /// Küçük durum penceresi: klavye bağlı mı, ne çalıyor, ses kaç, hangi mod.
    /// Kapatınca uygulama kapanmaz, tepside çalışmaya devam eder.
    /// Tasarımcı dosyası yok; her şey kodla çiziliyor.
    /// </summary>
    [System.ComponentModel.DesignerCategory("Code")]
    public sealed class DurumPenceresi : Form
    {
        static readonly Color Arka = Color.FromArgb(17, 24, 39);
        static readonly Color Kart = Color.FromArgb(31, 41, 55);
        static readonly Color Yazi = Color.FromArgb(241, 245, 249);
        static readonly Color Soluk = Color.FromArgb(148, 163, 184);
        static readonly Color Yesil = Color.FromArgb(34, 197, 94);
        static readonly Color Gri = Color.FromArgb(100, 116, 139);

        readonly Label lBaglanti, lBaslik, lSanatci, lKaynak, lSes, lMod;
        readonly Panel nokta;
        readonly PictureBox kapak;
        public event Action YenidenAraIstendi;

        public DurumPenceresi(Icon simge)
        {
            // başlıkta derleme saati: hangi sürümün çalıştığı hemen görülsün
            string derleme = "";
            try { derleme = System.IO.File.GetLastWriteTime(typeof(DurumPenceresi).Assembly.Location).ToString("dd.MM HH:mm"); } catch { }
            Text = "Makro Klavye Ekran  v" + Program.Surum + "  ·  derleme " + derleme;
            Icon = simge;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(440, 250);
            BackColor = Arka;
            ForeColor = Yazi;
            Font = new Font("Segoe UI", 10f);

            nokta = new Panel { Location = new Point(20, 24), Size = new Size(12, 12), BackColor = Gri };
            lBaglanti = Etiket("Klavye aranıyor…", 40, 18, 380, 11f, FontStyle.Bold, Yazi);
            lMod = Etiket("", 40, 42, 380, 9f, FontStyle.Regular, Soluk);

            var kart = new Panel { Location = new Point(16, 72), Size = new Size(408, 120), BackColor = Kart };
            kapak = new PictureBox { Location = new Point(12, 12), Size = new Size(96, 96), BackColor = Arka, SizeMode = PictureBoxSizeMode.Zoom };
            lKaynak = Etiket("", 120, 12, 280, 9f, FontStyle.Regular, Soluk);
            lBaslik = Etiket("Çalan bir şey yok", 120, 34, 280, 12f, FontStyle.Bold, Yazi);
            lSanatci = Etiket("", 120, 62, 280, 10f, FontStyle.Regular, Soluk);
            lSes = Etiket("", 120, 88, 280, 9f, FontStyle.Regular, Soluk);
            kart.Controls.AddRange(new Control[] { kapak, lKaynak, lBaslik, lSanatci, lSes });

            var bYeniden = Dugme("Klavyeyi yeniden ara", 16, 204, 180);
            bYeniden.Click += (s, e) => YenidenAraIstendi?.Invoke();
            var bKapat = Dugme("Tepsiye gizle", 324, 204, 100);
            bKapat.Click += (s, e) => Hide();
            var ipucu = Etiket("Simge saatin yanında (gizli simgeler ^)", 204, 210, 120, 7.5f, FontStyle.Regular, Soluk);
            ipucu.Size = new Size(118, 34);

            Controls.AddRange(new Control[] { nokta, lBaglanti, lMod, kart, bYeniden, bKapat, ipucu });
        }

        Label Etiket(string metin, int x, int y, int w, float boyut, FontStyle stil, Color renk) =>
            new Label
            {
                Text = metin,
                Location = new Point(x, y),
                Size = new Size(w, (int)(boyut * 2.2f)),
                Font = new Font("Segoe UI", boyut, stil),
                ForeColor = renk,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
            };

        Button Dugme(string metin, int x, int y, int w)
        {
            var b = new Button
            {
                Text = metin,
                Location = new Point(x, y),
                Size = new Size(w, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Kart,
                ForeColor = Yazi,
            };
            b.FlatAppearance.BorderColor = Gri;
            return b;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // kullanıcı kapatırsa yalnızca gizle; uygulama tepside çalışmaya devam eder
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        public void Goster()
        {
            Show();
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
        }

        public void Guncelle(bool bagli, string port, string modAdi, MedyaBilgisi m, int ses, bool sessiz)
        {
            nokta.BackColor = bagli ? Yesil : Gri;
            lBaglanti.Text = bagli ? $"Klavye bağlı ({port})"
                : "Klavye aranıyor…" + (string.IsNullOrEmpty(port) ? " (USB'ye takılı mı?)" : $" (son port: {port})");
            lMod.Text = bagli && !string.IsNullOrEmpty(modAdi) ? "Mod: " + modAdi : (bagli ? "" : "Klavye bulununca şarkı bilgisi ekranına gider");
            if (m == null)
            {
                lKaynak.Text = "";
                lBaslik.Text = "Çalan bir şey yok";
                lSanatci.Text = "Bir oynatıcıda şarkı başlat";
            }
            else
            {
                lKaynak.Text = (m.Durum == 'P' ? "▶ Çalıyor" : m.Durum == 'D' ? "❚❚ Duraklatıldı" : "■ Durdu") + (m.Kaynak != "" ? "  ·  " + m.Kaynak : "");
                lBaslik.Text = m.Baslik;
                lSanatci.Text = m.Sanatci + (m.Album != "" ? "  —  " + m.Album : "");
            }
            lSes.Text = ses < 0 ? "" : (sessiz ? "Ses: sessiz" : $"Ses: %{ses}");
        }

        public void KapakAyarla(Image resim)
        {
            var eski = kapak.Image;
            kapak.Image = resim == null ? null : new Bitmap(resim);
            eski?.Dispose();
        }
    }
}