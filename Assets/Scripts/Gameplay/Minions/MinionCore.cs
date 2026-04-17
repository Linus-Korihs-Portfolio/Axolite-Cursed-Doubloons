using UnityEngine;

[RequireComponent(typeof(CombatantStats))]
public class MinionAgent : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private MinionSettings settings;
    [SerializeField] private MinionRoleType roleType = MinionRoleType.Melee;

    [Header("Runtime Targets")]
    [SerializeField] private Transform followTarget;

    [Header("Auto Targeting")]
    [SerializeField] private bool autoAssignCombatCommands = true;
    [SerializeField] private float autoTargetRadius = 35f;
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string allyTag = "Ally";
    [SerializeField] private string breakableTag = "Breakable";
    [SerializeField] private bool fallbackToNameSearch = true;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float followStopDistance = 1.75f;

    [Header("Grounding")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundRayStartHeight = 1.5f;
    [SerializeField] private float groundRayLength = 8f;
    [SerializeField] private float groundOffset = 0f;

    [Header("Gravity")]
    [SerializeField] private bool useSimpleGravity = true;
    [SerializeField] private float gravityAcceleration = 25f;
    [SerializeField] private float maxFallSpeed = 40f;

    [Header("Abilities")]
    [SerializeField] private LayerMask lineOfSightBlockMask = ~0;
    [SerializeField] private StatusEffectDefinition supportBuffEffect;
    [SerializeField] private StatusEffectDefinition supportDebuffEffect;

    private Transform currentTarget;
    private bool hasLineOfSight = true;
    private bool isAbilityReady = true;
    private bool debugOverrideLineOfSight;
    private bool debugLineOfSightValue = true;
    private bool debugOverrideAbilityReady;
    private bool debugAbilityReadyValue = true;
    private bool debugUseDistanceOverride;
    private float debugDistanceOverride;
    private bool isGrounded;
    private float verticalVelocity;

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
    private MinionAbilitySystem abilitySystem;
    private CombatantStats sharedCombatStats;

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
        abilitySystem = new MinionAbilitySystem();
        sharedCombatStats = GetComponent<CombatantStats>();

        currentRole = MinionRoleFactory.Create(roleType, settings);

        if (currentRole == null)
        {
            Debug.LogError($"[{name}] Failed to create role for type: {roleType}");
            return;
        }

        abilitySystem.BuildDefaultLoadout(currentRole, supportBuffEffect, supportDebuffEffect);

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

        if (snapToGround)
        {
            isGrounded = SnapToGround();
        }

        ApplyGravity(Time.deltaTime);

        float currentTime = Time.time;

        // Decision is not needed every frame.
        if (tickScheduler.ShouldRunDecision(currentTime, settings.TickRates))
        {
            TryAssignAutoCommand(currentTime);

            currentIntent = decisionLayer.ResolveIntent(currentCommand, currentTime);
            stateMachine.UpdateState(currentIntent);

            if (debugForceState)
            {
                stateMachine.ForceState(debugForcedState);
            }
        }

        // Combat phase can be updated every frame for a smooth prototype.
        UpdateCombatPhase();

        // Execute the current state and combat phase.
        ExecuteCurrentState(currentTime);

        // Push debug values into inspector.
        UpdateDebugData();
    }

    private void UpdateCombatPhase()
    {
        currentTarget = ResolveActiveTarget();
        currentDistanceToTarget = debugUseDistanceOverride ? debugDistanceOverride : GetDistanceToTarget(currentTarget);
        hasLineOfSight = EvaluateLineOfSight(currentTarget);
        isAbilityReady = EvaluateAbilityReady(currentTarget);

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

    private void ExecuteCurrentState(float currentTime)
    {
        switch (stateMachine.CurrentState)
        {
            case MinionState.Idle:
                return;

            case MinionState.Follow:
                ExecuteFollow();
                return;

            case MinionState.Combat:
                ExecuteCombat(currentTime);
                return;
        }
    }

    private void ExecuteFollow()
    {
        if (followTarget == null) return;
        MoveTowardsDistance(followTarget.position, followStopDistance);
    }

    private void ExecuteCombat(float currentTime)
    {
        if (!IsValidTarget(currentTarget))
        {
            HandleMissingCombatTarget();
            return;
        }

        RangePolicy rangePolicy = currentRole.GetRangePolicy();
        float desiredRange = rangePolicy != null ? rangePolicy.DesiredRange : 0f;

        switch (combatPhaseController.CurrentPhase)
        {
            case CombatPhase.None:
                break;

            case CombatPhase.Approach:
                MoveTowardsDistance(currentTarget.position, desiredRange);
                break;

            case CombatPhase.Reposition:
                RepositionAroundTarget(currentTarget.position, desiredRange);
                break;

            case CombatPhase.AttackWindow:
            case CombatPhase.Cast:
                HoldRange(currentTarget.position, desiredRange);
                abilitySystem.TryUseBestAbility(transform, currentTarget, currentTime, sharedCombatStats, roleType);
                break;

            case CombatPhase.Recover:
                HoldRange(currentTarget.position, desiredRange);
                break;
        }
    }

    private void MoveTowardsDistance(Vector3 targetPosition, float stopDistance)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= Mathf.Max(0f, stopDistance))
        {
            if (distance > 0.0001f)
            {
                SmoothFaceDirection(toTarget / distance);
            }
            return;
        }

        Vector3 direction = toTarget / Mathf.Max(distance, 0.0001f);
        transform.position += direction * GetRuntimeMoveSpeed() * Time.deltaTime;
        SmoothFaceDirection(direction);
    }

    private void RepositionAroundTarget(Vector3 targetPosition, float desiredRange)
    {
        Vector3 offset = transform.position - targetPosition;
        offset.y = 0f;

        float distance = offset.magnitude;
        Vector3 facingDirection = (targetPosition - transform.position);
        facingDirection.y = 0f;

        if (distance < Mathf.Max(0.01f, desiredRange - 0.25f))
        {
            Vector3 away = offset.sqrMagnitude > 0.0001f ? offset.normalized : -transform.forward;
            transform.position += away * GetRuntimeMoveSpeed() * Time.deltaTime;
        }
        else if (distance > desiredRange + 0.25f)
        {
            MoveTowardsDistance(targetPosition, desiredRange);
            return;
        }

        if (facingDirection.sqrMagnitude > 0.0001f)
        {
            SmoothFaceDirection(facingDirection.normalized);
        }
    }

    private void HoldRange(Vector3 targetPosition, float desiredRange)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance > desiredRange + 0.35f)
        {
            MoveTowardsDistance(targetPosition, desiredRange);
            return;
        }

        if (distance < Mathf.Max(0f, desiredRange - 0.35f))
        {
            RepositionAroundTarget(targetPosition, desiredRange);
            return;
        }

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            SmoothFaceDirection(toTarget.normalized);
        }
    }

    private void SmoothFaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void UpdateDebugData()
    {
        currentState = stateMachine.CurrentState;
        currentCombatPhase = combatPhaseController.CurrentPhase;
        currentCommandType = currentCommand != null ? currentCommand.Type : CommandType.None;
    }

    private float GetDistanceToTarget(Transform target)
    {
        if (!IsValidTarget(target)) return 0f;

        return Vector3.Distance(transform.position, target.position);
    }

    private Transform ResolveActiveTarget()
    {
        Transform explicitTarget = AsTransform(currentCommand != null ? currentCommand.Target : null);
        if (IsValidTarget(explicitTarget)) return explicitTarget;

        if (currentCommand == null) return null;

        switch (currentCommand.Type)
        {
            case CommandType.AttackEnemy:
                return FindNearestByTag(enemyTag, autoTargetRadius);

            case CommandType.AttackObject:
                return FindNearestByTag(breakableTag, autoTargetRadius);

            case CommandType.SupportTarget:
                return ResolveSupportTarget();

            default:
                return null;
        }
    }

    private Transform ResolveSupportTarget()
    {
        if (currentRole == null) return followTarget;

        SupportMode mode = currentRole.GetSupportMode() ?? SupportMode.Heal;
        if (mode == SupportMode.Debuff)
        {
            return FindNearestByTag(enemyTag, autoTargetRadius);
        }

        Transform ally = FindNearestByTag(allyTag, autoTargetRadius, includeSelf: false);
        Transform resolved = ally != null ? ally : followTarget;

        if (resolved == transform)
        {
            return null;
        }

        return resolved;
    }

    private static Transform AsTransform(object target)
    {
        if (target is Transform transformTarget) return transformTarget;
        if (target is GameObject gameObjectTarget) return gameObjectTarget.transform;
        if (target is Component componentTarget) return componentTarget.transform;
        return null;
    }

    private static bool IsValidTarget(Transform target)
    {
        return target != null && target.gameObject.activeInHierarchy;
    }

    private Transform FindNearestByTag(string tag, float maxDistance = float.PositiveInfinity, bool includeSelf = true)
    {
        GameObject[] objects = null;

        try
        {
            objects = GameObject.FindGameObjectsWithTag(tag);
        }
        catch
        {
            objects = null;
        }

        Transform nearest = null;
        float nearestSqDistance = float.PositiveInfinity;
        float maxSqDistance = float.IsPositiveInfinity(maxDistance) ? float.PositiveInfinity : maxDistance * maxDistance;

        if (objects != null)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null) continue;
                if (!includeSelf && candidate.transform == transform) continue;

                float sqDistance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqDistance > maxSqDistance) continue;

                if (sqDistance < nearestSqDistance)
                {
                    nearestSqDistance = sqDistance;
                    nearest = candidate.transform;
                }
            }
        }

        if (nearest != null || !fallbackToNameSearch || string.IsNullOrWhiteSpace(tag))
        {
            return nearest;
        }

        string match = tag.ToLowerInvariant();
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null || candidate == transform) continue;
            if (!candidate.gameObject.activeInHierarchy) continue;

            string candidateName = candidate.name.ToLowerInvariant();
            if (!candidateName.Contains(match)) continue;

            float sqDistance = (candidate.position - transform.position).sqrMagnitude;
            if (sqDistance > maxSqDistance) continue;

            if (sqDistance < nearestSqDistance)
            {
                nearestSqDistance = sqDistance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private void TryAssignAutoCommand(float currentTime)
    {
        if (!autoAssignCombatCommands || currentCommand == null) return;

        if (currentCommand.IsExpired(currentTime))
        {
            currentCommand.LastFailureReason = FailureReason.CommandExpired;
            ClearCommand();
        }

        if (currentCommand.Type == CommandType.SupportTarget)
        {
            Transform supportTarget = ResolveSupportTarget();
            if (!IsSupportActionNeeded(supportTarget))
            {
                ClearCommand();
            }
        }

        if (currentCommand.Type != CommandType.None) return;

        if (roleType == MinionRoleType.Support)
        {
            Transform supportTarget = ResolveSupportTarget();
            if (IsSupportActionNeeded(supportTarget))
            {
                SetSupportCommand(supportTarget);
                return;
            }
        }
        else
        {
            Transform enemy = FindNearestByTag(enemyTag, autoTargetRadius);
            if (enemy != null)
            {
                SetAttackEnemyCommand(enemy);
                return;
            }
        }

        if (followTarget != null)
        {
            SetFollowCommand();
        }
    }

    private bool IsSupportActionNeeded(Transform supportTarget)
    {
        if (!IsValidTarget(supportTarget) || currentRole == null) return false;

        SupportMode mode = currentRole.GetSupportMode() ?? SupportMode.Heal;
        switch (mode)
        {
            case SupportMode.Heal:
            {
                CombatantStats targetStats = supportTarget.GetComponentInParent<CombatantStats>();
                if (targetStats == null || targetStats.IsDead) return false;

                float maxHealth = targetStats.GetStat(CombatStatType.MaxHealth);
                return targetStats.CurrentHealth < maxHealth - 0.01f;
            }

            case SupportMode.Buff:
                return supportBuffEffect != null;

            case SupportMode.Debuff:
                return supportDebuffEffect != null;

            default:
                return false;
        }
    }

    private bool SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * Mathf.Max(0.01f, groundRayStartHeight);

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, Mathf.Max(0.1f, groundRayLength), groundMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
        {
            return false;
        }

        Vector3 position = transform.position;
        position.y = hit.point.y + groundOffset;
        transform.position = position;
        return true;
    }

    private void ApplyGravity(float deltaTime)
    {
        if (!useSimpleGravity) return;

        if (isGrounded)
        {
            verticalVelocity = 0f;
            return;
        }

        verticalVelocity -= Mathf.Max(0f, gravityAcceleration) * deltaTime;
        verticalVelocity = Mathf.Max(-Mathf.Max(0f, maxFallSpeed), verticalVelocity);
        transform.position += Vector3.up * (verticalVelocity * deltaTime);
    }

    private bool EvaluateLineOfSight(Transform target)
    {
        if (debugOverrideLineOfSight) return debugLineOfSightValue;
        if (!IsValidTarget(target)) return false;

        Vector3 start = transform.position + Vector3.up * 0.8f;
        Vector3 end = target.position + Vector3.up * 0.8f;

        if (Physics.Linecast(start, end, out RaycastHit hit, lineOfSightBlockMask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == target || hit.transform.IsChildOf(target);
        }

        return true;
    }

    private bool EvaluateAbilityReady(Transform target)
    {
        if (debugOverrideAbilityReady) return debugAbilityReadyValue;
        if (!IsValidTarget(target) || abilitySystem == null) return false;

        return abilitySystem.HasAnyReadyAbility(transform, target, Time.time, sharedCombatStats, roleType);
    }

    private float GetRuntimeMoveSpeed()
    {
        if (sharedCombatStats == null)
        {
            return moveSpeed;
        }

        return Mathf.Max(0.1f, sharedCombatStats.GetStat(CombatStatType.MoveSpeed));
    }


    private void HandleMissingCombatTarget()
    {
        if (currentCommand != null)
        {
            currentCommand.LastFailureReason = FailureReason.TargetLost;
        }

        combatPhaseController.Reset();

        if (followTarget != null)
        {
            SetFollowCommand();
            stateMachine.ForceState(MinionState.Follow);
        }
        else
        {
            ClearCommand();
            stateMachine.ForceState(MinionState.Idle);
        }
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
        if (target == transform)
        {
            if (followTarget != null && followTarget != transform)
            {
                SetFollowCommand();
            }
            else
            {
                ClearCommand();
            }

            return;
        }

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
        currentTarget = FindNearestByTag(enemyTag, autoTargetRadius);
    }

    public void DebugSetLineOfSight(bool value)
    {
        debugOverrideLineOfSight = true;
        debugLineOfSightValue = value;
        hasLineOfSight = value;
    }

    public void DebugSetAbilityReady(bool value)
    {
        debugOverrideAbilityReady = true;
        debugAbilityReadyValue = value;
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