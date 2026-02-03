using UnityEngine;

[DisallowMultipleComponent]
public class Comment : MonoBehaviour
{
    [TextArea(5, 20)]
    public string commentText;
}
