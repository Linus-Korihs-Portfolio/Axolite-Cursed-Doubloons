#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class EnemyAnimationBindingDiagnostics
{
    private static readonly DiagnosticTarget[] Targets =
    {
        new DiagnosticTarget("Lunger", "Assets/Prefabs/Enemies/PF_Lunger.prefab"),
        new DiagnosticTarget("ShellSpinner animated model", "Assets/Prefabs/Enemies/PF_Enemy2.prefab"),
        new DiagnosticTarget("Burrower animated model", "Assets/Prefabs/Enemies/PF_Enemy3.prefab")
    };

    [MenuItem("Tools/Animation/Diagnose Enemy Bindings/All")]
    public static void DiagnoseAllEnemyBindings()
    {
        for (int i = 0; i < Targets.Length; i++)
            Diagnose(Targets[i]);
    }

    [MenuItem("Tools/Animation/Diagnose Enemy Bindings/Lunger")]
    public static void DiagnoseLungerBindings()
    {
        Diagnose(Targets[0]);
    }

    [MenuItem("Tools/Animation/Diagnose Enemy Bindings/ShellSpinner Model")]
    public static void DiagnoseShellSpinnerBindings()
    {
        Diagnose(Targets[1]);
    }

    [MenuItem("Tools/Animation/Diagnose Enemy Bindings/Burrower Model")]
    public static void DiagnoseBurrowerBindings()
    {
        Diagnose(Targets[2]);
    }

    private static void Diagnose(DiagnosticTarget target)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(target.PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[EnemyAnimationBindingDiagnostics] Could not load " + target.Label + " prefab at " + target.PrefabPath);
            return;
        }

        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Debug.LogError("[EnemyAnimationBindingDiagnostics] No Animator found under " + target.Label + " prefab '" + prefab.name + "'.", prefab);
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError("[EnemyAnimationBindingDiagnostics] Animator '" + animator.name + "' has no controller.", animator);
            return;
        }

        HashSet<string> prefabTransformPaths = new HashSet<string>();
        CollectTransformPaths(animator.transform, animator.transform, prefabTransformPaths);

        List<string> topLevelTransformPaths = new List<string>();
        CollectTopLevelTransformPaths(animator.transform, topLevelTransformPaths);

        StringBuilder report = new StringBuilder();
        report.AppendLine("[EnemyAnimationBindingDiagnostics] " + target.Label);
        report.AppendLine("Prefab: " + target.PrefabPath);
        report.AppendLine("Animator: " + GetFullPath(animator.transform));
        report.AppendLine("Controller: " + animator.runtimeAnimatorController.name);
        report.AppendLine("Avatar: " + (animator.avatar != null ? animator.avatar.name : "None"));
        report.AppendLine("Transforms under Animator: " + prefabTransformPaths.Count);
        AppendSample(report, "Top-level transforms under Animator", topLevelTransformPaths, 12);

        SkinnedMeshRenderer[] renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        report.AppendLine("SkinnedMeshRenderers: " + renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            string rootBonePath = GetRelativePath(animator.transform, renderer.rootBone);
            report.AppendLine("  Renderer '" + renderer.name + "' rootBone='" + rootBonePath + "', bones=" + (renderer.bones != null ? renderer.bones.Length : 0));

            if (renderer.rootBone == null)
                report.AppendLine("    ERROR: rootBone is missing.");
            else if (!renderer.rootBone.IsChildOf(animator.transform))
                report.AppendLine("    ERROR: rootBone is outside the Animator hierarchy.");
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        report.AppendLine("Animation clips in controller: " + clips.Length);

        bool foundHardError = false;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null) continue;

            ClipBindingSummary summary = AnalyzeClip(clip, prefabTransformPaths, topLevelTransformPaths);
            foundHardError |= summary.HardError;
            AppendClipReport(report, summary);
        }

        if (foundHardError)
            Debug.LogError(report.ToString(), prefab);
        else
            Debug.Log(report.ToString(), prefab);
    }

    private static ClipBindingSummary AnalyzeClip(AnimationClip clip, HashSet<string> prefabTransformPaths, List<string> topLevelTransformPaths)
    {
        EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
        EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);

        HashSet<string> transformPaths = new HashSet<string>();
        AddTransformBindings(curveBindings, transformPaths);
        AddTransformBindings(objectBindings, transformPaths);

        List<string> matchedPaths = new List<string>();
        List<string> missingPaths = new List<string>();
        List<string> missingButMatchWithoutFirstSegment = new List<string>();
        List<string> missingButMatchWithPrefabPrefix = new List<string>();
        HashSet<string> firstSegments = new HashSet<string>();

        foreach (string path in transformPaths)
        {
            AddFirstSegment(path, firstSegments);

            if (prefabTransformPaths.Contains(path))
            {
                matchedPaths.Add(path);
                continue;
            }

            missingPaths.Add(path);

            string withoutFirstSegment = StripFirstSegment(path);
            if (!string.IsNullOrEmpty(withoutFirstSegment) && prefabTransformPaths.Contains(withoutFirstSegment))
                missingButMatchWithoutFirstSegment.Add(path + " -> " + withoutFirstSegment);

            for (int i = 0; i < topLevelTransformPaths.Count; i++)
            {
                string prefix = topLevelTransformPaths[i];
                string withPrefabPrefix = string.IsNullOrEmpty(path) ? prefix : prefix + "/" + path;
                if (prefabTransformPaths.Contains(withPrefabPrefix))
                    missingButMatchWithPrefabPrefix.Add(path + " -> " + withPrefabPrefix);
            }
        }

        bool hasTransformCurves = transformPaths.Count > 0;
        bool hasAnyMatch = matchedPaths.Count > 0;
        bool hardError = !hasTransformCurves || !hasAnyMatch;

        return new ClipBindingSummary
        {
            Clip = clip,
            CurveBindingCount = curveBindings.Length,
            ObjectBindingCount = objectBindings.Length,
            TransformPaths = Sorted(transformPaths),
            MatchedPaths = Sorted(matchedPaths),
            MissingPaths = Sorted(missingPaths),
            MissingButMatchWithoutFirstSegment = Sorted(missingButMatchWithoutFirstSegment),
            MissingButMatchWithPrefabPrefix = Sorted(missingButMatchWithPrefabPrefix),
            FirstSegments = Sorted(firstSegments),
            HardError = hardError
        };
    }

    private static void AppendClipReport(StringBuilder report, ClipBindingSummary summary)
    {
        report.AppendLine();
        report.AppendLine("Clip '" + summary.Clip.name + "'");
        report.AppendLine("  curveBindings=" + summary.CurveBindingCount +
                          ", objectBindings=" + summary.ObjectBindingCount +
                          ", transformBindingPaths=" + summary.TransformPaths.Count);
        report.AppendLine("  matchedTransformPaths=" + summary.MatchedPaths.Count +
                          ", missingTransformPaths=" + summary.MissingPaths.Count);

        if (summary.TransformPaths.Count == 0)
        {
            report.AppendLine("  ERROR: This clip has no transform curves. It can fire events but cannot move the rig.");
            return;
        }

        if (summary.MatchedPaths.Count == 0)
            report.AppendLine("  ERROR: None of this clip's transform paths match transforms under the Animator.");

        AppendSample(report, "  clip root segments", summary.FirstSegments, 8);
        AppendSample(report, "  matched sample", summary.MatchedPaths, 8);
        AppendSample(report, "  missing sample", summary.MissingPaths, 12);

        if (summary.MissingButMatchWithPrefabPrefix.Count > 0)
        {
            report.AppendLine("  LIKELY FIX: Clip paths appear to be missing one prefab rig/model prefix.");
            AppendSample(report, "  examples", summary.MissingButMatchWithPrefabPrefix, 8);
        }

        if (summary.MissingButMatchWithoutFirstSegment.Count > 0)
        {
            report.AppendLine("  LIKELY FIX: Clip paths appear to include one extra root object. Rename/re-parent the rig to match the clip root, or re-export with the same root hierarchy.");
            AppendSample(report, "  examples", summary.MissingButMatchWithoutFirstSegment, 8);
        }
    }

    private static void AddTransformBindings(EditorCurveBinding[] bindings, HashSet<string> paths)
    {
        for (int i = 0; i < bindings.Length; i++)
        {
            EditorCurveBinding binding = bindings[i];
            if (binding.type == typeof(Transform))
                paths.Add(binding.path ?? string.Empty);
        }
    }

    private static void CollectTransformPaths(Transform root, Transform current, HashSet<string> paths)
    {
        paths.Add(GetRelativePath(root, current));

        for (int i = 0; i < current.childCount; i++)
            CollectTransformPaths(root, current.GetChild(i), paths);
    }

    private static void CollectTopLevelTransformPaths(Transform animatorRoot, List<string> paths)
    {
        paths.Clear();
        paths.Add(string.Empty);

        for (int i = 0; i < animatorRoot.childCount; i++)
            paths.Add(animatorRoot.GetChild(i).name);

        paths.Sort();
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (root == null || target == null) return string.Empty;
        if (root == target) return string.Empty;

        List<string> names = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            names.Add(current.name);
            current = current.parent;
        }

        if (current != root) return GetFullPath(target);

        names.Reverse();
        return string.Join("/", names);
    }

    private static string GetFullPath(Transform target)
    {
        if (target == null) return "None";

        List<string> names = new List<string>();
        Transform current = target;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static string StripFirstSegment(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        int slash = path.IndexOf('/');
        return slash >= 0 && slash + 1 < path.Length ? path.Substring(slash + 1) : string.Empty;
    }

    private static void AddFirstSegment(string path, HashSet<string> firstSegments)
    {
        if (string.IsNullOrEmpty(path))
        {
            firstSegments.Add("(root)");
            return;
        }

        int slash = path.IndexOf('/');
        firstSegments.Add(slash >= 0 ? path.Substring(0, slash) : path);
    }

    private static List<string> Sorted(IEnumerable<string> values)
    {
        List<string> result = new List<string>(values);
        result.Sort();
        return result;
    }

    private static void AppendSample(StringBuilder report, string label, List<string> values, int max)
    {
        if (values.Count == 0) return;

        report.AppendLine(label + ":");
        int count = Mathf.Min(values.Count, max);
        for (int i = 0; i < count; i++)
            report.AppendLine("    " + values[i]);

        if (values.Count > count)
            report.AppendLine("    ... +" + (values.Count - count) + " more");
    }

    private struct DiagnosticTarget
    {
        public DiagnosticTarget(string label, string prefabPath)
        {
            Label = label;
            PrefabPath = prefabPath;
        }

        public string Label;
        public string PrefabPath;
    }

    private struct ClipBindingSummary
    {
        public AnimationClip Clip;
        public int CurveBindingCount;
        public int ObjectBindingCount;
        public List<string> TransformPaths;
        public List<string> MatchedPaths;
        public List<string> MissingPaths;
        public List<string> MissingButMatchWithoutFirstSegment;
        public List<string> MissingButMatchWithPrefabPrefix;
        public List<string> FirstSegments;
        public bool HardError;
    }
}
#endif
