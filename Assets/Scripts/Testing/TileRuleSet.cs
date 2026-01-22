using System;
using System.Collections.Generic;
using UnityEngine;

public static class TileRuleSet
{
	public class TileOption
	{
		public int tileIndex;
		public TileEntry entry;
		public GameObject prefab;
		public int rotationSteps;
		public float rotationDegrees;
		public float weight;
		public TileRole role;
		public SocketData[] sockets;
		public float suggestedHeight;
	}

	public struct SocketData
	{
		public SocketType type;
		public float height;

		public SocketData(SocketType type, float height)
		{
			this.type = type;
			this.height = height;
		}
	}

	public class BuildResult
	{
		public List<TileOption> all = new();
		public List<TileOption> normal = new();
		public List<TileOption> start = new();
		public List<TileOption> end = new();
		public float detectedCellSize = 1f;
	}

	public static bool TryBuild(SO_TileSetting settings, out BuildResult result, out string error)
	{
		result = null;
		error = string.Empty;

		if (settings == null)
		{
			error = "Settings asset missing.";
			return false;
		}

		if (settings.tiles == null || settings.tiles.Count == 0)
		{
			error = "No tiles configured.";
			return false;
		}

		result = new BuildResult();
		var seenPrefabs = new HashSet<GameObject>();
		float detectedCell = 1f;

		for (int i = 0; i < settings.tiles.Count; i++)
		{
			TileEntry entry = settings.tiles[i];
			if (entry.prefab == null)
			{
				error = $"Tile entry {i} has no prefab.";
				return false;
			}

			if (!TryReadSockets(entry.prefab, out SocketData[] baseSockets, out error))
			{
				error = $"{entry.id}: {error}";
				return false;
			}

			int rotations = entry.allowRotation ? 4 : 1;
			for (int rot = 0; rot < rotations; rot++)
			{
				SocketData[] rotated = RotateSockets(baseSockets, rot);
				var option = new TileOption
				{
					tileIndex = i,
					entry = entry,
					prefab = entry.prefab,
					rotationSteps = rot,
					rotationDegrees = rot * 90f,
					weight = Mathf.Max(0.0001f, entry.weight),
					role = entry.role,
					sockets = rotated,
					suggestedHeight = ComputeSuggestedHeight(rotated)
				};

				result.all.Add(option);
				switch (entry.role)
				{
					case TileRole.Normal:
						result.normal.Add(option);
						break;
					case TileRole.Start:
						result.start.Add(option);
						break;
					case TileRole.End:
						result.end.Add(option);
						break;
				}
			}

			if (seenPrefabs.Add(entry.prefab))
			{
				detectedCell = Mathf.Max(detectedCell, DetectCellSize(entry.prefab));
			}
		}

		// REGEL 8: Tile-Pool Validierung
		if (!TileConstraints.ValidateTilePool(result, settings.useStartEnd, out error))
		{
			return false;
		}

		result.detectedCellSize = detectedCell;
		return true;
	}

	private static bool TryReadSockets(GameObject prefab, out SocketData[] sockets, out string error)
	{
		sockets = null;
		error = string.Empty;

		TileSocket[] socketComponents = prefab.GetComponentsInChildren<TileSocket>(true);
		if (socketComponents.Length != 4)
		{
			error = "Each prefab needs exactly 4 TileSocket components (N/E/S/W).";
			return false;
		}

		sockets = new SocketData[4];
		bool[] seenDir = new bool[4];
		foreach (TileSocket socket in socketComponents)
		{
			int dirIndex = (int)socket.direction;
			if (dirIndex < 0 || dirIndex > 3)
			{
				error = "Socket direction out of range.";
				return false;
			}

			if (seenDir[dirIndex])
			{
				error = "Duplicate socket direction found.";
				return false;
			}

			seenDir[dirIndex] = true;
			sockets[dirIndex] = new SocketData(socket.socketType, socket.heightLevel);
		}

		return true;
	}

	private static SocketData[] RotateSockets(SocketData[] source, int rotationSteps)
	{
		SocketData[] rotated = new SocketData[4];
		for (int i = 0; i < 4; i++)
		{
			Direction dir = (Direction)i;
			Direction target = Rotate(dir, rotationSteps);
			rotated[(int)target] = source[i];
		}

		return rotated;
	}

	private static Direction Rotate(Direction dir, int steps)
	{
		int value = ((int)dir + steps) % 4;
		return (Direction)value;
	}

	private static float ComputeSuggestedHeight(SocketData[] sockets)
	{
		float sum = 0f;
		int count = 0;
		foreach (SocketData socket in sockets)
		{
			if (socket.type == SocketType.Floor)
			{
				sum += socket.height;
				count++;
			}
		}

		return count == 0 ? 0f : sum / count;
	}

	private static float DetectCellSize(GameObject prefab)
	{
		// Prefer renderer bounds, fall back to socket extents if meshes are missing/thin.
		float size = 1f;
		Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
		foreach (Renderer r in renderers)
		{
			size = Mathf.Max(size, r.bounds.size.x, r.bounds.size.z);
		}

		if (renderers.Length == 0)
		{
			TileSocket[] sockets = prefab.GetComponentsInChildren<TileSocket>(true);
			if (sockets.Length > 0)
			{
				Vector3 min = Vector3.positiveInfinity;
				Vector3 max = Vector3.negativeInfinity;
				Transform root = prefab.transform;
				foreach (TileSocket s in sockets)
				{
					Vector3 local = root.InverseTransformPoint(s.transform.position);
					min = Vector3.Min(min, local);
					max = Vector3.Max(max, local);
				}

				size = Mathf.Max(size, max.x - min.x, max.z - min.z);
			}
		}

		return Mathf.Max(0.01f, size);
	}
}
