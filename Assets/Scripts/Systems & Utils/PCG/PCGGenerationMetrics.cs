using System;
using System.Collections.Generic;

namespace PCG.RoomAssembler.Metrics
{
    [Serializable]
    public sealed class PCGGenerationMetrics
    {
        public string timestampUtc;
        public string generatorName;
        public string variantId = "Standard";
        public int runIndex;
        public int initialSeed;
        public int seed;

        public bool success;
        public string failureCategory = "None";
        public bool emergencyFallbackUsed;
        public int failedFullAttempts;
        public int fullAttemptsUsed;
        public int candidatePlacementAttempts;
        public int unfillableSockets;

        public int configuredMinRooms;
        public int configuredMaxRooms;
        public int configuredMinBossDistanceRooms;
        public int effectiveMinRooms;
        public int effectiveMaxRooms;
        public int actualRoomsBeforeCapping;
        public int actualRoomsAfterCapping;
        public int openSocketsBeforeCapping = -1;
        public int remainingOpenSocketsAfterCapping = -1;

        public int capPlacements;
        public int deadEndCaps;
        public int wallCaps;
        public int logicalOnlyCaps;

        public int startToBossGraphDistance = -1;
        public int criticalPathRooms;
        public int sidePathRooms;
        public float branchingFactor;
        public bool specialRoomPlacementValid = true;
        public int invalidSpecialRoomCount;
        public string specialRoomValidation = string.Empty;

        public double totalGenerationMs;
        public double roomPlacementMs;
        public double cappingMs;
        public double navMeshMs;
        public double contentSpawningMs;

        public readonly Dictionary<string, int> roomTypeFrequency = new();
        public readonly Dictionary<string, int> generationFailureCounts = new();
        public readonly Dictionary<string, int> placementFailureCounts = new();
        public readonly List<PCGSpecialRoomMetric> specialRooms = new();

        public static PCGGenerationMetrics Create(
            string generatorName,
            int runIndex,
            RoomAssemblerConfig config)
        {
            return new PCGGenerationMetrics
            {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                generatorName = generatorName,
                runIndex = runIndex,
                configuredMinRooms = config != null ? config.minRooms : 0,
                configuredMaxRooms = config != null ? config.maxRooms : 0,
                configuredMinBossDistanceRooms = config != null ? config.minEndDistanceRooms : 0,
                effectiveMinRooms = config != null ? config.minRooms : 0,
                effectiveMaxRooms = config != null ? config.maxRooms : 0
            };
        }

        public void IncrementRoomType(string key)
        {
            Increment(roomTypeFrequency, key, 1);
        }

        public static void Increment(Dictionary<string, int> counts, string key, int amount)
        {
            if (string.IsNullOrWhiteSpace(key))
                key = "Unknown";

            if (counts.TryGetValue(key, out int current))
            {
                counts[key] = current + amount;
            }
            else
            {
                counts.Add(key, amount);
            }
        }
    }

    [Serializable]
    public sealed class PCGSpecialRoomMetric
    {
        public string kind;
        public string roomId;
        public int graphDegree;
        public int graphDistance = -1;
        public bool requiresDeadEnd;
        public bool deadEndValid;
        public bool distanceValid = true;
        public bool valid;
    }
}
