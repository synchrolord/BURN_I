using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class LevelSkeletonBuilder
{
    private const string SourceScene =
        "Assets/Game/Scenes/Levels/Level_Alpha.unity";

    private const string SceneFolder =
        "Assets/Game/Scenes/Levels/Floor Skeletons";

    private const string PrefabFolder =
        "Assets/Game/Prefabs/Levels";

    private const string SystemsPrefab =
        PrefabFolder + "/Level Systems.prefab";

    private const string RoomPrefab =
        PrefabFolder + "/Level Room.prefab";

    private const string CellWorldPrefab =
        "Assets/Game/Prefabs/CellWorld/CellWorld.prefab";

    private const string MusicPath =
        "Assets/Game/Audio/[Official] Celeste Original Soundtrack - 20 - Heart of the Mountain.mp3";

    private const int RoomWidth = 320;
    private const int RoomHeight = 192;
    private const float CellSize = 0.125f;

    private static readonly Vector2[] RoomPositions =
    {
        new Vector2(0f, 0f),
        new Vector2(40f, 0f),
        new Vector2(80f, 0f),
        new Vector2(80f, 24f),
        new Vector2(40f, 24f),
        new Vector2(0f, 24f)
    };

    private static readonly Vector2[] DoorPositions =
    {
        new Vector2(40f, 4f),
        new Vector2(80f, 4f),
        new Vector2(100f, 24f),
        new Vector2(80f, 28f),
        new Vector2(40f, 28f)
    };


    //build the missing skeletons once after this script finishes compiling
    [InitializeOnLoadMethod]
    private static void BuildMissingSkeletons()
    {
        string firstLevel = SceneFolder + "/Floor_01.unity";

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(firstLevel) != null ||
            SessionState.GetBool("BURN_I.BuildFloorSkeletons", false))
        {
            return;
        }

        SessionState.SetBool("BURN_I.BuildFloorSkeletons", true);
        EditorApplication.delayCall += BuildFloorSkeletons;
    }


    [MenuItem("Tools/Until We Are Ash/Build Floor Skeletons")]
    public static void BuildFloorSkeletons()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        MakeFolder("Assets/Game/Prefabs", "Levels");
        MakeFolder("Assets/Game/Scenes/Levels", "Floor Skeletons");

        GameObject systemsPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(SystemsPrefab);

        if (systemsPrefab == null)
            systemsPrefab = BuildSystemsPrefab();

        GameObject roomPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefab);

        if (roomPrefab == null)
            roomPrefab = BuildRoomPrefab();

        BuildScene("Floor_Template", "", systemsPrefab, roomPrefab);

        List<string> levelScenes = new List<string>();

        for (int i = 1; i <= 7; i++)
        {
            string levelName = $"Floor_{i:00}";
            string nextScene = i < 7
                ? $"Floor_{i + 1:00}"
                : "StartScreen";

            string path = BuildScene(
                levelName,
                nextScene,
                systemsPrefab,
                roomPrefab);

            levelScenes.Add(path);
        }

        AddToBuildSettings(levelScenes);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorSceneManager.OpenScene(
            SceneFolder + "/Floor_01.unity",
            OpenSceneMode.Single);

        Debug.Log(
            "Built Floor_01 to Floor_07, Floor_Template, " +
            "Level Systems, and Level Room.");
    }


    private static GameObject BuildSystemsPrefab()
    {
        Scene scene = EditorSceneManager.OpenScene(
            SourceScene,
            OpenSceneMode.Single);

        GameObject[] oldRoots = scene.GetRootGameObjects();

        //remove level content but keep the already working systems and links
        foreach (CellWorld2D world in
                 UnityEngine.Object.FindObjectsByType<CellWorld2D>(
                     FindObjectsInactive.Include))
        {
            if (world.gameObject.scene == scene)
                UnityEngine.Object.DestroyImmediate(world.gameObject);
        }

        GameObject systems = new GameObject("Level Systems");

        foreach (GameObject root in oldRoots)
        {
            if (root == null) continue;

            if (IsSystemRoot(root))
                root.transform.SetParent(systems.transform, true);
            else
                UnityEngine.Object.DestroyImmediate(root);
        }

        PlayerMotor2D player =
            systems.GetComponentInChildren<PlayerMotor2D>(true);

        CellWorldRoomManager manager =
            systems.GetComponentInChildren<CellWorldRoomManager>(true);

        if (player == null || manager == null)
            throw new InvalidOperationException(
                "Level_Alpha needs a player and CellWorldRoomManager.");

        player.transform.position = new Vector3(5f, 3f, 0f);

        GameObject respawn = new GameObject("Respawn Point");
        respawn.transform.SetParent(systems.transform, false);
        respawn.transform.position = player.transform.position;

        PlayerRespawn2D playerRespawn =
            player.GetComponent<PlayerRespawn2D>();

        if (playerRespawn != null)
            SetObject(playerRespawn, "respawnPoint", respawn.transform);

        SetArray(manager, "worlds", Array.Empty<UnityEngine.Object>());

        PressureTool2D tool = player.GetComponent<PressureTool2D>();
        if (tool != null) tool.SetWorld(null);

        PlayerLiquidInteraction2D liquids =
            player.GetComponent<PlayerLiquidInteraction2D>();
        if (liquids != null) liquids.SetWorld(null);

        SetupMusic(systems);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
            systems,
            SystemsPrefab);

        UnityEngine.Object.DestroyImmediate(systems);
        return prefab;
    }


    private static bool IsSystemRoot(GameObject root)
    {
        return root.GetComponentInChildren<PlayerMotor2D>(true) != null ||
               root.GetComponentInChildren<Camera>(true) != null ||
               root.GetComponentInChildren<Canvas>(true) != null ||
               root.GetComponentInChildren<EventSystem>(true) != null ||
               root.GetComponentInChildren<CellWorldRoomManager>(true) != null ||
               root.name.Equals(
                   "background music",
                   StringComparison.OrdinalIgnoreCase);
    }


    private static void SetupMusic(GameObject systems)
    {
        AudioSource music = systems
            .GetComponentsInChildren<AudioSource>(true)
            .FirstOrDefault(source => source.gameObject.name.Equals(
                "background music",
                StringComparison.OrdinalIgnoreCase));

        if (music == null)
        {
            GameObject musicObject = new GameObject("background music");
            musicObject.transform.SetParent(systems.transform, false);
            music = musicObject.AddComponent<AudioSource>();
            music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
        }

        music.loop = true;
        music.playOnAwake = true;
        music.spatialBlend = 0f;
        music.volume = 0.45f;
    }


    private static GameObject BuildRoomPrefab()
    {
        GameObject room = new GameObject("Level Room");
        GameObject cellWorldPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(CellWorldPrefab);

        GameObject worldObject = (GameObject)PrefabUtility.InstantiatePrefab(
            cellWorldPrefab,
            room.scene);

        worldObject.name = "CellWorld";
        worldObject.transform.SetParent(room.transform, false);

        CellWorld2D world = worldObject.GetComponent<CellWorld2D>();
        ConfigureWorld(world, null);

        GameObject gridObject = new GameObject("Tilemaps", typeof(Grid));
        gridObject.transform.SetParent(room.transform, false);

        CreateMap("Background", gridObject.transform, -20, false);
        CreateMap("Ground", gridObject.transform, 0, true);

        Tilemap plants = CreateMap(
            "Burnable Plants",
            gridObject.transform,
            2,
            false);

        BurnablePlantMap2D plantMap =
            plants.gameObject.AddComponent<BurnablePlantMap2D>();
        plantMap.World = world;

        CreateMap("Decorations Back", gridObject.transform, -10, false);
        CreateMap("Decorations Front", gridObject.transform, 10, false);

        GameObject roomPrefab = PrefabUtility.SaveAsPrefabAsset(
            room,
            RoomPrefab);

        UnityEngine.Object.DestroyImmediate(room);
        return roomPrefab;
    }


    private static Tilemap CreateMap(
        string name,
        Transform parent,
        int order,
        bool hasCollider)
    {
        GameObject mapObject = new GameObject(
            name,
            typeof(Tilemap),
            typeof(TilemapRenderer));

        mapObject.transform.SetParent(parent, false);

        TilemapRenderer renderer =
            mapObject.GetComponent<TilemapRenderer>();
        renderer.sortingOrder = order;

        if (hasCollider)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            mapObject.layer = groundLayer >= 0 ? groundLayer : 3;
            mapObject.AddComponent<TilemapCollider2D>();
        }

        return mapObject.GetComponent<Tilemap>();
    }


    private static string BuildScene(
        string sceneName,
        string nextScene,
        GameObject systemsPrefab,
        GameObject roomPrefab)
    {
        string path = SceneFolder + "/" + sceneName + ".unity";

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
        {
            Debug.LogWarning(path + " already exists, so it was not replaced.");
            return path;
        }

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single);

        GameObject systems = (GameObject)PrefabUtility.InstantiatePrefab(
            systemsPrefab,
            scene);

        PlayerMotor2D player =
            systems.GetComponentInChildren<PlayerMotor2D>(true);

        CellWorldRoomManager manager =
            systems.GetComponentInChildren<CellWorldRoomManager>(true);

        GameObject rooms = new GameObject("Rooms");
        List<CellWorld2D> worlds = new List<CellWorld2D>();

        for (int i = 0; i < RoomPositions.Length; i++)
        {
            GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(
                roomPrefab,
                scene);

            room.name = $"Room_{i + 1}";
            room.transform.SetParent(rooms.transform, false);
            room.transform.position = RoomPositions[i];

            CellWorld2D world = room.GetComponentInChildren<CellWorld2D>(true);
            ConfigureWorld(world, player.transform);
            worlds.Add(world);
        }

        SetArray(
            manager,
            "worlds",
            worlds.Cast<UnityEngine.Object>().ToArray());

        CreateDoorPoints();
        CreateExit(nextScene);

        EditorSceneManager.SaveScene(scene, path);
        return path;
    }


    private static void ConfigureWorld(
        CellWorld2D world,
        Transform player)
    {
        SerializedObject data = new SerializedObject(world);
        data.FindProperty("width").intValue = RoomWidth;
        data.FindProperty("height").intValue = RoomHeight;
        data.FindProperty("cellSize").floatValue = CellSize;
        data.FindProperty("activityTarget").objectReferenceValue = player;
        data.FindProperty("simulateAllChunks").boolValue = false;
        data.FindProperty("chunkSize").intValue = 32;
        data.FindProperty("activeChunkRadius").intValue = 2;
        data.FindProperty("usePixelDarkness").boolValue = true;
        data.FindProperty("darkness").floatValue = 0.95f;
        data.FindProperty("fullyLitLevel").intValue = 28;
        data.FindProperty("fireLightLevel").intValue = 28;
        data.FindProperty("lightRefreshSeconds").floatValue = 0.2f;
        data.ApplyModifiedPropertiesWithoutUndo();
    }


    private static void CreateDoorPoints()
    {
        GameObject points = new GameObject("Door Points");

        for (int i = 0; i < DoorPositions.Length; i++)
        {
            GameObject point = new GameObject(
                $"Door {i + 1}-{i + 2} - Put Door Here");

            point.transform.SetParent(points.transform, false);
            point.transform.position = DoorPositions[i];
        }
    }


    private static void CreateExit(string nextScene)
    {
        GameObject exit = new GameObject("Next Level Trigger");
        exit.transform.position = new Vector3(5f, 28f, 0f);

        BoxCollider2D collider = exit.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(2f, 4f);

        NextLevelTrigger2D trigger =
            exit.AddComponent<NextLevelTrigger2D>();

        SerializedObject data = new SerializedObject(trigger);
        data.FindProperty("nextScene").stringValue = nextScene;
        data.ApplyModifiedPropertiesWithoutUndo();
    }


    private static void SetObject(
        UnityEngine.Object target,
        string property,
        UnityEngine.Object value)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty(property).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }


    private static void SetArray(
        UnityEngine.Object target,
        string property,
        UnityEngine.Object[] values)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty array = data.FindProperty(property);
        array.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        data.ApplyModifiedPropertiesWithoutUndo();
    }


    private static void AddToBuildSettings(IEnumerable<string> paths)
    {
        List<EditorBuildSettingsScene> scenes =
            EditorBuildSettings.scenes.ToList();

        foreach (string path in paths)
        {
            if (scenes.Any(scene => scene.path == path))
                continue;

            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }


    private static void MakeFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
