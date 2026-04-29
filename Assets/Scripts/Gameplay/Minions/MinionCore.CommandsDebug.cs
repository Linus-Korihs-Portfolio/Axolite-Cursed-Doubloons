using UnityEngine;

public partial class MinionCore
{
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

        if (currentCommand.Type == CommandType.None && followTarget != null && ShouldFollowTarget())
        {
            SetFollowCommand();
            return;
        }

        // Combat auto-targeting is optional; follow fallback above always keeps the minion attached to its owner.
        if (!autoAssignCombatCommands) return;
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

        currentCommand = new MinionCommand
        {
            Type = CommandType.AttackEnemy,
            Target = target,
            TargetPosition = target != null ? target.position : transform.position,
            Priority = 10,
            IssuedTime = Time.time,
            TimeToLive = 999f,
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

        currentCommand = new MinionCommand
        {
            Type = CommandType.AttackObject,
            Target = target,
            TargetPosition = target != null ? target.position : transform.position,
            Priority = 10,
            IssuedTime = Time.time,
            TimeToLive = 999f,
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
        abilitySystem.BuildDefaultLoadout(currentRole, supportBuffEffect, supportDebuffEffect, abilityCooldown);

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
        if (followTarget != null && target == followTarget) return false;
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

        RoleSettings roleSettings = settings.GetForRole(roleType);

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
}
