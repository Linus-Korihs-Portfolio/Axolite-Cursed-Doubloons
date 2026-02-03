using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PCG/Room Assembler/Generator Config", fileName = "RoomAssemblerConfig")]
public class RoomAssemblerConfig : ScriptableObject
{
    [Header("Rooms")]
    public RoomDefinition startRoom;
    public RoomDefinition endRoom;
    public List<RoomDefinition> roomPool = new();

    [Header("Wall Cap (Fallback)")]
    public RoomDefinition wallCapRoom;

    [Header("Room Count")]
    [Min(1)] public int minRooms = 20;
    [Min(1)] public int maxRooms = 50;

    [Header("End Distance (in Rooms)")]
    [Tooltip("Distance measured in multiples of one room-size (derived from Start Bounds unless overridden).")]
    public bool useEndDistanceRange = true;

    [Min(0)] public int minEndDistanceRooms = 5;
    [Min(0)] public int maxEndDistanceRooms = 10;

    [Tooltip("0 = auto from Start Bounds (XZ max). Otherwise overrides size of 1 'room' in world units.")]
    [Min(0f)] public float roomUnitWorldOverride = 0f;

    [Header("Attempts")]
    [Min(1)] public int attemptsPerOpenSocket = 50;

    [Tooltip("How many full generation retries if constraints fail.")]
    [Min(1)] public int maxGenerationRetries = 5;

    [Header("Loops")]
    public bool allowLoops = true;
    public bool autoCloseMatchingSockets = true;

    [Header("Socket Matching")]
    public float centerSnapTolerance = 0.02f;
    public float forwardDotTolerance = 0.95f;
    public float widthToleranceFallback = 0.05f;

    [Header("Overlap / Layers")]
    public LayerMask roomOverlapMask;
    public LayerMask capOverlapMask;
    public float overlapPadding = 0.02f;

    [Header("Capping")]
    public bool capOpenSocketsAfterEnd = true;
    public bool useDeadEndsToCap = true;
    
    [Min(0)] public int maxCapsAfterEnd = 999;
    public float capExtraPadding = 0.03f;
    public bool preventCapOverlappingCaps = true;

    [Header("Wall Cap Placement")]
    public float wallCapInset = 0.0f;
    public float wallCapYawOffset = 0f;

    [Header("Seed")]
    public bool randomSeed = true;
    public int seed = 12345;

    [Header("Debug")]
    public bool log = false;
}
