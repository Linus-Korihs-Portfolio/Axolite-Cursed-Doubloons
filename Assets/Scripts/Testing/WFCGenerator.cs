using System;
using System.Collections.Generic;
using UnityEngine;

public class WFCGenerator : MonoBehaviour
{
    [Header("Nodes (Tiles)")]
    [Tooltip("All possible WFC-Nodes (Tiles) without special rules")]
    public List<WFCNode> allNodes = new();

    [Header("Grid")]
    [Min(1)] public int width = 10;
    [Min(1)] public int height = 10;
    public float cellSize = 3f;
    public bool autoCellSize = true;

    [Header("Start/End (optional fixed placements)")]
    public bool placeStartAndEnd = true;
    public WFCNode startNode;
    public WFCNode endNode;
    public bool startEndOnBorder = true;

    [Header("Seed")]
    public bool randomSeed = true;
    public int seed = 12345;

    [Header("Retry")]
    [Min(1)] public int maxRetries = 50;

    [Header("Output")]
    public Transform parent;
    public bool clearBeforeGenerate = true;

    [Header("Debug")]
    public bool showLogs = false;

    [Header("Border Walls")]
    public bool forceBorderWalls = true;
    public WFCNode wallNode;
    public bool stackBorderWalls = true;

    [Min(1)]
    public int borderWallHeight = 5;

    [Tooltip("Height per wall segment in world units (1 if your wall tile is 1 unit tall).")]
    public float wallSegmentHeight = 1f;

    [Tooltip("If true, the first wall segment starts at y=0. If false, it starts at y=1 segmentHeight.")]
    public bool wallStartsAtZero = true;


    [Tooltip("Rotate border walls so they face OUTWARD (fixes west/north/south wrong orientation).")]
    public bool orientBorderWallsOutward = true;

    public enum WallFacing
    {
        North, // +Z
        East,  // +X
        South, // -Z
        West   // -X
    }

    [Tooltip("Which direction does your Wall prefab face when rotation = identity? (Most Unity meshes face +Z by default, yours seems to face East.)")]
    public WallFacing wallPrefabDefaultFacing = WallFacing.East;

    [Header("Wall Height")]
    [Tooltip("If enabled, border walls get lifted by this amount (Y).")]
    public bool raiseBorderWalls = true;

    [Header("Visual Rotation")]
    [Tooltip("If enabled, tiles with allowVisualRotation=true get random rotation 0/90/180/270 (visual only).")]
    public bool randomizeVisualRotation = true;

    private System.Random rng;
    private Cell[,] grid;

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (allNodes == null || allNodes.Count == 0)
        {
            Debug.LogError("WFCGenerator: allNodes is empty.");
            return;
        }

        if (parent == null) parent = transform;

        if (randomSeed) seed = Environment.TickCount;
        rng = new System.Random(seed);

        if (clearBeforeGenerate) ClearChildren(parent);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (TryGenerateOnce())
            {
                Debug.Log($"✓ WFC Success! Seed={seed}, Attempt={attempt}");
                return;
            }

            seed++;
            rng = new System.Random(seed);

            if (showLogs) Debug.LogWarning($"Retry {attempt}/{maxRetries} failed. New Seed={seed}");
        }

        Debug.LogError($"✗ WFC failed after {maxRetries} retries. Last Seed={seed}");
    }

    private bool TryGenerateOnce()
    {
        if (autoCellSize) cellSize = DetectCellSizeFromPrefab();
        InitGrid();

        // Border walls first
        if (forceBorderWalls)
        {
            if (wallNode == null)
            {
                Debug.LogError("forceBorderWalls is true but wallNode is not assigned.");
                return false;
            }

            ForceBorderWalls();

            if (!PropagateAllBorders()) return false;

            // If we force border walls, Start/End should NOT be on border.
            startEndOnBorder = false;
        }

        // Start / End
        if (placeStartAndEnd)
        {
            if (startNode == null || endNode == null)
            {
                Debug.LogError("placeStartAndEnd is true, but startNode or endNode is null.");
                return false;
            }

            var startPos = PickRandomPos(startEndOnBorder);
            var endPos = PickRandomPos(startEndOnBorder);

            // If border walls are forced: clamp to inner area
            if (forceBorderWalls)
            {
                startPos = ClampToInner(startPos);
                endPos = ClampToInner(endPos);
            }

            int safety = 0;
            while (endPos == startPos && safety++ < 2000)
            {
                endPos = PickRandomPos(startEndOnBorder);
                if (forceBorderWalls) endPos = ClampToInner(endPos);
            }

            if (!ForceCell(startPos.x, startPos.y, startNode)) return false;
            if (!ForceCell(endPos.x, endPos.y, endNode)) return false;

            if (!PropagateFrom(startPos.x, startPos.y)) return false;
            if (!PropagateFrom(endPos.x, endPos.y)) return false;
        }

        // Main WFC loop
        int guard = width * height * 50;

        while (!IsFullyCollapsed())
        {
            if (guard-- <= 0) return false;

            var cell = GetLowestEntropyCell();
            if (cell == null) break;

            if (cell.options.Count == 0) return false;

            var chosen = WeightedPick(cell.options);
            cell.CollapseTo(chosen);

            if (!PropagateFrom(cell.x, cell.y))
                return false;
        }

        BuildWorld();
        return true;
    }

    // ---------------- Grid / Cell ----------------

    private void InitGrid()
    {
        grid = new Cell[width, height];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            grid[x, y] = new Cell(x, y, allNodes);
    }

    private bool IsFullyCollapsed()
    {
        foreach (var c in grid)
            if (!c.collapsed) return false;
        return true;
    }

    private Cell GetLowestEntropyCell()
    {
        int min = int.MaxValue;
        List<Cell> candidates = new();

        foreach (var c in grid)
        {
            if (c.collapsed) continue;

            int e = c.options.Count;
            if (e == 0) return c;

            if (e < min)
            {
                min = e;
                candidates.Clear();
                candidates.Add(c);
            }
            else if (e == min)
            {
                candidates.Add(c);
            }
        }

        if (candidates.Count == 0) return null;
        return candidates[rng.Next(candidates.Count)];
    }

    private float DetectCellSizeFromPrefab()
    {
        foreach (var n in allNodes)
        {
            if (n == null || n.prefab == null) continue;

            var r = n.prefab.GetComponentInChildren<Renderer>();
            if (r == null) continue;

            float sizeX = r.bounds.size.x;
            float sizeZ = r.bounds.size.z;

            float s = Mathf.Max(sizeX, sizeZ);
            return Mathf.Max(0.01f, s);
        }
        return cellSize;
    }

    // ---------------- Propagation ----------------

    private bool PropagateFrom(int startX, int startY)
    {
        Queue<(int x, int y)> q = new();
        q.Enqueue((startX, startY));

        HashSet<(int x, int y)> inQueue = new();
        inQueue.Add((startX, startY));

        while (q.Count > 0)
        {
            var (cx, cy) = q.Dequeue();
            inQueue.Remove((cx, cy));

            if (!FilterNeighbor(cx, cy, cx, cy + 1, WFCDirection.North, WFCDirection.South, q, inQueue)) return false;
            if (!FilterNeighbor(cx, cy, cx + 1, cy, WFCDirection.East,  WFCDirection.West,  q, inQueue)) return false;
            if (!FilterNeighbor(cx, cy, cx, cy - 1, WFCDirection.South, WFCDirection.North, q, inQueue)) return false;
            if (!FilterNeighbor(cx, cy, cx - 1, cy, WFCDirection.West,  WFCDirection.East,  q, inQueue)) return false;
        }

        return true;
    }

    private bool FilterNeighbor(
        int srcX, int srcY,
        int nx, int ny,
        WFCDirection dirFromSrcToNeighbour,
        WFCDirection dirFromNeighbourToSrc,
        Queue<(int x, int y)> q,
        HashSet<(int x, int y)> inQueue)
    {
        if (nx < 0 || nx >= width || ny < 0 || ny >= height)
            return true;

        var src = grid[srcX, srcY];
        var n = grid[nx, ny];

        if (n.collapsed)
            return true;

        bool changed = false;

        for (int i = n.options.Count - 1; i >= 0; i--)
        {
            var nOpt = n.options[i];
            bool ok = false;

            for (int j = 0; j < src.options.Count; j++)
            {
                var srcOpt = src.options[j];

                if (srcOpt.AllowsNeighbour(dirFromSrcToNeighbour, nOpt) &&
                    nOpt.AllowsNeighbour(dirFromNeighbourToSrc, srcOpt))
                {
                    ok = true;
                    break;
                }
            }

            if (!ok)
            {
                n.options.RemoveAt(i);
                changed = true;
            }
        }

        if (n.options.Count == 0)
            return false;

        if (changed && !inQueue.Contains((nx, ny)))
        {
            q.Enqueue((nx, ny));
            inQueue.Add((nx, ny));
        }

        return true;
    }

    private bool PropagateAllBorders()
    {
        for (int x = 0; x < width; x++)
        {
            if (!PropagateFrom(x, 0)) return false;
            if (!PropagateFrom(x, height - 1)) return false;
        }

        for (int y = 0; y < height; y++)
        {
            if (!PropagateFrom(0, y)) return false;
            if (!PropagateFrom(width - 1, y)) return false;
        }

        return true;
    }

    // ---------------- Force / Pick ----------------

    private bool ForceCell(int x, int y, WFCNode node)
    {
        var c = grid[x, y];
        c.options.Clear();
        c.options.Add(node);
        c.collapsed = true;
        c.final = node;
        return true;
    }

    private WFCNode WeightedPick(List<WFCNode> options)
    {
        int total = 0;
        for (int i = 0; i < options.Count; i++)
            total += Mathf.Max(1, options[i].weight);

        int roll = rng.Next(0, total);
        int sum = 0;

        for (int i = 0; i < options.Count; i++)
        {
            sum += Mathf.Max(1, options[i].weight);
            if (roll < sum) return options[i];
        }

        return options[options.Count - 1];
    }

    private Vector2Int PickRandomPos(bool borderOnly)
    {
        if (!borderOnly)
            return new Vector2Int(rng.Next(0, width), rng.Next(0, height));

        bool horizontal = rng.NextDouble() < 0.5;
        if (horizontal)
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

    private Vector2Int ClampToInner(Vector2Int p)
    {
        return new Vector2Int(
            Mathf.Clamp(p.x, 1, width - 2),
            Mathf.Clamp(p.y, 1, height - 2)
        );
    }

    private bool ForceBorderWalls()
    {
        for (int x = 0; x < width; x++)
        {
            ForceCell(x, 0, wallNode);
            ForceCell(x, height - 1, wallNode);
        }

        for (int y = 0; y < height; y++)
        {
            ForceCell(0, y, wallNode);
            ForceCell(width - 1, y, wallNode);
        }

        return true;
    }

    // ---------------- Build (Rotation + Height) ----------------

    private void BuildWorld()
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var c = grid[x, y];
            if (!c.collapsed) return;

            var node = c.final;
            if (node == null || node.prefab == null) continue;

            float yOffset = node.spawnYOffset;

            bool isBorder = (x == 0 || x == width - 1 || y == 0 || y == height - 1);
            bool isWall = (wallNode != null && node == wallNode);

            Quaternion rot = Quaternion.identity;

            // 1) Border wall outward rotation
            if (isBorder && isWall && orientBorderWallsOutward)
            {
                WallFacing outward = GetOutwardFacingForBorder(x, y);
                float desiredYaw = FacingToYaw(outward);

                float defaultYaw = FacingToYaw(wallPrefabDefaultFacing);
                float finalYaw = NormalizeYaw(desiredYaw - defaultYaw);

                rot = Quaternion.Euler(0f, finalYaw, 0f);
            }
            else
            {
                // 2) Random visual rotation (for floors etc.)
                if (randomizeVisualRotation && node.allowVisualRotation)
                {
                    int steps = rng.Next(0, 4);
                    rot = Quaternion.Euler(0f, steps * 90f, 0f);
                }
            }

        Vector3 basePos = new Vector3(x * cellSize, yOffset, y * cellSize);

        if (isBorder && isWall && stackBorderWalls)
        {
            int h = Mathf.Max(1, borderWallHeight);
            float start = wallStartsAtZero ? 0f : wallSegmentHeight;

            for (int i = 0; i < h; i++)
            {
                float yAdd = start + i * wallSegmentHeight;
                Vector3 p = basePos + new Vector3(0f, yAdd, 0f);

                var go = Instantiate(node.prefab, p, rot, parent);
                go.name = $"{node.id}_{x}_{y}_H{i}";
            }
        }
        else
        {
            var go = Instantiate(node.prefab, basePos, rot, parent);
            go.name = $"{node.id}_{x}_{y}";
        }
        }
    }

    private WallFacing GetOutwardFacingForBorder(int x, int y)
    {
        // Assumes grid: +Y = North in grid space -> world +Z
        if (y == 0) return WallFacing.South;             // bottom edge faces outward -Z
        if (y == height - 1) return WallFacing.North;    // top edge faces outward +Z
        if (x == 0) return WallFacing.West;              // left edge faces outward -X
        return WallFacing.East;                           // right edge faces outward +X
    }

    private static float FacingToYaw(WallFacing f)
    {
        // yaw in degrees where 0 = +Z (Unity forward)
        return f switch
        {
            WallFacing.North => 0f,
            WallFacing.East  => 90f,
            WallFacing.South => 180f,
            WallFacing.West  => 270f,
            _ => 0f
        };
    }

    private static float NormalizeYaw(float yaw)
    {
        yaw %= 360f;
        if (yaw < 0f) yaw += 360f;
        return yaw;
    }

    private static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            DestroyImmediate(t.GetChild(i).gameObject);
    }

    // ---------------- Internal Cell ----------------

    private class Cell
    {
        public int x, y;
        public bool collapsed;
        public WFCNode final;
        public List<WFCNode> options;

        public Cell(int x, int y, List<WFCNode> all)
        {
            this.x = x;
            this.y = y;
            collapsed = false;
            final = null;
            options = new List<WFCNode>(all);
        }

        public void CollapseTo(WFCNode node)
        {
            options.Clear();
            options.Add(node);
            collapsed = true;
            final = node;
        }
    }
}
