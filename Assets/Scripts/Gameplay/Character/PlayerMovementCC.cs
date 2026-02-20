using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovementCC : MonoBehaviour
{
    [SerializeField] private PlayerConfig config;

    [Header("Physics")]
    private float Gravity => config.gravity;
    private float GroundedStickForce => config.groundedStickForce;
    private float TerminalVelocity => config.terminalVelocity;
    private float DirUpdateDeadzone => config.dirUpdateDeadzone;

    [Header("Movement")]
    private float WalkSpeed => config.walkSpeed;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] public Transform cameraTransform;

    public Vector3 LastMoveDir { get; private set; } = Vector3.forward;

    private CharacterController cc;
    private float verticalVelocity;
    public Vector2 moveInput;

    private Vector3 externalVelocity;
    private float externalTimer;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
    }

    private void Update()
    {
        ReadInput();
        ApplyGravity();
        TickExternal();
        Move();
    }

    private void ReadInput()
    {
        moveInput = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
    }

    private void ApplyGravity()
    {
        if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = GroundedStickForce; 
        else
        {
            verticalVelocity += Gravity * Time.deltaTime;
            if (verticalVelocity < TerminalVelocity) verticalVelocity = TerminalVelocity;
        }
    }

    private void TickExternal()
    {
        if (externalTimer > 0f) externalTimer -= Time.deltaTime;
        else externalVelocity = Vector3.zero;
    }

    private void Move()
    {
        Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);

        if (cameraTransform != null)
        {
            float yaw = cameraTransform.eulerAngles.y;
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = cameraTransform.right;
            right.y = 0f;
            right.Normalize();

            move = (right * move.x + forward * move.z);
        }

        if (move.sqrMagnitude > 1f) move.Normalize();
        
        // block small input to prevent unwanted movement direction changes
        if (move.magnitude >= DirUpdateDeadzone) LastMoveDir = move.normalized;

        Vector3 velocity = move * WalkSpeed;
        if (externalTimer > 0f) velocity += externalVelocity;

        velocity.y = verticalVelocity;
        cc.Move(velocity * Time.deltaTime);
    }

    public void AddExternalVelocity(Vector3 vel, float duration)
    {
        vel.y = 0f;
        externalVelocity = vel;
        externalTimer = Mathf.Max(duration, 0.01f);
    }
}
