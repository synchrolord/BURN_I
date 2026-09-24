using UnityEditor;
using UnityEngine;

// Editor-only controls. The runtime component contains the actual simulation boundary.
[CustomEditor(typeof(CellWorldPoly2D))]
public sealed class CellWorldPoly2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        DrawPropertiesExcluding(serializedObject, "m_Script", "outline");
        using (new EditorGUI.DisabledScope(Application.isPlaying))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("outline"), true);
        serializedObject.ApplyModifiedProperties();
        CellWorldPoly2D world = (CellWorldPoly2D)target;
        EditorGUILayout.HelpBox(
            "Outline points are local positions, in order around the edge. Drag the numbered " +
            "points in Scene view. Expand Outline to add or remove points. " +
            "Keep the outline inside the grid and avoid crossing edges. No collider is needed.",
            MessageType.Info);

        if (!world.TryValidateOutline(out string message))
            EditorGUILayout.HelpBox(message, MessageType.Error);

        EditorGUILayout.LabelField("Grid size before Transform scale",
            $"{world.Width * world.CellSize:0.##} × {world.Height * world.CellSize:0.##}");

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Reset Outline To Grid"))
            {
                Undo.RecordObject(world, "Reset polygon outline");
                world.ResetOutlineToGrid();
                PrefabUtility.RecordPrefabInstancePropertyModifications(world);
                EditorUtility.SetDirty(world);
                SceneView.RepaintAll();
            }
        }
        if (Application.isPlaying)
            EditorGUILayout.HelpBox("Edit the outline outside Play mode, then enter Play again to test it.", MessageType.Info);
    }

    private void OnSceneGUI()
    {
        CellWorldPoly2D world = (CellWorldPoly2D)target;
        Vector2[] outline = world.Outline;
        if (outline == null || outline.Length == 0) return;

        // Invalid numbers cannot be drawn safely; the Inspector explains the problem.
        foreach (Vector2 point in outline)
            if (float.IsNaN(point.x) || float.IsNaN(point.y) ||
                float.IsInfinity(point.x) || float.IsInfinity(point.y)) return;

        Transform space = world.transform;
        Vector3[] corners = new Vector3[outline.Length + 1];
        for (int i = 0; i < outline.Length; i++)
            corners[i] = space.TransformPoint(outline[i]);
        corners[outline.Length] = corners[0]; // Close the last edge.

        Color color = world.TryValidateOutline(out _) ? Color.green : Color.red;
        using (new Handles.DrawingScope(color))
        {
            Handles.DrawAAPolyLine(3, corners);
            serializedObject.Update();
            SerializedProperty points = serializedObject.FindProperty("outline");
            for (int i = 0; i < outline.Length; i++)
            {
                Vector3 position = corners[i];
                float size = HandleUtility.GetHandleSize(position) * 0.06f;
                Handles.Label(position + space.up * size * 1.5f, $"{i}");
                if (Application.isPlaying) continue;

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.Slider2D(position, space.forward, space.right,
                    space.up, size, Handles.DotHandleCap, Vector2.zero);
                if (!EditorGUI.EndChangeCheck()) continue;

                Vector3 local = space.InverseTransformPoint(moved);
                points.GetArrayElementAtIndex(i).vector2Value = new Vector2(local.x, local.y);
                // SerializedProperty handles Undo and prefab overrides for the point edit.
                serializedObject.ApplyModifiedProperties();
            }
        }
    }

    [MenuItem("GameObject/2D Object/BURN/Polygon CellWorld", false, 10)]
    private static void CreatePolygonWorld(MenuCommand command)
    {
        GameObject item = new GameObject("CellWorld (Polygon)");
        GameObjectUtility.SetParentAndAlign(item, command.context as GameObject);
        CellWorldPoly2D world = item.AddComponent<CellWorldPoly2D>();
        world.ResetOutlineToGrid();
        Undo.RegisterCreatedObjectUndo(item, "Create polygon CellWorld");
        Selection.activeGameObject = item;
    }
}
