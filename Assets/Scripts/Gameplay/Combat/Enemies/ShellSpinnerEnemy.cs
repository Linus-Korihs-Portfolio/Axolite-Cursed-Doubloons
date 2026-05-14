using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy 2 — Shell Spinner (Wind Waker Armos-like).
///
/// Behaviour:
///   • Idle / Approach: walks toward the detected target normally.
///   • Trigger conditions (either starts a Retract → Spin cycle):
///       1. Enemy spots a player or minion within detection range.
///       2. Enemy takes damage from a target it cannot currently see
///          (attacked from behind / out of sight).
///   • Retract: enemy pulls into its shell (wind-up, RetractDuration seconds).
///   • Spinning: charges at high speed in the locked direction dealing contact
///               damage every SpinDamageInterval seconds.
///   • Crash (wall hit OR max distance reached): enemy pops out of shell, lies
///     Dazed on the ground (fully vulnerable) for DazedDuration seconds.
///   • After Dazed: returns to Idle and repeats.
/// </summary>
[RequireComponent(typeof(CombatantStats))]
public class ShellSpinnerEnemy : MonoBehaviour
{
    private enum SpinnerState { Idle, Approach, Retract, Spinning, Dazed }

    [SerializeField] private ShellSpinnerEnemySettings settings;

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
    private float        stateTimer;

    // ── Spin data ──────────────────────────────────────────────────────────────
    private Vector3 spinDirection;
    private Vector3 spinStartPos;
    private bool    hitWallDuringSpin;
    private float   lastSpinDamageTime;

    // ── Physics (horizontal velocity set in Update, applied in FixedUpdate) ────
    private Vector3 frameVelocity;

    // ── NavMesh ────────────────────────────────────────────────────────────────
    private NavMeshPath navPath;
    private int         navCornerIndex;
    private bool        hasNavPath;
    private float       nextNavRepathTime;
    private Vector3     navLastDestination;

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

        if (settings != null && settings.IgnoreCollisionMask != 0)
        {
            int myLayer = gameObject.layer;
            for (int i = 0; i < 32; i++)
            {
                if ((settings.IgnoreCollisionMask.value & (1 << i)) != 0)
                    Physics.IgnoreLayerCollision(myLayer, i, true);
            }
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

    private void FixedUpdate()
    {
        if (rb == null || stats == null || stats.IsDead) return;
        rb.linearVelocity = new Vector3(frameVelocity.x, rb.linearVelocity.y, frameVelocity.z);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (currentState == SpinnerState.Spinning && settings != null)
        {
            if ((settings.WallMask.value & (1 << collision.gameObject.layer)) != 0)
                hitWallDuringSpin = true;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Damage-taken reaction
    // ──────────────────────────────────────────────────────────────────────────

    private void OnDamageTaken(float _)
    {
        // If hit while calm and the attacker is not in sight → retract and spin.
        if (currentState == SpinnerState.Idle || currentState == SpinnerState.Approach)
        {
            if (currentTarget == null || !HasLineOfSight(currentTarget))
            {
                Log("Damaged from blind spot — retracting into shell!");
                BeginRetract();
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Target tracking
    // ──────────────────────────────────────────────────────────────────────────

    private void RefreshTarget()
    {
        float forgetRadius = settings != null ? settings.ForgetRadius : 16f;

        if (currentTarget != null)
        {
            CombatantStats ts = currentTarget.GetComponentInParent<CombatantStats>();
            bool dead = ts != null && ts.IsDead;
            bool far  = HorizontalDistance(currentTarget.position) > forgetRadius;

            if (dead || far || !currentTarget.gameObject.activeInHierarchy)
            {
                Log($"Target lost: {currentTarget.name}");
                currentTarget = null;
                ResetNavPath();
            }
        }

        if (currentTarget != null) return;

        bool hadTarget = false; // We only reach here when currentTarget is null.
        Transform found = FindBestTarget();

        // Newly spotted target while calm → retract and spin.
        if (found != null && !hadTarget &&
            (currentState == SpinnerState.Idle || currentState == SpinnerState.Approach))
        {
            currentTarget = found;
            Log($"Target spotted: {found.name} — retracting into shell!");
            BeginRetract();
            return;
        }

        if (found != null && currentTarget == null)
            Log($"Target acquired: {found.name}");

        currentTarget = found;
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

            Transform t = col.transform;
            if (!t.gameObject.activeInHierarchy) continue;

            CombatantStats cs = t.GetComponentInParent<CombatantStats>();
            if (cs != null && cs.IsDead) continue;

            bool isPlayer = t.CompareTag(settings.PlayerTag);
            bool isMinion = t.CompareTag(settings.MinionTag);
            if (!isPlayer && !isMinion) continue;

            if (settings.RequireLOSToDetect && !HasLineOfSight(t)) continue;

            float sq = (t.position - transform.position).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = t; }
        }

        return best;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  State machine
    // ──────────────────────────────────────────────────────────────────────────

    private void UpdateState()
    {
        // These states self-terminate.
        if (currentState == SpinnerState.Retract ||
            currentState == SpinnerState.Spinning ||
            currentState == SpinnerState.Dazed) return;

        SetState(currentTarget != null ? SpinnerState.Approach : SpinnerState.Idle);
    }

    private void ExecuteState()
    {
        switch (currentState)
        {
            case SpinnerState.Idle: break;

            case SpinnerState.Approach:
            {
                if (currentTarget == null) break;
                MoveTowards(currentTarget.position, 1.5f);
                break;
            }

            case SpinnerState.Retract:
            {
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    // Lock spin direction toward target (or forward if target lost).
                    Vector3 dir = currentTarget != null
                        ? currentTarget.position - transform.position
                        : transform.forward;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

                    spinDirection      = dir.normalized;
                    spinStartPos       = transform.position;
                    hitWallDuringSpin  = false;
                    lastSpinDamageTime = Time.time;
                    SetState(SpinnerState.Spinning);
                }
                break;
            }

            case SpinnerState.Spinning:
            {
                float traveled   = HorizontalDistance(spinStartPos);
                bool  reachedMax = traveled >= settings.SpinMaxDistance;

                if (hitWallDuringSpin || reachedMax)
                {
                    BeginDazed();
                    break;
                }

                frameVelocity = spinDirection * settings.SpinSpeed;
                FaceTowards(spinDirection);

                // Damage tick.
                if (Time.time >= lastSpinDamageTime + settings.SpinDamageInterval)
                {
                    lastSpinDamageTime = Time.time;
                    ApplySpinDamage();
                }
                break;
            }

            case SpinnerState.Dazed:
            {
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    SetState(SpinnerState.Idle);
                    ResetNavPath();
                    Log("Recovered from daze — back to Idle");
                }
                break;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Spin helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void BeginRetract()
    {
        if (currentState == SpinnerState.Retract  ||
            currentState == SpinnerState.Spinning  ||
            currentState == SpinnerState.Dazed) return;

        stateTimer   = settings != null ? settings.RetractDuration : 0.5f;
        SetState(SpinnerState.Retract);
        ResetNavPath();
    }

    private void BeginDazed()
    {
        frameVelocity = Vector3.zero;
        if (rb != null) rb.linearVelocity = Vector3.zero;
        stateTimer   = settings != null ? settings.DazedDuration : 3f;
        Log($"Crashed! Entering Dazed for {stateTimer}s");
        SetState(SpinnerState.Dazed);
    }

    private void ApplySpinDamage()
    {
        if (settings == null) return;

        float damage = settings.SpinDamagePerSecond * settings.SpinDamageInterval;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, settings.SpinHitRadius, overlapBuffer, settings.DetectMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;

            bool valid = col.CompareTag(settings.PlayerTag) || col.CompareTag(settings.MinionTag);
            if (!valid) continue;

            CombatantStats ts = col.GetComponentInParent<CombatantStats>();
            if (ts != null && !ts.IsDead)
            {
                ts.ApplyDamage(damage);
                Log($"Spin hit {col.name} for {damage:F1} damage");
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Movement & navigation
    // ──────────────────────────────────────────────────────────────────────────

    private void MoveTowards(Vector3 targetPos, float stopDistance)
    {
        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        if (dist <= stopDistance) { ResetNavPath(); return; }

        float speed = settings != null ? settings.MoveSpeed : 2.5f;

        if (settings != null && settings.UseNavMesh)
        {
            if (TryMoveAlongNavPath(targetPos, stopDistance, speed)) return;
        }

        Vector3 dir   = toTarget / Mathf.Max(dist, 0.0001f);
        frameVelocity = dir * speed;
        SmoothFaceDirection(dir);
    }

    private bool TryMoveAlongNavPath(Vector3 destination, float stopDistance, float speed)
    {
        float now             = Time.time;
        bool  destinationMoved = (navLastDestination - destination).sqrMagnitude > 0.35f * 0.35f;

        if (!hasNavPath || now >= nextNavRepathTime || destinationMoved)
        {
            if (!TryBuildNavPath(destination)) return false;
        }

        Vector3[] corners = navPath.corners;
        if (corners == null || corners.Length == 0) { hasNavPath = false; return false; }

        float tolerance = settings != null ? settings.NavWaypointTolerance : 0.3f;
        navCornerIndex  = Mathf.Clamp(navCornerIndex, 1, corners.Length - 1);

        while (navCornerIndex < corners.Length)
        {
            Vector3 toCorner = corners[navCornerIndex] - transform.position;
            toCorner.y = 0f;

            if (toCorner.sqrMagnitude <= tolerance * tolerance) { navCornerIndex++; continue; }

            if (navCornerIndex == corners.Length - 1 && toCorner.magnitude <= stopDistance)
            { ResetNavPath(); return true; }

            frameVelocity = toCorner.normalized * speed;
            SmoothFaceDirection(toCorner.normalized);
            return true;
        }

        ResetNavPath();
        return true;
    }

    private bool TryBuildNavPath(Vector3 destination)
    {
        if (navPath == null) navPath = new NavMeshPath();

        float repathInterval = settings != null ? settings.NavRepathInterval : 0.3f;
        nextNavRepathTime = Time.time + repathInterval;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 1.5f, NavMesh.AllAreas))
        { hasNavPath = false; return false; }

        float sampleRadius = settings != null ? settings.NavTargetSampleRadius : 1.5f;
        if (!NavMesh.SamplePosition(destination, out NavMeshHit destHit, sampleRadius, NavMesh.AllAreas))
        { hasNavPath = false; return false; }

        bool ok = NavMesh.CalculatePath(startHit.position, destHit.position, NavMesh.AllAreas, navPath);
        if (!ok || navPath.status == NavMeshPathStatus.PathInvalid ||
            navPath.corners == null || navPath.corners.Length < 2)
        { hasNavPath = false; return false; }

        hasNavPath         = true;
        navCornerIndex     = 1;
        navLastDestination = destination;
        return true;
    }

    private void ResetNavPath() { hasNavPath = false; navCornerIndex = 0; nextNavRepathTime = 0f; }

    // ──────────────────────────────────────────────────────────────────────────
    //  Rotation helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void FaceTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f) SmoothFaceDirection(direction.normalized);
    }

    private void SmoothFaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        float      rs        = settings != null ? settings.RotationSpeed : 6f;
        Quaternion targetRot = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, rs * Time.deltaTime);
    }

    private float HorizontalDistance(Vector3 pos)
    {
        Vector3 d = pos - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Line of sight
    // ──────────────────────────────────────────────────────────────────────────

    private bool HasLineOfSight(Transform target)
    {
        if (target == null || settings == null) return false;

        float   h     = settings.LosHeightOffset;
        Vector3 start = transform.position + Vector3.up * h;
        Vector3 end   = target.position    + Vector3.up * h;
        Vector3 dir   = end - start;
        float   dist  = dir.magnitude;

        if (dist <= 0.0001f) return true;

        if (Physics.Raycast(start, dir / dist, out RaycastHit hit, dist, settings.LosBlockMask, QueryTriggerInteraction.Ignore))
            return hit.transform == target || hit.transform.IsChildOf(target);

        return true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Logging & state helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void SetState(SpinnerState newState)
    {
        if (newState == currentState) return;
        Log($"State: {currentState} → {newState}");
        currentState = newState;
    }

    private void Log(string msg)
    {
        if (enableLogs) Debug.Log($"[ShellSpinner:{name}] {msg}");
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Death
    // ──────────────────────────────────────────────────────────────────────────

    private void OnDied() => Destroy(gameObject);
}
