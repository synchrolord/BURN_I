# Basic pressure tool

`Assets/Game/Scripts/Weapons/PressureTool2D.cs` is now one complete script instead of six partial files. Read `Update`, then `LaunchDrops`, `MoveDrops`, and `DrawDrops`. These are the four steps performed while the game runs.

The tool still switches between water and burning flame with Q, scrolls through Pilot/Jet/Wide, travels through the air, hits walls, cools fire, wets plants, and spends/refills water and fuel. Water remains four times faster than the nozzle's flame speed. Each drop carries a limited water payload, so firing through several cells cannot create unlimited water. Flame changes from yellow to orange and fades as each drop ages.

The drop array is created once. A free slot is reused when its lifetime reaches zero. There is one built-in Unity ParticleSystem for drawing the entire spray; firing does not create a GameObject for every drop. The ParticleSystem does not control the gameplay simulation. `MoveDrops` does that, and `DrawDrops` gives Unity the resulting positions and colors.

## Where to change things

- **Pilot/Jet/Wide → speed:** flame launch speed in world units per second.
- **Water Speed Multiplier:** multiplies the nozzle speed for water. Start here to tune the water gun's speed.
- **Nozzle lifetime:** how long a drop travels. More time means more range.
- **Nozzle angle degrees:** width of the cone.
- **Nozzle probes per second:** number of drops fired each second.
- **Flame Lift/Drag:** rise and slowing of flames.
- **Water Gravity Scale:** how strongly water arcs downward.
- **Water/Flame Width:** visual thickness.
- **Unlimited Resources For Testing:** turn off to spend water and fuel.
- **Obstacle Mask:** include the solid ground and solid plant layers.
- **Muzzle:** the barrel tip; its red local X arrow points forward.

Inspector values saved on the scene override the defaults written in C#. Change the Inspector for an existing player. Defaults in code apply when a new component is added or reset.

## What is intentionally simpler

There is no Perlin-noise turbulence, random speed, inherited player velocity, water drag, jittered deposits, optional oil residue, cone gizmo, or hand-built mesh. Nozzle spread, flame lift, two-color fading, and the existing pressure-spray material still provide motion and visible shape. Wide water spreads through its firing cone; each drop deposits at its contact cell instead of a circular water brush. These are visual/motion simplifications, not new gameplay systems.

## Dependencies and swapping

Requires the player Input Actions (`Spray`, `SwitchMaterial`, `NozzleScroll`), `PlayerMotor2D.SetUsingTool`, the existing CellWorld public methods, and `Assets/Resources/Effects/PressureSpray.mat` with its shader. `PlayerAim2D` rotates the muzzle separately. The script's Unity `.meta` ID is unchanged, so saved scene references survive.

Remove all five old companion partial scripts **and their `.meta` files** when installing this basic version:

```
Assets/Game/Scripts/Weapons/PressureTool2D.Input.cs
Assets/Game/Scripts/Weapons/PressureTool2D.Presentation.cs
Assets/Game/Scripts/Weapons/PressureTool2D.Resources.cs
Assets/Game/Scripts/Weapons/PressureTool2D.Simulation.cs
Assets/Game/Scripts/Weapons/PressureTool2D.Visuals.cs
```

To restore the advanced tool, replace the basic `PressureTool2D.cs` with the archived main file and restore **all five companion scripts together**, including their `.meta` files. They are pieces of one class, not separate optional upgrades. Both CellWorld editions provide the methods this tool needs. Restore Advanced CellWorld separately when you want its more elaborate fluid motion. Do not copy an advanced partial beside the basic complete class.

The read-only properties `CurrentWater`, `MaximumWater`, `CurrentFuel`, `MaximumFuel`, `CurrentMaterial`, and `ActiveProbeCount` let a UI read the tool. `AddWaterResource`, `AddFuelResource`, and `RefillAllResources` are the simple refill entry points. In C#, `=>` is a short method/property whose result is the expression on its right; it does not mean a separate thread or delayed execution.

