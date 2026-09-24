using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One Rigidbody controls walking, jumping, walls, ladders and water.
/// Input callbacks record intent; FixedUpdate changes physics; the Animator displays it.
/// Existing serialized field names are kept so saved scenes retain their settings.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMotor2D : MonoBehaviour
{
    [Header("Movement (world units / second)")]
    [SerializeField, Min(0f)] private float moveSpeed = 6f;
    [SerializeField, Min(0f)] private float jumpSpeed = 10f;
    [SerializeField, Min(1f)] private float acceleration = 70f;
    [SerializeField, Min(1f)] private float deceleration = 90f;
    [SerializeField, Range(0.1f, 1f)] private float airControl = 0.8f;

    [Header("Forgiving Jumps")]
    [Tooltip("A jump still works briefly after walking off an edge.")]
    [SerializeField, Range(0f, 0.3f)] private float coyoteTime = 0.12f;
    [Tooltip("Pressing jump shortly before landing queues a jump on landing.")]
    [SerializeField, Range(0f, 0.3f)] private float jumpBufferTime = 0.14f;
    [Tooltip("Releasing jump early keeps this fraction of upward speed.")]
    [SerializeField, Range(0.1f, 1f)] private float jumpReleaseMultiplier = 0.45f;
    [SerializeField, Range(1f, 3f)] private float fallingGravityMultiplier = 1.35f;
    [SerializeField, Min(1f)] private float maximumFallSpeed = 20f;

    [Header("Ground / Wall Checks")]
    [SerializeField] private Transform groundCheck;
    [SerializeField, Min(0.01f)] private float groundRadius = 0.12f;
    [SerializeField] private Transform wallCheckLeft;
    [SerializeField] private Transform wallCheckRight;
    [SerializeField, Min(0.01f)] private float wallCheckRadius = 0.10f;
    [SerializeField] private LayerMask groundMask;

    [Header("Wall Slide / Jump")]
    [SerializeField, Min(0f)] private float wallSlideSpeed = 2.5f;
    [SerializeField] private bool wallSlideRequiresInput = true;
    [SerializeField, Min(0f)] private float wallJumpHorizontalSpeed = 8f;
    [SerializeField, Min(0f)] private float wallJumpVerticalSpeed = 10f;
    [SerializeField, Range(0f, 0.5f)] private float wallJumpControlLockDuration = 0.16f;

    [Header("Climb / Swim")]
    [Tooltip("The existing CellWorld that holds this level's water. If blank, find it once when enabled.")]
    [SerializeField] private CellWorld2D swimmingWorld;
    [Tooltip("Put a ClimbArea2D trigger over ladders. Move up/down to grab them.")]
    [SerializeField, Min(0.1f)] private float climbSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float swimSpeed = 3.5f;
    [SerializeField, Range(0f, 1f)] private float swimmingWaterThreshold = 0.45f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0f)] private float landingAnimationSeconds = 0.12f;
    [SerializeField] private string idleAnimationName = "Idle";
    [SerializeField] private string runAnimationName = "Run";
    [SerializeField] private string jumpUpAnimationName = "JumUp";
    [SerializeField] private string jumpFallAnimationName = "JumpFall";
    [SerializeField] private string landAnimationName = "Land";
    [SerializeField] private string shootAnimationName = "Shoot";
    [SerializeField] private bool useWallSlideAnimation;
    [SerializeField] private string wallSlideAnimationName = "WallSlide";
    [Tooltip("Optional. Leave empty to reuse Run until climb artwork exists.")]
    [SerializeField] private string climbAnimationName = "";
    [Tooltip("Optional. Leave empty to reuse Run until swim artwork exists.")]
    [SerializeField] private string swimAnimationName = "";

    private Rigidbody2D body;
    private Collider2D playerCollider;
    private readonly Collider2D[] contactResults = new Collider2D[8];
    private readonly HashSet<ClimbArea2D> climbAreas = new HashSet<ClimbArea2D>();
    private Vector2 moveInput;
    private Vector2 lastMoveInput;
    private bool jumpHeld;
    private bool cutJumpQueued;
    private bool isUsingTool;
    private bool controlsEnabled = true;
    private bool wasGrounded;
    private bool groundJumpConsumed;
    private float movementMultiplier = 1f;
    private float originalGravity;
    private float jumpBufferRemaining;
    private float coyoteRemaining;
    private float ignoreGroundRemaining;
    private float wallJumpControlLockTimer;
    private float climbDetachRemaining;
    private float landingRemaining;
    private string currentAnimation;
    private InputAction jumpReleaseAction;

    public bool IsGrounded { get; private set; }
    public bool IsTouchingLeftWall { get; private set; }
    public bool IsTouchingRightWall { get; private set; }
    public bool IsWallSliding { get; private set; }
    public bool IsSwimming { get; private set; }
    public bool IsClimbing { get; private set; }
    public bool ControlsEnabled => controlsEnabled;
    public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        originalGravity = body.gravityScale;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        ListenForJumpRelease();
        // Stage 01 targets the original single-CellWorld levels. Cache this reference once;
        // do not search every physics step or depend on the later multi-room registry.
        if (swimmingWorld == null) swimmingWorld = FindFirstObjectByType<CellWorld2D>();
    }

    private void ListenForJumpRelease()
    {
        PlayerInput input = GetComponent<PlayerInput>();
        // Access PlayerInput.actions, not the shared source asset: PlayerInput may clone actions per player.
        InputAction action = input != null && input.actions != null ? input.actions.FindAction("Jump", false) : null;
        if (action == jumpReleaseAction) return;
        if (jumpReleaseAction != null) jumpReleaseAction.canceled -= OnJumpReleased;
        jumpReleaseAction = action;
        if (jumpReleaseAction != null) jumpReleaseAction.canceled += OnJumpReleased;
    }

    private void OnJumpReleased(InputAction.CallbackContext context) => SetJumpInput(false);

    // PlayerInput's Send Messages calls these methods by action name.
    public void OnMove(InputValue value) => SetMoveInput(value.Get<Vector2>());
    public void OnJump(InputValue value)
    {
        // Send Messages forwards Button presses, but not their canceled/release phase.
        // Listen to that release explicitly so a second jump and short-hop release both work.
        ListenForJumpRelease();
        SetJumpInput(value.isPressed);
    }

    public void SetMoveInput(Vector2 value)
    {
        lastMoveInput = Vector2.ClampMagnitude(value, 1f);
        moveInput = controlsEnabled ? lastMoveInput : Vector2.zero;
    }

    public void SetJumpInput(bool pressed)
    {
        if (!controlsEnabled) return;
        if (pressed && !jumpHeld) jumpBufferRemaining = Mathf.Max(jumpBufferTime, Time.fixedDeltaTime);
        if (!pressed && jumpHeld) cutJumpQueued = true;
        jumpHeld = pressed;
    }

    public void SetUsingTool(bool value) => isUsingTool = value;
    public void SetMovementMultiplier(float value) => movementMultiplier = Mathf.Clamp(value, 0.1f, 3f);
    public void EnterClimbArea(ClimbArea2D area) => climbAreas.Add(area);
    public void ExitClimbArea(ClimbArea2D area) => climbAreas.Remove(area);

    public void SetControlsEnabled(bool value)
    {
        controlsEnabled = value;
        // A key already held when the pause menu closes may not send another input event.
        if (value) moveInput = lastMoveInput;
        if (!value)
        {
            moveInput = Vector2.zero;
            jumpHeld = false;
            jumpBufferRemaining = 0f;
            isUsingTool = false;
            IsClimbing = false;
        }
    }

    // Respawning / changing rooms should discard input that belonged to the old position.
    public void ResetMotion()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        body.linearVelocity = Vector2.zero;
        moveInput = Vector2.zero;
        lastMoveInput = Vector2.zero;
        jumpHeld = cutJumpQueued = groundJumpConsumed = wasGrounded = false;
        jumpBufferRemaining = coyoteRemaining = ignoreGroundRemaining = 0f;
        wallJumpControlLockTimer = landingRemaining = climbDetachRemaining = 0f;
        IsGrounded = IsClimbing = IsSwimming = IsWallSliding = false;
        climbAreas.Clear();
        currentAnimation = null;
        body.gravityScale = originalGravity;
    }

    private void FixedUpdate()
    {
        float step = Time.fixedDeltaTime;
        ignoreGroundRemaining = Mathf.Max(0f, ignoreGroundRemaining - step);
        wallJumpControlLockTimer = Mathf.Max(0f, wallJumpControlLockTimer - step);
        climbDetachRemaining = Mathf.Max(0f, climbDetachRemaining - step);
        landingRemaining = Mathf.Max(0f, landingRemaining - step);
        UpdateContactState();
        UpdateTraversalState();

        if (IsGrounded)
        {
            groundJumpConsumed = false;
            coyoteRemaining = coyoteTime;
            if (!wasGrounded) landingRemaining = landingAnimationSeconds;
        }
        else coyoteRemaining = Mathf.Max(0f, coyoteRemaining - step);

        if (controlsEnabled && jumpBufferRemaining > 0f) TryJump();
        if (cutJumpQueued && body.linearVelocity.y > 0f && !IsSwimming && !IsClimbing)
            body.linearVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.y * jumpReleaseMultiplier);
        cutJumpQueued = false;

        ApplyMovement(step);
        ApplyWallSlide();
        if (controlsEnabled) { UpdateFacing(); UpdateAnimation(); }
        jumpBufferRemaining = Mathf.Max(0f, jumpBufferRemaining - step);
        wasGrounded = IsGrounded;
    }

    private void UpdateContactState()
    {
        Bounds bounds = playerCollider != null ? playerCollider.bounds : new Bounds(transform.position, Vector3.one);
        Vector2 feet = groundCheck != null ? groundCheck.position : new Vector3(bounds.center.x, bounds.min.y, 0f);
        Vector2 left = wallCheckLeft != null ? wallCheckLeft.position : new Vector3(bounds.min.x - 0.02f, bounds.center.y, 0f);
        Vector2 right = wallCheckRight != null ? wallCheckRight.position : new Vector3(bounds.max.x + 0.02f, bounds.center.y, 0f);
        // Ignoring the ground briefly after takeoff prevents the same floor overlap giving a second jump.
        IsGrounded = ignoreGroundRemaining <= 0f && body.linearVelocity.y <= 0.2f && HasSolidContact(feet, groundRadius);
        IsTouchingLeftWall = HasSolidContact(left, wallCheckRadius);
        IsTouchingRightWall = HasSolidContact(right, wallCheckRadius);
    }

    private bool HasSolidContact(Vector2 position, float radius)
    {
        ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(groundMask);
        int count = Physics2D.OverlapCircle(position, radius, filter, contactResults);
        for (int index = 0; index < count; index++)
            if (contactResults[index].attachedRigidbody != body) return true;
        return false;
    }

    private void UpdateTraversalState()
    {
        Vector2 center = playerCollider != null ? playerCollider.bounds.center : transform.position;
        IsSwimming = swimmingWorld != null && swimmingWorld.isActiveAndEnabled &&
            swimmingWorld.IsInitialized && swimmingWorld.GetWaterFraction(center) >= swimmingWaterThreshold;
        bool canClimb = controlsEnabled && climbAreas.Count > 0 && climbDetachRemaining <= 0f && !IsSwimming;
        IsClimbing = canClimb && (IsClimbing || Mathf.Abs(moveInput.y) > 0.1f);
        if (IsSwimming || IsClimbing) { IsGrounded = false; coyoteRemaining = 0f; }
    }

    private void TryJump()
    {
        if (IsSwimming)
        {
            StartJump(body.linearVelocity.x, jumpSpeed * 0.65f);
            return;
        }
        if (IsClimbing)
        {
            IsClimbing = false;
            climbDetachRemaining = 0.25f;
            StartJump(moveInput.x * moveSpeed, jumpSpeed);
            return;
        }
        if (!groundJumpConsumed && (IsGrounded || coyoteRemaining > 0f))
        {
            StartJump(body.linearVelocity.x, jumpSpeed);
            return;
        }
        if (wallJumpControlLockTimer > 0f || (!IsTouchingLeftWall && !IsTouchingRightWall)) return;
        float direction = IsTouchingLeftWall ? 1f : -1f;
        if (IsTouchingLeftWall && IsTouchingRightWall) direction = moveInput.x != 0f ? -Mathf.Sign(moveInput.x) : 1f;
        StartJump(direction * wallJumpHorizontalSpeed, wallJumpVerticalSpeed);
        wallJumpControlLockTimer = wallJumpControlLockDuration;
    }

    private void StartJump(float horizontalSpeed, float verticalSpeed)
    {
        body.linearVelocity = new Vector2(horizontalSpeed, verticalSpeed);
        groundJumpConsumed = true;
        IsGrounded = false;
        coyoteRemaining = jumpBufferRemaining = landingRemaining = 0f;
        ignoreGroundRemaining = 0.1f;
        // Buffered quick taps should still produce short jumps even if released before landing.
        if (!jumpHeld) cutJumpQueued = true;
        PlayAnimation(jumpUpAnimationName);
    }

    private void ApplyMovement(float step)
    {
        Vector2 velocity = body.linearVelocity;
        if (IsClimbing)
        {
            body.gravityScale = 0f;
            body.linearVelocity = moveInput * (climbSpeed * movementMultiplier);
            return;
        }
        if (IsSwimming && controlsEnabled)
        {
            body.gravityScale = 0f;
            Vector2 target = moveInput * (swimSpeed * movementMultiplier);
            // Jump doubles as an upward swim control. Released input naturally settles.
            if (jumpHeld) target.y = swimSpeed * movementMultiplier;
            body.linearVelocity = Vector2.MoveTowards(velocity, target, acceleration * 0.4f * step);
            return;
        }
        body.gravityScale = originalGravity * (velocity.y < 0f ? fallingGravityMultiplier : 1f);
        if (wallJumpControlLockTimer <= 0f)
        {
            float target = moveInput.x * moveSpeed * movementMultiplier;
            float rate = Mathf.Abs(moveInput.x) > 0.01f ? acceleration : deceleration;
            velocity.x = Mathf.MoveTowards(velocity.x, target, rate * (IsGrounded ? 1f : airControl) * step);
        }
        velocity.y = Mathf.Max(velocity.y, -maximumFallSpeed);
        body.linearVelocity = velocity;
    }

    private void ApplyWallSlide()
    {
        bool againstWall = (IsTouchingLeftWall && moveInput.x < -0.01f) || (IsTouchingRightWall && moveInput.x > 0.01f);
        IsWallSliding = controlsEnabled && !IsGrounded && !IsClimbing && !IsSwimming && body.linearVelocity.y < 0f &&
            (wallSlideRequiresInput ? againstWall : IsTouchingLeftWall || IsTouchingRightWall);
        if (IsWallSliding)
        {
            // Physics applies gravity AFTER FixedUpdate. Integrate it here while sliding, then turn
            // native gravity off for this step so it cannot push past the configured slide speed.
            float fallingSpeed = body.linearVelocity.y + Physics2D.gravity.y * body.gravityScale * Time.fixedDeltaTime;
            body.linearVelocity = new Vector2(body.linearVelocity.x, Mathf.Max(fallingSpeed, -wallSlideSpeed));
            body.gravityScale = 0f;
        }
    }

    private void UpdateFacing()
    {
        if (spriteRenderer != null && Mathf.Abs(moveInput.x) > 0.01f) spriteRenderer.flipX = moveInput.x < 0f;
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;
        if (IsClimbing) { PlayAnimation(string.IsNullOrEmpty(climbAnimationName) ? runAnimationName : climbAnimationName); return; }
        if (IsSwimming) { PlayAnimation(string.IsNullOrEmpty(swimAnimationName) ? runAnimationName : swimAnimationName); return; }
        if (IsWallSliding) { PlayAnimation(useWallSlideAnimation ? wallSlideAnimationName : jumpFallAnimationName); return; }
        if (!IsGrounded) { PlayAnimation(body.linearVelocity.y > 0.1f ? jumpUpAnimationName : jumpFallAnimationName); return; }
        if (landingRemaining > 0f) { PlayAnimation(landAnimationName); return; }
        if (isUsingTool) { PlayAnimation(shootAnimationName); return; }
        PlayAnimation(Mathf.Abs(body.linearVelocity.x) > 0.1f ? runAnimationName : idleAnimationName);
    }

    private void PlayAnimation(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName) || currentAnimation == stateName) return;
        if (!animator.HasState(0, Animator.StringToHash(stateName))) return;
        currentAnimation = stateName;
        animator.Play(stateName);
    }

    private void OnDisable()
    {
        if (jumpReleaseAction != null) jumpReleaseAction.canceled -= OnJumpReleased;
        jumpReleaseAction = null;
        if (body != null) body.gravityScale = originalGravity;
        moveInput = Vector2.zero;
        lastMoveInput = Vector2.zero;
        jumpBufferRemaining = 0f;
        jumpHeld = false;
        climbAreas.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (groundCheck != null) Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        if (wallCheckLeft != null) Gizmos.DrawWireSphere(wallCheckLeft.position, wallCheckRadius);
        if (wallCheckRight != null) Gizmos.DrawWireSphere(wallCheckRight.position, wallCheckRadius);
    }
}
