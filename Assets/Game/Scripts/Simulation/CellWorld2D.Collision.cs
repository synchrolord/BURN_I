using System;
using System.Collections.Generic;
using UnityEngine;

public partial class CellWorld2D
{
    //build the blocked-cell array from the current Unity colliders
    private void BakeSolidsFromPhysics()
    {
        Physics2D.SyncTransforms();
        Array.Clear(solid, 0, solid.Length);

        //close the outside ring so materials stay inside the grid
        for (int x = 0; x < width; x++)
        {
            solid[Index(x, 0)] = true;
            solid[Index(x, height - 1)] = true;
        }

        for (int y = 0; y < height; y++)
        {
            solid[Index(0, y)] = true;
            solid[Index(width - 1, y)] = true;
        }

        //check a slightly smaller box inside each cell
        Vector2 sampleSize = Vector2.one * (cellSize * 0.85f);

        for (int y = 1; y < height - 1; y++)
        for (int x = 1; x < width - 1; x++)
        {
            int i = Index(x, y);
            //block cells outside the room or overlapping the solid layers
            solid[i] = !ContainsCell(x, y) || Physics2D.OverlapBox(CellToWorld(x, y), sampleSize, 0f, solidMask.value) != null;

            if (solid[i])
            {
                //clear materials from walls, but allow registered plants to keep fire
                bool clearFire = !ContainsCell(x, y) || !HasPlant(i);
                ClearCell(i, clearFire);
            }
        }
    }

    /// <summary>Diagnostic count for the latest local collider refresh, not a full-world scan.</summary>
    public int LastLocalSolidRefreshCellCount { get; private set; }

    //collect the unique grid cells near a changed collider
    internal void CollectSolidRefreshCells(Bounds worldBounds, HashSet<int> cells)
    {
        if (!IsReady) return;
        // Include query extents and boundary rounding. Transform all AABB corners for rotated grids.
        worldBounds.Expand(cellSize * 2f);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int y = 0; y < 2; y++)
        for (int x = 0; x < 2; x++)
        {
            Vector2 local = transform.InverseTransformPoint(new Vector3(x == 0 ? worldBounds.min.x : worldBounds.max.x,
                y == 0 ? worldBounds.min.y : worldBounds.max.y, worldBounds.center.z));
            min = Vector2.Min(min, local); max = Vector2.Max(max, local);
        }
        //convert the affected area to cell coordinates and keep it inside the border
        int minX = Mathf.Max(1, Mathf.FloorToInt(min.x / cellSize));
        int maxX = Mathf.Min(width - 2, Mathf.FloorToInt(max.x / cellSize));
        int minY = Mathf.Max(1, Mathf.FloorToInt(min.y / cellSize));
        int maxY = Mathf.Min(height - 2, Mathf.FloorToInt(max.y / cellSize));
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++) cells.Add(Index(x, y));
    }

    //recheck only the cells affected by collision changes
    internal void RefreshSolidCells(HashSet<int> cells)
    {
        if (!IsReady) return;
        Physics2D.SyncTransforms();
        LastLocalSolidRefreshCellCount = cells.Count;
        Vector2 sampleSize = Vector2.one * (cellSize * 0.85f);
        foreach (int i in cells)
        {
            int x = i % width, y = i / width;
            bool blocked = !ContainsCell(x, y) || x == 0 || y == 0 || x == width - 1 || y == height - 1 ||
                Physics2D.OverlapBox(CellToWorld(x, y), sampleSize, 0f, solidMask.value) != null;
            //leave unchanged cells alone and update cells whose blocked state changed
            if (solid[i] == blocked) continue;
            solid[i] = blocked;
            if (blocked)
            {
                bool clearFire = !ContainsCell(x, y) || !HasPlant(i);
                ClearCell(i, clearFire);
            }
            WakeCell(x, y);
            textureNeedsRefresh = true;
        }
    }
}
