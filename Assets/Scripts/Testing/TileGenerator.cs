// TileGenerator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Deterministic Wave Function Collapse tile generator (3D grid, X/Z).
/// Rules:
/// - Road must connect to Road (and match height within epsilon)
/// - Ground must connect to Ground
/// - Exactly one Start and one End (optional: placed pre-collapse)
/// - No Road sockets facing outside on grid borders
/// </summary>
public class TileGenerator : MonoBehaviour
{
    [Header("Data")]
    public TileSettingsSO settings;

    [Header("Grid")]
    public int width = 10;
    public int height = 10;

    [Header("Seed")]
    public int seed = 12345;
    public bool randomSeed = true;

    [Header("Output")]
    public Transform parent;
    public bool clearBeforeGenerate = true;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private System.Random rng;

    private List<TileOption> allOptions;
    private List<TileOption> startOptions;
    private List<TileOption> endOptions;

    private WFCCell[,] grid;

    private void Start() => Generate();

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (settings == null || settings.tiles == null || settings.tiles.Count == 0)
        {
            Debug.LogError("TileGenerator: No TileSettingsSO assigned or empty tiles list.");
            return;
        }

        if (parent == null) parent = transform;

        if (randomSeed)
            seed = Environment.TickCount;

        rng = new System.Random(seed);

        if (clearBeforeGenerate) ClearChildren(parent);

        allOptions = BuildOptions(settings, TileRole.Normal, TileRole.Start, TileRole.End);

        startOptions = allOptions.Where(o => o.role == TileRole.Start).ToList();
        endOptions   = allOptions.Where(o => o.role == TileRole.End).ToList();

        if (settings.placeStartAndEnd)
        {
            if (startOptions.Count == 0 || endOptions.Count == 0)
            {
                Debug.LogError("placeStartAndEnd is enabled, but no Start or End tiles exist in TileSettingsSO (TileEntry.role).");
                return;
            }
        }

        InitializeGrid();

        // Apply border constraints to all cells initially (reduces contradictions early)
        ApplyBorderConstraintsToAllCells();

        // Pre-place Start/End as hard constraints (exactly one each)
        if (settings.placeStartAndEnd)
            PlaceStartAndEnd();

        // Run WFC
        if (CollapseWaveFunction())
        {
            BuildFinalGrid();
            Debug.Log($"✓ WFC Success! Seed={seed}");
        }
        else
        {
            Debug.LogError($"✗ WFC Failed. Seed={seed} (Check socket setup / constraints / tileset completeness)");
        }
    }

    private void InitializeGrid()
    {
        grid = new WFCCell[width, height];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            grid[x, y] = new WFCCell(x, y, new List<TileOption>(allOptions));
    }

    private bool CollapseWaveFunction()
    {
        int iterations = 0;
        const int MAX_ITERATIONS = 20000;

        while (!IsFullyCollapsed())
        {
            iterations++;
            if (iterations > MAX_ITERATIONS)
            {
                Debug.LogError($"Max iterations ({MAX_ITERATIONS}) reached!");
                return false;
            }

            WFCCell cell = GetCellWithLowestEntropy();
            if (cell == null)
                break;

            CollapseCell(cell);

            if (!Propagate(cell))
                return false;
        }

        // Final sanity pass: ensure all adjacent pairs satisfy the hard rules
        return VerifyAdjacencyConstraints();
    }

    private bool IsFullyCollapsed()
    {
        foreach (var c in grid)
            if (!c.isCollapsed) return false;
        return true;
    }

    private WFCCell GetCellWithLowestEntropy()
    {
        List<WFCCell> candidates = new();
        int minEntropy = int.MaxValue;

        foreach (var c in grid)
        {
            if (c.isCollapsed) continue;
            int e = c.possibleOptions.Count;
            if (e == 0) return c; // contradiction cell -> will fail later quickly

            if (e < minEntropy)
            {
                minEntropy = e;
                candidates.Clear();
                candidates.Add(c);
            }
            else if (e == minEntropy)
            {
                candidates.Add(c);
            }
        }

        if (candidates.Count == 0) return null;
        return candidates[rng.Next(candidates.Count)];
    }

    private void CollapseCell(WFCCell cell)
    {
        if (cell.possibleOptions.Count == 0)
            return;

        TileOption chosen = WeightedPickRNG(cell.possibleOptions);

        cell.possibleOptions.Clear();
        cell.possibleOptions.Add(chosen);
        cell.isCollapsed = true;
        cell.finalOption = chosen;

        LogDebug($"Collapsed ({cell.x},{cell.y}) -> {chosen.basePrefab.name}_r{chosen.rotationSteps}");
    }

    private bool Propagate(WFCCell startCell)
    {
        Queue<WFCCell> q = new();
        q.Enqueue(startCell);

        while (q.Count > 0)
        {
            WFCCell cell = q.Dequeue();

            // For each direction, reduce neighbor options based on current cell options
            if (!PropagateToNeighbor(cell, Direction.North, q)) return false;
            if (!PropagateToNeighbor(cell, Direction.South, q)) return false;
            if (!PropagateToNeighbor(cell, Direction.East,  q)) return false;
            if (!PropagateToNeighbor(cell, Direction.West,  q)) return false;
        }

        return true;
    }

    private bool PropagateToNeighbor(WFCCell cell, Direction dir, Queue<WFCCell> q)
    {
        WFCCell n = GetNeighbor(cell, dir);
        if (n == null || n.isCollapsed) return true;

        Direction opp = GetOppositeDirection(dir);

        bool changed = false;

        // Keep neighbor option only if there exists SOME option in current cell that matches pairwise rules
        for (int i = n.possibleOptions.Count - 1; i >= 0; i--)
        {
            TileOption nOpt = n.possibleOptions[i];

            bool hasMatch = false;
            foreach (TileOption cOpt in cell.possibleOptions)
            {
                if (AreEdgeCompatible(cOpt, dir, nOpt, opp))
                {
                    hasMatch = true;
                    break;
                }
            }

            if (!hasMatch)
            {
                n.possibleOptions.RemoveAt(i);
                changed = true;
            }
        }

        // Border constraints (no roads pointing outside)
        if (ApplyBorderConstraints(n))
            changed = true;

        if (changed)
        {
            if (n.possibleOptions.Count == 0)
            {
                LogDebug($"Contradiction at ({n.x},{n.y}) after propagating from ({cell.x},{cell.y})");
                return false;
            }
            q.Enqueue(n);
        }

        return true;
    }

    private bool AreEdgeCompatible(TileOption a, Direction aDir, TileOption b, Direction bDir)
    {
        SocketType aType = a.GetSocketType(aDir);
        SocketType bType = b.GetSocketType(bDir);

        // Hard rule: must be same type
        if (aType != bType) return false;

        // If it's Road, must match height within epsilon
        if (aType == SocketType.Road)
        {
            float ah = a.GetHeight(aDir);
            float bh = b.GetHeight(bDir);
            return Mathf.Abs(ah - bh) <= settings.heightEpsilon;
        }

        // Ground: ok (ignore height)
        return true;
    }

    private WFCCell GetNeighbor(WFCCell cell, Direction dir)
    {
        int nx = cell.x, ny = cell.y;

        switch (dir)
        {
            case Direction.North: ny++; break;
            case Direction.South: ny--; break;
            case Direction.East:  nx++; break;
            case Direction.West:  nx--; break;
        }

        if (nx < 0 || nx >= width || ny < 0 || ny >= height)
            return null;

        return grid[nx, ny];
    }

    private static Direction GetOppositeDirection(Direction dir)
    {
        return dir switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East  => Direction.West,
            Direction.West  => Direction.East,
            _ => Direction.North
        };
    }

    private void PlaceStartAndEnd()
    {
        // Choose positions
        Vector2Int startPos = PickRandomCellPos(settings.startEndOnBorder);
        Vector2Int endPos;

        int safety = 0;
        do
        {
            endPos = PickRandomCellPos(settings.startEndOnBorder);
            safety++;
        } while (endPos == startPos && safety < 1000);

        // Constrain those cells to Start / End options
        ForceCellToRole(startPos.x, startPos.y, TileRole.Start);
        ForceCellToRole(endPos.x, endPos.y, TileRole.End);

        // Propagate from both constraints
        if (!Propagate(grid[startPos.x, startPos.y]))
            Debug.LogError("Propagation failed after placing Start.");

        if (!Propagate(grid[endPos.x, endPos.y]))
            Debug.LogError("Propagation failed after placing End.");

        LogDebug($"Placed Start at {startPos}, End at {endPos}");
    }

    private Vector2Int PickRandomCellPos(bool borderOnly)
    {
        if (!borderOnly)
            return new Vector2Int(rng.Next(0, width), rng.Next(0, height));

        // Border cell: x==0|w-1 or y==0|h-1
        bool pickHorizontalEdge = rng.NextDouble() < 0.5;
        if (pickHorizontalEdge)
        {
            int x = rng.Next(0, width);
            int y = (rng.NextDouble() < 0.5) ? 0 : (height - 1);
            return new Vector2Int(x, y);
        }
        else
        {
            int y = rng.Next(0, height);
            int x = (rng.NextDouble() < 0.5) ? 0 : (width - 1);
            return new Vector2Int(x, y);
        }
    }

    private void ForceCellToRole(int x, int y, TileRole role)
    {
        WFCCell cell = grid[x, y];

        List<TileOption> roleOpts = role switch
        {
            TileRole.Start => startOptions,
            TileRole.End   => endOptions,
            _              => allOptions.Where(o => o.role == TileRole.Normal).ToList()
        };

        // Intersect existing possibilities with the role options
        cell.possibleOptions = cell.possibleOptions
            .Where(o => o.role == role)
            .ToList();

        // Apply border constraints too
        ApplyBorderConstraints(cell);

        if (cell.possibleOptions.Count == 0)
        {
            Debug.LogError($"No valid {role} options for cell ({x},{y}). Check your Start/End tiles & border rules.");
            return;
        }

        // Collapse immediately to one of them (weighted)
        TileOption chosen = WeightedPickRNG(cell.possibleOptions);
        cell.possibleOptions.Clear();
        cell.possibleOptions.Add(chosen);
        cell.isCollapsed = true;
        cell.finalOption = chosen;
    }

    private void ApplyBorderConstraintsToAllCells()
    {
        foreach (var c in grid)
            ApplyBorderConstraints(c);
    }

    private bool ApplyBorderConstraints(WFCCell cell)
    {
        bool changed = false;

        for (int i = cell.possibleOptions.Count - 1; i >= 0; i--)
        {
            var opt = cell.possibleOptions[i];

            bool invalid = false;

            // IMPORTANT: independent checks (no else-if)
            if (cell.x == 0 && opt.GetSocketType(Direction.West) == SocketType.Road) invalid = true;
            if (cell.x == width - 1 && opt.GetSocketType(Direction.East) == SocketType.Road) invalid = true;
            if (cell.y == 0 && opt.GetSocketType(Direction.South) == SocketType.Road) invalid = true;
            if (cell.y == height - 1 && opt.GetSocketType(Direction.North) == SocketType.Road) invalid = true;

            if (invalid)
            {
                cell.possibleOptions.RemoveAt(i);
                changed = true;
            }
        }

        return changed;
    }

    private bool VerifyAdjacencyConstraints()
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var c = grid[x, y];
            if (c.finalOption == null) return false;

            var east = GetNeighbor(c, Direction.East);
            if (east != null && !AreEdgeCompatible(c.finalOption, Direction.East, east.finalOption, Direction.West))
                return false;

            var north = GetNeighbor(c, Direction.North);
            if (north != null && !AreEdgeCompatible(c.finalOption, Direction.North, north.finalOption, Direction.South))
                return false;
        }

        return true;
    }

    private void BuildFinalGrid()
    {
        float s = settings.cellSize;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var cell = grid[x, y];
            if (cell.finalOption == null) continue;

            var opt = cell.finalOption;
            var pos = new Vector3(x * s, 0f, y * s);
            var go = Instantiate(opt.basePrefab, pos, Quaternion.Euler(0f, opt.rotationY, 0f), parent);
            go.name = $"{opt.basePrefab.name}_r{opt.rotationSteps}_{x}_{y}";
        }
    }

    private TileOption WeightedPickRNG(List<TileOption> list)
    {
        int total = 0;
        foreach (var opt in list)
            total += Mathf.Max(1, opt.weight);

        int roll = rng.Next(0, total);
        int sum = 0;

        foreach (var opt in list)
        {
            sum += Mathf.Max(1, opt.weight);
            if (roll < sum) return opt;
        }

        return list[list.Count - 1];
    }

    private void LogDebug(string msg)
    {
        if (showDebugLogs) Debug.Log(msg);
    }

    private static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            DestroyImmediate(t.GetChild(i).gameObject);
    }

    private static List<TileOption> BuildOptions(TileSettingsSO settings, params TileRole[] allowedRoles)
    {
        var all = new List<TileOption>(512);

        foreach (var entry in settings.tiles)
        {
            if (entry.prefab == null) continue;
            if (allowedRoles != null && allowedRoles.Length > 0 && !allowedRoles.Contains(entry.role))
                continue;

            var sockets = entry.prefab.GetComponentsInChildren<TileSocket>(true);
            if (sockets == null || sockets.Length == 0)
            {
                Debug.LogWarning($"Prefab '{entry.prefab.name}' has no TileSocket components.");
                continue;
            }

            var baseData = TileSocketData.FromSockets(sockets);
            int rotCount = entry.allowRotation ? 4 : 1;

            for (int r = 0; r < rotCount; r++)
                all.Add(new TileOption(entry.prefab, entry.role, baseData, r, entry.weight));
        }

        return all;
    }

    // ----------------- Helper Types -----------------

    private class WFCCell
    {
        public int x, y;
        public List<TileOption> possibleOptions;
        public bool isCollapsed;
        public TileOption finalOption;

        public WFCCell(int x, int y, List<TileOption> options)
        {
            this.x = x;
            this.y = y;
            possibleOptions = options;
        }
    }

    private class TileOption
    {
        public GameObject basePrefab;
        public TileRole role;

        public int rotationSteps; // 0..3
        public int rotationY => rotationSteps * 90;
        public int weight;

        private TileSocketData baseData;

        public TileOption(GameObject prefab, TileRole role, TileSocketData baseData, int rotationSteps, int weight)
        {
            basePrefab = prefab;
            this.role = role;
            this.baseData = baseData;
            this.rotationSteps = rotationSteps;
            this.weight = weight;
        }

        public SocketType GetSocketType(Direction worldDir)
        {
            int idx = RotateIndex((int)worldDir, -rotationSteps);
            return baseData.types[idx];
        }

        public float GetHeight(Direction worldDir)
        {
            int idx = RotateIndex((int)worldDir, -rotationSteps);
            return baseData.heights[idx];
        }

        private static int RotateIndex(int dir, int steps)
        {
            // dir: 0=N,1=E,2=S,3=W
            int v = (dir + steps) % 4;
            if (v < 0) v += 4;
            return v;
        }
    }

    private class TileSocketData
    {
        public SocketType[] types = new SocketType[4];
        public float[] heights = new float[4];

        public static TileSocketData FromSockets(TileSocket[] sockets)
        {
            var d = new TileSocketData();

            for (int i = 0; i < 4; i++)
            {
                d.types[i] = SocketType.Ground;
                d.heights[i] = 0f;
            }

            foreach (var s in sockets)
            {
                int idx = (int)s.direction; // enum order N,E,S,W
                d.types[idx] = s.socketType;
                d.heights[idx] = s.heightLevel;
            }

            return d;
        }
    }
}
