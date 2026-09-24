using System;
using UnityEngine;

// A chunk is a small rectangle of cells. Settled chunks sleep; edits and movement wake them.
public sealed partial class CellWorld2D
{
    private void AgeChunks()
    {
        if (simulateAllChunks) return;
        for (int i = 0; i < chunkAwakeTicks.Length; i++)
            if (chunkAwakeTicks[i] > 0) chunkAwakeTicks[i]--;
    }

    private void WakeChunksNearTarget()
    {
        if (simulateAllChunks || activityTarget == null) return;
        if (!TryWorldToCell(activityTarget.position, out Vector2Int cell)) return;

        int centerX = cell.x / chunkSize;
        int centerY = cell.y / chunkSize;

        for (int y = centerY - activeChunkRadius; y <= centerY + activeChunkRadius; y++)
        for (int x = centerX - activeChunkRadius; x <= centerX + activeChunkRadius; x++)
            WakeChunk(x, y);
    }

    private void WakeCell(int x, int y)
    {
        int chunkX = x / chunkSize;
        int chunkY = y / chunkSize;

        WakeChunk(chunkX, chunkY);

        int localX = x - chunkX * chunkSize;
        int localY = y - chunkY * chunkSize;

        const int border = 1;

        // Only wake neighbours when activity is actually
        // close to a chunk boundary.

        if (localX <= border)
            WakeChunk(chunkX - 1, chunkY);

        if (localX >= chunkSize - 1 - border)
            WakeChunk(chunkX + 1, chunkY);

        if (localY <= border)
            WakeChunk(chunkX, chunkY - 1);

        if (localY >= chunkSize - 1 - border)
            WakeChunk(chunkX, chunkY + 1);
    }

    private void WakeChunk(int x, int y)
    {
        if (x < 0 || x >= chunkCountX || y < 0 || y >= chunkCountY) return;
        chunkAwakeTicks[ChunkIndex(x, y)] = (byte)Mathf.Min(255, chunkStayAwakeTicks);
    }

    private void WakeEveryChunk()
    {
        byte awakeTime = (byte)Mathf.Min(255, chunkStayAwakeTicks);
        for (int i = 0; i < chunkAwakeTicks.Length; i++) chunkAwakeTicks[i] = awakeTime;
    }

    private bool IsChunkAwake(int x, int y) =>
        simulateAllChunks || chunkAwakeTicks[ChunkIndex(x, y)] > 0;

    private int ChunkIndex(int x, int y) => x + y * chunkCountX;

    private void GetChunkBounds(int x, int y,
        out int minX, out int maxX, out int minY, out int maxY)
    {
        // The outside cell ring is a solid boundary and never needs simulation.
        minX = Mathf.Max(1, x * chunkSize);
        minY = Mathf.Max(1, y * chunkSize);
        maxX = Mathf.Min(width - 2, (x + 1) * chunkSize - 1);
        maxY = Mathf.Min(height - 2, (y + 1) * chunkSize - 1);
    }

    private void ClearMovementFlagsInAwakeChunks()
    {
        for (int cy = 0; cy < chunkCountY; cy++)
        for (int cx = 0; cx < chunkCountX; cx++)
        {
            if (!IsChunkAwake(cx, cy)) continue;
            GetChunkBounds(cx, cy, out int minX, out int maxX, out int minY, out int maxY);
            int rowLength = maxX - minX + 1;

            for (int y = minY; y <= maxY; y++)
            {
                int start = Index(minX, y);
                Array.Clear(movedWater, start, rowLength);
                Array.Clear(movedFuel, start, rowLength);
                Array.Clear(movedSmoke, start, rowLength);
            }
        }
    }
}
