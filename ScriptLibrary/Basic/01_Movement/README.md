# Basic player movement

The basic motor has one straightforward physics loop: check the floor, remember a recent jump press, handle a ladder if present, set velocity, then select an animation. There is no dependency on CellWorld, infection, menus, checkpoints, or a room manager.

Kept: walking with immediate start/stop, grounded fixed-height jumping, repeated Space presses, 0.12-second coyote time, 0.14-second jump buffering, facing left/right, the existing Idle/Run/JumUp/JumpFall/Shoot animation links, and ladder climbing. Existing saved speed, jump speed, ground probe, ground radius, ground layer mask, climb speed, renderer, Animator and retained animation-name fields keep their names and values. The original `.meta` files keep the same script GUIDs.

Deliberately left in the advanced archive: smooth acceleration/braking, variable-height jump release, extra falling gravity, maximum fall speed, wall sliding/jumping, swimming, dedicated landing/climb/swim animation states, and pause/respawn/movement-modifier APIs. The basic motor does not play Land; it returns to Idle or Run on landing. The existing Land clip is not deleted. Existing water still simulates, but the player does not swim automatically. Old advanced Inspector fields disappear from this component after Unity reloads it; they are not hidden active behavior.

PlayerInput stays on Send Messages with Move and Jump actions. Each Jump press queues one jump. The motor never stores a `jumpHeld` flag or waits for a Button release callback, so the previous stuck-release failure cannot block later jumps. Holding Space does not automatically jump again. Releasing Space does not change jump height in this version.

The **Ground Check** reference is required and must point to the existing transform at the player's feet. The current Level_0/player prefab already supplies it. **Ground Mask** must include the solid terrain layer. The ground check skips triggers and all colliders attached to the player's own Rigidbody. It returns false while rising, so overlap with the floor immediately after takeoff cannot grant another jump. For a new player, assign the ground-check transform explicitly; this basic version has no automatic collider-bounds fallback.

Ladders use the existing ClimbArea2D trigger. Enter it and press W/S to grab, then use W/S to climb and A/D to leave sideways. Releasing movement holds position. Space jumps away and prevents immediate re-grabbing for 0.25 seconds. Exiting or disabling the ladder restores normal gravity on the next physics step. The ladder keeps a small contact count so multiple player colliders cannot make it detach too early.

For Java readers: `Awake` is Unity's initialization callback, `FixedUpdate` is its physics callback, and `OnDisable` clears transient state. `[SerializeField]` makes private settings editable and saved in Unity. `IsGrounded { get; private set; }` allows other scripts to read the property while only this class changes it. The HashSet records distinct overlapping ladders; Dictionary is the C# counterpart to a Java map. `OverlapCircleAll` returns nearby colliders for a simple loop; one small allocation per player physics update is accepted here to keep the ground test easy to follow.

Changed files: `Assets/Game/Scripts/Player/PlayerMotor2D.cs`, `ClimbArea2D.cs`, and their unchanged `.meta` companions. PlayerAim2D and PlayerAnimation are already small direct scripts and were left as they are.


See `Docs/BASIC_EDITION_VALIDATION.md` in the project for the completed checks. Use the script switcher to install either edition with matching metadata.
