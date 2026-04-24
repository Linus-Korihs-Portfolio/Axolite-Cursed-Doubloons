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
    [SerializeField] private PlayerMinionCommanderSettings settings;
    [SerializeField] private GroundCursor cursor;
    [SerializeField] private Transform player;

    [Header("Input")]
    [SerializeField] private InputActionReference commandAction;
    [SerializeField] private InputActionReference recallAction;


    [Header("Minion Selection")]
    [SerializeField] private MinionAgent[] controlledMinions;
    [SerializeField] private bool autoFindMinionsIfEmpty = true;
    [Header("Command Preview")]
    [SerializeField] private Renderer[] previewRenderers;

    private readonly Collider[] commandHits = new Collider[32];
    private MinionAgent[] cachedAutoMinions;
    private float nextAutoFindRefreshTime;
    private MaterialPropertyBlock previewPropertyBlock;
    private Color lastAppliedPreviewColor;
    private bool hasAppliedPreviewColor;
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

    // Exposed for editor display only.
    public InputActionReference CommandAction => commandAction;
    public InputActionReference RecallAction => recallAction;

    private void Start()
    {
        // Auto-populate the controlled minions list so it's visible in the inspector at runtime.
        if (autoFindMinionsIfEmpty && (controlledMinions == null || controlledMinions.Length == 0))
        {
            controlledMinions = FindObjectsByType<MinionAgent>(FindObjectsSortMode.None);
            cachedAutoMinions = controlledMinions;
            nextAutoFindRefreshTime = Time.time + Mathf.Max(0.05f, settings != null ? settings.autoFindRefreshInterval : 5f);
        }
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

        string enemyTagValue = settings.enemyTag;
        string breakableTagValue = settings.breakableTag;
        bool isEnemy = HasTag(target, enemyTagValue);
        bool isBreakable = HasTag(target, breakableTagValue);
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
        return ResolveTargetFromCursor(includeLockTarget: true, includeAimAssistTarget: true, acquireRadius: settings.commandAcquireRadius);
    }

    // Resolves preview target strictly from the cursor position (no lock/aim-assist shortcuts).
    private Transform ResolvePreviewTarget()
    {
        return ResolveTargetFromCursor(includeLockTarget: false, includeAimAssistTarget: false, acquireRadius: settings.previewAcquireRadius);
    }

    // Shared target resolution for both command issuing and preview modes.
    private Transform ResolveTargetFromCursor(bool includeLockTarget, bool includeAimAssistTarget, float acquireRadius)
    {
        if (includeLockTarget && cursor.IsLocked && IsValidTarget(cursor.LockedTarget))
        {
            return cursor.LockedTarget;
        }

        if (includeAimAssistTarget && IsValidTarget(cursor.AimAssistTarget))
        {
            return cursor.AimAssistTarget;
        }

        Vector3 origin = cursor.WorldPos;
        string enemyTagValue = settings.enemyTag;
        string breakableTagValue = settings.breakableTag;
        QueryTriggerInteraction qti = settings.includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        int hitCount = Physics.OverlapSphereNonAlloc(origin, Mathf.Max(0.05f, acquireRadius), commandHits, settings.commandTargetMask, qti);
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

            if (HasTag(candidate, enemyTagValue))
            {
                if (sq < bestEnemySq)
                {
                    bestEnemySq = sq;
                    bestEnemy = candidate;
                }
                continue;
            }

            if (HasTag(candidate, breakableTagValue) && breakableTagValue != enemyTagValue)
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
        if (!settings.enableCommandPreview)
        {
            return;
        }

        MinionAgent[] minions = ResolveControlledMinions();
        if (cursor == null || minions.Length == 0)
        {
            ApplyPreviewColor(settings.previewNoTargetColor);
            return;
        }

        Transform target = ResolvePreviewTarget();
        if (!IsValidTarget(target))
        {
            ApplyPreviewColor(settings.previewNoTargetColor);
            return;
        }

        CommandPreviewType previewType = EvaluatePreviewType(target, minions);
        switch (previewType)
        {
            case CommandPreviewType.Attack:
                ApplyPreviewColor(settings.previewAttackColor);
                break;

            case CommandPreviewType.Support:
                ApplyPreviewColor(settings.previewSupportColor);
                break;

            case CommandPreviewType.Invalid:
                ApplyPreviewColor(settings.previewInvalidColor);
                break;

            default:
                ApplyPreviewColor(settings.previewNoTargetColor);
                break;
        }
    }

    private CommandPreviewType EvaluatePreviewType(Transform target, MinionAgent[] minions)
    {
        string enemyTagValue = settings.enemyTag;
        string breakableTagValue = settings.breakableTag;
        bool isEnemy = HasTag(target, enemyTagValue);
        bool isBreakable = HasTag(target, breakableTagValue);
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

        if (hasAppliedPreviewColor && ColorsAlmostEqual(lastAppliedPreviewColor, color))
        {
            return;
        }

        hasAppliedPreviewColor = true;
        lastAppliedPreviewColor = color;

        Color emission = color * Mathf.Max(0f, settings.previewEmissionIntensity);

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

    private static bool ColorsAlmostEqual(Color a, Color b)
    {
        const float epsilon = 0.001f;
        return Mathf.Abs(a.r - b.r) < epsilon
               && Mathf.Abs(a.g - b.g) < epsilon
               && Mathf.Abs(a.b - b.b) < epsilon
               && Mathf.Abs(a.a - b.a) < epsilon;
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

        if (cachedAutoMinions == null || Time.time >= nextAutoFindRefreshTime)
        {
            cachedAutoMinions = FindObjectsByType<MinionAgent>(FindObjectsSortMode.None);
            nextAutoFindRefreshTime = Time.time + Mathf.Max(0.05f, settings.autoFindRefreshInterval);
        }

        return cachedAutoMinions;
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
