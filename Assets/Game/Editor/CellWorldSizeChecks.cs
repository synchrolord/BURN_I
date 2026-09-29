using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

//check grid sizes, material placement, and changes made while running
//run CellWorldSizeChecks.Run from Unity's -executeMethod option
//RunBaseline checks the original bugs before applying the fix
public static class CellWorldSizeChecks
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Report = new List<string>();
    private static readonly List<string> Errors = new List<string>();
    private static int failures;

    public static void Run() => RunSuite(false);
    public static void RunBaseline() => RunSuite(true);

    private static void RunSuite(bool baseline)
    {
        Report.Clear();
        Errors.Clear();
        failures = 0;
        Application.logMessageReceived += CaptureLog;
        Report.Add("CellWorld2D size checks: " + (baseline ? "original-bug baseline" : "regression acceptance"));
        Report.Add("Unity " + Application.unityVersion + "; maxTextureSize=" + SystemInfo.maxTextureSize);
        int[,] sizes = { { 240, 136 }, { 480, 272 }, { 960, 544 }, { 900, 600 }, { 1800, 1200 }, { 3600, 2400 } };
        try
        {
            for (int i = 0; i < sizes.GetLength(0); i++)
            {
                int width = sizes[i, 0], height = sizes[i, 1];
                bool expectRejected = baseline && i >= 4;
                RunCase(width + "x" + height, () =>
                {
                    if (expectRejected) CheckRejectedSize(width, height);
                    else CheckSize(width, height);
                });
                GC.Collect();
            }
            RunCase("live Inspector dimension mutation", () => CheckLiveMutation(baseline));
            RunCase("width above 4096 fails safely", () => CheckRejectedSize(4097, 16));
            RunCase("height above 4096 fails safely", () => CheckRejectedSize(16, 4097));
            RunCase("oversized request fails safely", () => CheckRejectedSize(100000, 100000));
        }
        finally
        {
            Application.logMessageReceived -= CaptureLog;
            Report.Add("RESULT: " + (failures == 0 ? "PASS" : "FAIL") + "; failed cases=" + failures);
            string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "size-checks.txt");
            File.WriteAllLines(output, Report);
            Debug.Log(string.Join("\n", Report));
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
    }

    private static void RunCase(string name, Action body)
    {
        Errors.Clear();
        try
        {
            body();
            Require(Errors.Count == 0, "Unexpected Unity errors: " + string.Join(" | ", Errors));
            Report.Add("PASS " + name);
        }
        catch (Exception e)
        {
            failures++;
            Report.Add("FAIL " + name + ": " + e);
            if (Errors.Count > 0) Report.Add("  Unity errors: " + string.Join(" | ", Errors));
        }
    }

    private static void CheckSize(int width, int height)
    {
        CellWorld2D world = CreateWorld(width, height);
        try
        {
            Invoke(world, "InitializeGrid");
            Require(world.IsInitialized && !world.InitializationFailed && world.enabled,
                "Initialization rejected the requested dimensions.");
            CheckGeometry(world, width, height);

            int waterX = width - 10, waterY = height - 10;
            int fireX = width - 20, fireY = height - 10;
            Vector2 waterPoint = world.CellToWorld(waterX, waterY);
            Vector2 firePoint = world.CellToWorld(fireX, fireY);
            CheckRoundTrip(world, waterPoint, waterX, waterY);
            CheckRoundTrip(world, firePoint, fireX, fireY);
            Require(world.GetActiveChunkCount() == 0, "An empty world should have no awake chunks.");

            //check the same shot paths used by particles
            Require(world.ApplyWaterShot(PathAt(waterPoint), 8, true) == 0,
                "Expired water shot did not consume its remaining units.");
            Require(world.GetWaterFraction(waterPoint) == 1f, "Water was not deposited at the shot endpoint.");
            world.ApplyFlameShot(PathAt(firePoint), 0);
            Require(world.GetFireEnergy(firePoint) == 3, "Flame shot did not ignite its endpoint.");
            Require(world.GetActiveChunkCount() > 0, "Shot impacts did not wake simulation chunks.");
            Require(world.GetActiveChunkCount() <= 8, "A pair of point impacts woke unrelated chunks.");

            Invoke(world, "RenderNow");
            Texture2D texture = Get<Texture2D>(world, "runtimeTexture");
            Color waterColor = texture.GetPixel(waterX, waterY);
            Require(waterColor.a > 0.5f && waterColor.b > waterColor.r,
                "Water's expected texture pixel is not blue and visible.");
            Color fireColor = texture.GetPixel(fireX, fireY);
            Require(fireColor.a > 0.5f && fireColor.r > fireColor.b,
                "Fire's expected texture pixel is not warm and visible.");
            Require(texture.GetPixel(10, 10).a == 0f, "A remote empty pixel was unexpectedly painted.");
            Require(Get<byte[]>(world, "water")[waterX + waterY * width] == 8,
                "Water storage uses a different stride than the rendered texture.");
            Require(Get<byte[]>(world, "fire")[fireX + fireY * width] == 3,
                "Fire storage uses a different stride than the rendered texture.");

            //use three water units to extinguish fire and keep the remaining two
            int remaining = world.ApplyWaterShot(PathAt(firePoint), 5, false);
            Require(remaining == 0 && world.GetFireEnergy(firePoint) == 0,
                "Water shot did not extinguish the fire it crossed.");
            Require(Get<byte[]>(world, "water")[fireX + fireY * width] == 2,
                "Extinguishing did not preserve the remaining water units.");

            world.ClearAllMaterials();
            Array.Clear(Get<byte[]>(world, "chunkAwakeTicks"), 0, Get<byte[]>(world, "chunkAwakeTicks").Length);
            Require(world.AddWater(waterPoint, 0, 8) == 8, "Could not refill water for the simulation check.");
            int beforeTick = Get<int>(world, "tickNumber");
            world.StepOnce();
            Require(Get<int>(world, "tickNumber") == beforeTick + 1, "Simulation tick did not advance.");
            Require(world.GetWaterFraction(waterPoint) == 0f, "Awake water did not leave its source cell.");
            Require(world.GetWaterFraction(world.CellToWorld(waterX, waterY - 1)) == 1f,
                "Awake water did not fall exactly one cell in one tick.");
            Require(world.GetTotalWaterUnits() == 8, "Simulation lost water units.");
            Invoke(world, "RenderNow");
            Require(texture.GetPixel(waterX, waterY).a == 0f && texture.GetPixel(waterX, waterY - 1).a > 0.5f,
                "Rendered water did not follow the simulated cell.");
        }
        finally { DestroyWorld(world); }
    }

    private static void CheckLiveMutation(bool baseline)
    {
        CellWorld2D world = CreateWorld(240, 136);
        try
        {
            Invoke(world, "InitializeGrid");
            Require(world.IsInitialized, "Live-mutation setup could not initialize.");
            float initializedCellSize = world.CellSize;
            Set(world, "width", 480);
            Set(world, "height", 272);
            //call OnValidate like the Inspector does when changing fields
            MethodInfo validate = typeof(CellWorld2D).GetMethod("OnValidate", Fields);
            if (validate != null) validate.Invoke(world, null);
            Vector2 point = world.CellToWorld(10, 10);
            Require(world.AddWater(point, 0, 8) == 8, "A live Inspector change disabled previously working water.");
            Invoke(world, "RenderNow");
            Texture2D texture = Get<Texture2D>(world, "runtimeTexture");
            if (baseline)
            {
                Require(world.Width == 480 && texture.width == 240, "Expected original width/texture mismatch was not reproduced.");
                Require(Get<byte[]>(world, "water")[4810] == 8, "Expected erroneous water index 4810 was not reproduced.");
                Require(texture.GetPixel(10, 10).a == 0f && texture.GetPixel(10, 20).a > 0.5f,
                    "Expected off-position water pixel at (10,20) was not reproduced.");
                bool caught = false;
                try { world.AddWater(world.CellToWorld(470, 260), 0, 8); }
                catch (IndexOutOfRangeException) { caught = true; }
                Require(caught, "Expected out-of-range write attempt in the newly advertised bounds was not reproduced.");
                Report.Add("  Reproduced: intended cell (10,10), array index 4810, texture pixel (10,20); far-cell API throws IndexOutOfRangeException.");
                return;
            }

            CheckGeometry(world, world.Width, world.Height);
            Require(texture.GetPixel(10, 10).a > 0.5f && texture.GetPixel(10, 20).a == 0f,
                "Live mutation redirected water to the wrong visible pixel.");
            Vector2 firePoint = world.CellToWorld(12, 10);
            Require(world.Ignite(firePoint, 0) == 1 && world.GetFireEnergy(firePoint) == 3,
                "Fire APIs failed after a live Inspector change.");
            Require(world.ApplyWaterImpact(firePoint, 3) == 3 && world.GetFireEnergy(firePoint) == 0,
                "Extinguishing failed after a live Inspector change.");
            //reject positions outside the running grid
            Vector2 farPoint = world.CellToWorld(470, 260);
            bool isFarInside = world.TryWorldToCell(farPoint, out Vector2Int farCell);
            int accepted = world.AddWater(farPoint, 0, 8);
            Require(isFarInside ? accepted == 8 : accepted == 0,
                "Post-mutation world bounds and material writes disagree.");
            Set(world, "cellSize", initializedCellSize * 2f);
            if (validate != null) validate.Invoke(world, null);
            Require(Mathf.Approximately(Get<Sprite>(world, "runtimeSprite").pixelsPerUnit, 1f / world.CellSize),
                "Live Cell Size change left the rendered scale and world-coordinate mapping inconsistent.");
            world.StepOnce();
            Invoke(world, "RenderNow");
            CheckGeometry(world, world.Width, world.Height);
        }
        finally { DestroyWorld(world); }
    }

    private static void CheckRejectedSize(int width, int height)
    {
        CellWorld2D world = CreateWorld(width, height);
        try
        {
            Invoke(world, "InitializeGrid");
            Require(!world.IsInitialized && world.InitializationFailed && !world.enabled,
                "Invalid dimensions should fail before simulation allocation.");
            Require(Get<byte[]>(world, "water") == null && Get<Texture2D>(world, "runtimeTexture") == null,
                "Rejected dimensions left allocated runtime storage.");
            Require(Errors.Count == 1 && Errors[0].Contains("CellWorld"),
                "Dimension rejection did not produce exactly one explanatory CellWorld error.");
            Report.Add("  Expected rejection: " + Errors[0]);
            Errors.Clear();
            Require(world.AddWater(Vector2.zero, 0, 8) == 0 && world.Ignite(Vector2.zero, 0) == 0,
                "Material APIs accepted writes after failed initialization.");
            world.StepOnce();
            Invoke(world, "RenderNow");
        }
        finally { DestroyWorld(world); }
    }

    private static CellWorld2D CreateWorld(int width, int height)
    {
        GameObject go = new GameObject("CellWorld size regression " + width + "x" + height);
        go.transform.position = new Vector3(-28f, -19f, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, 17f);
        go.transform.localScale = new Vector3(0.8f, 1.3f, 1f);
        CellWorld2D world = go.AddComponent<CellWorld2D>();
        Set(world, "width", width);
        Set(world, "height", height);
        Set(world, "solidMask", (LayerMask)0);
        return world;
    }

    private static void CheckGeometry(CellWorld2D world, int width, int height)
    {
        Require(world.Width == width && world.Height == height, "Reported grid dimensions changed.");
        int count = checked(width * height);
        foreach (string name in new[] { "water", "fuel", "smoke", "fire", "solid", "movedWater", "movedFuel", "movedSmoke", "pendingFire", "pixels" })
            Require(Get<Array>(world, name).Length == count, name + " does not match the current dimensions.");
        Texture2D texture = Get<Texture2D>(world, "runtimeTexture");
        Require(texture.width == width && texture.height == height, "Texture dimensions disagree with the grid.");
        Sprite sprite = Get<Sprite>(world, "runtimeSprite");
        Require(sprite.rect.width == width && sprite.rect.height == height, "Sprite rectangle disagrees with the grid.");
        Require(world.IncludedCellCount == count, "Included cell count disagrees with the rectangular grid.");
        int chunkSize = Get<int>(world, "chunkSize");
        int chunksX = (width + chunkSize - 1) / chunkSize, chunksY = (height + chunkSize - 1) / chunkSize;
        Require(Get<int>(world, "chunkCountX") == chunksX && Get<int>(world, "chunkCountY") == chunksY,
            "Chunk dimensions disagree with the grid.");
        Require(Get<byte[]>(world, "chunkAwakeTicks").Length == chunksX * chunksY, "Chunk storage has the wrong size.");
    }

    private static void CheckRoundTrip(CellWorld2D world, Vector2 point, int x, int y)
    {
        Require(world.TryWorldToCell(point, out Vector2Int cell) && cell == new Vector2Int(x, y),
            "World/cell round-trip failed at (" + x + "," + y + ").");
    }

    private static CellWorld2D.ShotPath PathAt(Vector2 point) => new CellWorld2D.ShotPath
    {
        startPosition = point,
        endPosition = point,
        hitObstacle = false
    };

    private static void DestroyWorld(CellWorld2D world)
    {
        if (world == null) return;
        //remove the sprite and texture immediately because these checks run in edit mode
        Sprite sprite = Get<Sprite>(world, "runtimeSprite");
        Texture2D texture = Get<Texture2D>(world, "runtimeTexture");
        Set(world, "runtimeSprite", null);
        Set(world, "runtimeTexture", null);
        world.GetComponent<SpriteRenderer>().sprite = null;
        if (sprite != null) Object.DestroyImmediate(sprite);
        if (texture != null) Object.DestroyImmediate(texture);
        Object.DestroyImmediate(world.gameObject);
    }

    private static T Get<T>(CellWorld2D world, string name) => (T)typeof(CellWorld2D).GetField(name, Fields).GetValue(world);
    private static void Set(CellWorld2D world, string name, object value) => typeof(CellWorld2D).GetField(name, Fields).SetValue(world, value);
    private static void Invoke(CellWorld2D world, string name)
    {
        try { typeof(CellWorld2D).GetMethod(name, Fields).Invoke(world, null); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(condition);
    }
}
