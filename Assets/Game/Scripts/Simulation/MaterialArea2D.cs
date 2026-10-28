using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class MaterialArea2D : MonoBehaviour

//stop the player using water inside this area

{
    private enum CellMaterial
    {
        Water,
        Fuel,
        Acid,
        Fire,
        Smoke,
        Steam,
        Plant
    }

    private enum AreaShape
    {
        Box,
        HollowBox,
        Circle
    }

    [SerializeField] private CellWorld2D world;
    [FormerlySerializedAs("liquidType")]
    [SerializeField] private CellMaterial material;
    [SerializeField] private AreaShape shape;

    //used by liquids, gases and fire
    [SerializeField, Range(1, 32)] private int amount = 8;

    //color used by visible plant cells
    [SerializeField] private Color plantColor = new Color(0.35f, 0.18f, 0.06f, 1f);

    //space between the outside and inside of a hollow box
    [SerializeField, Min(0.01f)] private float wallThickness = 0.1f;
    
    private BoxCollider2D area;
    [SerializeField] private bool blockPlayerWater = true;

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
            Debug.LogWarning("Material Area needs a CellWorld.", this);
            yield break;
        }

        //wait for CellWorld to create its arrays
        while (!world.IsInitialized && !world.InitializationFailed)
            yield return null;

        if (!world.IsInitialized)
            yield break;

        FillArea();
        area.enabled = false;
    }


    private void FillArea()
    {
        //fill every CellWorld cell inside the selected shape
        for (int y = 0; y < world.Height; y++)
        for (int x = 0; x < world.Width; x++)
        {
            Vector2 position = world.CellToWorld(x, y);
            if (!ContainsPoint(position)) continue;

            AddMaterial(position);
        }
    }


    private bool ContainsPoint(Vector2 position)
    {
        //convert the point into the collider's local rectangle
        Vector2 point = transform.InverseTransformPoint(position);
        point -= area.offset;

        Vector2 half = area.size * 0.5f;

        if (Mathf.Abs(point.x) > half.x ||
            Mathf.Abs(point.y) > half.y)
        {
            return false;
        }

        if (shape == AreaShape.Box)
            return true;

        if (shape == AreaShape.Circle)
        {
            if (half.x <= 0f || half.y <= 0f)
                return false;

            //a square makes a circle and a rectangle makes an oval
            Vector2 circlePoint = new Vector2(
                point.x / half.x,
                point.y / half.y);

            return circlePoint.sqrMagnitude <= 1f;
        }

        Vector2 inner = half - Vector2.one * wallThickness;

        //thick walls can close the hollow center completely
        if (inner.x <= 0f || inner.y <= 0f)
            return true;

        return Mathf.Abs(point.x) >= inner.x ||
               Mathf.Abs(point.y) >= inner.y;
    }


    //add the selected material to one CellWorld cell
    private void AddMaterial(Vector2 position)
    {
        switch (material)
        {
            case CellMaterial.Water:
                world.AddWater(position, 0, amount);
                break;

            case CellMaterial.Fuel:
                world.AddFuel(position, 0, amount);
                break;

            case CellMaterial.Acid:
                world.AddAcid(position, 0, amount);
                break;

            case CellMaterial.Fire:
                world.Ignite(
                    position,
                    0,
                    (byte)Mathf.Clamp(amount, 1, 3));
                break;

            case CellMaterial.Smoke:
                world.AddSmoke(position, 0, amount);
                break;

            case CellMaterial.Steam:
                world.AddSteam(position, 0, amount);
                break;

            case CellMaterial.Plant:
                world.AddPlant(position, plantColor);
                break;
        }
    }
    
    
    //get player overlaps with liquid area  
    public bool BlocksPlayerWater(Vector2 position)
    {
        return blockPlayerWater &&
               material == CellMaterial.Water &&
               ContainsPoint(position);
    }
}
