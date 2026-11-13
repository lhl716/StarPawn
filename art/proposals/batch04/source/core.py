"""Shared build core: manifests, rendering of one model state, diffs, hotspots."""
import csv
import json
import os

import numpy as np
from PIL import Image

import ops
from px import Canvas, up

SRC = os.path.dirname(os.path.abspath(__file__))
B04 = os.path.normpath(os.path.join(SRC, ".."))
ROOT = os.path.normpath(os.path.join(B04, "..", "..", ".."))


def read_csv(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


LAYOUT = {r["asset_key"]: r for r in read_csv("art/ITEM_LAYOUT_04.csv")}
CATALOG = read_csv("docs/DEFECT_CATALOG_04.csv")
BY_TID = {r["template_id"]: r for r in CATALOG}
MODELS, TOPS = {}, {}


def register(mod):
    MODELS.update(mod.M)
    TOPS.update(getattr(mod, "T", {}))


def templates_for(row):
    sub = row["defect_subtype"] or "all_models"
    return [r for r in CATALOG if r["category"] == row["category"] and r["subtype"] == sub]


def short(tid, row=None):
    s = tid[4:] if tid.startswith("sp4_") else tid
    if s.startswith("rough_"):
        return s.split("_", 2)[2]
    cat = BY_TID[tid]["category"] if tid in BY_TID else ""
    return s[len(cat) + 1:] if cat and s.startswith(cat + "_") else s


def size_of(key, lod):
    r = LAYOUT[key]
    return (192, 128) if lod else (int(r["items_width"]), int(r["items_height"]))


def render(key, lod, tids=(), **extra):
    """Draw one model state. tids are full template ids applied in catalog order."""
    fn, hw, hh, v = MODELS[key]
    w, h = size_of(key, lod)
    c = Canvas(w, h, hw, hh, lod, margin=6 if lod else 3)
    tids = [t for t in tids]
    v = dict(v, flags=tuple(tids), key=key, **extra)
    c.v = v
    fn(c, v)
    todo = []
    for t in tids:
        for op, anchor, kw in TOPS.get(t[4:] if t.startswith("sp4_") else t, []):
            todo.append((op, anchor, kw))
    todo += list(v.get("ops", []))
    for op, anchor, kw in todo:
        if op in ops.PRE:
            ops.PRE[op](c, anchor, **kw)
    c.outline()
    if getattr(c, "post_outline", None):
        c.post_outline()
    for op, anchor, kw in todo:
        if op in ops.POST:
            ops.POST[op](c, anchor, **kw)
    return c


def export(c, lod, view=None, glow=None):
    return up(c.image(view, glow), 2 if lod else 1)


def arr(im):
    return np.array(im.convert("RGBA"))


def diff(a, b):
    """bool mask of pixels that differ between two same-size RGBA images."""
    return (arr(a) != arr(b)).any(axis=2)


def alpha(im):
    return arr(im)[:, :, 3] > 0


def bounds(mask, pad=0):
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return None
    h, w = mask.shape
    return (max(0, int(xs.min()) - pad), max(0, int(ys.min()) - pad),
            min(w, int(xs.max()) + 1 + pad), min(h, int(ys.max()) + 1 + pad))


def hotspot(b, size):
    """Center-based normalized rectangle from integer bounds [x0,x1) x [y0,y1)."""
    x0, y0, x1, y1 = b
    w, h = size
    return dict(U=(x0 + x1) / (2.0 * w), V=(y0 + y1) / (2.0 * h), Width=(x1 - x0) / float(w),
                Height=(y1 - y0) / float(h), bounds_px=[x0, y0, x1, y1])


def overlay(state_im, mask):
    a = arr(state_im)
    a[~mask] = 0
    return Image.fromarray(a, "RGBA")


def mask_image(m):
    a = np.zeros(m.shape + (4,), np.uint8)
    a[m] = 255
    return Image.fromarray(a, "RGBA")


def dilate(m, n=1):
    for _ in range(n):
        p = np.pad(m, 1)
        m = p[1:-1, 1:-1] | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    return m


def save(im, *path):
    p = os.path.join(*path)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    im.save(p, optimize=False)
    return p


def dump(obj, *path):
    p = os.path.join(*path)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w", encoding="utf-8") as f:
        json.dump(obj, f, indent=1, ensure_ascii=False)
        f.write("\n")


def text(s, *path):
    p = os.path.join(*path)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w", encoding="utf-8") as f:
        f.write(s if s.endswith("\n") else s + "\n")
