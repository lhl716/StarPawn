Plain Jade Cabochon (jade_cabochon_plain)

Status: PROPOSAL, required_revision05_not_implemented. Not registered in the game; no runtime file was changed.
Footprint 1x1 cells -> inventory 64x64 px; inspection 384x256 px (authored 192x128, exported 2x nearest).
Current 0.3 source: sp3_jade_cabochon. Planned 0.4 family: sp4_jade_cabochon_plain.

States (45):
  dou_intact
  dou_light_observation - planned light observation view, needs runtime integration
  dou_uv_observation - planned uv observation view, needs runtime integration
  dou_dome_fissure - templates: sp4_jade_cabochon_dome_fissure
  dou_girdle_chip - templates: sp4_jade_cabochon_girdle_chip
  dou_central_inclusion - templates: sp4_jade_cabochon_central_inclusion
  dou_central_inclusion__light - templates: sp4_jade_cabochon_central_inclusion; planned light observation view, needs runtime integration
  dou_dyed_grain_web - templates: sp4_jade_cabochon_dyed_grain_web
  dou_dyed_grain_web__uv - templates: sp4_jade_cabochon_dyed_grain_web; planned uv observation view, needs runtime integration
  dou_filled_dome_seam - templates: sp4_jade_cabochon_filled_dome_seam
  dou_filled_dome_seam__uv - templates: sp4_jade_cabochon_filled_dome_seam; planned uv observation view, needs runtime integration
  dou_combo__dome_fissure__girdle_chip - templates: sp4_jade_cabochon_dome_fissure, sp4_jade_cabochon_girdle_chip
  dou_combo__dome_fissure__central_inclusion - templates: sp4_jade_cabochon_dome_fissure, sp4_jade_cabochon_central_inclusion
  dou_combo__dome_fissure__dyed_grain_web - templates: sp4_jade_cabochon_dome_fissure, sp4_jade_cabochon_dyed_grain_web
  dou_combo__dome_fissure__girdle_chip__central_inclusion - templates: sp4_jade_cabochon_dome_fissure, sp4_jade_cabochon_girdle_chip, sp4_jade_cabochon_central_inclusion
  nuo_intact
  nuo_light_observation - planned light observation view, needs runtime integration
  nuo_uv_observation - planned uv observation view, needs runtime integration
  nuo_dome_fissure - templates: sp4_jade_cabochon_dome_fissure
  nuo_girdle_chip - templates: sp4_jade_cabochon_girdle_chip
  nuo_central_inclusion - templates: sp4_jade_cabochon_central_inclusion
  nuo_central_inclusion__light - templates: sp4_jade_cabochon_central_inclusion; planned light observation view, needs runtime integration
  nuo_dyed_grain_web - templates: sp4_jade_cabochon_dyed_grain_web
  nuo_dyed_grain_web__uv - templates: sp4_jade_cabochon_dyed_grain_web; planned uv observation view, needs runtime integration
  nuo_filled_dome_seam - templates: sp4_jade_cabochon_filled_dome_seam
  nuo_filled_dome_seam__uv - templates: sp4_jade_cabochon_filled_dome_seam; planned uv observation view, needs runtime integration
  nuo_combo__dome_fissure__girdle_chip - templates: sp4_jade_cabochon_dome_fissure, sp4_jade_cabochon_girdle_chip
  nuo_combo__dome_fissure__central_inclusion - templates: sp4_jade_cabochon_dome_fissure, sp4_jade_cabochon_central_inclusion
  nuo_combo__dome_fissure__dyed_grain_web - templates: sp4_jade_cabochon_dome_fissure, sp4_jade_cabochon_dyed_grain_web
  nuo_combo__dome_fissure__girdle_chip__central_inclusion - templates: sp4_jade_cabochon_dome_fissure, sp4_jade_cabochon_girdle_chip, sp4_jade_cabochon_central_inclusion
  bing_intact
  bing_light_observation - planned light observation view, needs runtime integration
  bing_uv_observation - planned uv observation view, needs runtime integration
  bing_dome_fissure - templates: sp4_jade_cabochon_dome_fissure
  bing_girdle_chip - templates: sp4_jade_cabochon_girdle_chip
  bing_central_inclusion - templates: sp4_jade_cabochon_central_inclusion
  bing_central_inclusion__light - templates: sp4_jade_cabochon_central_inclusion; planned light observation view, needs runtime integration
  bing_dyed_grain_web - templates: sp4_jade_cabochon_dyed_grain_web
  bing_dyed_grain_web__uv - templates: sp4_jade_cabochon_dyed_grain_web; planned uv observation view, needs runtime integration
  bing_filled_dome_seam - templates: sp4_jade_cabochon_filled_dome_seam
  bing_filled_dome_seam__uv - templates: sp4_jade_cabochon_filled_dome_seam; planned uv observation view, needs runtime integration
  bing_combo__dome_fissure__girdle_chip - templates: sp4_jade_cabochon_dome_fissure, sp4_jade_cabochon_girdle_chip
  bing_combo__dome_fissure__central_inclusion - templates: sp4_jade_cabochon_dome_fissure, sp4_jade_cabochon_central_inclusion
  bing_combo__dome_fissure__dyed_grain_web - templates: sp4_jade_cabochon_dome_fissure, sp4_jade_cabochon_dyed_grain_web
  bing_combo__dome_fissure__girdle_chip__central_inclusion - templates: sp4_jade_cabochon_dome_fissure, sp4_jade_cabochon_girdle_chip, sp4_jade_cabochon_central_inclusion

Files: paper/, items/, layers/paper, layers/items, hotspots.json (center-based), combinations.json, manifest.json, comparison.png, source/generate.py.

Notes and known limitations:
  - No template of this category has a repair class, so no repaired composite exists; combinations.json maps repaired_state to null.
  - Art is generated by the shared deterministic generator in art/proposals/batch05/source; edit there, not the PNGs.
  - In-game filtering at the 298x199 frame is not an integer scale and still needs validation in the game.
