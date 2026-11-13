"""Collectibles, electronic scrap, ore, recovered metals and alloys."""
import math

import numpy as np

from px import shade
import m_metal


def vase(c, v):
    s = v.get("scale", 1.0)
    prof = [(-1.05, 0.26), (-0.98, 0.3), (-0.8, 0.2), (-0.55, 0.22), (-0.2, 0.55), (0.25, 0.66), (0.7, 0.5), (0.98, 0.3), (1.05, 0.34)]
    prof = [(y * s, r * s) for y, r in prof]
    x0 = -0.22 * s
    c.lathe(x0, prof, "ceramic", "gloss", "body")
    b = c.sel("body")
    band = b & (c.gy > -0.12 * s) & (c.gy < 0.0 * s) | b & (c.gy > 0.52 * s) & (c.gy < 0.6 * s)
    c.mat[band] = c.mat[band] * 0 + __import__("px").MID["inkblue"]
    ys, xs = np.mgrid[0:c.h, 0:c.w]
    mot = b & (c.gy > 0.12 * s) & (c.gy < 0.4 * s) & (np.sin((c.gx - x0) * 9 / s) * np.sin(c.gy * 14 / s) > 0.45)
    c.mat[mot] = __import__("px").MID["inkblue"]
    c.ell(x0, -1.03 * s, 0.26 * s, 0.07 * s, "ceramic", flat=2, part="mouth")
    # the foot mark is shown as a separate angled medallion beside the vase
    c.ell(0.72 * s, 0.7 * s, 0.26 * s, 0.2 * s, "ceramic", "matte", "basemark")
    c.arc(0.72 * s, 0.7 * s, 0.17 * s, 0.12 * s, 0, 360, c.px(1), "inkblue", flat=3, part="basemark", minpx=1)
    c.box(0.66 * s, 0.66 * s, 0.78 * s, 0.74 * s, "inkblue", 3, "basemark", bevel=False)
    c.anchor("neck_fr", x0 + 0.08 * s, -0.78 * s, "body")
    c.anchor("lip_ul", x0 - 0.26 * s, -1.0 * s, ["body", "mouth"], 225)
    c.anchor("bowl_low", x0 + 0.05 * s, 0.75 * s, "body")
    c.anchor("field_fl", x0 - 0.3 * s, 0.2 * s, "body")
    c.anchor("basemark", 0.72 * s, 0.7 * s, "basemark")


def painting(c, v):
    s = v.get("scale", 1.0)
    c.box(-1.05 * s, -0.9 * s, 1.05 * s, 0.9 * s, "paper", 5, "sheet")
    c.box(-0.85 * s, -0.7 * s, 0.85 * s, 0.6 * s, "sheet", 5, "field", bevel=False)
    f = c.sel("field")
    MID = __import__("px").MID
    hills = f & (c.gy > (0.1 + 0.18 * np.sin(c.gx * 3.1 / s + 0.5)) * s)
    c.mat[hills] = MID["teal"]; c.tone[hills] = 4
    far = f & ~hills & (c.gy > (-0.12 + 0.12 * np.sin(c.gx * 4.3 / s + 2)) * s)
    c.mat[far] = MID["inkblue"]; c.tone[far] = 5
    near = f & (c.gy > (0.38 + 0.1 * np.sin(c.gx * 5.0 / s + 1)) * s)
    c.mat[near] = MID["inkblue"]; c.tone[near] = 3
    c.ell(0.42 * s, -0.4 * s, 0.14 * s, 0.14 * s, "red", flat=4, part="field")
    c.tube([(-0.6 * s, -0.25 * s), (-0.3 * s, -0.45 * s), (0.0 * s, -0.3 * s)], 0.05 * s, "inkblue", flat=2, part="field", minpx=1)
    c.box(0.6 * s, 0.34 * s, 0.78 * s, 0.52 * s, "red", 4, "seal", bevel=False)
    c.box(0.64 * s, 0.38 * s, 0.74 * s, 0.41 * s, "paper", 6, "seal", bevel=False)
    c.box(0.64 * s, 0.44 * s, 0.68 * s, 0.49 * s, "paper", 6, "seal", bevel=False)
    c.anchor("seal", 0.69 * s, 0.43 * s, "seal")
    c.anchor("stroke_c", -0.2 * s, -0.33 * s, "field")
    c.anchor("edge_l", -1.05 * s, -0.1 * s, ["sheet", "field"], 0)
    c.anchor("margin_low", -0.1 * s, 0.75 * s, "sheet")
    c.anchor("field_ul", -0.55 * s, -0.5 * s, "field")


def console(c, v):
    s = v.get("scale", 1.0)
    A, B = (-1.1 * s, -0.2 * s), (0.7 * s, -0.2 * s)
    C, D = (1.1 * s, -0.75 * s), (-0.7 * s, -0.75 * s)
    E, F, G = (-1.1 * s, 0.7 * s), (0.7 * s, 0.7 * s), (1.1 * s, 0.15 * s)
    c.poly([A, B, C, D], "dark", 5, "top")
    c.poly([A, B, F, E], "dark", 3, "front")
    c.poly([B, C, G, F], "dark", 2, "side")
    p = c.px(1)
    for i in range(5):                                   # vent slats on the top face
        x = (-0.2 + i * 0.14) * s
        c.poly([(x, -0.3 * s), (x + 0.07 * s, -0.3 * s), (x + 0.27 * s, -0.62 * s), (x + 0.2 * s, -0.62 * s)], "dark", 2, "vent")
    c.box(-0.95 * s, 0.05 * s, 0.55 * s, 0.14 * s, "red", 3, "front", bevel=False)
    for i in range(2):                                   # input ports with contact rows
        x = (-0.8 + i * 0.55) * s
        c.box(x, 0.28 * s, x + 0.4 * s, 0.5 * s, "dark", 1, "port%d" % i, bevel=False)
        for j in range(5):
            c.box(x + (0.05 + j * 0.07) * s, 0.34 * s, x + (0.05 + j * 0.07) * s + max(p, 0.03 * s), 0.46 * s, "gold", 4, "port%d" % i, bevel=False)
    c.ell(0.42 * s, 0.4 * s, 0.07 * s, 0.07 * s, "screen", flat=5, part="front")
    c.ell(-1.0 * s, 0.6 * s, 0.05 * s, 0.05 * s, "silver", flat=4, part="screw")
    c.poly([(0.84 * s, -0.12 * s), (0.98 * s, -0.31 * s), (0.98 * s, -0.05 * s), (0.84 * s, 0.14 * s)], "dark", 1, "socket")
    c.poly([(0.88 * s, -0.08 * s), (0.94 * s, -0.16 * s), (0.94 * s, -0.06 * s), (0.88 * s, 0.02 * s)], "gold", 4, "socket")
    c.anchor("port", -0.6 * s, 0.4 * s, "port0")
    c.anchor("corner_fr", 0.6 * s, 0.6 * s, "front")
    c.anchor("vent", 0.2 * s, -0.46 * s, ["top", "vent"])
    c.anchor("socket", 0.91 * s, -0.08 * s, ["socket", "side"])
    c.anchor("screw", -1.0 * s, 0.6 * s, ["screw", "front"])


def handheld(c, v):
    s = v.get("scale", 1.0)
    c.box(-1.7 * s, -0.72 * s, 1.7 * s, 0.72 * s, "plastic", 4, "shell", r=0.25 * s)
    c.box(-0.72 * s, -0.5 * s, 0.72 * s, 0.42 * s, "dark", 2, "bezel", bevel=False)
    c.box(-0.64 * s, -0.43 * s, 0.64 * s, 0.35 * s, "screen", 3, "screen", bevel=False)
    for i in range(4):                                   # diagnostic bars, no text
        c.box(-0.5 * s, (-0.32 + i * 0.16) * s, (-0.1 + 0.15 * ((i * 3) % 4)) * s, (-0.26 + i * 0.16) * s, "screen", 5, "screen", bevel=False)
    c.box(-1.38 * s, -0.1 * s, -0.94 * s, 0.06 * s, "dark", 3, "dpad", bevel=False)
    c.box(-1.24 * s, -0.24 * s, -1.08 * s, 0.2 * s, "dark", 3, "dpad", bevel=False)
    c.ell(1.12 * s, 0.08 * s, 0.11 * s, 0.11 * s, "red", "matte", "btn_a")
    c.ell(1.4 * s, -0.14 * s, 0.11 * s, 0.11 * s, "red", "matte", "btn_b")
    c.box(1.0 * s, 0.56 * s, 1.36 * s, 0.68 * s, "dark", 1, "charge", bevel=False)
    c.box(1.08 * s, 0.6 * s, 1.14 * s, 0.66 * s, "gold", 4, "charge", bevel=False)
    c.box(1.22 * s, 0.6 * s, 1.28 * s, 0.66 * s, "gold", 4, "charge", bevel=False)
    c.ell(-0.3 * s, 0.58 * s, 0.05 * s, 0.05 * s, "silver", flat=4, part="screw")
    c.seg(-1.6 * s, 0.4 * s, -0.9 * s, 0.4 * s, None, "-1")
    c.anchor("charge", 1.18 * s, 0.63 * s, "charge")
    c.anchor("display_low", 0.0, 0.2 * s, "screen")
    c.anchor("button_r", 1.4 * s, -0.14 * s, ["btn_b", "shell"])
    c.anchor("seam_ll", -1.62 * s, 0.4 * s, "shell", 0)
    c.anchor("seam_in", -1.4 * s, 0.4 * s, "shell")
    c.anchor("screw", -0.3 * s, 0.58 * s, ["screw", "shell"])


def cartridge(c, v):
    s = v.get("scale", 1.0)
    c.box(-0.78 * s, -0.86 * s, 0.78 * s, 0.9 * s, "dark", 4, "shell")
    c.box(-0.78 * s, -0.96 * s, -0.5 * s, -0.86 * s, "dark", 4, "tab_l", bevel=False)
    c.box(0.5 * s, -0.96 * s, 0.78 * s, -0.86 * s, "dark", 4, "tab_r", bevel=False)
    c.box(-0.6 * s, -0.66 * s, 0.6 * s, 0.34 * s, "label", 5, "label", bevel=False)
    c.box(-0.6 * s, -0.66 * s, 0.6 * s, -0.4 * s, "inkblue", 4, "label", bevel=False)
    c.box(-0.5 * s, -0.6 * s, -0.2 * s, -0.46 * s, "gold", 4, "label", bevel=False)
    c.ell(0.2 * s, 0.0, 0.24 * s, 0.2 * s, "red", flat=4, part="label")
    c.ell(-0.2 * s, 0.06 * s, 0.2 * s, 0.14 * s, "teal", flat=4, part="label")
    for i in range(6):
        c.box((-0.55 + i * 0.19) * s, 0.56 * s, (-0.43 + i * 0.19) * s, 0.84 * s, "gold", 4, "contact%d" % i, bevel=False)
    c.ell(0.68 * s, 0.44 * s, 0.05 * s, 0.05 * s, "silver", flat=4, part="screw")
    c.anchor("label_corner", -0.35 * s, -0.53 * s, "label")
    c.anchor("contacts", -0.11 * s, 0.7 * s, "contact2")
    c.anchor("tab_ur", 0.5 * s, -0.87 * s, "tab_r", 0)
    c.anchor("label_ll", -0.52 * s, 0.26 * s, "label")
    c.anchor("seam_r", 0.69 * s, 0.1 * s, "shell")


def disc(c, v):
    s = v.get("scale", 1.0)
    MID = __import__("px").MID
    m = c.ell(0, 0, 0.95 * s, 0.9 * s, "glass", flat=4, part="disc")
    a = np.degrees(np.arctan2(c.gy, c.gx))
    r = np.hypot(c.gx / 0.95, c.gy / 0.9) / s
    for lo, hi, mat, t in ((-80, -50, "vio", 5), (-50, -20, "glass", 6), (100, 130, "teal", 5), (130, 160, "glass", 6), (20, 40, "vio", 4)):
        g = m & (a > lo) & (a < hi) & (r > 0.36)
        c.mat[g] = MID[mat]; c.tone[g] = t
    lab = m & (a > -170) & (a < -95) & (r > 0.4) & (r < 0.9)
    c.mat[lab] = MID["label"]; c.tone[lab] = 5; c.part[lab] = c.pid("label")
    lb = lab & (r > 0.55) & (r < 0.7)
    c.mat[lb] = MID["inkblue"]; c.tone[lb] = 4
    ring = m & (r > 0.24) & (r < 0.36)
    c.mat[ring] = MID["silver"]; c.tone[ring] = 4; c.part[ring] = c.pid("matrix")
    tick = ring & (np.floor(a / 12) % 3 == 0) & (r > 0.27) & (r < 0.33)
    c.tone[tick] = 2
    c.erase(m & (r < 0.13))
    c.anchor("read_lr", 0.5 * s, 0.42 * s, "disc")
    c.anchor("hub_up", 0.02 * s, -0.16 * s, ["disc", "matrix"])
    c.anchor("outer_l", -0.75 * s, 0.25 * s, "disc")
    c.anchor("matrix", 0.26 * s, 0.1 * s, "matrix")
    c.anchor("label", -0.4 * s, -0.45 * s, "label")


def book(c, v):
    s = v.get("scale", 1.0)
    c.poly([(-1.02 * s, -0.62 * s), (1.02 * s, -0.62 * s), (1.02 * s, 0.6 * s), (0.12 * s, 0.78 * s), (-0.12 * s, 0.78 * s), (-1.02 * s, 0.6 * s)], "leather", 3, "cover")
    c.poly([(-0.94 * s, -0.56 * s), (-0.03 * s, -0.5 * s), (-0.03 * s, 0.62 * s), (-0.94 * s, 0.5 * s)], "paper", 5, "page_l")
    c.poly([(0.94 * s, -0.56 * s), (0.03 * s, -0.5 * s), (0.03 * s, 0.62 * s), (0.94 * s, 0.5 * s)], "paper", 6, "page_r")
    c.poly([(-0.03 * s, -0.5 * s), (0.03 * s, -0.5 * s), (0.03 * s, 0.62 * s), (-0.03 * s, 0.62 * s)], "paper", 2, "gutter")
    p = max(c.px(1), 0.03 * s)
    for i in range(5):
        y = (-0.4 + i * 0.12) * s
        c.box(-0.82 * s, y, (-0.2 - (i % 2) * 0.12) * s, y + p, "inkblue", 3, "page_l", bevel=False)
        c.box(0.16 * s, y, (0.8 - (i % 3) * 0.1) * s, y + p, "inkblue", 3, "page_r", bevel=False)
    c.box(-0.7 * s, 0.26 * s, -0.36 * s, 0.4 * s, "paper", 4, "copyright", bevel=False)
    c.box(-0.64 * s, 0.31 * s, -0.5 * s, 0.31 * s + p, "inkblue", 3, "copyright", bevel=False)
    c.tube([(0.3 * s, 0.34 * s), (0.42 * s, 0.24 * s), (0.5 * s, 0.38 * s), (0.62 * s, 0.26 * s), (0.74 * s, 0.34 * s)], 0.03 * s, "inkblue", flat=2, part="signature", minpx=1)
    c.poly([(0.94 * s, 0.5 * s), (1.0 * s, 0.56 * s), (0.1 * s, 0.72 * s), (0.03 * s, 0.62 * s)], "paper", 3, "fore")
    c.anchor("copyright", -0.53 * s, 0.33 * s, "copyright")
    c.anchor("signature", 0.52 * s, 0.31 * s, ["signature", "page_r"])
    c.anchor("gutter_low", 0.0, 0.4 * s, ["gutter", "page_l", "page_r"])
    c.anchor("spine_low", 0.0, 0.72 * s, "cover")
    c.anchor("fore_lr", 0.96 * s, 0.56 * s, ["fore", "cover", "page_r"], 45)
    c.anchor("fore_in", 0.72 * s, 0.5 * s, ["fore", "page_r"])


def scrap_board(c, v):
    c.box(-1.6, -0.7, 1.6, 0.7, "pcb", 4, "board")
    p = max(c.px(1), 0.03)
    for (x0, y0, x1, y1) in ((-1.3, -0.5, -0.7, 0.0), (-0.3, -0.45, 0.25, -0.1), (0.6, 0.0, 0.95, 0.3)):
        c.box(x0, y0, x1, y1, "dark", 3, "chip_%d" % int((x0 + 2) * 10))
    for i in range(12):
        c.box(-1.4 + i * 0.2, 0.46, -1.28 + i * 0.2, 0.7, "gold", 4, "fingers", bevel=False)
    c.seg(-1.5, 0.22, 0.5, 0.22, "copper", 4)
    c.seg(-0.6, 0.22, -0.6, -0.1, "copper", 4)
    c.seg(0.5, 0.22, 0.5, -0.6, "copper", 4)
    for i in range(6):
        c.box(1.05 + (i % 3) * 0.16, -0.5 + (i // 3) * 0.2, 1.14 + (i % 3) * 0.16, -0.4 + (i // 3) * 0.2, "solder", 5, "pads", bevel=False)
    c.box(1.1, 0.18, 1.52, 0.42, "label", 5, "tag")
    c.box(1.18, 0.27, 1.4, 0.27 + p, "inkblue", 3, "tag", bevel=False)
    c.anchor("chip_ul", -1.0, -0.25, "chip_7")
    c.anchor("contacts_low", -0.6, 0.58, "fingers")
    c.anchor("trace_c", -0.1, 0.22, "board")
    c.anchor("pads_r", 1.25, -0.35, ["pads", "board"])
    c.anchor("tag", 1.31, 0.3, "tag")


def scrap_processor(c, v):
    p = max(c.px(1), 0.05)
    for i in range(8):
        t = -0.63 + i * 0.18
        for (x0, y0, x1, y1), name in (((t, -0.95, t + 0.08, -0.7), "pins_t"), ((t, 0.7, t + 0.08, 0.95), "pins_b"),
                                       ((-0.95, t, -0.7, t + 0.08), "pins_l"), ((0.7, t, 0.95, t + 0.08), "pins_r")):
            c.box(x0, y0, x1, y1, "gold", 4, name, bevel=False)
    c.box(-0.74, -0.74, 0.74, 0.74, "vio", 3, "package")
    c.box(-0.4, -0.3, 0.4, 0.45, "silver", 4, "cap")
    c.box(-0.5, -0.62, 0.1, -0.44, "label", 5, "idblock", bevel=False)
    c.box(-0.44, -0.56, -0.1, -0.56 + p, "inkblue", 3, "idblock", bevel=False)
    c.anchor("pins_ll", -0.4, 0.84, "pins_b", 0)
    c.anchor("cap", 0.0, 0.08, "cap")
    c.anchor("corner_fr", 0.72, 0.72, "package", 45)
    c.anchor("top_l", -0.56, 0.1, "package")
    c.anchor("idblock", -0.2, -0.53, "idblock")


def scrap_connectors(c, v):
    p = max(c.px(1), 0.05)
    c.box(-0.9, -0.1, 0.5, 0.35, "dark", 4, "housing")
    for i in range(7):
        x = -0.82 + i * 0.19
        c.box(x, 0.35, x + 0.09, 0.9, "gold", 4, "tips_l" if i < 3 else "tips", bevel=False)
        c.box(x, -0.02, x + 0.09, 0.12, "gold", 5, "housing", bevel=False)
    c.box(0.5, -0.02, 0.92, 0.3, "silver", 4, "cluster")
    c.box(0.58, 0.3, 0.84, 0.6, "silver", 3, "cluster")
    c.box(-0.2, -0.86, 0.5, -0.5, "plastic", 4, "tagcomp")
    for i in range(3):
        c.box(-0.1 + i * 0.2, -0.5, 0.0 + i * 0.2, -0.3, "silver", 4, "tagcomp", bevel=False)
    c.box(0.5, -0.9, 0.9, -0.62, "label", 5, "tag")
    c.box(0.58, -0.78, 0.8, -0.78 + p, "inkblue", 3, "tag", bevel=False)
    c.anchor("row_front", 0.2, 0.78, "tips", 0)
    c.anchor("group_l", -0.6, 0.6, "tips_l")
    c.anchor("cluster_r", 0.72, 0.16, "cluster")
    c.anchor("ends_low", 0.1, 0.5, "tips")
    c.anchor("tag", 0.7, -0.76, "tag")


def _rock(c, x, y, rx, ry, mat, part, seed):
    gx, gy = (c.gx - x) / rx, (c.gy - y) / ry
    th = np.arctan2(gy, gx)
    R = 1 + 0.12 * np.cos(3 * th + seed) + 0.07 * np.cos(5 * th + seed * 2.3)
    R = np.floor(R * 8 + 0.5) / 8
    m = np.hypot(gx, gy) <= R
    nx, ny = np.round(np.clip(gx / R, -1, 1) * 2) / 2, np.round(np.clip(gy / R, -1, 1) * 2) / 2
    c.put(m, mat, shade(nx, ny, None, "matte"), part)
    return m


def ore(c, v):
    kind = v["ore"]
    base = {"gold": "quartz", "silver": "rock", "copper": "rock"}[kind]
    c.poly([(-1.6, 0.2), (1.6, 0.2), (1.4, 0.75), (-1.4, 0.75)], "dark", 3, "tray")
    c.box(-1.66, 0.1, 1.66, 0.24, "dark", 5, "tray", bevel=False)
    MID = __import__("px").MID
    ys, xs = np.mgrid[0:c.h, 0:c.w]
    frs = [(-0.85, -0.12, 0.62, 0.5, 0.3), (0.2, -0.25, 0.7, 0.58, 1.9), (1.05, 0.0, 0.46, 0.38, 3.1)]
    for i, (x, y, rx, ry, sd) in enumerate(frs):
        m = _rock(c, x, y, rx, ry, base, "frag%d" % i, sd)
        m &= c.gy < 0.24
        if kind == "gold":
            g = m & (np.sin(c.gx * 7 + c.gy * 5 + sd) > 0.6)
            c.mat[g] = MID["rock"]
            sp = m & ((xs * 7 + ys * 11) % 37 == 0)
            c.mat[sp] = MID["gold"]; c.tone[sp] = 5
        elif kind == "silver":
            g = m & (np.abs(np.sin(c.gx * 6 - c.gy * 7 + sd)) < 0.12)
            c.mat[g] = MID["silver"]; c.tone[g] = 5
        else:
            g = m & (np.sin(c.gx * 5 + sd) * np.sin(c.gy * 6 + sd) > 0.3)
            c.mat[g] = MID["malachite"]
            g2 = m & ((xs * 5 + ys * 9) % 29 == 0)
            c.mat[g2] = MID["copper"]; c.tone[g2] = 4
    c.erase(c.sel("frag0", "frag1", "frag2") & (c.gy >= 0.24) & False)
    p = max(c.px(1), 0.03)
    c.box(-1.5, 0.34, -1.0, 0.66, "label", 5, "tag")
    c.box(-1.42, 0.44, -1.1, 0.44 + p, "inkblue", 3, "tag", bevel=False)
    c.box(-1.42, 0.54, -1.2, 0.54 + p, "inkblue", 3, "tag", bevel=False)
    c.box(1.05, -0.5, 1.4, -0.3, "label", 5, "fragtag")
    c.anchor("band_front", 0.2, -0.2, "frag1")
    c.anchor("ridge", 0.1, -0.6, "frag1")
    c.anchor("seam", -0.28, 0.0, ["frag0", "frag1"])
    c.anchor("tag", -1.25, 0.5, "tag")
    c.anchor("zone_fl", -0.9, -0.1, "frag0")
    c.anchor("container_low", 0.3, 0.3, "tray")
    c.anchor("fragtag", 1.22, -0.4, "fragtag")
    c.anchor("frag_r", 1.05, 0.0, "frag2")
    c.anchor("recess", -0.7, -0.3, "frag0")


def cast_bar(c, v):
    m_metal.bar(c, v)


T = {
    "antique_vase_neck_crack": [("crack", "neck_fr", dict(L=0.34, ang=75))],
    "antique_vase_lip_chip": [("bite", "lip_ul", dict(r=0.11, shape="round", out=0.3, face=5, mat="quartz"))],
    "antique_vase_restoration_join": [("stripe", "bowl_low", dict(L=0.6, ang=-8, wid=0.03, dt=1)), ("stain", "bowl_low", dict(rx=0.26, ry=0.1, dt=1, dither="thin"))],
    "antique_vase_false_crazing": [("halftone", "field_fl", dict(rw=0.34, rh=0.3, dt=-2))],
    "antique_vase_base_mark_recut": [("ghost", "basemark", dict(rw=0.4, rh=0.3))],
    "painting_seal_mismatch": [("shift", "seal", dict(rw=0.1, rh=0.05, dx=2, dy=3))],
    "painting_print_screen": [("halftone", "stroke_c", dict(rw=0.5, rh=0.3, dt=2))],
    "painting_canvas_tear": [("bite", "edge_l", dict(r=0.14, shape="wedge", ang=0, face=3))],
    "painting_water_tide": [("stripe", "margin_low", dict(L=1.0, ang=4, wid=0.03, mat="brown", flat=5)), ("stain", "margin_low", dict(rx=0.5, ry=0.06, dt=-1, dither="thin"))],
    "painting_overpaint_patch": [("stain", "field_ul", dict(rx=0.16, ry=0.12, dt=-1, dither="thin"))],
    "console_port_contacts": [("shift", "port", dict(rw=0.12, rh=0.14, dx=1, dy=-2))],
    "console_shell_fracture": [("crack", "corner_fr", dict(L=0.42, ang=205, lip=2))],
    "console_vent_heat": [("stain", "vent", dict(rx=0.26, ry=0.14, mat="brown", dt=-1))],
    "console_power_socket": [("shift", "socket", dict(rw=0.1, rh=0.14, dx=-2, dy=2))],
    "console_service_screw": [("slot", "screw", dict(rx=0.05, ry=0.03)), ("scratch", "screw", dict(n=2, L=0.2, ang=-30, gap=4))],
    "handheld_charging_contacts": [("shift", "charge", dict(rw=0.26, rh=0.07, dx=2, dy=1))],
    "handheld_display_line": [("stripe", "display_low", dict(L=1.4, ang=0, wid=0.02, flat=1))],
    "handheld_stuck_button": [("shift", "button_r", dict(rw=0.26, rh=0.26, dx=0, dy=2))],
    "handheld_shell_split": [("bite", "seam_ll", dict(r=0.09, shape="wedge", ang=0, face=2)), ("crack", "seam_in", dict(L=0.3, ang=0, branch=False))],
    "handheld_battery_cover_rework": [("slot", "screw", dict(rx=0.05, ry=0.03)), ("scratch", "screw", dict(n=2, L=0.2, ang=20, gap=4))],
    "cartridge_label_misregister": [("shift", "label_corner", dict(rw=0.34, rh=0.18, dx=2, dy=2))],
    "cartridge_contact_loss": [("patch", "contacts", dict(rx=0.06, ry=0.09, mat="slag", lip=False))],
    "cartridge_shell_tab": [("bite", "tab_ur", dict(shape="flat", ang=0, face=3))],
    "cartridge_label_lift": [("crack", "label_ll", dict(L=0.2, ang=-45, branch=False, zig=False, lip=2)), ("stain", (-0.56, 0.3, "label"), dict(rx=0.05, ry=0.05, dt=-2, dither=False))],
    "cartridge_case_reseal": [("stripe", "seam_r", dict(L=0.6, ang=90, wid=0.03, dt=2)), ("scratch", (0.68, 0.44, "shell"), dict(n=2, L=0.14, ang=30, gap=3))],
    "disc_radial_score": [("scratch", "read_lr", dict(n=1, L=0.4, ang=40, lip=True))],
    "disc_hub_fissure": [("crack", "hub_up", dict(L=0.3, ang=-80, branch=False))],
    "disc_layer_delamination": [("stain", "outer_l", dict(rx=0.1, ry=0.26, mat="quartz", dt=0))],
    "disc_matrix_mismatch": [("shift", "matrix", dict(rw=0.12, rh=0.1, dx=2, dy=1))],
    "disc_label_resurface": [("ghost", "label", dict(rw=0.5, rh=0.3, dx=2, dy=1))],
    "book_reprint_notice": [("seam", "copyright", dict(L=0.12, ang=0, mat="red", wpx=1, beads=False))],
    "book_signature_copy": [("ghost", "signature", dict(rw=0.56, rh=0.24, dx=1, dy=1))],
    "book_missing_leaf": [("stripe", "gutter_low", dict(L=0.3, ang=90, wid=0.05, flat=6)), ("gapline", (0.04, 0.4, None), dict(L=0.3, ang=90, lip=0))],
    "book_spine_split": [("crack", "spine_low", dict(L=0.1, ang=90, branch=False, wide=True))],
    "book_water_warp": [("bite", "fore_lr", dict(r=0.08, shape="round", out=0.6, face=3)), ("stain", "fore_in", dict(rx=0.2, ry=0.1, mat="brown", flat=5, dither=True))],
    "scrap_board_missing_chip": [("stain", "chip_ul", dict(rx=0.34, ry=0.3, mat="pcb", flat=3, dither=False, wob=0)), ("specks", "chip_ul", dict(n=9, sc=2, mat="solder", tone=5))],
    "scrap_board_contact_stripping": [("stripe", "contacts_low", dict(L=0.7, ang=0, wid=0.2, mat="slag", flat=3))],
    "scrap_board_burned_trace": [("stripe", "trace_c", dict(L=0.4, ang=0, wid=0.06, mat="slag", flat=1)), ("stain", "trace_c", dict(rx=0.2, ry=0.08, mat="brown", dt=-1))],
    "scrap_board_corroded_pad": [("stain", "pads_r", dict(rx=0.2, ry=0.14, mat="teal", dt=0)), ("specks", "pads_r", dict(n=6, sc=1, tone=1))],
    "scrap_board_false_grade_label": [("xmark", "tag", dict(r=0.07))],
    "scrap_processor_missing_pins": [("gap", "pins_ll", dict(w=20, L=0.5, ang=0))],
    "scrap_processor_cap_removed": [("recess", "cap", dict(rx=0.3, ry=0.28))],
    "scrap_processor_package_fracture": [("bite", "corner_fr", dict(r=0.22, shape="wedge", ang=225, face=5))],
    "scrap_processor_scorched_package": [("stain", "top_l", dict(rx=0.14, ry=0.26, mat="slag", dt=-1))],
    "scrap_processor_reprinted_grade": [("ghost", "idblock", dict(rw=0.5, rh=0.16, dx=1, dy=2))],
    "scrap_connectors_clipped_contacts": [("gap", "row_front", dict(w=8, L=0.75, ang=0))],
    "scrap_connectors_stripped_plating": [("stripe", "group_l", dict(L=0.5, ang=0, wid=0.3, mat="base", dt=0))],
    "scrap_connectors_polymer_contamination": [("patch", "cluster_r", dict(rx=0.17, ry=0.14, mat="dark", lip=False))],
    "scrap_connectors_oxidized_contact": [("specks", "ends_low", dict(n=9, sc=2, mat="teal", tone=2))],
    "scrap_connectors_false_sorting": [("xmark", "tag", dict(r=0.07))],
    "refined_gold_bar_purity_shortfall": [], "refined_gold_bar_short_pour": [],
    "refined_gold_bar_cast_pores": [("specks", "face_ul", dict(n=9, sc=1, tone=2, lip=True))],
    "refined_gold_bar_slag_inclusion": [("patch", "face_fr", dict(rx=0.14, ry=0.08, mat="slag", lip=True))],
    "refined_gold_bar_traceability_mismatch": [("xmark", "stamp", dict(r=0.07, mat="red"))],
    "refined_silver_bar_assay_deficit": [], "refined_silver_bar_short_mass": [],
    "refined_silver_bar_shrink_cavity": [("slot", "face_up_c", dict(rx=0.14, ry=0.05))],
    "refined_silver_bar_slag_seam": [("stripe", "edge_low", dict(L=0.8, ang=0, wid=0.05, mat="slag", flat=2)), ("specks", "edge_low", dict(n=5, sc=2, tone=4))],
    "refined_silver_bar_batch_restrike": [("ghost", "stamp", dict(rw=0.5, rh=0.24, dx=2, dy=1))],
    "refined_copper_ingot_low_copper_fraction": [], "refined_copper_ingot_short_fill": [],
    "refined_copper_ingot_cast_lap": [("crack", "face_fr", dict(L=0.3, ang=10, branch=False, zig=False, lip=0)), ("stripe", "face_fr", dict(L=0.3, ang=10, wid=0.05, dt=1))],
    "refined_copper_ingot_trapped_slag": [("patch", "face_ul", dict(rx=0.14, ry=0.07, mat="slag", lip=True))],
    "refined_copper_ingot_misidentified_batch": [("xmark", "tag", dict(r=0.07))],
    "ore_gold_low_gold_grade": [], "ore_silver_low_silver_grade": [], "ore_copper_low_copper_grade": [],
    "ore_gold_waste_rock_band": [("stripe", "band_front", dict(L=1.0, ang=-35, wid=0.16, mat="rock", flat=3))],
    "ore_gold_surface_gilding": [("stain", "ridge", dict(rx=0.26, ry=0.08, mat="gold", flat=5, dither=False))],
    "ore_gold_added_weight": [("stripe", "seam", dict(L=0.5, ang=80, wid=0.06, mat="solder", flat=3))],
    "ore_gold_mislabelled_origin": [("xmark", "tag", dict(r=0.07))],
    "ore_silver_coarse_gangue": [("stain", "zone_fl", dict(rx=0.3, ry=0.22, mat="quartz", dt=0)), ("specks", "zone_fl", dict(n=8, sc=2, tone=2))],
    "ore_silver_painted_sheen": [("stain", "ridge", dict(rx=0.3, ry=0.09, mat="silver", flat=5, dither=False))],
    "ore_silver_moisture_mass": [("stripe", "container_low", dict(L=1.4, ang=0, wid=0.08, dt=-2))],
    "ore_silver_mixed_source": [("stain", "frag_r", dict(rx=0.3, ry=0.24, mat="skin_rust", dt=0, dither=False)), ("xmark", "fragtag", dict(r=0.05))],
    "ore_copper_barren_matrix": [("stain", "band_front", dict(rx=0.46, ry=0.3, mat="quartz", dt=0, dither=False))],
    "ore_copper_applied_green_coating": [("stain", "recess", dict(rx=0.24, ry=0.16, mat="dye", dt=0, dither=False))],
    "ore_copper_foreign_aggregate": [("patch", "frag_r", dict(rx=0.2, ry=0.16, mat="slag", lip=True))],
    "ore_copper_source_tag_mismatch": [("xmark", "tag", dict(r=0.07))],
    "alloy_gold_bar_karat_shortfall": [], "alloy_gold_bar_short_weight": [],
    "alloy_gold_bar_segregated_face": [("stripe", "face_low", dict(L=1.0, ang=0, wid=0.1, dt=-1, edge=True))],
    "alloy_gold_bar_cast_crack": [("crack", "corner_ur", dict(L=0.26, ang=160, branch=False, lip=2))],
    "alloy_gold_bar_altered_karat_stamp": [("ghost", "stamp", dict(rw=0.6, rh=0.3))],
    "alloy_silver_bar_fineness_shortfall": [], "alloy_silver_bar_weight_deficit": [],
    "alloy_silver_bar_inhomogeneous_cast": [("halftone", "face_up_c", dict(rw=0.6, rh=0.14, dt=-2))],
    "alloy_silver_bar_edge_fissure": [("crack", "edge_right", dict(L=0.4, ang=180, branch=False))],
    "alloy_silver_bar_restamped_fineness": [("ghost", "stamp", dict(rw=0.6, rh=0.3))],
}

M = {
    "collectible_vase": (vase, 1.02, 1.12, {}),
    "collectible_painting": (painting, 1.1, 0.95, {}),
    "collectible_console": (console, 1.15, 0.8, {}),
    "collectible_handheld": (handheld, 1.74, 0.76, {}),
    "collectible_cartridge": (cartridge, 0.82, 1.0, {}),
    "collectible_disc": (disc, 1.0, 0.95, {}),
    "collectible_book": (book, 1.06, 0.82, {}),
    "scrap_board": (scrap_board, 1.66, 0.74, {}),
    "scrap_processor": (scrap_processor, 1.0, 1.0, {}),
    "scrap_connectors": (scrap_connectors, 0.98, 0.98, {}),
    "ore_gold": (ore, 1.7, 0.84, dict(ore="gold")),
    "ore_silver": (ore, 1.7, 0.84, dict(ore="silver")),
    "ore_copper": (ore, 1.7, 0.84, dict(ore="copper")),
    "refined_gold_bar": (cast_bar, 1.8, 0.8, dict(mat="gold", bar=(1.3, 0.46, 0.7, 0.14, "cast"))),
    "refined_silver_bar": (cast_bar, 1.8, 0.8, dict(mat="silver", bar=(1.15, 0.58, 0.6, 0.18, "cast"))),
    "refined_copper_ingot": (cast_bar, 1.8, 0.8, dict(mat="copper", bar=(1.45, 0.42, 0.78, 0.22, "tag"))),
    "alloy_gold18k": (cast_bar, 1.8, 0.8, dict(mat="gold", bar=(1.58, 0.36, 0.5, 0.04, "orbit"))),
    "alloy_silver925": (cast_bar, 1.8, 0.8, dict(mat="silver", bar=(1.42, 0.44, 0.44, 0.06, "starburst"))),
}
