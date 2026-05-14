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

    [Header("Camera Actions")]
    [SerializeField] public InputActionReference lookAction;
    [SerializeField] public InputActionReference lockOnAction;

    [Header("Debug")]
    [SerializeField] private bool enableLogs;

    private bool isLockedOn;
    private GroundCursor cursor; // reference to GroundCursor for optional interaction (e.g. force unlock when player teleports)
    private Transform lockTarget;
    private Transform defaultLookAt;

    private bool isThird;
    private float targetRadius;
    private float targetVertical;
    private Transform lockFramingTarget;
    private Vector3 lockFramingVelocity;
    private float baseModeRadius;

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
    private float LockOnPlayerBias => settings.lockOnPlayerBias;
    private float LockOnHeightOffset => settings.lockOnHeightOffset;
    private float LockOnLookTargetSmooth => settings.lockOnLookTargetSmooth;
    private float LockOnRadiusPerMeter => settings.lockOnRadiusPerMeter;
    private float LockOnMaxExtraRadius => settings.lockOnMaxExtraRadius;
    private float LockOnVertical => settings.lockOnVertical;
    private float LockOnVerticalSmooth => settings.lockOnVerticalSmooth;
    private float LockOnMinDistance => settings.lockOnMinDistance;

    private void Log(string msg) { if (enableLogs) Debug.Log(msg); }

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
        CreateLockFramingTarget();
        cursor = FindFirstObjectByType<GroundCursor>();
        SetMode(false, instant: true);
    }

    private void Update()
    {
        if (ShouldBreakLockOn())
        {
            ClearLockOn();
            return;
        }

        if (zoomAction)
        {
            float z = zoomAction.action.ReadValue<float>();
            if (Mathf.Abs(z) > 0.001f)
            {
                bool usingMouseWheel = Mouse.current != null && Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.01f;

                float speed = usingMouseWheel ? settings.mouseZoomSpeed : settings.gamepadZoomSpeed;
                float dt = usingMouseWheel ? 1f : Time.deltaTime; // Controller = per second

                targetRadius = Mathf.Clamp(targetRadius - z * speed * dt, MinRadius, MaxRadius);
            }
        }

        if (!isLockedOn && lookAction)
        {
            Vector2 look = lookAction.action.ReadValue<Vector2>();

            // Block camera look ONLY for the device currently aiming the cursor
            if (!isLockedOn && cursor != null)
            {
                bool mouseMoving = Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f;
                bool stickMoving = Gamepad.current != null && Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.01f;

                if ((mouseMoving && cursor.IsAimingWithMouseKeyboard) || (stickMoving && cursor.IsAimingWithGamepad))
                {
                    return;
                }
            }

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

        if (isLockedOn && lockTarget != null) UpdateLockOnFraming();

        // Smooth transition
        orbital.Radius = Mathf.Lerp(orbital.Radius, targetRadius, Time.deltaTime * settings.zoomSmoothing);

        var v = orbital.VerticalAxis;
        v.Value = Mathf.Lerp(v.Value, targetVertical, Time.deltaTime * TransitionSpeed);
        orbital.VerticalAxis = v;
    }

    private void OnToggle(InputAction.CallbackContext _)
    {
        SetMode(!isThird, instant: false);
        Log($"Camera mode toggled. Now in {(isThird ? "third-person" : "top-down")} mode.");
    }

    private void SetMode(bool third, bool instant)
    {
        isThird = third;

        baseModeRadius = third ? ThirdRadius : TopRadius;
        targetRadius = baseModeRadius;
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
        Log($"SetMode -> targetRadius:{targetRadius}, targetVertical:{targetVertical} | currentRadius:{orbital.Radius}, currentVertical:{orbital.VerticalAxis.Value}");
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

        UpdateLockOnFraming();
        cmCamera.LookAt = lockFramingTarget;

        if (cursor) cursor.LockTo(lockTarget);

        if (settings.debugEnabled) Debug.Log($"[CameraCM] LockOn -> {lockTarget.name}");
        
    }

    private void ClearLockOn()
    {
        isLockedOn = false;
        lockTarget = null;

        targetRadius = baseModeRadius;
        targetVertical = isThird ? ThirdVertical : TopVertical;

        if (cmCamera) cmCamera.LookAt = defaultLookAt;

        if (settings.debugEnabled) Debug.Log("[CameraCM] LockOn cleared.");
        if (cursor) cursor.ForceUnlockToPlayer();
    }

    private Transform FindClosestLockTarget()
    {
        Vector3 originPos = (cmCamera && cmCamera.Follow) ? cmCamera.Follow.position : transform.position;

        bool useCursorOrigin = cursor != null && cursor.LockWithCursor;
        if (useCursorOrigin) originPos = cursor.WorldPos;

        float maxDist = LockOnMaxDistance;
        if (useCursorOrigin && cursor.LockRangeWithCursor > 0f)
            maxDist = Mathf.Min(maxDist, cursor.LockRangeWithCursor);

        float bestDistSq = maxDist * maxDist;
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

                float dSq = (go.transform.position - originPos).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = go.transform;
                }
            }
        }
        return best;
    }

    private bool ShouldBreakLockOn()
    {
        if (!isLockedOn) return false;
        if (lockTarget == null || !lockTarget.gameObject.activeInHierarchy) return true;

        // If cursor-based lock was lost, camera lock should also break
        if (cursor != null && !cursor.IsLocked) return true;

        if (cursor != null && cursor.IsLocked && cursor.LockedTarget != lockTarget) return true;

        Transform playerTarget = (cmCamera != null && cmCamera.Follow != null) ? cmCamera.Follow : transform;

        float distSq = (lockTarget.position - playerTarget.position).sqrMagnitude;
        return distSq > LockOnMaxDistance * LockOnMaxDistance;
    }

    private void CreateLockFramingTarget()
    {
        if (lockFramingTarget != null) return;

        GameObject go = new GameObject("LockOnFramingTarget");
        go.hideFlags = HideFlags.HideInHierarchy;
        lockFramingTarget = go.transform;

        Vector3 startPos = transform.position + Vector3.up * LockOnHeightOffset;
        lockFramingTarget.position = startPos;
    }

    private void UpdateLockOnFraming()
    {
        if (!cmCamera || !lockTarget || !lockFramingTarget) return;

        Transform playerTarget = cmCamera.Follow != null ? cmCamera.Follow : transform;

        Vector3 playerPos = playerTarget.position;
        Vector3 enemyPos = lockTarget.position;

        // Framing point between player and enemy, but slightly enemy-favored
        Vector3 framedPos = Vector3.Lerp(playerPos, enemyPos, 1f - LockOnPlayerBias);
        framedPos.y += LockOnHeightOffset;

        float lookT = 1f - Mathf.Exp(-LockOnLookTargetSmooth * Time.deltaTime);
        lockFramingTarget.position = Vector3.Lerp(lockFramingTarget.position, framedPos, lookT);

        // Keep both visible by forcing a combat angle during lock-on
        targetVertical = Mathf.Lerp(targetVertical, LockOnVertical, Time.deltaTime * LockOnVerticalSmooth);

        // Zoom OUT only, based on player-enemy distance
        float distance = Vector3.Distance(playerPos, enemyPos);
        float extraRadius = Mathf.Min(distance * LockOnRadiusPerMeter, LockOnMaxExtraRadius);

        float desiredRadius = Mathf.Max(LockOnMinDistance, baseModeRadius + extraRadius);
        targetRadius = Mathf.Clamp(desiredRadius, baseModeRadius, MaxRadius);
    }
}
