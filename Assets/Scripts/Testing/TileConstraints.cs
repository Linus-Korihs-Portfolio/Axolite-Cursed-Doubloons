using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zentrale Verwaltung aller Constraints und Regeln für die WFC-Generierung.
/// Diese Datei enthält alle Validierungen und Einschränkungen übersichtlich an einer Stelle.
/// </summary>
public static class TileConstraints
{
	/// <summary>
	/// REGEL 1: Validierung der Socket-Kompatibilität zwischen benachbarten Tiles
	/// - Prüft, ob Socket-Typen (Floor/Wall) kompatibel sind
	/// - Prüft Höhen-Kompatibilität für Floor-Sockets (mit HeightEpsilon-Toleranz)
	/// </summary>
	public static bool IsSocketCompatible(
		List<TileRuleSet.TileOption> sourceOptions,
		TileRuleSet.TileOption neighborOption,
		Direction fromDirection,
		float heightEpsilon)
	{
		Direction neighborSide = Opposite(fromDirection);
		TileRuleSet.SocketData neighborSocket = neighborOption.sockets[(int)neighborSide];

		foreach (TileRuleSet.TileOption source in sourceOptions)
		{
			TileRuleSet.SocketData sourceSocket = source.sockets[(int)fromDirection];
			
			// Socket-Typen müssen gleich sein (Floor <-> Floor, Wall <-> Wall)
			if (sourceSocket.type != neighborSocket.type)
			{
				continue;
			}

			// Bei Floor-Sockets: Höhen müssen kompatibel sein
			if (sourceSocket.type == SocketType.Floor)
			{
				if (Mathf.Abs(sourceSocket.height - neighborSocket.height) > heightEpsilon)
				{
					continue;
				}
			}

			return true;
		}

		return false;
	}

	/// <summary>
	/// REGEL 2: Grenzregeln - Keine Floor-Sockets an den Grenzen (bei geschlossenen Grenzen)
	/// - Entfernt alle Tiles mit Floor-Sockets an den Grenzpositionen
	/// </summary>
	public static void ApplyBorderConstraint(
		Cell[,] grid,
		int gridWidth,
		int gridHeight,
		bool closedBorder)
	{
		if (!closedBorder)
		{
			return;
		}

		for (int x = 0; x < gridWidth; x++)
		{
			for (int y = 0; y < gridHeight; y++)
			{
				bool isNorth = y == gridHeight - 1;
				bool isSouth = y == 0;
				bool isWest = x == 0;
				bool isEast = x == gridWidth - 1;

				Cell cell = grid[x, y];
				cell.options.RemoveAll(option =>
				{
					if (isNorth && option.sockets[(int)Direction.North].type == SocketType.Floor)
						return true;
					if (isSouth && option.sockets[(int)Direction.South].type == SocketType.Floor)
						return true;
					if (isWest && option.sockets[(int)Direction.West].type == SocketType.Floor)
						return true;
					if (isEast && option.sockets[(int)Direction.East].type == SocketType.Floor)
						return true;

					return false;
				});
			}
		}
	}

	/// <summary>
	/// REGEL 3: Minimum Anzahl-Constraint - Jedes Tile-Prefab muss mindestens minCount mal vorkommen
	/// Prüfung: Sind alle Mindestanzahlen erfüllt?
	/// </summary>
	public static bool CheckMinimumCounts(int[] placed, int[] minCount)
	{
		for (int i = 0; i < placed.Length; i++)
		{
			if (placed[i] < minCount[i])
			{
				return false; // Mindestanzahl nicht erfüllt
			}
		}
		return true;
	}

	/// <summary>
	/// REGEL 4: Maximum Anzahl-Constraint - Kein Tile darf maxCount überschreiten
	/// Prüfung: Sind alle Maximalanzahlen eingehalten?
	/// </summary>
	public static bool CheckMaximumCounts(int[] placed, int[] effectiveMax)
	{
		for (int i = 0; i < placed.Length; i++)
		{
			if (placed[i] > effectiveMax[i])
			{
				return false; // Maximalanzahl überschritten
			}
		}
		return true;
	}

	/// <summary>
	/// REGEL 5: Start/End-Tile Mindestdistanz-Constraint
	/// - Start und End Tiles müssen mindestens minDistance zellen voneinander entfernt sein
	/// - Manhattan-Distanz wird verwendet
	/// </summary>
	public static bool CheckStartEndDistance(Vector2Int start, Vector2Int end, int minDistance)
	{
		if (start.x < 0 || end.x < 0)
		{
			return true; // Start/End nicht aktiv
		}

		int manhattanDistance = Mathf.Abs(start.x - end.x) + Mathf.Abs(start.y - end.y);
		return manhattanDistance >= minDistance;
	}

	/// <summary>
	/// REGEL 6: Grenzkompatibilität für Start/End Tiles
	/// - Prüft, ob ein Tile an einer Grenzposition platzierbar ist
	/// </summary>
	public static bool FitsBorderPosition(
		TileRuleSet.TileOption option,
		Vector2Int pos,
		int gridWidth,
		int gridHeight,
		bool closedBorder)
	{
		if (!closedBorder)
		{
			return true;
		}

		bool isNorth = pos.y == gridHeight - 1;
		bool isSouth = pos.y == 0;
		bool isWest = pos.x == 0;
		bool isEast = pos.x == gridWidth - 1;

		if (isNorth && option.sockets[(int)Direction.North].type == SocketType.Floor)
			return false;
		if (isSouth && option.sockets[(int)Direction.South].type == SocketType.Floor)
			return false;
		if (isWest && option.sockets[(int)Direction.West].type == SocketType.Floor)
			return false;
		if (isEast && option.sockets[(int)Direction.East].type == SocketType.Floor)
			return false;

		return true;
	}

	/// <summary>
	/// REGEL 7: Gitter-Größe vs. Mindestanzahl Constraint
	/// - Die Summe aller minCounts darf nicht größer als die Gittergröße sein
	/// </summary>
	public static bool CheckGridSizeVsMinimum(int gridSize, int totalMinCount)
	{
		return totalMinCount <= gridSize;
	}

	/// <summary>
	/// REGEL 8: Tile-Pool Validierung
	/// - Es müssen genügend Normal-Tiles vorhanden sein
	/// - Wenn Start/End aktiv: Es müssen Start- und End-Tiles vorhanden sein
	/// </summary>
	public static bool ValidateTilePool(
		TileRuleSet.BuildResult ruleSet,
		bool useStartEnd,
		out string error)
	{
		error = string.Empty;

		if (ruleSet.normal.Count == 0)
		{
			error = "No Normal tiles available.";
			return false;
		}

		if (useStartEnd)
		{
			if (ruleSet.start.Count == 0)
			{
				error = "Start tiles missing while Start/End is enabled.";
				return false;
			}

			if (ruleSet.end.Count == 0)
			{
				error = "End tiles missing while Start/End is enabled.";
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// REGEL 9: Kollapsraum-Reduktion Constraint
	/// - Wenn noch genau so viele Zellen übrig sind wie Mindestanzahlen erforderlich:
	///   Diese Zellen MÜSSEN die fehlenden Tiles enthalten
	/// </summary>
	public static bool CheckIfCanFulfillMinimums(int[] missing, int remainingCells)
	{
		int missingTotal = 0;
		for (int i = 0; i < missing.Length; i++)
		{
			missingTotal += Mathf.Max(0, missing[i]);
		}

		return missingTotal <= remainingCells;
	}

	// ============= HILFS-FUNKTIONEN =============

	private static Direction Opposite(Direction dir)
	{
		return (Direction)(((int)dir + 2) % 4);
	}
}

/// <summary>
/// Hilfklasse für einzelne Gitterzellen im Solve-Prozess
/// </summary>
public class Cell
{
	public List<TileRuleSet.TileOption> options;
	public bool Collapsed => options.Count == 1;

	public Cell(List<TileRuleSet.TileOption> source)
	{
		options = new List<TileRuleSet.TileOption>(source);
	}

	public void CollapseTo(TileRuleSet.TileOption option)
	{
		options.Clear();
		options.Add(option);
	}
}
