using System;
using System.Collections.Generic;
using UnityEngine;

namespace PCG.RoomAssembler.Data
{
    [Serializable]
    public class PlacedRoom
    {
        public RoomDefinition def;
        public GameObject root;
        public HashSet<int> connectedSocketInstanceIds = new(); // InstanceIDs of sockets that are already connected / blocked
        public List<PlacedRoom> connectedRooms = new();
        public bool isCap;

        public PlacedRoom(RoomDefinition def, GameObject root)
        {
            this.def = def;
            this.root = root;
        }

        public void ConnectTo(PlacedRoom other)
        {
            if (other == null || ReferenceEquals(this, other)) return;

            if (!connectedRooms.Contains(other))
                connectedRooms.Add(other);

            if (!other.connectedRooms.Contains(this))
                other.connectedRooms.Add(this);
        }
    }
}
