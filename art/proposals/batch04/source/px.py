"""Deterministic pixel-art engine for the StarPawn batch 04 proposals.

Everything is drawn into an indexed canvas (material, tone, part) and mapped to
fixed six-tone ramps, following the approved gold-ring sample: upper-left
light, hard pixel clusters, no anti-aliasing, no partial alpha, no random
numbers. Paper art is authored at 192x128 and exported 2x; inventory art is
authored separately at its native size (lod 0) with thicker strokes.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

INK = (26, 32, 48)


def _ramp(s):
    return [INK] + [tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) for h in s.split()]


PAL = {k: _ramp(v) for k, v in {
    "gold": "4a2f1c 7d5320 b07d28 d9ac3c f0d568 fbf1b4",
    "silver": "2e3440 566070 8794a0 b4bec6 d8e0e2 f4f8f6",
    "base": "27353a 35474b 43585a 6b8580 86a096 9db3a6",
    "copper": "4a2418 7a3b22 a8562e cc7a44 e6a06a f6d0a0",
    "solder": "3a3c40 5c6064 7e8284 9a9e9c b4b8b2 d0d2c8",
    "dou": "1f3a2c 2f5a3c 4a7e4e 74a264 a0c084 cfe0b0",
    "nuo": "1d4038 2c6650 44906a 6eb88a a2d8b0 d8f2d8",
    "bing": "24484c 3c7474 62a49c 94ccc0 c4e8dc f0fcf4",
    "skin_yellow": "4a3a1e 76602c a48a44 c8ae68 e0cc90 f0e4b8",
    "skin_black": "14161c 22262e 343a42 4a525a 687078 8c949a",
    "skin_water": "2c342c 465044 66725e 8a947c acb49a ccd2b8",
    "skin_white": "5a5a54 84847a a8a89c c4c4b8 dcdcd0 f0f0e6",
    "skin_rust": "3a1e16 62301e 8c4a28 b06a3a cc8e58 e0b488",
    "skin_mountain": "2a2e30 444a4a 626a66 828a82 a2aa9e c2c8ba",
    "diamond": "3a5068 6888a4 9cbcd0 c8e0ec e8f4f8 ffffff",
    "ruby": "3c0a1c 6e1028 a41c34 d03a48 ec7074 fcc0bc",
    "sapphire": "101c4c 1c3480 2c54b0 4c80d4 84b0ec c8e0fc",
    "emerald": "06302a 0c5440 1a7e58 3ca874 7cd09c c4f0cc",
    "pearl": "6a5a5c 9a8a88 c4b6b0 e0d6cc f2ece2 fffcf6",
    "paper": "6e6250 a09276 c8b894 decfb0 ece2c8 f8f2e0",
    "sheet": "5e5e52 8a8a78 a8a892 c5c4b0 d6d5c2 e6e5d4",
    "inkblue": "121e2c 1c3044 28465a 3e6690 6a90b4 a0c0d8",
    "dark": "18161e 26222e 36323e 4a4654 625e6c 827e8a",
    "plastic": "4a4638 6e6854 928c72 b4ae92 cec8ae e4e0cc",
    "screen": "0a2428 104044 1a6664 2c9488 5cc4ac a8ecd4",
    "glass": "1c2c38 2c4858 4a7084 7498a8 a8c4cc dceaea",
    "wood": "2e1c14 4c2e1c 6e4628 8e6238 ac8250 c8a474",
    "leather": "241a1c 3c2826 583a30 74503c 906a4c ac8a66",
    "red": "3c1414 641c1c 8e2c26 a64741 c86c5c e4a090",
    "teal": "12302c 1e4c44 2e6c5e 4f8a7a 78ac98 a8ccb8",
    "ceramic": "3c4c58 5c7484 849ca4 a8c0c0 ccdcd4 ecf4ea",
    "pcb": "0e2a1c 16402a 22583a 34744c 549464 80b488",
    "rock": "26262a 3e3e42 5a5a5c 78787a 989894 b8b8b0",
    "quartz": "5c5850 86827a aaa69c c8c4ba e0dcd2 f4f2ea",
    "slag": "0e0e12 1a1a20 2a2a30 3c3c42 505056 6a6a70",
    "brown": "2a1a10 44291a 5e3c24 7a5230 96704a b4946e",
    "dye": "043c1c 066428 0a8c34 1cb444 54d86c a4f4a4",
    "malachite": "0c3a34 14584c 207c64 34a07c 64c49c a4e4c0",
    "uvdark": "140c2c 20143c 2e1e54 40306c 584888 7868a8",
    "uvglow": "1c6c6c 28a090 3cd0a8 70f0bc b0fcd4 e8fff0",
    "litdark": "1a1208 2e2010 46321a 624826 80623a a08456",
    "hot": "5c1408 962408 cc4410 ec7c1c fcbc48 fff0b0",
    "label": "5a5a52 8c8a7c b8b4a2 d8d4c0 ece8d8 fbf8ee",
    "vio": "241a34 3a2a50 54406e 705c8c 9482ac bcaed0",
}.items()}
MATS = [None] + list(PAL)
MID = {m: i for i, m in enumerate(MATS)}
LUT = np.zeros((len(MATS), 7, 4), np.uint8)
for _i, _m in enumerate(MATS[1:], 1):
    for _t in range(7):
        LUT[_i, _t] = PAL[_m][_t] + (255,)

LIGHT = np.array([-0.55, -0.65, 0.52])
LIGHT = LIGHT / np.linalg.norm(LIGHT)


def shade(nx, ny, nz=None, kind="metal"):
    if nz is None:
        nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0, 1))
    i = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]
    if kind == "metal":
        t = 1 + np.digitize(i, [-0.3, 0.08, 0.42, 0.7, 0.93])
        t = np.where(i < -0.72, 2, t)
    elif kind == "gloss":
        t = 1 + np.digitize(i, [-0.3, 0.1, 0.45, 0.78, 0.965])
    else:
        t = 1 + np.digitize(i, [-0.3, 0.1, 0.45, 0.8])
    return t


class Canvas:
    def __init__(self, w, h, hw=1.0, hh=1.0, lod=1, margin=5, oy=0.0):
        self.w, self.h, self.lod = w, h, lod
        self.u = min((w - 2 * margin) / (2 * hw), (h - 2 * margin) / (2 * hh))
        self.cx, self.cy = w / 2.0, h / 2.0 + oy
        ys, xs = np.mgrid[0:h, 0:w]
        self.gx = (xs + 0.5 - self.cx) / self.u
        self.gy = (ys + 0.5 - self.cy) / self.u
        self.mat = np.zeros((h, w), np.uint8)
        self.tone = np.zeros((h, w), np.int8)
        self.part = np.zeros((h, w), np.uint8)
        self.edge = np.zeros((h, w), bool)
        self.parts = {}
        self.anchors = {}
        self.sockets = {}

    # ---- helpers
    def px(self, n):
        """n pixels expressed in design units."""
        return n / self.u

    def w_(self, wid, minpx):
        return max(wid, minpx / self.u)

    def P(self, x, y):
        return (x * self.u + self.cx, y * self.u + self.cy)

    def pid(self, name):
        if name is None:
            return 0
        if name not in self.parts:
            self.parts[name] = len(self.parts) + 1
        return self.parts[name]

    def sel(self, *names):
        m = np.zeros((self.h, self.w), bool)
        for n in names:
            if n in self.parts:
                m |= self.part == self.parts[n]
        return m

    @property
    def solid(self):
        return self.mat > 0

    @property
    def inner(self):
        return self.solid & ~self.edge

    def anchor(self, name, x, y, part=None, ang=0.0):
        self.anchors[name] = (x, y, part, ang)

    def put(self, mask, mat, tone, part=None, over=True):
        if not over:
            mask = mask & ~self.solid
        if mat is not None:
            self.mat[mask] = MID[mat]
        if isinstance(tone, np.ndarray):
            self.tone[mask] = np.clip(tone, 1, 6)[mask]
        elif tone is not None:
            self.tone[mask] = tone
        if part is not None:
            self.part[mask] = self.pid(part)
        self.edge[mask] = False
        return mask

    def erase(self, mask):
        self.mat[mask] = 0
        self.tone[mask] = 0
        self.part[mask] = 0
        self.edge[mask] = False

    def add(self, mask, d, lo=1, hi=6):
        m = mask & self.solid & (self.tone > 0)
        self.tone[m] = np.clip(self.tone[m] + d, lo, hi)

    # ---- primitives (design coordinates)
    def ell(self, cx, cy, rx, ry, mat, kind="metal", part=None, flat=None, over=True, rot=0.0):
        dx, dy = self.gx - cx, self.gy - cy
        if rot:
            ca, sa = math.cos(rot), math.sin(rot)
            dx, dy = dx * ca + dy * sa, -dx * sa + dy * ca
        nx, ny = dx / rx, dy / ry
        m = nx * nx + ny * ny <= 1.0
        if flat is not None:
            t = flat
        else:
            if rot:
                ca, sa = math.cos(rot), math.sin(rot)
                nx, ny = nx * ca - ny * sa, nx * sa + ny * ca
            t = shade(nx, ny, None, kind)
        return self.put(m, mat, t, part, over)

    def box(self, x0, y0, x1, y1, mat, tone=4, part=None, bevel=True, over=True, r=0.0):
        gx, gy = self.gx, self.gy
        m = (gx >= x0) & (gx < x1) & (gy >= y0) & (gy < y1)
        if r:
            qx = np.maximum(np.maximum(x0 + r - gx, gx - (x1 - r)), 0)
            qy = np.maximum(np.maximum(y0 + r - gy, gy - (y1 - r)), 0)
            m &= qx * qx + qy * qy <= r * r
        t = np.full((self.h, self.w), tone, np.int8)
        if bevel:
            p = self.px(1.0)
            t = np.where((gy < y0 + p) | (gx < x0 + p), tone + 1, t)
            t = np.where((gy >= y1 - p) | (gx >= x1 - p), tone - 1, t)
        return self.put(m, mat, t, part, over)

    def polymask(self, pts):
        im = Image.new("1", (self.w, self.h), 0)
        ImageDraw.Draw(im).polygon([self.P(x, y) for x, y in pts], fill=1)
        return np.array(im, bool)

    def poly(self, pts, mat, tone=4, part=None, over=True):
        return self.put(self.polymask(pts), mat, tone, part, over)

    def frame(self, u, cx, cy):
        self.u, self.cx, self.cy = u, cx, cy
        ys, xs = np.mgrid[0:self.h, 0:self.w]
        self.gx = (xs + 0.5 - cx) / u
        self.gy = (ys + 0.5 - cy) / u

    def tube(self, pts, wid, mat, kind="metal", part=None, over=True, minpx=2.0, flat=None):
        r = self.w_(wid, minpx) / 2.0
        xsr = [self.P(x, y)[0] for x, y in pts]
        ysr = [self.P(x, y)[1] for x, y in pts]
        pad = r * self.u + 2
        X0, X1 = max(0, int(min(xsr) - pad)), min(self.w, int(max(xsr) + pad) + 1)
        Y0, Y1 = max(0, int(min(ysr) - pad)), min(self.h, int(max(ysr) + pad) + 1)
        if X0 >= X1 or Y0 >= Y1:
            return np.zeros((self.h, self.w), bool)
        gx, gy = self.gx[Y0:Y1, X0:X1], self.gy[Y0:Y1, X0:X1]
        bd = np.full(gx.shape, 9e9)
        ox = np.zeros(gx.shape)
        oy = np.zeros(gx.shape)
        for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
            vx, vy = x1 - x0, y1 - y0
            L2 = vx * vx + vy * vy or 1e-9
            t = np.clip(((gx - x0) * vx + (gy - y0) * vy) / L2, 0, 1)
            dx, dy = gx - (x0 + t * vx), gy - (y0 + t * vy)
            d = dx * dx + dy * dy
            b = d < bd
            bd = np.where(b, d, bd)
            ox = np.where(b, dx, ox)
            oy = np.where(b, dy, oy)
        m = np.zeros((self.h, self.w), bool)
        m[Y0:Y1, X0:X1] = bd <= r * r
        if flat is not None:
            t = flat
        else:
            t = np.zeros((self.h, self.w), np.int64)
            t[Y0:Y1, X0:X1] = shade(ox / r, oy / r, None, kind)
        return self.put(m, mat, t, part, over)

    def lathe(self, cx, prof, mat, kind="metal", part=None, over=True):
        """Solid of revolution seen side-on. prof = [(y, radius), ...] top to bottom."""
        ys = np.array([p[0] for p in prof])
        rs = np.array([p[1] for p in prof])
        r = np.interp(self.gy, ys, rs, left=0, right=0)
        slope = np.interp(self.gy, ys, np.gradient(rs, ys), left=0, right=0)
        nx = (self.gx - cx) / np.maximum(r, 1e-6)
        m = (np.abs(nx) <= 1.0) & (self.gy >= ys[0]) & (self.gy <= ys[-1]) & (r > 0)
        ny = np.clip(-slope * 0.5, -0.6, 0.6) * np.sqrt(np.clip(1 - nx * nx, 0, 1))
        nxs = nx * np.sqrt(1 - np.clip(ny, -0.6, 0.6) ** 2)
        return self.put(m, mat, shade(nxs, ny, None, kind), part, over)

    def arc(self, cx, cy, rx, ry, a0, a1, wid, mat, kind="metal", part=None, over=True, minpx=2.0,
            rot=0.0, flat=None):
        n = max(8, int(abs(a1 - a0) / 10))
        pts = []
        for i in range(n + 1):
            a = math.radians(a0 + (a1 - a0) * i / n)
            x, y = rx * math.cos(a), ry * math.sin(a)
            if rot:
                x, y = x * math.cos(rot) - y * math.sin(rot), x * math.sin(rot) + y * math.cos(rot)
            pts.append((cx + x, cy + y))
        return self.tube(pts, wid, mat, kind, part, over, minpx, flat)

    def seg(self, x0, y0, x1, y1, mat=None, tone=0, inside=True, wpx=1, part=None):
        """Pixel line. With inside=True only existing object pixels change."""
        im = Image.new("1", (self.w, self.h), 0)
        ImageDraw.Draw(im).line([self.P(x0, y0), self.P(x1, y1)], fill=1, width=wpx)
        m = np.array(im, bool)
        if inside:
            m &= self.solid
        if tone is None:
            return m
        if isinstance(tone, str):                       # relative, e.g. "-2"
            self.add(m, int(tone))
            if mat:
                self.mat[m] = MID[mat]
        else:
            self.put(m, mat, tone, part)
        return m

    def dots(self, pts, mat=None, tone=0, inside=True):
        for x, y in pts:
            ix, iy = int(math.floor(x * self.u + self.cx)), int(math.floor(y * self.u + self.cy))
            if 0 <= ix < self.w and 0 <= iy < self.h and (self.mat[iy, ix] or not inside):
                if mat:
                    self.mat[iy, ix] = MID[mat]
                if isinstance(tone, str):
                    self.tone[iy, ix] = min(6, max(1, self.tone[iy, ix] + int(tone)))
                else:
                    self.tone[iy, ix] = tone

    def band(self, cx, cy, R, r, k, d, mat, wall_bands, inner_bands, groove=None, prefix=""):
        """Tilted cylindrical band: the approved ring construction."""
        gx, gy = self.gx - cx, self.gy - cy

        def ins(rad, oy):
            return (gx / rad) ** 2 + ((gy - oy) / (rad * k)) ** 2 <= 1.0
        to, ti, bo, bi = ins(R, 0), ins(r, 0), ins(R, d), ins(r, d)
        solid = (to | bo | ((np.abs(gx) <= R) & (gy >= 0) & (gy <= d))) & ~(ti & bi)
        p = self.px(1.0)
        # top face
        a = np.degrees(np.arctan2(gy / k, gx))
        rr = np.hypot(gx, gy / k)
        t = np.full(gx.shape, 4)
        t = np.where((a >= -165) & (a <= -100), 5, t)
        t = np.where((a >= -25) & (a <= 75), 3, t)
        t = np.where((rr > R - 1.2 * p) & (a >= 80) & (a <= 170), 5, t)
        t = np.where((rr < r + 1.2 * p) & ((a >= 150) | (a <= -150)), 6, t)
        top = solid & to & ~ti
        self.put(top, mat, t, prefix + "top")
        # back inner wall
        u = gx / r
        v = (gy + r * k * np.sqrt(np.clip(1 - u * u, 0, 1))) / p
        t = np.select([u < b for b, _ in inner_bands], [tn for _, tn in inner_bands], inner_bands[-1][1])
        t = np.where(v < 2.0, t - 1, t)
        inn = solid & ti & ~top
        self.put(inn, mat, np.clip(t, 1, 6), prefix + "inner")
        # front outer wall
        u = np.clip(gx / R, -1, 1)
        v = (gy - R * k * np.sqrt(1 - u * u)) / p
        t = np.select([u < b for b, _ in wall_bands], [tn for _, tn in wall_bands], wall_bands[-1][1])
        t0 = t
        done = v < 1.0
        t = np.where(done, t0 + 1, t)
        for g0, g1 in (groove or []):
            ga = ~done & (v >= g0) & (v < g0 + 1)
            gb = ~done & ~ga & (v >= g0 + 1) & (v < g1 + 1)
            t = np.where(ga, t0 - 2, np.where(gb, t0 + 1, t))
            done = done | ga | gb
        t = np.where(~done & (v > d / p - 2.0), t0 - 1, t)
        wall = solid & ~top & ~inn
        self.put(wall, mat, np.clip(t, 1, 6), prefix + "wall")
        self.wall_uv = (u, v, d / p)
        return solid

    # ---- finishing
    def outline(self, warm=True):
        s = self.solid
        pad = np.pad(s, 1)
        up, dn = ~pad[:-2, 1:-1], ~pad[2:, 1:-1]
        lf, rt = ~pad[1:-1, :-2], ~pad[1:-1, 2:]
        e = s & (up | dn | lf | rt)
        self.edge = e
        lit = e & (up | lf) & ~(dn | rt) & warm
        self.tone[e] = 0
        self.tone[lit] = 1
        return e

    def image(self, view=None, glow=None):
        mat, tone = self.mat, self.tone
        if view == "uv":
            m2 = np.where(mat > 0, MID["uvdark"], 0).astype(np.uint8)
            t2 = np.where(tone > 0, (tone + 1) // 2 + 1, 0)
            if glow is not None:
                g = glow & (mat > 0) & (tone > 0)
                m2[g] = MID["uvglow"]
                t2[g] = np.clip(tone[g] + 1, 3, 6)
            mat, tone = m2, t2
        elif view == "light":
            t2 = np.where(tone > 0, np.clip(tone + 1, 2, 6), 0)
            m2 = mat.copy()
            if glow is not None:
                g = glow & (mat > 0) & (tone > 0)
                m2[g] = MID["litdark"]
                t2[g] = np.clip(tone[g] - 2, 1, 3)
            mat, tone = m2, t2
        rgba = LUT[mat, np.clip(tone, 0, 6)]
        rgba[mat == 0] = 0
        return Image.fromarray(rgba, "RGBA")


def up(im, n):
    return im if n == 1 else im.resize((im.width * n, im.height * n), Image.NEAREST)


def ellipse_pts(cx, cy, rx, ry, n, a0=0.0, a1=360.0, rot=0.0):
    out = []
    for i in range(n):
        a = math.radians(a0 + (a1 - a0) * i / n)
        x, y = rx * math.cos(a), ry * math.sin(a)
        if rot:
            x, y = x * math.cos(rot) - y * math.sin(rot), x * math.sin(rot) + y * math.cos(rot)
        out.append((cx + x, cy + y))
    return out
