using UnityEngine;

public class Projectile2D : MonoBehaviour
{
    
    
    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float speed = 6f;
    [SerializeField, Min(0.1f)] private float lifetime = 3f;

    [Header("Impact")]
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(0f)] private float poisonAmount = 25f;
    [SerializeField] private LayerMask hitMask;

    [Header("Acid Splash")]
    [SerializeField, Range(0, 20)] private int acidSplashRadius = 1;
    [SerializeField, Range(0, 250)] private int acidUnitsPerCell = 4;

    private CellWorld2D cellWorld;
    
    
    private Vector2 direction;
    private float lifetimeRemaining;
    private bool hasLaunched;

    public void Launch(Vector2 launchDirection, CellWorld2D world)
    {
        //assign default launch to right
        if (launchDirection.sqrMagnitude < 0.001f)
            launchDirection = Vector2.right;
        
        //make projectile face direction
        direction = launchDirection.normalized;
        transform.right = launchDirection;
        
        cellWorld = world;

        lifetimeRemaining = lifetime;
        hasLaunched = true;

    }
    
    

    private void Update()
    {
        if (!hasLaunched)return;
        
        
        float seconds = Time.deltaTime;
        Vector2 start = transform.position;
        Vector2 end = start + (direction * speed * seconds);

        //trace path of projectile and return RaycastHit2D collision information 
        RaycastHit2D hit = Physics2D.Linecast(
            start,
            end,
            hitMask
        );


        //if projectile collides this update 
        if (hit.collider != null)
        {
            transform.position = hit.point;
            HandleImpact(hit);
            return;
        }
        
        //if it did not collide
        transform.position = end;
        lifetimeRemaining -= seconds;

        if (lifetimeRemaining <= 0f)
        {
            Destroy(gameObject);
        }
        
        

        
    }


    public void HandleImpact(RaycastHit2D hit)
    {
        Collider2D collider = hit.collider;
        
        //add poison to player when hit
        PlayerVitals2D playerVitals = collider.GetComponentInParent<PlayerVitals2D>();
        if (playerVitals != null)
            playerVitals.AddPoison(poisonAmount);
        
        //look for objects that are damageable and apply damage 
        Damageable2D target = collider.GetComponentInParent<Damageable2D>();
        target?.TakeDamage(damage);
        
        CreateAcidSplash(hit);
        Destroy(gameObject);    
    }    
    
    
    
    private void CreateAcidSplash(RaycastHit2D hit)
    {
        if (cellWorld == null ||
            acidUnitsPerCell <= 0)
        {
            return;
        }

       // move slightly to avoid creating liquid inside ground
        Vector2 splashPosition =
            hit.point +
            hit.normal * (cellWorld.CellSize * 0.5f);

        cellWorld.AddAcid(
            splashPosition,
            acidSplashRadius,
            acidUnitsPerCell);
    }
    
    
}
