using UnityEngine;

public class ItemAnimationEvents : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public void OnChestOpenStarted()
    {
        LogEvent("Chest opening started");
    }

    public void OnChestLidUnlocked()
    {
        LogEvent("Chest lid unlocked / latch opened");
    }

    public void OnChestHalfOpen()
    {
        LogEvent("Chest half open");
    }

    public void OnLootSpawnMoment()
    {
        LogEvent("Loot spawn moment reached");
    }

    public void OnChestFullyOpen()
    {
        LogEvent("Chest fully open");
    }

    public void OnChestOpenFinished()
    {
        LogEvent("Chest opening animation finished");
    }

    private void LogEvent(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log("[Item Animation Event] " + gameObject.name + ": " + message);
    }
}