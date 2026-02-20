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
    [Header("Look (Right Stick / Mouse)")]
    public float lookSensitivityX = 180f;
    public float lookSensitivityY = 120f;
    public bool invertY = false;
    public bool lookScaleWithDeltaTime = true;
    public float minVertical = 5f;
    public float maxVertical = 85f;

    [Header("Lock-On")]
    public string[] lockOnTags = new[] { "Enemy" };
    public float lockOnMaxDistance = 25f;

    [Header("Look - Gamepad")]
    public float gamepadSensitivityX = 180f;
    public float gamepadSensitivityY = 120f;

    [Header("Look - Mouse")]
    public float mouseSensitivityX = 0.15f;
    public float mouseSensitivityY = 0.15f;
    public bool mouseScaleWithDeltaTime = false;

}
