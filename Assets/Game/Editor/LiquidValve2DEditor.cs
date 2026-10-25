using UnityEditor;

[CustomEditor(typeof(LiquidValve2D))]
public sealed class LiquidValve2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty valveType =
            serializedObject.FindProperty("valveType");

        EditorGUILayout.PropertyField(valveType);
        EditorGUILayout.Space();

        if (valveType.enumValueIndex == 0)
        {
            //source valve only needs source
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("source"));
        }
        else
        {
            //water valve drains from its sink
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("world"));

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("sink"));

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("radius"));

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("amount"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}