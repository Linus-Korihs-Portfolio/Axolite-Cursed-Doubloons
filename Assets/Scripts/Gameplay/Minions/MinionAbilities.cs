using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MinionStats
{
    public float Damage = 10f;
    public float HealAmount = 8f;
    public float CastCooldownMultiplier = 1f;
}

// Simple stat modifier for later item/loadout support.
[System.Serializable]
public class StatModifier
{
    public float BonusDamage;
    public float BonusHealAmount;
    public float CooldownMultiplier = 1f;
}

// Rebuilds final runtime stats from a base profile plus modifiers.
public class MinionStatsSystem
{
    public MinionStats FinalStats { get; private set; } = new MinionStats();

    public void Rebuild(MinionStats baseStats, List<StatModifier> modifiers)
    {
        if (baseStats == null)
        {
            FinalStats = new MinionStats();
            return;
        }

        // Start from clean base every time.
        FinalStats = new MinionStats
        {
            Damage = baseStats.Damage,
            HealAmount = baseStats.HealAmount,
            CastCooldownMultiplier = baseStats.CastCooldownMultiplier
        };

        if (modifiers == null) return;

        for (int i = 0; i < modifiers.Count; i++)
        {
            StatModifier mod = modifiers[i];
            if (mod == null) continue;

            FinalStats.Damage += mod.BonusDamage;
            FinalStats.HealAmount += mod.BonusHealAmount;
            FinalStats.CastCooldownMultiplier *= mod.CooldownMultiplier;
        }
    }
}

// Shared runtime contract for minion abilities.
public abstract class AbilityBase
{
    public string Id { get; protected set; }
    public float Cooldown { get; protected set; }
    public float Range { get; protected set; }
    public TargetType TargetType { get; protected set; }

    protected float lastUseTime = -999f;

    // Checks if cooldown is ready.
    public virtual bool IsReady(float currentTime, MinionStats stats)
    {
        float finalCooldown = Cooldown;
        if (stats != null)
        {
            finalCooldown *= Mathf.Max(0.01f, stats.CastCooldownMultiplier);
        }

        return currentTime >= lastUseTime + finalCooldown;
    }

    // Checks if the target type and distance are valid.
    public virtual bool CanUse(Transform caster, Transform target, float currentTime, MinionStats stats)
    {
        if (caster == null || target == null) return false;
        if (!IsReady(currentTime, stats)) return false;

        float distance = Vector3.Distance(caster.position, target.position);
        return distance <= Range;
    }

    // Executes the ability.
    public bool TryUse(Transform caster, Transform target, float currentTime, MinionStats stats)
    {
        if (!CanUse(caster, target, currentTime, stats)) return false;

        Execute(caster, target, stats);
        lastUseTime = currentTime;
        return true;
    }

    // Concrete behavior lives here.
    protected abstract void Execute(Transform caster, Transform target, MinionStats stats);
}

// Simple melee damage ability.
public class MeleeAttackAbility : AbilityBase
{
    public MeleeAttackAbility(float damageRange, float cooldown)
    {
        Id = "MeleeAttack";
        Range = damageRange;
        Cooldown = cooldown;
        TargetType = TargetType.Enemy;
    }

    protected override void Execute(Transform caster, Transform target, MinionStats stats)
    {
        float damage = stats != null ? stats.Damage : 0f;
        Debug.Log($"[{caster.name}] used {Id} on [{target.name}] for {damage} damage.");
    }
}

// Simple ranged damage ability.
public class RangedAttackAbility : AbilityBase
{
    public RangedAttackAbility(float damageRange, float cooldown)
    {
        Id = "RangedAttack";
        Range = damageRange;
        Cooldown = cooldown;
        TargetType = TargetType.Enemy;
    }

    protected override void Execute(Transform caster, Transform target, MinionStats stats)
    {
        float damage = stats != null ? stats.Damage : 0f;
        Debug.Log($"[{caster.name}] fired {Id} at [{target.name}] for {damage} damage.");
    }
}

// Support ability that changes behavior based on mode.
public class SupportAbility : AbilityBase
{
    private readonly SupportMode supportMode;

    public SupportAbility(float castRange, float cooldown, SupportMode mode)
    {
        supportMode = mode;
        Id = $"Support_{mode}";
        Range = castRange;
        Cooldown = cooldown;

        // Heal/Buff usually target allies, Debuff targets enemies.
        TargetType = mode == SupportMode.Debuff ? TargetType.Enemy : TargetType.Ally;
    }

    protected override void Execute(Transform caster, Transform target, MinionStats stats)
    {
        switch (supportMode)
        {
            case SupportMode.Heal:
                Debug.Log($"[{caster.name}] cast Heal on [{target.name}] for {stats?.HealAmount ?? 0f} HP.");
                break;

            case SupportMode.Buff:
                Debug.Log($"[{caster.name}] cast Buff on [{target.name}].");
                break;

            case SupportMode.Debuff:
                Debug.Log($"[{caster.name}] cast Debuff on [{target.name}].");
                break;
        }
    }
}

// Selects and executes abilities for the minion.
public class MinionAbilitySystem
{
    private readonly List<AbilityBase> equippedAbilities = new List<AbilityBase>();

    public IReadOnlyList<AbilityBase> EquippedAbilities => equippedAbilities;

    public void Clear()
    {
        equippedAbilities.Clear();
    }

    public void AddAbility(AbilityBase ability)
    {
        if (ability == null) return;
        equippedAbilities.Add(ability);
    }

    // Rebuilds a very small default loadout based on role.
    public void BuildDefaultLoadout(IMinionRole role)
    {
        Clear();

        if (role == null) return;

        RangePolicy policy = role.GetRangePolicy();
        if (policy == null) return;

        switch (role.RoleType)
        {
            case MinionRoleType.Melee:
                AddAbility(new MeleeAttackAbility(policy.MaxRange, 1.0f));
                break;

            case MinionRoleType.Ranged:
                AddAbility(new RangedAttackAbility(policy.MaxRange, 1.25f));
                break;

            case MinionRoleType.Support:
                SupportMode mode = role.GetSupportMode() ?? SupportMode.Heal;
                AddAbility(new SupportAbility(policy.MaxRange, 2.0f, mode));
                break;
        }
    }

    // Tries to use the best offensive ability for the current target.
    public bool TryUseBestAbility(
        Transform caster,
        Transform target,
        float currentTime,
        MinionStats stats,
        MinionRoleType roleType)
    {
        AbilityBase best = SelectBestAbility(caster, target, currentTime, stats, roleType);
        if (best == null) return false;

        return best.TryUse(caster, target, currentTime, stats);
    }

    private AbilityBase SelectBestAbility(
        Transform caster,
        Transform target,
        float currentTime,
        MinionStats stats,
        MinionRoleType roleType)
    {
        AbilityBase best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < equippedAbilities.Count; i++)
        {
            AbilityBase ability = equippedAbilities[i];
            if (ability == null) continue;
            if (!ability.CanUse(caster, target, currentTime, stats)) continue;

            float score = ScoreAbility(ability, caster, target, roleType);
            if (score > bestScore)
            {
                bestScore = score;
                best = ability;
            }
        }

        return best;
    }

    // Very small scoring model for now.
    private float ScoreAbility(AbilityBase ability, Transform caster, Transform target, MinionRoleType roleType)
    {
        float distance = Vector3.Distance(caster.position, target.position);
        float score = 0f;

        // Prefer abilities that fit the current distance better.
        score -= Mathf.Abs(ability.Range - distance);

        // Small role preference bonus.
        switch (roleType)
        {
            case MinionRoleType.Melee:
                if (ability is MeleeAttackAbility) score += 10f;
                break;

            case MinionRoleType.Ranged:
                if (ability is RangedAttackAbility) score += 10f;
                break;

            case MinionRoleType.Support:
                if (ability is SupportAbility) score += 10f;
                break;
        }

        return score;
    }
}