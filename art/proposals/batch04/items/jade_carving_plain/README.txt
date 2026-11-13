Plain Jade Small Carving (jade_carving_plain)

Status: PROPOSAL, planned_04_not_registered. Not registered in the game; no runtime file was changed.
Footprint 2x2 cells -> inventory 128x128 px; inspection 384x256 px (authored 192x128, exported 2x nearest).
Current 0.3 source: none (new in 0.4). Planned 0.4 family: sp4_jade_carving_plain.

States (45):
  dou_intact
  dou_light_observation - planned light observation view, needs runtime integration
  dou_uv_observation - planned uv observation view, needs runtime integration
  dou_recess_fissure - templates: sp4_jade_carving_recess_fissure
  dou_relief_chip - templates: sp4_jade_carving_relief_chip
  dou_opaque_relief_zone - templates: sp4_jade_carving_opaque_relief_zone
  dou_opaque_relief_zone__light - templates: sp4_jade_carving_opaque_relief_zone; planned light observation view, needs runtime integration
  dou_dyed_recess - templates: sp4_jade_carving_dyed_recess
  dou_dyed_recess__uv - templates: sp4_jade_carving_dyed_recess; planned uv observation view, needs runtime integration
  dou_filled_relief_bridge - templates: sp4_jade_carving_filled_relief_bridge
  dou_filled_relief_bridge__uv - templates: sp4_jade_carving_filled_relief_bridge; planned uv observation view, needs runtime integration
  dou_combo__recess_fissure__relief_chip - templates: sp4_jade_carving_recess_fissure, sp4_jade_carving_relief_chip
  dou_combo__recess_fissure__dyed_recess - templates: sp4_jade_carving_recess_fissure, sp4_jade_carving_dyed_recess
  dou_combo__recess_fissure__filled_relief_bridge - templates: sp4_jade_carving_recess_fissure, sp4_jade_carving_filled_relief_bridge
  dou_combo__recess_fissure__relief_chip__dyed_recess - templates: sp4_jade_carving_recess_fissure, sp4_jade_carving_relief_chip, sp4_jade_carving_dyed_recess
  nuo_intact
  nuo_light_observation - planned light observation view, needs runtime integration
  nuo_uv_observation - planned uv observation view, needs runtime integration
  nuo_recess_fissure - templates: sp4_jade_carving_recess_fissure
  nuo_relief_chip - templates: sp4_jade_carving_relief_chip
  nuo_opaque_relief_zone - templates: sp4_jade_carving_opaque_relief_zone
  nuo_opaque_relief_zone__light - templates: sp4_jade_carving_opaque_relief_zone; planned light observation view, needs runtime integration
  nuo_dyed_recess - templates: sp4_jade_carving_dyed_recess
  nuo_dyed_recess__uv - templates: sp4_jade_carving_dyed_recess; planned uv observation view, needs runtime integration
  nuo_filled_relief_bridge - templates: sp4_jade_carving_filled_relief_bridge
  nuo_filled_relief_bridge__uv - templates: sp4_jade_carving_filled_relief_bridge; planned uv observation view, needs runtime integration
  nuo_combo__recess_fissure__relief_chip - templates: sp4_jade_carving_recess_fissure, sp4_jade_carving_relief_chip
  nuo_combo__recess_fissure__dyed_recess - templates: sp4_jade_carving_recess_fissure, sp4_jade_carving_dyed_recess
  nuo_combo__recess_fissure__filled_relief_bridge - templates: sp4_jade_carving_recess_fissure, sp4_jade_carving_filled_relief_bridge
  nuo_combo__recess_fissure__relief_chip__dyed_recess - templates: sp4_jade_carving_recess_fissure, sp4_jade_carving_relief_chip, sp4_jade_carving_dyed_recess
  bing_intact
  bing_light_observation - planned light observation view, needs runtime integration
  bing_uv_observation - planned uv observation view, needs runtime integration
  bing_recess_fissure - templates: sp4_jade_carving_recess_fissure
  bing_relief_chip - templates: sp4_jade_carving_relief_chip
  bing_opaque_relief_zone - templates: sp4_jade_carving_opaque_relief_zone
  bing_opaque_relief_zone__light - templates: sp4_jade_carving_opaque_relief_zone; planned light observation view, needs runtime integration
  bing_dyed_recess - templates: sp4_jade_carving_dyed_recess
  bing_dyed_recess__uv - templates: sp4_jade_carving_dyed_recess; planned uv observation view, needs runtime integration
  bing_filled_relief_bridge - templates: sp4_jade_carving_filled_relief_bridge
  bing_filled_relief_bridge__uv - templates: sp4_jade_carving_filled_relief_bridge; planned uv observation view, needs runtime integration
  bing_combo__recess_fissure__relief_chip - templates: sp4_jade_carving_recess_fissure, sp4_jade_carving_relief_chip
  bing_combo__recess_fissure__dyed_recess - templates: sp4_jade_carving_recess_fissure, sp4_jade_carving_dyed_recess
  bing_combo__recess_fissure__filled_relief_bridge - templates: sp4_jade_carving_recess_fissure, sp4_jade_carving_filled_relief_bridge
  bing_combo__recess_fissure__relief_chip__dyed_recess - templates: sp4_jade_carving_recess_fissure, sp4_jade_carving_relief_chip, sp4_jade_carving_dyed_recess

Files: paper/, items/, layers/paper, layers/items, hotspots.json (center-based), combinations.json, manifest.json, comparison.png, source/generate.py.

Notes and known limitations:
  - No template of this category has a repair class, so no repaired composite exists; combinations.json maps repaired_state to null.
  - Art is generated by the shared deterministic generator in art/proposals/batch04/source; edit there, not the PNGs.
  - In-game filtering at the 298x199 frame is not an integer scale and still needs validation in the game.
