"""Export the frozen 0.4 ART DESIGN contract. Never changes runtime content or images."""
from pathlib import Path
import csv, json

ROOT = Path(__file__).resolve().parents[2]
rows = []

def add(key, name, category, width, height, source='', states='intact;category_defects;allowed_combinations;repaired', tier='none', role='appraisable', priority=2, note=''):
    subtype=category.removeprefix('jade_rough_') if category.startswith('jade_rough_') else ''
    rows.append(dict(asset_key=key, name_en=name, category='jade_rough' if subtype else category, defect_subtype=subtype, role=role,
        source_family_or_id=source, target_family_id='sp4_'+key,
        grid_width=width, grid_height=height, items_width=width*64, items_height=height*64,
        paper_width=384, paper_height=256, tier_policy=tier, required_states=states,
        output_folder='art/proposals/batch04/items/'+key, priority=priority,
        implementation_status='planned_04_not_registered', notes=note))

forms=[('ring','Ring',1,1),('bracelet','Bracelet',2,1),('necklace','Necklace',2,2),('earrings','Earring Pair',1,1),('bar','Bar',2,1)]
for metal in ('gold','silver'):
    for form,label,w,h in forms:
        for n,style in enumerate(('orbit','starburst')):
            add(f'{metal}_{form}_{style}', f'{style.title()} {metal.title()} {label}', f'{metal}_{form}',w,h,
                f'sp3_{metal}_{form}_{n}',priority=1 if metal=='gold' else 3,
                note='Two distinct designs per category; earrings are one paired item. Fixed footprint across every condition.')

for skin,label in [('yellow','Yellow Sand Skin'),('black','Black Sand Skin'),('water','Water-Worn'),('white','White Salt Skin'),('rust','Rust Skin'),('mountain','Mountain Rough')]:
    add('jade_rough_'+skin,label+' Jade Rough','jade_rough_'+skin,2,2,'sp3_rough_'+skin,
        'sealed;window_open;cut_surface;category_defects;allowed_combinations;light_observation;uv_observation',
        'hidden_dou_nuo_bing',priority=2,note='Whole exterior does not reveal grade. Grade changes only interior/observation layers. No quality rerolls.')

for form,label,w,h in [('pendant','Pendant',1,1),('cabochon','Cabochon',1,1),('bangle','Bangle',2,1),('beads','Bead Strand',2,2),('carving','Small Carving',2,2)]:
    for n,style in enumerate(('plain','crafted')):
        add(f'jade_{form}_{style}',f'{style.title()} Jade {label}','jade_'+form,w,h,
            'sp3_jade_'+form if n==0 and form!='carving' else '',
            'intact;category_defects;allowed_combinations;light_observation;uv_observation;repaired',
            'visible_dou_nuo_bing',priority=2,note='Exactly three game quality tiers; treatment/defects are separate from grade.')

# Loose stones and mounted goods have distinct truth, custody and template sets.
for family,shapes in [('diamond',('round','princess')),('ruby',('oval','cushion')),
                     ('sapphire',('oval','pear')),('emerald',('step','oval')),('pearl',('round','baroque'))]:
    for shape in shapes:
        add(f'gem_{family}_{shape}',f'{shape.title()} {family.title()}','gem_'+family,1,1,
            states='intact;category_defects;allowed_combinations;instrument_observation',priority=2,
            note='Separate stone AssetId; carat/mass or pearl diameter is UI data. Origin/treatment is saved, not inferred from color. No polishing-away inclusions or magical grade upgrade.')
for form,style,w,h in [('ring','solitaire',1,1),('pendant','bezel',1,1),('earrings','stud',1,1),
                      ('bracelet','trilogy',2,1),('necklace','drop',2,2)]:
    add(f'jewelry_{form}_{style}',f'{style.title()} Gem-Set {form.title()}','jewelry_'+form,w,h,
        states='intact;category_defects;allowed_combinations;repaired;stone_variants',priority=2,
        note='One fixed setting model with separate stone layers. JEWELRY_SETTINGS_04.csv fixes compatible families/shapes, socket count and render anchors. Au18K/Ag925 are material variants, not new size variants.')
    add('setting_'+form,'Empty '+form.title()+' Setting','setting_'+form,w,h,
        states='empty;service_worn;repaired',role='component',priority=3,
        note='Same geometry as its mounted model with stones removed. Preserve setting metal mass and mechanical history. An intentionally empty socket is not a missing-stone defect.')

legacy=[('gold_bracelet','Gold Bracelet','gold_bracelet'),('gold_bar','50 g Gold Bar','gold_bar'),
        ('silver_cup','Silver Cup','silver_cup'),('silver_bracelet','Silver Bracelet','silver_bracelet'),
        ('silver_bar','100 g Silver Bar','silver_bar'),('jade_rough','Jade Rough','jade_rough_yellow'),
        ('jade_pendant','Jade Pendant','jade_pendant'),('antique_vase','Antique Vase','antique_vase'),
        ('painting','Painting','painting'),('console','Console','console'),('handheld','Handheld','handheld'),
        ('cartridge','Cartridge','cartridge'),('disc','Disc','disc'),('book','Signed Book','book')]
for key,label,category in legacy:
    add('legacy_'+key,'Legacy '+label,category,1,1,'legacy_'+key,
        'existing_runtime_states_only',role='legacy_appraisable',priority=4,
        note='Compatibility rework: preserve old 1x1 footprint and existing condition semantics. Do not add new defects to saved definitions.')
    rows[-1]['target_family_id']='legacy_'+key

for key,label,category,w,h in [('vase','Antique Vase','antique_vase',2,2),('painting','Painting','painting',2,2),
    ('console','Console','console',2,2),('handheld','Handheld','handheld',2,1),('cartridge','Cartridge','cartridge',1,1),
    ('disc','Disc','disc',1,1),('book','Signed Book','book',1,1),('silver_cup','Silver Cup','silver_cup',1,2)]:
    add('collectible_'+key,label,category,w,h,priority=4,
        note='New-generation physical definition; electronics have authored hidden component/material budgets, not visible free gold.')

for kind,label,w,h in [('scale','Digital Scale',2,1),('assay','Purity Analyzer',2,2),('loupe','10x Loupe',1,1),
                       ('jade_light','Jade Inspection Light',1,2),('uv','UV Lamp',1,2),('gem_probe','Gem Screening Probe',2,1)]:
    add('tool_'+kind,label,'tool_'+kind,w,h,'sp3_tool_'+kind if kind!='gem_probe' else '',
        'idle;active;reading_ready;unavailable',role='instrument',priority=3)

for category,label in [('metals','Precious Metals'),('jade','Jade'),('antiques','Antiques'),('art','Art'),
                      ('electronics','Electronics'),('media','Media'),('books','Books'),('recovery','Recovery and Refining'),('jewelry','Gemstones and Jewelry')]:
    add('manual_'+category,label+' Handbook','manual',1,1,'sp3_manual_'+category if category not in ('recovery','jewelry') else '',
        'closed;open_page_template',role='manual',priority=3,
        note='Native-style portrait illustrated reference sheets; follow VANILLA_HANDBOOK_SPEC_04.txt. Nine category books use the larger organizer; legacy eight-slot container retains its capacity.')
add('manual_index','Legacy Appraisal Guide','manual',1,1,'inspection_guide','closed;open_page_template',role='manual',priority=4)
add('manual_organizer','Eight-Slot Handbook Organizer','organizer',2,2,'sp3_manual_organizer',
    'empty;partly_filled;full;open',role='container',priority=3,note='Native internal grid is 4x2; accepts manuals only. Outer footprint is 2x2. Decorative books must not imply duplicated real children.')
add('manual_organizer_large','Twelve-Slot Handbook Organizer','organizer',2,2,
    states='empty;partly_filled;full;open',role='container',priority=3,
    note='New item; native internal grid 4x3. Holds nine category manuals plus legacy guide without changing the old eight-slot organizer. No nested containers.')
for kind in ('savings','credit'):
    add(kind+'_card',kind.title()+' Card','card',2,1,'starpawn.'+kind+'_card',
        'front_valid;back;front_void',role='credential',priority=3,note='Fictional issuer; dynamic serial, account, limits and balances are UI text.')
add('card_holder','Card Holder','card_holder',2,1,states='empty;partly_filled;full;open',role='container',priority=4,
    note='Planned native internal 4x1 grid: two 2x1 cards. Never duplicates credentials or refreshes limits.')

for key,label,category,w,h in [
    ('scrap_board','Circuit Board','scrap_board',2,1),('scrap_processor','Processor Module','scrap_processor',1,1),
    ('scrap_connectors','Connector Lot','scrap_connectors',1,1),('mixed_e_scrap','Mixed Electronic Scrap','scrap_board',2,2),
    ('ore_gold','Gold-Bearing Ore','ore_gold',2,1),('ore_silver','Silver-Bearing Ore','ore_silver',2,1),('ore_copper','Copper-Bearing Ore','ore_copper',2,1),
    ('refined_gold_bar','Recovered Gold Bar','refined_gold_bar',2,1),('refined_silver_bar','Recovered Silver Bar','refined_silver_bar',2,1),
    ('refined_copper_ingot','Recovered Copper Ingot','refined_copper_ingot',2,1),
    ('alloy_gold18k','18K Gold Alloy Bar','alloy_gold_bar',2,1),('alloy_silver925','925 Silver Alloy Bar','alloy_silver_bar',2,1)]:
    add(key,label,category,w,h,priority=5,note='Source/provenance does not determine visible purity. Recover only elements already present; each material budget persists.')
    if key=='mixed_e_scrap':
        rows[-1].update(role='material_lot',required_states='sealed;assayed;sorted',category='mixed_e_scrap')

for key,label,w,h,states,role in [
    ('sorted_gold_scrap','Sorted Gold-Bearing Scrap',1,1,'sealed;assayed','material_lot'),
    ('sorted_silver_scrap','Sorted Silver-Bearing Scrap',1,1,'sealed;assayed','material_lot'),
    ('sorted_copper_scrap','Sorted Copper Scrap',1,1,'sealed;assayed','material_lot'),
    ('recovered_gold_lot','Measured Gold Granules',1,1,'sealed;measured','material_lot'),
    ('recovered_silver_lot','Measured Silver Granules',1,1,'sealed;measured','material_lot'),
    ('recovered_copper_lot','Measured Copper Granules',1,1,'sealed;measured','material_lot'),
    ('assay_sample','Sealed Assay Sample',1,1,'sealed;tested','process_item'),
    ('recovery_batch','Sealed Recovery Batch',2,1,'queued;in_process;ready;interrupted','process_item'),
    ('inert_residue','Recovery Residue',2,1,'sealed','material_lot'),
    ('process_cartridge','Sealed Processing Cartridge',1,1,'sealed;spent','consumable'),
    ('energy_cell','Energy Cell',1,1,'charged;partly_used;empty','consumable'),
    ('crucible','Crucible',2,1,'empty;loaded;cooling','tool'),('ingot_mold','Ingot Mold',2,1,'empty;filled','tool'),
    ('recovery_station','Recovery Station',2,2,'idle;working;ready;interrupted;maintenance','machine'),
    ('refining_furnace','Refining Furnace',2,2,'idle;working;cooling;ready;interrupted;maintenance','machine'),
    ('materials_bin','Materials Bin',2,2,'empty;partly_filled;full;open','container'),
    ('financial_terminal','Financial Terminal',2,2,'idle;active;alert;unavailable','machine')]:
    add(key,label,key,w,h,states=states,role=role,priority=5,
        note='Functional states only; no random defect pool. Values and quantities are UI text. Processing is abstract game machinery.')
rows[-2]['notes']='Native internal grid planned as 4x4; material lots only. No nested bins, cards or process batches; same custody/provenance ledger as outer inventory.'

for key,label,w,h,states,role,note in [
    ('jeweler_bench','Jeweler Workbench',2,2,'idle;working;ready;blocked','machine','Shares station energy, capacity and job receipts; no in-place reroll.'),
    ('jeweler_toolkit','Jeweler Toolkit',2,1,'closed;in_use;maintenance','tool','Abstract repair/setting tools; durable required equipment, not free materials.'),
    ('gem_case','Gem Storage Case',2,2,'empty;partly_filled;full;open','container','4x4 internal cells; loose gems only; preserve individual stone IDs.'),
    ('presentation_case','Jewelry Presentation Case',2,2,'empty;partly_filled;full;open','container','4x2 internal cells for assembled jewelry. A set links actual child assets; packaging creates no copies or automatic material premium.'),
    ('jewelry_display','Jewelry Display Tray',2,2,'empty;partly_filled;full;reserved;open','container','4x4 internal cells for saleable gems/jewelry; customer property and escrow are excluded. No nested containers.'),
    ('jewelry_certificate','Jewelry Identification Report',1,1,'valid;superseded;unverified','certificate','Dynamic report identity and observations; portraits follow native document style. Document does not reveal untested truth or add collateral.'),
    ('jewelry_job_ticket','Jewelry Job Ticket',1,1,'accepted;in_progress;ready;delivered;cancelled','document','Physical optional view of one persisted order. Reprint preserves OrderId and cannot redeem twice.')]:
    add(key,label,key,w,h,states=states,role=role,priority=3,note=note)

def write_csv(path,data):
    with path.open('w',encoding='utf-8',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=list(data[0]));writer.writeheader();writer.writerows(data)

assert len({r['asset_key'] for r in rows})==len(rows)
write_csv(ROOT/'art/ITEM_LAYOUT_04.csv',rows)
runtime=json.loads((ROOT/'art/runtime-art-map.json').read_text(encoding='utf-8'))
by_source={r['source_family_or_id']:r for r in rows if r['source_family_or_id']}
coverage=[]
for d in runtime['items']:
    key=d['familyId'] or d['id'];r=by_source[key]
    coverage.append(dict(current_id=d['id'],current_family=key,art_asset_key=r['asset_key'],
        current_grid_width=d['gridCells'][0],current_grid_height=d['gridCells'][1],
        target_grid_width=r['grid_width'],target_grid_height=r['grid_height'],
        policy='preserve_legacy' if r['role']=='legacy_appraisable' else 'explicit_atomic_migration_or_new_04_item'))
for d in runtime['cards']:
    r=by_source[d['nativeId']]
    coverage.append(dict(current_id=d['nativeId'],current_family=d['nativeId'],art_asset_key=r['asset_key'],
        current_grid_width=d['gridCells'][0],current_grid_height=d['gridCells'][1],
        target_grid_width=r['grid_width'],target_grid_height=r['grid_height'],policy='preserve_account_and_serial'))
write_csv(ROOT/'art/RUNTIME_COVERAGE_04.csv',coverage)
ui=[]
def ui_add(key,kind,width,height,states,notes):
    ui.append(dict(asset_key=key,kind=kind,width=width,height=height,required_states=states,
        output_folder='art/proposals/batch04/ui/'+key,implementation_status='planned_04_skin_or_screen',notes=notes))
for key,name in [('hub','Business Hub'),('catalog','Visitor Catalog'),('inventory','Owned Inventory'),
    ('inspection','Inspection Desk'),('instruments','Instrument Readings'),('quote','Quote Confirmation'),
    ('asset_record','Asset Record'),('workshop','Repair and Jade Cutting'),('bank','Bank and Physical Cards'),
    ('credit','Credit and Loans'),('securities','Securities and Holdings'),('financial_overview','Financial Overview'),
    ('events','Event Board and Forecasts'),('settlement','Nightly Settlement'),('pawn','Pawn Contracts'),
    ('handbook','Handbook Reader'),('organizer','Handbook Organizer'),('supply_shop','Supply Shop'),
    ('recovery','Disassembly and Recovery'),('refining','Refining and Alloys'),
    ('jobs','Production Jobs and Energy'),('provenance','Material Provenance'),('procurement','Event Procurement Orders'),
    ('jewelry','Jewelry Buying and Showroom'),('setting','Demounting and Setting'),
    ('jewelry_orders','Jewelry Custom Orders'),('gem_lab','Gem Screening and Lab Reports')]:
    ui_add('screen_'+key,'screen_plate',440 if key=='handbook' else 600,616 if key=='handbook' else 400,'default;empty;blocked',
        name+'; text-free skin plus annotated 1920x1080 and 3840x2160 full-screen mockups. Preserve logical anchors; future screens marked planned.')
for key,w,h,states,note in [
    ('button_primary',120,28,'default;hover;pressed;disabled','Nine-slice; margins8,8,8,8.'),
    ('button_secondary',88,24,'default;hover;pressed;disabled','Nine-slice; margins6,6,6,6.'),
    ('button_icon',24,24,'default;hover;pressed;disabled','No baked label.'),
    ('category_tab',96,24,'default;selected;disabled','Nine-slice; margins6,6,6,6.'),
    ('catalog_row',520,42,'default;selected;disabled','Five rows per standard catalog; text and thumbnail drawn separately.'),
    ('event_card',520,72,'forecast;active;expired;resolved','Stable dynamic modifier/date/status fields; text-free.'),
    ('job_row',520,48,'queued;running;paused;ready;claimed','Input/output and energy fields are dynamic.'),
    ('tooltip',240,80,'default;warning;error','Nine-slice; margins8,8,8,8.'),
    ('dialog',360,200,'confirm;blocked;error','Nine-slice; margins12,12,12,12.'),
    ('progress_track',240,12,'default;paused;complete','Fill separate; no baked percentage.'),
    ('progress_fill',240,12,'default;paused;complete','Clipped by runtime; do not resize whole texture per value.'),
    ('evidence_marker',32,32,'discovered;selected;resolved','Transparent; preview circles overlay only; no markers on item art.'),
    ('inspection_frame',298,199,'default;locked','Frame plate must not reduce the existing clickable image rectangle.'),
    ('handbook_page',440,616,'introduction;defect_example;index','Native-style PORTRAIT appraisal sheet; see VANILLA_HANDBOOK_SPEC_04.txt and actual game references. Supersedes old 512x320 landscape proposal.'),
    ('certificate_page',440,616,'valid;superseded','Native-style portrait report; no baked serial, issuer account, purity or date. Physical paper item still uses its 384x256 inspection illustration.'),
    ('inventory_cell',64,64,'empty;hover;selected;blocked','Repeats per grid cell; item art retains its exact manifest canvas.'),
    ('native_bookbox',288,176,'empty;partly_filled;full','4x2 cells of64px; content origin16,32; right/bottom margins16px. Mockup until native integration.'),
    ('native_cardholder',288,112,'empty;partly_filled;full','4x1 cells of64px; content origin16,32; right/bottom margins16px. Two2x1 cards.'),
    ('native_materials_bin',288,304,'empty;partly_filled;full','4x4 cells of64px; content origin16,32; right/bottom margins16px. No nested bins.'),
    ('grade_badge',24,24,'dou;nuo;bing','Labels separate; icon shapes distinguish tiers without color alone.'),
    ('status_badge',24,24,'owned;consigned;reserved;consumed;unknown;confirmed','Knowledge and ownership are distinct statuses.'),
    ('native_bookbox_large',288,240,'empty;partly_filled;full','4x3 cells of64px; content origin16,32; margins16. New twelve-slot organizer only.'),
    ('native_gem_case',288,304,'empty;partly_filled;full','4x4 cells; content origin16,32; only loose gems.'),
    ('native_presentation_case',288,176,'empty;partly_filled;full','4x2 cells; content origin16,32; actual child items retained.'),
    ('native_jewelry_display',288,304,'empty;partly_filled;full;reserved','4x4 cells; content origin16,32; custody and stock limits remain visible.'),
    ('stone_socket',48,48,'empty;compatible;incompatible;reserved','Logical slot indicator only, not an extra stone item or embedded text.'),
    ('order_receipt',440,616,'accepted;ready;delivered;cancelled','Portrait native-paper document; dynamic owner, terms, asset IDs and dates.'),
    ('resource_icon',32,32,'gold;silver;copper;gem;energy;capacity;time','No numeric quantities.'),
    ('feedback_symbol',64,64,'neutral;impatient;withdrawn;satisfied','Small feedback symbol/gesture treatment, not a new NPC character roster.')]:
    ui_add(key,'component',w,h,states,note)
events_path=ROOT/'docs/EVENT_EFFECTS_04.csv'
if events_path.exists():
    events=list(csv.DictReader(events_path.open(encoding='utf-8-sig',newline='')))
    # One icon per event identity, not per effect row. The design table may have multiple channels.
    event_ids=sorted({r.get('event_id') or r.get('EventId') or r.get('id') for r in events})
    assert None not in event_ids
    for event_id in event_ids:
        ui_add('event_'+event_id,'event_icon',64,64,'default',
            'Event '+event_id+'; depict its cause. Percentages, dates and price directions are separate UI state.')
write_csv(ROOT/'art/UI_ASSETS_04.csv',ui)

# Freeze handbook ownership and pagination for every category-local template set.
def manual_for(category):
    if category.startswith(('gold_','silver_')):return 'manual_metals'
    if category.startswith('jade_') or category=='jade_rough':return 'manual_jade'
    if category.startswith(('gem_','jewelry_')):return 'manual_jewelry'
    if category.startswith(('ore_','refined_','alloy_')):return 'manual_recovery'
    if category.startswith('scrap_') or category in ('console','handheld'):return 'manual_electronics'
    return {'antique_vase':'manual_antiques','painting':'manual_art','cartridge':'manual_media',
            'disc':'manual_media','book':'manual_books'}[category]
with (ROOT/'docs/DEFECT_CATALOG_04.csv').open(encoding='utf-8-sig',newline='') as f:
    defect_rows=list(csv.DictReader(f))
sets={}
for defect in defect_rows:
    sets.setdefault((defect['category'],defect['subtype']),[]).append(defect['template_id'])
handbook=[];next_page={}
for (category,subtype),template_ids in sets.items():
    manual=manual_for(category);first=next_page.get(manual,1)
    example=next(r['asset_key'] for r in rows if r['role']=='appraisable' and
        r['category']==category and (r['defect_subtype'] or 'all_models')==subtype)
    assert len(template_ids)==5
    handbook.append(dict(manual_asset_key=manual,category=category,subtype=subtype,
        example_asset_key=example,template_ids='|'.join(template_ids),first_page=first,page_count=3,
        templates_per_page='2|2|1',page_width=440,page_height=616,
        style_spec='art/VANILLA_HANDBOOK_SPEC_04.txt'))
    next_page[manual]=first+3
write_csv(ROOT/'docs/HANDBOOK_INDEX_04.csv',handbook)
print(f'Exported {len(rows)} fixed physical-art rows, {len(coverage)} current runtime mappings, '
      f'{len(ui)} UI rows and {len(handbook)} handbook chapters. Runtime files untouched.')
