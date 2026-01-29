using System.Collections.Generic;
using UnityEngine;
using PCG.RoomAssembler.Data;

namespace PCG.RoomAssembler.Logic
{
    public static class SocketUtils
    {
        public static void AddOpenSocketsFromRoom(List<OpenSocket> openSockets, PlacedRoom r)
        {
            var markers = r.root.GetComponentsInChildren<SocketMarker>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null || m.left == null || m.right == null) continue;

                if (r.connectedSocketInstanceIds.Contains(m.GetInstanceID()))
                    continue;

                openSockets.Add(new OpenSocket
                {
                    owner = r,
                    marker = m,
                    type = m.type,
                    center = m.CenterWorld,
                    forward = m.ForwardWorld.normalized,
                    width = m.WidthWorld
                });
            }
        }

        /// Closes ONE matching socket pair (loop) if found. Returns true if it closed something.
        public static bool CloseAnySocketPairsThatMeet(
            List<OpenSocket> openSockets,
            float centerSnapTolerance,
            float forwardDotTolerance,
            float widthToleranceFallback)
        {
            for (int i = openSockets.Count - 1; i >= 0; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    var a = openSockets[i];
                    var b = openSockets[j];

                    if (a.type != b.type) continue;
                    if (Vector3.Distance(a.center, b.center) > centerSnapTolerance) continue;

                    float dot = Vector3.Dot(a.forward, -b.forward);
                    if (dot < forwardDotTolerance) continue;

                    if (Mathf.Abs(a.width - b.width) > widthToleranceFallback) continue;

                    a.owner.connectedSocketInstanceIds.Add(a.marker.GetInstanceID());
                    b.owner.connectedSocketInstanceIds.Add(b.marker.GetInstanceID());

                    openSockets.RemoveAt(i);
                    openSockets.RemoveAt(j);
                    return true;
                }
            }

            return false;
        }
    }
}
