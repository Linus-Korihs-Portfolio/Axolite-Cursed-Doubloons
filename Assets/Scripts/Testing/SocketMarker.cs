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
            // derive direction from name
            if (name.Contains("North")) return transform.parent.forward;
            if (name.Contains("South")) return -transform.parent.forward;
            if (name.Contains("East"))  return transform.parent.right;
            if (name.Contains("West"))  return -transform.parent.right;

            // fallback
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
