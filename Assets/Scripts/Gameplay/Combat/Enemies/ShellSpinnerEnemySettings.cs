using UnityEngine;

/// <summary>
/// Shared configuration for the Shell Spinner enemy (Enemy 2 — Wind Waker Armos-like).
/// Create via Assets > Create > SO > Combat > Enemy > Shell Spinner Enemy Settings.
/// </summary>
[CreateAssetMenu(menuName = "SO/Combat/Enemy/Shell Spinner")]
public class ShellSpinnerEnemySettings : ScriptableObject
{
    [Header("Tags")]
    public string PlayerTag = "Player";
    public string MinionTag = "Ally";

    [Header("Detection")]
    public float     DetectRadius       = 10f;
    public float     ForgetRadius       = 16f;
    public LayerMask DetectMask         = ~0;
    [Tooltip("Enemy ignores targets it cannot see during normal detection.")]
    public bool      RequireLOSToDetect = true;

    [Header("Line of Sight")]
    public LayerMask LosBlockMask    = ~0;
    public float     LosHeightOffset = 0.8f;

    [Header("Movement (Normal)")]
    public float MoveSpeed     = 2.5f;
    public float RotationSpeed = 6f;

    [Header("Shell Retract")]
    [Tooltip("Duration of the retract animation before spinning begins.")]
    public float RetractDuration = 0.5f;

    [Header("Spinning")]
    [Tooltip("Movement speed while spinning.")]
    public float SpinSpeed           = 14f;
    [Tooltip("Maximum distance traveled during a single spin charge.")]
    public float SpinMaxDistance     = 12f;
    [Tooltip("Damage per second dealt to targets hit by the spinning shell.")]
    public float SpinDamagePerSecond = 20f;
    [Tooltip("Radius of the contact hit-sphere while spinning.")]
    public float SpinHitRadius       = 0.9f;
    [Tooltip("Seconds between spin damage ticks.")]
    public float SpinDamageInterval  = 0.15f;

    [Header("Dazed")]
    [Tooltip("How long the enemy lies dazed (fully vulnerable) after crashing out of a spin.")]
    public float DazedDuration = 3f;

    [Header("Navigation")]
    public bool  UseNavMesh            = true;
    public float NavRepathInterval     = 0.3f;
    public float NavTargetSampleRadius = 1.5f;
    public float NavWaypointTolerance  = 0.3f;

    [Header("Physics")]
    [Tooltip("Layers the Shell Spinner's Rigidbody should pass through.")]
    public LayerMask IgnoreCollisionMask;
    [Tooltip("Layers treated as walls for spin-crash detection.")]
    public LayerMask WallMask = ~0;
}
