Rust Skin Jade Rough (jade_rough_rust)

Status: PROPOSAL, required_revision05_not_implemented. Not registered in the game; no runtime file was changed.
Footprint 3x2 cells -> inventory 192x128 px; inspection 384x256 px (authored 192x128, exported 2x nearest).
Current 0.3 source: sp3_rough_rust. Planned 0.4 family: sp4_jade_rough_rust.

States (47):
  sealed
  rust_ridge_split - templates: sp4_rough_rust_rust_ridge_split
  painted_rust - templates: sp4_rough_rust_painted_rust
  patched_shell - templates: sp4_rough_rust_patched_shell
  combo__rust_ridge_split__painted_rust - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_painted_rust
  combo__rust_ridge_split__patched_shell - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_patched_shell
  combo__rust_ridge_split__painted_rust__patched_shell - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_painted_rust, sp4_rough_rust_patched_shell
  uv_observation - planned uv observation view, needs runtime integration
  window_open_dou
  cut_surface_dou
  light_observation_dou - planned light observation view, needs runtime integration
  window_dou_rust_ridge_split - templates: sp4_rough_rust_rust_ridge_split
  window_dou_brown_interior_patch - templates: sp4_rough_rust_brown_interior_patch
  window_dou_cotton_sheet - templates: sp4_rough_rust_cotton_sheet
  window_dou_cotton_sheet__light - templates: sp4_rough_rust_cotton_sheet; planned light observation view, needs runtime integration
  window_dou_painted_rust - templates: sp4_rough_rust_painted_rust
  window_dou_patched_shell - templates: sp4_rough_rust_patched_shell
  window_dou_combo__rust_ridge_split__brown_interior_patch - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_brown_interior_patch
  window_dou_combo__rust_ridge_split__cotton_sheet - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_cotton_sheet
  window_dou_combo__rust_ridge_split__painted_rust - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_painted_rust
  window_dou_combo__rust_ridge_split__brown_interior_patch__painted_rust - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_brown_interior_patch, sp4_rough_rust_painted_rust
  window_open_nuo
  cut_surface_nuo
  light_observation_nuo - planned light observation view, needs runtime integration
  window_nuo_rust_ridge_split - templates: sp4_rough_rust_rust_ridge_split
  window_nuo_brown_interior_patch - templates: sp4_rough_rust_brown_interior_patch
  window_nuo_cotton_sheet - templates: sp4_rough_rust_cotton_sheet
  window_nuo_cotton_sheet__light - templates: sp4_rough_rust_cotton_sheet; planned light observation view, needs runtime integration
  window_nuo_painted_rust - templates: sp4_rough_rust_painted_rust
  window_nuo_patched_shell - templates: sp4_rough_rust_patched_shell
  window_nuo_combo__rust_ridge_split__brown_interior_patch - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_brown_interior_patch
  window_nuo_combo__rust_ridge_split__cotton_sheet - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_cotton_sheet
  window_nuo_combo__rust_ridge_split__painted_rust - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_painted_rust
  window_nuo_combo__rust_ridge_split__brown_interior_patch__painted_rust - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_brown_interior_patch, sp4_rough_rust_painted_rust
  window_open_bing
  cut_surface_bing
  light_observation_bing - planned light observation view, needs runtime integration
  window_bing_rust_ridge_split - templates: sp4_rough_rust_rust_ridge_split
  window_bing_brown_interior_patch - templates: sp4_rough_rust_brown_interior_patch
  window_bing_cotton_sheet - templates: sp4_rough_rust_cotton_sheet
  window_bing_cotton_sheet__light - templates: sp4_rough_rust_cotton_sheet; planned light observation view, needs runtime integration
  window_bing_painted_rust - templates: sp4_rough_rust_painted_rust
  window_bing_patched_shell - templates: sp4_rough_rust_patched_shell
  window_bing_combo__rust_ridge_split__brown_interior_patch - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_brown_interior_patch
  window_bing_combo__rust_ridge_split__cotton_sheet - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_cotton_sheet
  window_bing_combo__rust_ridge_split__painted_rust - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_painted_rust
  window_bing_combo__rust_ridge_split__brown_interior_patch__painted_rust - templates: sp4_rough_rust_rust_ridge_split, sp4_rough_rust_brown_interior_patch, sp4_rough_rust_painted_rust

Files: paper/, items/, layers/paper, layers/items, hotspots.json (center-based), combinations.json, manifest.json, comparison.png, source/generate.py.

Notes and known limitations:
  - Sealed exterior states carry no tier: one file serves dou, nuo and bing. Tier appears only in window_open_*, cut_surface_* and light_observation_* states.
  - Window-view templates exist only on window_* states (ViewId=window).
  - Art is generated by the shared deterministic generator in art/proposals/batch05/source; edit there, not the PNGs.
  - In-game filtering at the 298x199 frame is not an integer scale and still needs validation in the game.
