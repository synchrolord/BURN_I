# Basic cell simulation

The Simulation folder is reduced from **2,409 to 1,185 C# lines** across the same 12 files, including CellWorldDebugPainter. The largest remaining file is 197 lines. The module includes all matching scripts and their original `.meta` files. `CellWorld2D.Materials.cs` (197 lines) and `CellWorld2D.Vegetation.cs` (135 lines) stay exactly as before. No files are deleted or renamed.

This is a real rule simplification: `Liquids` drops from 655 to 72 lines and `Buoyancy` from 338 to 23. The saved Level_0 remains one finite 900×600 CellWorld. There is no new zone registry, room streaming, manager hierarchy, or per-cell GameObject.

## How to read it

`partial` means the files contribute to one class, not separate components. Attach CellWorld2D once. Start with `CellWorld2D.cs`: it holds Inspector settings and one simulation loop. `Grid` allocates the flat arrays and converts positions. Each array index is `x + y * width`.

The loop visits awake chunks, then cells inside each chunk. For each cell it performs a small adjacent oil/water exchange, liquid movement, smoke movement, and fire. Alternating left/right and water/fuel order avoids a permanent directional preference. Movement flags prevent newly received material moving again in that tick. New fire is queued until the loop finishes so one fire cannot cross an entire plant in one tick.

`Liquids`, `Buoyancy`, `Smoke` and `Fire` contain those short rules. `Chunks` skips settled areas; deposits and movement wake neighboring chunks. `Collision` samples solids and refreshes only the affected area when plants disappear. `Vegetation` keeps the existing accurate per-pixel plant registration, independent damage, wet time and overlapping-plant bookkeeping. `Rendering` draws one texture and retains the current fire colors/flicker. FireLightPool2D still receives weighted fire centers from `GetBrightestFireClusters`.

## Intentional differences from Advanced

- Liquids try down, both lower diagonals, then equalize with the two immediate side neighbors. They no longer search for distant drops or push a row of liquid toward one. Wide pools therefore level more slowly and locally. Diagonal movement requires the neighboring side cell to be open, preventing leakage through wall corners.
- Water and fuel share each cell's capacity. Basic viscosity limits how much transfers to each neighbor. Water/fuel amounts are conserved during movement.
- Buoyancy only swaps equal amounts of fuel below and water immediately above, every other tick. It has no long-distance displacement or lateral separation rules.
- `waterSpreadSearchDistance`, `waterSidewaysPasses`, `fuelSpreadSearchDistance` and `fuelSidewaysPasses` remain serialized but are hidden and ignored by Basic. Their saved values are retained for switching back to Advanced.
- Smoke rises at most one cell per tick, may move sideways once, and fades at its configured interval. Trapped smoke keeps its chunk awake until it fades, so it cannot become permanently frozen by sleeping.
- Smoke and fire run **once** per tick. The duplicated smoke/fire passes in the original main loop are removed. Liquid fuel consumption and fire spreading/decay now follow their Inspector tick intervals once, instead of some work happening twice. Existing plant burn duration remains time based and is still counted once per tick.
- The single cell loop replaces separate whole-grid material passes. It is deliberately a basic cellular simulation; exact fluid patterns/timing differ from Advanced.
- Adding zero or negative liquid units now returns zero rather than silently creating one unit.

## Preserved connections

There are **no public or internal API changes**. Current tool, player, lighting, plant, collision and editor callers retain their names/signatures: initialization/dimension/pause properties; coordinate conversion; AddWater/AddFuel/AddSmoke/Ignite; ApplyWaterImpact/ApplyPlantWaterImpact; ExtractWater/EraseCircle; water fraction/fire energy; total counts, chunk count, clearing, stepping and rebaking; plant registration/unregistration; local collision refresh and diagnostics; fire cluster sampling; and the internal externally-managed tick hooks.

Script paths, class names, original GUIDs and serialized setting names are preserved. No scene, material, imported sprite, baked plant mask, prefab or rendering asset is changed by this simulation patch. Burning plants retain their previous damage artwork and collision behavior through the existing plant manager.

## Limits and tuning

Tune viscosity, per-cell capacity, fire intervals, smoke decay, tick rate and chunk settings in the Inspector. Width/height/cell size are initialization settings: stop Play before editing them. Startup accepts dimensions 16–2048, up to 1,048,576 total cells, with a finite cell size of at least 0.01. It reports invalid grids rather than silently resizing the authored scene. The existing 900×600 level fits. Invalid grids must be corrected before plant registration can finish.

Chunk timers remain important for this size. Keep Simulate All Chunks off for normal play and assign Activity Target to the player as before. Awake timers have a minimum of two ticks so a newly touched chunk survives the timer-aging step. One full texture is still allocated/uploaded when material visuals change; this is not an unlimited-world solution. Physics solids are baked at startup and locally updated for changing plants, as before.

## Verification

See the project `Docs/BASIC_EDITION_VALIDATION.md` for the completed compilation and runtime checks. Use the script switcher to replace this whole module.
