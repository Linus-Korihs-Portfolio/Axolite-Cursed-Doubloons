using UnityEngine;

/// <summary>
/// Enemy 2 — Shell Spinner (Koopa / Armos-like).
///
/// State flow:  Idle → Windup → Spinning → Hit → ExitingShell → Dizzy → WakingUp → Windup …
///
///   • Sleeps until a player/minion enters LOS or the spinner takes damage.
///   • Immediately starts the windup once a target is found; shows a live aim line
///     toward the target until the shell closes.
///   • Spin direction is locked toward the target the moment the shell closes;
///     the spinner travels in that straight line regardless of where the target moves.
///   • Any collision (player, minion, or wall) ends the spin.
///   • Invincible only while fully in the shell (Spinning + Hit states).
///   • Vulnerable during Idle, Windup, ExitingShell, Dizzy, WakingUp.
///   • Death can only occur during the vulnerable states.
///
/// Hitbox design — assign both colliders in the Inspector:
///   bodyCollider  — the full body (head + legs), enabled when vulnerable.
///   shellCollider — the shell only (sphere/capsule), enabled when in shell.
///   Both are mutually exclusive; switching is handled automatically by SetHitboxState().
/// </summary>
[RequireComponent(typeof(CombatantStats))]
public class ShellSpinnerEnemy : MonoBehaviour
{
    private enum SpinnerState
    {
        Idle,
        Windup,
        Spinning,
        Hit,
        ExitingShell,
        Dizzy,
        WakingUp
    }

    [SerializeField] private ShellSpinnerEnemySettings settings;

    [Tooltip("Assign the player's actual moving transform (the object that physically moves). " +
             "Required when the player prefab root is a static anchor above the moving body.")]
    [SerializeField] private Transform playerTransform;

    [Header("Hitboxes")]
    [Tooltip("Child GameObject containing the full-body collider (head + legs). Active when the spinner is vulnerable.")]
    [SerializeField] private GameObject bodyObject;
    [Tooltip("Child GameObject containing the shell-only collider. Active while the spinner is inside the shell (invincible). " +
             "This is the collider that physically contacts players/walls during the spin.")]
    [SerializeField] private GameObject shellObject;

    [Header("Targeting Line")]
    [Tooltip("LineRenderer used to draw a live aim line toward the target during the windup. " +
             "Assign a child LineRenderer (2 positions, world space). Leave empty to skip.")]
    [SerializeField] private LineRenderer targetingLine;

    [Header("Debug")]
    [SerializeField] private bool enableLogs;

    // ── Core references ────────────────────────────────────────────────────────
    private CombatantStats stats;
    private Rigidbody       rb;

    // ── Target tracking ────────────────────────────────────────────────────────
    private Transform currentTarget;

    // ── State machine ──────────────────────────────────────────────────────────
    [Header("Runtime (Read Only)")]
    [SerializeField] private SpinnerState currentState = SpinnerState.Idle;
    private float stateTimer;

    // ── Behaviour flags ────────────────────────────────────────────────────────
    private bool isAsleep = true;

    // ── Spin data ──────────────────────────────────────────────────────────────
    private Vector3 spinDirection;      // locked when the shell closes; never updated mid-spin
    private Vector3 spinStartPosition;  // recorded when Spinning begins; used for MaxSpinRange
    private bool    spinHitSomething;   // set by OnCollisionEnter, consumed in ExecuteState

    // Per-spin dedup: each target is damaged at most once per spin.
    private readonly System.Collections.Generic.HashSet<int> spinHitIds =
        new System.Collections.Generic.HashSet<int>();

    private float spinStartTime = -999f; // used for the collision grace period

    // ── SpinUntilWall pass-through ─────────────────────────────────────────
    // When SpinUntilWall is enabled we Physics.IgnoreCollision each hit target so the
    // shell can physically pass through them.  Collisions are restored in ExitingShell.
    private Collider shellCollider;
    private readonly System.Collections.Generic.List<Collider> ignoredColliders =
        new System.Collections.Generic.List<Collider>();

    // ── Physics (horizontal velocity set in Update, applied in FixedUpdate) ────
    private Vector3 frameVelocity;

    private static readonly Collider[] overlapBuffer = new Collider[32];

    // ──────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        stats              = GetComponent<CombatantStats>();
        stats.Died        += OnDied;
        stats.DamageTaken += OnDamageTaken;

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic  = false;
            rb.constraints  = RigidbodyConstraints.FreezeRotation;
            // Interpolate between physics steps so fast spin movement renders smoothly.
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        SetHitboxState(inShell: false);

        shellCollider = shellObject != null ? shellObject.GetComponent<Collider>() : null;

        if (targetingLine != null)
        {
            targetingLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            targetingLine.receiveShadows    = false;
            targetingLine.positionCount     = 2;
            targetingLine.enabled           = false;
        }
    }

    private void OnDestroy()
    {
        if (stats != null)
        {
            stats.Died        -= OnDied;
            stats.DamageTaken -= OnDamageTaken;
        }
    }

    private void Update()
    {
        if (stats.IsDead) return;

        frameVelocity = Vector3.zero;
        RefreshTarget();
        UpdateState();
        ExecuteState();
    }

    // Line renderer positions are updated here so they read transforms after
    // all Update() calls and physics interpolation have settled for the frame.
    private void LateUpdate()
    {
        if (currentState != SpinnerState.Windup || targetingLine == null || !targetingLine.enabled) return;
        if (currentTarget == null) return;
        targetingLine.SetPosition(0, SnapToGround(transform.position));
        targetingLine.SetPosition(1, SnapToGround(currentTarget.position));
    }

    // Projects a world-space point down onto the ground surface.
    // Uses GroundMask if assigned; falls back to the spinner's own Y.
    private Vector3 SnapToGround(Vector3 worldPos)
    {
        const float offset    = 0.05f; // hover just above the surface to avoid z-fighting
        const float rayHeight = 6f;
        if (settings != null && settings.GroundMask != 0)
        {
            Vector3 origin = worldPos + Vector3.up * rayHeight;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayHeight + 2f,
                                settings.GroundMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * offset;
        }
        // Fallback: flatten to the spinner's ground level.
        return new Vector3(worldPos.x, transform.position.y + offset, worldPos.z);
    }

    private void FixedUpdate()
    {
        if (rb == null || stats == null || stats.IsDead) return;
        rb.linearVelocity = new Vector3(frameVelocity.x, rb.linearVelocity.y, frameVelocity.z);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (currentState != SpinnerState.Spinning) return;

        Transform root    = collision.transform.root;
        bool      isPlayer = root.CompareTag(settings.PlayerTag);
        bool      isMinion = collision.transform.CompareTag(settings.MinionTag);

        // Ignore floor / ceiling: only horizontal contacts (wall normals) matter.
        bool hasHorizontalContact = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Mathf.Abs(collision.GetContact(i).normal.y) <= 0.5f)
            {
                hasHorizontalContact = true;
                break;
            }
        }
        if (!hasHorizontalContact) return;

        // Grace period only applies to wall collisions, NOT to players/minions.
        // This prevents the spinner from re-triggering on the wall it was resting against,
        // while still allowing it to hit a nearby player immediately.
        if (!isPlayer && !isMinion)
        {
            float grace = settings != null ? settings.SpinCollisionGrace : 0.12f;
            if (Time.time < spinStartTime + grace) return;
        }

        if (isPlayer || isMinion)
        {
            // SpinUntilWall: physically pass through this target so the shell isn't stopped
            // by the target's collider.  Restored when the shell opens (ExitingShell).
            if (settings != null && settings.SpinUntilWall && shellCollider != null
                && !ignoredColliders.Contains(collision.collider))
            {
                Physics.IgnoreCollision(shellCollider, collision.collider, true);
                ignoredColliders.Add(collision.collider);
            }

            // Deal damage to the hit target (once per spin per target).
            // Use CombatantStats.GetInstanceID() as the key — root.GetInstanceID() would be
            // the same for all minions if they share a common scene-hierarchy parent.
            //
            // For the player, start the stats search from playerTransform (inspector-assigned
            // moving body) rather than from the collider transform, so we find CombatantStats
            // at most one level up instead of traversing the whole scene hierarchy.
            Transform statsRoot = (isPlayer && playerTransform != null)
                ? playerTransform
                : collision.transform;
            CombatantStats ts = GetStats(statsRoot);
            if (ts != null)
            {
                int id = ts.GetInstanceID();
                if (!spinHitIds.Contains(id) && !ts.IsDead)
                {
                    spinHitIds.Add(id);
                    ts.ApplyDamage(settings != null ? settings.SpinDamage : 18f);
                    // For the player, apply knockback to playerTransform (the moving body with
                    // the Rigidbody), not to the stats component's transform.
                    Transform knockbackTarget = (isPlayer && playerTransform != null)
                        ? playerTransform
                        : ts.transform;
                    ApplyKnockback(knockbackTarget);
                    Log($"Spin hit {ts.name} for {settings.SpinDamage}");
                }
            }
            // SpinUntilWall: pass through targets; only a wall stops the spin.
            if (settings == null || !settings.SpinUntilWall)
                spinHitSomething = true;
        }
        else
        {
            // Wall hit — always ends the spin.
            spinHitSomething = true;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Target tracking
    // ──────────────────────────────────────────────────────────────────────────

    private static CombatantStats GetStats(Transform t)
    {
        if (t == null) return null;
        return t.GetComponent<CombatantStats>()
            ?? t.GetComponentInParent<CombatantStats>()
            ?? t.GetComponentInChildren<CombatantStats>();
    }

    private void OnDamageTaken(float _)
    {
        isAsleep = false;
    }

    private void RefreshTarget()
    {
        float forgetRadius = settings != null ? settings.ForgetRadius : 18f;

        // Drop stale / dead targets.
        if (currentTarget != null)
        {
            CombatantStats ts = GetStats(currentTarget);
            bool dead = ts != null && ts.IsDead;
            bool far  = HorizontalDistance(currentTarget.position) > forgetRadius;

            if (dead || far || !currentTarget.gameObject.activeInHierarchy)
                currentTarget = null;
        }

        if (currentTarget != null) return;

        if (isAsleep)
        {
            Transform player = FindPlayerInSight();
            if (player != null)
            {
                isAsleep      = false;
                currentTarget = player;
                Log($"Player spotted — waking: {player.name}");
            }
            return;
        }

        Transform found = FindBestTarget();
        if (found != null)
        {
            currentTarget = found;
            Log($"Target acquired: {found.name}");
        }
        else
        {
            isAsleep = true;
            Log("No targets in range — returning to sleep");
        }
    }

    private Transform FindPlayerInSight()
    {
        if (settings == null) return null;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, settings.DetectRadius, overlapBuffer, settings.DetectMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;
            Transform root = col.transform.root;
            if (!root.gameObject.activeInHierarchy) continue;
            if (!root.CompareTag(settings.PlayerTag)) continue;
            CombatantStats cs = GetStats(col.transform);
            if (cs != null && cs.IsDead) continue;
            if (!HasLineOfSight(col.transform)) continue;

            return playerTransform != null ? playerTransform : root;
        }
        return null;
    }

    private Transform FindBestTarget()
    {
        if (settings == null) return null;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, settings.DetectRadius, overlapBuffer, settings.DetectMask, QueryTriggerInteraction.Ignore);

        Transform best   = null;
        float     bestSq = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;
            Transform root    = col.transform.root;
            bool      isPlayer = root.CompareTag(settings.PlayerTag);
            bool      isMinion = col.transform.CompareTag(settings.MinionTag);
            if (!isPlayer && !isMinion) continue;
            if (!root.gameObject.activeInHierarchy) continue;
            CombatantStats cs = GetStats(col.transform);
            if (cs != null && cs.IsDead) continue;
            if (settings.RequireLOSToDetect && !HasLineOfSight(col.transform)) continue;

            Transform t  = (isPlayer && playerTransform != null) ? playerTransform : col.transform;
            float     sq = (transform.position - t.position).sqrMagnitude;
            if (sq < bestSq) { best = t; bestSq = sq; }
        }

        return best;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  State machine
    // ──────────────────────────────────────────────────────────────────────────

    private void UpdateState()
    {
        // Locked states self-terminate via their own timers / collision flags.
        if (currentState == SpinnerState.Windup      ||
            currentState == SpinnerState.Spinning       ||
            currentState == SpinnerState.Hit            ||
            currentState == SpinnerState.ExitingShell   ||
            currentState == SpinnerState.Dizzy          ||
            currentState == SpinnerState.WakingUp) return;

        // Idle: go to sleep when no target, start shell entry when one is found.
        if (isAsleep || currentTarget == null)
        {
            SetState(SpinnerState.Idle);
            return;
        }

        SetState(SpinnerState.Windup);
    }

    private void ExecuteState()
    {
        switch (currentState)
        {
            case SpinnerState.Idle:
            {
                // Face the target so it looks alert; no movement.
                if (currentTarget != null) FaceTarget();
                break;
            }

            case SpinnerState.Windup:
            {
                stateTimer -= Time.deltaTime;

                if (stateTimer <= 0f)
                {
                    // Lock the spin direction toward the target (or straight ahead if lost).
                    Vector3 dir = currentTarget != null
                        ? currentTarget.position - transform.position
                        : transform.forward;
                    dir.y = 0f;
                    spinDirection    = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
                    spinHitSomething = false;
                    spinHitIds.Clear();
                    SetState(SpinnerState.Spinning);
                }
                break;
            }

            case SpinnerState.Spinning:
            {
                if (spinHitSomething)
                {
                    SetState(SpinnerState.Hit);
                    break;
                }
                // When SpinUntilWall is active, cap travel at MaxSpinRange.
                if (settings != null && settings.SpinUntilWall && settings.MaxSpinRange > 0f)
                {
                    float travelled = Vector3.Distance(transform.position, spinStartPosition);
                    if (travelled >= settings.MaxSpinRange)
                    {
                        SetState(SpinnerState.Hit);
                        break;
                    }
                }
                frameVelocity = spinDirection * (settings != null ? settings.SpinSpeed : 10f);
                FaceTowards(spinDirection);
                break;
            }

            case SpinnerState.Hit:
            {
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    SetState(SpinnerState.ExitingShell);
                break;
            }

            case SpinnerState.ExitingShell:
            {
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    SetState(SpinnerState.Dizzy);
                break;
            }

            case SpinnerState.Dizzy:
            {
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    SetState(SpinnerState.WakingUp);
                break;
            }

            case SpinnerState.WakingUp:
            {
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    SetState(currentTarget != null ? SpinnerState.Windup : SpinnerState.Idle);
                break;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Hitbox & invincibility
    // ──────────────────────────────────────────────────────────────────────────

    /// <param name="inShell">
    /// true  → shellObject active, bodyObject inactive, IsInvincible = true.<br/>
    /// false → bodyObject active, shellObject inactive, IsInvincible = false.
    /// </param>
    private void SetHitboxState(bool inShell)
    {
        if (stats != null) stats.IsInvincible = inShell;
        if (bodyObject  != null) bodyObject.SetActive(!inShell);
        if (shellObject != null) shellObject.SetActive( inShell);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Rotation helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void FaceTarget()
    {
        if (currentTarget == null) return;
        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) SmoothFaceDirection(dir.normalized);
    }

    private void FaceTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f) SmoothFaceDirection(direction.normalized);
    }

    private void SmoothFaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
        float speed = settings != null ? settings.RotationSpeed : 8f;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, speed * Time.deltaTime);
    }

    private float HorizontalDistance(Vector3 pos)
    {
        Vector3 delta = pos - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Line of sight
    // ──────────────────────────────────────────────────────────────────────────

    private bool HasLineOfSight(Transform target)
    {
        if (target == null || settings == null) return false;
        Vector3 start = transform.position + Vector3.up * settings.LosHeightOffset;
        Vector3 end   = target.position    + Vector3.up * settings.LosHeightOffset;
        Vector3 dir   = end - start;
        float   dist  = dir.magnitude;
        if (dist <= 0.0001f) return true;
        if (Physics.Raycast(start, dir / dist, out RaycastHit hit, dist, settings.LosBlockMask, QueryTriggerInteraction.Ignore))
            return hit.transform == target || hit.transform.IsChildOf(target) || target.IsChildOf(hit.transform);
        return true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  State helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void SetState(SpinnerState newState)
    {
        if (newState == currentState) return;
        Log($"State: {currentState} → {newState}");
        currentState = newState;

        // Hide the targeting line on every state transition; only Windup re-enables it.
        if (targetingLine != null) targetingLine.enabled = false;

        switch (newState)
        {
            case SpinnerState.Idle:
                SetHitboxState(inShell: false);
                break;

            case SpinnerState.Windup:
                // Vulnerable while targeting — shell isn’t closed yet.
                SetHitboxState(inShell: false);
                stateTimer = settings != null ? settings.WindupDuration : 0.6f;
                if (targetingLine != null) { targetingLine.positionCount = 2; targetingLine.enabled = true; }
                break;

            case SpinnerState.Spinning:
                // Shell is closed — invincible from now until ExitingShell.
                SetHitboxState(inShell: true);
                spinStartTime     = Time.time;
                spinStartPosition = transform.position;
                ignoredColliders.Clear(); // fresh slate — no lingering pass-through ignores
                break;

            case SpinnerState.Hit:
                // Still inside the shell — keep invincible, stop movement.
                SetHitboxState(inShell: true);
                frameVelocity = Vector3.zero;
                if (rb != null) rb.linearVelocity = Vector3.zero;
                stateTimer = settings != null ? settings.HitPauseDuration : 0.15f;
                break;

            case SpinnerState.ExitingShell:
                // Deactivate the shell collider FIRST so it is no longer active when we
                // restore the ignored collision pairs.  If we restored while the shell was
                // still active and it was geometrically overlapping a target, PhysX would
                // immediately apply a separation impulse, causing a visible pop/jitter.
                SetHitboxState(inShell: false);
                RestoreIgnoredColliders();
                stateTimer = settings != null ? settings.ExitShellDuration : 0.6f;
                break;

            case SpinnerState.Dizzy:
                stateTimer = settings != null ? settings.DizzyDuration : 2.5f;
                break;

            case SpinnerState.WakingUp:
                stateTimer = settings != null ? settings.WakeUpDuration : 0.4f;
                break;
        }
    }

    private void Log(string msg)
    {
        if (enableLogs) Debug.Log($"[ShellSpinner] {msg}", this);
    }

    // Restores all Physics.IgnoreCollision pairs that were created during a SpinUntilWall spin.
    private void RestoreIgnoredColliders()
    {
        if (shellCollider != null)
            foreach (var col in ignoredColliders)
                if (col != null) Physics.IgnoreCollision(shellCollider, col, false);
        ignoredColliders.Clear();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Knockback
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a sideways (lateral) impulse to a hit target.
    /// The push direction is the component of (target – spinner) that is perpendicular
    /// to the spin direction, so targets are deflected off the side of the path rather
    /// than pushed straight backwards.
    /// </summary>
    private void ApplyKnockback(Transform target)
    {
        float force = settings != null ? settings.KnockbackForce : 8f;
        if (force <= 0f) return;

        // target is ts.transform (the CombatantStats GO), not collision.transform.root.
        // Using root caused all minions to resolve to a shared scene container, skipping knockback.

        // Lateral direction: remove the component parallel to the spin axis.
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        Vector3 lateral = toTarget - spinDirection * Vector3.Dot(toTarget, spinDirection);
        Vector3 dir = lateral.sqrMagnitude > 0.0001f
            ? lateral.normalized
            : Vector3.Cross(spinDirection, Vector3.up).normalized; // dead-centre → push right

        // Prefer Rigidbody impulse (player or other physics objects).
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null && !targetRb.isKinematic)
        {
            targetRb.AddForce(dir * force, ForceMode.Impulse);
            return;
        }

        // No usable Rigidbody — simulate via KnockbackReceiver (works with CharacterController or raw transform).
        KnockbackReceiver receiver = target.GetComponent<KnockbackReceiver>();
        if (receiver == null) receiver = target.gameObject.AddComponent<KnockbackReceiver>();
        receiver.AddImpulse(dir * force);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Death
    // ──────────────────────────────────────────────────────────────────────────

    private void OnDied()
    {
        RestoreIgnoredColliders();
        Destroy(gameObject);
    }
}
