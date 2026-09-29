using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Shared artwork/settings. Each painted instance keeps its own damage in BurnablePlantMap2D.</summary>
[CreateAssetMenu(fileName = "Plant", menuName = "BURN/Burnable Plant Tile")]
public sealed class BurnablePlantTile : TileBase
{
    
    [Tooltip("Healthy, lightly burned, half burned, heavily burned, last few pixels. Keep identical canvas/pivot/PPU.")]
    [SerializeField] private Sprite[] burnStages = new Sprite[5];
    [Tooltip("Seconds of fire needed to consume each occupied simulation cell. A long vine takes longer as fire travels.")]
    [SerializeField, Min(0.25f)] private float burnSeconds = 6f;
    [Tooltip("How long water prevents the plant fuel from reigniting, in simulation seconds.")]
    [SerializeField, Min(0f)] private float wetSeconds = 2f;
    [SerializeField] private bool disappearWhenBurned = true;
    [Tooltip("Requires a Tilemap Collider 2D on the plant map. Sprite keeps the HEALTHY sprite's physics shape until fully burned; Grid keeps the cell outline. None is decoration only.")]
    [SerializeField] private Tile.ColliderType collisionType = Tile.ColliderType.Sprite;
    //saved sample positions from the healthy artwork, used to register plant fuel
    [SerializeField, HideInInspector] private Vector2[] fuelSamples = new Vector2[0];
    [SerializeField, HideInInspector] private Sprite bakedSprite;
    [SerializeField, HideInInspector] private string bakedSourceHash;

    //let other classes read the plant settings
    public float BurnSeconds => Mathf.Max(0.25f, burnSeconds);
    public float WetSeconds => Mathf.Max(0f, wetSeconds);
    public bool DisappearWhenBurned => disappearWhenBurned;
    public Tile.ColliderType CollisionType => collisionType;
    public Vector2[] FuelSamples => fuelSamples;
    //check that samples exist and still refer to the current healthy sprite
    public bool HasBakedMask => bakedSprite != null && bakedSprite == GetStageSprite(0) && fuelSamples.Length > 0;
    public string BakedSourceHash => bakedSourceHash;

    //get the requested burn stage, falling back to an earlier assigned sprite
    public Sprite GetStageSprite(int stage)
    {
        if (burnStages == null || burnStages.Length == 0) return null;
        // A healthy-only prototype remains usable while the artist prepares its burned variants.
        for (int i = Mathf.Min(stage, burnStages.Length - 1); i >= 0; i--)
            if (burnStages[i] != null) return burnStages[i];
        return null;
    }

    //check that all five burn-stage pictures are assigned
    public bool HasAllStages
    {
        get
        {
            if (burnStages == null || burnStages.Length != 5) return false;
            foreach (Sprite sprite in burnStages) if (sprite == null) return false;
            return true;
        }
    }

    //give a newly drawn tile its healthy picture and collision settings
    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = GetStageSprite(0);
        tileData.color = Color.white;
        tileData.transform = Matrix4x4.identity;
        tileData.flags = TileFlags.None;
        tileData.colliderType = collisionType;
    }

#if UNITY_EDITOR
    // Editor baker owns these writes; runtime never needs readable source textures.
    public void SetArtwork(Sprite[] sprites) { burnStages = sprites; }
    //save the prepared fuel samples and which artwork they came from
    public void SetBakedMask(Vector2[] samples, string sourceHash)
    {
        fuelSamples = samples;
        bakedSprite = GetStageSprite(0);
        bakedSourceHash = sourceHash;
    }
#endif
}
