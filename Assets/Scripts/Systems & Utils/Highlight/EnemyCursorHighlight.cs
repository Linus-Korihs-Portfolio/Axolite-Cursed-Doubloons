using UnityEngine;

public class EnemyCursorHighlight : MonoBehaviour, ICursorHighlight
{
    [SerializeField] private Renderer[] renderers;

    [Header("Highlight Look")]
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 1f); // can be overridden per enemy prefab
    [SerializeField, Range(0f, 8f)] private float intensity = 2.0f;            // HDR-ish
    [SerializeField] private bool addToExistingEmission = true;

    private MaterialPropertyBlock mpb;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    // Cached ORIGINAL emission per renderer per sub-material
    private Color[][] originalEmission;

    private bool isHighlighted;
    public bool IsHighlighted => isHighlighted;

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
        if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();

        originalEmission = new Color[renderers.Length][];

        for (int r = 0; r < renderers.Length; r++)
        {
            var ren = renderers[r];
            if (!ren) continue;

            var mats = ren.materials; // instances ok for enemies
            originalEmission[r] = new Color[mats.Length];

            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (!mat) continue;

                // Make sure emission is enabled for URP Lit etc.
                mat.EnableKeyword("_EMISSION");

                // Cache material default emission (NOT from property block)
                if (mat.HasProperty(EmissionColor)) originalEmission[r][m] = mat.GetColor(EmissionColor);
                else originalEmission[r][m] = Color.black;
            }
        }
    }

    public void SetHighlighted(bool on)
    {
        isHighlighted = on;

        for (int r = 0; r < renderers.Length; r++)
        {
            var ren = renderers[r];
            if (!ren) continue;

            int matCount = ren.sharedMaterials != null ? ren.sharedMaterials.Length : 1;

            for (int m = 0; m < matCount; m++)
            {
                // Use per-material blocks (same as DamageFlash) so both systems write to the
                // same layer and the last writer wins cleanly.
                ren.GetPropertyBlock(mpb, m);

                Color baseE = (originalEmission[r] != null && m < originalEmission[r].Length) ? originalEmission[r][m] : Color.black;
                Color addE  = highlightColor * intensity;

                Color final = on
                    ? (addToExistingEmission ? (baseE + addE) : addE)
                    : baseE;

                mpb.SetColor(EmissionColor, final);
                ren.SetPropertyBlock(mpb, m);
            }
        }
    }
}