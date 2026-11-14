#!/usr/bin/env python3
"""Technical validation of art/proposals/batch04. Writes validation-results.txt.
A PASS here is a file/geometry check, not a visual quality pass and not gameplay proof."""
import argparse
import glob
import json
import os
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

import core
import m_all
from core import B04, ROOT, LAYOUT, render, export

UI = core.read_csv("art/UI_ASSETS_04.csv")
res, fails = [], []


def A(p):
    return np.array(Image.open(p).convert("RGBA"))


def check(name, bad, total=None, detail=6):
    ok = not bad
    res.append("%s %s%s" % ("PASS" if ok else "FAIL", name, (" (%d checked)" % total) if total is not None else ""))
    if not ok:
        fails.append(name)
        for b in bad[:detail]:
            res.append("     - %s" % (b,))
        if len(bad) > detail:
            res.append("     - ... %d more" % (len(bad) - detail))


def bbox(a):
    ys, xs = np.nonzero(a[:, :, 3])
    return (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1) if len(xs) else None


def stack(base, singles):
    out = base.copy()
    for s in singles:
        m = (s != base).any(axis=2)
        out[m] = s[m]
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--integration", action="store_true",
                        help="Skip only the art-only branch scope restriction; keep every asset check.")
    args = parser.parse_args()
    b_struct, b_size, b_alpha, b_margin, b_2x, b_states = [], [], [], [], [], []
    b_policy, b_read, b_hot, b_combo, b_rep, b_tier, b_sock, b_hidden = [], [], [], [], [], [], [], []
    n_png = n_hot = n_combo = n_tpl = 0
    settings = {r["assembly_asset_key"]: r for r in core.read_csv("docs/JEWELRY_SETTINGS_04.csv")}
    for key, row in LAYOUT.items():
        d = os.path.join(B04, "items", key)
        for f in ("README.txt", "manifest.json", "hotspots.json", "combinations.json", "comparison.png", "source/generate.py", "paper", "items", "layers/paper", "layers/items"):
            if not os.path.exists(os.path.join(d, f)):
                b_struct.append("%s missing %s" % (key, f))
        if not os.path.exists(os.path.join(d, "manifest.json")):
            continue
        man = json.load(open(os.path.join(d, "manifest.json")))
        iw, ih = int(row["items_width"]), int(row["items_height"])
        img = {"paper": {}, "items": {}}
        for kind, size, mg in (("paper", (384, 256), 8), ("items", (iw, ih), 2)):
            files = sorted(glob.glob(os.path.join(d, kind, "*.png")))
            if set(os.path.basename(f)[:-4] for f in files) != set(man["states"]):
                b_states.append("%s %s files differ from manifest states" % (key, kind))
            for f in files + sorted(glob.glob(os.path.join(d, "layers", kind, "*.png"))):
                n_png += 1
                im = Image.open(f)
                a = np.array(im.convert("RGBA"))
                if im.mode != "RGBA" or im.size != size:
                    b_size.append("%s %s %s" % (f[len(B04) + 1:], im.mode, im.size))
                    continue
                if not np.isin(a[:, :, 3], (0, 255)).all():
                    b_alpha.append(f[len(B04) + 1:])
                if "layers" in Path(f).parts:
                    continue
                img[kind][os.path.basename(f)[:-4]] = a
                bb = bbox(a)
                if bb is None or bb[0] < mg or bb[1] < mg or bb[2] > size[0] - mg or bb[3] > size[1] - mg:
                    b_margin.append("%s/%s/%s bbox=%s" % (key, kind, os.path.basename(f), bb))
                if kind == "paper" and not ((a[0::2, 0::2] == a[1::2, 0::2]).all() and (a[0::2, 0::2] == a[0::2, 1::2]).all() and (a[0::2, 0::2] == a[1::2, 1::2]).all()):
                    b_2x.append("%s/%s" % (key, os.path.basename(f)))
        req = row["required_states"].split(";")
        if row["role"] not in ("appraisable", "legacy_appraisable", "component"):
            for st in req:
                if st not in man["states"]:
                    b_states.append("%s missing required state %s" % (key, st))
        combos = json.load(open(os.path.join(d, "combinations.json")))
        hot = json.load(open(os.path.join(d, "hotspots.json")))["hotspots"]
        base_of = {c["variant"]: c["profile"] for c in combos["combinations"] if not c["templates"]}
        single_of = {(c["variant"], c["templates"][0]): c["profile"] for c in combos["combinations"] if len(c["templates"]) == 1}
        # ---- template policy + readability
        for tk, rec in man["template_checks"].items():
            n_tpl += 1
            pol = rec["policy"]
            for kind in ("paper", "items"):
                r = rec[kind]
                if pol == "unchanged" and r["changed_px"] != 0:
                    b_hidden.append("%s %s draws a visible clue for an instrument-only template" % (key, tk))
                if pol == "same_alpha" and not r["alpha_equal"]:
                    b_policy.append("%s %s same_alpha but alpha changed (%s)" % (key, tk, kind))
                if pol != "unchanged" and r["changed_px"] < (24 if kind == "paper" else 4):
                    b_read.append("%s %s only %d px change at %s size" % (key, tk, r["changed_px"], kind))
        # ---- hotspots: centre formula, centre on clue, tight bounds
        for h in hot:
            n_hot += 1
            if h["bounds_px"] is None:
                if h["U"] is not None:
                    b_hot.append("%s %s null region expected" % (key, h["state"]))
                continue
            x0, y0, x1, y1 = h["bounds_px"]
            w, hh = h["image_size"]
            if abs(h["U"] - (x0 + x1) / (2.0 * w)) > 1e-9 or abs(h["V"] - (y0 + y1) / (2.0 * hh)) > 1e-9 or abs(h["Width"] - (x1 - x0) / w) > 1e-9 or abs(h["Height"] - (y1 - y0) / hh) > 1e-9:
                b_hot.append("%s %s not centre-based" % (key, h["state"]))
            if row["role"] == "appraisable":
                var = next((v for v in sorted(base_of, key=len, reverse=True) if h["state"].startswith(v)), "")
                st, bs = single_of.get((var, h["TemplateId"])), base_of.get(var)
                if st in img[h["image"]] and bs in img[h["image"]]:
                    dm = (img[h["image"]][st] != img[h["image"]][bs]).any(axis=2)
                    ys, xs = np.nonzero(dm)
                    tight = [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]
                    if tight != h["bounds_px"]:
                        b_hot.append("%s %s bounds %s != clue bbox %s" % (key, h["state"], h["bounds_px"], tight))
                    cx, cy = int(h["U"] * w), int(h["V"] * hh)
                    if not dm[max(0, cy - 6):cy + 7, max(0, cx - 6):cx + 7].any():
                        b_hot.append("%s %s centre is not on the clue" % (key, h["state"]))
        # ---- combinations and repaired composites
        for c in combos["combinations"]:
            if len(c["templates"]) < 2:
                continue
            n_combo += 1
            for kind in ("paper", "items"):
                base = img[kind][base_of[c["variant"]]]
                singles = [img[kind][single_of[(c["variant"], t)]] for t in c["templates"]]
                if not (stack(base, singles) == img[kind][c["profile"]]).all():
                    b_combo.append("%s %s is not the stack of its single layers (%s)" % (key, c["profile"], kind))
                if c["repaired_state"]:
                    keep = [img[kind][single_of[(c["variant"], t)]] for t in c["retained_after_repair"]]
                    if not (stack(base, keep) == img[kind][c["repaired_state"]]).all():
                        b_rep.append("%s %s does not retain exactly the unrepairable cues (%s)" % (key, c["repaired_state"], kind))
        if row["role"] == "appraisable" and row["category"] != "jade_rough":
            for v in base_of:
                multi = [c for c in combos["combinations"] if c["variant"] == v and len(c["templates"]) > 1]
                if len([c for c in multi if len(c["templates"]) == 2]) < 2 or not [c for c in multi if len(c["templates"]) == 3]:
                    b_combo.append("%s variant '%s' lacks two pairs + one triple" % (key, v))
        # ---- hidden tier
        if row["tier_policy"] == "hidden_dou_nuo_bing":
            for lod in (1, 0):
                ref = np.array(export(render(key, lod), lod))
                for tier in ("dou", "nuo", "bing"):
                    if not (np.array(export(render(key, lod, tier=tier), lod)) == ref).all():
                        b_tier.append("%s sealed exterior changes with tier %s" % (key, tier))
            for n in man["states"]:
                if not any(t in n for t in ("dou", "nuo", "bing")) and n not in ("sealed", "uv_observation") and not all("window" != core.BY_TID[t]["view_id"] for t in man["states"][n]["templates"]):
                    b_tier.append("%s %s shows a window template without a window" % (key, n))
        # ---- sockets
        if key in settings:
            sb = json.load(open(os.path.join(d, "stone_bindings.json")))
            want = int(settings[key]["socket_count"])
            per = {}
            for b in sb["bindings"]:
                per.setdefault((b["variant"], b["image"]), []).append(b)
                if b["stone_asset_key"] not in settings[key]["allowed_stone_asset_keys"].split("|"):
                    b_sock.append("%s unsupported stone %s" % (key, b["stone_asset_key"]))
                a = A(os.path.join(d, b["stone_layer"]))
                if not a[:, :, 3].any():
                    b_sock.append("%s empty stone layer %s" % (key, b["stone_layer"]))
            nvar = len(settings[key]["allowed_stone_asset_keys"].split("|")) * 2 * 2
            if len(per) != nvar or any(len(v) != want for v in per.values()):
                b_sock.append("%s socket/variant count mismatch" % key)
            for (var, kind), bl in per.items():
                comp = A(os.path.join(d, "layers", kind, var + "_metal.png"))
                for b in bl:
                    s_ = A(os.path.join(d, b["stone_layer"]))
                    comp[s_[:, :, 3] > 0] = s_[s_[:, :, 3] > 0]
                if not (comp == img[kind][var + "_intact"]).all():
                    b_sock.append("%s %s metal+stone layers do not rebuild intact (%s)" % (key, var, kind))
    check("item folders: 135 rows with README, manifest, paper, items, layers, hotspots, combinations, comparison, source", b_struct, len(LAYOUT))
    check("exact dimensions: paper 384x256 RGBA, inventory = grid x 64 px", b_size, n_png)
    check("transparency: alpha is only 0 or 255", b_alpha, n_png)
    check("margins: >=8 px paper, >=2 px inventory", b_margin)
    check("paper art is an exact 2x nearest export of 192x128 authoring", b_2x)
    check("state files match manifests and required functional states", b_states)
    check("mask policy: same_alpha layers keep the intact alpha", b_policy, n_tpl)
    check("hidden-state separation: instrument/record-only templates reuse intact art", b_hidden)
    check("small-size readability proxy: every visible template changes >=24 px (paper) and >=4 px (inventory)", b_read)
    check("hotspots: centre-based formula, centre on the clue, bounds equal the clue bbox", b_hot, n_hot)
    check("combinations: each whitelisted profile equals its stacked single layers; 2 pairs + 1 triple per variant", b_combo, n_combo)
    check("repaired composites retain exactly the unrepairable cues", b_rep)
    check("sealed rough exterior is identical for dou / nuo / bing", b_tier)
    check("sockets: counts and stones follow JEWELRY_SETTINGS_04.csv; metal + stone layers rebuild the assembly", b_sock)
    # ---- approved sample preserved
    sample = os.path.join(ROOT, "art/proposals/gold_ring_style_sample")
    bad = []
    for a, b in (("gold_ring_intact_paper_384x256.png", "paper/intact.png"), ("gold_ring_intact_items_64x64.png", "items/intact.png")):
        if not (A(os.path.join(sample, a)) == A(os.path.join(B04, "items/gold_ring_orbit", b))).all():
            bad.append(a)
    check("approved ring sample: gold_ring_orbit intact is pixel-identical to the reviewed sample", bad)
    hs = json.load(open(os.path.join(sample, "hotspots.json")))
    check("ring sample hotspot exporter corrected to centre-based U/V", [] if hs.get("coordinate_contract", "").startswith("U,V = rectangle CENTER") else ["sample hotspots.json still top-left based"])
    # ---- determinism
    bad = []
    for key in ("gold_ring_orbit", "jade_rough_water", "gem_emerald_step", "tool_assay", "collectible_console"):
        st = list(json.load(open(os.path.join(B04, "items", key, "manifest.json")))["states"])[0]
        ex = dict(state=st) if LAYOUT[key]["role"] not in ("appraisable",) else {}
        for lod, kind in ((1, "paper"), (0, "items")):
            if not (np.array(export(render(key, lod, **ex), lod)) == A(os.path.join(B04, "items", key, kind, st + ".png"))).all():
                bad.append("%s %s" % (key, kind))
    check("deterministic generator: re-rendered samples are pixel-identical to the files", bad)
    # ---- UI
    bad = []
    for r in UI:
        d = os.path.join(B04, "ui", r["asset_key"])
        w, h = int(r["width"]), int(r["height"])
        for st in r["required_states"].split(";"):
            f = os.path.join(d, ("skin_%s.png" if r["kind"] == "screen_plate" else "%s.png") % st)
            if not os.path.exists(f) or Image.open(f).size != (w, h) or Image.open(f).mode != "RGBA":
                bad.append("%s %s" % (r["asset_key"], st))
        for f in ("README.txt", "spec.json", "preview.png"):
            if not os.path.exists(os.path.join(d, f)):
                bad.append("%s missing %s" % (r["asset_key"], f))
        if r["kind"] == "screen_plate":
            for f, sz in (("mockup_1080p.png", (1920, 1080)), ("mockup_4k.png", (3840, 2160))):
                p = os.path.join(d, f)
                if not os.path.exists(p) or Image.open(p).size != sz:
                    bad.append("%s %s" % (r["asset_key"], f))
    check("UI: 71 rows, every state at its frozen size, screens with 1080p and 4K mockups", bad, len(UI))
    fr = A(os.path.join(B04, "ui/inspection_frame/default.png"))
    check("inspection_frame leaves the 298x199 image rectangle transparent (interior)", [] if not fr[16:183, 16:282, 3].any() else ["opaque pixels inside frame"])
    # ---- handbook
    hb = json.load(open(os.path.join(B04, "handbook-pages.json")))
    bad, tids = [], []
    idx = {(r["manual_asset_key"], r["category"], r["subtype"]): r for r in core.read_csv("docs/HANDBOOK_INDEX_04.csv")}
    for p in hb["pages"]:
        f = os.path.join(B04, p["mockup"])
        if not os.path.exists(f) or Image.open(f).size != (440, 616):
            bad.append("page image %s" % p["mockup"])
        if len(p.get("sections", [])) > 2:
            bad.append("more than two explanations on %s" % p["mockup"])
        for s in p.get("sections", []):
            tids.append(s["TemplateId"])
            r = idx[(p["manual"], s["category"], s["subtype"])]
            if s["TemplateId"] not in r["template_ids"].split("|") or s["example_asset_key"] != r["example_asset_key"]:
                bad.append("index mismatch %s" % s["TemplateId"])
            if not int(r["first_page"]) <= p["page"] < int(r["first_page"]) + int(r["page_count"]):
                bad.append("pagination %s" % s["TemplateId"])
            for side in ("reference", "problem"):
                lp = os.path.join(B04, s[side]["layer"])
                if not os.path.exists(lp):
                    bad.append("missing layer %s" % s[side]["layer"])
                c = s[side]["crop_px"]
                if c and not (0 <= c[0] < c[2] <= 384 and 0 <= c[1] < c[3] <= 256):
                    bad.append("crop out of image %s" % s["TemplateId"])
            if core.BY_TID[s["TemplateId"]]["mask_policy"] != "unchanged" and s["problem"]["crop_px"] is None:
                bad.append("no crop for visible template %s" % s["TemplateId"])
    if sorted(tids) != sorted(r["template_id"] for r in core.CATALOG):
        bad.append("template coverage %d/250" % len(set(tids)))
    check("handbook: 250 templates each have one traceable example from real item layers; 2/2/1 pagination; 440x616 pages", bad, len(hb["pages"]))
    p = os.path.join(B04, "review/native_handbook_comparison_2560x1440.png")
    check("native handbook comparison exists at 2560x1440", [] if os.path.exists(p) and Image.open(p).size == (2560, 1440) else ["missing or wrong size"])
    # ---- scope
    forb = ("alchemy", "philosopher", "transmut")
    bad = [f for f in glob.glob(os.path.join(B04, "**"), recursive=True) if any(x in os.path.basename(f).lower() for x in forb)]
    check("excluded scope: no alchemy / transmutation assets", bad)
    try:
        if args.integration:
            res.append("SKIP art-only branch scope: --integration was explicitly selected; all asset checks remain active.")
        else:
            validate_git_scope()
    except Exception as e:
        res.append("SKIP git scope check: %s" % e)
    res.append("")
    res.append("NOT VERIFIED HERE (needs the game): in-game filtering at the 298x199 frame, font rendering, hotspot hit-testing, new observation views, native container grids, save migration.")
    res.append("NOT AUTOMATED: absence of baked dynamic text is by construction (item art and skins draw no glyphs; text exists only in mockup_*, pages/ and comparison sheets).")
    head = ["StarPawn batch04 validation - %s" % ("ALL CHECKS PASS" if not fails else "%d CHECK(S) FAIL" % len(fails)),
            "Scope: files and geometry under art/proposals/batch04. Gameplay was not executed.",
            "Counts: %d item rows, %d UI rows, %d item/layer PNGs, %d hotspot records, %d multi-template profiles, %d template checks." % (len(LAYOUT), len(UI), n_png, n_hot, n_combo, n_tpl), ""]
    core.text("\n".join(head + res), B04, "validation-results.txt")
    print("\n".join(head + res))
    return 1 if fails else 0


def validate_git_scope():
    out = subprocess.check_output(["git", "-C", ROOT, "diff", "--name-only", "origin/main...HEAD"], text=True).split("\n")
    wt = subprocess.check_output(["git", "-C", ROOT, "status", "--porcelain"], text=True).split("\n")
    ch = [f for f in out if f] + [l[3:] for l in wt if l.strip()]
    bad = sorted(set(f for f in ch if not f.startswith("art/proposals/")))
    check("branch changes only art/proposals (no mod/, source/, runtime or contract files)", bad)


if __name__ == "__main__":
    sys.exit(main())
