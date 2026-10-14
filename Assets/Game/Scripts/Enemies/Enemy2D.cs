using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlantEnemy2D : MonoBehaviour
{
    public enum EnemyType
    {
        Walker,
        Spitter
    }
    
    [Header("Body")]
    [SerializeField] private EnemyType enemyType;
    [SerializeField] private SpriteRenderer spriteRenderer;
    private Collider2D bodyCollider;
    private Collider2D playerCollider;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
    [SerializeField, Min(0f)] private float agroSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float patrolDistance = 2f;

    [Header("Detection and attack")]
    [SerializeField, Min(0f)] private float agroDistance = 5f;
    [SerializeField, Min(0f)] private float attackRange = 0.8f;
    [SerializeField, Min(0f)] private float attackDamage = 10f;
    [SerializeField, Min(0.05f)] private float attackCooldown = 1f;
    [SerializeField] private Transform facingRoot;
    
    
    [Header("Ledge Checking")]
    [SerializeField] private LayerMask mask;
    
    [SerializeField, Min(0.01f)] private float wallCheckDistance = 0.1f;
    [SerializeField, Min(0.05f)] private float ledgeCheckDepth = 0.5f;
    
    [Header("Spitter")]
    [SerializeField] private Projectile2D spitPrefab;
    [SerializeField] private Transform shotOrigin;
    [SerializeField, Min(0f)] private float spitRange = 7f;
    [SerializeField] private CellWorldRoomManager roomManager;

    
    private CellWorld2D cellWorld;

    //Animation
    [Header("Animation")]
    [SerializeField]float attackAnimationSeconds;
    [SerializeField] Animator animator;

    private static readonly int IdleAnimation =
        Animator.StringToHash("Idle");

    private static readonly int MoveAnimation =
        Animator.StringToHash("Move");

    private static readonly int AttackAnimation =
        Animator.StringToHash("Attack");

    private static readonly int DeadAnimation =
        Animator.StringToHash("Dead");

    private int currentAnimation;
    private float attackAnimationRemaining;
    
    
    

    private Rigidbody2D body;
    private PlayerVitals2D player;

    private float startingX;
    private float patrolDirection = 1f;
    private float attackCooldownRemaining;

    private void Awake()
    {
        if (roomManager == null)
        { roomManager = FindFirstObjectByType<CellWorldRoomManager>(); }
        
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        
        
        body = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        player = FindFirstObjectByType<PlayerVitals2D>();
        startingX = transform.position.x;
        
        bodyCollider = GetComponent<Collider2D>();

        if (player != null)
            playerCollider = player.GetComponent<Collider2D>();
        
    }

    private void Update()
    {
        
        
        attackAnimationRemaining = Mathf.Max(
            0f,
            attackAnimationRemaining - Time.deltaTime
        );
        
        attackCooldownRemaining =
            Mathf.Max(0f, attackCooldownRemaining - Time.deltaTime);


    }

    
    private void FixedUpdate()
    {


        
        // enemies stop when player dies
        if (player == null || player.IsDead)
        {
            StopMoving();
            PlayAnimation(IdleAnimation);
            return;
        }

        // Keep  enemy still until its attack animation finishes.
        if (attackAnimationRemaining > 0f)
        {
            StopMoving();
            return;
        }
        
        
        if (enemyType == EnemyType.Walker)
            UpdateWalker();
        else
            UpdateSpitter();

    }


        private void UpdateWalker(){
        float distanceToPlayer = GetDistanceToPlayer();
        
        
        //enemies notice player harder when infected
        float effectiveAggroDistance =
            agroDistance *
            player.CurrentStage.aggroMultiplier;
        
        if (distanceToPlayer <= attackRange)
        {
            StopMoving();
            FacePlayer();

            if (!TryAttack())
                PlayAnimation(IdleAnimation);

            return;
        }
        
        if (distanceToPlayer <= effectiveAggroDistance)
        {
            ChasePlayer();
            return;
        }
        
        Patrol();
    }
    private void UpdateSpitter()
    {
        StopMoving();
        FacePlayer();

        float effectiveSpitRange =
            spitRange * player.CurrentStage.aggroMultiplier;

        if (GetDistanceToPlayer() > effectiveSpitRange)
        {
            PlayAnimation(IdleAnimation);
            return;
        }

        if (attackCooldownRemaining > 0f)
        {
            PlayAnimation(IdleAnimation);
            return;
        }

        TrySpit();
    }
    
    
    private void Patrol()
    {
        float distanceFromStart =
            transform.position.x - startingX;
        
      if (distanceFromStart >= patrolDistance)
              patrolDirection = -1f;
      else if (distanceFromStart <= -patrolDistance)
          patrolDirection = 1f;
      
      
      if (!CanMove(patrolDirection))
          patrolDirection *= -1f;
        
        
      if (CanMove(patrolDirection))
      {
          MoveHorizontal(patrolDirection, moveSpeed);
      }

      else
      {
          StopMoving();
          PlayAnimation(IdleAnimation);
      }
      
      
    }
    private void ChasePlayer()
    {
        float direction = Mathf.Sign(
            player.transform.position.x -
            transform.position.x
        );
        

        if (CanMove(direction))
            MoveHorizontal(direction, agroSpeed);
        else
        {
            StopMoving();
            FacePlayer();
            PlayAnimation(IdleAnimation);
        }
    }

    
    
    private bool TryAttack()
    {
        if (attackCooldownRemaining > 0f)
            return false;

        // Apply the hit and briefly lock the attack animation.
        player.TakeDamage(attackDamage);

        attackCooldownRemaining = attackCooldown;
        
        attackAnimationRemaining = attackAnimationSeconds;

        PlayAnimation(AttackAnimation);
        return true;
    }
    private void TrySpit()
    {
        if (attackCooldownRemaining > 0f)
            return ;

        if (spitPrefab == null || shotOrigin == null)
            return ;

        
        Projectile2D shot = Instantiate(
            spitPrefab,
            shotOrigin.position,
            Quaternion.identity
        );

        Vector2 shotDirection =
            player.BodyCenter - (Vector2)shotOrigin.position;

        //shoot into the current cellWold
        CellWorld2D activeWorld =
            roomManager != null
                ? roomManager.CurrentWorld
                : null;

        shot.Launch(shotDirection, activeWorld);

        attackCooldownRemaining = attackCooldown;
        attackAnimationRemaining = attackAnimationSeconds;

        PlayAnimation(AttackAnimation);
    }

    
    
    
    private void MoveHorizontal(float direction, float speed)
    {
        FaceDirection(direction);

        body.linearVelocity = new Vector2(
            direction * speed,
            body.linearVelocity.y
        );

        PlayAnimation(MoveAnimation);
    }

    private void StopMoving()
    {
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }
    private float GetDistanceToPlayer()
    {
        if (bodyCollider != null && playerCollider != null)
            return bodyCollider.Distance(playerCollider).distance;

        return Vector2.Distance(
            transform.position,
            player.transform.position
        );
        
    }

    private bool CanMove(float direction)
    {
        if (bodyCollider == null || Mathf.Abs(direction) < 0.01f)
            return false;
            
        Bounds bounds = bodyCollider.bounds;
        float horizontalDirection = Mathf.Sign(direction);
        Vector2 wallDirection =
            new Vector2(horizontalDirection, 0f);
        
        float wallDistance = bounds.extents.x + wallCheckDistance;
        bool wallAhead = Physics2D.Raycast(
            bounds.center,
            wallDirection,
            wallDistance,
            mask
        );

        Vector2 ledgeOrigin = new Vector2(
            bounds.center.x +
            horizontalDirection * (bounds.extents.x + 0.05f),

            bounds.min.y + 0.05f
        );

        bool groundAhead = Physics2D.Raycast(
            ledgeOrigin,
            Vector2.down,
            ledgeCheckDepth,
            mask
        );

        return !wallAhead && groundAhead;

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
    
    private void FacePlayer()
    {
        FaceDirection(
            player.transform.position.x - transform.position.x
        );
    }
    
    private void FaceDirection(float directionX)
    {
        if (facingRoot == null || Mathf.Abs(directionX) < 0.01f)
            return;

        Vector3 scale = facingRoot.localScale;

        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(directionX);

        facingRoot.localScale = scale;
    }
    
    
}