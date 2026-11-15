using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue Portraits")]
public sealed class DialoguePortraits : ScriptableObject
{
    public enum Emotion { Neutral, Thinking, Happy, Sad, Angry, Surprised }
    public Sprite box;

    public GameObject[] boxes = new GameObject[InfectionProfile.InfectionCount];
    public TMP_FontAsset font;
    [Min(0f)] public float wordSpacing = 5f;

    public Sprite[] stages = new Sprite[InfectionProfile.InfectionCount];

    public GameObject faces;
}
