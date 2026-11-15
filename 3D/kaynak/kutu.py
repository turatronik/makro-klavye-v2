# Makro Klavye (RP2040-Zero + ST7789 240x240) kutu modeli
# Ölçüler mm. Koordinat: ekran penceresinin merkezi (0,0); gövde alt kenarı z=0, üst yüzey z=GOVDE_Y.
import sys
import numpy as np
from manifold3d import Manifold, CrossSection, JoinType

SEG = 64

# ---------------- Parametreler ----------------
KUTU_X0, KUTU_X1 = -46.0, 46.0       # dış genişlik 92
KUTU_Y0, KUTU_Y1 = -60.0, 18.0       # dış derinlik 78
KOSE_R = 5.0
DUVAR = 2.2
UST_KALINLIK = 3.0
IC_YUKSEKLIK = 18.0
GOVDE_Y = UST_KALINLIK + IC_YUKSEKLIK   # 21
KAPAK_K = 2.0

# Mekanik anahtar (Cherry MX uyumlu)
MX_DELIK = 14.1
MX_RAHAT = 15.4          # alttan genişletme: anahtar tırnakları 1.5 mm sacda tutunur
MX_SAC = 1.5
ARALIK = 19.05
TUS_SATIR_Y = [-25.0, -25.0 - ARALIK]
TUS_SUTUN_X = [-ARALIK, 0.0, ARALIK]

# Encoder (EC11, M7 burç)
ENC_DELIK = 7.4
ENC_X = 33.0

# Ekran (GMT130 1.3" 240x240, pinler solda)
PENCERE = 24.4                        # görünen alan ~23.4
PENCERE_PAH = 1.2                     # üstte dışa doğru pah
# Ölçülen: kart 39 x 27.5, cam 29 x 26. Görüntü alanı (23.4) camın pin tarafına yakın.
KART_X, KART_Y = 39.0, 27.5
CAM_X, CAM_Y = 29.0 + 0.4, 26.0 + 0.4  # cam cebi (0.2 pay)
CAM_MERKEZ_X = 1.3                    # cam merkezi pencere merkezine göre (+x: pinlerin karşı tarafı)
KART_SOL = -17.7                      # kartın pinli kenarı (pencere merkezine göre)
CAM_CEP_DERIN = 1.6

# RP2040-Zero (USB-C sola bakar, kapakta yuvaya oturur)
ZERO_UZ, ZERO_EN, ZERO_KAL = 23.5, 18.0, 1.2
ZERO_Y = -30.0
ZERO_TABAN = 1.0                      # kartın kapak üstünden yüksekliği
USB_EN, USB_YUK = 12.5, 7.5

# Vida (M3 sac vidası, kapaktan gövdeye)
VIDA_PILOT = 2.6
VIDA_GECIS = 3.4
BOSS_R = 3.0
BOSS_YER = [(KUTU_X0 + 5.0, KUTU_Y1 - 5.0), (KUTU_X1 - 5.0, KUTU_Y1 - 5.0),
            (KUTU_X0 + 5.0, KUTU_Y0 + 5.0), (KUTU_X1 - 5.0, KUTU_Y0 + 5.0)]

# ---------------- Yardımcılar ----------------
def yuvarlak_dik(x0, y0, x1, y1, r):
    w, h = x1 - x0, y1 - y0
    cs = CrossSection.square([w - 2 * r, h - 2 * r], center=True)
    cs = cs.offset(r, JoinType.Round, circular_segments=SEG)
    return cs.translate([(x0 + x1) / 2, (y0 + y1) / 2])

def kutu(cx, cy, z0, sx, sy, sz):
    return Manifold.cube([sx, sy, sz], center=True).translate([cx, cy, z0 + sz / 2])

def silindir(cx, cy, z0, h, r, r2=None):
    if r2 is None:
        r2 = r
    return Manifold.cylinder(h, r, r2, SEG).translate([cx, cy, z0])

DIS = yuvarlak_dik(KUTU_X0, KUTU_Y0, KUTU_X1, KUTU_Y1, KOSE_R)
IC = yuvarlak_dik(KUTU_X0 + DUVAR, KUTU_Y0 + DUVAR, KUTU_X1 - DUVAR, KUTU_Y1 - DUVAR, KOSE_R - DUVAR)

# ---------------- Gövde ----------------
def govde():
    g = DIS.extrude(GOVDE_Y) - IC.extrude(IC_YUKSEKLIK)

    # vida direkleri
    for (x, y) in BOSS_YER:
        g = g + (silindir(x, y, 0, IC_YUKSEKLIK + 0.01, BOSS_R) ^ IC.extrude(GOVDE_Y))
        g = g - silindir(x, y, -0.01, IC_YUKSEKLIK - 3.0, VIDA_PILOT / 2)

    ust = GOVDE_Y
    # anahtar delikleri
    for y in TUS_SATIR_Y:
        for x in TUS_SUTUN_X:
            g = g - kutu(x, y, ust - UST_KALINLIK - 1, MX_DELIK, MX_DELIK, UST_KALINLIK + 2)
            g = g - kutu(x, y, ust - UST_KALINLIK - 1, MX_RAHAT, MX_RAHAT, 1 + UST_KALINLIK - MX_SAC)

    # encoder delikleri
    for x in (-ENC_X, ENC_X):
        g = g - silindir(x, 0, ust - UST_KALINLIK - 1, UST_KALINLIK + 2, ENC_DELIK / 2)

    # ekran penceresi (pahlı) ve cam cebi
    p = PENCERE / 2
    pah = Manifold.cylinder(PENCERE_PAH + 0.01, p * 1.4142, (p + PENCERE_PAH + 0.01) * 1.4142, 4) \
        .rotate([0, 0, 45]).translate([0, 0, ust - PENCERE_PAH])
    g = g - kutu(0, 0, ust - UST_KALINLIK - 1, PENCERE, PENCERE, UST_KALINLIK + 2) - pah
    g = g - kutu(CAM_MERKEZ_X, 0, ust - UST_KALINLIK - 1, CAM_X, CAM_Y, 1 + CAM_CEP_DERIN)

    # USB-C açıklığı (sol duvar, alt kenara açık)
    usb_z = ZERO_TABAN + ZERO_KAL + 1.6
    usb = CrossSection.square([USB_YUK - 3, USB_EN - 3], center=True) \
        .offset(1.5, JoinType.Round, circular_segments=SEG)
    usb = usb.extrude(DUVAR + 2).rotate([0, 90, 0]).translate([KUTU_X0 - 1, ZERO_Y, usb_z])
    # extrude z ekseninde -> x eksenine çevrildi; yükseklik/genişlik eşleşmesi:
    g = g - usb
    g = g - kutu(KUTU_X0 + DUVAR / 2, ZERO_Y, -1, DUVAR + 2, USB_EN, usb_z + 1)   # alta açık
    return g

# ---------------- Kapak ----------------
def kapak():
    k = DIS.extrude(KAPAK_K).translate([0, 0, -KAPAK_K])
    # içe giren hizalama dudağı
    bosluk = 0.25
    dudak = yuvarlak_dik(KUTU_X0 + DUVAR + bosluk, KUTU_Y0 + DUVAR + bosluk,
                         KUTU_X1 - DUVAR - bosluk, KUTU_Y1 - DUVAR - bosluk, KOSE_R - DUVAR)
    dudak_ic = dudak.offset(-1.2, JoinType.Round, circular_segments=SEG)
    d = (dudak - dudak_ic).extrude(2.0)
    for (x, y) in BOSS_YER:
        d = d - silindir(x, y, -1, 4, BOSS_R + 0.5)
    # USB açıklığının önünde dudak olmasın
    d = d - kutu(KUTU_X0 + 4, ZERO_Y, -1, 8, ZERO_EN + 4, 4)   # Zero'nun USB ucu duvara dayanabilsin
    k = k + d

    # vida delikleri + havşa
    for (x, y) in BOSS_YER:
        k = k - silindir(x, y, -KAPAK_K - 1, KAPAK_K + 2, VIDA_GECIS / 2)
        k = k - silindir(x, y, -KAPAK_K - 0.01, 1.7, 3.2, VIDA_GECIS / 2)

    # lastik ayak yuvaları (10 mm, 0.6 derin)
    for (x, y) in [(KUTU_X0 + 12, KUTU_Y1 - 10), (KUTU_X1 - 12, KUTU_Y1 - 10),
                   (KUTU_X0 + 12, KUTU_Y0 + 10), (KUTU_X1 - 12, KUTU_Y0 + 10)]:
        k = k - silindir(x, y, -KAPAK_K - 0.01, 0.6, 5.2)

    # RP2040-Zero yuvası: kart duvara dayanır, üç yanda tutucu
    x0 = KUTU_X0 + DUVAR              # kartın USB kenarı iç duvarda
    x1 = x0 + ZERO_UZ
    yb = ZERO_Y - ZERO_EN / 2
    yt = ZERO_Y + ZERO_EN / 2
    tol = 0.3
    duvar_h = ZERO_TABAN + ZERO_KAL + 1.0
    dx1 = x1 + tol + 1.2
    dy = ZERO_EN / 2 + tol + 1.2
    yuva = kutu((x0 + dx1) / 2, ZERO_Y, 0, dx1 - x0, 2 * dy, duvar_h)
    yuva = yuva - kutu((x0 - 1 + x1 + tol) / 2, ZERO_Y, ZERO_TABAN, x1 + tol - x0 + 1, ZERO_EN + 2 * tol, duvar_h)
    # kart altında lehim noktaları için orta kanal
    yuva = yuva - kutu((x0 + x1) / 2, ZERO_Y, -0.5, ZERO_UZ - 4, ZERO_EN - 5, ZERO_TABAN + 1)
    # duvar tarafı açık (USB) – kutu zaten duvara kadar; arka (sağ) duvar kalır
    k = k + yuva
    return k

# ---------------- Düğme (EC11 D-mil 6 mm) ----------------
def dugme():
    R, H = 9.0, 14.0
    d = Manifold.cylinder(H, R, R - 0.6, 96)
    for i in range(24):  # tırtıl
        a = 360 / 24 * i
        d = d - Manifold.cylinder(H + 2, 0.7, 0.7, 16).translate([R + 0.15, 0, -1]).rotate([0, 0, a])
    # üst pah
    d = d - (Manifold.cylinder(1.2, R + 2, R + 2, 96) - Manifold.cylinder(1.2, R - 0.6, R - 1.8, 96)).translate([0, 0, H - 1.2])
    # D mil deliği
    mil = Manifold.cylinder(11.5, 3.1, 3.1, 48)
    mil = mil - kutu(0, 3.1 + 1.55 - (3.1 - 1.55) + 0.0, -1, 8, 3.1, 14)  # düz yüz: merkezden 1.55
    d = d - mil.translate([0, 0, -0.01])
    # işaret çizgisi
    d = d - kutu(0, R - 2.2, H - 0.6, 1.0, 4.0, 1)
    return d

# ---------------- STL ----------------
def stl_yaz(m, yol):
    mesh = m.to_mesh()
    v = np.asarray(mesh.vert_properties)[:, :3].astype(np.float32)
    t = np.asarray(mesh.tri_verts).astype(np.int64)
    a, b, c = v[t[:, 0]], v[t[:, 1]], v[t[:, 2]]
    n = np.cross(b - a, c - a)
    ln = np.linalg.norm(n, axis=1, keepdims=True)
    n = n / np.where(ln == 0, 1, ln)
    rec = np.zeros(len(t), dtype=[('n', '<f4', 3), ('a', '<f4', 3), ('b', '<f4', 3), ('c', '<f4', 3), ('x', '<u2')])
    rec['n'], rec['a'], rec['b'], rec['c'] = n, a, b, c
    with open(yol, 'wb') as f:
        f.write(b'Makro Klavye'.ljust(80, b' '))
        f.write(np.uint32(len(t)).tobytes())
        f.write(rec.tobytes())
    return len(t)

def baski(m):
    """Baskı yönü: tabana otur, ortala."""
    bb = m.bounding_box()
    return m.translate([-(bb[0] + bb[3]) / 2, -(bb[1] + bb[4]) / 2, -bb[2]])

if __name__ == '__main__':
    out = sys.argv[1]
    G, K, D = govde(), kapak(), dugme()
    for ad, m in [('govde', G), ('kapak', K), ('dugme', D)]:
        assert m.status().name == 'NoError', (ad, m.status())
        print(ad, 'hacim', round(m.volume(), 1), 'genus', m.genus(), 'bb', [round(x, 2) for x in m.bounding_box()])
    stl_yaz(baski(G.rotate([180, 0, 0])), f'{out}/govde.stl')   # üst yüzey tablada
    stl_yaz(baski(K), f'{out}/kapak.stl')
    stl_yaz(baski(D.rotate([180, 0, 0])), f'{out}/dugme.stl')   # üst yüzey tablada, delik yukarı
    # montaj önizlemesi için
    stl_yaz(G + K, f'{out}/_montaj.stl')
    print('tamam')
