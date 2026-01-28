using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PCG/Room Definition")]
public class RoomDefinition : ScriptableObject
{
    public string id;
    public GameObject prefab;

    [Min(1)] public int weight = 10;

    [Header("Flags")]
    public bool isStart = false;
    public bool isEnd = false;
    public bool isDeadEnd = false;
    public bool isHallway = false;
    public bool isRoom = false;

    [Header("Placement")]
    public bool allowRotation = true;

    [Tooltip("Socket width tolerance (world units). Example: 0.05")]
    public float widthTolerance = 0.05f;

    public List<SocketType> allowedSocketTypes = new() { SocketType.Corridor, SocketType.Door, SocketType.BigDoor };
}
