using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    private bool triggered = false;

    private void OnTriggerEnter(Collider other) // ✅ 3D version
    {
        Debug.Log("Trigger entered by: " + other.name);

        if (triggered) return;

        if (other.CompareTag("Player"))
        {
            Debug.Log("Player entered!");

            triggered = true;

            if (ItemSelectionUI.Instance != null)
            {
                ItemSelectionUI.Instance.Open();
            }
            else
            {
                Debug.LogError("ItemSelectionUI Instance is NULL!");
            }

            gameObject.SetActive(false);
        }
    }
}