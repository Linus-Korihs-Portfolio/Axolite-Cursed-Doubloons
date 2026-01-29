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

        public PlacedRoom(RoomDefinition def, GameObject root)
        {
            this.def = def;
            this.root = root;
        }
    }
}