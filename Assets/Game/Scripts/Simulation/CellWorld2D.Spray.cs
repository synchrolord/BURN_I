using System.Collections.Generic;
using UnityEngine;

public partial class CellWorld2D
{
    // One travelled segment, with its endpoint moved outside any wall it hits.
    public struct SprayPath
    {
        public Vector2 start, end, surface;
        public bool blocked;
    }

    private readonly List<RaycastHit2D> sprayHits = new List<RaycastHit2D>(16);

    public SprayPath TraceSpray(Vector2 start, Vector2 end, LayerMask obstacles, Transform source)
    {
        SprayPath path = new SprayPath { start = start, end = end };
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(obstacles);
        filter.useTriggers = false;
        Physics2D.Linecast(start, end, filter, sprayHits);

        // Unity returns nearest first. Ignore the player and its attached objects.
        foreach (RaycastHit2D hit in sprayHits)
        {
            if (source != null && hit.collider.transform.IsChildOf(source)) continue;
            path.blocked = true;
            path.end = hit.point + hit.normal * cellSize * 0.55f;
            path.surface = hit.point - hit.normal * cellSize * 0.5f;
            break;
        }
        return path;
    }

    // Return the water still travelling. A finished shot deposits what remains.
    public int ApplyWaterSpray(SprayPath path, int amount, bool finished)
    {
        int steps = SpraySteps(path);
        for (int i = 1; i <= steps && amount > 0; i++)
        {
            Vector2 point = Vector2.Lerp(path.start, path.end, i / (float)steps);
            if (GetFireEnergy(point) > 0)
                amount -= ApplyWaterImpact(point, amount);
        }

        if (path.blocked) amount -= ApplyPlantWaterImpact(path.surface, amount);
        if (path.blocked || finished)
        {
            ApplyWaterImpact(path.end, amount);
            return 0;
        }
        return amount;
    }

    public void ApplyFlameSpray(SprayPath path, int radius)
    {
        int steps = SpraySteps(path);
        for (int i = 1; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(path.start, path.end, i / (float)steps);
            Ignite(point, radius);
        }
        if (path.blocked) Ignite(path.surface, radius);
    }

    private int SpraySteps(SprayPath path)
    {
        // Half-cell spacing keeps fast sprays from skipping fire or plants.
        return Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(path.start, path.end) / (cellSize * 0.5f)));
    }
}
