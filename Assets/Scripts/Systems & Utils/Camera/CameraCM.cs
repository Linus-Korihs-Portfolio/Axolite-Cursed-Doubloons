using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraCM : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private CameraCMSettings settings;

    [Header("Cinemachine")]
    [SerializeField] public CinemachineOrbitalFollow orbital;

    [Header("Input Actions")]
    [SerializeField] public InputActionReference toggleAction;
    [SerializeField] public InputActionReference zoomAction;

    private bool isThird;
    private float targetRadius;
    private float targetVertical;

    private float ZoomSpeed => settings.zoomSpeed;
    private float TransitionSpeed => settings.transitionSpeed;
    private float MinRadius => settings.minRadius;
    private float MaxRadius => settings.maxRadius;
    private float TopRadius => settings.topRadius;
    private float TopVertical => settings.topVertical;
    private float ThirdRadius => settings.thirdRadius;
    private float ThirdVertical => settings.thirdVertical;

    private void OnEnable()
    {
        if (toggleAction) toggleAction.action.Enable();
        if (zoomAction) zoomAction.action.Enable();

        if (toggleAction) toggleAction.action.performed += OnToggle;
    }

    private void OnDisable()
    {
        if (toggleAction) toggleAction.action.performed -= OnToggle;

        if (toggleAction) toggleAction.action.Disable();
        if (zoomAction) zoomAction.action.Disable();
    }

    private void Start()
    {
        SetMode(false, instant: true);
    }

    private void Update()
    {
        if (zoomAction)
        {
            float scrollY = zoomAction.action.ReadValue<float>();
            if (Mathf.Abs(scrollY) > 0.001f)
            {
                targetRadius = Mathf.Clamp(targetRadius - scrollY * ZoomSpeed, MinRadius, MaxRadius);
            }
        }

        // Smooth transition
        orbital.Radius = Mathf.Lerp(orbital.Radius, targetRadius, Time.deltaTime * TransitionSpeed);

        var v = orbital.VerticalAxis;
        v.Value = Mathf.Lerp(v.Value, targetVertical, Time.deltaTime * TransitionSpeed);
        orbital.VerticalAxis = v;
    }

    private void OnToggle(InputAction.CallbackContext _)
    {
        SetMode(!isThird, instant: false);
        Debug.Log($"Camera mode toggled. Now in {(isThird ? "third-person" : "top-down")} mode.");
    }

    private void SetMode(bool third, bool instant)
    {
        isThird = third;

        targetRadius = third ? ThirdRadius : TopRadius;
        targetVertical = third ? ThirdVertical : TopVertical;

        if (!orbital)
        {
            Debug.LogError("No CinemachineOrbitalFollow assigned/found!");
            return;
        }

        if (instant)
        {
            orbital.Radius = targetRadius;
            var v = orbital.VerticalAxis;
            v.Value = targetVertical;
            orbital.VerticalAxis = v;
        }
        Debug.Log($"SetMode -> targetRadius:{targetRadius}, targetVertical:{targetVertical} | currentRadius:{orbital.Radius}, currentVertical:{orbital.VerticalAxis.Value}");
    }
}
