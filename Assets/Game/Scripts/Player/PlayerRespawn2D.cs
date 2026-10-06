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

        if (respawnRoutine == null)
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSecondsRealtime(respawnDelay);

        Debug.Log("Respawning player now."+ "infection=" +vitals.InfectionStage, this);

        int nextStage = Mathf.Min(
            vitals.InfectionStage + 1,
            InfectionProfile.InfectionCount - 1
        );

        transform.position = respawnPoint != null
            ? respawnPoint.position
            : startingPosition;

        if (body != null)
            body.linearVelocity = Vector2.zero;

        vitals.ResetForSpawn(nextStage);
        respawnRoutine = null;
    }
}