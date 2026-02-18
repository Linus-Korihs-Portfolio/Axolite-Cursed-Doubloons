using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody))]
public class CameraOcclusionTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform target;

    [Header("Occluders")]
    [SerializeField] private LayerMask occluderMask;
    [SerializeField] private bool makeTransparent = true;
    [SerializeField, Range(0.05f, 1f)] private float transparentAlpha = 0.25f;

    [Header("Trigger Shape")]
    [SerializeField] private float radius = 0.35f;
    [SerializeField] private float paddingFromCamera = 0.2f;
    [SerializeField] private float paddingFromTarget = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool debugEnabled = false;
    [SerializeField] private bool drawGizmos = true;

    private CapsuleCollider capsule;
    private Rigidbody rb;

    private readonly HashSet<Renderer> activeOccluders = new();
    private readonly Dictionary<Renderer, bool> originalEnabled = new();
    private MaterialPropertyBlock mpb;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private Vector3 gizmoStart, gizmoEnd;

    [SerializeField] private int staleFramesToRestore = 2; // failsafe gegen verpasste Exit-events

    private readonly Dictionary<Renderer, int> overlapCounts = new();
    private readonly Dictionary<Renderer, int> lastSeenFrame = new();


    private void Awake()
    {
        mpb = new MaterialPropertyBlock();

        capsule = GetComponent<CapsuleCollider>();
        rb = GetComponent<Rigidbody>();

        capsule.isTrigger = true;
        capsule.direction = 2;
        capsule.radius = radius;

        rb.isKinematic = true;
        rb.useGravity = false;

        if (!cameraTransform) cameraTransform = Camera.main ? Camera.main.transform : null;

        if (debugEnabled)
        {
            Debug.Log($"[CameraOcclusionTrigger] Awake on '{name}'. " + $"cameraTransform={(cameraTransform ? cameraTransform.name : "NULL")} target={(target ? target.name : "NULL")}");
        }
    }

    private void LateUpdate()
    {
        if (!cameraTransform || !target) return;

        Vector3 camPos = cameraTransform.position;
        Vector3 targetPos = target.position;

        Vector3 dir = targetPos - camPos;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return;

        Vector3 dirN = dir / dist;

        Vector3 start = camPos + dirN * paddingFromCamera;
        Vector3 end = targetPos - dirN * paddingFromTarget;

        float paddedDist = Vector3.Distance(start, end);
        if (paddedDist <= 0.001f) return;

        gizmoStart = start;
        gizmoEnd = end;

        transform.position = (start + end) * 0.5f;
        transform.rotation = Quaternion.LookRotation((end - start).normalized, Vector3.up);

        capsule.radius = radius;
        capsule.height = Mathf.Max(paddedDist + 2f * radius, 2f * radius);
        CleanupStaleOccluders();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsInMask(other.gameObject.layer, occluderMask)) return;

        var rend = other.GetComponentInParent<Renderer>();
        if (!rend)
        {
            if (debugEnabled) Debug.Log($"[CameraOcclusionTrigger] ENTER '{other.name}' but no Renderer found.");
            return;
        }

        MarkSeen(rend);

        overlapCounts.TryGetValue(rend, out int count);
        count++;
        overlapCounts[rend] = count;

        if (debugEnabled) Debug.Log($"[CameraOcclusionTrigger] ENTER '{other.name}' -> '{rend.name}' count={count}");

        if (count == 1) ApplyOcclusion(rend);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsInMask(other.gameObject.layer, occluderMask)) return;

        var rend = other.GetComponentInParent<Renderer>();
        if (!rend) return;

        MarkSeen(rend);

        if (!activeOccluders.Contains(rend))
        {
            overlapCounts.TryGetValue(rend, out int count);
            overlapCounts[rend] = Mathf.Max(1, count);
            ApplyOcclusion(rend);

            if (debugEnabled) Debug.Log($"[CameraOcclusionTrigger] STAY '{other.name}' -> '{rend.name}' count={overlapCounts[rend]}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsInMask(other.gameObject.layer, occluderMask)) return;

        var rend = other.GetComponentInParent<Renderer>();
        if (!rend)
        {
            if (debugEnabled) Debug.Log($"[CameraOcclusionTrigger] EXIT '{other.name}' but no Renderer found.");
            return;
        }

        overlapCounts.TryGetValue(rend, out int count);
        count = Mathf.Max(0, count - 1);

        if (count == 0)
        {
            overlapCounts.Remove(rend);
            lastSeenFrame.Remove(rend);
            RestoreOne(rend);

            if (debugEnabled) Debug.Log($"[CameraOcclusionTrigger] EXIT '{other.name}' -> '{rend.name}' restored (count=0)");
        }
        else
        {
            overlapCounts[rend] = count;

            if (debugEnabled) Debug.Log($"[CameraOcclusionTrigger] EXIT '{other.name}' -> '{rend.name}' count={count}");
        }
    }

    private void OnDisable()
    {
        if (debugEnabled)
            Debug.Log($"[CameraOcclusionTrigger] OnDisable - restoring {activeOccluders.Count} occluders.");

        foreach (var r in new List<Renderer>(activeOccluders))
            RestoreOne(r);

        activeOccluders.Clear();
        overlapCounts.Clear();
        lastSeenFrame.Clear();
    }

    private void ApplyOcclusion(Renderer rend)
    {
        if (!activeOccluders.Add(rend))
        {
            if (debugEnabled) Debug.Log($"[CameraOcclusionTrigger] Apply skipped (already active) -> '{rend.name}'");
            return;
        }

        if (!originalEnabled.ContainsKey(rend)) originalEnabled[rend] = rend.enabled;

        if (!makeTransparent)
        {
            rend.enabled = false;

            if (debugEnabled) Debug.Log($"[CameraOcclusionTrigger] HIDE -> '{rend.name}' (renderer.enabled=false)");
                Debug.Log($"[CameraOcclusionTrigger] HIDE -> '{rend.name}' (renderer.enabled=false)");

            return;
        }

        if (!rend.sharedMaterial)
        {
            if (debugEnabled) Debug.LogWarning($"[CameraOcclusionTrigger] '{rend.name}' has no sharedMaterial. Falling back to HIDE.");
            rend.enabled = false;
            return;
        }

        rend.GetPropertyBlock(mpb);

        bool applied = false;

        if (rend.sharedMaterial.HasProperty(BaseColorId))
        {
            Color c = rend.sharedMaterial.GetColor(BaseColorId);
            c.a = transparentAlpha;
            mpb.SetColor(BaseColorId, c);
            applied = true;
        }
        else if (rend.sharedMaterial.HasProperty(ColorId))
        {
            Color c = rend.sharedMaterial.GetColor(ColorId);
            c.a = transparentAlpha;
            mpb.SetColor(ColorId, c);
            applied = true;
        }

        if (!applied)
        {
            if (debugEnabled) Debug.LogWarning($"[CameraOcclusionTrigger] Shader on '{rend.name}' doesn't have _BaseColor/_Color. Falling back to HIDE.");
            rend.enabled = false;
            return;
        }

        rend.SetPropertyBlock(mpb);

        if (debugEnabled)
        {
            Debug.Log($"[CameraOcclusionTrigger] FADE -> '{rend.name}' alpha={transparentAlpha} " + $"(mat='{rend.sharedMaterial.name}', shader='{rend.sharedMaterial.shader.name}')");
        }
    }

    private void RestoreOne(Renderer rend)
    {
        if (!rend) return;
        if (!activeOccluders.Remove(rend))
        {
            if (debugEnabled) Debug.Log($"[CameraOcclusionTrigger] Restore skipped (not active) -> '{rend.name}'");
            return;
        }

        if (originalEnabled.TryGetValue(rend, out bool wasEnabled)) rend.enabled = wasEnabled;

        rend.SetPropertyBlock(null);

        if (debugEnabled) Debug.Log($"[CameraOcclusionTrigger] RESTORE -> '{rend.name}' (enabled={rend.enabled})");
    }
    private static bool IsInMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;

    private void OnDrawGizmos()
    {
        if (!debugEnabled || !drawGizmos) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(gizmoStart, gizmoEnd);

        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawWireSphere(gizmoStart, radius);
        Gizmos.DrawWireSphere(gizmoEnd, radius);
    }

    private void MarkSeen(Renderer rend)
    {
        lastSeenFrame[rend] = Time.frameCount;
    }

    private void CleanupStaleOccluders()
    {
        var toRestore = new List<Renderer>();

        foreach (var rend in activeOccluders)
        {
            if (!rend) { toRestore.Add(rend); continue; }

            if (!lastSeenFrame.TryGetValue(rend, out int last))
            {
                toRestore.Add(rend);
                continue;
            }

            if (Time.frameCount - last > staleFramesToRestore) toRestore.Add(rend);
        }

        for (int i = 0; i < toRestore.Count; i++)
        {
            var r = toRestore[i];
            if (!r) continue;

            overlapCounts.Remove(r);
            lastSeenFrame.Remove(r);
            RestoreOne(r);

            if (debugEnabled) Debug.Log($"[CameraOcclusionTrigger] FAILSAFE RESTORE -> '{r.name}' (stale)");
        }
    }

}
