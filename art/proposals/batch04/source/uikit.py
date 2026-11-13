"""UI drawing kit: paper plates, nine-slice widgets, icons and text helpers (review text only)."""
import math

from PIL import Image, ImageDraw, ImageFont

from px import Canvas, up
import m_metal, m_gem, m_util

PAPER, PAPER_HI, PAPER_SH = (231, 221, 197, 255), (243, 236, 218, 255), (200, 186, 156, 255)
INK, BLUE, GREEN, GOLD, RED = (37, 34, 43, 255), (40, 70, 90, 255), (107, 136, 128, 255), (215, 171, 84, 255), (166, 71, 65, 255)
SHEET, SHEET_SH, SHEET_HI = (197, 196, 176, 255), (160, 160, 140, 255), (214, 213, 196, 255)
HEAD = (62, 102, 144, 255)
OKG = (58, 126, 84, 255)
DARK, DARK2 = (38, 34, 46, 255), (54, 50, 62, 255)
CLEAR = (0, 0, 0, 0)
ZH = "/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc"
EN = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
_fc = {}


def font(size, zh=True):
    k = (size, zh)
    if k not in _fc:
        _fc[k] = ImageFont.truetype(ZH if zh else EN, size)
    return _fc[k]


def new(w, h, fill=CLEAR):
    return Image.new("RGBA", (w, h), fill)


def txt(im, xy, s, size=12, fill=INK, zh=True, anchor="la"):
    d = ImageDraw.Draw(im)
    d.fontmode = "1"
    d.text(xy, s, font=font(size, zh), fill=fill, anchor=anchor)


NOTCH = [7, 23, 41, 58, 77, 96, 118, 139, 161, 187, 211, 236, 262, 289, 317, 346, 377, 409, 442, 476, 511, 547, 584]


def plate(w, h, fill=PAPER, hi=PAPER_HI, sh=PAPER_SH, worn=True, edge=INK):
    """Paper plate with a dark pixel outline and restrained fixed edge wear."""
    im = new(w, h)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, w - 1, h - 1], fill=edge)
    d.rectangle([1, 1, w - 2, h - 2], fill=fill)
    d.line([1, 1, w - 2, 1], fill=hi)
    d.line([1, 1, 1, h - 2], fill=hi)
    d.line([1, h - 2, w - 2, h - 2], fill=sh)
    d.line([w - 2, 1, w - 2, h - 2], fill=sh)
    for x, y in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        im.putpixel((x, y), CLEAR)
    if worn:
        for i, n in enumerate(NOTCH):
            if n < w - 6 and i % 3 == 0:
                d.rectangle([n, 0, n + 2 + i % 2, 0], fill=CLEAR)
                d.line([n, 1, n + 2 + i % 2, 1], fill=edge)
            if n < w - 6 and i % 3 == 1:
                d.rectangle([n, h - 1, n + 3, h - 1], fill=CLEAR)
                d.line([n, h - 2, n + 3, h - 2], fill=edge)
            if n < h - 6 and i % 4 == 2:
                d.rectangle([0, n, 0, n + 2], fill=CLEAR)
                d.line([1, n, 1, n + 2], fill=edge)
            if n < h - 6 and i % 4 == 0:
                d.rectangle([w - 1, n, w - 1, n + 3], fill=CLEAR)
                d.line([w - 2, n, w - 2, n + 3], fill=edge)
        for i, n in enumerate(NOTCH[::2]):                     # faint fibres
            x, y = (n * 7) % max(8, w - 12) + 4, (n * 3) % max(8, h - 8) + 3
            d.line([x, y, x + 3 + i % 3, y], fill=sh)
        d.polygon([(w - 2, h - 9), (w - 9, h - 2), (w - 2, h - 2)], fill=sh)   # folded corner
        d.line([w - 9, h - 2, w - 2, h - 9], fill=edge)
    return im


def inset(d, box, fill=PAPER_SH, edge=INK, hi=None):
    x0, y0, x1, y1 = box
    d.rectangle([x0, y0, x1 - 1, y1 - 1], fill=edge)
    d.rectangle([x0 + 1, y0 + 1, x1 - 2, y1 - 2], fill=fill)
    d.line([x0 + 1, y0 + 1, x1 - 2, y0 + 1], fill=hi or tuple(max(0, c - 22) for c in fill[:3]) + (255,))


def raised(d, box, fill, edge=INK, press=False):
    x0, y0, x1, y1 = box
    li = tuple(min(255, c + 28) for c in fill[:3]) + (255,)
    dk = tuple(max(0, c - 34) for c in fill[:3]) + (255,)
    d.rectangle([x0, y0, x1 - 1, y1 - 1], fill=edge)
    d.rectangle([x0 + 1, y0 + 1, x1 - 2, y1 - 2], fill=fill)
    a, b = (dk, li) if press else (li, dk)
    d.line([x0 + 1, y0 + 1, x1 - 2, y0 + 1], fill=a)
    d.line([x0 + 1, y0 + 1, x0 + 1, y1 - 2], fill=a)
    d.line([x0 + 1, y1 - 2, x1 - 2, y1 - 2], fill=b)
    d.line([x1 - 2, y0 + 1, x1 - 2, y1 - 2], fill=b)
    for x, y in ((x0, y0), (x1 - 1, y0), (x0, y1 - 1), (x1 - 1, y1 - 1)):
        d.point((x, y), fill=CLEAR)


def dither(im, box, col):
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = box
    for y in range(y0, y1):
        for x in range(x0 + (y % 2), x1, 2):
            d.point((x, y), fill=col)


def icon(fn, size=64, hw=1.0, hh=1.0, margin=3):
    c = Canvas(size, size, hw, hh, 0, margin)
    fn(c)
    c.outline()
    return c.image()


def lock(c, x=0, y=0, s=1.0):
    c.arc(x, y - 0.3 * s, 0.3 * s, 0.34 * s, 180, 360, 0.14 * s, "silver", part="shackle", minpx=2)
    c.box(x - 0.5 * s, y - 0.3 * s, x + 0.5 * s, y + 0.5 * s, "gold", 4, "body")
    c.box(x - 0.08 * s, y - 0.05 * s, x + 0.08 * s, y + 0.25 * s, "dark", 1, "key", bevel=False)


def crate(c, x, y, s, mat="wood"):
    c.box(x - s, y - s * 0.7, x + s, y + s * 0.7, mat, 4, "crate")
    c.box(x - s, y - s * 0.12, x + s, y + s * 0.12, mat, 2, "crate", bevel=False)
    c.box(x - s * 0.12, y - s * 0.7, x + s * 0.12, y + s * 0.7, mat, 2, "crate", bevel=False)


def sheetdoc(c, x, y, s):
    c.box(x - 0.5 * s, y - 0.7 * s, x + 0.5 * s, y + 0.7 * s, "sheet", 5, "doc")
    for i in range(3):
        c.box(x - 0.34 * s, y - 0.4 * s + i * 0.3 * s, x + 0.3 * s - (i % 2) * 0.2 * s, y - 0.4 * s + i * 0.3 * s + max(c.px(1), 0.08 * s), "inkblue", 3, "doc", bevel=False)


EVENTS = {
    "assay_campaign": lambda c: (c.box(-0.8, -0.2, 0.4, 0.8, "plastic", 4, "dev"), c.box(-0.62, 0.0, -0.1, 0.4, "screen", 5, "dev", bevel=False), sheetdoc(c, 0.5, -0.3, 0.6), c.arc(0.5, 0.1, 0.2, 0.2, 0, 360, 0.08, "inkblue", flat=4, part="st", minpx=2)),
    "chip_shortage": lambda c: (c.box(-0.9, 0.1, 0.9, 0.8, "dark", 3, "tray"), [c.box(-0.75 + i * 0.58, 0.25, -0.35 + i * 0.58, 0.65, "dark", 1, "cell", bevel=False) for i in range(3)], c.box(-0.72, 0.28, -0.38, 0.62, "vio", 4, "chip"), c.box(-0.4, -0.8, 0.4, -0.1, "vio", 3, "chip2"), c.box(-0.2, -0.6, 0.2, -0.3, "silver", 4, "chip2")),
    "collectors_visit": lambda c: (c.lathe(-0.35, [(-0.8, 0.14), (-0.5, 0.12), (0.0, 0.42), (0.6, 0.3), (0.8, 0.2)], "ceramic", "gloss", "vase"), c.tube([(0.2, 0.2), (0.75, 0.8)], 0.14, "dark", part="h", minpx=3), c.ell(0.1, -0.1, 0.42, 0.42, "silver", part="ring"), c.ell(0.1, -0.1, 0.3, 0.3, "glass", flat=5, part="lens")),
    "credit_squeeze": lambda c: (c.box(-0.7, -0.3, 0.7, 0.35, "vio", 3, "card", r=0.08), c.box(-0.5, -0.12, -0.2, 0.1, "gold", 4, "card"), c.box(-0.9, -0.8, 0.9, -0.45, "silver", 3, "jaw"), c.box(-0.9, 0.5, 0.9, 0.85, "silver", 3, "jaw"), c.box(-0.1, -0.95, 0.1, -0.8, "dark", 3, "screw")),
    "gem_disclosure": lambda c: (sheetdoc(c, 0.25, 0.05, 1.05), m_gem.draw_gem(c, "ruby", "oval", -0.4, 0.35, 0.42, "gem")),
    "gem_freight": lambda c: (crate(c, 0.0, 0.3, 0.8), m_gem.draw_gem(c, "sapphire", "round", 0.0, -0.5, 0.42, "gem")),
    "gold_shipment": lambda c: (crate(c, 0.0, 0.35, 0.82), c.poly([(-0.6, -0.3), (0.3, -0.3), (0.55, -0.7), (-0.35, -0.7)], "gold", 5, "bar"), c.poly([(-0.6, -0.3), (0.3, -0.3), (0.3, -0.1), (-0.6, -0.1)], "gold", 3, "bar")),
    "jade_festival": lambda c: (c.tube([(-0.9, -0.75), (0.9, -0.75)], 0.06, "red", part="cord", minpx=2), [c.poly([(-0.8 + i * 0.55, -0.72), (-0.45 + i * 0.55, -0.72), (-0.62 + i * 0.55, -0.35)], "red" if i % 2 else "gold", 4, "flag") for i in range(3)], c.arc(0, 0.3, 0.5, 0.5, 0, 360, 0.26, "nuo", "gloss", "bi", minpx=4)),
    "jewelry_festival": lambda c: (c.arc(0, 0.25, 0.5, 0.52, 0, 360, 0.14, "gold", part="ring", minpx=3), m_gem.draw_gem(c, "diamond", "round", 0, -0.4, 0.34, "gem"), c.poly([(-0.9, -0.9), (-0.4, -0.75), (-0.6, -0.4)], "red", 4, "rib"), c.poly([(0.9, -0.9), (0.4, -0.75), (0.6, -0.4)], "red", 4, "rib")),
    "new_jade_seam": lambda c: (c.poly([(-0.9, 0.8), (-0.6, -0.2), (0.0, -0.6), (0.7, -0.1), (0.9, 0.8)], "skin_mountain", 3, "rock"), c.poly([(-0.6, -0.2), (0.0, -0.6), (0.1, 0.8), (-0.9, 0.8)], "skin_mountain", 5, "rock"), c.poly([(-0.2, 0.8), (0.1, -0.1), (0.3, -0.2), (0.15, 0.8)], "nuo", 4, "seam"), c.tube([(0.3, -0.9), (0.85, -0.35)], 0.1, "wood", part="pick", minpx=2), c.arc(0.3, -0.9, 0.34, 0.3, -20, 100, 0.1, "silver", part="pick", minpx=2)),
    "port_delay": lambda c: (crate(c, -0.3, 0.4, 0.6), c.poly([(0.2, -0.85), (0.9, -0.85), (0.55, -0.3)], "glass", 5, "hg"), c.poly([(0.2, 0.25), (0.9, 0.25), (0.55, -0.3)], "gold", 4, "hg"), c.box(0.15, -0.95, 0.95, -0.85, "dark", 3, "hg"), c.box(0.15, 0.25, 0.95, 0.35, "dark", 3, "hg")),
    "power_rationing": lambda c: (m_util.energy_cell(c, dict(state="partly_used")), c.seg(-0.9, 0.9, 0.9, -0.9, "red", 4, inside=False, wpx=3)),
    "recovery_grant": lambda c: (sheetdoc(c, 0.3, 0.0, 1.0), c.poly([(-0.5, -0.5), (-0.1, 0.25), (-0.9, 0.25)], "teal", 4, "tri"), c.poly([(-0.5, -0.2), (-0.3, 0.12), (-0.7, 0.12)], "sheet", 5, "tri")),
    "repair_drive": lambda c: (c.box(-0.9, 0.1, 0.5, 0.85, "plastic", 4, "dev", r=0.12), c.box(-0.6, 0.25, 0.1, 0.7, "screen", 4, "dev", bevel=False), c.tube([(0.0, -0.1), (0.7, -0.8)], 0.16, "silver", part="wr", minpx=3), c.arc(0.72, -0.82, 0.2, 0.2, 60, 330, 0.12, "silver", part="wr", minpx=3)),
    "safe_haven": lambda c: (c.box(-0.8, -0.8, 0.8, 0.8, "dark", 3, "safe"), c.box(-0.6, -0.6, 0.6, 0.6, "dark", 4, "door"), c.arc(0.1, 0, 0.26, 0.26, 0, 360, 0.1, "silver", part="dial", minpx=2), c.box(-0.5, 0.2, -0.1, 0.45, "gold", 5, "bar")),
}


def _ring(c):
    c.arc(0, 0, 0.6, 0.6, 0, 360, 0.22, "gold", part="r", minpx=3)


RES = {
    "gold": lambda c: (c.poly([(-0.8, 0.5), (0.5, 0.5), (0.8, -0.2), (-0.5, -0.2)], "gold", 5, "b"), c.poly([(-0.8, 0.5), (0.5, 0.5), (0.5, 0.8), (-0.8, 0.8)], "gold", 3, "b")),
    "silver": lambda c: (c.poly([(-0.8, 0.5), (0.5, 0.5), (0.8, -0.2), (-0.5, -0.2)], "silver", 5, "b"), c.poly([(-0.8, 0.5), (0.5, 0.5), (0.5, 0.8), (-0.8, 0.8)], "silver", 3, "b")),
    "copper": lambda c: (c.ell(0, 0.1, 0.8, 0.5, "copper", part="c"), c.ell(0, -0.1, 0.8, 0.5, "copper", flat=5, part="c")),
    "gem": lambda c: m_gem.draw_gem(c, "sapphire", "round", 0, 0, 0.9, "g"),
    "energy": lambda c: c.poly([(0.2, -0.9), (-0.6, 0.15), (-0.05, 0.15), (-0.25, 0.9), (0.6, -0.2), (0.05, -0.2)], "gold", 5, "e"),
    "capacity": lambda c: (c.box(-0.8, -0.6, 0.8, 0.8, "wood", 4, "box"), c.box(-0.8, -0.6, 0.8, -0.3, "wood", 5, "box"), c.box(-0.2, -0.1, 0.2, 0.2, "gold", 4, "box")),
    "time": lambda c: (c.ell(0, 0, 0.85, 0.85, "silver", flat=4, part="t"), c.ell(0, 0, 0.66, 0.66, "label", flat=6, part="t"), c.seg(0, 0, 0, -0.5, "dark", 1, wpx=1), c.seg(0, 0, 0.35, 0.2, "dark", 1, wpx=1)),
}
GRADE = {
    "dou": lambda c: [c.ell(x, y, 0.3, 0.3, "dou", "gloss", "g%d" % i) for i, (x, y) in enumerate(((-0.4, -0.35), (0.4, -0.35), (0, 0.4)))],
    "nuo": lambda c: c.box(-0.7, -0.7, 0.7, 0.7, "nuo", 4, "g", r=0.3),
    "bing": lambda c: (c.poly([(0, -0.9), (0.8, 0), (0, 0.9), (-0.8, 0)], "bing", 5, "g"), c.poly([(0, -0.9), (0.8, 0), (0, 0)], "bing", 6, "g"), c.poly([(0, 0.9), (-0.8, 0), (0, 0)], "bing", 3, "g")),
}
STATUS = {
    "owned": lambda c: (c.poly([(-0.8, 0.0), (0, -0.8), (0.8, 0.0)], "gold", 5, "s"), c.box(-0.6, 0.0, 0.6, 0.8, "gold", 4, "s")),
    "consigned": lambda c: (c.box(-0.7, -0.4, 0.7, 0.8, "label", 5, "s"), c.arc(0, -0.5, 0.2, 0.2, 0, 360, 0.1, "inkblue", flat=3, part="s", minpx=2)),
    "reserved": lambda c: lock(c, 0, 0.15, 1.3),
    "consumed": lambda c: (c.arc(0, 0, 0.7, 0.7, 0, 360, 0.16, "dark", flat=4, part="s", minpx=2), c.seg(-0.5, 0.5, 0.5, -0.5, "dark", 4, inside=False, wpx=2)),
    "unknown": lambda c: (c.arc(0, -0.3, 0.4, 0.4, 180, 420, 0.2, "inkblue", flat=4, part="s", minpx=2), c.box(-0.1, 0.1, 0.1, 0.4, "inkblue", 4, "s", bevel=False), c.box(-0.12, 0.6, 0.12, 0.84, "inkblue", 4, "s", bevel=False)),
    "confirmed": lambda c: (c.tube([(-0.7, 0.0), (-0.2, 0.55), (0.75, -0.6)], 0.26, "teal", flat=4, part="s", minpx=3),),
}
FEEDBACK = {
    "neutral": lambda c: (c.ell(0, 0, 0.8, 0.7, "label", flat=5, part="b"), c.box(-0.4, -0.05, 0.4, 0.05, "dark", 2, "m", bevel=False)),
    "impatient": lambda c: (c.ell(0, 0, 0.8, 0.7, "label", flat=5, part="b"), [c.ell(-0.4 + i * 0.4, 0, 0.1, 0.1, "gold", flat=4, part="m") for i in range(3)], c.poly([(0.3, 0.6), (0.7, 0.95), (0.6, 0.5)], "label", 5, "b")),
    "withdrawn": lambda c: (c.ell(0, 0, 0.8, 0.7, "label", flat=5, part="b"), c.seg(-0.45, -0.3, 0.45, 0.3, "red", 4, wpx=3), c.seg(-0.45, 0.3, 0.45, -0.3, "red", 4, wpx=3)),
    "satisfied": lambda c: (c.ell(0, 0, 0.8, 0.7, "label", flat=5, part="b"), c.tube([(-0.45, 0.0), (-0.1, 0.3), (0.45, -0.3)], 0.14, "teal", flat=4, part="m", minpx=3)),
}
