# Makro Klavye v2 - bağlantı şeması (1) ve kasa içi kablo yerleşimi (2)
import sys
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, Rectangle, Circle, Polygon, FancyArrowPatch
import tasarim3 as T

plt.rcParams['font.family'] = 'DejaVu Sans'
out = sys.argv[1]

INK = '#1f2937'; SOFT = '#6b7280'; BG = '#f8fafc'
RENK = {
    '5V': '#dc2626', '3V3': '#ea580c', 'GND': '#111827',
    'ekran': '#2563eb', 'encL': '#7c3aed', 'encR': '#9333ea',
    'tus': '#059669', 'led': '#d97706', 'bos': '#cbd5e1',
}

# RP2040-Zero pin dizilimi (USB yukarıda, üstten bakış)
SOL = ['5V', 'GND', '3V3', 'GP29', 'GP28', 'GP27', 'GP26', 'GP15', 'GP14']
SAG = ['GP0', 'GP1', 'GP2', 'GP3', 'GP4', 'GP5', 'GP6', 'GP7', 'GP8']
ALT = ['GP9', 'GP10', 'GP11', 'GP12', 'GP13']

BAG = {  # pin -> (açıklama, renk anahtarı)
    '5V': ('LED şeridi 5V', '5V'),
    'GND': ('ORTAK GND (hepsi)', 'GND'),
    '3V3': ('Ekran VCC', '3V3'),
    'GP29': ('boş', 'bos'), 'GP28': ('boş', 'bos'),
    'GP27': ('LED DIN (330 Ω üzerinden)', 'led'),
    'GP26': ('Tuş 6', 'tus'), 'GP15': ('Tuş 5', 'tus'), 'GP14': ('Tuş 4', 'tus'),
    'GP0': ('Sol encoder A', 'encL'), 'GP1': ('Sol encoder B', 'encL'), 'GP2': ('Sol encoder bas (SW)', 'encL'),
    'GP3': ('Sağ encoder A', 'encR'), 'GP4': ('Sağ encoder B', 'encR'), 'GP5': ('Sağ encoder bas (SW)', 'encR'),
    'GP6': ('Tuş 1', 'tus'), 'GP7': ('Tuş 2', 'tus'), 'GP8': ('Tuş 3', 'tus'),
    'GP9': ('Ekran DC', 'ekran'), 'GP10': ('Ekran SCK', 'ekran'), 'GP11': ('Ekran SDA', 'ekran'),
    'GP12': ('Ekran RES', 'ekran'), 'GP13': ('Ekran BLK', 'ekran'),
}


def kutu(ax, x, y, w, h, baslik, alt=None, fc='white', ec=INK):
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle='round,pad=0.6,rounding_size=2.2', fc=fc, ec=ec, lw=1.6))
    ax.text(x + w / 2, y + h - 3.2, baslik, ha='center', va='center', fontsize=13, weight='bold', color=INK)
    if alt:
        ax.text(x + w / 2, y + h - 7.0, alt, ha='center', va='center', fontsize=9.5, color=SOFT)


def uc(ax, x, y, ad, hedef, renk, yon='sol', uzun=9):
    """bileşen pini: kısa tel + hedef etiketi"""
    dx = -uzun if yon == 'sol' else (uzun if yon == 'sag' else 0)
    dy = -uzun if yon == 'alt' else (uzun if yon == 'ust' else 0)
    ax.plot([x, x + dx], [y, y + dy], color=RENK[renk], lw=3, solid_capstyle='round')
    ax.add_patch(Circle((x, y), 0.9, fc=RENK[renk], ec='none', zorder=3))
    if yon in ('sol', 'sag'):
        ax.text(x + (1.6 if yon == 'sol' else -1.6), y, ad, ha='left' if yon == 'sol' else 'right', va='center',
                fontsize=10, weight='bold', color=INK)
        ax.text(x + dx + (-1.2 if yon == 'sol' else 1.2), y, hedef, ha='right' if yon == 'sol' else 'left', va='center',
                fontsize=10, color=RENK[renk] if renk != 'GND' else INK, weight='bold')
    else:
        ax.text(x, y + (1.8 if yon == 'alt' else -1.8), ad, ha='center', va='bottom' if yon == 'alt' else 'top',
                fontsize=9.5, weight='bold', color=INK, rotation=90 if len(ad) > 3 else 0)
        ax.text(x, y + dy + (-1.4 if yon == 'alt' else 1.4), hedef, ha='center', va='top' if yon == 'alt' else 'bottom',
                fontsize=9.5, color=RENK[renk] if renk != 'GND' else INK, weight='bold', rotation=90)


# =====================================================================
# 1) BAĞLANTI ŞEMASI
# =====================================================================
def liste_kutu(ax, x, y_ust, w, baslik, alt, satirlar, renk, cizim=None, not_=None):
    """satirlar: (bileşen pini, Zero hedefi, renk anahtarı)"""
    sat_h = 4.6
    h = 12 + len(satirlar) * sat_h + (5 if not_ else 0) + (cizim[1] if cizim else 0)
    y = y_ust - h
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle='round,pad=0.6,rounding_size=2.2', fc='white', ec=RENK[renk], lw=1.8))
    ax.add_patch(Rectangle((x - 0.6, y_ust - 9), w + 1.2, 9.6, fc=RENK[renk], ec='none', alpha=.12))
    ax.text(x + 2, y_ust - 3.4, baslik, fontsize=13, weight='bold', color=INK, va='center')
    ax.text(x + 2, y_ust - 7.4, alt, fontsize=9.2, color=SOFT, va='center')
    yy = y_ust - 13
    if cizim:
        cizim[0](x, yy, w)
        yy -= cizim[1]
    for (pin, hedef, r) in satirlar:
        ax.text(x + 3, yy, pin, fontsize=11, weight='bold', color=INK, va='center')
        ax.plot([x + w * 0.36, x + w * 0.62], [yy, yy], color=RENK[r], lw=3.2, solid_capstyle='round')
        ax.add_patch(Circle((x + w * 0.36, yy), 0.8, fc=RENK[r], ec='none'))
        ax.text(x + w * 0.65, yy, hedef, fontsize=11, weight='bold', va='center',
                color=RENK[r] if r not in ('GND',) else INK)
        yy -= sat_h
    if not_:
        ax.text(x + 2, yy + 0.6, not_, fontsize=9.2, color='#b91c1c', weight='bold', va='center')
    return y


fig = plt.figure(figsize=(24, 15), dpi=100)
fig.patch.set_facecolor(BG)
ax = fig.add_axes([0, 0, 1, 1]); ax.set_xlim(0, 240); ax.set_ylim(0, 150); ax.axis('off')
ax.text(4, 144.5, 'Makro Klavye v2 · Bağlantı şeması', fontsize=26, weight='bold', color=INK)
ax.text(4, 139.8, 'Ortada RP2040-Zero ve her pininin nereye gittiği; çevredeki kutularda her parçanın hangi bacağının '
        'Zero\'nun hangi pinine bağlanacağı.', fontsize=12, color=SOFT)

# --- RP2040-Zero ---
zx, zy, zw, zh = 96, 54, 36, 52
ax.add_patch(FancyBboxPatch((zx, zy), zw, zh, boxstyle='round,pad=0.4,rounding_size=2', fc='#111827', ec='black', lw=2))
ax.add_patch(Rectangle((zx + zw / 2 - 5, zy + zh - 1), 10, 5, fc='#cbd5e1', ec='black', lw=1))
ax.text(zx + zw / 2, zy + zh + 6.2, 'USB-C → kasanın arka duvarı', ha='center', fontsize=10, color=SOFT)
ax.text(zx + zw / 2, zy + zh / 2 + 6, 'RP2040-Zero', ha='center', va='center', color='white', fontsize=16, weight='bold')
ax.text(zx + zw / 2, zy + zh / 2 + 1, 'üstten görünüm', ha='center', va='center', color='#94a3b8', fontsize=10)
ax.add_patch(Circle((zx + zw / 2, zy + zh / 2 - 7), 2.2, fc='#22c55e', ec='none'))
ax.text(zx + zw / 2, zy + zh / 2 - 11.5, 'kart üstü LED (GP16)\ndurum ışığı', ha='center', va='top', color='#94a3b8', fontsize=8.8)
py = np.linspace(zy + zh - 5, zy + 6, 9)
for i, p in enumerate(SOL):
    a, r = BAG[p]
    ax.add_patch(Circle((zx, py[i]), 1.15, fc='#e5e7eb', ec='black', zorder=4))
    ax.text(zx + 2.3, py[i], p, color='white', fontsize=10, va='center', weight='bold')
    ax.plot([zx - 1.2, zx - 14], [py[i], py[i]], color=RENK[r], lw=3.4, solid_capstyle='round')
    ax.text(zx - 15.4, py[i], a, ha='right', va='center', fontsize=11.5,
            color={'bos': SOFT, 'GND': INK}.get(r, RENK[r]), weight='bold')
for i, p in enumerate(SAG):
    a, r = BAG[p]
    ax.add_patch(Circle((zx + zw, py[i]), 1.15, fc='#e5e7eb', ec='black', zorder=4))
    ax.text(zx + zw - 2.3, py[i], p, color='white', fontsize=10, va='center', ha='right', weight='bold')
    ax.plot([zx + zw + 1.2, zx + zw + 14], [py[i], py[i]], color=RENK[r], lw=3.4, solid_capstyle='round')
    ax.text(zx + zw + 15.4, py[i], a, ha='left', va='center', fontsize=11.5, color=RENK[r], weight='bold')
px = np.linspace(zx + 6, zx + zw - 6, 5)
for i, p in enumerate(ALT):
    a, r = BAG[p]
    ax.add_patch(Circle((px[i], zy), 1.15, fc='#e5e7eb', ec='black', zorder=4))
    ax.text(px[i], zy + 2.4, p, color='white', fontsize=9, ha='center', va='bottom', weight='bold', rotation=90)
    ax.plot([px[i], px[i]], [zy - 1.2, zy - 6], color=RENK[r], lw=3.4, solid_capstyle='round')
    ax.text(px[i], zy - 7, a, ha='center', va='top', fontsize=10.5, color=RENK[r], weight='bold', rotation=90)

# --- Notlar (sol üst) ---
notlar = [
    ('Ortak GND', 'Zero GND → ekran, encoder C ve S2, tuşların 2. bacağı, LED GND.'),
    ('Konnektör', 'Tuş telleri kapak 1x8, ekran ve encoder telleri panel 1x14 konnektöründen geçer'),
    ('', '(pin sırası: konnektor_semasi.png). GND her iki konnektörden de gelir.'),
    ('Güç', '5V yalnızca LED\'lere, ekran 3V3\'ten. LED parlaklığı yazılımda %40.'),
    ('Direnç', 'Tuş ve encoder için direnç yok (iç pull-up). Sadece LED veri hattında 330 Ω.'),
    ('Encoder yönü', 'Ters dönerse telleri değil yazılımı değiştiririz.'),
]
for i, (bb, t) in enumerate(notlar):
    ax.text(4, 132 - i * 4.6, bb, fontsize=11.5, weight='bold', color=INK, va='center')
    ax.text(30, 132 - i * 4.6, t, fontsize=11, color='#374151', va='center')

# --- Encoderlar (sağ üst) ---
def ec11_ciz(x, y, w):
    cx = x + w / 2
    ax.add_patch(Rectangle((cx - 9, y - 13), 18, 13, fc='#f3f4f6', ec=INK, lw=1.2))
    ax.add_patch(Circle((cx, y - 6.5), 3.2, fc='white', ec=INK))
    for k, ad in enumerate(['A', 'C', 'B']):
        bx = cx - 6 + k * 6
        ax.plot([bx, bx], [y - 13, y - 16], color=INK, lw=2)
        ax.text(bx, y - 17.5, ad, ha='center', va='top', fontsize=9.5, weight='bold')
    for k, ad in enumerate(['S1', 'S2']):
        bx = cx - 3 + k * 6
        ax.plot([bx, bx], [y, y + 0.0 - (-3)], color=INK, lw=2)
    ax.text(cx, y + 3.6, 'S1   S2', ha='center', va='bottom', fontsize=9.5, weight='bold')
    ax.text(cx + 11, y - 6.5, 'alttan\nbakış', fontsize=8.5, color=SOFT, va='center')


for i, (ad, a_, b_, sw, r) in enumerate([('Sol encoder', 'GP0', 'GP1', 'GP2', 'encL'),
                                         ('Sağ encoder', 'GP3', 'GP4', 'GP5', 'encR')]):
    liste_kutu(ax, 186 + 0 * i, 136 - i * 0, 50, ad, 'EC11, 3 bacak + 2 bacak (bas)', [], r) if False else None
y_sol = liste_kutu(ax, 184, 136, 52, 'Sol encoder', 'EC11 · 3 bacaklı taraf + bas anahtarı',
                   [('A', 'GP0', 'encL'), ('B', 'GP1', 'encL'), ('C (orta)', 'GND', 'GND'), ('S1', 'GP2', 'encL'), ('S2', 'GND', 'GND')],
                   'encL', cizim=(ec11_ciz, 26))
y_sag = liste_kutu(ax, 184, y_sol - 4, 52, 'Sağ encoder', 'aynı bağlantı, farklı pinler',
                   [('A', 'GP3', 'encR'), ('B', 'GP4', 'encR'), ('C (orta)', 'GND', 'GND'), ('S1', 'GP5', 'encR'), ('S2', 'GND', 'GND')],
                   'encR')

# --- Tuşlar (sol orta) ---
def tus_ciz(x, y, w):
    yer = [('T1', 'GP6'), ('T2', 'GP7'), ('T3', 'GP8'), ('T4', 'GP14'), ('T5', 'GP15'), ('T6', 'GP26')]
    for i, (ad, pin) in enumerate(yer):
        cx = x + 8 + (i % 3) * 13; cy = y - 4 - (i // 3) * 12
        ax.add_patch(Rectangle((cx - 5.5, cy - 4.8), 11, 9.6, fc='#ecfdf5', ec=RENK['tus'], lw=1.3))
        ax.text(cx, cy + 1.9, ad, ha='center', va='center', fontsize=10, weight='bold')
        ax.text(cx, cy - 2.2, pin, ha='center', va='center', fontsize=10, weight='bold', color=RENK['tus'])
    ax.text(x + w - 2, y - 4, 'arka\nsıra', fontsize=8.5, color=SOFT, ha='right', va='center')
    ax.text(x + w - 2, y - 16, 'ön\nsıra', fontsize=8.5, color=SOFT, ha='right', va='center')


liste_kutu(ax, 4, 104, 49, '6 MX anahtar', 'her anahtarın 2 bacağı var, yönü yok',
           [('1. bacak', 'kendi GP pini', 'tus'), ('2. bacak', 'ortak GND', 'GND')], 'tus', cizim=(tus_ciz, 26))

# --- Ekran (alt orta-sol) ---
liste_kutu(ax, 4, 52, 49, 'Ekran ST7789 240x240', 'GMT130 · pin başlığı soldan sağa',
           [('GND', 'GND', 'GND'), ('VCC', '3V3', '3V3'), ('SCK', 'GP10', 'ekran'), ('SDA', 'GP11', 'ekran'),
            ('RES', 'GP12', 'ekran'), ('DC', 'GP9', 'ekran'), ('BLK', 'GP13', 'ekran')], 'ekran',
           not_='VCC → 3V3 (5V değil!)  ·  CS pini yok')

# --- LED zinciri (alt) ---
lx, ly, lw_, lh = 70, 4, 166, 30
ax.add_patch(FancyBboxPatch((lx, ly), lw_, lh, boxstyle='round,pad=0.6,rounding_size=2.2', fc='white', ec=RENK['led'], lw=1.8))
ax.add_patch(Rectangle((lx - 0.6, ly + lh - 9), lw_ + 1.2, 9.6, fc=RENK['led'], ec='none', alpha=.12))
ax.text(lx + 2, ly + lh - 3.4, 'RGB LED zinciri · 18x WS2812B', fontsize=13, weight='bold', color=INK, va='center')
ax.text(lx + 2, ly + lh - 7.4, 'her parçanın DIN\'i bir öncekinin DOUT\'una; 5V ve GND tüm parçalara (şerit pedlerinden)',
        fontsize=9.2, color=SOFT, va='center')
yv = ly + 11
ax.text(lx + 3, yv, 'GP27', fontsize=11, weight='bold', color=RENK['led'], va='center')
ax.plot([lx + 12, lx + 16], [yv, yv], color=RENK['led'], lw=3)
ax.add_patch(Rectangle((lx + 16, yv - 1.5), 9, 3, fc='#fde68a', ec=INK))
ax.text(lx + 20.5, yv + 3.4, '330 Ω', ha='center', fontsize=9.5, weight='bold')
ax.plot([lx + 25, lx + 30], [yv, yv], color=RENK['led'], lw=3)
xs = [lx + 30 + i * 18 for i in range(6)]
etik = ['1', '2', '3', '…', '12', '13…18']
for i, x in enumerate(xs):
    ax.add_patch(Rectangle((x, yv - 5), 13, 10, fc='#fff7ed', ec=RENK['led'], lw=1.4))
    ax.add_patch(Rectangle((x + 4.5, yv - 2), 4, 4, fc='#fcd34d', ec='none'))
    ax.text(x + 1.0, yv + 3.5, 'DIN', fontsize=6.8, color=SOFT)
    ax.text(x + 12.0, yv + 3.5, 'DOUT', fontsize=6.8, color=SOFT, ha='right')
    ax.text(x + 6.5, yv - 7.0, etik[i], ha='center', va='center', fontsize=10, weight='bold', color=INK)
    if i < 5:
        ax.annotate('', xy=(xs[i + 1], yv), xytext=(x + 13, yv), arrowprops=dict(arrowstyle='->', color=RENK['led'], lw=2))
ax.text(xs[-1] + 16, yv + 3.0, '1–12: taban (yüzü\ntabana dönük)', fontsize=9, color=INK, va='center')
ax.text(xs[-1] + 16, yv - 4.0, '13–18: tuşların\naltında', fontsize=9, color=INK, va='center')
ax.text(lx + 3, ly + 2.4, 'İsteğe bağlı: ilk LED\'in 5V ile GND\'si arasına 470–1000 µF kondansatör (açılışta ani akımı yumuşatır).',
        fontsize=9, color=SOFT, va='center')

fig.savefig(f'{out}/baglanti_semasi.png', facecolor=BG)
plt.close(fig)

# =====================================================================
# 2) KASA İÇİ KABLO YERLEŞİMİ (üstten)
# =====================================================================
fig = plt.figure(figsize=(16, 13), dpi=100)
fig.patch.set_facecolor(BG)
ax = fig.add_axes([0.03, 0.04, 0.94, 0.86]); ax.set_aspect('equal'); ax.axis('off')
fig.text(0.03, 0.955, 'Kasa içi kablo yerleşimi (üstten, kapaklar şeffaf)', fontsize=22, weight='bold', color=INK)
fig.text(0.03, 0.925, 'Zero arka ortada; ekran ve encoderlar eğik panelin altında, tuşlar düz kapağın altında. '
         'Kapakların açılabilmesi için tellerde 5-6 cm pay bırak.', fontsize=11.5, color=SOFT)
ya, za = T.arka()


def cs_ciz(cs, **kw):
    for p in cs.to_polygons():
        ax.add_patch(Polygon(np.asarray(p), closed=True, **kw))


cs_ciz(T.taban_cizgi(), fc='#e2e8f0', ec=INK, lw=1.4)
cs_ciz(T.taban_cizgi(-T.DUVAR), fc='#f8fafc', ec='#94a3b8', lw=0.8, ls='--')
ax.plot([-T.W2, T.W2], [T.Y_M, T.Y_M], color=SOFT, lw=1, ls='-.')
ax.text(T.W2 + 2, T.Y_M, 'eğik panel ↑\ndüz kapak ↓', fontsize=9, color=SOFT, va='center')
# izdüşüm
def izd(x, y):
    q = T.panel_nokta(x, y); return q[0], q[1]

# Zero
y0, y1 = T.zero_y0(), T.zero_y1()
ax.add_patch(Rectangle((T.ZERO_X - 9, y0), 18, T.ZERO_UZ, fc='#111827', ec='black', zorder=5))
ax.add_patch(Rectangle((T.ZERO_X - 4.45, y1 + 0.6 - 7.3), 8.9, 7.3, fc='#cbd5e1', ec='black', zorder=6))
ax.text(T.ZERO_X, y0 + 7, 'Zero', color='white', ha='center', va='center', fontsize=10, weight='bold', zorder=7)
# ekran
kart = [izd(T.KART_SOL, -T.KART_Y / 2), izd(T.KART_SOL + T.KART_X, -T.KART_Y / 2), izd(T.KART_SOL + T.KART_X, T.KART_Y / 2), izd(T.KART_SOL, T.KART_Y / 2)]
ax.add_patch(Polygon(kart, closed=True, fc='#dbeafe', ec=RENK['ekran'], lw=1.5, alpha=.85, zorder=2))
ax.text(*izd(6, -8), 'ekran (arkası)', ha='center', fontsize=9, color=RENK['ekran'], zorder=3)
pin_x = T.KART_SOL + 2.0
pinler = [izd(pin_x, yy) for yy in np.linspace(-7.6, 7.6, 7)]
for p in pinler:
    ax.add_patch(Circle(p, 0.7, fc=RENK['ekran'], zorder=4))
# encoderlar
for x, r, ad in ((-T.ENC_X, 'encL', 'sol enc.'), (T.ENC_X, 'encR', 'sağ enc.')):
    ex, ey = izd(x, 0)
    ax.add_patch(Rectangle((ex - 6, ey - 6), 12, 12 * np.cos(np.radians(22)), fc='#f3e8ff', ec=RENK[r], lw=1.5, zorder=2))
    ax.text(ex, ey + 8, ad, ha='center', fontsize=9, color=RENK[r], weight='bold')
# tuşlar
tnum = {(0, 0): 1, (0, 1): 2, (0, 2): 3, (1, 0): 4, (1, 1): 5, (1, 2): 6}
for i, y in enumerate(T.SATIR):
    for j, x in enumerate(T.SUTUN):
        ax.add_patch(Rectangle((x - 7, y - 7), 14, 14, fc='white', ec=RENK['tus'], lw=1.5, zorder=2))
        ax.text(x, y, f'T{tnum[(i, j)]}', ha='center', va='center', fontsize=11, weight='bold', color=RENK['tus'])
# LED'ler
for k, (x, y, o) in enumerate(T.led_taban_konumlari()):
    w, h = (T.LED_BOY, T.LED_EN) if o == 'x' else (T.LED_EN, T.LED_BOY)
    ax.add_patch(Rectangle((x - w / 2, y - h / 2), w, h, fc='#fff7ed', ec=RENK['led'], lw=1, zorder=1))
    ax.text(x, y, str(k + 1), ha='center', va='center', fontsize=8, color=RENK['led'], weight='bold')
n = len(T.led_taban_konumlari())
for k, (x, y, z) in enumerate(T.led_tus_konumlari()):
    ax.add_patch(Rectangle((x - T.LED_BOY / 2, y - T.LED_EN / 2), T.LED_BOY, T.LED_EN, fc='none', ec=RENK['led'], lw=1, ls='--', zorder=3))
    ax.text(x + 6.5, y, str(n + k + 1), ha='center', va='center', fontsize=8, color=RENK['led'], weight='bold', zorder=4)

def tel(noktalar, renk, lw=2.2, ls='-'):
    p = np.asarray(noktalar)
    ax.plot(p[:, 0], p[:, 1], color=RENK[renk], lw=lw, ls=ls, solid_capstyle='round', zorder=8, alpha=.9)

zx0, zx1 = T.ZERO_X - 9, T.ZERO_X + 9
# ekran demeti: Zero'nun alt kenarı (GP9-13) -> ekran pinleri (ekran Zero'nun hemen üstünde, panelin altında)
pm = pinler[3]
tel([(T.ZERO_X - 3, y0), (T.ZERO_X - 3, y0 - 3), (pm[0] + 4, y0 - 3), (pm[0] + 4, pm[1]), (pm[0] + 0.8, pm[1])], 'ekran', 3.2)
# encoder demetleri: Zero sağ kenarı (GP0-5)
eL = izd(-T.ENC_X, 0); eR = izd(T.ENC_X, 0)
tel([(zx1, y0 + 19), (zx1 + 6, y0 + 19), (zx1 + 6, eR[1]), (eR[0] - 6, eR[1])], 'encR', 3.0)
tel([(zx1, y0 + 21), (zx1 + 3, y0 + 21), (zx1 + 3, ya - 9), (eL[0], ya - 9), (eL[0], eL[1] + 6 * np.cos(np.radians(22)))], 'encL', 3.0)
# tuş demeti: Zero sol kenarı -> menteşeyi geçip iki sıranın arasındaki gövde hattı
trunk_y = (T.SATIR[0] + T.SATIR[1]) / 2          # -32.5
gx = -38.0
tel([(zx0, y0 + 10), (gx, y0 + 10), (gx, trunk_y), (T.SUTUN[2], trunk_y)], 'tus', 3.2)
for i, y in enumerate(T.SATIR):
    for x in T.SUTUN:
        ky = y - 7 if i == 0 else y + 7                 # anahtar bacakları iki sıranın arasındaki kenarda
        tel([(x - 3, trunk_y), (x - 3, ky)], 'tus', 2.2)
# GND hattı: Zero GND -> tuş gövdesine paralel, encoder ve ekrana da
tel([(zx0, y0 + 13), (gx - 3, y0 + 13), (gx - 3, trunk_y - 2), (T.SUTUN[2] + 6, trunk_y - 2)], 'GND', 2.2, '--')
for i, y in enumerate(T.SATIR):
    for x in T.SUTUN:
        ky = y - 7 if i == 0 else y + 7
        tel([(x + 3, trunk_y - 2), (x + 3, ky)], 'GND', 1.6, '--')
ax.text(T.W2 + 3, trunk_y - 1, 'tuş telleri (yeşil) ve\nortak GND (kesikli)\niki sıranın arasından', fontsize=8.8,
        va='center', color=INK)
# LED zinciri: Zero -> 1 (arka sol) -> duvar boyunca -> 12 (arka sağ) -> tuş LED'leri 13-18
pts = [(x, y) for (x, y, o) in T.led_taban_konumlari()]
tel([(T.ZERO_X - 6, y0 + 3), (pts[0][0] + 6, y0 + 3), (pts[0][0] + 6, pts[0][1])], 'led', 2.4)
for a_, b_ in zip(pts[:-1], pts[1:]):
    tel([a_, b_], 'led', 2.0)
kp = [(x, y) for (x, y, z) in T.led_tus_konumlari()]
tel([pts[-1], (pts[-1][0] + 12, pts[-1][1] - 8), (kp[0][0] + 12, kp[0][1]), kp[0]], 'led', 2.0, ':')
for a_, b_ in zip(kp[:3], kp[1:3]):
    tel([a_, b_], 'led', 2.0, ':')
tel([kp[2], (kp[2][0] - 12, kp[2][1]), (kp[3][0] - 12, kp[3][1]), kp[3]], 'led', 2.0, ':')
for a_, b_ in zip(kp[3:-1], kp[4:]):
    tel([a_, b_], 'led', 2.0, ':')

# lejant
lej = [('ekran', 'Ekran demeti (7 tel) → GP9-13, 3V3, GND'), ('encL', 'Sol encoder (5 tel) → GP0-2, GND'),
       ('encR', 'Sağ encoder (5 tel) → GP3-5, GND'), ('tus', 'Tuş telleri (6) → GP6, 7, 9, 14, 15, 26'),
       ('GND', 'Ortak GND (kesikli)'), ('led', 'LED zinciri: taban 1→12 (düz), sonra tuş LED\'leri 13→18 (noktalı)')]
for i, (r, t) in enumerate(lej):
    yy = T.Y_ON - 10 - i * 5.0
    ax.plot([-T.W2 - 8, -T.W2 - 1], [yy, yy], color=RENK[r], lw=3, ls='--' if r == 'GND' else '-')
    ax.text(-T.W2 + 1, yy, t, va='center', fontsize=10.5, color=INK)
ax.set_xlim(-T.W2 - 12, T.W2 + 22); ax.set_ylim(T.Y_ON - 42, ya + 8)
fig.savefig(f'{out}/kablo_yerlesimi.png', facecolor=BG)
print('ok')
