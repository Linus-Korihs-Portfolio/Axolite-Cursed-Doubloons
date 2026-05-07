using UnityEngine;

[AddComponentMenu("Highlight/Damage Flash")]
public class DamageFlash : MonoBehaviour
{
    [Header("Renderers")]
    [Tooltip("Renderers to flash. Auto-populated from children on Awake if left empty.")]
    [SerializeField] private Renderer[] renderers;

    [Header("Flash Settings")]
    [SerializeField] private Color flashColor = new Color(1f, 0.05f, 0.05f, 1f);
    [SerializeField, Range(0f, 8f)] private float flashIntensity = 3f;
    [SerializeField, Min(0.01f)] private float flashDuration = 0.2f;

    [Header("Source")]
    [Tooltip("CombatantStats to listen to. Auto-detected in the hierarchy if left empty.")]
    [SerializeField] private CombatantStats stats;

    // Runtime
    private MaterialPropertyBlock mpb;
    private Color[][] originalEmission;
    private float flashTimer;
    private bool isFlashing;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(includeInactive: false);

        CacheOriginalEmissions();

        if (stats == null)
            stats = GetComponentInParent<CombatantStats>() ?? GetComponentInChildren<CombatantStats>();
    }

    private void OnEnable()
    {
        if (stats != null) stats.DamageTaken += OnDamageTaken;
    }

    private void OnDisable()
    {
        if (stats != null) stats.DamageTaken -= OnDamageTaken;
    }

    private void Update()
    {
        if (!isFlashing) return;

        flashTimer -= Time.deltaTime;
        float t = Mathf.Clamp01(flashTimer / flashDuration); // 1 at start → 0 at end

        ApplyEmission(t);

        if (flashTimer <= 0f)
        {
            isFlashing = false;
            ApplyEmission(0f); // Ensure we land exactly on the original color
        }
    }

    private void OnDamageTaken(float _)
    {
        flashTimer = flashDuration;
        isFlashing = true;
        ApplyEmission(1f); // Snap to full flash immediately
    }

    // t = 1 → full flash color, t = 0 → original emission
    private void ApplyEmission(float t)
    {
        Color flashEmission = flashColor * flashIntensity;

        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer ren = renderers[r];
            if (ren == null) continue;

            ren.GetPropertyBlock(mpb);

            int matCount = ren.sharedMaterials != null ? ren.sharedMaterials.Length : 1;
            for (int m = 0; m < matCount; m++)
            {
                Color original = (originalEmission != null && r < originalEmission.Length && m < originalEmission[r].Length)
                    ? originalEmission[r][m]
                    : Color.black;

                mpb.SetColor(EmissionColorId, Color.Lerp(original, flashEmission, t));
                ren.SetPropertyBlock(mpb, m);
            }
        }
    }

    private void CacheOriginalEmissions()
    {
        originalEmission = new Color[renderers.Length][];

        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer ren = renderers[r];
            if (ren == null) { originalEmission[r] = new Color[0]; continue; }

            Material[] mats = ren.sharedMaterials;
            originalEmission[r] = new Color[mats.Length];

            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null) continue;

                mat.EnableKeyword("_EMISSION");
                originalEmission[r][m] = mat.HasProperty(EmissionColorId)
                    ? mat.GetColor(EmissionColorId)
                    : Color.black;
            }
        }
    }
}
