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
    private bool attached;

    public float Direction => direction;

    public Vector2 MoveDirection =>
        GetTangent() * direction;


    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

        oldGravity = body.gravityScale;
        oldConstraints = body.constraints;

        body.gravityScale = oldGravity;
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

        attached = FindSurface();
    }


    public void Move(float newDirection, float speed)
    {
        if (Mathf.Abs(newDirection) > 0.01f)
            direction = Mathf.Sign(newDirection);

        if (!StayOnSurface())
        {
            //fall normally until there is a surface close enough to grab
            body.gravityScale = oldGravity;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            return;
        }

        body.gravityScale = 0f;
        Vector2 moveDirection = MoveDirection;

        //turn onto a wall or ceiling directly ahead
        RaycastHit2D corner = Physics2D.Raycast(
            bodyCollider.bounds.center,
            moveDirection,
            GetOffset(moveDirection) + cornerDistance,
            surfaceMask);

        if (corner.collider != null &&
            corner.distance > 0.001f && Vector2.Dot(corner.normal, moveDirection) < -0.5f)
        {
            surfaceNormal = corner.normal;
            SnapToSurface(corner);
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
        //stopping in midair should not cancel the fall
        body.linearVelocity = body.gravityScale == 0f
            ? Vector2.zero : new Vector2(0f, body.linearVelocity.y);
    }


    private bool StayOnSurface()
    {
        if (!attached)
        {
            attached = FindSurface();
            return attached;
        }

        RaycastHit2D surface = Physics2D.Raycast(
            bodyCollider.bounds.center,
            -surfaceNormal,
            GetOffset(surfaceNormal) + extraSurfaceDistance,
            surfaceMask);

        if (surface.collider == null || surface.distance <= 0.001f)
        {
            //look back around the edge for the next face of the same corner
            Vector2 forward = MoveDirection;
            Vector2 centre = bodyCollider.bounds.center;
            Vector2 origin = centre + forward * (GetOffset(forward) + cornerDistance)
                - surfaceNormal * (GetOffset(surfaceNormal) + extraSurfaceDistance);
            RaycastHit2D edge = Physics2D.Raycast(origin, -forward,
                GetOffset(forward) * 2f + cornerDistance * 2f, surfaceMask);

            if (edge.collider != null && edge.distance > 0.001f &&
                Vector2.Dot(edge.normal, forward) > 0.5f)
            {
                surfaceNormal = edge.normal;
                //place the collider's centre, since its pivot is down at its feet
                Vector2 target = edge.point + surfaceNormal * (GetOffset(surfaceNormal) + 0.02f);
                body.position += target - centre;
                return true;
            }

            attached = false;
            return false;
        }

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
                GetOffset(checkDirection) + extraSurfaceDistance,
                surfaceMask);

            if (hit.collider == null || hit.distance <= 0.001f ||
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
            GetOffset(surfaceNormal) + 0.02f -
            Vector2.Dot((Vector2)bodyCollider.bounds.center - surface.point, surfaceNormal);

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
