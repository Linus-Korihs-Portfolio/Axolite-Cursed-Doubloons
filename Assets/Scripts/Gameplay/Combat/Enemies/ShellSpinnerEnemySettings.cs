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

    [Header("Shell Entry")]
    [Tooltip("Duration of the entering-shell animation. Spinner is still vulnerable during this time.")]
    public float EnterShellDuration = 0.6f;

    [Header("Spin Attack")]
    [Tooltip("Speed at which the spinner moves in a straight line once launched.")]
    public float SpinSpeed = 10f;
    [Tooltip("Damage dealt to any player or minion touched during the spin.")]
    public float SpinDamage = 18f;

    [Header("Hit Pause")]
    [Tooltip("Duration of the brief impact-stop when the spinner hits anything. Still in shell / invincible.")]
    public float HitPauseDuration = 0.15f;
    [Tooltip("Seconds after each spin launch during which collisions are ignored. " +
             "Prevents the spinner from immediately re-hitting the wall it was resting against.")]
    public float SpinCollisionGrace = 0.12f;

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
