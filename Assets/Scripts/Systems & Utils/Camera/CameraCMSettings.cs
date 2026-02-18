using UnityEngine;

[CreateAssetMenu(menuName = "Camera/CM Settings", fileName = "CameraCMSettings")]
public class CameraCMSettings : ScriptableObject
{
    [Header("Presets")]
    public float topRadius = 12f;
    public float topVertical = 75f;
    public float thirdRadius = 4f;
    public float thirdVertical = 25f;

    [Header("Tuning")]
    public float transitionSpeed = 6f;
    public float zoomSpeed = 0.02f;
    public float minRadius = 2.5f;
    public float maxRadius = 16f;

    [Header("Occlusion - Occluders")]
    public LayerMask occluderMask = ~0;
    public bool makeTransparent = true;
    [Range(0.05f, 1f)] public float transparentAlpha = 0.25f;

    [Header("Occlusion - Trigger Shape")]
    public float occlusionRadius = 0.35f;
    public float paddingFromCamera = 0.2f;
    public float paddingFromTarget = 0.1f;

    [Header("Occlusion - Robustness")]
    public int staleFramesToRestore = 2;

    [Header("Occlusion - Debug")]
    public bool debugEnabled = true;
    public bool drawGizmos = true;
}
