"""Revision 05 art import: explicit footprints and recomposed art for every definition.

Adapts the batch05 models to the saved profiles, without rerolling truth.

The 0.4 art catalog is broader than the playable catalog. This importer replaces
presentation only: IDs, causes, prices, purity, yields and occupied cells survive.
Old multi-defect profiles are rendered explicitly, including repaired composites.
Run without --apply to stage and validate; --apply publishes the staged files.
"""
import argparse
import copy
import csv
import json
import os
from pathlib import Path
import shutil
import sys
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--art-source', type=Path, default=ROOT/'art/proposals/batch05/source')
parser.add_argument('--apply', action='store_true')
args = parser.parse_args()
os.environ['STARPAWN_SOURCE_ROOT'] = str(ROOT)
sys.path.insert(0, str(args.art_source.resolve()))
import core
import m_all  # registers the reviewed, deterministic model and template renderers
import uikit
import sheets
import build_ui
from handbook_volumes import PREFIX as VOLUME_PREFIX, append_volumes

STAGE = ROOT/'work/art05-stage'
for folder in ('Textures/Paper', 'Textures/Items', 'Content/Localization'):
    (STAGE/folder).mkdir(parents=True, exist_ok=True)
pack = json.loads((ROOT/'mod/Content/items.json').read_text(encoding='utf-8-sig'))
before = copy.deepcopy(pack)
loc = json.loads((ROOT/'mod/Content/Localization/zh-CN.json').read_text(encoding='utf-8-sig'))
texts = {e['Key']: e for e in loc['Entries']}
with (ROOT/'art/RUNTIME_COVERAGE_04.csv').open(encoding='utf-8-sig', newline='') as f:
    coverage = {r['current_id']: r for r in csv.DictReader(f)}
# The fifteen playable short volumes have their own manifest rows in Revision 05.
for d in pack['Items']:
    if d['Id'].startswith(VOLUME_PREFIX):
        coverage[d['Id']] = dict(current_id=d['Id'], art_asset_key='manual_'+d['Id'][len(VOLUME_PREFIX):])
missing = [d['Id'] for d in pack['Items'] if d['Id'] not in coverage or coverage[d['Id']]['art_asset_key'] not in core.LAYOUT]
assert not missing, ('definitions without a Revision 05 manifest row', missing)
images, facts, problems = {}, [], []
manual_sources = {}

METAL_LABELS = {
    'gold_ring': ('外圈划痕', '戒臂裂缝', '戒面镀层露底'),
    'gold_bracelet': ('弧面擦伤', '镯缘裂隙', '内弧镀层露底'),
    'gold_necklace': ('链条缠结', '链环开口', '链环镀层露底'),
    'gold_earrings': ('耳针弯折', '吊环断裂', '耳饰镀层露底'),
    'gold_bar': ('金条划伤', '金条边角撞伤', '金条夹芯露缝'),
    'silver_ring': ('刻槽磨损', '银戒裂缝', '银戒镀层露底'),
    'silver_bracelet': ('银镯刻纹磨损', '银镯裂缝', '银镯镀层露底'),
    'silver_necklace': ('银链压扁', '银链开环', '银链镀层露底'),
    'silver_earrings': ('银耳钩扭曲', '银耳环断裂', '银耳饰镀层露底'),
    'silver_bar': ('银条划伤', '银条卷边', '银条夹芯露缝'),
}
INCLUSIONS = {
    'yellow': ('chalk_cloud', '开窗棉絮'), 'black': ('opaque_heart', '开窗暗色内含物'),
    'water': ('fibrous_cloud', '开窗纤维状棉絮'), 'white': ('carbon_pocket', '开窗碳色团块'),
    'rust': ('cotton_sheet', '开窗片状棉絮'), 'mountain': ('black_band', '开窗黑色带'),
}


def localized(h, template, label, guidance):
    h.update(TemplateId=template, Label=label, Guidance=guidance,
             LabelKey='template.'+template+'.label', GuidanceKey='template.'+template+'.guide')
    for key, value in ((h['LabelKey'], label), (h['GuidanceKey'], guidance)):
        texts[key] = dict(Key=key, Source=value, Text=value)


def template_for(d, h, row):
    if d['Id'].startswith('sp3_rough_'):
        family = row['defect_subtype']
        if h['Id'] == 'jade_inclusion_window':
            suffix, label = INCLUSIONS[family]
            tid = 'sp4_rough_'+family+'_'+suffix
            localized(h, 'compat04_'+family+'_inclusion', label,
                      '比较开窗内局部与周围的透明度和颜色。该处内含物影响可用料；透玉灯可辅助观察，切割前仍不能确认产量。')
            return tid
        window = h['ViewId'] == 'window'
        localized(h, 'compat04_'+family+('_window_crack' if window else '_shell_crack'),
                  '开窗延伸裂隙' if window else '皮壳裂隙',
                  '比较圈定部位的分叉深线与亮边。皮壳和开窗中的裂隙可能属于同一损伤原因，只计价一次；裂隙不能抛光消除。')
        return 'legacy_window_crack' if window else 'legacy_rough_crack'
    if not d['Id'].startswith('sp3_'):
        return 'legacy_'+h['Id']
    matches = [t for t in core.templates_for(row) if t['legacy_reference'] == h['Id']]
    if len(matches) != 1:
        raise ValueError('No unambiguous template: '+d['Id']+'/'+h['Id'])
    t = matches[0]
    if row['category'] in METAL_LABELS:
        index = ('metal_wear', 'metal_split', 'metal_plating').index(h['Id'])
        label = METAL_LABELS[row['category']][index]
        guide = ('比较示例中的局部形状与反光。此处'+label+'影响品相与工艺价；可送工坊修复。'
                 if index < 2 else '局部底材与表层的颜色不同。外表不能代表整体含量；用成色分析仪核实，再按实测含量估价。')
    else:
        label = {'jade_crack': '局部裂隙', 'jade_dye': '颜色聚集异常'}[h['Id']]
        guide = ('比较局部的分叉深线和错位亮边。裂隙影响结构及品相，不能靠抛光消除。'
                 if h['Id'] == 'jade_crack' else '比较沟槽或孔口的颜色聚集。使用紫外灯辅助检查；这一线索不能单独证明天然或处理结论。')
    localized(h, t['template_id'], label, guide)
    return t['template_id']


def render(key, lod, tids, window=False, state=None):
    extra = {}
    row = core.LAYOUT[key]
    if row['category'] == 'jade_rough':
        extra.update(window=window, tier='nuo')  # compatibility image never reads a saved hidden grade
    elif row['category'].startswith('jade_'):
        extra['tier'] = 'nuo'
    if state:
        extra['state'] = state
    return core.export(core.render(key, lod, tids, **extra), lod)


def output(key, kind, image):
    filename = key.split(':')[-1]+'.png'
    dest = 'Textures/'+kind+'/'+filename
    previous = images.get(dest)
    if previous is not None and not np.array_equal(np.array(previous), np.array(image)):
        raise ValueError('Shared texture key has conflicting states: '+dest)
    images[dest] = image


def box(mask):
    return core.bounds(mask)


def evidence(d, h, whole, without, base, kind, key, view):
    changed = core.diff(whole, without)
    b = box(changed)
    # A full profile must retain a visibly inspectable contribution from EVERY
    # cause. Measuring a single overlay alone would miss occlusion in composites.
    threshold = 24 if kind == 'Paper' else 4
    if b is None or int(changed.sum()) < threshold:
        problems.append(f"{d['Id']}/{h['Id']}/{kind}: only {int(changed.sum())} visible pixels")
        return
    a, original = core.alpha(whole), core.alpha(base)
    if np.any(changed & ~(a | original)):
        problems.append(f"{d['Id']}/{h['Id']}/{kind}: evidence outside object")
    if kind == 'Paper':
        h.update({k:v for k,v in core.hotspot(b, whole.size).items() if k != 'bounds_px'})
        manual_sources[(d['Id'], h['Id'])] = (without.copy(), whole.copy(), b)
        facts.append(dict(definition=d['Id'], evidence=h['Id'], template=h['TemplateId'],
                          view=view, texture=key, bounds=list(b), visible_pixels=int(changed.sum())))


coverage_rows = []
for d in pack['Items']:
    key = coverage[d['Id']]['art_asset_key']
    row = core.LAYOUT[key]
    # Explicit footprint on every definition: no silent 1x1 default survives regeneration.
    coverage_rows.append(dict(current_id=d['Id'], family=d.get('FamilyId', ''), art_asset_key=key,
                              previous_width=d.get('WidthCells', 1), previous_height=d.get('HeightCells', 1),
                              width=int(row['grid_width']), height=int(row['grid_height']),
                              internal_width=row['internal_grid_width'], internal_height=row['internal_grid_height']))
    d['WidthCells'], d['HeightCells'] = int(row['grid_width']), int(row['grid_height'])
    if d['Id'].startswith(VOLUME_PREFIX):
        d['TextureKey'] = 'starpawn:' + d['Id']
    mapping = {h['Id']: template_for(d,h,row) for h in d['Hotspots']}
    state = row['required_states'].split(';')[0] if row['role'] not in ('appraisable','legacy_appraisable') else None
    views = [('front', d['TextureKey'])]
    if d.get('WindowTextureKey'):
        views.append(('window',d['WindowTextureKey']))
    if d.get('RepairedTextureKey'):
        views.append(('repaired',d['RepairedTextureKey']))
    for view, texture in views:
        visible = [h for h in d['Hotspots'] if (view == 'window' or h['ViewId'] == 'front')
                   and (view != 'repaired' or not h['Repairable'])]
        tids = [mapping[h['Id']] for h in visible]
        for lod, kind in ((1,'Paper'),(0,'Items')):
            base = render(key,lod,[],window=view=='window',state=state)
            whole = render(key,lod,tids,window=view=='window',state=state)
            output(texture,kind,whole)
            if view == 'repaired':
                continue
            for h in visible:
                if h['ViewId'] != view:
                    continue
                without = render(key,lod,[mapping[q['Id']] for q in visible if q['Id'] != h['Id']],
                                 window=view=='window',state=state)
                evidence(d,h,whole,without,base,kind,texture,view)

for model, texture in (('savings_card','bank_card'),('credit_card','credit_card')):
    for lod,kind in ((1,'Paper'),(0,'Items')):
        output(texture,kind,render(model,lod,[],state='front_valid'))

# The reader uses only fixed reference examples; never the current player's asset.
# The grouping and representative order are the same as Appraisal.Handbook().
representatives = {}
for d in pack['Items']:
    if d.get('ShopOnly'):
        continue
    for h in d['Hotspots']:
        representatives.setdefault(h['TemplateId'],(d,h))
for d,h in representatives.values():
    source = manual_sources.get((d['Id'],h['Id']))
    if source is None:
        continue
    clean, damaged, b = source
    cx,cy=(b[0]+b[2])/2,(b[1]+b[3])/2
    width=max(96,(b[2]-b[0])*2.4,(b[3]-b[1])*2.4*160/104)
    width=min(width,384,256*160/104);height=width*104/160
    x=max(0,min(384-width,cx-width/2));y=max(0,min(256-height,cy-height/2))
    rect=(round(x),round(y),round(x+width),round(y+height))
    for suffix,im in (('reference',clean),('problem',damaged)):
        output('manual_'+d['Id']+'_'+h['Id']+'_'+suffix,'Paper',im.crop(rect).resize((160,104),Image.Resampling.NEAREST))

# Rebuild the compatible UI skins from the reviewed drawing code, keeping text
# in the game's own renderer. The introductory page has no illustration slots.
panel = uikit.plate(600,400)
draw = ImageDraw.Draw(panel)
draw.line((22,40,578,40),fill=uikit.PAPER_SH)
draw.line((22,365,578,365),fill=uikit.PAPER_SH)
output('panel','Paper',panel)
for key,source,state in (('button','button_primary','default'),
                         ('button_hover','button_primary','hover'),
                         ('marker','evidence_marker','discovered')):
    output(key,'Paper',build_ui.component(source,state))
for intro in (False,True):
    if intro:
        im,draw=sheets.base(stamp=False)
        draw.line((28,576,411,576),fill=sheets.SHEET_SH)
    else:
        im=sheets.handbook('defect_example')
    # Only the close control is exposed. The proposed duplicate-page glyph has
    # no runtime action, so omit that misleading decoration from both skins.
    draw=ImageDraw.Draw(im)
    draw.rectangle((390,8,407,25),fill=sheets.SHEET)
    # Page availability is dynamic. Do not leave an apparently clickable arrow
    # baked into the last page after its Next control has disappeared.
    draw.rectangle((34,584,42,596),fill=sheets.SHEET)
    draw.rectangle((398,584,406,596),fill=sheets.SHEET)
    output('handbook_intro' if intro else 'handbook_page','Paper',im)

# Static game facts and saved identifiers must be bit-for-bit stable.
allowed={'U','V','Width','Height','TemplateId','Label','LabelKey','Guidance','GuidanceKey'}
top_allowed={'Hotspots','WidthCells','HeightCells','TextureKey'}
for old,new in zip(before['Items'],pack['Items']):
    assert {k:v for k,v in old.items() if k not in top_allowed} == {k:v for k,v in new.items() if k not in top_allowed},new['Id']
    for a,b in zip(old['Hotspots'],new['Hotspots']):
        assert {k:v for k,v in a.items() if k not in allowed} == {k:v for k,v in b.items() if k not in allowed},new['Id']

volumes = [d['Id'] for d in pack['Items'] if d['Id'].startswith(VOLUME_PREFIX)]
with (ROOT/'art/RUNTIME_COVERAGE_05.csv').open('w', encoding='utf-8', newline='') as f:
    w = csv.DictWriter(f, fieldnames=list(coverage_rows[0])); w.writeheader(); w.writerows(coverage_rows)
report=dict(source='art/proposals/batch05/source',definitions=len(pack['Items']),handbook_volumes=volumes,
            textures=len(images),manual_examples=len(representatives),hotspots=facts,problems=problems,
            migration='Facts and saved IDs are preserved. Occupied cells change to ITEM_LAYOUT_05; placed items need the runtime layout migration.',
            scope='The 0.4 jewelry/recovery machines are art proposals, not newly enabled gameplay.')
(STAGE/'report.json').write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
for path,im in images.items():
    im.save(STAGE/path)
if problems:
    print('\n'.join(problems))
    raise SystemExit(f'BLOCKED: {len(problems)} unreadable or invalid clues; runtime files were not changed.')
loc['Entries']=list(texts.values())
for name,data in (('items.json',pack),('Localization/zh-CN.json',loc)):
    (STAGE/'Content'/name).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
if args.apply:
    for path in images:
        shutil.copy2(STAGE/path,ROOT/'mod'/path)
    for name in ('items.json','Localization/zh-CN.json'):
        shutil.copy2(STAGE/'Content'/name,ROOT/'mod/Content'/name)
print(f"PASS: {len(pack['Items'])} definitions, {len(images)} textures, {len(facts)} hotspots, {len(representatives)} manual examples; {'applied' if args.apply else 'staged'}.")
