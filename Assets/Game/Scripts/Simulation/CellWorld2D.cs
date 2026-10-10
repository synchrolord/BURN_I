using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
public partial class CellWorld2D : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField, Min(16)] private int width = 240;
    [SerializeField, Min(16)] private int height = 136;
    [SerializeField, Min(0.01f)] private float cellSize = 0.125f;//how the single cell is devided
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

    [Header("Basic Liquids")]
    [FormerlySerializedAs("maximumWaterPerCell")]
    [SerializeField, Range(1, 32)] private int liquidCapacity = 8;
    [Tooltip("Higher viscosity transfers fewer units to each neighboring cell.")]
    [SerializeField, Range(0f, 1f)] private float waterViscosity = 0.05f;
    [SerializeField, Range(0f, 1f)] private float fuelViscosity = 0.55f;
    [Tooltip("Fuel swaps upward with the water immediately above it, every other tick.")]
    [SerializeField, Range(1, 8)] private int oilBuoyancyStrength = 1;
    // Retained for archived Advanced settings. Basic only looks at immediate neighbors.
    [SerializeField, HideInInspector] private int waterSpreadSearchDistance = 8;
    [SerializeField, HideInInspector] private int waterSidewaysPasses = 3;
    [SerializeField, HideInInspector] private int fuelSpreadSearchDistance = 4;
    [SerializeField, HideInInspector] private int fuelSidewaysPasses = 1;

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
    [SerializeField] private bool showInactiveWorldsForDebug;
    public bool IsInitialized => IsReady;
    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;
    public bool IsPaused => paused;
    
    
    
    
    //whether the room manager wants this world updating
    private bool roomActive = true;

    public bool IsRoomActive => roomActive;

    public void SetRoomActive(bool active)
    {
        //avoid resetting the timer when nothing changed
        if (roomActive == active) return;

        if (spriteRenderer != null)
            spriteRenderer.enabled =
                roomActive || showInactiveWorldsForDebug;
        
        //refresh the image when returning to this room
        if (roomActive)
            textureNeedsRefresh = true;
    }
    
    

    

    //create grid arrays and visuals once when play starts
    protected void Start() => InitializeGrid();

    //collect frame time and run the simulation at its own tick speed
    protected void Update()
    {
        if (!roomActive || managedExternally || paused || !IsReady) return;
        float step = 1f / ticksPerSecond;
        accumulator += Time.deltaTime;
        int completed = 0;
        //catch up on waiting ticks, up to the limit for this frame
        while (accumulator >= step && completed < maximumTicksPerFrame)
        {
            SimulateOneTick();
            accumulator -= step;
            completed++;
        }
        // Drop excess catch-up work rather than freezing after a slow frame.
        if (completed == maximumTicksPerFrame) accumulator = 0f;
    }

    //redraw changed cells or refresh visible fire flicker
    protected void LateUpdate()
    {
        if (!roomActive || !IsReady) return;

        if (!textureNeedsRefresh &&
            !(hasVisibleFire && !paused && Time.time >= nextFireVisualUpdate))
            return;

        RenderNow();

        nextFireVisualUpdate =
            Time.time + 1f / Mathf.Max(1f, fireVisualFramesPerSecond);
    }

    //destroy the sprite and texture created for this grid
    protected void OnDestroy()
    {
        if (runtimeSprite != null) Destroy(runtimeSprite);
        if (runtimeTexture != null) Destroy(runtimeTexture);
    }

    //update all active cells by one simulation step
    private void SimulateOneTick()
    {
        if (!roomActive) return;
        
        //advance plant timers using simulation time
        plantSimulationTime += 1.0 / ticksPerSecond;
        AgeChunks();
        WakeChunksNearTarget();
        ClearMovementFlagsInAwakeChunks();
        //alternate liquid priority and horizontal scan direction every tick
        bool waterFirst = (tickNumber & 1) == 0;

        // Visit only awake chunks. Each cell follows the same short set of rules.
        // Movement flags stop incoming liquid/smoke traveling again in this tick.
        for (int cy = 0; cy < chunkCountY; cy++)
        for (int column = 0; column < chunkCountX; column++)
        {
            int cx = waterFirst ? column : chunkCountX - 1 - column;
            if (!IsChunkAwake(cx, cy)) continue;
            GetChunkBounds(cx, cy, out int minX, out int maxX, out int minY, out int maxY);
            for (int y = minY; y <= maxY; y++)
            for (int offset = 0; offset <= maxX - minX; offset++)
            {
                int x = waterFirst ? minX + offset : maxX - offset;
                if (!ContainsCell(x, y)) continue;
                //swap fuel above water every other tick
                if (waterFirst) SeparateOilAt(x, y);
                if (waterFirst)
                {
                    SimulateLiquidAt(water, fuel, movedWater, x, y, waterViscosity);
                    SimulateLiquidAt(fuel, water, movedFuel, x, y, fuelViscosity);
                }
                else
                {
                    SimulateLiquidAt(fuel, water, movedFuel, x, y, fuelViscosity);
                    SimulateLiquidAt(water, fuel, movedWater, x, y, waterViscosity);
                }
                //process smoke and fire after moving the liquids
                SimulateSmokeAt(x, y);
                SimulateFireAt(x, y);
            }
        }
        // New neighbors ignite next tick, so fire cannot cross a whole vine instantly.
        ApplyPendingFire();
        tickNumber++;
    }
}
