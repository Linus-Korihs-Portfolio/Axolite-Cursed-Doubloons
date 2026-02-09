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
    [SerializeField] private int damage = 1;

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
        Vector3 center = transform.position + dir * range;

        Collider[] hits = Physics.OverlapSphere(center, radius, hitMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            Rigidbody rb = col.attachedRigidbody;
            if (rb != null)
            {
                Vector3 kb = dir * knockbackForce;
                if (upwardKnock != 0f) kb.y += upwardKnock;

                rb.AddForce(kb, ForceMode.Impulse);
            }
        }

        cooldownTimer = cooldown;
    }

    private Vector3 GetPunchDirection()
    {
        if (aim != null && aim.AimDirection.sqrMagnitude > 0.0001f) return aim.AimDirection.normalized;
        if (movement != null && movement.LastMoveDir.sqrMagnitude > 0.0001f) return movement.LastMoveDir.normalized;

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Vector3 dir = Vector3.forward;
        var a = GetComponent<PlayerAim>();
        var m = GetComponent<PlayerMovementCC>();

        if (a != null && a.AimDirection.sqrMagnitude > 0.0001f) dir = a.AimDirection;
        else if (m != null && m.LastMoveDir.sqrMagnitude > 0.0001f) dir = m.LastMoveDir;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        Vector3 center = transform.position + dir * range;

        Gizmos.DrawWireSphere(center, radius);
        Gizmos.DrawLine(transform.position, center);
    }
}
