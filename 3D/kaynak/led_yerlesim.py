# LED yerleşim kılavuzu: (1) taban LED'leri üstten, (2) düz kapağın altı, (3) kesitler
import sys
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Polygon, Rectangle, FancyArrowPatch, Circle
import tasarim3 as T

plt.rcParams['font.family'] = 'DejaVu Sans'
INK = '#1f2937'; SOFT = '#6b7280'; LED = '#d97706'; BG = '#f8fafc'
fig = plt.figure(figsize=(20, 12.5), dpi=100)
fig.patch.set_facecolor(BG)
fig.text(0.02, 0.955, 'LED yerleşimi', fontsize=26, weight='bold', color=INK)
fig.text(0.02, 0.922, 'Numaralar zincir sırasıdır (GP27 → 1 → 2 → … → 18). Her parçada okun yönü DIN → DOUT; '
         'ok bir sonraki LED\'i göstermeli.', fontsize=12.5, color=SOFT)


def cs(ax, c, **kw):
    for p in c.to_polygons():
        ax.add_patch(Polygon(np.asarray(p), closed=True, **kw))


def led(ax, x, y, o, n, ok, renk=LED, yuz='alt'):
    w, h = (T.LED_BOY, T.LED_EN) if o == 'x' else (T.LED_EN, T.LED_BOY)
    ax.add_patch(Rectangle((x - w / 2, y - h / 2), w, h, fc='#fff7ed', ec=renk, lw=1.6, zorder=3))
    ax.add_patch(Rectangle((x - 2.5, y - 2.5), 5, 5, fc='#fcd34d' if yuz == 'ust' else '#fde68a', ec=renk, lw=0.8, zorder=4))
    ax.text(x, y, str(n), ha='center', va='center', fontsize=10, weight='bold', color=INK, zorder=5)
    dx, dy = ok
    ax.add_patch(FancyArrowPatch((x - dx * 0.38 * w, y - dy * 0.38 * h), (x + dx * 0.38 * w, y + dy * 0.38 * h),
                                 arrowstyle='-|>', mutation_scale=11, color=renk, lw=1.4, zorder=5))


ya, za = T.arka()

# ---------------- 1) TABAN ----------------
ax = fig.add_axes([0.01, 0.05, 0.36, 0.84]); ax.set_aspect('equal'); ax.axis('off')
ax.set_title('1 · Tabanın üstü (üstten bakış)\nLED 1–12 tabandaki yuvalara, yüzü AŞAĞI', fontsize=14, weight='bold', color=INK, loc='left')
cs(ax, T.taban_cizgi(), fc='#fed7aa', ec=INK, lw=1.4)
cs(ax, T.taban_cizgi(-T.DUVAR), fc='#fff7ed', ec='#94a3b8', lw=0.8, ls='--')
y0, y1 = T.zero_y0(), T.zero_y1()
ax.add_patch(Rectangle((T.ZERO_X - 9, y0), 18, T.ZERO_UZ, fc='#111827', ec='black', zorder=2))
ax.text(T.ZERO_X, (y0 + y1) / 2, 'Zero', color='white', ha='center', va='center', fontsize=11, weight='bold', zorder=3)
for (x, y) in T.boss_merkezleri():
    ax.add_patch(Circle((x, y), T.BOSS_R, fc='#cbd5e1', ec=INK, lw=0.6, zorder=2))
for s_ in (0, 1):
    bb = T.led_blok_yerlesik(s_).bounding_box()
    ax.add_patch(Rectangle((bb[0], bb[1]), bb[3] - bb[0], bb[4] - bb[1], fc='#e0f2fe', ec='#38bdf8', lw=0.8, zorder=1))
    ax.text(0, (bb[1] + bb[4]) / 2, 'tuş LED bloğu', ha='center', va='center', fontsize=8, color='#0369a1', zorder=2)
pk = T.pk_olcu()
ax.add_patch(Rectangle((-18.9, pk['yc'] - 2.4), 37.8, 4.8, fc='#ccfbf1', ec='#0f766e', lw=0.8, zorder=1))
ax.text(0, pk['yc'], 'panel dişi 1x14', ha='center', va='center', fontsize=7.5, color='#0f766e', zorder=2)
ax.add_patch(Rectangle((-46.3, T.KONN_Y - 11.3), 6.6, 22.6, fc='#ccfbf1', ec='#0f766e', lw=0.8, zorder=1))
ax.text(-43, T.KONN_Y, 'kapak\ndişi\n1x8', ha='center', va='center', fontsize=7, color='#0f766e', zorder=2)
tl = T.led_taban_konumlari()
for i, (x, y, o) in enumerate(tl):
    if i + 1 < len(tl):
        nx, ny, _ = tl[i + 1]; v = np.array([nx - x, ny - y])
    else:
        v = np.array([1.0, -1.0])
    if o == 'x':
        ok = (np.sign(v[0]) or 1, 0)
    else:
        ok = (0, np.sign(v[1]) or 1)
    led(ax, x, y, o, i + 1, ok)
ax.annotate('GP27 (330 Ω)', xy=(tl[0][0] + 8, tl[0][1]), xytext=(-30, 41), fontsize=10, color=LED, weight='bold',
            arrowprops=dict(arrowstyle='->', color=LED))
ax.annotate('12 → 13\n(tuş LED\'lerine)', xy=(tl[-1][0] + 8, tl[-1][1]), xytext=(30, -5), fontsize=9.5, color=LED,
            ha='center', arrowprops=dict(arrowstyle='->', color=LED))
ax.text(-T.W2, T.Y_ON - 6, 'Ön', fontsize=11, color=SOFT); ax.text(-T.W2, ya + 2, 'Arka (USB)', fontsize=11, color=SOFT)
ax.set_xlim(-T.W2 - 4, T.W2 + 4); ax.set_ylim(T.Y_ON - 9, ya + 8)

# ---------------- 2) KAPAK ALTI ----------------
ax = fig.add_axes([0.38, 0.30, 0.34, 0.59]); ax.set_aspect('equal'); ax.axis('off')
ax.set_title('2 · Tuş LED\'leri (üstten bakış)\nLED 13–18 tabandaki eğik yükseltici bloklarda',
             fontsize=14, weight='bold', color=INK, loc='left')
kap = [(-T.W2, T.Y_ON), (T.W2, T.Y_ON), (T.W2, T.Y_M), (-T.W2, T.Y_M)]
ax.add_patch(Polygon(kap, closed=True, fc='#e2e8f0', ec=INK, lw=1.4))
tnum = {(0, 0): 1, (0, 1): 2, (0, 2): 3, (1, 0): 4, (1, 1): 5, (1, 2): 6}
for i, y in enumerate(T.SATIR):
    for j, x in enumerate(T.SUTUN):
        d, c_ = T.tus_halkasi_cs(x, y, T.HALKA_PAY, T.HALKA_PAY)
        cs(ax, d - c_, fc='#e9d5ff', ec='#7c3aed', lw=0.8)
        ax.add_patch(Rectangle((x - 7, y - 7), 14, 14, fc='#475569', ec=INK, lw=1, zorder=2))
        ax.text(x, y, f'T{tnum[(i, j)]}', ha='center', va='center', color='white', fontsize=12, weight='bold', zorder=3)
kl = T.led_tus_konumlari_son()
for s_ in (0, 1):
    bb = T.led_blok_yerlesik(s_).bounding_box()
    ax.add_patch(Rectangle((bb[0], bb[1]), bb[3] - bb[0], bb[4] - bb[1], fc='#e0f2fe', ec='#38bdf8', lw=0.8, zorder=1.5))
for k, (x, y) in enumerate(kl):
    if k < 3:
        ok = (-1, 0)
    else:
        ok = (1, 0)
    led(ax, x, y, 'x', len(tl) + k + 1, ok, yuz='ust')
for a_, b_ in zip(kl[:2], kl[1:3]):
    ax.plot([a_[0] - 8.5, b_[0] + 8.5], [a_[1], b_[1]], color=LED, lw=1.6)
ax.plot([kl[2][0] - 8.5, kl[2][0] - 12, kl[3][0] - 12, kl[3][0] - 8.5], [kl[2][1], kl[2][1], kl[3][1], kl[3][1]], color=LED, lw=1.6)
for a_, b_ in zip(kl[3:5], kl[4:]):
    ax.plot([a_[0] + 8.5, b_[0] - 8.5], [a_[1], b_[1]], color=LED, lw=1.6)
ax.annotate('12\'den gelen tel', xy=(kl[0][0] + 8.5, kl[0][1]), xytext=(T.W2 - 2, T.Y_M + 6), fontsize=10, color=LED,
            ha='right', arrowprops=dict(arrowstyle='->', color=LED))
ax.text(0, T.Y_ON - 6, 'Bloklar tabanla tek parça; kapağa LED teli gitmez.\n'
        'LED 30° eğik, ışığı halka yarığına yöneltir. Her LED kendi tuşunun hizasında.', ha='center', fontsize=10.5, color=INK)
ax.set_xlim(-T.W2 - 4, T.W2 + 4); ax.set_ylim(T.Y_ON - 12, T.Y_M + 10)

# ---------------- 3) KESİTLER ----------------
def kesit_tus(ax):
    ax.set_title('3 · Tuş LED\'i, yandan kesit', fontsize=13, weight='bold', color=INK, loc='left')
    ax.add_patch(Rectangle((-25, 0), 50, 3, fc='#334155', ec=INK))
    ax.add_patch(Rectangle((-7, -5), 14, 5, fc='#94a3b8', ec=INK)); ax.text(0, -2.5, 'MX', ha='center', va='center', fontsize=10)
    ax.add_patch(Rectangle((-7.7, 3), 15.4, 5, fc='#94a3b8', ec=INK))
    ax.add_patch(Rectangle((10.25, 0), 1.6, 3, fc=BG, ec='none'))
    ax.add_patch(Rectangle((10.4, 0), 1.3, 4, fc='#e9d5ff', ec='#7c3aed'))
    ax.add_patch(Rectangle((9.5, 3), 2.2, 1, fc='#e9d5ff', ec='#7c3aed'))
    ax.add_patch(Rectangle((7.0, -1.9), 10, 0.4, fc='#fff7ed', ec=LED))
    ax.add_patch(Rectangle((9.5, -1.5), 5, 1.5, fc='#fcd34d', ec=LED))
    for x in (7.0, 15.4):
        ax.add_patch(Rectangle((x, -1.5), 1.6, 1.5, fc='#e5e7eb', ec=SOFT))
    for dx in (-1.2, 0, 1.2):
        ax.annotate('', xy=(11.0 + dx, 3.4), xytext=(11.6 + dx * 0.4, 0.1), arrowprops=dict(arrowstyle='->', color='#f59e0b', lw=1.2))
    ax.text(18, 4.8, 'halka', fontsize=10, color='#7c3aed'); ax.text(18, 1.2, 'düz kapak', fontsize=10, color=INK)
    ax.text(19, -1.0, 'LED YUKARI, halka yarığına bakar;\nkart tabandaki yükseltici bloğun\neğik yüzünde, alt kenarı tırnağa dayalı', fontsize=9.5, color=LED, va='top')
    ax.set_xlim(-12, 40); ax.set_ylim(-8, 9); ax.set_aspect('equal'); ax.axis('off')


def kesit_taban(ax):
    ax.set_title('4 · Taban LED\'i, yandan kesit', fontsize=13, weight='bold', color=INK, loc='left')
    ax.add_patch(Rectangle((-30, 0), 40, 3, fc='#fed7aa', ec=INK)); ax.text(-20, 1.5, 'şeffaf taban', va='center', fontsize=10)
    ax.add_patch(Rectangle((8, 3), 2.2, 14, fc='#1e293b', ec=INK)); ax.text(11, 12, 'duvar', fontsize=10)
    ax.add_patch(Rectangle((-5.5, 4.5), 10, 0.4, fc='#fff7ed', ec=LED))
    ax.add_patch(Rectangle((-3, 3), 5, 1.5, fc='#fcd34d', ec=LED))
    for x in (-5.8, 4.6):
        ax.add_patch(Circle((x, 4.3), 0.9, fc='#e5e7eb', ec=SOFT))
    for dx in (-1.5, 0, 1.5):
        ax.annotate('', xy=(-0.5 + dx * 2, -1.5), xytext=(-0.5 + dx * 0.4, 2.8), arrowprops=dict(arrowstyle='->', color='#f59e0b', lw=1.2))
    ax.text(-13, 8.5, 'LED AŞAĞI (tabana) bakar;\nkart tabandaki yuvaya oturur\n(kafa, C1 ve ped cepleri hazır)', fontsize=9.5, color=LED)
    ax.text(-30, -3.2, 'ışık tabandan masaya', fontsize=10, color='#b45309')
    ax.set_xlim(-31, 18); ax.set_ylim(-5, 18); ax.set_aspect('equal'); ax.axis('off')


ax_n = fig.add_axes([0.38, 0.05, 0.6, 0.2]); ax_n.axis('off')
ax_n.text(0, 0.8, 'Yandan kesit: aci_kesit.png · genel görünüm: son_hal.png, taban_son.png', fontsize=12, color=SOFT)

# ---------------- 5) ADIMLAR ----------------
ax = fig.add_axes([0.74, 0.36, 0.25, 0.53]); ax.axis('off')
ax.set_title('Adımlar', fontsize=14, weight='bold', color=INK, loc='left')
adim = [
    'Şeridi bakır pedlerin ortasından\nkes: her parça tek LED.',
    'Her parçaya 3 tel lehimle:\n5V, GND ve DIN/DOUT.\nLehimi yassı tut.',
    'Taban (1–12): modülü LED aşağı\nbakacak şekilde yuvaya bastır;\nkafa, C1 ve pedler kendi\ncebine oturur. Ok yönü:\nZero → 1 → … → 12.',
    'Tuşlar (13–18): modülü\nyükseltici bloğun eğik yüzüne,\nkendi tuşunun hizasına,\nkartın alt kenarı tırnağa\ngelecek şekilde koy; bir\ndamla yapıştırıcıyla sabitle.',
    'Sol taban LED\'leri 3 ve 4\nyatay (kapak konnektörünün\nyanında).',
    'Bütün LED\'ler tabanda;\nkapağa sadece tuş telleri,\npanele ekran ve encoder\ntelleri gider.',
]
y = 0.96
for i, t in enumerate(adim):
    ax.add_patch(Circle((0.04, y - 0.012), 0.028, transform=ax.transAxes, fc=LED, ec='none'))
    ax.text(0.04, y - 0.012, str(i + 1), transform=ax.transAxes, ha='center', va='center', color='white', fontsize=11, weight='bold')
    ax.text(0.11, y, t, transform=ax.transAxes, va='top', fontsize=10.5, color=INK)
    y -= 0.035 + 0.032 * t.count('\n') + 0.025

fig.savefig(sys.argv[1], facecolor=BG)
print('ok')
