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
    [SerializeField] private float activeMoveDeadzone = 0.35f;

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

        Vector3 dir = Vector3.zero;

        if (movement.moveInput.magnitude >= activeMoveDeadzone) dir = movement.LastMoveDir;
        else
        {
            if (aim != null && aim.FacingDirection.sqrMagnitude > 0.0001f) dir = aim.FacingDirection;
            else dir = movement.LastMoveDir;
        }

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        movement.AddExternalVelocity(dir * dodgeSpeed, dodgeDuration);
        cooldownTimer = dodgeCooldown;
    }

}
