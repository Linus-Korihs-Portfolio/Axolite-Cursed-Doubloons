using System;
using UnityEngine;
using PCG.RoomAssembler.Data;
using PCG.RoomAssembler.Logic;

namespace PCG.RoomAssembler
{
    public enum PlacementFailureReason
    {
        None,
        MissingRoomOrPrefab,
        MissingTargetSocket,
        NoValidCandidateSockets,
        SocketTypeMismatch,
        SocketWidthMismatch,
        InvalidSocketDirection,
        PlacementConstraintRejected,
        Overlap
    }

    public enum WallCapFailureReason
    {
        None,
        MissingWallCapPrefab,
        MissingTargetSocket,
        InvalidSocketDirection,
        Overlap
    }

    public class RoomPlacer
    {
        private readonly Transform parent;
        private readonly System.Random rng;
        private readonly float overlapPadding;
        private readonly float widthToleranceFallback;
        private readonly float wallCapInset;
        private readonly bool log;

        public RoomPlacer(
            Transform parent,
            System.Random rng,
            float overlapPadding,
            float widthToleranceFallback,
            float wallCapInset,
            bool log)
        {
            this.parent = parent;
            this.rng = rng;
            this.overlapPadding = overlapPadding;
            this.widthToleranceFallback = widthToleranceFallback;
            this.wallCapInset = wallCapInset;
            this.log = log;
        }

        public bool TryAttachRoom(
            OpenSocket target,
            RoomDefinition room,
            out PlacedRoom placedRoom,
            out PlacementFailureReason failureReason,
            float extraOverlapPadding,
            LayerMask overlapMaskToUse,
            Func<Vector3, bool> placementCenterValidator = null)
        {
            placedRoom = null;
            failureReason = PlacementFailureReason.None;

            if (room == null || room.prefab == null)
            {
                failureReason = PlacementFailureReason.MissingRoomOrPrefab;
                return false;
            }

            if (target.marker == null || target.owner == null || target.owner.root == null)
            {
                failureReason = PlacementFailureReason.MissingTargetSocket;
                return false;
            }

            var go = UnityEngine.Object.Instantiate(room.prefab, Vector3.zero, Quaternion.identity, parent);
            go.SetActive(false);

            var markers = go.GetComponentsInChildren<SocketMarker>(true);
            if (markers == null || markers.Length == 0)
            {
                UnityEngine.Object.DestroyImmediate(go);
                failureReason = PlacementFailureReason.NoValidCandidateSockets;
                return false;
            }

            Shuffle(markers); // Randomize order of candidate sockets to try different placements on each run
            bool foundValidSocket = false;
            bool foundMatchingType = false;
            bool foundMatchingWidth = false;
            bool foundValidDirection = false;
            bool rejectedByConstraint = false;
            bool rejectedByOverlap = false;

            for (int i = 0; i < markers.Length; i++)
            {
                var candSocket = markers[i];
                if (candSocket == null || candSocket.left == null || candSocket.right == null) continue;
                foundValidSocket = true;

                if (candSocket.type != target.type) continue;
                foundMatchingType = true;

                if (!IsWidthCompatible(target.width, candSocket.WidthWorld, room)) continue;
                foundMatchingWidth = true;

                Vector3 a = candSocket.ForwardWorld;
                Vector3 b = -target.forward;

                a.y = 0f;
                b.y = 0f;

                if (a.sqrMagnitude < 0.0001f || b.sqrMagnitude < 0.0001f) continue;

                a.Normalize();
                b.Normalize();
                foundValidDirection = true;

                float yaw = Vector3.SignedAngle(a, b, Vector3.up);
                if (room.allowRotation)
                {
                    go.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * go.transform.rotation;
                }
                else if (Vector3.Dot(a, b) < 0.999f)
                {
                    continue;
                }

                Vector3 candCenter = candSocket.CenterWorld;
                go.transform.position += (target.center - candCenter);

                go.SetActive(true);
                Physics.SyncTransforms();

                Vector3 center = OverlapChecker.TryGetBoundsCenter(go, out var boundsCenter) ? boundsCenter : go.transform.position;

                if (placementCenterValidator != null && !placementCenterValidator(center))
                {
                    rejectedByConstraint = true;
                    go.SetActive(false);
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    continue;
                }

                bool overlaps = OverlapChecker.Overlaps(go, overlapPadding, extraOverlapPadding, overlapMaskToUse, ignoreRoot: null);

                if (log)
                {
                    Debug.Log($"TryAttach {room.id} via candSocket={candSocket.name} to target={target.marker.name} | " + $"widthA={target.width:F2} widthB={candSocket.WidthWorld:F2} | overlaps={overlaps}");
                }

                if (!overlaps)
                {
                    go.name = room.id;

                    placedRoom = new PlacedRoom(room, go);
                    placedRoom.connectedSocketInstanceIds.Add(candSocket.GetInstanceID());
                    target.owner.connectedSocketInstanceIds.Add(target.marker.GetInstanceID());

                    return true;
                }

                rejectedByOverlap = true;
                go.SetActive(false);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            UnityEngine.Object.DestroyImmediate(go);
            if (!foundValidSocket) failureReason = PlacementFailureReason.NoValidCandidateSockets;
            else if (!foundMatchingType) failureReason = PlacementFailureReason.SocketTypeMismatch;
            else if (!foundMatchingWidth) failureReason = PlacementFailureReason.SocketWidthMismatch;
            else if (!foundValidDirection) failureReason = PlacementFailureReason.InvalidSocketDirection;
            else if (rejectedByOverlap) failureReason = PlacementFailureReason.Overlap;
            else if (rejectedByConstraint) failureReason = PlacementFailureReason.PlacementConstraintRejected;
            else failureReason = PlacementFailureReason.InvalidSocketDirection;

            return false;
        }

        public bool TryPlaceWallCapFallback(
            OpenSocket target,
            RoomDefinition wallCapRoom,
            float capExtraPadding,
            LayerMask roomOverlapMask,
            bool preventCapOverlappingCaps,
            LayerMask capOverlapMask,
            out WallCapFailureReason failureReason,
            out Collider blockingCollider)
        {
            failureReason = WallCapFailureReason.None;
            blockingCollider = null;

            if (wallCapRoom == null || wallCapRoom.prefab == null)
            {
                failureReason = WallCapFailureReason.MissingWallCapPrefab;
                return false;
            }

            if (target.marker == null || target.owner == null || target.owner.root == null)
            {
                failureReason = WallCapFailureReason.MissingTargetSocket;
                return false;
            }

            var go = UnityEngine.Object.Instantiate(wallCapRoom.prefab, Vector3.zero, Quaternion.identity, parent);
            go.SetActive(false);

            Vector3 capForward = target.forward;
            capForward.y = 0f;
            if (capForward.sqrMagnitude < 0.0001f)
            {
                UnityEngine.Object.DestroyImmediate(go);
                failureReason = WallCapFailureReason.InvalidSocketDirection;
                return false;
            }

            capForward.Normalize();
            float cardinalYaw = Mathf.Abs(capForward.x) > Mathf.Abs(capForward.z) ? 90f : 0f;
            Vector3 desiredSocketPosition = target.center - capForward * wallCapInset;

            go.transform.SetPositionAndRotation(
                desiredSocketPosition,
                Quaternion.Euler(0f, cardinalYaw, 0f));
            if (TryGetBoundsWorldGeometry(
                    go,
                    out Vector3 currentBoundsCenter,
                    out float boundsHalfHeight))
            {
                Vector3 desiredBoundsCenter = desiredSocketPosition;
                desiredBoundsCenter.y += boundsHalfHeight;
                go.transform.position += desiredBoundsCenter - currentBoundsCenter;
            }

            go.SetActive(true);
            Physics.SyncTransforms();

            LayerMask mask = roomOverlapMask;
            if (preventCapOverlappingCaps) mask |= capOverlapMask;

            bool overlaps = OverlapChecker.Overlaps(
                go,
                overlapPadding,
                capExtraPadding,
                mask,
                out blockingCollider,
                ignoreRoot: target.owner.root.transform);

            if (log)
            {
                string boundsInfo = TryGetBoundsWorldGeometry(
                    go,
                    out Vector3 placedBoundsCenter,
                    out float placedBoundsHalfHeight)
                    ? $" boundsBottom={placedBoundsCenter.y - placedBoundsHalfHeight:F2}"
                    : string.Empty;

                Debug.Log(
                    $"TryWallCap {wallCapRoom.id} at {target.marker.name} " +
                    $"position={go.transform.position:F2} yaw={cardinalYaw:F0} forward={capForward:F2} " +
                    $"socketY={target.center.y:F2}{boundsInfo} overlaps={overlaps}");
            }

            if (overlaps)
            {
                UnityEngine.Object.DestroyImmediate(go);
                failureReason = WallCapFailureReason.Overlap;
                return false;
            }

            go.name = $"CAP_{wallCapRoom.id}";
            return true;
        }

        private static bool TryGetBoundsWorldGeometry(
            GameObject candidate,
            out Vector3 center,
            out float halfHeight)
        {
            center = candidate.transform.position;
            halfHeight = 0f;

            Transform boundsTransform = candidate.transform.Find("Bounds");
            if (boundsTransform == null) return false;

            BoxCollider bounds = boundsTransform.GetComponent<BoxCollider>();
            if (bounds == null || !bounds.enabled) return false;

            center = bounds.transform.TransformPoint(bounds.center);

            Vector3 localHalf = bounds.size * 0.5f;
            Vector3 worldX = bounds.transform.TransformVector(localHalf.x, 0f, 0f);
            Vector3 worldY = bounds.transform.TransformVector(0f, localHalf.y, 0f);
            Vector3 worldZ = bounds.transform.TransformVector(0f, 0f, localHalf.z);
            halfHeight = Mathf.Abs(worldX.y) + Mathf.Abs(worldY.y) + Mathf.Abs(worldZ.y);
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
