using UnityEngine;

public enum SocketType
{
    Corridor,
    Door,
    BigDoor
}

public class SocketMarker : MonoBehaviour
{
    [Header("Socket")]
    public string socketId = "Socket";
    public SocketType type = SocketType.Corridor;

    [Header("Gate Definition")]
    public Transform left;
    public Transform right;

    public Vector3 CenterWorld => (left.position + right.position) * 0.5f;
    public float WidthWorld => Vector3.Distance(left.position, right.position);

    // Forward points "out of the room" (important!)
    public Vector3 ForwardWorld
    {
        get
        {
            Transform roomRoot = transform.parent;
            if (roomRoot != null)
            {
                Vector3 roomCenterWorld = roomRoot.position;
                Transform boundsTransform = roomRoot.Find("Bounds");
                if (boundsTransform != null)
                {
                    BoxCollider bounds = boundsTransform.GetComponent<BoxCollider>();
                    if (bounds != null)
                    {
                        roomCenterWorld = bounds.transform.TransformPoint(bounds.center);
                    }
                }

                Vector3 localDelta =
                    roomRoot.InverseTransformPoint(CenterWorld) -
                    roomRoot.InverseTransformPoint(roomCenterWorld);

                if (Mathf.Abs(localDelta.x) > Mathf.Abs(localDelta.z) &&
                    Mathf.Abs(localDelta.x) > 0.0001f)
                {
                    return localDelta.x > 0f ? roomRoot.right : -roomRoot.right;
                }

                if (Mathf.Abs(localDelta.z) > 0.0001f)
                {
                    return localDelta.z > 0f ? roomRoot.forward : -roomRoot.forward;
                }
            }

            // Legacy fallback for sockets without a room Bounds object.
            Transform fallbackBasis = transform.parent != null ? transform.parent : transform;
            if (name.Contains("North")) return fallbackBasis.forward;
            if (name.Contains("South")) return -fallbackBasis.forward;
            if (name.Contains("East")) return fallbackBasis.right;
            if (name.Contains("West")) return -fallbackBasis.right;

            return transform.forward;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (left == null || right == null) return;

        Gizmos.DrawLine(left.position, right.position);
        var c = CenterWorld;
        Gizmos.DrawSphere(c, 0.05f);
        Gizmos.DrawRay(c, ForwardWorld * 0.3f);
    }
#endif
}
