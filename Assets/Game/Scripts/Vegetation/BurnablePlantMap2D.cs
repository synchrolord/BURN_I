using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

//copy ordinary Tilemap sprites into CellWorld when play starts
[DisallowMultipleComponent, RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
public sealed class BurnablePlantMap2D : MonoBehaviour
{
    private sealed class PlantTile
    {
        public Vector3Int position;
        public BurnablePlantTile animatedTile;
        public Color color;
        public Matrix4x4 transform;
        public readonly List<Vector2Int> cells = new List<Vector2Int>();
        public int stage = -1;
    }

    [SerializeField] private CellWorld2D world;

    private Tilemap map;
    private readonly Dictionary<Sprite, Color32[]> spritePixels = new Dictionary<Sprite, Color32[]>();
    private readonly Dictionary<Vector3Int, PlantTile> plants = new Dictionary<Vector3Int, PlantTile>();
    private readonly Dictionary<Sprite, Tile> stageTiles = new Dictionary<Sprite, Tile>();
    private float nextVisualUpdate;
    public CellWorld2D World { get => world; set => world = value; }

    private void Awake()
    {
        map = GetComponent<Tilemap>();

        
        TilemapCollider2D plantCollider = GetComponent<TilemapCollider2D>();
        if (plantCollider != null) plantCollider.enabled = false;
    }

    private IEnumerator Start()
    {
        if (world == null) world = FindAnyObjectByType<CellWorld2D>();

        if (world == null)
        {
            Debug.LogWarning("Burnable Plants needs a CellWorld2D.", this);
            yield break;
        }

        //wait until CellWorld has created its arrays
        while (!world.IsInitialized && !world.InitializationFailed)
            yield return null;

        if (!world.IsInitialized) yield break;

        CopyPlants();
        UpdatePlantTiles();
    }

    private void CopyPlants()
    {
        for (int y = 0; y < world.Height; y++)
        for (int x = 0; x < world.Width; x++)
        {
            Vector2 worldPoint = world.CellToWorld(x, y);
            Vector3Int tilePosition = map.WorldToCell(worldPoint);
            Sprite sprite = map.GetSprite(tilePosition);
            if (sprite == null) continue;

            Vector3 localPoint = map.transform.InverseTransformPoint(worldPoint);
            Vector2 offset = localPoint - map.GetCellCenterLocal(tilePosition);

            int pixelX = Mathf.FloorToInt(offset.x * sprite.pixelsPerUnit + sprite.pivot.x);
            int pixelY = Mathf.FloorToInt(offset.y * sprite.pixelsPerUnit + sprite.pivot.y);
            int spriteWidth = Mathf.RoundToInt(sprite.rect.width);
            int spriteHeight = Mathf.RoundToInt(sprite.rect.height);

            if (pixelX < 0 || pixelY < 0 || pixelX >= spriteWidth || pixelY >= spriteHeight)
                continue;

            Color32 color = GetSpritePixels(sprite)[pixelX + pixelY * spriteWidth];
            if (color.a <= 25) continue;

            //include any color painted onto this Tilemap cell
            Color tint = map.GetColor(tilePosition);
            Color finalColor = (Color)color * tint;
            world.AddPlantCell(x, y, finalColor);

            if (!plants.TryGetValue(tilePosition, out PlantTile plantTile))
            {
                plantTile = new PlantTile
                {
                    position = tilePosition,
                    animatedTile = map.GetTile(tilePosition) as BurnablePlantTile,
                    color = tint,
                    transform = map.GetTransformMatrix(tilePosition)
                };
                plants.Add(tilePosition, plantTile);
            }

            plantTile.cells.Add(new Vector2Int(x, y));
        }

        spritePixels.Clear();
    }

    private void LateUpdate()
    {
        //visual stages do not need to be checked every frame
        if (world == null || Time.time < nextVisualUpdate) return;
        nextVisualUpdate = Time.time + 0.1f;
        UpdatePlantTiles();
    }

    private void UpdatePlantTiles()
    {
        foreach (PlantTile tile in plants.Values)
        {
            int remaining = 0;
            foreach (Vector2Int cell in tile.cells)
                remaining += world.GetPlant(cell.x, cell.y);

            if (remaining == 0)
            {
                if (tile.stage != 5) map.SetTile(tile.position, null);
                tile.stage = 5;
                continue;
            }

            float health = remaining / (tile.cells.Count * 5f);
            int stage = Mathf.Clamp(Mathf.FloorToInt((1f - health) * 5f), 0, 4);
            if (stage == tile.stage) continue;

            if (tile.animatedTile != null)
            {
                Sprite sprite = tile.animatedTile.GetStage(stage);
                if (sprite != null) map.SetTile(tile.position, GetStageTile(sprite));
                map.SetTransformMatrix(tile.position, tile.transform);
                map.SetColor(tile.position, tile.color);
            }
            else
            {
                //ordinary Tiles darken through the same five stages
                map.SetTileFlags(tile.position, TileFlags.None);
                float darkness = stage / 4f;
                Color darkColor = Color.Lerp(tile.color, tile.color * 0.3f, darkness);
                darkColor.a = tile.color.a;
                map.SetColor(tile.position, darkColor);
            }

            tile.stage = stage;
        }
    }

    private Tile GetStageTile(Sprite sprite)
    {
        if (stageTiles.TryGetValue(sprite, out Tile tile)) return tile;

        tile = ScriptableObject.CreateInstance<Tile>();
        tile.name = sprite.name + " burn stage";
        tile.hideFlags = HideFlags.HideAndDontSave;
        tile.sprite = sprite;
        tile.flags = TileFlags.None;
        tile.colliderType = Tile.ColliderType.None;
        stageTiles.Add(sprite, tile);
        return tile;
    }

    //read one sprite through the GPU, so Read/Write does not need to be enabled
    private Color32[] GetSpritePixels(Sprite sprite)
    {
        if (spritePixels.TryGetValue(sprite, out Color32[] colors)) return colors;

        int width = Mathf.RoundToInt(sprite.rect.width);
        int height = Mathf.RoundToInt(sprite.rect.height);
        RenderTexture oldTarget = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);

        Vector2 scale = new Vector2(
            sprite.rect.width / sprite.texture.width,
            sprite.rect.height / sprite.texture.height);
        Vector2 offset = new Vector2(
            sprite.rect.x / sprite.texture.width,
            sprite.rect.y / sprite.texture.height);

        Graphics.Blit(sprite.texture, target, scale, offset);
        RenderTexture.active = target;

        Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        copy.Apply();
        colors = copy.GetPixels32();

        Destroy(copy);
        RenderTexture.active = oldTarget;
        RenderTexture.ReleaseTemporary(target);
        spritePixels.Add(sprite, colors);
        return colors;
    }

    private void OnDestroy()
    {
        foreach (Tile tile in stageTiles.Values)
            if (tile != null) Destroy(tile);
    }
}
