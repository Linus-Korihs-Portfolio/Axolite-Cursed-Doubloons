using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovementCC))]
public class PlayerDodge : MonoBehaviour
{
    [Header("Dodge / Dash")]
    [SerializeField] private InputActionReference dodgeAction;
    [SerializeField] private float dodgeSpeed = 12f;
    [SerializeField] private float dodgeDuration = 0.18f;
    [SerializeField] private float dodgeCooldown = 0.35f;
    [SerializeField] private bool dodgeUsesAimDirection = true;

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
        if (dodgeAction != null) dodgeAction.action.Enable();
    }

    private void OnDisable()
    {
        if (dodgeAction != null) dodgeAction.action.Disable();
    }

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (dodgeAction != null && dodgeAction.action.WasPressedThisFrame()) TryDodge();
    }

    private void TryDodge()
    {
        if (cooldownTimer > 0f) return;

        Vector3 dir = movement.LastMoveDir;

        if (dodgeUsesAimDirection && aim != null && aim.AimDirection.sqrMagnitude > 0.0001f) dir = aim.AimDirection;

        movement.AddExternalVelocity(dir.normalized * dodgeSpeed, dodgeDuration);
        cooldownTimer = dodgeCooldown;
    }
}
