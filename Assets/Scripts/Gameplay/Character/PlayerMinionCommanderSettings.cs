using UnityEngine;

[CreateAssetMenu(menuName = "Player/Minion Commander Settings", fileName = "PlayerMinionCommanderSettings")]
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

    [Header("Command Preview")]
    public bool enableCommandPreview = true;
    public Color previewNoTargetColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    public Color previewAttackColor = new Color(1f, 0.25f, 0.25f, 1f);
    public Color previewSupportColor = new Color(0.2f, 0.9f, 0.45f, 1f);
    public Color previewInvalidColor = new Color(0.35f, 0.55f, 1f, 1f);
    [Range(0f, 4f)] public float previewEmissionIntensity = 0.35f;
}
