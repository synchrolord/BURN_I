using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(Room2D))]
public sealed class RoomInspector : Editor
{
    public override void OnInspectorGUI()
    {
        bool changed = DrawDefaultInspector();
        Room2D room = (Room2D)target;
        if (Application.isPlaying || !changed || room.World == null) return;
        Undo.RecordObjects(new Object[] { room.World, room.World.transform }, "Resize Room");
        room.ApplySize();
        EditorUtility.SetDirty(room.World);
        PrefabUtility.RecordPrefabInstancePropertyModifications(room.World);
        PrefabUtility.RecordPrefabInstancePropertyModifications(room.World.transform);
    }
}

[CustomEditor(typeof(CameraZone2D))]
public sealed class ScreenInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty area = serializedObject.FindProperty("area");
        if (area.objectReferenceValue == null)
        {
            EditorGUILayout.PropertyField(area, new GUIContent("Area"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("size"));
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("viewHeight"), new GUIContent("View Height"));
        if (serializedObject.FindProperty("mode").enumValueIndex == 1)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("followOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("priority"));
        serializedObject.ApplyModifiedProperties();

        BoxCollider2D trigger = area.objectReferenceValue as BoxCollider2D;
        if (trigger == null) return;
        EditorGUI.BeginChangeCheck();
        Vector2 size = EditorGUILayout.Vector2Field("Area Size", trigger.size);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(trigger, "Resize Screen Area");
            trigger.size = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            EditorUtility.SetDirty(trigger);
            if (PrefabUtility.IsPartOfPrefabInstance(trigger))
                PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
        }
        EditorGUILayout.HelpBox("Cyan: activation area. Yellow: view at the screen's position. Move the Area child to offset activation from the view.", MessageType.Info);
    }
}

//create reusable assets without saving or replacing the open level
public static class RoomToolsEditor
{
    private const string Folder = "Assets/Game/Prefabs/Levels/";

    [MenuItem("Tools/Until We Are Ash/Create Room Prefabs")]
    public static void BuildPrefabs()
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject screen = BuildScreen("Screen", CameraZone2D.Mode.Static, 22.5f, preview);
            GameObject follow = BuildScreen("Follow Screen", CameraZone2D.Mode.Follow, 18f, preview);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Room.prefab") != null) return;
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Level Room.prefab");
            GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
            //keep the existing tilemaps and their collider/shadow settings
            PrefabUtility.UnpackPrefabInstance(room, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            room.name = "Room";
            room.transform.position = Vector3.zero;
            CellWorld2D world = room.GetComponentInChildren<CellWorld2D>(true);
            room.AddComponent<Room2D>().Configure(world);
            Transform screens = Child("Screens", room.transform);
            Child("Lights", room.transform);
            Child("Props", room.transform);
            GameObject first = (GameObject)PrefabUtility.InstantiatePrefab(screen, preview);
            first.transform.SetParent(screens, false);
            first.transform.localPosition = new Vector3(20, 11.25f, 0);
            GameObject second = (GameObject)PrefabUtility.InstantiatePrefab(follow, preview);
            second.transform.SetParent(screens, false);
            second.transform.localPosition = new Vector3(60, 11.25f, 0);
            PrefabUtility.SaveAsPrefabAsset(room, Folder + "Room.prefab");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        AssetDatabase.SaveAssets();
    }

    private static GameObject BuildScreen(string name, CameraZone2D.Mode mode, float height, Scene preview)
    {
        string path = Folder + name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        GameObject screen = new GameObject(name);
        SceneManager.MoveGameObjectToScene(screen, preview);
        Transform area = Child("Area", screen.transform);
        area.gameObject.layer = 2;
        BoxCollider2D trigger = area.gameObject.AddComponent<BoxCollider2D>();
        trigger.size = new Vector2(40, 22.5f);
        trigger.isTrigger = true;
        screen.AddComponent<CameraZone2D>().Configure(trigger, mode, height);
        return PrefabUtility.SaveAsPrefabAsset(screen, path);
    }

    private static Transform Child(string name, Transform parent)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }
}
