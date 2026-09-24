# Start with the basic game

The scripts inside `Assets/Game/Scripts` are the **active** scripts Unity runs. Open `Assets/Game/Scenes/Levels/Level_0.unity` and press Play.

This edition deliberately has fewer rules. You can walk, jump repeatedly, climb ladders, aim, switch between water and burning flame with **Q**, change nozzles with the mouse wheel, and burn or extinguish plants. Coyote time and a small jump buffer make jumping forgiving. The original artwork, painted tiles, input bindings, scene objects and script IDs are retained.

Advanced wall jumps/sliding, swimming, variable jump height, acceleration/deceleration and more elaborate liquid motion are optional upgrades. This is still a finite, single-CellWorld level; simplifying code does not create an unlimited world.

## Where everything lives

| Location, relative to the Unity project folder | Purpose |
| --- | --- |
| `Assets/Game/Scripts` | Edit these files. This is the running game. |
| `ScriptLibrary/Basic` | Clean reference copies of the basic modules. |
| `ScriptLibrary/Advanced` | The pre-simplification behavior, with shared polygon compatibility added. |
| `ScriptLibrary/Switch-Script-Edition.cmd` | Switch one complete module while Unity is closed. |
| `Docs/BEGINNER_CODE_GUIDE.md` | This guide. |
| `../BURN_I_Feature_Library` | The older library containing the larger campaign/infection/enemy update, plus backups. |

`ScriptLibrary` is **beside Assets**, not inside it. Unity does not compile it. These reference scripts are included when you commit them in Git. Never drag the whole library into Assets: that would define the same C# classes twice.

## Read these first

1. **Player/CameraFollow2D.cs:** a short introduction to following a Transform.
2. **Player/PlayerMotor2D.cs:** read input, check the ground, set Rigidbody velocity, choose an animation.
3. **Player/PlayerAim2D.cs:** turn the mouse position into a direction.
4. **Weapons/PressureTool2D.cs:** select a nozzle, launch a drop, move it, apply its impact.
5. **Vegetation/BurnablePlantTile.cs:** a small data asset holding plant sprites and settings.

Start with those five. CellWorld's grid math and plant collision geometry are infrastructure; you do not need to understand them to tune movement, make plant artwork, or build a level. They are retained because the existing plant masks, collisions and sprite transforms need them.

## What each object/script does

| Script/object | Job | What you normally edit |
| --- | --- | --- |
| PlayerMotor2D on the player | Walking, ground jumping, ladders, animation selection | Speed, jump speed, ground checks, coyote/buffer times |
| ClimbArea2D on a trigger | Tells the motor when the player enters/leaves a ladder | Trigger size/position; it must remain a trigger |
| PlayerAim2D | Rotates tool and torch toward the mouse | Tool/torch references |
| CameraFollow2D | Follows the player | Target and follow settings |
| PlayerAnimation | Existing helper for explicitly playing named animation states | State names only when editing the Animator |
| SprayPreview2D | Existing optional spray-preview renderer | Preview renderer reference |
| PressureTool2D | Q selection, nozzle input, traveling shots, impacts, tanks | Nozzle speed/spread/lifetime and resource settings |
| TankLevelVisual | Chooses a tank sprite from empty to full | Ordered fill sprites |
| CellWorld2D | Owns the water, fuel, fire, smoke and solid-cell arrays | Grid size, cell size, solid mask, simulation/fire settings |
| CellWorldDebugPainter | Optional sandbox brush and debug keys | Disable while testing the actual weapon if both use the mouse |
| BurnablePlantTile asset | Five burned sprites, burn time, wet time, collision | The plant asset's Inspector |
| BurnablePlantMap2D on plant Tilemap | Registers painted plants and changes their visible burn stages | World reference; paint the Tilemap normally |
| BurnablePlantMap2D.Collision.cs | Keeps the healthy collision outline until burning finishes | Usually leave this helper alone |
| FireLightPool2D | Reuses a few lights near burning cells | Light references, brightness, color |
| Game/Editor/BurnablePlantEditor.cs | Creates plant tile assets and bakes healthy pixel masks | Use its Tools/BURN/Plants menu, usually no code changes |
| Game/Editor/CellWorldVisibilityProbe.cs | Editor diagnostics for the grid | Use only when diagnosing visibility |

Files named `CellWorld2D.Something.cs` all belong to **one class**. C#'s `partial` keyword permits this. You attach `CellWorld2D` once; you do not add its individual files as extra components. The same applies to `BurnablePlantMap2D.Collision.cs`.

CellWorld files separate distinct rules: `Grid` converts coordinates; `Collision` samples obstacles; `Materials` exposes add/remove/query methods; `Liquids` moves water/fuel; `Buoyancy` handles their simple local exchange; `Fire` burns/spreads; `Smoke` rises/fades; `Vegetation` accounts for anchored plant fuel; `Chunks` skips sleeping areas; `Rendering` draws cells. The main file owns data and calls these rules. This split is still necessary in the basic edition; the rules themselves are shorter.

## Unity and C# for someone who knows Java

Most ordinary code is familiar: classes, fields, methods, `if`, `for`, `foreach`, arrays, `List<T>` and `Dictionary<TKey,TValue>`. A `GameObject` holds components. `GetComponent<Rigidbody2D>()` gets the physics component on the same object.

| C#/Unity syntax | Meaning |
| --- | --- |
| `class X : MonoBehaviour` | A Unity component; roughly Java's `extends` syntax. |
| `[SerializeField] private float moveSpeed` | A private field Unity saves and exposes in the Inspector. The square brackets are attributes. |
| `public float Speed => speed;` | A short read-only property/getter. |
| `out Vector2Int cell` | The method returns an extra result through this variable. |
| `var player = ...` | Infer the variable's type at compile time. It is still strongly typed. |
| `Vector2` / `Vector3` | Two/three numbers representing a position, direction or velocity. |
| `LayerMask` | Which Unity layers a physics query should include. |
| `IEnumerator` + `yield return null` | Pause this coroutine until a later frame, rather than blocking Unity. |
| `partial` | Parts of one class written in several files. |

Unity calls `Awake` once when the component is initialized, `OnEnable` when enabled, `Start` before its first Update, `Update` each rendered frame, `FixedUpdate` on physics steps, and `LateUpdate` after Update. `OnDisable` cleans up subscriptions/state when disabled. You do not write a Java-style `main()` loop.

`PlayerInput` uses action names to send messages: an action named `Move` calls `OnMove`; `Jump` calls `OnJump`. The basic motor queues each jump press and uses a fixed jump height, avoiding a complicated held/released state machine.

**Change Inspector values for objects that already exist.** Editing `moveSpeed = 6f` in C# changes the default for newly added components; a saved scene/prefab can still hold its previous value. Changes made in Play mode normally revert when you stop. Edit outside Play mode and save the scene or prefab.

## Small exercises

- Select the player, change Move Speed in the Inspector, then test walking.
- Change Jump Speed and test several consecutive jumps. A higher value jumps higher.
- Change Coyote Time to see how long an edge jump is allowed; change Jump Buffer Time to test pressing just before landing.
- Select the tool, change a nozzle's spread or speed, test it, then compare water and flame.
- Select a plant **tile asset**, increase Burn Seconds, and watch its stages. Painting an ordinary Sprite Tile does not make it burnable.

Change one setting or one method, test, then commit with a concrete description such as `Tune jump height`. Do not tune five systems at once.

## Plants and artwork stay the same

Use a sprite sheet or separate sprites with the same canvas, pivot and pixels-per-unit for all five stages. In a BurnablePlantTile assign healthy through almost destroyed sprites to slots 0–4, then **Bake / Refresh Healthy Sprite Mask**. Paint that tile asset on the plant Tilemap. It needs BurnablePlantMap2D and a World reference. Collision also needs Tilemap Collider 2D and a non-None collision setting on the plant tile.

Fire consumes the baked healthy pixels. Damage selects one of the five visible sprites; it is not a looping Animator animation. The plant keeps its healthy collision until it finishes, then disappears. Water suppresses fire and temporarily protects the plant; it does not restore burned pixels. Registration handles existing rotated/scaled sprites, and one manager handles the whole map.

See `MAKE_ANY_SPRITE_BURNABLE.md` and `BURNABLE_PLANTS_GUIDE.md` for the existing artwork workflow. These remain applicable.

## Restore one advanced feature group

1. Stop Play, save, and close Unity. Commit your working basic version in GitHub Desktop first.
2. In File Explorer, open the project folder, open **ScriptLibrary**, and double-click **Switch-Script-Edition.cmd**.
3. Choose **Advanced**, then one module: Movement, Tool, CellWorld or Plants.
4. Reopen Unity and test that one change. Commit it separately after it works. Push origin copies committed work to GitHub.
5. To go back to the basic implementation, run the same switcher and choose Basic for that module.

Movement is essentially a single motor replacement plus its ladder companion. Tool and CellWorld have dependent files: switch the **whole module**, not a random partial file. The switcher copies matching `.meta` files, removes only obsolete files belonging to that module, verifies the package, and backs up your currently edited scripts first. It does not create a Git commit.

Backups from switching are saved beside the project at `../BURN_I_Feature_Library/Script_Switch_Backups`. The initial full source snapshot is in `../BURN_I_Feature_Library/Basic_Edition_Backup`. These backup folders are outside this Git repository. The reference Basic/Advanced packages inside ScriptLibrary *are* part of your repository once committed.

Switching restores the saved reference version of the code, so it replaces edits you made in that module. Commit those edits first or keep the automatic backup. Advanced Inspector settings absent from a saved basic scene can return with defaults; the switcher deliberately does not overwrite your levels. The initial snapshot preserves the earlier scene settings if you ever need an exact full restoration.

For entirely new systems such as infection or enemies, the earlier `BURN_I_Feature_Library/Stages` remains available. Those are a separate, larger set of upgrades: follow their dependency order and READMEs. They are not isolated one-file additions. Finish learning/testing this basic version before importing them.

## Adding your own new script

Create a C# script in an appropriate `Assets/Game/Scripts` subfolder, give its public MonoBehaviour class the same name as the file, and attach it to the object it controls. Let Unity generate its `.meta` file and commit that file too. Start with one responsibility, a few Inspector settings and one short update method.

To replace an existing script, overwrite its contents while preserving the class name and its existing `.meta`. Do not rename or delete the metadata to force Unity to reimport: scenes and prefabs use that ID to find their scripts. Keep public methods that other scripts call, or update those callers in the same change.

Older PROJECT_GUIDE/REFACTOR_NOTES descriptions may describe the advanced tool and physics. This guide is the entry point for the basic edition.

## Optional polygon worlds

See [POLYGON_CELLWORLD_GUIDE.md](POLYGON_CELLWORLD_GUIDE.md) to draw a room outline and assign CellWorldPoly2D to the existing gun/plant/light fields. Both script-library editions support it.
