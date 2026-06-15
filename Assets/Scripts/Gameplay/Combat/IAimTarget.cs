/// <summary>
/// Implemented by enemies that want projectiles to aim at a specific point rather than
/// the root transform (e.g. a burrower that is mostly underground).
/// </summary>
public interface IAimTarget
{
    /// <summary>
    /// Returns the world-space Transform projectiles should home toward.
    /// Must never return null — fall back to the object's own Transform if needed.
    /// </summary>
    UnityEngine.Transform GetAimTransform();
}
