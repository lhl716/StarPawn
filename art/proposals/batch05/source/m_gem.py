"""Loose gems (ten models) and the five mounted settings with separate stone layers."""
import math

import numpy as np

from px import shade, LIGHT
import m_metal


def gem_shape(shape):
    if shape == "round":
        return [(math.cos(math.radians(a)), math.sin(math.radians(a))) for a in range(0, 360, 30)]
    if shape == "oval":
        return [(math.cos(math.radians(a)), 0.74 * math.sin(math.radians(a))) for a in range(0, 360, 30)]
    if shape == "princess":
        return [(0.86, -0.86), (0.86, 0.86), (-0.86, 0.86), (-0.86, -0.86)]
    if shape == "cushion":
        return [(0.9, -0.5), (0.9, 0.5), (0.5, 0.9), (-0.5, 0.9), (-0.9, 0.5), (-0.9, -0.5), (-0.5, -0.9), (0.5, -0.9)]
    if shape == "pear":
        return [(0, -1.05), (0.42, -0.45), (0.72, 0.15), (0.6, 0.65), (0.25, 0.95), (-0.25, 0.95), (-0.6, 0.65), (-0.72, 0.15), (-0.42, -0.45)]
    if shape == "step":
        return [(0.72, -0.62), (0.72, 0.62), (0.45, 0.95), (-0.45, 0.95), (-0.72, 0.62), (-0.72, -0.62), (-0.45, -0.95), (0.45, -0.95)]
    raise KeyError(shape)


def draw_gem(c, kind, shape, cx, cy, s, part="stone"):
    """Face-up stone. Returns nothing; pearls are spheres with a drill opening."""
    if kind == "pearl":
        if shape == "baroque":
            c.ell(cx - 0.12 * s, cy - 0.2 * s, 0.72 * s, 0.7 * s, "pearl", "gloss", part)
            m = c.ell(cx + 0.14 * s, cy + 0.28 * s, 0.66 * s, 0.68 * s, "pearl", "gloss", part)
        else:
            c.ell(cx, cy, 0.9 * s, 0.9 * s, "pearl", "gloss", part)
        pm = c.sel(part)
        c.add(pm & (c.tone == 3) & (((c.gx - cx) / s) > 0.2), 1)        # soft overtone
        if s * c.u > 9:
            c.ell(cx + 0.05 * s, cy + (0.78 if shape == "baroque" else 0.72) * s, 0.1 * s, 0.07 * s, "pearl", flat=1, part=part)
        return
    P = [(cx + x * s, cy + y * s) for x, y in gem_shape(shape)]
    n = len(P)
    tf = 0.52
    ox, oy = cx - 0.04 * s, cy - 0.05 * s
    Tb = [(ox + (x - cx) * tf, oy + (y - cy) * tf) for x, y in P]
    tiny = s * c.u < 7
    for i in range(n):
        a, b = P[i], P[(i + 1) % n]
        ta, tb = Tb[i], Tb[(i + 1) % n]
        mx, my = (a[0] + b[0]) / 2 - cx, (a[1] + b[1]) / 2 - cy
        L = math.hypot(mx, my) or 1
        lit = (mx / L) * LIGHT[0] + (my / L) * LIGHT[1]
        t = 5 if lit > 0.45 else 4 if lit > 0.05 else 3 if lit > -0.4 else 2
        if shape == "step" or tiny:
            c.poly([a, b, tb, ta], kind, t, part)
            if shape == "step" and not tiny:
                ma = [((a[0] + ta[0]) / 2, (a[1] + ta[1]) / 2), ((b[0] + tb[0]) / 2, (b[1] + tb[1]) / 2)]
                c.poly([ma[0], ma[1], tb, ta], kind, min(6, t + 1), part)
        else:
            mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
            c.poly([a, mid, ta], kind, t, part)
            c.poly([mid, b, tb], kind, max(1, t - 1) if i % 2 else min(6, t + 1), part)
            c.poly([mid, tb, ta], kind, min(6, t + (1 if i % 3 == 0 else 0)), part)
    c.poly(Tb, kind, 4, part)
    if not tiny:
        c.poly([Tb[n * 5 // 8 % n], Tb[(n * 6 // 8) % n], (ox, oy)], kind, 6, part)
        c.poly([Tb[(n * 1 // 8) % n], Tb[(n * 2 // 8) % n], (ox, oy)], kind, 3, part)
    else:
        c.dots([(ox - 0.15 * s, oy - 0.15 * s)], kind, 6)


def gem(c, v):
    kind, shape = v["kind"], v["shape"]
    s = 0.92
    draw_gem(c, kind, shape, 0, 0, s, "stone")
    def A(name, ang, rad, outward=False):
        pts = gem_shape(shape) if kind != "pearl" else [(0.9 * math.cos(math.radians(a)), 0.9 * math.sin(math.radians(a))) for a in range(0, 360, 30)]
        # radius of the outline in this direction
        best = max(pts, key=lambda p: p[0] * math.cos(math.radians(ang)) + p[1] * math.sin(math.radians(ang)))
        R = best[0] * math.cos(math.radians(ang)) + best[1] * math.sin(math.radians(ang))
        c.anchor(name, R * rad * s * math.cos(math.radians(ang)), R * rad * s * math.sin(math.radians(ang)), "stone", ang)
    A("girdle_fr", 25, 0.97); A("crown_r", -10, 0.9); A("table_fl", 150, 0.42); A("table_c", 200, 0.12)
    A("crown_ll", 135, 0.97); A("crown_fr", 35, 0.55); A("left_c", 180, 0.3); A("low_edge", 90, 0.97)
    A("crown_ul", 225, 0.7); A("center_r", 0, 0.3); A("corner_fr", 50, 0.97); A("step_l", 180, 0.9)
    A("shoulder_fl", 160, 0.5); A("drill", 85, 0.9); A("curve_fr", 20, 0.55); A("center", 90, 0.1)


SETTINGS = {
    "jewelry_ring_solitaire": ("ring", ["crown_center"]),
    "jewelry_pendant_bezel": ("pendant", ["bezel_center"]),
    "jewelry_earrings_stud": ("earrings", ["left_stud", "right_stud"]),
    "jewelry_bracelet_trilogy": ("bracelet", ["left_link", "center_link", "right_link"]),
    "jewelry_necklace_drop": ("necklace", ["drop_center"]),
}


def _stone(c, v, socket, x, y, s):
    c.sockets[socket] = (x, y, s)
    st = (v.get("stones") or {}).get(socket)
    fl = v.get("flags", ())
    miss = {"crown_center": "missing_center", "bezel_center": "missing_insert", "right_stud": "missing_right",
            "left_link": "missing_left", "drop_center": "missing_drop"}.get(socket)
    if not st or (miss and any(t.endswith(miss) for t in fl)):
        return False
    kind, shape = st.split("_")[1:3]
    draw_gem(c, kind, shape, x, y, s, "stone_" + socket)
    gf = v.get("gem_ops", {}).get(socket)
    return True


def _prongs(c, mat, x, y, s, part="prong"):
    for i, (dx, dy) in enumerate(((-0.7, -0.7), (0.7, -0.7), (0.7, 0.7), (-0.7, 0.7))):
        c.ell(x + dx * s, y + dy * s, max(0.2 * s, c.px(1.2)), max(0.2 * s, c.px(1.2)), mat, part=part + str(i))


def j_ring(c, v):
    mat = v["metal"]
    c.arc(0.07, 0.28, 0.5, 0.56, 200, 340, 0.1, mat, part="shank_back", minpx=2)
    c.add(c.sel("shank_back"), -1)
    c.arc(0, 0.3, 0.56, 0.6, -62, 242, 0.14, mat, part="shank", minpx=3)
    c.poly([(-0.3, -0.5), (0.3, -0.5), (0.2, -0.22), (-0.2, -0.22)], mat, 3, "crown")
    c.poly([(-0.3, -0.5), (0.0, -0.5), (-0.04, -0.22), (-0.2, -0.22)], mat, 5, "crown")
    c.ell(0, -0.58, 0.25, 0.2, mat, flat=2, part="seat")
    full = _stone(c, v, "crown_center", 0, -0.6, 0.29)
    _prongs(c, mat, 0, -0.6, 0.3)
    c.anchor("prong_fr", 0.16, -0.44, ["stone_crown_center", "seat"])
    c.anchor("shank_low", 0.08, 0.92, "shank", 90)
    c.anchor("shank_low_in", 0.08, 0.86, "shank")
    c.anchor("shoulder_l", -0.46, 0.0, "shank")
    c.anchor("shank_back", -0.44, 0.66, "shank")
    c.anchor("wear", 0.45, 0.45, "shank")


def j_pendant(c, v):
    mat = v["metal"]
    c.arc(0, -0.74, 0.13, 0.17, 0, 360, 0.07, mat, part="bail", minpx=2)
    c.box(-0.07, -0.6, 0.07, -0.42, mat, 3, "bailroot", bevel=False)
    c.ell(0, 0.18, 0.56, 0.6, mat, part="bezel")
    c.ell(0, 0.18, 0.44, 0.48, mat, flat=2, part="seat")
    st = (v.get("stones") or {}).get("bezel_center") or ""
    if "pearl" in st or not st:
        c.ell(0, 0.18, 0.07, 0.07, mat, flat=5, part="peg")
    _stone(c, v, "bezel_center", 0, 0.18, 0.44)
    c.anchor("bail_top", 0.13, -0.74, "bail", 0)
    c.anchor("bezel_r", 0.42, 0.02, ["stone_bezel_center", "seat"])
    c.anchor("rim_ll", -0.36, 0.58, "bezel")
    c.anchor("bail_root", 0.0, -0.5, "bailroot")
    c.anchor("wear", 0.3, 0.66, "bezel")


def j_earrings(c, v):
    mat = v["metal"]
    bent = any(t.endswith("bent_post") for t in v.get("flags", ()))
    for side, x in (("left", -0.46), ("right", 0.38)):
        pts = [(x + 0.1, -0.05), (x + 0.3, -0.4), (x + 0.42, -0.62)]
        if side == "left" and bent:
            pts = [(x + 0.1, -0.05), (x + 0.3, -0.4), (x + 0.12, -0.66)]
        c.tube(pts, 0.06, mat, part=side + "_post", minpx=2)
        if side == "right":
            c.ell(x + 0.34, -0.47, 0.15, 0.09, mat, part="back", rot=-0.5)
            c.ell(x + 0.34, -0.47, 0.04, 0.03, mat, flat=2, part="back")
        c.ell(x, 0.12, 0.36, 0.36, mat, part=side + "_set")
        c.ell(x, 0.12, 0.28, 0.28, mat, flat=2, part=side + "_seat")
        _stone(c, v, side + "_stud", x, 0.12, 0.28)
        _prongs(c, mat, x, 0.12, 0.29, side + "_prong")
    c.anchor("l_post", -0.2, -0.5, "left_post")
    c.anchor("r_back", 0.72, -0.47, "back")
    c.anchor("l_prong", -0.28, 0.26, ["stone_left_stud", "left_seat"])
    c.anchor("r_post_base", 0.52, -0.12, ["right_post", "right_set"])
    c.anchor("wear", -0.72, 0.3, "left_set")


def j_bracelet(c, v):
    mat = v["metal"]
    def cy(x):                                           # 2x2 inventory art drapes in a deeper curve
        return (1.1 - 2.2 * (x / 1.55) ** 2) if v.get("inv") else (0.36 - 0.42 * (x / 1.55) ** 2)
    xs = [-1.5 + 3.0 * i / 20 for i in range(21)]
    stones = {4: "left_link", 10: "center_link", 16: "right_link"}
    for i, x in enumerate(xs):
        if any(abs(i - k) <= 1 for k in stones) or i in (0, 20):
            continue
        ang = math.atan2(cy(x + 0.05) - cy(x - 0.05), 0.1)
        m_metal._link(c, x, cy(x), ang, 0.2, 0.06, mat, "link%d" % i, i % 2 == 1, 2.0)
    for k, sock in stones.items():
        x = xs[k]
        c.ell(x, cy(x), 0.25, 0.25, mat, part="bez_" + sock)
        c.ell(x, cy(x), 0.18, 0.18, mat, flat=2, part="seat_" + sock)
        _stone(c, v, sock, x, cy(x), 0.18)
        _prongs(c, mat, x, cy(x), 0.2, "prong_" + sock)
    c.arc(xs[0], cy(xs[0]), 0.09, 0.09, 0, 360, 0.05, mat, part="catch", minpx=2)
    c.ell(xs[20] - 0.02, cy(xs[20]), 0.16, 0.09, mat, part="clasp", rot=-0.5)
    c.box(xs[20] - 0.1, cy(xs[20]) - 0.03, xs[20] + 0.02, cy(xs[20]) + 0.03, mat, 5, "clasp", bevel=False)
    c.anchor("clasp", xs[20] - 0.06, cy(xs[20]) + 0.02, "clasp", 60)
    c.anchor("link_l", xs[6], cy(xs[6]) + 0.07, "link6", 90)
    c.anchor("center_prong", xs[10] + 0.11, cy(xs[10]) - 0.1, ["stone_center_link", "seat_center_link"])
    c.anchor("link_rr", xs[13], cy(xs[13]), "link13")
    c.anchor("wear", xs[7], cy(xs[7]), "link7")


def j_necklace(c, v):
    mat = v["metal"]
    m_metal.necklace(c, dict(v, mat=mat, design="orbit"), pendant=False)
    bx, by = c.anchors["link_low"][0], c.anchors["link_low"][1] - 0.12
    c.box(bx - 0.04, by + 0.1, bx + 0.04, by + 0.2, mat, 3, "droproot", bevel=False)
    c.ell(bx, by + 0.42, 0.25, 0.27, mat, part="drop")
    c.ell(bx, by + 0.42, 0.19, 0.21, mat, flat=2, part="seat")
    st = (v.get("stones") or {}).get("drop_center") or ""
    if "pearl" in st or not st:
        c.ell(bx, by + 0.42, 0.04, 0.04, mat, flat=5, part="peg")
    _stone(c, v, "drop_center", bx, by + 0.42, 0.19)
    c.anchor("drop_rim", bx + 0.13, by + 0.3, ["stone_drop_center", "seat"])
    c.anchor("wear", bx - 0.2, by + 0.52, "drop")


JFN = {"ring": j_ring, "pendant": j_pendant, "earrings": j_earrings, "bracelet": j_bracelet, "necklace": j_necklace}

T = {
    "gem_diamond_girdle_chip": [("bite", "girdle_fr", dict(r=0.16, shape="wedge", ang=205, face=6))],
    "gem_diamond_edge_feather": [("crack", "crown_r", dict(L=0.3, ang=170, branch=False, lip=0))],
    "gem_diamond_table_abrasion": [("scratch", "table_fl", dict(n=3, L=0.16, ang=35, gap=2, lip=False))],
    "gem_diamond_obscuring_cloud": [("stain", "table_c", dict(rx=0.2, ry=0.16, mat="quartz", dt=0))],
    "gem_diamond_claim_mismatch": [],
    "gem_ruby_crown_chip": [("bite", "crown_ll", dict(r=0.16, shape="wedge", ang=315, face=6))],
    "gem_ruby_surface_fissure": [("crack", "crown_fr", dict(L=0.34, ang=50, lip=1))],
    "gem_ruby_dark_inclusion": [("specks", "left_c", dict(n=7, sc=1, tone=1))],
    "gem_ruby_filled_fissure": [],
    "gem_ruby_glass_substitute": [],
    "gem_sapphire_pavilion_chip": [("bite", "low_edge", dict(r=0.15, shape="wedge", ang=270, face=6))],
    "gem_sapphire_facet_abrasion": [("scratch", "crown_ul", dict(n=3, L=0.18, ang=-40, gap=2, lip=False))],
    "gem_sapphire_uneven_zone": [("stripe", "center_r", dict(L=0.5, ang=70, wid=0.12, dt=2))],
    "gem_sapphire_edge_fracture": [("crack", "crown_fr", dict(L=0.36, ang=30, lip=1))],
    "gem_sapphire_composite_claim": [],
    "gem_emerald_corner_chip": [("bite", "corner_fr", dict(r=0.16, shape="wedge", ang=230, face=6))],
    "gem_emerald_open_fracture": [("crack", "step_l", dict(L=0.34, ang=20, lip=1))],
    "gem_emerald_face_window": [("stain", "center", dict(rx=0.2, ry=0.26, flat=6, dither=True))],
    "gem_emerald_filling_record": [],
    "gem_emerald_imitation_record": [],
    "gem_pearl_nacre_peel": [("patch", "shoulder_fl", dict(rx=0.17, ry=0.13, mat="label", dt=-1))],
    "gem_pearl_drill_chip": [("bite", "drill", dict(r=0.13, shape="round", ang=90, out=0.5, face=3))],
    "gem_pearl_surface_scratch": [("scratch", "curve_fr", dict(n=1, L=0.3, ang=60, lip=False))],
    "gem_pearl_dull_patch": [("stain", "center", dict(rx=0.26, ry=0.2, flat=3, dither=True))],
    "gem_pearl_coated_imitation": [],
    "jewelry_ring_loose_prong": [("slot", "prong_fr", dict(rx=0.1, ry=0.08))],
    "jewelry_ring_shank_dent": [("bite", "shank_low", dict(r=0.12, shape="round", out=0.8, face=2)), ("kink", "shank_low_in", dict(rx=0.1, ry=0.04))],
    "jewelry_ring_missing_center": [("flag", (0, -0.6, None), {})],
    "jewelry_ring_tool_scars": [("scratch", "shoulder_l", dict(n=2, L=0.14, ang=0, gap=3))],
    "jewelry_ring_old_resize": [("seam", "shank_back", dict(L=0.16, ang=-45, wpx=2))],
    "jewelry_pendant_bail_gap": [("gap", "bail_top", dict(w=3, L=0.1, ang=0))],
    "jewelry_pendant_lifted_bezel": [("slot", "bezel_r", dict(rx=0.07, ry=0.15))],
    "jewelry_pendant_missing_insert": [("flag", (0, 0.18, None), {})],
    "jewelry_pendant_rim_scrape": [("scratch", "rim_ll", dict(n=2, L=0.22, ang=25, gap=2))],
    "jewelry_pendant_bail_join": [("seam", "bail_root", dict(L=0.14, ang=0, wpx=2))],
    "jewelry_earrings_bent_post": [("flag", (-0.3, -0.55, None), {})],
    "jewelry_earrings_loose_back": [("slot", "r_back", dict(rx=0.07, ry=0.05))],
    "jewelry_earrings_missing_right": [("flag", (0.38, 0.12, None), {})],
    "jewelry_earrings_prong_gap": [("slot", "l_prong", dict(rx=0.06, ry=0.06))],
    "jewelry_earrings_post_join": [("patch", "r_post_base", dict(rx=0.09, ry=0.09, mat="solder", lip=False))],
    "jewelry_bracelet_clasp_gap": [("gap", "clasp", dict(w=3, L=0.26, ang=60))],
    "jewelry_bracelet_flat_link": [("bite", "link_l", dict(shape="flat", ang=90, face=2))],
    "jewelry_bracelet_loose_center": [("slot", "center_prong", dict(rx=0.05, ry=0.05))],
    "jewelry_bracelet_missing_left": [("flag", (-0.9, 0.2, None), {})],
    "jewelry_bracelet_link_join": [("patch", "link_rr", dict(rx=0.08, ry=0.08, mat="solder", lip=False))],
    "jewelry_necklace_twisted_chain": [("flag", "chain_ll", {})],
    "jewelry_necklace_clasp_gap": [("gap", "clasp", dict(w=3, L=0.3, ang=40))],
    "jewelry_necklace_drop_retainer": [("slot", "drop_rim", dict(rx=0.05, ry=0.07))],
    "jewelry_necklace_missing_drop": [("flag", (0, 0.9, None), {})],
    "jewelry_necklace_repair_link": [("patch", "chain_ul", dict(rx=0.08, ry=0.08, mat="solder", lip=False))],
}

M = {}
for k, sh in (("diamond", ("round", "princess")), ("ruby", ("oval", "cushion")), ("sapphire", ("oval", "pear")),
              ("emerald", ("step", "oval")), ("pearl", ("round", "baroque"))):
    for s_ in sh:
        M["gem_%s_%s" % (k, s_)] = (gem, 1.0, 1.06, dict(kind=k, shape=s_))
EXT = {"ring": (0.82, 1.0), "pendant": (0.72, 1.0), "earrings": (0.95, 0.8), "bracelet": (1.72, 0.7), "necklace": (1.2, 1.4)}
for key, (form, socks) in SETTINGS.items():
    E0 = {"bracelet": dict(ext0=(1.76, 1.4)), "necklace": dict(ext0=(0.52, 1.72))}.get(form, {})
    M[key] = (JFN[form], EXT[form][0], EXT[form][1], dict(metal="gold", form=form, **E0))
    M["setting_" + form] = (JFN[form], EXT[form][0], EXT[form][1], dict(metal="gold", form=form, stones=None, **E0))
