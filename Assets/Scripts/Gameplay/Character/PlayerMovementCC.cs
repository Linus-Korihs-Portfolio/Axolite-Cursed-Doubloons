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

    private CharacterController cc;
    private float verticalVelocity;
    private Vector2 moveInput;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
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
}
