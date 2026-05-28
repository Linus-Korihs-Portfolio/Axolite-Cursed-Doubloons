using UnityEngine;

/// <summary>
/// Enemy 2 — Shell Spinner (Koopa / Armos-like).
///
/// State flow:  Idle → EnteringShell → Spinning → Hit → ExitingShell → Dizzy → WakingUp → EnteringShell …
///
///   • Sleeps until a player/minion enters LOS or the spinner takes damage.
///   • Immediately starts pulling into its shell once a target is found.
///   • Spin direction is locked toward the target the moment the shell closes;
///     the spinner travels in that straight line regardless of where the target moves.
///   • Any collision (player, minion, or wall) ends the spin.
///   • Invincible only while fully in the shell (Spinning + Hit states).
///   • Vulnerable during Idle, EnteringShell, ExitingShell, Dizzy, WakingUp.
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
        EnteringShell,
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
    private bool    spinHitSomething;   // set by OnCollisionEnter, consumed in ExecuteState

    // Per-spin dedup: each target is damaged at most once per spin.
    private readonly System.Collections.Generic.HashSet<int> spinHitIds =
        new System.Collections.Generic.HashSet<int>();

    private float spinStartTime = -999f; // used for the collision grace period

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
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        SetHitboxState(inShell: false);
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
            // Deal damage to the hit target (once per spin per target).
            int id = root.GetInstanceID();
            if (!spinHitIds.Contains(id))
            {
                CombatantStats ts = GetStats(collision.transform);
                if (ts != null && !ts.IsDead)
                {
                    spinHitIds.Add(id);
                    ts.ApplyDamage(settings != null ? settings.SpinDamage : 18f);
                    Log($"Spin hit {root.name} for {settings.SpinDamage}");
                }
            }
        }

        // Horizontal wall or target contact — stops the spin.
        spinHitSomething = true;
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
        if (currentState == SpinnerState.EnteringShell ||
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

        SetState(SpinnerState.EnteringShell);
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

            case SpinnerState.EnteringShell:
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
                    SetState(currentTarget != null ? SpinnerState.EnteringShell : SpinnerState.Idle);
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

        switch (newState)
        {
            case SpinnerState.Idle:
                SetHitboxState(inShell: false);
                break;

            case SpinnerState.EnteringShell:
                // Vulnerable while tucking in — shell isn't closed yet.
                SetHitboxState(inShell: false);
                stateTimer = settings != null ? settings.EnterShellDuration : 0.6f;
                break;

            case SpinnerState.Spinning:
                // Shell is closed — invincible from now until ExitingShell.
                SetHitboxState(inShell: true);
                spinStartTime = Time.time;
                break;

            case SpinnerState.Hit:
                // Still inside the shell — keep invincible, stop movement.
                SetHitboxState(inShell: true);
                frameVelocity = Vector3.zero;
                if (rb != null) rb.linearVelocity = Vector3.zero;
                stateTimer = settings != null ? settings.HitPauseDuration : 0.15f;
                break;

            case SpinnerState.ExitingShell:
                // Shell opens → vulnerable immediately.
                SetHitboxState(inShell: false);
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

    // ──────────────────────────────────────────────────────────────────────────
    //  Death
    // ──────────────────────────────────────────────────────────────────────────

    private void OnDied() => Destroy(gameObject);
}
