using System.Collections;
using UnityEngine;

public class ItemAnimatorAutoTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ItemAnimatorBridge itemAnimator;

    [Header("Auto Test Settings")]
    [SerializeField] private bool autoRunOnStart = true;

    [Tooltip("Wenn aktiv, wird die Testsequenz immer wiederholt.")]
    [SerializeField] private bool loopTest = false;

    [Header("Timing")]
    [SerializeField] private float closedWait = 1.0f;
    [SerializeField] private float openingAnimationWait = 2.5f;
    [SerializeField] private float delayBetweenLoops = 1.0f;

    private Coroutine testRoutine;

    private void Awake()
    {
        if (itemAnimator == null)
        {
            itemAnimator = GetComponent<ItemAnimatorBridge>();
        }

        if (itemAnimator == null)
        {
            Debug.LogError("ItemAnimatorAutoTester: Keine ItemAnimatorBridge gefunden. Bitte beide Scripts auf dasselbe GameObject legen.");
        }
    }

    private void Start()
    {
        if (autoRunOnStart && itemAnimator != null)
        {
            StartAutoTest();
        }
    }

    public void StartAutoTest()
    {
        if (testRoutine != null)
        {
            StopCoroutine(testRoutine);
        }

        testRoutine = StartCoroutine(AutoTestRoutine());
    }

    public void StopAutoTest()
    {
        if (testRoutine != null)
        {
            StopCoroutine(testRoutine);
            testRoutine = null;
        }
    }

    private IEnumerator AutoTestRoutine()
    {
        do
        {
            Debug.Log("ITEM AUTO TEST START");

            Debug.Log("ITEM AUTO TEST: Reset to Closed");
            itemAnimator.ResetToClosed();
            yield return new WaitForSeconds(closedWait);

            Debug.Log("ITEM AUTO TEST: Open Item");
            itemAnimator.OpenItem();
            yield return new WaitForSeconds(openingAnimationWait);

            Debug.Log("ITEM AUTO TEST FINISHED");

            if (loopTest)
            {
                Debug.Log("ITEM AUTO TEST LOOP: Restarting.");
                yield return new WaitForSeconds(delayBetweenLoops);
            }

        } while (loopTest);

        testRoutine = null;
    }
}