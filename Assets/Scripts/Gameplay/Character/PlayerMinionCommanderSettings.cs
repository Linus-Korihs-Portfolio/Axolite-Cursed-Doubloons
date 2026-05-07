using UnityEngine;

[CreateAssetMenu(menuName = "SO/Minions/Minion Commander Settings", fileName = "PlayerMinionCommanderSettings")]
public class PlayerMinionCommanderSettings : ScriptableObject
{
    [Header("Target Query")]
    public float commandAcquireRadius = 1.1f;
    public float previewAcquireRadius = 0.35f;
    public LayerMask commandTargetMask = ~0;
    public bool includeTriggers = false;
    public string enemyTag = "Enemy";
    public string breakableTag = "Breakable";

    [Header("Auto Find")]
    public float autoFindRefreshInterval = 0.5f;

    [Header("Target Visibility")]
    [Tooltip("Layers that block LOS between player and command targets (walls, terrain). Exclude character layers.")]
    public LayerMask cursorLOSBlockMask = ~0;
    [Tooltip("When true, targets behind walls cannot be commanded even if the cursor overlap detects them.")]
    public bool requireLineOfSightForCursorTargets = true;
    [Tooltip("Height offset used for the LOS ray from the player.")]
    public float cursorLOSHeightOffset = 0.8f;

    [Header("Call / Dismiss")]
    [Tooltip("Radius of the Call impulse wave and the maximum range for Dismiss to affect minions.")]
    public float callRange = 10f;
    [Tooltip("When a dismissed minion is farther than this from the player it automatically starts following again (should equal callRange).")]
    public float dismissResumeFollowRange = 10f;
    [Tooltip("Distance between each role group's centre point in the dismiss formation (Melee / Ranged / Support spread sideways).")]
    public float dismissFormationGroupSpacing = 2f;
    [Tooltip("Spacing between individual minions within the same role group.")]
    public float dismissFormationMemberSpacing = 1.2f;
    [Tooltip("Color of the call/dismiss range Gizmo sphere drawn in the editor.")]
    public Color callRangeGizmoColor = new Color(0.2f, 0.7f, 1f, 0.25f);

    [Header("Command Preview")]
    public bool enableCommandPreview = true;
    public Color previewNoTargetColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    public Color previewAttackColor = new Color(1f, 0.25f, 0.25f, 1f);
    public Color previewSupportColor = new Color(0.2f, 0.9f, 0.45f, 1f);
    public Color previewInvalidColor = new Color(0.35f, 0.55f, 1f, 1f);
    [Range(0f, 4f)] public float previewEmissionIntensity = 0.35f;
}
