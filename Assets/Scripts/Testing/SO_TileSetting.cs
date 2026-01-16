using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PCG/Tile Settings")]
public class TileSettingsSO : ScriptableObject
{
    public float cellSize = 3f;
    public float heightEpsilon = 0.01f;  // Tolerance for height comparisons

    public List<TileEntry> tiles = new();
}

[Serializable]
public class TileEntry
{
    /* public string id;   // optional: "Normal", "Curve", ... */
    public GameObject prefab;

    [Min(0)] public int weight = 10;
    public bool allowRotation = true;

    [Header("Occurrence constraints (optional)")]
    [Min(0)] public int minCount = 0;
    [Min(0)] public int maxCount = 0; // 0 = unlimited
}
