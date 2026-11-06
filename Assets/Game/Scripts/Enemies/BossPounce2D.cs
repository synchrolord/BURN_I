using System.Collections;
using UnityEngine;

//readable attacks layered over the existing grappling movement
[RequireComponent(typeof(BossGrapple2D), typeof(Rigidbody2D))]
public sealed class BossPounce2D : MonoBehaviour
{
    [SerializeField, Min(1f)] private float approachDistance = 10f;
    [SerializeField, Min(0.2f)] private float windupTime = 1f;
    [SerializeField, Min(0.1f)] private float pounceTime = 0.35f;
    [SerializeField, Min(0f)] private float pounceSpeed = 18f;
    [SerializeField, Min(0.2f)] private float recoveryTime = 1.2f;
    [SerializeField, Min(0f)] private float spawnGrace = 3f;
    [SerializeField] private AudioClip warningSound;
    [SerializeField, Range(0f, 1f)] private float warningVolume = 0.5f;

    private BossGrapple2D boss;
    private Rigidbody2D body;
    private AudioSource voice;
    private AudioClip generatedWarning;
    private Coroutine attack;
    private float gravity;
    private float nextAttack;

    private void Awake()
    {
        boss = GetComponent<BossGrapple2D>();
        body = GetComponent<Rigidbody2D>();
        gravity = body.gravityScale;
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        if (warningSound == null)
        {
            //a short rising whistle until a proper warning clip is assigned
            float[] samples = new float[8820];
            float phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)samples.Length;
                phase += Mathf.PI * 2f * Mathf.Lerp(550f, 1200f, t) / 44100f;
                samples[i] = Mathf.Sin(phase) * Mathf.Sin(t * Mathf.PI) * 0.35f;
            }
            generatedWarning = AudioClip.Create("MABEL Warning", samples.Length, 1, 44100, false);
            generatedWarning.SetData(samples, 0);
        }
    }

    private void OnEnable() => nextAttack = Time.time + spawnGrace;

    private void Update()
    {
        if (attack != null)
        {
            if (boss.CurrentState == BossGrapple2D.State.Burning ||
                boss.CurrentState == BossGrapple2D.State.Rest) CancelAttack();
            return;
        }
        if (!boss.isActiveAndEnabled || boss.CurrentState != BossGrapple2D.State.Chase ||
            boss.Target == null || Time.time < nextAttack) return;

        PlayerVitals2D player = boss.Target.GetComponentInParent<PlayerVitals2D>();
        if (player == null || player.IsDead) return;
        Vector2 direction = player.BodyCenter - body.position;
        if (direction.magnitude > approachDistance ||
            Physics2D.Raycast(body.position, direction, direction.magnitude, boss.WallMask)) return;
        attack = StartCoroutine(Pounce(direction.normalized));
    }

    private IEnumerator Pounce(Vector2 direction)
    {
        //lock the aim at the warning so moving afterward is a real dodge
        boss.PauseForAttack(BossGrapple2D.State.Windup);
        body.linearVelocity = Vector2.zero;
        body.gravityScale = 0f;
        voice.PlayOneShot(warningSound != null ? warningSound : generatedWarning,
            warningVolume * GameOptions.EffectsVolume);
        yield return new WaitForSeconds(windupTime);

        boss.PauseForAttack(BossGrapple2D.State.Pounce);
        body.gravityScale = gravity;
        body.linearVelocity = direction * pounceSpeed;
        yield return new WaitForSeconds(pounceTime);

        boss.PauseForAttack(BossGrapple2D.State.Recover);
        body.linearVelocity *= 0.2f;
        yield return new WaitForSeconds(recoveryTime);
        attack = null;
        nextAttack = Time.time + 0.5f;
        boss.ResumeGrappling();
    }

    private void CancelAttack()
    {
        if (attack != null) StopCoroutine(attack);
        attack = null;
        body.gravityScale = gravity;
        voice.Stop();
        nextAttack = Time.time + spawnGrace;
        if (boss.isActiveAndEnabled) boss.ResumeGrappling();
    }

    private void OnDisable() { if (body != null) CancelAttack(); }
    private void OnDestroy() { if (generatedWarning != null) Destroy(generatedWarning); }
}
