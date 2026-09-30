using UnityEditor;
using UnityEngine;

//custom Inspector for CellWorld and its subclasses
[CustomEditor(typeof(CellWorld2D), true)]
public class CellWorld2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawGridProperties();
        serializedObject.ApplyModifiedProperties();
    }

    //draw ordinary fields and insert our size information
    protected void DrawGridProperties(params string[] excluded)
    {
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (System.Array.IndexOf(excluded, property.name) >= 0)
                continue;

            bool gridSizeField =
                property.name == "width" ||
                property.name == "height" ||
                property.name == "cellSize";

            //arrays are created at startup, so prevent resizing during Play
            bool readOnly = property.name == "m_Script" ||
                            (Application.isPlaying && gridSizeField);

            using (new EditorGUI.DisabledScope(readOnly))
                EditorGUILayout.PropertyField(property, true);

            if (property.name == "height")
                DrawGridSizeStatus();
        }
    }

    private void DrawGridSizeStatus()
    {
        int width = serializedObject.FindProperty("width").intValue;
        int height = serializedObject.FindProperty("height").intValue;
        float cellSize = serializedObject.FindProperty("cellSize").floatValue;

        long count = (long)width * height;

        bool valid =
            width >= 16 && width <= 2048 &&
            height >= 16 && height <= 2048 &&
            count <= 1048576 &&
            cellSize >= 0.01f &&
            !float.IsNaN(cellSize) &&
            !float.IsInfinity(cellSize);

        EditorGUILayout.LabelField(
            "Cell count", $"{count:N0} / 1,048,576");

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Toggle("Within size limits", valid);

        if (!valid)
        {
            EditorGUILayout.HelpBox(
                "Width and Height must be 16–2048, with at most " +
                "1,048,576 cells. Cell Size must be finite and at least 0.01.",
                MessageType.Error);
        }
    }
}