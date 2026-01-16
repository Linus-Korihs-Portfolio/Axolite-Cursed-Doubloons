// TileSettingsSO.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum TileRole
{
    Normal,
    Start,
    End
}

[CreateAssetMenu(menuName = "PCG/Tile Settings")]
public class TileSettingsSO : ScriptableObject
{
    [Header("Grid")]
    public float cellSize = 3f;

    [Header("Height Matching")]
    [Tooltip("Tolerance for float height comparisons (e.g., 0.01).")]
    public float heightEpsilon = 0.01f;

    [Header("Start/End placement")]
    public bool placeStartAndEnd = true;

    [Tooltip("If true, Start/End are placed on border cells only.")]
    public bool startEndOnBorder = false;

    [Header("Tiles")]
    public List<TileEntry> tiles = new();
}

[Serializable]
public class TileEntry
{
    public GameObject prefab;

    [Min(0)] public int weight = 10;
    public bool allowRotation = true;

    [Header("Role")]
    public TileRole role = TileRole.Normal;

    [Header("Occurrence constraints (optional)")]
    [Min(0)] public int minCount = 0;
    [Min(0)] public int maxCount = 0; // 0 = unlimited
}
