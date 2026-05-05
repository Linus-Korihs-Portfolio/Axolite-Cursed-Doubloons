using UnityEngine;

// Set IsTrigger on the attached Collider so it registers hits on contact.
[RequireComponent(typeof(Collider))]
public class MinionProjectile : MonoBehaviour
{
    [Header("Collision")]
    [Tooltip("Layers treated as solid walls that destroy this projectile on contact (e.g. Default, Walls). Exclude character layers.")]
    [SerializeField] private LayerMask wallBlockMask = ~0;

    private Transform target;
    private float damage;
    private float speed;
    private bool homing;
    private string ownerTag;   // tag of the team that fired this (ignored on hit)
    private float lifetime;
    private float spawnTime;

    // Called immediately after Instantiate to configure the projectile. Owner tag is optional but prevents the projectile from hitting the shooter team.
    public void Initialize(
        Transform target,
        float damage,
        float speed,
        bool homing,
        string ownerTag,
        float lifetime = 5f)
    {
        this.target    = target;
        this.damage    = damage;
        this.speed     = Mathf.Max(0.5f, speed);
        this.homing    = homing;
        this.ownerTag  = ownerTag;
        this.lifetime  = Mathf.Max(0.1f, lifetime);
        spawnTime      = Time.time;

        // Face the target immediately on spawn.
        if (target != null)
        {
            Vector3 dir = target.position - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }
    }

    private void Update()
    {
        if (Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Homing: steer smoothly toward the moving target each frame.
        if (homing && target != null && target.gameObject.activeInHierarchy)
        {
            Vector3 dir = target.position - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }
        }

        float stepDist = speed * Time.deltaTime;

        // Raycast ahead before moving so the projectile cannot tunnel through thin walls.
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit wallHit, stepDist + 0.05f, wallBlockMask, QueryTriggerInteraction.Ignore))
        {
            // Make sure we didn’t hit the owner team or the current target.
            bool hitSelf   = !string.IsNullOrEmpty(ownerTag) && wallHit.collider.CompareTag(ownerTag);
            bool hitTarget = target != null && (wallHit.transform == target || wallHit.transform.IsChildOf(target));
            if (!hitSelf && !hitTarget)
            {
                transform.position = wallHit.point;
                Destroy(gameObject);
                return;
            }
        }

        // Travel forward.
        transform.position += transform.forward * stepDist;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        // Skip colliders on the same team as the shooter.
        if (!string.IsNullOrEmpty(ownerTag) && other.CompareTag(ownerTag)) return;

        CombatantStats stats = other.GetComponentInParent<CombatantStats>();
        if (stats != null && !stats.IsDead)
        {
            stats.ApplyDamage(damage);
        }

        Destroy(gameObject);
    }
}
