# Tool upgrade and project cleanup

## Behavior changes

- Q switches between water and burning flame; release does not toggle again. Old input-action callback spelling remains supported.
- Water's default speed is four times the corresponding flame/nozzle base speed, with longer flight and twice the emission rate. It has visible airborne streaks and a finite per-shot payload.
- Water cools fire immediately on contact, spending part of its finite payload; liquid flow cannot move it away before the cooling is applied.
- Transient tool data can rebuild safely after an editor script reload.
- Spray sweeps between its previous and next position to stop at thin colliders. It ignores triggers and the player's own colliders. Fire detection samples the whole traveled segment, including at higher water speeds.
- A full probe pool waits for space instead of overwriting an active shot. No resources are spent for a shot that cannot obtain a slot.
- Flame has a tapered, moving core with widening/fading tips. Burning cells and fire lights flicker.
- Diagnostic reporting is now an explicit Tools menu command and writes inside the project Temp folder. It does not access uninitialized arrays or obsolete chunk-rendering properties.
- Debug painter pause uses P; Space remains the player's jump binding.

Movement, wall slides/jumps, aiming, camera, animation hooks, three nozzles, resource/refill APIs, water/oil/smoke/fire simulation, chunk activity, painting, existing art and levels remain present. The simulation's existing tick ordering is retained.

## Organization changes

| Old path | New path |
|---|---|
| `Assets/Scripts` | `Assets/Game/Scripts` |
| `Scripts/Lighing` | `Scripts/Lighting` |
| `Scripts/Simulator` | `Scripts/Simulation` |
| `Assets/Editor` | `Assets/Game/Editor` |
| `Assets/Prefabs` | `Assets/Game/Prefabs` |
| `Prefabs/PLayer/PLayer.prefab` | `Prefabs/Player/Player.prefab` |
| `Prefabs/WolrdSystems.prefab` | `Prefabs/WorldSystems.prefab` |
| `Assets/Scenes/Levels` | `Assets/Game/Scenes/Levels` |
| `Scenes/Sandbox_Playerr.unity` | `Scenes/Sandboxes/Sandbox_Player.unity` under Game |
| `Scenes/Sandbox_Fluids.unity` | `Scenes/Sandboxes/Sandbox_Fluids.unity` under Game |
| `Assets/Settings`, `Audio`, `Materials` | Same named folders under `Assets/Game` |
| `Assets/frictionless.physicsMaterial2D` | `Assets/Game/Settings/Physics/frictionless.physicsMaterial2D` |
| `Assets/celeste stuff` | `Assets/ThirdParty/Celeste` |
| `Assets/Art/NICO PARDO` | `Assets/ThirdParty/NicoPardo` |
| `Assets/Art/IndustrialPack` | `Assets/ThirdParty/IndustrialPack` |
| `Assets/IndustrialPack` | `Assets/Legacy/IndustrialPackCopy` |
| `Assets/_Recovery` | `Assets/Legacy/RecoveryScenes` |
| `Assets/Welcome` | `Assets/Legacy/TemplateWelcome` |
| `Settings/Lit2DSceneTemplate.scenetemplate`, `Settings/Scenes` | `Assets/Legacy/SceneTemplates` |

PressureTool2D and CellWorld2D are split into focused partial files while keeping their original main-script GUIDs and component names. Existing serialized tool fields and enum values retain their identities. `FormerlySerializedAs` preserves the older `maximumWaterPerCell` and `waterUnitsPerStamp` fields in legacy prefab data.

The CellWorld and FluidDebugController prefabs had script GUIDs that no longer matched their current classes; those two references are repaired. The older recovery scene is preserved byte-for-byte, including its pre-existing stale script link, and is documented as an archive.

The branch includes the saved Level_0 transform, Solid Mask and sorting-order edits and grass assets from the working project snapshot. No automatic changes were applied to the original project folder.

## Preservation checks

The audit maps every one of the **11,247 original files** through the folder moves and checks content hashes. No original files were dropped and no original `.meta` files were changed by the refactor. The intended edits are the tool/simulation/rendering/diagnostic code, input action name, two repaired prefab links, and the build-scene path. New source, shader, material and folders have new unique GUIDs.

Compilation and runtime verification results are recorded in [VALIDATION.md](VALIDATION.md). The asset audit checks preservation and known script links; it is not a claim that every archived sample scene has been redesigned or made production-ready.

## What remains intentionally straightforward

- No new networking, save-game system, level progression, damage model, or production HUD was introduced.
- The tank sprite helper remains manually driven. The debug overlay reads the actual tool resources.
- The simulation still draws one full texture and uses its existing cell/chunk algorithms. The cleanup does not claim a new simulation engine or large-world performance redesign.
- Imported packs and duplicate/recovery content are retained rather than deleted, so anything you still need remains available.
