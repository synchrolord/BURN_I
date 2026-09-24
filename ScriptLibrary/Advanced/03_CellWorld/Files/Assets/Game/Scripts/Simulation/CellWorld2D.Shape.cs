using UnityEngine;

// Shared boundary support. A normal CellWorld uses its whole rectangle.
// CellWorldPoly2D supplies a mask once at startup; gameplay reads only the baked mask.
public partial class CellWorld2D
{
    private bool[] roomCells;
    private bool[] roomChunks;
    public bool InitializationFailed { get; private set; }
    public int IncludedCellCount { get; private set; }

    // Java equivalent: a subclass overrides this method to provide different behavior.
    protected virtual bool TryCreateCellMask(out bool[] mask)
    {
        mask = null; // No mask means the original rectangular world.
        return true;
    }

    public bool ContainsCell(int x, int y)
    {
        return !InitializationFailed && InBounds(x, y) && (roomCells == null || roomCells[Index(x, y)]);
    }

    private bool InitializeRoomShape()
    {
        if (width < 16 || height < 16 || width > 2048 || height > 2048 ||
            (long)width * height > 1048576 || cellSize < 0.01f || float.IsNaN(cellSize) || float.IsInfinity(cellSize))
        {
            Debug.LogError("CellWorld needs dimensions 16â€“2048, at most 1,048,576 cells, and finite Cell Size >= 0.01.", this);
            InitializationFailed = true;
            enabled = false;
            return false;
        }
        if (!TryCreateCellMask(out roomCells))
        {
            InitializationFailed = true;
            enabled = false;
            return false;
        }
        IncludedCellCount = width * height;
        if (roomCells != null)
        {
            IncludedCellCount = 0;
            foreach (bool included in roomCells)
                if (included) IncludedCellCount++;
        }
        return true;
    }

    private void BuildRoomChunks()
    {
        if (roomCells == null) return;
        roomChunks = new bool[chunkCountX * chunkCountY];
        for (int y = 1; y < height - 1; y++)
        for (int x = 1; x < width - 1; x++)
            if (roomCells[Index(x, y)]) roomChunks[ChunkIndex(x / chunkSize, y / chunkSize)] = true;
    }

    private bool IsRoomChunk(int x, int y)
    {
        return roomChunks == null || roomChunks[ChunkIndex(x, y)];
    }
}
