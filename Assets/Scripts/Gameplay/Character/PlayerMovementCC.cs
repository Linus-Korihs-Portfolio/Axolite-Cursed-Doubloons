using UnityEngine;

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
    [SerializeField] private CharacterController cc;
    [SerializeField] public Transform cameraTransform;

    public bool MovementLocked { get; set; } = false;
    public float SpeedMultiplier { get; set; } = 1f;
    public void SetMoveInput(Vector2 input) => moveInput = input;
    public Vector3 LastMoveDir { get; private set; } = Vector3.forward;

    public Transform BodyTransform => cc.transform;

    private float verticalVelocity;
    public Vector2 moveInput;

    private Vector3 externalVelocity;
    private float externalTimer;

    private void Awake()
    {
        if (cc == null) cc = GetComponentInParent<CharacterController>();
    }

    private void Update()
    {
        ApplyGravity();
        TickExternal();
        Move();
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
            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = cameraTransform.right;
            right.y = 0f;
            right.Normalize();

            move = right * move.x + forward * move.z;
        }

        if (MovementLocked) move = Vector3.zero;
        if (move.sqrMagnitude > 1f) move.Normalize();
        
        // block small input to prevent unwanted movement direction changes
        if (move.magnitude >= DirUpdateDeadzone) LastMoveDir = move.normalized;

        Vector3 velocity = move * WalkSpeed * SpeedMultiplier;
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
