using UnityEngine;

[CreateAssetMenu(menuName = "Items/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    [TextArea] public string description;

    [Header("Spawn Settings")]
    public int weight = 10;       // higher = more likely
    public bool canRepeat = false;
}