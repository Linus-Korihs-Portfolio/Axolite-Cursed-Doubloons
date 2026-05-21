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
///   8. LANDED    — (unused for player hits; kept for future use).
///      After DiveLandedDuration: check HP → BurrowingDown or FlyingUp.
///
///   9. BURROWING DOWN — descends to burrowedY (BurrowDepth below spawn),
///      then Burrowed (rest timer).
///
/// </summary>
[RequireComponent(typeof(CombatantStats))]
public class BurrowerEnemy : MonoBehaviour, IAimTarget
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

    [Tooltip("Assign the player's actual moving transform. " +
             "Required when the player prefab root is a static anchor above the moving body.")]
    [SerializeField] private Transform playerTransform;

    [Header("Aim Point")]
    [Tooltip("The visible head / tip that sticks out of the ground when burrowed. " +
             "Projectiles will home toward this point instead of the buried root. " +
             "Leave empty to fall back to the root transform.")]
    [SerializeField] private Transform headAimPoint;

    /// <summary>
    /// Returns the head aim point while burrowed (root is underground); the root otherwise.
    /// </summary>
    public Transform GetAimTransform() =>
        (currentState == BurrowerState.Burrowed && headAimPoint != null) ? headAimPoint : transform;

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
    private MinionCore     grabbedMinionAI;   // disabled during grab so the minion stops moving
    private float          grabTimer;
    private float          postGrabDelayTimer;

    // ── Re-emerge flag ────────────────────────────────────────────────────────
    private bool isInitialBurrow = true; // true = wait for snap; false = auto-emerge after rest

    // ── Hover idle timer (no targets in range) ────────────────────────────────
    private float noTargetHoverTimer;

    // ── Physics (full 3D velocity set in Update, applied in FixedUpdate) ───────
    private Vector3 frame3DVelocity;

    // ── Dive collision bypass ─────────────────────────────────────────────────
    private Collider myCollider;          // own collider cached for Physics.IgnoreCollision
    private Collider activeDiveCollider;  // target's collider; collision is restored after dive

    private static readonly Collider[] overlapBuffer = new Collider[32];

    // ──────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // The Burrower uses manual position/velocity — NavMeshAgent must not override transform.
        var nma = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nma != null) nma.enabled = false;

        stats              = GetComponent<CombatantStats>();
        stats.Died        += OnDied;
        stats.DamageTaken += OnDamageTaken;

        rb         = GetComponent<Rigidbody>();
        myCollider = GetComponent<Collider>();
        if (rb != null)
        {
            rb.isKinematic = true;  // ground phases use direct position; toggled to false when airborne
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
        if (stats != null)
        {
            stats.Died        -= OnDied;
            stats.DamageTaken -= OnDamageTaken;
        }
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
        if (!rb.isKinematic) rb.linearVelocity = frame3DVelocity;
    }

    // Wake up immediately when damaged while burrowed.
    private void OnDamageTaken(float _)
    {
        if (currentState != BurrowerState.Burrowed) return;
        Log("Damaged while burrowed — snapping awake!");
        stateTimer = settings != null ? settings.SnapPauseDuration : 0.3f;
        SetState(BurrowerState.Triggered);
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

                // Regenerate HP while underground.
                if (settings != null && settings.HpRegenPerSecond > 0f)
                    stats.Heal(settings.HpRegenPerSecond * Time.deltaTime);

                if (isInitialBurrow)
                {
                    // Wait for a player or minion to step on the snap trigger.
                    CheckSnapTrigger();
                }
                else
                {
                    // Fallback: count down the rest timer then switch to passive-wait mode
                    // (step 1) instead of auto-emerging. Emergence only via snap or damage.
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        isInitialBurrow = true;
                        Log("Rest timer expired — resetting to step-1 (waiting for snap/damage).");
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
                Vector3 ePos = transform.position;
                ePos.y = Mathf.MoveTowards(ePos.y, targetY, speed * Time.deltaTime);
                transform.position = ePos;
                break;
            }

            // ── Flying Up ─────────────────────────────────────────────────────
            case BurrowerState.FlyingUp:
            {
                float hoverY  = spawnY + (settings != null ? settings.HoverHeight : 5f);
                float diff    = hoverY - transform.position.y;

                if (diff <= 0.05f)
                {
                    // Restore collision only here — the burrower is now at hover height and
                    // cannot be overlapping the player. Restoring it any earlier (while still
                    // near the player) causes physics depenetration that lifts the player.
                    RestoreDiveCollision();
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
                    noTargetHoverTimer = 0f;
                    divingAtMinion = diveTarget.CompareTag(settings != null ? settings.MinionTag : "Ally");
                    // Bypass physical contact so the burrower can reach DiveHitDistance.
                    if (myCollider != null)
                    {
                        activeDiveCollider = diveTarget.GetComponentInParent<Collider>();
                        if (activeDiveCollider != null)
                            Physics.IgnoreCollision(myCollider, activeDiveCollider, true);
                    }
                    Log($"Target found: {diveTarget.name} (diving at minion: {divingAtMinion})");
                    SetState(BurrowerState.Diving);
                }
                else
                {
                    // No targets in range — count down and retreat underground (full step-1 reset).
                    noTargetHoverTimer += Time.deltaTime;
                    float timeout = settings != null ? settings.NoTargetTimeout : 3f;
                    if (noTargetHoverTimer >= timeout)
                    {
                        noTargetHoverTimer = 0f;
                        Log("No targets in range — retreating underground (step-1 reset).");
                        SetState(BurrowerState.BurrowingDown);
                    }
                }
                break;
            }

            // ── Diving ────────────────────────────────────────────────────────
            case BurrowerState.Diving:
            {
                if (diveTarget == null || IsDead(diveTarget))
                {
                    Log("Dive target gone — flying back up");
                    RestoreDiveCollision();
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
                // Post-kill cooldown: hover briefly before flying off.
                if (postGrabDelayTimer > 0f)
                {
                    postGrabDelayTimer -= Time.deltaTime;
                    frame3DVelocity = Vector3.zero;
                    if (postGrabDelayTimer <= 0f) TransitionAfterAttack();
                    break;
                }

                float carryY = spawnY + (settings != null ? settings.CarryHeight : 6f);
                float speed  = settings != null ? settings.FlySpeed : 7f;

                // Ascend toward carry height.
                float yDiff = carryY - transform.position.y;
                float yVel  = Mathf.Sign(yDiff) * speed;
                if (Mathf.Abs(yDiff) < 0.05f) { yVel = 0f; SnapYTo(carryY); }
                frame3DVelocity = new Vector3(0f, yVel, 0f);

                // Drag the minion along — keep it hanging just below the burrower.
                if (grabbedMinionTransform != null)
                    grabbedMinionTransform.position = transform.position + Vector3.down * 1.5f;

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
                    if (minionDead)
                    {
                        // Brief visual pause before flying off.
                        postGrabDelayTimer = settings != null ? settings.PostGrabCooldown : 0.3f;
                        frame3DVelocity    = Vector3.zero;
                    }
                    else
                    {
                        TransitionAfterAttack();
                    }
                }
                break;
            }

            // ── Landed ────────────────────────────────────────────────────────
            case BurrowerState.Landed:
            {
                // Ensure no residual dive velocity keeps pushing into the player.
                frame3DVelocity = Vector3.zero;

                // Re-enable gravity so the enemy actually rests on the ground.
                if (rb != null) rb.useGravity = true;

                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    // Safe to restore collision now — burrower has settled, no longer overlapping player.
                    RestoreDiveCollision();
                    if (rb != null) rb.useGravity = false;
                    Log("Recovered from landing stun");
                    TransitionAfterAttack();
                }
                break;
            }

            // ── Burrowing Down ────────────────────────────────────────────────
            case BurrowerState.BurrowingDown:
            {
                // If a player-dive collision was deferred, restore it once safely underground.
                if (activeDiveCollider != null && transform.position.y < spawnY)
                    RestoreDiveCollision();

                float diff  = transform.position.y - burrowedY;
                float speed = settings != null ? settings.BurrowDescentSpeed : 5f;

                if (diff <= 0.05f)
                {
                    SnapYTo(burrowedY);
                    // Always reset to step-1 mode: wait for snap or damage, never auto-emerge.
                    isInitialBurrow = true;
                    Log("Fully burrowed. Waiting for snap trigger or damage to re-emerge.");
                    SetState(BurrowerState.Burrowed);
                    break;
                }

                Vector3 bPos = transform.position;
                bPos.y = Mathf.MoveTowards(bPos.y, burrowedY, speed * Time.deltaTime);
                transform.position = bPos;
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
            RestoreDiveCollision();  // safe to restore now — minion is being grabbed, not physics-pushed

            // Grab the minion.
            grabbedMinionStats     = diveTarget.GetComponentInParent<CombatantStats>();
            grabbedMinionTransform = diveTarget;
            grabTimer              = settings != null ? settings.GrabDuration : 4f;

            // Disable the minion's AI so it freezes in place while carried.
            grabbedMinionAI = diveTarget.GetComponentInParent<MinionCore>();
            if (grabbedMinionAI != null) grabbedMinionAI.enabled = false;

            Log($"Grabbed minion: {diveTarget.name}");
            SetState(BurrowerState.GrabbingMinion);
        }
        else
        {
            // Phantom-style: deal damage on contact, then immediately fly back up.
            // Do NOT restore collision here — the burrower may still be overlapping the player.
            // Collision is restored at the start of FlyingUp, once the burrower is moving away.
            frame3DVelocity = Vector3.zero;

            if (diveTarget != null)
            {
                CombatantStats ts = diveTarget.GetComponent<CombatantStats>()
                                 ?? diveTarget.GetComponentInParent<CombatantStats>()
                                 ?? diveTarget.GetComponentInChildren<CombatantStats>();
                if (ts != null && !ts.IsDead)
                {
                    ts.ApplyDamage(settings != null ? settings.DiveDamage : 12f);
                    Log($"Dive hit {diveTarget.name} for {settings?.DiveDamage ?? 12f} damage");
                }
            }

            // Check HP — may retreat underground instead of flying back up.
            TransitionAfterAttack();
        }

        diveTarget = null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Post-attack HP check
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Test helper: forces the burrower to emerge from the ground immediately.
    /// Only has an effect when the enemy is in the Burrowed state.
    /// </summary>
    public void ForceEmerge()
    {
        if (currentState != BurrowerState.Burrowed) return;
        Log("[Test] Force-emerge triggered.");
        stateTimer = 0f;
        SetState(BurrowerState.Emerging);
    }

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
        if (grabbedMinionAI != null)
        {
            grabbedMinionAI.enabled = true;
            grabbedMinionAI = null;
        }
        grabbedMinionStats     = null;
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

            Transform t    = col.transform;
            Transform root = t.root;
            if (!root.gameObject.activeInHierarchy) continue;

            CombatantStats cs = t.GetComponentInParent<CombatantStats>()
                             ?? t.GetComponentInChildren<CombatantStats>();
            if (cs != null && cs.IsDead) continue;

            // Check collider transform AND the hierarchy root for the tag.
            bool isMinion = t.CompareTag(settings.MinionTag) || root.CompareTag(settings.MinionTag);
            bool isPlayer = t.CompareTag(settings.PlayerTag) || root.CompareTag(settings.PlayerTag);
            if (!isMinion && !isPlayer) continue;

            // For players, use the explicit playerTransform override if assigned.
            // For minions, use cs.transform (the individual minion, not a shared container root).
            Transform trackTarget;
            if (isPlayer)
                trackTarget = playerTransform != null ? playerTransform : root;
            else
                trackTarget = cs != null ? cs.transform : t;

            float sq = (trackTarget.position - transform.position).sqrMagnitude;

            if (isMinion && sq < bestMinionSq) { bestMinionSq = sq; bestMinion = trackTarget; }
            else if (isPlayer && sq < bestOtherSq) { bestOtherSq = sq; bestOther = trackTarget; }
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
        // Mirror the Lunger's GetStats: search self, parents, then children
        // so any hierarchy layout (tag on root, CS on child, etc.) is handled.
        CombatantStats cs = t.GetComponent<CombatantStats>()
                         ?? t.GetComponentInParent<CombatantStats>()
                         ?? t.GetComponentInChildren<CombatantStats>();
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
        // Ground / underground phases move via direct transform — keep kinematic so the
        // ground collider cannot block vertical movement.  Airborne phases use Rigidbody velocity.
        if (rb != null)
        {
            bool kinematic = newState == BurrowerState.Burrowed     ||
                             newState == BurrowerState.Triggered    ||
                             newState == BurrowerState.Emerging     ||
                             newState == BurrowerState.BurrowingDown;
            rb.isKinematic = kinematic;
            if (!kinematic) rb.linearVelocity = Vector3.zero;
        }
        currentState = newState;
    }

    private void Log(string msg)
    {
        if (enableLogs) Debug.Log($"[Burrower:{name}] {msg}");
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Death
    // ──────────────────────────────────────────────────────────────────────────

    private void RestoreDiveCollision()
    {
        if (myCollider != null && activeDiveCollider != null)
        {
            Physics.IgnoreCollision(myCollider, activeDiveCollider, false);
            activeDiveCollider = null;
        }
    }

    private void OnDied()
    {
        RestoreDiveCollision();
        Destroy(gameObject);
    }
}
