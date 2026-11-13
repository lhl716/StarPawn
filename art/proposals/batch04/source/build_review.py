#!/usr/bin/env python3
"""Readable contact sheets for the visual pass (review backgrounds only)."""
import json, os
from PIL import Image, ImageDraw, ImageFont
import core
from core import B04, LAYOUT, save
F = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 10)


def first(key):
    return list(json.load(open(os.path.join(B04, "items", key, "manifest.json")))["states"])[0]


def main():
    keys = list(LAYOUT)
    # inventory: exact footprints at 1x on a 64 px cell grid, 2 cells reserved per item
    cols = 9
    sheet = Image.new("RGBA", (cols * 134 + 6, ((len(keys) + cols - 1) // cols) * 146 + 6), (38, 48, 58, 255))
    d = ImageDraw.Draw(sheet)
    for i, k in enumerate(keys):
        x, y = 6 + (i % cols) * 134, 6 + (i // cols) * 146
        for cy in range(2):
            for cx in range(2):
                d.rectangle([x + cx * 64, y + cy * 64, x + cx * 64 + 63, y + cy * 64 + 63], fill=(52, 66, 72, 255), outline=(44, 56, 62, 255))
        im = Image.open(os.path.join(B04, "items", k, "items", first(k) + ".png"))
        sheet.alpha_composite(im, (x, y))
        d.text((x, y + 130), k[:24], font=F, fill=(222, 207, 176, 255))
    save(sheet.convert("RGB"), B04, "review", "contact_inventory_1x.png")
    # inspection: first state at the real 298x199 frame
    per = 30
    for p in range(0, len(keys), per):
        chunk = keys[p:p + per]
        sh = Image.new("RGBA", (5 * 304 + 6, ((len(chunk) + 4) // 5) * 218 + 6), (38, 48, 58, 255))
        d = ImageDraw.Draw(sh)
        for i, k in enumerate(chunk):
            x, y = 6 + (i % 5) * 304, 6 + (i // 5) * 218
            card = Image.new("RGBA", (298, 199), (222, 207, 176, 255))
            card.alpha_composite(Image.open(os.path.join(B04, "items", k, "paper", first(k) + ".png")).resize((298, 199), Image.NEAREST))
            sh.paste(card, (x, y))
            d.text((x, y + 201), k, font=F, fill=(222, 207, 176, 255))
        save(sh.convert("RGB"), B04, "review", "contact_inspection_298x199_%d.png" % (p // per + 1))
    # UI components and icons
    ui = [r for r in core.read_csv("art/UI_ASSETS_04.csv") if r["kind"] != "screen_plate"]
    ims = [(r["asset_key"], Image.open(os.path.join(B04, "ui", r["asset_key"], "preview.png"))) for r in ui if int(r["height"]) <= 80]
    H = sum(im.height + 16 for _, im in ims) + 6
    sh = Image.new("RGB", (max(im.width for _, im in ims) + 12, H), (38, 48, 58))
    d = ImageDraw.Draw(sh)
    y = 6
    for k, im in ims:
        d.text((6, y), k, font=F, fill=(222, 207, 176))
        sh.paste(im, (6, y + 12))
        y += im.height + 16
    save(sh, B04, "review", "contact_ui_components.png")
    # screens: default skins with mock content at 1x
    import build_ui
    sc = [r["asset_key"] for r in core.read_csv("art/UI_ASSETS_04.csv") if r["kind"] == "screen_plate" and r["asset_key"] != "screen_handbook"]
    sh = Image.new("RGB", (3 * 606 + 6, ((len(sc) + 2) // 3) * 406 + 6), (38, 48, 58))
    for i, k in enumerate(sc):
        sh.paste(build_ui.screen_mock(k)[0].convert("RGB"), (6 + (i % 3) * 606, 6 + (i // 3) * 406))
    save(sh, B04, "review", "contact_screens_1x.png")


if __name__ == "__main__":
    main()


def index():
    ui = core.read_csv("art/UI_ASSETS_04.csv")
    L = ["# StarPawn batch 04 art proposal (contract revision 04.2)", "",
         "Status: **proposal only**. Nothing here is registered or loaded by the game. No file under `mod/`, `source/` or the design contracts was changed, and this branch must not be merged into `main` by the art owner.", "",
         "Everything is produced by the deterministic generator in `source/` (`python3 source/build_all.py`). `validation-results.txt` holds the last technical run.", "",
         "## What is complete", "",
         "- **135 / 135 physical art rows** of `art/ITEM_LAYOUT_04.csv`, each in `items/<asset_key>/` with `paper/` (384x256), `items/` (grid x 64 px), `layers/`, `hotspots.json` (centre-based), `combinations.json`, `manifest.json`, `comparison.png`, `README.txt` and `source/generate.py`.",
         "- **250 / 250 category templates** from `docs/DEFECT_CATALOG_04.csv` drawn on every model of their category (two designs per metal and finished-jade category), with single-defect layers, whitelisted combinations and repaired composites.",
         "- **Jewelry business**: ten loose gems, five mounted settings in Au18K and Ag925 with every stone listed in `docs/JEWELRY_SETTINGS_04.csv`, five empty settings, bench, toolkit, gem case, presentation case, display tray, report and job ticket. `items/jewelry_*/stone_bindings.json` maps each stone layer to its socket.",
         "- **71 / 71 UI rows** of `art/UI_ASSETS_04.csv` in `ui/<asset_key>/`: text-free skins for every state, `spec.json` anchors, and for the 27 screens a 1920x1080 and a 3840x2160 annotated mockup.",
         "- **Handbooks**: 160 portrait pages (440x616) in `items/manual_*/pages/`, indexed by `handbook-pages.json` (root, plus one per manual). Each of the 250 templates has one example cropped from the example item's real layers.",
         "- **Review material** in `review/`: inventory and inspection contact sheets, component and screen sheets, and `native_handbook_comparison_2560x1440.png` beside the supplied native screenshot.",
         "- The approved ring is preserved: `items/gold_ring_orbit` intact art is pixel-identical to `art/proposals/gold_ring_style_sample`. That sample's hotspot exporter was corrected to centre-based U/V; its PNGs are unchanged.", "",
         "## Integration-only (art exists, game work needed)", "",
         "- All 0.4 footprints, templates, observation views (`uv`, `light`), new containers, jewelry assemblies, events and screens are planned contracts. Every screen mockup is marked *awaiting runtime integration*.",
         "- `__uv` / `__light` states and `*_observation` states are proposed instrument layers; the runtime today has only `front` and `window`.",
         "- Legacy rows keep their 0.3 IDs but their hotspot centres moved with the new drawings; adopt texture and coordinates together.",
         "- Chinese text on mockups and handbook pages is placeholder copy (headings come from a small glossary). Real text comes from the localization keys in `handbook-pages.json`.", "",
         "## Not done / not verified", "",
         "- Nothing was run in the game: filtering at the 298x199 frame, fonts, hit-testing, container grids and save migration are unverified.",
         "- The art is generator-drawn. It is consistent and readable, but individual models have not had hand polish, and several small inventory defects are only a few pixels.",
         "- No extra hand-authored instrument views beyond the uv/light layers and the reading-example diagrams for instrument-only templates.", "",
         "## Specification points that need a decision", "",
         "| Rows | Issue | What this batch did | Proposed resolution |", "| --- | --- | --- | --- |",
         "| `sp4_jewelry_pendant_missing_insert`, `sp4_jewelry_earrings_missing_right`, `sp4_jewelry_bracelet_missing_left`, `sp4_jewelry_necklace_missing_drop` | Catalog says `state_mask`, but the stone sits inside a closed bezel, so removing it does not change the silhouette. | Stone layer removed, seat shown, alpha identical. | Treat `state_mask` as permitted-not-required, or relabel these four `same_alpha`. |",
         "| `jade_rough_*` sealed view | Each family has only two front-view templates (water has three), so the sealed exterior cannot offer two pairs plus a triple. | Sealed view gets the valid pair(s); full pair/triple sets exist on the window-open states. | Accept as the documented exception the rules allow. |",
         "| `sp4_gem_pearl_drill_chip` on `gem_pearl_baroque` | Catalog says an undrilled profile excludes this template. | Both pearl models are drawn drilled so all five templates apply. | Confirm, or declare baroque undrilled and drop the template from that model. |",
         "| `jade_pendant/cabochon/bangle/beads/carving`, all `gem_*` rows (`required_states` lists `repaired` for jade) | No template in these sets has a repair class. | No repaired composite; `combinations.json` maps `repaired_state` to null. | Remove `repaired` from those rows or add a repairable template. |",
         "| Templates with `instrument_kind` uv / jade_light and `mask_policy=same_alpha` | The cue is 'visible evidence' but also instrument-gated. | Ordinary view shows a restrained cue; the strong response is in the planned `__uv` / `__light` layer. | Confirm, or switch the ordinary view to unchanged. |",
         "| `screen_handbook` at 1080p | A 440x616 sheet cannot be integer-scaled above x1 in 1080 px height. | 1080p mockup uses x1.5 nearest (uneven pixels, stated on the mockup); 4K uses x3; the 1440p comparison uses x2. | Pick x1 or x1.5 for 1080p at integration. |",
         "| Weld/reshape repairs | Rules mention a documented seam after repair. | Repaired composites simply remove the repaired cause; the category's separate service-history template is not added automatically. | Gameplay owner decides whether a weld repair should add the category's join template. |",
         "", "## Physical rows", "", "| Asset | Role | Cells | States | Hotspots | Multi-defect profiles | Sheet |", "| --- | --- | --- | --- | --- | --- | --- |"]
    for k, r in LAYOUT.items():
        man = json.load(open(os.path.join(B04, "items", k, "manifest.json")))
        hs = json.load(open(os.path.join(B04, "items", k, "hotspots.json")))["hotspots"]
        cb = json.load(open(os.path.join(B04, "items", k, "combinations.json")))["combinations"]
        L.append("| [%s](items/%s/README.txt) | %s | %sx%s | %d | %d | %d | [sheet](items/%s/comparison.png) |" % (
            k, k, r["role"], r["grid_width"], r["grid_height"], len(man["states"]), len(hs), len([c for c in cb if len(c["templates"]) > 1]), k))
    L += ["", "## UI rows", "", "| Asset | Kind | Size | States | Preview |", "| --- | --- | --- | --- | --- |"]
    for r in ui:
        L.append("| [%s](ui/%s/README.txt) | %s | %sx%s | %s | [preview](ui/%s/preview.png)%s |" % (
            r["asset_key"], r["asset_key"], r["kind"], r["width"], r["height"], r["required_states"].replace(";", ", "), r["asset_key"],
            " / [1080p](ui/%s/mockup_1080p.png) / [4K](ui/%s/mockup_4k.png)" % (r["asset_key"], r["asset_key"]) if r["kind"] == "screen_plate" else ""))
    core.text("\n".join(L), B04, "README.md")


if __name__ == "__main__":
    index()
