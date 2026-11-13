"""Check the compatibility release against the pre-art catalog, not itself.

Requires a completed Import-Batch04.py stage and the original main commit.
These checks establish file/data compatibility, not human visual readability.
"""
import json
from pathlib import Path
import subprocess
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
BASE = '60eddd027eccbbff92bb328ac97ed3265cf349a2'
old = json.loads(subprocess.check_output(
    ['git', 'show', BASE + ':mod/Content/items.json'], cwd=ROOT).decode('utf-8-sig'))
new = json.loads((ROOT/'mod/Content/items.json').read_text(encoding='utf-8-sig'))
report = json.loads((ROOT/'work/art04-stage/report.json').read_text(encoding='utf-8'))
allowed = {'U', 'V', 'Width', 'Height', 'TemplateId', 'Label', 'LabelKey', 'Guidance', 'GuidanceKey'}
assert {k:v for k,v in old.items() if k != 'Items'} == {k:v for k,v in new.items() if k != 'Items'}
assert len(old['Items']) == 239 and len(new['Items']) == 254
new_by_id = {d['Id']: d for d in new['Items']}
for before in old['Items']:
    after = new_by_id[before['Id']]
    assert {k:v for k,v in before.items() if k != 'Hotspots'} == {k:v for k,v in after.items() if k != 'Hotspots'}, after['Id']
    assert len(before['Hotspots']) == len(after['Hotspots']), after['Id']
    for a, b in zip(before['Hotspots'], after['Hotspots']):
        assert {k:v for k,v in a.items() if k not in allowed} == {k:v for k,v in b.items() if k not in allowed}, (after['Id'], b['Id'])
print('PASS: 239 pre-art definitions retain facts, price inputs, occupied cells and saved item/evidence/cause IDs.')
old_ids = {d['Id'] for d in old['Items']}
added = [d for d in new['Items'] if d['Id'] not in old_ids]
assert len(added) == 15
for d in added:
    assert d['Id'].startswith('sp31_manual_') and d['IsBook'] and d['ShopOnly']
    assert d['WidthCells'] == d['HeightCells'] == 1 and not d['Hotspots']
    assert 0 < len(d['HandbookTemplates']) <= 8 and d['HandbookFamilies']
print('PASS: 15 additive one-cell handbook volumes have at most 5 pages including the introduction.')

def texture(key, kind='Paper'):
    path = ROOT/'mod/Textures'/kind/(key.split(':')[-1]+'.png')
    im = Image.open(path)
    assert im.mode == 'RGBA', str(path)
    assert set(np.unique(np.array(im)[:,:,3])).issubset({0,255}), str(path)
    return im

refs = set()
entries = {}
for d in new['Items']:
    for field in ('TextureKey', 'WindowTextureKey', 'RepairedTextureKey'):
        if d.get(field):
            refs.add(d[field])
            assert texture(d[field]).size == (384,256), d['Id']
            texture(d[field], 'Items')
    if not d.get('ShopOnly'):
        for h in d['Hotspots']:
            entries.setdefault(h['TemplateId'], (d,h))
for d,h in entries.values():
    for side in ('reference','problem'):
        assert texture('manual_'+d['Id']+'_'+h['Id']+'_'+side).size == (160,104)
for key in ('bank_card','credit_card','handbook_intro','handbook_page','panel','button','button_hover','marker'):
    texture(key)
assert len(entries) == 78
print(f'PASS: {len(refs)} runtime item texture keys, 78 paired handbook examples, cards and UI skins resolve with binary alpha.')

assert not report['problems'], report['problems']
by_id = {d['Id']: d for d in new['Items']}
assert len(report['hotspots']) == 307
for record in report['hotspots']:
    d = by_id[record['definition']]
    h = next(h for h in d['Hotspots'] if h['Id'] == record['evidence'])
    x1,y1,x2,y2 = record['bounds']
    assert 0 <= x1 < x2 <= 384 and 0 <= y1 < y2 <= 256
    assert record['visible_pixels'] >= 24
    assert abs(h['U']-(x1+x2)/768) < 0.0001
    assert abs(h['V']-(y1+y2)/512) < 0.0001
    assert abs(h['Width']-(x2-x1)/384) < 0.0001
    assert abs(h['Height']-(y2-y1)/256) < 0.0001
print('PASS: 307 stored centre-based hotspots match effective composite clue bounds.')

for path in (ROOT/'work/art04-stage/Textures').rglob('*.png'):
    target = ROOT/'mod/Textures'/path.relative_to(ROOT/'work/art04-stage/Textures')
    assert target.read_bytes() == path.read_bytes(), str(target)
print('PASS: staged authored textures exactly match the runtime delivery.')
print('NOT PROVEN: every clue is intuitively readable at the final game scale; inspect in-game and report misses.')
