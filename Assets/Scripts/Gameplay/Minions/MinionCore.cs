using UnityEngine;

public class MinionAgent : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private MinionSettings settings;
    [SerializeField] private MinionRoleType roleType = MinionRoleType.Melee;

    private Transform currentTarget;
    private bool hasLineOfSight = true;
    private bool isAbilityReady = true;
    private bool debugUseDistanceOverride;
    private float debugDistanceOverride;

    private bool debugForceState;
    private MinionState debugForcedState;
    private bool debugForceCombatPhase;
    private CombatPhase debugForcedCombatPhase;

    [Header("Debug Visuals")]
    [SerializeField] private bool drawRoleRangeGizmos;

    [Header("Runtime Debug")]
    [SerializeField] private MinionState currentState;
    [SerializeField] private CombatPhase currentCombatPhase;
    [SerializeField] private CommandType currentCommandType;
    [SerializeField] private float currentDistanceToTarget;

    private IMinionRole currentRole;
    private MinionCommand currentCommand;

    private MinionDecisionLayer decisionLayer;
    private MinionStateMachine stateMachine;
    private MinionCombatPhaseController combatPhaseController;
    private MinionTickScheduler tickScheduler;

    private MinionIntent currentIntent;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (settings == null)
        {
            Debug.LogError($"[{name}] Missing MinionSettings.");
            return;
        }

        decisionLayer = new MinionDecisionLayer();
        stateMachine = new MinionStateMachine();
        combatPhaseController = new MinionCombatPhaseController();
        tickScheduler = new MinionTickScheduler();

        currentRole = MinionRoleFactory.Create(roleType, settings);

        if (currentRole == null)
        {
            Debug.LogError($"[{name}] Failed to create role for type: {roleType}");
            return;
        }

        currentCommand = new MinionCommand
        {
            Type = CommandType.None,
            Target = null,
            TargetPosition = transform.position,
            Priority = 0,
            IssuedTime = 0f,
            TimeToLive = 0f,
            Source = CommandSource.System,
            InterruptPolicy = InterruptPolicy.None,
            LastFailureReason = FailureReason.None
        };

        currentIntent = MinionIntent.Idle();
    }

    private void Update()
    {
        if (settings == null || currentRole == null) return;

        float currentTime = Time.time;

        // Decision is not needed every frame.
        if (tickScheduler.ShouldRunDecision(currentTime, settings.TickRates))
        {
            currentIntent = decisionLayer.ResolveIntent(currentCommand, currentTime);
            stateMachine.UpdateState(currentIntent);

            if (debugForceState)
            {
                stateMachine.ForceState(debugForcedState);
            }
        }

        // Combat phase can be updated every frame for a smooth prototype.
        UpdateCombatPhase();

        // Push debug values into inspector.
        UpdateDebugData();
    }

    private void UpdateCombatPhase()
    {
        currentDistanceToTarget = debugUseDistanceOverride ? debugDistanceOverride : GetDistanceToTarget();

        combatPhaseController.UpdatePhase(
            stateMachine.CurrentState,
            currentRole,
            currentDistanceToTarget,
            hasLineOfSight,
            isAbilityReady
        );

        if (debugForceCombatPhase)
        {
            combatPhaseController.ForcePhase(debugForcedCombatPhase);
        }
    }

    private void UpdateDebugData()
    {
        currentState = stateMachine.CurrentState;
        currentCombatPhase = combatPhaseController.CurrentPhase;
        currentCommandType = currentCommand != null ? currentCommand.Type : CommandType.None;
    }

    private float GetDistanceToTarget()
    {
        if (currentTarget == null) return float.MaxValue;

        return Vector3.Distance(transform.position, currentTarget.position);
    }

    // Makes the minion return to the player/follow behavior.
    public void SetFollowCommand()
    {
        currentCommand = new MinionCommand
        {
            Type = CommandType.FollowPlayer,
            Target = null,
            TargetPosition = transform.position,
            Priority = 1,
            IssuedTime = Time.time,
            TimeToLive = 0f,
            Source = CommandSource.Player,
            InterruptPolicy = InterruptPolicy.Soft,
            LastFailureReason = FailureReason.None
        };
    }

    // Makes the minion recall immediately.
    public void SetRecallCommand()
    {
        currentCommand = new MinionCommand
        {
            Type = CommandType.Recall,
            Target = null,
            TargetPosition = transform.position,
            Priority = 100,
            IssuedTime = Time.time,
            TimeToLive = 0f,
            Source = CommandSource.Player,
            InterruptPolicy = InterruptPolicy.Hard,
            LastFailureReason = FailureReason.None
        };
    }

    // Makes the minion attack an enemy target.
    public void SetAttackEnemyCommand(Transform target)
    {
        currentTarget = target;

        currentCommand = new MinionCommand
        {
            Type = CommandType.AttackEnemy,
            Target = target,
            TargetPosition = target != null ? target.position : transform.position,
            Priority = 10,
            IssuedTime = Time.time,
            TimeToLive = 5f,
            Source = CommandSource.Player,
            InterruptPolicy = InterruptPolicy.Soft,
            LastFailureReason = FailureReason.None
        };
    }

    // Makes the minion attack a breakable object.
    public void SetAttackObjectCommand(Transform target)
    {
        currentTarget = target;

        currentCommand = new MinionCommand
        {
            Type = CommandType.AttackObject,
            Target = target,
            TargetPosition = target != null ? target.position : transform.position,
            Priority = 10,
            IssuedTime = Time.time,
            TimeToLive = 5f,
            Source = CommandSource.Player,
            InterruptPolicy = InterruptPolicy.Soft,
            LastFailureReason = FailureReason.None
        };
    }

    // Makes the support minion act on a target.
    public void SetSupportCommand(Transform target)
    {
        currentTarget = target;

        currentCommand = new MinionCommand
        {
            Type = CommandType.SupportTarget,
            Target = target,
            TargetPosition = target != null ? target.position : transform.position,
            Priority = 10,
            IssuedTime = Time.time,
            TimeToLive = 5f,
            Source = CommandSource.Player,
            InterruptPolicy = InterruptPolicy.Soft,
            LastFailureReason = FailureReason.None
        };
    }

    // Clears the current command and returns to idle fallback.
    public void ClearCommand()
    {
        currentCommand = new MinionCommand
        {
            Type = CommandType.None,
            Target = null,
            TargetPosition = transform.position,
            Priority = 0,
            IssuedTime = Time.time,
            TimeToLive = 0f,
            Source = CommandSource.System,
            InterruptPolicy = InterruptPolicy.None,
            LastFailureReason = FailureReason.None
        };
    }

    [ContextMenu("Debug/Follow")]
    private void DebugFollow()
    {
        SetFollowCommand();
    }

    [ContextMenu("Debug/Recall")]
    private void DebugRecall()
    {
        SetRecallCommand();
    }

    [ContextMenu("Debug/Clear Command")]
    private void DebugClearCommand()
    {
        ClearCommand();
    }

    public void DebugFindNearestEnemyTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float nearestSq = float.MaxValue;

        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject go = enemies[i];
            if (go == null) continue;

            float sq = (go.transform.position - transform.position).sqrMagnitude;
            if (sq < nearestSq)
            {
                nearestSq = sq;
                nearest = go.transform;
            }
        }

        currentTarget = nearest;
    }

    public void DebugSetLineOfSight(bool value)
    {
        hasLineOfSight = value;
    }

    public void DebugSetAbilityReady(bool value)
    {
        isAbilityReady = value;
    }

    public void DebugSetDistanceStep(int distance)
    {
        debugUseDistanceOverride = true;
        debugDistanceOverride = Mathf.Clamp(distance, 0f, 50f);
    }

    public void DebugClearDistanceOverride()
    {
        debugUseDistanceOverride = false;
    }

    public void DebugSetCommandType(CommandType type)
    {
        switch (type)
        {
            case CommandType.None:
                ClearCommand();
                break;
            case CommandType.FollowPlayer:
                SetFollowCommand();
                break;
            case CommandType.Recall:
                SetRecallCommand();
                break;
            case CommandType.AttackEnemy:
                if (currentTarget == null) DebugFindNearestEnemyTarget();
                SetAttackEnemyCommand(currentTarget);
                break;
            case CommandType.AttackObject:
                SetAttackObjectCommand(currentTarget);
                break;
            case CommandType.SupportTarget:
                SetSupportCommand(currentTarget);
                break;
        }
    }

    public void DebugForceState(MinionState state)
    {
        debugForceState = true;
        debugForcedState = state;
        stateMachine.ForceState(state);
    }

    public void DebugClearForcedState()
    {
        debugForceState = false;
    }

    public void DebugForceCombatPhase(CombatPhase phase)
    {
        debugForceCombatPhase = true;
        debugForcedCombatPhase = phase;
        combatPhaseController.ForcePhase(phase);
    }

    public void DebugClearForcedCombatPhase()
    {
        debugForceCombatPhase = false;
    }

    public void DebugToggleRangeGizmos()
    {
        drawRoleRangeGizmos = !drawRoleRangeGizmos;
    }

    public bool DebugAreRangeGizmosEnabled()
    {
        return drawRoleRangeGizmos;
    }

    public void DebugRotateToTarget()
    {
        if (currentTarget == null) return;

        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    public void DebugRunCommandFlowTest()
    {
        float now = Time.time;
        CommandType before = currentCommand != null ? currentCommand.Type : CommandType.None;

        SetFollowCommand();
        MinionIntent intent = decisionLayer.ResolveIntent(currentCommand, now);
        bool followOk = intent.CommandType == CommandType.FollowPlayer;

        if (currentTarget == null) DebugFindNearestEnemyTarget();
        SetAttackEnemyCommand(currentTarget);
        intent = decisionLayer.ResolveIntent(currentCommand, now);
        bool attackOk = intent.CommandType == CommandType.AttackEnemy;

        ClearCommand();
        intent = decisionLayer.ResolveIntent(currentCommand, now);
        bool clearOk = intent.CommandType == CommandType.None;

        Debug.Log($"[{name}] CommandFlowTest | Before:{before} Follow:{followOk} Attack:{attackOk} Clear:{clearOk}", this);
    }

    public bool DebugHasLineOfSight()
    {
        return hasLineOfSight;
    }

    public bool DebugIsAbilityReady()
    {
        return isAbilityReady;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawRoleRangeGizmos || settings == null) return;

        RoleSettings roleSettings = settings.Melee;
        if (roleType == MinionRoleType.Ranged) roleSettings = settings.Ranged;
        else if (roleType == MinionRoleType.Support) roleSettings = settings.Support;

        if (roleSettings == null || roleSettings.RangePolicy == null) return;

        Vector3 p = transform.position;
        RangePolicy rp = roleSettings.RangePolicy;

        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
        Gizmos.DrawWireSphere(p, rp.MinRange);
        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(p, rp.DesiredRange);
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.9f);
        Gizmos.DrawWireSphere(p, rp.MaxRange);

        if (currentTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(p, currentTarget.position);
        }
    }
}