using UnityEngine;

public sealed class PlayerKelpVisualInstaller : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private GameObject kelpPrefab;
    [SerializeField] private string spawnedVisualName = "PF_Kelp_Visual";
    [SerializeField] private Vector3 localPosition = Vector3.zero;
    [SerializeField] private Vector3 localEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 localScale = Vector3.one;

    [Header("Placeholder")]
    [SerializeField] private bool disablePlaceholderRenderer = true;
    [SerializeField] private Renderer placeholderRenderer;

    public KelpAnimatorBridge Bridge { get; private set; }

    private CombatantStats stats;

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        Bridge = FindExistingBridge();
        if (Bridge == null && kelpPrefab != null)
        {
            GameObject visual = Instantiate(kelpPrefab, visualRoot);
            visual.name = spawnedVisualName;
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.Euler(localEulerAngles);
            visual.transform.localScale = localScale;
            Bridge = visual.GetComponentInChildren<KelpAnimatorBridge>(true);
        }

        if (disablePlaceholderRenderer)
        {
            DisablePlaceholderRenderer();
        }

        RefreshDamageFlashRenderers();

        stats = GetComponentInChildren<CombatantStats>();
        if (stats != null)
        {
            stats.Died += HandleDied;
        }
    }

    private void OnDestroy()
    {
        if (stats != null)
        {
            stats.Died -= HandleDied;
        }
    }

    private void HandleDied()
    {
        Bridge?.SetDead(true);
    }

    private KelpAnimatorBridge FindExistingBridge()
    {
        if (visualRoot != null)
        {
            KelpAnimatorBridge bridge = visualRoot.GetComponentInChildren<KelpAnimatorBridge>(true);
            if (bridge != null) return bridge;
        }

        return GetComponentInChildren<KelpAnimatorBridge>(true);
    }

    private void DisablePlaceholderRenderer()
    {
        if (placeholderRenderer == null && visualRoot != null)
        {
            placeholderRenderer = visualRoot.GetComponent<Renderer>();
        }

        if (placeholderRenderer != null)
        {
            placeholderRenderer.enabled = false;
        }
    }

    private void RefreshDamageFlashRenderers()
    {
        DamageFlash[] flashes = GetComponentsInChildren<DamageFlash>(true);
        for (int i = 0; i < flashes.Length; i++)
        {
            if (flashes[i] != null)
            {
                flashes[i].RefreshRenderers();
            }
        }
    }
}
