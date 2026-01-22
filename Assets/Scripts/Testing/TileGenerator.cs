using System;
using System.Collections.Generic;
using UnityEngine;

public class TileGenerator : MonoBehaviour
{
	[SerializeField] private SO_TileSetting settings;
	[SerializeField] private Transform rootParent;
	[SerializeField] private bool generateOnStart = true;
	[SerializeField] private bool clearPrevious = true;

	private TileRuleSet.BuildResult ruleSet;
	private System.Random rng;
	private float cellSize = 1f;

	private Cell[,] lastGrid;

	private void Start()
	{
		if (generateOnStart)
		{
			Generate();
		}
	}

	public void Generate()
	{
		if (!ValidateSetup(out string error))
		{
			Debug.LogError(error, this);
			return;
		}

		if (!TileRuleSet.TryBuild(settings, out ruleSet, out error))
		{
			Debug.LogError(error, this);
			return;
		}

		cellSize = Mathf.Max(0.01f, settings.autoCellSize ? ruleSet.detectedCellSize : settings.cellSize);
		int baseSeed = settings.useRandomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : settings.seed;

		int maxAttempts = Mathf.Max(1, settings.maxRetries);
		for (int attempt = 0; attempt < maxAttempts; attempt++)
		{
			rng = new System.Random(baseSeed + attempt);
			if (TrySolve(out Cell[,] grid))
			{
				lastGrid = grid;
				ClearChildren();
				Build(grid);
				if (settings.logSteps)
				{
					Debug.Log($"WFC succeeded after {attempt + 1} attempt(s) with seed {baseSeed + attempt}.", this);
				}
				return;
			}
		}

		Debug.LogError($"WFC failed after {maxAttempts} attempt(s).", this);
	}

	private bool ValidateSetup(out string error)
	{
		error = string.Empty;

		if (settings == null)
		{
			error = "TileGenerator is missing a settings asset.";
			return false;
		}

		if (settings.width < 1 || settings.height < 1)
		{
			error = "Grid width and height must be at least 1.";
			return false;
		}

		if (!settings.autoCellSize && settings.cellSize <= 0f)
		{
			error = "Cell size must be greater than zero or Auto Cell Size enabled.";
			return false;
		}

		if (settings.heightEpsilon < 0f)
		{
			error = "Height epsilon must be non-negative.";
			return false;
		}

		return true;
	}

	private bool TrySolve(out Cell[,] grid)
	{
		grid = new Cell[settings.width, settings.height];

		int tileCount = settings.tiles.Count;
		int[] placed = new int[tileCount];
		int[] minCount = new int[tileCount];
		int[] effectiveMax = new int[tileCount];

		int totalMin = 0;
		for (int i = 0; i < tileCount; i++)
		{
			TileEntry entry = settings.tiles[i];
			minCount[i] = Mathf.Max(0, entry.minCount);
			totalMin += minCount[i];

			if (entry.maxCount <= 0)
			{
				effectiveMax[i] = int.MaxValue;
			}
			else
			{
				effectiveMax[i] = Mathf.Max(entry.maxCount, entry.minCount) + Mathf.Max(0, settings.hardMaxTolerance);
			}
		}

		int totalCells = settings.width * settings.height;
		
		// REGEL 7: Gitter-Größe vs. Mindestanzahl
		if (!TileConstraints.CheckGridSizeVsMinimum(totalCells, totalMin))
		{
			if (settings.logSteps)
			{
				Debug.LogError("minCounts exceed grid size.", this);
			}
			return false;
		}

		List<TileRuleSet.TileOption> normalOptions = ruleSet.normal;
		for (int x = 0; x < settings.width; x++)
		{
			for (int y = 0; y < settings.height; y++)
			{
				grid[x, y] = new Cell(normalOptions);
			}
		}

		// REGEL 2: Border-Constraint anwenden
		TileConstraints.ApplyBorderConstraint(grid, settings.width, settings.height, settings.closedBorder);

		Queue<Vector2Int> propagateQueue = new();
		HashSet<Vector2Int> fixedCells = new();
		Vector2Int startPos = new(-1, -1);
		Vector2Int endPos = new(-1, -1);

		if (settings.useStartEnd)
		{
			if (!TryPlaceEndpoint(grid, ruleSet.start, settings.startOnBorderOnly, propagateQueue, placed, fixedCells, out startPos))
			{
				return false;
			}

			if (!TryPlaceEndpoint(grid, ruleSet.end, settings.endOnBorderOnly, propagateQueue, placed, fixedCells, out endPos))
			{
				return false;
			}

			// REGEL 5: Start/End Mindestdistanz-Constraint
			if (!TileConstraints.CheckStartEndDistance(startPos, endPos, settings.minStartEndDistance))
			{
				return false;
			}
		}

		if (!Propagate(grid, propagateQueue))
		{
			return false;
		}

		while (true)
		{
			int remainingCells = CountUncollapsed(grid);
			if (remainingCells == 0)
			{
				break;
			}

			int[] missing = new int[tileCount];
			for (int i = 0; i < tileCount; i++)
			{
				missing[i] = Mathf.Max(0, minCount[i] - placed[i]);
			}

			// REGEL 9: Überprüfung, ob Mindestanzahlen noch erfüllbar sind
			if (!TileConstraints.CheckIfCanFulfillMinimums(missing, remainingCells))
			{
				return false;
			}

			Vector2Int pick = PickLowestEntropyCell(grid);
			if (pick.x < 0)
			{
				return false;
			}

			Cell cell = grid[pick.x, pick.y];
			
			// Filter: Nur Tiles die nicht ihr Maximum erreicht haben
			cell.options.RemoveAll(option => placed[option.tileIndex] >= effectiveMax[option.tileIndex]);
			if (cell.options.Count == 0)
			{
				return false;
			}

			// Berechne fehlende Anzahlen
			int missingTotal = 0;
			for (int i = 0; i < missing.Length; i++)
			{
				missingTotal += missing[i];
			}

			if (missingTotal == remainingCells)
			{
				// REGEL: Wenn nur noch Raum für fehlende Tiles ist, nur diese erlauben
				cell.options.RemoveAll(option => missing[option.tileIndex] == 0);
				if (cell.options.Count == 0)
				{
					return false;
				}
			}

			TileRuleSet.TileOption choice = PickOption(cell.options, missing);
			cell.CollapseTo(choice);
			placed[choice.tileIndex]++;
			propagateQueue.Enqueue(pick);

			if (!Propagate(grid, propagateQueue))
			{
				return false;
			}
		}

		// REGEL 3 + 4: Finale Überprüfung - Min/Max Counts
		if (settings.enforceCounts)
		{
			if (!TileConstraints.CheckMinimumCounts(placed, minCount))
			{
				return false;
			}
			if (!TileConstraints.CheckMaximumCounts(placed, effectiveMax))
			{
				return false;
			}
		}

		return true;
	}

	private bool TryPlaceEndpoint(Cell[,] grid, List<TileRuleSet.TileOption> pool, bool borderOnly, Queue<Vector2Int> queue, int[] placed, HashSet<Vector2Int> reserved, out Vector2Int chosen)
	{
		chosen = new Vector2Int(-1, -1);

		if (pool.Count == 0)
		{
			return false;
		}

		List<Vector2Int> candidates = new();
		for (int x = 0; x < settings.width; x++)
		{
			for (int y = 0; y < settings.height; y++)
			{
				bool onBorder = x == 0 || x == settings.width - 1 || y == 0 || y == settings.height - 1;
				Vector2Int pos = new(x, y);
				if ((!borderOnly || onBorder) && !reserved.Contains(pos))
				{
					candidates.Add(pos);
				}
			}
		}

		if (candidates.Count == 0)
		{
			return false;
		}

		chosen = candidates[rng.Next(candidates.Count)];
		if (!TryPickBorderSafe(pool, chosen, out TileRuleSet.TileOption option))
		{
			return false;
		}

		grid[chosen.x, chosen.y].CollapseTo(option);
		placed[option.tileIndex]++;
		reserved.Add(chosen);
		queue.Enqueue(chosen);
		return true;
	}

	private bool Propagate(Cell[,] grid, Queue<Vector2Int> queue)
	{
		while (queue.Count > 0)
		{
			Vector2Int pos = queue.Dequeue();
			Cell cell = grid[pos.x, pos.y];

			foreach (Direction dir in Enum.GetValues(typeof(Direction)))
			{
				Vector2Int neighborPos = pos + ToOffset(dir);
				if (!InBounds(neighborPos))
				{
					continue;
				}

				Cell neighbor = grid[neighborPos.x, neighborPos.y];
				int before = neighbor.options.Count;
				
				// REGEL 1: Socket-Kompatibilität zwischen benachbarten Tiles prüfen
				neighbor.options.RemoveAll(option => !TileConstraints.IsSocketCompatible(cell.options, option, dir, settings.heightEpsilon));

				if (neighbor.options.Count == 0)
				{
					return false;
				}

				if (neighbor.options.Count < before)
				{
					queue.Enqueue(neighborPos);
				}
			}
		}

		return true;
	}

	private TileRuleSet.TileOption PickOption(List<TileRuleSet.TileOption> options, int[] missing)
	{
		float totalWeight = 0f;
		foreach (TileRuleSet.TileOption option in options)
		{
			float weight = option.weight;
			if (missing != null && missing.Length > option.tileIndex && missing[option.tileIndex] > 0)
			{
				weight *= 2f;
			}
			totalWeight += weight;
		}

		float roll = (float)rng.NextDouble() * totalWeight;
		foreach (TileRuleSet.TileOption option in options)
		{
			float weight = option.weight;
			if (missing != null && missing.Length > option.tileIndex && missing[option.tileIndex] > 0)
			{
				weight *= 2f;
			}

			roll -= weight;
			if (roll <= 0f)
			{
				return option;
			}
		}

		return options[options.Count - 1];
	}

	private bool TryPickBorderSafe(List<TileRuleSet.TileOption> pool, Vector2Int pos, out TileRuleSet.TileOption option)
	{
		option = null;

		if (!settings.closedBorder)
		{
			option = PickOption(pool, null);
			return true;
		}

		List<TileRuleSet.TileOption> filtered = new();
		foreach (TileRuleSet.TileOption candidate in pool)
		{
			// REGEL 6: Border-Kompatibilität prüfen
			if (TileConstraints.FitsBorderPosition(candidate, pos, settings.width, settings.height, settings.closedBorder))
			{
				filtered.Add(candidate);
			}
		}

		if (filtered.Count == 0)
		{
			return false;
		}

		option = PickOption(filtered, null);
		return true;
	}

	private Vector2Int PickLowestEntropyCell(Cell[,] grid)
	{
		int bestEntropy = int.MaxValue;
		List<Vector2Int> candidates = new();

		for (int x = 0; x < settings.width; x++)
		{
			for (int y = 0; y < settings.height; y++)
			{
				Cell cell = grid[x, y];
				int entropy = cell.options.Count;
				if (entropy <= 1)
				{
					continue;
				}

				if (entropy < bestEntropy)
				{
					bestEntropy = entropy;
					candidates.Clear();
					candidates.Add(new Vector2Int(x, y));
				}
				else if (entropy == bestEntropy)
				{
					candidates.Add(new Vector2Int(x, y));
				}
			}
		}

		if (candidates.Count == 0)
		{
			return new Vector2Int(-1, -1);
		}

		return candidates[rng.Next(candidates.Count)];
	}

	private int CountUncollapsed(Cell[,] grid)
	{
		int count = 0;
		for (int x = 0; x < settings.width; x++)
		{
			for (int y = 0; y < settings.height; y++)
			{
				if (!grid[x, y].Collapsed)
				{
					count++;
				}
			}
		}

		return count;
	}

	private void Build(Cell[,] grid)
	{
		if (rootParent == null)
		{
			rootParent = transform;
		}

		HashSet<Vector3> occupied = new();

		for (int x = 0; x < settings.width; x++)
		{
			for (int y = 0; y < settings.height; y++)
			{
				TileRuleSet.TileOption option = grid[x, y].options[0];
				Vector3 pos = new Vector3(x * cellSize, option.suggestedHeight, y * cellSize);
				if (!occupied.Add(pos))
				{
					Debug.LogWarning($"Duplicate placement at {pos}; skipping instantiation.", this);
					continue;
				}

				Quaternion rot = Quaternion.Euler(0f, option.rotationDegrees, 0f);
				Instantiate(option.prefab, pos, rot, rootParent);
			}
		}
	}

	private void ClearChildren()
	{
		if (!clearPrevious || rootParent == null)
		{
			return;
		}

		for (int i = rootParent.childCount - 1; i >= 0; i--)
		{
			Transform child = rootParent.GetChild(i);
			if (Application.isPlaying)
			{
				Destroy(child.gameObject);
			}
			else
			{
				DestroyImmediate(child.gameObject);
			}
		}
	}

	private bool InBounds(Vector2Int p)
	{
		return p.x >= 0 && p.x < settings.width && p.y >= 0 && p.y < settings.height;
	}

	private static Vector2Int ToOffset(Direction dir)
	{
		return dir switch
		{
			Direction.North => new Vector2Int(0, 1),
			Direction.East => new Vector2Int(1, 0),
			Direction.South => new Vector2Int(0, -1),
			Direction.West => new Vector2Int(-1, 0),
			_ => Vector2Int.zero
		};
	}
}