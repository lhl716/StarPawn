Yellow Sand Skin Jade Rough (jade_rough_yellow)

Status: PROPOSAL, required_revision05_not_implemented. Not registered in the game; no runtime file was changed.
Footprint 3x2 cells -> inventory 192x128 px; inspection 384x256 px (authored 192x128, exported 2x nearest).
Current 0.3 source: sp3_rough_yellow. Planned 0.4 family: sp4_jade_rough_yellow.

States (48):
  sealed
  shell_fissure - templates: sp4_rough_yellow_shell_fissure
  dyed_sand - templates: sp4_rough_yellow_dyed_sand
  dyed_sand__uv - templates: sp4_rough_yellow_dyed_sand; planned uv observation view, needs runtime integration
  combo__shell_fissure__dyed_sand - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_dyed_sand
  uv_observation - planned uv observation view, needs runtime integration
  window_open_dou
  cut_surface_dou
  light_observation_dou - planned light observation view, needs runtime integration
  window_dou_shell_fissure - templates: sp4_rough_yellow_shell_fissure
  window_dou_chalk_cloud - templates: sp4_rough_yellow_chalk_cloud
  window_dou_chalk_cloud__light - templates: sp4_rough_yellow_chalk_cloud; planned light observation view, needs runtime integration
  window_dou_black_grain_pocket - templates: sp4_rough_yellow_black_grain_pocket
  window_dou_dyed_sand - templates: sp4_rough_yellow_dyed_sand
  window_dou_dyed_sand__uv - templates: sp4_rough_yellow_dyed_sand; planned uv observation view, needs runtime integration
  window_dou_window_inlay - templates: sp4_rough_yellow_window_inlay
  window_dou_combo__shell_fissure__chalk_cloud - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_chalk_cloud
  window_dou_combo__shell_fissure__black_grain_pocket - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_black_grain_pocket
  window_dou_combo__shell_fissure__dyed_sand - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_dyed_sand
  window_dou_combo__shell_fissure__chalk_cloud__dyed_sand - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_chalk_cloud, sp4_rough_yellow_dyed_sand
  window_open_nuo
  cut_surface_nuo
  light_observation_nuo - planned light observation view, needs runtime integration
  window_nuo_shell_fissure - templates: sp4_rough_yellow_shell_fissure
  window_nuo_chalk_cloud - templates: sp4_rough_yellow_chalk_cloud
  window_nuo_chalk_cloud__light - templates: sp4_rough_yellow_chalk_cloud; planned light observation view, needs runtime integration
  window_nuo_black_grain_pocket - templates: sp4_rough_yellow_black_grain_pocket
  window_nuo_dyed_sand - templates: sp4_rough_yellow_dyed_sand
  window_nuo_dyed_sand__uv - templates: sp4_rough_yellow_dyed_sand; planned uv observation view, needs runtime integration
  window_nuo_window_inlay - templates: sp4_rough_yellow_window_inlay
  window_nuo_combo__shell_fissure__chalk_cloud - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_chalk_cloud
  window_nuo_combo__shell_fissure__black_grain_pocket - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_black_grain_pocket
  window_nuo_combo__shell_fissure__dyed_sand - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_dyed_sand
  window_nuo_combo__shell_fissure__chalk_cloud__dyed_sand - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_chalk_cloud, sp4_rough_yellow_dyed_sand
  window_open_bing
  cut_surface_bing
  light_observation_bing - planned light observation view, needs runtime integration
  window_bing_shell_fissure - templates: sp4_rough_yellow_shell_fissure
  window_bing_chalk_cloud - templates: sp4_rough_yellow_chalk_cloud
  window_bing_chalk_cloud__light - templates: sp4_rough_yellow_chalk_cloud; planned light observation view, needs runtime integration
  window_bing_black_grain_pocket - templates: sp4_rough_yellow_black_grain_pocket
  window_bing_dyed_sand - templates: sp4_rough_yellow_dyed_sand
  window_bing_dyed_sand__uv - templates: sp4_rough_yellow_dyed_sand; planned uv observation view, needs runtime integration
  window_bing_window_inlay - templates: sp4_rough_yellow_window_inlay
  window_bing_combo__shell_fissure__chalk_cloud - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_chalk_cloud
  window_bing_combo__shell_fissure__black_grain_pocket - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_black_grain_pocket
  window_bing_combo__shell_fissure__dyed_sand - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_dyed_sand
  window_bing_combo__shell_fissure__chalk_cloud__dyed_sand - templates: sp4_rough_yellow_shell_fissure, sp4_rough_yellow_chalk_cloud, sp4_rough_yellow_dyed_sand

Files: paper/, items/, layers/paper, layers/items, hotspots.json (center-based), combinations.json, manifest.json, comparison.png, source/generate.py.

Notes and known limitations:
  - Sealed exterior states carry no tier: one file serves dou, nuo and bing. Tier appears only in window_open_*, cut_surface_* and light_observation_* states.
  - Window-view templates exist only on window_* states (ViewId=window).
  - Art is generated by the shared deterministic generator in art/proposals/batch05/source; edit there, not the PNGs.
  - In-game filtering at the 298x199 frame is not an integer scale and still needs validation in the game.
