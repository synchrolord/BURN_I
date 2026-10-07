using System;
using UnityEngine;

public class CreatureHealth2D : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 30f;

    [Header("Death")]
    [SerializeField, Min(0f)] private float deathDelay = 0.5f;
    [SerializeField] private Animator animator;
    [SerializeField] private PlantEnemy2D behaviour;


    private static readonly int DeadAnimation =
        Animator.StringToHash("Dead");

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }
    
    
    private void Awake()
    
    {   body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (behaviour == null)
            behaviour = GetComponent<PlantEnemy2D>();

        currentHealth = maxHealth;
        
    }

    //Apply Damage to enemies and kill them if necessary
    public void TakeDamage(float damage)
    {
        if (IsDead||damage <= 0f)return;
        
        
        currentHealth = Math.Max(currentHealth - damage, 0f);

        if (currentHealth <= 0) Die();
    }
    
    //custom fire damage to add Burn logic to later
    public void TakeFireDamage(float amount)
    {
        TakeDamage(amount);
    }
    private void Die()
    {
        
        //disable enemy when they die
        IsDead=true;
        if (behaviour != null) behaviour.enabled = false;
        
        //stop enemy when they die
        if(body != null) body.linearVelocity = Vector2.zero;
        body.simulated = false;
        
        //disable enemy collider when they die
        if(bodyCollider != null) bodyCollider.enabled = false;
        
        //play death animation
        if (animator != null &&
            animator.HasState(0, DeadAnimation))
        {
            animator.Play(DeadAnimation);
        }
        
        Destroy(gameObject, deathDelay);
        
    }
}
