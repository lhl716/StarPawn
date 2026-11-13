"""Quick review sheet: python3 preview.py out.png key [key ...]"""
import sys
from PIL import Image
import core, m_all
from core import render, export, templates_for, LAYOUT

def sheet(keys, out, extra=None, lod_items=True):
    rows = []
    for k in keys:
        tl = [None] + [t["template_id"] for t in templates_for(LAYOUT[k])] if k in LAYOUT else [None]
        cells = []
        for t in tl:
            ex = dict(extra or {})
            p = export(render(k, 1, [t] if t else [], **ex), 1)
            i = export(render(k, 0, [t] if t else [], **ex), 0)
            cell = Image.new("RGBA", (384, 256 + i.height), (222, 207, 176, 255))
            cell.alpha_composite(p)
            cell.paste((38, 48, 58, 255), (0, 256, 384, 256 + i.height))
            cell.alpha_composite(i, (4, 256))
            cell.paste((222, 207, 176, 255), (200, 256, 200 + i.width, 256 + i.height))
            cell.alpha_composite(i, (200, 256))
            cells.append(cell)
        rows.append(cells)
    W = max(len(r) for r in rows) * 388
    H = sum(r[0].height + 4 for r in rows)
    im = Image.new("RGB", (W, H), (20, 24, 30))
    y = 0
    for r in rows:
        for j, cimg in enumerate(r):
            im.paste(cimg.convert("RGB"), (j * 388, y))
        y += r[0].height + 4
    im.save(out)
    print(im.size)

if __name__ == "__main__":
    sheet(sys.argv[2:], sys.argv[1])
