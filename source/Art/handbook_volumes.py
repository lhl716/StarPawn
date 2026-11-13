"""Physical short volumes; old anthologies retain access through collection indexes."""
import copy

PREFIX = 'sp31_manual_'


def append_volumes(pack, texts):
    original = pack['Items']
    plans = []
    forms = [('ring', '戒指'), ('bracelet', '手镯'), ('necklace', '项链'),
             ('earrings', '耳环'), ('bar', '金条')]
    for material, label in [('gold', '黄金'), ('silver', '白银')]:
        for form, title in forms:
            if material == 'silver' and form == 'bar':
                title = '银条与器皿'
            families = ['sp3_%s_%s_%d' % (material, form, variant) for variant in (0, 1)]
            if form in ('bracelet', 'bar'):
                families.append('legacy_%s_%s' % (material, form))
            if material == 'silver' and form == 'bar':
                families.append('legacy_silver_cup')
            plans.append((material+'_'+form, 'metals', label+'鉴定·'+title, families))
    plans += [
        ('rough_sand', 'jade', '翡翠原石·沙皮与乌沙',
         ['sp3_rough_yellow', 'sp3_rough_black', 'legacy_jade_rough']),
        ('rough_water', 'jade', '翡翠原石·水石与白皮',
         ['sp3_rough_water', 'sp3_rough_white']),
        ('rough_mountain', 'jade', '翡翠原石·铁锈与山石',
         ['sp3_rough_rust', 'sp3_rough_mountain']),
        ('jade_pendant', 'jade', '翡翠成品·玉佩与蛋面',
         ['sp3_jade_pendant', 'sp3_jade_cabochon', 'legacy_jade_pendant']),
        ('jade_bangle', 'jade', '翡翠成品·手镯与珠串',
         ['sp3_jade_bangle', 'sp3_jade_beads']),
    ]
    rows, volumes = [], []
    for key, category, title, families in plans:
        source = next(d for d in original if d['Id'] == 'sp3_manual_'+category)
        book = copy.deepcopy(source)
        templates = sorted({h['TemplateId'] for d in original
                            if d.get('FamilyId', d['Id']) in families for h in d['Hotspots']})
        assert 0 < len(templates) <= 8, (key, templates)
        ident = PREFIX+key
        reading = ('本册只收录标题所列品类的固定特征。先比较左侧参考与右侧异常，再检查实物。'
                   '图例不代表待检物品的真实状态。\n\n')
        reading += ('称重与成色应使用对应仪器核验；划伤、断裂和镀层露底需要分别判断。'
                    if category == 'metals' else
                    '先查皮壳或表面，再结合开窗与透光观察。豆、糯、冰为三个品质层次，原石外观不能证明内部品质或产量。')
        book.update(Id=ident, Name='《'+title+'》', NameKey='content.'+ident+'.name',
                    Description='独立鉴定分册，含说明页共 %d 页；每页两组固定对照图例。' % (1+(len(templates)+1)//2),
                    DescriptionKey='content.'+ident+'.description', ReadingText=reading,
                    ReadingKey='content.'+ident+'.reading', BaseValue=25, ActualBaseValue=25,
                    WorkId='starpawn-handbook-'+key, EditionId='2247 分册版',
                    WidthCells=1, HeightCells=1, HandbookTemplates=templates,
                    HandbookFamilies=families)
        for field, text_field in [('NameKey', 'Name'), ('DescriptionKey', 'Description'), ('ReadingKey', 'ReadingText')]:
            texts[book[field]] = dict(Key=book[field], Source=book[text_field], Text=book[text_field])
        volumes.append(book)
        rows.append(dict(id=ident, category=category, title=title, templates=len(templates),
                         pages=1+(len(templates)+1)//2, families=families))
    pack['Items'].extend(volumes)
    return rows
