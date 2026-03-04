using UnityEngine;
using UnityEngine.InputSystem;

public class GroundCursor : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private GroundCursorSettings settings;

    [Header("Refs")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform marker;

    [Header("Input")]
    [SerializeField] private InputActionReference cursorMoveAction; // Vector2 (right stick / optional keys)
    [SerializeField] private InputActionReference cursorExtraKeyAction; // Button (e.g. Shift)

    public Vector3 WorldPos { get; private set; }
    public Transform LockedTarget { get; private set; }
    public bool IsLocked => LockedTarget != null;

    private Vector3 freeFlatPos; // world-space XZ cursor position (Y ignored, ground sampled)
    private bool freePosInitialized;
    private Vector3 externalMoveDir = Vector3.zero;
    private Vector3 smoothedDir = Vector3.forward;
    private bool smoothedDirInit;

    public void SetMoveDirection(Vector3 worldMoveDir) => externalMoveDir = worldMoveDir;
    public bool IsAimingWithMouseKeyboard { get; private set; }
    public bool IsAimingWithGamepad { get; private set; }

    private void OnEnable()
    {
        if (cursorMoveAction) cursorMoveAction.action.Enable();
        if (cursorExtraKeyAction) cursorExtraKeyAction.action.Enable();
    }

    private void OnDisable()
    {
        if (cursorMoveAction) cursorMoveAction.action.Disable();
        if (cursorExtraKeyAction) cursorExtraKeyAction.action.Disable();
    }

    private void Start()
    {
        if (marker) marker.localScale = settings.markerScale;
        smoothedDir = GetDesiredDirFlat();
        smoothedDirInit = true;

        Vector3 startFlat = GetBasePointFlat();
        freeFlatPos = startFlat;
        freePosInitialized = true;

        WorldPos = SampleGroundPoint(freeFlatPos) + Vector3.up * settings.heightOffset;
        ApplyVisuals(WorldPos);
    }

    private void LateUpdate()
    {
        if (settings == null || !settings.cursorEnabled)
        {
            SetVisualsActive(false);
            UnlockInternal(resetToPlayer: true);
            return;
        }

        SetVisualsActive(true);

        // LOCKED MODE (Pikmin 2: cursor sticks to target; unlock if too far)
        if (IsLocked)
        {
            if (LockedTarget == null || !LockedTarget.gameObject.activeInHierarchy)
            {
                UnlockInternal(resetToPlayer: true);
                return;
            }

            if (Vector3.Distance(player.position, LockedTarget.position) > settings.lockRange)
            {
                UnlockInternal(resetToPlayer: true);
                return;
            }

            Vector3 lockedPos = SampleGroundPoint(LockedTarget.position) + Vector3.up * settings.heightOffset;
            WorldPos = lockedPos; // no smoothing while locked = stable aiming
            ApplyVisuals(WorldPos);
            return;
        }

        // FREE MODE: fixed distance X in movement direction; cursor moves with player automatically
        IsAimingWithMouseKeyboard = false;
        IsAimingWithGamepad = false;

        Vector3 dir = GetSmoothedDirFlat();
        float d = settings.baseDistance; // fixed distance (no min/max ring)
        Vector3 desiredFlat = player.position + dir * d;
        desiredFlat.y = 0f;

        Vector3 desired = SampleGroundPoint(desiredFlat) + Vector3.up * settings.heightOffset;

        // hard set (no smoothing) so it never lags behind the player
        WorldPos = desired;

        // keep for future legacy/manual mode (world anchor)
        freeFlatPos = new Vector3(desiredFlat.x, 0f, desiredFlat.z);
        freePosInitialized = true;

        ApplyVisuals(WorldPos);

        /*
        ================================================================================
        LEGACY FEATURE (manual cursor movement) - intentionally disabled for now
        Uncomment this block to restore mouse/right-stick cursor movement.
        ================================================================================

            // Inputs
            bool extraPressedRaw = cursorExtraKeyAction != null && cursorExtraKeyAction.action != null && cursorExtraKeyAction.action.IsPressed();

            Vector2 mouse = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            Vector2 gamepadStick = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;
            Vector2 kbStick = cursorMoveAction ? cursorMoveAction.action.ReadValue<Vector2>() : Vector2.zero;

            IsAimingWithMouseKeyboard = settings.extraPressMouse && extraPressedRaw;
            IsAimingWithGamepad = settings.extraPressGamepad && extraPressedRaw;

            if (settings.extraPressMouse && !extraPressedRaw)
            {
                mouse = Vector2.zero;
                kbStick = Vector2.zero;
            }

            if (settings.extraPressGamepad && !extraPressedRaw)
            {
                gamepadStick = Vector2.zero;
            }

            Vector2 stick = gamepadStick + kbStick;

            Vector3 camFwd = cameraTransform.forward; camFwd.y = 0f;
            if (camFwd.sqrMagnitude < 0.0001f) camFwd = player.forward;
            camFwd.Normalize();

            Vector3 camRight = cameraTransform.right; camRight.y = 0f; camRight.Normalize();

            if (!freePosInitialized)
            {
                freeFlatPos = GetBasePointFlat();
                freePosInitialized = true;
            }

            if (mouse.sqrMagnitude > 0.0001f)
                freeFlatPos += (camRight * mouse.x + camFwd * mouse.y) * settings.mouseSpeed;

            if (stick.sqrMagnitude > 0.0001f)
                freeFlatPos += (camRight * stick.x + camFwd * stick.y) * (settings.stickSpeed * Time.deltaTime);

            freeFlatPos = ClampDistanceFromPlayerFlat(freeFlatPos);

            Vector3 desired = SampleGroundPoint(freeFlatPos) + Vector3.up * settings.heightOffset;

            if (settings.smoothWhenFree)
            {
                float t = 1f - Mathf.Exp(-settings.followSmoothing * Time.deltaTime);
                WorldPos = Vector3.Lerp(WorldPos, desired, t);
            }
            else
            {
                WorldPos = desired;
            }

            ApplyVisuals(WorldPos);

        */
    }
    private Vector3 GetBasePointFlat()
    {
        Vector3 dir = GetSmoothedDirFlat();
        float d = Mathf.Clamp(settings.baseDistance, settings.minDistance, settings.maxDistance);
        Vector3 flat = player.position + dir * d;
        flat.y = 0f; // flat target (we sample ground after)
        return flat;
    }

    private Vector3 GetDesiredDirFlat()
    {
        Vector3 dir;

        dir = (externalMoveDir.sqrMagnitude > 0.0001f) ? externalMoveDir : player.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        return dir.normalized;
    }

    private Vector3 GetSmoothedDirFlat()
    {
        Vector3 desired = GetDesiredDirFlat();

        if (!smoothedDirInit)
        {
            smoothedDir = desired;
            smoothedDirInit = true;
            return smoothedDir;
        }

        // Smooth direction so cursor doesn't "teleport" when player rotates quickly
        float t = 1f - Mathf.Exp(-(settings != null ? settings.dirSmoothing : 16f) * Time.deltaTime);
        smoothedDir = Vector3.Slerp(smoothedDir, desired, t);
        smoothedDir.y = 0f;

        if (smoothedDir.sqrMagnitude < 0.0001f) smoothedDir = desired;
        return smoothedDir.normalized;
    }

    private Vector3 ClampToRingFlat(Vector3 flatPos)
    {
        Vector3 from = flatPos - player.position;
        from.y = 0f;

        float dist = from.magnitude;
        if (dist < 0.001f) from = GetSmoothedDirFlat();
        else from /= dist;

        float clamped = Mathf.Clamp(dist, settings.minDistance, settings.maxDistance);

        Vector3 outPos = player.position + from * clamped;
        outPos.y = 0f;
        return outPos;
    }

    private Vector3 SampleGroundPoint(Vector3 nearPos)
    {
        Vector3 origin = nearPos + Vector3.up * settings.groundRayStartHeight;
        if (Physics.Raycast(origin, Vector3.down, out var hit, settings.groundRayLength, settings.groundMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        // fallback: keep Y as is
        return nearPos;
    }

    private void UnlockInternal(bool resetToPlayer)
    {
        LockedTarget = null;

        if (resetToPlayer)
        {
            Vector3 baseFlat = GetBasePointFlat();
            freeFlatPos = baseFlat;
            WorldPos = SampleGroundPoint(baseFlat) + Vector3.up * settings.heightOffset;
            smoothedDir = GetDesiredDirFlat();
            smoothedDirInit = true;
            ApplyVisuals(WorldPos);
        }
    }

    public void Unlock()
    {
        UnlockInternal(resetToPlayer: false);
    }

    private void ApplyVisuals(Vector3 pos)
    {
        if (marker) marker.position = pos;
        if (marker) marker.localScale = settings.markerScale;
    }

    private void SetVisualsActive(bool active)
    {
        if (marker && marker.gameObject.activeSelf != active) marker.gameObject.SetActive(active);
    }

    public void LockTo(Transform target)
    {
        LockedTarget = target;

        if (LockedTarget)
        {
            Vector3 tPos = SampleGroundPoint(LockedTarget.position) + Vector3.up * settings.heightOffset;
            WorldPos = tPos;
            freeFlatPos = new Vector3(tPos.x, 0f, tPos.z);
            smoothedDir = GetDesiredDirFlat();
            smoothedDirInit = true;
            ApplyVisuals(WorldPos);
        }
    }

    public void ForceUnlockToPlayer()
    {
        UnlockInternal(resetToPlayer: true);
    }
}