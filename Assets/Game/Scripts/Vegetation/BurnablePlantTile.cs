using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "Burnable Plant", menuName = "BURN/Burnable Plant Tile")]
public sealed class BurnablePlantTile : TileBase
{
    [SerializeField] private Sprite[] burnStages = new Sprite[5];

    public Sprite GetStage(int stage)
    {
        if (burnStages == null || burnStages.Length == 0) return null;

        //use the nearest earlier picture when a stage is left empty
        for (int i = Mathf.Min(stage, burnStages.Length - 1); i >= 0; i--)
            if (burnStages[i] != null) return burnStages[i];

        return null;
    }

    
    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = GetStage(0);

        tileData.color = Color.white;
        tileData.transform = Matrix4x4.identity;
        tileData.flags = TileFlags.None;
        tileData.colliderType = Tile.ColliderType.None;
    }
}
