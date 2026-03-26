using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MinionDebugToolkit : MonoBehaviour
{
    [SerializeField] private MinionAgent agent;

    private void Awake()
    {
        if (agent == null)
        {
            agent = GetComponent<MinionAgent>();
        }
    }

    public void FindNearestEnemyTarget()
    {
        if (agent == null) return;
        agent.DebugFindNearestEnemyTarget();
    }

    public void SetDistanceStep(int distance)
    {
        if (agent == null) return;
        agent.DebugSetDistanceStep(distance);
    }

    public void ClearDistanceOverride()
    {
        if (agent == null) return;
        agent.DebugClearDistanceOverride();
    }

    public void SetLineOfSight(bool value)
    {
        if (agent == null) return;
        agent.DebugSetLineOfSight(value);
    }

    public void SetAbilityReady(bool value)
    {
        if (agent == null) return;
        agent.DebugSetAbilityReady(value);
    }

    public void SendCommand(CommandType type)
    {
        if (agent == null) return;
        agent.DebugSetCommandType(type);
    }

    public void ForceState(MinionState state)
    {
        if (agent == null) return;
        agent.DebugForceState(state);
    }

    public void ClearForcedState()
    {
        if (agent == null) return;
        agent.DebugClearForcedState();
    }

    public void ForceCombatPhase(CombatPhase phase)
    {
        if (agent == null) return;
        agent.DebugForceCombatPhase(phase);
    }

    public void ClearForcedCombatPhase()
    {
        if (agent == null) return;
        agent.DebugClearForcedCombatPhase();
    }

    public void ToggleRangeGizmos()
    {
        if (agent == null) return;
        agent.DebugToggleRangeGizmos();
    }

    public void RotateToTarget()
    {
        if (agent == null) return;
        agent.DebugRotateToTarget();
    }

    public void TestCommandFlow()
    {
        if (agent == null) return;
        agent.DebugRunCommandFlowTest();
    }

    public bool HasLineOfSight()
    {
        return agent != null && agent.DebugHasLineOfSight();
    }

    public bool IsAbilityReady()
    {
        return agent != null && agent.DebugIsAbilityReady();
    }

    public bool AreRangeGizmosEnabled()
    {
        return agent != null && agent.DebugAreRangeGizmosEnabled();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MinionDebugToolkit))]
public class MinionDebugToolkitEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MinionDebugToolkit dbg = (MinionDebugToolkit)target;

        GUILayout.Space(10);
        GUILayout.Label("Target", EditorStyles.boldLabel);
        if (GUILayout.Button("Find Nearest Enemy")) dbg.FindNearestEnemyTarget();

        GUILayout.Space(8);
        GUILayout.Label("Distance Override", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("0")) dbg.SetDistanceStep(0);
        if (GUILayout.Button("10")) dbg.SetDistanceStep(10);
        if (GUILayout.Button("20")) dbg.SetDistanceStep(20);
        if (GUILayout.Button("30")) dbg.SetDistanceStep(30);
        if (GUILayout.Button("40")) dbg.SetDistanceStep(40);
        if (GUILayout.Button("50")) dbg.SetDistanceStep(50);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Clear Distance Override")) dbg.ClearDistanceOverride();

        GUILayout.Space(8);
        GUILayout.Label("Flags", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("LOS ON")) dbg.SetLineOfSight(true);
        if (GUILayout.Button("LOS OFF")) dbg.SetLineOfSight(false);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Ability Ready ON")) dbg.SetAbilityReady(true);
        if (GUILayout.Button("Ability Ready OFF")) dbg.SetAbilityReady(false);
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("Command", EditorStyles.boldLabel);
        if (GUILayout.Button("Command: None")) dbg.SendCommand(CommandType.None);
        if (GUILayout.Button("Command: Follow")) dbg.SendCommand(CommandType.FollowPlayer);
        if (GUILayout.Button("Command: Recall")) dbg.SendCommand(CommandType.Recall);
        if (GUILayout.Button("Command: Attack Enemy")) dbg.SendCommand(CommandType.AttackEnemy);
        if (GUILayout.Button("Command: Attack Object")) dbg.SendCommand(CommandType.AttackObject);
        if (GUILayout.Button("Command: Support")) dbg.SendCommand(CommandType.SupportTarget);

        GUILayout.Space(8);
        GUILayout.Label("Force State", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Idle")) dbg.ForceState(MinionState.Idle);
        if (GUILayout.Button("Follow")) dbg.ForceState(MinionState.Follow);
        if (GUILayout.Button("Combat")) dbg.ForceState(MinionState.Combat);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Clear Forced State")) dbg.ClearForcedState();

        GUILayout.Space(8);
        GUILayout.Label("Force Combat Phase", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("None")) dbg.ForceCombatPhase(CombatPhase.None);
        if (GUILayout.Button("Approach")) dbg.ForceCombatPhase(CombatPhase.Approach);
        if (GUILayout.Button("Reposition")) dbg.ForceCombatPhase(CombatPhase.Reposition);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("AttackWindow")) dbg.ForceCombatPhase(CombatPhase.AttackWindow);
        if (GUILayout.Button("Cast")) dbg.ForceCombatPhase(CombatPhase.Cast);
        if (GUILayout.Button("Recover")) dbg.ForceCombatPhase(CombatPhase.Recover);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Clear Forced Combat Phase")) dbg.ClearForcedCombatPhase();

        GUILayout.Space(8);
        GUILayout.Label("Helpers", EditorStyles.boldLabel);
        if (GUILayout.Button("Toggle Range Gizmos")) dbg.ToggleRangeGizmos();
        if (GUILayout.Button("Rotate Minion To Target")) dbg.RotateToTarget();
        if (GUILayout.Button("Test Command Flow")) dbg.TestCommandFlow();

        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Status: LOS=" + dbg.HasLineOfSight() +
            " | AbilityReady=" + dbg.IsAbilityReady() +
            " | RangeGizmos=" + dbg.AreRangeGizmosEnabled(),
            MessageType.Info);

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dbg);
        }
    }
}
#endif
