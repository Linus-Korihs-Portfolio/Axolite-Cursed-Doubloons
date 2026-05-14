using UnityEngine;

// Attach to any destructible wall or crate that minions should be able to break.
// Requires a CombatantStats component (for health) and the "Breakable" tag on the GameObject.
[RequireComponent(typeof(CombatantStats))]
public class BreakableWall : MonoBehaviour
{
    [Header("Death FX")]
    [SerializeField] private GameObject deathFxPrefab;
    [SerializeField] private float deathFxDuration = 2f;

    private CombatantStats stats;

    private void Awake()
    {
        stats = GetComponent<CombatantStats>();
        stats.Died += OnDied;
    }

    private void OnDestroy()
    {
        if (stats != null) stats.Died -= OnDied;
    }

    private void OnDied()
    {
        if (deathFxPrefab != null)
        {
            GameObject fx = Instantiate(deathFxPrefab, transform.position, transform.rotation);
            Destroy(fx, Mathf.Max(0.1f, deathFxDuration));
        }

        Destroy(gameObject);
    }
}
