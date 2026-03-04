#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

[CustomEditor(typeof(PlayerBrain))]
public class PlayerBrainEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var brain = (PlayerBrain)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Current Bindings", EditorStyles.boldLabel);

        DrawBinding("Move", brain.MoveAction);
        DrawBinding("Dodge", brain.DodgeAction);
        DrawBinding("Punch", brain.PunchAction);
        DrawBinding("Camera Look", brain.CameraLookAction);
        DrawBinding("Camera Toggle", brain.CameraToggleAction);
        DrawBinding("Camera Zoom", brain.CameraZoomAction);
        DrawBinding("Camera Lock-On", brain.CameraLockOnAction);
        DrawBinding("Cursor Move", brain.CursorMoveAction);
        DrawBinding("Cursor Extra Key", brain.CursorExtraKeyAction);
    }

    private void DrawBinding(string label, InputActionReference actionRef)
    {
        if (actionRef == null || actionRef.action == null)
        {
            EditorGUILayout.HelpBox($"{label}: No Action Assigned", MessageType.Warning);
            return;
        }

        var action = actionRef.action;
        string kbMouse = JoinBindingsForGroups(action, "Keyboard&Mouse", "Keyboard", "Mouse");
        string gamepad = JoinBindingsForGroups(action, "Gamepad");

        if (string.IsNullOrWhiteSpace(kbMouse)) kbMouse = "Unbound";
        if (string.IsNullOrWhiteSpace(gamepad)) gamepad = "Unbound";

        string text = $"{label}\nKeyboard/Mouse:\n{kbMouse}\n\nGamepad:\n{gamepad}";
        EditorGUILayout.TextArea(text, GUILayout.MinHeight(60));
    }

    private string JoinBindingsForGroups(InputAction action, params string[] groups)
    {
        bool MatchesGroups(InputBinding b)
        {
            if (groups == null || groups.Length == 0) return true;
            if (string.IsNullOrEmpty(b.groups)) return false;
            return groups.Any(g => b.groups.Contains(g));
        }

        bool LooksKbMouse(InputBinding b)
        {
            string path = b.effectivePath ?? b.path ?? "";
            return path.Contains("Keyboard") || path.Contains("Mouse") || path.Contains("Pointer") || path.Contains("delta") || path.Contains("scroll");
        }

        bool LooksGamepad(InputBinding b)
        {
            string path = b.effectivePath ?? b.path ?? "";
            return path.Contains("Gamepad");
        }

        bool MatchesByHeuristic(InputBinding b)
        {
            if (!string.IsNullOrEmpty(b.groups)) return MatchesGroups(b);

            bool wantGamepad = groups.Any(g => g.Contains("Gamepad"));
            bool wantKbMouse = !wantGamepad;

            return wantGamepad ? LooksGamepad(b) : LooksKbMouse(b);
        }

        string Human(InputBinding b)
        {
            return InputControlPath.ToHumanReadableString(b.effectivePath ?? b.path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }

        var lines = new List<string>();
        var seen = new HashSet<string>();

        for (int i = 0; i < action.bindings.Count; i++)
        {
            var b = action.bindings[i];

            if (b.isPartOfComposite) continue;

            if (b.isComposite)
            {
                for (int j = i + 1; j < action.bindings.Count && action.bindings[j].isPartOfComposite; j++)
                {
                    var part = action.bindings[j];
                    if (!MatchesByHeuristic(part)) continue;

                    string partName = string.IsNullOrWhiteSpace(part.name) ? "part" : part.name;
                    string key = Human(part);
                    string line = $"{partName}: {key}";

                    if (seen.Add(line)) lines.Add(line);
                }
                continue;
            }

            if (!MatchesByHeuristic(b)) continue;

            string display = action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            if (string.IsNullOrWhiteSpace(display)) display = Human(b);

            if (!string.IsNullOrWhiteSpace(display) && seen.Add(display)) lines.Add(display);
        }
        return string.Join("\n", lines);
    }
}
#endif
