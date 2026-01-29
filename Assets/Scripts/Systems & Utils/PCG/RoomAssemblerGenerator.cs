using System;
using System.Collections.Generic;
using UnityEngine;
using PCG.RoomAssembler;
using PCG.RoomAssembler.Data;
using PCG.RoomAssembler.Logic;

public class RoomAssemblerGenerator : MonoBehaviour
{
    [Header("Rooms")]
    public RoomDefinition startRoom;
    public RoomDefinition endRoom;
    public List<RoomDefinition> roomPool = new();

    [Header("Wall Cap (Fallback)")]
    [Tooltip("Cap prefab without SocketMarkers (e.g. wall/door blocker). Layer should be Cap")]
    public RoomDefinition wallCapRoom;

    [Header("Generation")]
    [Min(1)] public int maxRooms = 50;

    [Tooltip("Minimum number of connections (steps) from Start to End.")]
    [Min(0)] public int minStepsToEnd = 6;

    [Tooltip("Maximum number of connections (steps) from Start to End. After this we try to force End.")]
    [Min(1)] public int maxStepsToEnd = 20;

    [Tooltip("How many attempts per socket before we give up on that socket.")]
    [Min(1)] public int attemptsPerOpenSocket = 50;

    [Header("Capping")]
    public bool capOpenSocketsAfterEnd = true;
    public bool useDeadEndsToCap = true;

    [Tooltip("Max number of caps we place after End to avoid runaway spawning.")]
    [Min(0)] public int maxCapsAfterEnd = 999;

    [Header("Capping Safety")]
    [Tooltip("Extra overlap padding applied only during capping (dead-ends / caps).")]
    public float capExtraPadding = 0.03f;

    [Tooltip("If true, caps will also avoid overlapping other caps (Cap layer).")]
    public bool preventCapOverlappingCaps = true;

    [Header("Overlap / Layers")]
    [Tooltip("Only 'Generated' rooms layer. CAPS should NOT be included here.")]
    public LayerMask roomOverlapMask;

    [Tooltip("Only 'Cap' layer. Used only if preventCapOverlappingCaps = true.")]
    public LayerMask capOverlapMask;

    [Tooltip("Shrink bounds slightly so edge-touching is NOT considered overlap.")]
    public float overlapPadding = 0.02f;

    [Header("Wall Cap Placement")]
    [Tooltip("Push the wall slightly towards the socket (0 = exactly at socket center).")]
    public float wallCapInset = 0.0f;

    [Tooltip("Extra rotation offset for wall caps if your prefab forward is not aligned.")]
    public float wallCapYawOffset = 0f;

    [Header("Socket Matching")]
    [Tooltip("If two socket centers are closer than this, they can be treated as meeting (for loops).")]
    public float centerSnapTolerance = 0.02f;

    [Tooltip("Dot threshold for opposite facing. 1 = perfect opposite. 0.95 ~ 18 degrees.")]
    public float forwardDotTolerance = 0.95f;

    [Tooltip("Fallback width tolerance if RoomDefinition has none.")]
    public float widthToleranceFallback = 0.05f;

    [Header("Loops")]
    public bool allowLoops = true;
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
    private readonly List<RoomDefinition> deadEndCache = new();

    private RoomPicker roomPicker;
    private RoomPlacer roomPlacer;
    private Capping capping;

    private void Start() => Generate();

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (!ValidateSetup()) return;

        if (parent == null) parent = transform;
        if (clearBeforeGenerate) ClearChildren(parent);

        if (randomSeed) seed = Environment.TickCount;
        rng = new System.Random(seed);

        roomPicker = new RoomPicker(rng);
        roomPlacer = new RoomPlacer(
            parent: parent,
            rng: rng,
            overlapPadding: overlapPadding,
            widthToleranceFallback: widthToleranceFallback,
            wallCapInset: wallCapInset,
            wallCapYawOffset: wallCapYawOffset,
            log: log
        );
        capping = new Capping(roomPicker, roomPlacer);

        placed.Clear();
        openSockets.Clear();
        BuildDeadEndCache();

        // Spawn Start at origin
        var startGO = Instantiate(startRoom.prefab, Vector3.zero, Quaternion.identity, parent);
        startGO.name = $"START_{startRoom.id}";
        var startPlaced = new PlacedRoom(startRoom, startGO);
        placed.Add(startPlaced);

        SocketUtils.AddOpenSocketsFromRoom(openSockets, startPlaced);

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
        if (roomOverlapMask == 0)
            Debug.LogWarning("roomOverlapMask is 0. Set it to your 'Generated' layer.");

        if (wallCapRoom == null || wallCapRoom.prefab == null)
            Debug.LogWarning("wallCapRoom is not set. Fallback wall caps will not be placed.");

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
        int forcedEndAttempts = 0;
        int maxForcedEndAttempts = attemptsPerOpenSocket * 2;

        while (safety-- > 0 && placed.Count < maxRooms)
        {
            if (openSockets.Count == 0) return false;

            bool forceEndNow = placedAfterStart >= maxStepsToEnd;

            int idx = rng.Next(openSockets.Count);
            var target = openSockets[idx];

            bool placedSomething = false;

            for (int attempt = 0; attempt < attemptsPerOpenSocket; attempt++)
            {
                RoomDefinition candidate =
                    (forceEndNow || placedAfterStart >= minStepsToEnd)
                        ? roomPicker.PickRoomWithBiasToEnd(roomPool, endRoom, forceEndNow)
                        : roomPicker.PickNonEndRoom(roomPool, endPlaced);

                if (candidate == null || candidate.prefab == null)
                    continue;

                if (roomPlacer.TryAttachRoom(target, candidate, out var newPlaced, extraOverlapPadding: 0f, overlapMaskToUse: roomOverlapMask))
                {
                    openSockets.RemoveAt(idx);

                    placed.Add(newPlaced);
                    placedAfterStart++;

                    SocketUtils.AddOpenSocketsFromRoom(openSockets, newPlaced);

                    if (allowLoops && autoCloseMatchingSockets)
                        SocketUtils.CloseAnySocketPairsThatMeet(openSockets, centerSnapTolerance, forwardDotTolerance, widthToleranceFallback);

                    if (candidate == endRoom)
                    {
                        endPlaced = true;

                        if (capOpenSocketsAfterEnd)
                        {       
                        capping.CapAllOpenSockets(
                            openSockets: openSockets,
                            placedRooms: placed,
                            deadEndCache: deadEndCache,
                            wallCapRoom: wallCapRoom,
                            attemptsPerOpenSocket: attemptsPerOpenSocket,
                            maxCapsAfterEnd: maxCapsAfterEnd,
                            useDeadEndsToCap: useDeadEndsToCap,
                            capExtraPadding: capExtraPadding,
                            roomOverlapMask: roomOverlapMask,
                            preventCapOverlappingCaps: preventCapOverlappingCaps,
                            capOverlapMask: capOverlapMask,
                            log: log
                        );
                    }
                        return true;
                    }

                    placedSomething = true;
                    break;
                }
            }

            if (!placedSomething)
            {
                target.owner.connectedSocketInstanceIds.Add(target.marker.GetInstanceID());

                if (forceEndNow)
                {
                    forcedEndAttempts++;
                    if (forcedEndAttempts >= maxForcedEndAttempts)
                        return false;
                }
            }
            else
            {
                forcedEndAttempts = 0;
            }
        }

        return false;
    }
    private static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            DestroyImmediate(t.GetChild(i).gameObject);
    }
}