using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovementCC))]
public class PlayerPunch : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference punchAction;

    [Header("Punch")]
    [SerializeField] private float cooldown = 0.25f;
    [SerializeField] private float range = 1.1f;
    [SerializeField] private float radius = 0.6f;
    [SerializeField] private LayerMask hitMask;
    //[SerializeField] private int damage = 1;
    [SerializeField] private float hitboxBufferUpwards = 0.5f;

    [Header("Knockback (optional)")]
    [SerializeField] private float knockbackForce = 6f;
    [SerializeField] private float upwardKnock = 0f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private PlayerMovementCC movement;
    private PlayerAim aim;

    private float cooldownTimer;

    private void Awake()
    {
        movement = GetComponent<PlayerMovementCC>();
        aim = GetComponent<PlayerAim>();
    }

    private void OnEnable()
    {
        if (punchAction != null) punchAction.action.Enable();
    }

    private void OnDisable()
    {
        if (punchAction != null) punchAction.action.Disable();
    }

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        if (punchAction != null && punchAction.action.WasPressedThisFrame()) TryPunch();
    }

    private void TryPunch()
    {
        if (cooldownTimer > 0f) return;

        Vector3 dir = GetPunchDirection();
        Vector3 center = transform.position + Vector3.up * hitboxBufferUpwards + dir * range;

        Collider[] hits = Physics.OverlapSphere(center, radius, hitMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            Rigidbody rb = col.attachedRigidbody != null ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
                Vector3 kb = dir * knockbackForce;
                if (upwardKnock != 0f) kb.y += upwardKnock;

                rb.AddForce(kb, ForceMode.VelocityChange);
            }
        }
        cooldownTimer = cooldown;
    }

    private Vector3 GetPunchDirection()
    {
        if (aim != null && aim.FacingDirection.sqrMagnitude > 0.0001f) return aim.FacingDirection.normalized;
        if (movement != null && movement.LastMoveDir.sqrMagnitude > 0.0001f) return movement.LastMoveDir.normalized;

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Vector3 dir = GetPunchDirection();
        Vector3 center = transform.position + Vector3.up * hitboxBufferUpwards + dir * range;

        Gizmos.DrawWireSphere(center, radius);
        Gizmos.DrawLine(transform.position + Vector3.up * hitboxBufferUpwards, center);
    }

}
