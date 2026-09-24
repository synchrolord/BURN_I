using System.Collections.Generic;
using UnityEngine;

public partial class CellWorld2D
{
    // A flat array is a rectangle stored row by row: index = x + y * width.
    private byte[] water, fuel, smoke, fire;
    private bool[] solid, movedWater, movedFuel, movedSmoke;
    private byte[] pendingFire;
    private readonly List<int> pendingFireIndices = new List<int>();
    private byte[] chunkAwakeTicks;
    private int chunkCountX, chunkCountY, tickNumber;
    private float accumulator;
    private bool paused, managedExternally, textureNeedsRefresh;
    private SpriteRenderer spriteRenderer;
    private Texture2D runtimeTexture;
    private Sprite runtimeSprite;
    private Color32[] pixels;
    internal bool IsReady => water != null && fuel != null && smoke != null && fire != null && solid != null;

    private void InitializeGrid()
    {
        if (!InitializeRoomShape()) return;
        ticksPerSecond = Mathf.Clamp(ticksPerSecond, 1f, 60f);
        maximumTicksPerFrame = Mathf.Clamp(maximumTicksPerFrame, 1, 8);
        chunkSize = Mathf.Clamp(chunkSize, 8, 64);
        chunkStayAwakeTicks = Mathf.Clamp(chunkStayAwakeTicks, 2, 200);
        liquidCapacity = Mathf.Clamp(liquidCapacity, 1, 32);
        smokeCapacity = Mathf.Clamp(smokeCapacity, 1, 16);
        fuelBurnEveryTicks = Mathf.Max(1, fuelBurnEveryTicks);
        fireDecayEveryTicks = Mathf.Max(1, fireDecayEveryTicks);
        fireSpreadEveryTicks = Mathf.Max(1, fireSpreadEveryTicks);
        paused = startPaused;
        spriteRenderer = GetComponent<SpriteRenderer>();
        int count = width * height;
        water = new byte[count];
        fuel = new byte[count];
        smoke = new byte[count];
        fire = new byte[count];
        solid = new bool[count];
        movedWater = new bool[count];
        movedFuel = new bool[count];
        movedSmoke = new bool[count];
        pendingFire = new byte[count];
        pixels = new Color32[count];
        chunkCountX = Mathf.CeilToInt(width / (float)chunkSize);
        chunkCountY = Mathf.CeilToInt(height / (float)chunkSize);
        chunkAwakeTicks = new byte[chunkCountX * chunkCountY];
        BuildRoomChunks();
        CreateRuntimeTexture();
        BakeSolidsFromPhysics();
        textureNeedsRefresh = true;
    }

    public bool TryWorldToCell(Vector2 worldPosition, out Vector2Int cell)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        cell = new Vector2Int(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(local.y / cellSize));
        return ContainsCell(cell.x, cell.y);
    }

    public Vector2 CellToWorld(int x, int y) =>
        transform.TransformPoint(new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize));

    private int Index(int x, int y) => x + y * width;
    private bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;
    private bool IsInsideCircle(int x, int y, Vector2Int center, int radiusSquared)
    {
        int dx = x - center.x, dy = y - center.y;
        return ContainsCell(x, y) && dx * dx + dy * dy <= radiusSquared;
    }

    // Compatibility for callers that choose to advance the same simulation themselves.
    internal void SetManagedExternally(bool value)
    {
        managedExternally = value;
        accumulator = 0f;
    }
    internal void ManagedSimulateOneTick()
    {
        if (!paused && IsReady) SimulateOneTick();
    }
}
