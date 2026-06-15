using UnityEngine;

public static class EnemyTargetUtility
{
    public static Transform FindTaggedActor(Transform source, string tag)
    {
        if (source == null || string.IsNullOrEmpty(tag))
            return null;

        Transform current = source;
        while (current != null)
        {
            if (current.CompareTag(tag))
                return current;

            current = current.parent;
        }

        return null;
    }

    public static CombatantStats GetStats(Transform source)
    {
        if (source == null)
            return null;

        return source.GetComponent<CombatantStats>()
            ?? source.GetComponentInParent<CombatantStats>()
            ?? source.GetComponentInChildren<CombatantStats>();
    }

    public static bool BelongsToActor(Transform hit, Transform actor)
    {
        if (hit == null || actor == null)
            return false;

        if (hit == actor || hit.IsChildOf(actor) || actor.IsChildOf(hit))
            return true;

        CombatantStats hitStats = GetStats(hit);
        CombatantStats actorStats = GetStats(actor);
        return hitStats != null && hitStats == actorStats;
    }
}
