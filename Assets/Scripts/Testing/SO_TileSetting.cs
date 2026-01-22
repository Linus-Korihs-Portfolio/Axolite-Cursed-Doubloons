using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "WFC/Tile Settings", fileName = "TileSettings")]
public class SO_TileSetting : ScriptableObject
{
	[Header("Grid")]
	public int width = 4;
	public int height = 4;
	public bool autoCellSize = true;
	public float cellSize = 1f;
	public float heightEpsilon = 0.01f;

	[Header("Seed")]
	public bool useRandomSeed = true;
	public int seed = 0;

	[Header("Border")]
	public bool closedBorder = true;

	[Header("Start / End")]
	public bool useStartEnd = false;
	public bool startOnBorderOnly = true;
	public bool endOnBorderOnly = true;
	public int minStartEndDistance = 2;

	[Header("Counts")]
	public bool enforceCounts = true;
	public int hardMaxTolerance = 2;

	[Header("Retry")]
	public int maxRetries = 20;
	public bool logSteps = false;

	[Header("Tiles")]
	public List<TileEntry> tiles = new();
}

public enum TileRole
{
	Normal,
	Start,
	End,
	Wall
}

[Serializable]
public class TileEntry
{
	[Tooltip("Optional id to recognize the tile in logs.")]
	public string id = "Tile";

	[Tooltip("Prefab that contains four TileSocket components (N/E/S/W).")]
	public GameObject prefab;

	[Tooltip("How this tile is allowed to be used.")]
	public TileRole role = TileRole.Normal;

	[Tooltip("Allow automatic 90 degree rotations when building options.")]
	public bool allowRotation = true;

	[Tooltip("Weight used for the random pick during collapse.")]
	public float weight = 1f;

	[Tooltip("Minimum number of times this tile should appear (per base prefab, rotations share the same counter).")]
	public int minCount = 0;

	[Tooltip("Maximum allowed count. 0 = unlimited. A small tolerance is applied during solving.")]
	public int maxCount = 0;
}
