#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class LungerAnimationBindingDiagnostics
{
    private const string PrefabPath = "Assets/Prefabs/Enemies/PF_Lunger.prefab";

    [MenuItem("Tools/Animation/Diagnose Lunger Bindings")]
    public static void DiagnoseLungerBindings()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[LungerAnimationBindingDiagnostics] Could not load prefab at " + PrefabPath);
            return;
        }

        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Debug.LogError("[LungerAnimationBindingDiagnostics] No Animator found under " + prefab.name + ".", prefab);
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError("[LungerAnimationBindingDiagnostics] Animator '" + animator.name + "' has no controller.", animator);
            return;
        }

        HashSet<string> prefabTransformPaths = new HashSet<string>();
        CollectTransformPaths(animator.transform, animator.transform, prefabTransformPaths);

        StringBuilder report = new StringBuilder();
        report.AppendLine("[LungerAnimationBindingDiagnostics] Prefab: " + PrefabPath);
        report.AppendLine("Animator: " + GetFullPath(animator.transform));
        report.AppendLine("Controller: " + animator.runtimeAnimatorController.name);
        report.AppendLine("Avatar: " + (animator.avatar != null ? animator.avatar.name : "None"));
        report.AppendLine("Transforms under Animator: " + prefabTransformPaths.Count);

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

            ClipBindingSummary summary = AnalyzeClip(clip, prefabTransformPaths);
            foundHardError |= summary.HardError;
            AppendClipReport(report, summary);
        }

        if (foundHardError)
            Debug.LogError(report.ToString(), prefab);
        else
            Debug.Log(report.ToString(), prefab);
    }

    private static ClipBindingSummary AnalyzeClip(AnimationClip clip, HashSet<string> prefabTransformPaths)
    {
        EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
        EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);

        HashSet<string> transformPaths = new HashSet<string>();
        AddTransformBindings(curveBindings, transformPaths);
        AddTransformBindings(objectBindings, transformPaths);

        List<string> matchedPaths = new List<string>();
        List<string> missingPaths = new List<string>();
        List<string> missingButMatchWithoutFirstSegment = new List<string>();
        List<string> missingButMatchWithLungerRigPrefix = new List<string>();

        foreach (string path in transformPaths)
        {
            if (prefabTransformPaths.Contains(path))
            {
                matchedPaths.Add(path);
                continue;
            }

            missingPaths.Add(path);

            string withoutFirstSegment = StripFirstSegment(path);
            if (!string.IsNullOrEmpty(withoutFirstSegment) && prefabTransformPaths.Contains(withoutFirstSegment))
                missingButMatchWithoutFirstSegment.Add(path + " -> " + withoutFirstSegment);

            string withRigPrefix = string.IsNullOrEmpty(path) ? "Lunger_Rig" : "Lunger_Rig/" + path;
            if (prefabTransformPaths.Contains(withRigPrefix))
                missingButMatchWithLungerRigPrefix.Add(path + " -> " + withRigPrefix);
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
            MissingButMatchWithLungerRigPrefix = Sorted(missingButMatchWithLungerRigPrefix),
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
        {
            report.AppendLine("  ERROR: None of this clip's transform paths match transforms under the Animator.");
        }

        AppendSample(report, "  matched sample", summary.MatchedPaths, 8);
        AppendSample(report, "  missing sample", summary.MissingPaths, 12);

        if (summary.MissingButMatchWithLungerRigPrefix.Count > 0)
        {
            report.AppendLine("  LIKELY FIX: Clip paths appear to be missing the 'Lunger_Rig/' prefix. Either re-export/reimport with the same root hierarchy, or put the Animator on Lunger_Rig and reference that Animator from the bridge.");
            AppendSample(report, "  examples", summary.MissingButMatchWithLungerRigPrefix, 8);
        }

        if (summary.MissingButMatchWithoutFirstSegment.Count > 0)
        {
            report.AppendLine("  LIKELY FIX: Clip paths appear to include one extra root object. Re-export/reimport with the same root hierarchy as the prefab Animator.");
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

    private struct ClipBindingSummary
    {
        public AnimationClip Clip;
        public int CurveBindingCount;
        public int ObjectBindingCount;
        public List<string> TransformPaths;
        public List<string> MatchedPaths;
        public List<string> MissingPaths;
        public List<string> MissingButMatchWithoutFirstSegment;
        public List<string> MissingButMatchWithLungerRigPrefix;
        public bool HardError;
    }
}
#endif
