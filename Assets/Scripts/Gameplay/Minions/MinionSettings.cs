using UnityEngine;

[CreateAssetMenu(menuName = "Minions/Minion Settings")]
public class MinionSettings : ScriptableObject
{
    [Header("Tick Rates")]
    [Tooltip("How often the minion updates its state and behavior (in seconds).")]
    public MinionTickRates TickRates;

    [Header("Role Settings")]
    [Tooltip("Settings specific to melee minions.")]
    public RoleSettings Melee;
    [Tooltip("Settings specific to ranged minions.")]
    public RoleSettings Ranged;
    [Tooltip("Settings specific to support minions.")]
    public SupportRoleSettings Support;
}

// Base settings shared by most roles.
[System.Serializable]
public class RoleSettings 
{
    [Tooltip("Base combat stats for this role (health, damage, etc.).")]
    public RangePolicy RangePolicy;
}

// Extended settings for support-specific behavior.
[System.Serializable]
public class SupportRoleSettings : RoleSettings
{
    [Tooltip("The starting mode for support minions.")]
    public SupportMode StartMode = SupportMode.Heal;
}