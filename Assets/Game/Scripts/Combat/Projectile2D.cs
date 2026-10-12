using System;
using System.Reflection.Metadata;
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


    private Vector2 direction;
    private float lifetimeRemaining;
    private bool hasLaunched;

    public void Launch(Vector2 launchDirection)
    {
        //assign default launch to right
        if (launchDirection.sqrMagnitude < 0.001f)
            launchDirection = Vector2.right;
        
        //make projectile face direction
        direction = launchDirection.normalized;
        transform.right = launchDirection;
        
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
            HandleImpact(hit.collider);
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


    public void HandleImpact(Collider2D collider)
    {
        //add poison to player when hit
        PlayerVitals2D playerVitals = collider.GetComponentInParent<PlayerVitals2D>();
        if (playerVitals != null)
            playerVitals.AddPoison(poisonAmount);
        
        //look for objects that are damageable and apply damage 
        Damageable2D target = collider.GetComponent<Damageable2D>();
        target?.TakeDamage(damage);
        Destroy(gameObject);    
    }    
}
