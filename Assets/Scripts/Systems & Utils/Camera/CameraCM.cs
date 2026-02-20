using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class CameraCM : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private CameraCMSettings settings;

    [Header("Cinemachine")]
    [SerializeField] public CinemachineOrbitalFollow orbital;

    [Header("Input Actions")]
    [SerializeField] public InputActionReference toggleAction;
    [SerializeField] public InputActionReference zoomAction;

    [Header("Cinemachine (optional but recommended)")]
    [SerializeField] private CinemachineCamera cmCamera;

    [Header("Input Actions")]
    [SerializeField] public InputActionReference lookAction;
    [SerializeField] public InputActionReference lockOnAction;

    private bool isLockedOn;
    private Transform lockTarget;
    private Transform defaultLookAt;

    private bool isThird;
    private float targetRadius;
    private float targetVertical;

    private float ZoomSpeed => settings.zoomSpeed;
    private float TransitionSpeed => settings.transitionSpeed;
    private float MinRadius => settings.minRadius;
    private float MaxRadius => settings.maxRadius;
    private float TopRadius => settings.topRadius;
    private float TopVertical => settings.topVertical;
    private float ThirdRadius => settings.thirdRadius;
    private float ThirdVertical => settings.thirdVertical;
    private float LookSensX => settings.lookSensitivityX;
    private float LookSensY => settings.lookSensitivityY;
    private bool InvertY => settings.invertY;
    private bool LookScaleWithDeltaTime => settings.lookScaleWithDeltaTime;
    private float MinVertical => settings.minVertical;
    private float MaxVertical => settings.maxVertical;
    private string[] LockOnTags => settings.lockOnTags;
    private float LockOnMaxDistance => settings.lockOnMaxDistance;
    private float GamepadSensX => settings.gamepadSensitivityX;
    private float GamepadSensY => settings.gamepadSensitivityY;

    private float MouseSensX => settings.mouseSensitivityX;
    private float MouseSensY => settings.mouseSensitivityY;
    private bool MouseScaleWithDeltaTime => settings.mouseScaleWithDeltaTime;

    private void OnEnable()
    {
        if (toggleAction) toggleAction.action.Enable();
        if (zoomAction) zoomAction.action.Enable();
        if (lookAction) lookAction.action.Enable();
        if (lockOnAction) lockOnAction.action.Enable();

        if (toggleAction) toggleAction.action.performed += OnToggle;
        if (lockOnAction) lockOnAction.action.performed += OnLockOn;
    }

    private void OnDisable()
    {
        if (toggleAction) toggleAction.action.performed -= OnToggle;
        if (lockOnAction) lockOnAction.action.performed -= OnLockOn;

        if (toggleAction) toggleAction.action.Disable();
        if (zoomAction) zoomAction.action.Disable();
        if (lookAction) lookAction.action.Disable();
        if (lockOnAction) lockOnAction.action.Disable();
    }

    private void Start()
    {
        if (!cmCamera) cmCamera = GetComponent<CinemachineCamera>();
        if (cmCamera) defaultLookAt = cmCamera.LookAt;

        SetMode(false, instant: true);
    }

    private void Update()
    {
        if (zoomAction)
        {
            float scrollY = zoomAction.action.ReadValue<float>();
            if (Mathf.Abs(scrollY) > 0.001f)
            {
                targetRadius = Mathf.Clamp(targetRadius - scrollY * ZoomSpeed, MinRadius, MaxRadius);
            }
        }

        if (!isLockedOn && lookAction)
        {
            Vector2 look = lookAction.action.ReadValue<Vector2>();
            if (look.sqrMagnitude > 0.0001f)
            {
                bool usingMouse = Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f;

                float sensX = usingMouse ? MouseSensX : GamepadSensX;
                float sensY = usingMouse ? MouseSensY : GamepadSensY;

                float dt = 1f;
                if (!usingMouse || MouseScaleWithDeltaTime) dt = Time.deltaTime;

                // Horizontal
                var h = orbital.HorizontalAxis;
                h.Value += look.x * sensX * dt;
                orbital.HorizontalAxis = h;

                // Vertical
                float y = (InvertY ? look.y : -look.y);
                var vAxis = orbital.VerticalAxis;
                vAxis.Value = Mathf.Clamp(vAxis.Value + y * sensY * dt, MinVertical, MaxVertical);
                orbital.VerticalAxis = vAxis;

                targetVertical = vAxis.Value;
            }
        }

        // Smooth transition
        orbital.Radius = Mathf.Lerp(orbital.Radius, targetRadius, Time.deltaTime * TransitionSpeed);

        var v = orbital.VerticalAxis;
        v.Value = Mathf.Lerp(v.Value, targetVertical, Time.deltaTime * TransitionSpeed);
        orbital.VerticalAxis = v;
    }

    private void OnToggle(InputAction.CallbackContext _)
    {
        SetMode(!isThird, instant: false);
        Debug.Log($"Camera mode toggled. Now in {(isThird ? "third-person" : "top-down")} mode.");
    }

    private void SetMode(bool third, bool instant)
    {
        isThird = third;

        targetRadius = third ? ThirdRadius : TopRadius;
        targetVertical = third ? ThirdVertical : TopVertical;

        if (!orbital)
        {
            Debug.LogError("No CinemachineOrbitalFollow assigned/found!");
            return;
        }

        if (instant)
        {
            orbital.Radius = targetRadius;
            var v = orbital.VerticalAxis;
            v.Value = targetVertical;
            orbital.VerticalAxis = v;
        }
        Debug.Log($"SetMode -> targetRadius:{targetRadius}, targetVertical:{targetVertical} | currentRadius:{orbital.Radius}, currentVertical:{orbital.VerticalAxis.Value}");
    }

    private void OnLockOn(InputAction.CallbackContext _)
    {
        if (!cmCamera)
        {
            Debug.LogWarning("[CameraCM] LockOn: No CinemachineCamera assigned/found.");
            return;
        }

        // toggle off
        if (isLockedOn)
        {
            ClearLockOn();
            return;
        }

        // toggle on
        Transform best = FindClosestLockTarget();
        if (!best)
        {
            if (settings.debugEnabled) Debug.Log("[CameraCM] LockOn: no target found.");
            return;
        }

        lockTarget = best;
        isLockedOn = true;

        cmCamera.LookAt = lockTarget;

        if (settings.debugEnabled) Debug.Log($"[CameraCM] LockOn -> {lockTarget.name}");
    }

    private void ClearLockOn()
    {
        isLockedOn = false;
        lockTarget = null;

        if (cmCamera) cmCamera.LookAt = defaultLookAt;

        if (settings.debugEnabled) Debug.Log("[CameraCM] LockOn cleared.");
    }

    private Transform FindClosestLockTarget()
    {
        Transform origin = (cmCamera && cmCamera.Follow) ? cmCamera.Follow : transform;

        float bestDistSq = LockOnMaxDistance * LockOnMaxDistance;
        Transform best = null;

        if (LockOnTags == null || LockOnTags.Length == 0) return null;

        for (int t = 0; t < LockOnTags.Length; t++)
        {
            string tag = LockOnTags[t];
            if (string.IsNullOrWhiteSpace(tag)) continue;

            GameObject[] objs;
            try
            {
                objs = GameObject.FindGameObjectsWithTag(tag);
            }
            catch
            {
                continue;
            }

            for (int i = 0; i < objs.Length; i++)
            {
                var go = objs[i];
                if (!go) continue;

                float dSq = (go.transform.position - origin.position).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = go.transform;
                }
            }
        }

        return best;
    }

}
