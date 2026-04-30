using UnityEngine;

public class RunStartTrigger : MonoBehaviour
{
    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        if (other.CompareTag("Player"))
        {
            triggered = true;

            RunStartUI.Instance.Open();
        }
    }
}