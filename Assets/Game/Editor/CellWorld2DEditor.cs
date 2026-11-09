using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CellWorld2D), true), CanEditMultipleObjects]
public class CellWorld2DEditor : Editor
{
    private readonly Dictionary<string, List<string>> sections = new Dictionary<string, List<string>>();
    private static readonly string[] order =
    {
        "Grid and room size", "Simulation and performance", "Water, fuel and acid",
        "Smoke and steam", "Fire", "Plants", "Colours and appearance",
        "Darkness and lighting", "Debug"
    };

    private void OnEnable()
    {
        sections.Clear();
        string section = "Other";
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.name == "m_Script") continue;
            //reuse the existing headers so new settings appear without another field list
            System.Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(property.name, BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                type = type.BaseType;
            }
            HeaderAttribute header = field != null ? field.GetCustomAttribute<HeaderAttribute>() : null;
            if (header != null) section = SectionName(header.header);
            if (!sections.ContainsKey(section)) sections[section] = new List<string>();
            sections[section].Add(property.name);
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        foreach (string name in order) DrawSection(name);
        //keep unfamiliar sections visible if more settings are added later
        foreach (string name in sections.Keys)
            if (System.Array.IndexOf(order, name) < 0) DrawSection(name);
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSection(string name)
    {
        if (!sections.TryGetValue(name, out List<string> fields)) return;
        string key = "BURN.CellWorld." + name;
        bool open = SessionState.GetBool(key, name == "Grid and room size");
        open = EditorGUILayout.BeginFoldoutHeaderGroup(open, name);
        SessionState.SetBool(key, open);
        if (open)
        {
            EditorGUI.indentLevel++;
            foreach (string field in fields)
            {
                bool gridField = field == "width" || field == "height" || field == "cellSize";
                using (new EditorGUI.DisabledScope(Application.isPlaying && gridField))
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(field), true);
                if (field == "height") DrawGridSizeStatus();
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static string SectionName(string header)
    {
        switch (header)
        {
            case "Grid": return "Grid and room size";
            case "Simulation": case "Chunks": return "Simulation and performance";
            case "Basic Liquids": return "Water, fuel and acid";
            case "Gases": case "Gas Momentum": case "Smoke": case "Steam": return "Smoke and steam";
            case "Fire": case "Fire Puff": case "Fire Appearance": return "Fire";
            case "Colors": return "Colours and appearance";
            case "Pixel Darkness": return "Darkness and lighting";
            default: return header;
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
