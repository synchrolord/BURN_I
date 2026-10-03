using UnityEngine;
using UnityEngine.InputSystem;

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
    private float coyoteRemaining;
    private float jumpBufferRemaining;
    private string currentAnimation;


    public bool IsGrounded { get; private set; }
    
    //get velocity every update
    public Vector2 Velocity
    {
        get
        {
            return body.linearVelocity;
        }
    }


    //set up body, animator, and spriteRenderer.
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (animator == null) { animator = GetComponentInChildren<Animator>(); }
        if (spriteRenderer == null){ spriteRenderer = GetComponentInChildren<SpriteRenderer>();}
    }

 
    //store players input as vector2
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
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
        IsGrounded = TouchesGround();
        coyoteRemaining = Mathf.Max(0f, coyoteRemaining - step);
        
        if (IsGrounded)
        {coyoteRemaining = coyoteTime; }
        
        //reset players velocity, gravity
        Vector2 velocity = body.linearVelocity;
        velocity.x = moveInput.x * moveSpeed;
        
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
    }



    //handles switching to appropriate animation using player state
    private void UpdateAppearance()
    {
        //Mirror sprite if player moving in negative direction
        if (spriteRenderer != null && moveInput.x != 0f)
        {
            spriteRenderer.flipX = moveInput.x < 0f;
        }
        
        
        //start with defualt idle animation and switch to relevant animation
        string nextAnimation = idleAnimationName;
        
        
        //Running
        if (moveInput.x != 0f || (moveInput.y != 0f))
        {
            nextAnimation = runAnimationName;
        }
        
        //Jumping
        if (!IsGrounded)
        {
            nextAnimation = jumpFallAnimationName;
            if (body.linearVelocity.y > 0f)
            {
                nextAnimation = jumpUpAnimationName;
            }
        }
        
        //Using tool
        else if (isUsingTool)
        {
            nextAnimation = shootAnimationName;
        }
        
        //if animator exists, Animation Exists, Aanimation differs from current animation, 
        if (animator != null && animator.HasState(0, Animator.StringToHash(nextAnimation))&& nextAnimation != currentAnimation )
        {
            animator.Play(nextAnimation);
            currentAnimation = nextAnimation;
        }
    }

    
   
    private void OnDisable()
    {

        moveInput = Vector2.zero;
        isUsingTool = false;
        currentAnimation = null;
        jumpBufferRemaining = 0f;
        coyoteRemaining = 0f;
    }
}
