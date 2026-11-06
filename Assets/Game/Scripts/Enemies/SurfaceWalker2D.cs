using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class SurfaceWalker2D : MonoBehaviour
{
    private const float extraSurfaceDistance = 0.35f;
    private const float cornerDistance = 0.1f;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private Transform visualRoot;
    private LayerMask surfaceMask;

    private Vector2 surfaceNormal = Vector2.up;
    private float oldGravity;
    private RigidbodyConstraints2D oldConstraints;
    private Quaternion oldVisualRotation;
    private float direction = 1f;

    public float Direction => direction;

    public Vector2 MoveDirection =>
        GetTangent() * direction;


    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

        oldGravity = body.gravityScale;
        oldConstraints = body.constraints;

        body.gravityScale = 0f;
        body.constraints |=
            RigidbodyConstraints2D.FreezeRotation;
    }


    public void Setup(
        LayerMask newSurfaceMask,
        Transform newVisualRoot)
    {
        surfaceMask = newSurfaceMask;
        visualRoot = newVisualRoot;

        if (visualRoot != null)
            oldVisualRotation = visualRoot.localRotation;

        FindSurface();
    }


    public void Move(float newDirection, float speed)
    {
        if (Mathf.Abs(newDirection) > 0.01f)
            direction = Mathf.Sign(newDirection);

        if (!StayOnSurface())
        {
            Stop();
            return;
        }

        Vector2 moveDirection = MoveDirection;

        //turn onto a wall or ceiling directly ahead
        RaycastHit2D corner = Physics2D.Raycast(
            bodyCollider.bounds.center,
            moveDirection,
            GetOffset(moveDirection) + cornerDistance,
            surfaceMask);

        if (corner.collider != null)
        {
            surfaceNormal = corner.normal;
            SnapToSurface(corner);
            moveDirection = MoveDirection;
        }

        //turn back instead of walking away from the surface
        Vector2 supportPoint =
            (Vector2)bodyCollider.bounds.center +
            moveDirection *
            (GetOffset(moveDirection) + cornerDistance);

        bool hasSurfaceAhead = Physics2D.Raycast(
            supportPoint,
            -surfaceNormal,
            GetOffset(surfaceNormal) + extraSurfaceDistance,
            surfaceMask);

        if (!hasSurfaceAhead)
        {
            Reverse();
            moveDirection = MoveDirection;
        }

        //only turn the art so the offset collider stays stable
        if (visualRoot != null)
        {
            visualRoot.localRotation = Quaternion.Euler(
                0f,
                0f,
                Vector2.SignedAngle(
                    Vector2.up,
                    surfaceNormal));
        }

        body.linearVelocity =
            moveDirection * Mathf.Max(0f, speed);
    }


    public void MoveToward(Vector2 target, float speed)
    {
        Move(DirectionTo(target), speed);
    }


    public float DirectionTo(Vector2 target)
    {
        float side = Vector2.Dot(
            target - body.position,
            GetTangent());

        return Mathf.Abs(side) > 0.05f
            ? Mathf.Sign(side)
            : direction;
    }


    public bool MovingAwayFrom(Vector2 point)
    {
        return Vector2.Dot(
            body.position - point,
            MoveDirection) > 0f;
    }


    public void Reverse()
    {
        direction *= -1f;
    }


    public void Stop()
    {
        body.linearVelocity = Vector2.zero;
    }


    private bool StayOnSurface()
    {
        RaycastHit2D surface = Physics2D.Raycast(
            bodyCollider.bounds.center,
            -surfaceNormal,
            GetOffset(surfaceNormal) + extraSurfaceDistance,
            surfaceMask);

        if (surface.collider == null)
            return FindSurface();

        surfaceNormal = surface.normal;
        SnapToSurface(surface);
        return true;
    }


    private bool FindSurface()
    {
        Vector2[] directions =
        {
            Vector2.down,
            Vector2.up,
            Vector2.left,
            Vector2.right
        };

        RaycastHit2D closest = new RaycastHit2D();
        float closestDistance = float.MaxValue;

        foreach (Vector2 checkDirection in directions)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                bodyCollider.bounds.center,
                checkDirection,
                GetOffset(checkDirection) + 1.5f,
                surfaceMask);

            if (hit.collider == null ||
                hit.distance >= closestDistance)
            {
                continue;
            }

            closest = hit;
            closestDistance = hit.distance;
        }

        if (closest.collider == null)
            return false;

        surfaceNormal = closest.normal;
        SnapToSurface(closest);
        return true;
    }


    private void SnapToSurface(RaycastHit2D surface)
    {
        float correction =
            GetOffset(surfaceNormal) -
            surface.distance;

        body.position +=
            surfaceNormal * correction;
    }


    private float GetOffset(Vector2 direction)
    {
        Bounds bounds = bodyCollider.bounds;

        return
            Mathf.Abs(direction.x) * bounds.extents.x +
            Mathf.Abs(direction.y) * bounds.extents.y;
    }


    private Vector2 GetTangent()
    {
        return new Vector2(
            surfaceNormal.y,
            -surfaceNormal.x);
    }


    private void OnDisable()
    {
        if (body != null)
        {
            body.gravityScale = oldGravity;
            body.constraints = oldConstraints;
        }

        if (visualRoot != null)
            visualRoot.localRotation = oldVisualRotation;
    }
}
