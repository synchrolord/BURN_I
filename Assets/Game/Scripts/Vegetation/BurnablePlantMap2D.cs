using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>One component for the whole plant Tilemap. No per-plant GameObjects or Update methods.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
public sealed class BurnablePlantMap2D : MonoBehaviour
{
    [SerializeField] private CellWorld2D world;
    [Tooltip("This is a static decoration map. After runtime painting or moving it, call ResetPlantsAndRebuild().")]
    [SerializeField] private bool warnAboutUnregisteredPlants = true;

    private sealed class Plant
    {
        public Vector3Int position;
        public BurnablePlantTile tile;
        public Matrix4x4 transform;
        public Color color;
        public double capacity, consumed;
        public int stage;
        public bool queued, finished;
    }

    private Tilemap map;
    private readonly List<Plant> plants = new List<Plant>();
    private readonly Dictionary<Vector3Int, int> plantAt = new Dictionary<Vector3Int, int>();
    private readonly Dictionary<BurnablePlantTile, Tile[]> stageTiles = new Dictionary<BurnablePlantTile, Tile[]>();
    private readonly List<int> dirtyPlants = new List<int>();
    private Coroutine initialization;
    private bool registered;
    private CellWorld2D registeredWorld;
    public int RegisteredPlantCount => plants.Count;
    public int RegisteredFuelSampleCount { get; private set; }
    public CellWorld2D World { get => world; set => world = value; }

    private void OnEnable() { initialization = StartCoroutine(InitializeWhenReady()); }

    private IEnumerator InitializeWhenReady()
    {
        map = GetComponent<Tilemap>();
        if (world == null) world = FindAnyObjectByType<CellWorld2D>();
        if (world == null)
        {
            Debug.LogWarning("Burnable plants need a CellWorld2D. Assign World, then re-enable the component.", this);
            yield break;
        }
        while (world != null && !world.IsInitialized) yield return null;
        if (world != null) RegisterMap();
        initialization = null;
    }

    private void RegisterMap()
    {
        registeredWorld = world;
        BoundsInt bounds = map.cellBounds;
        if (bounds.size == Vector3Int.zero) { registered = true; return; }
        Vector3Int last = bounds.max - Vector3Int.one;
        int count = map.GetTilesRangeCount(bounds.min, last);
        var positions = new Vector3Int[count];
        var tiles = new TileBase[count];
        count = map.GetTilesRangeNonAlloc(bounds.min, last, positions, tiles);
        var cells = new HashSet<Vector2Int>();
        int missingMasks = 0, outside = 0, partial = 0;
        for (int n = 0; n < count; n++)
        {
            if (!(tiles[n] is BurnablePlantTile tile)) continue;
            if (!tile.HasBakedMask) { missingMasks++; continue; }
            Vector3Int position = positions[n];
            Matrix4x4 tileTransform = map.GetTransformMatrix(position);
            Matrix4x4 orientation = map.orientationMatrix;
            Vector3 pivot = map.CellToLocalInterpolated((Vector3)position + map.tileAnchor);
            float pixelSize = 1f / tile.GetStageSprite(0).pixelsPerUnit;
            Matrix4x4 toWorldGrid = world.transform.worldToLocalMatrix * map.transform.localToWorldMatrix * orientation * tileTransform;
            float sampleSpan = Mathf.Max(toWorldGrid.MultiplyVector(Vector3.right * pixelSize).magnitude,
                toWorldGrid.MultiplyVector(Vector3.up * pixelSize).magnitude);
            int subdivisions = Mathf.Max(1, Mathf.CeilToInt(sampleSpan / world.CellSize));
            cells.Clear();
            bool clipped = false;
            foreach (Vector2 sample in tile.FuelSamples)
            {
                for (int sy = 0; sy < subdivisions; sy++)
                for (int sx = 0; sx < subdivisions; sx++)
                {
                    Vector2 offset = new Vector2((sx + 0.5f) / subdivisions - 0.5f, (sy + 0.5f) / subdivisions - 0.5f) * pixelSize;
                    Vector3 local = pivot + orientation.MultiplyPoint3x4(tileTransform.MultiplyPoint3x4(sample + offset));
                    if (world.TryWorldToCell(map.transform.TransformPoint(local), out Vector2Int cell)
                        && cell.x > 0 && cell.y > 0 && cell.x < world.Width - 1 && cell.y < world.Height - 1)
                        cells.Add(cell);
                    else clipped = true;
                }
            }
            if (cells.Count == 0) { outside++; continue; }
            if (clipped) partial++;
            int id = plants.Count;
            var plant = new Plant { position = position, tile = tile, transform = tileTransform, color = map.GetColor(position) };
            int accepted = 0;
            foreach (Vector2Int cell in cells)
                if (world.RegisterPlantFuel(cell, this, id, tile.BurnSeconds, tile.WetSeconds)) accepted++;
            plant.capacity = accepted * (double)tile.BurnSeconds;
            plants.Add(plant);
            plantAt.Add(position, id);
            RegisteredFuelSampleCount += accepted;
        }
        registered = true;
        if (warnAboutUnregisteredPlants && (missingMasks + outside + partial > 0))
            Debug.LogWarning($"Plants: {missingMasks} missing baked masks, {outside} outside CellWorld, {partial} partly outside. Bake masks / enlarge or reposition CellWorld.", this);
    }

    internal void OnPlantFuelBurned(int id, float amount)
    {
        Plant plant = plants[id];
        plant.consumed = System.Math.Min(plant.capacity, plant.consumed + amount);
        if (plant.queued) return;
        plant.queued = true;
        dirtyPlants.Add(id);
    }

    public bool TryGetBurnProgress(Vector3Int position, out float progress)
    {
        progress = 0f;
        if (!plantAt.TryGetValue(position, out int id)) return false;
        Plant plant = plants[id];
        progress = plant.capacity > 0 ? Mathf.Clamp01((float)(plant.consumed / plant.capacity)) : 0f;
        return true;
    }

    private void LateUpdate()
    {
        for (int n = 0; n < dirtyPlants.Count; n++)
        {
            Plant plant = plants[dirtyPlants[n]];
            plant.queued = false;
            if (plant.finished) continue;
            float progress = (float)(plant.consumed / plant.capacity);
            int stage = Mathf.Min(4, Mathf.FloorToInt(progress * 5f));
            bool finished = progress >= 0.99999f;
            if (finished && plant.tile.DisappearWhenBurned) map.SetTile(plant.position, null);
            else if (stage != plant.stage) SetStage(plant, stage);
            plant.stage = stage;
            plant.finished = finished;
        }
        dirtyPlants.Clear();
    }

    private void SetStage(Plant plant, int stage)
    {
        if (!stageTiles.TryGetValue(plant.tile, out Tile[] stages))
        {
            stages = new Tile[5];
            stageTiles.Add(plant.tile, stages);
        }
        if (stages[stage] == null)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = plant.tile.name + " burn " + stage;
            tile.hideFlags = HideFlags.HideAndDontSave;
            tile.sprite = plant.tile.GetStageSprite(stage);
            tile.flags = TileFlags.None;
            tile.colliderType = Tile.ColliderType.None;
            stages[stage] = tile;
        }
        map.SetTile(plant.position, stages[stage]);
        map.SetTransformMatrix(plant.position, plant.transform);
        map.SetColor(plant.position, plant.color);
    }

    /// <summary>Restore original artwork, forget damage, and register the current static map again.</summary>
    [ContextMenu("Reset Plants And Rebuild (Play Mode)")]
    public void ResetPlantsAndRebuild()
    {
        if (!Application.isPlaying) return;
        ReleaseRegistration();
        if (initialization != null) StopCoroutine(initialization);
        initialization = StartCoroutine(InitializeWhenReady());
    }

    private void OnDisable()
    {
        if (initialization != null) StopCoroutine(initialization);
        initialization = null;
        ReleaseRegistration();
    }

    private void ReleaseRegistration()
    {
        if (registered && registeredWorld != null) registeredWorld.UnregisterPlantFuel(this);
        if (map != null)
        {
            foreach (Plant plant in plants)
            {
                // Restore only our runtime stages / vanished pieces; don't overwrite newly painted tiles.
                TileBase current = map.GetTile(plant.position);
                bool ours = current == null || current == plant.tile;
                if (!ours && stageTiles.TryGetValue(plant.tile, out Tile[] stages))
                    foreach (Tile tile in stages) if (current == tile) { ours = true; break; }
                if (!ours) continue;
                map.SetTile(plant.position, plant.tile);
                map.SetTransformMatrix(plant.position, plant.transform);
                map.SetColor(plant.position, plant.color);
            }
        }
        foreach (Tile[] stages in stageTiles.Values)
            foreach (Tile tile in stages) if (tile != null) Destroy(tile);
        plants.Clear(); plantAt.Clear(); stageTiles.Clear(); dirtyPlants.Clear();
        RegisteredFuelSampleCount = 0;
        registered = false;
        registeredWorld = null;
    }
}
