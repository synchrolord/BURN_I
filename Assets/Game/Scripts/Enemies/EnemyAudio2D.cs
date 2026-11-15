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
    [SerializeField] private Vector2 idlePause = new Vector2(8f, 15f);
    [SerializeField, Min(0f)] private float hurtPause = 0.75f;
    [SerializeField, Min(0f)] private float aggroPause = 10f;
    [SerializeField, Min(1f)] private float hearingDistance = 40f;

    [Header("Voice")]
    [SerializeField] private Vector2 pitch = new Vector2(0.75f, 1.05f);
    [SerializeField] private Vector2 loudness = new Vector2(0.65f, 0.9f);
    [SerializeField, Range(0f, 1f)] private float distortion = 0.15f;

    private BossGrapple2D boss;
    private PlantEnemy2D enemy;
    private CreatureHealth2D health;
    private BossSpit2D spitter;
    private EnemyVoiceManager manager;
    private AudioSource voice;
    private AudioLowPassFilter lowPass;
    private AudioDistortionFilter distortionFilter;
    private AudioReverbFilter reverbFilter;
    private Transform listener;
    private int previousState = -1;
    private EnemyVoiceManager.Sound currentSound;
    private float basePitch;
    private float voiceLoudness = 1f;
    private float nextSound;
    private float nextHurt;
    private float nextAggro;
    private readonly Dictionary<AudioClip[], AudioClip> lastClips = new Dictionary<AudioClip[], AudioClip>();

    private void Awake()
    {
        boss = GetComponent<BossGrapple2D>();
        enemy = GetComponent<PlantEnemy2D>();
        health = GetComponent<CreatureHealth2D>();
        spitter = GetComponent<BossSpit2D>();
        manager = EnemyVoiceManager.Get();
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.volume = 0f;
        //keep this enemy's voice recognisable instead of choosing a completely new pitch each time
        basePitch = Random.Range(Mathf.Max(0.1f, Mathf.Min(pitch.x, pitch.y)), Mathf.Max(0.1f, Mathf.Max(pitch.x, pitch.y)));
        lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        distortionFilter = gameObject.AddComponent<AudioDistortionFilter>();
        reverbFilter = gameObject.AddComponent<AudioReverbFilter>();
        AudioListener ears = FindAnyObjectByType<AudioListener>();
        if (ears != null) listener = ears.transform;
    }

    private void OnEnable()
    {
        if (boss != null) { boss.Attacked += PlayAttack; boss.Hurt += PlayHurt; }
        if (enemy != null) { enemy.Attacked += PlayAttack; enemy.Spat += PlaySpit; }
        if (health != null) health.Hurt += PlayHurt;
        if (spitter != null) spitter.Spat += PlaySpit;
        nextSound = Time.time + Random.Range(1f, 5f);
        nextHurt = nextAggro = 0f;
        previousState = -1;
    }

    private void Update()
    {
        if ((boss != null && !boss.isActiveAndEnabled) || (health != null && health.IsDead)) { voice.Stop(); return; }
        int state = boss != null ? (int)boss.CurrentState :
            health != null && health.IsBurning ? 2 : enemy != null && enemy.IsAggro ? 1 : 0;
        if (state != previousState)
        {
            //a state change can cancel background chatter, but not an attack or hurt sound
            if (currentSound <= EnemyVoiceManager.Sound.Chase) voice.Stop();
            if (previousState <= 0 && state == 1 && Time.time >= nextAggro)
            {
                nextAggro = Time.time + aggroPause;
                Play(aggroSounds, EnemyVoiceManager.Sound.Aggro);
            }
            if (previousState >= 0) nextSound = Time.time + Random.Range(1f, 2f);
            previousState = state;
        }
        if (voice.isPlaying || Time.time < nextSound) return;
        AudioClip[] sounds = state == 0 ? idleSounds : state == 2 ? fireSounds : chaseSounds;
        EnemyVoiceManager.Sound kind = state == 0 ? EnemyVoiceManager.Sound.Idle :
            state == 2 ? EnemyVoiceManager.Sound.Hurt : EnemyVoiceManager.Sound.Chase;
        if (Play(sounds, kind))
        {
            Vector2 pause = state == 0 ? idlePause : pauseBetweenSounds;
            nextSound = Time.time + voice.clip.length / voice.pitch +
                Random.Range(Mathf.Max(0f, pause.x), Mathf.Max(0f, Mathf.Max(pause.x, pause.y)));
        }
        else nextSound = Time.time + Random.Range(1f, 2f);
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
        float volume = GetVolume();
        //inaudible enemies shouldn't occupy a shared voice slot
        if (volume <= 0f) voice.Stop();
        voice.volume = Mathf.MoveTowards(voice.volume, volume * voiceLoudness, Time.deltaTime * 2f);
        RoomAudio2D room = RoomAudio2D.Current;
        lowPass.cutoffFrequency = room != null ? room.Cutoff : 22000f;
        distortionFilter.distortionLevel = Mathf.Min(0.8f, distortion + (room != null ? room.Distortion : 0f));
        reverbFilter.reverbPreset = room != null ? room.Reverb : AudioReverbPreset.Off;
    }

    private AudioClip Pick(AudioClip[] sounds)
    {
        if (sounds == null || sounds.Length == 0) return null;
        lastClips.TryGetValue(sounds, out AudioClip last);
        AudioClip chosen = null;
        int count = 0;
        //each usable entry has the same chance; skip the last clip if there is another choice
        foreach (AudioClip clip in sounds)
        {
            if (clip == null || clip == last) continue;
            if (Random.Range(0, ++count) == 0) chosen = clip;
        }
        return chosen != null ? chosen : last;
    }

    private bool Play(AudioClip[] sounds, EnemyVoiceManager.Sound kind)
    {
        if (GetVolume() <= 0f || (health != null && health.IsDead)) return false;
        AudioClip clip = Pick(sounds);
        if (clip == null || manager == null || !manager.Request(voice, kind)) return false;
        lastClips[sounds] = clip;
        currentSound = kind;
        voice.clip = clip;
        voice.pitch = basePitch * Random.Range(0.97f, 1.03f);
        voiceLoudness = Random.Range(loudness.x, loudness.y);
        voice.volume = GetVolume() * voiceLoudness;
        voice.Play();
        return true;
    }

    private void PlayAttack() => Play(attackSounds, EnemyVoiceManager.Sound.Attack);
    private void PlaySpit() => Play(spitSounds, EnemyVoiceManager.Sound.Attack);
    private void PlayHurt()
    {
        if (Time.time < nextHurt) return;
        nextHurt = Time.time + hurtPause;
        Play(hurtSounds, EnemyVoiceManager.Sound.Hurt);
    }

    private void OnDisable()
    {
        if (boss != null) { boss.Attacked -= PlayAttack; boss.Hurt -= PlayHurt; }
        if (enemy != null) { enemy.Attacked -= PlayAttack; enemy.Spat -= PlaySpit; }
        if (health != null) health.Hurt -= PlayHurt;
        if (spitter != null) spitter.Spat -= PlaySpit;
        voice.Stop();
    }
}
