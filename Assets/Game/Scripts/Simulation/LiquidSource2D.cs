using UnityEngine;

public sealed class LiquidSource2D : MonoBehaviour
{
    private enum LiquidType
    {
        Water,
        Fuel,
        Acid
    }

    [SerializeField] private CellWorld2D world;
    [SerializeField] private LiquidType liquidType;
    
    [SerializeField, Min(1)] private int totalAmount =10;
    [SerializeField, Min(1)] private int dopSize = 1;
    [SerializeField, Min(0)] private int radius = 1;
    
    [SerializeField, Min(0.01f)] private float secondsBetweenDrops = 0.01f;
    [SerializeField] private bool running = true;
    private float timer;
    
    
    private void Awake()
    {
        if (world == null) world = GetComponentInParent<CellWorld2D>();
    }

    
    private void Update()
    {
        if (!running || world == null || !world.IsInitialized) { return; }
        
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = secondsBetweenDrops;
        
        AddLiquid();
        
        totalAmount-=dopSize;
        if (totalAmount <= 0) gameObject.SetActive(false);
    }
    
    private void AddLiquid()
    {
        switch (liquidType)
        {
            case LiquidType.Water:
                world.AddWater(transform.position, radius, dopSize);
                break;

            case LiquidType.Fuel:
                world.AddFuel(transform.position, radius, dopSize);
                break;

            case LiquidType.Acid:
                world.AddAcid(transform.position, radius, dopSize);
                break;
        }
    }
    
    public void SetRunning(bool value)
    {
        running = value;
    }
    
}
