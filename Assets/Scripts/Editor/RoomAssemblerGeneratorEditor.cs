#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomAssemblerGenerator))]
public class RoomAssemblerGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var gen = (RoomAssemblerGenerator)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Generate"))
        {
            gen.Generate();
            EditorUtility.SetDirty(gen);
        }
    }
}
#endif
