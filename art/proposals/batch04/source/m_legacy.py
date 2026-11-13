"""Legacy 0.3 compatibility art: old 1x1 footprints and the existing saved conditions only."""
import json
import os

import core
import m_metal, m_jade, m_goods


def leg_bracelet(c, v):
    mat = v["mat"]
    c.arc(0, 0.1, 0.82, 0.62, -60, 240, 0.2, mat, part="arc", minpx=4)
    # The clasp must bridge the two arc ends at x=+/-0.41; the old narrow
    # rectangle floated in the open gap, even in intact and repaired states.
    c.box(-0.46, -0.62, 0.46, -0.38, mat, 4, "clasp")
    c.box(-0.06, -0.56, 0.06, -0.44, mat, 2, "clasp", bevel=False)
    c.anchor("wear", -0.62, -0.28, "arc")
    c.anchor("seam", 0.66, -0.26, "arc")
    c.anchor("clasp", 0.17, -0.5, ["arc", "clasp"], 0)


def leg_handheld(c, v):
    m_goods.handheld(c, dict(v, scale=0.56))


T = {
    "legacy_surface_wear": [("scratch", "wear", dict(n=3, L=0.3, ang=-40))],
    "legacy_surface_seam": [("seam", "seam", dict(L=0.2, ang=40, wpx=2))],
    "legacy_clasp_break": [("gap", "clasp", dict(w=3, L=0.4, ang=90))],
    "legacy_plating_edge": [("patch", "wear", dict(rx=0.16, ry=0.12, mat="base"))],
    "legacy_plating_seam": [("seam", "seam", dict(L=0.22, ang=40, wpx=2, mat="base", beads=False))],
    "legacy_bar_seam": [("stripe", "edge_low", dict(L=0.8, ang=0, wid=0.08, mat="base", dt=1))],
    "legacy_bar_stamp": [("ghost", "stamp", dict(rw=0.6, rh=0.3))],
    "legacy_cup_dent": [("kink", "wall", dict(rx=0.14, ry=0.16))],
    "legacy_silver_clasp": [("gap", "clasp", dict(w=3, L=0.4, ang=90))],
    "legacy_silver_bar_seam": [("stripe", "edge_low", dict(L=0.8, ang=0, wid=0.08, mat="copper", dt=1))],
    "legacy_rough_crack": [("crack", "a1", dict(L=0.4, ang=70, wide=True))],
    "legacy_window_crack": [("crack", "win_u", dict(L=0.3, ang=80))],
    "legacy_dye_pool": [("stain", "groove", dict(rx=0.24, ry=0.14, mat="dye", dt=-1, dither="thin"))],
    "legacy_vase_restore": [("stripe", "bowl_low", dict(L=0.5, ang=-8, wid=0.04, dt=1)), ("stain", "bowl_low", dict(rx=0.22, ry=0.1, dt=1, dither="thin"))],
    "legacy_seal_mismatch": [("shift", "seal", dict(rw=0.1, rh=0.06, dx=1, dy=2))],
    "legacy_print_dots": [("halftone", "stroke_c", dict(rw=0.5, rh=0.3, dt=2))],
    "legacy_console_port": [("shift", "port", dict(rw=0.14, rh=0.14, dx=1, dy=-1))],
    "legacy_console_screw": [("slot", "screw", dict(rx=0.06, ry=0.04)), ("scratch", "screw", dict(n=2, L=0.2, ang=-30, gap=3))],
    "legacy_handheld_port": [("shift", "charge", dict(rw=0.16, rh=0.06, dx=1, dy=1))],
    "legacy_label_print": [("shift", "label_corner", dict(rw=0.34, rh=0.18, dx=1, dy=1))],
    "legacy_disc_scratch": [("scratch", "read_lr", dict(n=1, L=0.4, ang=40))],
    "legacy_reprint_mark": [("seam", "copyright", dict(L=0.14, ang=0, mat="red", wpx=1, beads=False))],
}
T = {"xx_" + k if False else k: v for k, v in T.items()}

M = {
    "legacy_gold_bracelet": (leg_bracelet, 1.0, 0.86, dict(mat="gold")),
    "legacy_silver_bracelet": (leg_bracelet, 1.0, 0.86, dict(mat="silver")),
    "legacy_gold_bar": (m_metal.bar, 1.42, 0.86, dict(mat="gold", bar=(1.0, 0.5, 0.62, 0.0, "orbit"))),
    "legacy_silver_bar": (m_metal.bar, 1.42, 0.86, dict(mat="silver", bar=(1.0, 0.5, 0.62, 0.0, "orbit"))),
    "legacy_silver_cup": (m_metal.cup, 1.0, 1.0, dict(mat="silver", cup_scale=0.6)),
    "legacy_jade_rough": (m_jade.rough, 1.02, 0.9, dict(family="yellow", scale=0.92)),
    "legacy_jade_pendant": (m_jade.pendant, 0.7, 1.0, dict(design="plain", tier="nuo")),
    "legacy_antique_vase": (m_goods.vase, 1.0, 1.0, dict(scale=0.88)),
    "legacy_painting": (m_goods.painting, 1.0, 0.86, dict(scale=0.9)),
    "legacy_console": (m_goods.console, 1.0, 0.72, dict(scale=0.86)),
    "legacy_handheld": (leg_handheld, 1.0, 0.44, {}),
    "legacy_cartridge": (m_goods.cartridge, 0.82, 1.0, dict(scale=0.94)),
    "legacy_disc": (m_goods.disc, 1.0, 0.95, dict(scale=0.94)),
    "legacy_book": (m_goods.book, 1.06, 0.82, dict(scale=0.94)),
}


def legacy_states():
    """family -> [(runtime id, [evidence dicts])] from the authoritative 0.3 map."""
    m = json.load(open(os.path.join(core.ROOT, "art", "runtime-art-map.json"), encoding="utf-8"))
    out = {}
    for it in m["items"]:
        fam = it.get("familyId", "")
        if fam in M:
            out.setdefault(fam, []).append((it["id"], it["fixedEvidence"], it.get("window") is not None))
    return out
