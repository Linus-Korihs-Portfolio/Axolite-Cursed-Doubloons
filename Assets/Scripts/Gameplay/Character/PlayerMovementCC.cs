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


    private CharacterController cc;
    private float verticalVelocity;
    private Vector2 moveInput;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (aimCamera == null) aimCamera = Camera.main;
    }

    private void Update()
    {
        ReadInput();
        ApplyGravity();
        MoveCC();

    }

    private void ApplyGravity()
    {
        if (cc.isGrounded)
        {
            // If the player is grounded, we want to apply a small downward force to keep them grounded, but not so much that it causes them to stick to slopes or small bumps.
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

        Vector3 velocity = move * walkSpeed;
        velocity.y = verticalVelocity;

        cc.Move(velocity * Time.deltaTime);

        if (rotateVisualsOnly) RotateVisualsToMouse();
        else RotateToMouse(); 
    }

    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
    }

    private void ReadInput()
    {
        moveInput = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
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
}
