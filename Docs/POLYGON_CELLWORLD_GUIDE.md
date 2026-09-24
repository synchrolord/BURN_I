# Using a polygon CellWorld

`CellWorldPoly2D` runs the existing water, fuel, fire, smoke, and plant simulation inside an outline you draw. Your rectangular `CellWorld2D` remains available. Installing these scripts does **not** change any scene or move any references automatically.

The polygon script inherits from `CellWorld2D`: in Java terms, it **extends** the existing world. That is why you can drag it into the gun's existing **Cell World** field. There is no separate polygon gun or polygon plant script.

## Create and connect one

1. **Stop Play mode.** Use **GameObject → 2D Object → BURN → Polygon CellWorld**. Put the new object beside your old world in the Hierarchy, rather than inside the old object.
2. Keep the old world for reference. Manually copy its Transform, Width, Height, Cell Size, Solid Mask, and any simulation/color settings you want. Match its Sprite Renderer sorting layer/order so the simulation appears in the same visual layer. Do not put both simulation components on one object.
3. Click **Reset Outline To Grid** after setting the grid size. Expand **Outline** to edit the points, or drag their numbered handles in Scene view. Green means valid; red means the Inspector has an error to fix.
4. Assign the new polygon world to the fields below. Use an explicit assignment for every plant layer; automatic searching is ambiguous when two worlds exist.
5. Deactivate the **old world object only**, then enter Play and test spraying, burning plants, and lighting. The old world and the new one must not simulate the same room at once.

These are the current Level_0 references. The unusual object spelling matches the saved scene.

| Object | Component → field |
| --- | --- |
| `PLayer` | Pressure Tool 2D → **Cell World** |
| `Grid/Burnable Plants` and every other burnable plant layer | Burnable Plant Map 2D → **World** |
| `WolrdSystems/FireLights` | Fire Light Pool 2D → **Cell World** |
| `CellWorldDebugPainter`, if you use it | Cell World Debug Painter → **Cell World** |

The old world is `WolrdSystems/CellWorld2D`. **FireLights is a separate object:** keep it active and change its reference. Do not disable the entire `WolrdSystems` parent. If you later install Advanced movement, also assign **Player Motor 2D → Swimming World**. Basic movement has no swimming-world field.

References are saved per scene or prefab instance. Repeat the assignments in **Level_1** if you convert that scene: its player, FireLights, and debug painter also reference the rectangular world. If you create a polygon version of **WorldSystems.prefab**, update its FireLights reference inside that prefab too. **FluidDebugController.prefab** has an unassigned debug-painter field; assign it when you use it. The old recovery scene has another debug-painter reference, but you can leave that scene alone unless you deliberately convert it. Editor probes detect both world types automatically and need no field change.

To go back, stop Play, restore these references to the old world, deactivate the polygon object, and reactivate the old world.

## Draw the shape

Points use **local X/Y distances**, not cell numbers. With Transform scale `(1, 1, 1)`, a grid of Width `128`, Height `96`, and Cell Size `0.125` covers a `16 × 12` rectangle starting at the world's origin. All points must fit inside that rectangle.

An L-shaped example, entered in this order:

```
(0, 0), (16, 0), (16, 4), (6, 4), (6, 12), (0, 12)
```

The last point connects to the first automatically; do not repeat the first point. Concave shapes such as this L are supported. Crossing edges, duplicate adjacent points, holes, and separate islands are not. Expand the Outline array to add/remove corners. Stop Play before editing the outline or grid dimensions.

The grid's outermost **one-cell border stays closed**, including where the outline follows its edges. Membership is decided by each cell's center, so very narrow details need a smaller Cell Size. Keep plants inside the included area. The outline needs no collider and does not block the player: real wall/floor colliders still handle movement.

## What the scripts do

- `CellWorldPoly2D.cs` checks the corners and creates an inside/outside mask once at startup. Its parent still performs the simulation.
- `CellWorld2D.Shape.cs` is part of the parent class. It stores the baked mask and skips chunks that contain no included cells.
- `CellWorldPoly2DEditor.cs` supplies the creation menu, validation display, and draggable handles. It only runs in the Unity Editor.

Both **Basic and Advanced `03_CellWorld` library editions** include polygon support, so the script-edition switcher keeps it available.

Excluded regions skip simulation work, but the world still allocates arrays and a texture for the **whole Width × Height rectangle**. A polygon saves processing outside its shape; it does not provide unlimited world size or sparse memory storage. Separate world objects remain separate simulations: touching or overlapping them does not transfer water, fire, plant state, or gun targeting between them.
