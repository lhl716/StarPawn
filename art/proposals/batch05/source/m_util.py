"""Functional objects: instruments, manuals, containers, cards, process items, machines, documents.
They use the status states from ITEM_LAYOUT_04.csv, never appraisal defects."""
import math

import numpy as np

from px import MID
import m_metal
import m_gem

LAMP = {"idle": ("teal", 3), "active": ("screen", 5), "working": ("screen", 5), "in_process": ("hot", 5), "running": ("screen", 5),
        "reading_ready": ("gold", 5), "ready": ("gold", 5), "cooling": ("hot", 3), "unavailable": ("red", 4),
        "interrupted": ("red", 4), "maintenance": ("red", 3), "blocked": ("red", 4), "alert": ("red", 5),
        "queued": ("teal", 3), "in_use": ("screen", 5), "closed": ("teal", 2)}
BAD = ("unavailable", "interrupted", "maintenance", "blocked", "alert")


def lamp(c, x, y, st, r=0.07):
    mat, t = LAMP.get(st, ("teal", 3))
    r = max(r, c.px(1.5))
    c.ell(x, y, r * 1.5, r * 1.5, "dark", flat=1, part="lamp")
    if st in ("reading_ready", "ready"):
        c.box(x - r, y - r, x + r, y + r, mat, t, "lamp", bevel=False)      # square = ready
    else:
        c.ell(x, y, r, r, mat, flat=t, part="lamp")
    if st in BAD:
        c.seg(x - r * 1.4, y + r * 1.4, x + r * 1.4, y - r * 1.4, "dark", 1)


def screen(c, x0, y0, x1, y1, st, bars=3):
    on = st not in ("idle", "unavailable", "closed", "maintenance", "empty")
    c.box(x0, y0, x1, y1, "dark", 1, "screen", bevel=False)
    p = c.px(1)
    c.box(x0 + p, y0 + p, x1 - p, y1 - p, "screen", 3 if on else 1, "screen", bevel=False)
    if on:
        h = (y1 - y0 - 2 * p) / (bars * 2 + 1)
        for i in range(bars):
            w = (x1 - x0) * (0.75 - 0.2 * ((i * 2) % 3))
            c.box(x0 + 2 * p, y0 + p + h * (2 * i + 1), x0 + 2 * p + w, y0 + p + h * (2 * i + 2), "screen",
                  6 if st in ("reading_ready", "ready") else 5, "screen", bevel=False)
    if st in BAD:
        c.seg(x0 + p, y1 - p, x1 - p, y0 + p, "red", 4)


def prism(c, x0, y0, x1, y1, dep, mat, sk=0.5, tones=(5, 3, 2), part="body"):
    """Box in 3/4 view; (x0,y0)-(x1,y1) is the front face, dep the receding depth."""
    dx, dy = sk * dep, -dep
    c.poly([(x0, y0), (x1, y0), (x1 + dx, y0 + dy), (x0 + dx, y0 + dy)], mat, tones[0], part + "_top")
    c.poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], mat, tones[1], part + "_front")
    c.poly([(x1, y0), (x1 + dx, y0 + dy), (x1 + dx, y1 + dy), (x1, y1)], mat, tones[2], part + "_side")
    c.seg(x0, y0, x1, y0, mat, min(6, tones[0] + 1))


# ------------------------------------------------------------------ instruments
def tool_scale(c, v):
    st = v["state"]
    prism(c, -1.5, 0.15, 1.2, 0.62, 0.55, "dark")
    c.ell(-0.55, -0.22, 0.85, 0.3, "silver", flat=3, part="pan")
    c.ell(-0.55, -0.27, 0.85, 0.3, "silver", part="pan")
    c.ell(-0.55, -0.27, 0.66, 0.2, "silver", flat=4, part="pan")
    screen(c, 0.3, 0.24, 1.05, 0.52, st, 2)
    lamp(c, 0.1, 0.4, st)


def tool_assay(c, v):
    st = v["state"]
    prism(c, -1.0, -0.2, 0.75, 0.85, 0.5, "plastic")
    c.box(-0.85, -0.05, -0.05, 0.45, "glass", 3 if st == "idle" else 4, "chamber")
    if st in ("active", "reading_ready"):
        c.ell(-0.45, 0.25, 0.2, 0.1, "screen", flat=6 if st == "active" else 4, part="chamber")
    c.box(-0.5, 0.36, -0.4, 0.45, "silver", 4, "chamber", bevel=False)
    screen(c, 0.08, -0.05, 0.62, 0.3, st, 3)
    lamp(c, 0.2, 0.55, st)
    c.box(0.36, 0.48, 0.62, 0.62, "dark", 3, "key", bevel=False)
    c.tube([(-0.3, -0.72), (-0.1, -0.95), (0.4, -0.9)], 0.07, "dark", part="arm", minpx=2)


def tool_loupe(c, v):
    st = v["state"]
    op = st != "idle"
    c.poly([(-0.2, 0.1), (0.75, 0.35), (0.6, 0.85), (-0.5, 0.6)], "dark", 3, "cover")
    c.poly([(-0.2, 0.1), (0.75, 0.35), (0.72, 0.45), (-0.24, 0.2)], "dark", 5, "cover")
    cx, cy = (-0.18, -0.3) if op else (0.02, 0.2)
    c.ell(cx, cy, 0.56, 0.5, "silver", part="ring")
    c.ell(cx, cy, 0.42, 0.37, "glass", flat=3 if st == "unavailable" else 4, part="lens")
    if st != "unavailable":
        c.ell(cx - 0.14, cy - 0.12, 0.16, 0.1, "glass", flat=6, part="lens", rot=-0.5)
    if st == "reading_ready":
        c.box(cx + 0.1, cy + 0.08, cx + 0.24, cy + 0.2, "gold", 5, "lens", bevel=False)
    if st == "unavailable":
        c.seg(cx - 0.3, cy + 0.26, cx + 0.3, cy - 0.26, "red", 4, wpx=2 if c.lod else 1)
    c.ell(0.52, 0.62, 0.07, 0.07, "silver", flat=5, part="pivot")


def _wand(c, v, glow_mat, head):
    st = v["state"]
    c.lathe(0, [(-0.2, 0.2), (-0.1, 0.24), (1.5, 0.22), (1.62, 0.16)], "dark", part="grip")
    for i in range(4):
        c.box(-0.2, 0.4 + i * 0.22, 0.2, 0.46 + i * 0.22, "dark", 2, "grip", bevel=False)
    c.lathe(0, [(-0.95, 0.4), (-0.85, 0.44), (-0.3, 0.3), (-0.2, 0.22)], "silver" if head == "lens" else "vio", part="head")
    on = st in ("active", "reading_ready")
    c.ell(0, -0.95, 0.4, 0.12, glow_mat if on else "glass", flat=6 if on else 3, part="lens")
    if st == "active":
        g = c.polymask([(-0.36, -1.0), (0.36, -1.0), (0.62, -1.62), (-0.62, -1.62)])
        ys, xs = np.mgrid[0:c.h, 0:c.w]
        c.put(g & ((xs + ys) % 2 == 0), glow_mat, 5, "beam", over=False)
    lamp(c, 0, 0.12, st, 0.06)


def tool_jade_light(c, v):
    _wand(c, v, "hot", "lens")


def tool_uv(c, v):
    _wand(c, v, "vio", "tube")


def tool_gem_probe(c, v):
    st = v["state"]
    prism(c, 0.1, -0.05, 1.5, 0.6, 0.45, "plastic")
    for i in range(5):                                   # response bar, no numerals
        lit = st in ("active", "reading_ready") and i < (2 if st == "active" else 4)
        c.box(0.25 + i * 0.2, 0.1, 0.39 + i * 0.2, 0.26, "screen" if lit else "dark", 5 if lit else 2, "bar", bevel=False)
    lamp(c, 1.32, 0.42, st)
    c.tube([(0.1, 0.3), (-0.3, 0.55), (-0.7, 0.2)], 0.04, "dark", part="cable", minpx=1)
    c.tube([(-0.7, 0.2), (-1.4, -0.5)], 0.14, "dark", part="pen", minpx=3)
    c.tube([(-1.4, -0.5), (-1.58, -0.68)], 0.06, "silver", part="tip", minpx=2)


# ------------------------------------------------------------------ manuals, organizers
MANUAL = {"metals": ("gold", "bar"), "jade": ("nuo", "disc"), "antiques": ("ceramic", "vase"), "art": ("red", "frame"),
          "electronics": ("pcb", "chip"), "media": ("vio", "ring"), "books": ("leather", "book"),
          "recovery": ("copper", "tri"), "jewelry": ("sapphire", "gem"), "index": ("inkblue", "star")}


def _emblem(c, kind, x, y, r, mat="paper"):
    if kind == "bar":
        c.poly([(x - r, y + r * 0.3), (x + r * 0.6, y + r * 0.3), (x + r, y - r * 0.3), (x - r * 0.6, y - r * 0.3)], mat, 6, "emblem")
    elif kind == "disc":
        c.arc(x, y, r * 0.8, r * 0.8, 0, 360, r * 0.35, mat, flat=6, part="emblem", minpx=2)
    elif kind == "vase":
        c.lathe(x, [(y - r, r * 0.3), (y - r * 0.4, r * 0.25), (y + r * 0.2, r * 0.7), (y + r, r * 0.35)], mat, "matte", "emblem")
    elif kind == "frame":
        c.box(x - r, y - r * 0.8, x + r, y + r * 0.8, mat, 6, "emblem", bevel=False)
        c.box(x - r * 0.6, y - r * 0.45, x + r * 0.6, y + r * 0.45, "inkblue", 4, "emblem", bevel=False)
    elif kind == "chip":
        c.box(x - r * 0.7, y - r * 0.7, x + r * 0.7, y + r * 0.7, mat, 6, "emblem", bevel=False)
        for i in (-1, 0, 1):
            c.box(x + i * r * 0.45 - r * 0.1, y - r, x + i * r * 0.45 + r * 0.1, y + r, mat, 5, "emblem", bevel=False)
    elif kind == "ring":
        c.ell(x, y, r, r, mat, flat=6, part="emblem")
        c.ell(x, y, r * 0.35, r * 0.35, "dark", flat=2, part="emblem")
    elif kind == "book":
        c.poly([(x - r, y - r * 0.6), (x, y - r * 0.4), (x, y + r * 0.7), (x - r, y + r * 0.5)], mat, 6, "emblem")
        c.poly([(x + r, y - r * 0.6), (x, y - r * 0.4), (x, y + r * 0.7), (x + r, y + r * 0.5)], mat, 5, "emblem")
    elif kind == "tri":
        c.poly([(x, y - r), (x + r, y + r * 0.8), (x - r, y + r * 0.8)], mat, 6, "emblem")
        c.poly([(x, y - r * 0.3), (x + r * 0.4, y + r * 0.5), (x - r * 0.4, y + r * 0.5)], "dark", 3, "emblem")
    elif kind == "gem":
        m_gem.draw_gem(c, "diamond", "round", x, y, r, "emblem")
    else:
        m_metal._star(c, x, y, r, mat, "emblem", tone=6, n=5)


def manual(c, v):
    mat, emb = MANUAL[v["book"]]
    if v["state"] == "closed":                           # upright 2x3 volume
        c.box(-0.72, -1.2, 0.78, 1.2, "paper", 3, "pages", bevel=False)
        c.box(-0.78, -1.24, 0.7, 1.14, mat, 3, "cover")
        c.box(-0.78, -1.24, -0.6, 1.14, mat, 2, "spine", bevel=False)
        c.box(-0.42, -0.96, 0.5, -0.46, "sheet", 5, "plate", bevel=False)
        c.box(-0.34, -0.84, 0.42, -0.84 + max(c.px(1), 0.05), "inkblue", 4, "plate", bevel=False)
        c.box(-0.2, -0.68, 0.28, -0.68 + max(c.px(1), 0.05), "dark", 3, "plate", bevel=False)
        c.box(-0.2, -0.58, 0.16, -0.58 + max(c.px(1), 0.05), "dark", 3, "plate", bevel=False)
        c.box(-0.5, 0.86, 0.6, 0.86 + max(c.px(1), 0.05), "paper", 5, "rule", bevel=False)
        _emblem(c, emb, 0.04, 0.24, 0.36, "paper" if mat not in ("silver", "bing", "skin_water", "skin_yellow") else "inkblue")
    else:                                                # open: shows the portrait sheet layout in miniature
        c.frame(c.u * 0.84, c.cx, c.cy)
        c.poly([(-0.98, -0.72), (0.98, -0.72), (0.98, 0.74), (0.08, 0.9), (-0.08, 0.9), (-0.98, 0.74)], mat, 3, "cover")
        for sx in (-1, 1):
            c.poly([(sx * 0.92, -0.66), (sx * 0.03, -0.6), (sx * 0.03, 0.74), (sx * 0.92, 0.62)], "sheet", 5 if sx < 0 else 6, "page")
            x0, x1 = (sx * 0.82, sx * 0.14) if sx < 0 else (sx * 0.14, sx * 0.82)
            p = max(c.px(1), 0.04)
            c.box(x0 + 0.1, -0.52, x1 - 0.1, -0.52 + p, "inkblue", 4, "page", bevel=False)
            c.box(x0, -0.4, x1, -0.4 + p, "dark", 3, "page", bevel=False)
            c.box(x0, -0.3, x1 - 0.2, -0.3 + p, "dark", 3, "page", bevel=False)
            w = (x1 - x0 - 0.08) / 2
            c.box(x0, -0.14, x0 + w, 0.22, "sheet", 3, "page", bevel=False)
            c.box(x0 + w + 0.08, -0.14, x1, 0.22, "sheet", 3, "page", bevel=False)
            c.box(x0, 0.28, x0 + w * 0.6, 0.28 + p, "pcb", 4, "page", bevel=False)
            c.box(x0 + w + 0.08, 0.28, x0 + w + 0.08 + w * 0.6, 0.28 + p, "red", 4, "page", bevel=False)
        c.box(-0.03, -0.6, 0.03, 0.76, "sheet", 2, "gutter", bevel=False)


def organizer(c, v):
    st, n = v["state"], v["slots"]
    cols = 4 if n == 8 else 6
    mat = "leather" if n == 8 else "wood"
    m_util_box(c, mat, st)
    fill = {"empty": 0, "partly_filled": cols // 2, "full": cols, "open": cols // 2}[st]
    books = list(MANUAL.values())
    w = 1.6 / cols
    for i in range(cols):
        x = -0.8 + i * w
        c.box(x + 0.02, -0.5, x + w - 0.02, 0.12, "dark", 1, "slot", bevel=False)
        if i < fill:
            c.box(x + 0.03, -0.62, x + w - 0.03, 0.12, books[i][0], 4, "spine%d" % i)
            c.box(x + 0.03, -0.4, x + w - 0.03, -0.32, "paper", 6, "spine%d" % i, bevel=False)
    if n == 12:                                           # second visible tier marks the larger box
        c.box(-0.86, 0.34, 0.86, 0.42, "gold", 4, "trim", bevel=False)


def m_util_box(c, mat, st, w=0.9, top=-0.5, bot=0.9, dep=0.35):
    prism(c, -w, 0.12, w, bot, dep, mat, sk=0.35)
    c.poly([(-w, 0.12), (w, 0.12), (w + 0.12, 0.12 - dep), (-w + 0.12, 0.12 - dep)], "dark", 2, "well")
    if st == "open":
        c.poly([(-w + 0.12, 0.12 - dep), (w + 0.12, 0.12 - dep), (w + 0.2, -1.0), (-w + 0.2, -1.0)], mat, 4, "lid")
        c.poly([(-w + 0.2, -0.3 - dep), (w + 0.12, -0.3 - dep), (w + 0.16, -0.94), (-w + 0.26, -0.94)], mat, 2, "lid")
    c.box(-0.16, 0.4, 0.16, 0.56, "gold", 4, "latch")


def case(c, v):
    """Tray-style container seen from above-front with a visible grid."""
    st, cols, rows, mat, fill_mat = v["state"], v["cols"], v["rows"], v["mat"], v["fill"]
    prism(c, -0.95, 0.5, 0.95, 0.9, 0.0, mat)
    c.poly([(-0.95, 0.5), (0.95, 0.5), (0.78, -0.5), (-0.78, -0.5)], mat, 4, "tray")
    c.poly([(-0.86, 0.42), (0.86, 0.42), (0.71, -0.42), (-0.71, -0.42)], "dark", 2, "well")
    n = cols * rows
    k = {"empty": 0, "partly_filled": n // 3, "full": n, "open": n // 3, "reserved": n // 2, "sealed": 0, "assayed": 0, "sorted": n}.get(st, 0)
    idx = 0
    for r in range(rows):
        for q in range(cols):
            fy = (r + 0.5) / rows
            y = -0.42 + 0.84 * fy
            half = 0.71 + 0.15 * fy
            x = -half + 2 * half * (q + 0.5) / cols
            rx, ry = half / cols * 0.62, 0.84 / rows * 0.3
            c.ell(x, y, rx, ry, "dark", flat=1, part="cell")
            if idx < k:
                fm = fill_mat[idx % len(fill_mat)]
                c.ell(x, y - ry * 0.2, rx * 0.8, ry * 0.9, fm, "gloss", "item%d" % idx)
                if st == "reserved" and idx % 2 == 0:
                    c.box(x - rx, y + ry * 0.5, x + rx, y + ry * 0.5 + max(c.px(1), 0.03), "red", 4, "tagr", bevel=False)
            idx += 1
    if st == "open" or v.get("lid"):
        c.poly([(-0.78, -0.5), (0.78, -0.5), (0.7, -1.0), (-0.7, -1.0)], mat, 3, "lid")
        c.poly([(-0.7, -0.56), (0.7, -0.56), (0.64, -0.94), (-0.64, -0.94)], mat, 5 if st == "open" else 4, "lid")
    if st in ("sealed", "assayed"):
        c.poly([(-0.86, 0.42), (0.86, 0.42), (0.71, -0.42), (-0.71, -0.42)], mat, 3, "cover")
        c.poly([(-0.2, 0.42), (0.2, 0.42), (0.16, -0.42), (-0.16, -0.42)], "red" if st == "sealed" else "label", 4, "strap")
        if st == "assayed":
            c.box(0.3, -0.1, 0.62, 0.14, "label", 5, "tag")
            c.box(0.36, 0.0, 0.56, 0.0 + max(c.px(1), 0.03), "inkblue", 3, "tag", bevel=False)
    c.box(-0.14, 0.62, 0.14, 0.76, "gold", 4, "latch")


# ------------------------------------------------------------------ cards
def card(c, v):
    st, kind = v["state"], v["card"]
    mat = "inkblue" if kind == "savings" else "vio"
    c.box(-1.6, -0.82, 1.6, 0.82, mat, 3, "card", r=0.14)
    p = max(c.px(1), 0.04)
    if st == "back":
        c.box(-1.6, -0.5, 1.6, -0.2, "dark", 1, "stripe", bevel=False)
        c.box(-1.3, 0.05, 0.5, 0.35, "label", 5, "sign", bevel=False)
        c.box(0.8, 0.1, 1.3, 0.3, mat, 5, "holo", bevel=False)
    else:
        c.poly([(-1.6, 0.2), (1.6, -0.5), (1.6, -0.2), (-1.6, 0.5)], mat, 4, "sweep")
        c.box(-1.25, -0.3, -0.75, 0.1, "gold", 4, "chip")
        c.seg(-1.25, -0.1, -0.75, -0.1, "gold", 2)
        c.seg(-1.0, -0.3, -1.0, 0.1, "gold", 2)
        c.arc(1.05, -0.4, 0.26, 0.22, 0, 360, 0.07, "gold" if kind == "credit" else "silver", part="emblem", minpx=2)
        m_metal._star(c, 1.05, -0.4, 0.1, "gold" if kind == "credit" else "silver", "emblem", tone=5, n=4)
        for i in range(4):                               # blank embossed groups; serial is UI text
            c.box(-1.25 + i * 0.62, 0.42, -0.8 + i * 0.62, 0.42 + p * 2, mat, 5, "emboss", bevel=False)
        if st == "front_void":
            c.poly([(-1.6, 0.82), (-1.2, 0.82), (1.6, -0.6), (1.6, -0.82), (1.2, -0.82), (-1.6, 0.6)], "red", 4, "void")
            c.erase(((c.gx + 0.2) / 0.16) ** 2 + ((c.gy - 0.05) / 0.16) ** 2 <= 1)


def card_holder(c, v):
    st = v["state"]
    c.box(-1.6, -0.75, 1.6, 0.8, "leather", 3, "holder", r=0.1)
    k = {"empty": 0, "partly_filled": 1, "full": 2, "open": 1}[st]
    for i in range(2):
        x = -1.45 + i * 1.5
        if i < k:
            c.box(x + 0.08, -0.62 if st != "open" else -0.74, x + 1.32, 0.2, ("inkblue", "vio")[i], 4, "card%d" % i, r=0.06)
            c.box(x + 0.2, -0.4, x + 0.48, -0.2, "gold", 4, "card%d" % i, bevel=False)
        c.box(x, -0.3, x + 1.4, 0.68, "leather", 4, "pocket%d" % i)
        c.seg(x + 0.05, -0.24, x + 1.35, -0.24, "leather", 6)
    c.box(-0.06, -0.75, 0.06, 0.8, "leather", 1, "fold", bevel=False)


# ------------------------------------------------------------------ lots and process items
def jar(c, v):
    st, fill = v["state"], v["fill"]
    c.lathe(0, [(-0.55, 0.5), (-0.45, 0.62), (0.75, 0.62), (0.9, 0.5)], "glass", "gloss", "jar")
    j = c.sel("jar")
    lvl = v.get("level", 0.0)
    f = j & (c.gy > lvl) & (np.abs(c.gx) < 0.52)
    c.mat[f] = MID[fill]
    ys, xs = np.mgrid[0:c.h, 0:c.w]
    c.add(f & ((xs * 3 + ys * 5) % 7 == 0), 1)
    c.add(f & ((xs * 5 + ys * 3) % 5 == 0), -1)
    c.box(-0.56, -0.86, 0.56, -0.55, "dark", 3, "lid")
    if st == "sealed":
        c.box(-0.12, -0.9, 0.12, -0.2, "red", 4, "seal", bevel=False)
    else:                                                # assayed / measured / tested: tag with a tick shape
        c.box(0.1, -0.3, 0.7, 0.1, "label", 5, "tag")
        c.seg(0.22, -0.1, 0.34, 0.0, "inkblue", 3, wpx=2 if c.lod else 1)
        c.seg(0.34, 0.0, 0.56, -0.2, "inkblue", 3, wpx=2 if c.lod else 1)


def recovery_batch(c, v):
    st = v["state"]
    prism(c, -1.5, 0.2, 1.5, 0.7, 0.5, "dark")
    for i in range(3):
        x = -1.0 + i * 0.9
        c.lathe(x, [(-0.5, 0.26), (-0.42, 0.3), (0.3, 0.3)], "silver", part="can%d" % i)
        c.box(x - 0.3, -0.56, x + 0.3, -0.44, "dark", 3, "cap", bevel=False)
        if st == "in_process" and i < 2:
            c.box(x - 0.14, -0.2, x + 0.14, 0.1, "hot", 5, "win", bevel=False)
        elif st == "ready":
            c.box(x - 0.14, -0.2, x + 0.14, 0.1, "gold", 5, "win", bevel=False)
        else:
            c.box(x - 0.14, -0.2, x + 0.14, 0.1, "dark", 2, "win", bevel=False)
    lamp(c, 1.25, 0.45, st)


def residue(c, v):
    c.poly([(-1.4, 0.7), (-1.55, -0.1), (-1.0, -0.5), (0.9, -0.55), (1.5, -0.05), (1.4, 0.7)], "plastic", 3, "sack")
    c.poly([(-1.3, 0.0), (-0.9, -0.42), (0.8, -0.46), (1.3, 0.0)], "plastic", 5, "sack")
    c.box(-0.3, -0.78, 0.3, -0.48, "plastic", 4, "neck")
    c.box(-0.36, -0.62, 0.36, -0.54, "red", 4, "tie", bevel=False)
    c.box(-0.5, 0.1, 0.5, 0.5, "slag", 3, "window")
    ys, xs = np.mgrid[0:c.h, 0:c.w]
    c.add(c.sel("window") & ((xs * 3 + ys * 5) % 7 == 0), 2)


def process_cartridge(c, v):
    st = v["state"]
    c.lathe(0, [(-0.9, 0.3), (-0.8, 0.5), (0.8, 0.5), (0.9, 0.4)], "plastic", part="can")
    c.box(-0.5, -0.95, 0.5, -0.8, "dark", 3, "cap", bevel=False)
    c.box(-0.24, -0.4, 0.24, 0.5, "dark", 1, "win", bevel=False)
    if st == "sealed":
        c.box(-0.2, -0.3, 0.2, 0.46, "teal", 5, "win", bevel=False)
        c.box(-0.5, -0.74, 0.5, -0.64, "red", 4, "seal", bevel=False)
    else:
        c.box(-0.2, 0.36, 0.2, 0.46, "slag", 3, "win", bevel=False)


def energy_cell(c, v):
    st = v["state"]
    c.box(-0.6, -0.8, 0.6, 0.92, "dark", 3, "cell", r=0.1)
    c.box(-0.22, -0.95, 0.22, -0.8, "silver", 4, "term")
    k = {"charged": 3, "partly_used": 1, "empty": 0}[st]
    for i in range(3):
        on = (2 - i) < k
        c.box(-0.42, -0.6 + i * 0.46, 0.42, -0.24 + i * 0.46, "screen" if on else "dark", 5 if on else 1, "seg%d" % i, bevel=False)
    if st == "empty":
        c.seg(-0.4, 0.74, 0.4, -0.58, "red", 4)


def crucible(c, v):
    st = v["state"]
    c.lathe(-0.5, [(-0.5, 0.7), (-0.4, 0.74), (0.3, 0.5), (0.62, 0.32), (0.7, 0.36)], "ceramic" if st != "cooling" else "brown", "matte", "bowl")
    c.ell(-0.5, -0.48, 0.68, 0.16, "dark", flat=1, part="mouth")
    if st == "loaded":
        for i, (x, y) in enumerate(((-0.75, -0.5), (-0.5, -0.46), (-0.25, -0.5), (-0.6, -0.55))):
            c.ell(x, y, 0.13, 0.07, "gold" if i % 2 else "copper", part="charge")
    if st == "cooling":
        c.ell(-0.5, -0.48, 0.58, 0.12, "hot", flat=4, part="mouth")
        c.ell(-0.56, -0.5, 0.3, 0.06, "hot", flat=6, part="mouth")
    c.tube([(0.5, 0.6), (1.5, -0.5)], 0.06, "silver", part="tongs", minpx=2)
    c.tube([(0.7, 0.66), (1.55, -0.38)], 0.06, "silver", part="tongs", minpx=2)
    c.arc(0.5, 0.5, 0.2, 0.16, 90, 270, 0.06, "silver", part="tongs", minpx=2)


def ingot_mold(c, v):
    st = v["state"]
    prism(c, -1.4, 0.0, 1.1, 0.62, 0.7, "slag", tones=(5, 3, 2))
    c.poly([(-1.05, -0.1), (0.85, -0.1), (1.1, -0.58), (-0.8, -0.58)], "slag", 1, "cavity")
    if st == "filled":
        c.poly([(-0.98, -0.14), (0.82, -0.14), (1.02, -0.54), (-0.78, -0.54)], "gold", 5, "metal")
        c.poly([(-0.4, -0.14), (-0.1, -0.14), (0.1, -0.54), (-0.2, -0.54)], "gold", 6, "metal")
    else:
        c.poly([(-0.98, -0.14), (0.82, -0.14), (0.9, -0.3), (-0.9, -0.3)], "slag", 2, "cavity")


# ------------------------------------------------------------------ machines
def machine(c, v):
    st, kind = v["state"], v["machine"]
    if kind == "recovery":
        prism(c, -0.9, -0.3, 0.7, 0.92, 0.45, "plastic")
        c.box(-0.75, -0.15, 0.05, 0.5, "dark", 2, "hatch")
        c.ell(-0.35, 0.18, 0.26, 0.26, "glass", flat=5 if st == "working" else 3, part="port")
        if st == "working":
            c.arc(-0.35, 0.18, 0.16, 0.16, 20, 250, 0.05, "screen", flat=6, part="port", minpx=1)
        screen(c, 0.14, -0.15, 0.58, 0.12, st, 2)
        lamp(c, 0.24, 0.34, st)
        c.box(0.14, 0.56, 0.58, 0.8, "dark", 2 if st != "ready" else 3, "tray", bevel=False)
        if st == "ready":
            c.box(0.2, 0.62, 0.52, 0.74, "gold", 5, "tray", bevel=False)
        c.tube([(-0.4, -0.75), (-0.4, -1.02), (0.3, -1.02)], 0.1, "silver", part="duct", minpx=2)
    elif kind == "furnace":
        prism(c, -0.85, -0.5, 0.65, 0.92, 0.45, "slag", tones=(5, 3, 2))
        hot = {"working": ("hot", 5), "cooling": ("hot", 2), "ready": ("gold", 4)}.get(st, ("dark", 1))
        c.box(-0.6, -0.1, 0.4, 0.5, "dark", 1, "door")
        c.box(-0.5, 0.0, 0.3, 0.4, hot[0], hot[1], "glow", bevel=False)
        if st == "working":
            c.ell(-0.1, 0.24, 0.26, 0.12, "hot", flat=6, part="glow")
        c.box(-0.6, 0.62, 0.4, 0.74, "copper", 4, "bus", bevel=False)
        lamp(c, 0.52, -0.3, st)
        c.lathe(-0.1, [(-1.02, 0.14), (-0.92, 0.2), (-0.74, 0.2)], "silver", part="flue")
    elif kind == "terminal":
        prism(c, -0.9, 0.35, 0.75, 0.9, 0.5, "plastic")
        c.poly([(-0.75, -0.85), (0.7, -0.85), (0.8, 0.2), (-0.85, 0.2)], "dark", 3, "monitor")
        screen(c, -0.62, -0.72, 0.6, 0.08, "idle" if st == "idle" else st, 4)
        for i in range(8):
            c.box(-0.7 + i * 0.17, 0.5, -0.58 + i * 0.17, 0.6, "dark", 4, "keys", bevel=False)
            c.box(-0.7 + i * 0.17, 0.68, -0.58 + i * 0.17, 0.78, "dark", 4, "keys", bevel=False)
        c.box(0.5, 0.45, 0.7, 0.5, "dark", 1, "cardslot", bevel=False)
        lamp(c, 0.6, 0.7, st)
    else:                                                # jeweler bench
        c.box(-1.0, 0.1, 1.0, 0.26, "wood", 5, "top")
        c.box(-0.92, 0.26, -0.78, 0.95, "wood", 3, "leg")
        c.box(0.78, 0.26, 0.92, 0.95, "wood", 3, "leg")
        c.box(-0.6, 0.26, 0.6, 0.6, "wood", 4, "drawer")
        c.box(-0.12, 0.38, 0.12, 0.46, "gold", 4, "pull", bevel=False)
        c.poly([(-0.2, 0.1), (0.2, 0.1), (0.1, -0.12), (-0.1, -0.12)], "wood", 3, "pin")
        c.tube([(0.6, 0.1), (0.6, -0.6), (0.25, -0.85)], 0.06, "silver", part="lamp_arm", minpx=2)
        c.ell(0.2, -0.82, 0.2, 0.1, "dark", flat=3, part="shade")
        if st in ("working", "ready"):
            g = c.polymask([(0.05, -0.76), (0.35, -0.76), (0.5, 0.08), (-0.3, 0.08)])
            ys, xs = np.mgrid[0:c.h, 0:c.w]
            c.put(g & ((xs + ys) % 2 == 0), "hot", 6, "beam", over=False)
        c.box(-0.9, -0.2, -0.5, 0.1, "dark", 3, "vise")
        c.box(-0.82, -0.34, -0.58, -0.2, "silver", 4, "vise")
        if st == "ready":
            c.arc(0.0, -0.02, 0.1, 0.06, 0, 360, 0.04, "gold", part="work", minpx=2)
        lamp(c, -0.3, 0.44, st, 0.05)


def toolkit(c, v):
    st = v["state"]
    if st == "closed":
        c.box(-1.5, -0.5, 1.5, 0.6, "leather", 3, "roll", r=0.2)
        c.box(-0.3, -0.56, 0.3, 0.66, "leather", 5, "strap")
        c.box(-0.12, -0.1, 0.12, 0.14, "gold", 4, "buckle")
    else:
        c.box(-1.6, -0.7, 1.6, 0.75, "leather", 3, "roll")
        c.box(-1.5, 0.2, 1.5, 0.66, "leather", 4, "pockets")
        tools = [("silver", 0.05), ("silver", 0.08), ("dark", 0.1), ("silver", 0.04), ("wood", 0.1), ("silver", 0.06)]
        for i, (m, w) in enumerate(tools):
            x = -1.25 + i * 0.5
            c.tube([(x, 0.5), (x, -0.55 + (i % 3) * 0.1)], w, m, part="tool%d" % i, minpx=2)
            c.tube([(x, 0.5), (x, 0.0)], 0.12, "wood" if i % 2 else "dark", part="handle%d" % i, minpx=3)
        if st == "maintenance":
            c.box(0.9, -0.66, 1.5, -0.3, "red", 4, "tag")
            c.seg(1.0, -0.4, 1.4, -0.56, "paper", 6)


def paper_doc(c, v):
    st, kind = v["state"], v["doc"]
    c.poly([(-0.7, -0.95), (0.5, -0.95), (0.72, -0.72), (0.72, 0.95), (-0.7, 0.95)], "sheet", 5, "sheet")
    c.poly([(0.5, -0.95), (0.72, -0.72), (0.5, -0.72)], "sheet", 3, "fold")
    p = max(c.px(1), 0.05)
    c.box(-0.5, -0.76, 0.3, -0.76 + p, "inkblue", 4, "head", bevel=False)
    c.box(-0.36, -0.62, 0.16, -0.62 + p, "inkblue", 3, "head", bevel=False)
    for i in range(4):
        c.box(-0.52, -0.38 + i * 0.2, 0.5 - (i % 2) * 0.3, -0.38 + i * 0.2 + p, "dark", 3, "lines", bevel=False)
    if kind == "certificate":
        c.box(-0.52, 0.42, -0.1, 0.78, "sheet", 3, "photo", bevel=False)
        if st == "valid":
            c.arc(0.32, 0.6, 0.22, 0.22, 0, 360, 0.07, "inkblue", flat=4, part="stamp", minpx=2)
            m_metal._star(c, 0.32, 0.6, 0.1, "inkblue", "stamp", tone=4, n=4)
        elif st == "unverified":
            for a in range(0, 360, 60):
                c.arc(0.32, 0.6, 0.22, 0.22, a, a + 30, 0.05, "inkblue", flat=5, part="stamp", minpx=1)
        else:
            c.poly([(-0.7, 0.5), (-0.7, 0.75), (0.72, -0.3), (0.72, -0.55)], "red", 4, "band")
    else:
        c.box(-0.7, 0.3, 0.72, 0.3 + p, "sheet", 2, "perf", bevel=False)
        k = {"accepted": 1, "in_progress": 2, "ready": 3, "delivered": 4, "cancelled": 0}[st]
        for i in range(4):
            on = i < k
            c.box(-0.52 + i * 0.3, 0.5, -0.32 + i * 0.3, 0.7, "inkblue" if on else "sheet", 4 if on else 3, "step%d" % i, bevel=False)
        if st == "ready":
            c.box(0.3, -0.95, 0.5, -0.8, "gold", 5, "tab", bevel=False)
        if st == "cancelled":
            c.poly([(-0.7, 0.5), (-0.7, 0.75), (0.72, -0.3), (0.72, -0.55)], "red", 4, "band")


M = {
    "tool_scale": (tool_scale, 1.75, 0.8, {}), "tool_assay": (tool_assay, 1.1, 1.05, {}), "tool_loupe": (tool_loupe, 0.85, 0.92, {}),
    "tool_jade_light": (tool_jade_light, 0.7, 1.68, {}), "tool_uv": (tool_uv, 0.7, 1.68, {}), "tool_gem_probe": (tool_gem_probe, 1.72, 0.76, {}),
    "manual_organizer": (organizer, 1.1, 1.05, dict(slots=8)), "manual_organizer_large": (organizer, 1.1, 1.05, dict(slots=12)),
    "savings_card": (card, 1.64, 0.86, dict(card="savings")), "credit_card": (card, 1.64, 0.86, dict(card="credit")),
    "card_holder": (card_holder, 1.64, 0.84, {}),
    "mixed_e_scrap": (case, 1.0, 1.05, dict(cols=4, rows=3, mat="dark", fill=["pcb", "gold", "copper", "silver"])),
    "materials_bin": (case, 1.0, 1.05, dict(cols=4, rows=4, mat="plastic", fill=["gold", "silver", "copper", "rock"])),
    "gem_case": (case, 1.0, 1.05, dict(cols=4, rows=4, mat="inkblue", fill=["diamond", "ruby", "sapphire", "emerald", "pearl"])),
    "presentation_case": (case, 1.0, 1.05, dict(cols=4, rows=2, mat="red", fill=["gold", "silver"])),
    "jewelry_display": (case, 1.0, 1.05, dict(cols=4, rows=4, mat="wood", fill=["gold", "ruby", "silver", "emerald", "pearl"])),
    "assay_sample": (jar, 0.8, 1.0, dict(fill="rock", level=0.3)),
    "recovery_batch": (recovery_batch, 1.75, 0.82, {}), "inert_residue": (residue, 1.62, 0.84, {}),
    "process_cartridge": (process_cartridge, 0.8, 1.0, {}), "energy_cell": (energy_cell, 0.8, 1.0, {}),
    "crucible": (crucible, 1.66, 0.82, {}), "ingot_mold": (ingot_mold, 1.6, 0.78, {}),
    "recovery_station": (machine, 1.05, 1.08, dict(machine="recovery")), "refining_furnace": (machine, 1.05, 1.08, dict(machine="furnace")),
    "financial_terminal": (machine, 1.08, 0.98, dict(machine="terminal")), "jeweler_bench": (machine, 1.06, 1.0, dict(machine="bench")),
    "jeweler_toolkit": (toolkit, 1.66, 0.8, {}),
    "jewelry_certificate": (paper_doc, 0.8, 1.0, dict(doc="certificate")), "jewelry_job_ticket": (paper_doc, 0.8, 1.0, dict(doc="ticket")),
}
# Short physical volumes (existing 0.3.1 identities): distinct cover colour and subject emblem each.
for _m, _mat in (("gold", "gold"), ("silver", "silver")):
    for _f, _e in (("ring", "ring"), ("bracelet", "disc"), ("necklace", "tri"), ("earrings", "gem"), ("bar", "bar")):
        MANUAL["%s_%s" % (_m, _f)] = (_mat, _e)
MANUAL.update({"rough_sand": ("skin_yellow", "tri"), "rough_water": ("skin_water", "ring"), "rough_mountain": ("skin_rust", "chip"),
               "jade_pendant": ("dou", "frame"), "jade_bangle": ("bing", "disc")})
MANUAL.update({"gems_a": ("diamond", "gem"), "gems_b": ("sapphire", "gem"), "gems_c": ("pearl", "ring"),
               "mounts_a": ("ruby", "ring"), "mounts_b": ("vio", "disc"), "mounts_c": ("emerald", "tri"),
               "scrap_a": ("pcb", "chip"), "scrap_b": ("dark", "chip"), "ore": ("rock", "tri"),
               "bars_a": ("solder", "bar"), "bars_b": ("brown", "bar")})
for _b in MANUAL:
    M["manual_" + _b] = (manual, 0.86, 1.28, dict(book=_b))
for _m, _f in (("gold", "gold"), ("silver", "silver"), ("copper", "copper")):
    M["sorted_%s_scrap" % _m] = (jar, 0.8, 1.0, dict(fill="pcb" if _m != "copper" else "copper", level=-0.1, tint=_f))
    M["recovered_%s_lot" % _m] = (jar, 0.8, 1.0, dict(fill=_f, level=0.25))
