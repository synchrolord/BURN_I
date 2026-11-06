using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerVitals2D))]
public sealed class PlayerRespawn2D : MonoBehaviour
{
    
    [SerializeField] private Transform respawnPoint;
    [SerializeField, Min(0f)] private float respawnDelay = 1.5f;

    private PlayerVitals2D vitals;
    private Rigidbody2D body;
    private Vector3 startingPosition;
    private Coroutine respawnRoutine;

    private bool diedWhilePoisoned;
    public Transform BossSpawnPoint { get; private set; }
    public event System.Action Respawned;
    
    private void Awake()
    {
        Debug.Log("PlayerRespawn2D Awake", this);

        vitals = GetComponent<PlayerVitals2D>();
        body = GetComponent<Rigidbody2D>();
        startingPosition = transform.position;

        if (vitals == null)
            Debug.LogError("PlayerRespawn2D cannot find PlayerVitals2D.", this);
    }

    private void OnEnable()
    {
        if (vitals == null)
            vitals = GetComponent<PlayerVitals2D>();

        if (vitals == null)
            return;

        vitals.Died += HandleDeath;
        Debug.Log("PlayerRespawn2D subscribed to Died.", this);
    }
    private void OnDisable()
    {
        vitals.Died -= HandleDeath;

        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }
    }

    private void HandleDeath()
    {

        diedWhilePoisoned = vitals.IsPoisoned;
        if (respawnRoutine == null)
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSecondsRealtime(respawnDelay);

        Debug.Log("Respawning player now."+ "infection=" +vitals.InfectionStage, this);

        int nextStage = vitals.InfectionStage;

        //only increase infection status if player dies with poison
        if (diedWhilePoisoned)
        {
            nextStage = Mathf.Min(
                nextStage + 1,
                InfectionProfile.InfectionCount - 1
            );
        }
        
        transform.position = respawnPoint != null
            ? respawnPoint.position
            : startingPosition;

        if (body != null)
            body.linearVelocity = Vector2.zero;

        
        //reset stats for player when they die
        vitals.ResetForSpawn(nextStage);
        diedWhilePoisoned = false;
        respawnRoutine = null;
        Respawned?.Invoke();
    }

    public void SetCheckpoint(Transform point, Transform bossPoint = null)
    {
        respawnPoint = point;
        BossSpawnPoint = bossPoint;
    }
}
