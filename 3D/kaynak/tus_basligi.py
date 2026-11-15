# MX tuş başlığı: alt 18x18, üst 14x14, 8 mm yükseklik; üst yüz tablada basılır (sap yukarı bakar)
import sys
import numpy as np
from manifold3d import Manifold, CrossSection, JoinType
import tasarim3 as T

ALT, UST, H = 18.0, 14.0, 7.4
DUVAR_K, TAVAN = 1.4, 2.5
SAP_BOY = 3.9                    # testte 6.5 idi, 2.6 mm uzundu
SAP_Z = H - TAVAN - SAP_BOY      # sapın alt ucu etek alt kenarından 1.0 mm yukarıda
SAP_R = 2.75                     # sap silindiri (5.5 mm, MX standardı)
ARTI_UZ, ARTI_EN = 4.285, 1.385   # 1.37 dar, 1.40 bol -> ortası
ARTI_DER = 3.7


def kare(a, r):
    return CrossSection.square([a - 2 * r, a - 2 * r], center=True).offset(r, JoinType.Round, circular_segments=48)


def kesik_piramit(a0, a1, h, r0, r1, z0=0.0):
    alt = kare(a0, r0).extrude(0.01).translate([0, 0, z0])
    ust = kare(a1, r1).extrude(0.01).translate([0, 0, z0 + h - 0.01])
    return Manifold.batch_hull([alt, ust])


def artı(uz, en, der):
    return (T.kutu(0, 0, -0.01, uz, en, der) + T.kutu(0, 0, -0.01, en, uz, der))


def tus_basligi(uz=ARTI_UZ, en=ARTI_EN):
    dis = kesik_piramit(ALT, UST, H, 2.0, 2.4)
    # üst kenarda 0.6 mm pah (tablada güzel kenar)
    dis = dis ^ (kesik_piramit(ALT + 10, UST + 10, H - 0.6, 2.0, 2.4) + kesik_piramit(UST, UST - 1.2, 0.6, 2.4, 1.8, H - 0.6))
    ic = kesik_piramit(ALT - 2 * DUVAR_K, UST - 2 * DUVAR_K - 0.6, H - TAVAN, 1.2, 1.2, -0.01)
    k = dis - ic
    # sap yerel koordinatta (alt ucu z=0), sonra SAP_Z kadar yukarı
    sap = Manifold.cylinder(SAP_BOY + 0.2, SAP_R, SAP_R, 48)
    # sap ile yan duvarlar arasında 4 ince destek (sap yanlara oynamasın)
    ic_yerel = ic.translate([0, 0, -SAP_Z])
    for a in (0, 90):
        sap = sap + (T.kutu(0, 0, ARTI_DER, ALT - 2, 1.0, SAP_BOY - ARTI_DER + 0.2).rotate([0, 0, a]) ^ ic_yerel)
    pah = Manifold.batch_hull([artı(uz + 0.6, en + 0.6, 0.01).translate([0, 0, -0.01]),
                               artı(uz, en, 0.01).translate([0, 0, 0.3])])
    dis_pah = Manifold.cylinder(0.4, SAP_R + 1, SAP_R + 1, 48) - Manifold.cylinder(0.4, SAP_R - 0.4, SAP_R, 48)
    sap = sap - artı(uz, en, ARTI_DER) - pah - dis_pah.translate([0, 0, -0.001])
    return k + sap.translate([0, 0, SAP_Z])


def tek_sap_testi():
    """başlığın sapının aynısı, küçük bir plakanın üstünde (ağız yukarıda)"""
    p = T.kutu(0, 0, 0, 10, 10, 1.2)
    sap = Manifold.cylinder(5.0, SAP_R, SAP_R, 48).translate([0, 0, 1.2])
    k = (p + sap).rotate([180, 0, 0]).translate([0, 0, 6.2])   # ağız z=0'da -> çevir
    m = tus_basligi().rotate([180, 0, 0])
    # gerçek başlığın sap bölgesini kes (ağzı ve pahları aynen korur)
    bb = m.bounding_box()
    kes = m ^ Manifold.cylinder(20, SAP_R + 0.01, SAP_R + 0.01, 48).translate([0, 0, bb[2] - 1])
    return kes


def sap_testi():
    """üç farklı artı payı: 1.25 / 1.35 / 1.45 (en), işaret çentiği sayısı = sıra"""
    lst = []
    for i, en in enumerate((1.25, 1.35, 1.45)):
        p = T.kutu(0, 0, 0, 8, 8, 5) - artı(ARTI_UZ - 1.35 + en, en, ARTI_DER + 0.01)
        for j in range(i + 1):
            p = p - T.kutu(-3 + j * 1.5, 3.6, 4.4, 0.8, 1.2, 1)
        lst.append(p.translate([i * 10, 0, 0]))
    return T.birlesim(lst)


if __name__ == '__main__':
    from disari3 import stl_yaz, tablaya
    out = sys.argv[1]
    k = tus_basligi()
    print(k.status().name, len(k.decompose()), [round(v, 2) for v in k.bounding_box()], round(k.volume() / 1000, 2), 'cm3')
    stl_yaz(tablaya(k.rotate([180, 0, 0])), f'{out}/07_tus_basligi_x6.stl')
    stl_yaz(tablaya(sap_testi()), f'{out}/00c_tus_sap_testi.stl')
