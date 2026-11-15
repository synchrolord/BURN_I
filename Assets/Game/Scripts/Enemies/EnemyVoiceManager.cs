using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyVoiceManager : MonoBehaviour
{
    public enum Sound { Idle, Chase, Aggro, Attack, Hurt }

    [SerializeField, Min(1)] private int maxVoices = 2;
    
    [Header("Shared pauses")]
    [SerializeField, Min(0f)] private float idlePause = 6f;
    [SerializeField, Min(0f)] private float chasePause = 4f;
    [SerializeField, Min(0f)] private float aggroPause = 2f;
    [SerializeField, Min(0f)] private float attackPause = 0.5f;
    [SerializeField, Min(0f)] private float hurtPause = 1f;

    private struct Voice
    {
        public AudioSource source;
        public Sound sound;
    }

    private readonly List<Voice> voices = new List<Voice>();
    private readonly float[] nextSound = new float[5];
    private static EnemyVoiceManager current;

    public static EnemyVoiceManager Get()
    {
        if (current == null) current = FindAnyObjectByType<EnemyVoiceManager>();
        if (current == null) current = new GameObject("Enemy Voices").AddComponent<EnemyVoiceManager>();
        return current;
    }

    private void Awake() => current = this;

    public bool Request(AudioSource source, Sound sound)
    {
        if (!isActiveAndEnabled || Time.time < nextSound[(int)sound]) return false;
        voices.RemoveAll(v => v.source == null || !v.source.isPlaying);

        int replace = -1;
        for (int i = 0; i < voices.Count; i++)
        {
            if (voices[i].source != source) continue;

            if (sound <= voices[i].sound) return false;
            replace = i;
            break;
        }

        if (replace < 0 && voices.Count >= maxVoices)
        {
            for (int i = 0; i < voices.Count; i++)
                if (voices[i].sound < sound &&
                    (replace < 0 || voices[i].sound < voices[replace].sound)) replace = i;
            if (replace < 0) return false;
        }

        if (replace >= 0)
        {
            voices[replace].source.Stop();
            voices.RemoveAt(replace);
        }
        voices.Add(new Voice { source = source, sound = sound });
        float pause = sound switch
        {
            Sound.Idle => idlePause,
            Sound.Chase => chasePause,
            Sound.Aggro => aggroPause,
            Sound.Attack => attackPause,
            _ => hurtPause
        };
        nextSound[(int)sound] = Time.time + pause;
        return true;
    }

    private void OnDisable()
    {
        foreach (Voice voice in voices)
            if (voice.source != null) voice.source.Stop();
        voices.Clear();
        System.Array.Clear(nextSound, 0, nextSound.Length);
        if (current == this) current = null;
    }
}
