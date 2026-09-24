# Basic edition checks — 24 September 2026

The live scene, prefab, artwork and input files were preserved. Runtime verification used a separate Unity 6000.6.0f1 project copy with the saved Level_0 opened before entering Play mode.

| Module (including companions and comments) | Previous C# lines | Basic C# lines |
| --- | ---: | ---: |
| Movement | 421 | 273 |
| Tool | 828 | 326 |
| CellWorld | 2409 | 1185 |
| Plants | 411 | 416 |
| Total | 4069 | 2200 |

Plant geometry helpers remain substantial: supporting existing baked masks, transformed sprites and healthy collision requires that code. The plant update now uses a simple loop instead of a dirty queue. Read BurnablePlantTile first when changing plant behavior. Other already-small camera/aim/animation/UI/light scripts and the editor plant-authoring tools were retained.

Completed checks:

- All 16 combinations of Basic/Advanced Movement, Tool, CellWorld and Plants compile. This checks compatibility of script calls; it is not a claim that every mixed edition received a separate full playthrough.
- The user-facing switcher successfully installed Advanced then Basic for each of the four modules in the isolated copy. File hashes and removal of obsolete tool partials were verified.
- All original script metadata IDs were retained. Existing Level_0 script references and the ladder prefab resolve.
- Real PlayerInput keyboard/mouse events exercise movement, repeated jumping, held/air jump behavior, ladders, Q switching, nozzle scroll and spray release.
- Tool checks cover finite supplies, fast water, particle geometry/material support, thin-wall impact, plant cooling and flame ignition.
- Grid checks cover liquid conservation/shared capacity, floor/corner blocking, waking neighboring chunks, simple oil flotation, smoke decay and fire/cooling accounting.
- Actual painted plants register at scene startup, select degraded artwork and disappear. A delayed world also registers its plants when ready.
- A cloned real plant keeps its healthy collision while degraded, loses collision and grid blockage when fully burned, and observes temporary water protection.

The basic edition intentionally changes movement and fluid behavior; see BEGINNER_CODE_GUIDE.md and the module READMEs. These focused automated checks do not replace trying the game feel yourself. No performance benchmark or all-level playthrough was claimed.

The scripts in Assets are editable working copies. ScriptLibrary contains reference versions that Git can save with your commit. No commit or push was made automatically.

Detailed runtime output:

```text
PASS saved Level_0 and ladder prefab have valid script references
BASIC MOVEMENT PASS: The saved scene has an enabled player motor
BASIC MOVEMENT PASS: The actual player has PlayerInput, a Rigidbody and a collider
BASIC MOVEMENT PASS: Keyboard events use the scene's Send Messages input path
BASIC MOVEMENT PASS: The existing ground probe and terrain mask are assigned
BASIC MOVEMENT PASS: PlayerInput initialized its own device user
BASIC MOVEMENT PASS: The real Jump Button and Move Value actions are present
BASIC MOVEMENT PASS: The saved player recognizes a physical floor with its original ground probe
BASIC MOVEMENT PASS: A keyboard D event reaches the actual OnMove callback
BASIC MOVEMENT PASS: Walking right moves the real Rigidbody at the saved movement speed
BASIC MOVEMENT PASS: Walking left reverses direction
BASIC MOVEMENT PASS: Releasing movement stops horizontal motion
BASIC MOVEMENT PASS: Real Space press 1 queues a jump
BASIC MOVEMENT PASS: Grounded keyboard jump 1 launches; velocity=(0.00, 15.41)
BASIC MOVEMENT PASS: Jump 1 lands before the next fresh press
BASIC MOVEMENT PASS: Real Space press 2 queues a jump
BASIC MOVEMENT PASS: Grounded keyboard jump 2 launches; velocity=(0.00, 15.41)
BASIC MOVEMENT PASS: Another Space press while rising cannot create an air double jump
BASIC MOVEMENT PASS: Jump 2 lands before the next fresh press
BASIC MOVEMENT PASS: Real Space press 3 queues a jump
BASIC MOVEMENT PASS: Grounded keyboard jump 3 launches; velocity=(0.00, 15.41)
BASIC MOVEMENT PASS: Jump 3 lands before the next fresh press
BASIC MOVEMENT PASS: Holding Space through landing does not automatically jump again
BASIC MOVEMENT PASS: The test ladder and player layers are allowed to interact
BASIC MOVEMENT PASS: Walking into the ladder trigger registers it without automatically climbing
BASIC MOVEMENT PASS: W climbs inside the trigger at the saved climb speed
BASIC MOVEMENT PASS: Releasing movement holds the player on the ladder
BASIC MOVEMENT PASS: Space detaches from the ladder and launches a normal jump
BASIC MOVEMENT PASS: Holding W cannot immediately cancel the ladder jump
BASIC MOVEMENT PASS: The ladder jump returns to the floor
BASIC MOVEMENT PASS: The player can grab the ladder again after landing
BASIC MOVEMENT PASS: Moving out of the physical ladder trigger removes it and restores gravity
BASIC_MOVEMENT_CHECKS_PASSED
PASS Level_0 pressure tool retains world, muzzle and player references
PASS small tool fixture initializes paused
PASS real Q press selects flame
PASS Q can switch back after release
PASS real mouse scroll changes nozzle
PASS held mouse button emits traveling water and spends finite supplies
PASS water launches faster than 20 world units per second
PASS spray produces visible particle geometry using a supported material
PASS holding spray spends exactly the available water without going negative
PASS real mouse release stops firing
PASS contact fixture has a burning plant cell
PASS fast water stops at a thin wall and cools its burning plant without duplicating water
PASS traveling flame ignites cells along its path
PASS 96 x 32 fixture initializes with a physics-baked floor
PASS floor collider is actually baked into the grid
PASS 240 ticks of falling, leveling and oil exchange conserve both liquids
PASS liquids share capacity without occupying solid cells
PASS solid floor rejects added water
PASS water cannot leak diagonally through a blocked wall corner
PASS flow across a chunk edge wakes its sleeping receiving chunk
PASS cross-chunk flow keeps its finite payload
PASS one adjacent oil/water swap preserves each cell's total volume
PASS a trapped vertical pair separates with fuel above water
PASS trapped smoke stays awake long enough to fade completely
PASS ordinary liquid fuel burns once, not twice, in a tick
PASS fuel burn timing honors the configured tick interval
PASS three contact-water units extinguish fire without duplicating stored water
PASS saved scene contains its authored plant maps
PASS Burnable Plants: painted CornerVine1 has its baked mask
PASS Burnable Plants: painted CornerVine2 has its baked mask
PASS Burnable Plants: painted CrossVine1 has its baked mask
PASS Burnable Plants: painted CrossVine2 has its baked mask
PASS Burnable Plants: painted EndVine1 has its baked mask
PASS Burnable Plants: painted EndVine2 has its baked mask
PASS Burnable Plants: painted strightVine1 has its baked mask
PASS Burnable Plants: painted strightVine2 has its baked mask
PASS Burnable Plants: painted strightVine3 has its baked mask
PASS Burnable Plants: painted TVine1 has its baked mask
PASS Burnable Plants: authored room initialized
PASS Burnable Plants: all 10 authored plants registered during scene startup
PASS saved scene has a real painted plant to burn
PASS CornerVine1: actual painted instance begins healthy
PASS CornerVine1: painted pixel mask produced anchored fuel
PASS CornerVine1: ordinary flame consumes the actual plant's fuel
PASS CornerVine1: burning selects a degraded sprite before disappearance
PASS CornerVine1: anchored fire remains awake until completely burned
PASS CornerVine1: fully burned plant disappears
PASS plant map waits while its assigned room is not enabled yet
PASS plant map automatically registers when its delayed room initializes
PASS CornerVine1: actual painted instance begins healthy
PASS CornerVine1: painted pixel mask produced anchored fuel
PASS CornerVine1: ordinary flame consumes the actual plant's fuel
PASS CornerVine1: burning selects a degraded sprite before disappearance
PASS CornerVine1: anchored fire remains awake until completely burned
PASS CornerVine1: fully burned plant disappears
BASIC PLANT PASS: A painted plant has baked fuel, burn stages and a healthy physics shape
BASIC PLANT PASS: The single painted plant registers normally against its initialized world
BASIC PLANT PASS: Real baked pixels provide cells to ignite through the public API
BASIC PLANT PASS: Healthy Sprite collision also blocks its CellWorld cells
BASIC PLANT PASS: Dry anchored fuel accepts ignition
BASIC PLANT PASS: A real water impact consumes cooling units and extinguishes the plant
BASIC PLANT PASS: Wet plant fuel refuses immediate reignition
BASIC PLANT PASS: WetSeconds still suppresses fire before its simulation-time deadline
BASIC PLANT PASS: The plant accepts fire after WetSeconds expires
BASIC PLANT PASS: Actual fire advances the plant to its first degraded stage
BASIC PLANT PASS: Degraded artwork retains the complete healthy collision outline and solid grid cell
BASIC PLANT PASS: The fully burned plant disappears
BASIC PLANT PASS: Finishing removes physical collision and refreshes the formerly blocked CellWorld cell
BASIC PLANT PASS: The original imported plant settings and baked mask remain unchanged
ALL_BASIC_EDITION_CHECKS_PASSED
```
