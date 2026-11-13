"""Export exact runtime IDs and artwork contracts without regenerating artwork."""
from pathlib import Path
import json, re
from PIL import Image

root = Path(__file__).resolve().parents[2]
items = json.loads((root / 'mod/Content/items.json').read_text(encoding='utf-8'))['Items']

def texture(key):
    if not key:
        return None
    name = key.split(':', 1)[1]
    paths = {kind: f'mod/Textures/{kind}/{name}.png' for kind in ('Paper', 'Items')}
    return {'key': key, 'files': {kind: {'path': path, 'pixels': list(Image.open(root/path).size)}
                                for kind, path in paths.items() if (root/path).is_file()}}

rows = []
for d in items:
    family = re.sub(r'_p\d+$', '', d['Id'])
    mask = f'art/development/masks/{family}.png'
    rows.append({
        'id': d['Id'], 'nativeId': 'starpawn.' + d['Id'], 'name': d['Name'],
        'familyId': d.get('FamilyId', ''), 'category': d['Category'],
        'gridCells': [d.get('WidthCells', 1), d.get('HeightCells', 1)],
        'role': 'organizer' if d.get('IsOrganizer') else 'manual' if d.get('ManualFor') else 'instrument' if d.get('InstrumentKind') else 'asset',
        'instrumentKind': d.get('InstrumentKind', ''), 'manualFor': d.get('ManualFor', ''),
        'front': texture(d['TextureKey']), 'window': texture(d.get('WindowTextureKey', '')),
        'repaired': texture(d.get('RepairedTextureKey', '')),
        'fixedEvidence': d['Hotspots'],
        'developmentMask': mask if (root/mask).is_file() else None,
        'artStatus': 'development-placeholder' if d['Id'].startswith('sp3_') else 'legacy-art-awaiting-style-approval'
    })
cards = [{'nativeId': f'starpawn.{kind}_card', 'gridCells': [2, 1], 'texture': texture('starpawn:' + key),
          'role': 'account-reference', 'note': 'Card serial/account state is rendered by UI; never bake real or save-specific account values into art.'}
         for kind, key in [('savings', 'bank_card'), ('credit', 'credit_card')]]
report = {'version': '0.3.0', 'catalogDefinitions': len(rows),
          'inspectionFrame': {'width': 298, 'height': 199, 'coordinates': 'normalized UV, origin top-left'},
          'notes': ['This is the authoritative runtime mapping; asset-plan.csv also includes optional visual proposals.',
                    'Do not change IDs to match proposal filenames. Align proposal artwork to existing fixed-evidence coordinates or submit coordinated config changes.',
                    'source/Content/Generate-Catalog.py --development-art overwrites development art. Do not run it after integrating approved artwork.',
                    'source/Art/Generate-Art.py is a legacy generator; do not use it to regenerate the new catalog.'],
          'items': rows, 'cards': cards}
(root/'art/runtime-art-map.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(f'Exported {len(rows)} catalog definitions + {len(cards)} physical card mappings.')
