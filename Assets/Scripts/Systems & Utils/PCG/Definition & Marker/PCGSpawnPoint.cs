using System;
using System.Collections.Generic;
using UnityEngine;

public enum PCGSpawnPointKind
{
    Player,
    Minion,
    Enemy,
    Item
}

public class PCGSpawnPoint : MonoBehaviour
{
    [Header("Spawn")]
    public PCGSpawnPointKind kind = PCGSpawnPointKind.Enemy;

    [Tooltip("Required points are filled before random weighted points are considered.")]
    public bool required;

    [Tooltip("Higher values make this point more likely when the room has more valid points than budget.")]
    [Min(0)] public int weight = 10;

    [Tooltip("Optional stable id for debugging and special rules.")]
    public string spawnPointId;

    [Header("Level Range")]
    [Min(1)] public int minLevel = 1;
    [Min(1)] public int maxLevel = 999;

    [Header("Allowed Content")]
    [Tooltip("Empty means every entry from the matching pool is allowed. Otherwise match by WeightedSpawnEntry id.")]
    public List<string> allowedContentIds = new List<string>();

    [NonSerialized] public bool occupied;

    public bool IsValidForLevel(int levelIndex)
    {
        return levelIndex >= minLevel && levelIndex <= Mathf.Max(minLevel, maxLevel);
    }

    public bool AllowsContent(string contentId)
    {
        if (allowedContentIds == null || allowedContentIds.Count == 0) return true;
        if (string.IsNullOrWhiteSpace(contentId)) return false;

        for (int i = 0; i < allowedContentIds.Count; i++)
        {
            if (string.Equals(allowedContentIds[i], contentId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = kind switch
        {
            PCGSpawnPointKind.Player => Color.green,
            PCGSpawnPointKind.Minion => Color.cyan,
            PCGSpawnPointKind.Enemy => Color.red,
            PCGSpawnPointKind.Item => Color.yellow,
            _ => Color.white
        };

        Gizmos.DrawSphere(transform.position, required ? 0.22f : 0.14f);
        Gizmos.DrawRay(transform.position, transform.forward * 0.5f);
    }
#endif
}
