using UnityEngine;

[CreateAssetMenu(menuName = "Player/PlayerConfig", fileName = "PlayerConfig")]
public class PlayerConfig : ScriptableObject
{
    [Header("Movement / Physics")]
    public float gravity = -25f;
    public float groundedStickForce = -2f;
    public float terminalVelocity = -50f;

    public float walkSpeed = 5f;

    [Header("Dodge")]
    public float dodgeSpeed = 12f;
    public float dodgeDuration = 0.18f;
    public float dodgeCooldown = 0.35f;
    [Range(0f, 1f)] public float activeMoveDeadzone = 0.35f;

    [Header("Punch")]
    public float punchCooldown = 0.25f;
    public float punchRange = 1.1f;
    public float punchRadius = 0.6f;
    //public float damage = 1f;
    public float hitboxBufferUpwards = 0.5f;
    public float knockbackForce = 6f;
    public float upwardKnock = 0f;

    [Header("Visual Aim")]
    public float visualTurnSpeed = 50f;
    [Range(0f, 1f)] public float visualDeadzone = 0.3f;
}
