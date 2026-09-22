# Burnable plants and collision validation — 22 September 2026

Tested in a separate copy of BURN_I with Unity **6000.6.0f1**, using the working project's current code, scene and repaired package settings.

## Results

- Full project script compilation passed.
- **38 collision assertions passed**: the healthy outline stays intact across all five sprites; a dynamic character remains supported until completion and falls afterward; liquid obstacles clear; overlapping permanent walls stay solid; charred remnants release collision; painted rotations/flips, reset/re-enable, Sprite/Grid/None settings and manual Composite Collider updates work.
- **55 plant runtime assertions passed**: thin-pixel registration, independent painted instances, all five stages, final disappearance, optional charred remains, water/wetness, retained damage after extinguishing, disable/re-enable cleanup, solid-backed vines, exact water payloads, actual swept water-shot contact, diagonal spread, connected pieces, gaps, rotated/flipped tiles, overlapping plants and current spritesheet mask baking.
- **103 pressure-tool assertions passed**, including Q switching, water travel/collision, finite resources, mixed liquids, scene references and actual water/flame emission in the current Level_0. The spray shader also passed the rendered-camera check. This includes the user's newly painted plant map in the scene-reference checks.
- A **1,000-piece, single-cell plant test** created no extra GameObjects during registration. On this isolated batch run, registration took 2 ms and 100 dormant simulation ticks took 2 ms. These are small-case measurements, not a frame-rate prediction for a complete level or for 1,000 simultaneously burning large plants.
- The real `plants.png` sheet baked without enabling Read/Write. The included `Example_Vine_HealthyOnly` asset was produced through the editor creation tool.
- **1,000 solid Grid-collider plants** used one Tilemap Collider 2D and only the Grid/Tilemap GameObjects. Registration took 22 ms in this batch run. Two distant plant removals refreshed 450 simulation cells out of 32,768, avoiding a scan of the space between them. These are bounded checks, not a full-level FPS benchmark.
- Runtime sprite copies preserve damaged artwork/UVs while carrying the healthy physics outline. Original damaged sprites retain their original small physics shapes; the feature does not mutate the imported assets.

The checks found and fixed an existing water edge case: when every water unit was used for cooling, a call to the liquid brush with zero units previously clamped up to one and created extra water. The impact path now deposits liquid only when a positive payload remains.

No five-stage burned artwork was supplied, so the final artistic appearance still needs your five matching sprites and a visual check in your level. The tests validate stage selection and simulation behavior, not the appearance of artwork that has not been made yet.

## Repeat the checks

Use a separate project copy. The harness creates an example asset and temporary runtime objects, opens Level_0 in Play mode, and exits Unity afterward.

1. Copy `Tools/Validation/PlantCollisionCheck.cs`, `Tools/Validation/PlantCheck.cs` and `Tools/Validation/PressureCheck.cs` into the copy's `Assets/Game/Editor` folder.
2. Launch Unity 6000.6.0f1 with `-batchmode -force-d3d11 -projectPath <short-copy-path> -executeMethod PlantCollisionCheck.Run -logFile <log-path>`.
3. Do not add `-quit`; the coroutine harness exits after the checks finish.
4. Inspect `ValidationResults/plant-collision.txt` for `ALL COLLISION CHECKS PASSED`, `ValidationResults/plants.txt` for `ALL PLANT CHECKS PASSED` and `ValidationResults/check-result.txt` for `COMPLETE`.
5. Remove the three temporary scripts and their `.meta` files from `Assets/Game/Editor` after testing. Keep the originals under `Tools/Validation`.

Graphics were enabled because the project's tutorial welcome package tried to open a window during a graphics-free run. The final successful run included normal graphics initialization.

## Installation boundaries

The original feature added vegetation scripts, editor tools and a healthy-only example tile. This collision follow-up updates the plant tile/manager/editor code, adds two collision helper files, updates the guides and includes its regression harness. Existing script/asset GUIDs are preserved. Level_0, artwork, packages and project settings are not overwritten.

Your existing painted plant map is retained. To enable solid plants, add a Tilemap Collider 2D to that map, choose the Ground physics layer and use Sprite or Grid on the plant assets. Follow `BURNABLE_PLANTS_GUIDE.md`; ordinary existing decorative tiles are not automatically converted.
