using UnityEngine;

//optional attack, grappling and state decisions stay in the boss controller
[RequireComponent(typeof(BossGrapple2D))]
public sealed class BossSpit2D : MonoBehaviour
{
    public event System.Action Spat;
    [SerializeField] private bool canSpit;
    [SerializeField] private Projectile2D spitPrefab;
    [SerializeField] private Transform shotOrigin;
    [SerializeField, Min(0f)] private float range = 8f;
    [SerializeField, Min(0.1f)] private float spitInterval = 2f;
    [SerializeField] private CellWorldRoomManager roomManager;

    private BossGrapple2D boss;
    private Collider2D bodyCollider;
    private float nextSpit;

    private void Awake()
    {
        boss = GetComponent<BossGrapple2D>();
        bodyCollider = GetComponent<Collider2D>();
        if (roomManager == null)
            roomManager = FindAnyObjectByType<CellWorldRoomManager>();
    }

    private void OnEnable()
    {
        nextSpit = Time.time + spitInterval;
    }

    private void Update()
    {
        if (!canSpit || !boss.isActiveAndEnabled ||
            boss.CurrentState != BossGrapple2D.State.Chase ||
            boss.Target == null || spitPrefab == null ||
            Time.time < nextSpit) return;

        PlayerVitals2D player = boss.Target.GetComponentInParent<PlayerVitals2D>();
        if (player == null || player.IsDead) return;

        Vector2 start = shotOrigin != null
            ? (Vector2)shotOrigin.position
            : (Vector2)bodyCollider.bounds.center;
        Vector2 direction = player.BodyCenter - start;
        float distance = direction.magnitude;
        if (distance > range || distance < 0.01f) return;

        //do not shoot through the level walls
        if (Physics2D.Raycast(start, direction, distance, boss.WallMask)) return;

        Projectile2D shot = Instantiate(spitPrefab, start, Quaternion.identity);
        shot.Launch(direction, roomManager != null ? roomManager.CurrentWorld : null);
        Spat?.Invoke();
        nextSpit = Time.time + spitInterval;
    }
}
