using System;
using System.Collections.Generic;
using UnityEngine;

// "partial" only means this one class is split into several smaller files.
// Attach ONLY this CellWorld2D.cs file to the CellWorld GameObject.
[RequireComponent(typeof(SpriteRenderer))]
public sealed partial class CellWorld2D : MonoBehaviour
{
    
    private bool managedExternally;

    internal bool IsReady =>
        water != null &&
        fuel != null &&
        smoke != null &&
        fire != null &&
        solid != null;

    internal void SetManagedExternally(bool value)
    {
        managedExternally = value;
        accumulator = 0f;
    }

    internal void ManagedSimulateOneTick()
    {
        if (paused || !IsReady)
            return;

        SimulateOneTick();
    }
    
    
    [Header("Grid")]
    [SerializeField, Min(16)] private int width = 240;
    [SerializeField, Min(16)] private int height = 136;
    [SerializeField, Min(0.01f)] private float cellSize = 0.125f;
    [SerializeField] private LayerMask solidMask;

    [Header("Simulation")]
    [SerializeField, Range(1f, 60f)] private float ticksPerSecond = 20f;
    [SerializeField, Range(1, 8)] private int maximumTicksPerFrame = 3;
    [SerializeField] private Transform activityTarget;
    [SerializeField] private bool simulateAllChunks;

    [Header("Chunk Sleeping")]
    [SerializeField, Range(8, 64)] private int chunkSize = 32;
    [SerializeField, Range(0, 4)] private int activeChunkRadius = 1;
    [SerializeField, Range(1, 200)] private int chunkStayAwakeTicks = 40;

    [Header("Liquid")]
    [SerializeField, Range(1, 16)]
    private int liquidCapacity = 8;

    [Tooltip("0 = runny; 1 = thick/sticky.")]
    [SerializeField, Range(0f, 1f)]
    private float waterViscosity = 0.05f;

    [Tooltip("How far water looks sideways for somewhere it can fall.")]
    [SerializeField, Range(1, 16)]
    private int waterSpreadSearchDistance = 8;

    [Tooltip("How aggressively water pushes sideways toward a nearby drop.")]
    [SerializeField, Range(1, 6)] private int waterSidewaysPasses = 3;
    [Tooltip("0 = runny; 1 = thick/sticky.")]
    [SerializeField, Range(0f, 1f)] private float fuelViscosity = 0.55f;
    [SerializeField, Range(1, 16)] private int fuelSpreadSearchDistance = 4;
    [SerializeField, Range(1, 6)] private int fuelSidewaysPasses = 1;
    
    [Tooltip("How many liquid units per tick oil can exchange upward through water.")]
    [SerializeField, Range(1, 8)]
    private int oilBuoyancyStrength = 1;

    [Header("Smoke")]
    [SerializeField, Range(1, 16)] private int smokeCapacity = 8;
    [SerializeField, Range(1, 8)] private int smokeRisePerTick = 4;
    [SerializeField, Range(1, 1000)] private int smokeDecayEveryTicks = 200;
    [SerializeField, Range(0f, 1f)] private float smokeSidewaysChance = 0.35f;

    [Header("Fire")]
    [SerializeField, Range(1, 30)] private int fuelBurnEveryTicks = 4;
    [SerializeField, Range(1, 30)] private int fireDecayEveryTicks = 6;
    [SerializeField, Range(1, 30)] private int fireSpreadEveryTicks = 3;
    [SerializeField, Range(0f, 1f)] private float fireSpreadChance = 0.20f;
    [SerializeField, Range(0f, 1f)] private float upwardSpreadBonus = 0.15f;
    [SerializeField, Range(0, 8)] private int smokeMadeByFire = 2;
    [SerializeField, Range(1, 8)] private int waterUsedToExtinguish = 2;

    [Header("Colors")]
    [SerializeField] private Color shallowWaterColor = new Color(0.15f, 0.48f, 0.85f, 0.45f);
    [SerializeField] private Color deepWaterColor = new Color(0.03f, 0.22f, 0.65f, 0.95f);
    [SerializeField] private Color shallowFuelColor = new Color(0.50f, 0.28f, 0.06f, 0.45f);
    [SerializeField] private Color deepFuelColor = new Color(0.92f, 0.48f, 0.05f, 0.90f);
    [SerializeField] private Color smokeColor = new Color(0.30f, 0.31f, 0.34f, 0.72f);
    [SerializeField] private Color weakFireColor = new Color(0.90f, 0.18f, 0.03f, 1f);
    [SerializeField] private Color mediumFireColor = new Color(1f, 0.48f, 0.03f, 1f);
    [SerializeField] private Color hotFireColor = new Color(1f, 0.95f, 0.45f, 1f);

    [Header("Debug")]
    [SerializeField] private bool startPaused;
    [SerializeField] private bool showSolidCells;
    [SerializeField] private Color solidDebugColor = new Color(0.2f, 0.2f, 0.2f, 0.55f);

    private SpriteRenderer spriteRenderer;
    private Texture2D runtimeTexture;
    private Sprite runtimeSprite;
    private Color32[] pixels;

    // Each location can hold several systems at once.
    private byte[] water;
    private byte[] fuel;
    private byte[] fire;   // 0, 1, 2, or 3.
    private byte[] smoke;  // 0 to smokeCapacity.
    private bool[] solid;

    // Prevents a cell moving more than once per simulation tick.
    private bool[] movedWater;
    private bool[] movedFuel;
    private bool[] movedSmoke;

    // New fire is delayed until the end of the tick so it cannot cross a
    // whole fuel line instantly.
    private byte[] pendingFire;
    private readonly List<int> pendingFireIndices = new List<int>(256);

    private int chunkCountX;
    private int chunkCountY;
    private byte[] chunkAwakeTicks;

    private float accumulator;
    private int tickNumber;
    private bool paused;
    private bool textureNeedsRefresh;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;
    public bool IsPaused => paused;

    private void Start()
    {
        paused = startPaused;
        spriteRenderer = GetComponent<SpriteRenderer>();

        int count = width * height;
        water = new byte[count];
        fuel = new byte[count];
        fire = new byte[count];
        smoke = new byte[count];
        solid = new bool[count];
        movedWater = new bool[count];
        movedFuel = new bool[count];
        movedSmoke = new bool[count];
        pendingFire = new byte[count];
        pixels = new Color32[count];

        chunkCountX = Mathf.CeilToInt(width / (float)chunkSize);
        chunkCountY = Mathf.CeilToInt(height / (float)chunkSize);
        chunkAwakeTicks = new byte[chunkCountX * chunkCountY];

        CreateRuntimeTexture();
        BakeSolidsFromPhysics();
        textureNeedsRefresh = true;
    }

    private void Update()
    {
        if (managedExternally || paused || runtimeTexture == null)
            return;

        float step = 1f / ticksPerSecond;
        accumulator += Time.deltaTime;
        int completed = 0;

        while (accumulator >= step && completed < maximumTicksPerFrame)
        {
            SimulateOneTick();
            accumulator -= step;
            completed++;
        }

        if (completed == maximumTicksPerFrame) accumulator = 0f;
    }

    private void LateUpdate()
    {
        // All changes made this frame are uploaded together.
        if (textureNeedsRefresh) RenderNow();
    }

    private void OnDestroy()
    {
        if (runtimeSprite != null) Destroy(runtimeSprite);
        if (runtimeTexture != null) Destroy(runtimeTexture);
    }

    private void SimulateOneTick()
    {
        AgeChunks();
        WakeChunksNearTarget();
        ClearMovementFlagsInAwakeChunks();

        // Alternate order so water or fuel does not always win ties.
        if ((tickNumber & 1) == 0)
        {
            SimulateLiquid(
                water,
                fuel,
                movedWater,
                waterViscosity,
                waterSpreadSearchDistance,
                waterSidewaysPasses
            );

            SimulateLiquid(
                fuel,
                water,
                movedFuel,
                fuelViscosity,
                fuelSpreadSearchDistance,
                fuelSidewaysPasses
            );
        }
        else
        {
            SimulateLiquid(
                fuel,
                water,
                movedFuel,
                fuelViscosity,
                fuelSpreadSearchDistance,
                fuelSidewaysPasses
            );

            SimulateLiquid(
                water,
                fuel,
                movedWater,
                waterViscosity,
                waterSpreadSearchDistance,
                waterSidewaysPasses
            );
        }
        
        if ((tickNumber & 1) == 0)
        {
            SimulateOilWaterSeparation();
        }

        SimulateSmoke();
        SimulateFire();
        ApplyPendingFire();

        SimulateSmoke();
        SimulateFire();
        ApplyPendingFire();
        tickNumber++;
    }

    // ----- Public methods used by weapons, pickups, drains, and debug tools.

    public int AddWater(Vector2 position, int radius, int amountPerCell) =>
        AddLiquidCircle(water, fuel, position, radius, amountPerCell);

    public int AddFuel(Vector2 position, int radius, int amountPerCell) =>
        AddLiquidCircle(fuel, water, position, radius, amountPerCell);

    public int AddSmoke(Vector2 position, int radius, int amountPerCell)
    {
        if (!TryWorldToCell(position, out Vector2Int center)) return 0;
        int acceptedTotal = 0;
        int radiusSquared = radius * radius;

        for (int y = center.y - radius; y <= center.y + radius; y++)
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            if (!IsInsideCircle(x, y, center, radiusSquared)) continue;
            int i = Index(x, y);
            if (solid[i]) continue;

            int accepted = Mathf.Min(amountPerCell, smokeCapacity - smoke[i]);
            if (accepted <= 0) continue;

            smoke[i] += (byte)accepted;
            acceptedTotal += accepted;
            WakeCell(x, y);
        }

        if (acceptedTotal > 0) textureNeedsRefresh = true;
        return acceptedTotal;
    }

    public int Ignite(Vector2 position, int radius, byte energy = 3)
    {
        if (!TryWorldToCell(position, out Vector2Int center)) return 0;
        energy = (byte)Mathf.Clamp(energy, 1, 3);
        int changed = 0;
        int radiusSquared = radius * radius;

        for (int y = center.y - radius; y <= center.y + radius; y++)
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            if (!IsInsideCircle(x, y, center, radiusSquared)) continue;
            int i = Index(x, y);
            if (solid[i]) continue;

            byte oldValue = fire[i];
            fire[i] = (byte)Mathf.Max(fire[i], energy);
            if (fire[i] == oldValue) continue;

            changed++;
            WakeCell(x, y);
        }

        if (changed > 0) textureNeedsRefresh = true;
        return changed;
    }

    public int ExtractWater(Vector2 position, int radius, int requestedAmount)
    {
        if (requestedAmount <= 0 || !TryWorldToCell(position, out Vector2Int center)) return 0;
        int removedTotal = 0;
        int radiusSquared = radius * radius;

        for (int y = center.y - radius; y <= center.y + radius && removedTotal < requestedAmount; y++)
        for (int x = center.x - radius; x <= center.x + radius && removedTotal < requestedAmount; x++)
        {
            if (!IsInsideCircle(x, y, center, radiusSquared)) continue;
            int i = Index(x, y);
            int removed = Mathf.Min(water[i], requestedAmount - removedTotal);
            if (removed <= 0) continue;

            water[i] -= (byte)removed;
            removedTotal += removed;
            WakeCell(x, y);
        }

        if (removedTotal > 0) textureNeedsRefresh = true;
        return removedTotal;
    }

    public void EraseCircle(Vector2 position, int radius)
    {
        if (!TryWorldToCell(position, out Vector2Int center)) return;
        int radiusSquared = radius * radius;
        bool changed = false;

        for (int y = center.y - radius; y <= center.y + radius; y++)
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            if (!IsInsideCircle(x, y, center, radiusSquared)) continue;
            int i = Index(x, y);
            if (water[i] == 0 && fuel[i] == 0 && fire[i] == 0 && smoke[i] == 0) continue;

            water[i] = fuel[i] = fire[i] = smoke[i] = 0;
            WakeCell(x, y);
            changed = true;
        }

        if (changed) textureNeedsRefresh = true;
    }

    public float GetWaterFraction(Vector2 position)
    {
        if (!TryWorldToCell(position, out Vector2Int cell)) return 0f;
        return water[Index(cell.x, cell.y)] / (float)liquidCapacity;
    }

    public byte GetFireEnergy(Vector2 position)
    {
        if (!TryWorldToCell(position, out Vector2Int cell)) return 0;
        return fire[Index(cell.x, cell.y)];
    }

    public void ClearAllMaterials()
    {
        Array.Clear(water, 0, water.Length);
        Array.Clear(fuel, 0, fuel.Length);
        Array.Clear(fire, 0, fire.Length);
        Array.Clear(smoke, 0, smoke.Length);
        textureNeedsRefresh = true;
    }

    public void TogglePaused()
    {
        paused = !paused;
        accumulator = 0f;
    }

    public void StepOnce() => SimulateOneTick();

    public void RebakeSolidsFromPhysics()
    {
        if (solid == null) return;
        BakeSolidsFromPhysics();
        WakeEveryChunk();
        textureNeedsRefresh = true;
    }

    public int GetActiveChunkCount()
    {
        if (simulateAllChunks) return chunkAwakeTicks.Length;
        int total = 0;
        for (int i = 0; i < chunkAwakeTicks.Length; i++)
            if (chunkAwakeTicks[i] > 0) total++;
        return total;
    }

    public int GetTotalWaterUnits() => Sum(water);
    public int GetTotalFuelUnits() => Sum(fuel);

    private int Sum(byte[] values)
    {
        int total = 0;
        for (int i = 0; i < values.Length; i++) total += values[i];
        return total;
    }

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
