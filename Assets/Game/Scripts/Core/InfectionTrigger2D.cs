using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class InfectionTrigger2D : MonoBehaviour
{
    [SerializeField, Range(1, 5)] private int increase = 1;
    private bool used;
    private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
    private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerVitals2D player = other.GetComponentInParent<PlayerVitals2D>();
        if (used || player == null || player.IsDead) return;
        used = true;
        player.SetInfectionStage(player.InfectionStage + increase);
    }
}
