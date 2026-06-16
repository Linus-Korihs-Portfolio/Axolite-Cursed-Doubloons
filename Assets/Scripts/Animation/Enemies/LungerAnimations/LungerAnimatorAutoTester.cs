using System.Collections;
using UnityEngine;

public class LungerAnimatorAutoTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LungerAnimatorBridge lungerAnimator;

    [Header("Auto Test")]
    [SerializeField] private bool autoRunOnStart = true;

    [Tooltip("Wenn aktiv, läuft die komplette Testsequenz immer wieder von vorne.")]
    [SerializeField] private bool loopTest = false;

    [SerializeField] private float idleWait = 1.5f;
    [SerializeField] private float walkDuration = 2.0f;
    [SerializeField] private float attackWait = 1.5f;
    [SerializeField] private float lungeWait = 1.5f;
    [SerializeField] private float idleBreakWait = 1.5f;
    [SerializeField] private float deathWait = 2.0f;
    [SerializeField] private float delayBetweenLoops = 1.0f;

    [SerializeField] private float walkSpeed = 1.0f;

    private void Awake()
    {
        if (lungerAnimator == null)
        {
            lungerAnimator = GetComponent<LungerAnimatorBridge>();
        }

        if (lungerAnimator == null)
        {
            Debug.LogError("LungerAnimatorAutoTester: Keine LungerAnimatorBridge gefunden.");
        }
    }

    private IEnumerator Start()
    {
        if (autoRunOnStart && lungerAnimator != null)
        {
            if (loopTest)
            {
                while (true)
                {
                    yield return RunFullAnimationTest();

                    Debug.Log("AUTO TEST LOOP: Restarting test sequence.");
                    yield return new WaitForSeconds(delayBetweenLoops);

                    ResetAnimatorForNextLoop();
                }
            }
            else
            {
                yield return RunFullAnimationTest();
            }
        }
    }

    private IEnumerator RunFullAnimationTest()
    {
        Debug.Log("AUTO TEST START: Idle");

        // Idle
        lungerAnimator.SetSpeed(0f);
        lungerAnimator.SetDead(false);
        yield return new WaitForSeconds(idleWait);

        // Walk
        Debug.Log("AUTO TEST: Walk");
        lungerAnimator.SetSpeed(walkSpeed);
        yield return new WaitForSeconds(walkDuration);

        // Back to Idle
        Debug.Log("AUTO TEST: Back to Idle");
        lungerAnimator.SetSpeed(0f);
        yield return new WaitForSeconds(idleWait);

        // Main Attack
        Debug.Log("AUTO TEST: MainAttack");
        lungerAnimator.PlayMainAttack();
        yield return new WaitForSeconds(attackWait);

        // Lunge Attack
        Debug.Log("AUTO TEST: LungeAttack");
        lungerAnimator.PlayLungeAttack();
        yield return new WaitForSeconds(lungeWait);

        // Idle Break optional
        Debug.Log("AUTO TEST: IdleBreak");
        lungerAnimator.PlayIdleBreak();
        yield return new WaitForSeconds(idleBreakWait);

        // Death
        Debug.Log("AUTO TEST: Death");
        lungerAnimator.SetDead(true);
        yield return new WaitForSeconds(deathWait);

        Debug.Log("AUTO TEST FINISHED");
    }

    private void ResetAnimatorForNextLoop()
    {
        lungerAnimator.ResetToIdle();
    }
}