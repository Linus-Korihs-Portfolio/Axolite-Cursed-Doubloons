using UnityEngine;

[CreateAssetMenu(menuName = "Minions/Minion Settings")]
public class MinionSettings : ScriptableObject
{
    [Header("Tick Rates")]
    public MinionTickRates TickRates;

    [Header("Role Settings")]
    public RoleSettings Melee;
    public RoleSettings Ranged;
    public SupportRoleSettings Support;
}

// Base settings shared by most roles.
[System.Serializable]
public class RoleSettings 
{
    public RangePolicy RangePolicy;
}

// Extended settings for support-specific behavior.
[System.Serializable]
public class SupportRoleSettings : RoleSettings
{
    public SupportMode StartMode = SupportMode.Heal;
}