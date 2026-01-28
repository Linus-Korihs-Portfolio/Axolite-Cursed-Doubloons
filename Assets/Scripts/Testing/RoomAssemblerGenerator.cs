using System;
using System.Collections.Generic;
using UnityEngine;

public class RoomAssemblerGenerator : MonoBehaviour
{
    [Header("Rooms")]
    public RoomDefinition startRoom;
    public RoomDefinition endRoom;
    public List<RoomDefinition> roomPool = new();

    [Header("Generation")]
    [Min(1)] public int maxRooms = 50;

    [Tooltip("Minimum number of connections (steps) from Start to End.")]
    [Min(0)] public int minStepsToEnd = 6;

    [Tooltip("Maximum number of connections (steps) from Start to End. After this we try to force End.")]
    [Min(1)] public int maxStepsToEnd = 20;

    [Tooltip("How many attempts per socket before we give up on that socket.")]
    [Min(1)] public int attemptsPerOpenSocket = 30;

    [Header("Capping (No exits to void)")]
    public bool capOpenSocketsAfterEnd = true;

    [Tooltip("If true, we will try to cap remaining open sockets with DeadEnd rooms.")]
    public bool useDeadEndsToCap = true;

    [Tooltip("Max number of caps we place after End to avoid runaway spawning.")]
    [Min(0)] public int maxCapsAfterEnd = 999;

    [Header("Capping Safety")]
    [Tooltip("Extra overlap padding applied only during capping (dead-ends).")]
    public float capExtraPadding = 0.03f;

    [Header("Physics / Overlap")]
    [Tooltip("Best practice: set to 'Generated' layer only.")]
    public LayerMask overlapMask = ~0;

    [Tooltip("Shrink bounds slightly so edge-touching is NOT considered overlap.")]
    public float overlapPadding = 0.02f;

    [Header("Socket Matching")]
    [Tooltip("If two socket centers are closer than this, they can be treated as meeting (for loops).")]
    public float centerSnapTolerance = 0.02f;

    [Tooltip("Dot threshold for opposite facing. 1 = perfect opposite. 0.95 ~ 18 degrees.")]
    public float forwardDotTolerance = 0.95f;

    [Tooltip("Fallback width tolerance if RoomDefinition has none.")]
    public float widthToleranceFallback = 0.05f;

    [Header("Loops")]
    public bool allowLoops = true;

    [Tooltip("If true, we will close sockets that meet (creates loops).")]
    public bool autoCloseMatchingSockets = true;

    [Header("Seed")]
    public bool randomSeed = true;
    public int seed = 12345;

    [Header("Output")]
    public Transform parent;
    public bool clearBeforeGenerate = true;

    [Header("Debug")]
    public bool log = false;

    private System.Random rng;

    private readonly List<PlacedRoom> placed = new();
    private readonly List<OpenSocket> openSockets = new();

    // cache dead ends for capping
    private readonly List<RoomDefinition> deadEndCache = new();

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (!ValidateSetup()) return;

        if (parent == null) parent = transform;
        if (clearBeforeGenerate) ClearChildren(parent);

        if (randomSeed) seed = Environment.TickCount;
        rng = new System.Random(seed);

        placed.Clear();
        openSockets.Clear();
        BuildDeadEndCache();

        // Spawn Start at origin
        var startGO = Instantiate(startRoom.prefab, Vector3.zero, Quaternion.identity, parent);
        startGO.name = $"START_{startRoom.id}";
        var startPlaced = new PlacedRoom(startRoom, startGO);
        placed.Add(startPlaced);

        AddOpenSocketsFromRoom(startPlaced);

        if (log)
        {
            Debug.Log($"Start sockets found: {startGO.GetComponentsInChildren<SocketMarker>(true).Length}");
            Debug.Log($"Open sockets after start: {openSockets.Count}");
            Debug.Log($"RoomPool count: {roomPool.Count}");
            Debug.Log($"DeadEnd cache count: {deadEndCache.Count}");
        }

        bool success = GrowUntilEnd();

        if (!success)
            Debug.LogError($"✗ Generation failed. Seed={seed}");
        else
            Debug.Log($"✓ Generation success. Seed={seed}, Rooms={placed.Count}");
    }

    private bool ValidateSetup()
    {
        if (startRoom == null || startRoom.prefab == null)
        {
            Debug.LogError("Start room missing.");
            return false;
        }
        if (endRoom == null || endRoom.prefab == null)
        {
            Debug.LogError("End room missing.");
            return false;
        }
        if (roomPool == null || roomPool.Count == 0)
        {
            Debug.LogError("Room pool is empty.");
            return false;
        }
        return true;
    }

    private void BuildDeadEndCache()
    {
        deadEndCache.Clear();
        for (int i = 0; i < roomPool.Count; i++)
        {
            var r = roomPool[i];
            if (r == null || r.prefab == null) continue;
            if (r.isDeadEnd) deadEndCache.Add(r);
        }
    }

    private bool GrowUntilEnd()
    {
        int safety = maxRooms * 200;
        bool endPlaced = false;
        int placedAfterStart = 0;

        while (safety-- > 0 && placed.Count < maxRooms)
        {
            if (openSockets.Count == 0) return false;

            bool forceEndNow = placedAfterStart >= maxStepsToEnd;

            // pick a random open socket to expand
            int idx = rng.Next(openSockets.Count);
            var target = openSockets[idx];

            bool placedSomething = false;

            for (int attempt = 0; attempt < attemptsPerOpenSocket; attempt++)
            {
                RoomDefinition candidate =
                    (forceEndNow || placedAfterStart >= minStepsToEnd)
                        ? PickRoomWithBiasToEnd(forceEndNow)
                        : PickNonEndRoom(endPlaced);

                if (candidate == null || candidate.prefab == null)
                    continue;

                // normal growth uses 0 extra padding
                if (TryAttachRoom(target, candidate, out var newPlaced, extraOverlapPadding: 0f))
                {
                    openSockets.RemoveAt(idx);

                    placed.Add(newPlaced);
                    placedAfterStart++;

                    AddOpenSocketsFromRoom(newPlaced);

                    if (allowLoops && autoCloseMatchingSockets)
                        CloseAnySocketPairsThatMeet();

                    if (candidate == endRoom)
                    {
                        endPlaced = true;

                        if (capOpenSocketsAfterEnd)
                            CapAllOpenSockets();

                        return true;
                    }

                    placedSomething = true;
                    break;
                }
            }

            if (!placedSomething)
            {
                // couldn't fill this socket => close it logically
                openSockets.RemoveAt(idx);

                if (forceEndNow) return false;
            }
        }

        return false;
    }

    // ---------------- Candidate Picking ----------------

    private RoomDefinition PickNonEndRoom(bool endPlaced)
    {
        // while End not placed: avoid dead ends (keeps main path alive)
        if (!endPlaced)
            return WeightedPickFiltered(roomPool, r => r != null && !r.isDeadEnd);

        return WeightedPick(roomPool);
    }

    private RoomDefinition PickRoomWithBiasToEnd(bool forceEnd)
    {
        if (forceEnd) return endRoom;

        // 20% chance to place End after minSteps
        if (rng.Next(0, 100) < 20) return endRoom;

        return WeightedPick(roomPool);
    }

    private RoomDefinition WeightedPick(List<RoomDefinition> list)
    {
        if (list == null || list.Count == 0) return null;

        int total = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null) continue;
            total += Mathf.Max(1, list[i].weight);
        }

        if (total <= 0) return null;

        int roll = rng.Next(0, total);
        int sum = 0;

        for (int i = 0; i < list.Count; i++)
        {
            var r = list[i];
            if (r == null) continue;

            sum += Mathf.Max(1, r.weight);
            if (roll < sum) return r;
        }

        return list[list.Count - 1];
    }

    private RoomDefinition WeightedPickFiltered(List<RoomDefinition> list, Func<RoomDefinition, bool> predicate)
    {
        var filtered = new List<RoomDefinition>();
        for (int i = 0; i < list.Count; i++)
        {
            var r = list[i];
            if (r == null) continue;
            if (predicate(r)) filtered.Add(r);
        }
        return WeightedPick(filtered);
    }

    // ---------------- Placement ----------------

    private bool TryAttachRoom(OpenSocket target, RoomDefinition room, out PlacedRoom placedRoom, float extraOverlapPadding)
    {
        placedRoom = null;

        var go = Instantiate(room.prefab, Vector3.zero, Quaternion.identity, parent);
        go.SetActive(false);

        var markers = go.GetComponentsInChildren<SocketMarker>(true);
        if (markers == null || markers.Length == 0)
        {
            DestroyImmediate(go);
            return false;
        }

        Shuffle(markers);

        for (int i = 0; i < markers.Length; i++)
        {
            var candSocket = markers[i];
            if (candSocket == null || candSocket.left == null || candSocket.right == null) continue;

            if (candSocket.type != target.type) continue;
            if (!IsWidthCompatible(target.width, candSocket.WidthWorld, room)) continue;

            // --- compute yaw-only rotation so candidate socket faces opposite of target socket ---
            Vector3 a = candSocket.ForwardWorld;
            Vector3 b = -target.forward;

            a.y = 0f;
            b.y = 0f;

            if (a.sqrMagnitude < 0.0001f || b.sqrMagnitude < 0.0001f)
                continue;

            a.Normalize();
            b.Normalize();

            float yaw = Vector3.SignedAngle(a, b, Vector3.up);
            go.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * go.transform.rotation;

            // after rotation, recompute center
            Vector3 candCenter = candSocket.CenterWorld;

            // translate so centers coincide
            go.transform.position += (target.center - candCenter);

            go.SetActive(true);
            Physics.SyncTransforms();

            bool overlaps = OverlapsAnything(go, extraOverlapPadding);

            if (log)
            {
                Debug.Log($"TryAttach {room.id} via candSocket={candSocket.name} to target={target.marker.name} | " +
                          $"widthA={target.width:F2} widthB={candSocket.WidthWorld:F2} | overlaps={overlaps}");
            }

            if (!overlaps)
            {
                go.name = room.id;

                placedRoom = new PlacedRoom(room, go);
                placedRoom.connectedSocketInstanceIds.Add(candSocket.GetInstanceID());
                return true;
            }

            // revert and try next
            go.SetActive(false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        DestroyImmediate(go);
        return false;
    }

    private bool IsWidthCompatible(float a, float b, RoomDefinition room)
    {
        float tol = (room != null) ? Mathf.Max(0.0001f, room.widthTolerance) : widthToleranceFallback;
        return Mathf.Abs(a - b) <= tol;
    }

    // ---------------- Overlap ----------------

    private bool OverlapsAnything(GameObject candidate, float extraPadding)
    {
        float pad = overlapPadding + extraPadding;

        // Prefer dedicated Bounds collider (OBB)
        var boundsTf = candidate.transform.Find("Bounds");
        if (boundsTf != null)
        {
            var bc = boundsTf.GetComponent<BoxCollider>();
            if (bc != null && bc.enabled)
            {
                Vector3 center = bc.transform.TransformPoint(bc.center);
                Vector3 half = Vector3.Scale(bc.size * 0.5f, bc.transform.lossyScale) - Vector3.one * pad;

                half.x = Mathf.Max(0.001f, half.x);
                half.y = Mathf.Max(0.001f, half.y);
                half.z = Mathf.Max(0.001f, half.z);

                Quaternion rot = bc.transform.rotation;

                var hits = Physics.OverlapBox(center, half, rot, overlapMask, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < hits.Length; h++)
                {
                    if (hits[h].transform.IsChildOf(candidate.transform)) continue;
                    return true;
                }
                return false;
            }
        }

        // fallback: any colliders (AABB)
        var cols = candidate.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null || !c.enabled) continue;

            var b = c.bounds;
            Vector3 center = b.center;
            Vector3 half = b.extents - Vector3.one * pad;

            half.x = Mathf.Max(0.001f, half.x);
            half.y = Mathf.Max(0.001f, half.y);
            half.z = Mathf.Max(0.001f, half.z);

            var hits = Physics.OverlapBox(center, half, Quaternion.identity, overlapMask, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < hits.Length; h++)
            {
                if (hits[h].transform.IsChildOf(candidate.transform)) continue;
                return true;
            }
        }

        return false;
    }

    // ---------------- Socket Management ----------------

    private void AddOpenSocketsFromRoom(PlacedRoom r)
    {
        var markers = r.root.GetComponentsInChildren<SocketMarker>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            var m = markers[i];
            if (m == null || m.left == null || m.right == null) continue;

            if (r.connectedSocketInstanceIds.Contains(m.GetInstanceID()))
                continue;

            openSockets.Add(new OpenSocket
            {
                owner = r,
                marker = m,
                type = m.type,
                center = m.CenterWorld,
                forward = m.ForwardWorld.normalized,
                width = m.WidthWorld
            });
        }
    }

    private void CloseAnySocketPairsThatMeet()
    {
        for (int i = openSockets.Count - 1; i >= 0; i--)
        {
            for (int j = i - 1; j >= 0; j--)
            {
                var a = openSockets[i];
                var b = openSockets[j];

                if (a.type != b.type) continue;

                if (Vector3.Distance(a.center, b.center) > centerSnapTolerance) continue;

                float dot = Vector3.Dot(a.forward, -b.forward);
                if (dot < forwardDotTolerance) continue;

                float tol = widthToleranceFallback;
                if (Mathf.Abs(a.width - b.width) > tol) continue;

                openSockets.RemoveAt(i);
                openSockets.RemoveAt(j);
                return;
            }
        }
    }

    // ---------------- Capping ----------------

    private void CapAllOpenSockets()
    {
        if (!useDeadEndsToCap) return;
        if (deadEndCache.Count == 0)
        {
            if (log) Debug.LogWarning("CapAllOpenSockets: No dead ends available.");
            return;
        }

        int caps = 0;

        for (int i = openSockets.Count - 1; i >= 0; i--)
        {
            if (caps >= maxCapsAfterEnd) break;

            var target = openSockets[i];
            bool capped = false;

            for (int attempt = 0; attempt < attemptsPerOpenSocket; attempt++)
            {
                var dead = WeightedPick(deadEndCache);
                if (dead == null) continue;

                // capping uses extra padding (more conservative)
                if (TryAttachRoom(target, dead, out var newPlaced, extraOverlapPadding: capExtraPadding))
                {
                    openSockets.RemoveAt(i);
                    placed.Add(newPlaced);

                    // IMPORTANT: dead ends are caps -> do NOT add their sockets
                    caps++;
                    capped = true;
                    break;
                }
            }

            if (!capped)
            {
                // can't cap => close logically (treated as wall)
                openSockets.RemoveAt(i);
            }
        }

        if (log) Debug.Log($"CapAllOpenSockets: capped={caps}");
    }

    // ---------------- Utils ----------------

    private void Shuffle<T>(T[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int k = rng.Next(0, i + 1);
            (array[i], array[k]) = (array[k], array[i]);
        }
    }

    private static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            DestroyImmediate(t.GetChild(i).gameObject);
    }

    // ---------------- Data ----------------

    [Serializable]
    private class PlacedRoom
    {
        public RoomDefinition def;
        public GameObject root;
        public HashSet<int> connectedSocketInstanceIds = new();

        public PlacedRoom(RoomDefinition def, GameObject root)
        {
            this.def = def;
            this.root = root;
        }
    }

    private struct OpenSocket
    {
        public PlacedRoom owner;
        public SocketMarker marker;

        public SocketType type;
        public Vector3 center;
        public Vector3 forward;
        public float width;
    }
}