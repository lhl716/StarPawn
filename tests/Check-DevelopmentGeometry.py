"""Check code-authored geometry fixtures; this does not approve final artwork."""
from pathlib import Path
import json,re
from PIL import Image,ImageChops

root=Path(__file__).resolve().parents[1]
items=json.loads((root/'mod/Content/items.json').read_text(encoding='utf-8'))['Items']
tested=0; centers=0
for d in items:
    if not d['Id'].startswith('sp3_') or d.get('ShopOnly'):continue
    path=root/'mod/Textures/Paper'/(d['TextureKey'].split(':')[1]+'.png')
    image=Image.open(path).convert('RGBA');w,h=image.size
    assert image.size==(384,256), (d['Id'],image.size)
    alpha=image.getchannel('A')
    family=re.sub(r'_p[0-9]+$','',d['Id'])
    mask=Image.open(root/'art/development/masks'/(family+'.png')).convert('L')
    assert ImageChops.difference(alpha,mask).getbbox() is None, 'Defect alpha escaped silhouette: '+d['Id']
    for spot in d['Hotspots']:
        assert mask.getpixel((round(spot['U']*w),round(spot['V']*h)))>0, 'Hotspot center outside object: '+d['Id']
        centers+=1
    if d.get('Material') in ('gold','silver'):
        base=Image.open(root/'mod/Textures/Paper'/(family+'_p0.png')).convert('RGBA')
        diff=ImageChops.difference(image.convert('RGB'),base.convert('RGB')).convert('L')
        assert ImageChops.multiply(diff,ImageChops.invert(mask)).getbbox() is None, 'Visible damage outside object: '+d['Id']
    tested+=1
print(f'PASS {tested} authored profile silhouettes and {centers} fixed hotspot centers; no damage extends outside object mask.')
print('Development geometry only; Claude style approval and final asset QA remain separate.')
