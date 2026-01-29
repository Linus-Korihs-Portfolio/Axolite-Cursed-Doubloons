using System.Collections.Generic;
using UnityEngine;
using PCG.RoomAssembler.Data;

namespace PCG.RoomAssembler.Logic
{
    public class Capping
    {
        private readonly RoomPicker roomPicker;
        private readonly RoomPlacer roomPlacer;

        public Capping(RoomPicker roomPicker, RoomPlacer roomPlacer)
        {
            this.roomPicker = roomPicker;
            this.roomPlacer = roomPlacer;
        }

        public int CapAllOpenSockets(
            List<OpenSocket> openSockets,
            List<PlacedRoom> placedRooms,
            List<RoomDefinition> deadEndCache,
            RoomDefinition wallCapRoom,
            int attemptsPerOpenSocket,
            int maxCapsAfterEnd,
            bool useDeadEndsToCap,
            float capExtraPadding,
            LayerMask roomOverlapMask,
            bool preventCapOverlappingCaps,
            LayerMask capOverlapMask,
            bool log = false)
        {
            int caps = 0;

            for (int i = openSockets.Count - 1; i >= 0; i--)
            {
                if (caps >= maxCapsAfterEnd) break;

                var target = openSockets[i];
                bool capped = false;

                // 1) Try dead ends (these are "caps", we don't add their sockets)
                if (useDeadEndsToCap && deadEndCache != null && deadEndCache.Count > 0)
                {
                    LayerMask capCheckMask = roomOverlapMask;
                    if (preventCapOverlappingCaps)
                        capCheckMask |= capOverlapMask;

                    for (int attempt = 0; attempt < attemptsPerOpenSocket; attempt++)
                    {
                        var dead = roomPicker.PickAny(deadEndCache);
                        if (dead == null || dead.prefab == null) continue;

                        if (roomPlacer.TryAttachRoom(target, dead, out var newPlaced, extraOverlapPadding: capExtraPadding, overlapMaskToUse: capCheckMask))
                        {
                            openSockets.RemoveAt(i);
                            placedRooms.Add(newPlaced);

                            caps++;
                            capped = true;
                            break;
                        }
                    }
                }

                if (capped) continue;

                // 2) Fallback wall cap
                if (roomPlacer.TryPlaceWallCapFallback(
                        target: target,
                        wallCapRoom: wallCapRoom,
                        capExtraPadding: capExtraPadding,
                        roomOverlapMask: roomOverlapMask,
                        preventCapOverlappingCaps: preventCapOverlappingCaps,
                        capOverlapMask: capOverlapMask))
                {
                    target.owner.connectedSocketInstanceIds.Add(target.marker.GetInstanceID());
                    openSockets.RemoveAt(i);
                    caps++;
                    capped = true;
                }

                if (!capped)
                {
                    // can't cap => close logically (treated as wall)
                    target.owner.connectedSocketInstanceIds.Add(target.marker.GetInstanceID());
                    openSockets.RemoveAt(i);
                }
            }

            if (log) Debug.Log($"Capping: capped={caps}");
            return caps;
        }
    }
}
