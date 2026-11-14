# StarPawn Revision 05.1 — Engineering Report

Branch: `feature/unified-business-05` (from `origin/integration/art04` at `e96a149`). `main` is unchanged.
Mod version string: `0.5.0-dev` (this is a development build of the Revision 05.1 contract, not a release).

## 1. What was verified, and how

| Evidence | Status |
| --- | --- |
| 9 C# suites, 227 checks, built and run on Linux with Roslyn 4.8 / .NET 8 (`tests/run-linux.sh`) | **Passed** — output in `docs/report-assets/rev05/test-run-linux.txt` |
| New suite `tests/UnifiedTests.cs`: 42 Revision 05 regression tests against the real packaged content | **Passed** |
| All of `source/Game` type-checked against permissive stand-ins (`tests/stubs`) | **Passed** — checks syntax and the mod's own types only |
| Compile against the real `Assembly-CSharp.dll` (`source/Build.ps1`) | **Not run** — the game is not installed in this environment |
| Any in-game behaviour, screenshot, or demo save | **Not available** — no game, no Computer Use |

Nothing below that touches the native game (registration, hooks, Read action, sound, inventory hit-testing, migration, follow-up conversation) has been observed in the game. Those paths are implemented to the audited signatures where the audit names them, and through logged, scoped reflection where it does not. They must be checked with the route in section 9 before this is merged.

The `tests/stubs` stand-ins type every native member as `dynamic` unless hand-typed from this repository's own call sites. A passing stub compile therefore does **not** prove a native member exists with that signature.

## 2. Native appraisal career (handoff §3)

- **Registration.** `Native05.RegisterCareer` registers 20 StarPawn commission groups through the audited `ModHelper.AddAppraisalItem`. The groups are the gold and silver forms, finished and rough jade, loose gems, mounted jewelry, collectibles, electronics and recovery. Each registration uses a unique `starpawn_*` ID. Vanilla hand, ribwich and plasma_fuel are never touched. `RemoveAppraisalItems("starpawn")` removes only StarPawn's types.
- **Rank schedule.** Rank 0 covers gold and silver goods. Rank 1 covers finished jade and introductory collectibles. Rank 2 covers rough jade, loose gems, complex collectibles, electronics and recovery. Rank 3 covers mounted jewelry. The schedule is stored as `CommissionRank` per definition. It gates paid commissions only; trade, reading and repair are not gated.
- **Click-XP retired.** Discoveries award nothing and mastery auto-reveal is gone. The old `Experience` value is copied once to `Unified.LegacyExperience` as history. It is never converted into a promotion.
- **Settlement.** A postfix on `AppraisalServiceHelper.OnEstimate`, found by name, records the grade and resolves the customer's follow-up. It never calls native settlement again. `getCorrectValue` returns the private answer computed when the commission opened, from that day's saved market. A later price move cannot invalidate the answer.
- **Commission fee.** StarPawn commissions pay a base fee of 5 Cr plus 3% of value, capped at 60 Cr, scaled by accuracy grade. A postfix on `GiveAppraisalFee`, found by name, applies the cap after the native receipt through the idempotent wallet journal. Vanilla commissions are unchanged.
- **Promotion books.** `Career.ClaimEntitlements` grants each rank's short volumes once, keyed `rank:N:id`. A higher-rank save receives only the volumes it is missing. A reload grants nothing.

## 3. Appraisal → sale / pawn / leave (handoff §5–6)

- **Visits.** `VisitService` keeps one visit per customer and item. The visit stores the profile, the weights after eligibility and event shifts, the draw, the intent, the offer and its expiry. Reopening a window or reloading returns the same visit; nothing is redrawn.
- **Weights.** The five handoff profiles are used as written (ordinary 50/30/20 … collector 70/15/15). An ineligible pawn route moves its weight to "leave". A credit squeeze moves 15 points toward pawn. All shifts are data rows in `mod/Content/unified-rules.json`.
- **Same item.** A sale transfers the same `AssetId` and native item. A pawn signs one ledger contract on the same asset. The contract records the customer, the appraisal reference and the LTV. Declining never undoes the appraisal fee.
- **Pawn policy.** New offers start at 50% LTV, bounded to 30–65%. A credit squeeze subtracts 10 points. Unassayed gold is lent on a 60% haircut, with the reason "A material test is needed…". Signed contracts keep their principal, rates and deadline. A renewal is an amendment on the same contract.
- **Direct visits.** Direct sale and pawn visits skip the commission and are priced from the same valuation.

## 4. Component valuation (handoff §8)

`ComponentValuation.Quote` is the single service used by quotes, buyback, pawn collateral, commissions and workshop comparisons:

```
metal       = eligible mass x confirmed (or claimed) purity x the context's rate
workmanship = max(0, basis x demand - cosmetic/structural deductions)   (capped, never below 0)
stones      = each real embedded stone: carats x family rate x gem index x origin factor
premium     = justified collectible premium, shown separately
```

- Every cause has an explicit component: workmanship, condition, metal purity, metal mass, stone or premium. Scratches and dents can never reduce the metal value.
- The handoff example (600 + 200 → scratch −40 → 760) is a regression test.
- Several views of one crack count as one cause.
- Evidence toggles and "cite all evidence" are removed. The old API calls remain as no-ops so old saves still load.
- Unknown purity is not leaked: plated and genuine items quote identically until a finding or assay.

## 5. Markets, labels and events (handoff §4, §8)

- **Labels.** Luxury, Collectibles, Materials, Weapons, Electronics and Entertainment are explicit fields on every definition. They drive the new filters.
- **Native goods.** Native hands map to Weapons + Augments, plus Collectibles for rare configurations. Plasma fuel maps to Materials + Energy. Mapping is by registered native ID only.
- **Saved daily market.** `MarketService.EnsureDay` writes one `MarketDay` per day: event selection, channel sums, and the derived Au/Ag/Cu rates. It allows at most three concurrent events, respects cooldowns, and makes no events without opt-in. All 15 events and their 57 rows come from `docs/EVENT_EFFECTS_04.csv`.
- **Gold jewelry.** It follows the gold price through its metal component only. Luxury demand rows move workmanship only. Gem rows move each stone once. Credit rows change new pawn terms and intent.

## 6. Jewelry business and physical recovery (handoff §9)

- **Catalog.** 167 new `sp5_*` definitions:
  - 10 loose-gem models, plus disclosed laboratory-grown and imitation variants that share the natural stone's name and art.
  - 5 settings in Au18K and Ag925 with the socket counts and stone lists from `JEWELRY_SETTINGS_04.csv`.
  - Mounted profiles, recovery materials with their category templates, lots, equipment, 6 containers, and 13 new five-page volumes.
- **Mount and demount** keep the real setting and stone IDs. Stones are `embedded` children with a socket. Nothing is spawned, wrapped or recreated, and a missing stone cannot be recovered.
- **Laboratory reports** establish gem identity. A probe can only "refer for testing".
- **Custom orders.** The deposit is `floor(price x 30%)`. It is held, excluded from lendable cash, released once at delivery, and refunded per the cancellation rule. Customer parts stay customer-owned in custody.
- **Recovery jobs.** Inputs are reserved; fee, energy and time are committed once and scaled by events for new jobs only. The output plan is saved. Every element is conserved: outputs plus residue equal inputs. A full inventory leaves the job ready, and repeated collection or a reload duplicates nothing.
- **Alloys** use only metal that is present and return the remainder. No alchemy or transmutation exists anywhere.

## 7. Footprints, art, hit targets, books (handoff §10–11)

- **Footprints.** All 421 definitions carry explicit dimensions from `art/ITEM_LAYOUT_05.csv`. The full coverage map is `art/RUNTIME_COVERAGE_05.csv`, with no default 1x1 fallbacks. Rough jade is 3x2, manuals 2x3, bracelets 2x2 and necklaces 1x3. 11 manifest rows were added for the new volumes.
- **Art.** `art/proposals/batch05/source` recomposes the models for their footprints rather than stretching them. Necklaces and bead strands hang vertically, bracelets and bangles show their opening, and books stand upright. Inspection art stays 384x256 and the reviewed ring sample is unchanged. See `docs/report-assets/rev05/inventory-footprints-05.png`.
- **Pipeline.** `source/Art/Import-Batch05.py` publishes runtime textures and footprints. Then `source/Content/Apply-Revision05.py` adds labels, components, the sp5 catalog, rules, strings, handbook crops and location cues. Both are deterministic, so a normal regeneration cannot restore old sizes or text.
- **Inventory hit targets.**
  - *Diagnosed cause (not yet observed in game):* StarPawn art is mostly transparent, and the image layers were fitted with `preserveAspect`. An alpha-tested raycast therefore hit only painted pixels.
  - *Fix:* StarPawn layers no longer alpha-test. One transparent hit plate per occupied cell sits under the item's own layer, so events bubble to the native handler.
  - *Geometry:* `FootprintGeometry` covers rotation, flip and non-rectangular shapes. Its test matrix covers 1x1, 3x2, 2x3, 2x2 and 1x3 at three UI scales, cell centres, corners and edges, and outside points.
  - Defect hotspots are untouched and still need the authored region.
  - An `SP5_HIT_DIAG` log line per item type records the native alpha threshold, so the cause can be confirmed in game.
- **Books.**
  - Manuals call the audited `BookHelper.InitBookItem`, so the native Read action applies to them.
  - Read handlers found on `BookHelper` and related helpers are routed to the StarPawn reader for StarPawn books only; the list of patched methods is logged as `SP5_READ_ROUTE`.
  - Every book, index included, is at most five pages (`Handbooks.Pages`). The opening shares page 1 with real content, and a tools-and-limits page follows.
  - Each example shows a whole-object location cue, a matched crop pair from the real defect layer, and concrete captions such as "Intact clasp".
- **Containers.** Interiors come from the manifest:
  - Organizer 8x6; twelve-book organizer 8x9.
  - Card holder 4x1; gem case 4x4; presentation case 4x3; display 6x4.
  - Child types are restricted, and there are no nested containers.

## 8. Migration and saves (handoff §14)

- **Old saves load.** `CatalogState.Unified` and the new asset fields default safely, so an 0.3.1 save loads. This is tested by stripping the new fields.
- **Shape migration** is per item and versioned (`layout05`), and resumes after interruption. An item already at the correct shape is only recorded. Otherwise it is detached, reshaped and restored. If nothing fits, it keeps its old shape in place and is listed as an overflow claim with a retry action. Nothing is deleted, overlapped or copied.
- **Pledged or serviced items** are skipped until released.
- **Not migrated by design.** Pre-05 pawn contracts keep their signed 60% LTV and rates, and 0.3 definitions keep their IDs, evidence and truth.

## 9. Reproducible in-game check route (not yet run)

Back up `Mods/starpawn` and the save folder. Use an independent slot, never the player's own save. Build with `source/Build.ps1 -GameDir …`, deploy only `mod/`, and confirm `SP_CONTENT_READY` (421 definitions, 15 events, 15 recipes) and `SP5_CAREER: registered 20` in the log. Then:

1. Native promotion: confirm the volume grant once, then reload and confirm no second grant.
2. Accept a gold commission. Inspect it; the first finding should play the exposure sound once and show "Workmanship −N". Submit the estimate.
3. Check the follow-up: sale, pawn or leave. Reload before and after the reveal; the intent must not change.
4. Sign a pawn, then redeem it, or let it default at the deadline.
5. Opt into events and compare a gold-shipment quote with the day before.
6. Mount a stone on a setting at the bench. Accept a custom order and deliver it.
7. Dismantle a console, then recover and claim with a full inventory.
8. Check that 3x2, 2x3, 2x2, 1x3 and 1x1 items select from every cell, including transparent areas, at 1080p and 1440p. Check rotated items and items inside the organizer.
9. Open a manual with Read and page through it.

Record the `SP5_*` log lines and screenshots in `docs/report-assets/rev05/`.

## 10. Known limitations and open items

- **No native price adapters.** Fuel and hands are labelled and filterable, but no adapter changes native fuel or hand prices or commission answers, so their events affect nothing yet. Adapters need the native price methods confirmed in game.
- **Follow-up conversation uses a StarPawn paper panel**, not the native customer dialogue system, whose API is not in the audit.
- **Discovered hook names are unconfirmed.** `OnEstimate`, `GiveAppraisalFee` and the Read handlers are found by name at runtime. If absent, they are skipped and logged, and the StarPawn desk remains the fallback.
- **The hit-target fix is a diagnosis plus mitigation** until `SP5_HIT_DIAG` is read in game.
- **Inventory art for mounted pieces** uses the first compatible stone for the profile texture. At runtime `JewelryArt.TextureFor` switches to the real embedded stone's art.
- **Chinese handbook headings** for new templates come from a keyword glossary. They are reasonable but should be reviewed by a native speaker.
- **Balance values** are fictional defaults covered by tests:
  - New pawn LTV 50% (0.3 used 60%).
  - Commission fee cap 60 Cr.
  - Order deposit 30%.
  - Recipe efficiencies taken from `RECIPES_04.csv`.
  - Finance rates are unchanged, and deposit rates stay below all borrowing rates, which is asserted.

## 11. Files

- **Core:** `source/Core/Unified05.cs` (valuation, market, labels, career, visits) and `source/Core/Operations05.cs` (jewelry, recovery, orders, migration, hit geometry, handbooks). `InspectionV2.cs`, `Content.cs`, `Economy.cs` and `Appraisal.cs` were updated.
- **Game:** `Native05.cs`, `Business05.cs` and `Workshop05.cs`, plus edits to the inspection desk, reader, stock filters, equipment, visuals and entry point.
- **Data:** `mod/Content/items.json`, `unified-rules.json`, `finance-rules.json`, `inspection-rules.json` and localization (zh-CN source, en overlay), plus textures.
- **Tests:** `tests/UnifiedTests.cs`, `tests/run-linux.sh`, `tests/Run-UnifiedTests.ps1` and `tests/stubs/`.
