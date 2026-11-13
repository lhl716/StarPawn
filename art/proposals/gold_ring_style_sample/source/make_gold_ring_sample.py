#!/usr/bin/env python3
"""Stage 1 style sample: gold ring in three aligned states.

Proposal-only art source. Writes PNGs next to this folder (..), never to
mod/Textures. Deterministic: no random numbers, defects are fixed templates.

    python3 make_gold_ring_sample.py
"""
import json
import math
import os

from PIL import Image

OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

# ---------------------------------------------------------------- palette
INK = (26, 32, 48, 255)            # ink blue: shade-side outline, crack
GOLD = {
    1: (74, 47, 28, 255),          # deep shadow / lit-side outline
    2: (125, 83, 32, 255),
    3: (176, 125, 40, 255),
    4: (217, 172, 60, 255),
    5: (240, 213, 104, 255),
    6: (251, 241, 180, 255),       # restrained highlight
}
BASE = {                           # exposed gray-green base metal
    1: (39, 53, 58, 255),
    2: (67, 88, 90, 255),
    3: (107, 133, 128, 255),
    4: (157, 179, 166, 255),
}
GOLD_TO_BASE = {1: 1, 2: 1, 3: 2, 4: 3, 5: 3, 6: 4}
CLEAR = (0, 0, 0, 0)

# Scene colours used only by the comparison sheet.
PAPER = (222, 207, 176, 255)
PAPER_LINE = (196, 178, 144, 255)
DESK = (38, 48, 58, 255)
SLOT = (52, 66, 72, 255)
SLOT_EDGE = (86, 104, 100, 255)
COPPER = (79, 138, 122, 255)

# ---------------------------------------------------------------- geometry
# One ring model, two authored sizes. Lengths are in authored pixels.
PAPER_GEO = dict(w=192, h=128, cx=96.0, cy=51.0, R=52.0, r=43.0, k=0.60, d=21.0,
                 groove=(9, 10), wear_rim=2.2)
ITEM_GEO = dict(w=64, h=64, cx=32.0, cy=24.5, R=28.0, r=22.0, k=0.60, d=13.0,
                groove=(6, 6), wear_rim=1.3)

WALL_BANDS = [(-0.94, 1), (-0.78, 2), (-0.62, 3), (-0.46, 4), (-0.36, 5), (-0.30, 6),
              (-0.20, 5), (-0.04, 4), (0.34, 3), (0.62, 2), (0.83, 1), (0.94, 2), (9, 1)]
INNER_BANDS = [(-0.62, 1), (-0.15, 2), (0.30, 3), (0.52, 4), (0.60, 5), (0.78, 4),
               (0.92, 3), (9, 2)]


def band(u, table):
    for limit, tone in table:
        if u < limit:
            return tone
    return table[-1][1]


def clamp(t):
    return max(1, min(6, t))


def build(geo):
    """Return (tone grid, region grid, surface coords) for the intact ring."""
    w, h = geo["w"], geo["h"]
    cx, cy, R, r, k, d = (geo[n] for n in ("cx", "cy", "R", "r", "k", "d"))
    g0, g1 = geo["groove"]
    tone = [[0] * w for _ in range(h)]
    region = [[""] * w for _ in range(h)]
    surf = [[None] * w for _ in range(h)]

    def inside(px, py, rad, oy):
        return (px / rad) ** 2 + ((py - oy) / (rad * k)) ** 2 <= 1.0

    for y in range(h):
        for x in range(w):
            px, py = x + 0.5 - cx, y + 0.5 - cy
            top_o, top_i = inside(px, py, R, 0), inside(px, py, r, 0)
            bot_o, bot_i = inside(px, py, R, d), inside(px, py, r, d)
            solid = top_o or bot_o or (abs(px) <= R and 0 <= py <= d)
            if not solid or (top_i and bot_i):
                continue
            if top_o and not top_i:                      # flat top face
                a = math.degrees(math.atan2(py / k, px))
                t = 4
                if -165 <= a <= -100:
                    t = 5
                if -25 <= a <= 75:
                    t = 3
                rr = math.hypot(px, py / k)
                if rr > R - 1.2 and 80 <= a <= 170:      # lit front-left edge
                    t = 5
                if rr < r + 1.2 and (a >= 150 or a <= -150):
                    t = 6                                # single glint on inner edge
                region[y][x], tone[y][x] = "top", t
                surf[y][x] = (px / R, rr, a)
            elif top_i:                                  # back inner wall
                u = px / r
                edge = -r * k * math.sqrt(max(0.0, 1 - u * u))
                v = py - edge
                t = band(u, INNER_BANDS)
                if v < 2.0:
                    t -= 1                               # shadow under the rim
                region[y][x], tone[y][x] = "inner", clamp(t)
                surf[y][x] = (u, v, 0)
            else:                                        # front outer wall
                u = max(-1.0, min(1.0, px / R))
                edge = R * k * math.sqrt(1 - u * u)
                v = py - edge
                t = band(u, WALL_BANDS)
                if v < 1.0:
                    t += 1                               # bevel catches light
                elif g0 <= v < g0 + 1:
                    t -= 2                               # orbit-rail groove, shaded
                elif g0 + 1 <= v < g1 + 1:
                    t += 1                               # groove lower lip
                elif v > d - 2.0:
                    t -= 1                               # turns away underneath
                region[y][x], tone[y][x] = "wall", clamp(t)
                surf[y][x] = (u, v, 0)

    # Outline every pixel that touches transparency. Warm on the lit side.
    out = [row[:] for row in tone]
    for y in range(h):
        for x in range(w):
            if not region[y][x]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if not (0 <= nx < w and 0 <= ny < h) or not region[ny][nx]:
                    lit = region[y][x] == "top" and (y + 0.5 - cy) < 0 and (x + 0.5 - cx) < R * 0.55
                    out[y][x] = 1 if lit else 0
                    region[y][x] = "line"
                    break
    return out, region, surf


# ---------------------------------------------------------------- defects
def worn_plating(geo, tone, region, surf):
    """Fixed template metal_plating: plating rubbed through on the front
    contact zone. Returns {(x, y): rgba} for pixels that change."""
    d, R = geo["d"], geo["R"]
    small = geo["w"] <= 64
    patch = set()
    for y, row in enumerate(region):
        for x, reg in enumerate(row):
            if reg == "wall":
                u, v, _ = surf[y][x]
                vn = v / d
                wob = 0.045 * math.sin(u * 23.0) + 0.03 * math.sin(vn * 17.0 + u * 9.0)
                main = ((u + 0.20) / 0.34) ** 2 + ((vn - 0.74) / 0.50) ** 2 < 1.0 + wob * 4
                spur = ((u + 0.50) / 0.10) ** 2 + ((vn - 0.42) / 0.20) ** 2 < 1.0
                fleck = (not small) and ((u - 0.20) / 0.045) ** 2 + ((vn - 0.70) / 0.14) ** 2 < 1.0
                if main or spur or fleck:
                    patch.add((x, y))
            elif reg == "top":
                _, rr, a = surf[y][x]
                if rr > R - geo["wear_rim"] and 96 <= a <= 128:   # rubbed front edge
                    patch.add((x, y))
    change = {}
    for (x, y) in patch:
        t = GOLD_TO_BASE[tone[y][x]]
        if (x - 1, y) not in patch and (x, y - 1) not in patch and region[y][x] == "wall":
            t = max(1, t - 1)                            # plating lip casts shadow
        change[(x, y)] = BASE[t]
    for (x, y) in patch:                                 # lit plating lip below/right
        for nx, ny in ((x + 1, y), (x, y + 1)):
            if (nx, ny) not in patch and region[ny][nx] == "wall":
                change[(nx, ny)] = GOLD[clamp(tone[ny][nx] + 1)]
    return change


def cracked_band(geo, tone, region, surf):
    """Fixed template metal_split: one fracture through the band, front right.
    The outline is left untouched so the silhouette cannot change."""
    cx, cy, R, r, k, d = (geo[n] for n in ("cx", "cy", "R", "r", "k", "d"))
    small = geo["w"] <= 64
    u0 = 0.27
    x0 = int(cx + R * u0)
    c = math.sqrt(1 - u0 * u0)
    y_in = int(cy + r * k * math.sqrt(max(0.0, 1 - (R * u0 / r) ** 2)))
    y_edge = int(cy + R * k * c)
    y_end = int(y_edge + d)
    zig = [0, 0, 1, 1, 0, -1, -1, 0, 0, 1, 2, 2, 1, 1, 0, -1, 0, 1, 1, 2, 1, 0, 0, -1, -1,
           0, 1, 1, 0, 0, 1, 2, 2, 1, 0, 0, -1, 0]
    if small:
        zig = [0, 0, 1, 0, -1, -1, 0, 1, 1, 0, -1, 0, 1, 1, 0, 0, -1, 0, 1, 0, 0, 1, 0]
    dark, glint = set(), set()
    for i, y in enumerate(range(y_in - 1, y_end + 1)):
        x = x0 + zig[i % len(zig)] - (i // (7 if small else 12))  # leans with the curve
        wide = y <= y_edge + 1 or ((not small) and (i % 11) in (4, 5))
        for xx in ([x, x + 1] if wide else [x]):
            dark.add((xx, y))
        glint.add((x + (2 if wide else 1), y))
    if not small:                                        # short fixed side branch
        by = y_edge + 12
        bx = x0 + zig[(by - y_in + 1) % len(zig)] - ((by - y_in + 1) // 12)
        for j in range(1, 6):
            dark.add((bx + j, by + j // 2))
    change = {}
    for (x, y) in dark:
        if region[y][x] in ("wall", "top"):
            change[(x, y)] = INK
    for (x, y) in glint:
        if (x, y) not in dark and region[y][x] in ("wall", "top"):
            change[(x, y)] = GOLD[clamp(tone[y][x] + 2)]
    return change


# ---------------------------------------------------------------- output
def paint(geo, tone, region, change=None):
    im = Image.new("RGBA", (geo["w"], geo["h"]), CLEAR)
    px = im.load()
    for y, row in enumerate(region):
        for x, reg in enumerate(row):
            if reg:
                t = tone[y][x]
                px[x, y] = INK if t == 0 else GOLD[t]
    for (x, y), col in (change or {}).items():
        px[x, y] = col
    return im


def overlay(geo, change):
    im = Image.new("RGBA", (geo["w"], geo["h"]), CLEAR)
    for (x, y), col in change.items():
        im.putpixel((x, y), col)
    return im


def up(im, n):
    return im.resize((im.width * n, im.height * n), Image.NEAREST)


def bbox_norm(change, geo, pad=2):
    xs = [p[0] for p in change]
    ys = [p[1] for p in change]
    x0, y0 = max(0, min(xs) - pad), max(0, min(ys) - pad)
    x1, y1 = min(geo["w"], max(xs) + 1 + pad), min(geo["h"], max(ys) + 1 + pad)
    # StarPawn contract: U/V are the rectangle CENTER (top-left image origin), computed from integer bounds.
    return dict(U=(x0 + x1) / (2.0 * geo["w"]), V=(y0 + y1) / (2.0 * geo["h"]),
                Width=(x1 - x0) / float(geo["w"]), Height=(y1 - y0) / float(geo["h"]),
                bounds_px=[x0 * 2, y0 * 2, x1 * 2, y1 * 2])


def main():
    states = ("intact", "worn_plating", "cracked_band")
    results = {}
    hotspots = []
    for kind, geo, scale in (("paper", PAPER_GEO, 2), ("items", ITEM_GEO, 1)):
        tone, region, surf = build(geo)
        changes = {"intact": {},
                   "worn_plating": worn_plating(geo, tone, region, surf),
                   "cracked_band": cracked_band(geo, tone, region, surf)}
        mask = Image.new("RGBA", (geo["w"], geo["h"]), CLEAR)
        for y, row in enumerate(region):
            for x, reg in enumerate(row):
                if reg:
                    mask.putpixel((x, y), (255, 255, 255, 255))
        size = "%dx%d" % (geo["w"] * scale, geo["h"] * scale)
        for s in states:
            assert all(region[y][x] and region[y][x] != "line" for (x, y) in changes[s]), s
            im = paint(geo, tone, region, changes[s])
            assert im.getchannel("A").tobytes() == mask.getchannel("A").tobytes(), s
            up(im, scale).save(os.path.join(OUT, "gold_ring_%s_%s_%s.png" % (s, kind, size)))
            results[(kind, s)] = im
            if s != "intact":
                up(overlay(geo, changes[s]), scale).save(
                    os.path.join(OUT, "layers", "gold_ring_%s_overlay_%s_%s.png" % (kind, s, size)))
        up(mask, scale).save(os.path.join(OUT, "layers", "gold_ring_%s_mask_%s.png" % (kind, size)))
        if kind == "paper":
            for s, tid, inst in (("worn_plating", "metal_plating", "assay"),
                                 ("cracked_band", "metal_split", "")):
                spot = dict(State=s, Id=tid, CauseId=tid, TemplateId=tid,
                            ViewId="front", InstrumentKind=inst)
                spot.update(bbox_norm(changes[s], geo))
                hotspots.append(spot)
    with open(os.path.join(OUT, "hotspots.json"), "w") as f:
        json.dump({"frame": [384, 256], "origin": "top-left",
                   "coordinate_contract": "U,V = rectangle CENTER normalized to the image; Width,Height = full normalized size",
                   "hotspots": hotspots}, f, indent=2)
        f.write("\n")

    # Side-by-side comparison sheet: inspection view on paper, inventory at 1x and 3x.
    cell_w, pad = 384, 12
    sheet = Image.new("RGBA", (pad + 3 * (cell_w + pad), pad + 256 + pad + 192 + pad + 22), DESK)
    for i, s in enumerate(states):
        x = pad + i * (cell_w + pad)
        card = Image.new("RGBA", (cell_w, 256), PAPER)
        for yy in range(31, 256, 32):
            for xx in range(0, cell_w, 2):
                card.putpixel((xx, yy), PAPER_LINE)
        card.alpha_composite(up(results[("paper", s)], 2))
        sheet.paste(card, (x, pad))
        y2 = pad + 256 + pad
        for sc, ox in ((3, 0), (1, 204)):
            side = 64 * sc
            slot = Image.new("RGBA", (side, side), SLOT_EDGE)
            slot.paste(Image.new("RGBA", (side - 2 * sc, side - 2 * sc), SLOT), (sc, sc))
            slot.alpha_composite(up(results[("items", s)], sc))
            sheet.paste(slot, (x + ox, y2))
        for sc, ox in ((1, 280),):                       # 1x on warm paper as well
            slot = Image.new("RGBA", (64, 64), PAPER)
            slot.alpha_composite(results[("items", s)])
            sheet.paste(slot, (x + ox, y2))
    sw = [INK] + [GOLD[i] for i in range(1, 7)] + [BASE[i] for i in range(1, 5)]
    for i, col in enumerate(sw):
        sheet.paste(Image.new("RGBA", (22, 14), col), (pad + i * 24, sheet.height - pad - 14))
    sheet.paste(Image.new("RGBA", (sheet.width - 2 * pad - len(sw) * 24 - 8, 2), COPPER),
                (pad + len(sw) * 24 + 8, sheet.height - pad - 8))
    sheet.convert("RGB").save(os.path.join(OUT, "gold_ring_states_comparison.png"))


if __name__ == "__main__":
    os.makedirs(os.path.join(OUT, "layers"), exist_ok=True)
    main()
