using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class PlantCollisionCheck
{
    static PlantCollisionCheck()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PlantCollisionCheck.Run", false))
            {
                SessionState.SetBool("PlantCollisionCheck.Run", false);
                new GameObject("Plant collision checks").AddComponent<PlantCollisionCheckRunner>();
            }
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Run in an isolated batch project.");
        Directory.CreateDirectory("ValidationResults");
        File.WriteAllText("ValidationResults/plant-collision.txt", "");
        File.WriteAllText("ValidationResults/plants.txt", "");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.LockReloadAssemblies();
        SessionState.SetBool("PlantCollisionCheck.Run", true);
        EditorApplication.EnterPlaymode();
    }
}

public sealed class PlantCollisionCheckRunner : MonoBehaviour
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    static void Call(object target, string method) => target.GetType().GetMethod(method, Flags).Invoke(target, null);
    static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        File.AppendAllText("ValidationResults/plant-collision.txt", "PASS: " + description + "\n");
    }
    CellWorld2D world;
    readonly List<GameObject> roots = new List<GameObject>();
    readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
    readonly Vector3Int position = new Vector3Int(4,4,0);

    BurnablePlantTile MakeTile(bool disappear = true, Tile.ColliderType collision = Tile.ColliderType.Sprite)
    {
        var texture = new Texture2D(16,16); assets.Add(texture);
        var pixels = Enumerable.Repeat(new Color32(80,180,40,255),256).ToArray();
        texture.SetPixels32(pixels); texture.Apply();
        var sprites = new Sprite[5];
        for(int i=0;i<5;i++)
        {
            sprites[i]=Sprite.Create(texture,new Rect(0,0,16,16),Vector2.one*.5f,16,0,SpriteMeshType.FullRect);
            sprites[i].name="Stage "+i;
            float edge=7.2f-i*1.44f;
            sprites[i].OverridePhysicsShape(new[]{ new[]{new Vector2(8-edge,8-edge),new Vector2(8-edge,8+edge),new Vector2(8+edge,8+edge),new Vector2(8+edge,8-edge)} });
            assets.Add(sprites[i]);
        }
        var tile=ScriptableObject.CreateInstance<BurnablePlantTile>(); assets.Add(tile);
        tile.SetArtwork(sprites); tile.SetBakedMask(new[]{new Vector2(.03f,.03f)},"test");
        Set(tile,"burnSeconds",2f); Set(tile,"collisionType",collision); Set(tile,"disappearWhenBurned",disappear);
        return tile;
    }
    BurnablePlantMap2D MakeMap(BurnablePlantTile tile, Vector3Int[] positions = null, bool composite = false,
        Matrix4x4? tileTransform = null, float gridSize = 1f, bool collider = true)
    {
        var root = new GameObject("Collision grid",typeof(Grid)); roots.Add(root);
        root.GetComponent<Grid>().cellSize = new Vector3(gridSize,gridSize,1f);
        var go = new GameObject("Collision plants",typeof(Tilemap),typeof(TilemapRenderer));
        go.SetActive(false); go.transform.SetParent(root.transform,false);
        if(composite)
        {
            go.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;
            var merged=go.AddComponent<CompositeCollider2D>();
            merged.geometryType=CompositeCollider2D.GeometryType.Polygons;
            merged.generationType=CompositeCollider2D.GenerationType.Manual;
        }
        if(collider)
        {
            var coll=go.AddComponent<TilemapCollider2D>();
            if(composite) coll.compositeOperation=Collider2D.CompositeOperation.Merge;
        }
        var map=go.GetComponent<Tilemap>();
        foreach(var cell in positions ?? new[]{position})
        {
            map.SetTile(cell,tile);
            if(tileTransform.HasValue) map.SetTransformMatrix(cell,tileTransform.Value);
        }
        var manager=go.AddComponent<BurnablePlantMap2D>(); manager.World=world;
        go.SetActive(true); return manager;
    }
    void Tick(BurnablePlantMap2D map,int count)
    {
        for(int i=0;i<count;i++) {world.StepOnce(); Call(map,"LateUpdate");}
    }
    bool Solid(Vector2 point) => world.AddWater(point,0,1)==0;
    void Finish(BurnablePlantMap2D map, Vector2 point)
    {
        world.Ignite(point,0); Tick(map,43);
    }
    void Cleanup()
    {
        foreach(var root in roots) if(root!=null) DestroyImmediate(root);
        roots.Clear(); world.ClearAllMaterials(); world.RebakeSolidsFromPhysics();
    }
    IEnumerator Start()
    {
        Application.logMessageReceived+=(message,trace,type)=>
        {
            if(type==LogType.Error || type==LogType.Exception)
            {
                File.AppendAllText("ValidationResults/plant-collision.txt","FAIL: "+message+"\n"+trace);
                EditorApplication.Exit(1);
            }
        };
        var previousMode=Physics2D.simulationMode;
        Physics2D.simulationMode=SimulationMode2D.Script;
        world=new GameObject("Collision test world").AddComponent<CellWorld2D>();
        Set(world,"width",256); Set(world,"height",128); Set(world,"cellSize",.125f);
        Set(world,"solidMask",(LayerMask)1); Set(world,"startPaused",true);
        yield return null;
        Vector2 center=new Vector2(4.53f,4.53f), edge=new Vector2(4.85f,4.8f);
        var tile=MakeTile(); var manager=MakeMap(tile);
        var collider=manager.GetComponent<TilemapCollider2D>(); var map=manager.GetComponent<Tilemap>();
        Check(collider.OverlapPoint(edge),"Healthy sprite collider covers its original outer edge");
        Check(Solid(center),"Initial plant collider is baked into liquid obstacles after registration");
        var bodyObject=new GameObject("Standing character",typeof(CircleCollider2D),typeof(Rigidbody2D)); roots.Add(bodyObject);
        bodyObject.layer=6;
        bodyObject.GetComponent<CircleCollider2D>().radius=.1f;
        var body=bodyObject.GetComponent<Rigidbody2D>(); body.freezeRotation=true;
        body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        body.position=new Vector2(4.8f,5.6f);
        for(int i=0;i<120;i++) Physics2D.Simulate(1f/60f);
        Check(body.position.y>5f && body.position.y<5.2f,"Character stands on the healthy plant");
        world.Ignite(center,0);
        for(int stage=1;stage<=4;stage++)
        {
            Tick(manager,8);
            Sprite displayed=map.GetSprite(position);
            Check(displayed.texture==tile.GetStageSprite(stage).texture && displayed.uv.SequenceEqual(tile.GetStageSprite(stage).uv),"Damage stage "+stage+" keeps its correct artwork/UVs");
            Check(collider.OverlapPoint(edge),"Damage stage "+stage+" keeps the full healthy collider");
            Check(Solid(center),"Damage stage "+stage+" remains a liquid obstacle");
        }
        var sourceShape=new List<Vector2>(); tile.GetStageSprite(4).GetPhysicsShape(0,sourceShape);
        Check(sourceShape.All(p=>Mathf.Abs(p.x)<.1f),"Runtime collision overrides do not modify imported damaged sprite shapes");
        for(int i=0;i<30;i++) Physics2D.Simulate(1f/60f);
        Check(body.position.y>5f,"Character stays supported on the sparse final stage");
        Tick(manager,12);
        Check(!map.HasTile(position) && !collider.OverlapPoint(edge),"Full burn removes the tile and its collider together");
        Check(!Solid(center),"Liquid can enter the newly cleared plant space");
        for(int i=0;i<60;i++) Physics2D.Simulate(1f/60f);
        Check(body.position.y<4f,"Character falls through only after complete burning");
        Cleanup();

        tile=MakeTile(false); manager=MakeMap(tile); Finish(manager,center);
        Check(manager.GetComponent<Tilemap>().HasTile(position),"Optional charred remnant stays visible");
        Check(!manager.GetComponent<TilemapCollider2D>().OverlapPoint(center) && !Solid(center),"Charred remnant releases collision at full burn");
        Cleanup();

        tile=MakeTile(); manager=MakeMap(tile);
        var wall=new GameObject("Independent wall",typeof(BoxCollider2D)); roots.Add(wall);
        wall.transform.position=new Vector3(4.5f,4.5f,0f); Physics2D.SyncTransforms();
        world.RebakeSolidsFromPhysics(); Finish(manager,center);
        Check(wall.GetComponent<BoxCollider2D>().OverlapPoint(center) && Solid(center),"Burning a plant leaves an overlapping wall and its simulation blockage intact");
        Cleanup();

        tile=MakeTile(collision:Tile.ColliderType.Grid);
        var far=new Vector3Int(20,4,0); manager=MakeMap(tile,new[]{position,far},true);
        var composite=manager.GetComponent<CompositeCollider2D>();
        Check(composite.OverlapPoint(center) && composite.OverlapPoint(new Vector2(20.5f,4.5f)),"Merged collider initially contains both distant plants (paths "+composite.pathCount+", shapes "+composite.shapeCount+", bounds "+composite.bounds+")");
        world.Ignite(center,0); world.Ignite(new Vector2(20.53f,4.53f),0); Tick(manager,43);
        Check(!composite.OverlapPoint(center) && !composite.OverlapPoint(new Vector2(20.5f,4.5f)),"Manual composite geometry updates after both plants disappear");
        Check(!Solid(center) && !Solid(new Vector2(20.53f,4.53f)),"Both distant openings refresh in the simulation");
        Check(world.LastLocalSolidRefreshCellCount>0 && world.LastLocalSolidRefreshCellCount<500,
            "Separated removals refresh only local cells ("+world.LastLocalSolidRefreshCellCount+" of "+world.Width*world.Height+")");
        Cleanup();

        tile=MakeTile(); tile.GetStageSprite(0).OverridePhysicsShape(new[]{new[]{new Vector2(1.6f,1.6f),new Vector2(1.6f,14.4f),new Vector2(6.4f,14.4f),new Vector2(6.4f,1.6f)}});
        var rotation=Matrix4x4.TRS(Vector3.zero,Quaternion.Euler(0,0,90),new Vector3(-1,1,1));
        manager=MakeMap(tile,tileTransform:rotation); Vector2 rotatedPoint=new Vector2(4.5f,4.75f);
        Check(manager.GetComponent<TilemapCollider2D>().OverlapPoint(rotatedPoint),"Painted flip and rotation also transform the healthy collision shape");
        world.Ignite(new Vector2(4.47f,4.47f),0); Tick(manager,33);
        Check(manager.GetComponent<TilemapCollider2D>().OverlapPoint(rotatedPoint),"Rotated collision survives late damage stages");
        manager.enabled=false;
        Check(manager.GetComponent<Tilemap>().GetTile(position)==tile && Solid(rotatedPoint),"Disabling restores original sprite and collision consistently");
        manager.enabled=true; Finish(manager,new Vector2(4.47f,4.47f));
        Check(!manager.GetComponent<Tilemap>().HasTile(position) && !Solid(rotatedPoint),"Re-enabled rotated plant can burn away without stale obstacles");
        Cleanup();

        tile=MakeTile(collision:Tile.ColliderType.None); manager=MakeMap(tile);
        Check(!manager.GetComponent<TilemapCollider2D>().OverlapPoint(center) && !Solid(center),"None keeps decorative plants walk-through even on a collider map");
        Cleanup();
        tile=MakeTile(); manager=MakeMap(tile,collider:false);
        Check(!Solid(center),"Maps without Tilemap Collider 2D retain their previous non-solid behavior");
        Cleanup();

        var example=AssetDatabase.LoadAssetAtPath<BurnablePlantTile>("Assets/Game/Tiles/BurnablePlants/Example_Vine_HealthyOnly.asset");
        Check(example.CollisionType==Tile.ColliderType.Sprite,"Existing example assets inherit the Sprite collision default");
        manager=MakeMap(example);
        Check(manager.GetComponent<TilemapCollider2D>().shapeCount>0,"The current vine spritesheet generates real sprite collision");
        Cleanup();

        tile=MakeTile(collision:Tile.ColliderType.Grid);
        var many=new List<Vector3Int>();
        for(int y=1;y<=25;y++) for(int x=1;x<=40;x++) many.Add(new Vector3Int(x,y,0));
        int before=FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;
        var watch=System.Diagnostics.Stopwatch.StartNew();
        manager=MakeMap(tile,many.ToArray(),gridSize:.25f); watch.Stop();
        Check(manager.RegisteredPlantCount==1000,"1000 collidable plants register on a single map");
        Check(FindObjectsByType<Transform>(FindObjectsInactive.Include).Length-before==2,"1000 solid plants add only the Grid and Tilemap GameObjects");
        Check(manager.GetComponentsInChildren<Collider2D>().Length==1,"1000 solid plants use one Tilemap Collider 2D component");
        File.AppendAllText("ValidationResults/plant-collision.txt","MEASURE: 1000 Grid-collider plants registered in "+watch.ElapsedMilliseconds+" ms; this is not a full-level FPS benchmark.\n");
        Cleanup();
        foreach(var asset in assets) if(asset!=null) Destroy(asset);
        Destroy(world.gameObject);
        Physics2D.simulationMode=previousMode;
        File.AppendAllText("ValidationResults/plant-collision.txt","ALL COLLISION CHECKS PASSED\n");
        yield return null;
        new GameObject("Existing plant checks").AddComponent<PlantCheckRunner>();
        Destroy(gameObject);
    }
}
