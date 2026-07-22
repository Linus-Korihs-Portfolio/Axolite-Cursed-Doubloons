#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PCGGenerationBatchRunner))]
public class PCGGenerationBatchRunnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        var runner = (PCGGenerationBatchRunner)target;
        if (GUILayout.Button("Run Batch And Export CSV"))
        {
            runner.RunBatchAndExportCsv();
            EditorUtility.SetDirty(runner);
        }

        if (!string.IsNullOrWhiteSpace(runner.LastExportPath))
        {
            EditorGUILayout.HelpBox(runner.LastExportPath, MessageType.Info);
        }
    }
}
#endif
