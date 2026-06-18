using System.Collections;
using UnityEngine;

public class KelpAnimatorAutoTester : MonoBehaviour
{
    private enum TestScenario
    {
        IdleWalk,
        IdleBreak,
        PunchStanding,
        PunchWalking,
        CallMinionsStanding,
        CallMinionsWalking,
        OrderMinionsStanding,
        OrderMinionsWalking,
        DismissStanding,
        DismissWalking,
        Dodge,
        Interact,
        Death,
        FullBasicCycle
    }

    [Header("References")]
    [SerializeField] private KelpAnimatorBridge kelpAnimator;

    [Header("Auto Test")]
    [SerializeField] private bool autoRunOnStart = true;
    [SerializeField] private bool loopTest = false;
    [SerializeField] private TestScenario scenario = TestScenario.FullBasicCycle;

    [Header("Timing")]
    [SerializeField] private float idleWait = 1.5f;
    [SerializeField] private float walkWait = 2.0f;
    [SerializeField] private float actionWait = 2.0f;
    [SerializeField] private float idleBreakWait = 3.0f;
    [SerializeField] private float dodgeWait = 1.5f;
    [SerializeField] private float deathWait = 2.5f;
    [SerializeField] private float delayBetweenLoops = 1.0f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.0f;

    private Coroutine testRoutine;

    private void Awake()
    {
        if (kelpAnimator == null)
        {
            kelpAnimator = GetComponent<KelpAnimatorBridge>();
        }

        if (kelpAnimator == null)
        {
            Debug.LogError("KelpAnimatorAutoTester: Keine KelpAnimatorBridge gefunden.");
        }
    }

    private void Start()
    {
        if (autoRunOnStart && kelpAnimator != null)
        {
            StartAutoTest();
        }
    }

    public void StartAutoTest()
    {
        StopAutoTest();
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
            Debug.Log("KELP AUTO TEST START: " + scenario);

            switch (scenario)
            {
                case TestScenario.IdleWalk:
                    yield return TestIdleWalk();
                    break;

                case TestScenario.IdleBreak:
                    yield return TestIdleBreak();
                    break;

                case TestScenario.PunchStanding:
                    yield return TestStandingAction(kelpAnimator.PlayPunch);
                    break;

                case TestScenario.PunchWalking:
                    yield return TestWalkingAction(kelpAnimator.PlayPunch);
                    break;

                case TestScenario.CallMinionsStanding:
                    yield return TestStandingAction(kelpAnimator.PlayCallMinions);
                    break;

                case TestScenario.CallMinionsWalking:
                    yield return TestWalkingAction(kelpAnimator.PlayCallMinions);
                    break;

                case TestScenario.OrderMinionsStanding:
                    yield return TestStandingAction(kelpAnimator.PlayOrderMinions);
                    break;

                case TestScenario.OrderMinionsWalking:
                    yield return TestWalkingAction(kelpAnimator.PlayOrderMinions);
                    break;

                case TestScenario.DismissStanding:
                    yield return TestStandingAction(kelpAnimator.PlayDismiss);
                    break;

                case TestScenario.DismissWalking:
                    yield return TestWalkingAction(kelpAnimator.PlayDismiss);
                    break;

                case TestScenario.Dodge:
                    yield return TestStandingAction(kelpAnimator.PlayDodge, dodgeWait);
                    break;

                case TestScenario.Interact:
                    yield return TestStandingAction(kelpAnimator.PlayInteract);
                    break;

                case TestScenario.Death:
                    yield return TestDeath();
                    break;

                case TestScenario.FullBasicCycle:
                    yield return TestFullBasicCycle();
                    break;
            }

            Debug.Log("KELP AUTO TEST FINISHED: " + scenario);

            if (loopTest)
            {
                yield return new WaitForSeconds(delayBetweenLoops);
            }

        } while (loopTest);

        testRoutine = null;
    }

    private IEnumerator TestIdleWalk()
    {
        kelpAnimator.ResetToIdle();
        yield return new WaitForSeconds(idleWait);

        kelpAnimator.SetSpeed(walkSpeed);
        yield return new WaitForSeconds(walkWait);

        kelpAnimator.SetSpeed(0f);
        yield return new WaitForSeconds(idleWait);
    }

    private IEnumerator TestIdleBreak()
    {
        kelpAnimator.ResetToIdle();
        yield return new WaitForSeconds(idleWait);

        kelpAnimator.PlayIdleBreak();
        yield return new WaitForSeconds(idleBreakWait);
    }

    private IEnumerator TestStandingAction(System.Action action, float waitOverride = -1f)
    {
        kelpAnimator.ResetToIdle();
        yield return new WaitForSeconds(idleWait);

        kelpAnimator.SetSpeed(0f);
        action.Invoke();

        yield return new WaitForSeconds(waitOverride > 0f ? waitOverride : actionWait);
    }

    private IEnumerator TestWalkingAction(System.Action action)
    {
        kelpAnimator.ResetToIdle();
        yield return new WaitForSeconds(0.5f);

        kelpAnimator.SetSpeed(walkSpeed);
        yield return new WaitForSeconds(0.5f);

        action.Invoke();
        yield return new WaitForSeconds(actionWait);

        kelpAnimator.SetSpeed(0f);
        yield return new WaitForSeconds(idleWait);
    }

    private IEnumerator TestDeath()
    {
        kelpAnimator.ResetToIdle();
        yield return new WaitForSeconds(0.5f);

        kelpAnimator.SetDead(true);
        yield return new WaitForSeconds(deathWait);
    }

    private IEnumerator TestFullBasicCycle()
    {
        yield return TestIdleWalk();
        yield return TestIdleBreak();

        yield return TestStandingAction(kelpAnimator.PlayPunch);
        yield return TestWalkingAction(kelpAnimator.PlayPunch);

        yield return TestStandingAction(kelpAnimator.PlayCallMinions);
        yield return TestWalkingAction(kelpAnimator.PlayCallMinions);

        yield return TestStandingAction(kelpAnimator.PlayOrderMinions);
        yield return TestWalkingAction(kelpAnimator.PlayOrderMinions);

        yield return TestStandingAction(kelpAnimator.PlayDismiss);
        yield return TestWalkingAction(kelpAnimator.PlayDismiss);

        yield return TestStandingAction(kelpAnimator.PlayDodge, dodgeWait);
        yield return TestStandingAction(kelpAnimator.PlayInteract);
    }
}