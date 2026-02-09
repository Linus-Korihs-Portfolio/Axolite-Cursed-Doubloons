using System;
using System.Collections.Generic;
using UnityEngine;
using PCG.RoomAssembler;
using PCG.RoomAssembler.Data;
using PCG.RoomAssembler.Logic;

public class RoomAssemblerGenerator : MonoBehaviour
{
    public RoomAssemblerConfig config;

    [Header("Output")]
    public Transform parent;
    public bool clearBeforeGenerate = true;
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

        for (int attempt = 0; attempt < config.maxGenerationRetries; attempt++)
        {
            if (clearBeforeGenerate) ClearChildren(parent);

            int runSeed = config.randomSeed ? (Environment.TickCount + attempt) : config.seed;
            rng = new System.Random(runSeed);

            roomPicker = new RoomPicker(rng);
            roomPlacer = new RoomPlacer(
                parent: parent,
                rng: rng,
                overlapPadding: config.overlapPadding,
                widthToleranceFallback: config.widthToleranceFallback,
                wallCapInset: config.wallCapInset,
                wallCapYawOffset: config.wallCapYawOffset,
                log: config.log
            );
            capping = new Capping(roomPicker, roomPlacer);

            placed.Clear();
            openSockets.Clear();
            BuildDeadEndCache(config.roomPool);

            var startGO = Instantiate(config.startRoom.prefab, Vector3.zero, Quaternion.identity, parent);
            startGO.name = $"START_{config.startRoom.id}";
            var startPlaced = new PlacedRoom(config.startRoom, startGO);
            placed.Add(startPlaced);

            SocketUtils.AddOpenSocketsFromRoom(openSockets, startPlaced);

            float roomUnitWorld = (config.roomUnitWorldOverride > 0f) ? config.roomUnitWorldOverride : ComputeRoomUnitWorldFromBounds(startGO);

            float minEndWorld = config.minEndDistanceRooms * roomUnitWorld;
            float maxEndWorld = Mathf.Max(minEndWorld, config.maxEndDistanceRooms * roomUnitWorld);

            bool success = GrowUntilEnd(
                startWorldPos: GetStartCenterWorld(startGO),
                useDistanceRange: config.useEndDistanceRange,
                minEndWorld: minEndWorld,
                maxEndWorld: maxEndWorld,
                minRooms: config.minRooms,
                maxRooms: config.maxRooms,
                attemptsPerOpenSocket: config.attemptsPerOpenSocket,
                runSeed: runSeed
            );

            if (success)
            {
                if (config.log) Debug.Log($"✓ Generation success. Seed={runSeed}, Rooms={placed.Count}, attempt={attempt + 1}");
                return;
            }
        }
        Debug.LogError($"✗ Generation failed after {config.maxGenerationRetries} retries.");
    }

    private bool ValidateSetup()
    {
        if (config == null)
        {
            Debug.LogError("RoomAssemblerConfig missing.");
            return false;
        }

        if (config.startRoom == null || config.startRoom.prefab == null)
        {
            Debug.LogError("Start room missing.");
            return false;
        }

        if (config.endRoom == null || config.endRoom.prefab == null)
        {
            Debug.LogError("End room missing.");
            return false;
        }

        if (config.roomPool == null || config.roomPool.Count == 0)
        {
            Debug.LogError("Room pool is empty.");
            return false;
        }

        if (config.maxRooms < config.minRooms)
        {
            Debug.LogError("Config invalid: maxRooms < minRooms.");
            return false;
        }

        if (config.roomOverlapMask == 0)
            Debug.LogWarning("roomOverlapMask is 0. Set it to your 'Generated' layer.");

        return true;
    }

    private void BuildDeadEndCache()
    {
        deadEndCache.Clear();
        for (int i = 0; i < config.roomPool.Count; i++)
        {
            var r = config.roomPool[i];
            if (r == null || r.prefab == null) continue;
            if (r.isDeadEnd) deadEndCache.Add(r);
        }
    }

    private bool GrowUntilEnd(
        Vector3 startWorldPos,
        bool useDistanceRange,
        float minEndWorld,
        float maxEndWorld,
        int minRooms,
        int maxRooms,
        int attemptsPerOpenSocket,
        int runSeed)
    {
        int safety = maxRooms * 250;
        bool endPlaced = false;

        while (safety-- > 0 && placed.Count < maxRooms)
        {
            if (openSockets.Count == 0) return false;

            int idx = rng.Next(openSockets.Count);
            var target = openSockets[idx];

            bool placedSomething = false;

            for (int attempt = 0; attempt < attemptsPerOpenSocket; attempt++)
            {
                bool canTryEndByRoomCount = (placed.Count >= Mathf.Max(1, minRooms - 1));
                RoomDefinition candidate;
                if (!endPlaced && canTryEndByRoomCount)
                {
                    candidate = roomPicker.PickRoomWithBiasToEnd(config.roomPool, config.endRoom, forceEnd: false);
                }
                else
                {
                    candidate = roomPicker.PickNonEndRoom(config.roomPool, endPlaced);
                }

                if (candidate == null || candidate.prefab == null) continue;

                bool isEnd = (!endPlaced && candidate == config.endRoom);

                Func<Vector3, bool> endValidator = null;
                if (isEnd && useDistanceRange)
                {
                    endValidator = (endCenter) =>
                    {
                        float d = Vector3.Distance(startWorldPos, endCenter);
                        return d >= minEndWorld && d <= maxEndWorld;
                    };
                }

                if (roomPlacer.TryAttachRoom(
                        target,
                        candidate,
                        out var newPlaced,
                        extraOverlapPadding: 0f,
                        overlapMaskToUse: config.roomOverlapMask,
                        placementCenterValidator: endValidator))
                {
                    openSockets.RemoveAt(idx);
                    placed.Add(newPlaced);

                    SocketUtils.AddOpenSocketsFromRoom(openSockets, newPlaced);

                    if (config.allowLoops && config.autoCloseMatchingSockets)
                        SocketUtils.CloseAnySocketPairsThatMeet(
                            openSockets,
                            config.centerSnapTolerance,
                            config.forwardDotTolerance,
                            config.widthToleranceFallback);

                    if (isEnd)
                    {
                        endPlaced = true;
                    }

                    placedSomething = true;
                    break;
                }
            }

            if (!placedSomething)
            {
                target.owner.connectedSocketInstanceIds.Add(target.marker.GetInstanceID());
            }

            if (endPlaced && placed.Count >= minRooms)
            {
                if (config.capOpenSocketsAfterEnd)
                {
                    capping.CapAllOpenSockets(
                        openSockets: openSockets,
                        placedRooms: placed,
                        deadEndCache: deadEndCache,
                        wallCapRoom: config.wallCapRoom,
                        attemptsPerOpenSocket: attemptsPerOpenSocket,
                        maxCapsAfterEnd: config.maxCapsAfterEnd,
                        useDeadEndsToCap: config.useDeadEndsToCap,
                        capExtraPadding: config.capExtraPadding,
                        roomOverlapMask: config.roomOverlapMask,
                        preventCapOverlappingCaps: config.preventCapOverlappingCaps,
                        capOverlapMask: config.capOverlapMask,
                        log: config.log
                    );
                }
                return true;
            }
        }
        return false;
    }

    private static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            DestroyImmediate(t.GetChild(i).gameObject);
    }

    private void BuildDeadEndCache(List<RoomDefinition> pool)
    {
        deadEndCache.Clear();
        for (int i = 0; i < pool.Count; i++)
        {
            var r = pool[i];
            if (r == null || r.prefab == null) continue;
            if (r.isDeadEnd) deadEndCache.Add(r);
        }
    }

    private static float ComputeRoomUnitWorldFromBounds(GameObject roomRoot)
    {
        var boundsTf = roomRoot.transform.Find("Bounds");
        if (boundsTf != null)
        {
            var bc = boundsTf.GetComponent<BoxCollider>();
            if (bc != null)
            {
                Vector3 size = Vector3.Scale(bc.size, bc.transform.lossyScale);
                float unit = Mathf.Max(size.x, size.z);
                return Mathf.Max(0.01f, unit);
            }
        }
        return 4.5f;
    }

    private static Vector3 GetStartCenterWorld(GameObject startGO)
    {
        return OverlapChecker.TryGetBoundsCenter(startGO, out var c) ? c : startGO.transform.position;
    }
}