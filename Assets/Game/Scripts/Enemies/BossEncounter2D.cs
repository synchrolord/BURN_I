using UnityEngine;

//place one encounter trigger with its own boss prefab and starting position
[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossEncounter2D : MonoBehaviour
{
    [SerializeField] private BossGrapple2D bossPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private PlayerVitals2D player;
    [SerializeField] private bool wakeOnEntry = true;
    [SerializeField] private vineController wakeVines;
    private PlayerRespawn2D playerRespawn;
    private bool hasStarted;

    public void Configure(BossGrapple2D prefab, Transform spawn, vineController camouflage)
    {
        bossPrefab = prefab;
        spawnPoint = spawn;
        wakeVines = camouflage;
        wakeOnEntry = false;
    }

    private BossGrapple2D boss;
    private bool triggered;
    private bool waitingForRespawn;
    private bool started;
    public BossGrapple2D ActiveBoss => boss;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        if (player == null) player = FindAnyObjectByType<PlayerVitals2D>();
        if (player != null) playerRespawn = player.GetComponent<PlayerRespawn2D>();
    }

    private void OnEnable()
    {
        if (player != null) player.Died += PlayerDied;
        if (playerRespawn != null) playerRespawn.Respawned += PlayerRespawned;
        if (started) SpawnBoss();
    }

    private void Start()
    {
        started = true;
        if (bossPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("Boss Encounter needs a boss prefab and spawn point.", this);
            return;
        }
        SpawnBoss();
    }

    private void Update()
    {
        if (boss != null && boss.CurrentState != BossGrapple2D.State.Rest)
            hasStarted = true;
        if (!waitingForRespawn || player == null || player.IsDead) return;

        //the normal player respawn has finished, now prepare a fresh encounter
        waitingForRespawn = false;
        SpawnBoss();
    }

    private void SpawnBoss()
    {
        if (boss != null || bossPrefab == null || spawnPoint == null) return;
        if (player != null && player.IsDead)
        {
            waitingForRespawn = true;
            return;
        }

        Transform spawn = hasStarted && playerRespawn != null && playerRespawn.BossSpawnPoint != null
            ? playerRespawn.BossSpawnPoint : spawnPoint;
        boss = Instantiate(bossPrefab, spawn.position, spawn.rotation);
        boss.PrepareEncounter(player != null ? player.transform : null, wakeVines, hasStarted);
        boss.gameObject.SetActive(true);
        triggered = hasStarted;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        StartEncounter(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        StartEncounter(other);
    }

    private void StartEncounter(Collider2D other)
    {
        if (!wakeOnEntry || triggered || boss == null || player == null || player.IsDead ||
            other.GetComponentInParent<PlayerVitals2D>() != player) return;

        triggered = true;
        hasStarted = true;
        boss.SetAggro(true);
    }

    private void PlayerDied()
    {
        if (boss != null && boss.CurrentState != BossGrapple2D.State.Rest) hasStarted = true;
        ClearBoss();
        waitingForRespawn = true;
    }

    private void PlayerRespawned()
    {
        if (!waitingForRespawn) return;
        waitingForRespawn = false;
        SpawnBoss();
    }

    private void ClearBoss()
    {
        if (boss != null)
        {
            //stop attacks and release the grapples immediately
            boss.gameObject.SetActive(false);
            Destroy(boss.gameObject);
        }
        boss = null;
        triggered = false;
    }

    private void OnDisable()
    {
        if (player != null) player.Died -= PlayerDied;
        if (playerRespawn != null) playerRespawn.Respawned -= PlayerRespawned;
        ClearBoss();
        waitingForRespawn = false;
    }
}
