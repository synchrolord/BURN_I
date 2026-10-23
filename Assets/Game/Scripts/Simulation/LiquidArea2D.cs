using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class LiquidArea2D : MonoBehaviour
{
    private enum LiquidType
    {
        Water,
        Fuel,
        Acid,
        Fire
    }

    [SerializeField] private CellWorld2D world;
    [SerializeField] private LiquidType liquidType;

    //amount placed in every CellWorld cell
    [SerializeField, Range(1, 32)] private int amount = 8;
    private BoxCollider2D area;


    private void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        area.isTrigger = true;
    }


    private IEnumerator Start()
    {
        if (world == null) world = GetComponentInParent<CellWorld2D>();
        if (world == null) world = FindFirstObjectByType<CellWorld2D>();

        if (world == null)
        {
            Debug.LogWarning("Liquid Area needs a CellWorld.", this);
            yield break;
        }
        

        //wait for CellWorld to create its arrays
        while (!world.IsInitialized && !world.InitializationFailed) { yield return null; }

        if (!world.IsInitialized)
            yield break;

        FillArea();

        area.enabled = false;
    }


    private void FillArea()
    {
        //fill every cell that overlaps the object
        for (int y = 0; y < world.Height; y++)
        for (int x = 0; x < world.Width; x++)
        {
            Vector2 position = world.CellToWorld(x, y);
            if (!area.OverlapPoint(position)) continue;

            AddLiquid(position);
        }
    }


    //add desired liquid
    private void AddLiquid(Vector2 position)
    {
        switch (liquidType)
        {
            case LiquidType.Water:
                world.AddWater(position, 0, amount);
                break;

            case LiquidType.Fuel:
                world.AddFuel(position, 0, amount);
                break;

            case LiquidType.Acid:
                world.AddAcid(position, 0, amount);
                break;
            
            case LiquidType.Fire:
                world.Ignite(position, 0, 3);
                break;
            
        }
    }
}