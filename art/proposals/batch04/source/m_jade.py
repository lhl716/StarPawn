"""Jade: six rough families (hidden tier) and five finished forms x two designs (visible tier)."""
import math

import numpy as np

from px import shade, ellipse_pts

TIERS = ("dou", "nuo", "bing")


def jade_tex(c, m, tier):
    ys, xs = np.mgrid[0:c.h, 0:c.w]
    if tier == "dou":                                    # coarse, dull, visibly grainy
        c.add(m & (c.tone == 6), -1)
        c.add(m & (((xs * 7 + ys * 13) % 11 == 0) | ((xs * 5 + ys * 3) % 17 == 0)), -1)
        c.add(m & ((xs * 3 + ys * 11) % 19 == 0), 1, hi=5)
    elif tier == "nuo":                                  # even, soft, a few mottles
        c.add(m & ((xs * 7 + ys * 13) % 31 == 0), -1)
    else:                                                # bing: clear, light passes through
        c.add(m & (c.tone <= 2), 1)
        c.add(m & (c.tone == 4) & ((xs + ys) % 9 == 0), 1)


# family: (radial harmonics, squash, window (x,y,rx,ry), texture id)
ROUGH = {
    "yellow": ([(2, 0.10, 0.4), (3, 0.06, 1.9), (5, 0.03, 0.7)], (1.0, 0.82), (-0.32, 0.12, 0.3, 0.24)),
    "black": ([(2, 0.16, 1.2), (4, 0.05, 0.3), (7, 0.02, 2.0)], (0.86, 0.98), (0.02, 0.2, 0.3, 0.27)),
    "water": ([(2, 0.05, 2.4), (3, 0.03, 0.2)], (1.0, 0.7), (0.3, -0.05, 0.3, 0.22)),
    "white": ([(3, 0.12, 0.9), (4, 0.06, 2.6), (6, 0.03, 1.1)], (0.95, 0.9), (-0.25, -0.2, 0.3, 0.26)),
    "rust": ([(2, 0.12, 2.9), (5, 0.07, 1.4), (8, 0.02, 0.4)], (1.0, 0.86), (0.3, 0.26, 0.3, 0.24)),
    "mountain": ([(3, 0.14, 0.2), (4, 0.10, 1.7), (9, 0.04, 2.2)], (0.92, 0.95), (-0.05, -0.2, 0.28, 0.27)),
}
# surface anchors per family: name -> (x, y[, outward angle])
ROUGH_ANCH = {
    "yellow": dict(a1=(0.48, -0.2), a2=(-0.42, -0.42)),
    "black": dict(a1=(0.1, -0.5), a2=(-0.45, 0.45)),
    "water": dict(a1=(-0.9, -0.1, 180), a2=(-0.3, 0.05), a3=(0.72, 0.28)),
    "white": dict(a1=(0.45, 0.4), a2=(-0.1, 0.5)),
    "rust": dict(a1=(-0.2, -0.3), a2=(-0.5, -0.45), a3=(-0.7, 0.25)),
    "mountain": dict(a1=(0.3, 0.1), a2=(-0.1, 0.55)),
}


def rough(c, v):
    fam = v["family"]
    harm, (sx, sy), (wx, wy, wrx, wry) = ROUGH[fam]
    s = v.get("scale", 1.0)
    mat = "skin_" + fam
    gx, gy = c.gx / (sx * s), c.gy / (sy * s)
    th = np.arctan2(gy, gx)
    R = np.ones_like(th)
    for kk, a, ph in harm:
        R += a * np.cos(kk * th + ph)
    if fam == "mountain":                                # angular crag: quantised radius
        R = np.floor(R * 9 + 0.5) / 9
    d = np.hypot(gx, gy) / R
    m = d <= 1.0
    nx, ny = gx / R, gy / R
    if fam == "mountain":
        nx, ny = np.round(nx * 2.5) / 2.5, np.round(ny * 2.5) / 2.5
    t = shade(np.clip(nx, -1, 1), np.clip(ny, -1, 1), None, "matte")
    c.put(m, mat, t, "skin")
    ys, xs = np.mgrid[0:c.h, 0:c.w]
    k = 1 if c.lod else 1
    if fam == "yellow":
        c.add(m & ((xs * 3 + ys * 5) % 7 == 0), -1)
        c.add(m & ((xs * 5 + ys * 3) % 11 == 0), 1, hi=5)
    elif fam == "black":
        c.add(m & ((xs * 7 + ys * 3) % 23 == 0), 2, hi=5)
        c.add(m & (np.abs(gx * 0.8 + gy + 0.35) < c.px(1.2) / s) & (d < 0.8), 1)
    elif fam == "water":
        c.add(m & (((gx + 0.3) / 0.45) ** 2 + ((gy - 0.05) / 0.3) ** 2 < 1) & (c.tone < 5), 1)
    elif fam == "white":
        c.add(m & ((xs // 2 + ys // 2) % 3 == 0) & ((xs + ys) % 2 == 0), -1)
    elif fam == "rust":
        c.add(m & (np.sin(gy * 9 + gx * 3) > 0.55), -1)
        c.add(m & ((xs * 5 + ys * 7) % 13 == 0), 1, hi=5)
    else:
        c.add(m & ((xs * 5 + ys * 9) % 23 == 0), -1)
        g = m & (np.sin(gx * 5 - gy * 4) > 0.8)
        c.mat[g] = c.mat[g]                               # facets only; no colour hint of interior
    tier = v.get("tier")
    wx, wy, wrx, wry = wx * s, wy * s, wrx * s, wry * s
    if v.get("cut") and tier:
        cm = (((c.gx - 0.25 * s) / (0.62 * s)) ** 2 + ((c.gy - 0.05 * s) / (0.6 * s)) ** 2 <= 1) & m
        rim = cm & ~((((c.gx - 0.25 * s) / (0.62 * s - c.px(2))) ** 2 + ((c.gy - 0.05 * s) / (0.6 * s - c.px(2))) ** 2) <= 1)
        c.put(cm, tier, 4, "cut")
        c.add(cm & (ys % 4 == 0), 1)
        jade_tex(c, cm & ~rim, tier)
        c.put(rim, mat, 5, "cutrim")
        c.anchor("win_c", 0.25 * s, 0.05 * s, "cut")
    elif v.get("window") and tier:
        wm = (((c.gx - wx) / wrx) ** 2 + ((c.gy - wy) / wry) ** 2 <= 1) & m
        nxw, nyw = (c.gx - wx) / wrx, (c.gy - wy) / wry
        c.put(wm, tier, shade(nxw * 0.7, nyw * 0.7, None, "gloss"), "window")
        inner = (((c.gx - wx) / (wrx - c.px(1.5))) ** 2 + ((c.gy - wy) / (wry - c.px(1.5))) ** 2 <= 1) & m
        jade_tex(c, inner, tier)
        c.put(wm & ~inner, mat, 2, "winrim")
    W = ["window"]
    c.anchor("win_c", wx, wy, W)
    c.anchor("win_l", wx - wrx * 0.5, wy, W)
    c.anchor("win_r", wx + wrx * 0.55, wy, W)
    c.anchor("win_u", wx, wy - wry * 0.45, W)
    c.anchor("win_d", wx, wy + wry * 0.55, W)
    c.anchor("win_rim_r", wx + wrx * 0.72, wy - wry * 0.1, W)
    c.anchor("win_rim_d", wx - wrx * 0.1, wy + wry * 0.72, W)
    for name, a in ROUGH_ANCH[fam].items():
        c.anchor(name, a[0] * s, a[1] * s, "skin", a[2] if len(a) > 2 else 0)


def _gloss_poly(c, pts, tier, part, cx=0, cy=0, rx=1, ry=1):
    m = c.polymask(pts)
    t = shade(np.clip((c.gx - cx) / rx, -1, 1) * 0.8, np.clip((c.gy - cy) / ry, -1, 1) * 0.8, None, "gloss")
    c.put(m, tier, t, part)
    return m


def pendant(c, v):
    tier, crafted = v["tier"], v["design"] == "crafted"
    if crafted:                                          # leaf with carved veins
        pts = [(0, -0.95), (0.42, -0.6), (0.6, -0.05), (0.42, 0.55), (0, 0.95), (-0.42, 0.55), (-0.6, -0.05), (-0.42, -0.6)]
        m = _gloss_poly(c, pts, tier, "body", 0, 0, 0.6, 0.95)
        for sgn in (-1, 1):
            for yy in (-0.2, 0.15, 0.5):
                c.seg(0, yy, sgn * 0.36, yy - 0.22, None, "-1")
        c.seg(0, -0.5, 0, 0.8, None, "-2")
        c.anchor("groove", 0.02, 0.15, "body")
    else:                                                # rounded safety tablet
        m = c.box(-0.52, -0.92, 0.52, 0.92, tier, 4, "body", bevel=False, r=0.26)
        c.tone[m] = shade(np.clip(c.gx / 0.52, -1, 1) * 0.8, np.clip(c.gy / 0.92, -1, 1) * 0.8, None, "gloss")[m]
        c.arc(0, 0.2, 0.26, 0.3, 200, 520, c.px(1), tier, flat=2, part="body", minpx=1)
        c.arc(0, 0.2, 0.12, 0.14, 0, 360, c.px(1), tier, flat=2, part="body", minpx=1)
        c.anchor("groove", 0.0, 0.5, "body")
    jade_tex(c, m, tier)
    c.ell(0, -0.68, 0.08, 0.08, tier, flat=1, part="eye")
    c.erase(((c.gx / 0.045) ** 2 + ((c.gy + 0.68) / 0.045) ** 2) <= 1)
    c.anchor("eye", 0.05, -0.6, "body")
    c.anchor("eye_low", -0.06, -0.52, "body")
    c.anchor("edge_ll", -0.42 if crafted else -0.45, 0.55 if crafted else 0.82, "body", 135)
    c.anchor("center", 0.1, -0.15, "body")


def cabochon(c, v):
    tier, crafted = v["tier"], v["design"] == "crafted"
    if crafted:                                          # marquise dome with a carved rim line
        m = (((c.gx) / 0.98) ** 2 + ((c.gy - 0.55) / 1.1) ** 2 <= 1) & (((c.gx) / 0.98) ** 2 + ((c.gy + 0.55) / 1.1) ** 2 <= 1)
        c.put(m, tier, shade(c.gx / 0.98 * 0.9, np.clip(c.gy / 0.55, -1, 1) * 0.9, None, "gloss"), "dome")
        c.arc(0, 0.55, 0.8, 0.92, 215, 325, c.px(1), tier, flat=2, part="dome", minpx=1)
    else:
        m = c.ell(0, 0, 0.9, 0.66, tier, "gloss", "dome")
        g = m & (c.gy > 0.4)
        c.add(g & ((c.gx / 0.9) ** 2 + ((c.gy - 0.02) / 0.6) ** 2 > 1), -1)
    c.ell(-0.32, -0.26, 0.2, 0.09, tier, flat=6, part="dome", rot=-0.3)
    jade_tex(c, m, tier)
    c.anchor("dome_fr", 0.3, -0.12, "dome")
    c.anchor("girdle_ll", -0.6, 0.42 if not crafted else 0.26, "dome", 135)
    c.anchor("dome_ul", -0.3, -0.02, "dome")
    c.anchor("dome_low", 0.0, 0.3, "dome")
    c.anchor("rim_r", 0.62, 0.02, "dome")


def bangle(c, v):
    tier, crafted = v["tier"], v["design"] == "crafted"
    if crafted:                                          # flat-profile band with carved notches
        c.arc(0, 0, 1.5, 0.62, 0, 360, 0.2, tier, "gloss", "arc", minpx=4)
        c.arc(0, 0.09, 1.5, 0.62, 0, 180, 0.2, tier, "gloss", "arc", minpx=4)
        for a in range(20, 170, 25):
            x, y = 1.5 * math.cos(math.radians(a)), 0.62 * math.sin(math.radians(a))
            c.seg(x, y - 0.02, x, y + 0.16, None, "-1")
    else:
        c.arc(0, 0, 1.45, 0.62, 0, 360, 0.3, tier, "gloss", "arc", minpx=5)
    jade_tex(c, c.sel("arc"), tier)
    c.anchor("arc_fr", 0.9, 0.42, "arc")
    c.anchor("inner_ul", -0.92, -0.36, "arc", 30)
    c.anchor("arc_fl", -0.85, 0.52, "arc")
    c.anchor("arc_fc", 0.05, 0.63, "arc")
    c.anchor("arc_br", 1.0, -0.47, "arc")


def beads(c, v):
    tier, crafted = v["tier"], v["design"] == "crafted"
    n = 20 if crafted else 16
    pts = ellipse_pts(0, -0.1, 0.82, 0.74, n, a0=-90 + 180.0 / n, a1=270 + 180.0 / n)
    order = sorted(range(n), key=lambda i: pts[i][1])
    for i in order:
        x, y = pts[i]
        f = (y + 0.84) / 1.48
        r = (0.09 + 0.07 * f) if crafted else (0.13 + 0.035 * f)
        if crafted and i % 5 == 2:
            c.ell(x, y, r * 0.55, r * 0.9, tier, "gloss", "spacer")
        else:
            c.ell(x, y, r, r, tier, "gloss", "bead%d" % i)
    if crafted:                                          # carved guru bead and jade drop, no metal
        c.ell(0, 0.72, 0.2, 0.2, tier, "gloss", "guru")
        c.seg(-0.14, 0.72, 0.14, 0.72, None, "-2")
        c.ell(0, 1.0, 0.09, 0.14, tier, "gloss", "drop")
    jade_tex(c, c.solid, tier)
    lows = sorted(range(n), key=lambda i: -pts[i][1])
    hero = [i for i in lows if not (crafted and i % 5 == 2)]
    hc, h2, h3, h4 = hero[0], hero[1], hero[2], hero[3]
    by_x = sorted([hc, h2, h3, h4], key=lambda i: pts[i][0])
    if crafted:
        c.anchor("hero_c", 0.0, 0.72, "guru")
    else:
        c.anchor("hero_c", pts[by_x[1]][0], pts[by_x[1]][1], "bead%d" % by_x[1])
    c.anchor("hero_ll", pts[by_x[0]][0] - 0.07, pts[by_x[0]][1] + 0.07, "bead%d" % by_x[0], 135)
    c.anchor("hero_l", pts[by_x[0]][0], pts[by_x[0]][1] - 0.02, "bead%d" % by_x[0])
    c.anchor("hero_r", pts[by_x[3]][0], pts[by_x[3]][1], "bead%d" % by_x[3])
    c.anchor("hole_fr", pts[by_x[2]][0] + 0.06, pts[by_x[2]][1] + 0.03, "bead%d" % by_x[2])


def carving(c, v):
    tier, crafted = v["tier"], v["design"] == "crafted"
    if crafted:                                          # gourd with a vine bridge and leaf tip
        c.ell(0, 0.38, 0.62, 0.58, tier, "gloss", "body")
        c.ell(0, -0.42, 0.42, 0.42, tier, "gloss", "body")
        c.tube([(0.05, -0.85), (0.3, -0.98), (0.55, -0.7), (0.5, -0.3)], 0.09, tier, "gloss", "bridge", minpx=3)
        c.poly([(0.45, -0.3), (0.82, -0.1), (0.5, 0.05)], tier, 5, "tip")
        c.arc(0, 0.38, 0.4, 0.36, 200, 330, c.px(1), tier, flat=2, part="body", minpx=1)
        c.arc(-0.1, 0.45, 0.25, 0.2, 30, 170, c.px(1), tier, flat=2, part="body", minpx=1)
        A = dict(recess=(-0.05, 0.02), tip=(0.8, -0.1, 0), field=(0.05, 0.62), groove=(-0.36, 0.3), bridge=(0.5, -0.78))
    else:                                                # pebble with a raised leaf relief
        c.ell(0, 0.02, 0.9, 0.78, tier, "gloss", "body", rot=-0.25)
        c.poly([(-0.5, 0.3), (-0.1, -0.5), (0.55, -0.6), (0.3, 0.05), (-0.2, 0.45)], tier, 5, "relief")
        c.poly([(-0.5, 0.3), (0.55, -0.6), (0.3, 0.05), (-0.2, 0.45)], tier, 4, "relief")
        c.seg(-0.5, 0.3, 0.55, -0.6, None, "-2")
        c.seg(-0.44, 0.34, 0.6, -0.55, None, "+1")
        c.tube([(0.5, -0.58), (0.72, -0.72)], 0.08, tier, "gloss", "bridge", minpx=3)
        c.poly([(0.66, -0.78), (0.95, -0.62), (0.7, -0.5)], tier, 5, "tip")
        A = dict(recess=(-0.05, -0.02), tip=(0.93, -0.63, 0), field=(0.1, 0.5), groove=(-0.42, 0.0), bridge=(0.6, -0.65))
    jade_tex(c, c.solid, tier)
    for k, a in A.items():
        c.anchor(k, a[0], a[1], None, a[2] if len(a) > 2 else 0)


T = {
    # rough: front templates use skin anchors, window templates use window anchors
    "rough_yellow_shell_fissure": [("crack", "a1", dict(L=0.42, ang=70, wide=True))],
    "rough_yellow_chalk_cloud": [("stain", "win_l", dict(rx=0.11, ry=0.13, mat="quartz", dt=1))],
    "rough_yellow_black_grain_pocket": [("specks", "win_d", dict(n=7, sc=1, mat="slag", tone=2))],
    "rough_yellow_dyed_sand": [("stain", "a2", dict(rx=0.18, ry=0.12, mat="dye", dt=-1, dither="thin"))],
    "rough_yellow_window_inlay": [("stripe", "win_rim_r", dict(L=0.3, ang=90, wid=0.03, flat=0)), ("stripe", "win_rim_d", dict(L=0.3, ang=0, wid=0.03, flat=0)), ("stain", "win_r", dict(rx=0.05, ry=0.1, dt=2, dither=False))],
    "rough_black_hairline_skin_split": [("crack", "a1", dict(L=0.5, ang=35, lip=3))],
    "rough_black_opaque_heart": [("stain", "win_c", dict(rx=0.13, ry=0.11, mat="slag", flat=3, dither=True))],
    "rough_black_white_vein": [("stripe", "win_r", dict(L=0.4, ang=75, wid=0.045, mat="quartz", flat=5))],
    "rough_black_artificial_dark_skin": [("stain", "a2", dict(rx=0.18, ry=0.13, mat="skin_mountain", dt=2, dither=True))],
    "rough_black_plugged_window": [("stripe", "win_rim_d", dict(L=0.34, ang=0, wid=0.03, flat=0)), ("stain", "win_d", dict(rx=0.14, ry=0.05, dt=2, dither=False))],
    "rough_water_impact_spall": [("bite", "a1", dict(r=0.12, shape="wedge", ang=0, face=5))],
    "rough_water_through_fracture": [("crack", "win_u", dict(L=0.34, ang=80, wide=True))],
    "rough_water_fibrous_cloud": [("stain", "win_u", dict(rx=0.16, ry=0.07, mat="quartz", dt=1))],
    "rough_water_surface_color_wash": [("stain", "a2", dict(rx=0.22, ry=0.13, mat="dye", dt=-1, dither="thin"))],
    "rough_water_joined_cap": [("stripe", "a3", dict(L=0.5, ang=115, wid=0.03, flat=0)), ("stain", (0.86, 0.2, "skin"), dict(rx=0.1, ry=0.2, mat="skin_white", dt=-1, dither=False))],
    "rough_white_salt_skin_split": [("crack", "a1", dict(L=0.4, ang=40, wide=True))],
    "rough_white_coarse_void_zone": [("specks", "win_l", dict(n=9, sc=1, tone=1, lip=True))],
    "rough_white_carbon_pocket": [("stain", "win_u", dict(rx=0.12, ry=0.08, mat="slag", flat=2))],
    "rough_white_chalk_skin_patch": [("stain", "a2", dict(rx=0.22, ry=0.12, mat="quartz", flat=5, dither=False))],
    "rough_white_window_plug": [("stripe", "win_rim_r", dict(L=0.34, ang=90, wid=0.03, flat=0)), ("stain", "win_r", dict(rx=0.05, ry=0.13, dt=2, dither=False))],
    "rough_rust_rust_ridge_split": [("crack", "a1", dict(L=0.45, ang=20, wide=True))],
    "rough_rust_brown_interior_patch": [("stain", "win_d", dict(rx=0.14, ry=0.08, mat="brown", dt=0))],
    "rough_rust_cotton_sheet": [("stain", "win_r", dict(rx=0.09, ry=0.15, mat="quartz", dt=1))],
    "rough_rust_painted_rust": [("stain", "a2", dict(rx=0.2, ry=0.14, mat="red", dt=0, dither=False)), ("stripe", (-0.5, -0.3, "skin"), dict(L=0.36, ang=10, wid=0.03, dt=-2))],
    "rough_rust_patched_shell": [("stripe", "a3", dict(L=0.5, ang=65, wid=0.03, flat=0)), ("stain", (-0.86, 0.2, "skin"), dict(rx=0.1, ry=0.2, mat="skin_yellow", dt=-1, dither=False))],
    "rough_mountain_open_mountain_fissure": [("crack", "a1", dict(L=0.5, ang=65, wide=True))],
    "rough_mountain_coarse_interior": [("specks", "win_u", dict(n=9, sc=1, tone=2, lip=True))],
    "rough_mountain_black_band": [("stripe", "win_c", dict(L=0.6, ang=35, wid=0.05, mat="slag", flat=2))],
    "rough_mountain_filled_shell_seam": [("gapline", "a2", dict(L=0.42, ang=-12, lip=0)), ("stripe", (-0.1, 0.58, "skin"), dict(L=0.42, ang=-12, wid=0.03, dt=2))],
    "rough_mountain_colored_window": [("stain", "win_c", dict(rx=0.13, ry=0.11, mat="dye", dt=0, dither="thin"))],
    "jade_pendant_drilled_eye_crack": [("crack", "eye", dict(L=0.42, ang=65))],
    "jade_pendant_edge_chip": [("bite", "edge_ll", dict(r=0.15, shape="wedge", ang=315, face=6))],
    "jade_pendant_clouded_center": [("stain", "center", dict(rx=0.2, ry=0.22, mat="quartz", dt=0))],
    "jade_pendant_dyed_groove": [("stain", "groove", dict(rx=0.24, ry=0.12, mat="dye", dt=-1, dither="thin"))],
    "jade_pendant_filled_eye_seam": [("stripe", "eye_low", dict(L=0.3, ang=150, wid=0.03, dt=2))],
    "jade_cabochon_dome_fissure": [("crack", "dome_fr", dict(L=0.4, ang=60))],
    "jade_cabochon_girdle_chip": [("bite", "girdle_ll", dict(r=0.15, shape="wedge", ang=315, face=6))],
    "jade_cabochon_central_inclusion": [("specks", "dome_ul", dict(n=8, sc=1, mat="slag", tone=2))],
    "jade_cabochon_dyed_grain_web": [("stain", "dome_low", dict(rx=0.3, ry=0.1, mat="dye", dt=-1, dither="thin"))],
    "jade_cabochon_filled_dome_seam": [("stripe", "rim_r", dict(L=0.36, ang=80, wid=0.03, dt=2))],
    "jade_bangle_radial_bangle_crack": [("crack", "arc_fr", dict(L=0.3, ang=110, branch=False))],
    "jade_bangle_inner_rim_chip": [("bite", "inner_ul", dict(r=0.08, shape="round", face=6, out=0.5))],
    "jade_bangle_clouded_arc": [("stain", "arc_fl", dict(rx=0.26, ry=0.1, mat="quartz", dt=0))],
    "jade_bangle_dyed_band": [("stain", "arc_fc", dict(rx=0.2, ry=0.14, mat="dye", dt=-1, dither="thin"))],
    "jade_bangle_joined_bangle_seam": [("stripe", "arc_br", dict(L=0.3, ang=70, wid=0.03, dt=2))],
    "jade_beads_drill_exit_crack": [("crack", "hero_c", dict(L=0.16, ang=40, branch=True)), ("crack", "hero_c", dict(L=0.12, ang=200, branch=False))],
    "jade_beads_bead_chip": [("bite", "hero_ll", dict(r=0.07, shape="wedge", ang=315, face=6))],
    "jade_beads_mismatched_bead": [("specks", "hero_r", dict(n=6, sc=1, mat="slag", tone=2))],
    "jade_beads_dyed_drill_pool": [("stain", "hole_fr", dict(rx=0.08, ry=0.08, mat="dye", dt=-1, dither="thin"))],
    "jade_beads_filled_bead_split": [("stripe", "hero_l", dict(L=0.26, ang=60, wid=0.03, dt=2))],
    "jade_carving_recess_fissure": [("crack", "recess", dict(L=0.42, ang=75))],
    "jade_carving_relief_chip": [("bite", "tip", dict(r=0.2, shape="round", ang=0, out=0.2, face=6))],
    "jade_carving_opaque_relief_zone": [("stain", "field", dict(rx=0.26, ry=0.14, mat="quartz", dt=0))],
    "jade_carving_dyed_recess": [("stain", "groove", dict(rx=0.12, ry=0.2, mat="dye", dt=-1, dither="thin"))],
    "jade_carving_filled_relief_bridge": [("stripe", "bridge", dict(L=0.2, ang=120, wid=0.03, dt=2))],
}

M = {}
for fam in ROUGH:
    M["jade_rough_" + fam] = (rough, 1.08, 1.08, dict(family=fam))
for des in ("plain", "crafted"):
    M["jade_pendant_" + des] = (pendant, 0.7, 1.0, dict(design=des, tier="nuo"))
    M["jade_cabochon_" + des] = (cabochon, 1.0, 0.72, dict(design=des, tier="nuo"))
    M["jade_bangle_" + des] = (bangle, 1.66, 0.82, dict(design=des, tier="nuo"))
    M["jade_beads_" + des] = (beads, 1.04, 1.2 if des == "crafted" else 1.0, dict(design=des, tier="nuo"))
    M["jade_carving_" + des] = (carving, 1.0, 1.02, dict(design=des, tier="nuo"))
