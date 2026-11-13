# StarPawn

A unified shop-management expansion for **Probably Stolen**, covering physical-item appraisal, precious metals and jewelry, jade processing, collectibles, entertainment goods, banking, securities, and pawning.

Current playable version: **0.3.0 / Windows / 100-Rev13 / Steam BuildID 26525552**. The gameplay revision has been implemented and tested in the game using a separate save slot. New artwork consists of development assets used for geometry checks; final art awaits the user's style-sample approval.

This revision adds two designs for each of five gold and silver item types, six jade-rough types, four finished-jade types, five physical instruments, seven fixed-defect handbooks, and an eight-slot handbook organizer. Banking gains daily transaction limits, revised rates for new contracts, a financial overview, and overnight settlement receipts. See the [0.3 delivery record](docs/REVISION_03.txt) for exact scope and limitations. The 239 catalog definitions include condition combinations; they are not 239 distinct designs.

## Art handoff for Claude

Start with the [art direction and handoff](art/ART_DIRECTION.txt), [minimal style-sample prompt](art/CLAUDE_STYLE_PROMPT.txt), and [0.3 delivery record](docs/REVISION_03.txt).

The first deliverable is one gold ring in three conditions. Wait for the user's style approval before producing the full item and UI set. Design scope is listed in [asset-plan.csv](art/asset-plan.csv); exact runtime files and condition mappings are in [runtime-art-map.json](art/runtime-art-map.json); screen specifications are in [UI_SPEC.txt](art/UI_SPEC.txt). `existing-assets.json` and `existing-items.json` are historical 0.2 snapshots.

Pull the current `main` branch, then create `art/style-sample`. The `gameplay/0.3` branch also contains the 0.3 gameplay baseline. Put proposals in `art/proposals/`; approved assets can later enter `mod/Textures/`. Preserve item IDs, evidence IDs, interfaces, and save compatibility. Any hotspot-coordinate changes require matching configuration updates and validation.

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
