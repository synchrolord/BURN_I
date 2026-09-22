# BURN_I — project and modification guide

## What this project is

BURN_I is a single-player 2D platformer prototype with a small material simulation. The character uses normal Unity 2D physics. The water, oil, fire, and smoke use a separate grid of cells drawn as a texture. The tool connects those two systems: it launches moving spray probes, checks their paths against colliders, and adds material to the grid.

An airborne probe is a small C# data object, not a separate GameObject. A pooled mesh draws the flying spray. This makes fast water visible before it lands, and lets the flame plume bend and spread as it rises.

Use Unity **6000.6.0f1**, the version recorded in `ProjectSettings/ProjectVersion.txt`. The project uses the Universal Render Pipeline's 2D renderer and the Input System package. Keep `Packages/manifest.json` and `packages-lock.json` together when sharing changes.

## Start here

For burnable vines, connectors and plant artwork, see [BURNABLE_PLANTS_GUIDE.md](BURNABLE_PLANTS_GUIDE.md). It covers the five sprite stages, Tile Palette setup, stationary fire spread, water and performance.

1. Open `Assets/Game/Scenes/Levels/Level_0.unity`.
2. Press Play and click the Game view so it receives keyboard and mouse input.
3. Move with A/D, jump with Space, and aim with the mouse. The input asset also contains W/S, but the motor currently reads only horizontal movement.
4. Hold the left mouse button to spray. Tap Q to change between water and burning flame. Scroll to change nozzle.
5. Select the player and inspect `PressureTool2D` to tune the tool. Stop Play before making changes you want to keep. Normal changes to scene objects during Play are discarded when Play ends.

The small tool overlay shows the selected mode, nozzle, and resource counts. `Show Debug Overlay` controls it. With `Unlimited Resources For Testing` enabled, the overlay displays infinite supplies; disable that setting to test depletion and refills.

## Where things live

| Folder | Contents and purpose |
|---|---|
| `Assets/Game/Scenes/Levels` | Level_0 and Level_1; the playable level scenes. |
| `Assets/Game/Scenes/Sandboxes` | Focused player and fluid experiments. |
| `Assets/Game/Prefabs` | Reusable CellWorld, FluidDebugController, WorldSystems, and Player objects. |
| `Assets/Game/Scripts/Player` | Movement, aiming, camera, animation helpers, old spray preview. |
| `Assets/Game/Scripts/Weapons` | Pressure tool input, resources, spray motion, and appearance. |
| `Assets/Game/Scripts/Simulation` | Grid, liquids, buoyancy, fire, smoke, rendering, debug painting. |
| `Assets/Game/Scripts/Vegetation` | Burnable plant tile definitions and one manager per plant Tilemap. |
| `Assets/Game/Scripts/Lighting` | Reusable lights that follow burning regions. |
| `Assets/Game/Scripts/UI` | Tank sprite display. |
| `Assets/Game/Editor` | Editor-only diagnostics; excluded from player builds. |
| `Assets/Game/Settings` | Input actions, render pipeline assets, and physics material. |
| `Assets/Game/Materials`, `Shaders`, `Audio` | Game-owned rendering and audio assets. |
| `Assets/Resources/Effects` | Spray material loaded by the tool at runtime. Keep its path unchanged unless you also change the load call. |
| `Assets/Art` | Project animation and tile assets, including grass tiles. |
| `Assets/ThirdParty` | Celeste, NicoPardo, and IndustrialPack imported assets. Keep their supplied license information. |
| `Assets/Legacy` | Preserved recovery scenes, duplicate imported assets, and template material. These are retained for reference. |
| `ProjectSettings` | Project-wide layers, physics, graphics, build scenes, and other Unity settings. |
| `Packages` | Package dependencies and their resolved versions. |

`Library`, `Temp`, `Logs`, and `UserSettings` are local/generated folders. They are not the source of the game and are ignored by Git.

## How one shot works

```text
Keyboard / mouse
   ↓ inputActions.inputactions → PlayerInput (Send Messages)
   ├─ OnMove / OnJump → PlayerMotor2D → Rigidbody2D and animation
   ├─ OnAim → PlayerAim2D → tool and torch pivots
   └─ OnSpray / OnSwitchMaterial / OnNozzleScroll → PressureTool2D
         ↓ spend water or fuel, allocate a free probe
         ↓ advance probe, apply drag / gravity / lift / turbulence
         ↓ sweep its path against obstacle colliders
         ├─ pooled mesh → visible airborne water / flame
         └─ CellWorld2D → water deposit or ignition
                ↓ fixed simulation ticks
                ├─ flow, oil buoyancy, burning, extinction, smoke
                ├─ runtime texture → SpriteRenderer
                └─ fire clusters → FireLightPool2D → Light2D
```

The action map is still named `PLayer` to preserve scene configuration. `PlayerInput` uses **Send Messages**, so an action named `Spray` calls `OnSpray`. The Q action is now `SwitchMaterial`; the old misspelled `OnSwitch_matterials` callback remains as a compatibility alias.

Q toggles on the press only. Water already in flight stays water when you switch; existing flames finish their lives while new shots use the new mode. Releasing the mouse stops emission and lets airborne probes finish. Disabling the tool clears its probes and visuals.

## Every game script

### Player and supporting components

| Script | Responsibility and useful settings |
|---|---|
| `PlayerMotor2D.cs` | Horizontal speed, ground detection, jump, wall slide and wall jump. Owns Rigidbody2D movement and chooses animation states. Set ground/wall check transforms and Ground Mask. Animation names must match the Animator exactly; the existing `JumUp` spelling is intentional compatibility. |
| `PlayerAim2D.cs` | Converts the pointer position through the camera and rotates Tool Pivot and Torch Pivot. The tool fires along the muzzle's local positive X axis. |
| `CameraFollow2D.cs` | Follows its target using its configured offset. |
| `PlayerAnimation.cs` | Older explicit animation helper methods, retained for existing scene/animation hookups. Avoid adding a second animation controller to the same player without checking who is already controlling it. |
| `SprayPreview2D.cs` | Older renderer toggle driven by OnSpray. Retained for compatibility; the pressure tool draws its own actual moving spray. |
| `TankLevelVisual.cs` | Selects a sprite from empty to full. `SetAmount(current, maximum)` or `SetNormalized(value)` changes it. It is a display helper, not an inventory: it does not automatically read the tool's tank values. |
| `FireLightPool2D.cs` | Repositions assigned Light2D objects over bright fire clusters, scales their intensity, and adds flicker. Assign reusable lights and the CellWorld reference. |
| `CellWorldDebugPainter.cs` | Mouse painting and simulation debug shortcuts. Use it in the fluid sandbox; if enabled alongside the player, both receive the same mouse button. |
| `CellWorldVisibilityProbe.cs` (Editor) | **Tools → BURN → Write Simulation Diagnostics** writes a report to `Temp/cellworld-visibility.txt`. Reports world, renderer, camera, and tool state. It is now an explicit command instead of a recurring background report. |

### PressureTool2D files

These files form **one component** through C# `partial`. Attach `PressureTool2D.cs` once. Do not attach each part as a separate component.

| File | Read or edit this for… |
|---|---|
| `PressureTool2D.cs` | Inspector fields, nozzle definitions, probe data, startup, frame update, cleanup. |
| `PressureTool2D.Input.cs` | Input callbacks, mode switching, scrolling; `SetSpraying`, `ToggleMaterial`, `UseWater`, `UseFlame` for code/UI access. |
| `PressureTool2D.Resources.cs` | Selecting a nozzle, checking/spending resources, adding water/fuel, refilling. |
| `PressureTool2D.Simulation.cs` | Emission, free probe selection, velocity, swept collision, finite water payload, flame deposition. |
| `PressureTool2D.Visuals.cs` | Pooled mesh, streak dimensions, plume colors, age fade, sorting, cleanup. |
| `PressureTool2D.Presentation.cs` | On-screen debug display and selected-object range guides. |
| `PressureSpray.shader` | Smooth water streak edges, flame taper, animated sway, and bright inner core. The shared material is `Resources/Effects/PressureSpray.mat`. |

### CellWorld2D files

These also form one component. Attach only `CellWorld2D.cs`.

| File | Responsibility |
|---|---|
| `CellWorld2D.cs` | Inspector configuration, material arrays, initialization, fixed tick scheduling, simulation order, cleanup. |
| `.Grid.cs` | World position ↔ cell conversion, array indexing, boundary and circle checks. |
| `.Materials.cs` | Public APIs: add water/fuel/smoke, ignite, erase, extract water, query totals/fire, pause/step, rebake, and apply water impacts with immediate cooling. |
| `.Liquids.cs` | Downward and sideways liquid movement, spreading, viscosity, capacity checks. |
| `.Buoyancy.cs` | Oil/water exchange and separation. Oil rises through water. |
| `.Fire.cs` | Burning fuel, fire spread/decay, extinguishing, delayed ignition, and fire cluster queries. |
| `.Vegetation.cs` | Stationary plant fuel, per-cell burn time, wetness, overlapping plants and diagonal vine connections. |
| `.Smoke.cs` | Smoke rise, sideways movement, and decay. |
| `.Chunks.cs` | Active/sleeping regions and waking cells near changes or a target. These are simulation chunks, not separate rendered sprites. |
| `.Rendering.cs` | Physics-to-solid-grid baking, runtime texture/sprite, material colors, animated fire colors, bounds gizmo. |

Each cell has separate water, fuel, fire, and smoke data. Water and fuel share a liquid capacity. Fire energy is 0–3. A solid cell blocks liquids; stationary plants can burn on a wall without making the wall passable. CellWorld's transform is the grid origin; with the normal unrotated, unit-scale setup its visible size is `Width × Cell Size` by `Height × Cell Size`. Select it to see its bounds.

`Vegetation/BurnablePlantTile.cs` stores the five sprite stages and shared settings. `Vegetation/BurnablePlantMap2D.cs` tracks each painted tile's damage and changes its appearance. `Editor/BurnablePlantEditor.cs` provides creation, mask baking, Tilemap setup and build checks. Use the [plant guide](BURNABLE_PLANTS_GUIDE.md) for the artwork workflow.

The existing simulation order is preserved, including two smoke/fire passes per tick. Tick-based fire settings therefore retain their existing behavior; changing that order is a gameplay change, not just tidying.

## Tune the tool in the Inspector

Select the player's `PressureTool2D`. Existing saved Inspector values take precedence over field defaults in source code.

| Setting | Effect |
|---|---|
| Current Material | Starting mode: Water or Flame. |
| Current Nozzle Index | 0 Pilot, 1 Jet, 2 Wide. |
| Water Speed Multiplier | Default 4. Jet's base speed 6.5 becomes **26 units/second** before its existing speed randomness. |
| Water Lifetime Multiplier | Default 1.4. Increases how long water can travel; drag and gravity still affect its actual range. |
| Water Emission Multiplier | Default 2. Jet's 26 probes/second becomes 52. This also spends resources twice as often. |
| Water Amount Multiplier | Default 4. Multiplies the nozzle's Units Per Deposit into a per-probe payload. Tank costs and simulation units are separate tuning quantities. |
| Water Gravity Scale / Drag | More gravity bends downward faster; more drag reduces reach. |
| Water Drop Width | Thickness of the visible streaks. |
| Flame Width | Plume width; older flames expand and fade. |
| Flame Lift / Drag / Turbulence | Upward acceleration, slowdown, and lateral waviness. |
| Flame Deposit Jitter | Variation in where fire is added to the grid. |
| Leave Unburned Fuel Residue | Optional oil deposition; normally off because this mode sprays already-burning flame. |
| Maximum Probe Count | Fixed airborne pool size. When full, the tool waits for space and does not spend resources on an overwritten drop. |
| Obstacle Mask | Physics layers the moving spray should hit. Include terrain/walls. Player-owned colliders and triggers are ignored. |
| Spray Sorting Offset | Added to the CellWorld renderer's order on the same sorting layer. Default +2. |

Each nozzle separately controls angle, base speed, lifetime, speed variation, brush radius, probe rate, resource cost, and ignition radius. Pilot is narrow and short, Jet is the general stream, Wide is a broader cone.

Water keeps a finite payload while airborne. It deposits on collision or expiry and can deposit early into a burning cell along its path. A spray impact immediately spends up to one water unit per fire-energy point on cooling, before liquid flow can move that water away. The grid continues to handle ordinary pooled-water suppression during its ticks. Deposits subtract only what the grid accepts. Water outside the grid, or payload left when an impact cell is full, is discarded when the probe ends. Wider brush settings give it more nearby cells to try. It cannot duplicate its payload with extra frames or longer travel.

Flame probes ignite along their swept paths. Their warm core, taper, expansion, lift and fading are visual/motion behavior; grid fire still governs environmental burning. Firelight flicker and grid color flicker have separate settings, so changing appearance does not require changing burn rates.

## Tune the world

- **Grid resolution:** smaller Cell Size gives finer material detail but needs more cells to cover the same area. Width and Height multiply, so doubling both creates four times as many cells.
- **Liquid feel:** lower viscosity flows more easily; larger spread search distance finds farther drops; sideways passes increase lateral movement work.
- **Fire:** fuel burn, decay, and spread intervals control persistence; spread chance and upward bonus control propagation. Water Used To Extinguish limits pooled-water consumption per fire pass. Direct spray impacts spend water immediately, up to the fire energy at the hit cell.
- **Smoke:** capacity, rise amount, sideways chance, and decay interval control the cloud.
- **Activity:** chunk size, awake radius, awake lifetime and Activity Target limit work. Simulate All Chunks is useful for small experiments.
- **Rendering:** water/fuel/smoke colors affect the cell texture. Fire Appearance controls flicker speed, amount, and refresh rate. It still renders a full grid texture; larger grids cost more to upload.
- **Solids:** Solid Mask is sampled from Physics2D colliders at startup. After changing terrain colliders while running, call `RebakeSolidsFromPhysics()` or press B with the debug painter enabled.

## Debug painter controls

| Input | Action |
|---|---|
| 1 / 2 / 3 / 4 | Select water / fuel / smoke / fire. |
| Left mouse / Right mouse | Paint / erase. |
| P | Pause or resume the material simulation. |
| N | Advance one simulation tick. |
| R | Clear all materials. |
| B | Rebake solid cells from colliders. |
| F1 | Print water/fuel totals and active chunk count. |

The pause key is P so it does not compete with player jumping. Pausing CellWorld stops its automatic ticks; it does not pause the player or airborne tool probes.

## Modify it yourself

**Make water faster:** increase Water Speed Multiplier on the player. Test Jet against a thin wall and while aiming upward. Tune lifetime for range separately; avoid making the grid larger merely to hide a bad muzzle position.

**Make the flame wavier:** increase Flame Turbulence slightly and adjust Flame Lift. For its shape/colors, edit `.Visuals.cs` and `PressureSpray.shader`. For longer-lasting environmental fire, edit CellWorld's fire settings instead.

**Change the switch key:** open `Assets/Game/Settings/inputActions.inputactions`, select `PLayer → SwitchMaterial`, edit its binding, and save the asset. Keep the action name or update its matching `On...` callback too.

**Add a refill pickup:** obtain the player's PressureTool2D and call `AddWaterResource(amount)`, `AddFuelResource(amount)`, or `RefillAllResources()`. The add methods clamp to capacity and reject negative additions. A visual tank can be updated with `tank.SetAmount(tool.CurrentWater, tool.MaximumWater)`; use the fuel properties for a fuel tank.

**Add another nozzle:** the current selection is intentionally three slots. Add settings, extend `GetCurrentNozzle`, update cycling/clamping and Inspector range, then test scroll wraparound. For a large nozzle catalogue, replace these three fields with a data asset or array as a separate migration.

**Add a material:** append a new enum value without reordering Water=0 and Flame=1. Update resource selection, emission/motion, deposition, visuals, and display. A genuinely new simulated liquid also needs CellWorld arrays, capacity rules, movement, rendering, and conservation tests.

**Make another level:** duplicate a working scene inside Unity. Keep the player's tool assigned to that scene's CellWorld and muzzle. Check all ground/obstacle/solid masks and the grid bounds. Add the scene to the build profile when you want it in a player build.

**Rename or move files:** move assets through Unity's Project window, or move the asset and its `.meta` together. A `.meta` GUID is Unity's stable identity for that asset. Keep the main MonoBehaviour class and main filename matching. Use `[FormerlySerializedAs("oldFieldName")]` when renaming saved fields, otherwise existing Inspector values may be lost. Do not reorder serialized enum values casually.

## If spraying disappears again

Check these in order:

1. The Game view has focus; PlayerInput is enabled, has the correct input asset, and uses the `PLayer` map with Send Messages.
2. PressureTool2D has its Cell World and Muzzle references. The world is initialized, the tank has resources (or unlimited testing is on), and the muzzle lies in the intended grid area.
3. The muzzle's red/local X axis points out of the barrel and starts outside the wall.
4. **GameObject Layer / camera Culling Mask** determine whether the camera can see an object. **Sorting Layer / Order in Layer** determine which visible 2D object draws on top. **Obstacle Mask / Solid Mask / Ground Mask** choose physics layers. These are different settings.
5. The CellWorld renderer is above opaque backgrounds; the spray inherits its sorting layer plus the configured offset. The runtime spray object's layer follows the player's layer at creation.
6. The material exists at `Assets/Resources/Effects/PressureSpray.mat` and the camera uses the project's URP 2D renderer. A missing material logs a warning; pink spray indicates a shader problem.
7. Run **Tools → BURN → Write Simulation Diagnostics** while playing and inspect `Temp/cellworld-visibility.txt`.

The old `Assets/Legacy/RecoveryScenes/0.unity` snapshot retains its pre-existing stale CellWorld script link. It is an archive, not the startup scene. Use Level_0 or a sandbox for current work.
