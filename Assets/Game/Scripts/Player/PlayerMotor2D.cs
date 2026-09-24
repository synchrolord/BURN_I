using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// PlayerInput reads the buttons. This script moves the Rigidbody and chooses an animation.
// The saved field names stay the same so existing scenes keep their player settings.
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMotor2D : MonoBehaviour
{
    [Header("Walking and jumping")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpSpeed = 10f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.14f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundRadius = 0.12f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float climbSpeed = 3.5f;

    [Header("Existing Animator state names")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string idleAnimationName = "Idle";
    [SerializeField] private string runAnimationName = "Run";
    [SerializeField] private string jumpUpAnimationName = "JumUp";
    [SerializeField] private string jumpFallAnimationName = "JumpFall";
    [SerializeField] private string shootAnimationName = "Shoot";

    private Rigidbody2D body;
    private Vector2 moveInput;
    private bool isUsingTool;
    private float normalGravity;
    private float coyoteRemaining;
    private float jumpBufferRemaining;
    private float ladderDetachRemaining;
    private string currentAnimation;
    private readonly HashSet<ClimbArea2D> ladders = new HashSet<ClimbArea2D>();

    public bool IsGrounded { get; private set; }
    public bool IsClimbing { get; private set; }
    public Vector2 Velocity
    {
        get
        {
            return body.linearVelocity;
        }
    }

    // Unity calls Awake once when this player is created.
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        normalGravity = body.gravityScale;
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    // PlayerInput calls these because the actions are named Move and Jump.
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
            // Remember the press briefly. No held flag means Button releases cannot get stuck.
            jumpBufferRemaining = Mathf.Max(jumpBufferTime, Time.fixedDeltaTime);
        }
    }

    public void SetUsingTool(bool value)
    {
        isUsingTool = value;
    }

    public void EnterClimbArea(ClimbArea2D area)
    {
        ladders.Add(area);
    }

    public void ExitClimbArea(ClimbArea2D area)
    {
        ladders.Remove(area);
    }

    // Unity calls FixedUpdate at a steady rate. Change physics here, not in the input callbacks.
    private void FixedUpdate()
    {
        float step = Time.fixedDeltaTime;
        IsGrounded = TouchesGround();
        coyoteRemaining = Mathf.Max(0f, coyoteRemaining - step);
        if (IsGrounded)
        {
            coyoteRemaining = coyoteTime;
        }

        if (ladders.Count == 0)
        {
            IsClimbing = false;
        }
        if (ladders.Count > 0 && Mathf.Abs(moveInput.y) > 0.1f && ladderDetachRemaining <= 0f)
        {
            IsClimbing = true;
        }

        Vector2 velocity = body.linearVelocity;
        velocity.x = moveInput.x * moveSpeed;
        body.gravityScale = normalGravity;
        if (IsClimbing)
        {
            body.gravityScale = 0f;
            velocity.y = moveInput.y * climbSpeed;
            IsGrounded = false;
            coyoteRemaining = 0f;
        }

        if (jumpBufferRemaining > 0f && (IsGrounded || coyoteRemaining > 0f || IsClimbing))
        {
            velocity.y = jumpSpeed;
            IsGrounded = false;
            IsClimbing = false;
            coyoteRemaining = 0f;
            jumpBufferRemaining = 0f;
            ladderDetachRemaining = 0.25f;
            body.gravityScale = normalGravity;
        }

        body.linearVelocity = velocity;
        jumpBufferRemaining = Mathf.Max(0f, jumpBufferRemaining - step);
        ladderDetachRemaining = Mathf.Max(0f, ladderDetachRemaining - step);
        UpdateAppearance();
    }

    private bool TouchesGround()
    {
        // The feet can still overlap the floor on takeoff. Rising is never grounded.
        if (groundCheck == null || body.linearVelocity.y > 0.1f)
        {
            return false;
        }
        Collider2D[] hits = Physics2D.OverlapCircleAll(groundCheck.position, groundRadius, groundMask);
        foreach (Collider2D hit in hits)
        {
            if (!hit.isTrigger && hit.attachedRigidbody != body)
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateAppearance()
    {
        if (spriteRenderer != null && moveInput.x != 0f)
        {
            spriteRenderer.flipX = moveInput.x < 0f;
        }
        string nextAnimation = idleAnimationName;
        if (moveInput.x != 0f || (IsClimbing && moveInput.y != 0f))
        {
            nextAnimation = runAnimationName;
        }
        if (!IsGrounded && !IsClimbing)
        {
            nextAnimation = jumpFallAnimationName;
            if (body.linearVelocity.y > 0f)
            {
                nextAnimation = jumpUpAnimationName;
            }
        }
        else if (isUsingTool)
        {
            nextAnimation = shootAnimationName;
        }
        if (animator != null && nextAnimation != currentAnimation && animator.HasState(0, Animator.StringToHash(nextAnimation)))
        {
            animator.Play(nextAnimation);
            currentAnimation = nextAnimation;
        }
    }

    // Restore gravity if this component is switched off while the player is on a ladder.
    private void OnDisable()
    {
        if (body != null)
        {
            body.gravityScale = normalGravity;
        }
        moveInput = Vector2.zero;
        isUsingTool = false;
        currentAnimation = null;
        jumpBufferRemaining = 0f;
        coyoteRemaining = 0f;
        IsClimbing = false;
        ladders.Clear();
    }
}
