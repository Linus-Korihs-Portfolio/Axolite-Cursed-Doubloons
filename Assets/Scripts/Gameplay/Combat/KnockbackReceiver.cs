using UnityEngine;

/// <summary>
/// Applied at runtime to any target that receives a knockback impulse.
/// Decays the impulse over time and applies movement through a CharacterController
/// (if present) or directly via transform — so it works whether or not the target
/// has a Rigidbody.
/// </summary>
public class KnockbackReceiver : MonoBehaviour
{
    // Decay rate: 0 = no decay (slide forever), higher = faster stop.
    // At 8 the impulse reaches ~13 % of its original speed after 0.3 s.
    private const float DecayRate = 8f;
    private const float StopThreshold = 0.01f; // m/s²; velocity below this is zeroed

    private Vector3 velocity;
    private CharacterController cc;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        // Start disabled; AddImpulse will re-enable.
        enabled = false;
    }

    /// <summary>Add an instant knockback impulse (world-space velocity in m/s).</summary>
    public void AddImpulse(Vector3 impulse)
    {
        velocity += impulse;
        enabled = true; // make Update() run
    }

    private void Update()
    {
        if (velocity.sqrMagnitude < StopThreshold * StopThreshold)
        {
            velocity = Vector3.zero;
            enabled = false; // stop running Update() until next impulse
            return;
        }

        if (cc != null && cc.enabled) cc.Move(velocity * Time.deltaTime);
        else transform.position += velocity * Time.deltaTime;

        velocity = Vector3.Lerp(velocity, Vector3.zero, DecayRate * Time.deltaTime);
    }
}
