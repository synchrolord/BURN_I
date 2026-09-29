using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// BASIC EDITION: one manager for the whole plant Tilemap.
// Each Plant below is plain data, not a GameObject. Hundreds of tiles share this Update.
// CellWorld burns the baked pixels and reports how much fuel each plant has lost.
[DisallowMultipleComponent, RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
public sealed partial class BurnablePlantMap2D : MonoBehaviour
{
    [SerializeField] private CellWorld2D world;
    [Tooltip("This is a static decoration map. After runtime painting or moving it, call ResetPlantsAndRebuild().")]
    [SerializeField] private bool warnAboutUnregisteredPlants = true;

    //store one painted plant's position, appearance and total burn damage
    private sealed class Plant
    {
        public Vector3Int position;
        public BurnablePlantTile tile;
        public Matrix4x4 transform;
        public Color color;
        //total registered burn time across its cells, and how much has been used
        public double capacity, consumed;
        public int stage;
        public bool finished;
        public Bounds collisionBounds;
    }

    private Tilemap map;
    private readonly List<Plant> plants = new List<Plant>();
    //find a plant's list index from its tilemap position
    private readonly Dictionary<Vector3Int, int> plantAt = new Dictionary<Vector3Int, int>();
    //reuse the temporary tiles made for each asset's burn stages
    private readonly Dictionary<BurnablePlantTile, Tile[]> stageTiles = new Dictionary<BurnablePlantTile, Tile[]>();
    private Coroutine initialization;
    private bool registered;
    private CellWorld2D registeredWorld;
    public int RegisteredPlantCount => plants.Count;
    public int RegisteredFuelSampleCount { get; private set; }
    public CellWorld2D World { get => world; set => world = value; }

    private void OnEnable() { initialization = StartCoroutine(InitializeWhenReady()); }

    //find the tilemap and wait for CellWorld to finish starting
    private IEnumerator InitializeWhenReady()
    {
        map = GetComponent<Tilemap>();
        if (world == null) world = FindAnyObjectByType<CellWorld2D>();
        if (world == null)
        {
            Debug.LogWarning("Burnable plants need a CellWorld2D. Assign World, then re-enable the component.", this);
            yield break;
        }
        //wait until the next frame while the grid is still initializing
        while (world != null && !world.IsInitialized && !world.InitializationFailed) yield return null;
        if (world != null && world.IsInitialized) RegisterMap();
        initialization = null;
    }

    // Register the HEALTHY pixels once. Changing the visible sprite does not move the fuel.
    // The matrix calculations support rotated tiles, sprite pivots and different pixel sizes.
    private void RegisterMap()
    {
        registeredWorld = world;
        InitializePlantCollision();
        BoundsInt bounds = map.cellBounds;
        if (bounds.size == Vector3Int.zero) { registered = true; return; }
        //the bounds end is exclusive, so subtract one to get the last tile position
        Vector3Int last = bounds.max - Vector3Int.one;
        //collect the painted tiles and their positions into matching arrays
        int count = map.GetTilesRangeCount(bounds.min, last);
        var positions = new Vector3Int[count];
        var tiles = new TileBase[count];
        count = map.GetTilesRangeNonAlloc(bounds.min, last, positions, tiles);
        //keep each covered simulation cell only once per plant
        var cells = new HashSet<Vector2Int>();
        int missingMasks = 0, outside = 0, partial = 0;
        for (int n = 0; n < count; n++)
        {
            //skip ordinary tiles and plants without prepared fuel samples
            if (!(tiles[n] is BurnablePlantTile tile)) continue;
            if (!tile.HasBakedMask) { missingMasks++; continue; }
            Vector3Int position = positions[n];
            Matrix4x4 tileTransform = map.GetTransformMatrix(position);
            Matrix4x4 orientation = map.orientationMatrix;
            Vector3 pivot = map.CellToLocalInterpolated((Vector3)position + map.tileAnchor);
            float pixelSize = 1f / tile.GetStageSprite(0).pixelsPerUnit;
            //measure the sprite pixel size after the tile and grid transforms
            Matrix4x4 toWorldGrid = world.transform.worldToLocalMatrix * map.transform.localToWorldMatrix * orientation * tileTransform;
            float sampleSpan = Mathf.Max(toWorldGrid.MultiplyVector(Vector3.right * pixelSize).magnitude,
                toWorldGrid.MultiplyVector(Vector3.up * pixelSize).magnitude);
            //take extra samples when one artwork pixel covers multiple simulation cells
            int subdivisions = Mathf.Max(1, Mathf.CeilToInt(sampleSpan / world.CellSize));
            cells.Clear();
            bool clipped = false;
            foreach (Vector2 sample in tile.FuelSamples)
            {
                for (int sy = 0; sy < subdivisions; sy++)
                for (int sx = 0; sx < subdivisions; sx++)
                {
                    Vector2 offset = new Vector2((sx + 0.5f) / subdivisions - 0.5f, (sy + 0.5f) / subdivisions - 0.5f) * pixelSize;
                    //move the sample from sprite space to the tilemap, then into the world grid
                    Vector3 local = pivot + orientation.MultiplyPoint3x4(tileTransform.MultiplyPoint3x4(sample + offset));
                    if (world.TryWorldToCell(map.transform.TransformPoint(local), out Vector2Int cell)
                        && cell.x > 0 && cell.y > 0 && cell.x < world.Width - 1 && cell.y < world.Height - 1)
                        cells.Add(cell);
                    else clipped = true;
                }
            }
            //skip plants completely outside the grid and count partly clipped ones
            if (cells.Count == 0) { outside++; continue; }
            if (clipped) partial++;
            //use the plant's list position as its id when registering its fuel
            int id = plants.Count;
            var plant = new Plant { position = position, tile = tile, transform = tileTransform, color = map.GetColor(position) };
            if (plantCollider != null && tile.CollisionType != Tile.ColliderType.None)
                plant.collisionBounds = GetPlantCollisionBounds(plant);
            int accepted = 0;
            //register a separate plant-fuel entry in every covered simulation cell
            foreach (Vector2Int cell in cells)
                if (world.RegisterPlantFuel(cell, this, id, tile.BurnSeconds, tile.WetSeconds)) accepted++;
            //sum the burn time across registered cells to track total plant damage
            plant.capacity = accepted * (double)tile.BurnSeconds;
            plants.Add(plant);
            plantAt.Add(position, id);
            RegisteredFuelSampleCount += accepted;
            QueueCollisionRefresh(plant);
        }
        registered = true;
        FlushPlantCollisionChanges();
        if (warnAboutUnregisteredPlants && (missingMasks + outside + partial > 0))
            Debug.LogWarning($"Plants: {missingMasks} missing baked masks, {outside} outside CellWorld, {partial} partly outside. Bake masks / enlarge or reposition CellWorld.", this);
    }

    //receive consumed plant fuel from CellWorld and add it to this plant's damage
    internal void OnPlantFuelBurned(int id, float amount)
    {
        Plant plant = plants[id];
        plant.consumed = System.Math.Min(plant.capacity, plant.consumed + amount);
    }

    //look up a painted plant and return its burned fraction from 0 to 1
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
        // A plain loop is enough for hundreds of plants; it allocates nothing per frame.
        // Only a change of burn stage replaces a Tile.
        foreach (Plant plant in plants)
        {
            if (plant.finished || plant.capacity <= 0 || plant.consumed <= 0) continue;
            float progress = (float)(plant.consumed / plant.capacity);
            //convert the burned fraction into one of five artwork stages
            int stage = Mathf.Min(4, Mathf.FloorToInt(progress * 5f));
            bool finished = progress >= 0.99999f;
            //fully burned plants disappear or keep a remnant without collision
            if (finished)
            {
                QueueCollisionRefresh(plant);
                if (plant.tile.DisappearWhenBurned) map.SetTile(plant.position, null);
                else SetStage(plant, 4, true); // An optional charred remnant is no longer solid.
            }
            else if (stage != plant.stage) SetStage(plant, stage);
            plant.stage = stage;
            plant.finished = finished;
        }
        FlushPlantCollisionChanges();
    }

    // Cache one Tile per artwork/stage. Plants reuse it instead of making one every frame.
    private void SetStage(Plant plant, int stage, bool finished = false)
    {
        //reserve slot 5 for the finished version without collision
        int cacheIndex = finished ? 5 : stage;
        if (!stageTiles.TryGetValue(plant.tile, out Tile[] stages))
        {
            stages = new Tile[6];
            stageTiles.Add(plant.tile, stages);
        }
        //create each temporary stage tile only when first needed
        if (stages[cacheIndex] == null)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = plant.tile.name + " burn " + stage;
            tile.hideFlags = HideFlags.HideAndDontSave;
            tile.sprite = GetPlantStageSprite(plant.tile, stage, finished);
            tile.flags = TileFlags.None;
            tile.colliderType = finished ? Tile.ColliderType.None : plant.tile.CollisionType;
            stages[cacheIndex] = tile;
        }
        //replace the artwork and restore this placement's transform and tint
        map.SetTile(plant.position, stages[cacheIndex]);
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

    //stop pending initialization and release this map's registered plants
    private void OnDisable()
    {
        if (initialization != null) StopCoroutine(initialization);
        initialization = null;
        ReleaseRegistration();
    }

    //remove this map's fuel, restore its tiles and clean up temporary objects
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
                QueueCollisionRefresh(plant);
                map.SetTile(plant.position, plant.tile);
                map.SetTransformMatrix(plant.position, plant.transform);
                map.SetColor(plant.position, plant.color);
            }
        }
        FlushPlantCollisionChanges();
        //destroy cached stage tiles and clear the registration data
        foreach (Tile[] stages in stageTiles.Values)
            foreach (Tile tile in stages) if (tile != null) Destroy(tile);
        plants.Clear();
        plantAt.Clear();
        stageTiles.Clear();
        ReleasePlantCollision();
        RegisteredFuelSampleCount = 0;
        registered = false;
        registeredWorld = null;
    }
}
