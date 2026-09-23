# Burnable vines and plants

For the current ten four-frame vine sheets and a step-by-step recipe for future artwork, start with [MAKE_ANY_SPRITE_BURNABLE.md](MAKE_ANY_SPRITE_BURNABLE.md). The **Burnable Vines** palette contains the prepared tiles, and Level_0 contains a test patch beside the player.

Use a **Tilemap**, with one `BurnablePlantMap2D` component for the whole map. Paint vines, connectors, flowers and other plants as tiles. Each painted piece has its own damage, but does not create its own GameObject, Animator, collider or particle system.

The existing CellWorld fire ignites the plant's visible healthy shape and spreads through touching fuel cells. Plant fuel stays in place, including on walls. Water extinguishes it and temporarily prevents reignition. Damage remains after extinguishing. The default final result is **disappearance**.

## Prepare the artwork

You can keep everything on your existing `Assets/Art/Tiles/Vines/plants.png` sheet. Its current pixels-per-unit setting is **16**. You do not need seven separate textures or an animated prefab for every piece.

Make five sprites for **each shape**, including each connector and miscellaneous plant:

| Stage | Meaning | Suggested slice name | Visible during |
|---|---|---|---|
| 0 | Healthy | `vine_vertical_burn0` | 0–20% consumed |
| 1 | Light damage | `vine_vertical_burn1` | 20–40% |
| 2 | Medium damage | `vine_vertical_burn2` | 40–60% |
| 3 | Heavy damage | `vine_vertical_burn3` | 60–80% |
| 4 | Last few pixels | `vine_vertical_burn4` | 80–100% |
| Finished | Empty space | No sixth sprite needed | 100% |

Seven shapes therefore need **35 sprites**, plus five for every additional connector or plant type. A shape that is just a rotation or mirror of another can reuse its tile: rotate or flip it when painting. You only need separate art where its shape really differs.

The last sprite being only a handful of pixels is fine. **Keep those pixels on the original transparent canvas.** Do not crop that frame down to a tiny rectangle. All five stages of a given plant must have:

- Identical slice width and height. Different plant types may have different sizes.
- Identical pivot position.
- Identical pixels per unit.
- The same attachment position: a vine's stem should not jump between frames.

For example, a 16×32 vine needs five 16×32 slices even if the last slice contains only three visible pixels. Larger plants may span multiple grid cells; paint one anchor tile for that plant.

Recommended sheet layout: one row per shape, five equal-size frames from healthy to nearly gone. This layout is for your convenience; the script uses sprite references/names, not row numbers.

Use **Sprite (2D and UI)**, **Multiple**, **16 pixels per unit**, **Point** filtering and no compression for crisp pixel art. Keep Max Size large enough that Unity does not resize the original sheet. Export as PNG. Read/Write can remain **off**; masks are prepared in the editor.

Your existing `plants_0`, `plants_1`, etc. are independent sprite slices, not a five-frame burn sequence. The tool does not assume the first five belong together. Existing numbered names work as healthy-only prototypes. For automatic five-frame grouping, use the explicit `_burn0` through `_burn4` suffix.

**Keep existing slice rectangles and sprite IDs intact.** Append new frames in unused space or use a second sheet for new artwork. Do not automatically re-slice the whole original sheet: existing painted tiles already reference its current slices. You can assign five sprites manually in the tile Inspector if you prefer to keep existing names.

## Set it up in Unity

1. Stop Play mode. In the Project window, expand the spritesheet so you can select its sprite slices.
2. Select the five correctly named slices for one plant, or select several complete sets. Choose **Tools → BURN → Plants → Create Tiles From Selected Sprites**.
3. The tool creates assets in **Assets/Game/Tiles/BurnablePlants** and bakes a mask from each healthy sprite. Select the resulting tile asset to check its five stages. Missing stages reuse the previous available sprite so you can test behavior before drawing all the artwork.
4. For manual setup, use **Create → BURN → Burnable Plant Tile** in the Project window. Assign its five sprites, then click **Bake / Refresh Healthy Sprite Mask**.
5. Select the level's Grid in the Hierarchy and choose **Tools → BURN → Plants → Create Burnable Plant Tilemap**. This adds a child named **Burnable Plants**, with a Tilemap, chunk renderer and one manager. It assigns the scene's CellWorld and puts the plants just behind the fire's sorting order. If there are multiple CellWorlds, explicitly assign the correct one in the Inspector.
6. Drag the new **BurnablePlantTile assets** into the Tile Palette. Paint on **Burnable Plants**. Do not drag the sprite images into the palette again: that creates ordinary, non-burnable tiles.
7. Keep plants inside the CellWorld rectangle, excluding its one-cell border. Select CellWorld to see its bounds. Place the visible ends of vine pieces so they touch, including diagonal contacts.
8. Save your scene. Enter Play mode, press **Q** to select flame and ignite one end. Switch to water to interrupt it. Stop Play mode to restore the original vegetation.

To get a quick first test, select a single healthy vine slice such as one of your existing `plants_*` sprites, run the creation command, and paint several connected copies. It will burn and disappear, but its appearance will not show all five damage stages until you assign those sprites. No burned artwork is generated by the script.

The prepared **CornerVine1**, **CornerVine2**, **CrossVine1**, **CrossVine2**, **EndVine1**, **EndVine2**, **strightVine1**, **strightVine2**, **strightVine3** and **TVine1** assets now provide real four-frame examples. Each repeats its final frame in slot 4. The old healthy-only example was deleted during the artwork setup; use these finished assets instead.

Existing ordinary plant tiles do not become burnable automatically. Repaint them using the new assets on the plant map. Keep them separate from permanent level walls. Plant collision is optional: use the setup below for solid plants, or leave the map without a collider for walk-through decoration. Burning a plant does not remove a separate wall behind it. This system does not implement climbing.

If the plant layer is obscured, adjust its TilemapRenderer sorting layer/order for your scenery; keep CellWorld's fire above the plants. The setup command chooses a starting order, but cannot know every layer in your level.

## Adjust the behavior

On each BurnablePlantTile asset:

| Setting | Default | Effect |
|---|---|---|
| Burn Stages | Five sprite slots | Artwork from healthy to nearly gone |
| Burn Seconds | 6 | Time needed to consume each burning simulation cell |
| Wet Seconds | 2 | Simulation time during which water prevents plant fuel from reigniting |
| Disappear When Burned | On | Remove the tile completely at full consumption; off leaves stage 4 as an inert remnant |
| Collision Type | Sprite | Keeps the healthy sprite's physics shape until fully burned. Grid uses the cell outline; None is walk-through. Requires a Tilemap Collider 2D on the map. |

Burn Seconds is **not a fixed duration for a whole vine network**. Fire must travel between cells. A longer network takes longer, and water can stop it halfway. The sprite stage is based on total fuel consumed in that individual painted piece. The existing CellWorld fire spread chance, spread interval and upward bonus also affect plant fire.

Flames, smoke, flicker and pooled lights use the existing CellWorld system. You do not draw fire into the plant's five sprites. Plants do not spawn an individual light or particle system.

At the project's usual CellWorld cell size of 0.125 and artwork at 16 PPU, a simulation cell covers roughly **2×2 artwork pixels**. Fire attachment/spread has that resolution; it is not exact per-artwork-pixel simulation. Even a one-pixel stem is sampled during mask baking. Touching diagonal fuel cells can spread fire. A visible gap narrower than a simulation cell can still connect; leave a larger gap where fire must stop.

The healthy silhouette defines the stationary fuel footprint for the duration of that burn. Damaged artwork replaces the whole tile in five steps; this is not a directional burn-cut shader. Remaining flame can briefly outlive consumed pixels while its existing fire energy decays. If you want more localized damage on a long vine, paint shorter connected pieces. Completely disconnected islands inside a sprite may stay unburned until fire reaches them separately; connect the healthy stem or ignite those regions too.

## When you change the sprites

After changing a healthy shape, its slice, pivot or PPU, select its tile asset and click **Bake / Refresh Healthy Sprite Mask**, or use **Tools → BURN → Plants → Bake All Plant Masks**. This prepares a list of opaque healthy pixel positions; it does not change the image or enable runtime texture reads.

All five stage sprites must keep matching dimensions, pivot and PPU. The baker reports mismatches instead of producing jumping artwork. A build check also catches missing or stale masks, including assets not currently painted in the open scene. If it names a prototype you no longer want, delete that unused tile asset or finish its mask.

Adding sprites to a sheet changes its source hash, so rebake masks for that sheet afterward. Re-running the creation command makes **new** tile assets; to update an existing type, edit its existing asset instead. This preserves painted references.

## Solid plants: keep collision until the entire piece burns

1. Stop Play mode and select **Burnable Plants** in the Hierarchy.
2. Add **Tilemap Collider 2D** to that same object. Leave **Is Trigger** off.
3. Set that object's **Layer** at the top of the Inspector to **Ground**. This is the physics layer, not the renderer's sorting layer. In the current Level_0, Ground is already included in the player's Ground Mask, the tool's Obstacle Mask and CellWorld's Solid Mask.
4. Select each BurnablePlantTile asset and leave **Collision Type = Sprite**. It uses the healthy sprite's Unity physics shape, which you can edit in Sprite Editor's **Custom Physics Shape**. Use **Grid** for a full cell-shaped block, or **None** for flowers/grass that should remain walk-through on the same map.
5. Save the scene and test. The healthy collision shape stays unchanged through all five visual stages, including the final handful of pixels. At 100% burned, collision disappears. If Disappear When Burned is off, its charred artwork stays but is no longer solid.

You need one Tilemap Collider 2D for the map, not a collider or Rigidbody on every plant. A Rigidbody is not needed on this static Tilemap for basic collision. The moving player already has one.

For a different layer or scene, include the plant layer in **CellWorld → Solid Mask** and **PressureTool2D → Obstacle Mask**. Include it in the player's **Ground Mask** if plants should support standing/wall checks, and allow contact with the Player layer in the Physics 2D collision matrix. Setting Collision Type alone does not add a collider component or change these masks.

Optional for large connected solid maps: add a **Rigidbody 2D** set to **Static** and a **Composite Collider 2D** on the same object; set the Tilemap Collider's **Composite Operation** to **Merge**. The plant manager also refreshes composite geometry when tiles change, including a composite configured for manual generation.

When a plant finishes burning, the manager processes the Tilemap's collision change and resamples only nearby CellWorld cells. It checks the remaining physics geometry, so overlapping permanent walls stay solid. Distant plants finishing in one frame do not force a scan of the space between them. Changing to a damaged sprite does not trigger a full-world physics bake.

Keep the map, its transforms and collider configuration static during Play mode. Configure collision before playing. Use Reset Plants And Rebuild after deliberate runtime changes; it also heals damage. Disabling the manager restores its healthy tiles and collision, while disabling the entire GameObject removes its colliders from physics.

## Performance and boundaries

- A thousand painted tiles still use one manager; there is no per-plant Update loop.
- The renderer uses Tilemap chunk mode. A shared sheet/material keeps batching effective.
- Initialization scans the map once and maps opaque samples onto CellWorld cells. Sparse fuel records exist only where plants are painted.
- The simulation looks up plant fuel when fire touches it. Idle plants do not perform distance searches, physics checks or full-map scans every frame.
- Only pieces whose burn stage changes need a tile replacement. Runtime stage tiles are shared per plant type and stage.
- Sprite-collision stages also share runtime sprite copies per type/stage. Their appearance uses the damaged artwork, while their physics outline comes from the healthy sprite; imported sprite assets are never modified.
- Overlapping large plants keep independent damage even where their fuel cells overlap.

This avoids the overhead of hundreds of animated plant objects, but it is not a guarantee of no frame drops. Huge sheets, very large/scaled plants, lots of simultaneous burning cells, and the existing CellWorld texture upload still have costs. Profile the actual level before increasing the fire grid resolution. A finer global grid costs much more than simply adding more tile artwork.

This version is for **static level decoration**. Plant positions are registered when the manager enables after CellWorld is ready. Moving the map or editing it during Play mode does not automatically move the registered fuel. `ResetPlantsAndRebuild()` restores original sprites, resets damage and registers again. Disabling/re-enabling the manager also resets its vegetation. Do not use disabling as a way to pause/save plant damage.

Saving/loading burn damage between game sessions is not included. Stopping Play mode restores your scene's unburned tiles. Clearing CellWorld's liquids/fire stops burning but does not heal plant damage; reset the plant manager to heal it. Plant colliders are removed at full burn; independent terrain colliders are preserved.

## Where the code lives

| File | Responsibility |
|---|---|
| `Assets/Game/Scripts/Vegetation/BurnablePlantTile.cs` | Shared sprite references and burn settings; baked alpha samples |
| `Assets/Game/Scripts/Vegetation/BurnablePlantMap2D.cs` | One manager per Tilemap; transforms masks, tracks individual damage, changes stages and removes completed plants |
| `Assets/Game/Scripts/Vegetation/BurnablePlantMap2D.Collision.cs` | Keeps the healthy collision shape on damaged artwork, batches collider changes, restores collision on reset |
| `Assets/Game/Scripts/Simulation/CellWorld2D.Collision.cs` | Updates only affected simulation obstacle cells from the remaining physical colliders |
| `Assets/Game/Scripts/Simulation/CellWorld2D.Vegetation.cs` | Sparse stationary fuel, wetness, consumption, overlap handling and diagonal spread |
| `Assets/Game/Scripts/Simulation/CellWorld2D.Fire.cs` | Existing fire tick, now also consuming plant fuel |
| `Assets/Game/Scripts/Simulation/CellWorld2D.Materials.cs` | Existing ignition/water entry points, including wall-backed plants |
| `Assets/Game/Scripts/Weapons/PressureTool2D.Simulation.cs` | Water shot contact with wall-backed plants; finite water payload is preserved |
| `Assets/Game/Editor/BurnablePlantEditor.cs` | Tile creation, PNG mask baking, Tilemap setup and build validation |

For ordinary art/timing adjustments, use the tile Inspector. You only need the scripts when changing the underlying burning rules.

## GitHub Desktop checkpoint

Unity saving a scene or script changes files on disk. **It does not automatically create a Git checkpoint.** After checking this feature, save the scene in Unity, then review the changes in GitHub Desktop and commit them with a name such as `Add burnable plant tiles`. Include every new asset's `.meta` file. Push if you want the checkpoint backed up on GitHub.

Make another commit before changing lots of artwork. To undo a committed change later, use **History → right-click the commit → Revert Changes in Commit**. Merging an older branch does not rewind the project. See `Docs/GITHUB_DESKTOP_GUIDE.md` for the longer workflow.
