using System.Collections.Generic;
using UnityEngine;

public partial class CellWorld2D
{
    
    //initialize all possible materials arrays in a cell
    private byte[] water, fuel, acid, smoke, steam, fire;
    private byte[] light, placedLight;
        /*
            Single cell can store an amount of each material
            cell #25
            water[25] = 5;
            fuel[25] = 2;
            smoke[25] = 0;
            fire[25] = 0;
         */
    
    //cell is blocked, no material allowed tom move here
    private bool[] solid;
    
    //flags to mark materials that have already moved this simulation step
    private bool[] movedWater, movedFuel, movedAcid;
    private bool[] movedSmoke, movedSteam,movedFire;
    private Vector2[] smokeVelocity,steamVelocity;
    
    //fire waiting to be applied, avoid instantaneous burning.
    private byte[] pendingFire;
    private readonly List<int> pendingFireIndices = new List<int>();
    
    //allow chunk to go to sleep if not updated
    private byte[] chunkAwakeTicks;
    private int chunkCountX, chunkCountY, tickNumber;
    
   
    private float accumulator;
    private bool paused, managedExternally, textureNeedsRefresh;
    private SpriteRenderer spriteRenderer;
    private Texture2D runtimeTexture;
    private Sprite runtimeSprite;
    private Color32[] pixels;

    internal bool IsReady =>

        water != null && fuel != null && acid != null &&
        smoke != null && steam != null && fire != null &&
        plant != null && plantColor != null && visiblePlant != null &&
        solid != null && movedFire != null &&
        smokeVelocity != null && steamVelocity != null &&
        light != null && placedLight != null;

    
    
    private void InitializeGrid()
    {
        if (!InitializeRoomShape()) return;
        
        //tickSpeed
        ticksPerSecond = Mathf.Clamp(ticksPerSecond, 1f, 60f);
        maximumTicksPerFrame = Mathf.Clamp(maximumTicksPerFrame, 1, 8);
        
        //Chunks
        chunkSize = Mathf.Clamp(chunkSize, 8, 64);
        chunkStayAwakeTicks = Mathf.Clamp(chunkStayAwakeTicks, 2, 200);
        
        //physical capacity
        liquidCapacity = Mathf.Clamp(liquidCapacity, 1, 32);
        gasCapacity = Mathf.Clamp(gasCapacity, 1, 16);        
        //Fire updates every N ticks
        fuelBurnEveryTicks = Mathf.Max(1, fuelBurnEveryTicks);
        fireDecayEveryTicks = Mathf.Max(1, fireDecayEveryTicks);
        fireSpreadEveryTicks = Mathf.Max(1, fireSpreadEveryTicks);
        plantBurnEveryTicks = Mathf.Max(1, plantBurnEveryTicks);
        plantSpreadEveryTicks = Mathf.Max(1, plantSpreadEveryTicks);
        firePuffEveryTicks = Mathf.Max(1, firePuffEveryTicks);
        //initialize cellWorld properties
        paused = startPaused;
        spriteRenderer = GetComponent<SpriteRenderer>();
        int count = width * height;
        
        //fill arrays with 0s
        solid = new bool[count];
        pixels = new Color32[count];

        water = new byte[count];
        acid = new byte[count];
        fuel = new byte[count];
        
        
        smoke = new byte[count];
        steam = new byte[count];
        fire = new byte[count];
        light = new byte[count];
        placedLight = new byte[count];
        plant = new byte[count];
        plantColor = new Color32[count];
        visiblePlant = new bool[count];
        
        smokeVelocity = new Vector2[count];
        steamVelocity = new Vector2[count];
        
        movedWater = new bool[count];
        movedAcid = new bool[count];
        movedFuel = new bool[count];
        
        
        movedSmoke = new bool[count];
        movedSteam = new bool[count];
        movedFire = new bool[count];
        pendingFire = new byte[count];
        
        
        //how many chunks to cove Grid
        chunkCountX = Mathf.CeilToInt(width / (float)chunkSize);
        chunkCountY = Mathf.CeilToInt(height / (float)chunkSize);
        chunkAwakeTicks = new byte[chunkCountX * chunkCountY];
        
       
        BuildRoomChunks();
        CreateRuntimeTexture();
        BakeSolidsFromPhysics();
        CreateDarknessTexture();
        RefreshPixelLight();
        textureNeedsRefresh = true;
        
        RenderNow();
    }

    
    
    // Wold-Grid connection
    public bool TryWorldToCell(Vector2 worldPosition, out Vector2Int cell)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        cell = new Vector2Int(
            Mathf.FloorToInt(local.x / cellSize),
            Mathf.FloorToInt(local.y / cellSize));
        
        return ContainsCell(cell.x, cell.y);
    }
    public Vector2 CellToWorld(int cellx, int cellY) =>
        transform.TransformPoint(
            new Vector3(
                (cellx + 0.5f) * cellSize,
                (cellY + 0.5f) * cellSize));

    //transform cell coordinates into the index used by every material array
    private int Index(int cellX, int cellY) => cellX + cellY * width;
    private bool InBounds(int cellX, int cellY) => cellX >= 0 && cellX < width && cellY >= 0 && cellY < height;

    //clear every material stored in one cell
    private void ClearCell(int index, bool clearFire = true)
    {
        water[index] = 0;
        fuel[index] = 0;
        acid[index] = 0;
        smoke[index] = 0;
        steam[index] = 0;
        smokeVelocity[index] = Vector2.zero;
        steamVelocity[index] = Vector2.zero;
        pendingFire[index] = 0;

        if (clearFire)
            fire[index] = 0;
    }
    
    //allow dynamic effects to effect sounding cells in a radius
    private bool IsInsideCircle(int x, int y, Vector2Int center, int radiusSquared)
    {
        int dx = x - center.x, dy = y - center.y;
        return ContainsCell(x, y) && dx * dx + dy * dy <= radiusSquared;
    }

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
