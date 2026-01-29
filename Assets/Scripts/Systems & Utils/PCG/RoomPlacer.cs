using System;
using UnityEngine;
using PCG.RoomAssembler.Data;
using PCG.RoomAssembler.Logic;

namespace PCG.RoomAssembler
{
    public class RoomPlacer
    {
        private readonly Transform parent;
        private readonly System.Random rng;

        private readonly float overlapPadding;
        private readonly float widthToleranceFallback;

        private readonly float wallCapInset;
        private readonly float wallCapYawOffset;

        private readonly bool log;

        public RoomPlacer(
            Transform parent,
            System.Random rng,
            float overlapPadding,
            float widthToleranceFallback,
            float wallCapInset,
            float wallCapYawOffset,
            bool log)
        {
            this.parent = parent;
            this.rng = rng;

            this.overlapPadding = overlapPadding;
            this.widthToleranceFallback = widthToleranceFallback;

            this.wallCapInset = wallCapInset;
            this.wallCapYawOffset = wallCapYawOffset;

            this.log = log;
        }

        public bool TryAttachRoom(
            OpenSocket target,
            RoomDefinition room,
            out PlacedRoom placedRoom,
            float extraOverlapPadding,
            LayerMask overlapMaskToUse)
        {
            placedRoom = null;
            if (room == null || room.prefab == null) return false;

            var go = UnityEngine.Object.Instantiate(room.prefab, Vector3.zero, Quaternion.identity, parent);
            go.SetActive(false);

            var markers = go.GetComponentsInChildren<SocketMarker>(true);
            if (markers == null || markers.Length == 0)
            {
                UnityEngine.Object.DestroyImmediate(go);
                return false;
            }

            Shuffle(markers);

            for (int i = 0; i < markers.Length; i++)
            {
                var candSocket = markers[i];
                if (candSocket == null || candSocket.left == null || candSocket.right == null) continue;

                if (candSocket.type != target.type) continue;
                if (!IsWidthCompatible(target.width, candSocket.WidthWorld, room)) continue;

                // rotate to face the target (opposite forward)
                Vector3 a = candSocket.ForwardWorld;
                Vector3 b = -target.forward;

                a.y = 0f;
                b.y = 0f;

                if (a.sqrMagnitude < 0.0001f || b.sqrMagnitude < 0.0001f)
                    continue;

                a.Normalize();
                b.Normalize();

                float yaw = Vector3.SignedAngle(a, b, Vector3.up);
                if (room.allowRotation)
                    go.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * go.transform.rotation;

                // snap socket centers
                Vector3 candCenter = candSocket.CenterWorld;
                go.transform.position += (target.center - candCenter);

                go.SetActive(true);
                Physics.SyncTransforms();

                bool overlaps = OverlapChecker.Overlaps(go, overlapPadding, extraOverlapPadding, overlapMaskToUse, ignoreRoot: null);

                if (log)
                {
                    Debug.Log($"TryAttach {room.id} via candSocket={candSocket.name} to target={target.marker.name} | " +
                              $"widthA={target.width:F2} widthB={candSocket.WidthWorld:F2} | overlaps={overlaps}");
                }

                if (!overlaps)
                {
                    go.name = room.id;

                    placedRoom = new PlacedRoom(room, go);

                    // mark BOTH sockets connected
                    placedRoom.connectedSocketInstanceIds.Add(candSocket.GetInstanceID());
                    target.owner.connectedSocketInstanceIds.Add(target.marker.GetInstanceID());

                    return true;
                }

                go.SetActive(false);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            UnityEngine.Object.DestroyImmediate(go);
            return false;
        }

        public bool TryPlaceWallCapFallback(
            OpenSocket target,
            RoomDefinition wallCapRoom,
            float capExtraPadding,
            LayerMask roomOverlapMask,
            bool preventCapOverlappingCaps,
            LayerMask capOverlapMask)
        {
            if (wallCapRoom == null || wallCapRoom.prefab == null) return false;

            var go = UnityEngine.Object.Instantiate(wallCapRoom.prefab, Vector3.zero, Quaternion.identity, parent);
            go.SetActive(false);

            Quaternion rot = Quaternion.LookRotation(target.forward, Vector3.up) * Quaternion.Euler(0f, wallCapYawOffset, 0f);
            Vector3 pos = target.center + (-target.forward * wallCapInset);

            go.transform.SetPositionAndRotation(pos, rot);

            go.SetActive(true);
            Physics.SyncTransforms();

            LayerMask mask = roomOverlapMask;
            if (preventCapOverlappingCaps)
                mask |= capOverlapMask;

            bool overlaps = OverlapChecker.Overlaps(go, overlapPadding, capExtraPadding, mask, ignoreRoot: target.owner.root.transform);

            if (log)
                Debug.Log($"TryWallCap {wallCapRoom.id} at {target.marker.name} overlaps={overlaps}");

            if (overlaps)
            {
                UnityEngine.Object.DestroyImmediate(go);
                return false;
            }

            go.name = $"CAP_{wallCapRoom.id}";
            return true;
        }

        private bool IsWidthCompatible(float a, float b, RoomDefinition room)
        {
            float tol = (room != null) ? Mathf.Max(0.0001f, room.widthTolerance) : widthToleranceFallback;
            return Mathf.Abs(a - b) <= tol;
        }

        private void Shuffle<T>(T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int k = rng.Next(0, i + 1);
                (array[i], array[k]) = (array[k], array[i]);
            }
        }
    }
}
