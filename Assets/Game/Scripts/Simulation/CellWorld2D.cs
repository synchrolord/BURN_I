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

    [Header("Chunks")]
    [SerializeField, Range(8, 64)] private int chunkSize = 32;
    [SerializeField, Range(0, 4)] private int activeChunkRadius = 1;
    [SerializeField, Range(1, 200)] private int chunkStayAwakeTicks = 40;

    [Header("Basic Liquids")]
    [FormerlySerializedAs("maximumWaterPerCell")]
    [SerializeField, Range(1, 32)] private int liquidCapacity = 8;
    [Tooltip("Higher viscosity transfers fewer units to each neighboring cell.")]
    [SerializeField, Range(0f, 1f)] private float waterViscosity = 0.05f;
    [SerializeField, Range(0f, 1f)] private float fuelViscosity = 0.55f;
    [SerializeField, Range(0f, 1f)] private float acidViscosity = 0.15f;
    
    
    [Tooltip("Fuel swaps upward with the water immediately above it, every other tick.")]
    [SerializeField, Range(1, 8)] private int oilBuoyancyStrength = 1;
    // Retained for archived Advanced settings. Basic only looks at immediate neighbors.
    [SerializeField, HideInInspector] private int waterSpreadSearchDistance = 8;
    [SerializeField, HideInInspector] private int waterSidewaysPasses = 3;
    [SerializeField, HideInInspector] private int fuelSpreadSearchDistance = 4;
    [SerializeField, HideInInspector] private int fuelSidewaysPasses = 1;

    
    [Header("Gases")]
    [FormerlySerializedAs("smokeCapacity")]
    [SerializeField, Range(1, 16)] private int gasCapacity = 8;
    [SerializeField, Range(1, 8)] private int gasSpreadDistance = 4;

    
    [Header("Gas Momentum")]
    [SerializeField, Min(0f)] private float gasBuoyancy = 0.9f;
    [SerializeField, Range(0f, 0.95f)] private float gasVelocityDrag = 0.12f;
    [SerializeField, Range(1f, 4f)] private float gasMaximumSpeedInCells = 3f;
    
    

    [Header("Smoke")]
    [SerializeField, Range(1, 8)] private int smokeRisePerTick = 4;
    [SerializeField, Range(1, 1000)] private int smokeDecayEveryTicks = 200;
    [SerializeField, Range(0f, 1f)] private float smokeSidewaysChance = 0.35f;
    
    
    
    
    
    
    [Header("Steam")]
    [SerializeField, Range(1, 8)] private int steamRisePerTick = 6;
    [SerializeField, Range(1, 1000)] private int steamDecayEveryTicks = 100;
    [SerializeField, Range(0f, 1f)] private float steamSidewaysChance = 0.55f;
    
    [Header("Fire")]
    [SerializeField, Range(1, 30)] private int fuelBurnEveryTicks = 4;
    [SerializeField, Range(1, 30)] private int fireDecayEveryTicks = 6;
    [SerializeField, Range(1, 30)] private int fireSpreadEveryTicks = 3;
    [SerializeField, Range(0f, 1f)] private float fireSpreadChance = 0.20f;
    [SerializeField, Range(0f, 1f)] private float upwardSpreadBonus = 0.15f;
    [SerializeField, Range(0, 8)] private int smokeMadeByFire = 2;
    [SerializeField, Range(1, 8)] private int waterUsedToExtinguish = 2;

    [Header("Plants")]
    [Tooltip("Ticks between each of the five visible burn stages.")]
    [SerializeField, Range(1, 60)] private int plantBurnEveryTicks = 20;
    [Tooltip("Ticks between attempts to spread fire from a burning plant cell.")]
    [SerializeField, Range(1, 60)] private int plantSpreadEveryTicks = 10;
    [SerializeField, Range(0f, 1f)] private float plantSpreadChance = 0.12f;
    [SerializeField, Range(0f, 1f)] private float plantUpwardSpreadBonus = 0.08f;
    [SerializeField] private Color burnedPlantColor = new Color(0.18f, 0.09f, 0.03f, 1f);

    [Header("Colors")]
    [SerializeField] private Color shallowWaterColor = new Color(0.15f, 0.48f, 0.85f, 0.45f);
    [SerializeField] private Color deepWaterColor = new Color(0.03f, 0.22f, 0.65f, 0.95f);
    [SerializeField] private Color shallowFuelColor = new Color(0.50f, 0.28f, 0.06f, 0.45f);
    [SerializeField] private Color deepFuelColor = new Color(0.92f, 0.48f, 0.05f, 0.90f);
    [SerializeField] private Color shallowAcidColor = new Color(0.35f, 0.85f, 0.18f, 0.50f);
    [SerializeField] private Color deepAcidColor = new Color(0.55f, 1f, 0.15f, 0.95f);
    [SerializeField] private Color smokeColor = new Color(0.30f, 0.31f, 0.34f, 0.72f);
    [SerializeField] private Color weakFireColor = new Color(0.90f, 0.18f, 0.03f, 1f);
    [SerializeField] private Color mediumFireColor = new Color(1f, 0.48f, 0.03f, 1f);
    [SerializeField] private Color hotFireColor = new Color(1f, 0.95f, 0.45f, 1f);
    [SerializeField] private Color steamColor = new Color(0.75f, 0.85f, 0.92f, 0.60f);
    
    
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
        
        AgeChunks();
        WakeChunksNearTarget();
        ClearMovementFlags();
        
        //change scan direction every tick
        bool scanForward = (tickNumber & 1) == 0;

        
        //loop over chunks
        for (int chunkY = 0; chunkY < chunkCountY; chunkY++)
        for (int column = 0; column < chunkCountX; column++)
        {
            
            //change scan order, left to right when treu
            int chunkX = scanForward ? column : chunkCountX - 1 - column;
            if (!IsChunkAwake(chunkX, chunkY)) continue;
            
            GetChunkBounds(chunkX, chunkY,
                out int minCellX, out int maxCellX,
                out int minCellY, out int maxY);
            
            //loop over one chunks cells
            for (int cellY = minCellY; cellY <= maxY; cellY++)
            for (int offset = 0; offset <= maxCellX - minCellX; offset++)
            {
                int cellX = scanForward ? minCellX + offset : maxCellX - offset;
                if (!ContainsCell(cellX, cellY))
                    continue;

                SeparateOilAt(cellX, cellY);
                SimulateLiquidsAt(cellX,cellY);
                //process smoke and fire after moving the liquids
                SimulateGasesAt(cellX, cellY);
                SimulateFireAt(cellX, cellY);
                SimulatePlantAt(cellX, cellY);
            }
        }
        // New neighbors ignite next tick, so fire cannot cross a whole vine instantly.
        ApplyPendingFire();
        tickNumber++;
    }
    
    //simulate all three liquids, alternating to avoid bias
    private void SimulateLiquidsAt(int cellX, int cellY)
    {
        switch (tickNumber % 3)
        {
            case 0:
                SimulateLiquidAt(water, movedWater, cellX, cellY, waterViscosity);
                SimulateLiquidAt(fuel, movedFuel, cellX, cellY, fuelViscosity);
                SimulateLiquidAt(acid, movedAcid, cellX, cellY, acidViscosity);
                break;

            case 1:
                SimulateLiquidAt(fuel, movedFuel, cellX,cellY, fuelViscosity);
                SimulateLiquidAt(acid, movedAcid, cellX, cellY, acidViscosity);
                SimulateLiquidAt(water, movedWater, cellX, cellY, waterViscosity);
                break;

            default:
                SimulateLiquidAt(acid, movedAcid, cellX, cellY, acidViscosity);
                SimulateLiquidAt(water, movedWater, cellX, cellY, waterViscosity);
                SimulateLiquidAt(fuel, movedFuel, cellX, cellY, fuelViscosity);
                break;
        }
    }
    
    
   //simulate gasses like how water is handled
    private void SimulateGasesAt(int cellX, int cellY)
    {
        if ((tickNumber & 1) == 0)
        {
            SimulateSmokeAt(cellX, cellY);
            SimulateSteamAt(cellX, cellY);
        }
        else
        {
            SimulateSteamAt(cellX, cellY);
            SimulateSmokeAt(cellX, cellY);
        }
    }
    
}
