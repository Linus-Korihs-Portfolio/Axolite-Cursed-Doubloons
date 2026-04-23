using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMinionCommander : MonoBehaviour
{
    private enum CommandPreviewType
    {
        None,
        Attack,
        Support,
        Invalid
    }

    [Header("References")]
    [SerializeField] private GroundCursor cursor;
    [SerializeField] private Transform player;

    [Header("Input")]
    [SerializeField] private InputActionReference commandAction;
    [SerializeField] private InputActionReference recallAction;

    [Header("Target Query")]
    [SerializeField] private float commandAcquireRadius = 1.1f;
    [SerializeField] private LayerMask commandTargetMask = ~0;
    [SerializeField] private bool includeTriggers = false;
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string breakableTag = "Breakable";

    [Header("Minion Selection")]
    [SerializeField] private MinionAgent[] controlledMinions;
    [SerializeField] private bool autoFindMinionsIfEmpty = true;

    [Header("Command Preview")]
    [SerializeField] private bool enableCommandPreview = true;
    [SerializeField] private Renderer[] previewRenderers;
    [SerializeField] private Color previewNoTargetColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    [SerializeField] private Color previewAttackColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color previewSupportColor = new Color(0.2f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color previewInvalidColor = new Color(0.35f, 0.55f, 1f, 1f);
    [SerializeField, Range(0f, 4f)] private float previewEmissionIntensity = 0.35f;

    private readonly Collider[] commandHits = new Collider[32];
    private MaterialPropertyBlock previewPropertyBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        if (cursor == null) cursor = GetComponentInChildren<GroundCursor>();
        if (player == null) player = transform;

        // Auto-pick preview renderers from cursor hierarchy if none were assigned.
        if ((previewRenderers == null || previewRenderers.Length == 0) && cursor != null)
        {
            previewRenderers = cursor.GetComponentsInChildren<Renderer>(true);

            // Fallback: if marker is not a child of cursor, resolve renderers directly from marker.
            if ((previewRenderers == null || previewRenderers.Length == 0) && cursor.Marker != null)
            {
                previewRenderers = cursor.Marker.GetComponentsInChildren<Renderer>(true);
            }
        }

        previewPropertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (commandAction != null) commandAction.action.Enable();
        if (recallAction != null) recallAction.action.Enable();
    }

    private void OnDisable()
    {
        if (commandAction != null) commandAction.action.Disable();
        if (recallAction != null) recallAction.action.Disable();
    }

    private void Update()
    {
        // Preview updates continuously so the player gets immediate cursor feedback.
        UpdateCommandPreview();

        if (WasCommandPressedThisFrame())
        {
            IssueCommandFromCursor();
        }

        if (WasRecallPressedThisFrame())
        {
            RecallAll();
        }
    }

    // Switches the active support action (Heal/Buff/Debuff) for all support minions.
    public void SetSupportModeForAll(SupportMode mode)
    {
        MinionAgent[] minions = ResolveControlledMinions();
        for (int i = 0; i < minions.Length; i++)
        {
            MinionAgent minion = minions[i];
            if (minion == null || minion.RoleType != MinionRoleType.Support) continue;
            minion.TrySetSupportMode(mode);
        }
    }

    // Issues a command based on the currently hovered/locked cursor target.
    public void IssueCommandFromCursor()
    {
        MinionAgent[] minions = ResolveControlledMinions();
        if (minions.Length == 0 || cursor == null) return;

        Transform target = ResolveCommandTarget();
        if (target == null) return;

        bool isEnemy = HasTag(target, enemyTag);
        bool isBreakable = HasTag(target, breakableTag);
        bool isAllyMinion = target.GetComponentInParent<MinionAgent>() != null && target != player;

        for (int i = 0; i < minions.Length; i++)
        {
            MinionAgent minion = minions[i];
            if (minion == null) continue;

            if (isEnemy)
            {
                if (minion.RoleType == MinionRoleType.Support)
                {
                    if (minion.ActiveSupportMode == SupportMode.Debuff)
                    {
                        minion.SetSupportCommand(target);
                    }
                }
                else
                {
                    minion.SetAttackEnemyCommand(target);
                }

                continue;
            }

            if (isBreakable)
            {
                if (minion.RoleType != MinionRoleType.Support)
                {
                    minion.SetAttackObjectCommand(target);
                }

                continue;
            }

            if (isAllyMinion)
            {
                if (minion.RoleType == MinionRoleType.Support)
                {
                    minion.SetSupportCommand(target);
                }
            }
        }
    }

    // Recalls all controlled minions to the player.
    public void RecallAll()
    {
        MinionAgent[] minions = ResolveControlledMinions();
        for (int i = 0; i < minions.Length; i++)
        {
            if (minions[i] == null) continue;
            minions[i].SetRecallCommand();
        }
    }

    // Resolves the best command target from lock-on, aim assist, and local cursor overlap.
    private Transform ResolveCommandTarget()
    {
        if (cursor.IsLocked && IsValidTarget(cursor.LockedTarget))
        {
            return cursor.LockedTarget;
        }

        if (IsValidTarget(cursor.AimAssistTarget))
        {
            return cursor.AimAssistTarget;
        }

        Vector3 origin = cursor.WorldPos;
        QueryTriggerInteraction qti = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        int hitCount = Physics.OverlapSphereNonAlloc(origin, Mathf.Max(0.05f, commandAcquireRadius), commandHits, commandTargetMask, qti);
        if (hitCount <= 0) return null;

        Transform bestEnemy = null;
        float bestEnemySq = float.PositiveInfinity;
        Transform bestBreakable = null;
        float bestBreakableSq = float.PositiveInfinity;
        Transform bestAlly = null;
        float bestAllySq = float.PositiveInfinity;
        Transform bestUnknown = null;
        float bestUnknownSq = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = commandHits[i];
            if (hit == null) continue;

            Transform candidate = hit.transform;
            if (!IsValidTarget(candidate)) continue;

            float sq = (candidate.position - origin).sqrMagnitude;

            if (HasTag(candidate, enemyTag))
            {
                if (sq < bestEnemySq)
                {
                    bestEnemySq = sq;
                    bestEnemy = candidate;
                }
                continue;
            }

            if (HasTag(candidate, breakableTag) && breakableTag != enemyTag)
            {
                if (sq < bestBreakableSq)
                {
                    bestBreakableSq = sq;
                    bestBreakable = candidate;
                }

                continue;
            }

            MinionAgent ally = candidate.GetComponentInParent<MinionAgent>();
            if (ally != null && candidate != player)
            {
                if (sq < bestAllySq)
                {
                    bestAllySq = sq;
                    bestAlly = candidate;
                }

                continue;
            }

            // Unknown object under cursor
            if (IsEnvironmentCandidate(candidate))
            {
                continue;
            }

            if (sq < bestUnknownSq)
            {
                bestUnknownSq = sq;
                bestUnknown = candidate;
            }
        }

        if (bestEnemy != null) return bestEnemy;
        if (bestBreakable != null) return bestBreakable;
        if (bestAlly != null) return bestAlly;
        return bestUnknown;
    }

    // Computes and applies command-preview color to cursor visuals.
    private void UpdateCommandPreview()
    {
        if (!enableCommandPreview)
        {
            return;
        }

        MinionAgent[] minions = ResolveControlledMinions();
        if (cursor == null || minions.Length == 0)
        {
            ApplyPreviewColor(previewNoTargetColor);
            return;
        }

        Transform target = ResolveCommandTarget();
        if (!IsValidTarget(target))
        {
            ApplyPreviewColor(previewNoTargetColor);
            return;
        }

        CommandPreviewType previewType = EvaluatePreviewType(target, minions);
        switch (previewType)
        {
            case CommandPreviewType.Attack:
                ApplyPreviewColor(previewAttackColor);
                break;

            case CommandPreviewType.Support:
                ApplyPreviewColor(previewSupportColor);
                break;

            case CommandPreviewType.Invalid:
                ApplyPreviewColor(previewInvalidColor);
                break;

            default:
                ApplyPreviewColor(previewNoTargetColor);
                break;
        }
    }

    private CommandPreviewType EvaluatePreviewType(Transform target, MinionAgent[] minions)
    {
        bool isEnemy = HasTag(target, enemyTag);
        bool isBreakable = HasTag(target, breakableTag);
        bool isAllyMinion = target.GetComponentInParent<MinionAgent>() != null && target != player;

        if (isEnemy)
        {
            bool hasAttackIssuer = false;

            for (int i = 0; i < minions.Length; i++)
            {
                MinionAgent minion = minions[i];
                if (minion == null) continue;

                if (minion.RoleType != MinionRoleType.Support)
                {
                    hasAttackIssuer = true;
                    continue;
                }

                if (minion.ActiveSupportMode == SupportMode.Debuff && minion.CanAcceptSupportTarget(target))
                {
                    hasAttackIssuer = true;
                }
            }

            return hasAttackIssuer ? CommandPreviewType.Attack : CommandPreviewType.Invalid;
        }

        if (isBreakable)
        {
            for (int i = 0; i < minions.Length; i++)
            {
                MinionAgent minion = minions[i];
                if (minion == null) continue;
                if (minion.RoleType != MinionRoleType.Support) return CommandPreviewType.Attack;
            }

            return CommandPreviewType.Invalid;
        }

        if (isAllyMinion)
        {
            bool hasSupport = false;

            for (int i = 0; i < minions.Length; i++)
            {
                MinionAgent minion = minions[i];
                if (minion == null || minion.RoleType != MinionRoleType.Support) continue;

                hasSupport = true;
                if (minion.CanAcceptSupportTarget(target)) return CommandPreviewType.Support;
            }

            return hasSupport ? CommandPreviewType.Invalid : CommandPreviewType.None;
        }

        // Non-environment objects under cursor that don't match valid command tags are invalid targets.
        if (IsEnvironmentCandidate(target))
        {
            return CommandPreviewType.None;
        }

        return CommandPreviewType.Invalid;
    }

    private void ApplyPreviewColor(Color color)
    {
        if (previewRenderers == null || previewRenderers.Length == 0)
        {
            return;
        }

        Color emission = color * Mathf.Max(0f, previewEmissionIntensity);

        for (int i = 0; i < previewRenderers.Length; i++)
        {
            Renderer renderer = previewRenderers[i];
            if (renderer == null) continue;

            renderer.GetPropertyBlock(previewPropertyBlock);
            previewPropertyBlock.SetColor(BaseColorId, color);
            previewPropertyBlock.SetColor(ColorId, color);
            previewPropertyBlock.SetColor(EmissionColorId, emission);
            renderer.SetPropertyBlock(previewPropertyBlock);

            // Additional fallback for shaders that ignore property blocks for color channels.
            Material[] materials = renderer.materials;
            for (int m = 0; m < materials.Length; m++)
            {
                Material mat = materials[m];
                if (mat == null) continue;

                if (mat.HasProperty(BaseColorId))
                {
                    mat.SetColor(BaseColorId, color);
                }

                if (mat.HasProperty(ColorId))
                {
                    mat.SetColor(ColorId, color);
                }

                if (mat.HasProperty(EmissionColorId))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor(EmissionColorId, emission);
                }
            }
        }
    }

    private static bool HasTag(Transform target, string tag)
    {
        if (!IsValidTarget(target)) return false;
        if (string.IsNullOrWhiteSpace(tag)) return false;
        return target.CompareTag(tag);
    }

    private bool IsEnvironmentCandidate(Transform target)
    {
        if (!IsValidTarget(target)) return false;
        if (cursor == null) return false;
        return cursor.IsEnvironmentLayer(target.gameObject.layer);
    }

    private MinionAgent[] ResolveControlledMinions()
    {
        if (controlledMinions != null && controlledMinions.Length > 0)
        {
            return controlledMinions;
        }

        if (!autoFindMinionsIfEmpty)
        {
            return System.Array.Empty<MinionAgent>();
        }

        return FindObjectsByType<MinionAgent>(FindObjectsSortMode.None);
    }

    private bool WasCommandPressedThisFrame()
    {
        if (commandAction != null) return commandAction.action.WasPressedThisFrame();
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private bool WasRecallPressedThisFrame()
    {
        if (recallAction != null) return recallAction.action.WasPressedThisFrame();
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
    }

    private static bool IsValidTarget(Transform target)
    {
        return target != null && target.gameObject.activeInHierarchy;
    }
}
