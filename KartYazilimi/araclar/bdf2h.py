# BDF yazı tiplerini C dizisine çevirir (yalnızca ekranda kullanılan harfler)
import sys
TUM = "ÇçĞğİıÖöŞşÜüÂâÊêÎîÔôÛûÉéÈèËëÁáÀàÄäÍíÌìÏïÓóÒòÕõÚúÙùÑñßÅåØøÆæ…•–—‘’“”°«»×·€£¡¿"
ISTENEN = set(range(32, 127)) | {ord(c) for c in TUM}

def oku(yol):
    harfler = {}
    bbh = 0
    with open(yol, encoding='latin-1') as f:
        satirlar = f.read().split('\n')
    i = 0
    while i < len(satirlar):
        s = satirlar[i]
        if s.startswith('FONTBOUNDINGBOX'):
            bbh = int(s.split()[2])
        if s.startswith('STARTCHAR'):
            kod = w = h = xo = yo = dx = None
            bit = []
            i += 1
            while not satirlar[i].startswith('ENDCHAR'):
                t = satirlar[i]
                if t.startswith('ENCODING'): kod = int(t.split()[1])
                elif t.startswith('DWIDTH'): dx = int(t.split()[1])
                elif t.startswith('BBX'): w, h, xo, yo = map(int, t.split()[1:5])
                elif t.startswith('BITMAP'):
                    i += 1
                    while not satirlar[i].startswith('ENDCHAR'):
                        bit.append(satirlar[i].strip()); i += 1
                    break
                i += 1
            if kod in ISTENEN:
                nb = (w + 7) // 8
                # satır başına nb bayt (BDF satırı dolgulu olabilir)
                ham = []
                for hx in bit[:h]:
                    b = bytes.fromhex(hx if len(hx) % 2 == 0 else hx + '0')
                    ham.extend((b + bytes(nb))[:nb])
                harfler[kod] = (w, h, xo, yo, dx, ham)
        i += 1
    return harfler, bbh

def yaz(ad, yol, out):
    harfler, bbh = oku(yol)
    def uy(c):
        w, h, xo, yo, dx, _ = harfler[ord(c)]
        return h + yo, -yo
    asc = max(uy(c)[0] for c in "Mj'")
    desc = max(uy(c)[1] for c in "Mj'")
    bitler, kayit = [], []
    for kod in sorted(harfler):
        w, h, xo, yo, dx, ham = harfler[kod]
        kayit.append((kod, w, h, xo, yo, dx, len(bitler)))
        bitler.extend(ham)
    out.write(f"static const uint8_t {ad}_bit[] = {{\n")
    for k in range(0, len(bitler), 24):
        out.write("  " + ",".join(str(b) for b in bitler[k:k + 24]) + ",\n")
    out.write("};\n")
    out.write(f"static const Harf {ad}_harf[] = {{\n")
    for r in kayit:
        out.write("  {%d,%d,%d,%d,%d,%d,%d},\n" % r)
    out.write("};\n")
    out.write(f"const YaziTipi {ad} = {{{ad}_harf, {len(kayit)}, {ad}_bit, {asc}, {desc}, {bbh}}};\n\n")
    return len(kayit), len(bitler)

with open(sys.argv[2], 'w', encoding='utf-8') as out:
    out.write("// fonts/*.bdf dosyalarından üretildi (araclar/bdf2h.py) - elle değiştirme\n#include \"yazi.h\"\n\n")
    for ad in ("baslik", "orta", "kucuk"):
        n, b = yaz("YT_" + ad, f"{sys.argv[1]}/{ad}.bdf", out)
        print(ad, n, "harf", b, "bayt")
