using UnityEngine;
[RequireComponent(typeof(PlayerVitals2D))]


public class PlayerLiquidInteraction2D : MonoBehaviour
{
    [SerializeField] private CellWorld2D cellWorld;
    [SerializeField] private Transform samplePoint;

    [Header("Sampling")]
    [SerializeField, Min(0.02f)] private float sampleInterval = 0.1f;
    [SerializeField, Range(0, 4)] private int sampleRadius = 1;
    [SerializeField, Range(1, 12)] private int liquidUnitsPerSample = 1;
    
    
    [Header("Acid & Water")]
    [SerializeField, Min(0.1f)] private float poisonPerAcidUnit = 1f;
    [SerializeField, Min(0.1f)] private float cleansePerWaterUnit = 3f;

    
    
    
    private PlayerVitals2D vitals;
    private float sampleTimer;

    private void Awake()
    {
        vitals = GetComponent<PlayerVitals2D>();
    }
    
    private void Update()
    {
        
        if (vitals.IsDead || cellWorld == null)
            return;

        sampleTimer -= Time.deltaTime;

        if (sampleTimer > 0f)
            return;

        sampleTimer = sampleInterval;
        
        
        //PLayer location and fallBack for sample point
        Vector2 position = samplePoint != null
            ? samplePoint.position
            : transform.position;

        
        

        AbsorbAcid(position);
        AbsorbWater(position);
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

    public void SetWorld(CellWorld2D world)
    {
        cellWorld = world;
    }


    public void AbsorbWater(Vector2 position)
    {
        if (!vitals.IsPoisoned)return;
        
        int absorbedUnits = cellWorld.ExtractWater(
            position,
            sampleRadius,
            liquidUnitsPerSample);

        if (absorbedUnits > 0)
        {
            vitals.CleansePoison(absorbedUnits);
        }


    }
    
}
