# Verification record

Checked on 21 September 2026 with Unity **6000.6.0f1** on Windows, using an isolated project copy. The original Unity project folder was not edited.

## Passed

- Full Unity script compilation and import of the game assets; standalone compilation against the installed Unity and package assemblies also passed.
- Real Input System keyboard/mouse events routed through PlayerInput: Q press/hold/release/second press, left mouse press/release, and nozzle scrolling.
- Deterministic Jet water launch at **26 units/second**, with nozzle randomness disabled for the measurement.
- Water travels before depositing; each shot has a bounded payload, spent once on cooling and/or stored liquid.
- Fast water stops at a thin wall during a long simulation step and ignores its player's collider.
- Water cools an intervening burning cell immediately: three units extinguish energy 3 and the remaining unit is stored from a four-unit shot.
- Water conservation over 60 ticks; mixed oil/water quantity conservation over 60 ticks without burning.
- Separate resource spending, no negative tank values from emission, no resource spend when the pool is full, and probe/mesh cleanup on disable.
- Rebuilding transient probe data after the equivalent of an editor script reload.
- All game prefabs load without missing scripts. Level_0 starts, has no missing scripts in its scene hierarchy, places the muzzle inside the cell grid, emits both modes, and ignites its CellWorld.
- Spray shader imported and compiled for the rendered camera. Water and flame screenshots were captured from Level_0 and visually inspected.
- All 11,247 original snapshot files were accounted for after moves. Original `.meta` contents were preserved, with no duplicate asset GUIDs.

The batch harness completed **102 assertions**. It temporarily held assembly reloads during the short play sequence because installed editor packages requested a reload during startup. An unrelated Unity Search startup `ArgumentOutOfRangeException` was logged before the play checks; the test completed afterward. The final run used a shorter path to avoid Windows path-length errors in an installed package's editor stylesheet loader.

## Scope and limits

These are targeted runtime and preservation checks, not a built-player certification or exhaustive manual playthrough of every level. No standalone player build was produced. The old recovery scene still has its original stale script reference and is kept as an archive. Large grids retain their existing full-texture rendering costs.

After merging, test your normal movement, wall interactions and scene edits in the Game view, especially if you changed the project after the snapshot used for this branch.

## Reproduce the runtime checks

The harness is stored outside Unity's Assets folder at `Tools/Validation/PressureCheck.cs`, so it is not part of normal game compilation.

1. Use a separate project copy with a short path; keep the Unity version and package files unchanged.
2. Copy the harness into that copy's `Assets/Game/Editor/PressureCheck.cs`.
3. Run Unity in batch mode with that copy's `-projectPath`, `-executeMethod PressureCheck.Run`, and a `-logFile` path. Do not add `-quit`: the harness exits after its coroutine finishes. Run with graphics enabled to capture the two images.
4. Read `ValidationResults/check-result.txt` and the Unity log. A completed suite ends with `COMPLETE`. Screenshots are saved beside the report. Generated results are ignored by Git.

The harness deliberately constructs its own test scene, controls Input System update settings, and loads Level_0. It refuses to run through its entry point outside batch mode. Use it only in an isolated copy.
