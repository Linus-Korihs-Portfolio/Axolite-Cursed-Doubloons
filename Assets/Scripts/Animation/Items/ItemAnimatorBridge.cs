using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ItemAnimatorBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Animator Parameter Names")]
    [SerializeField] private string openTrigger = "Open";

    [Header("State Names")]
    [SerializeField] private string closedStateName = "Item_Closed";
    [SerializeField] private string openingStateName = "Item_OpeningChest";

    [Header("Settings")]
    [SerializeField] private bool preventMultipleOpens = true;

    private int openHash;
    private bool hasOpenTrigger;
    private bool isOpen;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        CacheHashes();
        CheckAvailableParameters();
    }

    private void OnValidate()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void CacheHashes()
    {
        openHash = Animator.StringToHash(openTrigger);
    }

    private void CheckAvailableParameters()
    {
        hasOpenTrigger = HasParameter(openTrigger, AnimatorControllerParameterType.Trigger);
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("ItemAnimatorBridge: Kein Animator oder kein Animator Controller gefunden.");
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == expectedType)
            {
                return true;
            }
        }

        return false;
    }

    public void OpenItem()
    {
        if (preventMultipleOpens && isOpen)
        {
            Debug.Log("ItemAnimatorBridge: Item ist bereits geöffnet.");
            return;
        }

        isOpen = true;

        if (hasOpenTrigger)
        {
            animator.SetTrigger(openHash);
            return;
        }

        Debug.LogWarning("ItemAnimatorBridge: Open Trigger fehlt. Spiele Opening-State direkt ab.");

        if (HasState(openingStateName))
        {
            animator.Play(openingStateName, 0, 0f);
        }
    }

    public void ResetToClosed()
    {
        isOpen = false;

        if (hasOpenTrigger)
        {
            animator.ResetTrigger(openHash);
        }

        if (HasState(closedStateName))
        {
            animator.Play(closedStateName, 0, 0f);
        }
        else
        {
            Debug.LogWarning("ItemAnimatorBridge: Closed-State nicht gefunden: " + closedStateName);
        }
    }

    public void ForceOpeningAnimation()
    {
        isOpen = true;

        if (HasState(openingStateName))
        {
            animator.Play(openingStateName, 0, 0f);
        }
        else
        {
            Debug.LogWarning("ItemAnimatorBridge: Opening-State nicht gefunden: " + openingStateName);
        }
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    private bool HasState(string stateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        return animator.HasState(0, Animator.StringToHash(stateName));
    }
}