# Makro Klavye v2 - "Halkalar" tasarımı
# Parçalar: şeffaf taban, çerçeve (yan duvarlar), düz üst kapak (tuşlar), eğik panel (ekran + encoderlar),
#           6 tuş halkası difüzörü. LED: WS2812B şerit (10 mm, 60 LED/m), tek veri hattı.
# Ölçüler mm. x: sağ, y: arka, z: yukarı. Ekran penceresi panelin yerel (0,0) noktası.
import numpy as np
from manifold3d import Manifold, CrossSection, JoinType, OpType

SEG = 64

# ---------- Genel ----------
W2 = 48.0                # yarım genişlik -> 96
Y_ON = -65.0
Z_TABAN = 3.0            # şeffaf taban kalınlığı
Z_DUZ = 22.0             # düz üst yüzey
KAP = 3.0                # üst kapak kalınlığı
Y_M = -1.0               # menteşe çizgisi (düz kapak / panel)
PANEL_L = 40.0
TETA = 22.0
R_KOSE = 6.0
DUVAR = 2.2

# ---------- Tuşlar ----------
ARALIK = 25.0
SUTUN = [-ARALIK, 0.0, ARALIK]
SATIR = [-20.0, -20.0 - ARALIK]
MX = 14.1
MX_RAHAT = 15.4
MX_SAC = 1.5
HALKA_DIS = 23.2         # halka dış ölçüsü
HALKA_EN = 1.4
HALKA_PAY = 0.1              # halka yuvasında her yandan geçme payı
CENTIK_PAY = 0.35            # halkadaki çentiklerin köprülere göre payı
HALKA_R = 2.0
KOPRU_W, KOPRU_H = 2.5, 2.0   # halka yuvasındaki köprüler (doğu/batı kenarda 2'şer, yalnızca altta)
KOPRU_Y = 5.0

# ---------- LED (WS2812B şerit parçası: 10 x 16.7 mm) ----------
LED_EN, LED_BOY = 10.0, 16.7
LED_TUS_DY = [7.2 + 5.1, 7.2 + 5.1]   # iki sırada da arka tarafta; kart kenarı anahtar gövdesinden 0.2 mm

# ---------- Panel ----------
ENC_X = 33.0
ENC_DELIK = 7.4
PENCERE = 24.4
CAM_X, CAM_Y = 30.0, 27.0
CAM_MX = 1.3
CAM_DERIN = 1.6
KART_X, KART_Y = 39.0, 27.5
KART_SOL = -17.7
PANEL_ALT = -3.0 * np.cos(np.radians(22.0))   # panelin alt yüzü (yerel z)
# ekran modülü montajı (pin başlığı solda; 4 köşe deliği)
EKRAN_DELIK_KENAR = 2.5          # delik merkezinin kart kenarlarına uzaklığı
EKRAN_DIREK_R = 1.8
EKRAN_DIREK_H = 0.4              # cam+arka ışık ~2.0 mm varsayımı: kart ön yüzü alt yüzden ~0.4 aşağıda
EKRAN_VIDA_PILOT = 1.6           # M2 sac vidası
PIN_YARIK_EN = 4.8               # pin sırası lehim noktaları için yarık (kart kenarından içeri)
PIN_YARIK_Y = 9.0                # +-, 7 pin x 2.54 mm = 15.2 mm
PIN_YARIK_DERIN = 1.8
FPC_PAY = 1.2                    # camın karşı ucunda bükülen şerit kablo için cep uzantısı

# ---------- RP2040-Zero ----------
ZERO_UZ, ZERO_EN, ZERO_KAL = 23.5, 18.0, 1.2
ZERO_X = 0.0                 # kart arka duvarın ortasında, USB-C arkaya bakar
ZERO_TABAN = 1.0
LEHIM_CEP = 1.2             # Zero'nun altında tabana açılan lehim payı cebi
USB_W, USB_H = 9.3, 3.7      # USB-C ağzı (yalnızca soket)
USB_CEP_W = ZERO_EN + 1.4
BIRLESIM_BOSLUK = 0.2        # ön/arka gövde ve kapak/panel birleşim boşluğu
USB_DUVAR = 1.2              # soket önündeki incelmiş duvar

# ---------- Vidalar ----------
VIDA_PILOT, VIDA_GECIS, HAVSA = 2.6, 3.4, 6.4
BOSS_R = 4.3
DUZ_VIDA = [(-42.0, -59.0), (42.0, -59.0), (-42.0, -6.0), (42.0, -6.0)]
PANEL_VIDA = [(-42.0, -14.5), (42.0, -14.5), (-42.0, 13.0), (42.0, 13.0)]   # panel yerel


def T():
    t = np.radians(TETA)
    return np.cos(t), np.sin(t)


def panel_merkez():
    c, s = T()
    return np.array([0.0, Y_M + PANEL_L / 2 * c, Z_DUZ + PANEL_L / 2 * s])


def panel(m):
    """Panel yerel koordinatından dünyaya (yerel z=0: panel üst yüzeyi)"""
    p = panel_merkez()
    return m.rotate([TETA, 0, 0]).translate(list(p))


def panel_nokta(x, y, z=0.0):
    c, s = T()
    p = panel_merkez()
    return np.array([x, p[1] + y * c - z * s, p[2] + y * s + z * c])


def arka():
    c, s = T()
    return Y_M + PANEL_L * c, Z_DUZ + PANEL_L * s


def yuvarlak_dik(x0, y0, x1, y1, r):
    cs = CrossSection.square([x1 - x0 - 2 * r, y1 - y0 - 2 * r], center=True)
    return cs.offset(r, JoinType.Round, circular_segments=SEG).translate([(x0 + x1) / 2, (y0 + y1) / 2])


def kutu(cx, cy, z0, sx, sy, sz):
    return Manifold.cube([sx, sy, sz], center=True).translate([cx, cy, z0 + sz / 2])


def sil(cx, cy, z0, h, r, r2=None, seg=SEG):
    return Manifold.cylinder(h, r, r if r2 is None else r2, seg).translate([cx, cy, z0])


def birlesim(lst):
    return Manifold.batch_boolean(lst, OpType.Add)


def taban_cizgi(pay=0.0):
    ya, _ = arka()
    return yuvarlak_dik(-W2 - pay, Y_ON - pay, W2 + pay, ya + pay, R_KOSE + pay)


def govde_dolu():
    ya, za = arka()
    prof = CrossSection([[(Y_ON, 0), (ya, 0), (ya, za), (Y_M, Z_DUZ), (Y_ON, Z_DUZ)]])
    yan = prof.extrude(2 * W2 + 20).rotate([90, 0, 90]).translate([-W2 - 10, 0, 0])
    return yan ^ taban_cizgi().extrude(100)


def ust_deri():
    g = govde_dolu()
    return g - g.translate([0, 0, -KAP])


def bolme_duzlemi_on(kayma=0.0):
    """menteşeden geçen açıortay düzleminin ön (düz kapak) tarafı; kayma>0 düzlemi arkaya iter"""
    a = TETA / 2
    blok = kutu(0, -100 + kayma, -100, 400, 200, 300)  # y<kayma yarı uzay
    return blok.rotate([a, 0, 0]).translate([0, Y_M, Z_DUZ])


def tus_halkasi_cs(x, y, dis_pay=0.0, ic_pay=0.0):
    h = HALKA_DIS / 2
    dis = yuvarlak_dik(x - h - dis_pay, y - h - dis_pay, x + h + dis_pay, y + h + dis_pay, HALKA_R + dis_pay)
    ic = dis.offset(-(HALKA_EN + dis_pay + ic_pay), JoinType.Round, circular_segments=32)
    return dis, ic


# ---------------- Parçalar ----------------
def duz_kapak():
    k = ust_deri() ^ bolme_duzlemi_on(-BIRLESIM_BOSLUK / 2)
    kes = []
    for y in SATIR:
        for x in SUTUN:
            kes.append(kutu(x, y, Z_DUZ - KAP - 1, MX, MX, KAP + 2))
            kes.append(kutu(x, y, Z_DUZ - KAP - 1, MX_RAHAT, MX_RAHAT, 1 + KAP - MX_SAC))
            dis, ic = tus_halkasi_cs(x, y, HALKA_PAY, HALKA_PAY)
            kes.append((dis - ic).extrude(KAP + 2).translate([0, 0, Z_DUZ - KAP - 1]) - kopruler(x, y))
    for (x, y) in DUZ_VIDA:
        kes.append(sil(x, y, Z_DUZ - KAP - 1, KAP + 2, VIDA_GECIS / 2))
        kes.append(sil(x, y, Z_DUZ - 1.6, 1.61, VIDA_GECIS / 2, HAVSA / 2))
    return k - birlesim(kes)


def eğik_panel():
    k = ust_deri() - bolme_duzlemi_on(BIRLESIM_BOSLUK / 2)
    kes = []
    p = PENCERE / 2
    kes.append(panel(kutu(0, 0, -KAP - 1, PENCERE, PENCERE, KAP + 2)))
    kes.append(panel(Manifold.cylinder(1.21, p * 1.4142, (p + 1.2) * 1.4142, 4).rotate([0, 0, 45]).translate([0, 0, -1.2])))
    kes.append(panel(kutu(CAM_MX, 0, -KAP - 1, CAM_X, CAM_Y, 1 + CAM_DERIN + (KAP - 2.78))))
    for x in (-ENC_X, ENC_X):
        kes.append(panel(sil(x, 0, -KAP - 1, KAP + 2, ENC_DELIK / 2)))
    for (x, y) in PANEL_VIDA:
        kes.append(panel(sil(x, y, -KAP - 1, KAP + 2, VIDA_GECIS / 2)))
        kes.append(panel(sil(x, y, -1.6, 1.61, VIDA_GECIS / 2, HAVSA / 2)))
    # pin başlığının ön yüzdeki lehim noktaları için yarık
    kes.append(panel(kutu(KART_SOL - 0.6 + (PIN_YARIK_EN + 0.6) / 2, 0, PANEL_ALT - 1,
                          PIN_YARIK_EN + 0.6, 2 * PIN_YARIK_Y, 1 + PIN_YARIK_DERIN)))
    # camın karşı ucunda şerit kablo bükümü
    cam_uc = CAM_MX + CAM_X / 2
    kes.append(panel(kutu(cam_uc + FPC_PAY / 2 - 0.01, 0, PANEL_ALT - 1, FPC_PAY, CAM_Y - 4, 1 + 1.0)))
    k = k - birlesim(kes)
    # ekran vida direkleri (M2, arkadan kart deliğinden)
    direk, pilot = [], []
    for (x, y) in ekran_delikleri():
        direk.append(panel(sil(x, y, PANEL_ALT - EKRAN_DIREK_H, EKRAN_DIREK_H + 0.3, EKRAN_DIREK_R)))
        pilot.append(panel(sil(x, y, PANEL_ALT - EKRAN_DIREK_H - 0.1, EKRAN_DIREK_H + 2.1, EKRAN_VIDA_PILOT / 2)))
    # direkler cam cebine taşmasın (cam her yöne serbestçe girsin)
    cep = panel(kutu(CAM_MX, 0, PANEL_ALT - 5, CAM_X, CAM_Y, 5 + 1.0))
    return (k + (birlesim(direk) - cep)) - birlesim(pilot)


def ekran_delikleri():
    """kart köşe delikleri (panel yerel x, y)"""
    x0 = KART_SOL + EKRAN_DELIK_KENAR
    x1 = KART_SOL + KART_X - EKRAN_DELIK_KENAR
    y = KART_Y / 2 - EKRAN_DELIK_KENAR
    return [(x0, -y), (x0, y), (x1, -y), (x1, y)]


def ic_bolge():
    """kapak altı, duvar içi hacim"""
    ic = taban_cizgi(-DUVAR).extrude(100)
    return ic ^ govde_dolu().translate([0, 0, -KAP])


def boss_merkezleri():
    lst = list(DUZ_VIDA)
    for (x, y) in PANEL_VIDA:
        q = panel_nokta(x, y)
        lst.append((q[0], q[1]))
    return lst


def bosslar():
    lst = []
    for (x, y) in DUZ_VIDA:
        lst.append(sil(x, y, 0, 60, BOSS_R))
    for (x, y) in PANEL_VIDA:
        q = panel_nokta(x, y)
        # eğik vida deliği derine indikçe arkaya kayar; direği o yöne kaydırıp büyüt
        lst.append(sil(q[0], q[1] + 1.4, 0, 60, BOSS_R + 0.7))
        # direk ile köşe duvarları arasında ince boşluk kalmasın: direği duvarlara doğru doldur
        sx = 1 if q[0] > 0 else -1
        lst.append(kutu(q[0] + sx * 4, q[1] + 1.4, 0, 8, 2 * (BOSS_R + 0.7), 60))
    for (x, y) in PANEL_VIDA[2:]:
        q = panel_nokta(x, y)
        lst.append(kutu(q[0], (q[1] + 1.4 + arka()[0]) / 2, 0, 2 * (BOSS_R + 0.7), arka()[0] - q[1] - 1.4, 60))
    return birlesim(lst) ^ govde_dolu().translate([0, 0, -KAP])


def cerceve():
    g = govde_dolu().translate([0, 0, -KAP])        # kapak altı
    g = g - kutu(0, 0, -10, 400, 400, 10 + Z_TABAN)  # taban altı
    g = g - ic_bolge()
    g = g + (bosslar() - kutu(0, 0, -10, 400, 400, 10 + Z_TABAN))
    kes = []
    # üst vidalar için kılavuz delikler
    for (x, y) in DUZ_VIDA:
        kes.append(sil(x, y, Z_DUZ - KAP - 9, 9.01, VIDA_PILOT / 2))
    for (x, y) in PANEL_VIDA:
        kes.append(panel(sil(x, y, PANEL_ALT - 7.5, 7.5 + 3.0, VIDA_PILOT / 2)))   # direğin tepesinden açık; eğik olduğu için 7.5 mm (direk yanından taşmasın)
    # alt (taban) vidaları: ön düz vidalar + arka panel vidaları
    for (x, y) in alt_vidalar():
        kes.append(sil(x, y, Z_TABAN - 0.01, 6.5, VIDA_PILOT / 2))
    # USB-C: duvarı içten incelt, yalnızca soket kadar ağız aç
    uz = usb_z()
    ya, _ = arka()
    kes.append(kutu(ZERO_X, ya - USB_DUVAR - 5, Z_TABAN - 0.01, USB_CEP_W, 10, uz + 3.5 - Z_TABAN))
    agiz = CrossSection.square([USB_W - USB_H, 0.01], center=True).offset(USB_H / 2 - 0.005, JoinType.Round, circular_segments=48)
    # kesit (x, z) düzleminde; y boyunca uzat
    kes.append(agiz.extrude(6).rotate([-90, 0, 0]).translate([ZERO_X, ya - 3, uz]))
    return g - birlesim(kes)


def on_cerceve():
    return cerceve() ^ kutu(0, Y_M - BIRLESIM_BOSLUK / 2 - 200, -10, 400, 400, 200)


def arka_cerceve():
    return cerceve() ^ kutu(0, Y_M + BIRLESIM_BOSLUK / 2 + 200, -10, 400, 400, 200)


def alt_vidalar():
    """taban -> gövde: dört köşe (ön düz kapak direkleri + arka panel direkleri)"""
    lst = [DUZ_VIDA[0], DUZ_VIDA[1]]
    for (x, y) in PANEL_VIDA[2:]:
        q = panel_nokta(x, y)
        lst.append((q[0], q[1]))
    return lst


def usb_z():
    return Z_TABAN + ZERO_TABAN + ZERO_KAL + 1.6


def zero_y1():
    """kartın USB kenarı (arka); soket kart kenarından ~0.6 taşar"""
    ya, _ = arka()
    return ya - USB_DUVAR - 0.6


def zero_y0():
    return zero_y1() - ZERO_UZ


AYAKLAR = [(-18.0, -58.5), (18.0, -58.5), (-18.0, 12.0), (18.0, 12.0)]


def taban():
    b = taban_cizgi().extrude(Z_TABAN)
    kes = []
    for (x, y) in alt_vidalar():
        kes.append(sil(x, y, -1, Z_TABAN + 2, VIDA_GECIS / 2))
        kes.append(sil(x, y, -0.01, 1.61, HAVSA / 2, VIDA_GECIS / 2))
    ya, _ = arka()
    for (x, y) in AYAKLAR:   # LED yuvalarından uzak (ön ayaklar ön LED ceplerinin önüne alındı)
        kes.append(sil(x, y, -0.01, 0.8, 5.3))
    b = b - birlesim(kes)
    # Zero yuvası: kart ortadaki bir dayanağa oturur; kenarlardaki lehim noktalarının altı boştur
    y1 = zero_y1(); y0 = zero_y0(); tol = 0.3
    h = ZERO_TABAN + ZERO_KAL + 1.0
    dy0 = y0 - tol - 1.2; dx = ZERO_EN / 2 + tol + 1.2
    yuva = kutu(ZERO_X, (dy0 + y1) / 2, Z_TABAN, 2 * dx, y1 - dy0, h)
    yuva = yuva - kutu(ZERO_X, (y0 - tol + y1 + 1) / 2, Z_TABAN - 1, ZERO_EN + 2 * tol, y1 + 1 - y0 + tol, h + 2)
    # lehim payı: kartın altında tabana 1.2 mm cep (kartın altında toplam 2.2 mm boşluk)
    b = b - kutu(ZERO_X, (y0 - tol + y1 + 0.5) / 2, Z_TABAN - LEHIM_CEP, ZERO_EN + 2 * tol, y1 + 0.5 - y0 + tol, LEHIM_CEP + 0.01)
    # orta dayanak: kenarlardan 3.5 mm, uzak uçtan 4 mm içeride (pin delikleri kenar boyunca)
    yuva = yuva + kutu(ZERO_X, (y0 + 4.0 + y1) / 2, Z_TABAN - LEHIM_CEP, ZERO_EN - 7.0, y1 - y0 - 4.0, LEHIM_CEP + ZERO_TABAN)
    # duvar içine taşmasın
    yuva = yuva ^ taban_cizgi(-DUVAR - 0.2).extrude(50)
    yuva = yuva - bosslar().translate([0, 0, 0]) - birlesim([sil(q[0], q[1], 0, 20, BOSS_R + 0.6) for q in boss_merkezleri()])
    return b + yuva


def kopruler(x, y, pay=0.0):
    """halka yuvasının altında, ada ile kapağı bağlayan köprüler (doğu ve batı kenarda)"""
    h = HALKA_DIS / 2 - HALKA_EN / 2
    lst = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            lst.append(kutu(x + sx * h, y + sy * KOPRU_Y, Z_DUZ - KAP - 0.5,
                            HALKA_EN + 1.0, KOPRU_W + 2 * pay, KOPRU_H + 0.5 + pay))
    return birlesim(lst)


def halka_difuzor(x, y):
    """üstten geçer; altındaki çentikler köprülere oturur"""
    dis, ic = tus_halkasi_cs(x, y)
    h = (dis - ic).extrude(KAP).translate([0, 0, Z_DUZ - KAP]) - kopruler(x, y, CENTIK_PAY)
    # alt kenarlarda 0.4 mm pah: yuvaya kolay girsin
    dis_k = dis.offset(-0.4, JoinType.Round, circular_segments=32)
    ic_k = ic.offset(0.4, JoinType.Round, circular_segments=32)
    alt = Manifold.batch_hull([(dis_k - ic).extrude(0.01).translate([0, 0, Z_DUZ - KAP]),
                               dis.extrude(0.01).translate([0, 0, Z_DUZ - KAP + 0.4])]) if False else None
    pah_dis = (dis - dis_k).extrude(0.4).translate([0, 0, Z_DUZ - KAP])
    pah_ic = (ic_k - ic).extrude(0.4).translate([0, 0, Z_DUZ - KAP])
    return h - pah_dis - pah_ic


# ---------------- Yerleşim bilgisi (görsel ve şema için) ----------------
def led_taban_konumlari():
    """tutuculu taban LED'leri; zincir Zero'nun solundan başlar, saatin tersine döner"""
    ya, _ = arka()
    ic = DUVAR + 0.3 + 5.1 + 1.2           # tutucunun dış kenarı duvara 0.3 mm
    xs = [-27.9, -9.3, 9.3, 27.9]
    lst = [(-24.0, ya - ic, 'x')]                         # arka sol
    lst.append((-30.5, 11.3, 'y'))                        # sol arka (direklerin içinde)
    for y in (-21.0, -39.8):                              # sol
        lst.append((-W2 + ic, y, 'y'))
    for x in xs:                                          # ön
        lst.append((x, Y_ON + ic, 'x'))
    for y in (-39.8, -21.0):                              # sağ
        lst.append((W2 - ic, y, 'y'))
    lst.append((30.5, 11.3, 'y'))                         # sağ arka
    lst.append((24.0, ya - ic, 'x'))                      # arka sağ
    return lst


def led_tus_konumlari():
    """(x, y, z) taban LED'lerinden sonra: arka satır sağdan sola, ön satır soldan sağa"""
    lst = []
    for x in SUTUN[::-1]:
        lst.append((x, SATIR[0] + LED_TUS_DY[0], Z_DUZ - KAP))
    for x in SUTUN:
        lst.append((x, SATIR[1] + LED_TUS_DY[1], Z_DUZ - KAP))
    return lst


def parcalar():
    halkalar = [halka_difuzor(x, y) for y in SATIR for x in SUTUN]
    return {
        'taban': taban(), 'cerceve': cerceve(),
        'duz_kapak': duz_kapak(), 'panel': eğik_panel(),
        'halkalar': halkalar,
    }


if __name__ == '__main__':
    P = parcalar()
    for k, v in P.items():
        for i, m in enumerate(v if isinstance(v, list) else [v]):
            print(k, i, m.status().name, 'genus', m.genus(), 'hacim', round(m.volume(), 1),
                  [round(a, 1) for a in m.bounding_box()])
            if isinstance(v, list):
                break


# =====================================================================
# LED TUTUCU (WS2812B modül: ~10 x 16.7 mm kart, 5x5 LED, kart üstünde C1 kondansatör ve 6 ped)
# Aynı parça hem tuşlarda (LED yukarı, kapağın altına) hem tabanda (LED aşağı) kullanılır.
# Yerel eksen: x kart boyu, y kart eni, z=0 tutucu tabanı; LED kafası z=TUT_H'de biter.
# =====================================================================
MOD_EN, MOD_BOY = 10.0, 16.7
MOD_KAL = 1.2            # kart kalınlığı (ölçüp değiştir)
LED_KAFA = 1.6           # 5050 LED yüksekliği
TUT_TOL = 0.1            # karta her yandan pay
TUT_TABAN = 0.8          # kartın arkasını taşıyan plaka
TUT_KOL = 1.2            # köşe kollarının kalınlığı
TUT_KOL_IC = 7.2         # kollar kart merkezinden en az bu kadar uzakta başlar (anahtar gövdesine çarpmasın)
TUT_H = TUT_TABAN + MOD_KAL + LED_KAFA
TUS_LED_KENAR = 7.2      # kart kenarı anahtar merkezinden bu kadar uzakta (anahtar gövdesi 7.0)


def led_tutucu():
    lh = MOD_BOY / 2 + TUT_TOL
    wh = MOD_EN / 2 + TUT_TOL
    p = kutu(0, 0, 0, 2 * lh + 2 * 0.6, 2 * wh, TUT_TABAN)          # kartın arkası
    kollar = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            # uzun kenar boyunca kol (yalnızca uçlarda: anahtar gövdesine çarpmasın)
            x0, x1 = TUT_KOL_IC, lh + 0.6
            kollar.append(kutu(sx * (x0 + x1) / 2, sy * (wh + TUT_KOL / 2), 0, x1 - x0, TUT_KOL, TUT_H))
            # uç durdurucu (kart boyuna kaymasın), pedlerin dışında kalan köşede
            kollar.append(kutu(sx * (lh + 0.3), sy * (wh - 1.0), 0, 0.6, 2.0, TUT_TABAN + MOD_KAL + 0.4))
    return p + birlesim(kollar)


def tutucu_ayak_izi():
    lh = MOD_BOY / 2 + TUT_TOL + 0.6
    wh = MOD_EN / 2 + TUT_TOL + TUT_KOL
    return lh, wh


# =====================================================================
# v3: TABANDA LED YUVALARI + KAPAK ALTINDA AÇILI LED BAĞLANTISI
# =====================================================================
YUVA_KAFA = 5.4          # LED kafası yuvası (5x5 + pay)
YUVA_KAFA_DER = 2.0      # kart yuvası 0.3 + kafa 1.6 + pay; altında 1.0 mm taban kalır
YUVA_C_DER = 1.0         # kondansatör (C1) cebi
YUVA_PED_DER = 1.4       # uçlardaki ped/lehim cebi
YUVA_KART_DER = 0.3      # kartın oturduğu sığ yuva (hizalama)
C1_X = MOD_BOY / 2 - 3.7     # C1'in kart merkezine uzaklığı (iki yöne de cep açılır)


def taban_led_yuvasi():
    """kart merkezinde, z=0 taban üst yüzeyi; x kart boyu"""
    k = []
    k.append(kutu(0, 0, -YUVA_KART_DER, MOD_BOY + 0.4, MOD_EN + 0.4, YUVA_KART_DER + 1))
    k.append(kutu(0, 0, -YUVA_KAFA_DER, YUVA_KAFA, YUVA_KAFA, YUVA_KAFA_DER + 1))
    for sx in (-1, 1):
        k.append(kutu(sx * C1_X, 0, -YUVA_C_DER, 2.6, 2.0, YUVA_C_DER + 1))
        # ped + lehim + tel çıkışı (kartın ucundan 1.5 mm dışarı taşar)
        k.append(kutu(sx * (MOD_BOY / 2 - 1.3 + 0.75), 0, -YUVA_PED_DER, 4.1, MOD_EN + 0.4, YUVA_PED_DER + 1))
    return birlesim(k)


def taban_led_konumlari_v3():
    ya, _ = arka()
    ic = DUVAR + (MOD_EN + 0.4) / 2 + 0.6
    lst = [(-24.0, ya - ic, 'x')]
    lst.append((-32.0, 14.0, 'y'))
    for y in (-22.0, -40.0):
        lst.append((-W2 + ic, y, 'y'))
    for x in (-28.0, -9.4, 9.4, 28.0):
        lst.append((x, Y_ON + ic, 'x'))
    for y in (-40.0, -22.0):
        lst.append((W2 - ic, y, 'y'))
    lst.append((32.0, 14.0, 'y'))
    lst.append((24.0, ya - ic, 'x'))
    return lst


def taban_v3():
    b = taban()
    kes = []
    for (x, y, o) in taban_led_konumlari_v3():
        m = taban_led_yuvasi()
        if o == 'y':
            m = m.rotate([0, 0, 90])
        kes.append(m.translate([x, y, Z_TABAN]))
    return b - birlesim(kes)


# ---------- açılı LED bağlantısı (kapağın altına) ----------
# Yerel: x kart boyu; y anahtardan dışa (yarık y=SLOT_Y); z=0 kapak alt yüzü, aşağısı negatif.
SLOT_Y = HALKA_DIS / 2 - HALKA_EN / 2          # 10.9: halka yarığının orta çizgisi
ACI_ALFA = 30.0          # LED yüzünün düşeyden eğimi (0 = tam yukarı, 90 = tam yana)
ACI_D = 2.0              # LED kafası üst yüzünün yarık tabanına uzaklığı
ACI_DUDAK = 0.8          # kartı alttan tutan dudak
ACI_YAN = 0.8            # uç duvarları
ACI_DIS_DUVAR = 1.2      # kartın üst kenarının dışındaki dikey duvar


def _vec(a):
    a = np.radians(a)
    n = np.array([-np.sin(a), np.cos(a)])      # LED yüzünün normali (y, z): anahtara ve yukarı
    u = np.array([np.cos(a), np.sin(a)])       # kart düzlemi içinde, dışa ve yukarı
    return n, u


def aci_geometri(alfa=None, d=None):
    alfa = ACI_ALFA if alfa is None else alfa
    d = ACI_D if d is None else d
    n, u = _vec(alfa)
    slot = np.array([SLOT_Y, 0.0])
    kafa_ust = slot - n * d                     # kafanın üst yüzü merkezi
    kart_on = kafa_ust - n * LED_KAFA           # kartın LED tarafı
    kart_arka = kart_on - n * MOD_KAL
    return dict(n=n, u=u, kafa_ust=kafa_ust, kart_on=kart_on, kart_arka=kart_arka)


def _cs(pts):
    pts = [tuple(map(float, q)) for q in pts]
    ar = sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))
    if ar < 0:
        pts = pts[::-1]
    return CrossSection([pts])


def _x_uzat(cs, x0, x1):
    """(y, z) kesitini x0..x1 arasında uzat"""
    return cs.extrude(x1 - x0).rotate([90, 0, 90]).translate([x0, 0, 0])


def aci_tutucu(alfa=None, d=None):
    g = aci_geometri(alfa, d)
    n, u = g['n'], g['u']
    w = (MOD_EN + 0.3) / 2
    ka = g['kart_arka']
    A = ka - u * w                     # kart arkası, alt-iç kenar
    B = ka + u * w                     # kart arkası, üst-dış kenar
    t = 1.6                            # kama kalınlığı (arka düzleme dik)
    A2 = A - n * t
    far = B[0] + ACI_DIS_DUVAR         # dış dikey duvar; kapağa buradan yapışır
    C = np.array([B[0], 0.0])
    L = MOD_BOY + 0.3
    kama = _x_uzat(_cs([A, B, C, (far, 0.0), (far, min(A2[1], A[1])), A2]), -(L / 2 + ACI_YAN), L / 2 + ACI_YAN)
    on = MOD_KAL + 0.4
    dudak = _x_uzat(_cs([A - u * ACI_DUDAK - n * t, A - n * t, A, A + n * on, A + n * on - u * ACI_DUDAK]), -L / 2, L / 2)
    uc = []
    for sx in (-1, 1):
        x0 = L / 2 if sx > 0 else -(L / 2 + ACI_YAN)
        uc.append(_x_uzat(_cs([A - u * ACI_DUDAK, B, B + n * on, A + n * on - u * ACI_DUDAK]), x0, x0 + ACI_YAN))
    t_ = kama + dudak + birlesim(uc)
    return t_ ^ kutu(0, 10, -40, 80, 80, 40)


def aci_modul(alfa=None, d=None):
    g = aci_geometri(alfa, d)
    n, u = g['n'], g['u']
    w = MOD_EN / 2
    kart = CrossSection([[tuple(g['kart_arka'] - u * w), tuple(g['kart_arka'] + u * w),
                          tuple(g['kart_on'] + u * w), tuple(g['kart_on'] - u * w)]])
    kart = kart.extrude(MOD_BOY).rotate([90, 0, 90]).translate([-MOD_BOY / 2, 0, 0])
    kafa = CrossSection([[tuple(g['kart_on'] - u * 2.5), tuple(g['kart_on'] + u * 2.5),
                          tuple(g['kafa_ust'] + u * 2.5), tuple(g['kafa_ust'] - u * 2.5)]])
    kafa = kafa.extrude(5.0).rotate([90, 0, 90]).translate([-2.5, 0, 0])
    return kart, kafa


def aci_tutucu_yerlesik(satir, x):
    """dünya koordinatında açılı tutucu + modül. Arka sıra: arka tarafta; ön sıra: ön tarafta (ön duvara yaslanır)"""
    t = aci_tutucu(); k, h = aci_modul()
    y = SATIR[satir]
    if satir == 1:
        t, k, h = (m.rotate([0, 0, 180]) for m in (t, k, h))
    t, k, h = (m.translate([x, y, Z_DUZ - KAP]) for m in (t, k, h))
    if satir == 1:
        t = t ^ kutu(0, 0, -50, 300, 2 * (-(Y_ON + DUVAR) - 0.2), 200)   # ön duvarın içinde kalsın
    return t, k, h


def tus_led_konumlari_v3():
    """(x, y) LED kafası merkezi; zincir: arka sıra sağdan sola, ön sıra soldan sağa"""
    g = aci_geometri()
    dy = g['kafa_ust'][0]
    lst = [(x, SATIR[0] + dy) for x in SUTUN[::-1]]
    lst += [(x, SATIR[1] - dy) for x in SUTUN]
    return lst


# v3 yerleşimini çizimlerin kullandığı adlara bağla
led_taban_konumlari = taban_led_konumlari_v3


def led_tus_konumlari():
    return [(x, y, Z_DUZ - KAP) for (x, y) in tus_led_konumlari_v3()]


# =====================================================================
# v4: TUŞ LED TUTUCULARI TABANA OTURUR (ayaklı), TABANDA YER İŞARETLERİ
# =====================================================================
AYAK_UST_BOSLUK = 0.5     # tutucunun üstü ile kapak arasında boşluk (kapak rahat açılsın)
ISARET_EN, ISARET_DER = 0.6, 0.4


def ayakli_tutucu_yerel():
    """yerel (anahtar) koordinat: tutucu tabana kadar uzanır, üstü kapağın 0.5 mm altında"""
    t = aci_tutucu()
    bb = t.bounding_box()
    z_taban = Z_TABAN - (Z_DUZ - KAP)
    g = aci_geometri(); n, u = g['n'], g['u']
    A = g['kart_arka'] - u * (MOD_EN + 0.3) / 2
    A2 = A - n * 1.6                         # kamanın alt-iç köşesi
    z_ust = min(A2[1], A[1]) + 0.3           # kamanın düz alt yüzüne binsin
    y0 = A2[0]
    sutun = kutu(0, (y0 + bb[4]) / 2, z_taban, bb[3] - bb[0], bb[4] - y0, z_ust - z_taban)
    # sütunun iç (anahtar tarafı) yüzü, dudağın altından dik iner
    return (t + sutun) ^ kutu(0, 0, z_taban, 200, 200, -AYAK_UST_BOSLUK - z_taban)


def ayakli_tutucu_yerlesik(satir, x):
    t = ayakli_tutucu_yerel(); k, h = aci_modul()
    y = SATIR[satir]
    if satir == 1:
        t, k, h = (m.rotate([0, 0, 180]) for m in (t, k, h))
    t, k, h = (m.translate([x, y, Z_DUZ - KAP]) for m in (t, k, h))
    if satir == 1:
        t = t ^ kutu(0, 0, -50, 300, 2 * (-(Y_ON + DUVAR) - 0.2), 200)
    return t, k, h


def taban_led_konumlari_v4():
    ya, _ = arka()
    ic = DUVAR + (MOD_EN + 0.4) / 2 + 0.6
    lst = [(-24.0, ya - ic, 'x'), (-32.0, 14.0, 'y')]
    for y in (-22.0, -40.0):
        lst.append((-W2 + ic, y, 'y'))
    for x in (-25.9, -8.6, 8.6, 25.9):       # ön: tuş LED ayaklarının arkasında
        lst.append((x, -46.5, 'x'))
    for y in (-40.0, -22.0):
        lst.append((W2 - ic, y, 'y'))
    lst += [(32.0, 14.0, 'y'), (24.0, ya - ic, 'x')]
    return lst


def taban_v4():
    b = taban()
    kes = []
    for (x, y, o) in taban_led_konumlari_v4():
        m = taban_led_yuvasi()
        if o == 'y':
            m = m.rotate([0, 0, 90])
        kes.append(m.translate([x, y, Z_TABAN]))
    # tuş LED tutucularının izi: çevre çizgisi + LED tarafını gösteren çentik
    for satir in (0, 1):
        for x in SUTUN:
            t, _, _ = ayakli_tutucu_yerlesik(satir, x)
            iz = t.slice(Z_TABAN + 0.05)
            cizgi = iz.offset(ISARET_EN / 2 + 0.15, JoinType.Miter) - iz.offset(-ISARET_EN / 2 + 0.15, JoinType.Miter)
            kes.append(cizgi.extrude(ISARET_DER + 1).translate([0, 0, Z_TABAN - ISARET_DER]))
            # LED tarafı (anahtara bakan kenar) için üçgen ok
            yk = SATIR[satir]
            yon = 1 if satir == 0 else -1
            yy = yk + yon * 5.5
            ok = _cs([(x - 1.6, yy + yon * 1.6), (x + 1.6, yy + yon * 1.6), (x, yy)])
            kes.append(ok.extrude(ISARET_DER + 1).translate([0, 0, Z_TABAN - ISARET_DER]))
    return b - birlesim(kes)


led_taban_konumlari = taban_led_konumlari_v4


# =====================================================================
# v5: ÜST KASA <-> ALT KASA KONNEKTÖRLERİ (tek sıra 2.54 mm header, iki sıra yan yana)
# Dişi tutucu arka duvara yapışır; erkek tutucu eğik panelin altına yapışır.
# =====================================================================
P254 = 2.54
HDR_TOL = 0.15
HDR_DUVAR = 0.8
ERKEK_GOVDE = 2.5         # erkek header plastik yüksekliği
ERKEK_KISA = 3.0          # kısa uç (tel lehimlenen)
DISI_YUK = 8.5            # dişi header gövde yüksekliği
ERKEK_ALT_Z = 24.0        # erkek plastiğin alt yüzü (dünya z)
KONN_BOSLUK = 0.5         # takılıyken erkek plastiği ile dişi üstü arası
KONNEKTORLER = [          # (ad, x merkezi, pin sayısı / sıra)
    ('SOL', -28.1, 6),
    ('SAG', 29.1, 5),
]


def panel_alt_z(y):
    t = np.tan(np.radians(TETA))
    return Z_DUZ + (y - Y_M) * t - KAP / np.cos(np.radians(TETA))


def konn_olcu(n):
    lx = n * P254 + 2 * HDR_TOL + 2 * HDR_DUVAR
    ly = 2 * P254 + 2 * HDR_TOL + 2 * HDR_DUVAR
    ya, _ = arka()
    y1 = ya - DUVAR                 # arka duvarın iç yüzü
    return lx, ly, y1 - ly, y1


def disi_tutucu(x, n):
    lx, ly, y0, y1 = konn_olcu(n)
    z1 = ERKEK_ALT_Z - KONN_BOSLUK
    z0 = z1 - DISI_YUK
    t = kutu(x, (y0 + y1) / 2, z0, lx, ly, DISI_YUK)
    t = t - kutu(x, (y0 + y1) / 2, z0 - 1, n * P254 + 2 * HDR_TOL, 2 * P254 + 2 * HDR_TOL, DISI_YUK + 2)
    # üstte 0.4 mm pah: erkek pinler kolay girsin
    return t


def erkek_tutucu(x, n):
    lx, ly, y0, y1 = konn_olcu(n)
    z0 = ERKEK_ALT_Z
    z_ust = panel_alt_z(y1) + 1       # eğik yüzün üstüne taşar, sonra kesilir
    t = kutu(x, (y0 + y1) / 2, z0, lx, ly, z_ust - z0)
    # panelin alt yüzüyle kes (eğik üst yüz)
    t = t - (govde_dolu().translate([0, 0, -KAP]) - govde_dolu().translate([0, 0, -200]) if False else
             panel(kutu(0, 0, 0, 200, 200, 30)))
    ic_x, ic_y = n * P254 + 2 * HDR_TOL, 2 * P254 + 2 * HDR_TOL
    t = t - kutu(x, (y0 + y1) / 2, z0 - 1, ic_x, ic_y, 1 + ERKEK_GOVDE)                  # plastik yuvası
    t = t - kutu(x, (y0 + y1) / 2, z0 + ERKEK_GOVDE, ic_x, ic_y, 40)                       # kısa uçlar + lehim
    t = t - kutu(x, y0 - 1, z0 + ERKEK_GOVDE + 0.6, ic_x, 4.0, 40)                          # tel çıkışı (öne)
    # plastiği yuvada tutan iki küçük dil (uçlarda)
    return t


def konn_yerlesik():
    lst = []
    for (ad, x, n) in KONNEKTORLER:
        lx, ly, y0, y1 = konn_olcu(n)
        d = disi_tutucu(x, n); e = erkek_tutucu(x, n)
        # header'lar (çakışma kontrolü için)
        cy = (y0 + y1) / 2
        disi_h = kutu(x, cy, ERKEK_ALT_Z - KONN_BOSLUK - DISI_YUK, n * P254, 2 * P254, DISI_YUK)
        disi_uc = kutu(x, cy, ERKEK_ALT_Z - KONN_BOSLUK - DISI_YUK - 3.0, n * P254 - 1, 2 * P254 - 1, 3.0)
        erkek_h = kutu(x, cy, ERKEK_ALT_Z, n * P254, 2 * P254, ERKEK_GOVDE)
        erkek_uc = kutu(x, cy, ERKEK_ALT_Z + ERKEK_GOVDE, n * P254 - 1, 2 * P254 - 1, ERKEK_KISA)
        lst.append((ad, n, d, e, disi_h + disi_uc + erkek_h + erkek_uc))
    return lst


# ---- v5b: konnektörler düz kapağın altında, sol ve sağ yan duvarda (1x10 tek sıra) ----
# Kısa uçlar 90° bükülüp tel yandan lehimlenir (yükseklik kazanmak için).
UC_BUKUK = 1.5            # bükülmüş uç + lehim yüksekliği
KONN_Y = -32.5            # konnektör merkezi (kapak vida direklerinin arası)
KONN_N = 10
KONN_TAKILI_BOSLUK = 0.3


def konn2_olcu(sag):
    lx = P254 + 2 * HDR_TOL + 2 * HDR_DUVAR
    ly = KONN_N * P254 + 2 * HDR_TOL + 2 * HDR_DUVAR
    duvar_ic = W2 - DUVAR
    x1 = duvar_ic if sag else -duvar_ic
    x0 = x1 - lx if sag else x1 + lx
    xc = (x0 + x1) / 2
    zk = Z_DUZ - KAP                                  # kapak alt yüzü
    z_erkek_ust = zk - UC_BUKUK
    z_erkek_alt = z_erkek_ust - ERKEK_GOVDE
    z_disi_ust = z_erkek_alt - KONN_TAKILI_BOSLUK
    z_disi_alt = z_disi_ust - DISI_YUK
    return dict(lx=lx, ly=ly, xc=xc, iceri=-1 if sag else 1, zk=zk, z_eu=z_erkek_ust, z_ea=z_erkek_alt,
                z_du=z_disi_ust, z_da=z_disi_alt)


def disi_tutucu2(sag):
    o = konn2_olcu(sag)
    t = kutu(o['xc'], KONN_Y, o['z_da'], o['lx'], o['ly'], DISI_YUK)
    t = t - kutu(o['xc'], KONN_Y, o['z_da'] - 1, P254 + 2 * HDR_TOL, KONN_N * P254 + 2 * HDR_TOL, DISI_YUK + 2)
    # üst kenarda giriş pahı
    pah = Manifold.batch_hull([kutu(o['xc'], KONN_Y, o['z_du'] - 0.01, P254 + 2 * HDR_TOL + 0.8, KONN_N * P254 + 2 * HDR_TOL + 0.8, 0.01),
                               kutu(o['xc'], KONN_Y, o['z_du'] - 0.4, P254 + 2 * HDR_TOL, KONN_N * P254 + 2 * HDR_TOL, 0.01)])
    return t - pah


def erkek_tutucu2(sag):
    o = konn2_olcu(sag)
    h = o['zk'] - o['z_ea']
    t = kutu(o['xc'], KONN_Y, o['z_ea'], o['lx'], o['ly'], h)
    ic = (P254 + 2 * HDR_TOL, KONN_N * P254 + 2 * HDR_TOL)
    t = t - kutu(o['xc'], KONN_Y, o['z_ea'] - 1, ic[0], ic[1], 1 + ERKEK_GOVDE)          # plastik yuvası (alttan)
    # bükülmüş uçlar ve teller için içeri bakan yarık
    t = t - kutu(o['xc'] + o['iceri'] * 2.0, KONN_Y, o['z_eu'] - 0.01, ic[0] + 4.0, ic[1], UC_BUKUK + 0.02)
    return t


def konn2_header(sag):
    o = konn2_olcu(sag)
    n = KONN_N
    d = kutu(o['xc'], KONN_Y, o['z_da'], P254, n * P254, DISI_YUK)
    d_uc = kutu(o['xc'] + o['iceri'] * 1.5, KONN_Y, o['z_da'] - UC_BUKUK, 3.0, n * P254 - 0.6, UC_BUKUK - 0.1)
    e = kutu(o['xc'], KONN_Y, o['z_ea'], P254, n * P254, ERKEK_GOVDE)
    e_uc = kutu(o['xc'] + o['iceri'] * 1.5, KONN_Y, o['z_eu'] + 0.05, 3.0, n * P254 - 0.6, UC_BUKUK - 0.1)
    return d + d_uc + e + e_uc


# ---- v6: eğik panel (ekran + encoderlar) için ayrı konnektör ----
# Panelin ön kenarının altında, menteşenin hemen arkasında, x boyunca tek 1x14 header.
# Dişi tutucu tabana yapışır (ayakta duran blok), erkek tutucu panelin alt yüzüne yapışır.
# Panel dikey kalkar; düz kapak da arka alt kenarı dik kesildiği için dikey kalkar -> ikisi ayrı ayrı çıkar.
PK_N = 14
PK_Y0 = 0.6               # tutucuların ön yüzü (menteşe y=-1)
PK_DUDAK = 0.6            # dişi header'ı alttan tutan dudak (ön tarafta)


def panel_alt_z_y(y):
    """panelin alt yüzünün dünya z'si (y'de)"""
    return Z_DUZ + (y - Y_M) * np.tan(np.radians(TETA)) - KAP / np.cos(np.radians(TETA))


def pk_olcu():
    lx = PK_N * P254 + 2 * HDR_TOL + 2 * HDR_DUVAR
    ly = P254 + 2 * HDR_TOL + 2 * HDR_DUVAR
    y0, y1 = PK_Y0, PK_Y0 + ly
    yc = (y0 + y1) / 2
    zt = panel_alt_z_y(y0)                     # tutucunun en alçak tepe noktası
    z_eu = zt - UC_BUKUK
    z_ea = z_eu - ERKEK_GOVDE
    z_du = z_ea - KONN_TAKILI_BOSLUK
    z_da = z_du - DISI_YUK
    return dict(lx=lx, ly=ly, y0=y0, y1=y1, yc=yc, zt=zt, z_eu=z_eu, z_ea=z_ea, z_du=z_du, z_da=z_da,
                ic=(PK_N * P254 + 2 * HDR_TOL, P254 + 2 * HDR_TOL))


def pk_disi():
    o = pk_olcu(); ix, iy = o['ic']
    h = o['z_du'] - Z_TABAN
    t = kutu(0, o['yc'], Z_TABAN, o['lx'], o['ly'], h)
    t = t - kutu(0, o['yc'], o['z_da'], ix, iy, DISI_YUK + 1)                       # header yuvası
    # bükülmüş uçlar: yuvanın altında, arkaya (Zero'ya) doğru açık yarık; önde header'ı tutan dudak
    yarik = kutu(0, o['yc'] + PK_DUDAK / 2 + 2, o['z_da'] - UC_BUKUK, ix, iy - PK_DUDAK + 4, UC_BUKUK + 0.01)
    # tarak: uçlar arasında dişler (uçlar birbirine değmez, arka duvar köprüsü kısa kalır)
    dis = [kutu((i - (PK_N - 2) / 2) * P254, o['yc'] + 2, o['z_da'] - UC_BUKUK - 1, P254 - 1.3, 10, UC_BUKUK + 2)
           for i in range(PK_N - 1)]
    t = t - (yarik - birlesim(dis))
    pah = Manifold.batch_hull([kutu(0, o['yc'], o['z_du'] - 0.01, ix + 0.8, iy + 0.8, 0.01),
                               kutu(0, o['yc'], o['z_du'] - 0.4, ix, iy, 0.01)])
    return t - pah


def pk_erkek():
    o = pk_olcu(); ix, iy = o['ic']
    t = kutu(0, o['yc'], o['z_ea'], o['lx'], o['ly'], 30)
    t = t - panel(kutu(0, 0, PANEL_ALT, 300, 300, 30))           # üst yüz panel alt yüzüne oturur
    t = t - kutu(0, o['yc'], o['z_ea'] - 1, ix, iy, 1 + ERKEK_GOVDE)
    t = t - kutu(0, o['yc'] + 2.0, o['z_eu'] - 0.01, ix, iy + 4.0, UC_BUKUK + 0.02)   # uçlar arkaya bükülür
    return t


def pk_header():
    o = pk_olcu(); n = PK_N
    d = kutu(0, o['yc'], o['z_da'], n * P254, P254, DISI_YUK)
    d_uc = birlesim([kutu((i - (n - 1) / 2) * P254, o['yc'] + 1.5, o['z_da'] - UC_BUKUK + 0.05, 0.64, 3.0, UC_BUKUK - 0.1)
                     for i in range(n)])
    e = kutu(0, o['yc'], o['z_ea'], n * P254, P254, ERKEK_GOVDE)
    e_uc = kutu(0, o['yc'] + 1.5, o['z_eu'] + 0.05, n * P254 - 0.6, 3.0, UC_BUKUK - 0.1)
    return d + d_uc + e + e_uc


def duz_kapak_v6():
    """arka alt kenarı dik kesilmiş kapak: panel yerindeyken dikey kalkabilir"""
    y_kes = Y_M - (BIRLESIM_BOSLUK / 2) / np.cos(np.radians(TETA / 2))
    return duz_kapak() ^ kutu(0, y_kes - 100, -10, 400, 200, 100)


# =====================================================================
# v7: yapıştırmalı her şey baskılara gömülü. Header'lar sonradan takılır:
# uçlar önceden 90° bükülüp (tel lehimli) header yuvaya uzunlamasına kaydırılır,
# bükülü uçlar iç duvardaki tarak aralıklarından geçer.
# =====================================================================
TARAK_ARALIK = 1.5        # uç + tel için tarak boşluğu
KAPAK_KONN_N = 8          # T1..T6 + GND + yedek
KAPAK_KONN_Y = -21.89     # sol duvarda: ön ucu -33.0 (sol-ön LED'in arkası), arka ucu kapak direğine kaynar


def _hdr_ic(n):
    return n * P254 + 2 * HDR_TOL, P254 + 2 * HDR_TOL


def _tarak(n, z0, z1):
    """her pin için: cebin ortasından içe (+y) doğru tam yükseklikte açık yarık"""
    L, W = _hdr_ic(n)
    return birlesim([kutu((i - (n - 1) / 2) * P254, (-0.4 + W / 2 + HDR_DUVAR + 1) / 2, z0,
                          TARAK_ARALIK, W / 2 + HDR_DUVAR + 1.4, z1 - z0) for i in range(n)])


def disi_yerel(n, z_taban, z_da, z_du):
    """yerel: sıra x boyunca, uçlar +y'ye bükülür. Dişi header z_da..z_du, altında z_taban'a kadar dolu."""
    L, W = _hdr_ic(n)
    t = kutu(0, 0, z_taban, L + 2 * HDR_DUVAR, W + 2 * HDR_DUVAR, z_du - z_taban)
    t = t - kutu(0, 0, z_da, L, W, z_du - z_da + 1)
    t = t - _tarak(n, z_da - UC_BUKUK, z_du + 1)
    pah = Manifold.batch_hull([kutu(0, 0, z_du - 0.01, L + 0.8, W + 0.8, 0.01), kutu(0, 0, z_du - 0.4, L, W, 0.01)])
    return t - pah


def erkek_yerel(n, z_ea, z_tavan):
    """yerel: erkek plastiği z_ea..z_ea+2.5 (pinler aşağı), üstte bükülü uçlar, tavan (kapak/panel) z_tavan"""
    L, W = _hdr_ic(n)
    t = kutu(0, 0, z_ea, L + 2 * HDR_DUVAR, W + 2 * HDR_DUVAR, z_tavan + 20 - z_ea)
    t = t - kutu(0, 0, z_ea - 1, L, W, 1 + ERKEK_GOVDE)
    t = t - _tarak(n, z_ea - 1, z_tavan + 20)
    return t


def kapak_konn_yerlesim(m):
    """yerel -> sol yan duvar: sıra y boyunca, uçlar +x (içeri)"""
    _, W = _hdr_ic(KAPAK_KONN_N)
    xc = -(W2 - DUVAR) + HDR_DUVAR + W / 2
    return m.rotate([0, 0, -90]).translate([xc, KAPAK_KONN_Y, 0])


def kapak_disi7():
    o = konn2_olcu(False)
    return kapak_konn_yerlesim(disi_yerel(KAPAK_KONN_N, Z_TABAN, o['z_da'], o['z_du']))


def kapak_erkek7():
    o = konn2_olcu(False)
    e = kapak_konn_yerlesim(erkek_yerel(KAPAK_KONN_N, o['z_ea'], o['zk']))
    return e ^ kutu(0, 0, 0, 400, 400, o['zk'] + 0.5)


def panel_disi14():
    o = pk_olcu()
    return disi_yerel(PK_N, Z_TABAN, o['z_da'], o['z_du']).translate([0, o['yc'], 0])


def panel_erkek14():
    o = pk_olcu()
    e = erkek_yerel(PK_N, o['z_ea'], o['zt']).translate([0, o['yc'], 0])
    return e - panel(kutu(0, 0, PANEL_ALT + 0.3, 300, 300, 40))     # panelin 0.3 mm içine gömülür


def taban_led_konumlari_v7():
    lst = taban_led_konumlari_v4()
    lst[2] = (-35.5, -22.0, 'y')     # sol-arka LED kapak konnektörüne yer açmak için içeri
    lst[3] = (-40.0, -42.0, 'y')     # sol-ön LED 2 mm öne (8 pinlik konnektöre yer)
    return lst


def taban_v7():
    b = taban()
    kes = []
    for (x, y, o) in taban_led_konumlari_v7():
        m = taban_led_yuvasi()
        if o == 'y':
            m = m.rotate([0, 0, 90])
        kes.append(m.translate([x, y, Z_TABAN]))
    b = b - birlesim(kes)
    tut = [ayakli_tutucu_yerlesik(s, x)[0].translate([0, 0, -0.2]) for s in (0, 1) for x in SUTUN]  # 0.2 gömülü
    return b + birlesim(tut) + panel_disi14().translate([0, 0, -0.2]) ^ kutu(0, 0, -1, 400, 400, 100)


def govde_v7():
    return cerceve() + (kapak_disi7() ^ ic_bolge().translate([0, 0, 0]) if False else kapak_disi7())


def duz_kapak_v7():
    return duz_kapak_v6() + kapak_erkek7()


def panel_v7():
    return eğik_panel() + panel_erkek14()


led_taban_konumlari = taban_led_konumlari_v7


# ---- v8: eski basılmış taban+gövde ile; üst kapaklar yeniden basılır (erkek yuvalar gömülü),
#      dişi tutucular yapıştırmalı ----
def duz_kapak_v8():
    T_ = konn2_olcu(False)
    _, W = _hdr_ic(8)
    xc = -(W2 - DUVAR) + HDR_DUVAR + W / 2
    e = erkek_yerel(8, T_['z_ea'], T_['zk']).rotate([0, 0, -90]).translate([xc, KONN_Y, 0])
    return duz_kapak_v6() + (e ^ kutu(0, 0, 0, 400, 400, T_['zk'] + 0.5))


def panel_disi14_yapistirmali():
    return panel_disi14()          # z=3'ten başlar, tabanın üst yüzüne yapışır


# ---- v8b: kapak konnektörü duvardan 1.5 mm içeri (baskıda erkek yuva duvara değdi) ----
KAPAK_KONN_ICERI = 1.5
DUVAR_DOLGU = KAPAK_KONN_ICERI - 0.2      # dişi tutucunun duvar tarafına ek kalınlık (0.2 yapıştırıcı payı)


def duz_kapak_v8b():
    o = konn2_olcu(False)
    _, W = _hdr_ic(8)
    xc = -(W2 - DUVAR) + HDR_DUVAR + W / 2 + KAPAK_KONN_ICERI
    e = erkek_yerel(8, o['z_ea'], o['zk']).rotate([0, 0, -90]).translate([xc, KONN_Y, 0])
    return duz_kapak_v6() + (e ^ kutu(0, 0, 0, 400, 400, o['zk'] + 0.5))


def kapak_disi8_v8b():
    """yapıştırmalı dişi: aynı cep, 1.5 mm içeride; duvar tarafı 1.3 mm kalınlaştırıldı"""
    global KONN_N
    eski = KONN_N; KONN_N = 8
    d = disi_tutucu2(False).translate([KAPAK_KONN_ICERI, 0, 0])
    o = konn2_olcu(False)
    KONN_N = eski
    bb = d.bounding_box()
    dolgu = kutu(bb[0] - DUVAR_DOLGU / 2 + 0.01, KONN_Y, o['z_da'], DUVAR_DOLGU + 0.02, bb[4] - bb[1], DISI_YUK)
    return d + dolgu


# ---- v8c: panel dişi tutucusu (14 pin) cebi büyütüldü: dişi header girmedi ----
DISI14_TOL = 0.35         # her yandan (önce 0.15)


def panel_disi14_v8c():
    global HDR_TOL
    eski = HDR_TOL; HDR_TOL = DISI14_TOL
    try:
        o = pk_olcu()
        d = disi_yerel(PK_N, Z_TABAN, o['z_da'], o['z_du']).translate([0, o['yc'], 0])
    finally:
        HDR_TOL = eski
    return d


# ---- v9: şeffaf taban + 14 pinlik dişi yuva tek parça (v8c cebi, erkek ile aynı merkezde) ----
def taban_v9():
    d = panel_disi14_v8c()
    bb = d.bounding_box()
    d = d.translate([0, pk_olcu()['yc'] - (bb[1] + bb[4]) / 2, -0.2])   # erkek yuvayla aynı merkez, tabana 0.2 gömülü
    return taban_v4() + d


# ---- v10: şeffaf taban + 6 tuş LED tutucusu + 14 pinlik dişi yuva tek parça (LED yerleri v4) ----
def taban_v10():
    b = taban()
    kes = []
    for (x, y, o) in taban_led_konumlari_v4():
        m = taban_led_yuvasi()
        if o == 'y':
            m = m.rotate([0, 0, 90])
        kes.append(m.translate([x, y, Z_TABAN]))
    b = b - birlesim(kes)
    tut = [ayakli_tutucu_yerlesik(s, x)[0].translate([0, 0, -0.2]) for s in (0, 1) for x in SUTUN]
    d = panel_disi14_v8c()
    bb = d.bounding_box()
    d = d.translate([0, pk_olcu()['yc'] - (bb[1] + bb[4]) / 2, -0.2])
    return (b + birlesim(tut) + d) ^ kutu(0, 0, -1, 400, 400, 100)


# ---- v11: her şey tek parça (yapıştırma yok) ----
#  - tuş LED yükselticileri her sırada tek sağlam blok, uçlardaki çıkıntılar (yan duvarlar) yok
#  - gövdenin sol duvarında dişi 1x8 yuvası (tabandan yükselen sütun, kayarak takılır)
#  - sol taban LED'leri sütuna yer açmak için içeri alınıp x yönünde döndürüldü
def taban_led_konumlari_v11():
    lst = taban_led_konumlari_v4()
    lst[2] = (-31.0, -22.0, 'x')
    lst[3] = (-31.0, -35.5, 'x')
    return lst


def led_blok_yerel():
    """tutucunun orta kesiti (yan duvarsız) üç tuş boyunca uzatılır"""
    t = ayakli_tutucu_yerel()
    prof = t.rotate([0, -90, 0]).slice(0.0)          # (y, -z) değil: rotate sonrası x'=-z, y'=y
    pts = [[(-p[0], p[1]) for p in poly] for poly in prof.to_polygons()]   # (z, y)
    pts = [[(p[1], p[0]) for p in poly] for poly in pts]                   # (y, z)
    cs = _cs(pts[0]) if len(pts) == 1 else CrossSection([*pts], FillRule.EvenOdd) if False else _cs(pts[0])
    bb = t.bounding_box()
    x0 = SUTUN[0] + bb[0]; x1 = SUTUN[-1] + bb[3]
    # dudağın altındaki 60° sarkmayı 45° dolgu ile kapat
    ys = [q[0] for q in pts[0]]
    yl = min(ys)
    alt = min(q[1] for q in pts[0] if q[0] < 9.0)
    yk = min(q[0] for q in pts[0] if abs(q[1] - (-16.0)) < 1e-3)    # sütunun ön yüzü
    lip = [q for q in pts[0] if abs(q[1] - alt) < 1e-6][0]
    ust = max(q[1] for q in pts[0] if abs(q[0] - yk) < 1e-3 and q[1] < -6.0)   # sütun yüzünün dudakla birleştiği nokta
    dolgu = _cs([(lip[0] - 0.01, lip[1]), (yk + 0.01, ust + 0.01), (yk + 0.01, lip[1] - (yk - lip[0]) - 0.01)])
    return _x_uzat(cs + dolgu, x0, x1)


def led_blok_yerlesik(satir):
    t = led_blok_yerel()
    y = SATIR[satir]
    if satir == 1:
        t = t.rotate([0, 0, 180])
    t = t.translate([0, y, Z_DUZ - KAP])
    if satir == 1:
        t = t ^ kutu(0, 0, -50, 300, 2 * (-(Y_ON + DUVAR) - 0.2), 200)
    return t


def _tol_ile(fn, tol):
    global HDR_TOL
    eski = HDR_TOL; HDR_TOL = tol
    try:
        return fn()
    finally:
        HDR_TOL = eski


def govde_disi8():
    o = konn2_olcu(False)
    xc = -(W2 - DUVAR) + HDR_DUVAR + (P254 + 2 * HDR_TOL) / 2 + KAPAK_KONN_ICERI   # erkek ile aynı merkez
    d = _tol_ile(lambda: disi_yerel(8, Z_TABAN, o['z_da'], o['z_du']), DISI14_TOL)
    d = d.rotate([0, 0, -90]).translate([xc, KONN_Y, 0])
    bb = d.bounding_box()
    dolgu = kutu((-(W2 - DUVAR) - 0.5 + bb[0]) / 2, KONN_Y, Z_TABAN, bb[0] - (-(W2 - DUVAR) - 0.5), bb[4] - bb[1], o['z_du'] - Z_TABAN)
    return d + dolgu


def govde_v11():
    return cerceve() + govde_disi8()


def taban_v11():
    b = taban()
    kes = []
    for (x, y, o) in taban_led_konumlari_v11():
        m = taban_led_yuvasi()
        if o == 'y':
            m = m.rotate([0, 0, 90])
        kes.append(m.translate([x, y, Z_TABAN]))
    b = b - birlesim(kes)
    bloklar = [led_blok_yerlesik(s).translate([0, 0, -0.2]) for s in (0, 1)]
    d = panel_disi14_v8c()
    bb = d.bounding_box()
    d = d.translate([0, pk_olcu()['yc'] - (bb[1] + bb[4]) / 2, -0.2])
    return (b + birlesim(bloklar) + d) ^ kutu(0, 0, -1, 400, 400, 100)


# ---- v12: LED bloğunun öndeki dudağı (tırnak) kaldırıldı; arka blok ile 14'lü dişi yuva arası dolduruldu ----
def led_blok_yerel_v12():
    t = ayakli_tutucu_yerel()
    prof = t.rotate([0, -90, 0]).slice(0.0)
    poly = [(p[1], -p[0]) for p in prof.to_polygons()[0]]          # (y, z)
    yk = min(q[0] for q in poly if abs(q[1] + 16.0) < 1e-3)         # sütunun ön yüzü (9.64)
    # kart arka yüzü çizgisinin ön ucu (dudağın başladığı nokta): yüzey çizgisi üzerindeki en düşük-ön nokta
    tab_on = min((q for q in poly if q[0] > yk - 1.0 and q[0] < yk and q[1] > -7.0), key=lambda q: q[1])
    yeni = []
    for q in poly:
        if q[0] < yk and q[1] > -9.0 and q is not tab_on:
            continue                                                  # dudak noktaları atılır
        yeni.append(q)
    # dudak yerine: kart yüzeyinin ön ucundan sütun yüzüne 45° iniş
    i = yeni.index(tab_on)
    yeni.insert(i + 1, (yk, tab_on[1] - (yk - tab_on[0])))
    cs = _cs(yeni)
    bb = t.bounding_box()
    return _x_uzat(cs, SUTUN[0] + bb[0], SUTUN[-1] + bb[3]), yeni


def led_blok_yerlesik_v12(satir):
    t = led_blok_yerel_v12()[0]
    y = SATIR[satir]
    if satir == 1:
        t = t.rotate([0, 0, 180])
    t = t.translate([0, y, Z_DUZ - KAP])
    if satir == 1:
        t = t ^ kutu(0, 0, -50, 300, 2 * (-(Y_ON + DUVAR) - 0.2), 200)
    return t


def taban_v12():
    b = taban()
    kes = []
    for (x, y, o) in taban_led_konumlari_v11():
        m = taban_led_yuvasi()
        if o == 'y':
            m = m.rotate([0, 0, 90])
        kes.append(m.translate([x, y, Z_TABAN]))
    b = b - birlesim(kes)
    bloklar = [led_blok_yerlesik_v12(s).translate([0, 0, -0.2]) for s in (0, 1)]
    d = panel_disi14_v8c()
    bb = d.bounding_box()
    d = d.translate([0, pk_olcu()['yc'] - (bb[1] + bb[4]) / 2, -0.2])
    db = d.bounding_box()
    arka_blok = bloklar[0].bounding_box()
    ara = kutu(0, (arka_blok[4] + db[1]) / 2, Z_TABAN - 0.2, db[3] - db[0], db[1] - arka_blok[4] + 0.2, db[5] - Z_TABAN + 0.2)
    return (b + birlesim(bloklar) + d + ara) ^ kutu(0, 0, -1, 400, 400, 100)


# ---- v13: LED dudağı geri geldi; header yuvalarındaki tarak dişleri (kırılıyordu) kaldırıldı ----
def _tarak(n, z0, z1):
    """dişsiz: cebin ortasından içe doğru tek, kesintisiz yarık (uçlar ve teller rahat geçer)"""
    L, W = _hdr_ic(n)
    return kutu(0, (-0.4 + W / 2 + HDR_DUVAR + 1) / 2, z0, L, W / 2 + HDR_DUVAR + 1.4, z1 - z0)


def taban_v13():
    b = taban()
    kes = []
    for (x, y, o) in taban_led_konumlari_v11():
        m = taban_led_yuvasi()
        if o == 'y':
            m = m.rotate([0, 0, 90])
        kes.append(m.translate([x, y, Z_TABAN]))
    b = b - birlesim(kes)
    bloklar = [led_blok_yerlesik(s).translate([0, 0, -0.2]) for s in (0, 1)]
    d = panel_disi14_v8c()
    bb = d.bounding_box()
    d = d.translate([0, pk_olcu()['yc'] - (bb[1] + bb[4]) / 2, -0.2])
    db = d.bounding_box()
    arka_blok = bloklar[0].bounding_box()
    ara = kutu(0, (arka_blok[4] + db[1]) / 2, Z_TABAN - 0.2, db[3] - db[0], db[1] - arka_blok[4] + 0.2, db[5] - Z_TABAN + 0.2)
    return (b + birlesim(bloklar) + d + ara) ^ kutu(0, 0, -1, 400, 400, 100)


led_taban_konumlari = taban_led_konumlari_v11


def led_tus_konumlari_son():
    """(x, y) tuş LED'leri, yükseltici blokların eğik yüzünde: arka sıra sağdan sola, ön sıra soldan sağa"""
    dy = (8.84 + 17.76) / 2
    return [(x, SATIR[0] + dy) for x in SUTUN[::-1]] + [(x, SATIR[1] - dy) for x in SUTUN]
