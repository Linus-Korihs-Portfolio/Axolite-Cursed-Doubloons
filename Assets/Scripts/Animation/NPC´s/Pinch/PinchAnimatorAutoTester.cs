using UnityEngine;

public class PinchAnimatorAutoTester : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private PinchAnimatorBridge bridge;

    [Header("Keyboard Test Keys")]
    [SerializeField] private KeyCode idleKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode dialogKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode idleBreakKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode resetKey = KeyCode.R;

    [Header("On Screen Tester")]
    [SerializeField] private bool showOnScreenTester = true;
    [SerializeField] private Rect testerWindowRect = new Rect(15f, 15f, 260f, 170f);

    [Header("Start Behaviour")]
    [SerializeField] private bool resetToIdleOnStart = true;

    private void Awake()
    {
        FindBridgeIfNeeded();
    }

    private void Start()
    {
        if (resetToIdleOnStart && bridge != null)
            bridge.ResetToStartAndIdle();
    }

    private void Update()
    {
        FindBridgeIfNeeded();

        if (bridge == null) return;

        if (Input.GetKeyDown(idleKey))
            bridge.PlayIdle();

        if (Input.GetKeyDown(dialogKey))
            bridge.PlayDialog();

        if (Input.GetKeyDown(idleBreakKey))
            bridge.PlayIdleBreak();

        if (Input.GetKeyDown(resetKey))
            bridge.ResetToStartAndIdle();
    }

    private void OnGUI()
    {
        if (!showOnScreenTester) return;
        if (!Application.isPlaying) return;

        testerWindowRect = GUI.Window(
            123456,
            testerWindowRect,
            DrawTesterWindow,
            "Pinch Animation Tester"
        );
    }

    private void DrawTesterWindow(int windowId)
    {
        FindBridgeIfNeeded();

        GUILayout.Label("Keys:");
        GUILayout.Label("1 = Idle");
        GUILayout.Label("2 = Dialog");
        GUILayout.Label("3 = IdleBreak");
        GUILayout.Label("R = Reset");

        GUILayout.Space(5);

        GUI.enabled = bridge != null;

        if (GUILayout.Button("Play Idle"))
            bridge.PlayIdle();

        if (GUILayout.Button("Play Dialog"))
            bridge.PlayDialog();

        if (GUILayout.Button("Play IdleBreak"))
            bridge.PlayIdleBreak();

        if (GUILayout.Button("Reset To Start + Idle"))
            bridge.ResetToStartAndIdle();

        GUI.enabled = true;

        GUI.DragWindow();
    }

    private void FindBridgeIfNeeded()
    {
        if (bridge != null) return;

        bridge = GetComponent<PinchAnimatorBridge>();

        if (bridge == null)
            bridge = GetComponentInChildren<PinchAnimatorBridge>();

        if (bridge == null)
            bridge = GetComponentInParent<PinchAnimatorBridge>();
    }

    [ContextMenu("Test / Play Idle")]
    private void TestPlayIdle()
    {
        FindBridgeIfNeeded();

        if (bridge != null)
            bridge.PlayIdle();
    }

    [ContextMenu("Test / Play Dialog")]
    private void TestPlayDialog()
    {
        FindBridgeIfNeeded();

        if (bridge != null)
            bridge.PlayDialog();
    }

    [ContextMenu("Test / Play IdleBreak")]
    private void TestPlayIdleBreak()
    {
        FindBridgeIfNeeded();

        if (bridge != null)
            bridge.PlayIdleBreak();
    }

    [ContextMenu("Test / Reset To Start + Idle")]
    private void TestResetToStartAndIdle()
    {
        FindBridgeIfNeeded();

        if (bridge != null)
            bridge.ResetToStartAndIdle();
    }
}