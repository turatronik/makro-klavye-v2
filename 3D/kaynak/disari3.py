# v2 parçalarını baskı yönünde STL olarak dışa aktarır + tolerans test parçası
import sys
import numpy as np
from manifold3d import Manifold
import tasarim3 as T
import kutu as K1

out = sys.argv[1] if len(sys.argv) > 1 else "."


def stl_yaz(m, yol):
    me = m.to_mesh()
    v = np.asarray(me.vert_properties)[:, :3].astype(np.float32)
    t = np.asarray(me.tri_verts).astype(np.int64)
    a, b, c = v[t[:, 0]], v[t[:, 1]], v[t[:, 2]]
    n = np.cross(b - a, c - a); ln = np.linalg.norm(n, axis=1, keepdims=True); n = n / np.where(ln == 0, 1, ln)
    rec = np.zeros(len(t), dtype=[('n', '<f4', 3), ('a', '<f4', 3), ('b', '<f4', 3), ('c', '<f4', 3), ('x', '<u2')])
    rec['n'], rec['a'], rec['b'], rec['c'] = n, a, b, c
    with open(yol, 'wb') as f:
        f.write(b'Makro Klavye v2'.ljust(80, b' ')); f.write(np.uint32(len(t)).tobytes()); f.write(rec.tobytes())


def tablaya(m):
    bb = m.bounding_box()
    return m.translate([-(bb[0] + bb[3]) / 2, -(bb[1] + bb[4]) / 2, -bb[2]])


def panel_duz(m):
    """paneli yerel koordinata getirir (üst yüz z=0), sonra ters çevirir: üst yüz tablada"""
    p = T.panel_merkez()
    return m.translate(list(-p)).rotate([-T.TETA, 0, 0]).rotate([180, 0, 0])


def test_parcasi():
    """tolerans testi: MX yuvası + halka (köprülü) + halka difüzörü yeri, encoder deliği, vida kılavuzu, düğme mili"""
    plaka = T.kutu(0, 0, T.Z_DUZ - T.KAP, 62, 32, T.KAP)
    kes = []
    x, y = -14.0, 0.0
    kes.append(T.kutu(x, y, T.Z_DUZ - T.KAP - 1, T.MX, T.MX, T.KAP + 2))
    kes.append(T.kutu(x, y, T.Z_DUZ - T.KAP - 1, T.MX_RAHAT, T.MX_RAHAT, 1 + T.KAP - T.MX_SAC))
    dis, ic = T.tus_halkasi_cs(x, y, T.HALKA_PAY, T.HALKA_PAY)
    kes.append((dis - ic).extrude(T.KAP + 2).translate([0, 0, T.Z_DUZ - T.KAP - 1]) - T.kopruler(x, y))
    kes.append(T.sil(13, 6, T.Z_DUZ - T.KAP - 1, T.KAP + 2, T.ENC_DELIK / 2))
    kes.append(T.sil(13, -8, T.Z_DUZ - T.KAP - 1, T.KAP + 2, T.VIDA_PILOT / 2))
    kes.append(T.sil(23, -8, T.Z_DUZ - T.KAP - 1, T.KAP + 2, T.VIDA_GECIS / 2))
    p = plaka - T.birlesim(kes)
    p = tablaya(p.rotate([180, 0, 0]))          # gerçek kapak gibi üst yüz tablada
    # USB ağzı denemesi: plakanın yanında dik duran 1.2 mm duvar (arka duvarın inceltilmiş kısmı gibi)
    from manifold3d import CrossSection, JoinType
    duvar = T.kutu(0, 0, 0, 16, 1.2, 9)
    agiz = CrossSection.square([T.USB_W - T.USB_H, 0.01], center=True).offset(T.USB_H / 2 - 0.005, JoinType.Round, circular_segments=48)
    duvar = duvar - agiz.extrude(6).rotate([-90, 0, 0]).translate([0, -3, T.usb_z() - T.Z_TABAN])
    ayak = T.kutu(0, 2.0, 0, 16, 4.0, 1.0)      # devrilmesin
    return p + (duvar + ayak).translate([0, 16 + 3.0, 0])


def ekran_testi(panel_m):
    """panelin ekran bölgesi: pencere, cam cebi, pin yarığı ve vida direkleri"""
    p = T.panel_merkez()
    yerel = panel_m.translate(list(-p)).rotate([-T.TETA, 0, 0])
    kes = T.kutu(1.5, 0, -10, 46, 36, 11)
    return yerel ^ kes


if __name__ == '__main__':
    P = T.parcalar()
    liste = [
        ('01_taban_SEFFAF_PETG', tablaya(P['taban'])),
        ('02_govde', tablaya(P['cerceve'])),
        ('03_duz_kapak', tablaya(P['duz_kapak'].rotate([180, 0, 0]))),
        ('04_egik_panel', tablaya(panel_duz(P['panel']))),
        ('05_halka_difuzor_x6_SEFFAF', tablaya(P['halkalar'][0].rotate([180, 0, 0]))),
        ('06_dugme_x2', tablaya(K1.dugme().rotate([180, 0, 0]))),
        ('00_tolerans_testi', tablaya(test_parcasi())),
        ('00b_ekran_testi', tablaya(ekran_testi(P['panel']).rotate([180, 0, 0]))),
    ]
    for ad, m in liste:
        assert m.status().name == 'NoError', ad
        bb = m.bounding_box()
        print(f'{ad:30s} {bb[3]-bb[0]:6.1f} x {bb[4]-bb[1]:6.1f} x {bb[5]-bb[2]:5.1f} mm  parça={len(m.decompose())}  hacim={m.volume()/1000:.1f} cm3')
        stl_yaz(m, f'{out}/{ad}.stl')
