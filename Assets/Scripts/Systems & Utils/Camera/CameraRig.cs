using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class CameraRig : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera topDownCam;
    [SerializeField] private CinemachineCamera thirdPersonCam;

    [Header("TopDown (Follow)")]
    [SerializeField] private CinemachineFollow topDownFollow;
    [SerializeField] private float topMinHeight = 8f;
    [SerializeField] private float topMaxHeight = 22f;
    [SerializeField] private float topMinBack = -2f;   // closer (less negative)
    [SerializeField] private float topMaxBack = -12f;  // farther (more negative)

    [Header("ThirdPerson (Orbital)")]
    [SerializeField] private CinemachineOrbitalFollow thirdPersonOrbital;
    [SerializeField] private float thirdMinRadius = 2.5f;
    [SerializeField] private float thirdMaxRadius = 10f;

    [Header("Input")]
    [SerializeField] private Key toggleKey = Key.Tab;
    [SerializeField] private float zoomSpeed = 1.0f;

    private bool isThirdPerson;

    private void Awake()
    {
        // Safety auto-grab if not assigned
        if (topDownCam && !topDownFollow) topDownFollow = topDownCam.GetComponent<CinemachineFollow>();
        if (thirdPersonCam && !thirdPersonOrbital) thirdPersonOrbital = thirdPersonCam.GetComponent<CinemachineOrbitalFollow>();

        SetMode(thirdPerson: false, instant: true);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame) SetMode(!isThirdPerson, instant: false);

        float scroll = 0f;
        if (Mouse.current != null) scroll = Mouse.current.scroll.y.ReadValue();
        if (Mathf.Abs(scroll) > 0.001f) Zoom(scroll * 0.01f);
    }

    private void SetMode(bool thirdPerson, bool instant)
    {
        isThirdPerson = thirdPerson;

        // Priority: higher wins (Brain blends automatically) :contentReference[oaicite:6]{index=6}
        SetPriority(topDownCam,  thirdPerson ? 0 : 10, instant);
        SetPriority(thirdPersonCam, thirdPerson ? 10 : 0, instant);
    }

    private static void SetPriority(CinemachineCamera cam, int value, bool instant)
    {
        if (!cam) return;

        var p = cam.Priority;
        p.Enabled = true;
        p.Value = value;
        cam.Priority = p;

        // Push to top among same-priority peers if needed :contentReference[oaicite:7]{index=7}
        cam.Prioritize();
    }

    private void Zoom(float scrollDelta)
    {
        // Scroll up should zoom IN → move closer / reduce height/radius
        float step = scrollDelta * zoomSpeed;

        if (!isThirdPerson && topDownFollow)
        {
            Vector3 o = topDownFollow.FollowOffset;

            o.y = Mathf.Clamp(o.y - step, topMinHeight, topMaxHeight);
            // keep camera "behind" target by staying negative on Z
            o.z = Mathf.Clamp(o.z + step, topMaxBack, topMinBack);

            topDownFollow.FollowOffset = o; // :contentReference[oaicite:8]{index=8}
        }
        else if (isThirdPerson && thirdPersonOrbital)
        {
            thirdPersonOrbital.Radius = Mathf.Clamp(thirdPersonOrbital.Radius - step, thirdMinRadius, thirdMaxRadius); // :contentReference[oaicite:9]{index=9}
        }
    }
}
