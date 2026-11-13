Mountain Rough Jade Rough (jade_rough_mountain)

Status: PROPOSAL, planned_04_not_registered. Not registered in the game; no runtime file was changed.
Footprint 2x2 cells -> inventory 128x128 px; inspection 384x256 px (authored 192x128, exported 2x nearest).
Current 0.3 source: sp3_rough_mountain. Planned 0.4 family: sp4_jade_rough_mountain.

States (51):
  sealed
  open_mountain_fissure - templates: sp4_rough_mountain_open_mountain_fissure
  filled_shell_seam - templates: sp4_rough_mountain_filled_shell_seam
  filled_shell_seam__uv - templates: sp4_rough_mountain_filled_shell_seam; planned uv observation view, needs runtime integration
  combo__open_mountain_fissure__filled_shell_seam - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_filled_shell_seam
  uv_observation - planned uv observation view, needs runtime integration
  window_open_dou
  cut_surface_dou
  light_observation_dou - planned light observation view, needs runtime integration
  window_dou_open_mountain_fissure - templates: sp4_rough_mountain_open_mountain_fissure
  window_dou_coarse_interior - templates: sp4_rough_mountain_coarse_interior
  window_dou_black_band - templates: sp4_rough_mountain_black_band
  window_dou_black_band__light - templates: sp4_rough_mountain_black_band; planned light observation view, needs runtime integration
  window_dou_filled_shell_seam - templates: sp4_rough_mountain_filled_shell_seam
  window_dou_filled_shell_seam__uv - templates: sp4_rough_mountain_filled_shell_seam; planned uv observation view, needs runtime integration
  window_dou_colored_window - templates: sp4_rough_mountain_colored_window
  window_dou_colored_window__uv - templates: sp4_rough_mountain_colored_window; planned uv observation view, needs runtime integration
  window_dou_combo__open_mountain_fissure__coarse_interior - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_coarse_interior
  window_dou_combo__open_mountain_fissure__black_band - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_black_band
  window_dou_combo__open_mountain_fissure__filled_shell_seam - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_filled_shell_seam
  window_dou_combo__open_mountain_fissure__coarse_interior__filled_shell_seam - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_coarse_interior, sp4_rough_mountain_filled_shell_seam
  window_open_nuo
  cut_surface_nuo
  light_observation_nuo - planned light observation view, needs runtime integration
  window_nuo_open_mountain_fissure - templates: sp4_rough_mountain_open_mountain_fissure
  window_nuo_coarse_interior - templates: sp4_rough_mountain_coarse_interior
  window_nuo_black_band - templates: sp4_rough_mountain_black_band
  window_nuo_black_band__light - templates: sp4_rough_mountain_black_band; planned light observation view, needs runtime integration
  window_nuo_filled_shell_seam - templates: sp4_rough_mountain_filled_shell_seam
  window_nuo_filled_shell_seam__uv - templates: sp4_rough_mountain_filled_shell_seam; planned uv observation view, needs runtime integration
  window_nuo_colored_window - templates: sp4_rough_mountain_colored_window
  window_nuo_colored_window__uv - templates: sp4_rough_mountain_colored_window; planned uv observation view, needs runtime integration
  window_nuo_combo__open_mountain_fissure__coarse_interior - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_coarse_interior
  window_nuo_combo__open_mountain_fissure__black_band - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_black_band
  window_nuo_combo__open_mountain_fissure__filled_shell_seam - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_filled_shell_seam
  window_nuo_combo__open_mountain_fissure__coarse_interior__filled_shell_seam - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_coarse_interior, sp4_rough_mountain_filled_shell_seam
  window_open_bing
  cut_surface_bing
  light_observation_bing - planned light observation view, needs runtime integration
  window_bing_open_mountain_fissure - templates: sp4_rough_mountain_open_mountain_fissure
  window_bing_coarse_interior - templates: sp4_rough_mountain_coarse_interior
  window_bing_black_band - templates: sp4_rough_mountain_black_band
  window_bing_black_band__light - templates: sp4_rough_mountain_black_band; planned light observation view, needs runtime integration
  window_bing_filled_shell_seam - templates: sp4_rough_mountain_filled_shell_seam
  window_bing_filled_shell_seam__uv - templates: sp4_rough_mountain_filled_shell_seam; planned uv observation view, needs runtime integration
  window_bing_colored_window - templates: sp4_rough_mountain_colored_window
  window_bing_colored_window__uv - templates: sp4_rough_mountain_colored_window; planned uv observation view, needs runtime integration
  window_bing_combo__open_mountain_fissure__coarse_interior - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_coarse_interior
  window_bing_combo__open_mountain_fissure__black_band - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_black_band
  window_bing_combo__open_mountain_fissure__filled_shell_seam - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_filled_shell_seam
  window_bing_combo__open_mountain_fissure__coarse_interior__filled_shell_seam - templates: sp4_rough_mountain_open_mountain_fissure, sp4_rough_mountain_coarse_interior, sp4_rough_mountain_filled_shell_seam

Files: paper/, items/, layers/paper, layers/items, hotspots.json (center-based), combinations.json, manifest.json, comparison.png, source/generate.py.

Notes and known limitations:
  - Sealed exterior states carry no tier: one file serves dou, nuo and bing. Tier appears only in window_open_*, cut_surface_* and light_observation_* states.
  - Window-view templates exist only on window_* states (ViewId=window).
  - Art is generated by the shared deterministic generator in art/proposals/batch04/source; edit there, not the PNGs.
  - In-game filtering at the 298x199 frame is not an integer scale and still needs validation in the game.
