using UnityEngine;



[RequireComponent(typeof(Rigidbody2D))]
public class PLantEnemy2D : MonoBehaviour
{
  public enum EnemyType { Walker, Spitter }

  [Header("Body")]
  [SerializeField] private EnemyType enemyType;
  [SerializeField] private SpriteRenderer spriteRenderer;
  
  [SerializeField] private LayerMask layerMask;
  
  [Header("Movement")]
  [SerializeField, Min(0f)] private float moveSpeed;
  [SerializeField, Min(0f)] private float agroSpeed;
  [SerializeField, Min(0f)] private float patrolDistance;
  
  [Header("Behavior")]
  [SerializeField, Min(0f)] private float agroDistance;
  [SerializeField, Min(0f)] private float warningDistance;
  [SerializeField, Min(0f)] private float attackRange;
  [SerializeField] private float attackDamage;
  [SerializeField] private float attackCooldown;
  [SerializeField] private float telegraphTime;
  
  [Header("spore shot")]
  [SerializeField] private Transform shotOrigin;
  [SerializeField] private SporeShot sporeShot;
  
  
  [Header("Audio")]
  [SerializeField] private AudioSource audioSource;
  [SerializeField] private AudioClip attackAudio;
  [SerializeField] private AudioClip sporeHitAudio;
  [SerializeField] private AudioClip warningAudio;
  

  






}
