using System.Collections;
using UnityEngine;

//one moving body that follows the player by pulling on walls
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class BossGrapple2D : MonoBehaviour
{
    public enum State { Rest, Chase, Burning, Windup, Pounce, Recover }

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private LayerMask wallMask;

    [Header("Rest")]
    [SerializeField] private bool aggro = true;
    [SerializeField] private Transform restPoint;
    [SerializeField] private vineController wakeVines;
    [SerializeField, Min(0f)] private float breathSize = 0.05f;
    [SerializeField, Min(0f)] private float breathSpeed = 1.5f;

    [Header("Burning")]
    [SerializeField] private BossBody2D vineBody;
    [SerializeField, Min(0.1f)] private float retreatTime = 2f;
    [SerializeField, Min(0f)] private float fireProtectionTime = 6f;
    [SerializeField] private State state;
    private float burningUntil;
    private float nextBurnCheck;
    private float fireProtectedUntil;

    public State CurrentState => state;
    public Transform Target => target;
    public LayerMask WallMask => wallMask;

    public void PrepareEncounter(Transform playerTarget, vineController camouflage, bool chase)
    {
        target = playerTarget;
        wakeVines = camouflage;
        restPoint = null;
        nextContactHit = Time.time + 3f;
        SetAggro(chase);
    }

    public void PauseForAttack(State attackState)
    {
        state = attackState;
        foreach (Arm arm in arms)
        {
            if (arm.routine != null) StopCoroutine(arm.routine);
            arm.routine = null;
            arm.joint.enabled = false;
            arm.aimedWall = null;
        }
    }

    public void ResumeGrappling()
    {
        state = State.Chase;
        UpdateState();
        OnEnable();
    }

    [Header("Vine")]
    [SerializeField] private vineController vines;

    [Header("Anchor")]
    [SerializeField, Range(1, 12)] private int anchorCount = 2;
    [SerializeField, Min(0.05f)] private float shotDelay = 0.3f;
    [SerializeField, Min(0f)] private float anchorSpacing = 4f;
    [SerializeField, Min(0.1f)] private float regenTime = 6f;
    [SerializeField, Min(1f)] private float reach = 12f;
    [SerializeField, Range(0f, 180f)] private float spread = 65f;
    [SerializeField, Range(1, 100)] private int moveSteps = 12;
    [SerializeField, Min(0f)] private float embedDelay = 0.05f;
    [SerializeField, Min(0.05f)] private float pause = 0.08f;

    [Header("Pull")]
    [SerializeField, Min(0.1f)] private float pullSpeed = 14f;
    [SerializeField, Min(0.1f)] private float pullTime = 0.8f;
    [SerializeField, Min(0.1f)] private float closeDistance = 1f;
    [SerializeField, Min(0.1f)] private float maxSpeed = 16f;

    [Header("Contact")]
    [SerializeField, Min(0f)] private float contactDamage = 15f;
    [SerializeField, Min(0.1f)] private float contactInterval = 0.75f;
    private float nextContactHit;

    [Header("Test Rope")]
    [SerializeField] private Color ropeColor = new Color(0.3f, 0.5f, 0.2f);
    [SerializeField, Min(0.01f)] private float ropeWidth = 0.08f;
    [SerializeField] private int sortingOrder = 110;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private Material ropeMaterial;
    private Transform bodyPoint;
    private Arm[] arms;
    private Arm pullingArm;
    private RigidbodyType2D movingBodyType;
    private bool resting;
    private Vector2 restPosition;
    private float restStarted;

    public void SetAggro(bool value)
    {
        aggro = value;
        UpdateState();
    }

    private void UpdateState()
    {
        //burning takes priority over both resting and chasing
        if (Time.time < burningUntil) state = State.Burning;
        else if (!aggro) state = State.Rest;
        else if (state == State.Rest || state == State.Burning) state = State.Chase;
    }

    public void Ignite()
    {
        //one retreat per ignition, without extending it on every burning check
        if (Time.time < fireProtectedUntil || Time.time < burningUntil) return;
        //fire wakes her even if the encounter trigger has not been entered
        aggro = true;
        burningUntil = Time.time + retreatTime;
        UpdateState();
    }

    private void Update()
    {
        if (burningUntil > 0f && Time.time >= burningUntil)
        {
            burningUntil = 0f;
            fireProtectedUntil = Time.time + fireProtectionTime;
            if (vineBody != null) vineBody.Extinguish(fireProtectionTime);
            foreach (Arm arm in arms)
                if (arm.vine != null) arm.vine.Extinguish(fireProtectionTime);
        }

        if (Time.time >= nextBurnCheck)
        {
            nextBurnCheck = Time.time + 0.1f;
            //the surrounding camouflage can wake her before her own body burns
            if (!aggro && wakeVines != null && wakeVines.IsBurning)
                SetAggro(true);
            if (!aggro && arms != null)
            {
                foreach (Arm arm in arms)
                {
                    if (arm.vine == null || !arm.vine.IsBurning) continue;
                    SetAggro(true);
                    break;
                }
            }

            if (vineBody != null && vineBody.IsBurning)
                Ignite();
        }

        UpdateState();
    }

    private Vector2 AimDirection()
    {
        if (state == State.Rest || target == null) return Vector2.up;
        Vector2 direction = ((Vector2)target.position - BodyCenter).normalized;
        return state == State.Burning ? -direction : direction;
    }

    //each anchor keeps its own joint, flight and burning vine
    private sealed class Arm
    {
        public DistanceJoint2D joint;
        public Transform anchor;
        public LineRenderer rope;
        public Coroutine routine;
        public VineRope2D vine;
        public Transform vineAnchor;
        public Transform wall;
        public Vector3 wallPoint;
        public Transform aimedWall;
        public Vector3 aimedPoint;
        public bool restMade;
        public float regrowAt;
    }

    private Vector2 BodyCenter => bodyCollider.bounds.center;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        movingBodyType = body.bodyType;
        if (vineBody == null) vineBody = GetComponentInChildren<BossBody2D>(true);
        UpdateState();
        bodyPoint = new GameObject("Grapple Point").transform;
        bodyPoint.SetParent(transform, false);
        bodyPoint.position = BodyCenter;

        ropeMaterial = new Material(Shader.Find("Sprites/Default"));
        arms = new Arm[anchorCount];
        for (int i = 0; i < arms.Length; i++)
        {
            Arm arm = new Arm();
            arms[i] = arm;
            arm.joint = gameObject.AddComponent<DistanceJoint2D>();
            //world-attached joints must keep contact with static floors and walls
            arm.joint.enableCollision = true;
            arm.joint.enabled = false;
            arm.joint.autoConfigureDistance = false;
            arm.joint.autoConfigureConnectedAnchor = false;
            arm.joint.maxDistanceOnly = true;
            arm.anchor = new GameObject("Grapple Anchor " + (i + 1)).transform;
            arm.rope = arm.anchor.gameObject.AddComponent<LineRenderer>();
            arm.rope.useWorldSpace = true;
            arm.rope.positionCount = 2;
            arm.rope.startWidth = arm.rope.endWidth = ropeWidth;
            arm.rope.startColor = arm.rope.endColor = ropeColor;
            arm.rope.sortingOrder = sortingOrder;
            arm.rope.sharedMaterial = ropeMaterial;
            arm.rope.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (arms == null) return;
        for (int i = 0; i < arms.Length; i++)
            if (arms[i].routine == null)
                arms[i].routine = StartCoroutine(Follow(arms[i], i * shotDelay));
    }

    private void FixedUpdate()
    {
        if (state == State.Rest)
        {
            if (!resting)
            {
                resting = true;
                restPosition = restPoint != null ? (Vector2)restPoint.position : body.position;
                restStarted = Time.time;
                body.linearVelocity = Vector2.zero;
                //hold its placed pose without gravity dragging it down
                body.bodyType = RigidbodyType2D.Kinematic;
            }

            float breath = Mathf.Sin((Time.time - restStarted) * breathSpeed) * breathSize;
            body.MovePosition(restPosition + Vector2.up * breath);
        }
        else if (resting)
        {
            resting = false;
            body.bodyType = movingBodyType;
            body.WakeUp();
        }

        foreach (Arm arm in arms)
        {
            if (arm.vine != null && arm.vine.IsBroken)
            {
                //a burned arm cannot immediately grow another rope into the fire
                arm.regrowAt = Time.time + regenTime;
                arm.restMade = false;
                Release(arm);
            }

            if (!arm.joint.enabled) continue;
            if (arm.wall == null)
                Release(arm);
            else
            {
                arm.joint.connectedAnchor = arm.wall.TransformPoint(arm.wallPoint);
                float distance = Vector2.Distance(BodyCenter, arm.joint.connectedAnchor);

                //the older arm keeps some slack so two joints cannot lock the body
                if (state != State.Rest && arm != pullingArm)
                    arm.joint.distance = Mathf.Max(arm.joint.distance, distance + 0.25f);

                if (arm.vineAnchor != null && arm.aimedWall == null)
                    arm.vineAnchor.position = arm.joint.connectedAnchor;

                if (arm.vine != null)
                    arm.vine.SetLength(arm.aimedWall != null
                        ? Vector2.Distance(BodyCenter, arm.vineAnchor.position)
                        : Mathf.Max(arm.joint.distance, distance));
            }
        }

        body.linearVelocity = Vector2.ClampMagnitude(
            body.linearVelocity, maxSpeed);
    }

    private void LateUpdate()
    {
        foreach (Arm arm in arms)
        {
            if (!arm.rope.enabled) continue;
            arm.rope.SetPosition(0, BodyCenter);
            arm.rope.SetPosition(1, arm.anchor.position);
        }
    }

    private IEnumerator Follow(Arm arm, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        while (true)
        {
            if (Time.time < arm.regrowAt)
            {
                yield return new WaitForSeconds(arm.regrowAt - Time.time);
                continue;
            }

            if (state == State.Rest)
            {
                //let physics place the body at its rest point before casting rays
                if (!resting)
                {
                    yield return new WaitForFixedUpdate();
                    continue;
                }

                //one resting attachment per arm, even if it later burns away
                if (!arm.restMade)
                {
                    arm.restMade = true;
                    if (FindAnchor(arm, out RaycastHit2D restHit))
                        yield return Grapple(arm, restHit);
                }
                yield return new WaitForSeconds(pause);
                continue;
            }

            arm.restMade = false;
            if (target == null)
            {
                PlayerMotor2D player = FindAnyObjectByType<PlayerMotor2D>();
                if (player != null) target = player.transform;
            }

            if (target != null &&
                FindAnchor(arm, out RaycastHit2D hit))
            {
                yield return Grapple(arm, hit);
            }

            yield return new WaitForSeconds(pause);
        }
    }

    private bool FindAnchor(Arm arm, out RaycastHit2D chosen)
    {
        chosen = default;
        Vector2 towardPlayer = AimDirection();
        Vector2 tangent = new Vector2(-towardPlayer.y, towardPlayer.x);

        //sample across the aiming cone so spaced anchors have more options
        float side = Random.value < 0.5f ? -1f : 1f;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 9; i++)
        {
            float angle = Mathf.Lerp(state != State.Rest ? -spread : -180f,
                state != State.Rest ? spread : 180f, i / 8f) * side;
            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = towardPlayer * Mathf.Cos(radians) +
                                tangent * Mathf.Sin(radians);

            RaycastHit2D hit = Physics2D.Raycast(
                BodyCenter, direction, reach, wallMask);

            //ignore surfaces too close to make a useful pull
            if (hit.collider == null || hit.distance <= closeDistance ||
                hit.collider.attachedRigidbody == body)
                continue;

            if (!HasSpace(arm, hit.point)) continue;

            //higher anchors help it glide instead of pulling into the floor
            float score = state != State.Rest
                ? Vector2.Dot(direction, towardPlayer) + direction.y * 0.6f
                : Random.value;
            if (score > bestScore)
            {
                bestScore = score;
                chosen = hit;
            }
        }

        return chosen.collider != null;
    }

    private bool HasSpace(Arm arm, Vector2 point)
    {
        foreach (Arm other in arms)
        {
            if (other == arm) continue;

            if (other.joint.enabled && other.wall != null &&
                Vector2.Distance(point, other.wall.TransformPoint(other.wallPoint)) < anchorSpacing)
                return false;

            //reserve the landing point while an anchor is still flying
            if (other.aimedWall != null &&
                Vector2.Distance(point, other.aimedWall.TransformPoint(other.aimedPoint)) < anchorSpacing)
                return false;
        }

        return true;
    }

    private IEnumerator Grapple(Arm arm, RaycastHit2D hit)
    {
        //keep the anchor attached even if the hit object moves
        Transform wall = hit.collider.transform;
        Vector3 localHit = wall.InverseTransformPoint(hit.point);
        arm.aimedWall = wall;
        arm.aimedPoint = localHit;
        Vector2 start = arm.vineAnchor != null
            ? (Vector2)arm.vineAnchor.position
            : BodyCenter;
        arm.anchor.position = start;
        arm.rope.enabled = arm.vine == null;

        //move the existing tip instead of destroying and rebuilding its vine
        int steps = Mathf.Max(1, moveSteps);
        for (int i = 1; i <= steps && wall != null; i++)
        {
            if (Time.time < arm.regrowAt) yield break;
            float t = Mathf.SmoothStep(0f, 1f, i / (float)steps);
            arm.anchor.position = Vector2.Lerp(
                start,
                wall.TransformPoint(localHit),
                t);

            if (arm.vineAnchor != null)
            {
                arm.vineAnchor.position = arm.anchor.position;
                arm.vine.SetLength(Vector2.Distance(BodyCenter, arm.anchor.position));
            }

            yield return new WaitForFixedUpdate();
        }

        if (wall != null)
        {
            yield return new WaitForSeconds(embedDelay);

            if (Time.time < arm.regrowAt || wall == null ||
                !HasSpace(arm, wall.TransformPoint(localHit)))
            {
                arm.rope.enabled = false;
                arm.aimedWall = null;
                yield break;
            }

            //keep the previous attachment until the new anchor lands
            arm.joint.enabled = false;
            arm.wall = wall;
            arm.wallPoint = localHit;
            arm.aimedWall = null;
            arm.anchor.position = wall.TransformPoint(localHit);

            arm.joint.anchor = transform.InverseTransformPoint(BodyCenter);
            arm.joint.distance = Vector2.Distance(BodyCenter, arm.anchor.position);
            arm.joint.connectedAnchor = arm.anchor.position;
            arm.joint.enabled = true;
            pullingArm = arm;
            body.WakeUp();
            arm.rope.enabled = vines == null;

            //reuse the same sprites and preserve their burning progress
            if (vines != null && arm.vine == null)
            {
                arm.vineAnchor = new GameObject("Vine Anchor").transform;
                arm.vineAnchor.position = arm.anchor.position;
                arm.vine = vines.CreateVine(bodyPoint, arm.vineAnchor, arm.joint.distance);
                if (Time.time < fireProtectedUntil)
                    arm.vine.Extinguish(fireProtectedUntil - Time.time);
                //the endpoint is cleaned up with the broken or released vine
                arm.vineAnchor.SetParent(arm.vine.transform, true);
                arm.rope.enabled = false;
            }

            float remaining = pullTime;
            while (state != State.Rest && wall != null && arm.joint.enabled && remaining > 0f &&
                   Vector2.Distance(BodyCenter, arm.anchor.position) > closeDistance)
            {
                arm.anchor.position = wall.TransformPoint(localHit);

                //stop reeling if this anchor opposes the current state direction
                if (target == null || Vector2.Dot(
                    AimDirection(),
                    (Vector2)arm.anchor.position - BodyCenter) < 0f)
                    break;

                arm.joint.connectedAnchor = arm.anchor.position;
                if (pullingArm == arm)
                    arm.joint.distance = Mathf.MoveTowards(
                        arm.joint.distance, closeDistance, pullSpeed * Time.fixedDeltaTime);

                remaining -= Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        //stay attached between attempts so gravity cannot simply drop it
        arm.rope.enabled = false;
        arm.aimedWall = null;
    }

    private void Release(Arm arm)
    {
        arm.joint.enabled = false;
        arm.rope.enabled = false;
        if (arm.vine != null && !arm.vine.IsBroken)
            Destroy(arm.vine.gameObject);
        arm.vine = null;
        arm.vineAnchor = null;
        arm.wall = null;
        arm.aimedWall = null;
        if (pullingArm == arm) pullingArm = null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HurtPlayer(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HurtPlayer(collision);
    }

    private void HurtPlayer(Collision2D collision)
    {
        if (state == State.Rest || state == State.Windup || state == State.Recover || contactDamage <= 0f ||
            Time.time < nextContactHit) return;

        PlayerVitals2D player = collision.collider.GetComponentInParent<PlayerVitals2D>();
        if (player == null || player.IsDead) return;

        //space out contact hits even if player invincibility is set to zero
        player.TakeDamage(contactDamage);
        nextContactHit = Time.time + contactInterval;
    }

    private void OnDisable()
    {
        if (arms == null) return;
        foreach (Arm arm in arms)
        {
            if (arm.routine != null) StopCoroutine(arm.routine);
            arm.routine = null;
            Release(arm);
            arm.restMade = false;
        }
        if (resting)
        {
            body.bodyType = movingBodyType;
            resting = false;
        }
    }

    private void OnDestroy()
    {
        if (arms != null)
        foreach (Arm arm in arms)
        {
            if (arm.anchor != null) Destroy(arm.anchor.gameObject);
            if (arm.joint != null) Destroy(arm.joint);
        }
        if (bodyPoint != null) Destroy(bodyPoint.gameObject);
        if (ropeMaterial != null) Destroy(ropeMaterial);
    }
}
