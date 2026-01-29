using UnityEngine;

namespace PCG.RoomAssembler.Logic
{
    public static class OverlapChecker
    {
        public static bool Overlaps(
            GameObject candidate,
            float overlapPadding,
            float extraPadding,
            LayerMask maskToUse,
            Transform ignoreRoot = null)
        {
            float pad = overlapPadding + extraPadding;

            // Prefer explicit Bounds collider
            var boundsTf = candidate.transform.Find("Bounds");
            if (boundsTf != null)
            {
                var bc = boundsTf.GetComponent<BoxCollider>();
                if (bc != null && bc.enabled)
                {
                    Vector3 center = bc.transform.TransformPoint(bc.center);
                    Vector3 half = Vector3.Scale(bc.size * 0.5f, bc.transform.lossyScale) - Vector3.one * pad;

                    half.x = Mathf.Max(0.001f, half.x);
                    half.y = Mathf.Max(0.001f, half.y);
                    half.z = Mathf.Max(0.001f, half.z);

                    Quaternion rot = bc.transform.rotation;

                    var hits = Physics.OverlapBox(center, half, rot, maskToUse, QueryTriggerInteraction.Collide);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var hit = hits[i];
                        if (hit == null) continue;

                        if (hit.transform.IsChildOf(candidate.transform)) continue;
                        if (ignoreRoot != null && hit.transform.IsChildOf(ignoreRoot)) continue;

                        return true;
                    }
                    return false;
                }
            }

            // Fallback: all colliders
            var cols = candidate.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null || !c.enabled) continue;

                var b = c.bounds;
                Vector3 center = b.center;
                Vector3 half = b.extents - Vector3.one * pad;

                half.x = Mathf.Max(0.001f, half.x);
                half.y = Mathf.Max(0.001f, half.y);
                half.z = Mathf.Max(0.001f, half.z);

                var hits = Physics.OverlapBox(center, half, Quaternion.identity, maskToUse, QueryTriggerInteraction.Collide);
                for (int h = 0; h < hits.Length; h++)
                {
                    var hit = hits[h];
                    if (hit == null) continue;

                    if (hit.transform.IsChildOf(candidate.transform)) continue;
                    if (ignoreRoot != null && hit.transform.IsChildOf(ignoreRoot)) continue;

                    return true;
                }
            }

            return false;
        }
    }
}
