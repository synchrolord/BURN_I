using UnityEngine;

// ": CellWorld2D" is like "extends CellWorld2D" in Java.
// Guns, plants and lights can use this anywhere they already accept CellWorld2D.
// The parent owns the simulation; this subclass only defines which cells belong to it.
[AddComponentMenu("BURN/Cell World Poly 2D")]
public sealed class CellWorldPoly2D : CellWorld2D
{
    [Header("Polygon room outline")]
    [Tooltip("Ordered corners in local world units, not cell indices. Last corner connects back to the first. Edit before Play.")]
    [SerializeField] private Vector2[] outline =
    {
        new Vector2(0, 0), new Vector2(8, 0), new Vector2(8, 3),
        new Vector2(3, 3), new Vector2(3, 8), new Vector2(0, 8)
    };

    public Vector2[] Outline => outline;

    // Unity calls Reset when the component is added in the Editor.
    private void Reset() { ResetOutlineToGrid(); }

    public void ResetOutlineToGrid()
    {
        if (Application.isPlaying) return;
        float right = Width * CellSize;
        float top = Height * CellSize;
        outline = new[] { Vector2.zero, new Vector2(right, 0), new Vector2(right, top), new Vector2(0, top) };
    }

    protected override bool TryCreateCellMask(out bool[] mask)
    {
        mask = null;
        if (!TryValidateOutline(out string error))
        {
            Debug.LogError("CellWorldPoly2D: " + error, this);
            return false;
        }
        mask = new bool[Width * Height];
        bool foundCell = false;
        // Keep the original one-cell outer boundary closed. The polygon adds its own boundary inside it.
        for (int y = 1; y < Height - 1; y++)
        for (int x = 1; x < Width - 1; x++)
        {
            Vector2 center = new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);
            bool inside = PointInside(center);
            mask[x + y * Width] = inside;
            foundCell |= inside;
        }
        if (!foundCell) Debug.LogError("CellWorldPoly2D: the outline contains no interior cell centers. Enlarge it or reduce Cell Size.", this);
        return foundCell;
    }

    // Cast an imaginary ray to the right. An odd number of edge crossings means inside.
    // This runs only while baking, not every simulation tick.
    private bool PointInside(Vector2 point)
    {
        bool inside = false;
        for (int i = 0, previous = outline.Length - 1; i < outline.Length; previous = i++)
        {
            Vector2 a = outline[previous], b = outline[i];
            if (OnSegment(a, b, point)) return true;
            if ((a.y > point.y) == (b.y > point.y)) continue;
            float crossingX = a.x + (point.y - a.y) * (b.x - a.x) / (b.y - a.y);
            if (point.x < crossingX) inside = !inside;
        }
        return inside;
    }

    public bool TryValidateOutline(out string error)
    {
        error = "";
        if (outline == null || outline.Length < 3)
        {
            error = "Add at least three corners. Do not repeat the first corner at the end.";
            return false;
        }
        float area = 0;
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
            if (float.IsNaN(a.x) || float.IsNaN(a.y) || float.IsInfinity(a.x) || float.IsInfinity(a.y))
            {
                error = "Every corner must have finite X and Y values.";
                return false;
            }
            if (a.x < 0 || a.y < 0 || a.x > Width * CellSize || a.y > Height * CellSize)
            {
                error = "Keep corners inside the grid rectangle. Increase Width/Height or move the outline points.";
                return false;
            }
            if ((b - a).sqrMagnitude < 0.00000001f)
            {
                error = "Two consecutive corners overlap. Remove the duplicate corner.";
                return false;
            }
            area += a.x * b.y - b.x * a.y;
            for (int j = i + 1; j < outline.Length; j++)
            {
                if (j == (i + 1) % outline.Length || (j + 1) % outline.Length == i) continue;
                if (EdgesIntersect(a, b, outline[j], outline[(j + 1) % outline.Length]))
                {
                    error = "Outline edges cross or touch each other. Draw one simple closed loop.";
                    return false;
                }
            }
        }
        if (Mathf.Abs(area) < 0.000001f)
        {
            error = "The outline has no area. Its corners cannot all lie on one line.";
            return false;
        }
        return true;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 point)
    {
        return (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
    }

    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 point)
    {
        return Mathf.Abs(Cross(a, b, point)) < 0.000001f &&
            point.x >= Mathf.Min(a.x, b.x) && point.x <= Mathf.Max(a.x, b.x) &&
            point.y >= Mathf.Min(a.y, b.y) && point.y <= Mathf.Max(a.y, b.y);
    }

    private static bool EdgesIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (OnSegment(a, b, c) || OnSegment(a, b, d) || OnSegment(c, d, a) || OnSegment(c, d, b)) return true;
        return Cross(a, b, c) * Cross(a, b, d) < 0 && Cross(c, d, a) * Cross(c, d, b) < 0;
    }
}
