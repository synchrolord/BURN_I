using UnityEngine;

[RequireComponent(typeof(PlayerVitals2D))]
public class PlayerLiquidInteraction2D : MonoBehaviour
{
    [SerializeField] private CellWorld2D cellWorld;
    [SerializeField] private Transform samplePoint;

    [Header("Sampling")]
    [SerializeField, Min(0.02f)]
    private float sampleInterval = 0.1f;

    [SerializeField, Range(0, 4)]
    private int sampleRadius = 1;

    [Header("Acid")]
    [SerializeField, Range(1, 8)]
    private int acidUnitsPerSample = 1;

    [SerializeField, Min(0.1f)]
    private float poisonPerAcidUnit = 2f;

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

        AbsorbAcid();
    }
    private void AbsorbAcid()
    {
        // A saturated player cannot absorb more acid.
        if (vitals.CurrentPoison >= vitals.MaxPoison)
            return;

        Vector2 position = samplePoint != null
            ? samplePoint.position
            : transform.position;

        int absorbedUnits = cellWorld.ExtractAcid(
            position,
            sampleRadius,
            acidUnitsPerSample);

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
}
