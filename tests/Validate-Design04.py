"""Validate published design relationships; this does not test or enable 0.4 gameplay."""
from pathlib import Path
from collections import Counter, defaultdict
import csv, json, re

root=Path(__file__).resolve().parents[1]
def read(name):
    with (root/name).open(encoding='utf-8-sig',newline='') as f:
        rows=list(csv.DictReader(f))
    assert rows and all(None not in r and all(v is not None for v in r.values()) for r in rows),name
    return rows
def unique(rows,key):
    values=[r[key] for r in rows]
    assert len(values)==len(set(values)),key

items=read('art/ITEM_LAYOUT_04.csv');unique(items,'asset_key');unique(items,'target_family_id')
assert len(items)==110
by_key={r['asset_key']:r for r in items}
for r in items:
    w,h=int(r['grid_width']),int(r['grid_height'])
    assert (w,h) in {(1,1),(2,1),(1,2),(2,2)},r
    assert (int(r['items_width']),int(r['items_height']))==(w*64,h*64),r
    assert (int(r['paper_width']),int(r['paper_height']))==(384,256),r
    assert r['output_folder']=='art/proposals/batch04/items/'+r['asset_key']
    assert r['implementation_status']=='planned_04_not_registered'
for metal in ('gold','silver'):
    for form in ('ring','bracelet','necklace','earrings','bar'):
        assert sum(r['category']==metal+'_'+form and r['role']=='appraisable' for r in items)==2
assert sum(r['category']=='jade_rough' and r['role']=='appraisable' for r in items)==6
for form in ('pendant','cabochon','bangle','beads','carving'):
    assert sum(r['category']=='jade_'+form and r['role']=='appraisable' for r in items)==2

coverage=read('art/RUNTIME_COVERAGE_04.csv');unique(coverage,'current_id')
runtime=json.loads((root/'art/runtime-art-map.json').read_text(encoding='utf-8'))
expected={r['id'] for r in runtime['items']}|{r['nativeId'] for r in runtime['cards']}
assert {r['current_id'] for r in coverage}==expected and len(coverage)==241
for r in coverage:
    assert r['art_asset_key'] in by_key
    target=by_key[r['art_asset_key']]
    assert (r['target_grid_width'],r['target_grid_height'])==(target['grid_width'],target['grid_height'])

defects=read('docs/DEFECT_CATALOG_04.csv');unique(defects,'template_id')
groups=Counter((r['category'],r['subtype']) for r in defects)
assert len(defects)==200 and len(groups)==40 and set(groups.values())=={5}
for r in items:
    if r['role']=='appraisable':
        assert groups[(r['category'],r['defect_subtype'] or 'all_models')]==5,(r['asset_key'],r['category'])
for r in defects:
    assert r['view_id'] in {'front','window'}
    assert r['instrument_kind'] in {'none','scale','assay','loupe','jade_light','uv'}
    assert r['mask_policy'] in {'same_alpha','state_mask','unchanged'}
    assert r['composition_effect']=='none_at_discovery'

effects=read('docs/EVENT_EFFECTS_04.csv')
event_ids={r['event_id'] for r in effects};assert len(event_ids)==14
channels={'commodity_price','jade_tier_value','category_demand','supplier_availability',
    'processing_energy_cost','processing_service_cost','processing_duration','recovery_efficiency',
    'new_savings_rate','new_credit_rate','new_loan_rate','new_pawn_rate','new_pawn_ltv',
    'risk_appetite','assay_service_cost','alchemy_catalyst_cost','alchemy_daily_quota'}
metadata=defaultdict(set)
for r in effects:
    metadata[r['event_id']].add(tuple(r[k] for k in ('title','trigger','duration_days','cooldown_days')))
    assert r['channel'] in channels,r
    assert 1<=int(r['duration_days'])<=7 and int(r['cooldown_days'])>=int(r['duration_days'])
    assert abs(int(r['delta_bps']))<=10000
    if r['channel']=='jade_tier_value':assert r['target'] in {'dou','nuo','bing'}
assert all(len(x)==1 for x in metadata.values())
stories=read('docs/EVENT_STORIES_04.csv');unique(stories,'event_id')
assert {r['event_id'] for r in stories}==event_ids
recipes=read('docs/RECIPES_04.csv');unique(recipes,'recipe_id')
for r in recipes:
    for column in ('input_asset_keys','machine_asset_key','required_equipment_or_supplies','output_asset_keys'):
        for key in r[column].split('|'):
            assert key in by_key,(r['recipe_id'],column,key)
    assert int(r['input_limit'])>0 and int(r['total_energy_units'])>=0 and int(r['duration_days'])>=1
    for entry in r['metal_efficiency_bps'].split('|'):
        assert 0<=int(entry.split(':')[1])<=10000

ui=read('art/UI_ASSETS_04.csv');unique(ui,'asset_key')
assert len(ui)==61
icons={r['asset_key'].removeprefix('event_') for r in ui if r['kind']=='event_icon'}
assert icons==event_ids
for r in ui:
    assert int(r['width'])>0 and int(r['height'])>0
    if r['kind']=='screen_plate':assert (int(r['width']),int(r['height']))==(600,400)
    if r['kind']=='event_icon':assert (int(r['width']),int(r['height']))==(64,64)
    assert r['output_folder']=='art/proposals/batch04/ui/'+r['asset_key']
for r in items+ui:
    assert '..' not in r['output_folder']
for link in re.findall(r'\[[^\]]+\]\(([^)]+)\)',(root/'README.md').read_text(encoding='utf-8')):
    if not link.startswith(('https:','#')):assert (root/link).is_file(),link
for name in ('art/BATCH_ART_SPEC_04.txt','art/CLAUDE_BATCH_PROMPT_04.txt','docs/DEFECT_RULES_04.txt',
             'docs/SYSTEMS_DESIGN_04.txt','docs/MOD_RESEARCH_04.txt'):
    assert not re.search(r'[\u3400-\u9fff]',(root/name).read_text(encoding='utf-8')),name
print(f'PASS design only: {len(items)} fixed item-art rows; {len(coverage)} runtime mappings; '
      f'{len(defects)} templates in {len(groups)} category/subtype sets; {len(event_ids)} events; '
      f'{len(recipes)} recipes; {len(ui)} UI rows; complete links and English prose. Gameplay not executed.')
