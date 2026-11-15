# Teknik çizim sayfası: üstten ölçülü görünüm + LED zinciri, yan profil, pin tablosu, parça listesi
import sys
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import PathPatch, FancyArrowPatch, Rectangle, Circle, Polygon
from matplotlib.path import Path
import tasarim3 as T

plt.rcParams['font.family'] = 'DejaVu Sans'
INK = '#1f2937'; SOFT = '#6b7280'; ACC = '#ea580c'; LEDC = '#f59e0b'

fig = plt.figure(figsize=(17, 11.5), dpi=100)
fig.patch.set_facecolor('#f8fafc')
fig.text(0.03, 0.955, 'Makro Klavye v2 · Teknik çizim', fontsize=24, weight='bold', color=INK)
fig.text(0.03, 0.925, 'Ölçüler mm. Üstten görünüm (kapaklar şeffaf çizildi), yan profil, LED zinciri ve pin planı.',
         fontsize=12, color=SOFT)


def cs_ciz(ax, cs, **kw):
    for p in cs.to_polygons():
        p = np.asarray(p)
        ax.add_patch(Polygon(p, closed=True, **kw))


def olcu(ax, p0, p1, metin, ofs, yon='h', renk=INK):
    (x0, y0), (x1, y1) = p0, p1
    if yon == 'h':
        y = ofs
        ax.plot([x0, x0], [y0, y], color=renk, lw=0.6); ax.plot([x1, x1], [y1, y], color=renk, lw=0.6)
        ax.add_patch(FancyArrowPatch((x0, y), (x1, y), arrowstyle='<->', mutation_scale=10, color=renk, lw=0.9))
        ax.text((x0 + x1) / 2, y + 1.2, metin, ha='center', va='bottom', fontsize=10, color=renk)
    else:
        x = ofs
        ax.plot([x0, x], [y0, y0], color=renk, lw=0.6); ax.plot([x1, x], [y1, y1], color=renk, lw=0.6)
        ax.add_patch(FancyArrowPatch((x, y0), (x, y1), arrowstyle='<->', mutation_scale=10, color=renk, lw=0.9))
        ax.text(x - 1.2, (y0 + y1) / 2, metin, ha='right', va='center', fontsize=10, color=renk, rotation=90)


# ---------------- Üstten görünüm ----------------
ax = fig.add_axes([0.02, 0.05, 0.56, 0.84])
ax.set_aspect('equal'); ax.axis('off')
ya, za = T.arka()
cs_ciz(ax, T.taban_cizgi(), fc='#e2e8f0', ec=INK, lw=1.4)
cs_ciz(ax, T.taban_cizgi(-T.DUVAR), fc='#f1f5f9', ec='#94a3b8', lw=0.8, ls='--')
ax.plot([-T.W2, T.W2], [T.Y_M, T.Y_M], color=SOFT, lw=1.0, ls='-.')
ax.text(T.W2 + 1.5, T.Y_M, 'eğim başlangıcı\n(kapak/panel birleşimi)', fontsize=8.5, color=SOFT, va='center')
# taban LED'leri
def taban_ledleri():
    for i, (x, y, o) in enumerate(T.led_taban_konumlari()):
        w, h = (T.LED_BOY, T.LED_EN) if o == 'x' else (T.LED_EN, T.LED_BOY)
        ax.add_patch(Rectangle((x - w / 2, y - h / 2), w, h, fc='#fff7ed', ec=LEDC, lw=1.2, alpha=0.55, zorder=6))
        ax.add_patch(Rectangle((x - 2.5, y - 2.5), 5, 5, fc=LEDC, ec='none', zorder=7))
        ax.text(x, y, str(i + 1), ha='center', va='center', fontsize=8, color='white', weight='bold', zorder=8)
# Zero
y0, y1 = T.zero_y0(), T.zero_y1()
ax.add_patch(Rectangle((T.ZERO_X - 9, y0), 18, T.ZERO_UZ, fc='#111827', ec='black', lw=1, zorder=3))
ax.add_patch(Rectangle((T.ZERO_X - 4.45, y1 + 0.6 - 7.3), 8.9, 7.3, fc='#cbd5e1', ec='black', lw=0.6, zorder=3))
ax.text(T.ZERO_X, y0 + 8, 'RP2040\nZero', color='white', fontsize=8, ha='center', va='center', weight='bold', zorder=4)
ax.annotate('USB-C ağzı 9.3 x 3.7 (arka duvar)', xy=(0, ya), xytext=(0, ya + 9), fontsize=9.5, color=ACC,
            arrowprops=dict(arrowstyle='->', color=ACC), ha='center')
# tuşlar, halkalar, tuş LED'leri
nled = len(T.led_taban_konumlari())
for s_ in (0, 1):
    bb = T.led_blok_yerlesik(s_).bounding_box()
    ax.add_patch(Rectangle((bb[0], bb[1]), bb[3] - bb[0], bb[4] - bb[1], fc='#e0f2fe', ec='#38bdf8', lw=0.8, zorder=0.5))
pk = T.pk_olcu()
ax.add_patch(Rectangle((-18.9, pk['yc'] - 2.4), 37.8, 4.8, fc='#ccfbf1', ec='#0f766e', lw=0.9, zorder=2))
ax.text(19.6, pk['yc'] - 3.8, 'panel konn. 1x14', fontsize=7.5, color='#0f766e')
ax.add_patch(Rectangle((-46.3, T.KONN_Y - 11.3), 6.6, 22.6, fc='#ccfbf1', ec='#0f766e', lw=0.9, zorder=2))
ax.text(-47.5, T.KONN_Y, 'kapak\nkonn.\n1x8', fontsize=7.5, color='#0f766e', ha='right', va='center')
for (x, y) in T.AYAKLAR:
    ax.add_patch(Circle((x, y), 5.0, fc='none', ec='#94a3b8', lw=0.8, ls=':', zorder=0.6))
for j, (x, y) in enumerate(T.led_tus_konumlari_son()):
    ax.add_patch(Rectangle((x - T.LED_BOY / 2, y - T.LED_EN / 2), T.LED_BOY, T.LED_EN, fc='none', ec='#7c3aed', lw=1.1, ls='--'))
    ax.add_patch(Rectangle((x - 2.5, y - 2.5), 5, 5, fc='#7c3aed', ec='none'))
    ax.text(x, y, str(nled + j + 1), ha='center', va='center', fontsize=8, color='white', weight='bold')
for y in T.SATIR:
    for x in T.SUTUN:
        dis, ic = T.tus_halkasi_cs(x, y)
        cs_ciz(ax, dis - ic, fc='#a78bfa', ec='#7c3aed', lw=0.6, alpha=.85)
        ax.add_patch(Rectangle((x - T.MX / 2, y - T.MX / 2), T.MX, T.MX, fc='white', ec=INK, lw=1))
taban_ledleri()
for (x, y) in T.AYAKLAR:
    ax.add_patch(Circle((x, y), 5.0, fc='none', ec='#64748b', lw=0.9, ls=':', zorder=6))
# ekran ve encoderlar (üstten izdüşüm)
c, s = T.T()
def izd(x, y):
    q = T.panel_nokta(x, y); return q[0], q[1]
pen = [izd(-T.PENCERE / 2, -T.PENCERE / 2), izd(T.PENCERE / 2, -T.PENCERE / 2), izd(T.PENCERE / 2, T.PENCERE / 2), izd(-T.PENCERE / 2, T.PENCERE / 2)]
ax.add_patch(Polygon(pen, closed=True, fc='#0b0f14', ec=INK))
kart = [izd(T.KART_SOL, -T.KART_Y / 2), izd(T.KART_SOL + T.KART_X, -T.KART_Y / 2), izd(T.KART_SOL + T.KART_X, T.KART_Y / 2), izd(T.KART_SOL, T.KART_Y / 2)]
ax.add_patch(Polygon(kart, closed=True, fc='none', ec='#2563eb', lw=1, ls='--'))
ax.text(*izd(0, -10), 'ekran', color='white', ha='center', va='center', fontsize=8)
for x in (-T.ENC_X, T.ENC_X):
    ex, ey = izd(x, 0)
    ax.add_patch(Circle((ex, ey), 9 * 1.0, fc='none', ec=SOFT, lw=0.8, ls=':'))
    ax.add_patch(Circle((ex, ey), T.ENC_DELIK / 2, fc='white', ec=INK, lw=1))
for (x, y) in T.DUZ_VIDA:
    ax.add_patch(Circle((x, y), T.HAVSA / 2, fc='#cbd5e1', ec=INK, lw=0.8))
for (x, y) in T.PANEL_VIDA:
    ax.add_patch(Circle(izd(x, y), T.HAVSA / 2, fc='#cbd5e1', ec=INK, lw=0.8))
# zincir yolu
pts = [(x, y) for (x, y, o) in T.led_taban_konumlari()] + list(T.led_tus_konumlari_son())
zx, zy = T.ZERO_X - 9, y0 + 4
yol = [(zx, zy)] + pts
for a, b in zip(yol[:-1], yol[1:]):
    ax.add_patch(FancyArrowPatch(a, b, arrowstyle='-|>', mutation_scale=8, color=ACC, lw=0.8, alpha=.6,
                                 connectionstyle='arc3,rad=0.12', shrinkA=4, shrinkB=4))
ax.text(zx - 1, zy - 5, '← GP27', color=ACC, fontsize=9, weight='bold', ha='right')
# ölçüler
olcu(ax, (-T.W2, T.Y_ON), (T.W2, T.Y_ON), '96', T.Y_ON - 8)
olcu(ax, (-T.W2, T.Y_ON), (-T.W2, ya), f'{ya - T.Y_ON:.1f}', -T.W2 - 8, 'v')
olcu(ax, (T.SUTUN[2] + 11.6, T.SATIR[1]), (T.SUTUN[2] + 11.6, T.SATIR[0]), '25', T.W2 + 9, 'v', renk='#7c3aed')
ax.text(-T.W2 - 38, -32, 'Tuş aralığı 25 x 25\nHalka 23.2 mm,\n1.4 mm genişlik', fontsize=9, color='#7c3aed')
ax.set_xlim(-T.W2 - 40, T.W2 + 40); ax.set_ylim(T.Y_ON - 19, ya + 14)
ax.text(-T.W2 - 38, ya + 10, 'ÜSTTEN', fontsize=13, weight='bold', color=INK)
ax.text(-T.W2 - 38, T.Y_ON - 18.5, 'Turuncu 1–12: taban LED\'leri (yüzü tabana dönük, tuşların altında kalanlar yarı saydam)\n'
        'Mor 13–18: tuş LED\'leri (tabandaki eğik bloklarda)   Yeşil: konnektörler   Noktalı daire: lastik ayak   → veri hattı', fontsize=9, color=SOFT)

# ---------------- Yan profil ----------------
ax2 = fig.add_axes([0.60, 0.53, 0.38, 0.36]); ax2.set_aspect('equal'); ax2.axis('off')
prof = [(T.Y_ON, 0), (ya, 0), (ya, za), (T.Y_M, T.Z_DUZ), (T.Y_ON, T.Z_DUZ)]
ax2.add_patch(Polygon(prof, closed=True, fc='#1e293b', ec=INK))
ax2.add_patch(Rectangle((T.Y_ON, 0), ya - T.Y_ON, T.Z_TABAN, fc='#fb923c', ec=INK))
ax2.plot([T.Y_ON, ya], [T.Z_DUZ - T.KAP, T.Z_DUZ - T.KAP], color='#475569', lw=0.8)
# tuşlar ve düğme silüetleri
for y in T.SATIR:
    ax2.add_patch(Polygon([(y - 9, T.Z_DUZ + 5.5), (y + 9, T.Z_DUZ + 5.5), (y + 7, T.Z_DUZ + 12.9), (y - 7, T.Z_DUZ + 12.9)], fc='#e7e5e4', ec=INK, lw=0.6))
    ax2.add_patch(Rectangle((y - 7.7, T.Z_DUZ), 15.4, 6, fc='#475569', ec=INK, lw=0.5))
k = [T.panel_nokta(0, yy, zz) for yy, zz in [(-9, 2.4), (9, 2.4), (8.4, 16.4), (-8.4, 16.4)]]
ax2.add_patch(Polygon([(q[1], q[2]) for q in k], fc='#e7e5e4', ec=INK, lw=0.6))
olcu(ax2, (T.Y_ON, 0), (ya, 0), f'{ya - T.Y_ON:.1f}', -9)
olcu(ax2, (T.Y_ON, 0), (T.Y_ON, T.Z_DUZ), '22', T.Y_ON - 5, 'v')
olcu(ax2, (ya, 0), (ya, za), f'{za:.0f}', ya + 13, 'v')
ax2.annotate('', xy=(T.Y_M + 16, T.Z_DUZ + 16 * np.tan(np.radians(22))), xytext=(T.Y_M + 16, T.Z_DUZ),
             arrowprops=dict(arrowstyle='-', color=ACC))
ax2.plot([T.Y_M, T.Y_M + 22], [T.Z_DUZ, T.Z_DUZ], color=ACC, lw=0.8, ls='--')
ax2.text(T.Y_M + 17, T.Z_DUZ + 1.2, '22°', color=ACC, fontsize=11, weight='bold')
ax2.text(T.Y_ON + 2, 1.0, 'şeffaf taban 3', fontsize=8, color='#7c2d12')
ax2.set_xlim(T.Y_ON - 12, ya + 18); ax2.set_ylim(-14, za + 22)
ax2.text(T.Y_ON - 10, za + 18, 'YANDAN', fontsize=13, weight='bold', color=INK)

# ---------------- Pin planı ve parça listesi ----------------
ax3 = fig.add_axes([0.60, 0.05, 0.38, 0.45]); ax3.axis('off')
satirlar = [
    ('PİN PLANI (RP2040-Zero)', None),
    ('Ekran ST7789', 'SCL GP10 · SDA GP11 · DC GP9 · RES GP12 · BLK GP13'),
    ('Sol encoder', 'A GP0 · B GP1 · bas GP2'),
    ('Sağ encoder', 'A GP3 · B GP4 · bas GP5'),
    ('Tuşlar 1–6', 'GP6 · GP7 · GP8 · GP14 · GP15 · GP26'),
    ('RGB LED (18)', 'DIN GP27 · 5V · GND'),
    ('', ''),
    ('PARÇA LİSTESİ', None),
    ('Baskı', 'taban (şeffaf PETG), gövde, düz kapak,'),
    ('', 'eğik panel, 6 halka (şeffaf/beyaz), 2 düğme, 6 tuş başlığı'),
    ('Elektronik', 'RP2040-Zero, ST7789 240x240, 2x EC11, 6x MX anahtar,'),
    ('', '18x WS2812B (60 LED/m şeritten tek tek kesilmiş)'),
    ('Vida', '8x M3x10 havşa (kapaklar), 4x M3x8 havşa (taban), 4x M2x4'),
    ('Konnektör', '1x40 erkek + 1x40 dişi header (1x8 ve 1x14 kesilir)'),
    ('Diğer', '4x 10 mm lastik ayak, 330 Ω direnç, 28 AWG ince kablo'),
    ('', ''),
    ('Not', 'LED parlaklığı yazılımda %40 ile sınırlanır'),
    ('', '(18 LED tam beyazda ~1.1 A; USB 0.5 A verir)'),
]
y = 0.98
for a, b in satirlar:
    if b is None:
        ax3.text(0.0, y, a, fontsize=12.5, weight='bold', color=INK, transform=ax3.transAxes); y -= 0.065; continue
    ax3.text(0.0, y, a, fontsize=10.5, weight='bold', color=INK, transform=ax3.transAxes)
    ax3.text(0.27, y, b, fontsize=10.5, color='#374151', transform=ax3.transAxes)
    y -= 0.055

fig.savefig(sys.argv[1] + '/v2_teknik.png', facecolor=fig.get_facecolor())
print('ok')
