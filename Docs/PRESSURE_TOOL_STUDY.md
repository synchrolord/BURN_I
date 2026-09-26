# Studying the simplified pressure tool

Start with `Assets/Game/Scripts/Weapons/PressureTool2D.cs`.

The tool still has water/flame switching, Pilot/Jet/Wide nozzles, finite supplies,
refill methods, and the testing option for unlimited supplies. Existing Inspector
field names, values, input message names, and the original script GUID are retained.
No scene or prefab needs rewiring.

## The main loop

1. Move the existing drops.
2. Launch new drops while the button is held and supplies are available.
3. Draw the drops.

`drops` is a normal `List<Drop>`, similar to Java's `ArrayList`.
It contains only flying drops. Launching adds a small data object; finishing removes
it. There are no empty slots, reusable-slot markers, or separate active-drop counter.
The existing maximum-drop setting still caps the number flying at once.

## Who does what?

| File | Responsibility |
| --- | --- |
| `PressureTool2D.cs` | Controls, nozzle settings, supplies, emission timing, and drop velocity/lifetime. |
| `CellWorld2D.Spray.cs` | Finds the first wall, samples the travelled path, wets plants, extinguishes fire, and deposits remaining water. This is part of the existing CellWorld class. |
| `PressureSprayVisuals.cs` | Sets up one ParticleSystem and draws the drops. This is a plain helper object, not a component to attach. |

`TraceSpray` returns a `SprayPath`: start, reachable endpoint, inside-surface point,
and whether the path hit a wall. The tool does not calculate cell offsets or inspect
plant cells. `ApplyWaterSpray` returns the amount of water still airborne.
`ApplyFlameSpray` ignites the travelled path and any hit surface.

The liquid grid's flow rules are unchanged. Water leaving the tool still enters that
same simulation. Water speed, gravity, spread, lifetime, flame lift/drag, supply costs,
and the particle appearance formulas are retained.

## Study order

Read `Update`, then the `Drop` fields, `LaunchDrops`, and `MoveDrops`.
Treat the three world calls as named operations first. Study their implementations
after the tool's flow makes sense. Study the drawing helper last.

The simplification removes manual slot management and a redundant nearest-hit search.
It also gives world interactions and drawing their own homes. Retaining all the
features still requires settings and input code; total project code is not drastically
shorter. Each shot now allocates a small plain C# object instead of recycling one.

Archived Basic/Advanced tool packages predate these helpers. The edition switcher
blocks replacing this tool with those packages; use a complete Git version when
returning to an older implementation.
