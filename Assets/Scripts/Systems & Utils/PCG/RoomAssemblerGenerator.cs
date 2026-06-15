using System;
using System.Collections.Generic;
using System.Text;
using PCG.RoomAssembler;
using PCG.RoomAssembler.Data;
using PCG.RoomAssembler.Logic;
using UnityEngine;

public class RoomAssemblerGenerator : MonoBehaviour
{
    public RoomAssemblerConfig config;

    [Header("Output")]
    public Transform parent;
    public bool clearBeforeGenerate = true;

    [Header("Content")]
    [Tooltip("Optional runtime NavMesh build step. Runs after layout generation and before content spawning.")]
    public RuntimeNavMeshBuilder navMeshBuilder;

    [Tooltip("Optional second pass that fills generated rooms with player, minions, enemies, and items.")]
    public LevelContentSpawner contentSpawner;

    public int LastRunSeed { get; private set; }
    public int LastGenerationFailedAttempts { get; private set; }
    public string LastGenerationFailureSummary { get; private set; }
    public bool IsGenerating => isGenerating;

    private System.Random rng;
    private bool isGenerating;

    private readonly List<PlacedRoom> placed = new();
    private readonly List<OpenSocket> openSockets = new();
    private readonly List<OpenSocket> socketsToCap = new();
    private readonly List<RoomDefinition> deadEndCache = new();

    private RoomPicker roomPicker;
    private RoomPlacer roomPlacer;
    private Capping capping;

    private void Start() => Generate();

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (!ValidateSetup()) return;
        if (isGenerating)
        {
            Debug.LogWarning("[PCG] Generate ignored because generation is already running.", this);
            return;
        }

        if (parent == null) parent = transform;
        if (navMeshBuilder == null) navMeshBuilder = GetComponent<RuntimeNavMeshBuilder>();
        if (navMeshBuilder == null && parent != null) navMeshBuilder = parent.GetComponent<RuntimeNavMeshBuilder>();
        if (contentSpawner == null) contentSpawner = GetComponent<LevelContentSpawner>();

        // Content lives outside PCG_Level, so clear it before layout retries begin.
        // Otherwise a failed regeneration leaves enemies from the previous layout active.
        if (contentSpawner != null)
        {
            contentSpawner.ClearSpawnedObjects();
        }

        isGenerating = true;
        LastGenerationFailedAttempts = 0;
        LastGenerationFailureSummary = string.Empty;
        var runDiagnostics = new GenerationRunDiagnostics();

        try
        {
            for (int attempt = 0; attempt < config.maxGenerationRetries; attempt++)
            {
                if (clearBeforeGenerate) ClearChildren(parent);

                int runSeed = config.randomSeed
                    ? Environment.TickCount + attempt
                    : config.seed + attempt;

                LastRunSeed = runSeed;
                rng = new System.Random(runSeed);

                roomPicker = new RoomPicker(rng);
                roomPlacer = new RoomPlacer(
                    parent: parent,
                    rng: rng,
                    overlapPadding: config.overlapPadding,
                    widthToleranceFallback: config.widthToleranceFallback,
                    wallCapInset: config.wallCapInset,
                    log: config.log
                );
                capping = new Capping(roomPicker, roomPlacer);

                placed.Clear();
                openSockets.Clear();
                socketsToCap.Clear();
                BuildDeadEndCache(config.roomPool);

                var startGO = Instantiate(config.startRoom.prefab, Vector3.zero, Quaternion.identity, parent);
                startGO.name = $"START_{config.startRoom.id}";
                var startPlaced = new PlacedRoom(config.startRoom, startGO);
                placed.Add(startPlaced);

                SocketUtils.AddOpenSocketsFromRoom(openSockets, startPlaced);

                float roomUnitWorld = config.roomUnitWorldOverride > 0f
                    ? config.roomUnitWorldOverride
                    : ComputeRoomUnitWorldFromBounds(startGO);

                float minEndWorld = config.minEndDistanceRooms * roomUnitWorld;
                float maxEndWorld = Mathf.Max(minEndWorld, config.maxEndDistanceRooms * roomUnitWorld);
                var attemptDiagnostics = new GenerationAttemptDiagnostics(runSeed);

                bool success = GrowUntilEnd(
                    startWorldPos: GetStartCenterWorld(startGO),
                    useDistanceRange: config.useEndDistanceRange,
                    minEndWorld: minEndWorld,
                    maxEndWorld: maxEndWorld,
                    minRooms: config.minRooms,
                    maxRooms: config.maxRooms,
                    attemptsPerOpenSocket: config.attemptsPerOpenSocket,
                    diagnostics: attemptDiagnostics,
                    failureReason: out GenerationFailureReason failureReason
                );

                if (success)
                {
                    LastGenerationFailedAttempts = attempt;
                    LastGenerationFailureSummary = runDiagnostics.FormatSummary();

                    Debug.Log(
                        $"[PCG] Layout succeeded. Seed={runSeed}, Rooms={placed.Count}, " +
                        $"FailedAttempts={attempt}. {LastGenerationFailureSummary}",
                        this);

                    if (navMeshBuilder != null)
                    {
                        Debug.Log("[PCG] Building NavMesh.", this);
                        navMeshBuilder.Build(parent);
                        Debug.Log("[PCG] NavMesh build complete.", this);
                    }

                    if (contentSpawner != null)
                    {
                        Debug.Log("[PCG] Spawning generated level content.", this);
                        contentSpawner.SpawnForGeneratedRooms(placed, runSeed);
                        Debug.Log("[PCG] Content spawning complete.", this);
                    }
                    return;
                }

                runDiagnostics.Record(failureReason, attemptDiagnostics);
                Debug.LogWarning(
                    $"[PCG] Attempt {attempt + 1}/{config.maxGenerationRetries} failed. " +
                    attemptDiagnostics.FormatAttempt(failureReason, placed.Count, openSockets.Count),
                    this);
            }

            LastGenerationFailedAttempts = config.maxGenerationRetries;
            LastGenerationFailureSummary = runDiagnostics.FormatSummary();
            Debug.LogError(
                $"[PCG] Generation failed after {config.maxGenerationRetries} retries. " +
                LastGenerationFailureSummary,
                this);
        }
        finally
        {
            isGenerating = false;
        }
    }

    private bool ValidateSetup()
    {
        if (config == null)
        {
            Debug.LogError("RoomAssemblerConfig missing.", this);
            return false;
        }

        if (config.startRoom == null || config.startRoom.prefab == null)
        {
            Debug.LogError("Start room missing.", this);
            return false;
        }

        if (config.endRoom == null || config.endRoom.prefab == null)
        {
            Debug.LogError("End room missing.", this);
            return false;
        }

        if (config.roomPool == null || config.roomPool.Count == 0)
        {
            Debug.LogError("Room pool is empty.", this);
            return false;
        }

        if (config.maxRooms < config.minRooms)
        {
            Debug.LogError("Config invalid: maxRooms < minRooms.", this);
            return false;
        }

        if (config.attemptsPerOpenSocket < 1 || config.maxGenerationRetries < 1)
        {
            Debug.LogError("Config invalid: generation attempts and retries must be at least 1.", this);
            return false;
        }

        if (config.roomOverlapMask == 0)
        {
            Debug.LogWarning("roomOverlapMask is 0. Set it to your 'Generated' layer.", this);
        }

        return true;
    }

    private bool GrowUntilEnd(
        Vector3 startWorldPos,
        bool useDistanceRange,
        float minEndWorld,
        float maxEndWorld,
        int minRooms,
        int maxRooms,
        int attemptsPerOpenSocket,
        GenerationAttemptDiagnostics diagnostics,
        out GenerationFailureReason failureReason)
    {
        int safety = Mathf.Max(1, maxRooms) * 250;
        bool endPlaced = false;
        failureReason = GenerationFailureReason.None;

        while (safety-- > 0 && placed.Count < maxRooms)
        {
            if (openSockets.Count == 0)
            {
                failureReason = GenerationFailureReason.OpenSocketsExhausted;
                return false;
            }

            int idx = rng.Next(openSockets.Count);
            OpenSocket target = openSockets[idx];
            bool placedSomething = false;

            for (int attempt = 0; attempt < attemptsPerOpenSocket; attempt++)
            {
                bool canTryEndByRoomCount = placed.Count >= Mathf.Max(1, minRooms - 1);
                RoomDefinition candidate;
                if (!endPlaced && canTryEndByRoomCount)
                {
                    candidate = roomPicker.PickRoomWithBiasToEnd(config.roomPool, config.endRoom, forceEnd: false);
                }
                else
                {
                    candidate = roomPicker.PickNonEndRoom(config.roomPool, endPlaced);
                }

                diagnostics.CandidateAttempts++;
                if (candidate == null || candidate.prefab == null)
                {
                    diagnostics.RecordPlacementFailure(PlacementFailureReason.MissingRoomOrPrefab);
                    continue;
                }

                bool isEnd = !endPlaced && candidate == config.endRoom;
                Func<Vector3, bool> endValidator = null;
                if (isEnd && useDistanceRange)
                {
                    endValidator = endCenter =>
                    {
                        float distance = Vector3.Distance(startWorldPos, endCenter);
                        return distance >= minEndWorld && distance <= maxEndWorld;
                    };
                }

                if (roomPlacer.TryAttachRoom(
                        target,
                        candidate,
                        out PlacedRoom newPlaced,
                        out PlacementFailureReason placementFailure,
                        extraOverlapPadding: 0f,
                        overlapMaskToUse: config.roomOverlapMask,
                        placementCenterValidator: endValidator))
                {
                    openSockets.RemoveAt(idx);
                    placed.Add(newPlaced);
                    SocketUtils.AddOpenSocketsFromRoom(openSockets, newPlaced);

                    if (config.allowLoops && config.autoCloseMatchingSockets)
                    {
                        while (SocketUtils.CloseAnySocketPairsThatMeet(
                                   openSockets,
                                   config.centerSnapTolerance,
                                   config.forwardDotTolerance,
                                   config.widthToleranceFallback))
                        {
                        }
                    }

                    if (isEnd) endPlaced = true;
                    placedSomething = true;
                    break;
                }

                diagnostics.RecordPlacementFailure(placementFailure);
            }

            if (!placedSomething)
            {
                openSockets.RemoveAt(idx);
                socketsToCap.Add(target);
                diagnostics.UnfillableSockets++;
            }

            if (endPlaced && placed.Count >= minRooms)
            {
                if (config.capOpenSocketsAfterEnd)
                {
                    openSockets.AddRange(socketsToCap);
                    socketsToCap.Clear();

                    if (config.allowLoops && config.autoCloseMatchingSockets)
                    {
                        while (SocketUtils.CloseAnySocketPairsThatMeet(
                                   openSockets,
                                   config.centerSnapTolerance,
                                   config.forwardDotTolerance,
                                   config.widthToleranceFallback))
                        {
                        }
                    }

                    Debug.Log($"[PCG] Capping {openSockets.Count} remaining open socket(s).", this);
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
                    Debug.Log($"[PCG] Capping complete. RemainingOpen={openSockets.Count}.", this);
                }

                return true;
            }
        }

        failureReason = placed.Count >= maxRooms
            ? GenerationFailureReason.RoomLimitReachedBeforeCompletion
            : GenerationFailureReason.SafetyLimitReached;
        return false;
    }

    private static void ClearChildren(Transform target)
    {
        for (int i = target.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(target.GetChild(i).gameObject);
        }
    }

    private void BuildDeadEndCache(List<RoomDefinition> pool)
    {
        deadEndCache.Clear();
        for (int i = 0; i < pool.Count; i++)
        {
            RoomDefinition room = pool[i];
            if (room == null || room.prefab == null) continue;
            if (room.isDeadEnd) deadEndCache.Add(room);
        }
    }

    private static float ComputeRoomUnitWorldFromBounds(GameObject roomRoot)
    {
        Transform boundsTransform = roomRoot.transform.Find("Bounds");
        if (boundsTransform != null)
        {
            BoxCollider bounds = boundsTransform.GetComponent<BoxCollider>();
            if (bounds != null)
            {
                Vector3 size = Vector3.Scale(bounds.size, bounds.transform.lossyScale);
                float unit = Mathf.Max(size.x, size.z);
                return Mathf.Max(0.01f, unit);
            }
        }

        return 4.5f;
    }

    private static Vector3 GetStartCenterWorld(GameObject startGO)
    {
        return OverlapChecker.TryGetBoundsCenter(startGO, out Vector3 center)
            ? center
            : startGO.transform.position;
    }

    private enum GenerationFailureReason
    {
        None,
        OpenSocketsExhausted,
        RoomLimitReachedBeforeCompletion,
        SafetyLimitReached
    }

    private sealed class GenerationAttemptDiagnostics
    {
        private readonly Dictionary<PlacementFailureReason, int> placementFailures = new();

        public readonly int Seed;
        public int CandidateAttempts;
        public int UnfillableSockets;
        public IReadOnlyDictionary<PlacementFailureReason, int> PlacementFailures => placementFailures;

        public GenerationAttemptDiagnostics(int seed)
        {
            Seed = seed;
        }

        public void RecordPlacementFailure(PlacementFailureReason reason)
        {
            if (reason == PlacementFailureReason.None) return;
            Increment(placementFailures, reason, 1);
        }

        public string FormatAttempt(GenerationFailureReason failureReason, int roomCount, int openSocketCount)
        {
            return
                $"Seed={Seed}, Reason={failureReason}, Rooms={roomCount}, OpenSockets={openSocketCount}, " +
                $"UnfillableSockets={UnfillableSockets}, CandidateAttempts={CandidateAttempts}, " +
                $"PlacementFailures=[{FormatCounts(placementFailures)}]";
        }
    }

    private sealed class GenerationRunDiagnostics
    {
        private readonly Dictionary<GenerationFailureReason, int> generationFailures = new();
        private readonly Dictionary<PlacementFailureReason, int> placementFailures = new();
        private int candidateAttempts;
        private int unfillableSockets;

        public void Record(GenerationFailureReason reason, GenerationAttemptDiagnostics attempt)
        {
            Increment(generationFailures, reason, 1);
            candidateAttempts += attempt.CandidateAttempts;
            unfillableSockets += attempt.UnfillableSockets;

            foreach (KeyValuePair<PlacementFailureReason, int> pair in attempt.PlacementFailures)
            {
                Increment(placementFailures, pair.Key, pair.Value);
            }
        }

        public string FormatSummary()
        {
            return
                $"FailureReasons=[{FormatCounts(generationFailures)}], " +
                $"PlacementFailures=[{FormatCounts(placementFailures)}], " +
                $"CandidateAttempts={candidateAttempts}, UnfillableSockets={unfillableSockets}.";
        }
    }

    private static void Increment<T>(Dictionary<T, int> counts, T key, int amount)
    {
        if (counts.TryGetValue(key, out int current))
        {
            counts[key] = current + amount;
        }
        else
        {
            counts.Add(key, amount);
        }
    }

    private static string FormatCounts<T>(Dictionary<T, int> counts)
    {
        if (counts.Count == 0) return "none";

        var builder = new StringBuilder();
        foreach (T value in Enum.GetValues(typeof(T)))
        {
            if (!counts.TryGetValue(value, out int count)) continue;
            if (builder.Length > 0) builder.Append(", ");
            builder.Append(value);
            builder.Append('=');
            builder.Append(count);
        }

        return builder.ToString();
    }
}
