using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class LiquidValve2D : MonoBehaviour
{
    private enum ValveType
    {
        Source,
        Water
    }

    [SerializeField] private ValveType valveType;

    [Header("Source Valve")]
    [SerializeField] private LiquidSource2D source;

    [Header("Water Valve")]
    [SerializeField] private CellWorld2D world;
    [SerializeField] private Transform sink;
    [SerializeField, Range(0, 4)] private int radius = 1;
    [SerializeField, Min(1)] private int amount = 4;

    private PlayerLiquidInteraction2D player;

    public bool HoldToUse => valveType == ValveType.Water;


    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }


    //capture player to fill pressure tool later
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (player != null) return;

        player = other.GetComponentInParent<PlayerLiquidInteraction2D>();

        if (player != null) player.SetValve(this);
    }


    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerLiquidInteraction2D leavingPlayer = 
            other.GetComponentInParent<PlayerLiquidInteraction2D>();

        if (leavingPlayer != player) return;

        player.ClearValve(this);
        player = null;
    }


    public void Use(PressureTool2D pressureTool, CellWorld2D currentWorld)
    {
        if (valveType == ValveType.Source)
        {
            if (source != null)
                source.Toggle();

            return;
        }

        //set current cellWorld
        CellWorld2D usedWorld = world != null ? world : currentWorld;

        if (usedWorld == null || pressureTool == null) { return; }

        //see space left in player tank
        int needed = pressureTool.MaximumWater - pressureTool.CurrentWater;
        if (needed <= 0) return;
        
        //sing position with fallback at player position
        Vector2 position =
            sink != null
                ? sink.position
                : transform.position;

        int requested = Mathf.Min(amount, needed);

//drain extra water while keeping the normal refill amount
        int removed = usedWorld.ExtractWater(
            position,
            radius,
            requested * 4);

        pressureTool.AddWaterResource(
            Mathf.Min(removed, requested));
    }
}