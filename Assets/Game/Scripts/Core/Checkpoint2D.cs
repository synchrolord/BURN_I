using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class Checkpoint2D : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform bossSpawnPoint;
    public Transform PlayerSpawn => spawnPoint != null ? spawnPoint : transform;
    public Transform BossSpawn => bossSpawnPoint;

    public void Configure(Transform playerPoint, Transform creaturePoint)
    {
        spawnPoint = playerPoint;
        bossSpawnPoint = creaturePoint;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerRespawn2D player =
            other.GetComponentInParent<PlayerRespawn2D>();

        if (player == null) return;

        //remember the last checkpoint touched
        player.SetCheckpoint(
            spawnPoint != null ? spawnPoint : transform,
            bossSpawnPoint);
    }
}
