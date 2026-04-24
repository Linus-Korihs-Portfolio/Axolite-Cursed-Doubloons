using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CombatantStats))]
public class MinionAgent : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private MinionSettings settings;
    [SerializeField] private MinionRoleType roleType = MinionRoleType.Melee;

    [Header("Runtime Targets")]
    [SerializeField] private Transform followTarget;

    [Header("Auto Targeting")]
    [SerializeField] private bool autoAssignCombatCommands = false;
    [SerializeField] private float autoTargetRadius = 35f;
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string allyTag = "Ally";
    [SerializeField] private string breakableTag = "Breakable";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float followStopDistance = 1.75f;

    [Header("Navigation (NavMesh)")]
    [SerializeField] private bool useNavMeshNavigation = true;
    [SerializeField] private float navRepathInterval = 0.2f;
    [SerializeField] private float navTargetSampleRadius = 1.25f;
    [SerializeField] private float navWaypointTolerance = 0.25f;
    [SerializeField] private bool recallOnPathFailure = true;
    [SerializeField] private float pathFailureCooldown = 0.75f;

    [Header("Minion Separation")]
    [SerializeField] private bool useLocalSeparation = true;
    [SerializeField] private float separationRadius = 0.9f;
    [SerializeField] private float separationStrength = 7f;
    [SerializeField] private float maxSeparationStep = 0.3f;
    [SerializeField] private LayerMask separationMask = ~0;

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
    [SerializeField] private float lineOfSightHeightOffset = 0.8f;
    [SerializeField] private bool requireLineOfSightForAllAttacks = true;

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
    private NavMeshPath navPath;
    private int navCornerIndex;
    private bool hasNavPath;
    private float nextNavRepathTime;
    private Vector3 navLastDestination;
    private float nextPathFailureRecoveryTime;
    private readonly RaycastHit[] lineOfSightHits = new RaycastHit[16];

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
    private readonly Collider[] separationHits = new Collider[24];

    private MinionIntent currentIntent;

    public MinionRoleType RoleType => roleType; // Exposed runtime metadata for commander/input systems.
    public SupportMode? ActiveSupportMode => currentRole != null ? currentRole.GetSupportMode() : null;

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
        navPath = new NavMeshPath();

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

        // Keep nearby minions from stacking into the same spot.
        ApplyLocalSeparation(Time.deltaTime);

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
        Transform target = ResolveFollowTarget();
        if (!IsValidTarget(target))
        {
            ClearCommand();
            stateMachine.ForceState(MinionState.Idle);
            return;
        }

        // Calculate horizontal distance (ignore Y to match MoveTowardsDistance)
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance <= Mathf.Max(0f, followStopDistance))
        {
            if (distance > 0.0001f)
            {
                SmoothFaceDirection(toTarget / Mathf.Max(distance, 0.0001f));
            }

            return;
        }

        MoveTowardsDistance(target.position, followStopDistance);
    }

    private Transform ResolveFollowTarget()
    {
        if (IsValidTarget(followTarget)) return followTarget;

        Transform commandTarget = AsTransform(currentCommand != null ? currentCommand.Target : null);
        if (IsValidTarget(commandTarget)) return commandTarget;

        return null;
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

                bool canAttack = !requireLineOfSightForAllAttacks || hasLineOfSight;
                if (canAttack)
                {
                    abilitySystem.TryUseBestAbility(transform, currentTarget, currentTime, sharedCombatStats, roleType);
                }
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

            ResetNavigationPath();
            return;
        }

        // NavMesh drives around corners/obstacles while keeping command/state logic unchanged.
        if (useNavMeshNavigation)
        {
            Vector3 stopPoint = targetPosition;

            if (stopDistance > 0.001f)
            {
                stopPoint -= (toTarget / Mathf.Max(distance, 0.0001f)) * stopDistance;
                stopPoint.y = targetPosition.y;
            }

            if (TryMoveAlongNavPath(stopPoint))
            {
                return;
            }

            HandleUnreachablePath();
            return;
        }

        Vector3 direction = toTarget / Mathf.Max(distance, 0.0001f);
        transform.position += direction * GetRuntimeMoveSpeed() * Time.deltaTime;
        SmoothFaceDirection(direction);
    }

    private bool TryMoveAlongNavPath(Vector3 desiredDestination)
    {
        float now = Time.time;
        float repathInterval = Mathf.Max(0.05f, navRepathInterval);

        if (!hasNavPath || now >= nextNavRepathTime || (navLastDestination - desiredDestination).sqrMagnitude > 0.35f * 0.35f)
        {
            if (!TryBuildNavPath(desiredDestination))
            {
                return false;
            }
        }

        Vector3[] corners = navPath.corners;
        if (corners == null || corners.Length == 0)
        {
            hasNavPath = false;
            return false;
        }

        navCornerIndex = Mathf.Clamp(navCornerIndex, 1, corners.Length - 1);
        float cornerTolerance = Mathf.Max(0.05f, navWaypointTolerance);

        while (navCornerIndex < corners.Length)
        {
            Vector3 toCorner = corners[navCornerIndex] - transform.position;
            toCorner.y = 0f;

            if (toCorner.sqrMagnitude <= cornerTolerance * cornerTolerance)
            {
                navCornerIndex++;
                continue;
            }

            Vector3 direction = toCorner.normalized;
            transform.position += direction * GetRuntimeMoveSpeed() * Time.deltaTime;
            SmoothFaceDirection(direction);
            return true;
        }

        return true;
    }

    private bool TryBuildNavPath(Vector3 desiredDestination)
    {
        if (navPath == null)
        {
            navPath = new NavMeshPath();
        }

        nextNavRepathTime = Time.time + Mathf.Max(0.05f, navRepathInterval);

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 1.0f, NavMesh.AllAreas))
        {
            hasNavPath = false;
            return false;
        }

        if (!NavMesh.SamplePosition(desiredDestination, out NavMeshHit destinationHit, Mathf.Max(0.1f, navTargetSampleRadius), NavMesh.AllAreas))
        {
            hasNavPath = false;
            return false;
        }

        bool calculated = NavMesh.CalculatePath(startHit.position, destinationHit.position, NavMesh.AllAreas, navPath);
        if (!calculated || navPath.status != NavMeshPathStatus.PathComplete || navPath.corners == null || navPath.corners.Length < 2)
        {
            hasNavPath = false;
            return false;
        }

        hasNavPath = true;
        navCornerIndex = 1;
        navLastDestination = desiredDestination;
        return true;
    }

    private void HandleUnreachablePath()
    {
        ResetNavigationPath();

        if (Time.time < nextPathFailureRecoveryTime)
        {
            return;
        }

        nextPathFailureRecoveryTime = Time.time + Mathf.Max(0.1f, pathFailureCooldown);

        if (currentCommand != null)
        {
            currentCommand.LastFailureReason = FailureReason.TargetLost;
        }

        if (recallOnPathFailure && currentCommand != null && currentCommand.Type != CommandType.Recall && IsValidTarget(followTarget))
        {
            SetRecallCommand();
            return;
        }

        ClearCommand();
        stateMachine.ForceState(MinionState.Idle);
    }

    private void ResetNavigationPath()
    {
        hasNavPath = false;
        navCornerIndex = 0;
        nextNavRepathTime = 0f;
        navLastDestination = transform.position;
    }

    private void ApplyLocalSeparation(float deltaTime)
    {
        if (!useLocalSeparation) return;

        float radius = Mathf.Max(0.05f, separationRadius);
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            separationHits,
            separationMask,
            QueryTriggerInteraction.Ignore
        );

        if (hitCount <= 0) return;

        Vector3 push = Vector3.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = separationHits[i];
            if (hit == null) continue;

            MinionAgent other = hit.GetComponentInParent<MinionAgent>();
            if (other == null || other == this) continue;

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;

            float distance = away.magnitude;
            if (distance >= radius) continue;

            Vector3 dir = distance > 0.0001f ? away / distance : (transform.forward.sqrMagnitude > 0.0001f ? transform.forward : Vector3.right);
            float overlap = radius - distance;
            float weight = overlap / radius;
            push += dir * weight;
        }

        if (push.sqrMagnitude <= 0.000001f) return;

        Vector3 step = push * Mathf.Max(0f, separationStrength) * deltaTime;
        step.y = 0f;

        float maxStep = Mathf.Max(0.01f, maxSeparationStep);
        if (step.magnitude > maxStep)
        {
            step = step.normalized * maxStep;
        }

        transform.position += step;
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

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        return toTarget.magnitude;
    }

    private Transform ResolveActiveTarget()
    {
        Transform explicitTarget = AsTransform(currentCommand != null ? currentCommand.Target : null);
        if (IsValidTarget(explicitTarget)) return explicitTarget;

        return null;
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
        if (string.IsNullOrWhiteSpace(tag)) return null;

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
        return nearest;
    }

    private void TryAssignAutoCommand(float currentTime)
    {
        if (currentCommand == null) return;

        if (currentCommand.IsExpired(currentTime))
        {
            currentCommand.LastFailureReason = FailureReason.CommandExpired;
            ClearCommand();
        }

        if (currentCommand.Type == CommandType.SupportTarget)
        {
            Transform supportTarget = AsTransform(currentCommand.Target);
            if (!IsSupportTargetValidForActiveMode(supportTarget))
            {
                ClearCommand();
            }
        }

        // Optional legacy auto behavior for prototyping only.
        if (!autoAssignCombatCommands) return;

        if (currentCommand.Type == CommandType.None && followTarget != null && ShouldFollowTarget())
        {
            SetFollowCommand();
        }
    }

    private bool ShouldFollowTarget()
    {
        Transform target = ResolveFollowTarget();
        if (!IsValidTarget(target)) return false;

        // Calculate horizontal distance (ignore Y to match ExecuteFollow & MoveTowardsDistance)
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        return distance > Mathf.Max(0f, followStopDistance) + 0.2f;
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

        Vector3 start = transform.position + Vector3.up * Mathf.Max(0f, lineOfSightHeightOffset);
        Vector3 end = target.position + Vector3.up * Mathf.Max(0f, lineOfSightHeightOffset);
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        if (distance <= 0.0001f) return true;

        int hitCount = Physics.RaycastNonAlloc(
            start,
            direction / distance,
            lineOfSightHits,
            distance,
            lineOfSightBlockMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0) return true;

        Array.Sort(lineOfSightHits, 0, hitCount, RaycastHitDistanceComparer.Instance);

        for (int i = 0; i < hitCount; i++)
        {
            Transform hitTransform = lineOfSightHits[i].transform;
            if (hitTransform == null) continue;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            if (hitTransform == target || hitTransform.IsChildOf(target))
            {
                return true;
            }

            return false;
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
        ResetNavigationPath();

        if (currentCommand != null)
        {
            currentCommand.LastFailureReason = FailureReason.TargetLost;
        }

        combatPhaseController.Reset();
        ClearCommand();
        stateMachine.ForceState(MinionState.Idle);
    }

    // Makes the minion return to the player/follow behavior.
    public void SetFollowCommand()
    {
        ResetNavigationPath();

        currentCommand = new MinionCommand
        {
            Type = CommandType.FollowPlayer,
            Target = followTarget,
            TargetPosition = followTarget != null ? followTarget.position : transform.position,
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
        ResetNavigationPath();

        currentCommand = new MinionCommand
        {
            Type = CommandType.Recall,
            Target = followTarget,
            TargetPosition = followTarget != null ? followTarget.position : transform.position,
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
        if (!IsEnemyTarget(target))
        {
            ClearCommand();
            return;
        }

        ResetNavigationPath();

        currentTarget = target;

        currentCommand = new MinionCommand
        {
            Type = CommandType.AttackEnemy,
            Target = target,
            TargetPosition = target != null ? target.position : transform.position,
            Priority = 10,
            IssuedTime = Time.time,
            TimeToLive = 999f, // After long time expire for safety. Should persist until enemy is dead or a new command is given.
            Source = CommandSource.Player,
            InterruptPolicy = InterruptPolicy.Soft,
            LastFailureReason = FailureReason.None
        };
    }

    // Makes the minion attack a breakable object.
    public void SetAttackObjectCommand(Transform target)
    {
        if (!IsBreakableTarget(target))
        {
            ClearCommand();
            return;
        }

        ResetNavigationPath();

        currentTarget = target;

        currentCommand = new MinionCommand
        {
            Type = CommandType.AttackObject,
            Target = target,
            TargetPosition = target != null ? target.position : transform.position,
            Priority = 10,
            IssuedTime = Time.time,
            TimeToLive = 999f, // After long time expire for safety. Should persist until object is destroyed or a new command is given.
            Source = CommandSource.Player,
            InterruptPolicy = InterruptPolicy.Soft,
            LastFailureReason = FailureReason.None
        };
    }

    // Makes the support minion act on a target.
    public void SetSupportCommand(Transform target)
    {
        if (!IsSupportTargetValidForActiveMode(target))
        {
            ClearCommand();
            return;
        }

        ResetNavigationPath();

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

    // Allows command systems to preview if a target is valid for this support minion right now.
    public bool CanAcceptSupportTarget(Transform target)
    {
        return IsSupportTargetValidForActiveMode(target);
    }

    // Switches the active support action and rebuilds support ability loadout.
    public bool TrySetSupportMode(SupportMode newMode)
    {
        if (currentRole is not SupportRole supportRole)
        {
            return false;
        }

        supportRole.SetSupportMode(newMode);
        abilitySystem.BuildDefaultLoadout(currentRole, supportBuffEffect, supportDebuffEffect);

        // Invalidate current support command if target no longer matches the new mode.
        if (currentCommand != null && currentCommand.Type == CommandType.SupportTarget)
        {
            Transform supportTarget = AsTransform(currentCommand.Target);
            if (!IsSupportTargetValidForActiveMode(supportTarget))
            {
                ClearCommand();
                stateMachine.ForceState(MinionState.Idle);
            }
        }

        return true;
    }

    private bool IsEnemyTarget(Transform target)
    {
        if (!IsValidTarget(target)) return false;
        return target.CompareTag(enemyTag);
    }

    private bool IsBreakableTarget(Transform target)
    {
        if (!IsValidTarget(target)) return false;
        return target.CompareTag(breakableTag);
    }

    private bool IsAllyMinionTarget(Transform target)
    {
        if (!IsValidTarget(target)) return false;
        if (target == transform) return false;
        if (followTarget != null && target == followTarget) return false; // Player cannot be healed/buffed by support minions.
        return target.CompareTag(allyTag);
    }

    // Validates target rules for the currently active support mode.
    private bool IsSupportTargetValidForActiveMode(Transform target)
    {
        if (!IsValidTarget(target) || currentRole == null) return false;

        SupportMode mode = currentRole.GetSupportMode() ?? SupportMode.Heal;
        switch (mode)
        {
            case SupportMode.Heal:
            {
                if (!IsAllyMinionTarget(target)) return false;

                CombatantStats targetStats = target.GetComponentInParent<CombatantStats>();
                if (targetStats == null || targetStats.IsDead) return false;

                float maxHealth = targetStats.GetStat(CombatStatType.MaxHealth);
                return targetStats.CurrentHealth < maxHealth - 0.01f;
            }

            case SupportMode.Buff:
                return supportBuffEffect != null && IsAllyMinionTarget(target);

            case SupportMode.Debuff:
                return supportDebuffEffect != null && IsEnemyTarget(target);

            default:
                return false;
        }
    }

    // Clears the current command and returns to idle fallback.
    public void ClearCommand()
    {
        ResetNavigationPath();

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

        // Draw only one circle for the active role to avoid mixed range visuals.
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.9f);
        Gizmos.DrawWireSphere(p, rp.MaxRange);

        if (currentTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(p, currentTarget.position);
        }
    }

    private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

        public int Compare(RaycastHit x, RaycastHit y)
        {
            return x.distance.CompareTo(y.distance);
        }
    }
}