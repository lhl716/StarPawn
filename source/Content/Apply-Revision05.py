"""Revision 05 content stage. Run AFTER source/Art/Import-Batch05.py --apply.

Deterministic and re-runnable. It never changes saved IDs, causes, prices or truth of
existing definitions; it adds business labels, explicit value components, commission
ranks and container grids, then (re)generates the sp5_* jewelry / recovery catalog,
unified-rules.json and the bilingual string entries from the authored art manifests.
"""
import csv
import json
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
B5 = ROOT / 'art/proposals/batch05'
sys.path.insert(0, str(B5 / 'source'))
import core  # noqa: E402  (batch05 generator: layout rows and the defect catalog)
from build_handbook import zh_label, CAT_ZH  # noqa: E402

pack_path = ROOT / 'mod/Content/items.json'
pack = json.loads(pack_path.read_text(encoding='utf-8-sig'))
pack['Items'] = [d for d in pack['Items'] if not d['Id'].startswith('sp5_')]
zh = json.loads((ROOT / 'mod/Content/Localization/zh-CN.json').read_text(encoding='utf-8-sig'))
en = json.loads((ROOT / 'mod/Content/Localization/en.json').read_text(encoding='utf-8-sig'))
ZH = {e['Key']: e for e in zh['Entries'] if not e['Key'].startswith(('sp5.', 'content.sp5_', 'template.sp5'))}
EN = {e['Key']: e for e in en['Entries'] if not e['Key'].startswith(('sp5.', 'content.sp5_', 'template.sp5'))}
with (ROOT / 'art/RUNTIME_COVERAGE_05.csv').open(encoding='utf-8-sig', newline='') as f:
    COVER = {r['current_id']: r for r in csv.DictReader(f)}
LAYOUT = core.LAYOUT


INLINE = {}


def text(key, english, chinese):
    # The runtime also translates inline by source text, so one Chinese source must have one English text.
    assert INLINE.setdefault(chinese, english) == english, ('conflicting translation', chinese, INLINE[chinese], english)
    ZH[key] = dict(Key=key, Source=chinese, Text=chinese)
    EN[key] = dict(Key=key, Source=chinese, Text=english)


# ------------------------------------------------------------------ A. existing definitions
LABELS = {'ring': ['Luxury'], 'bracelet': ['Luxury'], 'necklace': ['Luxury'], 'earrings': ['Luxury'], 'bar': ['Materials', 'Investment'],
          'silver_cup': ['Luxury', 'Collectibles'], 'jade_rough': ['Materials'], 'jade_pendant': ['Luxury'], 'jade_cabochon': ['Luxury'],
          'jade_bangle': ['Luxury'], 'jade_beads': ['Luxury'], 'jade_carving': ['Luxury'], 'antique_vase': ['Collectibles'], 'painting': ['Collectibles'],
          'console': ['Electronics', 'Entertainment'], 'handheld': ['Electronics', 'Entertainment'], 'cartridge': ['Entertainment', 'Collectibles'],
          'disc': ['Entertainment', 'Collectibles'], 'book': ['Collectibles', 'Entertainment']}
RANK = {'jade_rough': 2, 'jade_pendant': 1, 'jade_cabochon': 1, 'jade_bangle': 1, 'jade_beads': 1, 'jade_carving': 1, 'antique_vase': 2,
        'painting': 2, 'console': 2, 'handheld': 2, 'cartridge': 1, 'disc': 1, 'book': 1, 'silver_cup': 0}
DEVICE = {'console': (120000, 400000, 60000000), 'handheld': (60000, 200000, 25000000)}


def category_of(art_key):
    c = LAYOUT[art_key]['category']
    for metal in ('gold_', 'silver_'):
        if c.startswith(metal) and c != 'silver_cup':
            return c[len(metal):]
    return c


for d in pack['Items']:
    art = COVER[d['Id']]['art_asset_key']
    cat = category_of(art)
    row = LAYOUT[art]
    d['ArtModel'] = art
    d['Labels'] = [] if d.get('ShopOnly') else list(LABELS.get(cat, []))
    d['PrimaryLabel'] = d['Labels'][0] if d['Labels'] else ''
    metal = d.get('Material') in ('gold', 'silver')
    d['CommissionRank'] = -1 if d.get('ShopOnly') else (0 if metal else RANK.get(cat, -1))
    if d.get('IsBook') and d.get('HandbookTemplates'):                # promotion entitlement rank for short volumes
        d['CommissionRank'] = 0 if d['ManualFor'] == 'metals' else (2 if 'rough' in d['Id'] else 1)
    if row['internal_grid_width']:
        d['InternalWidthCells'], d['InternalHeightCells'], d['Role'] = int(row['internal_grid_width']), int(row['internal_grid_height']), 'container'
    if d.get('InstrumentKind'):
        d['Role'] = 'tool'
    if cat in DEVICE:
        d['GoldMicrograms'], d['SilverMicrograms'], d['CopperMicrograms'] = DEVICE[cat]
        d['RecoveryClass'] = 'device'
    if metal and not d.get('ShopOnly'):
        pure = d.get('WeightMilligrams', 0) * d.get('ActualPurityBasisPoints', 0) // 10    # micrograms of precious metal actually present
        d['GoldMicrograms' if d['Material'] == 'gold' else 'SilverMicrograms'] = pure
        d['RecoveryClass'] = 'metal_goods'
    for h in d['Hotspots']:
        h['Component'] = ('metal_purity' if h.get('RevealsMaterial') else 'premium' if h.get('RevealsAuthenticity')
                          else 'workmanship' if metal else 'condition')
    if d.get('ManualFor'):
        key = 'content.' + d['Id'] + '.description'
        if d.get('HandbookTemplates'):
            n = 1 + (len(d['HandbookTemplates']) + 1) // 2
            text(key, 'Short appraisal volume, %d pages. Use Read to open it.' % min(5, n), '鉴定分册，共 %d 页。在物品上选择“阅读”即可翻开。' % min(5, n))
        else:
            text(key, 'Appraisal handbook. Use Read to open it.', '鉴定手册。在物品上选择“阅读”即可翻开。')
        d['Description'], d['DescriptionKey'] = ZH[key]['Text'], key
    if d.get('IsOrganizer'):
        key = 'content.' + d['Id'] + '.description'
        text(key, 'Holds eight 2x3 handbooks (8x6 cells inside). Empty it before moving or selling.', '可收纳八本 2x3 手册（内部 8x6 格）。搬动或出售前请先取空。')
        d['Description'], d['DescriptionKey'] = ZH[key]['Text'], key

# ------------------------------------------------------------------ B. new sp5 catalog
TEX = {}      # texture key -> (paper png, items png)
NEW = []
OK_CHANNEL = {'craft_damage': 1500, 'structural_damage': 3000, 'service_history': 800, 'processability_damage': 2000,
              'gem_condition': 1500, 'gem_structure': 3000}
CH_TEXT = {'craft_damage': ('This reduces workmanship. Metal value is unchanged unless material is missing.', '影响工艺价；只要没有缺料，金属价值不变。'),
           'structural_damage': ('This weakens the structure and reduces workmanship; it can usually be repaired.', '影响结构、扣减工艺价；通常可以修复。'),
           'service_history': ('This records earlier work. It lowers workmanship slightly and stays on the record after polishing.', '这是旧修痕迹，略减工艺价；抛光后仍留在记录里。'),
           'processability_damage': ('This lowers what the piece can yield in processing. It does not remove metal a second time.', '影响加工可得量；不会重复扣除金属。'),
           'gem_condition': ('This lowers the stone\'s condition value once. Polishing a setting cannot remove it.', '只扣一次宝石品相价；抛光镶座无法消除。'),
           'gem_structure': ('This is structural damage to the stone and lowers its value once.', '这是宝石的结构损伤，只扣一次价值。')}
TOOL_TEXT = {'loupe': ('Use the loupe.', '用放大镜看。'), 'none': ('Visible to the eye.', '肉眼可见。')}


def manifest(model):
    d = B5 / 'items' / model
    return (json.loads((d / 'manifest.json').read_text()), json.loads((d / 'hotspots.json').read_text())['hotspots'],
            json.loads((d / 'combinations.json').read_text())['combinations'])


def tex(key, model, state):
    TEX[key] = (B5 / 'items' / model / 'paper' / (state + '.png'), B5 / 'items' / model / 'items' / (state + '.png'))
    assert TEX[key][0].exists(), (model, state)
    return 'starpawn:' + key


def chosen_templates(model, limit=3):
    out = []
    for t in core.templates_for(LAYOUT[model]):
        if t['mask_policy'] == 'unchanged' or t['instrument_kind'] not in ('none', 'loupe') or t['economic_channel'] not in OK_CHANNEL or 'missing' in t['template_id']:
            continue
        out.append(t)
    return out[:limit]


def hotspot(model, hot, state, t, basis):
    rec = [h for h in hot if h['state'] == state and h['image'] == 'paper' and h['TemplateId'] == t['template_id'] and h['bounds_px']][0]
    tid = t['template_id'].replace('sp4_', 'sp5_')
    label_zh = LABEL_ZH.get(tid)
    if label_zh is None:                                   # unique, category-specific Chinese label per template
        label_zh = CAT_ZH.get(t['category'], '') + zh_label(t['label_en'])
        n = 2
        while label_zh in LABEL_ZH.values():
            label_zh = CAT_ZH.get(t['category'], '') + zh_label(t['label_en']) + str(n)
            n += 1
        LABEL_ZH[tid] = label_zh
    ce, cz = CH_TEXT[t['economic_channel']]
    te, tz = TOOL_TEXT[t['instrument_kind']]
    guide_en = '%s: %s. %s %s' % (t['visual_anchor'].capitalize(), t['visual_cue'][0].lower() + t['visual_cue'][1:], te, ce)
    guide_zh = '%s%s：对照完好处，看%s的变化。%s' % (tz, '', label_zh, cz)
    text('template.%s.label' % tid, t['label_en'], label_zh)
    text('template.%s.guide' % tid, guide_en, guide_zh)
    gem = t['economic_channel'].startswith('gem')
    return dict(Id=core.short(t['template_id']), CauseId=tid, Label=label_zh, LabelKey='template.%s.label' % tid, TemplateId=tid,
                Guidance=guide_zh, GuidanceKey='template.%s.guide' % tid, InstrumentKind='' if t['instrument_kind'] == 'none' else t['instrument_kind'],
                ViewId='front', U=rec['U'], V=rec['V'], Width=rec['Width'], Height=rec['Height'],
                ValueDeduction=max(1, basis * OK_CHANNEL[t['economic_channel']] // 10000), Repairable=t['repair_class'] != 'none' and not gem,
                RevealsMaterial=False, RevealsAuthenticity=False, Component='stone' if gem else 'workmanship' if basis_kind[0] == 'craft' else 'condition')


basis_kind = ['craft']
LABEL_ZH = {}


def define(ident, name_en, name_zh, category_zh, desc_en, desc_zh, model, **fields):
    row = LAYOUT[model]
    text('content.%s.name' % ident, name_en, name_zh)
    text('content.%s.description' % ident, desc_en, desc_zh)
    d = dict(Id=ident, Name=name_zh, NameKey='content.%s.name' % ident, Category=category_zh, Material='none', TextureKey='',
             Description=desc_zh, DescriptionKey='content.%s.description' % ident, ReadingText='', ReadingKey='', BaseValue=0, ActualBaseValue=0,
             Hotspots=[], IsGenuine=True, WidthCells=int(row['grid_width']), HeightCells=int(row['grid_height']), ArtModel=model,
             Labels=[], PrimaryLabel='', CommissionRank=-1)
    d.update(fields)
    if d['Labels'] and not d['PrimaryLabel']:
        d['PrimaryLabel'] = d['Labels'][0]
    NEW.append(d)
    COVER[ident] = dict(current_id=ident, family=d.get('FamilyId', ''), art_asset_key=model, previous_width='', previous_height='',
                        width=d['WidthCells'], height=d['HeightCells'], internal_width=row['internal_grid_width'], internal_height=row['internal_grid_height'])
    return d


def profiles(model, prefix, basis, kind, limit=3):
    """-> [(suffix, state name, hotspots, repaired state or None)] for p0 + singles + one whitelisted pair."""
    basis_kind[0] = kind
    man, hot, combos = manifest(model)
    tpls = chosen_templates(model, limit)
    out = [('p0', prefix + 'intact', [], None)]
    names = {(): prefix + 'intact'}
    for t in tpls:
        names[(t['template_id'],)] = prefix + core.short(t['template_id'])
    pair = None
    ids = [t['template_id'] for t in tpls]
    for c in combos:
        if c['variant'] == prefix and len(c['templates']) == 2 and all(x in ids for x in c['templates']):
            pair = c
            names[tuple(c['templates'])] = c['profile']
            break
    sets = [(t,) for t in ids] + ([tuple(pair['templates'])] if pair else [])
    for i, group in enumerate(sets):
        state = names[group]
        hs = [hotspot(model, hot, state, next(t for t in tpls if t['template_id'] == g), basis) for g in group]
        keep = tuple(g for g in group if next(t for t in tpls if t['template_id'] == g)['repair_class'] == 'none' or kind == 'stone')
        rep = names.get(keep) if keep != group else None
        out.append(('p%d' % (i + 1), state, hs, rep))
    return out, tpls


# ---- gems
GEMS = {'diamond': ('Diamond', '钻石', 120), 'ruby': ('Ruby', '红宝石', 90), 'sapphire': ('Sapphire', '蓝宝石', 70), 'emerald': ('Emerald', '祖母绿', 85), 'pearl': ('Pearl', '珍珠', 40)}
SHAPES = {'round': ('Round', '圆形', 800), 'princess': ('Princess', '公主方', 900), 'oval': ('Oval', '椭圆', 1000), 'cushion': ('Cushion', '垫形', 1100),
          'pear': ('Pear', '梨形', 900), 'step': ('Step-cut', '阶梯', 1000), 'baroque': ('Baroque', '异形', 1000)}
VARIANTS = {'gem_diamond_round': ['lab'], 'gem_ruby_oval': ['lab'], 'gem_sapphire_oval': ['lab'], 'gem_emerald_oval': ['lab'], 'gem_ruby_cushion': ['imit'], 'gem_pearl_round': ['imit']}
gem_templates = {}
for model in [k for k in LAYOUT if LAYOUT[k]['category'].startswith('gem_') and LAYOUT[k]['role'] == 'appraisable']:
    _, fam, shape = model.split('_')
    fe, fz, unit = GEMS[fam]
    se, sz, mct = SHAPES[shape]
    value = mct * unit // 1000
    profs, tpls = profiles(model, '', value, 'stone')
    gem_templates.setdefault(fam, [])
    for origin_tag, origin, factor in [('nat', 'cultured' if fam == 'pearl' else 'natural', 10000)] + [(v, 'laboratory_grown' if v == 'lab' else 'imitation', 4000 if v == 'lab' else 500) for v in VARIANTS.get(model, [])]:
        for suffix, state, hs, rep in (profs if origin_tag == 'nat' else profs[:1]):
            ident = 'sp5_%s_%s_%s' % (model, origin_tag, suffix)
            d = define(ident, '%s %s' % (se, fe), '%s%s' % (sz, fz), '宝石',
                       'Loose stone. Carat weight and identity are read from tests and reports, not from colour.', '散石。克拉重量与身份以检测和报告为准，不看颜色下结论。',
                       model, TextureKey=tex(ident if origin_tag == 'nat' else 'sp5_%s_nat_%s' % (model, suffix), model, state), Role='gem', GemFamily=fam, GemShape=shape,
                       GemMilliCarats=mct, GemUnitValue=unit, GemOrigin=origin, GemClaim='cultured' if fam == 'pearl' else 'natural',
                       BaseValue=value, ActualBaseValue=value * factor // 10000, Hotspots=hs, FamilyId='sp5_gem_' + fam, Form=shape,
                       ManualCategory='jewelry', Labels=['Luxury'], CommissionRank=2, IsGenuine=origin_tag == 'nat')
            if rep:
                pass  # gem damage has no repair class: no repaired art
            for h in hs:
                if h['TemplateId'] not in gem_templates[fam]:
                    gem_templates[fam].append(h['TemplateId'])

# ---- settings and mounted jewelry
SETTINGS = {r['assembly_asset_key']: r for r in core.read_csv('docs/JEWELRY_SETTINGS_04.csv')}
FORM_TEXT = {'ring': ('Solitaire Ring', '单石戒指'), 'pendant': ('Bezel Pendant', '包镶吊坠'), 'earrings': ('Stud Earrings', '耳钉一对'),
             'bracelet': ('Trilogy Bracelet', '三石手链'), 'necklace': ('Drop Necklace', '吊坠项链')}
CRAFT = {'ring': 12, 'pendant': 10, 'earrings': 16, 'bracelet': 24, 'necklace': 20}
MISSING = {'ring': 'missing_center', 'pendant': 'missing_insert', 'earrings': 'missing_right', 'bracelet': 'missing_left', 'necklace': 'missing_drop'}
mount_templates = {}
for assembly, srow in SETTINGS.items():
    form = srow['category'].replace('jewelry_', '')
    stones = srow['allowed_stone_asset_keys'].split('|')
    smodel = srow['setting_asset_key']
    for metal, mname, purity, mlabel_en, mlabel_zh in (('gold', 'au18k', 7500, '18K Gold', '18K金'), ('silver', 'ag925', 9250, '925 Silver', '925银')):
        prefix = '%s_%s_' % (mname, stones[0][4:])
        profs, tpls = profiles(assembly, prefix, CRAFT[form], 'craft')
        profs = profs[:1 + len(tpls)]                                 # p0 + singles (no pair art per stone variant)
        common = dict(Material=metal, Role='setting', SettingForm=form, SocketCount=int(srow['socket_count']), CompatibleStones=stones,
                      WeightMilligrams=int(srow['setting_mass_ug']) // 1000, DeclaredPurityBasisPoints=purity, ActualPurityBasisPoints=purity,
                      CraftValue=CRAFT[form], FamilyId='sp5_jewelry_' + form, Form=form, ManualCategory='jewelry', Labels=['Luxury'],
                      GoldMicrograms=int(srow['setting_mass_ug']) * purity // 10000 if metal == 'gold' else 0,
                      SilverMicrograms=int(srow['setting_mass_ug']) * purity // 10000 if metal == 'silver' else 0, RecoveryClass='metal_goods')
        define('sp5_setting_%s_%s' % (form, mname), '%s %s Setting' % (mlabel_en, FORM_TEXT[form][0]), '%s%s空托' % (mlabel_zh, FORM_TEXT[form][1]), '戒托配件',
               'Empty setting. It takes one compatible stone per socket; an empty socket here is not a defect.', '空镶座。每个镶口配一颗适配的宝石；空着不算瑕疵。',
               smodel, TextureKey=tex('sp5_setting_%s_%s' % (form, mname), smodel, mname + '_empty'), ArtState='intact', CommissionRank=-1, **common)
        for suffix, state, hs, rep in profs[1:]:
            short = state[len(prefix):]
            ident = 'sp5_jewelry_%s_%s_%s' % (form, mname, suffix)
            define(ident, '%s %s' % (mlabel_en, FORM_TEXT[form][0]), '%s%s' % (mlabel_zh, FORM_TEXT[form][1]), '镶嵌珠宝',
                   'Mounted jewelry. Setting metal, workmanship and each real stone are valued separately.', '镶嵌首饰。镶座金属、工艺和每一颗真实宝石分别计价。',
                   smodel, TextureKey=tex('sp5_j_%s_%s_%s_%s' % (form, mname, stones[0][4:], short), assembly, state), ArtState=short, Hotspots=hs,
                   RepairedTextureKey='starpawn:sp5_setting_%s_%s' % (form, mname), RepairCost=4, CommissionRank=3, **common)
            for h in hs:
                mount_templates.setdefault(form, [])
                if h['TemplateId'] not in mount_templates[form]:
                    mount_templates[form].append(h['TemplateId'])
        man = manifest(assembly)[0]
        for g in stones:                                             # mounted art for every supported stone
            gp = '%s_%s_' % (mname, g[4:])
            for st in ['intact', MISSING[form]] + [s[len(prefix):] for _, s, _, _ in profs[1:]]:
                tex('sp5_j_%s_%s_%s_%s' % (form, mname, g[4:], st), assembly, gp + st)

# ---- recovery materials with category defects
REC = {'scrap_board': ('Scrap Circuit Board', '废电路板', 30, (80000, 250000, 30000000), 'component', ['Electronics', 'Materials']),
       'scrap_processor': ('Scrap Processor', '废处理器', 25, (150000, 50000, 5000000), 'component', ['Electronics', 'Materials']),
       'scrap_connectors': ('Scrap Connectors', '废接插件', 12, (40000, 100000, 15000000), 'component', ['Electronics', 'Materials']),
       'ore_gold': ('Gold Ore Lot', '金矿石批', 60, (3000000, 1500000, 20000000), 'ore', ['Materials']),
       'ore_silver': ('Silver Ore Lot', '银矿石批', 40, (200000, 30000000, 40000000), 'ore', ['Materials']),
       'ore_copper': ('Copper Ore Lot', '铜矿石批', 25, (50000, 2000000, 400000000), 'ore', ['Materials']),
       'refined_gold_bar': ('Refined Gold Bar', '精炼金条', 0, None, 'bar', ['Materials', 'Investment']),
       'refined_silver_bar': ('Refined Silver Bar', '精炼银条', 0, None, 'bar', ['Materials', 'Investment']),
       'refined_copper_ingot': ('Refined Copper Ingot', '精炼铜锭', 30, (0, 0, 500000000), 'bar', ['Materials']),
       'alloy_gold18k': ('18K Gold Alloy Bar', '18K金合金条', 0, None, 'bar', ['Materials']),
       'alloy_silver925': ('925 Silver Alloy Bar', '925银合金条', 0, None, 'bar', ['Materials'])}
BARS = {'refined_gold_bar': ('gold', 20000, 9990), 'refined_silver_bar': ('silver', 100000, 9990), 'alloy_gold18k': ('gold', 20000, 7500), 'alloy_silver925': ('silver', 100000, 9250)}
rec_templates = {}
for model, (ne, nz, base, budget, rclass, labels) in REC.items():
    bar = BARS.get(model)
    craft = 20 if bar else base
    profs, tpls = profiles(model, '', craft, 'craft' if bar else 'condition', limit=2 if model.startswith('alloy') else 3)
    for suffix, state, hs, rep in profs:
        ident = 'sp5_%s_%s' % (model, suffix)
        extra = {}
        if bar:
            extra = dict(Material=bar[0], WeightMilligrams=bar[1], DeclaredPurityBasisPoints=bar[2], ActualPurityBasisPoints=bar[2], CraftValue=craft, Form='bar',
                         GoldMicrograms=bar[1] * bar[2] // 10 if bar[0] == 'gold' else 0, SilverMicrograms=bar[1] * bar[2] // 10 if bar[0] == 'silver' else 0,
                         CopperMicrograms=bar[1] * (10000 - bar[2]) // 10 if model.startswith('alloy') else 0)
        else:
            extra = dict(BaseValue=base, ActualBaseValue=base, GoldMicrograms=budget[0], SilverMicrograms=budget[1], CopperMicrograms=budget[2])
        d = define(ident, ne, nz, '回收物料' if not bar else '精炼金属',
                   'Physical material. What it holds is finite and saved; recovery only separates what is already there.', '实物物料。其中含量有限且已记录；回收只是把已有的成分分离出来。',
                   model, TextureKey=tex(ident, model, state), Hotspots=hs, FamilyId='sp5_' + model, ManualCategory='recovery', Labels=labels,
                   CommissionRank=2, RecoveryClass=rclass, Role=rclass, **extra)
        if rep:
            d['RepairedTextureKey'] = 'starpawn:sp5_%s_%s' % (model, next(s for s, st, _, _ in profs if st == rep))
            d['RepairCost'] = 3
        for h in hs:
            rec_templates.setdefault(model, [])
            if h['TemplateId'] not in rec_templates[model]:
                rec_templates[model].append(h['TemplateId'])

# ---- functional items (status states only; never appraisal defects)
UTIL = [
    ('sp5_lot_gold_scrap', 'sorted_gold_scrap', 'sealed', 'Sorted Gold-bearing Scrap', '分拣含金废料', dict(Role='lot', RecoveryClass='sorted', Labels=['Materials'])),
    ('sp5_lot_silver_scrap', 'sorted_silver_scrap', 'sealed', 'Sorted Silver-bearing Scrap', '分拣含银废料', dict(Role='lot', RecoveryClass='sorted', Labels=['Materials'])),
    ('sp5_lot_copper_scrap', 'sorted_copper_scrap', 'sealed', 'Sorted Copper Scrap', '分拣含铜废料', dict(Role='lot', RecoveryClass='sorted', Labels=['Materials'])),
    ('sp5_lot_recovered_gold', 'recovered_gold_lot', 'measured', 'Recovered Gold Lot', '回收金料', dict(Role='lot', RecoveryClass='lot', Labels=['Materials'])),
    ('sp5_lot_recovered_silver', 'recovered_silver_lot', 'measured', 'Recovered Silver Lot', '回收银料', dict(Role='lot', RecoveryClass='lot', Labels=['Materials'])),
    ('sp5_lot_recovered_copper', 'recovered_copper_lot', 'measured', 'Recovered Copper Lot', '回收铜料', dict(Role='lot', RecoveryClass='lot', Labels=['Materials'])),
    ('sp5_cast_gold_bar', 'refined_gold_bar', 'intact', 'Cast Gold Bar', '自铸金条', dict(Role='lot', RecoveryClass='bar', Labels=['Materials', 'Investment'])),
    ('sp5_cast_silver_bar', 'refined_silver_bar', 'intact', 'Cast Silver Bar', '自铸银条', dict(Role='lot', RecoveryClass='bar', Labels=['Materials', 'Investment'])),
    ('sp5_cast_copper_ingot', 'refined_copper_ingot', 'intact', 'Cast Copper Ingot', '自铸铜锭', dict(Role='lot', RecoveryClass='bar', Labels=['Materials'])),
    ('sp5_alloy_gold18k_bar', 'alloy_gold18k', 'intact', 'Shop-made 18K Alloy Bar', '自配18K合金条', dict(Role='lot', RecoveryClass='bar', Labels=['Materials'])),
    ('sp5_alloy_silver925_bar', 'alloy_silver925', 'intact', 'Shop-made 925 Alloy Bar', '自配925合金条', dict(Role='lot', RecoveryClass='bar', Labels=['Materials'])),
    ('sp5_inert_residue', 'inert_residue', 'sealed', 'Inert Residue', '惰性残渣', dict(Role='lot', RecoveryClass='residue', Labels=['Materials'])),
    ('sp5_mixed_e_scrap', 'mixed_e_scrap', 'sealed', 'Mixed Electronic Scrap', '混合电子废料', dict(Role='mixed', RecoveryClass='mixed', Labels=['Electronics', 'Materials'], BaseValue=45, ActualBaseValue=45,
                                                                                         GoldMicrograms=200000, SilverMicrograms=600000, CopperMicrograms=80000000)),
    ('sp5_process_cartridge', 'process_cartridge', 'sealed', 'Process Cartridge', '工艺耗材筒', dict(Role='process_cartridge', ShopOnly=True, BaseValue=12, ActualBaseValue=12)),
    ('sp5_energy_cell', 'energy_cell', 'charged', 'Energy Cell', '能源电池', dict(Role='energy_cell', ShopOnly=True, BaseValue=20, ActualBaseValue=20, Labels=['Materials', 'Energy'])),
    ('sp5_crucible', 'crucible', 'empty', 'Crucible', '坩埚', dict(Role='crucible', ShopOnly=True, BaseValue=40, ActualBaseValue=40)),
    ('sp5_ingot_mold', 'ingot_mold', 'empty', 'Ingot Mold', '锭模', dict(Role='ingot_mold', ShopOnly=True, BaseValue=35, ActualBaseValue=35)),
    ('sp5_recovery_station', 'recovery_station', 'idle', 'Recovery Station', '回收工作站', dict(Role='recovery_station', ShopOnly=True, BaseValue=420, ActualBaseValue=420)),
    ('sp5_refining_furnace', 'refining_furnace', 'idle', 'Refining Furnace', '精炼炉', dict(Role='refining_furnace', ShopOnly=True, BaseValue=520, ActualBaseValue=520)),
    ('sp5_jeweler_bench', 'jeweler_bench', 'idle', "Jeweler's Bench", '珠宝工作台', dict(Role='jeweler_bench', ShopOnly=True, BaseValue=380, ActualBaseValue=380)),
    ('sp5_jeweler_toolkit', 'jeweler_toolkit', 'closed', "Jeweler's Toolkit", '珠宝工具包', dict(Role='jeweler_toolkit', ShopOnly=True, BaseValue=90, ActualBaseValue=90)),
    ('sp5_tool_gem_probe', 'tool_gem_probe', 'idle', 'Gem Screening Probe', '宝石筛查笔', dict(Role='tool', InstrumentKind='gem_probe', ShopOnly=True, BaseValue=180, ActualBaseValue=180)),
    ('sp5_materials_bin', 'materials_bin', 'empty', 'Materials Bin', '物料箱', dict(Role='container', ShopOnly=True, BaseValue=70, ActualBaseValue=70)),
    ('sp5_gem_case', 'gem_case', 'empty', 'Gem Case', '宝石收纳盒', dict(Role='container', ShopOnly=True, BaseValue=90, ActualBaseValue=90)),
    ('sp5_presentation_case', 'presentation_case', 'empty', 'Presentation Case', '首饰礼盒', dict(Role='container', ShopOnly=True, BaseValue=60, ActualBaseValue=60)),
    ('sp5_jewelry_display', 'jewelry_display', 'empty', 'Jewelry Display Tray', '首饰展示盘', dict(Role='container', ShopOnly=True, BaseValue=110, ActualBaseValue=110)),
    ('sp5_card_holder', 'card_holder', 'empty', 'Card Holder', '卡包', dict(Role='container', ShopOnly=True, BaseValue=30, ActualBaseValue=30)),
    ('sp5_manual_organizer_large', 'manual_organizer_large', 'empty', 'Twelve-book Organizer', '十二册手册书匣', dict(Role='container', ShopOnly=True, IsOrganizer=True, BaseValue=260, ActualBaseValue=260)),
]
CAP = {'sp5_materials_bin': ('Holds material lots (4x4 cells inside). No nested containers.', '收纳物料（内部 4x4 格）。不能再放容器。'),
       'sp5_gem_case': ('Holds loose stones (4x4 cells inside).', '收纳散石（内部 4x4 格）。'),
       'sp5_presentation_case': ('Holds finished jewelry (4x3 cells inside); a 1x3 necklace fits.', '收纳成品首饰（内部 4x3 格）；放得下 1x3 的项链。'),
       'sp5_jewelry_display': ('Displays stock for sale (6x4 cells inside). Customer property does not belong here.', '陈列待售货品（内部 6x4 格）。顾客的东西不能放这里。'),
       'sp5_card_holder': ('Holds two 2x1 cards (4x1 cells inside).', '可放两张 2x1 的卡（内部 4x1 格）。'),
       'sp5_manual_organizer_large': ('Holds twelve 2x3 handbooks (8x9 cells inside). Empty it before moving or selling.', '可收纳十二本 2x3 手册（内部 8x9 格）。搬动或出售前请先取空。')}
for ident, model, state, ne, nz, extra in UTIL:
    row = LAYOUT[model]
    de, dz = CAP.get(ident, ('Workshop item.' if extra.get('ShopOnly') else 'Measured material. Its recorded content sets its value.',
                             '工坊用品。' if extra.get('ShopOnly') else '已计量的物料，按记录的含量计价。'))
    d = define(ident, ne, nz, '工坊设备' if extra.get('ShopOnly') else '回收物料', de, dz, model, TextureKey=tex(ident, model, state),
               ManualCategory='recovery' if not extra.get('ShopOnly') else '', FamilyId=ident, **extra)
    if row['internal_grid_width']:
        d['InternalWidthCells'], d['InternalHeightCells'] = int(row['internal_grid_width']), int(row['internal_grid_height'])

# ---- handbooks for the new categories (each at most five pages)
READ = {'jewelry': ('Value the setting metal, its workmanship and each real stone separately. A probe can only say "refer for testing"; a laboratory report establishes identity. '
                    'An empty service setting is not a missing-stone defect.\n\nCompare the matched pictures, then look at the same place on the piece in front of you.',
                    '镶座金属、工艺和每一颗真实宝石要分开计价。筛查笔只能提示“建议送检”，身份以实验室报告为准。空着的备用镶座不算缺石瑕疵。\n\n先比较成对的图例，再看手上这件的同一位置。'),
        'recovery': ('Recovery only separates metal that is already in the lot. Appearance does not show content; weigh and assay before you value it. '
                     'Compare repair and resale against recovery before you commit a job.\n\nCompare the matched pictures, then look at the same place on the lot in front of you.',
                     '回收只是把物料里已有的金属分离出来。外观看不出含量，估价前先称重、测成色。开工前先比较“维修转卖”和“回收”哪个更划算。\n\n先比较成对的图例，再看手上这批的同一位置。')}
VOLUMES = [
    ('gems_a', 'jewelry', 'Gem Handbook: Diamond and Ruby', '宝石鉴定·钻石与红宝石', ['sp5_gem_diamond', 'sp5_gem_ruby'], 2),
    ('gems_b', 'jewelry', 'Gem Handbook: Sapphire and Emerald', '宝石鉴定·蓝宝石与祖母绿', ['sp5_gem_sapphire', 'sp5_gem_emerald'], 2),
    ('gems_c', 'jewelry', 'Gem Handbook: Pearl', '宝石鉴定·珍珠', ['sp5_gem_pearl'], 2),
    ('mounts_a', 'jewelry', 'Mounted Jewelry: Rings and Pendants', '镶嵌首饰·戒指与吊坠', ['sp5_jewelry_ring', 'sp5_jewelry_pendant'], 3),
    ('mounts_b', 'jewelry', 'Mounted Jewelry: Earrings and Bracelets', '镶嵌首饰·耳钉与手链', ['sp5_jewelry_earrings', 'sp5_jewelry_bracelet'], 3),
    ('mounts_c', 'jewelry', 'Mounted Jewelry: Necklaces', '镶嵌首饰·项链', ['sp5_jewelry_necklace'], 3),
    ('scrap_a', 'recovery', 'Recovery: Boards and Processors', '物料回收·电路板与处理器', ['sp5_scrap_board', 'sp5_scrap_processor'], 2),
    ('scrap_b', 'recovery', 'Recovery: Connectors', '物料回收·接插件', ['sp5_scrap_connectors'], 2),
    ('ore', 'recovery', 'Recovery: Ores', '物料回收·矿石', ['sp5_ore_gold', 'sp5_ore_silver', 'sp5_ore_copper'], 2),
    ('bars_a', 'recovery', 'Recovery: Refined Gold and Silver', '物料回收·精炼金银', ['sp5_refined_gold_bar', 'sp5_refined_silver_bar'], 2),
    ('bars_b', 'recovery', 'Recovery: Copper and Alloys', '物料回收·铜锭与合金', ['sp5_refined_copper_ingot', 'sp5_alloy_gold18k', 'sp5_alloy_silver925'], 2),
]
for key, category, ne, nz in (('jewelry', 'jewelry', 'Jewelry and Gem Handbook Index', '珠宝宝石手册总目'), ('recovery', 'recovery', 'Recovery and Refining Handbook Index', '回收精炼手册总目')):
    ident = 'sp5_manual_' + key
    text('content.%s.reading' % ident, READ[category][0], READ[category][1])
    define(ident, ne, '《%s》' % nz, '鉴定手册', 'Index of the short volumes for this business. Use Read to open it.', '本业务各分册的目录。在物品上选择“阅读”即可翻开。',
           'manual_' + key, TextureKey=tex(ident, 'manual_' + key, 'closed'), IsBook=True, ManualFor=category, ShopOnly=True, BaseValue=65, ActualBaseValue=65,
           ReadingText=READ[category][1], ReadingKey='content.%s.reading' % ident, WorkId='starpawn-manual-' + key, EditionId='2247')
for key, category, ne, nz, families, rank in VOLUMES:
    ident = 'sp5_manual_' + key
    templates = sorted({h['TemplateId'] for d in NEW if d.get('FamilyId') in families for h in d['Hotspots']})
    assert 0 < len(templates) <= 8, (key, len(templates))
    pages = min(5, (len(templates) + 1) // 2 + 1)
    text('content.%s.reading' % ident, READ[category][0], READ[category][1])
    define(ident, ne, '《%s》' % nz, '鉴定手册', 'Short appraisal volume, %d pages. Use Read to open it.' % pages, '鉴定分册，共 %d 页。在物品上选择“阅读”即可翻开。' % pages,
           'manual_' + key, TextureKey=tex(ident, 'manual_' + key, 'closed'), IsBook=True, ManualFor=category, ShopOnly=True, BaseValue=25, ActualBaseValue=25,
           ReadingText=READ[category][1], ReadingKey='content.%s.reading' % ident, WorkId='starpawn-handbook-' + key, EditionId='2247',
           HandbookTemplates=templates, HandbookFamilies=families, CommissionRank=rank)

pack['Items'].extend(NEW)
assert len({d['Id'] for d in pack['Items']}) == len(pack['Items']) <= 1024
assert all(d.get('WidthCells') and d.get('HeightCells') for d in pack['Items'])

# ------------------------------------------------------------------ C. unified rules (events, recipes, intent)
events = {}
for r in core.read_csv('docs/EVENT_EFFECTS_04.csv'):
    e = events.setdefault(r['event_id'], dict(Id=r['event_id'], DurationDays=int(r['duration_days']), CooldownDays=int(r['cooldown_days']), UnlockDay=8, Rows=[]))
    e['Rows'].append(dict(Channel=r['channel'], Target=r['target'], DeltaBps=int(r['delta_bps']), Application=r['application']))
for r in core.read_csv('docs/EVENT_STORIES_04.csv'):
    if r['event_id'] in events:
        events[r['event_id']]['UnlockDay'] = int(r['unlock_day'])
# Bounded intent shifts (Revision 05 section 5): a credit squeeze moves 15 points toward pawn requests.
events['credit_squeeze']['Rows'].append(dict(Channel='intent_shift', Target='request_pawn', DeltaBps=1500, Application='weight_points_x100'))
events['collectors_visit']['Rows'].append(dict(Channel='intent_shift', Target='offer_sale', DeltaBps=1000, Application='weight_points_x100'))
recipes = []
OUT = {'cast_gold': 'sp5_cast_gold_bar', 'cast_silver': 'sp5_cast_silver_bar', 'cast_copper': 'sp5_cast_copper_ingot', 'alloy_gold18k': 'sp5_alloy_gold18k_bar', 'alloy_silver925': 'sp5_alloy_silver925_bar'}
for r in core.read_csv('docs/RECIPES_04.csv'):
    eff = dict(x.split(':') for x in r['metal_efficiency_bps'].split('|'))
    inputs = r['input_asset_keys'].split('|')
    if r['recipe_id'] == 'dismantle_device':
        inputs = ['device']
    if r['recipe_id'] == 'sort_metal_goods':
        inputs = ['metal_goods']
    if r['recipe_id'] == 'sort_scrap':
        inputs = ['mixed']
    recipes.append(dict(Id=r['recipe_id'], Kind=r['process_kind'], Machine=r['machine_asset_key'], Inputs=inputs,
                        Equipment=[x for x in r['required_equipment_or_supplies'].split('|') if x], OutputBar=OUT.get(r['recipe_id'], ''),
                        InputLimit=int(r['input_limit']), EnergyUnits=int(r['total_energy_units']), DurationDays=int(r['duration_days']), ServiceFee=int(r['service_cr']),
                        GoldBps=int(eff.get('Au', eff.get('all', 10000))), SilverBps=int(eff.get('Ag', eff.get('all', 10000))), CopperBps=int(eff.get('Cu', eff.get('all', 10000)))))
rules = dict(RulesVersion=5, CopperSellPer100g=6, CopperBuyPer100g=4, EventStartChancePercent=35, MaxConcurrentEvents=3, MaxExtraEventVisitorsPerDay=2,
             CommissionBaseFee=5, CommissionFeeBps=300, CommissionFeeCap=60, PawnDefaultLtvBps=5000, PawnMinLtvBps=3000, PawnMaxLtvBps=6500,
             PawnUnverifiedHaircutBps=6000, PawnCraftLiquidationBps=5000, OfferValidDays=1, ResalePriceBasisPoints=8500, CustomerAskBasisPoints=9000,
             OrderDepositBps=3000, GemLabGrownBps=4000, GemImitationBps=500, GemLabFee=6, Events=list(events.values()), Recipes=recipes,
             IntentProfiles=[dict(Id='ordinary', Leave=50, Sale=30, Pawn=20), dict(Id='urgent_cash', Leave=15, Sale=35, Pawn=50),
                             dict(Id='sentimental', Leave=55, Sale=5, Pawn=40), dict(Id='dealer', Leave=15, Sale=75, Pawn=10), dict(Id='collector', Leave=70, Sale=15, Pawn=15)])
(ROOT / 'mod/Content/unified-rules.json').write_text(json.dumps(rules, indent=2) + '\n', encoding='utf-8')
fin = json.loads((ROOT / 'mod/Content/finance-rules.json').read_text(encoding='utf-8-sig'))
fin.update(PawnLoanToValueBps=5000, PawnMinLoanToValueBps=3000, PawnMaxLoanToValueBps=6500)
(ROOT / 'mod/Content/finance-rules.json').write_text(json.dumps(fin, indent=2) + '\n', encoding='utf-8')
insp = json.loads((ROOT / 'mod/Content/inspection-rules.json').read_text(encoding='utf-8-sig'))
insp['ExperiencePerDiscovery'] = 0   # click-XP is retired; the native career is the only progression
(ROOT / 'mod/Content/inspection-rules.json').write_text(json.dumps(insp, indent=2) + '\n', encoding='utf-8')

# ------------------------------------------------------------------ D2. handbook crops, location cues, captions
from PIL import Image, ImageDraw
PAPER = ROOT / 'mod/Textures/Paper'
CAT_ROWS = {r['template_id'].replace('sp4_', 'sp5_'): r for r in core.CATALOG}
PART_ZH = {'clasp': '搭扣', 'prong': '镶爪', 'post': '耳针', 'link': '链节', 'chain': '链条', 'band': '戒圈', 'shank': '戒臂', 'bezel': '包边', 'bail': '扣环',
           'girdle': '腰棱', 'crown': '冠部', 'table': '台面', 'face': '正面', 'edge': '边缘', 'corner': '边角', 'stamp': '印记', 'mark': '标记', 'seam': '接缝',
           'pin': '引脚', 'contact': '触点', 'pad': '焊盘', 'trace': '线路', 'tag': '标签', 'ridge': '凸脊', 'surface': '表面', 'wall': '外壁', 'socket': '镶口', 'drop': '吊坠'}
def part_of(anchor):
    for word in reversed(anchor.replace('-', ' ').split()):
        w = word.rstrip('s')
        if w in PART_ZH:
            return w
    return 'surface'
items = {d['Id']: d for d in pack['Items']}
reps = {}
for d in pack['Items']:
    if d.get('ShopOnly'):
        continue
    for h in d['Hotspots']:
        reps.setdefault(h['TemplateId'], (d, h))
def paper_png(key):
    return Image.open(PAPER / (key.split(':')[1] + '.png')).convert('RGBA')
made = 0
for tid, (d, h) in reps.items():
    prefix = 'manual_%s_%s' % (d['Id'], h['Id'])
    damaged = paper_png(d['TextureKey'])
    fam = [x for x in pack['Items'] if x.get('FamilyId') == d.get('FamilyId') and not x['Hotspots']]
    clean = paper_png(fam[0]['TextureKey']) if fam else damaged
    cx, cy = h['U'] * 384, h['V'] * 256
    w = min(384, max(96, h['Width'] * 384 * 2.6)); hh = w * 104 / 160
    if hh > 256:
        hh, w = 256, 256 * 160 / 104
    box = (int(max(0, min(384 - w, cx - w / 2))), int(max(0, min(256 - hh, cy - hh / 2))))
    box = box + (int(box[0] + w), int(box[1] + hh))
    if tid.startswith('sp5_') or not (PAPER / (prefix + '_reference.png')).exists():
        for suffix, im in (('reference', clean), ('problem', damaged)):
            im.crop(box).resize((160, 104), Image.NEAREST).save(PAPER / (prefix + '_' + suffix + '.png'))
            Image.new('RGBA', (64, 64)).save(ROOT / 'mod/Textures/Items' / (prefix + '_' + suffix + '.png'))
    loc = clean.resize((120, 80), Image.NEAREST)
    dr = ImageDraw.Draw(loc)
    x0, y0, x1, y1 = (cx - h['Width'] * 192) / 3.2, (cy - h['Height'] * 128) / 3.2, (cx + h['Width'] * 192) / 3.2, (cy + h['Height'] * 128) / 3.2
    dr.rectangle([x0 - 2, y0 - 2, x1 + 2, y1 + 2], outline=(166, 71, 65, 255), width=2)
    loc.save(PAPER / (prefix + '_location.png')); Image.new('RGBA', (64, 64)).save(ROOT / 'mod/Textures/Items' / (prefix + '_location.png'))
    made += 1
    row = CAT_ROWS.get(tid)
    part = part_of(row['visual_anchor']) if row else 'surface'
    text(h['LabelKey'].replace('.label', '.ref_caption'), 'Intact ' + part, '完好' + PART_ZH[part])

# ------------------------------------------------------------------ D. strings, textures, coverage
for key, (english, chinese) in json.loads((ROOT / 'source/Content/strings05.json').read_text(encoding='utf-8')).items():
    text(key, english, chinese)
for key, (paper, items) in TEX.items():
    shutil.copy2(paper, ROOT / 'mod/Textures/Paper' / (key + '.png'))
    shutil.copy2(items, ROOT / 'mod/Textures/Items' / (key + '.png'))
zh['Entries'], en['Entries'] = list(ZH.values()), list(EN.values())
pack_path.write_text(json.dumps(pack, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(ROOT / 'mod/Content/Localization/zh-CN.json').write_text(json.dumps(zh, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(ROOT / 'mod/Content/Localization/en.json').write_text(json.dumps(en, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
rows = list(COVER.values())
with (ROOT / 'art/RUNTIME_COVERAGE_05.csv').open('w', encoding='utf-8', newline='') as f:
    w = csv.DictWriter(f, fieldnames=['current_id', 'family', 'art_asset_key', 'previous_width', 'previous_height', 'width', 'height', 'internal_width', 'internal_height'])
    w.writeheader()
    w.writerows(rows)
bad = [d['Id'] for d in pack['Items'] if (d['WidthCells'], d['HeightCells']) != (int(LAYOUT[d['ArtModel']]['grid_width']), int(LAYOUT[d['ArtModel']]['grid_height']))]
assert not bad, bad
print('PASS: %d definitions (%d new sp5), %d textures, %d events, %d recipes, %d zh / %d en strings' % (
    len(pack['Items']), len(NEW), len(TEX), len(events), len(recipes), len(ZH), len(EN)))
