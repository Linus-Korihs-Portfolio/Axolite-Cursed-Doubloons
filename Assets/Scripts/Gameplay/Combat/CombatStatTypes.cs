using System;
using System.Collections.Generic;
using UnityEngine;

// Centralized definitions for combat stats and modifiers.
public enum CombatStatType
{
    MaxHealth,
    Damage,
    HealPower,
    MoveSpeed,
    AttackSpeed,
    Defense
}

// Defines how a modifier affects a stat (additive or multiplicative). Baserule: (Base + Additive) * Multiplicative.
public enum StatModifierOperation
{
    Add, // Additive (Health + 20, Damage + 5, etc.)
    Multiply // Multiplicative (Health * 1.5, Damage * 0.8, etc.)
}

[Serializable]
// Represents a single combat stat value.
public struct CombatStatValue
{
    public CombatStatType Type; // E.g., Damage
    public float Value; // E.g., 10 for Damage
}

[Serializable]
// Represents a modifier applied to a combat stat.
public struct CombatStatModifierData
{
    public CombatStatType Type; // E.g., Damage
    public StatModifierOperation Operation; // E.g., Additive or Multiplicative
    public float Value; // E.g., 5 for Additive (+5 Dmg), 1.2 for Multiplicative (+20% Dmg)
}

[CreateAssetMenu(menuName = "Combat/Stats Profile")]
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
