using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : MonoBehaviour
{
    [Header("Aim / Rotation")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private LayerMask aimGroundMask;
    [SerializeField] private float rotateSpeed = 20f;

    [SerializeField] private bool rotateByMouse = false;
    [SerializeField] public Transform visual;
    [SerializeField] private float visualTurnSpeed = 15f;

    public Vector3 AimDirection { get; private set; } = Vector3.forward;
    public Vector3 AimPoint { get; private set; }

    private PlayerMovementCC movement;

    private void Awake()
    {
        if (aimCamera == null) aimCamera = Camera.main;
        movement = GetComponent<PlayerMovementCC>();
    }

    private void Update()
    {
        UpdateAim();

        if (rotateByMouse) RotateRoot();
        else RotateByWASD();
    }

    private void UpdateAim()
    {
        if (aimCamera == null) return;

        Ray ray = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, aimGroundMask, QueryTriggerInteraction.Ignore)) return;

        AimPoint = hit.point;

        Vector3 dir = hit.point - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) return;

        AimDirection = dir.normalized;
    }

    private void RotateRoot()
    {
        if (AimDirection.sqrMagnitude < 0.0001f) return;
        Quaternion targetRot = Quaternion.LookRotation(AimDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }

    public void RotateByWASD()
    {
        if (movement == null) return;

        if (visual != null)
        {
            Vector3 dir = new Vector3(movement.moveInput.x, 0f, movement.moveInput.y);

            if (movement.cameraTransform != null)
            {
                Vector3 forward = movement.cameraTransform.forward; forward.y = 0f; forward.Normalize();
                Vector3 right = movement.cameraTransform.right; right.y = 0f; right.Normalize();
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
