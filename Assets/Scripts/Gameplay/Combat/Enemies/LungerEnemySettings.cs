using UnityEngine;

/// <summary>
/// Shared configuration for the Lunger enemy (Enemy 1 — Goomba-like).
/// Create via Assets > Create > SO > Combat > Enemy > Lunger Enemy Settings.
/// </summary>
[CreateAssetMenu(menuName = "SO/Combat/Enemy/Lunger")]
public class LungerEnemySettings : ScriptableObject
{
    [Header("Tags")]
    public string PlayerTag = "Player";
    public string MinionTag = "Ally";

    [Header("Detection")]
    [Tooltip("Radius within which the Lunger detects targets.")]
    public float DetectRadius = 12f;
    [Tooltip("Target is dropped when it leaves this radius.")]
    public float ForgetRadius = 18f;
    public LayerMask DetectMask = ~0;
    [Tooltip("Enemy ignores targets it cannot see.")]
    public bool RequireLOSToDetect = true;

    [Header("Line of Sight")]
    [Tooltip("Layers treated as solid walls.")]
    public LayerMask LosBlockMask = ~0;
    public float LosHeightOffset = 0.8f;
    [Tooltip("Enemy will not bite targets it cannot see.")]
    public bool RequireLOSToAttack = true;

    [Header("Movement")]
    public float MoveSpeed     = 3.5f;
    public float RotationSpeed = 8f;

    [Header("Bite Attack")]
    [Tooltip("Range at which the bite can connect.")]
    public float BiteRange    = 1.5f;
    public float BiteDamage   = 10f;
    public float BiteCooldown = 1.2f;

    [Header("Lunge Attack")]
    [Tooltip("If the target is farther than this the Lunger will lunge rather than walk.")]
    public float LungeMinDistance      = 3.5f;
    public float LungeSpeed            = 12f;
    [Tooltip("Extra distance added beyond the target so the player must sidestep, not just stand still.")]
    public float LungeOvershootDistance = 2f;
    [Tooltip("Radius of the body hit-sphere swept during the lunge (continuous damage).")]
    public float LungeHitRadius        = 1.6f;
    public float LungeDamage           = 15f;
    [Tooltip("Duration of the pre-lunge wind-up before launching.")]
    public float LungeWindupDuration   = 0.4f;
    [Tooltip("Duration the Lunger is stunned on the ground after landing.")]
    public float LungeRecoveryDuration = 1.2f;
    [Tooltip("Minimum time between two consecutive lunges (new encounter).")]
    public float LungeCooldown         = 4f;

    [Header("Navigation")]
    public bool  UseNavMesh              = true;
    public float NavRepathInterval       = 0.3f;
    public float NavTargetSampleRadius   = 1.5f;
    public float NavWaypointTolerance    = 0.3f;

    [Header("Physics")]
    [Tooltip("Layers the Lunger's Rigidbody should pass through (e.g. the Minion layer).")]
    public LayerMask IgnoreCollisionMask;
    [Tooltip("Layers treated as walls for lunge collision detection.")]
    public LayerMask WallMask = ~0;
}
