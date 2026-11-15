# 24x24 tuş simgeleri: 96x96 çizilir, 24x24'e indirilir, C dizisi olarak dışa verilir
from PIL import Image, ImageDraw
import math

B = 96  # çizim boyutu
K = 10  # çizgi kalınlığı

def yeni():
    im = Image.new('L', (B, B), 0)
    return im, ImageDraw.Draw(im)

def cizgi(d, p1, p2, k=K):
    d.line([p1, p2], fill=255, width=k)
    for p in (p1, p2):
        d.ellipse([p[0]-k/2, p[1]-k/2, p[0]+k/2, p[1]+k/2], fill=255)

S = {}

# oynat: üçgen
im, d = yeni(); d.polygon([(28,16),(28,80),(80,48)], fill=255); S['oynat'] = im
# duraklat
im, d = yeni(); d.rectangle([24,16,40,80], fill=255); d.rectangle([56,16,72,80], fill=255); S['duraklat'] = im
# dur
im, d = yeni(); d.rectangle([22,22,74,74], fill=255); S['dur'] = im
# onceki / sonraki
im, d = yeni(); d.polygon([(78,16),(78,80),(38,48)], fill=255); d.rectangle([18,16,30,80], fill=255); S['onceki'] = im
im, d = yeni(); d.polygon([(18,16),(18,80),(58,48)], fill=255); d.rectangle([66,16,78,80], fill=255); S['sonraki'] = im
# ses: hoparlör + dalgalar
def hoparlor(d):
    d.polygon([(12,36),(30,36),(50,18),(50,78),(30,60),(12,60)], fill=255)
im, d = yeni(); hoparlor(d)
d.arc([40,20,88,76], -50, 50, fill=255, width=10); d.arc([34,34,64,62], -50, 50, fill=255, width=10)
S['ses'] = im
# sessiz: hoparlör + çarpı
im, d = yeni(); hoparlor(d); cizgi(d,(58,36),(82,60),8); cizgi(d,(82,36),(58,60),8); S['sessiz'] = im
# mikrofon
im, d = yeni()
d.rounded_rectangle([36,10,60,52], radius=12, fill=255)
d.arc([24,30,72,66], 0, 180, fill=255, width=8)
d.rectangle([44,64,52,76], fill=255); d.rectangle([32,76,64,84], fill=255)
S['mikrofon'] = im
# kamera
im, d = yeni(); d.rounded_rectangle([10,28,58,68], radius=8, fill=255); d.polygon([(62,40),(86,26),(86,70),(62,56)], fill=255); S['kamera'] = im
# kopyala
im, d = yeni(); d.rectangle([14,14,58,62], outline=255, width=8); d.rectangle([36,34,82,84], fill=255); S['kopyala'] = im
# yapistir: pano
im, d = yeni(); d.rounded_rectangle([18,16,78,86], radius=6, outline=255, width=8); d.rectangle([34,8,62,24], fill=255); cizgi(d,(32,42),(64,42),6); cizgi(d,(32,56),(64,56),6); cizgi(d,(32,70),(54,70),6); S['yapistir'] = im
# kes: makas
im, d = yeni()
d.ellipse([10,58,34,82], outline=255, width=8); d.ellipse([62,58,86,82], outline=255, width=8)
cizgi(d,(26,62),(66,14),8); cizgi(d,(70,62),(30,14),8)
S['kes'] = im
# geri: geri al oku
im, d = yeni()
d.arc([22,22,82,82], 180, 450, fill=255, width=11)
d.polygon([(6,50),(40,50),(23,28)], fill=255)
S['geri'] = im
# kaydet: disket
im, d = yeni(); d.polygon([(14,14),(70,14),(84,28),(84,84),(14,84)], fill=255)
d.rectangle([30,14,64,36], fill=0); d.rectangle([48,18,58,32], fill=255)
d.rectangle([26,52,72,84], fill=0); d.rectangle([32,58,66,84], fill=255)
S['kaydet'] = im
# ara: büyüteç
im, d = yeni(); d.ellipse([14,14,62,62], outline=255, width=9); cizgi(d,(56,56),(82,82),10); S['ara'] = im
# ekran: monitör
im, d = yeni(); d.rounded_rectangle([10,16,86,64], radius=6, outline=255, width=8); d.rectangle([42,66,54,76], fill=255); d.rectangle([28,76,68,84], fill=255); S['ekran'] = im
# kilit
im, d = yeni(); d.arc([28,10,68,50], 180, 360, fill=255, width=8); d.rectangle([28,26,36,42], fill=255); d.rectangle([60,26,68,42], fill=255); d.rounded_rectangle([18,42,78,86], radius=6, fill=255); d.rectangle([44,56,52,72], fill=0); S['kilit'] = im
# klasor
im, d = yeni(); d.polygon([(10,22),(38,22),(48,32),(86,32),(86,78),(10,78)], fill=255); S['klasor'] = im
# dunya
im, d = yeni(); d.ellipse([12,12,84,84], outline=255, width=7)
d.ellipse([34,12,62,84], outline=255, width=6); cizgi(d,(14,48),(82,48),6); d.arc([20,-20,76,40], 35, 145, fill=255, width=6); d.arc([20,56,76,116], 215, 325, fill=255, width=6)
S['dunya'] = im
# ev
im, d = yeni(); d.polygon([(48,10),(88,44),(78,44),(78,84),(18,84),(18,44),(8,44)], fill=255); d.rectangle([40,56,56,84], fill=0); S['ev'] = im
# yildiz
im, d = yeni()
pts=[]
for i in range(10):
    a = -math.pi/2 + i*math.pi/5
    r = 42 if i%2==0 else 18
    pts.append((48+r*math.cos(a), 50+r*math.sin(a)))
d.polygon(pts, fill=255); S['yildiz'] = im
# kalp
im, d = yeni(); d.ellipse([12,16,50,54], fill=255); d.ellipse([46,16,84,54], fill=255); d.polygon([(14,44),(82,44),(48,86)], fill=255); S['kalp'] = im
# simsek
im, d = yeni(); d.polygon([(56,6),(24,54),(44,54),(38,90),(74,40),(52,40)], fill=255); S['simsek'] = im
# zaman: saat
im, d = yeni(); d.ellipse([12,12,84,84], outline=255, width=8); cizgi(d,(48,30),(48,50),7); cizgi(d,(48,50),(64,60),7); S['zaman'] = im
# guc
im, d = yeni(); d.arc([16,20,80,84], -60, 240, fill=255, width=9); d.rectangle([43,8,53,44], fill=255); S['guc'] = im
# muzik: nota
im, d = yeni(); d.ellipse([18,62,44,84], fill=255); d.ellipse([56,54,82,76], fill=255); d.rectangle([36,16,44,72], fill=255); d.rectangle([74,10,82,64], fill=255); d.polygon([(36,16),(82,10),(82,26),(36,32)], fill=255); S['muzik'] = im
# ayar: dişli
im, d = yeni()
for i in range(8):
    a = i*math.pi/4
    x, y = 48+30*math.cos(a), 48+30*math.sin(a)
    d.rounded_rectangle([x-9,y-9,x+9,y+9], radius=4, fill=255)
d.ellipse([22,22,74,74], fill=255); d.ellipse([38,38,58,58], fill=0)
S['ayar'] = im
# posta
im, d = yeni(); d.rectangle([10,22,86,74], outline=255, width=8); cizgi(d,(14,28),(48,54),8); cizgi(d,(82,28),(48,54),8); S['posta'] = im
# terminal
im, d = yeni(); d.rounded_rectangle([8,16,88,80], radius=6, outline=255, width=7); cizgi(d,(22,36),(36,48),7); cizgi(d,(36,48),(22,60),7); cizgi(d,(46,62),(66,62),7); S['terminal'] = im
# parlaklik: güneş
im, d = yeni(); d.ellipse([32,32,64,64], fill=255)
for i in range(8):
    a=i*math.pi/4
    cizgi(d,(48+24*math.cos(a),48+24*math.sin(a)),(48+38*math.cos(a),48+38*math.sin(a)),7)
S['parlaklik'] = im


# ---- 2.7b: ek simgeler
# ileri / geri sar: çift üçgen
im, d = yeni(); d.polygon([(10,18),(10,78),(46,48)], fill=255); d.polygon([(46,18),(46,78),(82,48)], fill=255); S['ileri'] = im
im, d = yeni(); d.polygon([(86,18),(86,78),(50,48)], fill=255); d.polygon([(50,18),(50,78),(14,48)], fill=255); S['geri_sar'] = im
# karıştır: çapraz oklar
im, d = yeni(); cizgi(d,(10,28),(34,28),8); cizgi(d,(34,28),(62,68),8); cizgi(d,(62,68),(74,68),8); cizgi(d,(10,68),(34,68),8); cizgi(d,(34,68),(62,28),8); cizgi(d,(62,28),(74,28),8)
d.polygon([(72,14),(72,42),(90,28)], fill=255); d.polygon([(72,54),(72,82),(90,68)], fill=255); S['karistir'] = im
# tekrar: döngü oku
im, d = yeni(); d.arc([14,18,82,78], 200, 520, fill=255, width=9); d.polygon([(60,6),(60,34),(84,20)], fill=255); S['tekrar'] = im
# ses artır / azalt: hoparlör + işaret
im, d = yeni(); d.polygon([(8,36),(24,36),(42,18),(42,78),(24,60),(8,60)], fill=255); cizgi(d,(54,48),(86,48),8); cizgi(d,(70,32),(70,64),8); S['ses_artir'] = im
im, d = yeni(); d.polygon([(8,36),(24,36),(42,18),(42,78),(24,60),(8,60)], fill=255); cizgi(d,(54,48),(86,48),8); S['ses_azalt'] = im
# kulaklık
im, d = yeni(); d.arc([14,10,82,78], 180, 360, fill=255, width=9); d.rounded_rectangle([8,48,30,84], radius=6, fill=255); d.rounded_rectangle([66,48,88,84], radius=6, fill=255); S['kulaklik'] = im
# ay
im, d = yeni(); d.ellipse([14,14,82,82], fill=255); d.ellipse([34,6,98,70], fill=0); S['ay'] = im
# bulut
im, d = yeni(); d.ellipse([10,44,42,76], fill=255); d.ellipse([28,22,68,62], fill=255); d.ellipse([52,36,86,76], fill=255); d.rectangle([26,50,70,76], fill=255); S['bulut'] = im
# wifi
im, d = yeni(); d.arc([8,14,88,94], 225, 315, fill=255, width=9); d.arc([22,30,74,82], 225, 315, fill=255, width=9); d.arc([36,46,60,70], 225, 315, fill=255, width=9); d.ellipse([41,66,55,80], fill=255); S['wifi'] = im
# pil
im, d = yeni(); d.rounded_rectangle([8,28,80,70], radius=7, outline=255, width=8); d.rectangle([82,40,90,58], fill=255); d.rectangle([18,38,50,60], fill=255); S['pil'] = im
# oyun kolu
im, d = yeni(); d.rounded_rectangle([6,28,90,72], radius=18, fill=255); cizgi(d,(26,40),(26,60),6); cizgi(d,(16,50),(36,50),6)
d.ellipse([58,36,68,46], fill=0); d.ellipse([70,50,80,60], fill=0); S['oyun'] = im
# kod </>
im, d = yeni(); cizgi(d,(34,28),(14,48),9); cizgi(d,(14,48),(34,68),9); cizgi(d,(62,28),(82,48),9); cizgi(d,(82,48),(62,68),9); cizgi(d,(54,20),(42,76),8); S['kod'] = im
# sohbet
im, d = yeni(); d.rounded_rectangle([8,14,88,64], radius=10, fill=255); d.polygon([(24,60),(24,86),(50,60)], fill=255); cizgi(d,(26,32),(70,32),6); cizgi(d,(26,46),(56,46),6)
d.line([(26,32),(70,32)], fill=0, width=6); d.line([(26,46),(56,46)], fill=0, width=6); S['sohbet'] = im
# takvim
im, d = yeni(); d.rounded_rectangle([10,18,86,86], radius=8, outline=255, width=8); d.rectangle([10,18,86,38], fill=255); d.rectangle([28,8,36,26], fill=255); d.rectangle([60,8,68,26], fill=255)
d.rectangle([24,50,38,62], fill=255); d.rectangle([44,50,58,62], fill=255); d.rectangle([64,50,76,62], fill=255); d.rectangle([24,68,38,78], fill=255); d.rectangle([44,68,58,78], fill=255); S['takvim'] = im
# kullanıcı
im, d = yeni(); d.ellipse([30,10,66,46], fill=255); d.pieslice([12,52,84,124], 180, 360, fill=255); S['kullanici'] = im
# konum
im, d = yeni(); d.ellipse([20,8,76,64], fill=255); d.polygon([(26,52),(70,52),(48,90)], fill=255); d.ellipse([38,26,58,46], fill=0); S['konum'] = im
# zil
im, d = yeni(); d.pieslice([20,10,76,66], 180, 360, fill=255); d.polygon([(20,38),(76,38),(86,72),(10,72)], fill=255); d.ellipse([38,70,58,88], fill=255); S['zil'] = im
# kalem
im, d = yeni(); d.polygon([(66,10),(86,30),(36,80),(12,84),(16,60)], fill=255); d.line([(58,18),(78,38)], fill=0, width=5); S['kalem'] = im
# dosya
im, d = yeni(); d.polygon([(18,8),(56,8),(80,32),(80,88),(18,88)], fill=255); d.polygon([(52,10),(52,36),(78,36)], fill=0)
cizgi(d,(30,52),(66,52),6); cizgi(d,(30,66),(66,66),6); d.line([(30,52),(66,52)], fill=0, width=6); d.line([(30,66),(66,66),], fill=0, width=6); S['dosya'] = im
# çöp
im, d = yeni(); d.rectangle([10,22,86,32], fill=255); d.rectangle([34,10,62,24], fill=255); d.polygon([(18,38),(78,38),(72,88),(24,88)], fill=255)
d.line([(38,48),(38,80)], fill=0, width=6); d.line([(58,48),(58,80)], fill=0, width=6); S['cop'] = im
# artı
im, d = yeni(); d.rectangle([40,12,56,84], fill=255); d.rectangle([12,40,84,56], fill=255); S['arti'] = im
# tamam
im, d = yeni(); cizgi(d,(14,52),(38,74),13); cizgi(d,(38,74),(84,22),13); S['tamam'] = im
# kapat (X)
im, d = yeni(); cizgi(d,(20,20),(76,76),13); cizgi(d,(76,20),(20,76),13); S['kapat'] = im
# yenile
im, d = yeni(); d.arc([14,14,82,82], 30, 320, fill=255, width=10); d.polygon([(60,4),(92,36),(56,40)], fill=255); S['yenile'] = im
# indir / yükle
im, d = yeni(); d.rectangle([40,8,56,48], fill=255); d.polygon([(20,44),(76,44),(48,76)], fill=255); d.rectangle([14,80,82,88], fill=255); S['indir'] = im
im, d = yeni(); d.rectangle([40,40,56,80], fill=255); d.polygon([(20,44),(76,44),(48,12)], fill=255); d.rectangle([14,84,82,92], fill=255); d.rectangle([14,84,82,90], fill=255); S['yukle'] = im
# bağlantı (zincir)
im, d = yeni(); d.rounded_rectangle([6,32,54,64], radius=16, outline=255, width=9); d.rounded_rectangle([42,32,90,64], radius=16, outline=255, width=9); cizgi(d,(36,48),(60,48),8); S['baglanti'] = im
# pencere (Windows)
im, d = yeni(); d.polygon([(10,16),(44,12),(44,44),(10,44)], fill=255); d.polygon([(50,11),(88,6),(88,44),(50,44)], fill=255); d.polygon([(10,52),(44,52),(44,84),(10,80)], fill=255); d.polygon([(50,52),(88,52),(88,90),(50,85)], fill=255); S['pencere'] = im
# bilgi
im, d = yeni(); d.ellipse([10,10,86,86], outline=255, width=8); d.ellipse([42,24,54,36], fill=255); d.rectangle([42,42,54,70], fill=255); S['bilgi'] = im
# uyarı
im, d = yeni(); d.polygon([(48,8),(92,84),(4,84)], fill=255); d.rectangle([43,34,53,58], fill=0); d.ellipse([42,64,54,76], fill=0); S['uyari'] = im
# yazıcı
im, d = yeni(); d.rectangle([24,8,72,32], outline=255, width=7); d.rounded_rectangle([8,30,88,70], radius=8, fill=255); d.rectangle([24,56,72,88], fill=0); d.rectangle([24,56,72,88], outline=255, width=7); d.rectangle([32,66,64,70], fill=255); S['yazici'] = im


# ---- düzeltmeler
im, d = yeni(); d.pieslice([-10,22,106,138], 225, 315, fill=255); d.pieslice([14,46,82,114], 225, 315, fill=0)
d.pieslice([22,54,74,106], 225, 315, fill=255); d.pieslice([36,68,60,92], 225, 315, fill=0)
d.ellipse([40,66,56,82], fill=255); S['wifi'] = im
im, d = yeni(); d.rounded_rectangle([6,26,90,72], radius=20, fill=255)
d.rectangle([24,38,32,62], fill=0); d.rectangle([16,46,40,54], fill=0)
d.ellipse([58,36,70,48], fill=0); d.ellipse([70,50,82,62], fill=0); S['oyun'] = im
im, d = yeni(); d.rectangle([22,8,74,34], fill=255); d.rectangle([32,16,64,34], fill=0); d.rounded_rectangle([6,32,90,72], radius=8, fill=255)
d.rectangle([22,58,74,90], fill=255); d.rectangle([30,66,66,72], fill=0); d.rectangle([30,78,56,84], fill=0); S['yazici'] = im
im, d = yeni(); d.arc([14,20,82,84], 210, 500, fill=255, width=10); d.polygon([(52,2),(52,36),(80,19)], fill=255); S['tekrar'] = im
ADLAR = list(S.keys())
print(len(ADLAR), 'simge:', ' '.join(ADLAR))

# 24x24'e indir ve eşikle
KUCUK = {}
for ad, im in S.items():
    k = im.resize((24,24), Image.LANCZOS).point(lambda v: 255 if v >= 110 else 0)
    KUCUK[ad] = k

# kontrol görseli
G = Image.new('L', (8*40+8, ((len(ADLAR)+7)//8)*40+8), 30)
from PIL import ImageOps
for i, ad in enumerate(ADLAR):
    G.paste(KUCUK[ad].resize((32,32), Image.NEAREST), ((i%8)*40+4, (i//8)*40+4))
G.save('simgeler_kontrol.png')

# C dışa verme
with open('../MakroKlavye/simgeler.cpp','w') as f:
    f.write('// araclar/simgeler.py ile üretildi - elle değiştirme\n#include "simgeler.h"\n#include <string.h>\n\n')
    for ad in ADLAR:
        k = KUCUK[ad]
        satirlar = []
        for y in range(24):
            v = 0
            for x in range(24):
                if k.getpixel((x,y)): v |= 1 << (23-x)
            satirlar.append(v)
        f.write('static const uint32_t s_%s[24] = {%s};\n' % (ad, ','.join('0x%06X'%v for v in satirlar)))
    f.write('\nconst Simge SIMGELER[] = {\n')
    for ad in ADLAR:
        f.write('  {"%s", s_%s},\n' % (ad, ad))
    f.write('};\nconst int SIMGE_SAYISI = %d;\n\nint simge_bul(const char *ad) {\n  if (!ad || !*ad) return -1;\n  for (int i = 0; i < SIMGE_SAYISI; i++)\n    if (!strcmp(ad, SIMGELER[i].ad)) return i;\n  return -1;\n}\n' % len(ADLAR))
print('yazıldı')
