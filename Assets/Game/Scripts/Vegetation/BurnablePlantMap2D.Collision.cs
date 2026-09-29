using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// This is still BurnablePlantMap2D, not another component to attach.
// Unity normally changes Sprite collision with the artwork. These helpers keep the
// healthy outline solid until the plant disappears, then update CellWorld obstacles.
public sealed partial class BurnablePlantMap2D
{
    private TilemapCollider2D plantCollider;
    private CompositeCollider2D plantComposite;
    //flag that the plant collider geometry needs rebuilding
    private bool collisionGeometryDirty;
    //keep temporary sprite copies so they can be destroyed later
    private readonly List<Sprite> collisionSprites = new List<Sprite>();
    //cache each plant asset's healthy collision outlines as arrays of points
    private readonly Dictionary<BurnablePlantTile, Vector2[][]> healthyShapes = new Dictionary<BurnablePlantTile, Vector2[][]>();
    //collect affected grid cells without adding the same index twice
    private readonly HashSet<int> solidCellsToRefresh = new HashSet<int>();

    //find the tilemap collider and any composite collider it uses
    private void InitializePlantCollision()
    {
        plantCollider = GetComponent<TilemapCollider2D>();
        // Old scene tile-data caches may predate the collision setting. Refresh once
        // on registration so existing painted tiles use their current asset settings.
        if (plantCollider != null) map.RefreshAllTiles();
        plantComposite = GetComponent<CompositeCollider2D>();
        if (plantComposite == null && plantCollider != null && plantCollider.attachedRigidbody != null)
            plantComposite = plantCollider.attachedRigidbody.GetComponent<CompositeCollider2D>();
        collisionGeometryDirty = plantCollider != null;
    }

    //read the healthy sprite's collision outlines once and reuse them
    private Vector2[][] GetHealthyPhysicsShapes(BurnablePlantTile tile)
    {
        if (healthyShapes.TryGetValue(tile, out Vector2[][] shapes)) return shapes;
        Sprite healthy = tile.GetStageSprite(0);
        shapes = new Vector2[healthy.GetPhysicsShapeCount()][];
        var points = new List<Vector2>();
        for (int i = 0; i < shapes.Length; i++)
        {
            points.Clear();
            //copy one collision outline into its own point array
            healthy.GetPhysicsShape(i, points);
            shapes[i] = points.ToArray();
        }
        healthyShapes.Add(tile, shapes);
        return shapes;
    }

    //show burned artwork while keeping the healthy collision shape until finished
    private Sprite GetPlantStageSprite(BurnablePlantTile tile, int stage, bool finished)
    {
        Sprite artwork = tile.GetStageSprite(stage);
        if (finished || tile.CollisionType != Tile.ColliderType.Sprite || artwork == tile.GetStageSprite(0)) return artwork;
        // Clone native sprite data: atlas UVs, mesh and secondary textures stay intact.
        // Never override the imported sprite asset, which other maps can be using.
        Sprite runtime = Instantiate(artwork);
        runtime.name = artwork.name + " (healthy collision)";
        runtime.hideFlags = HideFlags.HideAndDontSave;
        Vector2[][] localShapes = GetHealthyPhysicsShapes(tile);
        var rectShapes = new Vector2[localShapes.Length][];
        for (int s = 0; s < localShapes.Length; s++)
        {
            rectShapes[s] = new Vector2[localShapes[s].Length];
            for (int p = 0; p < localShapes[s].Length; p++)
                // GetPhysicsShape returns local units about the pivot; OverridePhysicsShape
                // takes pixel coordinates inside the sprite rect (not atlas coordinates).
                rectShapes[s][p] = localShapes[s][p] * runtime.pixelsPerUnit + runtime.pivot;
        }
        runtime.OverridePhysicsShape(rectShapes);
        collisionSprites.Add(runtime);
        return runtime;
    }

    //find a world-space box covering this plant's possible collision area
    private Bounds GetPlantCollisionBounds(Plant plant)
    {
        Vector3 pivot = map.CellToLocalInterpolated((Vector3)plant.position + map.tileAnchor);
        //combine the placement transforms so sprite points can be converted to world positions
        Matrix4x4 toWorld = map.transform.localToWorldMatrix * Matrix4x4.Translate(pivot)
            * map.orientationMatrix * plant.transform;
        Sprite healthy = plant.tile.GetStageSprite(0);
        Vector2 min = -healthy.pivot / healthy.pixelsPerUnit;
        Vector2 max = (healthy.rect.size - healthy.pivot) / healthy.pixelsPerUnit;
        //expand the box around all sprite corners and collision points
        Bounds bounds = new Bounds(toWorld.MultiplyPoint3x4(min), Vector3.zero);
        bounds.Encapsulate(toWorld.MultiplyPoint3x4(new Vector2(min.x, max.y)));
        bounds.Encapsulate(toWorld.MultiplyPoint3x4(new Vector2(max.x, min.y)));
        bounds.Encapsulate(toWorld.MultiplyPoint3x4(max));
        if (plant.tile.CollisionType == Tile.ColliderType.Sprite)
            foreach (Vector2[] shape in GetHealthyPhysicsShapes(plant.tile))
                foreach (Vector2 point in shape) bounds.Encapsulate(toWorld.MultiplyPoint3x4(point));

        // Also include the grid cell: it can be larger than the sprite, or use a different anchor.
        Bounds cell = map.GetBoundsLocal(plant.position);
        for (int y = 0; y < 2; y++)
        for (int x = 0; x < 2; x++)
            bounds.Encapsulate(map.transform.TransformPoint(new Vector3(x == 0 ? cell.min.x : cell.max.x,
                y == 0 ? cell.min.y : cell.max.y, cell.center.z)));
        return bounds;
    }

    //mark collision geometry as changed and queue nearby grid cells for rechecking
    private void QueueCollisionRefresh(Plant plant)
    {
        if (plantCollider == null || registeredWorld == null || plant.tile.CollisionType == Tile.ColliderType.None) return;
        collisionGeometryDirty = true;
        registeredWorld.CollectSolidRefreshCells(plant.collisionBounds, solidCellsToRefresh);
    }

    //apply queued collider changes before refreshing the grid's blocked cells
    private void FlushPlantCollisionChanges()
    {
        if (plantCollider != null && plantCollider.isActiveAndEnabled && (collisionGeometryDirty || plantCollider.hasTilemapChanges))
        {
            plantCollider.ProcessTilemapChanges();
            if (plantComposite != null && plantCollider.compositeOperation != Collider2D.CompositeOperation.None)
                plantComposite.GenerateGeometry();
        }
        if (registeredWorld != null && solidCellsToRefresh.Count > 0)
            registeredWorld.RefreshSolidCells(solidCellsToRefresh);
        //clear the queue and reset the change flag after refreshing
        solidCellsToRefresh.Clear();
        collisionGeometryDirty = false;
    }

    //destroy the temporary sprites and forget cached collision data
    private void ReleasePlantCollision()
    {
        foreach (Sprite sprite in collisionSprites) if (sprite != null) Destroy(sprite);
        collisionSprites.Clear(); healthyShapes.Clear(); solidCellsToRefresh.Clear();
        plantCollider = null; plantComposite = null;
    }
}
