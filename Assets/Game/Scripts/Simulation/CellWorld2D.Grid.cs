using UnityEngine;

public sealed partial class CellWorld2D
{
    public bool TryWorldToCell(Vector2 worldPosition, out Vector2Int cell)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        cell = new Vector2Int(
            Mathf.FloorToInt(local.x / cellSize),
            Mathf.FloorToInt(local.y / cellSize)
        );
        return InBounds(cell.x, cell.y);
    }

    public Vector2 CellToWorld(int x, int y)
    {
        Vector3 local = new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0f);
        Vector3 world = transform.TransformPoint(local);
        return new Vector2(world.x, world.y);
    }

    private int Index(int x, int y) => x + y * width;
    private bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    private bool IsInsideCircle(int x, int y, Vector2Int center, int radiusSquared)
    {
        if (!InBounds(x, y)) return false;
        int dx = x - center.x;
        int dy = y - center.y;
        return dx * dx + dy * dy <= radiusSquared;
    }
}
