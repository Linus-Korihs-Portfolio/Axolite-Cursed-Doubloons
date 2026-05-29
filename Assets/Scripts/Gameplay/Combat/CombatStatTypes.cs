using System;
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


