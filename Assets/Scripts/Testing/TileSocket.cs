// TileSocket.cs
using UnityEngine;

public enum SocketType
{
    Ground,
    Road
}

public enum Direction
{
    North = 0,
    East  = 1,
    South = 2,
    West  = 3
}

public class TileSocket : MonoBehaviour
{
    public SocketType socketType;
    public Direction direction;

    [Tooltip("World height level at this socket when connected. Only relevant for Road sockets.")]
    public float heightLevel;
}
