# StarPawn

A unified shop-management expansion for **Probably Stolen**, covering physical-item appraisal, precious metals and jewelry, jade processing, collectibles, entertainment goods, banking, securities, and pawning.

Current playable version: **0.3.0 / Windows / 100-Rev13 / Steam BuildID 26525552**. The gameplay revision has been implemented and tested in the game using a separate save slot. New artwork consists of development assets used for geometry checks; final art awaits the user's style-sample approval.

This revision adds two designs for each of five gold and silver item types, six jade-rough types, four finished-jade types, five physical instruments, seven fixed-defect handbooks, and an eight-slot handbook organizer. Banking gains daily transaction limits, revised rates for new contracts, a financial overview, and overnight settlement receipts. See the [0.3 delivery record](docs/REVISION_03.txt) for exact scope and limitations. The 239 catalog definitions include condition combinations; they are not 239 distinct designs.

## Current production handoff: 0.4 design

The user accepted the ring sample's visual direction and authorized the complete fixed-size art batch. **The playable release remains 0.3; the following 0.4 files are design and production contracts, not implemented gameplay.**

Start with the [batch prompt](art/CLAUDE_BATCH_PROMPT_04.txt) and [frozen production specification](art/BATCH_ART_SPEC_04.txt). The specifications cover:

- [110 physical art models and exact inventory footprints/pixel sizes](art/ITEM_LAYOUT_04.csv), with [all 241 current item/card mappings](art/RUNTIME_COVERAGE_04.csv).
- [UI screen, widget, and event-icon dimensions](art/UI_ASSETS_04.csv).
- [200 category-specific defect templates](docs/DEFECT_CATALOG_04.csv) and their [observation/combination rules](docs/DEFECT_RULES_04.txt).
- [Connected event, market, material, recovery, and fictional-alchemy design](docs/SYSTEMS_DESIGN_04.txt), [event effects](docs/EVENT_EFFECTS_04.csv), [event stories](docs/EVENT_STORIES_04.csv), and [abstract processing recipes](docs/RECIPES_04.csv).
- [Primary-source mod research and adaptation boundaries](docs/MOD_RESEARCH_04.txt).

Gold has five categories with two designs each. Jade uses exactly three game tiers: dou, nuo, and bing. Each appraisal category/subtype owns its own defect set. Events affect shared prices, demand, supply, new agreement terms and processing conditions; existing item truth remains saved. Material recovery and explicitly fictional transmutation use separate accounting rules.

Claude should preserve `art/style-sample`, create `art/batch04` from it, and merge current `origin/main` to obtain these specifications. Produce assets under `art/proposals/batch04`, then commit and push that art branch. No repeated style-only approval gate is required. Runtime code, live installation, and save migrations belong to the gameplay integration task.

Run `python tests/Validate-Design04.py` to check design-table relationships. `python source/Content/Export-Design04.py` deterministically exports the fixed-size manifests without changing runtime content or images. These checks validate specifications, not the future gameplay implementation or yet-to-be-produced art.

## Historical style-sample handoff (0.3)

Start with the [art direction and handoff](art/ART_DIRECTION.txt), [minimal style-sample prompt](art/CLAUDE_STYLE_PROMPT.txt), and [0.3 delivery record](docs/REVISION_03.txt).

The initial deliverable was one gold ring in three conditions; that visual direction is now accepted for the 0.4 production contract above. [asset-plan.csv](art/asset-plan.csv) is the earlier broad proposal list; exact 0.3 runtime files and condition mappings remain in [runtime-art-map.json](art/runtime-art-map.json); [UI_SPEC.txt](art/UI_SPEC.txt) documents the earlier screen baseline. `existing-assets.json` and `existing-items.json` are historical 0.2 snapshots. The newer fixed-size manifests take precedence for batch production.

The original sample work is on `art/style-sample`. Preserve that work when creating the batch branch. Approved and integrated assets can later enter `mod/Textures/`. Preserve item IDs, evidence IDs, interfaces, and save compatibility. Any hotspot-coordinate changes require matching configuration updates and validation.

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
