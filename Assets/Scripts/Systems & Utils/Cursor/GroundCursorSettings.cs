using UnityEngine;

[CreateAssetMenu(menuName = "Camera/Cursor Settings")]
public class GroundCursorSettings : ScriptableObject
{
    [Header("Enable")]
    public bool cursorEnabled = true;

    [Header("Ground")]
    public LayerMask groundMask;
    public float groundRayStartHeight = 25f;
    public float groundRayLength = 80f;
    public float heightOffset = 0.02f;

    [Header("Distances (from player)")]
    public float baseDistance = 4.5f;      // where the cursor "wants" to be by default
    public float minDistance = 1.5f;       // never closer than this
    public float maxDistance = 12f;        // never farther than this

    [Header("Manual Offset (Pikmin-like)")]
    public float maxOffsetRadius = 6f;     // clamp for manual aiming offset (relative around base)
    public float stickSpeed = 8f;          // units/sec (world plane)
    public float mouseSpeed = 0.06f;       // units per mouse-delta "tick"
    public float recenterSpeed = 10f;      // units/sec back to center when no input

    [Header("Extra Press Mode")]
    public bool extraPressMouse = false;    // mouse/keyboard input only applies when an extra button is held (e.g. right stick click or a keyboard key) - prevents unwanted cursor movement when just trying to move the character
    public bool extraPressGamepad = false;   // gamepad stick input only applies when an extra button is held - prevents unwanted cursor movement when just trying to move the character

    [Header("Smoothing")]
    public float followSmoothing = 20f;    // free mode smoothing
    public bool smoothWhenFree = true;

    [Header("Visual")]
    public Vector3 markerScale = new Vector3(1f, 0.1f, 1f);

    [Header("Lock-On")]
    public LayerMask lockableMask;
    public float lockSearchRadius = 1.25f;
    public float lockRange = 15f;          // auto unlock if player too far
}