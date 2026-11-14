Water-Worn Jade Rough (jade_rough_water)

Status: PROPOSAL, required_revision05_not_implemented. Not registered in the game; no runtime file was changed.
Footprint 3x2 cells -> inventory 192x128 px; inspection 384x256 px (authored 192x128, exported 2x nearest).
Current 0.3 source: sp3_rough_water. Planned 0.4 family: sp4_jade_rough_water.

States (51):
  sealed
  impact_spall - templates: sp4_rough_water_impact_spall
  surface_color_wash - templates: sp4_rough_water_surface_color_wash
  surface_color_wash__uv - templates: sp4_rough_water_surface_color_wash; planned uv observation view, needs runtime integration
  joined_cap - templates: sp4_rough_water_joined_cap
  combo__impact_spall__surface_color_wash - templates: sp4_rough_water_impact_spall, sp4_rough_water_surface_color_wash
  combo__impact_spall__joined_cap - templates: sp4_rough_water_impact_spall, sp4_rough_water_joined_cap
  combo__impact_spall__surface_color_wash__joined_cap - templates: sp4_rough_water_impact_spall, sp4_rough_water_surface_color_wash, sp4_rough_water_joined_cap
  uv_observation - planned uv observation view, needs runtime integration
  window_open_dou
  cut_surface_dou
  light_observation_dou - planned light observation view, needs runtime integration
  window_dou_impact_spall - templates: sp4_rough_water_impact_spall
  window_dou_through_fracture - templates: sp4_rough_water_through_fracture
  window_dou_fibrous_cloud - templates: sp4_rough_water_fibrous_cloud
  window_dou_fibrous_cloud__light - templates: sp4_rough_water_fibrous_cloud; planned light observation view, needs runtime integration
  window_dou_surface_color_wash - templates: sp4_rough_water_surface_color_wash
  window_dou_surface_color_wash__uv - templates: sp4_rough_water_surface_color_wash; planned uv observation view, needs runtime integration
  window_dou_joined_cap - templates: sp4_rough_water_joined_cap
  window_dou_combo__impact_spall__through_fracture - templates: sp4_rough_water_impact_spall, sp4_rough_water_through_fracture
  window_dou_combo__impact_spall__fibrous_cloud - templates: sp4_rough_water_impact_spall, sp4_rough_water_fibrous_cloud
  window_dou_combo__impact_spall__surface_color_wash - templates: sp4_rough_water_impact_spall, sp4_rough_water_surface_color_wash
  window_dou_combo__impact_spall__through_fracture__surface_color_wash - templates: sp4_rough_water_impact_spall, sp4_rough_water_through_fracture, sp4_rough_water_surface_color_wash
  window_open_nuo
  cut_surface_nuo
  light_observation_nuo - planned light observation view, needs runtime integration
  window_nuo_impact_spall - templates: sp4_rough_water_impact_spall
  window_nuo_through_fracture - templates: sp4_rough_water_through_fracture
  window_nuo_fibrous_cloud - templates: sp4_rough_water_fibrous_cloud
  window_nuo_fibrous_cloud__light - templates: sp4_rough_water_fibrous_cloud; planned light observation view, needs runtime integration
  window_nuo_surface_color_wash - templates: sp4_rough_water_surface_color_wash
  window_nuo_surface_color_wash__uv - templates: sp4_rough_water_surface_color_wash; planned uv observation view, needs runtime integration
  window_nuo_joined_cap - templates: sp4_rough_water_joined_cap
  window_nuo_combo__impact_spall__through_fracture - templates: sp4_rough_water_impact_spall, sp4_rough_water_through_fracture
  window_nuo_combo__impact_spall__fibrous_cloud - templates: sp4_rough_water_impact_spall, sp4_rough_water_fibrous_cloud
  window_nuo_combo__impact_spall__surface_color_wash - templates: sp4_rough_water_impact_spall, sp4_rough_water_surface_color_wash
  window_nuo_combo__impact_spall__through_fracture__surface_color_wash - templates: sp4_rough_water_impact_spall, sp4_rough_water_through_fracture, sp4_rough_water_surface_color_wash
  window_open_bing
  cut_surface_bing
  light_observation_bing - planned light observation view, needs runtime integration
  window_bing_impact_spall - templates: sp4_rough_water_impact_spall
  window_bing_through_fracture - templates: sp4_rough_water_through_fracture
  window_bing_fibrous_cloud - templates: sp4_rough_water_fibrous_cloud
  window_bing_fibrous_cloud__light - templates: sp4_rough_water_fibrous_cloud; planned light observation view, needs runtime integration
  window_bing_surface_color_wash - templates: sp4_rough_water_surface_color_wash
  window_bing_surface_color_wash__uv - templates: sp4_rough_water_surface_color_wash; planned uv observation view, needs runtime integration
  window_bing_joined_cap - templates: sp4_rough_water_joined_cap
  window_bing_combo__impact_spall__through_fracture - templates: sp4_rough_water_impact_spall, sp4_rough_water_through_fracture
  window_bing_combo__impact_spall__fibrous_cloud - templates: sp4_rough_water_impact_spall, sp4_rough_water_fibrous_cloud
  window_bing_combo__impact_spall__surface_color_wash - templates: sp4_rough_water_impact_spall, sp4_rough_water_surface_color_wash
  window_bing_combo__impact_spall__through_fracture__surface_color_wash - templates: sp4_rough_water_impact_spall, sp4_rough_water_through_fracture, sp4_rough_water_surface_color_wash

Files: paper/, items/, layers/paper, layers/items, hotspots.json (center-based), combinations.json, manifest.json, comparison.png, source/generate.py.

Notes and known limitations:
  - Sealed exterior states carry no tier: one file serves dou, nuo and bing. Tier appears only in window_open_*, cut_surface_* and light_observation_* states.
  - Window-view templates exist only on window_* states (ViewId=window).
  - Art is generated by the shared deterministic generator in art/proposals/batch05/source; edit there, not the PNGs.
  - In-game filtering at the 298x199 frame is not an integer scale and still needs validation in the game.
