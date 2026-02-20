using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovementCC))]
public class PlayerPunch : MonoBehaviour
{
    [SerializeField] private PlayerConfig config;
    [Header("Input")]
    [SerializeField] private InputActionReference punchAction;

    [Header("Punch")]
    private float Cooldown => config.punchCooldown;
    private float Range => config.punchRange;
    private float Radius => config.punchRadius;
    [SerializeField] private LayerMask hitMask;
    //private int Damage => config.damage;
    private float HitboxBufferUpwards => config.hitboxBufferUpwards;

    [Header("Knockback (optional)")]
    private float KnockbackForce => config.knockbackForce;
    private float UpwardKnock => config.upwardKnock;

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
        Vector3 center = transform.position + Vector3.up * HitboxBufferUpwards + dir * Range;

        Collider[] hits = Physics.OverlapSphere(center, Radius, hitMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            Rigidbody rb = col.attachedRigidbody != null ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
                Vector3 kb = dir * KnockbackForce;
                if (UpwardKnock != 0f) kb.y += UpwardKnock;

                rb.AddForce(kb, ForceMode.VelocityChange);
            }
        }
        cooldownTimer = Cooldown;
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
        Vector3 center = transform.position + Vector3.up * HitboxBufferUpwards + dir * Range;

        Gizmos.DrawWireSphere(center, Radius);
        Gizmos.DrawLine(transform.position + Vector3.up * HitboxBufferUpwards, center);
    }

}
