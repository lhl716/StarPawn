#!/usr/bin/env python3
"""Build all 71 rows of art/UI_ASSETS_04.csv into art/proposals/batch04/ui/<key>/.

Skins are text-free. Mockups add realistic Chinese placeholder text and English
annotations for review only; every screen is marked as awaiting runtime integration.
"""
import os
import sys

from PIL import Image, ImageDraw

import core
import sheets
import uikit as K
from core import B04, save, dump, text
from uikit import (new, plate, inset, raised, dither, txt, icon, PAPER, PAPER_HI, PAPER_SH, INK, BLUE, GREEN, GOLD, RED,
                   DARK, DARK2, CLEAR, HEAD, OKG)

UI = {r["asset_key"]: r for r in core.read_csv("art/UI_ASSETS_04.csv")}
OUT = os.path.join(B04, "ui")
DIS = (150, 150, 138, 255)


def item_thumb(key, state=None, size=None):
    d = os.path.join(B04, "items", key, "items")
    if not os.path.isdir(d):
        return None
    names = sorted(os.listdir(d))
    pick = (state + ".png") if state and (state + ".png") in names else None
    if not pick:
        for cand in ("intact.png", "nuo_intact.png", "sealed.png", "closed.png", "front_valid.png", "idle.png"):
            if cand in names:
                pick = cand
                break
    im = Image.open(os.path.join(d, pick or names[0])).convert("RGBA")
    if size:
        r = min(size / im.width, size / im.height)
        if r < 1:
            im = im.resize((max(1, int(im.width * r)), max(1, int(im.height * r))), Image.NEAREST)
    return im


# ------------------------------------------------------------------ components
def c_button(w, h, fill, st):
    im = new(w, h)
    d = ImageDraw.Draw(im)
    f = {"default": fill, "hover": tuple(min(255, c + 18) for c in fill[:3]) + (255,), "pressed": tuple(max(0, c - 22) for c in fill[:3]) + (255,), "disabled": DIS}[st]
    raised(d, (0, 0, w, h), f, press=(st == "pressed"))
    if st == "hover":
        d.rectangle([2, 2, w - 3, h - 3], outline=PAPER_HI)
    if st == "disabled":
        dither(im, (2, 2, w - 2, h - 2), (132, 132, 122, 255))
    return im


def c_tab(st):
    im = new(96, 24)
    d = ImageDraw.Draw(im)
    f = {"default": PAPER_SH, "selected": PAPER_HI, "disabled": DIS}[st]
    d.rectangle([0, 2 if st != "selected" else 0, 95, 23], fill=INK)
    d.rectangle([1, 3 if st != "selected" else 1, 94, 23], fill=f)
    if st == "selected":
        d.rectangle([1, 1, 94, 3], fill=BLUE)
    else:
        d.line([0, 23, 95, 23], fill=INK)
    if st == "disabled":
        dither(im, (2, 4, 94, 23), (132, 132, 122, 255))
    for x in (0, 95):
        im.putpixel((x, 2 if st != "selected" else 0), CLEAR)
    return im


def row_plate(w, h, st, wells, tab=None):
    im = plate(w, h, PAPER_HI if st in ("selected", "active", "running", "ready") else PAPER, worn=False)
    d = ImageDraw.Draw(im)
    for box in wells:
        inset(d, box, PAPER_SH)
    if tab:
        d.rectangle([1, 1, 5, h - 2], fill=tab)
    return im, d


def c_catalog_row(st):
    im, d = row_plate(520, 42, st, [(8, 3, 44, 39)], GOLD if st == "selected" else None)
    d.line([360, 6, 360, 35], fill=PAPER_SH)
    d.line([440, 6, 440, 35], fill=PAPER_SH)
    if st == "disabled":
        dither(im, (1, 1, 519, 41), DIS)
    return im


def c_event_card(st):
    col = {"forecast": BLUE, "active": GOLD, "expired": DIS, "resolved": OKG}[st]
    im, d = row_plate(520, 72, st, [(8, 4, 72, 68), (300, 8, 400, 30), (300, 38, 400, 60), (410, 8, 510, 30), (410, 38, 510, 60)], col)
    if st == "forecast":                                   # dashed frame = not yet in effect
        for x in range(8, 512, 8):
            d.line([x, 1, x + 3, 1], fill=BLUE)
            d.line([x, 70, x + 3, 70], fill=BLUE)
    if st == "expired":
        dither(im, (6, 1, 519, 71), DIS)
    if st == "resolved":
        d.line([492, 46, 497, 52, 506, 40], fill=OKG, width=2)
    if st == "active":
        d.rectangle([76, 4, 78, 67], fill=GOLD)
    return im


def c_job_row(st):
    col = {"queued": BLUE, "running": GOLD, "paused": RED, "ready": OKG, "claimed": DIS}[st]
    im, d = row_plate(520, 48, st, [(8, 4, 48, 44), (56, 4, 96, 44), (250, 18, 494, 32)], col)
    d.polygon([(102, 20), (112, 24), (102, 28)], fill=INK)   # input -> output arrow
    if st == "paused":
        d.rectangle([500, 16, 503, 32], fill=RED)
        d.rectangle([507, 16, 510, 32], fill=RED)
    if st == "ready":
        d.rectangle([498, 16, 512, 32], outline=OKG)
        d.rectangle([501, 19, 509, 29], fill=OKG)
    if st == "claimed":
        dither(im, (6, 1, 519, 47), DIS)
    return im


def c_tooltip(st):
    im = plate(240, 80, DARK2, (86, 82, 96, 255), DARK, worn=False, edge=INK)
    d = ImageDraw.Draw(im)
    d.rectangle([2, 2, 237, 77], outline={"default": PAPER_SH, "warning": GOLD, "error": RED}[st])
    if st != "default":
        d.polygon([(4, 4), (20, 4), (4, 20)], fill=GOLD if st == "warning" else RED)
    if st == "error":
        d.line([6, 6, 10, 10], fill=PAPER_HI)
        d.line([10, 6, 6, 10], fill=PAPER_HI)
    return im


def c_dialog(st):
    im = plate(360, 200, PAPER)
    d = ImageDraw.Draw(im)
    d.rectangle([1, 1, 358, 26], fill={"confirm": BLUE, "blocked": GREEN, "error": RED}[st])
    inset(d, (12, 38, 348, 150), PAPER_HI)
    raised(d, (40, 162, 160, 190), PAPER_SH)
    raised(d, (200, 162, 320, 190), GOLD if st == "confirm" else DIS)
    if st == "blocked":
        lk = icon(lambda c: K.lock(c), 24)
        im.alpha_composite(lk, (168, 2))
    if st == "error":
        d.line([172, 8, 184, 20], fill=PAPER_HI, width=2)
        d.line([184, 8, 172, 20], fill=PAPER_HI, width=2)
    return im


def c_progress(kind, st):
    im = new(240, 12)
    d = ImageDraw.Draw(im)
    if kind == "track":
        inset(d, (0, 0, 240, 12), DARK2 if st != "complete" else (60, 74, 66, 255))
        for x in range(24, 240, 24):
            d.line([x, 9, x, 10], fill=PAPER_SH)
    else:
        col = {"default": GOLD, "paused": DIS, "complete": OKG}[st]
        d.rectangle([2, 2, 237, 9], fill=col)
        d.line([2, 2, 237, 2], fill=tuple(min(255, c + 40) for c in col[:3]) + (255,))
        if st == "paused":
            for x in range(6, 236, 8):
                d.line([x, 8, x + 4, 3], fill=INK)
    return im


def c_marker(st):
    im = new(32, 32)
    d = ImageDraw.Draw(im)
    if st == "discovered":
        for a in range(0, 360, 45):
            d.arc([3, 3, 28, 28], a, a + 28, fill=BLUE, width=2)
    elif st == "selected":
        d.ellipse([3, 3, 28, 28], outline=GOLD, width=2)
        d.ellipse([2, 2, 29, 29], outline=INK)
        for x, y, dx, dy in ((0, 0, 1, 1), (31, 0, -1, 1), (0, 31, 1, -1), (31, 31, -1, -1)):
            d.line([x, y, x + 5 * dx, y], fill=INK)
            d.line([x, y, x, y + 5 * dy], fill=INK)
    else:
        d.ellipse([3, 3, 28, 28], outline=OKG, width=2)
        d.line([20, 24, 24, 29, 31, 19], fill=OKG, width=2)
    return im


def c_frame(st):
    im = new(298, 199)                                     # centre stays transparent and clickable
    d = ImageDraw.Draw(im)
    col = BLUE if st == "default" else RED
    for x, y, dx, dy in ((0, 0, 1, 1), (297, 0, -1, 1), (0, 198, 1, -1), (297, 198, -1, -1)):
        d.line([x, y, x + 14 * dx, y], fill=INK)
        d.line([x, y, x, y + 14 * dy], fill=INK)
        d.line([x + dx, y + dy, x + 13 * dx, y + dy], fill=col)
        d.line([x + dx, y + dy, x + dx, y + 13 * dy], fill=col)
    if st == "locked":
        for x in range(20, 280, 6):
            d.point((x, 0), fill=RED)
            d.point((x, 198), fill=RED)
        im.alpha_composite(icon(lambda c: K.lock(c), 24), (270, 4))
    return im


def c_cell(st):
    im = new(64, 64)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 63, 63], fill=(30, 26, 40, 255))
    d.rectangle([1, 1, 62, 62], fill={"empty": (44, 40, 56, 255), "hover": (62, 58, 78, 255), "selected": (62, 58, 78, 255), "blocked": (58, 38, 44, 255)}[st])
    d.line([1, 1, 62, 1], fill=(24, 20, 32, 255))
    if st == "selected":
        d.rectangle([1, 1, 62, 62], outline=GOLD)
    if st == "hover":
        d.rectangle([1, 1, 62, 62], outline=PAPER_SH)
    if st == "blocked":
        d.line([8, 55, 55, 8], fill=RED, width=2)
    return im


CONTAINERS = {
    "native_bookbox": (4, 2, ["manual_metals", "manual_jade", "manual_antiques", "manual_art", "manual_electronics", "manual_media", "manual_books", "manual_index"], 1),
    "native_bookbox_large": (4, 3, ["manual_metals", "manual_jade", "manual_antiques", "manual_art", "manual_electronics", "manual_media", "manual_books", "manual_recovery", "manual_jewelry", "manual_index"], 1),
    "native_cardholder": (4, 1, ["savings_card", "credit_card"], 2),
    "native_materials_bin": (4, 4, ["sorted_gold_scrap", "sorted_silver_scrap", "sorted_copper_scrap", "recovered_gold_lot", "recovered_silver_lot", "recovered_copper_lot", "assay_sample", "sorted_gold_scrap"], 1),
    "native_gem_case": (4, 4, ["gem_diamond_round", "gem_ruby_oval", "gem_sapphire_pear", "gem_emerald_step", "gem_pearl_round", "gem_diamond_princess", "gem_ruby_cushion", "gem_sapphire_oval", "gem_emerald_oval", "gem_pearl_baroque"], 1),
    "native_presentation_case": (4, 2, ["jewelry_ring_solitaire", "jewelry_pendant_bezel", "jewelry_earrings_stud", "setting_ring"], 1),
    "native_jewelry_display": (4, 4, ["jewelry_ring_solitaire", "gem_ruby_oval", "jewelry_pendant_bezel", "gem_sapphire_pear", "jewelry_earrings_stud", "gem_emerald_step", "gold_ring_orbit", "silver_ring_starburst"], 1),
}


def c_container(key, st):
    cols, rows, items, span = CONTAINERS[key]
    w, h = 16 + cols * 64 + 16, 32 + rows * 64 + 16
    im = plate(w, h, DARK2, (86, 82, 96, 255), DARK, worn=False)
    d = ImageDraw.Draw(im)
    d.rectangle([1, 1, w - 2, 24], fill=(70, 52, 70, 255))
    d.line([1, 25, w - 2, 25], fill=INK)
    d.rectangle([w - 22, 6, w - 9, 19], outline=PAPER_SH)
    cell = c_cell("empty")
    for r in range(rows):
        for q in range(cols):
            im.alpha_composite(cell, (16 + q * 64, 32 + r * 64))
    slots = cols * rows // span
    n = {"empty": 0, "partly_filled": max(1, min(len(items), slots) // 2), "full": min(len(items), slots), "reserved": min(len(items), slots) // 2}[st]
    for i in range(n):
        t = item_thumb(items[i])
        if t is None:
            continue
        q, r = (i * span) % cols, (i * span) // cols
        im.alpha_composite(t, (16 + q * 64, 32 + r * 64))
        if st == "reserved" and i % 2 == 0:               # custody stays visible: lock tab on the cell
            im.alpha_composite(icon(K.STATUS["reserved"], 24), (16 + q * 64 + 38, 32 + r * 64 + 38))
    return im


def c_socket(st):
    im = new(48, 48)
    d = ImageDraw.Draw(im)
    if st == "empty":
        for a in range(0, 360, 30):
            d.arc([5, 5, 42, 42], a, a + 18, fill=PAPER_SH, width=2)
    elif st == "compatible":
        d.ellipse([5, 5, 42, 42], outline=OKG, width=3)
        d.line([15, 24, 22, 31, 34, 16], fill=OKG, width=3)
    elif st == "incompatible":
        d.ellipse([5, 5, 42, 42], outline=RED, width=3)
        d.line([12, 36, 36, 12], fill=RED, width=3)
    else:
        d.ellipse([5, 5, 42, 42], outline=GOLD, width=3)
        im.alpha_composite(icon(lambda c: K.lock(c), 24), (12, 12))
    return im


def component(key, st):
    r = UI[key]
    w, h = int(r["width"]), int(r["height"])
    if key == "button_primary":
        return c_button(w, h, GOLD, st)
    if key == "button_secondary":
        return c_button(w, h, PAPER_HI, st)
    if key == "button_icon":
        return c_button(w, h, PAPER_SH, st)
    if key == "category_tab":
        return c_tab(st)
    if key == "catalog_row":
        return c_catalog_row(st)
    if key == "event_card":
        return c_event_card(st)
    if key == "job_row":
        return c_job_row(st)
    if key == "tooltip":
        return c_tooltip(st)
    if key == "dialog":
        return c_dialog(st)
    if key == "progress_track":
        return c_progress("track", st)
    if key == "progress_fill":
        return c_progress("fill", st)
    if key == "evidence_marker":
        return c_marker(st)
    if key == "inspection_frame":
        return c_frame(st)
    if key == "handbook_page":
        return sheets.handbook(st)
    if key == "certificate_page":
        return sheets.certificate(st)
    if key == "order_receipt":
        return sheets.receipt(st)
    if key == "inventory_cell":
        return c_cell(st)
    if key in CONTAINERS:
        return c_container(key, st)
    if key == "grade_badge":
        return icon(K.GRADE[st], 24, margin=2)
    if key == "status_badge":
        return icon(K.STATUS[st], 24, margin=2)
    if key == "stone_socket":
        return c_socket(st)
    if key == "resource_icon":
        return icon(K.RES[st], 32, margin=2)
    if key == "feedback_symbol":
        return icon(K.FEEDBACK[st], 64)
    if key.startswith("event_"):
        return icon(K.EVENTS[key[6:]], 64)
    raise KeyError(key)


# ------------------------------------------------------------------ screens
def F(x, y, w, rows):
    return [("F", x, y + i * 20, w, a, b) for i, (a, b) in enumerate(rows)]


BACK = ("B", 40, 366, 88, "返回", 0)
SCREENS = {
    "screen_hub": ("经营中心", "Business Hub", [("P", 40, 76, 250, 100)] + F(48, 84, 234, [("鉴定等级", "学徒 3 级"), ("经验", "128 / 300"), ("今日金价 卖/回购", "61.20 / 58.10"), ("今日银价 卖/回购", "0.82 / 0.74")]) +
                   [("P", 300, 76, 260, 100), ("K", 308, 84, "event_gold_shipment"), ("X", 380, 88, "金锭货运到港 [进行中]", 12, INK), ("X", 380, 108, "供应 +12% · 至第 9 天", 11, BLUE)] +
                   [("B", 40 + (i % 4) * 132, 190 + (i // 4) * 36, 120, s, 0) for i, s in enumerate(["交易目录", "库存", "鉴定台", "银行", "证券", "典当", "回收精炼", "珠宝工坊", "事件公告", "财务总览", "补给商店", "手册收纳"])] +
                   [("A", 40, 76, 250, 100, "Appraisal level, XP and today's shared metal quotes (dynamic text)"), ("A", 300, 76, 260, 100, "Active event card: cause icon + exact modifier and end day as text"), ("A", 40, 190, 520, 104, "Entry points; recovery, jewelry and events are planned 0.4 screens")]),
    "screen_catalog": ("来客目录", "Visitor Catalog", [("T", ["全部", "贵金属", "翡翠", "珠宝", "藏品/物料"]), ("R", "catalog", [("gold_ring_orbit", "环轨足金戒指", "客户持有 · 1x1", "待鉴定"), ("jade_rough_water", "水石翡翠原石", "客户持有 · 2x2", "未开窗"), ("jewelry_pendant_bezel", "包镶吊坠（红宝石）", "寄售 · 1x1", "已出报告"), ("collectible_handheld", "掌上游戏机", "客户持有 · 2x1", "待鉴定"), ("scrap_board", "废电路板", "供应商 · 2x1", "限量 3")]),
                        ("X", 250, 322, "第 1 / 4 页", 11, BLUE), ("B", 440, 366, 120, "开始鉴定", 1), BACK,
                        ("A", 40, 76, 520, 24, "Five category tabs incl. jewelry"), ("A", 40, 104, 520, 210, "Five rows per page; thumbnail uses the exact manifest footprint, ownership and state as text")]),
    "screen_inventory": ("自有库存", "Owned Inventory", [("G", 40, 80, 8, 3, ["gold_bar_orbit", None, "jade_bangle_plain", None, "gem_diamond_round", "tool_loupe", "manual_metals", "savings_card", None, "refined_gold_bar", None, "tool_scale", None, "energy_cell", "process_cartridge", "jewelry_job_ticket"]), ("P", 380, 80, 180, 208)] +
                         F(388, 88, 164, [("名称", "环轨金条"), ("归属", "自有"), ("占格", "2 x 1"), ("状态", "可出售"), ("已知证据", "2 项")]) + [("B", 388, 256, 164, "查看资产记录", 0), ("X", 40, 300, "客户寄存与订单占用的物品不可出售或典当", 11, RED), BACK,
                         ("A", 40, 80, 330, 208, "Grid of 64px cells (shown at half scale here); shapes come from ITEM_LAYOUT_04.csv"), ("A", 380, 80, 180, 208, "Selected item: owned / customer-owned / consigned shown as words")]),
    "screen_inspection": ("鉴定台", "Inspection Desk", [("I", "gold_ring_orbit", "combo__band_fissure__exposed_core"), ("P", 360, 99, 200, 199)] + F(368, 107, 184, [("当前视图", "正面"), ("重量", "7.42 g"), ("已发现证据", "1 / ?"), ("连续失误", "1 / 2")]) +
                          [("X", 368, 192, "议价理由（可多选）", 11, BLUE), ("F", 368, 208, 184, "[x] 戒壁裂纹", "结构"), ("F", 368, 228, 184, "[  ] 露底镀层", "材质"), ("B", 368, 262, 88, "仪器", 0), ("B", 464, 262, 88, "手册", 0), ("X", 49, 306, "客人皱眉：再指错一次，他就把东西收回去了。", 11, RED), ("B", 440, 366, 120, "报价", 1), BACK,
                           ("A", 49, 99, 298, 199, "Existing 298x199 image rectangle is untouched; markers are a separate overlay"), ("A", 360, 99, 200, 199, "Evidence list merges duplicate causes; two consecutive misses end the encounter"), ("A", 49, 306, 500, 14, "Miss feedback is delivered through the customer, not a warning box")]),
    "screen_instruments": ("仪器读数", "Instrument Readings", [("R", "catalog", [("tool_scale", "电子秤", "已记录", "7.42 g"), ("tool_assay", "成色分析仪", "未测量", "—"), ("tool_loupe", "十倍放大镜", "可用", "视图"), ("tool_jade_light", "翡翠透光灯", "不适用", "—"), ("tool_gem_probe", "宝石筛查笔", "未拥有", "—")]), ("X", 40, 322, "筛查笔只能提示“建议送检”，不能证明天然来源。", 11, BLUE), BACK,
                            ("A", 40, 104, 520, 210, "Six instruments; not owned / not applicable / not measured / recorded are distinct words"), ("A", 40, 322, 520, 14, "Probe wording never implies a hidden grade or origin")]),
    "screen_quote": ("报价确认", "Quote Confirmation", [("P", 40, 76, 250, 240)] + F(48, 84, 234, [("方向", "收购"), ("材质底价", "431.20"), ("工艺价", "+ 24.00"), ("证据扣减（去重）", "- 38.00"), ("事件修正", "金价 +3%"), ("行情日期", "第 8 天")]) + [("X", 48, 220, "最终报价", 12, BLUE), ("X", 48, 240, "417.20 Cr", 18, INK)] +
                      [("P", 300, 76, 260, 240), ("X", 308, 84, "已确认原因", 11, BLUE), ("F", 308, 104, 244, "戒壁裂纹", "结构 -26"), ("F", 308, 124, 244, "外壁划痕", "工艺 -12"), ("X", 308, 290, "报价已过期，请重新确认行情。", 11, RED), ("B", 440, 366, 120, "成交", 1), BACK,
                       ("A", 40, 76, 250, 240, "Material floor, workmanship, deduplicated causes, one shared market snapshot"), ("A", 300, 76, 260, 240, "Each cause counted once; expired-quote and insufficient-funds errors are readable")]),
    "screen_asset_record": ("资产记录", "Asset Record", [("I", "jewelry_ring_solitaire", "au18k_ruby_oval_intact"), ("P", 360, 99, 200, 199)] + F(368, 107, 184, [("资产编号", "A-000217"), ("来源", "第 6 天 收购"), ("托管", "自有 · 展示盘"), ("主石", "红宝石 · 已出报告"), ("戒托", "18K 金 · 已称重"), ("处理", "未检测")]) + [("B", 368, 262, 184, "查看鉴定报告", 0), BACK,
                             ("A", 360, 99, 200, 199, "Stone and setting identities stay separate; untested facts read as untested")]),
    "screen_workshop": ("维修与开料", "Repair and Jade Cutting", [("I", "jade_rough_yellow", "window_open_nuo"), ("P", 360, 99, 200, 199), ("X", 368, 107, "可选工序", 11, BLUE)] + F(368, 124, 184, [("开窗", "费用 4 · 1 天"), ("切片", "费用 9 · 2 天"), ("制作手镯", "需整料"), ("抛光修复", "不适用")]) + [("X", 368, 214, "预期产出：按已保存的内部料", 11, INK), ("X", 368, 230, "失败条件：裂隙贯穿时拒绝", 11, RED), ("B", 368, 262, 184, "确认工序", 1), BACK,
                         ("A", 360, 99, 200, 199, "Expected output and refusal conditions are explicit; no reroll, no guaranteed tier")]),
    "screen_bank": ("银行与实体卡", "Bank and Physical Cards", [("G2", 40, 80, "savings_card"), ("G2", 40, 152, "credit_card"), ("P", 180, 76, 380, 150)] + F(188, 84, 364, [("卡序列号", "SP-0042-17（有效）"), ("账户余额", "12,480.00 Cr"), ("今日已存 / 限额", "2,000 / 5,000"), ("今日已取 / 限额", "500 / 3,000"), ("日利率", "0.03%"), ("新存款起息", "次日")]) +
                     [("X", 40, 240, "补办新卡后，旧卡立即作废。", 11, RED), ("B", 180, 262, 120, "存入", 1), ("B", 310, 262, 120, "取出", 0), ("B", 440, 262, 120, "补办卡片", 0), BACK,
                      ("A", 40, 80, 130, 136, "Physical 2x1 card thumbnails; serial and balances are UI text"), ("A", 180, 76, 380, 150, "Usage and remaining quota keep fixed positions")]),
    "screen_credit": ("信贷与借款", "Credit and Loans", [("P", 40, 76, 250, 150)] + F(48, 84, 234, [("本金", "3,000.00"), ("应计利息", "42.50"), ("合同利率", "0.12% / 日"), ("到期日", "第 14 天"), ("今日可借额度", "1,500"), ("总授信", "8,000")]) + [("P", 300, 76, 260, 150), ("X", 308, 84, "新合同利率（与现有合同分开）", 11, BLUE), ("F", 308, 104, 244, "新借款日利率", "0.15%"), ("F", 308, 124, 244, "事件修正", "信贷收紧 +0.03%"), ("B", 300, 240, 120, "借款", 0), ("B", 440, 240, 120, "还款", 1), BACK,
                       ("A", 40, 76, 250, 150, "Existing contract terms"), ("A", 300, 76, 260, 150, "New-contract offer shown separately; repayment always reachable")]),
    "screen_securities": ("证券与持仓", "Securities and Holdings", [("R", "catalog", [(None, "空间站矿业", "持有 40 · 买 12.40 / 卖 12.10", "+2.1%"), (None, "轨道货运", "持有 0 · 买 8.05 / 卖 7.90", "-0.8%"), (None, "珠宝联合", "持有 15 · 买 21.30 / 卖 20.80", "+0.4%")]), ("P", 40, 240, 520, 74), ("X", 48, 248, "相关新闻", 11, BLUE), ("X", 48, 266, "货运延误推高物料价格；影响持续至第 9 天。", 11, INK), ("B", 310, 366, 120, "买入", 0), ("B", 440, 366, 120, "卖出", 1), BACK,
                           ("A", 40, 104, 520, 126, "Daily buy/sell and holdings only; no limit orders or derivatives"), ("A", 40, 240, 520, 74, "News ties price moves to event causes")]),
    "screen_financial_overview": ("财务总览", "Financial Overview", [("P", 40, 76, 250, 200)] + F(48, 84, 234, [("现金", "4,210.00"), ("存款", "12,480.00"), ("证券变现值", "803.50"), ("负债", "- 3,042.50"), ("净资金", "14,451.00"), ("典当应收（另列）", "1,200.00"), ("客户订金（代持）", "360.00")]) + [("P", 300, 76, 260, 200), ("X", 308, 84, "说明", 11, BLUE), ("X", 308, 104, "库存不计入净资金。", 11, INK), ("X", 308, 122, "客户订金是负债，不是利润。", 11, INK), BACK,
                                   ("A", 40, 76, 250, 200, "Held customer deposits listed apart from available cash"), ("A", 300, 76, 260, 200, "Inventory excluded from net funds")]),
    "screen_events": ("事件公告与预报", "Event Board and Forecasts", [("R", "event", [("event_gold_shipment", "金锭货运到港", "供应渠道 · 金价", "+12% 供应", "第 7–9 天", "进行中"), ("event_port_delay", "货运航线延误", "物料价 · 加工费", "+8% 物料", "第 10–12 天", "预报"), ("event_jade_festival", "翡翠藏家集市", "翡翠档位价", "冰 +15%", "第 3–5 天", "已结束")]), BACK,
                       ("A", 40, 104, 520, 72, "Icon shows the cause; channel, exact modifier, dates and status are stable text fields"), ("A", 40, 180, 520, 72, "Forecast uses a dashed frame, not colour alone")]),
    "screen_settlement": ("夜间结算", "Nightly Settlement", [("P", 40, 76, 520, 240)] + F(48, 84, 504, [("今日收购支出", "- 1,240.00"), ("今日售出收入", "+ 2,015.00"), ("加工与能源", "- 36.00"), ("利息收支", "+ 3.74 / - 3.60"), ("订单订金（代持变动）", "+ 108.00"), ("到期合同", "1 项待处理"), ("事件明日变化", "货运延误开始")]) + [("B", 440, 366, 120, "进入次日", 1), BACK,
                           ("A", 40, 76, 520, 240, "Summary rows with fixed number column; events listed as tomorrow's changes")]),
    "screen_pawn": ("典当合同", "Pawn Contracts", [("T", ["我放款", "我借款", "已结清"]), ("R", "catalog", [("silver_bracelet_orbit", "环轨银手镯", "放款 300 · 日息 0.4%", "第 12 天到期"), ("jade_pendant_plain", "平安牌挂件", "放款 900 · 日息 0.3%", "可赎回"), ("collectible_vase", "青花瓶", "放款 1,500 · 逾期", "可处置")]), ("B", 310, 366, 120, "赎回", 0), ("B", 440, 366, 120, "处置", 1), BACK,
                     ("A", 40, 104, 520, 126, "Direction, collateral, status, rate and maturity; settled and empty states exist as skins")]),
    "screen_organizer": ("手册收纳盒", "Handbook Organizer", [("C", 40, 80, "native_bookbox_large", "partly_filled"), ("P", 340, 80, 220, 208)] + F(348, 88, 204, [("容量", "12 格（4 x 3）"), ("已收纳", "5 本"), ("选中", "贵金属鉴定手册")]) + [("B", 348, 190, 100, "阅读", 1), ("B", 452, 190, 100, "取出", 0), ("X", 348, 230, "移动或出售前需先清空。", 11, RED), BACK,
                          ("A", 40, 80, 288, 240, "Native-style container mockup: 4x3 cells of 64px for the new twelve-slot organizer only"), ("A", 340, 80, 220, 208, "Insert / remove / read; the old eight-slot box is unchanged")]),
    "screen_supply_shop": ("补给商店", "Supply Shop", [("R", "catalog", [("tool_gem_probe", "宝石筛查笔", "仪器 · 2x1", "180 Cr"), ("process_cartridge", "工艺耗材筒", "耗材 · 已有 2", "12 Cr"), ("energy_cell", "能源电池", "耗材 · 已有 1", "20 Cr"), ("manual_jewelry", "珠宝宝石手册", "手册 · 未拥有", "60 Cr"), ("gem_case", "宝石收纳盒", "容器 · 2x2", "90 Cr")]), ("X", 40, 322, "资金不足或背包已满时不会扣除任何物品。", 11, RED), ("B", 440, 366, 120, "购买", 1), BACK,
                            ("A", 40, 104, 520, 210, "Finite stock, price, function and owned count; failures keep existing items")]),
    "screen_recovery": ("拆解与回收", "Disassembly and Recovery", [("P", 40, 76, 170, 140), ("X", 48, 84, "投入（已预留）", 11, BLUE), ("G", 48, 102, 4, 1, ["collectible_handheld", None]), ("P", 220, 76, 170, 140), ("X", 228, 84, "产出预览（守恒）", 11, BLUE), ("G", 228, 102, 4, 2, ["scrap_board", None, "scrap_processor", "scrap_connectors", "inert_residue", None]), ("P", 400, 76, 160, 140)] + F(408, 84, 144, [("路线", "拆解"), ("费用", "1 Cr"), ("能源", "2 单位"), ("工期", "1 天"), ("背包余格", "6")]) +
                         [("X", 40, 226, "原样出售 / 维修 / 拆解 / 回收  对比", 11, BLUE)] + F(40, 244, 520, [("原样出售（已知价值）", "64.00"), ("拆解后回收（估计，含不确定）", "71 – 96"), ("事件修正", "回收补贴 -10% 费用")]) + [("B", 440, 366, 120, "开始拆解", 1), BACK,
                          ("A", 40, 76, 170, 140, "Reserved input"), ("A", 220, 76, 170, 140, "Outputs only partition what the device already contains"), ("A", 40, 226, 520, 80, "Sell as-is / repair / dismantle / recover comparison with uncertainty")]),
    "screen_refining": ("精炼与合金", "Refining and Alloys", [("P", 40, 76, 250, 150), ("X", 48, 84, "物料预算（已测 / 估计）", 11, BLUE)] + F(48, 102, 234, [("金 Au", "3.20 g 已测"), ("银 Ag", "≈ 5.1 g 估计"), ("铜 Cu", "41.0 g 已测"), ("配方", "18K 金合金")]) + [("G", 48, 186, 6, 1, ["recovered_gold_lot", "recovered_copper_lot", "crucible", None, "ingot_mold", None]), ("P", 300, 76, 260, 150)] + F(308, 84, 244, [("设备", "精炼炉 · 空闲"), ("费用", "3 Cr"), ("能源", "6 / 10 单位"), ("工期", "2 天"), ("产出容量", "2 格可用")]) +
                         [("K", 308, 190, "energy"), ("Q", 344, 200, 200, 0.6), ("X", 40, 240, "产出预览：18K 合金条 1 件 + 余料退回；来源记录保留。", 11, INK), ("B", 440, 366, 120, "开始精炼", 1), BACK,
                          ("A", 40, 76, 250, 150, "Measured vs estimated composition; inputs reserved"), ("A", 300, 76, 260, 150, "Cost, finite energy, duration and output capacity"), ("A", 40, 240, 520, 14, "Conserved output preview with provenance; nothing is created")]),
    "screen_jobs": ("生产任务与能源", "Production Jobs and Energy", [("R", "job", [("scrap_board", "recovered_gold_lot", "回收电路板 ×3", "运行中 · 第 9 天完成", 0.55), ("ore_gold", "refined_gold_bar", "金矿精炼", "排队中", 0.0), ("setting_ring", "jewelry_ring_solitaire", "镶嵌：单石戒指", "已完成 · 待领取", 1.0), ("collectible_handheld", "scrap_processor", "拆解掌机", "已暂停 · 能源不足", 0.3)]), ("K", 40, 304, "energy"), ("Q", 76, 314, 240, 0.35), ("X", 330, 310, "能源 7 / 20 · 今日配给", 11, BLUE), ("B", 440, 366, 120, "领取产出", 1), BACK,
                     ("A", 40, 104, 520, 196, "Job rows: input -> output thumbnails, progress, status with shape cues"), ("A", 40, 304, 520, 22, "Shared, finite energy; full inventory keeps results claimable")]),
    "screen_provenance": ("物料来源记录", "Material Provenance", [("P", 40, 76, 520, 240), ("G", 48, 86, 1, 1, ["refined_gold_bar"]), ("X", 90, 90, "精炼金条 · 批次 R-0031", 12, INK)] + F(48, 130, 504, [("第 6 天 收购", "掌上游戏机 ×1（客户：王）"), ("第 7 天 拆解", "电路板 / 处理器 / 接插件 / 残渣"), ("第 8 天 回收", "金 0.41 g · 银 0.9 g · 铜 12 g"), ("第 9 天 精炼", "并入批次 R-0031"), ("守恒校验", "输入 = 产出 + 记录损耗")]) + [BACK,
                           ("A", 40, 76, 520, 240, "Chain of custody from source item to output; element totals are conserved")]),
    "screen_procurement": ("事件采购订单", "Event Procurement Orders", [("R", "catalog", [("refined_gold_bar", "金锭供应商 · 限量 2", "事件：货运到港 · 至第 9 天", "58.10 / g"), ("ore_copper", "铜矿石批次 · 限量 4", "事件：货运延误 +8%", "0.9 / g"), ("gem_sapphire_oval", "蓝宝石散石 · 限量 1", "事件：宝石货运", "70 / ct")]), ("X", 40, 240, "订单使用当日行情快照；不会自动签约，也不会补货。", 11, BLUE), ("B", 440, 366, 120, "下单", 1), BACK,
                            ("A", 40, 104, 520, 126, "Finite supplier stock priced from the same daily snapshot; optional, never automatic")]),
    "screen_jewelry": ("珠宝收购与展示", "Jewelry Buying and Showroom", [("T", ["收购", "展示盘", "散石", "空托"]), ("C", 40, 104, "native_jewelry_display", "reserved", 0.66), ("P", 250, 104, 310, 208)] + F(258, 112, 294, [("选中", "单石戒指（红宝石 / 18K）"), ("主石", "红宝石 0.82 ct · 已出报告"), ("戒托", "18K 金 4.0 g"), ("托管", "自有 · 可售"), ("买家库存 / 预算", "有限 · 余 2 件")]) + [("X", 258, 220, "带锁标记：客户财产或订单占用，不可出售。", 11, RED), ("B", 258, 280, 140, "报价出售", 1), ("B", 412, 280, 140, "送检", 0), BACK,
                        ("A", 40, 104, 200, 208, "Display tray: saleable stock only; reserved cells carry a lock tab"), ("A", 250, 104, 310, 208, "Stone and setting identities, custody and finite buyer stock")]),
    "screen_setting": ("拆镶与镶嵌", "Demounting and Setting", [("I", "setting_ring", "au18k_empty"), ("P", 360, 99, 200, 199), ("X", 368, 107, "镶口（按 JEWELRY_SETTINGS）", 11, BLUE), ("S", 368, 124, "compatible"), ("G", 424, 124, 2, 1, ["gem_ruby_oval", None]), ("S", 368, 180, "incompatible"), ("G", 424, 180, 2, 1, ["gem_pearl_baroque", None])] + [("X", 368, 236, "珍珠不适配此戒托。", 11, RED), ("F", 368, 252, 184, "工费 / 能源 / 工期", "5 · 2 · 1 天"), ("B", 440, 366, 120, "确认镶嵌", 1), BACK,
                        ("A", 49, 99, 298, 199, "Empty setting keeps the assembled footprint"), ("A", 360, 99, 200, 199, "One real stone per socket; compatibility shown by shape and words"), ("A", 368, 252, 184, 16, "Same stone and setting IDs are retained")]),
    "screen_jewelry_orders": ("珠宝定制订单", "Jewelry Custom Orders", [("R", "catalog", [("jewelry_job_ticket", "王女士 · 单石戒指（自带红宝石）", "客户财产托管中 · 订金 36 已代持", "第 11 天交付"), ("jewelry_job_ticket", "李先生 · 耳钉一对（店内蓝宝石）", "店料已预留 · 订金 108 已代持", "制作中"), ("jewelry_job_ticket", "赵小姐 · 吊坠改镶", "已完成 · 待确认交付", "尾款 84")]), ("P", 40, 240, 520, 74)] + F(48, 248, 504, [("签约价 / 订金 / 尾款", "120 / 36 / 84"), ("截止", "第 11 天 · 逾期按约退款"), ("可用现金（不含代持订金）", "4,210.00")]) + [("B", 310, 366, 120, "取消订单", 0), ("B", 440, 366, 120, "确认交付", 1), BACK,
                               ("A", 40, 104, 520, 126, "Customer property, held deposits and agreed terms are explicit per order"), ("A", 40, 240, 520, 74, "Deposit is a held liability; balance collected once at delivery")]),
    "screen_gem_lab": ("宝石筛查与送检", "Gem Screening and Lab Reports", [("I", "gem_ruby_oval", "intact"), ("P", 360, 99, 200, 199)] + F(368, 107, 184, [("筛查笔结果", "建议送检"), ("声明", "天然红宝石"), ("来源", "未知"), ("处理", "未检测"), ("送检费用 / 工期", "6 Cr · 1 天"), ("托管", "送检期间锁定")]) + [("X", 368, 232, "报告只写已检测的事实。", 11, BLUE), ("B", 368, 262, 184, "委托实验室", 1), BACK,
                         ("A", 360, 99, 200, 199, "Claim, screening result and unknown facts kept apart; lab is a paid external service")]),
}


def screen_skin(key, st):
    if key == "screen_handbook":
        im = sheets.handbook("defect_example" if st == "default" else "introduction")
        if st == "blocked":
            dither(im, (20, 136, 420, 572), K.SHEET_SH)
            im.alpha_composite(icon(lambda c: K.lock(c), 64), (188, 300))
        return im
    title_zh, title_en, els = SCREENS[key]
    im = plate(600, 400)
    d = ImageDraw.Draw(im)
    d.line([40, 42, 560, 42], fill=BLUE)
    d.line([40, 66, 560, 66], fill=PAPER_SH)
    d.line([40, 332, 560, 332], fill=PAPER_SH)
    d.line([40, 360, 560, 360], fill=PAPER_SH)
    inset(d, (500, 14, 560, 36), PAPER_SH)                  # day / status well
    for e in els:
        k = e[0]
        if k == "P":
            inset(d, (e[1], e[2], e[1] + e[3], e[2] + e[4]), PAPER_HI)
        elif k == "F":
            d.line([e[1], e[2] + 17, e[1] + e[3], e[2] + 17], fill=PAPER_SH)
        elif k == "B":
            b = component("button_primary" if e[5] else "button_secondary", "disabled" if st == "blocked" else "default")
            b = b.resize((e[3], 28 if e[5] else 24), Image.NEAREST) if b.width != e[3] else b
            im.alpha_composite(b, (e[1], e[2]))
        elif k == "T":
            for i in range(len(e[1])):
                im.alpha_composite(c_tab("selected" if i == 0 else "default"), (40 + i * 98, 76))
            d.line([40, 99, 560, 99], fill=INK)
        elif k == "R":
            kind, rows = e[1], e[2]
            hh = {"catalog": 42, "job": 48, "event": 72}[kind]
            y0 = 104
            if st == "empty":
                inset(d, (40, y0, 560, y0 + hh * 3), PAPER_HI)
                for x in range(60, 540, 10):
                    d.line([x, y0 + hh * 3 // 2, x + 4, y0 + hh * 3 // 2], fill=PAPER_SH)
                d.rectangle([286, y0 + hh * 3 // 2 - 14, 314, y0 + hh * 3 // 2 + 14], outline=PAPER_SH)
            else:
                for i in range(len(rows)):
                    if kind == "catalog":
                        r = c_catalog_row("selected" if i == 0 else "default")
                    elif kind == "job":
                        r = c_job_row(["running", "queued", "ready", "paused"][i % 4])
                    else:
                        r = c_event_card(["active", "forecast", "expired"][i % 3])
                    im.alpha_composite(r, (40, y0 + i * hh))
        elif k == "I":
            inset(d, (47, 97, 349, 300), PAPER_HI)
            im.alpha_composite(c_frame("locked" if st == "blocked" else "default"), (49, 99))
        elif k == "G":
            cols, rows = e[3], e[4]
            cell = c_cell("empty").resize((40, 40), Image.NEAREST)
            for r in range(rows):
                for q in range(cols):
                    im.alpha_composite(cell, (e[1] + q * 40, e[2] + r * 40))
        elif k == "G2":
            inset(d, (e[1], e[2], e[1] + 130, e[2] + 66), PAPER_SH)
        elif k == "C":
            if st != "empty":
                pass
            inset(d, (e[1] - 2, e[2] - 2, e[1] + int(288 * (e[5] if len(e) > 5 else 1)) + 2, e[2] + int((304 if "display" in e[3] or "gem" in e[3] or "bin" in e[3] else 240 if "large" in e[3] else 176) * (e[5] if len(e) > 5 else 1)) + 2), DARK2)
        elif k == "Q":
            im.alpha_composite(c_progress("track", "default").resize((e[3], 12), Image.NEAREST), (e[1], e[2]))
        elif k == "S":
            inset(d, (e[1], e[2], e[1] + 50, e[2] + 50), PAPER_SH)
    if st == "blocked":
        dither(im, (40, 74, 560, 330), PAPER_SH)
        d.rectangle([180, 170, 420, 234], fill=INK)
        d.rectangle([181, 171, 419, 233], fill=PAPER)
        im.alpha_composite(icon(lambda c: K.lock(c), 48), (190, 178))
        d.line([250, 190, 400, 190], fill=PAPER_SH)
        d.line([250, 212, 380, 212], fill=PAPER_SH)
    return im


def screen_mock(key):
    """Logical-size mockup with dynamic content, plus a list of annotations."""
    if key == "screen_handbook":
        p = os.path.join(B04, "items", "manual_metals", "pages", "page_01.png")
        im = Image.open(p).convert("RGBA") if os.path.exists(p) else sheets.handbook("defect_example")
        ann = [((28, 24, 332, 48), "Blue issuer line and centered dark title (runtime text)"), ((20, 140, 400, 204), "Section: blue heading, compact dark explanation, adjacent reference/problem crops from the real item layers"),
               ((40, 324, 360, 20), "Green 'reference' and red 'problem' captions always carry words"), ((28, 580, 384, 20), "Previous / next targets and page number"), ((390, 8, 40, 18), "Duplicate-window and close controls")]
        return im, ann, "手册阅读", "Handbook Reader"
    title_zh, title_en, els = SCREENS[key]
    im = screen_skin(key, "default")
    d = ImageDraw.Draw(im)
    txt(im, (40, 16), title_zh, 18)
    txt(im, (40, 48), "星当经营系统 · 0.4 规划界面（未接入）", 11, BLUE)
    txt(im, (530, 19), "第 8 天", 11, INK, anchor="ma")
    ann = []
    for e in els:
        k = e[0]
        if k == "F":
            txt(im, (e[1] + 2, e[2] + 2), e[4], 12)
            txt(im, (e[1] + e[3] - 2, e[2] + 2), e[5], 12, BLUE, anchor="ra")
        elif k == "B":
            txt(im, (e[1] + e[3] // 2, e[2] + (7 if e[5] else 5)), e[4], 12, INK, anchor="ma")
        elif k == "X":
            txt(im, (e[1], e[2]), e[3], e[4], e[5])
        elif k == "T":
            for i, s in enumerate(e[1]):
                txt(im, (40 + i * 98 + 48, 81), s, 12, INK, anchor="ma")
        elif k == "R":
            kind, rows = e[1], e[2]
            for i, r in enumerate(rows):
                if kind == "catalog":
                    y = 104 + i * 42
                    t = item_thumb(r[0], size=34) if r[0] else None
                    if t:
                        im.alpha_composite(t, (48 + 9 + (34 - t.width) // 2 - 8, y + 4 + (34 - t.height) // 2))
                    txt(im, (92, y + 5), r[1], 12)
                    txt(im, (92, y + 22), r[2], 11, BLUE)
                    txt(im, (552, y + 13), r[3], 12, INK, anchor="ra")
                elif kind == "job":
                    y = 104 + i * 48
                    for j, kk in enumerate(r[:2]):
                        t = item_thumb(kk, size=36)
                        if t:
                            im.alpha_composite(t, (40 + 10 + j * 48 + (36 - t.width) // 2, y + 6 + (36 - t.height) // 2))
                    txt(im, (160, y + 3), r[2], 12)
                    txt(im, (536, y + 3), r[3], 11, BLUE, anchor="ra")
                    fill = c_progress("fill", "complete" if r[4] >= 1 else "paused" if i == 3 else "default")
                    w = int(240 * r[4])
                    if w:
                        im.alpha_composite(fill.crop((0, 0, w, 12)), (292, y + 19))
                else:
                    y = 104 + i * 72
                    im.alpha_composite(component(r[0], "default"), (48, y + 4))
                    txt(im, (128, y + 10), r[1], 13)
                    txt(im, (128, y + 34), r[2], 11, BLUE)
                    txt(im, (390, y + 12), r[3], 11, INK, anchor="ma")
                    txt(im, (390, y + 42), r[4], 11, INK, anchor="ma")
                    txt(im, (500, y + 12), r[5], 11, INK, anchor="ma")
        elif k == "I":
            p = os.path.join(B04, "items", e[1], "paper", e[2] + ".png")
            pic = Image.open(p).convert("RGBA").resize((298, 199), Image.NEAREST)
            im.alpha_composite(pic, (49, 99))
            im.alpha_composite(c_frame("default"), (49, 99))
            if key == "screen_inspection":
                import json
                hs = [h_ for h_ in json.load(open(os.path.join(B04, "items", e[1], "hotspots.json")))["hotspots"]
                      if h_["state"] == e[2] and h_["image"] == "paper"]
                for i_, h_ in enumerate(hs[:2]):
                    im.alpha_composite(c_marker("selected" if i_ == 0 else "discovered"),
                                       (49 + int(h_["U"] * 298) - 16, 99 + int(h_["V"] * 199) - 16))
        elif k == "G":
            for i, kk in enumerate(e[5]):
                if kk:
                    t = item_thumb(kk)
                    t = t.resize((max(1, t.width * 40 // 64), max(1, t.height * 40 // 64)), Image.NEAREST)
                    im.alpha_composite(t, (e[1] + (i % e[3]) * 40, e[2] + (i // e[3]) * 40))
        elif k == "G2":
            t = item_thumb(e[3])
            im.alpha_composite(t, (e[1] + 1, e[2] + 1))
        elif k == "C":
            cimg = c_container(e[3], e[4])
            if len(e) > 5:
                cimg = cimg.resize((int(cimg.width * e[5]), int(cimg.height * e[5])), Image.NEAREST)
            im.alpha_composite(cimg, (e[1], e[2]))
        elif k == "K":
            ic = component(e[3], "default") if e[3].startswith("event_") else icon(K.RES[e[3]], 32, margin=2)
            im.alpha_composite(ic, (e[1], e[2]))
        elif k == "Q":
            w = int(e[3] * e[4])
            im.alpha_composite(c_progress("fill", "default").resize((e[3], 12), Image.NEAREST).crop((0, 0, w, 12)), (e[1], e[2]))
        elif k == "S":
            im.alpha_composite(c_socket(e[3]), (e[1] + 1, e[2] + 1))
        elif k == "A":
            ann.append(((e[1], e[2], e[3], e[4]), e[5]))
    return im, ann, title_zh, title_en


def backdrop(w, h):
    """Dark worn shop behind the UI. Review mockup only, not a game asset."""
    im = Image.new("RGBA", (w, h), (44, 36, 54, 255))
    d = ImageDraw.Draw(im)
    u = h // 135
    for i in range(0, w, 24 * u):
        d.rectangle([i, 0, i + 2 * u, h], fill=(38, 31, 48, 255))
    for j in range(8 * u, 70 * u, 5 * u):
        d.rectangle([w * 0.38, j, w, j + 2 * u], fill=(72, 54, 76, 255))
    d.rectangle([0, 86 * u, w, 100 * u], fill=(112, 120, 108, 255))
    d.rectangle([0, 100 * u, w, 104 * u], fill=(70, 78, 74, 255))
    d.rectangle([0, 104 * u, w, h], fill=(36, 30, 46, 255))
    for i in range(0, w, 8 * u):
        d.line([i, 104 * u, i, h], fill=(48, 40, 60, 255), width=max(1, u // 2))
    for j in range(104 * u, h, 8 * u):
        d.line([0, j, w, j], fill=(48, 40, 60, 255), width=max(1, u // 2))
    return im


def full_mock(key, k):
    """k=1 -> 1920x1080, k=2 -> 3840x2160."""
    W_, H_ = 1920 * k, 1080 * k
    ui, ann, tz, te = screen_mock(key)
    portrait = ui.height > ui.width
    num, den = (3, 2) if portrait else (2, 1)
    sw, sh = ui.width * num * k // den, ui.height * num * k // den
    bg = backdrop(W_, H_)
    ox, oy = 60 * k, (H_ - sh) // 2
    bg.alpha_composite(ui.resize((sw, sh), Image.NEAREST), (ox, oy))
    d = ImageDraw.Draw(bg)
    ax = ox + sw + 70 * k
    f, fb = K.font(15 * k, False), K.font(20 * k, False)
    d.text((ax, 60 * k), "%s  /  %s" % (te, key), font=fb, fill=(231, 221, 197, 255))
    d.text((ax, 92 * k), "PLANNED 0.4 SCREEN - AWAITING RUNTIME INTEGRATION", font=f, fill=(215, 171, 84, 255))
    d.text((ax, 116 * k), "UI at %s integer scale; Chinese text is placeholder, rendered by the game." % ("x%d" % (num * k // den) if (num * k) % den == 0 else "x%.1f (nearest, not integer)" % (num * k / den)),
           font=f, fill=(168, 204, 184, 255))
    y = 170 * k
    for i, (box, s) in enumerate(ann):
        bx, by, bw, bh = [v * num * k // den for v in box]
        d.rectangle([ox + bx - 2 * k, oy + by - 2 * k, ox + bx + bw + 2 * k, oy + by + bh + 2 * k], outline=(215, 171, 84, 255), width=max(1, k))
        cx, cy = ox + bx + bw, oy + by
        d.ellipse([cx - 11 * k, cy - 11 * k, cx + 11 * k, cy + 11 * k], fill=(215, 171, 84, 255), outline=(37, 34, 43, 255))
        d.text((cx, cy), str(i + 1), font=f, fill=(37, 34, 43, 255), anchor="mm")
        d.ellipse([ax, y, ax + 22 * k, y + 22 * k], fill=(215, 171, 84, 255))
        d.text((ax + 11 * k, y + 11 * k), str(i + 1), font=f, fill=(37, 34, 43, 255), anchor="mm")
        words, line, yy = s.split(), "", y
        for wd in words:
            if d.textlength(line + wd + " ", font=f) > W_ - ax - 60 * k:
                d.text((ax + 32 * k, yy), line, font=f, fill=(231, 221, 197, 255))
                yy += 20 * k
                line = ""
            line += wd + " "
        d.text((ax + 32 * k, yy), line, font=f, fill=(231, 221, 197, 255))
        y = yy + 34 * k
    return bg.convert("RGB")


def build(key):
    r = UI[key]
    out = os.path.join(OUT, key)
    w, h = int(r["width"]), int(r["height"])
    states = r["required_states"].split(";")
    ims = {}
    for st in states:
        im = screen_skin(key, st) if r["kind"] == "screen_plate" else component(key, st)
        assert im.size == (w, h), (key, st, im.size)
        ims[st] = im
        save(im, out, ("skin_%s.png" if r["kind"] == "screen_plate" else "%s.png") % st)
    sc = 1 if w >= 288 else 2 if w >= 96 else 4
    prev = Image.new("RGBA", (len(states) * (w * sc + 8) + 8, h * sc + 16), (38, 48, 58, 255))
    for i, st in enumerate(states):
        prev.alpha_composite(ims[st].resize((w * sc, h * sc), Image.NEAREST), (8 + i * (w * sc + 8), 8))
    save(prev.convert("RGB"), out, "preview.png")
    spec = dict(asset_key=key, kind=r["kind"], size=[w, h], states=states, implementation_status=r["implementation_status"],
                text_free=True, notes=r["notes"])
    if "Nine-slice" in r["notes"]:
        m = r["notes"].split("margins")[1].strip(". ").split(",")[:4]
        spec["nine_slice_margins_ltrb"] = [int(x) for x in m]
    if key in CONTAINERS:
        cols, rows, _, _ = CONTAINERS[key]
        spec.update(grid=[cols, rows], cell_px=64, content_origin=[16, 32], margins_right_bottom=[16, 16])
    lines = ["%s (%s, %dx%d)" % (key, r["kind"], w, h), "", "Status: %s - proposal skin, not integrated into the game." % r["implementation_status"],
             "States: " + ", ".join(states), "Text-free: all labels, numbers, dates and identifiers are rendered by the game.", r["notes"]]
    if r["kind"] == "screen_plate":
        if key == "screen_handbook":
            spec["anchors"] = {k_: list(v) for k_, v in sheets.SLOTS.items()}
        else:
            els = SCREENS[key][2]
            spec["anchors"] = dict(title=[40, 12, 440, 28], subtitle=[40, 46, 440, 16], content=[40, 74, 520, 256], feedback=[40, 336, 520, 22], navigation=[40, 364, 520, 28],
                                   inspection_image=[49, 99, 298, 199] if any(e[0] == "I" for e in els) else None)
            spec["elements"] = [dict(kind=e[0], rect=list(e[1:5])) for e in els if e[0] in ("P", "A")]
        for k_, nm in ((1, "mockup_1080p.png"), (2, "mockup_4k.png")):
            save(full_mock(key, k_), out, nm)
        spec["mockups"] = ["mockup_1080p.png (1920x1080)", "mockup_4k.png (3840x2160)"]
        lines += ["Mockups: mockup_1080p.png and mockup_4k.png carry Chinese placeholder text and English annotations for review only.",
                  "AWAITING RUNTIME INTEGRATION: layout anchors are in spec.json; no game screen uses this skin yet."]
    dump(spec, out, "spec.json")
    text("\n".join(lines), out, "README.txt")
    text('#!/usr/bin/env python3\nimport os, subprocess, sys\nhere = os.path.dirname(os.path.abspath(__file__))\n'
         'sys.exit(subprocess.call([sys.executable, os.path.normpath(os.path.join(here, "..", "..", "..", "source", "build_ui.py")), "%s"]))\n' % key, out, "source", "generate.py")


if __name__ == "__main__":
    for key in (sys.argv[1:] or list(UI)):
        build(key)
        print(key, flush=True)
