using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class DialogueTrigger2D : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        [TextArea(3, 6)] public string text;
        public DialoguePortraits.Emotion emotion;
    }

    [SerializeField] private Entry[] entries = new Entry[0];
    [SerializeField] private DialoguePortraits portraits;
    [SerializeField] private bool once = true;
    private bool played;

    private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
    private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (once && played) return;
        PlayerVitals2D player = other.GetComponentInParent<PlayerVitals2D>();
        if (player == null || player.IsDead || entries.Length == 0) return;
        if (DialoguePopup.Show(entries, portraits, player)) played = true;
    }
}
