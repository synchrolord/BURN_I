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
public static class PlantCheck
{
    static PlantCheck()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PlantCheck.Run", false))
            {
                SessionState.SetBool("PlantCheck.Run", false);
                new GameObject("Plant regression runner").AddComponent<PlantCheckRunner>();
            }
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Use an isolated batch project.");
        Directory.CreateDirectory("ValidationResults");
        File.WriteAllText("ValidationResults/plants.txt", "");
        Sprite exampleSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tiles/Vines/plants.png").OfType<Sprite>().First(s => s.name == "plants_0");
        var example = AssetDatabase.LoadAssetAtPath<BurnablePlantTile>("Assets/Game/Tiles/BurnablePlants/Example_Vine_HealthyOnly.asset");
        if (example == null)
        {
            Selection.objects = new UnityEngine.Object[] { exampleSprite };
            BurnablePlantEditor.CreateFromSelection();
            example = Selection.activeObject as BurnablePlantTile;
            string examplePath = AssetDatabase.GetAssetPath(example);
            if (AssetDatabase.RenameAsset(examplePath, "Example_Vine_HealthyOnly") != "") throw new Exception("Could not name example asset.");
        }
        if (example == null || !example.HasBakedMask) throw new Exception("Editor tile creation did not produce a baked tile.");
        File.AppendAllText("ValidationResults/plants.txt", "PASS: Editor selection tool creates a usable baked tile from the current spritesheet\n");
        Selection.objects = Array.Empty<UnityEngine.Object>();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.LockReloadAssemblies();
        SessionState.SetBool("PlantCheck.Run", true);
        EditorApplication.EnterPlaymode();
    }
}

public sealed class PlantCheckRunner : MonoBehaviour
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        File.AppendAllText("ValidationResults/plants.txt", "PASS: " + message + "\n");
    }
    private CellWorld2D world;
    private readonly List<GameObject> maps = new List<GameObject>();

    BurnablePlantTile Tile(Vector2[] samples, float seconds = 2f)
    {
        var tile = ScriptableObject.CreateInstance<BurnablePlantTile>();
        var texture = new Texture2D(16,16);
        Sprite[] sprites = new Sprite[5];
        for (int i=0;i<5;i++) sprites[i] = Sprite.Create(texture, new Rect(0,0,16,16), Vector2.one * .5f, 16);
        tile.SetArtwork(sprites); tile.SetBakedMask(samples, "test"); Set(tile,"burnSeconds",seconds);
        return tile;
    }
    BurnablePlantMap2D Map(BurnablePlantTile tile, Vector3Int[] positions, Matrix4x4? matrix = null)
    {
        var root = new GameObject("Test plant grid",typeof(Grid)); maps.Add(root);
        var go = new GameObject("Plants",typeof(Tilemap),typeof(TilemapRenderer)); go.SetActive(false);
        go.transform.SetParent(root.transform,false);
        var map=go.GetComponent<Tilemap>();
        foreach (Vector3Int position in positions)
        {
            map.SetTile(position,tile);
            if (matrix.HasValue) map.SetTransformMatrix(position,matrix.Value);
        }
        var manager=go.AddComponent<BurnablePlantMap2D>(); manager.World=world;
        go.SetActive(true);
        return manager;
    }
    float Progress(BurnablePlantMap2D manager, Vector3Int position)
    {
        Check(manager.TryGetBurnProgress(position,out float progress),"Tracked plant instance at " + position);
        return progress;
    }
    void Tick(int count=1) { for(int i=0;i<count;i++) world.StepOnce(); foreach(var root in maps) if(root!=null) Call(root.GetComponentInChildren<BurnablePlantMap2D>(),"LateUpdate"); }
    void Cleanup()
    {
        foreach(var root in maps) DestroyImmediate(root);
        maps.Clear(); world.ClearAllMaterials();
        Array.Clear((bool[])Get(world,"solid"),0,world.Width*world.Height);
        Check(world.PlantFuelCellCount==0,"Unregister removes anchored fuel");
    }

    private IEnumerator Start()
    {
        Application.logMessageReceived += (message,trace,type) =>
        {
            if(type==LogType.Exception || type==LogType.Error)
            {
                File.AppendAllText("ValidationResults/plants.txt","FAIL: "+message+"\n"+trace);
                EditorApplication.Exit(1);
            }
        };
        world=new GameObject("Test CellWorld").AddComponent<CellWorld2D>();
        Set(world,"width",320); Set(world,"height",180); Set(world,"cellSize",.125f);
        Set(world,"startPaused",true); Set(world,"fireSpreadChance",1f); Set(world,"fireSpreadEveryTicks",1);
        yield return null;
        var origin=new Vector3Int(3,3,0);
        Vector2 point=new Vector2(3.51f,3.51f);
        var single=Tile(new[]{new Vector2(.03125f,.03125f)});
        var manager=Map(single,new[]{origin,new Vector3Int(7,3,0)});
        Check(manager.RegisteredPlantCount==2 && world.PlantFuelCellCount==2,"Two thin one-pixel plants register without per-piece GameObjects");
        Check(world.Ignite(point,0)==1,"Existing flame API ignites plant");
        Tick();
        Check(Mathf.Abs(Progress(manager,origin)-.025f)<.0001f,"Two fire passes consume only one simulation step of plant fuel");
        Check(Progress(manager,new Vector3Int(7,3,0))==0f,"Shared tile asset has independent per-instance burn state");
        Check(world.GetTotalFuelUnits()==0,"Plant fuel never becomes falling liquid fuel");
        Tilemap tilemap=manager.GetComponent<Tilemap>();
        for(int stage=1;stage<=4;stage++)
        {
            while(ProgressNoCheck(manager,origin)<stage*.2f+.001f) Tick();
            Check(tilemap.GetSprite(origin)==single.GetStageSprite(stage),"Damage selects sprite stage "+stage);
        }
        Check(tilemap.HasTile(origin),"Last sparse sprite remains visible before complete burn");
        Tick(10);
        Check(!tilemap.HasTile(origin),"Plant disappears after all fuel is consumed");
        Check(tilemap.GetTile(new Vector3Int(7,3,0))==single,"Burning one tile never changes an untouched copy");
        Cleanup();

        var charred=Tile(new[]{Vector2.zero},.5f); Set(charred,"disappearWhenBurned",false);
        manager=Map(charred,new[]{origin}); world.Ignite(point,0); Tick(12);
        Check(manager.GetComponent<Tilemap>().GetSprite(origin)==charred.GetStageSprite(4) && world.PlantFuelCellCount==0,"Optional charred remains stop providing fuel");
        Cleanup();

        manager=Map(single,new[]{origin}); world.Ignite(point,0); Tick(4);
        float damage=ProgressNoCheck(manager,origin);
        int water=world.ApplyWaterImpact(point,3);
        Check(water==3 && world.GetFireEnergy(point)==0,"Three water units extinguish three fire energy");
        Check(world.Ignite(point,0)==0,"Wet plant rejects immediate flame reignition");
        Tick(10);
        Check(Mathf.Abs(ProgressNoCheck(manager,origin)-damage)<.0001f,"Extinguished plant retains damage and stops burning");
        manager.enabled=false;
        Check(world.PlantFuelCellCount==0 && manager.GetComponent<Tilemap>().GetTile(origin)==single,"Disable restores original tiles and releases fuel");
        manager.enabled=true;
        Check(world.PlantFuelCellCount==1 && ProgressNoCheck(manager,origin)==0,"Re-enable cleanly resets registration");
        Cleanup();

        manager=Map(single,new[]{origin});
        world.TryWorldToCell(point,out Vector2Int solidCell);
        ((bool[])Get(world,"solid"))[solidCell.x+solidCell.y*world.Width]=true;
        Check(world.Ignite(point,0)==1,"Wall-backed vine can ignite");
        Tick(3);
        Check(world.GetFireEnergy(point)==3 && world.GetTotalWaterUnits()==0,"Wall-backed plant sustains fire on solid terrain");
        Check(world.ApplyPlantWaterImpact(point,2)==2 && world.GetFireEnergy(point)==1,"Wall contact spends exact finite cooling payload");
        Check(world.ApplyPlantWaterImpact(point,1)==1 && world.GetFireEnergy(point)==0,"Second wall contact extinguishes remaining energy");
        Check(world.AddWater(point,0,8)==0,"Burnable wall remains impermeable to liquid");
        Check(world.Ignite(point,0)==0,"Wet wall plant rejects ignition");
        Tick(45);
        Check(world.Ignite(point,0)==1,"Plant becomes ignitable after simulation wet time");
        var wall = new GameObject("Water contact wall", typeof(BoxCollider2D));
        wall.transform.position = new Vector3(4f,3.53125f,0f);
        var player = new GameObject("Water contact tool"); player.SetActive(false);
        player.transform.position = new Vector3(2.5f,3.53125f,0f);
        var tool = player.AddComponent<PressureTool2D>();
        Set(tool,"cellWorld",world); Set(tool,"muzzle",player.transform); Set(tool,"obstacleMask",(LayerMask)1);
        Set(tool,"waterDepositJitterInCells",0f); Set(tool,"waterGravityScale",0f); Set(tool,"waterDrag",0f);
        Set(tool,"showDebugOverlay",false);
        object jet=Get(tool,"jet"); Set(jet,"angleDegrees",0f); Set(jet,"speedRandomness",0f);
        player.SetActive(true); tool.enabled=false;
        Physics2D.SyncTransforms();
        Call(tool,"SpawnProbe",jet,0);
        object shot=((Array)Get(tool,"probes")).GetValue(0); Set(shot,"remainingWater",3);
        Call(tool,"UpdateActiveProbes",.1f);
        Check(world.GetFireEnergy(point)==0 && (int)Get(shot,"remainingWater")==0,"Real swept water shot extinguishes wall plant and spends its payload once");
        Check(world.GetTotalWaterUnits()==0,"Wall cooling does not duplicate the shot into free liquid");
        DestroyImmediate(player); DestroyImmediate(wall);
        Cleanup();

        // Samples straddle the sprite origin diagonally at adjacent simulation cells.
        var diagonal=Tile(new[]{new Vector2(-.06f,-.06f),new Vector2(.06f,.06f),new Vector2(.19f,.19f)},2f);
        manager=Map(diagonal,new[]{origin});
        world.Ignite(new Vector2(3.44f,3.44f),0); Tick(4);
        Check(world.GetFireEnergy(new Vector2(3.69f,3.69f))>0,"Fire crosses a thin diagonal stem at cell corners");
        Check(world.GetFireEnergy(new Vector2(3.19f,3.69f))==0,"Empty nearby cells do not become anchored fire");
        Cleanup();

        var horizontalSamples=Enumerable.Range(0,16).Select(x=>new Vector2((x+.5f)/16f-.5f,.03125f)).ToArray();
        var straight=Tile(horizontalSamples,2f);
        manager=Map(straight,new[]{origin,new Vector3Int(4,3,0),new Vector3Int(7,3,0)});
        world.Ignite(new Vector2(3.05f,3.53f),0); Tick(12);
        Check(ProgressNoCheck(manager,new Vector3Int(4,3,0))>0,"Fire spreads between touching painted vine pieces");
        Check(ProgressNoCheck(manager,new Vector3Int(7,3,0))==0,"A separated vine does not ignite across an air gap");
        Cleanup();

        var matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.Euler(0,0,90),new Vector3(-1,1,1));
        manager=Map(straight,new[]{origin},matrix);
        Check(world.PlantFuelCellCount==8,"Rotated/flipped thin sprite has the expected footprint");
        world.Ignite(new Vector2(3.46f,3.05f),0); Tick(14);
        Check(ProgressNoCheck(manager,origin)>0.2f,"Rotated sprite burns along transformed shape");
        Check(manager.GetComponent<Tilemap>().GetTransformMatrix(origin)==matrix,"Damage stages preserve painted flip and rotation");
        Cleanup();

        manager=Map(single,new[]{origin}); var overlap=Map(single,new[]{origin});
        Check(world.PlantFuelCellCount==1,"Overlapping plants share a spatial fuel bucket");
        world.Ignite(point,0); Tick(4);
        Check(ProgressNoCheck(manager,origin)>0 && Mathf.Abs(ProgressNoCheck(manager,origin)-ProgressNoCheck(overlap,origin))<.0001f,"Overlapping owners each receive burn damage");
        DestroyImmediate(maps[0]); maps.RemoveAt(0); Tick(2);
        Check(world.PlantFuelCellCount==1 && ProgressNoCheck(overlap,origin)>.1f,"Removing one overlapping owner preserves the other");
        Cleanup();

        // Exercise source sheet baking without enabling Read/Write on the importer.
        Sprite source=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tiles/Vines/plants.png").OfType<Sprite>().First(s=>s.name=="plants_0");
        var actual=ScriptableObject.CreateInstance<BurnablePlantTile>(); actual.SetArtwork(new[]{source,null,null,null,null});
        Check(BurnablePlantEditor.BakeMask(actual) && actual.FuelSamples.Length>0,"Existing non-readable plants.png bakes from source alpha");
        Check(!((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source))).isReadable,"Baking leaves source texture Read/Write disabled");
        manager=Map(actual,new[]{origin});
        Check(manager.RegisteredFuelSampleCount>0,"Actual current vine artwork maps into simulation fuel");
        Cleanup();

        var positions=new List<Vector3Int>();
        for(int y=1;y<=20;y++) for(int x=1;x<=50;x++) positions.Add(new Vector3Int(x,y,0));
        // World bounds cover only 38x20 here; use smaller cells in the tile grid instead.
        var largeRoot=new GameObject("Large grid",typeof(Grid)); maps.Add(largeRoot);
        largeRoot.GetComponent<Grid>().cellSize=new Vector3(.5f,.5f,1f);
        var large=new GameObject("1000 plants",typeof(Tilemap),typeof(TilemapRenderer)); large.SetActive(false); large.transform.SetParent(largeRoot.transform,false);
        var largeMap=large.GetComponent<Tilemap>(); foreach(var pos in positions) largeMap.SetTile(pos,single);
        int objectsBefore=FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var largeManager=large.AddComponent<BurnablePlantMap2D>(); largeManager.World=world; large.SetActive(true); timer.Stop();
        Check(largeManager.RegisteredPlantCount==1000,"One manager registers 1000 painted plants");
        int objectsAfter=FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;
        Check(objectsAfter==objectsBefore,$"1000 plants create no per-piece GameObjects (before {objectsBefore}, after {objectsAfter})");
        long registrationMs=timer.ElapsedMilliseconds; timer.Restart(); Tick(100); timer.Stop();
        Check(world.GetTotalFuelUnits()==0 && world.GetFireEnergy(point)==0,"Dormant vegetation remains passive through 100 simulation ticks");
        File.AppendAllText("ValidationResults/plants.txt",$"MEASURE: 1000 single-cell plants: registration {registrationMs} ms; 100 dormant simulation ticks {timer.ElapsedMilliseconds} ms (batch editor, not a frame-rate guarantee).\n");
        Cleanup();
        File.AppendAllText("ValidationResults/plants.txt","ALL PLANT CHECKS PASSED\n");
        Destroy(world.gameObject);
        yield return null;
        new GameObject("Pressure regression runner").AddComponent<PressureCheckRunner>();
        Destroy(gameObject);
    }
    static float ProgressNoCheck(BurnablePlantMap2D manager,Vector3Int position) { manager.TryGetBurnProgress(position,out float p); return p; }
}
