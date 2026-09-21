using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMotor2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float moveSpeed = 6f;

    [SerializeField, Min(0f)]
    private float jumpSpeed = 10f;

    [Header("Ground Check")]
    [SerializeField]
    private Transform groundCheck;

    [SerializeField, Min(0.01f)]
    private float groundRadius = 0.12f;

    [Header("Wall Checks")]
    [Tooltip("Empty child placed slightly outside the middle-left side of the player collider.")]
    [SerializeField]
    private Transform wallCheckLeft;

    [Tooltip("Empty child placed slightly outside the middle-right side of the player collider.")]
    [SerializeField]
    private Transform wallCheckRight;

    [SerializeField, Min(0.01f)]
    private float wallCheckRadius = 0.10f;

    [SerializeField]
    private LayerMask groundMask;

    [Header("Wall Slide")]
    [Tooltip("Maximum downward speed while sliding on a wall.")]
    [SerializeField, Min(0f)]
    private float wallSlideSpeed = 2.5f;

    [Tooltip("When enabled, the player must hold toward the wall to slide.")]
    [SerializeField]
    private bool wallSlideRequiresInput = true;

    [Header("Wall Jump")]
    [SerializeField, Min(0f)]
    private float wallJumpHorizontalSpeed = 8f;

    [SerializeField, Min(0f)]
    private float wallJumpVerticalSpeed = 10f;

    [Tooltip("Briefly prevents normal horizontal input from cancelling the wall jump immediately.")]
    [SerializeField, Range(0f, 0.5f)]
    private float wallJumpControlLockDuration = 0.16f;

    [Header("Animation")]
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private SpriteRenderer spriteRenderer;

    [Header("Animation State Names")]
    [SerializeField]
    private string idleAnimationName = "Idle";

    [SerializeField]
    private string runAnimationName = "Run";

    [SerializeField]
    private string jumpUpAnimationName = "JumUp";

    [SerializeField]
    private string jumpFallAnimationName = "JumpFall";

    [SerializeField]
    private string landAnimationName = "Land";

    [SerializeField]
    private string shootAnimationName = "Shoot";

    [Tooltip("Turn this on only after creating an Animator state with the exact name below.")]
    [SerializeField]
    private bool useWallSlideAnimation;

    [SerializeField]
    private string wallSlideAnimationName = "WallSlide";

    private Rigidbody2D body;
    private float moveInputX;
    private bool jumpQueued;
    private bool isUsingTool;
    private bool wasGrounded;
    private float wallJumpControlLockTimer;
    private string currentAnimation;

    public bool IsGrounded { get; private set; }
    public bool IsTouchingLeftWall { get; private set; }
    public bool IsTouchingRightWall { get; private set; }
    public bool IsWallSliding { get; private set; }
    public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    // PlayerInput calls this because the action is named "Move".
    public void OnMove(InputValue value)
    {
        moveInputX = value.Get<Vector2>().x;
    }

    // PlayerInput calls this because the action is named "Jump".
    public void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
            jumpQueued = true;
        }
    }

    // PressureTool2D calls this so movement remains responsible for animation.
    public void SetUsingTool(bool value)
    {
        isUsingTool = value;
    }

    private void FixedUpdate()
    {
        UpdateContactState();

        wallJumpControlLockTimer = Mathf.Max(
            0f,
            wallJumpControlLockTimer - Time.fixedDeltaTime
        );

        if (jumpQueued)
        {
            TryJump();
        }

        ApplyHorizontalMovement();
        ApplyWallSlide();
        UpdateFacing();
        UpdateAnimation();

        jumpQueued = false;
        wasGrounded = IsGrounded;
    }

    private void UpdateContactState()
    {
        IsGrounded =
            groundCheck != null &&
            Physics2D.OverlapCircle(
                groundCheck.position,
                groundRadius,
                groundMask
            ) != null;

        IsTouchingLeftWall =
            wallCheckLeft != null &&
            Physics2D.OverlapCircle(
                wallCheckLeft.position,
                wallCheckRadius,
                groundMask
            ) != null;

        IsTouchingRightWall =
            wallCheckRight != null &&
            Physics2D.OverlapCircle(
                wallCheckRight.position,
                wallCheckRadius,
                groundMask
            ) != null;
    }

    private void TryJump()
    {
        if (IsGrounded)
        {
            body.linearVelocity = new Vector2(
                body.linearVelocity.x,
                jumpSpeed
            );

            PlayAnimation(jumpUpAnimationName);
            return;
        }

        if (!IsTouchingLeftWall && !IsTouchingRightWall)
        {
            return;
        }

        float jumpDirection = GetWallJumpDirection();

        body.linearVelocity = new Vector2(
            jumpDirection * wallJumpHorizontalSpeed,
            wallJumpVerticalSpeed
        );

        wallJumpControlLockTimer = wallJumpControlLockDuration;
        PlayAnimation(jumpUpAnimationName);
    }

    private float GetWallJumpDirection()
    {
        if (IsTouchingLeftWall && !IsTouchingRightWall)
        {
            return 1f;
        }

        if (IsTouchingRightWall && !IsTouchingLeftWall)
        {
            return -1f;
        }

        // This only occurs in a very narrow gap where both checks touch walls.
        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            return -Mathf.Sign(moveInputX);
        }

        return spriteRenderer != null && spriteRenderer.flipX
            ? 1f
            : -1f;
    }

    private void ApplyHorizontalMovement()
    {
        if (wallJumpControlLockTimer > 0f)
        {
            return;
        }

        body.linearVelocity = new Vector2(
            moveInputX * moveSpeed,
            body.linearVelocity.y
        );
    }

    private void ApplyWallSlide()
    {
        bool pressingTowardLeftWall =
            IsTouchingLeftWall && moveInputX < -0.01f;

        bool pressingTowardRightWall =
            IsTouchingRightWall && moveInputX > 0.01f;

        bool touchingAnyWall =
            IsTouchingLeftWall || IsTouchingRightWall;

        bool satisfiesInputRule = wallSlideRequiresInput
            ? pressingTowardLeftWall || pressingTowardRightWall
            : touchingAnyWall;

        IsWallSliding =
            !IsGrounded &&
            body.linearVelocity.y < 0f &&
            satisfiesInputRule;

        if (!IsWallSliding)
        {
            return;
        }

        if (body.linearVelocity.y < -wallSlideSpeed)
        {
            body.linearVelocity = new Vector2(
                body.linearVelocity.x,
                -wallSlideSpeed
            );
        }
    }

    private void UpdateFacing()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (moveInputX > 0.01f)
        {
            spriteRenderer.flipX = false;
        }
        else if (moveInputX < -0.01f)
        {
            spriteRenderer.flipX = true;
        }
    }

    private void UpdateAnimation()
    {
        if (animator == null)
        {
            return;
        }

        if (IsGrounded && !wasGrounded)
        {
            PlayAnimation(landAnimationName);
            return;
        }

        if (IsWallSliding)
        {
            PlayAnimation(
                useWallSlideAnimation
                    ? wallSlideAnimationName
                    : jumpFallAnimationName
            );

            return;
        }

        if (!IsGrounded)
        {
            PlayAnimation(
                body.linearVelocity.y > 0.1f
                    ? jumpUpAnimationName
                    : jumpFallAnimationName
            );

            return;
        }

        if (isUsingTool)
        {
            PlayAnimation(shootAnimationName);
            return;
        }

        PlayAnimation(
            Mathf.Abs(moveInputX) > 0.01f
                ? runAnimationName
                : idleAnimationName
        );
    }

    private void PlayAnimation(string animationName)
    {
        if (string.IsNullOrWhiteSpace(animationName) ||
            currentAnimation == animationName)
        {
            return;
        }

        currentAnimation = animationName;
        animator.Play(animationName);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.DrawWireSphere(
                groundCheck.position,
                groundRadius
            );
        }

        if (wallCheckLeft != null)
        {
            Gizmos.DrawWireSphere(
                wallCheckLeft.position,
                wallCheckRadius
            );
        }

        if (wallCheckRight != null)
        {
            Gizmos.DrawWireSphere(
                wallCheckRight.position,
                wallCheckRadius
            );
        }
    }
}
