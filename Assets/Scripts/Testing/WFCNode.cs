using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WFCDirection
{
    North,
    East,
    South,
    West
}

[CreateAssetMenu(menuName = "PCG/WFC Node")]
public class WFCNode : ScriptableObject
{
    [Header("ID / Prefab")]
    public string id;
    public GameObject prefab;

    [Header("Selection")]
    [Min(1)] public int weight = 10;

    [Header("Visual")]
    [Tooltip("If true, this tile may be randomly rotated by the generator (0/90/180/270). Rules do NOT change.")]
    public bool allowVisualRotation = false;

    [Tooltip("Extra Y offset when spawned (e.g. raise walls).")]
    public float spawnYOffset = 0f;

    [Header("Adjacency (Compatible Neighbours)")]
    public List<WFCNode> northAllowed = new();
    public List<WFCNode> eastAllowed  = new();
    public List<WFCNode> southAllowed = new();
    public List<WFCNode> westAllowed  = new();

    public IReadOnlyList<WFCNode> GetAllowed(WFCDirection dir)
    {
        return dir switch
        {
            WFCDirection.North => northAllowed,
            WFCDirection.East  => eastAllowed,
            WFCDirection.South => southAllowed,
            WFCDirection.West  => westAllowed,
            _ => northAllowed
        };
    }

    public bool AllowsNeighbour(WFCDirection dir, WFCNode neighbour)
    {
        if (neighbour == null) return false;
        var list = GetAllowed(dir);
        return list != null && list.Contains(neighbour);
    }
}
