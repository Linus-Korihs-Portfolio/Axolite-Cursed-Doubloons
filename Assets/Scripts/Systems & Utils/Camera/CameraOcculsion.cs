using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody))]
public class CameraOcclusionTrigger : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private CameraCMSettings settings;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform target;

    private CapsuleCollider capsule;
    private Rigidbody rb;

    private readonly HashSet<Renderer> activeOccluders = new();
    private readonly Dictionary<Renderer, bool> originalEnabled = new();
    private MaterialPropertyBlock mpb;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private Vector3 gizmoStart, gizmoEnd;

    private readonly Dictionary<Renderer, int> overlapCounts = new();
    private readonly Dictionary<Renderer, int> lastSeenFrame = new();

    private LayerMask OccluderMask => settings.occluderMask;
    private bool MakeTransparent => settings.makeTransparent;
    private float TransparentAlpha => settings.transparentAlpha;
    private float Radius => settings.occlusionRadius;
    private float PaddingFromCamera => settings.paddingFromCamera;
    private float PaddingFromTarget => settings.paddingFromTarget;
    private bool DebugEnabled => settings.debugEnabled;
    private bool DrawGizmos => settings.drawGizmos;
    private int StaleFramesToRestore => settings.staleFramesToRestore;

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();

        capsule = GetComponent<CapsuleCollider>();
        rb = GetComponent<Rigidbody>();

        capsule.isTrigger = true;
        capsule.direction = 2;
        capsule.radius = Radius;

        rb.isKinematic = true;
        rb.useGravity = false;

        if (!cameraTransform) cameraTransform = Camera.main ? Camera.main.transform : null;

        if (DebugEnabled)
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

        Vector3 start = camPos + dirN * PaddingFromCamera;
        Vector3 end = targetPos - dirN * PaddingFromTarget;

        float paddedDist = Vector3.Distance(start, end);
        if (paddedDist <= 0.001f) return;

        gizmoStart = start;
        gizmoEnd = end;

        transform.position = (start + end) * 0.5f;
        transform.rotation = Quaternion.LookRotation((end - start).normalized, Vector3.up);

        capsule.radius = Radius;
        capsule.height = Mathf.Max(paddedDist + 2f * Radius, 2f * Radius);
        CleanupStaleOccluders();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsInMask(other.gameObject.layer, OccluderMask)) return;

        var rend = other.GetComponentInParent<Renderer>();
        if (!rend)
        {
            if (DebugEnabled) Debug.Log($"[CameraOcclusionTrigger] ENTER '{other.name}' but no Renderer found.");
            return;
        }

        MarkSeen(rend);

        overlapCounts.TryGetValue(rend, out int count);
        count++;
        overlapCounts[rend] = count;

        if (DebugEnabled) Debug.Log($"[CameraOcclusionTrigger] ENTER '{other.name}' -> '{rend.name}' count={count}");

        if (count == 1) ApplyOcclusion(rend);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsInMask(other.gameObject.layer, OccluderMask)) return;

        var rend = other.GetComponentInParent<Renderer>();
        if (!rend) return;

        MarkSeen(rend);

        if (!activeOccluders.Contains(rend))
        {
            overlapCounts.TryGetValue(rend, out int count);
            overlapCounts[rend] = Mathf.Max(1, count);
            ApplyOcclusion(rend);

            if (DebugEnabled) Debug.Log($"[CameraOcclusionTrigger] STAY '{other.name}' -> '{rend.name}' count={overlapCounts[rend]}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsInMask(other.gameObject.layer, OccluderMask)) return;

        var rend = other.GetComponentInParent<Renderer>();
        if (!rend)
        {
            if (DebugEnabled) Debug.Log($"[CameraOcclusionTrigger] EXIT '{other.name}' but no Renderer found.");
            return;
        }

        overlapCounts.TryGetValue(rend, out int count);
        count = Mathf.Max(0, count - 1);

        if (count == 0)
        {
            overlapCounts.Remove(rend);
            lastSeenFrame.Remove(rend);
            RestoreOne(rend);

            if (DebugEnabled) Debug.Log($"[CameraOcclusionTrigger] EXIT '{other.name}' -> '{rend.name}' restored (count=0)");
        }
        else
        {
            overlapCounts[rend] = count;

            if (DebugEnabled) Debug.Log($"[CameraOcclusionTrigger] EXIT '{other.name}' -> '{rend.name}' count={count}");
        }
    }

    private void OnDisable()
    {
        if (DebugEnabled)
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
            if (DebugEnabled) Debug.Log($"[CameraOcclusionTrigger] Apply skipped (already active) -> '{rend.name}'");
            return;
        }

        if (!originalEnabled.ContainsKey(rend)) originalEnabled[rend] = rend.enabled;

        if (!MakeTransparent)
        {
            rend.enabled = false;

            if (DebugEnabled) Debug.Log($"[CameraOcclusionTrigger] HIDE -> '{rend.name}' (renderer.enabled=false)");
                Debug.Log($"[CameraOcclusionTrigger] HIDE -> '{rend.name}' (renderer.enabled=false)");

            return;
        }

        if (!rend.sharedMaterial)
        {
            if (DebugEnabled) Debug.LogWarning($"[CameraOcclusionTrigger] '{rend.name}' has no sharedMaterial. Falling back to HIDE.");
            rend.enabled = false;
            return;
        }

        rend.GetPropertyBlock(mpb);

        bool applied = false;

        if (rend.sharedMaterial.HasProperty(BaseColorId))
        {
            Color c = rend.sharedMaterial.GetColor(BaseColorId);
            c.a = TransparentAlpha;
            mpb.SetColor(BaseColorId, c);
            applied = true;
        }
        else if (rend.sharedMaterial.HasProperty(ColorId))
        {
            Color c = rend.sharedMaterial.GetColor(ColorId);
            c.a = TransparentAlpha;
            mpb.SetColor(ColorId, c);
            applied = true;
        }

        if (!applied)
        {
            if (DebugEnabled) Debug.LogWarning($"[CameraOcclusionTrigger] Shader on '{rend.name}' doesn't have _BaseColor/_Color. Falling back to HIDE.");
            rend.enabled = false;
            return;
        }

        rend.SetPropertyBlock(mpb);

        if (DebugEnabled)
        {
            Debug.Log($"[CameraOcclusionTrigger] FADE -> '{rend.name}' alpha={TransparentAlpha} " + $"(mat='{rend.sharedMaterial.name}', shader='{rend.sharedMaterial.shader.name}')");
        }
    }

    private void RestoreOne(Renderer rend)
    {
        if (!rend) return;
        if (!activeOccluders.Remove(rend))
        {
            if (DebugEnabled) Debug.Log($"[CameraOcclusionTrigger] Restore skipped (not active) -> '{rend.name}'");
            return;
        }

        if (originalEnabled.TryGetValue(rend, out bool wasEnabled)) rend.enabled = wasEnabled;

        rend.SetPropertyBlock(null);

        if (DebugEnabled) Debug.Log($"[CameraOcclusionTrigger] RESTORE -> '{rend.name}' (enabled={rend.enabled})");
    }
    private static bool IsInMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;

    private void OnDrawGizmos()
    {
        if (!DrawGizmos) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(gizmoStart, gizmoEnd);

        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawWireSphere(gizmoStart, Radius);
        Gizmos.DrawWireSphere(gizmoEnd, Radius);
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

            if (Time.frameCount - last > StaleFramesToRestore) toRestore.Add(rend);
        }

        for (int i = 0; i < toRestore.Count; i++)
        {
            var r = toRestore[i];
            if (!r) continue;

            overlapCounts.Remove(r);
            lastSeenFrame.Remove(r);
            RestoreOne(r);

            if (DebugEnabled) Debug.Log($"[CameraOcclusionTrigger] FAILSAFE RESTORE -> '{r.name}' (stale)");
        }
    }

}
