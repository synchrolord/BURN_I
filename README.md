# BURN_I

A Unity 2D platformer prototype built around a cell simulation: water flows, oil floats and burns, fire spreads, and smoke rises. The player runs, jumps, slides on walls, and aims a pressure tool that sprays water or burning flame.

Open with **Unity 6000.6.0f1**. Start with **Assets/Game/Scenes/Levels/Level_0.unity**.

- **A / D:** move; **Space:** jump / wall jump.
- **Mouse:** aim; **left button:** spray.
- **Q:** switch water / burning flame.
- **Mouse wheel:** Pilot / Jet / Wide nozzle.

The tool starts with unlimited resources enabled for testing. Turn that off on its `PressureTool2D` component to use separate water and fuel tanks.

## Documentation

- [Project guide](Docs/PROJECT_GUIDE.md): systems, script map, Inspector tuning, troubleshooting, and modification examples.
- [GitHub Desktop guide](Docs/GITHUB_DESKTOP_GUIDE.md): save a checkpoint, merge this branch, and undo it.
- [Refactor notes](Docs/REFACTOR_NOTES.md): what moved, compatibility, and verification.

Game code, prefabs, levels, and settings live under `Assets/Game`. Imported packs live under `Assets/ThirdParty`; older copies and recovery scenes remain under `Assets/Legacy`. Existing asset IDs are preserved.
