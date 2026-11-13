"""Build authored 0.3 profile combinations and optional geometric DEVELOPMENT art.

No runtime randomness or original game art is used. The optional art is a replaceable
geometry fixture for playtesting, NOT the approved Claude art direction. Never edits
legacy PNGs. Run from any directory; Pillow is needed only for --development-art.
"""
import argparse
import copy
import hashlib
import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--development-art', action='store_true')
args = parser.parse_args()
pack_path = ROOT / 'mod/Content/items.json'
pack = json.loads(pack_path.read_text(encoding='utf-8-sig'))
items = [d for d in pack['Items'] if not d['Id'].startswith('sp3_')]
loc_path = ROOT / 'mod/Content/Localization/zh-CN.json'
localization = json.loads(loc_path.read_text(encoding='utf-8-sig'))
texts = {e['Key']: e for e in localization['Entries']}
geometry = []

def text(key, source):
    texts[key] = {'Key': key, 'Source': source, 'Text': source}

def base(ident, name, category, value, texture=None):
    return dict(Id=ident, Name=name, NameKey='content.'+ident+'.name', Category=category,
                Material='none', TextureKey='starpawn:'+(texture or ident), Description='星港流通商品；先看实物，再决定交易。',
                DescriptionKey='content.'+ident+'.description', ReadingText='', ReadingKey='',
                BaseValue=value, ActualBaseValue=value, Hotspots=[], IsGenuine=True)

templates = {
    'metal_wear': ('表面划伤', '典型特征：浅色平行划痕，边缘无镀层色差。十倍镜可观察纹路；影响工艺价，可抛光修复。', True, False, False, 'loupe'),
    'metal_split': ('结构裂纹', '典型特征：结构部位的深色断线，伴随错开的亮边。它与表面磨损是两个原因，可分别议价；工坊可焊补。', True, False, False, ''),
    'metal_plating': ('镀层露底', '典型特征：边缘局部露出红铜或暗灰底材。外层颜色不代表整体成色；使用成色分析仪核实。同一材质问题的多处证据只扣一次。', False, True, False, 'assay'),
    'jade_crack': ('贯穿裂隙', '典型特征：呈分叉的深色裂隙，开窗后可见同一裂隙延伸。两处线索属于同一原因，不能重复压价。', False, False, False, 'loupe'),
    'jade_inclusion': ('团状棉絮', '典型特征：石料中成团的浅色棉絮；不能把每个亮点都当成裂纹。透玉灯只辅助观察，完整产量须切割后确认。', False, False, False, 'jade_light'),
    'jade_dye': ('裂隙染色聚集', '典型特征：裂隙内颜色异常浓集，紫外检测可记录相应异常。无荧光也不等于天然真品；结合纹理与证书判断。', False, False, True, 'uv'),
}

def hot(template, pos, deduction=0, view='front'):
    label, guidance, repair, material, authenticity, tool = templates[template]
    key = 'template.'+template
    text(key+'.label', label); text(key+'.guide', guidance)
    return dict(Id=template+('_window' if view=='window' else ''), CauseId=template,
                Label=label, LabelKey=key+'.label', TemplateId=template, Guidance=guidance,
                GuidanceKey=key+'.guide', InstrumentKind=tool, ViewId=view,
                U=pos[0]/384, V=pos[1]/256, Width=pos[2]/384, Height=pos[3]/256,
                ValueDeduction=deduction, Repairable=repair, RevealsMaterial=material, RevealsAuthenticity=authenticity)

legacy_guidance = {
    'surface_wear':'浅色平行划痕位于手镯弧面。与接缝刮痕属于同一表面损伤原因，可修复，只计一次价差。',
    'surface_seam':'接缝附近的细划痕属于表面损伤；同件物品上重复发现不会重复扣价。',
    'clasp_break':'搭扣处的深色断口与错开的亮边表示结构损伤。工坊可修复，不能当作材质证据。',
    'plating_edge':'边缘露出与金色不同的底材；这是镀层失效的常见特征，用成色分析仪可核实贵金属含量。',
    'plating_seam':'接缝露出铜色底材。与边缘露底属于同一材质原因，两处不能重复压价。',
    'bar_seam':'封边露出异色内芯，是夹心条的材料线索。不能仅根据正面印记推断整块含金量。',
    'bar_stamp':'压印局部边缘不规则。与封边线索一起提示内芯异常，需要材料核验。',
    'cup_dent':'杯壁反光带在局部凹陷处折断；此类外形损伤可修复，不改变银的回收底价。',
    'silver_clasp':'银镯搭扣裂损形成固定深色断线，工坊可焊补。',
    'silver_bar_seam':'银条封边露出铜色内芯；银色外观和含银量是不同事实。',
    'rough_crack':'皮壳上的分叉裂隙可延伸进入开窗。同一裂隙在两个视图出现时，只计一个原因。',
    'window_crack':'窗口中的裂纹与皮壳裂隙相连；它是同一物理损伤的另一条证据。',
    'dye_pool':'裂隙中的颜色比周围浓集，构成染色处理线索；结合紫外观察和检验报告判断。',
    'vase_restore':'釉面修补边界在光照下呈规则接缝，是永久修复历史，不等同于当前可修损伤。',
    'seal_mismatch':'印章笔画错位与标准印样不符；结合笔触印点判断同一真伪原因。',
    'print_dots':'规则排列的印点代替自然笔触，十倍镜可记录；多个真伪线索只改变一次真伪估值。',
    'console_port':'接口触片弯折而非整体阴影变化，可由工坊修复。',
    'console_screw':'螺丝槽口拆修痕迹是既往拆机证据，修好接口也不会抹去这段历史。',
    'handheld_port':'掌机侧面接口触片弯折，影响连接功能；修复后仍保留原资产身份。',
    'label_print':'标签套色偏移符合非授权重制版特征；同一作品有不同版本，不能把作品名当真品证明。',
    'disc_scratch':'读取面径向划伤切断反光纹理，工坊抛光可改善；同一划伤不重复计价。',
    'reprint_mark':'版权页明确的重印标记区分初版和重印版。书的内容相同，版本价值仍可能不同。',
}
families = {
    'gold_bracelet': ['gold_bracelet','gold_bracelet_worn','gold_bracelet_plated'],
    'gold_bar':['gold_bar','gold_bar_suspect'], 'silver_cup':['silver_cup','silver_cup_worn'],
    'silver_bracelet':['silver_bracelet','silver_bracelet_worn'], 'silver_bar':['silver_bar','silver_bar_suspect'],
    'jade_rough':['jade_rough','jade_rough_cracked'],'jade_pendant':['jade_pendant','jade_pendant_dyed'],
    'antique_vase':['antique_vase','antique_vase_repaired'],'painting':['painting','painting_forged'],
    'console':['console','console_damaged'],'handheld':['handheld','handheld_damaged'],
    'cartridge':['cartridge','cartridge_bootleg'],'disc':['disc','disc_scratched'],'book':['book','book_reprint'],
}
for d in items:
    for family, ids in families.items():
        if d['Id'] in ids: d['FamilyId']='legacy_'+family
    for h in d['Hotspots']:
        h['TemplateId']='legacy_'+h['Id']
        h['Guidance']=legacy_guidance[h['Id']]
        h['GuidanceKey']='template.'+h['TemplateId']+'.guide'
        h['InstrumentKind']='assay' if h['RevealsMaterial'] else 'uv' if h['Id'] in ['dye_pool','vase_restore'] else 'loupe' if h['Id'] in ['print_dots','label_print','reprint_mark','console_screw'] else ''
        text(h['GuidanceKey'],h['Guidance'])

if args.development_art:
    from PIL import Image, ImageDraw, ImageChops
    paper = ROOT/'mod/Textures/Paper'; native = ROOT/'mod/Textures/Items'; masks=ROOT/'art/development/masks'
    masks.mkdir(parents=True,exist_ok=True)

def shape(form, style=0):
    """Authored geometry, shared by clean, repaired and defective profiles."""
    mask=Image.new('L',(384,256)); draw=ImageDraw.Draw(mask)
    if form in ('ring','bracelet','necklace','earrings','bangle'):
        bounds={'ring':[(127,62,255,200),(149,85,233,179)],'bracelet':[(88,60,296,208),(110,82,274,186)],'bangle':[(88,60,296,208),(114,86,270,182)],'necklace':[(71,36,313,195),(89,54,295,177)]}
        if form=='earrings':
            for b in [(99,70,171,199),(213,70,285,199)]:
                draw.ellipse(b,fill=255);draw.ellipse((b[0]+14,b[1]+16,b[2]-14,b[3]-19),fill=0)
            draw.rectangle((128,43,141,77),fill=255);draw.rectangle((242,43,255,77),fill=255)
        else:
            outer,inner=bounds[form];draw.ellipse(outer,fill=255);draw.ellipse(inner,fill=0)
            if form=='necklace':draw.polygon([(192,152),(217,189),(192,226),(167,189)],fill=255)
        if style and form=='ring':draw.polygon([(192,40),(218,60),(192,84),(166,60)],fill=255)
    elif form=='bar':draw.polygon([(88,78),(270,52),(310,170),(128,206)],fill=255)
    elif form=='pendant':draw.rounded_rectangle((132,62,252,213),radius=40,fill=255);draw.ellipse((175,40,209,82),fill=255);draw.ellipse((185,51,199,69),fill=0)
    elif form=='cabochon':draw.ellipse((111,71,273,205),fill=255)
    elif form=='beads':
        for i in range(12):
            a=i*math.pi/6;x=192+93*math.cos(a);y=133+67*math.sin(a);draw.ellipse((x-16,y-16,x+16,y+16),fill=255)
    else:
        points=[(74,146),(85,79),(143,42),(238,48),(303,102),(307,172),(255,216),(132,211)]
        # Each skin has a fixed outline; no pixel noise or per-instance random texture.
        points=[(x+(style%3-1)*((i%2)*8),y+(style%2)*((i%3)*4)) for i,(x,y) in enumerate(points)]
        draw.polygon(points,fill=255)
    return mask

positions={'ring':[(139,130,22,22),(243,128,20,27),(192,190,30,16)],
           'bracelet':[(101,128,20,26),(283,133,20,28),(192,198,34,16)],
           'necklace':[(82,113,20,26),(302,112,18,26),(192,200,28,24)],
           'earrings':[(110,131,18,26),(273,132,18,26),(135,190,25,17)],
           'bar':[(143,105,32,22),(260,150,28,30),(196,180,36,22)]}

def artwork(key, form, material, style, hotspots, repaired=False, window=False):
    if not args.development_art:return
    mask=shape(form,style);image=Image.new('RGBA',(384,256));draw=ImageDraw.Draw(image)
    palette={'gold':('#ad7132','#e6b955','#fff0ab'),'silver':('#7c8d9e','#c6d4d5','#ffffff'),
             'jade':('#134b45','#448a70','#c3e2ae'),'rough0':('#80613d','#ae9164','#dbc99b'),
             'rough1':('#212e35','#45525b','#758b8d'),'rough2':('#536b66','#839c90','#bacbc0'),
             'rough3':('#9a9989','#c7c1a8','#f1e5bf'),'rough4':('#6a3b2f','#ac6842','#d69b69'),
             'rough5':('#495453','#758575','#adb99b')}
    dark,mid,light=palette[material]
    draw.rectangle((0,0,383,255),fill=mid)
    draw.polygon([(0,0),(384,0),(384,77),(0,178)],fill=light)
    draw.polygon([(0,195),(384,113),(384,256),(0,256)],fill=dark)
    # Engraving is authored and shared, not a defect. Keep it away from evidence slots.
    if style and material in ('gold','silver'):
        for x,y in [(174,65),(187,62),(200,60),(211,58)]:draw.line((x,y,x+4,y+4),fill=dark,width=2)
    if form=='bar':
        draw.polygon([(160,102),(198,96),(210,124),(172,131)],outline=dark,width=3)
        draw.line((178,145,236,131),fill=light,width=2)
    if material.startswith('rough'):
        for points in [[(107,82),(137,69),(166,76)],[(265,99),(281,122),(272,139)],[(125,173),(155,189),(176,183)]]:draw.line(points,fill=dark,width=4)
    if window:
        draw.ellipse((144,92,239,183),fill='#41765d',outline='#cad2aa',width=5)
        draw.line([(164,127),(177,145),(210,137)],fill='#96bf89',width=4)
    image.putalpha(mask)
    for h in hotspots:
        if repaired and h['Repairable']:continue
        if (h['ViewId']=='window')!=window:continue
        x=round(h['U']*384);y=round(h['V']*256);w=round(h['Width']*384);hh=round(h['Height']*256)
        overlay=Image.new('RGBA',(384,256));o=ImageDraw.Draw(overlay);kind=h['TemplateId']
        if kind=='metal_wear':
            for dy in [-4,1,5]:o.line((x-w//3,y+dy,x+w//3,y+dy-3),fill='#674725',width=2)
        elif kind in ('metal_split','jade_crack'):
            p=[(x-2,y-hh//2+2),(x+3,y-3),(x-4,y+3),(x+1,y+hh//2-2)]
            o.line(p,fill='#152028',width=3);o.line([(px+3,py) for px,py in p],fill='#dfdbba',width=1)
        elif kind=='metal_plating':
            o.polygon([(x-w//2+2,y-2),(x-w//3,y-hh//2+2),(x+w//3,y-hh//3),(x+w//2-2,y+2),(x,y+hh//2-2)],fill='#9b583d',outline='#4c342e')
        elif kind=='jade_dye':
            o.line([(x-w//3,y-hh//2),(x+3,y-2),(x-4,y+hh//2)],fill='#0b3331',width=5)
            o.line([(x-5,y-2),(x+9,y+3)],fill='#1cd69d',width=3)
        else:
            for dx,dy in [(-4,-3),(3,1),(0,6)]:o.ellipse((x+dx-4,y+dy-3,x+dx+4,y+dy+3),fill='#ced9b8')
        overlay.putalpha(ImageChops.multiply(overlay.getchannel('A'),mask))
        image=Image.alpha_composite(image,overlay)
    image.save(paper/(key+'.png'));image.resize((96,64),Image.Resampling.NEAREST).save(native/(key+'.png'))
    mask.save(masks/(re.sub(r'_p[0-9]+(?:_window)?$','',key)+'.png'))

forms=[('ring','戒指',6000,155),('bracelet','手镯',22000,300),('necklace','项链',14000,280),('earrings','耳环',8000,210),('bar','金条',50000,35)]
for metal, material_name in [('gold','金'),('silver','银')]:
    for form, form_name, mg, craft in forms:
        for style, style_name in enumerate(['环轨','星芒']):
            family=f'sp3_{metal}_{form}_{style}'
            for bits in range(8):
                ident=family+f'_p{bits}';name=style_name+('足金' if metal=='gold' else '纯银')+(form_name if metal=='gold' or form!='bar' else '银条')
                d=base(ident,name,'黄金' if metal=='gold' else '白银',0)
                d.update(FamilyId=family,Form=form,ManualCategory='metals',Material=metal,WeightMilligrams=mg if metal=='gold' else mg*2,
                         DeclaredPurityBasisPoints=9990 if form=='bar' or metal=='gold' else 9250,
                         ActualPurityBasisPoints=0 if bits&4 else 9990 if form=='bar' or metal=='gold' else 9250,
                         CraftValue=craft+(style*80),ActualBaseValue=20,RepairCost=45+style*15,IsGenuine=not bool(bits&4))
                for bit,template,pos,deduction in [(1,'metal_wear',positions[form][0],craft//4),(2,'metal_split',positions[form][1],craft//2),(4,'metal_plating',positions[form][2],0)]:
                    if bits&bit:d['Hotspots'].append(hot(template,pos,deduction))
                d['RepairedTextureKey']='starpawn:'+family+f'_p{bits&4}'
                items.append(d);artwork(ident,form,metal,style,d['Hotspots'])
                geometry.append({'id':ident,'family':family,'profile':bits,'form':form,'texture':d['TextureKey'],'hotspots':d['Hotspots']})

outforms=[('pendant','挂件',320),('cabochon','蛋面',500),('bangle','玉镯',680),('beads','珠串',410)]
for form,name,value in outforms:
    for profile in range(3):
        ident=f'sp3_jade_{form}_p{profile}'
        d=base(ident,'青岚翡翠'+name,'翡翠成品',value)
        d.update(FamilyId=f'sp3_jade_{form}',Form=form,ManualCategory='jade',WeightMilligrams=26000)
        pos=(192,196,28,18) if form in ('bangle','beads') else (192,141,30,35)
        if profile==1:d['Hotspots']=[hot('jade_dye',pos)];d['IsGenuine']=False;d['ActualBaseValue']=value//4
        if profile==2:d['Hotspots']=[hot('jade_crack',pos,value//3)]
        items.append(d);artwork(ident,form,'jade',0,d['Hotspots'])
        geometry.append({'id':ident,'family':d['FamilyId'],'form':form,'texture':d['TextureKey'],'hotspots':d['Hotspots']})

skins=[('yellow','黄沙皮',330,(5,4,1)),('black','黑乌砂',580,(4,3,3)),('water','水石',430,(3,5,2)),('white','白盐砂',680,(2,5,3)),('rust','铁锈皮',390,(5,3,2)),('mountain','山料',250,(6,3,1))]
for skin_index,(skin,name,value,weights) in enumerate(skins):
    for profile in range(4):
        ident=f'sp3_rough_{skin}_p{profile}';d=base(ident,name+'翡翠原石','翡翠原石',value)
        d.update(FamilyId='sp3_rough_'+skin,Form='rough',ManualCategory='jade',WeightMilligrams=800000+skin_index*175000,
                 IsJadeRough=True,WindowCost=35+skin_index*5,CutCost=80+skin_index*8,
                 WindowTextureKey='starpawn:'+ident+'_window',JadeLowWeight=weights[0],JadeMediumWeight=weights[1],JadeHighWeight=weights[2],
                 JadePendantOutput='sp3_jade_pendant_p0',JadeCabochonOutput='sp3_jade_cabochon_p0',JadeBangleOutput='sp3_jade_bangle_p0',JadeBeadsOutput='sp3_jade_beads_p0')
        if profile&1:d['Hotspots'] += [hot('jade_crack',(228,131,32,48),value//4),hot('jade_crack',(198,139,28,40),value//4,'window')]
        if profile&2:d['Hotspots'] += [hot('jade_inclusion',(164,151,28,26),value//6,'window')]
        items.append(d);artwork(ident,'rough','rough'+str(skin_index),skin_index,d['Hotspots']);artwork(ident+'_window','rough','rough'+str(skin_index),skin_index,d['Hotspots'],window=True)
        geometry.append({'id':ident,'family':d['FamilyId'],'form':'rough','texture':d['TextureKey'],'window':d['WindowTextureKey'],'hotspots':d['Hotspots']})

def equipment_art(ident,kind,color='#516c7f',index=0):
    if not args.development_art:return
    im=Image.new('RGBA',(384,256));o=ImageDraw.Draw(im)
    if kind=='manual':
        o.polygon([(112,65),(241,46),(274,187),(143,210)],fill=color,outline='#d7c59a',width=3)
        o.polygon([(143,210),(274,187),(274,202),(143,226)],fill='#e6d8ac',outline='#394f5c')
        o.line((131,66,161,200),fill='#273d4a',width=5)
        o.rectangle((160,90,223,142),outline='#e8d5a6',width=4)
        for n in range(index+1):o.line((172+n*7,108,172+n*7,127),fill='#e8d5a6',width=3)
    elif kind=='organizer':
        o.rounded_rectangle((77,67,308,213),radius=7,fill='#4a535b',outline='#b4b8ac',width=4)
        o.rectangle((94,83,291,192),fill='#18292f')
        for n in range(7):o.rectangle((103+n*26,97,120+n*26,181),fill=['#8c5850','#547c72','#b89b65','#637b9a','#806a91','#6b9b9a','#a48051'][n],outline='#d5caab',width=2)
    elif kind=='loupe':
        o.line((214,146,275,207),fill='#617f89',width=22);o.line((216,146,272,202),fill='#c4d5d2',width=6)
        o.ellipse((100,49,244,190),fill='#38505e',outline='#b2d3d2',width=9);o.ellipse((120,68,225,173),fill='#78a4a9',outline='#e0e9cb',width=4)
        o.arc((129,79,216,164),180,280,fill='#e9f6d1',width=5)
    elif kind in ('uv','jade_light'):
        o.polygon([(137,88),(173,67),(259,164),(225,192)],fill='#526773',outline='#aec3bf',width=4)
        o.polygon([(123,69),(157,45),(184,77),(148,104)],fill='#8e7aac' if kind=='uv' else '#bccfa2',outline='#e8e7c2',width=4)
        o.line((172,100,226,160),fill='#233b48',width=7)
    else:
        o.rounded_rectangle((88,81,296,207),radius=15,fill='#455c6c',outline='#adbbb8',width=4)
        o.rectangle((112,145,220,183),fill='#1c3c3e',outline='#acc9bb',width=3)
        for n in range(4):o.line((126+n*21,158,137+n*21,158),fill='#8fe0a8',width=3)
        o.ellipse((239,150,272,181),fill='#c89759',outline='#e2c490',width=3)
        if kind=='scale':o.ellipse((104,38,279,123),fill='#a9b8ba',outline='#e1e2ce',width=5)
        else:o.polygon([(130,81),(143,39),(233,39),(255,81)],fill='#869b95',outline='#d9dbc1',width=4)
    im.save(paper/(ident+'.png'));im.resize((96,64),Image.Resampling.NEAREST).save(native/(ident+'.png'))

tools=[('scale','精密电子秤',120,'称量贵金属和石料净重；重量不代表成色。'),('assay','贵金属成色分析仪',650,'读取金银含量，并将实测成色用于当天材料估价。'),('loupe','十倍珠宝镜',90,'观察划痕、印点、接口和微小标记；记录匹配的固定线索。'),('jade_light','强光透玉灯',180,'观察皮壳透光与开窗棉絮；不能直接读出产量。'),('uv','紫外检测灯',260,'辅助识别染色、修补与异常荧光；无荧光不保证真品。')]
for kind,name,value,desc in tools:
    ident='sp3_tool_'+kind;d=base(ident,name,'鉴定仪器',value)
    d.update(InstrumentKind=kind,ShopOnly=True,Description=desc,WidthCells=2 if kind in ('scale','assay') else 1)
    items.append(d);equipment_art(ident,kind)

manuals=[('metals','贵金属首饰','先对照款式和克重，再查表面损伤与材质线索。划伤、结构裂纹、镀层露底是不同瑕疵，可能同时出现。金银报价共用每天的卖出价和回收价；真实材料回收值是底线。成色分析仪提供含量，十倍镜提供微观证据。'),
         ('jade','翡翠与赌石','黄沙皮、黑乌砂、水石、白盐砂、铁锈皮和山料有不同的货价与出料分布，皮壳名称不保证内质。先照灯，再决定是否付费开窗或切割。裂隙、棉絮与染色使用典型图样。同一裂隙贯穿皮壳和窗口时只计一次；手镯需要足够大的料。'),
         ('antiques','古董陶器','观察器形、釉面与修补边界。修复历史不会因为再次修补而消失。用紫外灯辅助定位补釉区域，再对照图鉴；证书只对列明范围负责。'),
         ('art','字画印鉴','先比较印章标准笔画，再检查画面笔触。规则印点和错位印章都属于真伪证据，同一原因不重复压价；十倍镜有助于辨认细小印点。'),
         ('electronics','主机与掌机','先看接口触片，再看螺丝槽口。功能部件损伤可修复，拆修历史会留下记录。同一型号可以组合不同的常见瑕疵，不能把所有反光都当作异常。'),
         ('media','卡带与光盘','核对作品、版本和标签，观察光盘读取面。套色偏移是版本线索，径向划伤是可修复品相问题。作品相同并不意味着发行版本相同。'),
         ('books','书籍版本','核对版权页、印次和版本标记。内容相同的书仍可能有不同收藏价值。每件实物都有独立身份，证书与修复历史跟随该件物品。')]
for index,(category,title,reading) in enumerate(manuals):
    ident='sp3_manual_'+category;d=base(ident,'《'+title+'鉴定手册》','鉴定手册',65)
    d.update(IsBook=True,ManualFor=category,ShopOnly=True,WorkId='starpawn-manual-'+category,EditionId='2247 图鉴版',ReadingKey='content.'+ident+'.reading',
             ReadingText=reading+'\n\n翻页查看样本中圈出的异常，结合仪器读数与实物细节判断。',Description='分类鉴定图鉴；含外观样本、检测方法与议价依据。')
    items.append(d);equipment_art(ident,'manual',['#8c5850','#547c72','#b89b65','#637b9a','#806a91','#6b9b9a','#a48051'][index],index)
d=base('sp3_manual_organizer','鉴定手册收纳','书册收纳',160)
d.update(IsOrganizer=True,ShopOnly=True,WidthCells=2,HeightCells=2,Description='八格书匣，集中收纳常用鉴定手册。')
items.append(d);equipment_art(d['Id'],'organizer')

for d in items:
    text(d['NameKey'],d['Name']);text(d['DescriptionKey'],d['Description'])
    if d.get('ReadingKey'):text(d['ReadingKey'],d['ReadingText'])
    for h in d['Hotspots']:text(h['LabelKey'],h['Label'])
for file in (ROOT/'source').rglob('*.cs'):
    for literal in re.findall(r'"(?:[^"\\]|\\.)*"',file.read_text(encoding='utf-8-sig')):
        if not re.search(r'[\u4e00-\u9fff]',literal):continue
        try:value=json.loads(literal)
        except ValueError:continue
        if not any(e['Source']==value for e in texts.values()):text('ui03.'+hashlib.sha256(value.encode()).hexdigest()[:16],value)
pack['Items']=items;pack_path.write_text(json.dumps(pack,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
localization['Entries']=list(texts.values());loc_path.write_text(json.dumps(localization,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(ROOT/'art/development').mkdir(exist_ok=True)
(ROOT/'art/development/geometry-contract.json').write_text(json.dumps(geometry,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'Catalog: {len(items)} definitions, {len(geometry)} new profiles, {len(texts)} localization keys; development art={args.development_art}')
