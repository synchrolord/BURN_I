using System.Collections.Generic;
using UnityEngine;


public sealed class EnemyAudio2D : MonoBehaviour
{
    [SerializeField] private AudioClip[] idleSounds = new AudioClip[0];
    [SerializeField] private AudioClip[] aggroSounds = new AudioClip[0];
    [SerializeField] private AudioClip[] chaseSounds = new AudioClip[0];
    [SerializeField] private AudioClip[] attackSounds = new AudioClip[0];
    [SerializeField] private AudioClip[] spitSounds = new AudioClip[0];
    [SerializeField] private AudioClip[] hurtSounds = new AudioClip[0];
    [SerializeField] private AudioClip[] fireSounds = new AudioClip[0];
    [SerializeField] private Vector2 pauseBetweenSounds = new Vector2(2f, 5f);
    [SerializeField, Min(0f)] private float hurtPause = 0.75f;
    [SerializeField, Min(1f)] private float hearingDistance = 40f;

    [Header("Voice")]
    [SerializeField] private Vector2 pitch = new Vector2(0.75f, 1.05f);
    [SerializeField] private Vector2 loudness = new Vector2(0.65f, 0.9f);
    [SerializeField, Range(0f, 1f)] private float distortion = 0.15f;

    private static readonly List<EnemyAudio2D> active = new List<EnemyAudio2D>();
    private static int countFrame = -1;
    private static int playingCount;
    private AudioLowPassFilter lowPass;
    private AudioDistortionFilter distortionFilter;
    private AudioReverbFilter reverbFilter;
    private float voiceLoudness = 1f;
    private float effectLoudness = 1f;
    private float nextEffect;
    private BossGrapple2D boss;
    private PlantEnemy2D enemy;
    private CreatureHealth2D health;
    private BossSpit2D spitter;
    private AudioSource voice;
    private AudioSource effects;
    private Transform listener;
    private int previousState = -1;
    private float nextSound;
    private float nextHurt;

    private void Awake()
    {
        boss = GetComponent<BossGrapple2D>();
        enemy = GetComponent<PlantEnemy2D>();
        health = GetComponent<CreatureHealth2D>();
        spitter = GetComponent<BossSpit2D>();
        voice = gameObject.AddComponent<AudioSource>();
        effects = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = effects.playOnAwake = false;
        voice.volume = effects.volume = 0f;
        lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        distortionFilter = gameObject.AddComponent<AudioDistortionFilter>();
        reverbFilter = gameObject.AddComponent<AudioReverbFilter>();
        AudioListener ears = FindAnyObjectByType<AudioListener>();
        if (ears != null) listener = ears.transform;
    }

    private void OnEnable()
    {
        active.Add(this);
        if (boss != null) boss.Attacked += PlayAttack;
        if (enemy != null) { enemy.Attacked += PlayAttack; enemy.Spat += PlaySpit; }
        if (health != null) health.Hurt += PlayHurt;
        if (spitter != null) spitter.Spat += PlaySpit;
        if (boss != null) boss.Hurt += PlayHurt;
        //don't make every idle enemy start meowing on the same frame
        nextSound = Time.time + Random.Range(0f, Mathf.Max(0f, pauseBetweenSounds.y));
        nextHurt = nextEffect = 0f;
        previousState = -1;
    }

    private void Update()
    {
        if ((boss != null && !boss.isActiveAndEnabled) || (health != null && health.IsDead)) { voice.Stop(); return; }
        int state = boss != null ? (int)boss.CurrentState :
            health != null && health.IsBurning ? 2 : enemy != null && enemy.IsAggro ? 1 : 0;
        if (state != previousState)
        {
            voice.Stop();
            if (previousState <= 0 && state == 1) PlayEffect(aggroSounds);
            previousState = state;
            if (state != 0) nextSound = Time.time + Random.Range(0f, 0.6f);
        }

        if (voice.isPlaying || Time.time < nextSound) return;
        AudioClip[] sounds = previousState == 0 ? idleSounds :
            previousState == 2 ? fireSounds : chaseSounds;
        voice.clip = Pick(sounds);
        voice.pitch = Random.Range(Mathf.Max(0.1f, pitch.x), Mathf.Max(0.1f, Mathf.Max(pitch.x, pitch.y)));
        voiceLoudness = Random.Range(loudness.x, loudness.y);
        
        if (voice.clip != null) voice.Play();
        float pause = previousState == 2 ? 0f : 
            Random.Range(Mathf.Max(0f, pauseBetweenSounds.x), Mathf.Max(pauseBetweenSounds.x, pauseBetweenSounds.y));
        
        nextSound = Time.time + (voice.clip != null ? voice.clip.length / voice.pitch : 0.5f) + pause;
    }

    private float GetVolume()
    {
        Transform target = boss != null ? boss.Target : enemy != null ? enemy.Target : null;
        Transform hearFrom = target != null ? target : listener;
        float distance = hearFrom != null ? Vector2.Distance(transform.position, hearFrom.position) : hearingDistance;
        float distanceVolume = Mathf.Clamp01(1f - distance / hearingDistance);
        RoomAudio2D room = RoomAudio2D.Current;
        float heard = room != null ? room.GetVolume(this, distanceVolume) : distanceVolume;
        return 0.35f * GameOptions.EffectsVolume * heard;
    }

    private void LateUpdate()
    {
        if (countFrame != Time.frameCount)
        {
            countFrame = Time.frameCount;
            playingCount = 0;
            foreach (EnemyAudio2D other in active)
                if ((other.voice.isPlaying || other.effects.isPlaying) && other.GetVolume() > 0f) playingCount++;
        }
        float targetVolume = GetVolume() / Mathf.Sqrt(Mathf.Max(1, playingCount));
        effects.volume = Mathf.MoveTowards(effects.volume, targetVolume * effectLoudness, Time.deltaTime * 2f);
        voice.volume = Mathf.MoveTowards(voice.volume,
            targetVolume * voiceLoudness * (effects.isPlaying ? 0.35f : 1f), Time.deltaTime * 2f);

        RoomAudio2D room = RoomAudio2D.Current;
        lowPass.cutoffFrequency = room != null ? room.Cutoff : 22000f;
        distortionFilter.distortionLevel = Mathf.Min(0.8f, distortion + (room != null ? room.Distortion : 0f));
        reverbFilter.reverbPreset = room != null ? room.Reverb : AudioReverbPreset.Off;
    }

    private static AudioClip Pick(AudioClip[] sounds)
    {
        if (sounds == null || sounds.Length == 0) return null;
        int start = Random.Range(0, sounds.Length);
        for (int i = 0; i < sounds.Length; i++)
            if (sounds[(start + i) % sounds.Length] != null) return sounds[(start + i) % sounds.Length];
        return null;
    }

    private void PlayAttack() => PlayEffect(attackSounds);
    private void PlaySpit() => PlayEffect(spitSounds);
    private void PlayHurt()
    {
        if (Time.time < nextHurt) return;
        nextHurt = Time.time + hurtPause;
        PlayEffect(hurtSounds);
    }
    private void PlayEffect(AudioClip[] sounds)
    {
        AudioClip clip = Pick(sounds);
        if (clip == null || Time.time < nextEffect) return;
        nextEffect = Time.time + 0.15f;
        effects.pitch = Random.Range(Mathf.Max(0.1f, pitch.x), Mathf.Max(0.1f, Mathf.Max(pitch.x, pitch.y)));
        effectLoudness = Random.Range(loudness.x, loudness.y);
        effects.clip = clip;
        effects.volume = 0f;
        effects.Play();
    }

    private void OnDisable()
    {
        active.Remove(this);
        if (boss != null) boss.Attacked -= PlayAttack;
        if (enemy != null) { enemy.Attacked -= PlayAttack; enemy.Spat -= PlaySpit; }
        if (health != null) health.Hurt -= PlayHurt;
        if (spitter != null) spitter.Spat -= PlaySpit;
        if (boss != null) boss.Hurt -= PlayHurt;
        voice.Stop();
        effects.Stop();
    }
}
