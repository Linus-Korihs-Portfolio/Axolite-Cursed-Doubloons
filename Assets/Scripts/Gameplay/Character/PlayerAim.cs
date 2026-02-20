using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : MonoBehaviour
{
    [SerializeField] private PlayerConfig config;
    [Header("Aim / Rotation")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private LayerMask aimGroundMask;
    [SerializeField] public Transform visual;
    private float VisualTurnSpeed => config.visualTurnSpeed;
    private float VisualDeadzone => config.visualDeadzone;

    public Vector3 AimDirection { get; private set; } = Vector3.forward;
    public Vector3 AimPoint { get; private set; }
    public Vector3 FacingDirection { get; private set; } = Vector3.forward;

    private PlayerMovementCC movement;

    private void Awake()
    {
        if (aimCamera == null) aimCamera = Camera.main;
        movement = GetComponent<PlayerMovementCC>();
    }

    private void Update()
    {
        UpdateFacingFromMove();
        RotateByWASD();
    }

    private void UpdateFacingFromMove()
    {
        if (movement == null) return;

        Vector3 dir = movement.LastMoveDir;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) return;

        FacingDirection = dir.normalized;
    }

    public void RotateByWASD()
    {
        if (movement == null) return;

        if (visual != null)
        {
            Vector3 dir = new Vector3(movement.moveInput.x, 0f, movement.moveInput.y);

            if (movement.cameraTransform != null)
            {
                Vector3 forward = movement.cameraTransform.forward;
                forward.y = 0f;
                forward.Normalize();

                Vector3 right = movement.cameraTransform.right;
                right.y = 0f;
                right.Normalize();

                dir = (right * dir.x + forward * dir.z);
            }

            if (dir.magnitude >= VisualDeadzone)
            {
                Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
                visual.rotation = Quaternion.Slerp(visual.rotation, target * Quaternion.Euler(0f, 180f, 0f), VisualTurnSpeed * Time.deltaTime);
            }
        }
    }
}
