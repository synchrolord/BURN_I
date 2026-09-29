# Studying the pressure tool

Start with `Assets/Game/Scripts/Weapons/PressureTool2D.cs`.

The tool keeps water/flame switching, Pilot/Jet/Wide nozzles, finite supplies,
refills, and the option for unlimited supplies. The saved scenes now use the current
field names directly, so this tool no longer needs old-name compatibility attributes.
The original component name, script GUID, settings, and object references are preserved.

## One name for each concept

| Term | Meaning |
| --- | --- |
| Shot | One moving portion of water or flame. Its data is a `SprayShot`. |
| Spray | The continuous stream made by launching many shots. |
| Cell | One square in the world's simulation grid. A shot is not a cell. |
| Particle | The visual representation Unity draws for a shot. |
| Nozzle settings | The speed, spread, lifetime, supply cost, and rate used when launching. |
| Muzzle | The scene Transform marking where shots start and which way they travel. |
| Supply | The water or fuel in the tool's tank, spent when launching a shot. |
| Water units | The amount of water carried by a shot or stored in grid cells. |

Classes use PascalCase (`SprayShot`). Variables use camelCase (`shot`,
`activeShots`, `nozzleSettings`). The type names the kind of object; the variable
names the particular object or value being used.

## Names include units where they matter

| Name | Meaning |
| --- | --- |
| `shotsPerSecond` | How often the nozzle launches a shot, before the water rate multiplier. |
| `shotSpeed` | World units travelled per second, before the water speed multiplier. |
| `shotLifetimeSeconds` | How long a newly launched shot lasts, before the water lifetime multiplier. |
| `remainingLifetimeSeconds` | How much flight time a particular shot has left. |
| `remainingWaterUnits` | How much water that shot can still apply to the world. |
| `isWater` | A yes/no check, not an amount of water. |
| `ignitionRadiusCells` | Flame effect radius measured in grid cells. Zero affects only the sampled cell. |
| `spreadAngleDegrees` | The full angular spread of a nozzle, in degrees. |
| `directionAngleDegrees` | The shot's travel direction converted to degrees for drawing. |

`Mathf.Rad2Deg` is Unity's radians-to-degrees conversion constant. "Rad" here
means radians, not radius. The renderer uses it because `Atan2` returns radians.
Shot widths and positions use world units; ignition radius uses cell counts.

The old `radiusInCells` and `ignitionRadiusInCells` settings were combined with
`Max`. Every saved nozzle already had its ignition radius greater than or equal
to the other value. There is now one `ignitionRadiusCells` setting, retaining all
six saved nozzles' effective radii.

## The main loop

1. `MoveShots`: advance existing shots.
2. `LaunchShots`: add shots while spraying and supplies are available.
3. `sprayVisuals.Draw`: draw the shots.

`activeShots` is a `List<SprayShot>`, similar to Java's `ArrayList`.
It contains only flying shots. Launching adds a small data object; finishing
removes it. The existing maximum-shot setting still caps the number in flight.

## Who does what?

| File | Responsibility |
| --- | --- |
| `PressureTool2D.cs` | Controls, settings, supplies, launch timing, and shot movement/lifetime. |
| `CellWorld2D.Spray.cs` | Wall detection, path sampling, plant watering, extinguishing, and water deposits. Part of the existing CellWorld class. |
| `PressureSprayVisuals.cs` | One ParticleSystem and a particle buffer used to draw shots. A plain helper, not a component to attach. |

`TraceShotPath` returns a `ShotPath`: `startPosition`, `endPosition`,
`insideSurfacePosition`, and `hitObstacle`. The tool does not calculate cell
offsets or inspect plant cells. `ApplyWaterShot` returns the water still in flight;
`ApplyFlameShot` ignites the path and any hit surface.

Read `Update`, the `SprayShot` fields, `LaunchShots`, then `MoveShots`.
Treat world calls as named operations first. Study their implementations after
the tool's flow makes sense, and the drawing helper last.

The liquid grid's flow rules and the spray's tuning/appearance formulas remain
unchanged. The existing input callback names stay compatible with PlayerInput;
their calls now use the clearer `SprayType` vocabulary.

Archived tool packages predate these helpers. The edition switcher blocks replacing
this tool with those packages; use a complete Git version to return to older code.
