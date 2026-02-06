using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovementCC : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float groundedStickForce = -2f;
    [SerializeField] private float terminalVelocity = -50f;

    [Header("Movement")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private Transform cameraTransform;

    [Header("Aim / Rotation")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private LayerMask aimGroundMask;
    [SerializeField] private float rotateSpeed = 20f;
    [SerializeField] private bool rotateVisualsOnly = false;
    [SerializeField] private Transform visual;
    [SerializeField] private float visualTurnSpeed = 20f;

    [Header("Dodge / Dash")]
    [SerializeField] private InputActionReference dodgeAction;
    [SerializeField] private float dodgeSpeed = 12f;
    [SerializeField] private float dodgeDuration = 0.18f;
    [SerializeField] private float dodgeCooldown = 0.35f;
    [SerializeField] private bool lockMovementWhileDodging = true;
    [SerializeField] private bool dodgeUsesAimDirection = true;
    
    private CharacterController cc;
    private float verticalVelocity;
    private Vector2 moveInput;
    private float dodgeTimer;
    private float dodgeCooldownTimer;
    private Vector3 dodgeDir;
    private Vector3 lastMoveDir = Vector3.forward;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (aimCamera == null) aimCamera = Camera.main;
    }

    private void Update()
    {
        Cooldowns();
        ReadInput();
        ApplyGravity();
        MoveCC();
    }

    private void ApplyGravity()
    {
        if (cc.isGrounded)
        { 
            if (verticalVelocity < 0f) verticalVelocity = groundedStickForce;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
            if (verticalVelocity < terminalVelocity) verticalVelocity = terminalVelocity;
        }
    }

    private void MoveCC()
    {
        Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);

        if (cameraTransform != null)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            move = (right * move.x + forward * move.z);
        }

        if (move.sqrMagnitude > 1f) move.Normalize();

        Vector3 velocity = Vector3.zero;

        bool isDodging = dodgeTimer > 0f;

        if (!isDodging || !lockMovementWhileDodging)
        {
            velocity += move * walkSpeed;
        }

        if (isDodging)
        {
            velocity += dodgeDir * dodgeSpeed;
        }

        velocity.y = verticalVelocity;
        cc.Move(velocity * Time.deltaTime);

        if (rotateVisualsOnly) RotateVisualsToMouse();
        else RotateToMouse();
   
        if (move.sqrMagnitude > 0.0001f) lastMoveDir = move.normalized;
    }

    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (dodgeAction != null) dodgeAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (dodgeAction != null) dodgeAction.action.Disable();
    }

    private void ReadInput()
    {
        moveInput = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        if (dodgeAction != null && dodgeAction.action.WasPressedThisFrame()) TryDodge();
    }

    private void Cooldowns()
    {
        if (dodgeCooldownTimer > 0f) dodgeCooldownTimer -= Time.deltaTime;
        if (dodgeTimer > 0f) dodgeTimer -= Time.deltaTime;
    }

    private void RotateToMouse()
    {
        if (aimCamera == null) return;

        Ray ray = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, aimGroundMask, QueryTriggerInteraction.Ignore)) return;

        Vector3 look = hit.point - transform.position;
        look.y = 0f;

        if (look.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(look.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }

    private void RotateVisualsToMouse()
    {
        if (visual != null)
        {
            Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y);

            if (cameraTransform != null)
            {
                Vector3 forward = cameraTransform.forward; forward.y = 0f; forward.Normalize();
                Vector3 right = cameraTransform.right; right.y = 0f; right.Normalize();
                dir = (right * dir.x + forward * dir.z);
            }

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
                visual.rotation = Quaternion.Slerp(visual.rotation, target * Quaternion.Euler(0f, 180f, 0f), visualTurnSpeed * Time.deltaTime);
            }
        }
    }

    private void TryDodge()
    {
        if (dodgeCooldownTimer > 0f) return;
        if (dodgeTimer > 0f) return;

        if (dodgeUsesAimDirection && TryGetAimDirection(out Vector3 dir))
        {
            dodgeDir = dir;
        }
        else
        {
            dodgeDir = (lastMoveDir.sqrMagnitude > 0.0001f) ? lastMoveDir : transform.forward;
            dodgeDir.y = 0f;
            if (dodgeDir.sqrMagnitude < 0.0001f) return;
            dodgeDir.Normalize();
        }

        dodgeTimer = dodgeDuration;
        dodgeCooldownTimer = dodgeCooldown;
    }

    private bool TryGetAimDirection(out Vector3 dir)
    {
        dir = Vector3.zero;
        if (aimCamera == null) return false;

        Ray ray = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, aimGroundMask, QueryTriggerInteraction.Ignore)) return false;

        dir = hit.point - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) return false;

        dir.Normalize();
        return true;
    }
}
