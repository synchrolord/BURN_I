using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PressureCheck
{
    static PressureCheck()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PressureCheck.Run", false))
            {
                SessionState.SetBool("PressureCheck.Run", false);
                new GameObject("Regression runner").AddComponent<PressureCheckRunner>();
            }
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Run this scratch validation in an isolated batch project.");
        Directory.CreateDirectory("ValidationResults");
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                    throw new Exception("Missing script in " + path + ": " + child.name);
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Effects/PressureSpray.mat");
        if (material == null || ShaderUtil.ShaderHasError(material.shader)) throw new Exception("Spray shader failed import");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Package startup may request a reload during the first play frame.
        // Keep this short batch-only test atomic; the process exits afterward.
        EditorApplication.LockReloadAssemblies();
        SessionState.SetBool("PressureCheck.Run", true);
        EditorApplication.EnterPlaymode();
    }
}

public sealed class PressureCheckRunner : MonoBehaviour
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    static object[] Probes(PressureTool2D tool) => ((Array)Get(tool, "probes")).Cast<object>().Where(p => (bool)Get(p, "active")).ToArray();
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        File.AppendAllText("ValidationResults/check-result.txt", "PASS: " + message + "\n");
        Debug.Log("PRESSURE_CHECK PASS: " + message);
    }
    static void ResetTool(PressureTool2D tool, CellWorld2D world)
    {
        Call(tool, "OnDisable");
        world.ClearAllMaterials();
        tool.RefillAllResources();
        tool.UseWater();
    }

    static void AdvanceFireVisuals(CellWorld2D world, float seconds)
    {
        Set(world, "fireAnimationTime", (float)Get(world, "fireAnimationTime") + seconds);
        Call(world, "RenderNow");
    }

    static void CheckFireVisuals(CellWorld2D world, PressureTool2D tool)
    {
        var gradient = (Gradient)Get(tool, "flameColorOverLifetime");
        Require(gradient.colorKeys.Length == 5, "Flying flame has five blended lifetime colors");
        Color young = (Color)Call(tool, "EvaluateFlameColor", 0f);
        Color middle = (Color)Call(tool, "EvaluateFlameColor", .5f);
        Color old = (Color)Call(tool, "EvaluateFlameColor", .9f);
        Color end = (Color)Call(tool, "EvaluateFlameColor", 1f);
        Require(young.a > middle.a && middle.a > old.a && old.a > end.a && end.a == 0f,
            "Flame fades continuously to fully transparent at expiry");
        Color previous = young;
        float largestColorStep = 0f;
        for (int i = 1; i <= 600; i++)
        {
            Color current = (Color)Call(tool, "EvaluateFlameColor", i / 600f);
            largestColorStep = Mathf.Max(largestColorStep, ((Vector4)current - (Vector4)previous).magnitude);
            previous = current;
        }
        Require(largestColorStep < .015f, "Lifetime gradient has no jumps at its color stops");
        Set(world, "paused", false);
        Vector2 point = world.CellToWorld(30, 30);
        int index = 30 + 30 * world.Width;
        byte[] fire = (byte[])Get(world, "fire");
        world.ClearAllMaterials(); world.Ignite(point, 0, 3); Call(world, "RenderNow");
        var visuals = (Vector2[])Get(world, "fireVisuals");
        Require(visuals[index] == new Vector2(3f, 1f), "New grid fire appears immediately");
        fire[index] = 1; AdvanceFireVisuals(world, 1f / 60f);
        Require(visuals[index].x > 1f && visuals[index].x < 3f && visuals[index].y > .45f,
            "Grid heat and opacity blend instead of snapping between energy levels");
        Require(world.GetFireEnergy(point) == 1, "Visual interpolation does not modify simulated fire energy");
        AdvanceFireVisuals(world, .3f);
        Require(Mathf.Approximately(visuals[index].x, 1f) && Mathf.Approximately(visuals[index].y, .45f),
            "Cooling settles at the weak-fire color and opacity");
        fire[index] = 0; AdvanceFireVisuals(world, 1f / 60f);
        Require(visuals[index].y > 0f && (bool)Get(world, "hasVisibleFire"), "Extinguished fire keeps a short visual fade tail");
        AdvanceFireVisuals(world, .3f);
        Require(visuals[index].y == 0f && !(bool)Get(world, "hasVisibleFire"), "Fade ends fully transparent and stops requesting fire refreshes");
        world.Ignite(point, 0, 3); Call(world, "RenderNow"); fire[index] = 0;
        for (int i = 0; i < 6; i++) AdvanceFireVisuals(world, 1f / 60f);
        Vector2 at60 = visuals[index];
        world.ClearAllMaterials(); world.Ignite(point, 0, 3); Call(world, "RenderNow"); fire[index] = 0;
        for (int i = 0; i < 2; i++) AdvanceFireVisuals(world, 1f / 20f);
        Require(Vector2.Distance(at60, visuals[index]) < .0001f, "Fire fade has the same timing at 20 and 60 visual updates per second");
        Set(world, "paused", true);
        float clockBefore = (float)Get(world, "fireAnimationTime"); Vector2 pausedVisual = visuals[index];
        Call(world, "LateUpdate"); Call(world, "RenderNow");
        Require((float)Get(world, "fireAnimationTime") == clockBefore && visuals[index] == pausedVisual,
            "Pausing freezes the visual clock and fade without catch-up");
        world.EraseCircle(point, 0); Call(world, "RenderNow");
        Require(visuals[index] == Vector2.zero, "Erase removes a fading cell even after its simulated materials are empty");
        world.Ignite(point, 0, 3); Call(world, "RenderNow"); world.ClearAllMaterials(); Call(world, "RenderNow");
        Require(visuals[index] == Vector2.zero && !(bool)Get(world, "hasVisibleFire"), "Clear removes fire and its visual history immediately");
        Require((bool)Get(world, "smoothFireEveryFrame"), "Smooth fire animation is enabled without editing existing scene values");
    }
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        File.WriteAllText("ValidationResults/check-result.txt", "");
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                File.AppendAllText("ValidationResults/check-result.txt", "FAIL: " + message + "\n" + trace);
                EditorApplication.Exit(1);
            }
        };
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var mouse = InputSystem.AddDevice<Mouse>();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var world = new GameObject("Test world").AddComponent<CellWorld2D>();
        Set(world, "width", 160); Set(world, "height", 80); Set(world, "cellSize", .25f);
        Set(world, "startPaused", true); Set(world, "simulateAllChunks", true);
        var player = new GameObject("Test player"); player.SetActive(false);
        player.transform.position = new Vector3(5, 10, 0);
        var input = player.AddComponent<PlayerInput>();
        input.actions = InputActionAsset.FromJson(File.ReadAllText("Assets/Game/Settings/inputActions.inputactions"));
        input.defaultActionMap = "PLayer"; input.notificationBehavior = PlayerNotifications.SendMessages;
        var tool = player.AddComponent<PressureTool2D>();
        var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(player.transform, false);
        Set(tool, "cellWorld", world); Set(tool, "muzzle", muzzle); Set(tool, "showDebugOverlay", false);
        Set(tool, "unlimitedResourcesForTesting", false); Set(tool, "waterDepositJitterInCells", 0f);
        object jet = Get(tool, "jet"); Set(jet, "speedRandomness", 0f); Set(jet, "angleDegrees", 0f);
        player.SetActive(true);
        yield return null;
        tool.enabled = false; // Drive simulation with known time steps below.
        CheckFireVisuals(world, tool);
        input.user.UnpairDevices();
        UnityEngine.InputSystem.Users.InputUser.PerformPairingWithDevice(mouse, input.user);
        UnityEngine.InputSystem.Users.InputUser.PerformPairingWithDevice(keyboard, input.user);
        Require(input.currentActionMap != null && input.currentActionMap.enabled, "Player input map enabled");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); InputSystem.Update();
        Require(tool.CurrentMaterial == PressureTool2D.ToolMaterial.Flame, "Q switches water to flame through PlayerInput");
        InputSystem.Update();
        Require(tool.CurrentMaterial == PressureTool2D.ToolMaterial.Flame, "Holding Q does not repeatedly toggle");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        Require(tool.CurrentMaterial == PressureTool2D.ToolMaterial.Flame, "Releasing Q does not toggle");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); InputSystem.Update();
        Require(tool.CurrentMaterial == PressureTool2D.ToolMaterial.Water, "Next Q press switches back to water");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); InputSystem.Update();
        Require(tool.IsSpraying, "Left mouse reaches OnSpray");
        InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.Update();
        Require(!tool.IsSpraying, "Mouse release stops spraying");
        InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 120) }); InputSystem.Update();
        Require((int)Get(tool, "currentNozzleIndex") == 2, "Scroll cycles nozzles");
        InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.Update();
        Set(tool, "currentNozzleIndex", 1);
        tool.SetSpraying(true); Call(tool, "UpdateEmission", 0f); tool.SetSpraying(false);
        object drop = Probes(tool).Single();
        Require(Mathf.Abs(((Vector2)Get(drop, "velocity")).x - 26f) < .001f, "Water jet launches at 26 units per second");
        Require(tool.CurrentWater == 299 && world.GetTotalWaterUnits() == 0, "One shot spends one resource and keeps its payload airborne");
        Call(tool, "UpdateActiveProbes", .05f);
        Require(((Vector2)Get(drop, "position")).x > 6f && world.GetTotalWaterUnits() == 0, "Water travels forward before depositing");
        Call(tool, "UpdateSprayVisuals");
        Require(tool.ActiveProbeCount == 1 && ((MeshRenderer)Get(tool, "sprayRenderer")).enabled, "Active water has a visible pooled mesh");
        Call(tool, "UpdateActiveProbes", 2f);
        Require(world.GetTotalWaterUnits() == 4 && Probes(tool).Length == 0, "Water expires into exactly its four-unit payload");
        int total = world.GetTotalWaterUnits();
        for (int i = 0; i < 60; i++) world.StepOnce();
        Require(world.GetTotalWaterUnits() == total, "Liquid simulation conserves deposited water over 60 ticks");
        ResetTool(tool, world);
        var wall = new GameObject("Thin wall"); wall.transform.position = new Vector3(8, 10, 0);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(.05f, 10);
        player.AddComponent<BoxCollider2D>().size = Vector2.one * 2f;
        Set(tool, "obstacleMask", (LayerMask)1); Physics2D.SyncTransforms();
        tool.SetSpraying(true); Call(tool, "UpdateEmission", 0f); tool.SetSpraying(false);
        drop = Probes(tool).Single(); Call(tool, "UpdateActiveProbes", .25f);
        Require(!(bool)Get(drop, "active") && ((Vector2)Get(drop, "position")).x > 7f && ((Vector2)Get(drop, "position")).x < 8f,
            "Swept collision stops at thin wall and ignores the player's own collider");
        Require(world.GetTotalWaterUnits() == 4, "Wall impact deposits one payload");
        Destroy(wall); Set(tool, "obstacleMask", (LayerMask)0);
        ResetTool(tool, world);
        Vector2 burningCell = world.CellToWorld(26, 39); world.Ignite(burningCell, 0, 3);
        tool.SetSpraying(true); Call(tool, "UpdateEmission", 0f); tool.SetSpraying(false);
        Call(tool, "UpdateActiveProbes", .1f);
        Require(world.GetTotalWaterUnits() == 1 && world.GetFireEnergy(burningCell) == 0,
            "Fast water cools on contact: three units spent and one stored from a four-unit payload");
        world.StepOnce();
        Require(world.GetFireEnergy(burningCell) == 0, "Water extinguishes encountered fire");
        ResetTool(tool, world); Set(tool, "currentWater", 1);
        tool.SetSpraying(true); Call(tool, "UpdateEmission", .5f);
        Require(tool.CurrentWater == 0 && Probes(tool).Length == 1, "Finite resources cannot overspend");
        ResetTool(tool, world);
        foreach (object probe in (Array)Get(tool, "probes")) Set(probe, "active", true);
        tool.SetSpraying(true); Call(tool, "UpdateEmission", .1f);
        Require(tool.CurrentWater == 300, "Full probe pool does not spend resources or overwrite live probes");
        ResetTool(tool, world); tool.UseFlame(); tool.SetSpraying(true); Call(tool, "UpdateEmission", 0f); tool.SetSpraying(false);
        Require(((byte[])Get(world, "fire")).Any(v => v > 0) && tool.CurrentFuel == 299 && tool.CurrentWater == 300,
            "Flame ignites cells and spends only fuel");
        Call(tool, "UpdateSprayVisuals"); Call(tool, "OnDisable");
        Require(tool.ActiveProbeCount == 0 && !((MeshRenderer)Get(tool, "sprayRenderer")).enabled && Probes(tool).Length == 0,
            "Disabling tool clears probes and visuals");
        Set(tool, "probes", null); Call(tool, "OnEnable");
        Require(((Array)Get(tool, "probes")).Length == 192 && !tool.IsSpraying, "Transient probe pool rebuilds safely after script reload");
        ResetTool(tool, world);
        world.AddFuel(world.CellToWorld(30, 30), 0, 8);
        world.AddWater(world.CellToWorld(30, 31), 0, 8);
        for (int i = 0; i < 60; i++) world.StepOnce();
        Require(world.GetTotalFuelUnits() == 8 && world.GetTotalWaterUnits() == 8, "Mixed liquid simulation preserves water and oil quantities");
        SceneManager.LoadScene("Assets/Game/Scenes/Levels/Level_0.unity");
        yield return null; yield return null;
        var levelTool = FindAnyObjectByType<PressureTool2D>();
        var levelWorld = FindAnyObjectByType<CellWorld2D>();
        Require(levelTool != null && levelWorld != null && levelWorld.IsInitialized, "Level_0 starts with its tool and initialized cell world");
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "Scene component references: " + child.name);
        var aim = levelTool.GetComponent<PlayerAim2D>(); if (aim != null) aim.enabled = false;
        var motor = levelTool.GetComponent<PlayerMotor2D>(); if (motor != null) motor.enabled = false;
        var body = levelTool.GetComponent<Rigidbody2D>(); if (body != null) body.simulated = false;
        var levelMuzzle = (Transform)Get(levelTool, "muzzle"); levelMuzzle.right = Vector3.right;
        Require(levelWorld.TryWorldToCell(levelMuzzle.position, out _), "Level_0 muzzle is inside the simulation bounds");
        levelTool.UseWater(); levelTool.SetSpraying(true);
        yield return new WaitForSeconds(.35f);
        Require(levelTool.ActiveProbeCount > 0, "Level_0 emits water with existing scene wiring");
        yield return Capture("ValidationResults/water-check.png");
        levelTool.UseFlame(); yield return new WaitForSeconds(.4f);
        Require(levelTool.ActiveProbeCount > 0, "Level_0 emits flame after mode switch");
        yield return Capture("ValidationResults/flame-check.png");
        Require(((byte[])Get(levelWorld, "fire")).Any(v => v > 0), "Level_0 flame ignites the existing cell world");
        Require(!ShaderUtil.ShaderHasError(Resources.Load<Material>("Effects/PressureSpray").shader), "Spray shader compiles for the rendered camera");
        levelTool.SetSpraying(false); yield return new WaitForSeconds(1.5f);
        Require(levelTool.ActiveProbeCount == 0, "Releasing spray lets the remaining plume finish");
        File.AppendAllText("ValidationResults/check-result.txt", "COMPLETE\n");
        Debug.Log("PRESSURE_CHECK COMPLETE");
        EditorApplication.Exit(0);
    }
    static IEnumerator Capture(string path)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
        // Camera.Render works with the scene's active URP renderer through a normal target texture.
        var camera = Camera.main;
        if (camera == null) yield break;
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        yield return null; yield return null;
        RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null; target.Release();
        Destroy(target); Destroy(image);
    }
}

