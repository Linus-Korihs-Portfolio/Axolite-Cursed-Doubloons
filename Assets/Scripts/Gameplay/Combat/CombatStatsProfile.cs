using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SO/Combat/Stats Profile")]
public class CombatStatsProfile : ScriptableObject
{
    public List<CombatStatValue> BaseStats = new List<CombatStatValue> // Default base stats (can be overridden by specific profiles)
    {
        new CombatStatValue { Type = CombatStatType.MaxHealth, Value = 100f },
        new CombatStatValue { Type = CombatStatType.Damage, Value = 10f },
        new CombatStatValue { Type = CombatStatType.HealPower, Value = 8f },
        new CombatStatValue { Type = CombatStatType.MoveSpeed, Value = 4f },
        new CombatStatValue { Type = CombatStatType.AttackSpeed, Value = 1f },
        new CombatStatValue { Type = CombatStatType.Defense, Value = 1f }
    };

    public float GetBaseStat(CombatStatType type) // Search for the base value of a stat in the profile
    {
        for (int i = 0; i < BaseStats.Count; i++)
        {
            if (BaseStats[i].Type == type)
            {
                return BaseStats[i].Value;
            }
        }

        switch (type) // Fallback base values if not defined in profile (prevents errors and ensures all stats have a baseline)
        {
            case CombatStatType.AttackSpeed:
            case CombatStatType.Defense:
                return 1f; // Default 1 for multiplicative stats (no change)
            default:
                return 0f; // Default 0 for additive stats (no change)
        }
    }
}
