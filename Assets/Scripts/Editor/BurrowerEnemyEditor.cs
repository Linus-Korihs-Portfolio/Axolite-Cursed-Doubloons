#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BurrowerEnemy))]
public class BurrowerEnemyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Set HP to Burrow Threshold"))
            {
                var burrower = (BurrowerEnemy)target;

                var settingsProp = serializedObject.FindProperty("settings");
                var settings = settingsProp?.objectReferenceValue as BurrowerEnemySettings;

                var stats = burrower.GetComponent<CombatantStats>();

                if (stats == null)
                {
                    Debug.LogWarning("[BurrowerEnemyEditor] No CombatantStats found on this GameObject.");
                    return;
                }

                float threshold = settings != null ? settings.BurrowHealthThreshold : 0.3f;
                float maxHp     = stats.GetStat(CombatStatType.MaxHealth);
                float targetHp  = threshold * maxHp;

                if (stats.CurrentHealth > targetHp)
                {
                    // Use SetHealth to bypass defense so the test always reaches the threshold.
                    stats.SetHealth(targetHp);
                    Debug.Log($"[BurrowerEnemyEditor] HP set directly to {targetHp:F1} ({threshold * 100f:F0}% of {maxHp:F1}) — defense bypassed.");
                }
                else
                {
                    Debug.Log("[BurrowerEnemyEditor] HP is already at or below the burrow threshold.");
                }
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Force Emerge from Ground"))
            {
                var burrower = (BurrowerEnemy)target;
                burrower.ForceEmerge();
                Debug.Log("[BurrowerEnemyEditor] Force-emerge triggered.");
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use the test button.", MessageType.Info);
        }
    }
}
#endif
