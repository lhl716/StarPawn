#!/usr/bin/env python3
"""Handbook pages in the native portrait layout: one index page per manual plus the
2/2/1 chapter pages frozen in docs/HANDBOOK_INDEX_04.csv. Every example is a
registered crop of the example item's real layers; nothing is redrawn here.
Chinese text on the mockups is placeholder copy; the game renders the real text."""
import json
import os

from PIL import Image, ImageDraw

import core
import sheets
from core import B04, ROOT, save, dump, text
from uikit import txt, HEAD, INK, RED, OKG, SHEET, BLUE

IDX = core.read_csv("docs/HANDBOOK_INDEX_04.csv")
BOOK_ZH = {"manual_metals": "贵金属卷", "manual_jade": "翡翠卷", "manual_antiques": "古玩卷", "manual_art": "书画卷", "manual_electronics": "电子卷",
           "manual_media": "媒介卷", "manual_books": "书籍卷", "manual_recovery": "回收精炼卷", "manual_jewelry": "珠宝宝石卷", "manual_index": "总索引"}
CAT_ZH = {"gold_ring": "金戒指", "gold_bracelet": "金手镯", "gold_necklace": "金项链", "gold_earrings": "金耳饰", "gold_bar": "金条",
          "silver_ring": "银戒指", "silver_bracelet": "银手镯", "silver_necklace": "银项链", "silver_earrings": "银耳饰", "silver_bar": "银条", "silver_cup": "银杯",
          "jade_rough": "翡翠原石", "jade_pendant": "翡翠挂件", "jade_cabochon": "翡翠蛋面", "jade_bangle": "翡翠手镯", "jade_beads": "翡翠珠串", "jade_carving": "翡翠雕件",
          "antique_vase": "古瓷瓶", "painting": "书画", "console": "游戏主机", "handheld": "掌上游戏机", "cartridge": "游戏卡带", "disc": "光盘", "book": "书籍",
          "scrap_board": "废电路板", "scrap_processor": "废处理器", "scrap_connectors": "废接插件", "refined_gold_bar": "精炼金条", "refined_silver_bar": "精炼银条",
          "refined_copper_ingot": "精炼铜锭", "ore_gold": "金矿石", "ore_silver": "银矿石", "ore_copper": "铜矿石", "alloy_gold_bar": "18K金合金条", "alloy_silver_bar": "925银合金条",
          "gem_diamond": "钻石", "gem_ruby": "红宝石", "gem_sapphire": "蓝宝石", "gem_emerald": "祖母绿", "gem_pearl": "珍珠",
          "jewelry_ring": "镶嵌戒指", "jewelry_pendant": "镶嵌吊坠", "jewelry_earrings": "镶嵌耳钉", "jewelry_bracelet": "镶嵌手链", "jewelry_necklace": "镶嵌项链"}
SUB_ZH = {"yellow": "黄沙皮", "black": "黑乌沙", "water": "水石", "white": "白盐沙", "rust": "铁锈皮", "mountain": "山石", "all_models": ""}
TOOL_ZH = {"loupe": "十倍放大镜", "none": "肉眼", "assay": "成色分析仪", "scale": "电子秤", "jade_light": "翡翠透光灯", "uv": "紫外观察灯", "lab_report": "实验室报告"}
CHAN_ZH = {"craft_damage": "影响工艺价", "structural_damage": "影响结构完整", "material_disclosure": "按实测材质计价", "composition_disclosure": "按实测成分计价",
           "mass_disclosure": "按实测重量计价", "processability_damage": "影响可加工性", "service_history": "记为维修历史", "identity_disclosure": "影响身份声明",
           "edition_disclosure": "影响版本溢价", "authenticity_disclosure": "影响真伪溢价", "jade_quality": "影响翡翠品质评估", "jade_structure": "影响翡翠结构评估",
           "gem_condition": "影响宝石状态", "gem_structure": "影响宝石结构", "gem_identity": "按检测身份计价", "gem_treatment": "记为处理披露", "missing_component": "缺失部件不计价"}
WORDS = [("fissure", "裂隙"), ("crack", "裂纹"), ("split", "开裂"), ("fracture", "断裂"), ("chip", "缺口"), ("spall", "崩口"), ("dent", "凹陷"), ("compress", "压扁"), ("flat", "压平"), ("buckle", "翘曲"),
         ("plating", "镀层露底"), ("core", "露出内芯"), ("breakthrough", "露铜"), ("solder", "焊接痕"), ("join", "接合痕"), ("braze", "钎焊补片"), ("insert", "改圈嵌片"), ("rework", "返修痕"), ("repair", "旧修痕"),
         ("gouge", "刮槽"), ("scrape", "刮痕"), ("score", "划痕"), ("scratch", "划痕"), ("abrasion", "磨损"), ("wear", "磨损"), ("tangle", "缠结"), ("twist", "扭曲"), ("bent", "弯折"), ("open", "开口"), ("gap", "缺口"),
         ("jam", "卡滞"), ("loose", "松动"), ("missing", "缺失"), ("stamp", "印记异常"), ("mark", "标记异常"), ("recut", "重刻"), ("restrik", "重打"), ("weight", "重量不符"), ("mass", "重量不符"), ("measure", "重量不符"), ("pour", "重量不符"), ("fill", "填充痕"),
         ("purity", "成色不足"), ("karat", "成色不足"), ("fineness", "成色不足"), ("grade", "品位不符"), ("fraction", "成分不符"), ("deficit", "成色不足"), ("cloud", "棉絮云雾"), ("cotton", "棉絮"), ("inclusion", "内含物"),
         ("dye", "染色"), ("color", "染色"), ("colour", "染色"), ("paint", "涂色"), ("coat", "涂层"), ("wash", "浮色"), ("gild", "贴金"), ("sheen", "涂亮层"), ("plug", "嵌补"), ("inlay", "嵌补"), ("patch", "补片"), ("cap", "拼合"),
         ("vein", "石纹"), ("band", "杂质带"), ("void", "粗粒孔洞"), ("coarse", "粗粒"), ("opaque", "不透区"), ("pocket", "杂质团"), ("seal", "印章不符"), ("print", "印刷网点"), ("tear", "撕裂"), ("tide", "水渍"), ("warp", "受潮变形"),
         ("overpaint", "补色"), ("restor", "修复边界"), ("craz", "假开片"), ("contact", "触点损坏"), ("port", "接口损坏"), ("socket", "接口移位"), ("heat", "过热痕"), ("scorch", "灼烧痕"), ("burn", "烧蚀"), ("screw", "拆修痕"),
         ("line", "坏线"), ("button", "按键卡陷"), ("label", "标签异常"), ("tab", "卡扣缺失"), ("reseal", "重封痕"), ("delamin", "脱层"), ("matrix", "码圈不符"), ("reprint", "重印标记"), ("signature", "签名复制"), ("leaf", "缺页"),
         ("pin", "引脚缺失"), ("corro", "腐蚀"), ("oxid", "氧化"), ("strip", "镀层剥除"), ("clip", "剪短"), ("contamin", "杂质附着"), ("sort", "分拣不符"), ("pore", "气孔"), ("slag", "夹渣"), ("cavity", "缩孔"), ("lap", "冷隔"),
         ("segregat", "偏析"), ("inhomogen", "铸造不均"), ("trace", "批次不符"), ("batch", "批次不符"), ("tag", "标签不符"), ("origin", "产地不符"), ("source", "来源不符"), ("waste", "废石带"), ("barren", "贫矿基质"), ("gangue", "脉石"),
         ("moisture", "含水增重"), ("aggregate", "掺杂"), ("feather", "羽状纹"), ("zone", "色带"), ("window", "漏光窗"), ("nacre", "珠层剥落"), ("dull", "失光"), ("claim", "声明不符"), ("substitut", "仿品"), ("imitation", "仿品"),
         ("composite", "拼合石"), ("prong", "爪松动"), ("bezel", "包边翘起"), ("bail", "扣环"), ("clasp", "搭扣"), ("post", "耳针"), ("back", "耳堵松"), ("link", "链节"), ("retain", "固定松动"), ("drop", "吊坠"), ("resize", "改圈痕")]


def zh_label(en):
    e = en.lower()
    for k, z in WORDS:
        if k in e:
            return z
    return "外观异常"


def load(key):
    d = os.path.join(B04, "items", key)
    return json.load(open(os.path.join(d, "manifest.json"))), json.load(open(os.path.join(d, "hotspots.json")))["hotspots"]


def example(key, t):
    """-> dict(reference rel path, problem rel path, bounds, view)"""
    man, hot = load(key)
    st = man["states"]
    tid = t["template_id"]
    cands = [n for n, m in st.items() if m["templates"] == [tid] and m["view"] == "ordinary" and not n.count("combo")]
    pref = [n for n in cands if n.startswith(("nuo_", "window_nuo_"))] or cands
    name = pref[0]
    sh = core.short(tid)
    prefix = name[:-len(sh)]
    base = prefix + "intact" if prefix + "intact" in st else ("sealed" if prefix == "" and "sealed" in st else "window_open_nuo")
    view, ref, prob = "front" if t["view_id"] == "front" else "window", base, name
    ob = {"uv": "uv", "jade_light": "light"}.get(t["instrument_kind"])
    if ob and name + "__" + ob in st:
        prob, view = name + "__" + ob, ob
        for c in (prefix + ob + "_observation", ob + "_observation", "light_observation_nuo" if ob == "light" else ""):
            if c in st:
                ref = c
                break
    hs = [h for h in hot if h["state"] == name and h["TemplateId"] == tid and h["image"] == "paper"]
    b = hs[0]["bounds_px"] if hs and hs[0]["bounds_px"] else None
    out = dict(asset=key, state=name, view=view, reference="items/%s/paper/%s.png" % (key, ref), problem="items/%s/paper/%s.png" % (key, prob), bounds=b, dark=bool(ob and view == ob))
    if t["mask_policy"] == "unchanged":
        out.update(problem="items/%s/layers/paper/reading_example_%s.png" % (key, sh), view="instrument_reading")
    return out


def crop_box(b):
    if b is None:
        return None, 1
    bw, bh = b[2] - b[0], b[3] - b[1]
    for s in (4, 2, 1):
        cw, ch = 160 // s, 104 // s
        if bw <= cw * 0.9 and bh <= ch * 0.9:
            break
    else:
        s, cw, ch = 1, 160, 104
    cx, cy = (b[0] + b[2]) // 2, (b[1] + b[3]) // 2
    x0, y0 = min(max(0, cx - cw // 2), 384 - cw), min(max(0, cy - ch // 2), 256 - ch)
    return (x0, y0, x0 + cw, y0 + ch), s


def slot(page, box, path, crop, s, dark=False):
    src = Image.open(os.path.join(B04, path)).convert("RGBA")
    x, y, w, h = box
    if crop:
        im = src.crop(crop).resize(((crop[2] - crop[0]) * s, (crop[3] - crop[1]) * s), Image.NEAREST)
        pos = (x + (w - im.width) // 2, y + (h - im.height) // 2)
    else:
        im, pos = sheets.fit(src, box)
    if dark:
        ImageDraw.Draw(page).rectangle([pos[0], pos[1], pos[0] + im.width - 1, pos[1] + im.height - 1], fill=(30, 26, 44, 255))
    page.alpha_composite(im, pos)


def header(page, manual, title, pno, total):
    txt(page, (220, 22), "星当鉴定委员会 · " + BOOK_ZH[manual], 12, HEAD, anchor="ma")
    txt(page, (220, 46), title, 18, INK, anchor="ma")
    txt(page, (220, 583), "第 %d 页 / 共 %d 页" % (pno, total), 11, INK, anchor="ma")


def build():
    by_manual = {}
    for r in IDX:
        by_manual.setdefault(r["manual_asset_key"], []).append(r)
    allpages = []
    for manual in BOOK_ZH:
        chapters = by_manual.get(manual, [])
        total = 1 + sum(int(c["page_count"]) for c in chapters)
        out = os.path.join(B04, "items", manual)
        pages = []
        # ---- page 0: index
        page = sheets.handbook("index")
        header(page, manual, "鉴定手册 · " + BOOK_ZH[manual], 0, total)
        txt(page, (28, 84), "手册只讲解固定图例，不显示当前物品的真实结论。", 12)
        txt(page, (28, 102), "参照图与问题图取自同一件标准样品。", 12)
        rows = [(CAT_ZH[c["category"]] + (" · " + SUB_ZH[c["subtype"]] if SUB_ZH.get(c["subtype"]) else ""), c["first_page"]) for c in chapters] or \
               [(z, "另册") for k, z in BOOK_ZH.items() if k != "manual_index"]
        if len(rows) > 10:                                # two chapters per table row keeps page 0 a single index
            rows = [(rows[i][0] + "（%s）" % rows[i][1] + ("　　" + rows[i + 1][0] + "（%s）" % rows[i + 1][1] if i + 1 < len(rows) else ""),
                     "%s–%s" % (rows[i][1], rows[min(i + 1, len(rows) - 1)][1])) for i in range(0, len(rows), 2)]
        for i, (a, b) in enumerate(rows[:10]):
            txt(page, (52, 162 + i * 41), a, 13)
            txt(page, (350, 162 + i * 41), ("第 %s 页" % b) if b[:1].isdigit() else b, 12, HEAD, anchor="ma")
        save(page, out, "pages", "page_00.png")
        pages.append(dict(manual=manual, page=0, kind="index", template="ui/handbook_page/index.png", previous=None, next=1 if total > 1 else None,
                          entries=[dict(category=c["category"], subtype=c["subtype"], first_page=int(c["first_page"]), loc_key="sp4.handbook.%s.index.%s.%s" % (manual, c["category"], c["subtype"])) for c in chapters],
                          mockup="items/%s/pages/page_00.png" % manual))
        for c in chapters:
            tids = c["template_ids"].split("|")
            per = [int(x) for x in c["templates_per_page"].split("|")]
            assert sum(per) == len(tids) == 5 and len(per) == int(c["page_count"])
            title = CAT_ZH[c["category"]] + (SUB_ZH.get(c["subtype"]) and "（%s）" % SUB_ZH[c["subtype"]] or "") + "鉴定图例"
            k = 0
            for pi, n in enumerate(per):
                pno = int(c["first_page"]) + pi
                page = sheets.handbook("defect_example")
                header(page, manual, title, pno, total)
                txt(page, (28, 84), "先看完好参照，再对照问题图例的固定位置。", 12)
                txt(page, (28, 102), "线索只说明“值得怀疑”，结论以仪器记录为准。", 12)
                secs = []
                for si in range(2):
                    S = "s%d" % (si + 1)
                    if si < n:
                        t = core.BY_TID[tids[k]]
                        ex = example(c["example_asset_key"], t)
                        crop, s = crop_box(ex["bounds"])
                        x, y, w, h = sheets.SLOTS[S + "_head"]
                        txt(page, (x, y), "检查项 %d：%s" % (k + 1, zh_label(t["label_en"])), 14, HEAD)
                        tx, ty = sheets.SLOTS[S + "_text"][:2]
                        txt(page, (tx, ty), "工具：%s。位置：见图中固定标示处。" % TOOL_ZH[t["instrument_kind"]], 12)
                        txt(page, (tx, ty + 15), "提示：%s；%s。" % (CHAN_ZH.get(t["economic_channel"], "影响估价"), "不可修复" if t["repair_class"] == "none" else "可修复"), 12)
                        txt(page, (tx, ty + 30), "注意：单凭此线索不能断定材质、来源或真伪。", 12)
                        if t["mask_policy"] == "unchanged":
                            slot(page, sheets.SLOTS[S + "_left"], ex["reference"], None, 1)
                            slot(page, sheets.SLOTS[S + "_right"], ex["problem"], (230, 150, 384, 250), 1)
                            caps = ("外观无差别（参照）", "读数与声明不符（示意）")
                            crop_r, crop_p = None, [230, 150, 384, 250]
                        else:
                            slot(page, sheets.SLOTS[S + "_left"], ex["reference"], crop, s, ex["dark"])
                            slot(page, sheets.SLOTS[S + "_right"], ex["problem"], crop, s, ex["dark"])
                            caps = ("完好参照", "问题图例")
                            if crop is None:
                                print("WARNING no visible bounds", c["example_asset_key"], t["template_id"])
                            crop_r = crop_p = list(crop) if crop else None
                        cx, cy = sheets.SLOTS[S + "_cap_left"][:2]
                        txt(page, (cx + 80, cy + 2), caps[0], 12, OKG, anchor="ma")
                        cx, cy = sheets.SLOTS[S + "_cap_right"][:2]
                        txt(page, (cx + 80, cy + 2), caps[1], 12, RED, anchor="ma")
                        key = "sp4.handbook.%s" % t["template_id"]
                        secs.append(dict(TemplateId=t["template_id"], label_en=t["label_en"], category=t["category"], subtype=t["subtype"], example_asset_key=c["example_asset_key"],
                                         view=ex["view"], instrument=t["instrument_kind"], repair_class=t["repair_class"], economic_channel=t["economic_channel"],
                                         reference=dict(layer=ex["reference"], crop_px=crop_r, slot=S + "_left"), problem=dict(layer=ex["problem"], crop_px=crop_p, slot=S + "_right"),
                                         display_scale=s, loc_keys=dict(heading=key + ".heading", body=key + ".body", limits=key + ".limits", reference_caption=key + ".ref", problem_caption=key + ".problem")))
                        k += 1
                    else:                                   # single-template page: whole-object comparison
                        t = core.BY_TID[tids[k - 1]]
                        ex = example(c["example_asset_key"], t)
                        x, y, w, h = sheets.SLOTS[S + "_head"]
                        txt(page, (x, y), "整体对照", 14, HEAD)
                        tx, ty = sheets.SLOTS[S + "_text"][:2]
                        txt(page, (tx, ty), "同一件样品的整体视图，用于确认图例所在位置。", 12)
                        slot(page, sheets.SLOTS[S + "_left"], ex["reference"], None, 1, ex["dark"])
                        slot(page, sheets.SLOTS[S + "_right"], ex["problem"], None, 1, ex["dark"])
                        cx, cy = sheets.SLOTS[S + "_cap_left"][:2]
                        txt(page, (cx + 80, cy + 2), "整体参照", 12, OKG, anchor="ma")
                        cx, cy = sheets.SLOTS[S + "_cap_right"][:2]
                        txt(page, (cx + 80, cy + 2), "问题所在位置", 12, RED, anchor="ma")
                save(page, out, "pages", "page_%02d.png" % pno)
                pages.append(dict(manual=manual, page=pno, kind="defect_example", template="ui/handbook_page/defect_example.png", category=c["category"], subtype=c["subtype"],
                                  previous=pno - 1, next=pno + 1 if pno + 1 < total else None, sections=secs, mockup="items/%s/pages/page_%02d.png" % (manual, pno)))
        dump(dict(manual=manual, page_size=[440, 616], page_count=total, style_spec="art/VANILLA_HANDBOOK_SPEC_04.txt", pages=pages), out, "handbook-pages.json")
        allpages += pages
    tids = [s["TemplateId"] for p in allpages for s in p.get("sections", [])]
    assert len(tids) == len(set(tids)) == 250, len(tids)
    dump(dict(page_size=[440, 616], slots={k: list(v) for k, v in sheets.SLOTS.items()}, template_count=len(tids), pages=allpages,
              note="Mockup text is placeholder Chinese. Runtime text comes from the loc_keys; pages never read the current item's private truth."), B04, "handbook-pages.json")
    # ---- native comparison at the supplied 2560x1440 reference size
    ref = Image.open(os.path.join(ROOT, "art/references/vanilla_handbook/native_counterfeit_handbook.jpg")).convert("RGBA")
    ours = Image.open(os.path.join(B04, "items/manual_metals/pages/page_01.png")).convert("RGBA")
    ours = ours.resize((880, 1232), Image.NEAREST)
    d = ImageDraw.Draw(ref)
    d.rectangle([1500, 60, 2500, 1400], fill=(44, 36, 54, 255))
    ref.alpha_composite(ours, (1560, 104))
    from uikit import font
    d.rectangle([100, 1352, 990, 1392], fill=(26, 32, 48, 255))
    d.text((112, 1360), "NATIVE reference sheet (base game, reference only)", font=font(22, False), fill=(231, 221, 197, 255))
    d.rectangle([1560, 1352, 2440, 1392], fill=(26, 32, 48, 255))
    d.text((1572, 1360), "StarPawn 0.4 proposal page, 440x616 shown at x2 (not in game)", font=font(22, False), fill=(215, 171, 84, 255))
    save(ref.convert("RGB"), B04, "review", "native_handbook_comparison_2560x1440.png")
    print("pages", len(allpages), "templates", len(tids))


if __name__ == "__main__":
    build()
