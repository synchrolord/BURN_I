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
    [SerializeField, HideInInspector] private Vector2[] fuelSamples = new Vector2[0];
    [SerializeField, HideInInspector] private Sprite bakedSprite;
    [SerializeField, HideInInspector] private string bakedSourceHash;

    public float BurnSeconds => Mathf.Max(0.25f, burnSeconds);
    public float WetSeconds => Mathf.Max(0f, wetSeconds);
    public bool DisappearWhenBurned => disappearWhenBurned;
    public Vector2[] FuelSamples => fuelSamples;
    public bool HasBakedMask => bakedSprite != null && bakedSprite == GetStageSprite(0) && fuelSamples.Length > 0;
    public string BakedSourceHash => bakedSourceHash;

    public Sprite GetStageSprite(int stage)
    {
        if (burnStages == null || burnStages.Length == 0) return null;
        // A healthy-only prototype remains usable while the artist prepares its burned variants.
        for (int i = Mathf.Min(stage, burnStages.Length - 1); i >= 0; i--)
            if (burnStages[i] != null) return burnStages[i];
        return null;
    }

    public bool HasAllStages
    {
        get
        {
            if (burnStages == null || burnStages.Length != 5) return false;
            foreach (Sprite sprite in burnStages) if (sprite == null) return false;
            return true;
        }
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = GetStageSprite(0);
        tileData.color = Color.white;
        tileData.transform = Matrix4x4.identity;
        tileData.flags = TileFlags.None;
        tileData.colliderType = Tile.ColliderType.None;
    }

#if UNITY_EDITOR
    // Editor baker owns these writes; runtime never needs readable source textures.
    public void SetArtwork(Sprite[] sprites) { burnStages = sprites; }
    public void SetBakedMask(Vector2[] samples, string sourceHash)
    {
        fuelSamples = samples;
        bakedSprite = GetStageSprite(0);
        bakedSourceHash = sourceHash;
    }
#endif
}
