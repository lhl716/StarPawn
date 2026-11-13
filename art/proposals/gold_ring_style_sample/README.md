# Gold ring style sample (Stage 1 proposal)

Status: **awaiting style approval**. Nothing here is wired into the game. No
files under `mod/` or `source/` were changed, and no runtime image was replaced.

One original gold band ("orbit-rail" groove, no brand marks) in three states:

| State | Maps to template | Inspection (384 x 256) | Inventory (64 x 64) |
| --- | --- | --- | --- |
| Intact | none | `gold_ring_intact_paper_384x256.png` | `gold_ring_intact_items_64x64.png` |
| Worn plating, base metal exposed | `metal_plating` | `gold_ring_worn_plating_paper_384x256.png` | `gold_ring_worn_plating_items_64x64.png` |
| Cracked band | `metal_split` | `gold_ring_cracked_band_paper_384x256.png` | `gold_ring_cracked_band_items_64x64.png` |

`gold_ring_states_comparison.png` shows the three side by side on warm paper,
with each inventory image at 3x and 1x on a dark slot and 1x on paper. The
backdrops exist only in that sheet; every other PNG is transparent.

## What the sample proposes

- **Pixel scale.** The inspection image is authored at 192 x 128 and exported
  at 2x nearest-neighbour to the 384 x 256 master, so one art pixel is 2 x 2
  master pixels. The ring fills about 54% of the frame width.
- **Inventory image.** Redrawn at 64 x 64 from the same model, not downscaled:
  thicker band, one-pixel groove, simplified wear patch, shortened crack.
- **Palette.** 11 colours: ink blue outline, six golds, four gray-green
  base-metal tones. No anti-aliasing, no dithering, no partial alpha.
- **Lighting.** Upper left. The brightest tone is limited to one streak on the
  outer wall and one glint on the inner rim.
- **Alignment.** The three states share one mask per size; the generator
  asserts identical alpha and that every defect pixel is inside the mask and
  off the outline, so the silhouette cannot change.
- **Defects are fixed templates.** No random numbers. Exposed base metal reuses
  the gold shading in gray-green, so only the material changes. The crack is a
  fixed ink zigzag with a lit lip.

## Layers

- `layers/gold_ring_{paper,items}_mask_*.png` - object mask.
- `layers/gold_ring_{paper,items}_overlay_{worn_plating,cracked_band}_*.png` -
  transparent defect overlays; composited over the intact image they reproduce
  the state images exactly.
- `hotspots.json` - proposed U/V/Width/Height (0..1, top-left origin) for the
  two defects, using the `ViewId` / `InstrumentKind` fields.
- `source/make_gold_ring_sample.py` - regenerates everything in this folder.

## Decisions needed at review

1. Pixel scale: 2x art pixels in the 384 x 256 master (as shown), or 1x.
2. Inventory size: this sample follows the 64 x 64 in `ART_DIRECTION.txt`, but
   the current `sp3_gold_ring_0_*` Items textures are 96 x 64.
3. Hotspot slots: defects sit where they read best on this camera (plating
   front-centre, crack front-right). They do not match the placeholder slots in
   `runtime-art-map.json`; the gameplay branch would need to adopt these.
4. Base-metal colour: gray-green is used for contrast with gold at small sizes.
