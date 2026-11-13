"""Fixed defect primitives. Every op is deterministic and placed at a named
model anchor; none of them uses random numbers.

PRE ops change geometry (state_mask) and run before the outline pass.
POST ops recolour interior pixels only (same_alpha) and never touch the outline.
"""
import math

import numpy as np

from px import MID

ZIG = [0, 0, 1, 1, 0, -1, -1, 0, 0, 1, 1, 0, 0, -1, 0, 1, 1, 0, -1, -1, 0, 0, 1, 0]
SPECK = [(0, 0), (2, 1), (-2, 1), (1, -2), (-1, 3), (3, -1), (-3, -2), (4, 2), (-4, 0), (0, -4),
         (2, 4), (-2, -4)]


def _a(c, a):
    """anchor -> integer pixel position, part mask, angle"""
    if isinstance(a, str):
        x, y, part, ang = c.anchors[a]
    else:
        x, y = a[0], a[1]
        part, ang = (a[2] if len(a) > 2 else None), 0.0
    px, py = c.P(x, y)
    pm = c.sel(*(part if isinstance(part, (list, tuple)) else [part])) if part else c.solid
    return int(math.floor(px)), int(math.floor(py)), pm, ang


def _n(c, units, lo=1):
    return max(lo, int(round(units * c.u)))


def _disk(c, ax, ay, rx, ry, wob=0.0):
    ys, xs = np.mgrid[0:c.h, 0:c.w]
    dx, dy = (xs - ax) / max(rx, 0.5), (ys - ay) / max(ry, 0.5)
    w = wob * (np.sin(xs * 1.7 + ys * 0.9) + np.sin(ys * 2.3 - xs * 0.6)) if wob else 0.0
    return dx * dx + dy * dy <= 1.0 + w


def _path(ax, ay, L, ang, zig=True, lean=0):
    out = []
    ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    for i in range(L):
        z = ZIG[i % len(ZIG)] if zig else 0
        out.append((int(round(ax + ca * i - sa * z)), int(round(ay + sa * i + ca * z))))
    return out


def _ok(c, x, y, pm):
    return 0 <= x < c.w and 0 <= y < c.h and pm[y, x] and c.mat[y, x] and not c.edge[y, x]


# ------------------------------------------------------------------ POST ops
def scratch(c, a, n=3, L=0.3, ang=-25, gap=2, lip=True):
    ax, ay, pm, _ = _a(c, a)
    Lp = _n(c, L, 3)
    sa, ca = math.sin(math.radians(ang)), math.cos(math.radians(ang))
    for i in range(n):
        o = (i - (n - 1) / 2.0) * gap
        sx, sy = ax - ca * Lp / 2 - sa * o + (i % 2), ay - sa * Lp / 2 + ca * o
        pts = _path(sx, sy, Lp - (i % 2) * 2, ang, zig=False)
        for x, y in pts:
            if _ok(c, x, y, pm):
                c.tone[y, x] = max(1, c.tone[y, x] - 2)
        if lip:
            for x, y in pts:
                if _ok(c, x, y + 1, pm) and (x, y + 1) not in pts:
                    c.tone[y + 1, x] = min(6, c.tone[y + 1, x] + 1)


def crack(c, a, L=0.4, ang=80, branch=True, wide=False, zig=True, lip=2):
    ax, ay, pm, _ = _a(c, a)
    Lp = _n(c, L, 4)
    pts = _path(ax, ay, Lp, ang, zig)
    if branch and Lp >= 8:
        bx, by = pts[Lp // 2]
        pts += [(bx + j, by + j // 2 + 1) if abs(math.cos(math.radians(ang))) < 0.7 else
                (bx + j // 2 + 1, by - j) for j in range(1, max(3, Lp // 3))]
    if wide:
        pts += [(x + 1, y) for x, y in pts[:max(2, Lp // 3)]]
    ps = set(pts)
    for x, y in pts:
        if _ok(c, x, y, pm):
            c.tone[y, x] = 0
    if lip:
        for x, y in pts:
            if (x + 1, y) not in ps and _ok(c, x + 1, y, pm):
                c.tone[y, x + 1] = min(6, c.tone[y, x + 1] + lip)


def gapline(c, a, L=0.2, ang=0, lip=2):
    crack(c, a, L, ang, branch=False, zig=False, lip=lip)


def patch(c, a, rx=0.2, ry=0.15, mat="base", lip=True, wob=0.08, dt=0):
    ax, ay, pm, _ = _a(c, a)
    m = _disk(c, ax, ay, _n(c, rx, 2), _n(c, ry, 2), wob) & pm & c.inner
    c.mat[m] = MID[mat]
    c.tone[m] = np.clip(c.tone[m] + dt, 1, 6)
    if lip:
        up = np.zeros_like(m)
        up[1:, :] |= ~m[:-1, :]
        lf = np.zeros_like(m)
        lf[:, 1:] |= ~m[:, :-1]
        sh = m & up & lf
        c.tone[sh] = np.clip(c.tone[sh] - 1, 1, 6)
        lo = np.zeros_like(m)
        lo[1:, :] |= m[:-1, :]
        lo[:, 1:] |= m[:, :-1]
        lo &= ~m & pm & c.inner
        c.tone[lo] = np.clip(c.tone[lo] + 1, 1, 6)
    return m


def stain(c, a, rx=0.2, ry=0.15, mat=None, dt=0, dither=True, wob=0.1, flat=None):
    ax, ay, pm, _ = _a(c, a)
    rxp, ryp = _n(c, rx, 2), _n(c, ry, 2)
    m = _disk(c, ax, ay, rxp, ryp, wob) & pm & c.inner
    if dither == "thin":
        ys, xs = np.mgrid[0:c.h, 0:c.w]
        m &= (xs + ys) % 2 == 0
    elif dither:
        core = _disk(c, ax, ay, max(1, rxp - 2), max(1, ryp - 2), wob)
        ys, xs = np.mgrid[0:c.h, 0:c.w]
        m &= core | ((xs + ys) % 2 == 0)
    if mat:
        c.mat[m] = MID[mat]
    if flat is not None:
        c.tone[m] = flat
    else:
        c.tone[m] = np.clip(c.tone[m] + dt, 1, 6)
    return m


def specks(c, a, n=7, sc=1, mat=None, tone=1, lip=False):
    ax, ay, pm, _ = _a(c, a)
    for dx, dy in SPECK[:n]:
        x, y = ax + dx * sc, ay + dy * sc
        if _ok(c, x, y, pm):
            if mat:
                c.mat[y, x] = MID[mat]
            c.tone[y, x] = tone
            if lip and _ok(c, x + 1, y + 1, pm):
                c.tone[y + 1, x + 1] = min(6, c.tone[y + 1, x + 1] + 1)


def kink(c, a, rx=0.15, ry=0.12):
    ax, ay, pm, _ = _a(c, a)
    m = _disk(c, ax, ay, _n(c, rx, 2), _n(c, ry, 2)) & pm & c.inner
    c.tone[m] = np.clip(7 - c.tone[m], 2, 6)
    rim = _disk(c, ax + 1, ay + 1, _n(c, rx, 2), _n(c, ry, 2)) & ~_disk(c, ax, ay, _n(c, rx, 2), _n(c, ry, 2)) & pm & c.inner
    c.tone[rim] = np.clip(c.tone[rim] + 1, 1, 6)


def shift(c, a, rw=0.2, rh=0.1, dx=1, dy=1):
    ax, ay, pm, _ = _a(c, a)
    w, h = _n(c, rw, 2), _n(c, rh, 2)
    x0, y0 = ax - w // 2, ay - h // 2
    src_m = c.mat[y0:y0 + h, x0:x0 + w].copy()
    src_t = c.tone[y0:y0 + h, x0:x0 + w].copy()
    ring = [int(c.tone[y, x]) for x in range(x0 - 1, x0 + w + 1) for y in (y0 - 1, y0 + h)
            if _ok(c, x, y, pm)]
    face = max(set(ring), key=ring.count) if ring else 3
    fm = c.mat[y0 - 1, ax] if c.mat[y0 - 1, ax] else c.mat[ay, ax]
    for j in range(h):
        for i in range(w):
            if _ok(c, x0 + i, y0 + j, pm):
                c.tone[y0 + j, x0 + i] = max(1, face - 1)
                c.mat[y0 + j, x0 + i] = fm
    for j in range(h):
        for i in range(w):
            x, y = x0 + i + dx, y0 + j + dy
            if _ok(c, x, y, pm) and src_m[j, i]:
                c.mat[y, x] = src_m[j, i]
                c.tone[y, x] = src_t[j, i]


def slot(c, a, rx=0.08, ry=0.05):
    ax, ay, pm, _ = _a(c, a)
    m = _disk(c, ax, ay, _n(c, rx, 1), _n(c, ry, 1)) & pm & c.inner
    c.tone[m] = 1
    core = _disk(c, ax, ay, max(0.6, _n(c, rx, 1) - 1), max(0.6, _n(c, ry, 1) - 1)) & m
    c.tone[core] = 0


def smooth(c, a, rx=0.2, ry=0.06, streak=True):
    ax, ay, pm, _ = _a(c, a)
    m = _disk(c, ax, ay, _n(c, rx, 2), _n(c, ry, 1)) & pm & c.inner
    if m.any():
        vals = c.tone[m]
        face = int(np.bincount(vals.astype(int), minlength=7)[1:].argmax() + 1)
        c.tone[m] = max(2, face - 1)
        if streak:
            row = m & (np.mgrid[0:c.h, 0:c.w][0] == ay)
            c.tone[row] = min(6, face + 1)


def halftone(c, a, rw=0.3, rh=0.2, dt=-2):
    ax, ay, pm, _ = _a(c, a)
    w, h = _n(c, rw, 4), _n(c, rh, 4)
    ys, xs = np.mgrid[0:c.h, 0:c.w]
    m = (abs(xs - ax) <= w // 2) & (abs(ys - ay) <= h // 2) & (xs % 2 == 0) & (ys % 2 == 0) & pm & c.inner
    c.tone[m] = np.clip(c.tone[m] + dt, 1, 6)


def stripe(c, a, L=0.5, ang=30, wid=0.08, mat=None, dt=-1, flat=None, edge=False):
    ax, ay, pm, _ = _a(c, a)
    ys, xs = np.mgrid[0:c.h, 0:c.w]
    ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    t = (xs - ax) * ca + (ys - ay) * sa
    d = -(xs - ax) * sa + (ys - ay) * ca
    wp = max(1.0, wid * c.u)
    m = (abs(t) <= _n(c, L, 3) / 2) & (abs(d) <= wp / 2) & pm & c.inner
    if mat:
        c.mat[m] = MID[mat]
    if flat is not None:
        c.tone[m] = flat
    else:
        c.tone[m] = np.clip(c.tone[m] + dt, 1, 6)
    if edge:
        e = (abs(t) <= _n(c, L, 3) / 2) & (d > wp / 2) & (d <= wp / 2 + 1) & pm & c.inner
        c.tone[e] = np.clip(c.tone[e] - 1, 1, 6)
    return m


def seam(c, a, L=0.3, ang=90, mat="solder", wpx=1, beads=True):
    ax, ay, pm, _ = _a(c, a)
    Lp = _n(c, L, 3)
    ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    for i in range(Lp):
        for k in range(wpx):
            x = int(round(ax + ca * (i - Lp / 2) - sa * k))
            y = int(round(ay + sa * (i - Lp / 2) + ca * k))
            if _ok(c, x, y, pm):
                c.mat[y, x] = MID[mat]
                c.tone[y, x] = 5 if (beads and i % 3 == 1 and k == 0) else 3
        x = int(round(ax + ca * (i - Lp / 2) - sa * wpx))
        y = int(round(ay + sa * (i - Lp / 2) + ca * wpx))
        if _ok(c, x, y, pm):
            c.tone[y, x] = max(1, c.tone[y, x] - 1)


def ghost(c, a, rw=0.3, rh=0.15, dx=1, dy=1):
    ax, ay, pm, _ = _a(c, a)
    w, h = _n(c, rw, 3), _n(c, rh, 3)
    x0, y0 = ax - w // 2, ay - h // 2
    reg = [(x, y) for y in range(y0, y0 + h) for x in range(x0, x0 + w) if _ok(c, x, y, pm)]
    if not reg:
        return
    tones = [int(c.tone[y, x]) for x, y in reg]
    face = max(set(tones), key=tones.count)
    marks = [(x, y) for x, y in reg if c.tone[y, x] < face]
    for x, y in marks:
        if _ok(c, x + dx, y + dy, pm) and c.tone[y + dy, x + dx] == face:
            c.tone[y + dy, x + dx] = max(1, face - 1)
    for x, y in reg[::7]:                                   # scraped field
        if c.tone[y, x] == face:
            c.tone[y, x] = min(6, face + 1)


def xmark(c, a, r=0.08, mat="red"):
    ax, ay, pm, _ = _a(c, a)
    n = _n(c, r, 2)
    for i in range(-n, n + 1):
        for x, y in ((ax + i, ay + i), (ax + i, ay - i)):
            if _ok(c, x, y, pm):
                c.mat[y, x] = MID[mat]
                c.tone[y, x] = 3
    for i in range(-n - 1, n + 2):
        if _ok(c, ax + i, ay + n + 2, pm):
            c.tone[ay + n + 2, ax + i] = 0


# ------------------------------------------------------------------- PRE ops
def bite(c, a, r=0.12, shape="round", ang=None, face=5, out=0.6, mat=None):
    """Remove a fixed piece at the silhouette; the exposed break gets a matte facet."""
    ax, ay, pm, a_ang = _a(c, a)
    ang = a_ang if ang is None else ang
    rp = _n(c, r, 2)
    ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    ys, xs = np.mgrid[0:c.h, 0:c.w]
    near = (xs - ax) ** 2 + (ys - ay) ** 2 <= (rp * 2.2) ** 2
    if shape == "round":
        ox, oy = ax + ca * rp * out, ay + sa * rp * out
        m = (xs - ox) ** 2 + (ys - oy) ** 2 <= rp * rp
    elif shape == "flat":
        m = ((xs - ax) * ca + (ys - ay) * sa >= 0) & near
    else:                                                   # wedge
        t = (xs - ax) * ca + (ys - ay) * sa
        d = abs(-(xs - ax) * sa + (ys - ay) * ca)
        m = (t >= -rp) & (d <= (t + rp) * 0.6) & ((xs - ax) ** 2 + (ys - ay) ** 2 <= (rp * 1.25) ** 2)
    m &= pm & c.solid
    c.erase(m)
    grow = np.zeros_like(m)
    grow[1:, :] |= m[:-1, :]
    grow[:-1, :] |= m[1:, :]
    grow[:, 1:] |= m[:, :-1]
    grow[:, :-1] |= m[:, 1:]
    f = grow & c.solid & pm
    c.tone[f] = face
    if mat:
        c.mat[f] = MID[mat]
    return m


def gap(c, a, w=2, L=0.2, ang=None):
    """Cut a narrow slot through a wire or link."""
    ax, ay, pm, a_ang = _a(c, a)
    ang = a_ang if ang is None else ang
    ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    ys, xs = np.mgrid[0:c.h, 0:c.w]
    t = (xs - ax) * ca + (ys - ay) * sa
    d = -(xs - ax) * sa + (ys - ay) * ca
    m = (abs(t) <= _n(c, L, 3) / 2.0) & (abs(d) < w / 2.0 + 0.01) & pm & c.solid
    c.erase(m)
    return m


def chew(c, a, cells, sc=1):
    """Erase a fixed list of pixel offsets (cut pins, clipped contacts)."""
    ax, ay, pm, _ = _a(c, a)
    m = np.zeros((c.h, c.w), bool)
    for dx, dy, w, h in cells:
        m[ay + dy * sc:ay + (dy + h) * sc, ax + dx * sc:ax + (dx + w) * sc] = True
    m &= pm & c.solid
    c.erase(m)
    return m


def recess(c, a, rx=0.2, ry=0.15, mat="slag", tone=2):
    """Opened area inside the object (alpha unchanged but declared state_mask)."""
    ax, ay, pm, _ = _a(c, a)
    m = _disk(c, ax, ay, _n(c, rx, 2), _n(c, ry, 2)) & pm & c.solid
    c.mat[m] = MID[mat]
    c.tone[m] = tone
    ring = _disk(c, ax, ay, _n(c, rx, 2) + 1, _n(c, ry, 2) + 1) & ~m & pm & c.solid
    c.tone[ring] = 0
    return m


PRE = {"bite": bite, "gap": gap, "chew": chew, "recess": recess}
POST = {f.__name__: f for f in (scratch, crack, gapline, patch, stain, specks, kink, shift, slot,
                                smooth, halftone, stripe, seam, ghost, xmark)}
