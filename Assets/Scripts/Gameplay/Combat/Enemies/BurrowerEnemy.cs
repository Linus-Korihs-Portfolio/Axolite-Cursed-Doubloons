using UnityEngine;

/// <summary>
/// Enemy 3 — Burrower (ground-trap / aerial grabber).
///
/// Full behaviour loop:
///   1. BURROWED  — hidden underground; a body-part sticks out.
///      When a player or minion walks within SnapRadius → snap (damage) → Triggered.
///      After re-burrowing from low HP, auto-emerge after BurrowRestDuration.
///
///   2. TRIGGERED — brief snap pause, then Emerging.
///
///   3. EMERGING  — rises vertically to spawn Y level, then FlyingUp.
///
///   4. FLYING UP — ascends to HoverHeight, then Hovering.
///
///   5. HOVERING  — scans for closest target.
///      • Minion found → sets grab target, switches to Diving (grab dive).
///      • Player / any other target → switches to Diving (attack dive).
///
///   6. DIVING    — descends toward target at DiveSpeed.
///      On reaching DiveHitDistance:
///      • Minion target → GrabbingMinion.
///      • Player target → deal DiveDamage, Landed.
///
///   7. GRABBING MINION — ascends to CarryHeight applying GrabDamagePerSecond.
///      After GrabDuration: release minion (it is dead or near-dead).
///      Then: check HP → BurrowingDown (below threshold) or FlyingUp (repeat).
///
///   8. LANDED    — short stun on the ground after hitting a player.
///      After DiveLandedDuration: check HP → BurrowingDown or FlyingUp.
///
///   9. BURROWING DOWN — descends to burrowedY (BurrowDepth below spawn),
///      then Burrowed (rest timer).
///
/// </summary>
[RequireComponent(typeof(CombatantStats))]
public class BurrowerEnemy : MonoBehaviour
{
    private enum BurrowerState
    {
        Burrowed,       // Underground, waiting for snap trigger
        Triggered,      // Snap pause before emerging
        Emerging,       // Rising out of the ground
        FlyingUp,       // Ascending to hover height
        Hovering,       // Scanning for target
        Diving,         // Descending toward target
        GrabbingMinion, // Ascending while holding a grabbed minion
        Landed,         // Stunned on ground after hitting player
        BurrowingDown   // Descending back underground
    }

    [SerializeField] private BurrowerEnemySettings settings;

    [Header("Debug")]
    [SerializeField] private bool enableLogs;

    // ── Core references ────────────────────────────────────────────────────────
    private CombatantStats stats;
    private Rigidbody       rb;

    // ── State machine ──────────────────────────────────────────────────────────
    [Header("Runtime (Read Only)")]
    [SerializeField] private BurrowerState currentState = BurrowerState.Burrowed;
    private float         stateTimer;

    // ── Spawn / ground reference ───────────────────────────────────────────────
    private float spawnY;       // Y position at spawn (ground level)
    private float burrowedY;    // Y position when fully underground

    // ── Flight & grab data ─────────────────────────────────────────────────────
    private Transform      diveTarget;
    private bool           divingAtMinion;
    private CombatantStats grabbedMinionStats;
    private Transform      grabbedMinionTransform;
    private float          grabTimer;

    // ── Re-emerge flag ────────────────────────────────────────────────────────
    private bool isInitialBurrow = true; // true = wait for snap; false = auto-emerge after rest

    // ── Physics (full 3D velocity set in Update, applied in FixedUpdate) ───────
    private Vector3 frame3DVelocity;

    private static readonly Collider[] overlapBuffer = new Collider[32];

    // ──────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        stats       = GetComponent<CombatantStats>();
        stats.Died += OnDied;

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity  = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        spawnY    = transform.position.y;
        burrowedY = spawnY - (settings != null ? settings.BurrowDepth : 1.5f);

        // Start fully underground.
        Vector3 p = transform.position;
        p.y = burrowedY;
        transform.position = p;
    }

    private void OnDestroy()
    {
        if (stats != null) stats.Died -= OnDied;
    }

    private void Update()
    {
        if (stats.IsDead) return;

        frame3DVelocity = Vector3.zero;
        ExecuteState();
    }

    private void FixedUpdate()
    {
        if (rb == null || stats == null || stats.IsDead) return;
        rb.linearVelocity = frame3DVelocity;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  State execution
    // ──────────────────────────────────────────────────────────────────────────

    private void ExecuteState()
    {
        switch (currentState)
        {
            // ── Burrowed ──────────────────────────────────────────────────────
            case BurrowerState.Burrowed:
            {
                PinToGround();

                if (isInitialBurrow)
                {
                    // Wait for a player or minion to step on the snap trigger.
                    CheckSnapTrigger();
                }
                else
                {
                    // Automatic re-emerge after rest period.
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        isInitialBurrow = false;
                        SetState(BurrowerState.Emerging);
                    }
                }
                break;
            }

            // ── Triggered ─────────────────────────────────────────────────────
            case BurrowerState.Triggered:
            {
                PinToGround();
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    SetState(BurrowerState.Emerging);
                }
                break;
            }

            // ── Emerging ──────────────────────────────────────────────────────
            case BurrowerState.Emerging:
            {
                float targetY = spawnY;
                float diff    = targetY - transform.position.y;

                if (diff <= 0.05f)
                {
                    SnapYTo(targetY);
                    SetState(BurrowerState.FlyingUp);
                    break;
                }

                float speed = settings != null ? settings.EmergeRiseSpeed : 5f;
                frame3DVelocity = new Vector3(0f, speed, 0f);
                break;
            }

            // ── Flying Up ─────────────────────────────────────────────────────
            case BurrowerState.FlyingUp:
            {
                float hoverY  = spawnY + (settings != null ? settings.HoverHeight : 5f);
                float diff    = hoverY - transform.position.y;

                if (diff <= 0.05f)
                {
                    SnapYTo(hoverY);
                    SetState(BurrowerState.Hovering);
                    break;
                }

                float speed = settings != null ? settings.FlySpeed : 7f;
                frame3DVelocity = new Vector3(0f, speed, 0f);
                break;
            }

            // ── Hovering ──────────────────────────────────────────────────────
            case BurrowerState.Hovering:
            {
                // Hover in place and search for the closest target.
                diveTarget = FindClosestTarget();

                if (diveTarget != null)
                {
                    divingAtMinion = diveTarget.CompareTag(settings != null ? settings.MinionTag : "Ally");
                    Log($"Target found: {diveTarget.name} (diving at minion: {divingAtMinion})");
                    SetState(BurrowerState.Diving);
                }
                break;
            }

            // ── Diving ────────────────────────────────────────────────────────
            case BurrowerState.Diving:
            {
                if (diveTarget == null || IsDead(diveTarget))
                {
                    Log("Dive target gone — flying back up");
                    SetState(BurrowerState.FlyingUp);
                    break;
                }

                float speed      = divingAtMinion
                    ? (settings != null ? settings.GrabDescentSpeed : 8f)
                    : (settings != null ? settings.DiveSpeed : 9f);
                float hitDist    = settings != null ? settings.DiveHitDistance : 1.2f;

                // Move toward the target (both horizontally and vertically).
                Vector3 toTarget = diveTarget.position - transform.position;
                float   dist     = toTarget.magnitude;

                if (dist <= hitDist)
                {
                    OnDiveContact();
                    break;
                }

                Vector3 dir     = toTarget / Mathf.Max(dist, 0.0001f);
                frame3DVelocity = dir * speed;
                SmoothFaceDirection(new Vector3(dir.x, 0f, dir.z));
                break;
            }

            // ── Grabbing Minion ───────────────────────────────────────────────
            case BurrowerState.GrabbingMinion:
            {
                float carryY = spawnY + (settings != null ? settings.CarryHeight : 6f);
                float speed  = settings != null ? settings.FlySpeed : 7f;

                // Ascend toward carry height.
                float yDiff = carryY - transform.position.y;
                float yVel  = Mathf.Sign(yDiff) * speed;
                if (Mathf.Abs(yDiff) < 0.05f) { yVel = 0f; SnapYTo(carryY); }
                frame3DVelocity = new Vector3(0f, yVel, 0f);

                // Apply grab damage to the minion.
                grabTimer -= Time.deltaTime;
                if (grabbedMinionStats != null && !grabbedMinionStats.IsDead)
                {
                    float dps = settings != null ? settings.GrabDamagePerSecond : 20f;
                    grabbedMinionStats.ApplyDamage(dps * Time.deltaTime);
                }

                // Release after grab duration or when minion dies.
                bool minionDead = grabbedMinionStats == null || grabbedMinionStats.IsDead;
                if (grabTimer <= 0f || minionDead)
                {
                    Log(minionDead ? "Grabbed minion died — releasing" : "Grab duration ended — releasing minion");
                    ReleaseGrabbedMinion();
                    TransitionAfterAttack();
                }
                break;
            }

            // ── Landed ────────────────────────────────────────────────────────
            case BurrowerState.Landed:
            {
                // Re-enable gravity so the enemy actually rests on the ground.
                if (rb != null) rb.useGravity = true;

                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    if (rb != null) rb.useGravity = false;
                    Log("Recovered from landing stun");
                    TransitionAfterAttack();
                }
                break;
            }

            // ── Burrowing Down ────────────────────────────────────────────────
            case BurrowerState.BurrowingDown:
            {
                float diff  = transform.position.y - burrowedY;
                float speed = settings != null ? settings.BurrowDescentSpeed : 5f;

                if (diff <= 0.05f)
                {
                    SnapYTo(burrowedY);
                    isInitialBurrow = false;
                    stateTimer      = settings != null ? settings.BurrowRestDuration : 3f;
                    Log($"Fully burrowed. Resting for {stateTimer}s");
                    SetState(BurrowerState.Burrowed);
                    break;
                }

                frame3DVelocity = new Vector3(0f, -speed, 0f);
                break;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Snap trigger (while burrowed)
    // ──────────────────────────────────────────────────────────────────────────

    private void CheckSnapTrigger()
    {
        if (settings == null) return;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, settings.SnapRadius, overlapBuffer, settings.DetectMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;

            bool valid = col.CompareTag(settings.PlayerTag) || col.CompareTag(settings.MinionTag);
            if (!valid) continue;

            CombatantStats ts = col.GetComponentInParent<CombatantStats>();
            if (ts != null && !ts.IsDead)
                ts.ApplyDamage(settings.SnapDamage);

            Log($"Snap triggered by {col.name} — dealing {settings.SnapDamage} damage");
            stateTimer   = settings.SnapPauseDuration;
            SetState(BurrowerState.Triggered);
            break;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Dive contact resolution
    // ──────────────────────────────────────────────────────────────────────────

    private void OnDiveContact()
    {
        if (divingAtMinion && diveTarget != null && !IsDead(diveTarget))
        {
            // Grab the minion.
            grabbedMinionStats    = diveTarget.GetComponentInParent<CombatantStats>();
            grabbedMinionTransform = diveTarget;
            grabTimer             = settings != null ? settings.GrabDuration : 4f;
            Log($"Grabbed minion: {diveTarget.name}");
            SetState(BurrowerState.GrabbingMinion);
        }
        else
        {
            // Dive-attack the player (or any non-minion target).
            if (diveTarget != null)
            {
                CombatantStats ts = diveTarget.GetComponentInParent<CombatantStats>();
                if (ts != null && !ts.IsDead)
                {
                    ts.ApplyDamage(settings != null ? settings.DiveDamage : 12f);
                    Log($"Dive hit {diveTarget.name} for {settings?.DiveDamage ?? 12f} damage");
                }
            }

            stateTimer   = settings != null ? settings.DiveLandedDuration : 0.8f;
            SetState(BurrowerState.Landed);
        }

        diveTarget = null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Post-attack HP check
    // ──────────────────────────────────────────────────────────────────────────

    private void TransitionAfterAttack()
    {
        float threshold = settings != null ? settings.BurrowHealthThreshold : 0.3f;
        float hpRatio   = stats.CurrentHealth / Mathf.Max(stats.GetStat(CombatStatType.MaxHealth), 1f);

        if (hpRatio <= threshold)
        {
            Log($"HP below threshold ({hpRatio:P0}) — burrowing down!");
            SetState(BurrowerState.BurrowingDown);
        }
        else
        {
            SetState(BurrowerState.FlyingUp);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Grab helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void ReleaseGrabbedMinion()
    {
        grabbedMinionStats    = null;
        grabbedMinionTransform = null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Target finding
    // ──────────────────────────────────────────────────────────────────────────

    private Transform FindClosestTarget()
    {
        if (settings == null) return null;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, settings.DetectRadius, overlapBuffer, settings.DetectMask, QueryTriggerInteraction.Ignore);

        // Prefer minions as grab targets; fall back to player.
        Transform bestMinion = null;
        float     bestMinionSq = float.PositiveInfinity;
        Transform bestOther  = null;
        float     bestOtherSq = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;

            Transform t = col.transform;
            if (!t.gameObject.activeInHierarchy) continue;

            CombatantStats cs = t.GetComponentInParent<CombatantStats>();
            if (cs != null && cs.IsDead) continue;

            bool isMinion = t.CompareTag(settings.MinionTag);
            bool isPlayer = t.CompareTag(settings.PlayerTag);
            if (!isMinion && !isPlayer) continue;

            float sq = (t.position - transform.position).sqrMagnitude;

            if (isMinion && sq < bestMinionSq) { bestMinionSq = sq; bestMinion = t; }
            else if (isPlayer && sq < bestOtherSq) { bestOtherSq = sq; bestOther = t; }
        }

        // Prefer minions for grabbing; attack player if no minion is available.
        return bestMinion != null ? bestMinion : bestOther;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Utility
    // ──────────────────────────────────────────────────────────────────────────

    private void PinToGround()
    {
        Vector3 p = transform.position;
        p.y = burrowedY;
        transform.position = p;
        frame3DVelocity    = Vector3.zero;
    }

    private void SnapYTo(float y)
    {
        Vector3 p = transform.position;
        p.y = y;
        transform.position = p;
    }

    private bool IsDead(Transform t)
    {
        CombatantStats cs = t.GetComponentInParent<CombatantStats>();
        return cs == null || cs.IsDead;
    }

    private void SmoothFaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        float      rs        = settings != null ? settings.RotationSpeed : 8f;
        Quaternion targetRot = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, rs * Time.deltaTime);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Logging & state helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void SetState(BurrowerState newState)
    {
        if (newState == currentState) return;
        Log($"State: {currentState} → {newState}");
        currentState = newState;
    }

    private void Log(string msg)
    {
        if (enableLogs) Debug.Log($"[Burrower:{name}] {msg}");
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Death
    // ──────────────────────────────────────────────────────────────────────────

    private void OnDied() => Destroy(gameObject);
}
