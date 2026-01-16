using UnityEngine;

public enum SocketType
{
    Ground,
    Road
}

public enum Direction
{
    North,
    East,
    South,
    West
}


public class TileSocket : MonoBehaviour
{
    public SocketType socketType;
    public Direction direction;
    public float heightLevel;
}
