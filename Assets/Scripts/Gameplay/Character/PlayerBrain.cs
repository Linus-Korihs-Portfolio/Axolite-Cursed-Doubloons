using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBrain : MonoBehaviour
{
    [SerializeField] private PlayerConfig config;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference dodgeAction;
    [SerializeField] private InputActionReference punchAction;

    [SerializeField] private PlayerMovementCC movement;
    [SerializeField] private PlayerAim aim;
    [SerializeField] private PlayerDodge dodge;
    [SerializeField] private PlayerPunch punch;

    private void Awake()
    {
        if (movement == null) movement = GetComponentInChildren<PlayerMovementCC>();
        if (aim == null) aim = GetComponentInChildren<PlayerAim>();
        if (dodge == null) dodge = GetComponentInChildren<PlayerDodge>();
        if (punch == null) punch = GetComponentInChildren<PlayerPunch>();

        if (movement == null) Debug.LogError($"{name}: PlayerMovementCC is missing", this);
        if (aim == null) Debug.LogWarning($"{name}: PlayerAim is missing (Fallback to LastMoveDir/forward)", this);
        if (dodge == null) Debug.LogWarning($"{name}: PlayerDodge is missing", this);
        if (punch == null) Debug.LogWarning($"{name}: PlayerPunch is missing", this);

        if (config == null)
        {
            Debug.LogError($"{name}: PlayerConfig is missing", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (dodgeAction != null) dodgeAction.action.Enable();
        if (punchAction != null) punchAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (dodgeAction != null) dodgeAction.action.Disable();
        if (punchAction != null) punchAction.action.Disable();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 1) Tick modules
        if (dodge != null) dodge.Tick(dt);
        if (punch != null) punch.Tick(dt);

        // 2) Read input
        Vector2 move = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        movement.SetMoveInput(move);

        // 3) Apply rules -> movement lock & speed
        bool isDodging = dodge != null && dodge.IsDodging;
        bool isPunching = punch != null && punch.IsPunching;

        movement.MovementLocked = isDodging; // Rule: During Dodge -> Movement Locked
        movement.SpeedMultiplier = isPunching ? config.punchSlowMultiplier : 1f; // During Punch -> Speed Multiplier

        // 4) Actions with rule gates
        // Rule: During Dodge -> No Punch
        if (!isDodging && punchAction != null && punchAction.action.WasPressedThisFrame())
        {
            Vector3 dir = GetFacingDir();
            punch.TryPunch(dir);
        }

        // Rule: During Punch -> No Dodge
        if (!isPunching && dodgeAction != null && dodgeAction.action.WasPressedThisFrame())
        {
            Vector3 dir = GetDodgeDir(move);
            dodge.TryDodge(dir);
        }
    }

    private Vector3 GetFacingDir()
    {
        if (aim != null && aim.FacingDirection.sqrMagnitude > 0.0001f) return aim.FacingDirection;
        if (movement.LastMoveDir.sqrMagnitude > 0.0001f) return movement.LastMoveDir;
        return transform.forward;
    }

    private Vector3 GetDodgeDir(Vector2 moveInput)
    {
        if (moveInput.magnitude >= config.activeMoveDeadzone) return movement.LastMoveDir;
        return GetFacingDir();
    }
}
