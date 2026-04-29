using System;
using UnityEngine;

[CreateAssetMenu(menuName = "SO/Camera/Cursor Settings")]
public class GroundCursorSettings : ScriptableObject
{
    [Header("Enable")]
    public bool cursorEnabled = true;

    [Header("Ground")]
    public LayerMask groundMask;
    public float groundRayStartHeight = 25f;
    public float groundRayLength = 80f;
    public float heightOffset = 0.02f;

    [Header("Distances (from player)")]
    public float baseDistance = 4.5f;      // where the cursor "wants" to be by default
    public float minDistance = 1.5f;       // never closer than this
    public float maxDistance = 12f;        // never farther than this

    [Header("Direction Smoothing")]
    public float dirSmoothing = 16f;        // higher = faster turn, less teleport

    [Header("Lock-On")]
    public LayerMask lockableMask;
    public float lockRange = 15f;           // auto unlock if player too far
    public bool lockDashEnabled = true;     // if true, the player can dash towards the lock-on target (if not, the cursor will just snap to it but the player won't get any special movement benefits)
    public bool lockWithCursor = true;      // if true, the cursor will try to snap to lock-on targets and also auto-unlock if the target is too far from the cursor (instead of player)
    public float lockRangeWithCursor = 8f;  // if lockOnUsesCursor: auto unlock if target is farther than this from cursor (0 = ignore cursor distance for unlocking)

    [Header("Lock Transition")]
    public bool smoothLockTransition = true;
    public float lockMoveSpeed = 18f;     // units/sec (cursor slide to/from target)

    [Header("Wall Blocking")]
    public LayerMask wallMask;
    public float wallPadding = 0.05f;     // small offset so cursor doesn't clip into wall
    public float wallProbeRadius = 0.0f;  // 0 = Raycast, >0 = SphereCast (recommended: 0.1..0.25)

    [Header("Wall Climb Cursor")]
    public bool wallClimbEnabled = true;
    public bool wallHitTriggers = false; // if false, the cursor will ignore trigger colliders when checking for walls to climb on - prevents unwanted climbing on trigger volumes like bushes, but also prevents climbing on actual climbable trigger volumes if you have those
    public float wallSurfaceOffset = 0.02f; // how much the cursor should hover above the wall surface when climbing
    public bool rotateMarkerToSurface = true;

    [Header("Wall Climb Height")]
    public float wallMinHeight = 0.05f;      // least height
    public float wallEyeHeight = 1.55f;      // eye height relative to the player (or model)
    public float wallNearDistance = 0.6f;    // when the player is this close to the wall -> EyeHeight (max)
    public float wallFarDistance = 4.5f;     // when the player is this far -> near-ground (min)
    public float wallHeightSmoothing = 18f;  // optional smoothing (for nice feel)

    [Header("Visual")]
    public Vector3 markerScale = new Vector3(1f, 0.1f, 1f);

    [Header("Aim Assist (Near Enemy)")]
    public bool aimAssistEnabled = true;
    public float aimEnterRadius = 1.25f;
    public float aimExitRadius  = 1.60f;
    public float aimCooldown = 0.5f;
    public bool highlightEnabled = true;

    [Header("Aim Assist Ray Hold")]
    public bool aimHoldWhileRayHits = true;
    public float aimHoldRayRadius = 0.25f;     // 0 = Raycast, >0 = SphereCast (recommended: 0.1..0.25)
    public float aimHoldRayExtraLength = 2.0f; // a bit further than baseDistance
}