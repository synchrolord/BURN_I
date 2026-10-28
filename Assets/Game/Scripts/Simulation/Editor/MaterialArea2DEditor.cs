using UnityEditor;

[CustomEditor(typeof(MaterialArea2D))]
public sealed class MaterialArea2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty world = serializedObject.FindProperty("world");
        SerializedProperty material = serializedObject.FindProperty("material");
        SerializedProperty shape = serializedObject.FindProperty("shape");

        EditorGUILayout.PropertyField(world);
        EditorGUILayout.PropertyField(material);
        EditorGUILayout.PropertyField(shape);

//only water areas show this option
        if (material.enumValueIndex == 0)
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(
                    "blockPlayerWater"));
        }

//plant and light use their own values
        if (material.enumValueIndex != 6 &&
            material.enumValueIndex != 7)
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("amount"));
        }

        if (material.enumValueIndex == 6)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("plantColor"));

        if (material.enumValueIndex == 7)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lightLevel"));

        //only a hollow box needs wall thickness
        if (shape.enumValueIndex == 1)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wallThickness"));

        serializedObject.ApplyModifiedProperties();
    }
}
