using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class DeathZone2D : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerVitals2D player =
            other.GetComponentInParent<PlayerVitals2D>();

        if (player != null)
            player.Kill();
    }
}