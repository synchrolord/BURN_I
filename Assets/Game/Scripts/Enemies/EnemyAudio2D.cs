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
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
    [SerializeField, Min(1f)] private float hearingDistance = 40f;

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
        AudioListener ears = FindAnyObjectByType<AudioListener>();
        if (ears != null) listener = ears.transform;
    }

    private void OnEnable()
    {
        if (boss != null) boss.Attacked += PlayAttack;
        if (enemy != null) { enemy.Attacked += PlayAttack; enemy.Spat += PlaySpit; }
        if (health != null) health.Hurt += PlayHurt;
        if (spitter != null) spitter.Spat += PlaySpit;
        if (boss != null) boss.Hurt += PlayHurt;
        nextSound = nextHurt = 0f;
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
            nextSound = 0f;
        }
        effects.volume = GetVolume();
        //keep attacks and hurt sounds clear over the background voice
        voice.volume = effects.volume * (effects.isPlaying ? 0.35f : 1f);
        if (voice.isPlaying || Time.time < nextSound) return;
        AudioClip[] sounds = previousState == 0 ? idleSounds :
            previousState == 2 ? fireSounds : chaseSounds;
        voice.clip = Pick(sounds);
        if (voice.clip != null) voice.Play();
        float pause = previousState == 2 ? 0f :
            Random.Range(Mathf.Max(0f, pauseBetweenSounds.x), Mathf.Max(pauseBetweenSounds.x, pauseBetweenSounds.y));
        nextSound = Time.time + (voice.clip != null ? voice.clip.length : 0.5f) + pause;
    }

    private float GetVolume()
    {
        Transform target = boss != null ? boss.Target : enemy != null ? enemy.Target : null;
        Transform hearFrom = target != null ? target : listener;
        float distance = hearFrom != null ? Vector2.Distance(transform.position, hearFrom.position) : hearingDistance;
        return volume * GameOptions.EffectsVolume * Mathf.Clamp01(1f - distance / hearingDistance);
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
        //several vines can break together; don't stack a scream for every piece
        if (Time.time < nextHurt) return;
        nextHurt = Time.time + hurtPause;
        PlayEffect(hurtSounds);
    }
    private void PlayEffect(AudioClip[] sounds)
    {
        AudioClip clip = Pick(sounds);
        if (clip == null) return;
        effects.volume = GetVolume();
        effects.PlayOneShot(clip);
    }

    private void OnDisable()
    {
        if (boss != null) boss.Attacked -= PlayAttack;
        if (enemy != null) { enemy.Attacked -= PlayAttack; enemy.Spat -= PlaySpit; }
        if (health != null) health.Hurt -= PlayHurt;
        if (spitter != null) spitter.Spat -= PlaySpit;
        if (boss != null) boss.Hurt -= PlayHurt;
        voice.Stop();
        effects.Stop();
    }
}
