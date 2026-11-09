using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CellWorldRoomManager))]
public sealed class CellWorldRoomManagerEditor : Editor
{
    private static readonly string[] settings =
    {
        "usePixelDarkness", "darkness", "fullyLitLevel", "fireLightLevel",
        "lightRefreshSeconds", "darknessOrder", "unityLightBase",
        "unityLightEffect", "unityLightMaximum"
    };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Apply copies these settings once. Individual rooms remain editable afterward.", MessageType.Info);
        if (GUILayout.Button("Apply Darkness to All Worlds")) ApplyDarkness();
    }

    private void ApplyDarkness()
    {
        serializedObject.Update();
        CellWorldRoomManager manager = (CellWorldRoomManager)target;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply darkness to all worlds");
        int count = 0;
        //include room-prefab worlds that only register with the manager during Play
        foreach (CellWorld2D world in Object.FindObjectsByType<CellWorld2D>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (world.gameObject.scene != manager.gameObject.scene) continue;
            SerializedObject data = new SerializedObject(world);
            data.Update();
            foreach (string name in settings)
                data.FindProperty(name).boxedValue = serializedObject.FindProperty(name).boxedValue;
            data.ApplyModifiedProperties();
            if (PrefabUtility.IsPartOfPrefabInstance(world))
                PrefabUtility.RecordPrefabInstancePropertyModifications(world);
            if (Application.isPlaying) world.RefreshDarkness();
            count++;
        }
        Undo.CollapseUndoOperations(group);
        Debug.Log("Applied darkness settings to " + count + " CellWorlds.", manager);
    }
}
