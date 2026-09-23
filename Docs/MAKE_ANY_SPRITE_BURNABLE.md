# Make another sprite burnable

## The vines already set up for you

Your ten PNG sheets in `Assets/Game/Tiles/BurnablePlants/single vine animation sprties/` contain four 16×16 frames each, on a 64×16 canvas. Their slices are named `CornerVine1_burn0` through `CornerVine1_burn3`, and the equivalent names for each other shape. The original PNG pixels and sprite IDs are preserved.

There is **one burnable tile asset per shape** in `Assets/Game/Tiles/BurnablePlants/`. Each has its four damage sprites assigned, a baked healthy-pixel mask, and the last sprite repeated in slot 4. No new artwork was invented. Burn Seconds is 6 and collision uses the healthy sprite until the tile disappears.

Open **Window → 2D → Tile Palette**, choose **Burnable Vines**, and set **Active Tilemap → Burnable Plants**. The palette contains all ten shapes. Level_0 also has a small two-row test patch beside the player's starting position. Its plant layer has a Tilemap Collider 2D and uses the Ground physics layer.

Ignite with the flame tool. Q switches between flame and water. Briefly extinguish a partly burned vine to see its damaged sprite without the flame covering it. Stop Play mode to restore the patch.

## For each new plant, wooden prop, or other static object

1. **Draw the frames.** Start healthy and progressively remove/char its pixels. Export a transparent PNG. Four 16×16 frames make a 64×16 strip; five make an 80×16 strip. Larger frame sizes work too. Keep every frame of one object the same size, pivot and alignment. Do not crop the last few pixels into a smaller frame.
2. **Import and slice.** In Unity choose Sprite (2D and UI), Sprite Mode Multiple, 16 Pixels Per Unit for this project's art, Point filtering, and no compression. Open Sprite Editor, slice by your frame size and keep the same pivot. Read/Write can stay off.
3. **Name the individual slices inside Sprite Editor.** For a bush, use `Bush_burn0`, `Bush_burn1`, `Bush_burn2`, `Bush_burn3`, and optionally `Bush_burn4`. Stage 0 is healthy. Each different shape needs a unique prefix, such as `BushTall` and `BushWide`. Click Apply. The PNG filename alone does not group the frames.
4. **Select the entire PNG sheet, or all its frame sprites.** Use Tools → BURN → Plants → Create Tiles From Selected Sprites. You can select several sheets together. The tool makes one tile per named shape and automatically bakes its mask. You do not select only the healthy frame when you want all damage frames assigned.
5. **Check the generated tile.** Expand Burn Stages. Your frames should appear in order. If you have four frames, slot 4 may be empty; it reuses slot 3. You can drag the fourth sprite into slot 4 to make this explicit and remove the missing-stage warning.
6. **Paint with the generated tile asset.** Drag that `.asset` into a Tile Palette, select the Burnable Plants Tilemap, and paint inside the CellWorld area. Dragging the raw PNG or sprites into the palette creates ordinary tiles instead.
7. **Save, play and test.** Fire consumes the healthy visible pixels; the tile switches artwork at 20%, 40%, 60% and 80% burned and disappears at 100%. With the fourth frame reused, that last image spans 60–100%. These are damage stages, not a looping animation; Aseprite frame durations are not used.

## Changing an existing plant

Keep its existing tile asset and edit its Burn Stages in the Inspector. After changing the healthy artwork, slice, pivot or pixels per unit, click **Bake / Refresh Healthy Sprite Mask**, or run **Tools → BURN → Plants → Bake All Plant Masks** outside Play mode. Do this after replacing or extending the sheet too.

Do not delete and recreate tiles already used in a scene. Running Create Tiles again makes additional assets rather than updating the old ones. Editing an existing tile updates all its painted instances without repainting. Keep the `.meta` files so Unity keeps its references.

## Collision and slower previews

- **Collision Type = Sprite:** keeps the healthy shape solid until fully burned. **Grid:** a full tile-sized block. **None:** walk-through decoration, even on the same collider map.
- **Burn Seconds** on the tile controls fuel consumption. Try 15 instead of 6 for a longer preview. This is time per burning simulation cell, not a guaranteed duration for a whole network.
- On CellWorld, a slower spread starting point is **Fire Spread Every Ticks = 12**, **Fire Spread Chance = 0.05**, **Upward Spread Bonus = 0.02**. These settings affect other fire too. A spread chance of 0 prevents horizontal/downward spread, while a positive upward bonus can still spread upwards.
- The fire overlay can cover the artwork. A quick water spray stops the fire while preserving damage so you can inspect the sprite.

This system handles static sprites painted on a Tilemap. It does not automatically burn moving characters, skeletal animations or arbitrary SpriteRenderer objects. Different plant types can have different sizes, and a large sprite can extend over several cells from one painted anchor.

For a Git checkpoint, save your Unity scene, then commit the new assets, their `.meta` files, the scene and docs in GitHub Desktop. Saving in Unity does not automatically make a commit. Revert a commit to undo it; merging an old branch is not a rewind.

See [BURNABLE_PLANTS_GUIDE.md](BURNABLE_PLANTS_GUIDE.md) for the implementation, performance notes and full collision setup.
