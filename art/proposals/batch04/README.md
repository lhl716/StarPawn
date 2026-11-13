# StarPawn batch 04 art proposal (contract revision 04.2)

Status: **proposal only**. Nothing here is registered or loaded by the game. No file under `mod/`, `source/` or the design contracts was changed, and this branch must not be merged into `main` by the art owner.

Everything is produced by the deterministic generator in `source/` (`python3 source/build_all.py`). `validation-results.txt` holds the last technical run.

## What is complete

- **135 / 135 physical art rows** of `art/ITEM_LAYOUT_04.csv`, each in `items/<asset_key>/` with `paper/` (384x256), `items/` (grid x 64 px), `layers/`, `hotspots.json` (centre-based), `combinations.json`, `manifest.json`, `comparison.png`, `README.txt` and `source/generate.py`.
- **250 / 250 category templates** from `docs/DEFECT_CATALOG_04.csv` drawn on every model of their category (two designs per metal and finished-jade category), with single-defect layers, whitelisted combinations and repaired composites.
- **Jewelry business**: ten loose gems, five mounted settings in Au18K and Ag925 with every stone listed in `docs/JEWELRY_SETTINGS_04.csv`, five empty settings, bench, toolkit, gem case, presentation case, display tray, report and job ticket. `items/jewelry_*/stone_bindings.json` maps each stone layer to its socket.
- **71 / 71 UI rows** of `art/UI_ASSETS_04.csv` in `ui/<asset_key>/`: text-free skins for every state, `spec.json` anchors, and for the 27 screens a 1920x1080 and a 3840x2160 annotated mockup.
- **Handbooks**: 160 portrait pages (440x616) in `items/manual_*/pages/`, indexed by `handbook-pages.json` (root, plus one per manual). Each of the 250 templates has one example cropped from the example item's real layers.
- **Review material** in `review/`: inventory and inspection contact sheets, component and screen sheets, and `native_handbook_comparison_2560x1440.png` beside the supplied native screenshot.
- The approved ring is preserved: `items/gold_ring_orbit` intact art is pixel-identical to `art/proposals/gold_ring_style_sample`. That sample's hotspot exporter was corrected to centre-based U/V; its PNGs are unchanged.

## Integration-only (art exists, game work needed)

- All 0.4 footprints, templates, observation views (`uv`, `light`), new containers, jewelry assemblies, events and screens are planned contracts. Every screen mockup is marked *awaiting runtime integration*.
- `__uv` / `__light` states and `*_observation` states are proposed instrument layers; the runtime today has only `front` and `window`.
- Legacy rows keep their 0.3 IDs but their hotspot centres moved with the new drawings; adopt texture and coordinates together.
- Chinese text on mockups and handbook pages is placeholder copy (headings come from a small glossary). Real text comes from the localization keys in `handbook-pages.json`.

## Not done / not verified

- Nothing was run in the game: filtering at the 298x199 frame, fonts, hit-testing, container grids and save migration are unverified.
- The art is generator-drawn. It is consistent and readable, but individual models have not had hand polish, and several small inventory defects are only a few pixels.
- No extra hand-authored instrument views beyond the uv/light layers and the reading-example diagrams for instrument-only templates.

## Specification points that need a decision

| Rows | Issue | What this batch did | Proposed resolution |
| --- | --- | --- | --- |
| `sp4_jewelry_pendant_missing_insert`, `sp4_jewelry_earrings_missing_right`, `sp4_jewelry_bracelet_missing_left`, `sp4_jewelry_necklace_missing_drop` | Catalog says `state_mask`, but the stone sits inside a closed bezel, so removing it does not change the silhouette. | Stone layer removed, seat shown, alpha identical. | Treat `state_mask` as permitted-not-required, or relabel these four `same_alpha`. |
| `jade_rough_*` sealed view | Each family has only two front-view templates (water has three), so the sealed exterior cannot offer two pairs plus a triple. | Sealed view gets the valid pair(s); full pair/triple sets exist on the window-open states. | Accept as the documented exception the rules allow. |
| `sp4_gem_pearl_drill_chip` on `gem_pearl_baroque` | Catalog says an undrilled profile excludes this template. | Both pearl models are drawn drilled so all five templates apply. | Confirm, or declare baroque undrilled and drop the template from that model. |
| `jade_pendant/cabochon/bangle/beads/carving`, all `gem_*` rows (`required_states` lists `repaired` for jade) | No template in these sets has a repair class. | No repaired composite; `combinations.json` maps `repaired_state` to null. | Remove `repaired` from those rows or add a repairable template. |
| Templates with `instrument_kind` uv / jade_light and `mask_policy=same_alpha` | The cue is 'visible evidence' but also instrument-gated. | Ordinary view shows a restrained cue; the strong response is in the planned `__uv` / `__light` layer. | Confirm, or switch the ordinary view to unchanged. |
| `screen_handbook` at 1080p | A 440x616 sheet cannot be integer-scaled above x1 in 1080 px height. | 1080p mockup uses x1.5 nearest (uneven pixels, stated on the mockup); 4K uses x3; the 1440p comparison uses x2. | Pick x1 or x1.5 for 1080p at integration. |
| Weld/reshape repairs | Rules mention a documented seam after repair. | Repaired composites simply remove the repaired cause; the category's separate service-history template is not added automatically. | Gameplay owner decides whether a weld repair should add the category's join template. |

## Physical rows

| Asset | Role | Cells | States | Hotspots | Multi-defect profiles | Sheet |
| --- | --- | --- | --- | --- | --- | --- |
| [gold_ring_orbit](items/gold_ring_orbit/README.txt) | appraisable | 1x1 | 14 | 28 | 4 | [sheet](items/gold_ring_orbit/comparison.png) |
| [gold_ring_starburst](items/gold_ring_starburst/README.txt) | appraisable | 1x1 | 14 | 28 | 4 | [sheet](items/gold_ring_starburst/comparison.png) |
| [gold_bracelet_orbit](items/gold_bracelet_orbit/README.txt) | appraisable | 2x1 | 14 | 28 | 4 | [sheet](items/gold_bracelet_orbit/comparison.png) |
| [gold_bracelet_starburst](items/gold_bracelet_starburst/README.txt) | appraisable | 2x1 | 14 | 28 | 4 | [sheet](items/gold_bracelet_starburst/comparison.png) |
| [gold_necklace_orbit](items/gold_necklace_orbit/README.txt) | appraisable | 2x2 | 14 | 28 | 4 | [sheet](items/gold_necklace_orbit/comparison.png) |
| [gold_necklace_starburst](items/gold_necklace_starburst/README.txt) | appraisable | 2x2 | 14 | 28 | 4 | [sheet](items/gold_necklace_starburst/comparison.png) |
| [gold_earrings_orbit](items/gold_earrings_orbit/README.txt) | appraisable | 1x1 | 14 | 28 | 4 | [sheet](items/gold_earrings_orbit/comparison.png) |
| [gold_earrings_starburst](items/gold_earrings_starburst/README.txt) | appraisable | 1x1 | 14 | 28 | 4 | [sheet](items/gold_earrings_starburst/comparison.png) |
| [gold_bar_orbit](items/gold_bar_orbit/README.txt) | appraisable | 2x1 | 14 | 28 | 4 | [sheet](items/gold_bar_orbit/comparison.png) |
| [gold_bar_starburst](items/gold_bar_starburst/README.txt) | appraisable | 2x1 | 14 | 28 | 4 | [sheet](items/gold_bar_starburst/comparison.png) |
| [silver_ring_orbit](items/silver_ring_orbit/README.txt) | appraisable | 1x1 | 14 | 28 | 4 | [sheet](items/silver_ring_orbit/comparison.png) |
| [silver_ring_starburst](items/silver_ring_starburst/README.txt) | appraisable | 1x1 | 14 | 28 | 4 | [sheet](items/silver_ring_starburst/comparison.png) |
| [silver_bracelet_orbit](items/silver_bracelet_orbit/README.txt) | appraisable | 2x1 | 14 | 28 | 4 | [sheet](items/silver_bracelet_orbit/comparison.png) |
| [silver_bracelet_starburst](items/silver_bracelet_starburst/README.txt) | appraisable | 2x1 | 14 | 28 | 4 | [sheet](items/silver_bracelet_starburst/comparison.png) |
| [silver_necklace_orbit](items/silver_necklace_orbit/README.txt) | appraisable | 2x2 | 14 | 28 | 4 | [sheet](items/silver_necklace_orbit/comparison.png) |
| [silver_necklace_starburst](items/silver_necklace_starburst/README.txt) | appraisable | 2x2 | 14 | 28 | 4 | [sheet](items/silver_necklace_starburst/comparison.png) |
| [silver_earrings_orbit](items/silver_earrings_orbit/README.txt) | appraisable | 1x1 | 14 | 28 | 4 | [sheet](items/silver_earrings_orbit/comparison.png) |
| [silver_earrings_starburst](items/silver_earrings_starburst/README.txt) | appraisable | 1x1 | 14 | 28 | 4 | [sheet](items/silver_earrings_starburst/comparison.png) |
| [silver_bar_orbit](items/silver_bar_orbit/README.txt) | appraisable | 2x1 | 14 | 28 | 4 | [sheet](items/silver_bar_orbit/comparison.png) |
| [silver_bar_starburst](items/silver_bar_starburst/README.txt) | appraisable | 2x1 | 14 | 28 | 4 | [sheet](items/silver_bar_starburst/comparison.png) |
| [jade_rough_yellow](items/jade_rough_yellow/README.txt) | appraisable | 2x2 | 48 | 92 | 13 | [sheet](items/jade_rough_yellow/comparison.png) |
| [jade_rough_black](items/jade_rough_black/README.txt) | appraisable | 2x2 | 44 | 92 | 13 | [sheet](items/jade_rough_black/comparison.png) |
| [jade_rough_water](items/jade_rough_water/README.txt) | appraisable | 2x2 | 51 | 104 | 15 | [sheet](items/jade_rough_water/comparison.png) |
| [jade_rough_white](items/jade_rough_white/README.txt) | appraisable | 2x2 | 44 | 92 | 13 | [sheet](items/jade_rough_white/comparison.png) |
| [jade_rough_rust](items/jade_rough_rust/README.txt) | appraisable | 2x2 | 47 | 104 | 15 | [sheet](items/jade_rough_rust/comparison.png) |
| [jade_rough_mountain](items/jade_rough_mountain/README.txt) | appraisable | 2x2 | 51 | 92 | 13 | [sheet](items/jade_rough_mountain/comparison.png) |
| [jade_pendant_plain](items/jade_pendant_plain/README.txt) | appraisable | 1x1 | 45 | 84 | 12 | [sheet](items/jade_pendant_plain/comparison.png) |
| [jade_pendant_crafted](items/jade_pendant_crafted/README.txt) | appraisable | 1x1 | 45 | 84 | 12 | [sheet](items/jade_pendant_crafted/comparison.png) |
| [jade_cabochon_plain](items/jade_cabochon_plain/README.txt) | appraisable | 1x1 | 45 | 84 | 12 | [sheet](items/jade_cabochon_plain/comparison.png) |
| [jade_cabochon_crafted](items/jade_cabochon_crafted/README.txt) | appraisable | 1x1 | 45 | 84 | 12 | [sheet](items/jade_cabochon_crafted/comparison.png) |
| [jade_bangle_plain](items/jade_bangle_plain/README.txt) | appraisable | 2x1 | 45 | 84 | 12 | [sheet](items/jade_bangle_plain/comparison.png) |
| [jade_bangle_crafted](items/jade_bangle_crafted/README.txt) | appraisable | 2x1 | 45 | 84 | 12 | [sheet](items/jade_bangle_crafted/comparison.png) |
| [jade_beads_plain](items/jade_beads_plain/README.txt) | appraisable | 2x2 | 45 | 84 | 12 | [sheet](items/jade_beads_plain/comparison.png) |
| [jade_beads_crafted](items/jade_beads_crafted/README.txt) | appraisable | 2x2 | 45 | 84 | 12 | [sheet](items/jade_beads_crafted/comparison.png) |
| [jade_carving_plain](items/jade_carving_plain/README.txt) | appraisable | 2x2 | 45 | 84 | 12 | [sheet](items/jade_carving_plain/comparison.png) |
| [jade_carving_crafted](items/jade_carving_crafted/README.txt) | appraisable | 2x2 | 45 | 84 | 12 | [sheet](items/jade_carving_crafted/comparison.png) |
| [gem_diamond_round](items/gem_diamond_round/README.txt) | appraisable | 1x1 | 11 | 28 | 4 | [sheet](items/gem_diamond_round/comparison.png) |
| [gem_diamond_princess](items/gem_diamond_princess/README.txt) | appraisable | 1x1 | 11 | 28 | 4 | [sheet](items/gem_diamond_princess/comparison.png) |
| [gem_ruby_oval](items/gem_ruby_oval/README.txt) | appraisable | 1x1 | 10 | 24 | 3 | [sheet](items/gem_ruby_oval/comparison.png) |
| [gem_ruby_cushion](items/gem_ruby_cushion/README.txt) | appraisable | 1x1 | 10 | 24 | 3 | [sheet](items/gem_ruby_cushion/comparison.png) |
| [gem_sapphire_oval](items/gem_sapphire_oval/README.txt) | appraisable | 1x1 | 11 | 28 | 4 | [sheet](items/gem_sapphire_oval/comparison.png) |
| [gem_sapphire_pear](items/gem_sapphire_pear/README.txt) | appraisable | 1x1 | 11 | 28 | 4 | [sheet](items/gem_sapphire_pear/comparison.png) |
| [gem_emerald_step](items/gem_emerald_step/README.txt) | appraisable | 1x1 | 10 | 24 | 3 | [sheet](items/gem_emerald_step/comparison.png) |
| [gem_emerald_oval](items/gem_emerald_oval/README.txt) | appraisable | 1x1 | 10 | 24 | 3 | [sheet](items/gem_emerald_oval/comparison.png) |
| [gem_pearl_round](items/gem_pearl_round/README.txt) | appraisable | 1x1 | 11 | 28 | 4 | [sheet](items/gem_pearl_round/comparison.png) |
| [gem_pearl_baroque](items/gem_pearl_baroque/README.txt) | appraisable | 1x1 | 11 | 28 | 4 | [sheet](items/gem_pearl_baroque/comparison.png) |
| [jewelry_ring_solitaire](items/jewelry_ring_solitaire/README.txt) | appraisable | 1x1 | 141 | 280 | 40 | [sheet](items/jewelry_ring_solitaire/comparison.png) |
| [setting_ring](items/setting_ring/README.txt) | component | 1x1 | 6 | 0 | 0 | [sheet](items/setting_ring/comparison.png) |
| [jewelry_pendant_bezel](items/jewelry_pendant_bezel/README.txt) | appraisable | 1x1 | 141 | 280 | 40 | [sheet](items/jewelry_pendant_bezel/comparison.png) |
| [setting_pendant](items/setting_pendant/README.txt) | component | 1x1 | 6 | 0 | 0 | [sheet](items/setting_pendant/comparison.png) |
| [jewelry_earrings_stud](items/jewelry_earrings_stud/README.txt) | appraisable | 1x1 | 141 | 280 | 40 | [sheet](items/jewelry_earrings_stud/comparison.png) |
| [setting_earrings](items/setting_earrings/README.txt) | component | 1x1 | 6 | 0 | 0 | [sheet](items/setting_earrings/comparison.png) |
| [jewelry_bracelet_trilogy](items/jewelry_bracelet_trilogy/README.txt) | appraisable | 2x1 | 113 | 224 | 32 | [sheet](items/jewelry_bracelet_trilogy/comparison.png) |
| [setting_bracelet](items/setting_bracelet/README.txt) | component | 2x1 | 6 | 0 | 0 | [sheet](items/setting_bracelet/comparison.png) |
| [jewelry_necklace_drop](items/jewelry_necklace_drop/README.txt) | appraisable | 2x2 | 141 | 280 | 40 | [sheet](items/jewelry_necklace_drop/comparison.png) |
| [setting_necklace](items/setting_necklace/README.txt) | component | 2x2 | 6 | 0 | 0 | [sheet](items/setting_necklace/comparison.png) |
| [legacy_gold_bracelet](items/legacy_gold_bracelet/README.txt) | legacy_appraisable | 1x1 | 3 | 12 | 0 | [sheet](items/legacy_gold_bracelet/comparison.png) |
| [legacy_gold_bar](items/legacy_gold_bar/README.txt) | legacy_appraisable | 1x1 | 2 | 4 | 0 | [sheet](items/legacy_gold_bar/comparison.png) |
| [legacy_silver_cup](items/legacy_silver_cup/README.txt) | legacy_appraisable | 1x1 | 2 | 2 | 0 | [sheet](items/legacy_silver_cup/comparison.png) |
| [legacy_silver_bracelet](items/legacy_silver_bracelet/README.txt) | legacy_appraisable | 1x1 | 2 | 2 | 0 | [sheet](items/legacy_silver_bracelet/comparison.png) |
| [legacy_silver_bar](items/legacy_silver_bar/README.txt) | legacy_appraisable | 1x1 | 2 | 2 | 0 | [sheet](items/legacy_silver_bar/comparison.png) |
| [legacy_jade_rough](items/legacy_jade_rough/README.txt) | legacy_appraisable | 1x1 | 4 | 4 | 0 | [sheet](items/legacy_jade_rough/comparison.png) |
| [legacy_jade_pendant](items/legacy_jade_pendant/README.txt) | legacy_appraisable | 1x1 | 2 | 2 | 0 | [sheet](items/legacy_jade_pendant/comparison.png) |
| [legacy_antique_vase](items/legacy_antique_vase/README.txt) | legacy_appraisable | 1x1 | 2 | 2 | 0 | [sheet](items/legacy_antique_vase/comparison.png) |
| [legacy_painting](items/legacy_painting/README.txt) | legacy_appraisable | 1x1 | 2 | 4 | 0 | [sheet](items/legacy_painting/comparison.png) |
| [legacy_console](items/legacy_console/README.txt) | legacy_appraisable | 1x1 | 2 | 4 | 0 | [sheet](items/legacy_console/comparison.png) |
| [legacy_handheld](items/legacy_handheld/README.txt) | legacy_appraisable | 1x1 | 2 | 2 | 0 | [sheet](items/legacy_handheld/comparison.png) |
| [legacy_cartridge](items/legacy_cartridge/README.txt) | legacy_appraisable | 1x1 | 2 | 2 | 0 | [sheet](items/legacy_cartridge/comparison.png) |
| [legacy_disc](items/legacy_disc/README.txt) | legacy_appraisable | 1x1 | 2 | 2 | 0 | [sheet](items/legacy_disc/comparison.png) |
| [legacy_book](items/legacy_book/README.txt) | legacy_appraisable | 1x1 | 2 | 2 | 0 | [sheet](items/legacy_book/comparison.png) |
| [collectible_vase](items/collectible_vase/README.txt) | appraisable | 2x2 | 15 | 28 | 4 | [sheet](items/collectible_vase/comparison.png) |
| [collectible_painting](items/collectible_painting/README.txt) | appraisable | 2x2 | 15 | 28 | 4 | [sheet](items/collectible_painting/comparison.png) |
| [collectible_console](items/collectible_console/README.txt) | appraisable | 2x2 | 14 | 28 | 4 | [sheet](items/collectible_console/comparison.png) |
| [collectible_handheld](items/collectible_handheld/README.txt) | appraisable | 2x1 | 14 | 28 | 4 | [sheet](items/collectible_handheld/comparison.png) |
| [collectible_cartridge](items/collectible_cartridge/README.txt) | appraisable | 1x1 | 14 | 28 | 4 | [sheet](items/collectible_cartridge/comparison.png) |
| [collectible_disc](items/collectible_disc/README.txt) | appraisable | 1x1 | 14 | 28 | 4 | [sheet](items/collectible_disc/comparison.png) |
| [collectible_book](items/collectible_book/README.txt) | appraisable | 1x1 | 14 | 28 | 4 | [sheet](items/collectible_book/comparison.png) |
| [collectible_silver_cup](items/collectible_silver_cup/README.txt) | appraisable | 1x2 | 14 | 28 | 4 | [sheet](items/collectible_silver_cup/comparison.png) |
| [tool_scale](items/tool_scale/README.txt) | instrument | 2x1 | 4 | 0 | 0 | [sheet](items/tool_scale/comparison.png) |
| [tool_assay](items/tool_assay/README.txt) | instrument | 2x2 | 4 | 0 | 0 | [sheet](items/tool_assay/comparison.png) |
| [tool_loupe](items/tool_loupe/README.txt) | instrument | 1x1 | 4 | 0 | 0 | [sheet](items/tool_loupe/comparison.png) |
| [tool_jade_light](items/tool_jade_light/README.txt) | instrument | 1x2 | 4 | 0 | 0 | [sheet](items/tool_jade_light/comparison.png) |
| [tool_uv](items/tool_uv/README.txt) | instrument | 1x2 | 4 | 0 | 0 | [sheet](items/tool_uv/comparison.png) |
| [tool_gem_probe](items/tool_gem_probe/README.txt) | instrument | 2x1 | 4 | 0 | 0 | [sheet](items/tool_gem_probe/comparison.png) |
| [manual_metals](items/manual_metals/README.txt) | manual | 1x1 | 2 | 0 | 0 | [sheet](items/manual_metals/comparison.png) |
| [manual_jade](items/manual_jade/README.txt) | manual | 1x1 | 2 | 0 | 0 | [sheet](items/manual_jade/comparison.png) |
| [manual_antiques](items/manual_antiques/README.txt) | manual | 1x1 | 2 | 0 | 0 | [sheet](items/manual_antiques/comparison.png) |
| [manual_art](items/manual_art/README.txt) | manual | 1x1 | 2 | 0 | 0 | [sheet](items/manual_art/comparison.png) |
| [manual_electronics](items/manual_electronics/README.txt) | manual | 1x1 | 2 | 0 | 0 | [sheet](items/manual_electronics/comparison.png) |
| [manual_media](items/manual_media/README.txt) | manual | 1x1 | 2 | 0 | 0 | [sheet](items/manual_media/comparison.png) |
| [manual_books](items/manual_books/README.txt) | manual | 1x1 | 2 | 0 | 0 | [sheet](items/manual_books/comparison.png) |
| [manual_recovery](items/manual_recovery/README.txt) | manual | 1x1 | 2 | 0 | 0 | [sheet](items/manual_recovery/comparison.png) |
| [manual_jewelry](items/manual_jewelry/README.txt) | manual | 1x1 | 2 | 0 | 0 | [sheet](items/manual_jewelry/comparison.png) |
| [manual_index](items/manual_index/README.txt) | manual | 1x1 | 2 | 0 | 0 | [sheet](items/manual_index/comparison.png) |
| [manual_organizer](items/manual_organizer/README.txt) | container | 2x2 | 4 | 0 | 0 | [sheet](items/manual_organizer/comparison.png) |
| [manual_organizer_large](items/manual_organizer_large/README.txt) | container | 2x2 | 4 | 0 | 0 | [sheet](items/manual_organizer_large/comparison.png) |
| [savings_card](items/savings_card/README.txt) | credential | 2x1 | 3 | 0 | 0 | [sheet](items/savings_card/comparison.png) |
| [credit_card](items/credit_card/README.txt) | credential | 2x1 | 3 | 0 | 0 | [sheet](items/credit_card/comparison.png) |
| [card_holder](items/card_holder/README.txt) | container | 2x1 | 4 | 0 | 0 | [sheet](items/card_holder/comparison.png) |
| [scrap_board](items/scrap_board/README.txt) | appraisable | 2x1 | 10 | 28 | 4 | [sheet](items/scrap_board/comparison.png) |
| [scrap_processor](items/scrap_processor/README.txt) | appraisable | 1x1 | 10 | 28 | 4 | [sheet](items/scrap_processor/comparison.png) |
| [scrap_connectors](items/scrap_connectors/README.txt) | appraisable | 1x1 | 10 | 28 | 4 | [sheet](items/scrap_connectors/comparison.png) |
| [mixed_e_scrap](items/mixed_e_scrap/README.txt) | material_lot | 2x2 | 3 | 0 | 0 | [sheet](items/mixed_e_scrap/comparison.png) |
| [ore_gold](items/ore_gold/README.txt) | appraisable | 2x1 | 10 | 28 | 4 | [sheet](items/ore_gold/comparison.png) |
| [ore_silver](items/ore_silver/README.txt) | appraisable | 2x1 | 10 | 28 | 4 | [sheet](items/ore_silver/comparison.png) |
| [ore_copper](items/ore_copper/README.txt) | appraisable | 2x1 | 10 | 28 | 4 | [sheet](items/ore_copper/comparison.png) |
| [refined_gold_bar](items/refined_gold_bar/README.txt) | appraisable | 2x1 | 12 | 24 | 3 | [sheet](items/refined_gold_bar/comparison.png) |
| [refined_silver_bar](items/refined_silver_bar/README.txt) | appraisable | 2x1 | 12 | 24 | 3 | [sheet](items/refined_silver_bar/comparison.png) |
| [refined_copper_ingot](items/refined_copper_ingot/README.txt) | appraisable | 2x1 | 12 | 24 | 3 | [sheet](items/refined_copper_ingot/comparison.png) |
| [alloy_gold18k](items/alloy_gold18k/README.txt) | appraisable | 2x1 | 12 | 24 | 3 | [sheet](items/alloy_gold18k/comparison.png) |
| [alloy_silver925](items/alloy_silver925/README.txt) | appraisable | 2x1 | 12 | 24 | 3 | [sheet](items/alloy_silver925/comparison.png) |
| [sorted_gold_scrap](items/sorted_gold_scrap/README.txt) | material_lot | 1x1 | 2 | 0 | 0 | [sheet](items/sorted_gold_scrap/comparison.png) |
| [sorted_silver_scrap](items/sorted_silver_scrap/README.txt) | material_lot | 1x1 | 2 | 0 | 0 | [sheet](items/sorted_silver_scrap/comparison.png) |
| [sorted_copper_scrap](items/sorted_copper_scrap/README.txt) | material_lot | 1x1 | 2 | 0 | 0 | [sheet](items/sorted_copper_scrap/comparison.png) |
| [recovered_gold_lot](items/recovered_gold_lot/README.txt) | material_lot | 1x1 | 2 | 0 | 0 | [sheet](items/recovered_gold_lot/comparison.png) |
| [recovered_silver_lot](items/recovered_silver_lot/README.txt) | material_lot | 1x1 | 2 | 0 | 0 | [sheet](items/recovered_silver_lot/comparison.png) |
| [recovered_copper_lot](items/recovered_copper_lot/README.txt) | material_lot | 1x1 | 2 | 0 | 0 | [sheet](items/recovered_copper_lot/comparison.png) |
| [assay_sample](items/assay_sample/README.txt) | process_item | 1x1 | 2 | 0 | 0 | [sheet](items/assay_sample/comparison.png) |
| [recovery_batch](items/recovery_batch/README.txt) | process_item | 2x1 | 4 | 0 | 0 | [sheet](items/recovery_batch/comparison.png) |
| [inert_residue](items/inert_residue/README.txt) | material_lot | 2x1 | 1 | 0 | 0 | [sheet](items/inert_residue/comparison.png) |
| [process_cartridge](items/process_cartridge/README.txt) | consumable | 1x1 | 2 | 0 | 0 | [sheet](items/process_cartridge/comparison.png) |
| [energy_cell](items/energy_cell/README.txt) | consumable | 1x1 | 3 | 0 | 0 | [sheet](items/energy_cell/comparison.png) |
| [crucible](items/crucible/README.txt) | tool | 2x1 | 3 | 0 | 0 | [sheet](items/crucible/comparison.png) |
| [ingot_mold](items/ingot_mold/README.txt) | tool | 2x1 | 2 | 0 | 0 | [sheet](items/ingot_mold/comparison.png) |
| [recovery_station](items/recovery_station/README.txt) | machine | 2x2 | 5 | 0 | 0 | [sheet](items/recovery_station/comparison.png) |
| [refining_furnace](items/refining_furnace/README.txt) | machine | 2x2 | 6 | 0 | 0 | [sheet](items/refining_furnace/comparison.png) |
| [materials_bin](items/materials_bin/README.txt) | container | 2x2 | 4 | 0 | 0 | [sheet](items/materials_bin/comparison.png) |
| [financial_terminal](items/financial_terminal/README.txt) | machine | 2x2 | 4 | 0 | 0 | [sheet](items/financial_terminal/comparison.png) |
| [jeweler_bench](items/jeweler_bench/README.txt) | machine | 2x2 | 4 | 0 | 0 | [sheet](items/jeweler_bench/comparison.png) |
| [jeweler_toolkit](items/jeweler_toolkit/README.txt) | tool | 2x1 | 3 | 0 | 0 | [sheet](items/jeweler_toolkit/comparison.png) |
| [gem_case](items/gem_case/README.txt) | container | 2x2 | 4 | 0 | 0 | [sheet](items/gem_case/comparison.png) |
| [presentation_case](items/presentation_case/README.txt) | container | 2x2 | 4 | 0 | 0 | [sheet](items/presentation_case/comparison.png) |
| [jewelry_display](items/jewelry_display/README.txt) | container | 2x2 | 5 | 0 | 0 | [sheet](items/jewelry_display/comparison.png) |
| [jewelry_certificate](items/jewelry_certificate/README.txt) | certificate | 1x1 | 3 | 0 | 0 | [sheet](items/jewelry_certificate/comparison.png) |
| [jewelry_job_ticket](items/jewelry_job_ticket/README.txt) | document | 1x1 | 5 | 0 | 0 | [sheet](items/jewelry_job_ticket/comparison.png) |

## UI rows

| Asset | Kind | Size | States | Preview |
| --- | --- | --- | --- | --- |
| [screen_hub](ui/screen_hub/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_hub/preview.png) / [1080p](ui/screen_hub/mockup_1080p.png) / [4K](ui/screen_hub/mockup_4k.png) |
| [screen_catalog](ui/screen_catalog/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_catalog/preview.png) / [1080p](ui/screen_catalog/mockup_1080p.png) / [4K](ui/screen_catalog/mockup_4k.png) |
| [screen_inventory](ui/screen_inventory/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_inventory/preview.png) / [1080p](ui/screen_inventory/mockup_1080p.png) / [4K](ui/screen_inventory/mockup_4k.png) |
| [screen_inspection](ui/screen_inspection/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_inspection/preview.png) / [1080p](ui/screen_inspection/mockup_1080p.png) / [4K](ui/screen_inspection/mockup_4k.png) |
| [screen_instruments](ui/screen_instruments/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_instruments/preview.png) / [1080p](ui/screen_instruments/mockup_1080p.png) / [4K](ui/screen_instruments/mockup_4k.png) |
| [screen_quote](ui/screen_quote/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_quote/preview.png) / [1080p](ui/screen_quote/mockup_1080p.png) / [4K](ui/screen_quote/mockup_4k.png) |
| [screen_asset_record](ui/screen_asset_record/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_asset_record/preview.png) / [1080p](ui/screen_asset_record/mockup_1080p.png) / [4K](ui/screen_asset_record/mockup_4k.png) |
| [screen_workshop](ui/screen_workshop/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_workshop/preview.png) / [1080p](ui/screen_workshop/mockup_1080p.png) / [4K](ui/screen_workshop/mockup_4k.png) |
| [screen_bank](ui/screen_bank/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_bank/preview.png) / [1080p](ui/screen_bank/mockup_1080p.png) / [4K](ui/screen_bank/mockup_4k.png) |
| [screen_credit](ui/screen_credit/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_credit/preview.png) / [1080p](ui/screen_credit/mockup_1080p.png) / [4K](ui/screen_credit/mockup_4k.png) |
| [screen_securities](ui/screen_securities/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_securities/preview.png) / [1080p](ui/screen_securities/mockup_1080p.png) / [4K](ui/screen_securities/mockup_4k.png) |
| [screen_financial_overview](ui/screen_financial_overview/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_financial_overview/preview.png) / [1080p](ui/screen_financial_overview/mockup_1080p.png) / [4K](ui/screen_financial_overview/mockup_4k.png) |
| [screen_events](ui/screen_events/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_events/preview.png) / [1080p](ui/screen_events/mockup_1080p.png) / [4K](ui/screen_events/mockup_4k.png) |
| [screen_settlement](ui/screen_settlement/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_settlement/preview.png) / [1080p](ui/screen_settlement/mockup_1080p.png) / [4K](ui/screen_settlement/mockup_4k.png) |
| [screen_pawn](ui/screen_pawn/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_pawn/preview.png) / [1080p](ui/screen_pawn/mockup_1080p.png) / [4K](ui/screen_pawn/mockup_4k.png) |
| [screen_handbook](ui/screen_handbook/README.txt) | screen_plate | 440x616 | default, empty, blocked | [preview](ui/screen_handbook/preview.png) / [1080p](ui/screen_handbook/mockup_1080p.png) / [4K](ui/screen_handbook/mockup_4k.png) |
| [screen_organizer](ui/screen_organizer/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_organizer/preview.png) / [1080p](ui/screen_organizer/mockup_1080p.png) / [4K](ui/screen_organizer/mockup_4k.png) |
| [screen_supply_shop](ui/screen_supply_shop/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_supply_shop/preview.png) / [1080p](ui/screen_supply_shop/mockup_1080p.png) / [4K](ui/screen_supply_shop/mockup_4k.png) |
| [screen_recovery](ui/screen_recovery/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_recovery/preview.png) / [1080p](ui/screen_recovery/mockup_1080p.png) / [4K](ui/screen_recovery/mockup_4k.png) |
| [screen_refining](ui/screen_refining/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_refining/preview.png) / [1080p](ui/screen_refining/mockup_1080p.png) / [4K](ui/screen_refining/mockup_4k.png) |
| [screen_jobs](ui/screen_jobs/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_jobs/preview.png) / [1080p](ui/screen_jobs/mockup_1080p.png) / [4K](ui/screen_jobs/mockup_4k.png) |
| [screen_provenance](ui/screen_provenance/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_provenance/preview.png) / [1080p](ui/screen_provenance/mockup_1080p.png) / [4K](ui/screen_provenance/mockup_4k.png) |
| [screen_procurement](ui/screen_procurement/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_procurement/preview.png) / [1080p](ui/screen_procurement/mockup_1080p.png) / [4K](ui/screen_procurement/mockup_4k.png) |
| [screen_jewelry](ui/screen_jewelry/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_jewelry/preview.png) / [1080p](ui/screen_jewelry/mockup_1080p.png) / [4K](ui/screen_jewelry/mockup_4k.png) |
| [screen_setting](ui/screen_setting/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_setting/preview.png) / [1080p](ui/screen_setting/mockup_1080p.png) / [4K](ui/screen_setting/mockup_4k.png) |
| [screen_jewelry_orders](ui/screen_jewelry_orders/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_jewelry_orders/preview.png) / [1080p](ui/screen_jewelry_orders/mockup_1080p.png) / [4K](ui/screen_jewelry_orders/mockup_4k.png) |
| [screen_gem_lab](ui/screen_gem_lab/README.txt) | screen_plate | 600x400 | default, empty, blocked | [preview](ui/screen_gem_lab/preview.png) / [1080p](ui/screen_gem_lab/mockup_1080p.png) / [4K](ui/screen_gem_lab/mockup_4k.png) |
| [button_primary](ui/button_primary/README.txt) | component | 120x28 | default, hover, pressed, disabled | [preview](ui/button_primary/preview.png) |
| [button_secondary](ui/button_secondary/README.txt) | component | 88x24 | default, hover, pressed, disabled | [preview](ui/button_secondary/preview.png) |
| [button_icon](ui/button_icon/README.txt) | component | 24x24 | default, hover, pressed, disabled | [preview](ui/button_icon/preview.png) |
| [category_tab](ui/category_tab/README.txt) | component | 96x24 | default, selected, disabled | [preview](ui/category_tab/preview.png) |
| [catalog_row](ui/catalog_row/README.txt) | component | 520x42 | default, selected, disabled | [preview](ui/catalog_row/preview.png) |
| [event_card](ui/event_card/README.txt) | component | 520x72 | forecast, active, expired, resolved | [preview](ui/event_card/preview.png) |
| [job_row](ui/job_row/README.txt) | component | 520x48 | queued, running, paused, ready, claimed | [preview](ui/job_row/preview.png) |
| [tooltip](ui/tooltip/README.txt) | component | 240x80 | default, warning, error | [preview](ui/tooltip/preview.png) |
| [dialog](ui/dialog/README.txt) | component | 360x200 | confirm, blocked, error | [preview](ui/dialog/preview.png) |
| [progress_track](ui/progress_track/README.txt) | component | 240x12 | default, paused, complete | [preview](ui/progress_track/preview.png) |
| [progress_fill](ui/progress_fill/README.txt) | component | 240x12 | default, paused, complete | [preview](ui/progress_fill/preview.png) |
| [evidence_marker](ui/evidence_marker/README.txt) | component | 32x32 | discovered, selected, resolved | [preview](ui/evidence_marker/preview.png) |
| [inspection_frame](ui/inspection_frame/README.txt) | component | 298x199 | default, locked | [preview](ui/inspection_frame/preview.png) |
| [handbook_page](ui/handbook_page/README.txt) | component | 440x616 | introduction, defect_example, index | [preview](ui/handbook_page/preview.png) |
| [certificate_page](ui/certificate_page/README.txt) | component | 440x616 | valid, superseded | [preview](ui/certificate_page/preview.png) |
| [inventory_cell](ui/inventory_cell/README.txt) | component | 64x64 | empty, hover, selected, blocked | [preview](ui/inventory_cell/preview.png) |
| [native_bookbox](ui/native_bookbox/README.txt) | component | 288x176 | empty, partly_filled, full | [preview](ui/native_bookbox/preview.png) |
| [native_cardholder](ui/native_cardholder/README.txt) | component | 288x112 | empty, partly_filled, full | [preview](ui/native_cardholder/preview.png) |
| [native_materials_bin](ui/native_materials_bin/README.txt) | component | 288x304 | empty, partly_filled, full | [preview](ui/native_materials_bin/preview.png) |
| [grade_badge](ui/grade_badge/README.txt) | component | 24x24 | dou, nuo, bing | [preview](ui/grade_badge/preview.png) |
| [status_badge](ui/status_badge/README.txt) | component | 24x24 | owned, consigned, reserved, consumed, unknown, confirmed | [preview](ui/status_badge/preview.png) |
| [native_bookbox_large](ui/native_bookbox_large/README.txt) | component | 288x240 | empty, partly_filled, full | [preview](ui/native_bookbox_large/preview.png) |
| [native_gem_case](ui/native_gem_case/README.txt) | component | 288x304 | empty, partly_filled, full | [preview](ui/native_gem_case/preview.png) |
| [native_presentation_case](ui/native_presentation_case/README.txt) | component | 288x176 | empty, partly_filled, full | [preview](ui/native_presentation_case/preview.png) |
| [native_jewelry_display](ui/native_jewelry_display/README.txt) | component | 288x304 | empty, partly_filled, full, reserved | [preview](ui/native_jewelry_display/preview.png) |
| [stone_socket](ui/stone_socket/README.txt) | component | 48x48 | empty, compatible, incompatible, reserved | [preview](ui/stone_socket/preview.png) |
| [order_receipt](ui/order_receipt/README.txt) | component | 440x616 | accepted, ready, delivered, cancelled | [preview](ui/order_receipt/preview.png) |
| [resource_icon](ui/resource_icon/README.txt) | component | 32x32 | gold, silver, copper, gem, energy, capacity, time | [preview](ui/resource_icon/preview.png) |
| [feedback_symbol](ui/feedback_symbol/README.txt) | component | 64x64 | neutral, impatient, withdrawn, satisfied | [preview](ui/feedback_symbol/preview.png) |
| [event_assay_campaign](ui/event_assay_campaign/README.txt) | event_icon | 64x64 | default | [preview](ui/event_assay_campaign/preview.png) |
| [event_chip_shortage](ui/event_chip_shortage/README.txt) | event_icon | 64x64 | default | [preview](ui/event_chip_shortage/preview.png) |
| [event_collectors_visit](ui/event_collectors_visit/README.txt) | event_icon | 64x64 | default | [preview](ui/event_collectors_visit/preview.png) |
| [event_credit_squeeze](ui/event_credit_squeeze/README.txt) | event_icon | 64x64 | default | [preview](ui/event_credit_squeeze/preview.png) |
| [event_gem_disclosure](ui/event_gem_disclosure/README.txt) | event_icon | 64x64 | default | [preview](ui/event_gem_disclosure/preview.png) |
| [event_gem_freight](ui/event_gem_freight/README.txt) | event_icon | 64x64 | default | [preview](ui/event_gem_freight/preview.png) |
| [event_gold_shipment](ui/event_gold_shipment/README.txt) | event_icon | 64x64 | default | [preview](ui/event_gold_shipment/preview.png) |
| [event_jade_festival](ui/event_jade_festival/README.txt) | event_icon | 64x64 | default | [preview](ui/event_jade_festival/preview.png) |
| [event_jewelry_festival](ui/event_jewelry_festival/README.txt) | event_icon | 64x64 | default | [preview](ui/event_jewelry_festival/preview.png) |
| [event_new_jade_seam](ui/event_new_jade_seam/README.txt) | event_icon | 64x64 | default | [preview](ui/event_new_jade_seam/preview.png) |
| [event_port_delay](ui/event_port_delay/README.txt) | event_icon | 64x64 | default | [preview](ui/event_port_delay/preview.png) |
| [event_power_rationing](ui/event_power_rationing/README.txt) | event_icon | 64x64 | default | [preview](ui/event_power_rationing/preview.png) |
| [event_recovery_grant](ui/event_recovery_grant/README.txt) | event_icon | 64x64 | default | [preview](ui/event_recovery_grant/preview.png) |
| [event_repair_drive](ui/event_repair_drive/README.txt) | event_icon | 64x64 | default | [preview](ui/event_repair_drive/preview.png) |
| [event_safe_haven](ui/event_safe_haven/README.txt) | event_icon | 64x64 | default | [preview](ui/event_safe_haven/preview.png) |
