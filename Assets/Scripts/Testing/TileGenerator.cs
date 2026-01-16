using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    private System.Random rng;
    private List<TileOption> allOptions;
    private Dictionary<GameObject, int> currentCounts;

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (settings == null || settings.tiles == null || settings.tiles.Count == 0)
        {
            Debug.LogError("TileGenerator: No TileSettingsSO assigned or empty tiles list.");
            return;
        }

        if (parent == null) parent = this.transform;

        // Initialize seed BEFORE any randomness
        if (randomSeed)
        {
            // Use deterministic seed generation (based on system time, but reproducible via seed override)
            seed = System.Environment.TickCount; // Can be overridden by setting seed manually
        }
        
        rng = new System.Random(seed);

        if (clearBeforeGenerate) ClearChildren(parent);

        // Build all tile options (prefab + rotation)
        allOptions = BuildOptions(settings);
        currentCounts = new Dictionary<GameObject, int>();

        // Initialize current counts
        foreach (var entry in settings.tiles)
        {
            if (entry.prefab != null)
                currentCounts[entry.prefab] = 0;
        }

        // Try to fill grid with backtracking - always finds a solution
        var chosen = new TileOption[width, height];
        if (TryFillGridWithBacktracking(chosen, 0))
        {
            Build(chosen);
            Debug.Log($"PCG generated successfully. Seed={seed}");
        }
        else
        {
            Debug.LogError("FATAL: Could not generate grid even with backtracking. Check constraints!");
        }
    }

    /// <summary>
    /// Recursive backtracking: fills grid from top-left, backtracks if constraints violated.
    /// Guarantees a solution exists (or constraints are impossible).
    /// </summary>
    private bool TryFillGridWithBacktracking(TileOption[,] chosen, int cellIndex)
    {
        // Base case: all cells filled
        if (cellIndex == width * height)
        {
            // Final check: all occurrence constraints met
            return MeetsOccurrenceConstraints(settings, currentCounts);
        }

        int y = cellIndex / width;
        int x = cellIndex % width;

        // Collect valid options at this position
        var valid = new List<TileOption>(64);
        foreach (var opt in allOptions)
        {
            if (!IsValidAt(x, y, opt, chosen))
                continue;

            if (!PassesBorderRule(x, y, opt))
                continue;

            // Check if placing this tile still allows constraint satisfaction
            if (!CanStillMeetConstraints(opt, currentCounts, settings, width * height - cellIndex - 1))
                continue;

            valid.Add(opt);
        }

        if (valid.Count == 0)
            return false; // No valid options, backtrack

        // Shuffle valid options using seeded RNG for variety
        int n = valid.Count;
        for (int i = n - 1; i > 0; i--)
        {
            int randomIndex = rng.Next(i + 1);
            var temp = valid[i];
            valid[i] = valid[randomIndex];
            valid[randomIndex] = temp;
        }

        // Try each valid option
        foreach (var opt in valid)
        {
            // Place tile
            chosen[x, y] = opt;
            currentCounts[opt.basePrefab]++;

            // Recurse
            if (TryFillGridWithBacktracking(chosen, cellIndex + 1))
                return true;

            // Backtrack
            chosen[x, y] = null;
            currentCounts[opt.basePrefab]--;
        }

        return false;
    }

    /// <summary>
    /// Checks if placing this tile could still allow us to meet min/max constraints
    /// with remaining cells. Prunes impossible branches early.
    /// </summary>
    private bool CanStillMeetConstraints(TileOption opt, Dictionary<GameObject, int> counts, TileSettingsSO settings, int cellsRemaining)
    {
        int currentCount = counts[opt.basePrefab];

        foreach (var entry in settings.tiles)
        {
            if (entry.prefab == null) continue;

            counts.TryGetValue(entry.prefab, out int c);

            // Check if we can still reach minCount
            int maxPossible = c + (entry.prefab == opt.basePrefab ? cellsRemaining : cellsRemaining);
            if (maxPossible < entry.minCount)
                return false; // Can't reach minCount

            // Check if we can stay under maxCount
            if (entry.maxCount > 0)
            {
                int minPossible = c;
                if (entry.prefab == opt.basePrefab)
                    minPossible++; // We're placing one of this type
                
                if (minPossible > entry.maxCount)
                    return false; // Already exceeded maxCount
            }
        }

        return true;
    }

    private bool IsValidAt(int x, int y, TileOption opt, TileOption[,] chosen)
    {
        // West neighbor
        if (x - 1 >= 0 && chosen[x - 1, y] != null)
        {
            if (!AreCompatible(chosen[x - 1, y], opt, Direction.East, Direction.West))
                return false;
        }

        // South neighbor
        if (y - 1 >= 0 && chosen[x, y - 1] != null)
        {
            if (!AreCompatible(chosen[x, y - 1], opt, Direction.North, Direction.South))
                return false;
        }

        return true;
    }

    private bool PassesBorderRule(int x, int y, TileOption opt)
    {
        // "Closed border": no Road socket may face outside the grid
        if (x == 0 && opt.GetSocketType(Direction.West) == SocketType.Road) return false;
        if (x == width - 1 && opt.GetSocketType(Direction.East) == SocketType.Road) return false;
        if (y == 0 && opt.GetSocketType(Direction.South) == SocketType.Road) return false;
        if (y == height - 1 && opt.GetSocketType(Direction.North) == SocketType.Road) return false;
        return true;
    }

    private bool AreCompatible(TileOption a, TileOption b, Direction dirA, Direction dirB)
    {
        // Type must match (Road-Road, Ground-Ground)
        var typeA = a.GetSocketType(dirA);
        var typeB = b.GetSocketType(dirB);
        if (typeA != typeB) return false;

        // Height must match (for EVERYTHING is simplest & robust)
        float hA = a.GetHeight(dirA);
        float hB = b.GetHeight(dirB);
        return Mathf.Abs(hA - hB) <= settings.heightEpsilon;
    }

    private void Build(TileOption[,] chosen)
    {
        float s = settings.cellSize;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var opt = chosen[x, y];
                if (opt == null) 
                {
                    Debug.LogError($"Tile at ({x}, {y}) is null after successful backtracking!");
                    continue;
                }
                
                var pos = new Vector3(x * s, 0f, y * s);
                var go = Instantiate(opt.basePrefab, pos, Quaternion.Euler(0f, opt.rotationY, 0f), parent);
                go.name = $"{opt.basePrefab.name}_r{opt.rotationSteps}_{x}_{y}";
            }
        }
    }

    private static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            DestroyImmediate(t.GetChild(i).gameObject);
    }

    private static bool MeetsOccurrenceConstraints(TileSettingsSO settings, Dictionary<GameObject, int> counts)
    {
        foreach (var entry in settings.tiles)
        {
            if (entry.prefab == null) continue;

            counts.TryGetValue(entry.prefab, out int c);

            if (c < entry.minCount) return false;
            if (entry.maxCount > 0 && c > entry.maxCount) return false;
        }
        return true;
    }

    private static List<TileOption> BuildOptions(TileSettingsSO settings)
    {
        var all = new List<TileOption>(512);

        foreach (var entry in settings.tiles)
        {
            if (entry.prefab == null) continue;

            // Read sockets on the prefab
            var sockets = entry.prefab.GetComponentsInChildren<TileSocket>(true);
            if (sockets == null || sockets.Length == 0)
            {
                Debug.LogWarning($"Prefab '{entry.prefab.name}' has no TileSocket components.");
                continue;
            }

            var baseData = TileSocketData.FromSockets(sockets);

            int rotCount = entry.allowRotation ? 4 : 1;
            for (int r = 0; r < rotCount; r++)
            {
                all.Add(new TileOption(entry.prefab, baseData, r, entry.weight));
            }
        }

        return all;
    }

    // ----------------- Helper Types -----------------

    private class TileOption
    {
        public GameObject basePrefab;
        public int rotationSteps; // 0..3
        public int rotationY => rotationSteps * 90;
        public int weight;

        private TileSocketData baseData;

        public TileOption(GameObject prefab, TileSocketData baseData, int rotationSteps, int weight)
        {
            this.basePrefab = prefab;
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

            // Default values (avoid uninitialized)
            for (int i = 0; i < 4; i++)
            {
                d.types[i] = SocketType.Ground;
                d.heights[i] = 0f;
            }

            foreach (var s in sockets)
            {
                int idx = (int)s.direction; // assumes enum order N,E,S,W
                d.types[idx] = s.socketType;
                d.heights[idx] = s.heightLevel;
            }

            return d;
        }
    }
}
