# Burnable plants validation — 22 September 2026

Tested in a separate copy of BURN_I with Unity **6000.6.0f1**, using the working project's current code, scene and repaired package settings.

## Results

- Full project script compilation passed.
- **56 plant assertions passed**: thin-pixel registration, independent painted instances, all five stages, final disappearance, optional charred remains, water/wetness, retained damage after extinguishing, disable/re-enable cleanup, solid-backed vines, exact water payloads, actual swept water-shot contact, diagonal spread, connected pieces, gaps, rotated/flipped tiles, overlapping plants and current spritesheet mask baking.
- **102 existing pressure-tool assertions passed**, including Q switching, water travel/collision, finite resources, mixed liquids, scene references and actual water/flame emission in Level_0. The spray shader also passed the rendered-camera check.
- A **1,000-piece, single-cell plant test** created no extra GameObjects during registration. On this isolated batch run, registration took 2 ms and 100 dormant simulation ticks took 2 ms. These are small-case measurements, not a frame-rate prediction for a complete level or for 1,000 simultaneously burning large plants.
- The real `plants.png` sheet baked without enabling Read/Write. The included `Example_Vine_HealthyOnly` asset was produced through the editor creation tool.

The checks found and fixed an existing water edge case: when every water unit was used for cooling, a call to the liquid brush with zero units previously clamped up to one and created extra water. The impact path now deposits liquid only when a positive payload remains.

No five-stage burned artwork was supplied, so the final artistic appearance still needs your five matching sprites and a visual check in your level. The tests validate stage selection and simulation behavior, not the appearance of artwork that has not been made yet.

## Repeat the checks

Use a separate project copy. The harness creates an example asset and temporary runtime objects, opens Level_0 in Play mode, and exits Unity afterward.

1. Copy `Tools/Validation/PlantCheck.cs` and `Tools/Validation/PressureCheck.cs` into the copy's `Assets/Game/Editor` folder.
2. Launch Unity 6000.6.0f1 with `-batchmode -force-d3d11 -projectPath <short-copy-path> -executeMethod PlantCheck.Run -logFile <log-path>`.
3. Do not add `-quit`; the coroutine harness exits after the checks finish.
4. Inspect `ValidationResults/plants.txt` for `ALL PLANT CHECKS PASSED` and `ValidationResults/check-result.txt` for `COMPLETE`.
5. Remove the two temporary scripts and their `.meta` files from `Assets/Game/Editor` after testing. Keep the originals under `Tools/Validation`.

Graphics were enabled because the project's tutorial welcome package tried to open a window during a graphics-free run. The final successful run included normal graphics initialization.

## Installation boundaries

The feature adds the vegetation scripts, editor tools, one healthy-only example tile, this documentation and the test source. It changes five existing simulation/weapon files and updates the project guide. It does not replace Level_0, any existing sprites, existing asset GUIDs, packages or project settings.

Level_0 still needs a plant Tilemap and painted BurnablePlantTile assets. Follow `BURNABLE_PLANTS_GUIDE.md`; ordinary existing decorative tiles are not automatically converted.
