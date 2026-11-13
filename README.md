# StarPawn

A unified shop-management expansion for **Probably Stolen**, covering physical-item appraisal, precious metals and jewelry, jade processing, collectibles, entertainment goods, banking, securities, and pawning.

Current integration build: **0.3.1 / Windows / 100-Rev13 / Steam BuildID 26525552**, on `integration/art04`. Claude's `art/batch04` delivery is integrated into the existing playable catalog and tested in a separate save. See the [illustrated integration report](docs/ART_INTEGRATION_031.md) for fixes, screenshots, test evidence, demo instructions, and remaining limitations. This is a compatible art update to the 0.3 gameplay, not the complete 0.4 business expansion.

The 0.3 gameplay adds two designs for each of five gold and silver item types, six jade-rough types, four finished-jade types, five physical instruments, and an eight-slot handbook organizer. The 0.3.1 update adds [15 short physical handbook volumes](docs/HANDBOOK_VOLUMES_031.md), with every book limited to five pages including its introduction. Five existing short books remain on sale; old metals/jade anthologies retain collection access through short indexes. Banking has daily transaction limits, revised rates for new contracts, a financial overview, and overnight settlement receipts. See the [0.3 delivery record](docs/REVISION_03.txt) for historical scope. The catalog now has 254 definitions (239 preserved plus 15 books); condition combinations are included, so this is not a count of distinct art designs.

## Current production handoff: 0.4 design

The user accepted the ring sample's visual direction and authorized the complete fixed-size art batch. Contract revision **04.2** covers jewelry business, native-style manuals and physical refining; the **04.3 pagination amendment** limits all manuals to five pages. **The playable release is 0.3.1; the following 0.4 files are design and production contracts, not implemented gameplay.**

The batch has been delivered under `art/proposals/batch04`. The [batch prompt](art/CLAUDE_BATCH_PROMPT_04.txt) and [frozen production specification](art/BATCH_ART_SPEC_04.txt) remain its production contracts. The specifications cover:

- [135 physical art models and exact inventory footprints/pixel sizes](art/ITEM_LAYOUT_04.csv), with [all 241 current item/card mappings](art/RUNTIME_COVERAGE_04.csv).
- [71 UI screen, widget, and event-icon dimensions](art/UI_ASSETS_04.csv).
- [250 category-specific defect templates in 50 sets](docs/DEFECT_CATALOG_04.csv) and their [observation/combination rules](docs/DEFECT_RULES_04.txt).
- [Native-style portrait handbook requirements](art/VANILLA_HANDBOOK_SPEC_04.txt), [50 chapter assignments](docs/HANDBOOK_INDEX_04.csv), [actual in-game reference](art/references/vanilla_handbook/native_counterfeit_handbook.jpg) and [reference provenance](art/references/vanilla_handbook/README.txt).
- [Complete jewelry business](docs/JEWELRY_BUSINESS_04.txt), [five mounted forms and socket/stone compatibility](docs/JEWELRY_SETTINGS_04.csv), and [five jewelry service routes](docs/JEWELRY_OPERATIONS_04.csv).
- [Connected event, market, material and recovery design](docs/SYSTEMS_DESIGN_04.txt), [15 events](docs/EVENT_EFFECTS_04.csv), [event stories](docs/EVENT_STORIES_04.csv), and [15 physical processing recipes](docs/RECIPES_04.csv).
- [Primary-source mod research and adaptation boundaries](docs/MOD_RESEARCH_04.txt).

Gold has five categories with two designs each. Jade uses exactly three game tiers: dou, nuo, and bing. The jewelry loop adds diamond, ruby, sapphire, emerald and pearl goods, mounted forms, sourcing, appraisal, buying/selling, demounting/remounting, repairs, matching sets and customer orders. Each appraisal category/subtype owns its own defect set. Events affect shared prices, demand, supply, new agreement terms and processing conditions; existing item truth and signed orders remain saved. Fictional alchemy/transmutation is excluded. Recovery and refining conserve the elements already present, including trace metal in supported electronics.

Claude should preserve `art/style-sample`, create or continue `art/batch04`, and merge current `origin/main` into that art branch to obtain these specifications. Produce assets under `art/proposals/batch04`, then commit and push that art branch; do not merge it into or push `main`. No repeated style-only approval gate is required. Runtime code, live installation, and save migrations belong to the gameplay integration task.

Run `python tests/Validate-Design04.py` to check design-table relationships. `python source/Content/Export-Design04.py` deterministically exports the fixed-size manifests without changing runtime content or images. These checks validate specifications, not the future gameplay implementation or yet-to-be-produced art.

## Historical style-sample handoff (0.3)

Start with the [art direction and handoff](art/ART_DIRECTION.txt), [minimal style-sample prompt](art/CLAUDE_STYLE_PROMPT.txt), and [0.3 delivery record](docs/REVISION_03.txt).

The initial deliverable was one gold ring in three conditions; that visual direction is now accepted for the 0.4 production contract above. [asset-plan.csv](art/asset-plan.csv) is the earlier broad proposal list; exact 0.3 runtime files and condition mappings remain in [runtime-art-map.json](art/runtime-art-map.json); [UI_SPEC.txt](art/UI_SPEC.txt) documents the earlier screen baseline. `existing-assets.json` and `existing-items.json` are historical 0.2 snapshots. The newer fixed-size manifests take precedence for batch production.

The original sample work is on `art/style-sample`. Its approved direction is now adapted into `mod/Textures/`. The historical `runtime-art-map.json` remains the legacy generator input; [runtime-art-map-031.json](art/runtime-art-map-031.json) records the integrated clue bounds. Existing item IDs, evidence IDs, economic causes, and occupied cells are preserved.

Prompts and explanatory documentation are in English. The game remains localized in Simplified Chinese. Runtime exports, historical data snapshots, localization examples, and screenshots retain their original Chinese display strings so they can be matched to the actual game; these are source data, not instructions to translate the game.

## Repository layout

- `source/Core`: asset, appraisal, and economy rules without Unity dependencies.
- `source/Game`: native items, PaperUI, wallet, Harmony, and save adapters.
- `mod/Content`: items, rules, localization, and optional content packs.
- `mod/Textures`: item and UI textures loaded by the game.
- `tests`: business-rule tests, native-boundary test doubles, and metadata checks. `*-results-03.txt` contains 0.3 results; other result files preserve historical baselines.
- `art`: art tasks, condition contracts, and asset inventories.
- `docs`: gameplay revisions, extension formats, and reference research.

## Local build

Requires .NET SDK 8.0.424, the netstandard2.1 reference pack, and a legitimate game installation. Game assemblies are not included in this repository.

```powershell
./source/Build.ps1 -GameDir 'D:\Program Files (x86)\steam\steamapps\common\Probably Stolen'
```

The build produces `mod/StarPawn.Core.dll` and `mod/StarPawn.Game.dll`. With the game closed, copy the contents of `mod` into the game's `Mods/starpawn` directory, enable the mod, and restart. In the shop, press **F7**, or use the vanilla magnifying glass on a mod item. To use an instrument, right-click it, select **Use**, and click the target item; instruments are also available from the appraisal screen.

The manifest's `0.46` value is a compatibility field used by this loader version; the actual tested game baseline is 100-Rev13.

## Validation

```powershell
./tests/Run-EconomyTests.ps1
./tests/Run-ContentTests.ps1
./tests/Run-ContentBootstrapTests.ps1 -GameDir '<game directory>'
./tests/Run-CommerceTests.ps1 -GameDir '<game directory>'
./tests/Run-NativeTradeTests.ps1 -GameDir '<game directory>'
./tests/Run-BankCardTests.ps1 -GameDir '<game directory>'
./tests/Run-AppraisalTests.ps1 -GameDir '<game directory>'
./tests/Test-HarmonyBindings.ps1 -GameDir '<game directory>'
python ./tests/Check-DevelopmentGeometry.py
```

For 0.3, all 156 business-rule/boundary tests and 33 Harmony parameter-metadata checks passed. Geometry checks passed for 196 new item states and 284 evidence centers. In-game tests used slot5 for save migration, instruments, handbooks and storage, rough-jade processing, daily limits, normal overnight settlement, and reloading. Test doubles and metadata checks do not establish coverage of every Unity failure path. Save files and the illustrated engineering report remain local.

Content generation requires Python and Pillow. `Generate-Catalog.py` generates only data and localization by default; `--development-art` overwrites development artwork. Do not run an overwriting art generator after integrating approved assets. Use `python source/Content/Export-ArtMap.py` to refresh the file mapping without regenerating artwork.

## Collaboration and assets

This repository contains the project's source, configuration, and project-generated artwork. Game assets, third-party mod binaries, personal saves, logs, and backups remain local. Referenced gameplay ideas are implemented independently; verify the relevant license before copying or adapting third-party code or assets. No open-source license has been selected for this project.
