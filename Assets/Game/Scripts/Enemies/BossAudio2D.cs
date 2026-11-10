using UnityEngine;

[RequireComponent(typeof(BossGrapple2D))]
public sealed class BossAudio2D : MonoBehaviour
{
    [SerializeField] private AudioClip restLoop;
    [SerializeField] private AudioClip chaseLoop;
    [SerializeField] private AudioClip burningLoop;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
    [SerializeField, Min(1f)] private float hearingDistance = 40f;

    private BossGrapple2D boss;
    private AudioSource voice;
    private Transform listener;

    private void Awake()
    {
        boss = GetComponent<BossGrapple2D>();
        voice = GetComponent<AudioSource>();
        if (voice == null) voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.loop = true;
        voice.spatialBlend = 0f;
        AudioListener ears = FindAnyObjectByType<AudioListener>();
        if (ears != null) listener = ears.transform;
    }

    private void Update()
    {
        AudioClip clip = boss.CurrentState == BossGrapple2D.State.Rest ? restLoop :
            boss.CurrentState == BossGrapple2D.State.Burning ? burningLoop : chaseLoop;
        //switch only when the sound changes, so the loop isn't restarted every frame
        if (voice.clip != clip)
        {
            voice.Stop();
            voice.clip = clip;
        }
        Transform hearFrom = boss.Target != null ? boss.Target : listener;
        //use 2D distance so the camera's Z position doesn't make the sound quieter
        float distance = hearFrom != null ? Vector2.Distance(transform.position, hearFrom.position) : hearingDistance;
        voice.volume = volume * GameOptions.EffectsVolume * Mathf.Clamp01(1f - distance / hearingDistance);
        if (clip != null && !voice.isPlaying) voice.Play();
    }

    private void OnDisable()
    {
        if (voice != null) voice.Stop();
    }
}
