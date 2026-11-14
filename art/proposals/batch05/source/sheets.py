"""Native-style portrait sheets (440x616): handbook page, certificate, order receipt.

Layout follows art/VANILLA_HANDBOOK_SPEC_04.txt. Skins are text-free; all copy is
rendered by the game. The issuer mark is an original StarPawn motif.
"""
from PIL import Image, ImageDraw

from uikit import (new, plate, txt, SHEET, SHEET_SH, SHEET_HI, HEAD, INK, RED, OKG, CLEAR, dither)

W, H = 440, 616
SLOTS = dict(issuer=(28, 24, 332, 16), title=(28, 44, 332, 28), ctrl_a=(390, 8, 18, 18), ctrl_b=(412, 8, 18, 18),
             intro=(28, 80, 384, 48),
             s1_head=(20, 140, 400, 20), s1_text=(28, 164, 384, 44), s1_left=(40, 216, 160, 104), s1_right=(240, 216, 160, 104),
             s1_cap_left=(40, 324, 160, 20), s1_cap_right=(240, 324, 160, 20),
             s2_head=(20, 360, 400, 20), s2_text=(28, 384, 384, 44), s2_left=(40, 436, 160, 104), s2_right=(240, 436, 160, 104),
             s2_cap_left=(40, 544, 160, 20), s2_cap_right=(240, 544, 160, 20),
             footer=(28, 580, 384, 20), prev=(28, 580, 60, 20), next=(352, 580, 60, 20))
STAMP = (150, 160, 150, 255)


def base(stamp=True):
    im = plate(W, H, SHEET, SHEET_HI, SHEET_SH)
    d = ImageDraw.Draw(im)
    # top bracket rules either side of the issuer line
    for x0, x1, dx in ((8, 110, 1), (330, 384, -1)):
        d.line([x0, 36, x1, 36], fill=HEAD)
        d.line([x1, 36, x1 + 6 * dx, 42], fill=HEAD)
    # window controls
    d.rectangle([390, 8, 407, 25], outline=SHEET_SH)
    d.rectangle([394, 12, 403, 21], outline=INK)
    d.rectangle([396, 10, 405, 19], outline=INK)
    d.rectangle([412, 8, 429, 25], outline=SHEET_SH)
    d.line([416, 12, 425, 21], fill=INK, width=2)
    d.line([425, 12, 416, 21], fill=INK, width=2)
    if stamp:                                             # faint original issuer mark: octagon, orbit ring, star
        cx, cy, r = 330, 104, 34
        pts = [(cx + r * c, cy + r * s) for c, s in ((0.92, 0.38), (0.38, 0.92), (-0.38, 0.92), (-0.92, 0.38), (-0.92, -0.38), (-0.38, -0.92), (0.38, -0.92), (0.92, -0.38))]
        d.polygon(pts, outline=STAMP)
        d.ellipse([cx - 22, cy - 13, cx + 22, cy + 13], outline=STAMP)
        d.polygon([(cx, cy - 9), (cx + 3, cy - 3), (cx + 9, cy), (cx + 3, cy + 3), (cx, cy + 9), (cx - 3, cy + 3), (cx - 9, cy), (cx - 3, cy - 3)], outline=STAMP)
        for i in range(8):
            d.point((cx - 28 + i * 8, cy + 24), fill=STAMP)
    return im, d


def handbook(state):
    im, d = base()
    if state == "defect_example":
        for s in ("s1", "s2"):
            x, y, w, h = SLOTS[s + "_head"]
            d.line([x, y + h, x + 120, y + h], fill=HEAD)
            for k in ("_left", "_right"):
                sx, sy, sw, sh = SLOTS[s + k]
                for ax, ay, dx, dy in ((sx, sy, 1, 1), (sx + sw - 1, sy, -1, 1), (sx, sy + sh - 1, 1, -1), (sx + sw - 1, sy + sh - 1, -1, -1)):
                    d.line([ax, ay, ax + 6 * dx, ay], fill=SHEET_SH)
                    d.line([ax, ay, ax, ay + 6 * dy], fill=SHEET_SH)
    elif state == "index":
        d.rectangle([40, 150, 399, 560], outline=HEAD)
        d.rectangle([41, 151, 398, 559], outline=HEAD)
        for i in range(1, 10):
            d.line([40, 150 + i * 41, 399, 150 + i * 41], fill=HEAD)
        d.line([300, 150, 300, 560], fill=HEAD)
    else:                                                 # introduction: one full-width illustration
        d.line([20, 160, 140, 160], fill=HEAD)
        for ax, ay, dx, dy in ((40, 216, 1, 1), (399, 216, -1, 1), (40, 539, 1, -1), (399, 539, -1, -1)):
            d.line([ax, ay, ax + 8 * dx, ay], fill=SHEET_SH)
            d.line([ax, ay, ax, ay + 8 * dy], fill=SHEET_SH)
    d.line([28, 576, 411, 576], fill=SHEET_SH)
    d.polygon([(34, 590), (42, 584), (42, 596)], fill=HEAD)   # previous / next targets
    d.polygon([(406, 590), (398, 584), (398, 596)], fill=HEAD)
    return im


def certificate(state):
    im, d = base()
    d.rectangle([40, 150, 199, 253], outline=SHEET_SH)        # object illustration slot
    for i in range(5):
        d.line([216, 160 + i * 20, 399, 160 + i * 20], fill=SHEET_SH)
    d.line([20, 280, 140, 280], fill=HEAD)
    d.rectangle([40, 292, 399, 440], outline=HEAD)
    for i in range(1, 6):
        d.line([40, 292 + i * 25, 399, 292 + i * 25], fill=HEAD)
    d.line([170, 292, 170, 440], fill=HEAD)
    d.line([20, 460, 140, 460], fill=HEAD)
    for i in range(3):
        d.line([40, 484 + i * 20, 399, 484 + i * 20], fill=SHEET_SH)
    d.line([28, 576, 411, 576], fill=SHEET_SH)
    if state == "superseded":                                 # shape + colour: diagonal band and cut corner
        d.polygon([(0, 470), (0, 520), (439, 150), (439, 100)], fill=RED)
        d.polygon([(0, 480), (0, 484), (439, 114), (439, 110)], fill=SHEET)
        d.polygon([(439, 615), (400, 615), (439, 576)], fill=CLEAR)
    else:
        d.ellipse([318, 500, 386, 568], outline=HEAD)
        d.ellipse([322, 504, 382, 564], outline=HEAD)
    return im


def receipt(state):
    im, d = base()
    d.line([20, 160, 140, 160], fill=HEAD)
    d.rectangle([40, 172, 399, 300], outline=HEAD)            # parties / custody table
    for i in range(1, 5):
        d.line([40, 172 + i * 25, 399, 172 + i * 25], fill=HEAD)
    d.line([150, 172, 150, 300], fill=HEAD)
    d.line([20, 320, 140, 320], fill=HEAD)
    for i in range(4):                                        # component rows: stone / setting identities
        d.rectangle([40, 332 + i * 36, 71, 363 + i * 36], outline=SHEET_SH)
        d.line([80, 362 + i * 36, 399, 362 + i * 36], fill=SHEET_SH)
    d.line([20, 488, 140, 488], fill=HEAD)
    d.rectangle([40, 500, 215, 560], outline=HEAD)            # held deposit / balance
    d.rectangle([224, 500, 399, 560], outline=HEAD)
    for x in range(8, 432, 6):                                # tear-off perforation
        d.point((x, 572), fill=INK)
    k = {"accepted": 1, "ready": 3, "delivered": 4, "cancelled": 0}[state]
    for i in range(4):                                        # progress boxes: shape, not colour alone
        x = 300 + i * 26
        d.rectangle([x, 44, x + 18, 62], outline=HEAD)
        if i < k:
            d.rectangle([x + 4, 48, x + 14, 58], fill=HEAD)
    if state == "delivered":
        d.ellipse([300, 500, 368, 568], outline=OKG)
        d.line([318, 534, 330, 548, 352, 518], fill=OKG, width=3)
    if state == "cancelled":
        d.polygon([(0, 470), (0, 520), (439, 150), (439, 100)], fill=RED)
        d.polygon([(0, 480), (0, 484), (439, 114), (439, 110)], fill=SHEET)
    return im


def fit(src, box, bg=None):
    """Fit an image into a slot at the largest integer scale, preserving aspect."""
    x, y, w, h = box
    s = max(1, min(w // src.width, h // src.height))
    if src.width > w or src.height > h:
        r = min(w / src.width, h / src.height)
        im = src.resize((max(1, int(src.width * r)), max(1, int(src.height * r))), Image.NEAREST)
    else:
        im = src.resize((src.width * s, src.height * s), Image.NEAREST)
    return im, (x + (w - im.width) // 2, y + (h - im.height) // 2)
