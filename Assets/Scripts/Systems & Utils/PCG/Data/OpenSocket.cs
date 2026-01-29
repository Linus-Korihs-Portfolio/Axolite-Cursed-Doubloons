using UnityEngine;

namespace PCG.RoomAssembler.Data
{
    public struct OpenSocket
    {
        public PlacedRoom owner;
        public SocketMarker marker;

        public SocketType type;
        public Vector3 center;
        public Vector3 forward;
        public float width;
    }
}
