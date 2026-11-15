# Makro Klavye v2 - son tasarıma göre: kasa içi kablo yerleşimi + konnektör şeması
import sys
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, Rectangle, Circle, Polygon
import tasarim3 as T

plt.rcParams['font.family'] = 'DejaVu Sans'
out = sys.argv[1]
INK = '#1f2937'; SOFT = '#6b7280'; BG = '#f8fafc'
RENK = {'5V': '#dc2626', '3V3': '#ea580c', 'GND': '#111827', 'ekran': '#2563eb', 'encL': '#7c3aed',
        'encR': '#9333ea', 'tus': '#059669', 'led': '#d97706', 'konn': '#0f766e', 'bos': '#94a3b8'}

# ---- son tasarımın konumları (modelden) ----
YA, ZA = T.arka()
Y0, Y1 = T.zero_y0(), T.zero_y1()
LED_TABAN = T.taban_led_konumlari_v11()
KART_Y_YEREL = (8.84 + 17.76) / 2            # LED kartının blok üzerindeki merkezi (anahtar merkezinden)
LED_TUS = [(x, T.SATIR[0] + KART_Y_YEREL) for x in T.SUTUN[::-1]] + [(x, T.SATIR[1] - KART_Y_YEREL) for x in T.SUTUN]
o8 = T.konn2_olcu(False)
K8_X = -(T.W2 - T.DUVAR) + T.HDR_DUVAR + (T.P254 + 2 * T.HDR_TOL) / 2 + T.KAPAK_KONN_ICERI
K8_Y = T.KONN_Y
K14_Y = T.pk_olcu()['yc']
K8 = ['T1', 'T2', 'T3', 'T4', 'T5', 'T6', 'GND', 'boş']                     # önden arkaya
K8_ZERO = ['GP6', 'GP7', 'GP8', 'GP14', 'GP15', 'GP26', 'GND', '—']
K14 = ['SolEnc A', 'SolEnc B', 'SolEnc SW', 'GND', '3V3', 'SCK', 'SDA', 'RES', 'DC', 'BLK', 'GND', 'SağEnc SW', 'SağEnc B', 'SağEnc A']
K14_ZERO = ['GP0', 'GP1', 'GP2', 'GND', '3V3', 'GP10', 'GP11', 'GP12', 'GP9', 'GP13', 'GND', 'GP5', 'GP4', 'GP3']
K14_RENK = ['encL'] * 3 + ['GND', '3V3'] + ['ekran'] * 5 + ['GND'] + ['encR'] * 3
K8_RENK = ['tus'] * 6 + ['GND', 'bos']


def k8_pin(i):
    return K8_X, K8_Y + (i - 3.5) * T.P254


def k14_pin(i):
    return (i - 6.5) * T.P254, K14_Y


def izd(x, y):
    q = T.panel_nokta(x, y); return q[0], q[1]


# =====================================================================
# 1) KASA İÇİ KABLO YERLEŞİMİ
# =====================================================================
fig = plt.figure(figsize=(16, 14), dpi=100)
fig.patch.set_facecolor(BG)
ax = fig.add_axes([0.02, 0.03, 0.70, 0.87]); ax.set_aspect('equal'); ax.axis('off')
fig.text(0.03, 0.96, 'Kasa içi kablo yerleşimi (üstten, kapaklar şeffaf) · son tasarım', fontsize=21, weight='bold', color=INK)
fig.text(0.03, 0.932, 'Düz çizgi: alt kasa (taban/gövde) telleri.  Kesik çizgi: üst parçalardaki teller (kapak ve panel), '
         'konnektörde biter.', fontsize=11.5, color=SOFT)


def cs_ciz(cs, **kw):
    for p in cs.to_polygons():
        ax.add_patch(Polygon(np.asarray(p), closed=True, **kw))


def tel(nok, renk, lw=2.2, ls='-', z=8):
    p = np.asarray(nok)
    ax.plot(p[:, 0], p[:, 1], color=RENK[renk], lw=lw, ls=ls, solid_capstyle='round', zorder=z, alpha=.92)


cs_ciz(T.taban_cizgi(), fc='#e2e8f0', ec=INK, lw=1.4)
cs_ciz(T.taban_cizgi(-T.DUVAR), fc='#f8fafc', ec='#94a3b8', lw=0.8, ls='--')
ax.plot([-T.W2, T.W2], [T.Y_M, T.Y_M], color=SOFT, lw=1, ls='-.')
ax.text(T.W2 + 2, T.Y_M + 2.5, 'eğik panel ↑', fontsize=9, color=SOFT, va='center')
ax.text(T.W2 + 2, T.Y_M - 2.5, 'düz kapak ↓', fontsize=9, color=SOFT, va='center')
# vida direkleri
for (x, y) in T.boss_merkezleri():
    ax.add_patch(Circle((x, y), 3.2, fc='#cbd5e1', ec='#64748b', lw=0.8, zorder=1))

# LED yükseltici blokları
for s in (0, 1):
    bb = T.led_blok_yerlesik(s).bounding_box()
    ax.add_patch(Rectangle((bb[0], bb[1]), bb[3] - bb[0], bb[4] - bb[1], fc='#e0f2fe', ec='#7dd3fc', lw=1, zorder=1))
ax.text(0, T.SATIR[0] + 16.0, 'LED yükseltici bloğu (arka sıra)', ha='center', fontsize=8, color='#0369a1', zorder=2)
ax.text(0, T.SATIR[1] - 17.3, 'LED yükseltici bloğu (ön sıra)', ha='center', fontsize=8, color='#0369a1', zorder=2)

# Zero
ax.add_patch(Rectangle((T.ZERO_X - 9, Y0), 18, T.ZERO_UZ, fc='#111827', ec='black', zorder=5))
ax.add_patch(Rectangle((T.ZERO_X - 4.45, Y1 + 0.6 - 7.3), 8.9, 7.3, fc='#cbd5e1', ec='black', zorder=6))
ax.text(T.ZERO_X, Y0 + 9, 'RP2040\nZero', color='white', ha='center', va='center', fontsize=9.5, weight='bold', zorder=7)
ax.text(T.ZERO_X, Y0 - 1.6, 'GP9–13', color=INK, ha='center', va='top', fontsize=7, zorder=7)

# ekran ve encoderlar (panelin altında)
kart = [izd(T.KART_SOL, -T.KART_Y / 2), izd(T.KART_SOL + T.KART_X, -T.KART_Y / 2), izd(T.KART_SOL + T.KART_X, T.KART_Y / 2), izd(T.KART_SOL, T.KART_Y / 2)]
ax.add_patch(Polygon(kart, closed=True, fc='#dbeafe', ec=RENK['ekran'], lw=1.5, alpha=.6, zorder=2))
ax.text(*izd(10, 11), 'ekran (arkası)', ha='center', fontsize=9, color=RENK['ekran'], zorder=3)
ekr_pin = [izd(T.KART_SOL + 2.0, yy) for yy in np.linspace(-7.6, 7.6, 7)]
for p in ekr_pin:
    ax.add_patch(Circle(p, 0.7, fc=RENK['ekran'], zorder=4))
for x, r, ad in ((-T.ENC_X, 'encL', 'sol encoder'), (T.ENC_X, 'encR', 'sağ encoder')):
    ex, ey = izd(x, 0)
    ax.add_patch(Rectangle((ex - 6, ey - 5.5), 12, 11, fc='#f3e8ff', ec=RENK[r], lw=1.5, zorder=2))
    ax.text(ex, ey + 7.2, ad, ha='center', fontsize=8.5, color=RENK[r], weight='bold')

# tuşlar
tnum = {(0, 0): 1, (0, 1): 2, (0, 2): 3, (1, 0): 4, (1, 1): 5, (1, 2): 6}
for i, y in enumerate(T.SATIR):
    for j, x in enumerate(T.SUTUN):
        ax.add_patch(Rectangle((x - 7, y - 7), 14, 14, fc='white', ec=RENK['tus'], lw=1.5, zorder=3))
        ax.text(x, y, f'T{tnum[(i, j)]}', ha='center', va='center', fontsize=11, weight='bold', color=RENK['tus'], zorder=4)

# taban LED'leri (cepler) ve tuş LED'leri (bloklarda)
for k, (x, y, o) in enumerate(LED_TABAN):
    w, h = (16.7, 10) if o == 'x' else (10, 16.7)
    ax.add_patch(Rectangle((x - w / 2, y - h / 2), w, h, fc='#fff7ed', ec=RENK['led'], lw=1, zorder=1.5))
    ax.text(x, y, str(k + 1), ha='center', va='center', fontsize=8.5, color=RENK['led'], weight='bold', zorder=6)
for k, (x, y) in enumerate(LED_TUS):
    ax.add_patch(Rectangle((x - 8.35, y - 4.3), 16.7, 8.6, fc='#fffbeb', ec=RENK['led'], lw=1, ls='--', zorder=2))
    ax.text(x + 10.5, y, str(13 + k), ha='center', va='center', fontsize=8.5, color=RENK['led'], weight='bold', zorder=6)

# konnektörler
L8 = 8 * T.P254
ax.add_patch(Rectangle((K8_X - 2.2, K8_Y - L8 / 2 - 0.8), 4.4, L8 + 1.6, fc='#ccfbf1', ec=RENK['konn'], lw=1.6, zorder=9))
for i in range(8):
    ax.add_patch(Circle(k8_pin(i), 0.55, fc=RENK[K8_RENK[i]] if K8_RENK[i] != 'bos' else 'white', ec=RENK['konn'], lw=0.6, zorder=10))
ax.text(K8_X - 3.5, K8_Y, 'KAPAK\nKONN.\n1x8', ha='right', va='center', fontsize=8, color=RENK['konn'], weight='bold', zorder=10)
L14 = 14 * T.P254
ax.add_patch(Rectangle((-L14 / 2 - 0.8, K14_Y - 2.2), L14 + 1.6, 4.4, fc='#ccfbf1', ec=RENK['konn'], lw=1.6, zorder=9))
for i in range(14):
    ax.add_patch(Circle(k14_pin(i), 0.55, fc=RENK[K14_RENK[i]], ec=RENK['konn'], lw=0.6, zorder=10))
ax.text(L14 / 2 + 1.6, K14_Y - 3.6, 'PANEL KONN. 1x14', ha='left', va='center', fontsize=8, color=RENK['konn'], weight='bold', zorder=10)

# ---- üst parçalardaki teller (kesik) ----
trunk = T.KONN_Y                                  # -32.5: iki tuş sırasının arası = kapak konnektörünün ortası
for i, y in enumerate(T.SATIR):
    for j, x in enumerate(T.SUTUN):
        ky = y - 7 if i == 0 else y + 7
        tel([(x - 3, ky), (x - 3, trunk + (1.0 if i == 0 else -1.0))], 'tus', 1.6, '--')
tel([(T.SUTUN[2] - 3, trunk + 1.0), (K8_X + 2.4, trunk + 1.0)], 'tus', 2.4, '--')
tel([(T.SUTUN[2] - 3, trunk - 1.0), (K8_X + 2.4, trunk - 1.0)], 'tus', 2.4, '--')
tel([(K8_X + 2.4, trunk + 1.0), k8_pin(0)], 'tus', 1.4, '--')
ax.text(T.SUTUN[1] + 4, trunk + 2.3, 'T1–T6 + ortak GND → kapak konnektörü', fontsize=8, color=RENK['tus'], zorder=9)
# panel: encoderlar ve ekran -> panel erkek konnektörü
eL = izd(-T.ENC_X, 0); eR = izd(T.ENC_X, 0)
tel([(eL[0], eL[1] - 5.5), (eL[0], K14_Y + 4.5), (k14_pin(1)[0], K14_Y + 4.5), (k14_pin(1)[0], K14_Y + 2.2)], 'encL', 2.6, '--')
tel([(eR[0], eR[1] - 5.5), (eR[0], K14_Y + 4.5), (k14_pin(12)[0], K14_Y + 4.5), (k14_pin(12)[0], K14_Y + 2.2)], 'encR', 2.6, '--')
pm = ekr_pin[3]
tel([(pm[0] - 0.8, pm[1]), (pm[0] - 4, pm[1]), (pm[0] - 4, K14_Y + 6.5), (k14_pin(6)[0], K14_Y + 6.5), (k14_pin(6)[0], K14_Y + 2.2)], 'ekran', 3.0, '--')

# ---- alt kasa telleri (düz) ----
# panel dişi -> Zero: uçlar arkaya (Zero'ya) bükülü; ekran pinleri Zero'nun ön kenarına (GP9-13), encoderlar sağ kenara
tel([(k14_pin(7)[0], K14_Y + 2.2), (k14_pin(7)[0], Y0)], 'ekran', 3.0)
PIN = lambda i: Y1 - 1.6 - i * 2.54               # Zero kenar pinlerinin y'si (0 = USB tarafı)
tel([(k14_pin(12)[0], K14_Y + 2.2), (k14_pin(12)[0], PIN(4)), (T.ZERO_X + 9, PIN(4))], 'encR', 2.6)
tel([(k14_pin(2)[0], K14_Y + 2.2), (k14_pin(2)[0], Y0 - 3.2), (T.ZERO_X + 11.5, Y0 - 3.2), (T.ZERO_X + 11.5, PIN(1)), (T.ZERO_X + 9, PIN(1))], 'encL', 2.6)
# kapak dişi -> Zero (sol duvar boyunca)
tel([(K8_X + 2.4, K8_Y + L8 / 2 - 1), (K8_X + 4.5, K8_Y + L8 / 2 - 1), (K8_X + 4.5, PIN(7)), (T.ZERO_X - 9, PIN(7))], 'tus', 3.0)
tel([(K8_X + 4.5, Y0 - 5.5), (T.ZERO_X + 13.5, Y0 - 5.5), (T.ZERO_X + 13.5, PIN(7)), (T.ZERO_X + 9, PIN(7))], 'tus', 2.2)
ax.text(T.ZERO_X - 10.2, PIN(7) + 1.6, 'GP14·15·26', color=RENK['tus'], ha='right', fontsize=7.5, zorder=9)
ax.text(T.ZERO_X + 14.2, PIN(7), 'GP6·7·8', color=RENK['tus'], ha='left', va='center', fontsize=7.5, zorder=9)
ax.text(K8_X + 5.6, -12, 'tuş demeti\n(7 tel)', fontsize=8, color=RENK['tus'], va='center', zorder=9)
# LED zinciri: GP27 -> 1 -> ... -> 12 -> 13..18
pts = [(x, y) for (x, y, o) in LED_TABAN]
tel([(T.ZERO_X - 9, PIN(5)), (pts[0][0], PIN(5)), (pts[0][0], pts[0][1] - 5)], 'led', 2.4)
ax.text(pts[0][0] + 1, PIN(5) + 1.4, 'GP27 → 330 Ω', fontsize=7.5, color=RENK['led'], ha='left', zorder=9)
for a_, b_ in zip(pts[:-1], pts[1:]):
    tel([a_, b_], 'led', 1.8, z=7)
tel([pts[-1], (T.W2 - 6, pts[-1][1] - 6), (T.W2 - 6, LED_TUS[0][1]), LED_TUS[0]], 'led', 1.8, z=7)
for a_, b_ in zip(LED_TUS[:2], LED_TUS[1:3]):
    tel([a_, b_], 'led', 1.8, z=7)
tel([LED_TUS[2], (LED_TUS[2][0] - 12, LED_TUS[2][1]), (LED_TUS[3][0] - 12, LED_TUS[3][1]), LED_TUS[3]], 'led', 1.8, z=7)
for a_, b_ in zip(LED_TUS[3:-1], LED_TUS[4:]):
    tel([a_, b_], 'led', 1.8, z=7)
ax.set_xlim(-T.W2 - 14, T.W2 + 16); ax.set_ylim(T.Y_ON - 6, YA + 6)

# sağ panel: lejant + notlar
lx = fig.add_axes([0.73, 0.05, 0.26, 0.85]); lx.axis('off'); lx.set_xlim(0, 10); lx.set_ylim(0, 100)
satir = [('ekran', '-', 'Ekran: DC GP9, SCK GP10, SDA GP11,\nRES GP12, BLK GP13, 3V3, GND'),
         ('encL', '-', 'Sol encoder: A GP0, B GP1, SW GP2'),
         ('encR', '-', 'Sağ encoder: A GP3, B GP4, SW GP5'),
         ('tus', '-', 'Tuşlar: T1 GP6, T2 GP7, T3 GP8,\nT4 GP14, T5 GP15, T6 GP26'),
         ('led', '-', 'LED zinciri: GP27 → 330 Ω → 1 … 12\n(taban), sonra 13 … 18 (tuşlar)'),
         ('konn', '-', 'Konnektörler (pin sırası\nkonnektor_semasi.png\'de)')]
yy = 96
for r, ls, t in satir:
    lx.plot([0.2, 1.3], [yy, yy], color=RENK[r], lw=4, ls=ls)
    lx.text(1.6, yy, t, va='center', fontsize=10.2, color=INK)
    yy -= 8.5 if '\n' in t else 6
lx.plot([0.2, 1.3], [yy, yy], color=INK, lw=2.5, ls='--'); lx.text(1.6, yy, 'kesik: kapak/panel tarafı teller', va='center', fontsize=10.2, color=INK)
yy -= 9
notlar = ['NOTLAR',
          '• 5V ve GND tüm LED\'lere paralel gider',
          '  (tabanda halka gibi dolaştır).',
          '• Ortak GND: Zero GND → iki konnektörün',
          '  GND pinleri + LED GND.',
          '• Konnektör sayesinde kapak ve panel',
          '  telleri kısa kalır, pay gerekmez.',
          '• Sol taban LED\'leri 3 ve 4 yatay',
          '  (kapak konnektörünün yanında).',
          '• Tuş LED\'leri (13–18) yükseltici bloklarda,',
          '  kendi tuşunun hizasında.',
          '• Taban LED\'leri yüzü tabana dönük.']
for i, t in enumerate(notlar):
    lx.text(0.2, yy - i * 3.6, t, fontsize=10.2 if i else 11.5, weight='bold' if i == 0 else 'normal', color=INK, va='top')
fig.savefig(f'{out}/kablo_yerlesimi.png', facecolor=BG)
plt.close(fig)

# =====================================================================
# 2) KONNEKTÖR ŞEMASI
# =====================================================================
fig = plt.figure(figsize=(16, 10), dpi=100)
fig.patch.set_facecolor(BG)
fig.text(0.03, 0.94, 'Konnektör şeması · kapak 1x8 ve panel 1x14', fontsize=21, weight='bold', color=INK)
fig.text(0.03, 0.905, 'Erkek header üst parçada (kapak/panel), dişi header alt kasada. Her satır: üst parçadaki tel  ↔  pin  ↔  Zero\'ya giden tel.',
         fontsize=11.5, color=SOFT)


def tablo(ax, baslik, alt, pinler, ust, zero, renk, not_):
    ax.axis('off'); ax.set_xlim(0, 100); ax.set_ylim(0, 100)
    ax.text(0, 97, baslik, fontsize=15, weight='bold', color=INK)
    ax.text(0, 92.5, alt, fontsize=10, color=SOFT)
    n = len(pinler); h = min(5.6, 78 / n)
    ax.text(14, 87, 'Üst parça (erkek)', fontsize=10, weight='bold', color=SOFT, ha='center')
    ax.text(50, 87, 'Pin', fontsize=10, weight='bold', color=SOFT, ha='center')
    ax.text(84, 87, 'Alt kasa → Zero', fontsize=10, weight='bold', color=SOFT, ha='center')
    for i in range(n):
        y = 82 - i * h
        c = RENK[renk[i]]
        ax.add_patch(FancyBboxPatch((41, y - h * 0.38), 18, h * 0.76, boxstyle='round,pad=0.2,rounding_size=1', fc='#ccfbf1', ec=RENK['konn'], lw=1.2))
        ax.text(50, y, f'{i + 1}', ha='center', va='center', fontsize=11, weight='bold', color=RENK['konn'])
        ax.plot([28, 41], [y, y], color=c, lw=3, ls='--' if renk[i] != 'bos' else ':')
        ax.plot([59, 72], [y, y], color=c, lw=3, ls='-' if renk[i] != 'bos' else ':')
        ax.text(27, y, ust[i], ha='right', va='center', fontsize=10.5, color=INK, weight='bold')
        ax.text(73, y, zero[i], ha='left', va='center', fontsize=10.5, color=c if renk[i] not in ('GND', 'bos') else INK, weight='bold')
    ax.text(0, 82 - n * h - 2, not_, fontsize=9.8, color=INK, va='top')


a1 = fig.add_axes([0.03, 0.05, 0.40, 0.82])
tablo(a1, 'KAPAK KONNEKTÖRÜ · 1x8', 'Sol yan duvar · önden arkaya numaralı', K8,
      ['Tuş 1 (sol arka)', 'Tuş 2 (orta arka)', 'Tuş 3 (sağ arka)', 'Tuş 4 (sol ön)', 'Tuş 5 (orta ön)', 'Tuş 6 (sağ ön)', 'Tuşların ortak ucu', 'boş (yedek)'],
      K8_ZERO, K8_RENK,
      'Her anahtarın bir bacağı kendi pinine, diğer bacağı ortak GND\'ye.\n'
      'Erkek: kapağın altındaki yuva, uçlar içeri bükülü.\nDişi: gövdenin sol duvarındaki yuva, uçlar içeri bükülü.')
a2 = fig.add_axes([0.52, 0.05, 0.45, 0.82])
tablo(a2, 'PANEL KONNEKTÖRÜ · 1x14', 'Menteşenin arkası · önden bakınca soldan sağa numaralı', K14,
      ['Sol encoder A', 'Sol encoder B', 'Sol encoder bas (S1)', 'Ekran GND + enc. C/S2', 'Ekran VCC', 'Ekran SCK (SCL)',
       'Ekran SDA', 'Ekran RES', 'Ekran DC', 'Ekran BLK', 'GND (2.)', 'Sağ encoder bas (S1)', 'Sağ encoder B', 'Sağ encoder A'],
      K14_ZERO, K14_RENK,
      'Encoderların C (orta) ucu ve basma anahtarının S2 ucu panel tarafında GND\'ye.\n'
      'Erkek: panelin altındaki yuva.  Dişi: şeffaf tabandaki yuva, uçlar arkaya (Zero\'ya) bükülü.')
fig.savefig(f'{out}/konnektor_semasi.png', facecolor=BG)
print('ok')
