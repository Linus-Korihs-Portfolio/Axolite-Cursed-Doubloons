using UnityEngine;

/// <summary>
/// Shared configuration for the Shell Spinner enemy (Enemy 2 — Koopa/Armos-like).
/// Create via Assets > Create > SO > Combat > Enemy > Shell Spinner Enemy Settings.
/// </summary>
[CreateAssetMenu(menuName = "SO/Combat/Enemy/Shell Spinner")]
public class ShellSpinnerEnemySettings : ScriptableObject
{
    [Header("Tags")]
    public string PlayerTag = "Player";
    public string MinionTag = "Ally";

    [Header("Detection")]
    [Tooltip("Radius within which the Spinner detects targets and wakes up.")]
    public float DetectRadius = 12f;
    [Tooltip("Target is forgotten when it leaves this radius.")]
    public float ForgetRadius = 18f;
    public LayerMask DetectMask = ~0;
    [Tooltip("Only wake up to targets the Spinner can actually see.")]
    public bool RequireLOSToDetect = true;

    [Header("Line of Sight")]
    [Tooltip("Layers treated as solid walls for LOS checks.")]
    public LayerMask LosBlockMask = ~0;
    public float LosHeightOffset = 0.8f;

    [Header("Movement")]
    [Tooltip("Rotation speed while facing a target in the Idle state.")]
    public float RotationSpeed = 8f;

    [Header("Windup / Targeting")]
    [Tooltip("Duration of the windup / targeting phase. The spinner is still vulnerable and shows an aim line.")]
    public float WindupDuration = 0.6f;
    [Tooltip("LayerMask used to snap the targeting line to the ground surface. " +
             "Assign the same layer(s) as your floor geometry. If empty the spinner's own Y is used as a fallback.")]
    public LayerMask GroundMask;

    [Header("Spin Attack")]
    [Tooltip("Speed at which the spinner moves in a straight line once launched.")]
    public float SpinSpeed = 10f;
    [Tooltip("Damage dealt to any player or minion touched during the spin.")]
    public float SpinDamage = 18f;
    [Tooltip("When disabled (default) the spin stops as soon as it touches a player or minion. " +
             "When enabled the spin passes through all targets (dealing damage to each once) " +
             "and only stops when hitting a wall or travelling MaxSpinRange units.")]
    public bool  SpinUntilWall = false;
    [Tooltip("Maximum travel distance before the spin automatically ends. Only used when SpinUntilWall is enabled. 0 = unlimited.")]
    public float MaxSpinRange = 20f;

    [Header("Hit Pause")]
    [Tooltip("Duration of the brief impact-stop when the spinner hits anything. Still in shell / invincible.")]
    public float HitPauseDuration = 0.15f;
    [Tooltip("Seconds after each spin launch during which wall collisions are ignored. " +
             "Prevents the spinner from immediately re-hitting the wall it was resting against.")]
    public float SpinCollisionGrace = 0.12f;
    [Tooltip("Knockback impulse force applied to targets hit during the spin. " +
             "Works on both Rigidbody and non-Rigidbody (CharacterController) targets.")]
    public float KnockbackForce = 8f;

    [Header("Shell Exit")]
    [Tooltip("Duration of the exiting-shell animation. Spinner is vulnerable from the very start of this state.")]
    public float ExitShellDuration = 0.6f;

    [Header("Dizzy")]
    [Tooltip("How long the spinner stands dizzy and defenceless after exiting the shell.")]
    public float DizzyDuration = 2.5f;

    [Header("Wake Up")]
    [Tooltip("Duration of the waking-up / shaking-off-dizziness animation before the next shell entry.")]
    public float WakeUpDuration = 0.4f;
}
