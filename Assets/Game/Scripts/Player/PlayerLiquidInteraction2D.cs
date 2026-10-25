using UnityEngine;
using UnityEngine.InputSystem;
[RequireComponent(typeof(PlayerVitals2D))]


public class PlayerLiquidInteraction2D : MonoBehaviour
{
    [SerializeField] private CellWorld2D cellWorld;
    [SerializeField] private Transform samplePoint;

    [Header("Sampling")]
    [SerializeField, Min(0.02f)] private float sampleInterval = 0.1f;
    [SerializeField, Range(0, 4)] private int sampleRadius = 1;
    [SerializeField, Range(1, 24)] private int liquidUnitsPerSample = 1;
    
    
    [Header("Acid & Water")]
    [SerializeField, Min(0.1f)] private float poisonPerAcidUnit = 1f;
    [SerializeField, Min(0.1f)] private float cleansePerWaterUnit = 3f;

    
    [Header("Swimming")]
    [SerializeField] private Transform swimPoint;
    [SerializeField, Range(0f, 1f)] private float swimWaterNeeded = 0.5f;

    public bool IsSwimming { get; private set; }
    
    
    private PlayerVitals2D vitals;
    private float sampleTimer;
    
    private PressureTool2D pressureTool;
    private LiquidValve2D valve;
    private InputAction interactAction;    
    
    private void Awake()
    {
        vitals = GetComponent<PlayerVitals2D>();
        pressureTool = GetComponent<PressureTool2D>();
        interactAction =
            GetComponent<PlayerInput>().actions.FindAction("Interact");
    }
    
    
    private void Update()
    {

        if (vitals.IsDead || cellWorld == null) { IsSwimming = false; return; }
        
        
        //source valves toggle once when F is pressed
        if (interactAction != null &&
            interactAction.WasPressedThisFrame() &&
            valve != null &&
            !valve.HoldToUse)
        {
            valve.Use(pressureTool, cellWorld);
        }
        
        
        
        
        
        
        
        //fallback to player transform
        Vector2 swimPosition = swimPoint != null
            ? swimPoint.position
            : transform.position;

        IsSwimming =
            cellWorld.GetWaterFraction(swimPosition) >= swimWaterNeeded;

        sampleTimer -= Time.deltaTime;

        if (sampleTimer > 0f)
            return;

        sampleTimer = sampleInterval;
        
        
        //PLayer location and fallBack for sample point
        Vector2 position = samplePoint != null
            ? samplePoint.position
            : transform.position;

        
        

        AbsorbAcid(position);
        TouchFireAndWater(position);
        AbsorbWater(position);
        UseWater(position);
    }
    
    
    private void AbsorbAcid(Vector2 position)
    {
        // A saturated player cannot absorb more acid.
        if (vitals.IsPoisonSaturated)
            return;
            
        

        int absorbedUnits = cellWorld.ExtractAcid(
            position,
            sampleRadius,
            liquidUnitsPerSample);

        if (absorbedUnits > 0)
        {
            vitals.AddPoison(
                absorbedUnits * poisonPerAcidUnit);
        }
    }
    public void AbsorbWater(Vector2 position)
    {
        if (!vitals.HasPoison)
            return;
        
        int absorbedUnits = cellWorld.ExtractWater(
            position,
            sampleRadius,
            liquidUnitsPerSample);

        vitals.CleansePoison(
            absorbedUnits * cleansePerWaterUnit);


    }
    
    
    public void SetWorld(CellWorld2D world)
    {
        cellWorld = world;
    }


    
    
    private void TouchFireAndWater(Vector2 position)
    {
        bool touchingFire = false;

        //scan the cells in a circle around the player
        float cellSize = cellWorld.CellSize;
        int radius = sampleRadius;

        float worldRadius = radius * cellSize;
        float radiusSquared = worldRadius * worldRadius;

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                Vector2 offset = new Vector2(x, y) * cellSize;

                //skip the corners outside the circle
                if (offset.sqrMagnitude > radiusSquared)
                {
                    continue;
                }

                Vector2 samplePosition = position + offset;

                //water wins if fire and water are both touching the player
                if (cellWorld.GetWaterFraction(samplePosition) > 0f)
                {
                    vitals.SetWet();
                    return;
                }

                if (cellWorld.GetFireEnergy(samplePosition) > 0)
                {
                    touchingFire = true;
                }
            }
        }

        if (touchingFire)
        {
            vitals.Ignite();
        }
    }
    
    
    



    private void UseWater(Vector2 position)
    {
        if (interactAction == null ||
            !interactAction.IsPressed() ||
            pressureTool == null ||
            cellWorld == null)
        {
            return;
        }

        
        //drain water from sink when button is pressed
        if (valve != null)
        {
            
            if (valve.HoldToUse)
                valve.Use(pressureTool, cellWorld);

            return;
        }

        //drain water from environment
        int needed =
            pressureTool.MaximumWater -
            pressureTool.CurrentWater;

        if (needed <= 0) return;

        int removed = cellWorld.ExtractWater(
            position,
            sampleRadius,
            Mathf.Min(liquidUnitsPerSample, needed));

        pressureTool.AddWaterResource(removed);
    }


    public void SetValve(LiquidValve2D newValve)
    {
        valve = newValve;
    }


    public void ClearValve(LiquidValve2D oldValve)
    {
        if (valve == oldValve)
            valve = null;
    }
    
}
