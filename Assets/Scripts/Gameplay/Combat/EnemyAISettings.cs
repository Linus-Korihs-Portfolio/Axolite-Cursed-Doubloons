using UnityEngine;

// Top-level enum shared by EnemyAISettings and EnemyAI.
public enum EnemyType { Melee, Ranged }

[CreateAssetMenu(menuName = "SO/Combat/Enemy AI Settings")]
public class EnemyAISettings : ScriptableObject
{
    [Header("Type")]
    [Tooltip("Determines attack mode and state-machine transitions.")]
    public EnemyType EnemyType = EnemyType.Melee;

    [Header("Tags")]
    public string PlayerTag = "Player";
    public string MinionTag = "Ally";

    [Header("Detection")]
    [Tooltip("Radius within which enemies detect targets.")]
    public float DetectRadius = 12f;
    [Tooltip("Target is dropped when it leaves this radius.")]
    public float ForgetRadius  = 18f;
    public LayerMask DetectMask = ~0;

    [Header("Combat — Melee")]
    public float MeleeAttackRange    = 1.5f;
    public float MeleeAttackCooldown = 1.2f;
    public float MeleeDamage         = 10f;

    [Header("Combat — Ranged")]
    public float RangedMinRange       = 4f;
    public float RangedMaxRange       = 10f;
    public float RangedAttackCooldown = 2f;
    public float RangedDamage         = 8f;
    public float ProjectileSpeed      = 8f;
    public bool  UseHomingProjectile  = false;
    public GameObject ProjectilePrefab;

    [Header("Movement")]
    public float MoveSpeed     = 3.5f;
    public float RotationSpeed = 8f;

    [Header("Target Priority")]
    [Tooltip("Prefer targeting ranged minions over melee/player targets.")]
    public bool PrioritizeRangedMinions = true;

    [Header("Pursuit")]
    [Tooltip("When true, the enemy chases the last known target position after losing line of sight.")]
    public bool EnablePursuit = true;
    [Tooltip("Enemy abandons pursuit when the last known position is farther than this from the enemy. Should be ≥ ForgetRadius.")]
    public float PursuitRadius = 22f;

    [Header("Navigation (NavMesh)")]
    [Tooltip("When true, enemies use the baked NavMesh to route around obstacles. Requires a NavMesh in the scene.")]
    public bool UseNavMesh = true;
    [Tooltip("How often the path to the destination is recalculated (seconds).")]
    public float NavRepathInterval = 0.3f;
    [Tooltip("Radius within which the destination is snapped onto the NavMesh.")]
    public float NavTargetSampleRadius = 1.5f;
    [Tooltip("Distance threshold for advancing to the next waypoint corner.")]
    public float NavWaypointTolerance = 0.3f;

    [Header("Line of Sight")]
    [Tooltip("Layers treated as solid walls (exclude character layers).")]
    public LayerMask LosBlockMask = ~0;
    [Tooltip("Height offset for the LOS ray origin/destination.")]
    public float LosHeightOffset = 0.8f;
    [Tooltip("Enemy ignores targets it cannot see.")]
    public bool RequireLOSToDetect = true;
    [Tooltip("Enemy will not attack targets it cannot see.")]
    public bool RequireLOSToAttack = true;

    [Header("Physics")]
    [Tooltip("Layers the enemy's Rigidbody should pass through (e.g. the Minion layer). " +
             "Set this to the layer(s) used by minion colliders so the enemy is not blocked " +
             "when approaching the player through a group of minions.")]
    public LayerMask IgnoreCollisionMask;
}
