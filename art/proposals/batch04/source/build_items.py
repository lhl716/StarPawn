#!/usr/bin/env python3
"""Build every physical art row of ITEM_LAYOUT_04.csv into art/proposals/batch04/items/<key>/.

    python3 build_items.py            # all rows
    python3 build_items.py gold_ring_orbit jade_rough_yellow
"""
import itertools
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

import core
import m_all
import m_gem
import m_legacy
from core import (LAYOUT, B04, render, export, templates_for, short, diff, alpha, bounds, hotspot, overlay,
                  mask_image, dilate, save, dump, text)
from px import Canvas, up

REPAIRABLE = lambda r: r["repair_class"] != "none"
OBS = {"uv": "uv", "jade_light": "light"}
SEM_BAD = [("missing_center", "loose_prong"), ("missing_insert", "lifted_bezel"), ("missing_drop", "drop_retainer"),
           ("missing_left", "flat_link")]
PAPER_BG, DESK, SLOT = (222, 207, 176, 255), (38, 48, 58, 255), (52, 66, 72, 255)
FONT = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 10)
INHERIT = {"diamond": ("sp4_gem_diamond_table_abrasion", "scratch", dict(n=2, L=0.5, ang=35, gap=2, lip=False)),
           "ruby": ("sp4_gem_ruby_dark_inclusion", "specks", dict(n=5, sc=1, tone=1)),
           "sapphire": ("sp4_gem_sapphire_uneven_zone", "stripe", dict(L=0.9, ang=70, wid=0.3, dt=2)),
           "emerald": ("sp4_gem_emerald_face_window", "stain", dict(rx=0.4, ry=0.5, flat=6, dither=True)),
           "pearl": ("sp4_gem_pearl_dull_patch", "stain", dict(rx=0.5, ry=0.4, flat=3, dither=True))}


def cmask(a, b):
    """authored-resolution change mask between two canvases"""
    return (a.mat != b.mat) | (a.tone != b.tone)


class Row:
    def __init__(self, key):
        self.key, self.row = key, LAYOUT[key]
        self.out = os.path.join(B04, "items", key)
        self.states = []          # ordered names
        self.img = {1: {}, 0: {}}
        self.can = {1: {}, 0: {}}
        self.meta = {}            # state -> dict
        self.hot = []
        self.layers = []
        self.notes = []
        self.report = {"templates": {}, "combos": [], "rejected": []}

    def add(self, name, tids=(), view=None, glow_from=None, base=None, **extra):
        for lod in (1, 0):
            c = render(self.key, lod, tids, **extra)
            g = None
            if glow_from:
                g = dilate(cmask(c, self.can[lod][glow_from]), 1)
            self.can[lod][name] = c
            self.img[lod][name] = export(c, lod, view, g)
        self.states.append(name)
        self.meta[name] = dict(templates=list(tids), view=view, base=base, extra={k: v for k, v in extra.items() if k != "ops"})
        return name

    def alias(self, name, src, **meta):
        for lod in (1, 0):
            self.img[lod][name] = self.img[lod][src]
            self.can[lod][name] = self.can[lod][src]
        self.states.append(name)
        self.meta[name] = dict(self.meta[src], alias_of=src, **meta)

    def write(self):
        for lod, kind in ((1, "paper"), (0, "items")):
            for n in self.states:
                save(self.img[lod][n], self.out, kind, n + ".png")


def template_states(R, tpls, prefix, base, extra, primary=True, front_only=False):
    """Singles, validated combinations and repaired composites for one variant."""
    key = R.key
    singles = {}
    for t in tpls:
        tid, sh = t["template_id"], short(t["template_id"])
        name = prefix + sh
        if t["mask_policy"] == "unchanged":
            R.alias(name, base, templates=[tid], hidden=True, base=base)
        else:
            R.add(name, [tid], base=base, **extra)
        singles[tid] = name
        for lod, kind in ((1, "paper"), (0, "items")):
            d = diff(R.img[lod][name], R.img[lod][base])
            a_eq = bool((alpha(R.img[lod][name]) == alpha(R.img[lod][base])).all())
            inside = bool((~d | alpha(R.img[lod][base]) | alpha(R.img[lod][name])).all())
            contained = bool((alpha(R.img[lod][name]) <= alpha(R.img[lod][base])).all())
            b = bounds(d)
            rec = R.report["templates"].setdefault(prefix + tid, {})
            rec[kind] = dict(changed_px=int(d.sum()), alpha_equal=a_eq, within_object=contained, bounds=b)
            rec["policy"] = t["mask_policy"]
            hs = dict(asset_key=key, state=name, image=kind, image_size=list(R.img[lod][name].size), TemplateId=tid,
                      Id=sh, CauseId=tid, ViewId=t["view_id"], InstrumentKind="" if t["instrument_kind"] == "none" else t["instrument_kind"],
                      status="planned_04_not_runtime")
            if b is None:
                hs.update(U=None, V=None, Width=None, Height=None, bounds_px=None, reveal_gate=t["discovery_gate"],
                          note="instrument/record-only: intact art reused, no clickable region")
            else:
                hs.update(hotspot(b, R.img[lod][name].size))
            R.hot.append(hs)
            if primary and b is not None:
                save(overlay(R.img[lod][name], d), R.out, "layers", kind, "overlay_%s.png" % sh)
                R.layers.append("layers/%s/overlay_%s.png" % (kind, sh))
                if not a_eq:
                    save(mask_image(alpha(R.img[lod][name])), R.out, "layers", kind, "mask_%s.png" % sh)
                    save(mask_image(alpha(R.img[lod][base]) & ~alpha(R.img[lod][name])), R.out, "layers", kind, "removed_%s.png" % sh)
        ob = OBS.get(t["instrument_kind"])
        if ob and t["mask_policy"] != "unchanged":
            on = name + "__" + ob
            for lod in (1, 0):
                c = R.can[lod][name]
                g = dilate(cmask(c, R.can[lod][base]), 1)
                R.img[lod][on] = export(c, lod, ob, g)
                R.can[lod][on] = c
            R.states.append(on)
            R.meta[on] = dict(templates=[tid], view=ob, base=base, planned_view=True)
            if primary:
                for lod, kind in ((1, "paper"), (0, "items")):
                    save(R.img[lod][on], R.out, "layers", kind, "observation_%s_%s.png" % (ob, sh))
    # ---- combinations
    vis = [t for t in tpls if t["mask_policy"] != "unchanged"]
    masks = {t["template_id"]: {lod: cmask(R.can[lod][singles[t["template_id"]]], R.can[lod][base]) for lod in (1, 0)} for t in vis}

    def sem_ok(a, b):
        sa, sb = short(a["template_id"]), short(b["template_id"])
        for x, y in SEM_BAD:
            if (sa.endswith(x) and sb.endswith(y)) or (sa.endswith(y) and sb.endswith(x)):
                return "one part is both absent and inspected as present"
        return None

    def geo_ok(a, b):
        ma, mb = masks[a["template_id"]], masks[b["template_id"]]
        if (dilate(ma[1], 1) & mb[1]).any() or (ma[0] & mb[0]).any():
            return "defect layers overlap or touch"
        return None

    def try_combo(group):
        tids = [t["template_id"] for t in group]
        for lod in (1, 0):
            c = render(key, lod, tids, **extra)
            m = cmask(c, R.can[lod][base])
            un = np.zeros_like(m)
            for t in tids:
                sm = masks[t][lod]
                sc = R.can[lod][singles[t]]
                if not ((c.mat[sm] == sc.mat[sm]).all() and (c.tone[sm] == sc.tone[sm]).all()):
                    return "combined render changes a single-defect layer"
                un |= sm
            if (m != un).any():
                return "combined render differs from stacked single layers"
        return None

    pairs, tried = [], []
    for a, b in itertools.combinations(vis, 2):
        why = sem_ok(a, b) or geo_ok(a, b) or try_combo((a, b))
        if why:
            R.report["rejected"].append(dict(variant=prefix, templates=[a["template_id"], b["template_id"]], reason=why))
        else:
            pairs.append((a, b))
    score = lambda g: -(int(any(REPAIRABLE(t) for t in g)) + int(any(not REPAIRABLE(t) for t in g)))
    pairs.sort(key=score)
    chosen, used = [], set()
    for g in pairs:
        ids = {t["template_id"] for t in g}
        if len(chosen) < 3 and (not ids <= used or len(chosen) < 2):
            chosen.append(g)
            used |= ids
    ok_pairs = {frozenset(t["template_id"] for t in g) for g in pairs}
    triple = None
    for g in sorted(itertools.combinations(vis, 3), key=score):
        if all(frozenset((x["template_id"], y["template_id"])) in ok_pairs for x, y in itertools.combinations(g, 2)):
            if not try_combo(g):
                triple = g
                break
    if triple:
        chosen.append(triple)
    elif len(vis) >= 3:
        R.notes.append("%sno valid three-template combination: every candidate triple overlaps or conflicts." % (prefix and prefix + ": "))
    out = []
    for g in chosen:
        tids = [t["template_id"] for t in g]
        name = prefix + "combo__" + "__".join(short(t) for t in tids)
        R.add(name, tids, base=base, **extra)
        for t in g:
            for lod, kind in ((1, "paper"), (0, "items")):
                b = bounds(dilate(masks[t["template_id"]][lod], 0))
                sc = 2 if lod else 1
                if b is None:
                    continue
                bb = (b[0] * sc, b[1] * sc, b[2] * sc, b[3] * sc)
                hs = dict(asset_key=key, state=name, image=kind, image_size=list(R.img[lod][name].size), TemplateId=t["template_id"],
                          Id=short(t["template_id"]), CauseId=t["template_id"], ViewId=t["view_id"],
                          InstrumentKind="" if t["instrument_kind"] == "none" else t["instrument_kind"], status="planned_04_not_runtime")
                hs.update(hotspot(bb, R.img[lod][name].size))
                R.hot.append(hs)
        rep = [t["template_id"] for t in g if REPAIRABLE(t)]
        keep = [t["template_id"] for t in g if not REPAIRABLE(t)]
        entry = dict(profile=name, variant=prefix, templates=tids, repairable=rep, retained_after_repair=keep, repaired_state=None)
        if rep:
            rn = prefix + "repaired__" + "__".join(short(t) for t in tids)
            R.add(rn, keep, base=base, **extra)
            R.meta[rn]["repaired_from"] = name
            entry["repaired_state"] = rn
        out.append(entry)
    for t in tpls:
        tid = t["template_id"]
        out.append(dict(profile=singles[tid], variant=prefix, templates=[tid], repairable=[tid] if REPAIRABLE(t) else [],
                        retained_after_repair=[] if REPAIRABLE(t) else [tid],
                        repaired_state=base if REPAIRABLE(t) else None, hidden_reuses_intact=t["mask_policy"] == "unchanged"))
    out.append(dict(profile=base, variant=prefix, templates=[], repairable=[], retained_after_repair=[], repaired_state=None))
    R.report["combos"] += out
    return singles


def base_layers(R, name, tag):
    for lod, kind in ((1, "paper"), (0, "items")):
        save(R.img[lod][name], R.out, "layers", kind, "base_%s.png" % tag)
        save(mask_image(alpha(R.img[lod][name])), R.out, "layers", kind, "mask_%s.png" % tag)


def reading_example(R, base, t):
    """Separate diagram for an instrument/record-only template; never a runtime sprite."""
    c = Canvas(192, 128, 1.9, 1.27, 1, 0)
    kind = t["instrument_kind"]
    c.box(0.55, 0.35, 1.8, 1.15, "dark", 2, "panel", r=0.06)
    if kind == "scale":
        c.ell(0.85, 0.62, 0.22, 0.07, "silver", part="icon")
        c.box(0.7, 0.66, 1.0, 0.76, "dark", 4, "icon")
    elif kind == "assay":
        c.box(0.68, 0.5, 1.0, 0.76, "plastic", 4, "icon")
        c.box(0.74, 0.56, 0.9, 0.66, "screen", 5, "icon", bevel=False)
    else:
        c.poly([(0.7, 0.46), (0.94, 0.46), (1.0, 0.52), (1.0, 0.8), (0.7, 0.8)], "sheet", 5, "icon")
        c.box(0.76, 0.56, 0.94, 0.59, "inkblue", 4, "icon", bevel=False)
    c.box(1.1, 0.5, 1.7, 0.6, "inkblue", 5, "claim", bevel=False)      # claimed
    c.box(1.1, 0.68, 1.46, 0.78, "red", 4, "measured", bevel=False)    # recorded, shorter
    c.box(0.7, 0.92, 1.7, 0.98, "sheet", 4, "caption", bevel=False)
    c.outline()
    im = R.img[1][base].copy()
    im.alpha_composite(up(c.image(), 2))
    save(im, R.out, "layers", "paper", "reading_example_%s.png" % short(t["template_id"]))


# --------------------------------------------------------------------- builders
def build_appraisable(R):
    row, key = R.row, R.key
    tpls = templates_for(row)
    cat = row["category"]
    if cat == "jade_rough":
        R.add("sealed")
        base_layers(R, "sealed", "sealed")
        front = [t for t in tpls if t["view_id"] == "front"]
        template_states(R, front, "", "sealed", {})
        for lod in (1, 0):
            R.img[lod]["uv_observation"] = export(R.can[lod]["sealed"], lod, "uv")
            R.can[lod]["uv_observation"] = R.can[lod]["sealed"]
        R.states.append("uv_observation")
        R.meta["uv_observation"] = dict(templates=[], view="uv", base="sealed", planned_view=True)
        for tier in m_all.m_jade.TIERS:
            b = R.add("window_open_" + tier, window=True, tier=tier)
            R.add("cut_surface_" + tier, cut=True, tier=tier)
            base_layers(R, b, "window_open_" + tier)
            for lod in (1, 0):
                R.img[lod]["light_observation_" + tier] = export(R.can[lod][b], lod, "light")
                R.can[lod]["light_observation_" + tier] = R.can[lod][b]
            R.states.append("light_observation_" + tier)
            R.meta["light_observation_" + tier] = dict(templates=[], view="light", base=b, planned_view=True)
            template_states(R, tpls, "window_%s_" % tier, b, dict(window=True, tier=tier), primary=(tier == "nuo"))
        R.notes.append("Sealed exterior states carry no tier: one file serves dou, nuo and bing. Tier appears only in window_open_*, cut_surface_* and light_observation_* states.")
        R.notes.append("Window-view templates exist only on window_* states (ViewId=window).")
    elif row["tier_policy"] == "visible_dou_nuo_bing":
        for tier in m_all.m_jade.TIERS:
            b = R.add(tier + "_intact", tier=tier)
            base_layers(R, b, tier + "_intact")
            for ob, nm in (("light", "light_observation"), ("uv", "uv_observation")):
                n = "%s_%s" % (tier, nm)
                for lod in (1, 0):
                    R.img[lod][n] = export(R.can[lod][b], lod, ob)
                    R.can[lod][n] = R.can[lod][b]
                R.states.append(n)
                R.meta[n] = dict(templates=[], view=ob, base=b, planned_view=True)
            template_states(R, tpls, tier + "_", b, dict(tier=tier), primary=(tier == "nuo"))
        R.notes.append("No template of this category has a repair class, so no repaired composite exists; combinations.json maps repaired_state to null.")
    elif cat.startswith("jewelry_"):
        form, socks = m_gem.SETTINGS[key]
        srow = [r for r in core.read_csv("docs/JEWELRY_SETTINGS_04.csv") if r["assembly_asset_key"] == key][0]
        assert int(srow["socket_count"]) == len(socks) and srow["socket_anchors"].split("|") == socks
        stones = srow["allowed_stone_asset_keys"].split("|")
        binds = []
        first = True
        for metal, mname in (("gold", "au18k"), ("silver", "ag925")):
            for g in stones:
                pre = "%s_%s_" % (mname, g[4:])
                ex = dict(metal=metal, stones={s: g for s in socks})
                b = R.add(pre + "intact", **ex)
                for lod, kind in ((1, "paper"), (0, "items")):
                    c = R.can[lod][b]
                    sc = 2 if lod else 1
                    st_all = np.zeros((c.h, c.w), bool)
                    for s in socks:
                        sm = c.sel("stone_" + s)
                        st_all |= sm
                        save(overlay(R.img[lod][b], np.kron(sm, np.ones((sc, sc), bool))), R.out, "layers", kind, "%sstone_%s.png" % (pre, s))
                        x, y, s_ = c.sockets[s]
                        px_, py_ = c.P(x, y)
                        loose = Canvas(*core.size_of(g, lod), core.MODELS[g][1], core.MODELS[g][2], lod, 6 if lod else 3)
                        binds.append(dict(assembly=key, variant=pre[:-1], image=kind, socket_id=s, stone_asset_key=g,
                                          stone_source_layer="items/%s/layers/%s/base_intact.png" % (g, kind),
                                          stone_layer="layers/%s/%sstone_%s.png" % (kind, pre, s),
                                          socket_center_px=[round(px_ * sc, 2), round(py_ * sc, 2)],
                                          scale_from_source=round((s_ * c.u) / (0.92 * loose.u), 4),
                                          setting_asset_key=srow["setting_asset_key"]))
                    save(overlay(R.img[lod][b], np.kron(c.solid & ~st_all, np.ones((sc, sc), bool))), R.out, "layers", kind, pre + "metal.png")
                    save(mask_image(alpha(R.img[lod][b])), R.out, "layers", kind, "mask_%sintact.png" % pre)
                template_states(R, tpls, pre, b, ex, primary=first)
                if first:                                 # one inherited child-evidence example
                    kind_ = g.split("_")[1]
                    tid, op, kw = INHERIT[kind_]
                    c = R.can[1][b]
                    x, y, s_ = c.sockets[socks[0]]
                    kw2 = dict(kw)
                    for k in ("L", "wid", "rx", "ry"):
                        if k in kw2:
                            kw2[k] = kw2[k] * s_
                    n = R.add(pre + "inherited__" + short(tid), base=b, ops=[(op, (x, y, "stone_" + socks[0]), kw2)], **ex)
                    R.meta[n]["inherited_child_template"] = tid
                    R.meta[n]["inherited_socket"] = socks[0]
                first = False
        dump(dict(assembly_asset_key=key, socket_count=len(socks), sockets=socks, bindings=binds,
                  note="scale_from_source maps the loose stone's authored layer onto the socket; stones are redrawn at socket size, not resampled."),
             R.out, "stone_bindings.json")
        R.notes.append("Variants: au18k/ag925 x every stone in JEWELRY_SETTINGS_04.csv. Each socket holds one separate stone layer; no decorative stones exist outside the sockets.")
        R.notes.append("The inherited__* state shows one child stone's own visible template through the mounted pose; other child evidence needs demounting.")
        R.sockets = socks
    else:
        b = R.add("intact")
        base_layers(R, b, "intact")
        template_states(R, tpls, "", b, {})
        if cat.startswith("gem_"):
            for lod in (1, 0):
                R.img[lod]["instrument_observation"] = export(R.can[lod][b], lod, "light")
                R.can[lod]["instrument_observation"] = R.can[lod][b]
            R.states.append("instrument_observation")
            R.meta["instrument_observation"] = dict(templates=[], view="light", base=b, planned_view=True)
            R.notes.append("Gem chips, inclusions and treatments have no repair class: no repaired state is generated.")
    for t in tpls:
        if t["mask_policy"] == "unchanged":
            base = [n for n in R.states if R.meta[n].get("templates") == [] and not R.meta[n].get("view")][0]
            reading_example(R, base, t)
    R.tpls = tpls


def build_setting(R):
    for metal, mname in (("gold", "au18k"), ("silver", "ag925")):
        b = R.add(mname + "_empty", metal=metal)
        base_layers(R, b, mname + "_empty")
        R.add(mname + "_service_worn", base=b, metal=metal, ops=[("scratch", "wear", dict(n=3, L=0.2, ang=-30, gap=2)), ("smooth", "wear", dict(rx=0.08, ry=0.04))])
        R.add(mname + "_repaired", base=b, metal=metal, ops=[("seam", "wear", dict(L=0.1, ang=70, wpx=1))])
    R.notes.append("Same geometry as the mounted model with every stone layer removed. An empty service setting is a manufacturing state, not a missing-stone defect.")
    R.notes.append("repaired keeps one small join as persistent service history.")
    R.reason = "service component: functional states only, no appraisal defect combinations"


def build_legacy(R):
    fam = m_legacy.legacy_states()[R.key]
    base_id = fam[0][0]
    for rid, ev, has_win in fam:
        tids = [e["TemplateId"] for e in ev if e["ViewId"] == "front"]
        R.add(rid, tids)
        if rid == base_id:
            base_layers(R, rid, rid)
        if has_win:
            wt = [e["TemplateId"] for e in ev]
            R.add(rid + "__window", wt, window=True, tier="nuo")
        for e in ev:
            img_state = rid + "__window" if e["ViewId"] == "window" else rid
            ref = (base_id + "__window") if e["ViewId"] == "window" else base_id
            if ref not in R.img[1]:
                continue
            solo = {}
            for lod, kind in ((1, "paper"), (0, "items")):
                c = render(R.key, lod, [e["TemplateId"]], **(dict(window=True, tier="nuo") if e["ViewId"] == "window" else {}))
                d = diff(export(c, lod), R.img[lod][ref])
                b = bounds(d)
                hs = dict(asset_key=R.key, state=img_state, image=kind, image_size=list(R.img[lod][rid].size), TemplateId=e["TemplateId"],
                          Id=e["Id"], CauseId=e["CauseId"], ViewId=e["ViewId"], InstrumentKind=e["InstrumentKind"],
                          status="legacy_0.3_ids_preserved; geometry update needs matching texture adoption",
                          previous_runtime_center=[e["U"], e["V"]])
                if b is None:
                    R.notes.append("legacy evidence %s is not visible at %s size" % (e["Id"], kind))
                    continue
                hs.update(hotspot(b, R.img[lod][rid].size))
                R.hot.append(hs)
                if rid != base_id:
                    save(overlay(export(c, lod), d), R.out, "layers", kind, "overlay_%s.png" % e["Id"])
    R.notes.append("States are the existing 0.3 DefinitionIds of this family; evidence Id/CauseId/TemplateId are unchanged and no new causes were added.")
    R.notes.append("Hotspot centers moved with the new drawing; adopt texture and coordinates together or keep the 0.3 placeholder.")
    R.reason = "legacy compatibility row: existing saved condition profiles only"


def build_utility(R):
    for st in R.row["required_states"].split(";"):
        R.add(st, state=st)
    base_layers(R, R.states[0], R.states[0])
    R.reason = "functional/status states from ITEM_LAYOUT_04.csv; no appraisal defects by contract"


# --------------------------------------------------------------------- outputs
def pick_sheet_states(R):
    st = R.states
    if len(st) <= 24:
        return st
    firstv = None
    for n in st:
        if n.endswith("intact") or n in ("sealed",):
            firstv = n[:-len("intact")] if n.endswith("intact") else ""
            break
    pri = [n for n in st if n.endswith("_intact") or n in ("sealed", "intact")]
    nuo = "nuo_" if any(n.startswith("nuo_") for n in st) else ("window_nuo_" if any(n.startswith("window_nuo_") for n in st) else firstv)
    rest = [n for n in st if n.startswith(nuo) and n not in pri] if nuo else []
    other = [n for n in st if n not in pri and n not in rest and not any(n.startswith(p) for p in ("dou_", "bing_", "window_dou_", "window_bing_", "ag925_", "au18k_"))]
    return (pri[:10] + other + rest)[:28]


def comparison(R):
    names = pick_sheet_states(R)
    iw, ih = core.size_of(R.key, 0)
    cw, chh = 300, 199 + 6 + max(ih, 64) + 16
    cols = 4
    rows = (len(names) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * (cw + 6) + 6, rows * (chh + 6) + 6 + 14), DESK)
    d = ImageDraw.Draw(sheet)
    hs_by = {}
    for h in R.hot:
        if h["image"] == "paper" and h["bounds_px"]:
            hs_by.setdefault(h["state"], []).append(h)
    for i, n in enumerate(names):
        x, y = 6 + (i % cols) * (cw + 6), 6 + (i // cols) * (chh + 6)
        dark = R.meta[n].get("view") in ("uv", "light")
        card = Image.new("RGBA", (298, 199), (30, 26, 44, 255) if dark else PAPER_BG)
        card.alpha_composite(R.img[1][n].resize((298, 199), Image.NEAREST))
        dd = ImageDraw.Draw(card)
        for h in hs_by.get(n, []):                        # diagnostic bounds + center, review sheet only
            x0, y0, x1, y1 = [v * 298 / 384.0 for v in h["bounds_px"]]
            dd.rectangle([x0 - 1, y0 - 1, x1, y1], outline=(166, 71, 65, 255))
            cx_, cy_ = h["U"] * 298, h["V"] * 199
            dd.line([cx_ - 3, cy_, cx_ + 3, cy_], fill=(40, 70, 90, 255))
            dd.line([cx_, cy_ - 3, cx_, cy_ + 3], fill=(40, 70, 90, 255))
        sheet.paste(card, (x, y))
        y2 = y + 199 + 4
        slot = Image.new("RGBA", (iw, ih), SLOT)
        slot.alpha_composite(R.img[0][n])
        sheet.paste(slot, (x, y2))
        if iw <= 128:
            pp = Image.new("RGBA", (iw, ih), PAPER_BG)
            pp.alpha_composite(R.img[0][n])
            sheet.paste(pp, (x + iw + 6, y2))
        d.text((x + (2 * iw + 14 if iw <= 128 else iw + 6), y2 + 2), n[:24], font=FONT, fill=(222, 207, 176, 255))
        if len(n) > 24:
            d.text((x + (2 * iw + 14 if iw <= 128 else iw + 6), y2 + 14), n[24:48], font=FONT, fill=(222, 207, 176, 255))
            d.text((x + (2 * iw + 14 if iw <= 128 else iw + 6), y2 + 26), n[48:72], font=FONT, fill=(222, 207, 176, 255))
            d.text((x + (2 * iw + 14 if iw <= 128 else iw + 6), y2 + 38), n[72:96], font=FONT, fill=(222, 207, 176, 255))
    d.text((6, sheet.height - 14), "%s | inspection shown at the 298x199 game frame (nearest) | inventory at 1x on slot and paper | red boxes = hotspot bounds, cross = center (review only)" % R.key,
           font=FONT, fill=(168, 204, 184, 255))
    save(sheet.convert("RGB"), R.out, "comparison.png")


def finish(R):
    row, key = R.row, R.key
    R.write()
    dump(dict(asset_key=key, coordinate_contract="U,V = rectangle CENTER normalized to the image, top-left origin; Width,Height = full normalized size",
              formula="U=(x0+x1)/(2*w); V=(y0+y1)/(2*h); Width=(x1-x0)/w; Height=(y1-y0)/h",
              runtime_views=["front", "window"], planned_views=["uv", "light"], hotspots=R.hot), R.out, "hotspots.json")
    combos = R.report["combos"]
    dump(dict(asset_key=key, combinations=combos, rejected=R.report["rejected"],
              reason_if_empty=getattr(R, "reason", None) if not combos else None,
              rule="A new component is intact or carries 1-3 of these whitelisted profiles; nothing outside this list is offered."),
         R.out, "combinations.json")
    states = {}
    for n in R.states:
        m = R.meta[n]
        states[n] = dict(paper="paper/%s.png" % n, items="items/%s.png" % n, templates=m.get("templates", []),
                         view=m.get("view") or "ordinary", **{k: v for k, v in m.items() if k in ("alias_of", "hidden", "planned_view", "repaired_from", "inherited_child_template", "inherited_socket")})
    dump(dict(asset_key=key, name_en=row["name_en"], category=row["category"], subtype=row["defect_subtype"], role=row["role"],
              target_family_id=row["target_family_id"], grid=[int(row["grid_width"]), int(row["grid_height"])],
              items_px=[int(row["items_width"]), int(row["items_height"])], paper_px=[384, 256], paper_authored_px=[192, 128],
              tier_policy=row["tier_policy"], implementation_status=row["implementation_status"], states=states,
              template_checks=R.report["templates"], generator="source/generate.py"), R.out, "manifest.json")
    comparison(R)
    text('#!/usr/bin/env python3\n"""Regenerate this folder from the shared deterministic batch04 generator."""\nimport os, subprocess, sys\n'
         'here = os.path.dirname(os.path.abspath(__file__))\n'
         'gen = os.path.normpath(os.path.join(here, "..", "..", "..", "source", "build_items.py"))\n'
         'sys.exit(subprocess.call([sys.executable, gen, "%s"]))\n' % key, R.out, "source", "generate.py")
    lines = ["%s (%s)" % (row["name_en"], key), "",
             "Status: PROPOSAL, %s. Not registered in the game; no runtime file was changed." % row["implementation_status"],
             "Footprint %sx%s cells -> inventory %sx%s px; inspection 384x256 px (authored 192x128, exported 2x nearest)." % (
                 row["grid_width"], row["grid_height"], row["items_width"], row["items_height"]),
             "Current 0.3 source: %s. Planned 0.4 family: %s." % (row["source_family_or_id"] or "none (new in 0.4)", row["target_family_id"]), "",
             "States (%d):" % len(R.states)]
    for n in R.states:
        m = R.meta[n]
        extra = []
        if m.get("templates"):
            extra.append("templates: " + ", ".join(m["templates"]))
        if m.get("hidden"):
            extra.append("instrument/record-only, identical to intact by contract")
        if m.get("planned_view"):
            extra.append("planned %s observation view, needs runtime integration" % m.get("view"))
        if m.get("repaired_from"):
            extra.append("repaired composite of " + m["repaired_from"])
        lines.append("  %s%s" % (n, (" - " + "; ".join(extra)) if extra else ""))
    lines += ["", "Files: paper/, items/, layers/paper, layers/items, hotspots.json (center-based), combinations.json, manifest.json, comparison.png, source/generate.py."]
    if R.notes:
        lines += ["", "Notes and known limitations:"] + ["  - " + n for n in R.notes]
    lines += ["  - Art is generated by the shared deterministic generator in art/proposals/batch04/source; edit there, not the PNGs.",
              "  - In-game filtering at the 298x199 frame is not an integer scale and still needs validation in the game."]
    text("\n".join(lines), R.out, "README.txt")


def build(key):
    R = Row(key)
    role, cat = R.row["role"], R.row["category"]
    if role == "appraisable":
        build_appraisable(R)
    elif role == "component":
        build_setting(R)
    elif role == "legacy_appraisable":
        build_legacy(R)
    else:
        build_utility(R)
    finish(R)
    return R


if __name__ == "__main__":
    keys = sys.argv[1:] or list(LAYOUT)
    for k in keys:
        r = build(k)
        print("%-28s states=%3d hotspots=%3d combos=%2d rejected=%2d" % (k, len(r.states), len(r.hot), len([c for c in r.report["combos"] if len(c["templates"]) > 1]), len(r.report["rejected"])), flush=True)
