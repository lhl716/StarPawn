"""Gold and silver models: rings, bracelets, necklaces, earring pairs, bars, cup."""
import math

from px import ellipse_pts

WALL = [(-0.94, 1), (-0.78, 2), (-0.62, 3), (-0.46, 4), (-0.36, 5), (-0.30, 6),
        (-0.20, 5), (-0.04, 4), (0.34, 3), (0.62, 2), (0.83, 1), (0.94, 2), (9, 1)]
INNER = [(-0.62, 1), (-0.15, 2), (0.30, 3), (0.52, 4), (0.60, 5), (0.78, 4), (0.92, 3), (9, 2)]

RING_GEO = {  # (lod) -> cx, cy, R, r, k, d, grooves   (pixels; orbit matches the approved sample)
    "orbit": {1: (96, 51, 52, 43, 0.60, 21, [(9, 10)]), 0: (32, 24.5, 28, 22, 0.60, 13, [(6, 6)])},
    "starburst": {1: (96, 47, 45, 36.5, 0.66, 27, []), 0: (32, 22.5, 25, 19.5, 0.66, 16, [])},
}
BRACELET_GEO = {
    "orbit": {1: (96, 47, 84, 76, 0.42, 16, [(4, 5), (10, 11)]), 0: (64, 24, 59, 53, 0.42, 10, [(4, 4)])},
    "starburst": {1: (96, 44, 76, 70, 0.50, 13, []), 0: (64, 23, 54, 49, 0.50, 8, [])},
}


def _star(c, x, y, r, mat, part, tone=5, n=8, inner=0.45):
    pts = []
    for i in range(n * 2):
        a = math.pi * i / n - math.pi / 2
        rr = r if i % 2 == 0 else r * inner
        pts.append((x + rr * math.cos(a), y + rr * math.sin(a)))
    c.poly(pts, mat, tone, part)
    c.ell(x, y, r * 0.3, r * 0.3, mat, flat=min(6, tone + 1), part=part)


def _band(c, v, geo, kind):
    cx, cy, R, r, k, d, grooves = geo[v["design"]][c.lod]
    c.frame(R, cx, cy)
    mat = v["mat"]
    rn, dn = r / R, d / R
    c.band(0, 0, 1.0, rn, k, dn, mat, WALL, INNER, grooves)
    u_, v_, D = c.wall_uv
    wall = c.sel("wall")
    if v["design"] == "starburst":
        # engraved rays: alternating diagonal facets instead of the orbit groove
        import numpy as np
        ph = np.floor((u_ * (6 if kind == "ring" else 13) + v_ * 0.12 + 40)).astype(int)
        ray = wall & (ph % 2 == 0) & (v_ >= 2) & (v_ < D - 2)
        c.add(ray, -1)
        c.add(wall & (ph % 2 == 1) & (v_ >= 2) & (v_ < 3), 1)

    def wp(u, vf):
        return (u, k * math.sqrt(1 - u * u) + dn * vf)

    def post_outline():
        lit = c.edge & c.sel("top") & (c.gy < 0) & (c.gx < 0.55)
        c.tone[c.edge] = 0
        c.tone[lit] = 1
    c.post_outline = post_outline
    px = c.px(1)
    c.anchor("wall_left", *wp(-0.47, 0.38), "wall")
    c.anchor("wall_low", *wp(0.12, 1.0), ["wall"], 90)
    c.anchor("wall_low_in", wp(0.12, 0.6)[0], wp(0.12, 0.62)[1], "wall")
    c.anchor("wall_right", wp(0.27, 0)[0], wp(0.27, 0)[1] + px, "wall")
    c.anchor("wall_center", *wp(-0.18, 0.70), "wall")
    c.anchor("inner_back", rn * 0.38, -rn * k * math.sqrt(1 - 0.38 ** 2) + dn * 0.45, "inner")
    c.anchor("inner_back2", rn * 0.56, -rn * k * math.sqrt(1 - 0.56 ** 2) + dn * 0.45, "inner")
    gv = (grooves[0][0] + 0.5) * px if grooves else dn * 0.5
    off = kind == "bracelet" and v["design"] == "starburst"
    c.anchor("groove", wp(-0.36 if off else -0.08, 0)[0], wp(-0.36 if off else -0.08, 0)[1] + gv, "wall")
    c.anchor("side_right", 1.0 - 2.2 * px, k * 0.2 + dn * 0.5, ["wall", "top"], 0)
    c.anchor("side_right_in", 0.86, wp(0.86, 0.5)[1], "wall")
    c.anchor("wall_fl", wp(-0.56, 0)[0], wp(-0.56, 0)[1] + px, "wall")
    c.anchor("wall_fr", *wp(0.47, 0.55), "wall")
    c.anchor("wall_fr_top", wp(0.5, 0)[0], wp(0.5, 0)[1] + px, "wall")
    c.anchor("wall_front", *wp(-0.3, 0.5), "wall")
    c.anchor("wall_fc", wp(0.3 if off else 0.06, 0)[0], wp(0.3 if off else 0.06, 0)[1] + px, "wall")
    c.anchor("wall_fl_low", *wp(-0.5, 0.62), "wall")
    c.anchor("inner_lip", 0.05, -rn * k + dn * 0.72, "inner")
    c.anchor("inner_seam", rn * 0.3, -rn * k * math.sqrt(1 - 0.09) + dn * 0.5, "inner")
    return cx, cy, R, k, dn


def ring(c, v):
    _band(c, v, RING_GEO, "ring")


def bracelet(c, v):
    cx, cy, R, k, dn = _band(c, v, BRACELET_GEO, "bracelet")
    if v["design"] == "starburst":                     # medallion plate breaks the silhouette
        y = k + dn * 0.45
        c.ell(0, y, 0.2, 0.2 * 0.9, v["mat"], part="medal")
        _star(c, 0, y, 0.15, v["mat"], "medal", tone=6)
        c.ell(0, y, 0.05, 0.05, v["mat"], flat=3, part="medal")
    else:                                               # hinge pin at the left
        c.box(-0.86, k * 0.5 + dn * 0.1, -0.80, k * 0.5 + dn * 1.05, v["mat"], 3, "wall", bevel=False)


def _link(c, x, y, ang, ll, wid, mat, part, flat_side, minpx):
    if flat_side:
        dx, dy = math.cos(ang) * ll * 0.42, math.sin(ang) * ll * 0.42
        c.tube([(x - dx, y - dy), (x + dx, y + dy)], wid * 1.15, mat, part=part, minpx=minpx)
    else:
        c.tube(ellipse_pts(x, y, ll * 0.62, ll * 0.42, 10, rot=ang) + [ellipse_pts(x, y, ll * 0.62, ll * 0.42, 10, rot=ang)[0]],
               wid, mat, part=part, minpx=minpx)


def necklace(c, v, pendant=True):
    mat, des, fl = v["mat"], v["design"], v.get("flags", ())
    star = des == "starburst"
    n = 44 if star else 34
    ll = 0.15 if star else 0.2
    wid = 0.045 if star else 0.06
    minpx = 2.0
    path = []
    for i in range(n):
        a = math.radians(-58 + 360.0 * i / n)           # start at the clasp, go clockwise
        x, y = 0.86 * math.cos(a), 0.8 * math.sin(a)
        if star:                                        # V-shaped lay
            y = y * (0.86 + 0.24 * max(0.0, math.sin(a)) * (1 - abs(math.cos(a))))
            x *= 0.94 - 0.16 * max(0.0, math.sin(a))
        path.append((x, y - 0.12))
    knot = range(int(n * 0.50), int(n * 0.50) + 4)
    kc = path[knot[1]]
    for i in range(1, n - 1):
        x, y = path[i]
        nx, ny = path[i + 1]
        ang = math.atan2(ny - y, nx - x)
        part = "chain"
        if any(t.endswith(("tangled_run", "twisted_chain")) for t in fl) and i in knot:
            j = i - knot[0]
            x, y = kc[0] + 0.07 * math.cos(j * 2.2), kc[1] + 0.07 * math.sin(j * 2.2)
            ang += 1.2 + j * 1.1
            part = "knot"
        _link(c, x, y, ang, ll, wid, mat, part, i % 2 == 1, minpx)
    # clasp at upper right: catch ring + lobster body with tongue
    (x0, y0), (x1, y1) = path[0], path[n - 1]
    mx, my = (x0 + x1) / 2, (y0 + y1) / 2
    c.arc(x1, y1, 0.08, 0.08, 0, 360, wid, mat, part="catch", minpx=minpx)
    c.ell(mx + 0.02, my + 0.0, 0.15, 0.09, mat, part="clasp", rot=math.atan2(y0 - y1, x0 - x1))
    c.box(mx - 0.05, my - 0.035, mx + 0.07, my + 0.03, mat, 2, "clasp", bevel=False)
    c.box(mx - 0.03, my - 0.02, mx + 0.03, my + 0.015, mat, 5, "tongue", bevel=False)
    c.anchor("clasp", mx, my, "clasp")
    # bottom connector and pendant
    b = max(range(n), key=lambda i: path[i][1])
    bx, by = path[b]
    c.arc(bx, by + 0.02, 0.1, 0.1, 0, 360, wid * 1.3, mat, part="connector", minpx=minpx + 1)
    c.anchor("connector", bx + 0.1, by + 0.02, "connector", 0)
    c.anchor("link_low", bx, by + 0.12, "connector", 90)
    if pendant:
        if star:
            _star(c, bx, by + 0.36, 0.22, mat, "pendant", tone=4)
        else:
            c.arc(bx, by + 0.34, 0.17, 0.17, 0, 360, 0.09, mat, part="pendant", minpx=3)
    def at(frac, part="chain"):
        i = int(n * frac) | 1
        c.anchors[at.name] = (path[i][0], path[i][1], part, 0)
    for name, frac in (("chain_ll", 0.50), ("chain_lr", 0.24), ("chain_ul", 0.80), ("chain_fl", 0.62),
                       ("chain_ur", 0.07)):
        at.name = name
        at(frac, ["chain", "knot"])
    return path


def earrings(c, v):
    mat, des, fl = v["mat"], v["design"], v.get("flags", ())
    star = des == "starburst"
    hook = mat == "silver"
    # left earring, front view
    lx = -0.5
    if star:
        _star(c, lx, -0.42, 0.3, mat, "l_body", tone=4, n=6, inner=0.55)
    else:
        c.arc(lx, -0.42, 0.24, 0.24, 0, 360, 0.11, mat, part="l_body", minpx=3)
        c.ell(lx, -0.42, 0.09, 0.09, mat, part="l_body")
    c.arc(lx, -0.02, 0.085, 0.1, 0, 360, 0.05, mat, part="l_loop", minpx=2)
    if star:
        c.poly([(lx, 0.1), (lx + 0.2, 0.42), (lx, 0.78), (lx - 0.2, 0.42)], mat, 4, "l_lobe")
        c.poly([(lx, 0.1), (lx, 0.78), (lx - 0.2, 0.42)], mat, 5, "l_lobe")
        c.poly([(lx, 0.16), (lx + 0.07, 0.42), (lx, 0.5)], mat, 3, "l_lobe")
    else:
        c.ell(lx, 0.46, 0.22, 0.3, mat, part="l_lobe")
    c.anchor("l_loop", lx - 0.085, -0.02, "l_loop", 0)
    c.anchor("l_lobe", lx + 0.02, 0.5, "l_lobe")
    # right earring, side view showing the post / hook
    rx = 0.12
    c.ell(rx, -0.42, 0.11, 0.27, mat, part="r_body")
    c.arc(rx, -0.02, 0.05, 0.1, 0, 360, 0.05, mat, part="r_loop", minpx=2)
    c.ell(rx, 0.46, 0.1, 0.29, mat, part="r_lobe")
    bent = any(t.endswith(("bent_post", "twisted_hook")) for t in fl)
    if hook:
        pts = [(rx + 0.1, -0.42), (rx + 0.3, -0.74), (rx + 0.58, -0.7), (rx + 0.68, -0.4), (rx + 0.66, -0.1)]
        if bent:
            pts = pts[:3] + [(rx + 0.74, -0.52), (rx + 0.5, -0.3)]
    else:
        pts = [(rx + 0.1, -0.42), (rx + 0.42, -0.42), (rx + 0.74, -0.42)]
        if bent:
            pts = [(rx + 0.1, -0.42), (rx + 0.42, -0.42), (rx + 0.66, -0.66)]
    c.tube(pts, 0.05, mat, part="post", minpx=2)
    c.ell(rx + 0.12, -0.42, 0.06, 0.09, mat, part="joint")
    c.anchor("joint", rx + 0.12, -0.42, ["joint", "r_body"])
    c.anchor("r_body", rx - 0.01, -0.36, "r_body")
    # separate fastener shown beside the pair
    fx, fy = 0.62, 0.42
    c.ell(fx, fy, 0.17, 0.13, mat, part="fastener")
    c.arc(fx - 0.15, fy - 0.1, 0.07, 0.07, 0, 360, 0.045, mat, part="fastener", minpx=2)
    c.arc(fx + 0.15, fy - 0.1, 0.07, 0.07, 0, 360, 0.045, mat, part="fastener", minpx=2)
    c.ell(fx, fy, 0.035, 0.03, mat, flat=1, part="fastener")
    c.anchor("fastener", fx, fy, "fastener")


def bar(c, v):
    """Minted / cast bar in 3/4 view. v['bar'] = (half_len, front_h, depth, taper, style)"""
    mat = v["mat"]
    hl, fh, dep, taper, style = v.get("bar", (1.5, 0.55, 0.62, 0.0, "orbit"))
    sk = 0.42                                           # skew of the receding top face
    x0, x1 = -hl - sk * dep / 2, hl - sk * dep / 2
    yt = -(fh + dep) / 2 + dep                          # y of the front top edge
    A, B = (x0 + taper, yt), (x1 - taper, yt)           # front top edge
    C, D = (x1 - taper + sk * dep, yt - dep), (x0 + taper + sk * dep, yt - dep)   # rear top edge
    E, F = (x0, yt + fh), (x1, yt + fh)                 # front bottom edge
    G = (x1 + sk * dep, yt + fh - dep)                  # rear bottom right
    c.poly([A, B, C, D], mat, 5, "top")
    c.poly([A, B, F, E], mat, 3, "front")
    c.poly([B, C, G, F], mat, 2, "side")
    p = c.px(1)
    # edge accents
    c.seg(A[0] + p, A[1], B[0] - p, B[1], mat, 6)
    c.seg(E[0] + p, E[1] - p, F[0] - p, F[1] - p, None, "-1")
    # polished streak on the top face
    c.poly([(x0 + hl * 0.55, yt - p), (x0 + hl * 0.8, yt - p), (x0 + hl * 0.8 + sk * dep, yt - dep + p),
            (x0 + hl * 0.55 + sk * dep, yt - dep + p)], mat, 6, "top")
    cxm, cym = (A[0] + C[0]) / 2, (A[1] + C[1]) / 2
    if style in ("orbit", "starburst"):
        # inset stamp field with emblem and abstract mark blocks (no readable text)
        c.poly([(cxm - 0.42, cym + dep * 0.3), (cxm + 0.3, cym + dep * 0.3), (cxm + 0.42, cym - dep * 0.3),
                (cxm - 0.3, cym - dep * 0.3)], mat, 4, "stamp")
        if style == "orbit":
            c.arc(cxm - 0.14, cym, 0.11, 0.07, 0, 360, 0.03, mat, flat=2, part="stamp", minpx=1)
        else:
            _star(c, cxm - 0.14, cym, 0.1, mat, "stamp", tone=2, n=4)
        for i in range(3):
            c.box(cxm + 0.04 + i * 0.09, cym - 0.05, cxm + 0.09 + i * 0.09, cym + 0.05, mat, 2, "stamp", bevel=False)
        c.box(B[0] - 0.62, yt + fh * 0.3, B[0] - 0.2, yt + fh * 0.3 + max(2 * p, 0.1), mat, 2, "mass", bevel=False)
    else:
        # cast ingot: pour ripples and a shallow batch mark
        for i in range(3):
            xx = x0 + hl * (1.15 + i * 0.22)
            c.seg(xx, yt - 2 * p, xx + sk * dep * 0.8, yt - dep * 0.8, None, "-1")
        c.box(cxm - 0.5, cym - 0.07, cxm - 0.2, cym + 0.07, mat, 4, "stamp", bevel=False)
        c.box(cxm - 0.45, cym - 0.03, cxm - 0.38, cym + 0.04, mat, 2, "stamp", bevel=False)
        c.box(cxm - 0.33, cym - 0.03, cxm - 0.26, cym + 0.04, mat, 2, "stamp", bevel=False)
        c.box(B[0] - 0.6, yt + fh * 0.3, B[0] - 0.2, yt + fh * 0.3 + max(2 * p, 0.1), mat, 2, "mass", bevel=False)
    if style == "starburst":                            # chamfered kilobar corners
        c.poly([E, (E[0] + 0.14, E[1]), (E[0], E[1] - 0.14)], None, 0)
        c.erase(c.polymask([E, (E[0] + 0.12, E[1] + p), (E[0] - p, E[1] - 0.12)]))
        c.erase(c.polymask([D, (D[0] + 0.14, D[1] - p), (D[0] - 0.07, D[1] + 0.14)]))
    if style == "tag":                                  # lot tag tied to the front face
        c.box(E[0] + 0.15, yt + fh * 0.2, E[0] + 0.6, yt + fh * 0.75, "label", 5, "tag")
        c.box(E[0] + 0.25, yt + fh * 0.4, E[0] + 0.5, yt + fh * 0.4 + max(p, 0.04), "inkblue", 3, "tag", bevel=False)
        c.anchor("tag", E[0] + 0.37, yt + fh * 0.5, "tag")
    c.anchor("face_ul", D[0] + 0.45 - sk * dep * 0.3, yt - dep * 0.7, "top")
    c.anchor("face_broad", cxm + 0.75, cym + dep * 0.1, "top")
    c.anchor("face_ur", C[0] - 0.5, yt - dep * 0.72, "top")
    c.anchor("face_low", cxm - 0.3, yt + fh * 0.5, "front")
    c.anchor("face_fr", B[0] - 0.5, yt + fh * 0.5, "front")
    c.anchor("face_up_c", cxm + 0.1, yt - dep * 0.7, "top")
    c.anchor("corner_fr", F[0], F[1], ["front", "side"], 45)
    c.anchor("corner_ur", C[0] - 0.12, C[1] + 2 * p, "top")
    c.anchor("edge_low", cxm - 0.2, yt + fh - 3 * p, "front")
    c.anchor("edge_low_out", cxm - 0.5, yt + fh, ["front"], 90)
    c.anchor("edge_right", B[0] - 0.45, yt + 2 * p, "front")
    c.anchor("seam_right", (B[0] + C[0]) / 2 + 0.02, (B[1] + G[1]) / 2 + 0.05, "side")
    c.anchor("stamp", cxm + 0.02, cym, "stamp")
    c.anchor("mass", B[0] - 0.41, yt + fh * 0.3 + p, "front")


def cup(c, v):
    mat = v["mat"]
    s = v.get("cup_scale", 1.0)
    prof = [(-1.5, 0.62), (-1.42, 0.66), (-0.9, 0.6), (-0.45, 0.42), (-0.2, 0.16), (0.3, 0.1), (0.75, 0.12),
            (1.0, 0.3), (1.3, 0.56), (1.5, 0.6)]
    prof = [(y * s, r * s) for y, r in prof]
    c.arc(0.62 * s, -0.85 * s, 0.26 * s, 0.4 * s, -80, 100, 0.09 * s, mat, part="handle", minpx=2)
    c.lathe(0, prof, mat, part="body")
    c.ell(0, -1.44 * s, 0.6 * s, 0.13 * s, mat, flat=2, part="inside")
    c.ell(0.08 * s, -1.42 * s, 0.4 * s, 0.07 * s, mat, flat=3, part="inside")
    c.arc(0, -1.44 * s, 0.63 * s, 0.14 * s, 0, 360, 0.04 * s, mat, part="rim", minpx=1, flat=5)
    c.anchor("wall", -0.12 * s, -0.95 * s, "body")
    c.anchor("rim_ur", 0.42 * s, -1.34 * s, ["rim", "body", "inside"])
    c.anchor("foot", 0.3 * s, 1.5 * s, "body", 90)
    c.anchor("inside", -0.1 * s, -1.43 * s, "inside")
    c.anchor("handle_joint", 0.5 * s, -1.05 * s, ["body", "handle"])


T = {}   # template suffix -> [(op, anchor, kwargs)]
T.update({
    "gold_ring_outer_scrape": [("scratch", "wall_left", dict(n=4, L=0.3, ang=-18))],
    "gold_ring_band_dent": [("bite", "wall_low", dict(r=0.1, shape="round", out=0.75, face=2)), ("kink", "wall_low_in", dict(rx=0.13, ry=0.09))],
    "gold_ring_band_fissure": [("crack", "wall_right", dict(L=0.4, ang=92, wide=True))],
    "gold_ring_exposed_core": [("patch", "wall_center", dict(rx=0.3, ry=0.2, mat="base"))],
    "gold_ring_solder_join": [("seam", "inner_back", dict(L=0.22, ang=90, wpx=1))],
    "gold_bracelet_arc_gouge": [("scratch", "wall_front", dict(n=2, L=0.34, ang=6, gap=3))],
    "gold_bracelet_flattened_arc": [("bite", "side_right", dict(shape="flat", ang=0, face=2)), ("kink", "side_right_in", dict(rx=0.05, ry=0.06))],
    "gold_bracelet_rim_fissure": [("crack", "wall_fl", dict(L=0.17, ang=92, wide=True))],
    "gold_bracelet_inner_plating": [("patch", "inner_lip", dict(rx=0.3, ry=0.03, mat="base", wob=0.3))],
    "gold_bracelet_joined_arc": [("seam", "wall_fr", dict(L=0.16, ang=90, wpx=2)), ("smooth", "wall_fr_top", dict(rx=0.07, ry=0.03))],
    "gold_necklace_tangled_run": [("flag", "chain_ll", {})],
    "gold_necklace_open_link": [("gap", "link_low", dict(w=3, L=0.08, ang=90))],
    "gold_necklace_clasp_jam": [("shift", "clasp", dict(rw=0.1, rh=0.07, dx=3, dy=3))],
    "gold_necklace_link_plating": [("patch", "chain_lr", dict(rx=0.11, ry=0.09, mat="base", lip=False))],
    "gold_necklace_soldered_link": [("patch", "chain_ul", dict(rx=0.08, ry=0.08, mat="solder", lip=False, dt=1))],
    "gold_earrings_bent_post": [("flag", (0.66, -0.5, "post"), {})],
    "gold_earrings_split_loop": [("gap", "l_loop", dict(w=2, L=0.07, ang=0))],
    "gold_earrings_loose_back": [("slot", "fastener", dict(rx=0.08, ry=0.06))],
    "gold_earrings_post_plating": [("patch", "l_lobe", dict(rx=0.12, ry=0.12, mat="base"))],
    "gold_earrings_repaired_mount": [("patch", "joint", dict(rx=0.08, ry=0.11, mat="solder", lip=False))],
    "gold_bar_face_score": [("scratch", "face_ul", dict(n=3, L=0.4, ang=-20, gap=2))],
    "gold_bar_corner_impact": [("bite", "corner_fr", dict(r=0.12, shape="wedge", ang=225, face=4))],
    "gold_bar_seam_core": [("stripe", "edge_low", dict(L=0.9, ang=0, wid=0.07, mat="base", dt=1))],
    "gold_bar_short_measure": [],
    "gold_bar_altered_stamp": [("ghost", "stamp", dict(rw=0.6, rh=0.3))],
    "silver_ring_groove_abrasion": [("smooth", "groove", dict(rx=0.2, ry=0.05))],
    "silver_ring_ovalized_band": [("bite", "side_right", dict(shape="flat", ang=0, face=2)), ("kink", "side_right_in", dict(rx=0.07, ry=0.1))],
    "silver_ring_shank_split": [("crack", "wall_fl", dict(L=0.3, ang=92, wide=True))],
    "silver_ring_copper_breakthrough": [("patch", "wall_fr", dict(rx=0.15, ry=0.14, mat="copper"))],
    "silver_ring_sizing_insert": [("seam", "inner_back", dict(L=0.22, ang=90)), ("seam", "inner_back2", dict(L=0.2, ang=90))],
    "silver_bracelet_engraving_loss": [("smooth", "groove", dict(rx=0.16, ry=0.035))],
    "silver_bracelet_rim_buckle": [("bite", "side_right", dict(r=0.05, shape="round", ang=0, out=0.3, face=2)), ("kink", "side_right_in", dict(rx=0.05, ry=0.06))],
    "silver_bracelet_arc_split": [("crack", "wall_fc", dict(L=0.17, ang=92, wide=True))],
    "silver_bracelet_seam_breakthrough": [("seam", "inner_seam", dict(L=0.12, ang=90, mat="copper", wpx=2, beads=False))],
    "silver_bracelet_braze_patch": [("stain", "wall_fl_low", dict(rx=0.08, ry=0.045, mat="copper", dither=True))],
    "silver_necklace_flattened_links": [("smooth", "chain_fl", dict(rx=0.2, ry=0.13, streak=False))],
    "silver_necklace_jump_ring_gap": [("gap", "connector", dict(w=3, L=0.08, ang=0))],
    "silver_necklace_spring_failure": [("shift", "clasp", dict(rw=0.1, rh=0.07, dx=3, dy=3))],
    "silver_necklace_link_core": [("patch", "chain_lr", dict(rx=0.11, ry=0.09, mat="copper", lip=False))],
    "silver_necklace_repair_link": [("patch", "chain_ul", dict(rx=0.08, ry=0.08, mat="solder", lip=False, dt=-1))],
    "silver_earrings_twisted_hook": [("flag", (0.7, -0.4, "post"), {})],
    "silver_earrings_pendant_loop_split": [("gap", "l_loop", dict(w=2, L=0.07, ang=0))],
    "silver_earrings_fastener_wear": [("slot", "fastener", dict(rx=0.08, ry=0.06))],
    "silver_earrings_body_core": [("patch", "r_body", dict(rx=0.07, ry=0.13, mat="copper"))],
    "silver_earrings_mount_rework": [("patch", "joint", dict(rx=0.08, ry=0.11, mat="solder", lip=False))],
    "silver_bar_face_abrasion": [("scratch", "face_broad", dict(n=4, L=0.42, ang=-28, gap=2))],
    "silver_bar_edge_roll": [("bite", "edge_low_out", dict(r=0.16, shape="round", ang=90, out=0.72, face=5))],
    "silver_bar_copper_seam": [("stripe", "seam_right", dict(L=0.3, ang=-62, wid=0.045, mat="copper", dt=1))],
    "silver_bar_underweight": [],
    "silver_bar_stamp_recut": [("ghost", "stamp", dict(rw=0.6, rh=0.3))],
    "silver_cup_wall_dent": [("kink", "wall", dict(rx=0.16, ry=0.2))],
    "silver_cup_rim_split": [("crack", "rim_ur", dict(L=0.3, ang=100, branch=False))],
    "silver_cup_base_wobble": [("bite", "foot", dict(shape="wedge", r=0.16, ang=45, face=2))],
    "silver_cup_inside_breakthrough": [("patch", "inside", dict(rx=0.2, ry=0.05, mat="copper", lip=False))],
    "silver_cup_handle_rejoin": [("patch", "handle_joint", dict(rx=0.09, ry=0.1, mat="solder", lip=False))],
})

M = {}   # asset_key -> (fn, half_w, half_h, variant)
for metal in ("gold", "silver"):
    for des in ("orbit", "starburst"):
        M["%s_ring_%s" % (metal, des)] = (ring, 1, 1, dict(mat=metal, design=des))
        M["%s_bracelet_%s" % (metal, des)] = (bracelet, 2, 1, dict(mat=metal, design=des))
        M["%s_necklace_%s" % (metal, des)] = (necklace, 1.12, 1.26, dict(mat=metal, design=des))
        M["%s_earrings_%s" % (metal, des)] = (earrings, 0.9, 0.84, dict(mat=metal, design=des))
        M["%s_bar_%s" % (metal, des)] = (bar, 1.8, 0.8, dict(mat=metal, design=des, bar=(
            (1.5, 0.5, 0.62, 0.0, "orbit") if des == "orbit" else (1.25, 0.7, 0.5, 0.0, "starburst"))))
M["collectible_silver_cup"] = (cup, 0.95, 1.7, dict(mat="silver"))
