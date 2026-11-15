# Basit yazılım rasterleştirici: z-buffer, düz gölgeleme, ışıma (bloom), zemin gölgesi.
import numpy as np
from PIL import Image, ImageFilter, ImageDraw, ImageFont


def mesh_of(m):
    me = m.to_mesh()
    v = np.asarray(me.vert_properties)[:, :3].astype(np.float64)
    t = np.asarray(me.tri_verts).astype(np.int64)
    return v, t


def look_at(eye, target, up=(0, 0, 1)):
    eye, target, up = map(lambda a: np.asarray(a, float), (eye, target, up))
    f = target - eye; f /= np.linalg.norm(f)
    r = np.cross(f, up); r /= np.linalg.norm(r)
    u = np.cross(r, f)
    return eye, r, u, f


def render(parts, eye, target, W=1400, H=1000, ss=2, fov=28, bg=((236, 238, 242), (205, 209, 216)),
           light=(-0.45, -0.55, 0.75), floor_glow=None, shadow_poly=None, bloom=1.0, etiketler=None):
    """parts: [(manifold, (r,g,b) 0-1, emissive 0-1)]"""
    w, h = W * ss, H * ss
    eye, R, U, F = look_at(eye, target)
    fpx = (h / 2) / np.tan(np.radians(fov) / 2)
    L = np.asarray(light, float); L /= np.linalg.norm(L)
    Hh = L - F; Hh /= np.linalg.norm(Hh)

    def proj(v):
        d = v - eye
        x, y, z = d @ R, d @ U, d @ F
        return np.stack([w / 2 + fpx * x / z, h / 2 - fpx * y / z, z], axis=1)

    # arka plan dikey degrade
    g = np.linspace(0, 1, h)[:, None, None]
    img = (np.array(bg[0]) * (1 - g) + np.array(bg[1]) * g).repeat(w, axis=1) / 255.0

    # zemin gölgesi ve ışıması (z=0 düzleminde poligon)
    def floor_mask(poly):
        P = proj(np.c_[poly, np.zeros(len(poly))])
        mi = Image.new('L', (w, h), 0)
        ImageDraw.Draw(mi).polygon([tuple(p[:2]) for p in P], fill=255)
        return mi
    if shadow_poly is not None:
        m = floor_mask(shadow_poly).filter(ImageFilter.GaussianBlur(14 * ss))
        img *= (1 - 0.38 * np.asarray(m, float)[:, :, None] / 255)
    if floor_glow is not None:
        poly, col, rad, k = floor_glow
        m = floor_mask(poly).filter(ImageFilter.GaussianBlur(rad * ss))
        img += k * np.asarray(m, float)[:, :, None] / 255 * np.asarray(col)[None, None, :]

    zbuf = np.full((h, w), np.inf)
    emis = np.zeros((h, w, 3))
    for m, col, em in parts:
        v, t = mesh_of(m)
        P = proj(v)
        A, B, C = P[t[:, 0]], P[t[:, 1]], P[t[:, 2]]
        V0, V1, V2 = v[t[:, 0]], v[t[:, 1]], v[t[:, 2]]
        n = np.cross(V1 - V0, V2 - V0); n /= (np.linalg.norm(n, axis=1, keepdims=True) + 1e-12)
        # arka yüz eleme
        cen = (V0 + V1 + V2) / 3
        vis = np.einsum('ij,ij->i', n, cen - eye) < 0
        col = np.asarray(col, float)
        dif = np.clip(n @ L, 0, 1)
        fill = np.clip(n @ np.array([0.6, 0.7, 0.3]) * 0.5 + 0.5, 0, 1) * 0.18
        spec = np.clip(n @ Hh, 0, 1) ** 40 * 0.25
        shade = (0.30 + 0.62 * dif + fill)[:, None] * col[None, :] + spec[:, None]
        shade = shade * (1 - em) + col[None, :] * em
        for i in np.nonzero(vis)[0]:
            a, b, c = A[i], B[i], C[i]
            x0 = max(int(np.floor(min(a[0], b[0], c[0]))), 0); x1 = min(int(np.ceil(max(a[0], b[0], c[0]))), w - 1)
            y0 = max(int(np.floor(min(a[1], b[1], c[1]))), 0); y1 = min(int(np.ceil(max(a[1], b[1], c[1]))), h - 1)
            if x1 < x0 or y1 < y0:
                continue
            xs = np.arange(x0, x1 + 1) + 0.5; ys = np.arange(y0, y1 + 1) + 0.5
            X, Y = np.meshgrid(xs, ys)
            area = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
            if abs(area) < 1e-9:
                continue
            w0 = ((b[0] - X) * (c[1] - Y) - (b[1] - Y) * (c[0] - X)) / area
            w1 = ((c[0] - X) * (a[1] - Y) - (c[1] - Y) * (a[0] - X)) / area
            w2 = 1 - w0 - w1
            ins = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
            if not ins.any():
                continue
            iz = w0 / a[2] + w1 / b[2] + w2 / c[2]
            z = 1 / np.where(iz > 0, iz, 1e-9)
            sub = zbuf[y0:y1 + 1, x0:x1 + 1]
            upd = ins & (z < sub)
            sub[upd] = z[upd]
            img[y0:y1 + 1, x0:x1 + 1][upd] = shade[i]
            emis[y0:y1 + 1, x0:x1 + 1][upd] = col * em
    # ışıma
    if bloom and emis.any():
        e = Image.fromarray((np.clip(emis, 0, 1) * 255).astype(np.uint8))
        b1 = np.asarray(e.filter(ImageFilter.GaussianBlur(6 * ss)), float) / 255
        b2 = np.asarray(e.filter(ImageFilter.GaussianBlur(22 * ss)), float) / 255
        img = img + bloom * (0.9 * b1 + 0.9 * b2)
    img = np.clip(img, 0, 1)
    out = Image.fromarray((img * 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)
    if etiketler:
        d = ImageDraw.Draw(out)
        try:
            f = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 19)
            fb = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 19)
        except OSError:
            f = fb = ImageFont.load_default()
        for (pt, metin, (lx, ly)) in etiketler:
            q = proj(np.asarray([pt], float))[0][:2] / ss
            d.line([tuple(q), (lx, ly)], fill=(60, 66, 78), width=2)
            d.ellipse([q[0] - 4, q[1] - 4, q[0] + 4, q[1] + 4], fill=(60, 66, 78))
            satirlar = metin.split('\n')
            tw = max(d.textlength(t, font=fb) for t in satirlar)
            x = lx + 8 if lx >= q[0] else lx - 8 - tw
            for k, t in enumerate(satirlar):
                d.text((x, ly - 11 + k * 23), t, fill=(30, 34, 42) if k == 0 else (80, 86, 98), font=fb if k == 0 else f)
    return out


def etiket(img, baslik, alt):
    d = ImageDraw.Draw(img)
    try:
        f1 = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 40)
        f2 = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 22)
    except OSError:
        f1 = f2 = ImageFont.load_default()
    d.text((40, 30), baslik, fill=(30, 34, 42), font=f1)
    y = 84
    for satir in alt:
        d.text((42, y), satir, fill=(70, 76, 88), font=f2)
        y += 30
    return img
