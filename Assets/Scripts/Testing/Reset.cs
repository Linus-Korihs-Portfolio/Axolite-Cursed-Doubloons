using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Reset : MonoBehaviour
{
    [SerializeField] private Vector3 startPos;

    private void Awake()
    {
        startPos = transform.position;
    }

    public void ResetPosition()
    {
        Debug.Log("Resetting position to " + startPos);
        GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        transform.position = startPos;
        Debug.Log("Position reset. Current position: " + transform.position);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(Reset))]
public class ResetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var gen = (Reset)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Reset Position"))
        {
            gen.ResetPosition();
            EditorUtility.SetDirty(gen);
        }
    }
}
#endif