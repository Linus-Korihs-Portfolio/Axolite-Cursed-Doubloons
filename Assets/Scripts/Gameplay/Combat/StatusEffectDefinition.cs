using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SO/Combat/Status Effect")]
// Buffs and Debuffs defined as ScriptableObjects for easy creation & tweaking.
public class StatusEffectDefinition : ScriptableObject 
{
    [Tooltip("Unique identifier for this status effect (used for stacking, refreshing, etc.)")]
    public string EffectId = "Effect";
    [Tooltip("Indicates whether this status effect is a debuff. More of a tag for logic than anything else.")]
    public bool IsDebuff = false;
    [Tooltip("Duration of the status effect in seconds.")]
    public float Duration = 3f;
    [Tooltip("Maximum number of stacks for this status effect.")]
    public int MaxStacks = 1;
    [Tooltip("Indicates whether the duration should be refreshed when the effect is reapplied.")]
    public bool RefreshDurationOnReapply = true;

    [Header("Periodic")]
    [Tooltip("Interval in seconds between each tick of the status effect. Set to 0 for no periodic effect.")]
    public float TickInterval = 0f;
    [Tooltip("Amount of damage dealt each tick.")]
    public float PeriodicDamage = 0f;
    [Tooltip("Amount of healing applied each tick.")]
    public float PeriodicHeal = 0f;

    [Header("Stat Modifiers")]
    [Tooltip("List of combat stat modifiers applied by this status effect.")]
    public List<CombatStatModifierData> Modifiers = new List<CombatStatModifierData>();
}
