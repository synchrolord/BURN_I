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
    
    
    
    [Header("Ledge Checking")]
    [SerializeField] private LayerMask mask;
    
    [SerializeField, Min(0.01f)] private float wallCheckDistance = 0.1f;
    [SerializeField, Min(0.05f)] private float ledgeCheckDepth = 0.5f;
    

    private Rigidbody2D body;
    private PlayerVitals2D player;

    private float startingX;
    private float patrolDirection = 1f;
    private float attackCooldownRemaining;

    private void Awake()
    {
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
        attackCooldownRemaining =
            Mathf.Max(0f, attackCooldownRemaining - Time.deltaTime);
    }

    
    private void FixedUpdate()
    {
        if (enemyType != EnemyType.Walker)
        {
            StopMoving();
            return;
        }

        if (player == null || player.IsDead)
        {
            StopMoving();
            return;
        }

        float distanceToPlayer = GetDistanceToPlayer();
        
        
        //enemies notice player harder when infected
        float effectiveAggroDistance =
            agroDistance *
            player.CurrentStage.aggroMultiplier;


        
        if (distanceToPlayer <= attackRange)
        {
            StopMoving();
            TryAttack();
            return;
        }
        
        if (distanceToPlayer <= effectiveAggroDistance)
        {
            ChasePlayer();
            return;
        }



        Patrol();
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
            MoveHorizontal(patrolDirection, moveSpeed);
        else
            StopMoving();
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
            StopMoving();
    }

    private void TryAttack()
    {
        if (attackCooldownRemaining > 0f)
            return;

        player.TakeDamage(attackDamage);
        attackCooldownRemaining = attackCooldown;
    }

    private void MoveHorizontal(float direction, float speed)
    {
        body.linearVelocity = new Vector2(
            direction * speed,
            body.linearVelocity.y
        );

        if (spriteRenderer != null && direction != 0f)
            spriteRenderer.flipX = direction < 0f;
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
    
    
    
}