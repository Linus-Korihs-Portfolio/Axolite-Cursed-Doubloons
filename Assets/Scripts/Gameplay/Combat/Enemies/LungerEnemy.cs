using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy 1 — Lunger (Goomba-like).
///
/// Attacks:
///   • Bite  — short-range melee hit on the current target.
///   • Lunge — triggered on first detection (before being attacked), OR whenever the
///             target is beyond <see cref="LungerEnemySettings.LungeMinDistance"/>.
///             The enemy winds up briefly, charges forward at high speed, deals
///             impact damage at the landing zone, then recovers before biting again.
/// </summary>
[RequireComponent(typeof(CombatantStats))]
public class LungerEnemy : MonoBehaviour
{
    private enum LungerState { Idle, Approach, PreLunge, Lunging, Recovering, Bite }

    [SerializeField] private LungerEnemySettings settings;

    [Header("Debug")]
    [SerializeField] private bool enableLogs;

    // ── Core references ────────────────────────────────────────────────────────
    private CombatantStats stats;
    private Rigidbody       rb;

    // ── Target tracking ────────────────────────────────────────────────────────
    private Transform currentTarget;
    private Vector3   lastKnownTargetPos;
    private bool      hasLastKnownPos;

    // ── State machine ──────────────────────────────────────────────────────────
    [Header("Runtime (Read Only)")]
    [SerializeField] private LungerState currentState = LungerState.Idle;
    private float       stateTimer;

    // ── Lunge data ─────────────────────────────────────────────────────────────
    private Vector3 lungeDirection;
    private Vector3 lungeStartPos;
    private bool    hitWallDuringLunge;
    private float   lastLungeTime = -999f;

    // ── Attack history ─────────────────────────────────────────────────────────
    private bool hasBeenAttacked;    // true once this enemy has taken any damage
    private bool hasUsedOpenerLunge; // true once the first-detection lunge fires

    // ── Bite cooldown ──────────────────────────────────────────────────────────
    private float lastBiteTime = -999f;

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
        stats.DamageTaken += _ => hasBeenAttacked = true;

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
            stats.DamageTaken -= _ => hasBeenAttacked = true;
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
        if (currentState == LungerState.Lunging && settings != null)
        {
            if ((settings.WallMask.value & (1 << collision.gameObject.layer)) != 0)
                hitWallDuringLunge = true;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Target tracking
    // ──────────────────────────────────────────────────────────────────────────

    private void RefreshTarget()
    {
        float forgetRadius = settings != null ? settings.ForgetRadius : 18f;

        // Drop stale / dead targets.
        if (currentTarget != null)
        {
            CombatantStats ts = currentTarget.GetComponentInParent<CombatantStats>();
            bool dead = ts != null && ts.IsDead;
            bool far  = HorizontalDistance(currentTarget.position) > forgetRadius;

            if (dead || far || !currentTarget.gameObject.activeInHierarchy)
            {
                Log($"Target lost: {currentTarget.name}");
                currentTarget   = null;
                hasLastKnownPos = false;
                ResetNavPath();
            }
        }

        // Update last known position while we have LOS.
        if (currentTarget != null && HasLineOfSight(currentTarget))
        {
            lastKnownTargetPos = currentTarget.position;
            hasLastKnownPos    = true;
        }

        if (currentTarget != null) return;

        // Search for a new target.
        Transform found = FindBestTarget();

        // Opener lunge: first detection before this enemy has been hit.
        if (found != null && !hasBeenAttacked && !hasUsedOpenerLunge)
        {
            hasUsedOpenerLunge = true;
            currentTarget      = found;
            Log($"Opener lunge triggered — first target spotted: {found.name}");
            BeginPreLunge();
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
        // These states self-terminate via their own timers or events.
        if (currentState == LungerState.PreLunge  ||
            currentState == LungerState.Lunging    ||
            currentState == LungerState.Recovering) return;

        if (currentTarget == null)
        {
            SetState(LungerState.Idle);
            return;
        }

        float dist = HorizontalDistance(currentTarget.position);

        // Within bite range → bite.
        if (dist <= settings.BiteRange)
        {
            SetState(LungerState.Bite);
            return;
        }

        // Beyond lunge threshold → attempt lunge (has cooldown guard).
        if (dist > settings.LungeMinDistance)
        {
            BeginPreLunge();
            // If on cooldown, BeginPreLunge is a no-op → fall through to Approach.
            if (currentState != LungerState.PreLunge)
                SetState(LungerState.Approach);
            return;
        }

        // In-between: close in for bite.
        SetState(LungerState.Approach);
    }

    private void ExecuteState()
    {
        switch (currentState)
        {
            case LungerState.Idle: break;

            case LungerState.Approach:
            {
                if (currentTarget == null) break;
                MoveTowards(currentTarget.position, settings.BiteRange * 0.9f);
                break;
            }

            case LungerState.PreLunge:
            {
                stateTimer -= Time.deltaTime;
                FaceTowards(lungeDirection);
                if (stateTimer <= 0f)
                {
                    SetState(LungerState.Lunging);
                    lungeStartPos      = transform.position;
                    hitWallDuringLunge = false;
                }
                break;
            }

            case LungerState.Lunging:
            {
                float traveled   = HorizontalDistance(lungeStartPos);
                bool  reachedMax = traveled >= settings.LungeMaxDistance;

                if (hitWallDuringLunge || reachedMax)
                {
                    DealLungeLandingDamage();
                    BeginRecovery();
                    break;
                }

                frameVelocity = lungeDirection * settings.LungeSpeed;
                FaceTowards(lungeDirection);
                break;
            }

            case LungerState.Recovering:
            {
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    // Stand up: find the closest target and transition to approaching it.
                    currentTarget = FindBestTarget();
                    SetState(currentTarget != null ? LungerState.Approach : LungerState.Idle);
                }
                break;
            }

            case LungerState.Bite:
            {
                if (currentTarget == null) { SetState(LungerState.Idle); break; }

                FaceTarget();
                if (settings.RequireLOSToAttack && !HasLineOfSight(currentTarget)) break;

                if (Time.time >= lastBiteTime + settings.BiteCooldown)
                {
                    lastBiteTime = Time.time;
                    CombatantStats ts = currentTarget.GetComponentInParent<CombatantStats>();
                    if (ts != null && !ts.IsDead)
                    {
                        ts.ApplyDamage(settings.BiteDamage);
                        Log($"Bite hit {currentTarget.name} for {settings.BiteDamage} damage");
                    }
                }
                break;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Lunge helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void BeginPreLunge()
    {
        if (currentState == LungerState.PreLunge  ||
            currentState == LungerState.Lunging    ||
            currentState == LungerState.Recovering) return;

        if (settings != null && Time.time < lastLungeTime + settings.LungeCooldown) return;

        // Lock direction toward current target or last known position.
        Vector3 dir;
        if (currentTarget != null)
            dir = currentTarget.position - transform.position;
        else if (hasLastKnownPos)
            dir = lastKnownTargetPos - transform.position;
        else
            return;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        lungeDirection = dir.normalized;
        lastLungeTime  = Time.time;
        stateTimer     = settings != null ? settings.LungeWindupDuration : 0.4f;
        SetState(LungerState.PreLunge);
    }

    private void BeginRecovery()
    {
        frameVelocity = Vector3.zero;
        stateTimer    = settings != null ? settings.LungeRecoveryDuration : 1.2f;
        SetState(LungerState.Recovering);
    }

    private void DealLungeLandingDamage()
    {
        if (settings == null) return;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, settings.LungeHitRadius, overlapBuffer, settings.DetectMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;

            bool valid = col.CompareTag(settings.PlayerTag) || col.CompareTag(settings.MinionTag);
            if (!valid) continue;

            CombatantStats ts = col.GetComponentInParent<CombatantStats>();
            if (ts != null && !ts.IsDead)
            {
                ts.ApplyDamage(settings.LungeDamage);
                Log($"Lunge landing hit {col.name} for {settings.LungeDamage} damage");
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

        float speed = settings != null ? settings.MoveSpeed : 3.5f;

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
            {
                ResetNavPath();
                return true;
            }

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
        nextNavRepathTime    = Time.time + repathInterval;

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
        float      rs        = settings != null ? settings.RotationSpeed : 8f;
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

    private void SetState(LungerState newState)
    {
        if (newState == currentState) return;
        Log($"State: {currentState} → {newState}");
        currentState = newState;
    }

    private void Log(string msg)
    {
        if (enableLogs) Debug.Log($"[Lunger:{name}] {msg}");
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Death
    // ──────────────────────────────────────────────────────────────────────────

    private void OnDied() => Destroy(gameObject);
}
