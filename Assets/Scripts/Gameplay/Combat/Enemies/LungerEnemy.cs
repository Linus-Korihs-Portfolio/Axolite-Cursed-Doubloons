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

    // ── Behaviour flags ────────────────────────────────────────────────────────
    private bool isAsleep = true;            // stands still until hit or player spotted
    private bool hasBeenAttacked;            // set true on first damage hit
    private bool hasLungedThisEncounter;     // blocks re-lunge until target is fully lost

    // ── Lunge data ─────────────────────────────────────────────────────────────
    private Vector3 lungeDirection;
    private Vector3 lungeStartPos;
    private float   lungeDistance;           // dist-to-target + overshoot, locked at windup
    private bool    hitWallDuringLunge;
    private float   lastLungeTime = -999f;
    // Each instanceID added here has already taken lunge damage this sweep.
    private readonly System.Collections.Generic.HashSet<int> lungeHitIds =
        new System.Collections.Generic.HashSet<int>();

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
        if (currentState != LungerState.Lunging || settings == null) return;
        if ((settings.WallMask.value & (1 << collision.gameObject.layer)) == 0) return;

        // Targets are handled by sweep damage — don't stop the lunge on their colliders.
        Transform root = collision.transform.root;
        if (root.CompareTag(settings.PlayerTag) || collision.transform.CompareTag(settings.MinionTag)) return;

        hitWallDuringLunge = true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Target tracking
    // ──────────────────────────────────────────────────────────────────────────

    // Searches the transform itself, then up, then down — handles any hierarchy layout.
    private static CombatantStats GetStats(Transform t)
    {
        if (t == null) return null;
        return t.GetComponent<CombatantStats>()
            ?? t.GetComponentInParent<CombatantStats>()
            ?? t.GetComponentInChildren<CombatantStats>();
    }

    private void OnDamageTaken(float _)
    {
        hasBeenAttacked = true;
        isAsleep        = false;
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
            {
                Log($"Target lost: {currentTarget.name}");
                currentTarget          = null;
                hasLastKnownPos        = false;
                hasLungedThisEncounter = false; // new encounter → lunge allowed again
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

        // Asleep and never hit: only wake on direct player sight.
        if (isAsleep && !hasBeenAttacked)
        {
            Transform player = FindPlayerInSight();
            if (player != null)
            {
                isAsleep      = false;
                currentTarget = player;
                Log($"Player spotted — waking, lunging at: {player.name}");
                BeginPreLunge();
            }
            return;
        }

        // Woken by damage (isAsleep was cleared by OnDamageTaken): target nearest threat.
        if (currentTarget == null)
        {
            Transform found = FindBestTarget();
            if (found != null) Log($"Target acquired: {found.name}");
            currentTarget = found;
        }
    }

    // Returns the player root transform if visible; null otherwise.
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

            return root;
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

            Transform t    = col.transform;
            Transform root = t.root;
            if (!root.gameObject.activeInHierarchy) continue;

            CombatantStats cs = GetStats(t);
            if (cs != null && cs.IsDead) continue;

            // Check both the collider's own tag and the root tag to handle nested hierarchies.
            bool isPlayer = t.CompareTag(settings.PlayerTag) || root.CompareTag(settings.PlayerTag);
            bool isMinion = t.CompareTag(settings.MinionTag);
            if (!isPlayer && !isMinion) continue;

            if (settings.RequireLOSToDetect && !HasLineOfSight(t)) continue;

            float sq = (root.position - transform.position).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = root; }
        }

        return best;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  State machine
    // ──────────────────────────────────────────────────────────────────────────

    private void UpdateState()
    {
        // Sleeping: do nothing.
        if (isAsleep) { SetState(LungerState.Idle); return; }

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

        // Beyond lunge threshold AND haven't lunged this encounter → lunge.
        if (!hasLungedThisEncounter && dist > settings.LungeMinDistance)
        {
            BeginPreLunge();
            if (currentState != LungerState.PreLunge)
                SetState(LungerState.Approach);
            return;
        }

        // Post-lunge or within lunge threshold: close in to bite.
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
                    hitWallDuringLunge = false;
                    SetState(LungerState.Lunging); // lungeStartPos + lungeHitIds reset inside SetState
                }
                break;
            }

            case LungerState.Lunging:
            {
                // Deal damage to every player/minion the body overlaps (once per target this lunge).
                DealLungeSweepDamage();

                float traveled   = HorizontalDistance(lungeStartPos);
                bool  reachedEnd = traveled >= lungeDistance;

                if (hitWallDuringLunge || reachedEnd)
                {
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
                    // After recovery bite loop: find nearest alive threat and close in.
                    if (currentTarget == null) currentTarget = FindBestTarget();
                    SetState(currentTarget != null ? LungerState.Approach : LungerState.Idle);
                }
                break;
            }

            case LungerState.Bite:
            {
                if (currentTarget == null) { SetState(LungerState.Idle); break; }

                // Target walked out of range — close in.
                if (HorizontalDistance(currentTarget.position) > settings.BiteRange)
                {
                    SetState(LungerState.Approach);
                    break;
                }

                FaceTarget();

                // LOS blocked — approach until visible again.
                if (settings.RequireLOSToAttack && !HasLineOfSight(currentTarget))
                {
                    SetState(LungerState.Approach);
                    break;
                }

                if (Time.time >= lastBiteTime + settings.BiteCooldown)
                {
                    lastBiteTime = Time.time;
                    CombatantStats ts = GetStats(currentTarget);
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
        // Lunge exactly to target + overshoot so the player must sidestep.
        float overshoot = settings != null ? settings.LungeOvershootDistance : 2f;
        lungeDistance  = dir.magnitude + overshoot;
        lastLungeTime  = Time.time;
        stateTimer     = settings != null ? settings.LungeWindupDuration : 0.4f;
        SetState(LungerState.PreLunge);
    }

    private void BeginRecovery()
    {
        frameVelocity          = Vector3.zero;
        hasLungedThisEncounter = true;
        stateTimer             = settings != null ? settings.LungeRecoveryDuration : 1.2f;
        SetState(LungerState.Recovering);
    }

    // Deals lunge damage to every player/minion currently overlapping the body hitbox.
    // Uses lungeHitIds so each target is damaged at most once per lunge sweep.
    private void DealLungeSweepDamage()
    {
        if (settings == null) return;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, settings.LungeHitRadius, overlapBuffer, settings.DetectMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;

            Transform root = col.transform.root;
            bool isPlayer  = col.transform.CompareTag(settings.PlayerTag) || root.CompareTag(settings.PlayerTag);
            bool isMinion  = col.transform.CompareTag(settings.MinionTag);
            if (!isPlayer && !isMinion) continue;

            int id = root.GetInstanceID();
            if (lungeHitIds.Contains(id)) continue; // already hit this target this lunge

            CombatantStats ts = GetStats(col.transform);
            if (ts != null && !ts.IsDead)
            {
                lungeHitIds.Add(id);
                ts.ApplyDamage(settings.LungeDamage);
                Log($"Lunge sweep hit {root.name} for {settings.LungeDamage} damage");
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
        // Only repath when the target has moved significantly, to prevent per-frame path recalculation wobble.
        bool  destinationMoved = (navLastDestination - destination).sqrMagnitude > 2f * 2f;

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

            Vector3 moveDir = toCorner.normalized;

            // Blend facing toward the next corner early to smooth out sharp turns.
            if (navCornerIndex + 1 < corners.Length)
            {
                float blend = 1f - Mathf.Clamp01(toCorner.magnitude / 1.5f);
                Vector3 toNext = corners[navCornerIndex + 1] - transform.position;
                toNext.y = 0f;
                if (toNext.sqrMagnitude > 0.01f)
                    moveDir = Vector3.Slerp(moveDir, toNext.normalized, blend).normalized;
            }

            frameVelocity = toCorner.normalized * speed;
            SmoothFaceDirection(moveDir);
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
        if (newState == LungerState.Lunging)
        {
            lungeStartPos = transform.position;
            lungeHitIds.Clear();
        }
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
