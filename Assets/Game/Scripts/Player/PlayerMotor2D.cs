using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMotor2D : MonoBehaviour
{
    
    
    
    [Header("Walking and jumping")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float sprintMultiplier = 2f;
    [SerializeField] private float jumpSpeed = 10f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.14f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundRadius = 0.12f;
    [SerializeField] private LayerMask groundMask;
    
    private bool isRunning = false;
    
    

    
    [Header("Animation timing")]
    [SerializeField, Min(0f)]
    private float apexVelocityThreshold = 1f;

    [SerializeField, Min(0f)]
    private float landingPoseSeconds = 0.1f;

    private bool wasGrounded;
    private float landingPoseRemaining;
    
    [SerializeField] private PlayerVitals2D playerVitals;
    
    
    
    [Header("Existing Animator state names")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    private static readonly int IdleAnimation =
        Animator.StringToHash("Idle");

    private static readonly int RunAnimation =
        Animator.StringToHash("Run");

    private static readonly int JumpUpAnimation =
        Animator.StringToHash("JumpUp");

    private static readonly int JumpFallAnimation =
        Animator.StringToHash("JumpFall");

    private static readonly int ShootAnimation =
        Animator.StringToHash("Shoot");
    
    private static readonly int JumpApexAnimation =
        Animator.StringToHash("JumpApex");

    private static readonly int LandAnimation =
        Animator.StringToHash("Land");
    
    private static readonly int DeadAnimation =
        Animator.StringToHash("Dead");
    
    private static readonly int SprintAnimation= 
        Animator.StringToHash("Sprint");

    private Rigidbody2D body;
    private Vector2 moveInput;
    private bool isUsingTool;
    private float coyoteRemaining;
    private float jumpBufferRemaining;
    private int currentAnimation;


    
    public bool IsGrounded { get; private set; }
    
    
    
    
    
    private void Start()
    {
        IsGrounded = TouchesGround();
        wasGrounded = IsGrounded;
    }


    //set up body, animator, and spriteRenderer.
    private void Awake()
    {
        if (playerVitals == null)
            playerVitals = GetComponent<PlayerVitals2D>();
        
        body = GetComponent<Rigidbody2D>();
        if (animator == null) { animator = GetComponentInChildren<Animator>(); }
        if (spriteRenderer == null){ spriteRenderer = GetComponentInChildren<SpriteRenderer>();}
    }

 
    //store players input as vector2
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnRun(InputValue value)
    {
        // value.isPressed is true when held, false when released
        if (value.isPressed)
        {
            isRunning = true;
        }
        else
        {
            isRunning = false;
        }
    }

    //store jump input for some time to allow queueing jumps.
    public void OnJump(InputValue value)
    {
       
        if (value.isPressed)
        {
            jumpBufferRemaining = Mathf.Max(jumpBufferTime, Time.fixedDeltaTime);
        }
    }
    
    private bool TouchesGround()
    {
        // groundcheck object exists and player inst moving upwards
        if (groundCheck == null || body.linearVelocity.y > 0.1f)
        {
            return false;
        }
        
        //which collider is overlapping groundCheck layers.
        Collider2D[] hits = Physics2D.OverlapCircleAll(groundCheck.position, groundRadius, groundMask);
        
        foreach (Collider2D hit in hits)
        {
            //if not a trigger or the player's collider
            if (!hit.isTrigger && hit.attachedRigidbody != body){return true;}
        }
        return false;
    }
    
    public void SetUsingTool(bool value)
    {
        isUsingTool = value;
    }
    

    
  
    private void FixedUpdate()
    {
        
        
        float step = Time.fixedDeltaTime;
        
        if (playerVitals != null && playerVitals.IsDead)
        {
            moveInput = Vector2.zero;

            body.linearVelocity = new Vector2(
                0f,
                body.linearVelocity.y
            );

            UpdateAppearance();
            return;
        }
        
        
        bool groundedNow = TouchesGround();

        if (!wasGrounded && groundedNow)
            landingPoseRemaining = landingPoseSeconds;

        IsGrounded = groundedNow;
        landingPoseRemaining =
            Mathf.Max(0f, landingPoseRemaining - step);
        
        
        coyoteRemaining = Mathf.Max(0f, coyoteRemaining - step);
        
        if (IsGrounded)
        {coyoteRemaining = coyoteTime; }
        
        //reset players velocity, gravity
        Vector2 velocity = body.linearVelocity;
        
        velocity.x = isRunning ? 
            moveInput.x * moveSpeed * sprintMultiplier
            : moveInput.x * moveSpeed;

  
        
        
        //when to allow jumping
        if (jumpBufferRemaining > 0f && (IsGrounded || coyoteRemaining > 0f ))
        {
            velocity.y = jumpSpeed;
            IsGrounded = false;
            coyoteRemaining = 0f;
            jumpBufferRemaining = 0f;
          
        }

        //set players velocity to calculated vector
        body.linearVelocity = velocity;
        
        //decrement jumpBufferRemaining and ladderDetachRemaining
        jumpBufferRemaining = Mathf.Max(0f, jumpBufferRemaining - step);
        UpdateAppearance();
        wasGrounded = IsGrounded;
    }



    //handles switching to appropriate animation using player state
    private void UpdateAppearance()
    {
        if (playerVitals != null && playerVitals.IsDead)
        {
            PlayAnimation(DeadAnimation);
            return;
        }

        if (spriteRenderer != null &&
            Mathf.Abs(moveInput.x) > 0.01f)
        {
            spriteRenderer.flipX = moveInput.x < 0f;
        }

        int nextAnimation;

        if (!IsGrounded)
        {
            float verticalSpeed = body.linearVelocity.y;

            if (verticalSpeed > apexVelocityThreshold)
                nextAnimation = JumpUpAnimation;
            else if (verticalSpeed < -apexVelocityThreshold)
                nextAnimation = JumpFallAnimation;
            else
                nextAnimation = JumpApexAnimation;
        }
        else if (landingPoseRemaining > 0f)
        {
            nextAnimation = LandAnimation;
        }
        else if (isUsingTool)
        {
            nextAnimation = ShootAnimation;
        }
        else if (Mathf.Abs(body.linearVelocity.x) > 0.05f)
        {
            nextAnimation = RunAnimation;
        }
        else
        {
            nextAnimation = IdleAnimation;
        }

        PlayAnimation(nextAnimation);
    }
    
                
    private void PlayAnimation(int animation)                                                                                           
    {
        if (animator == null ||
            animation == currentAnimation ||
            !animator.HasState(0, animation))
        {
            return;
        }

        animator.Play(animation);
        currentAnimation = animation;
    }
    

    
   
    private void OnDisable()
    {

        moveInput = Vector2.zero;
        isUsingTool = false;
        currentAnimation = 0;
        jumpBufferRemaining = 0f;
        coyoteRemaining = 0f;
    }
}
