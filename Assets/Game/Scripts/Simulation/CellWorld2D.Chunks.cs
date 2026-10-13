using System;
using UnityEngine;


public partial class CellWorld2D
{
    //how long each chunk stays awake
    private void AgeChunks()
    {
        if (simulateAllChunks) return;
        for (int i = 0; i < chunkAwakeTicks.Length; i++)
            if (chunkAwakeTicks[i] > 0) chunkAwakeTicks[i]--;
    }

    //keep chunks around the player awake
    private void WakeChunksNearTarget()
    {
        if (simulateAllChunks || activityTarget == null) return;
        
        if (!TryWorldToCell(
                activityTarget.position, out Vector2Int cell)) return;

        //convert cell into chunk coordinates
        int centerX = cell.x / chunkSize;
        int centerY = cell.y / chunkSize;

        for (int y = centerY - activeChunkRadius; y <= centerY + activeChunkRadius; y++)
        for (int x = centerX - activeChunkRadius; x <= centerX + activeChunkRadius; x++)
            WakeChunk(x, y);
    }

    private void WakeCell(int cellX, int cellY )
    {
        int chunkX = cellX / chunkSize;
        int chunkY = cellY / chunkSize;

        WakeChunk(chunkX, chunkY);

        //find where the cell is inside its chunk
        int localX = cellX - chunkX * chunkSize;
        int localY = cellY - chunkY * chunkSize;

        const int border = 1;

        // Only wake neighbours when activity is actually


        if (localX <= border)
            WakeChunk(chunkX - 1, chunkY);

        if (localX >= chunkSize - 1 - border)
            WakeChunk(chunkX + 1, chunkY);

        if (localY <= border)
            WakeChunk(chunkX, chunkY - 1);

        if (localY >= chunkSize - 1 - border)
            WakeChunk(chunkX, chunkY + 1);
    }

    //reset the awake timer for a valid room chunk
    private void WakeChunk(int chunkX, int chunkY)
    {
        if (chunkX < 0 || chunkX >= chunkCountX || chunkY < 0 || chunkY >= chunkCountY || !IsRoomChunk(chunkX, chunkY)) return;
        chunkAwakeTicks[ChunkIndex(chunkX, chunkY)] = (byte)Mathf.Min(255, chunkStayAwakeTicks);
    }

    //give every chunk a fresh awake timer
    private void WakeEveryChunk()
    {
        byte awakeTime = (byte)Mathf.Min(255, chunkStayAwakeTicks);
        for (int i = 0; i < chunkAwakeTicks.Length; i++) chunkAwakeTicks[i] = awakeTime;
    }

    //only simulate room chunks that are forced awake or still have time left
    private bool IsChunkAwake(int chunkX, int chunkY) =>
        IsRoomChunk(chunkX, chunkY) && (simulateAllChunks || chunkAwakeTicks[ChunkIndex(chunkX, chunkY)] > 0);

    //convert chunk coordinates into the chunk array index
    private int ChunkIndex(int chunkX, int chunkY) => chunkX + chunkY * chunkCountX;

    private void GetChunkBounds(int chunkX, int chunkY,
        out int minCellX, out int maxCellX,
        out int minCellY, out int maxCellY)
    {
        // The outside cell ring is a solid boundary and never needs simulation.
        minCellX = Mathf.Max(1, chunkX * chunkSize);
        minCellY = Mathf.Max(1, chunkY * chunkSize);
        maxCellX = Mathf.Min(width - 2, (chunkX + 1) * chunkSize - 1);
        maxCellY = Mathf.Min(height - 2, (chunkY + 1) * chunkSize - 1);
    }

    //allow cells in awake chunks to move again this tick
    private void ClearMovementFlags()
    {
        Array.Clear(movedWater, 0, movedWater.Length);
        Array.Clear(movedFuel, 0, movedFuel.Length);
        Array.Clear(movedAcid, 0, movedAcid.Length);
        Array.Clear(movedSmoke, 0, movedSmoke.Length);
    }
}
