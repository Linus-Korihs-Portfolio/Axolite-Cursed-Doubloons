using System.Collections;
using UnityEngine;

public class Enemy3AnimatorAutoTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Enemy3AnimatorBridge enemyAnimator;

    [Header("Auto Test Settings")]
    [SerializeField] private bool autoRunOnStart = true;

    [Tooltip("Wenn aktiv, wird die komplette Testsequenz wiederholt.")]
    [SerializeField] private bool loopTest = false;

    [Header("Which Animation Parts Should Be Tested?")]
    [SerializeField] private bool testWakeUpSequence = true;
    [SerializeField] private bool testSecondAttack = true;
    [SerializeField] private bool testGrabAttack = true;
    [SerializeField] private bool testLowHpEscape = true;
    [SerializeField] private bool testDirectDigDown = false;

    [Header("Death Test")]
    [Tooltip("DeathFlying beendet die normale Sequenz. Nur aktivieren, wenn du genau diesen Death testen willst.")]
    [SerializeField] private bool testDeathFlying = false;

    [Tooltip("DeathDigging beendet die normale Sequenz. Nur aktivieren, wenn du genau diesen Death testen willst.")]
    [SerializeField] private bool testDeathDigging = false;

    [Header("Timing")]
    [SerializeField] private float hiddenWait = 1.0f;

    [Tooltip("Wartezeit für FirstAttack + FlyUp bis Walk erreicht ist.")]
    [SerializeField] private float wakeUpSequenceWait = 3.0f;

    [Tooltip("Wartezeit für SecondAttack bis Hover erreicht ist.")]
    [SerializeField] private float secondAttackWait = 2.0f;

    [Tooltip("Wartezeit für GrabAttack bis Hover wieder erreicht ist.")]
    [SerializeField] private float grabAttackWait = 2.0f;

    [Tooltip("Wartezeit für FlyDown + DiggingAndDown bis HiddenArmed wieder erreicht ist.")]
    [SerializeField] private float lowHpEscapeWait = 3.0f;

    [SerializeField] private float directDigDownWait = 2.0f;
    [SerializeField] private float deathWait = 2.0f;
    [SerializeField] private float delayBetweenLoops = 1.0f;

    [Header("Optional Speed Test")]
    [SerializeField] private bool setSpeedDuringWalk = false;
    [SerializeField] private float walkSpeed = 1.0f;

    private Coroutine testRoutine;

    private void Awake()
    {
        if (enemyAnimator == null)
        {
            enemyAnimator = GetComponent<Enemy3AnimatorBridge>();
        }

        if (enemyAnimator == null)
        {
            Debug.LogError("Enemy3AnimatorAutoTester: Keine Enemy3AnimatorBridge gefunden. Bitte beide Scripts auf dasselbe GameObject legen.");
        }
    }

    private void Start()
    {
        if (autoRunOnStart && enemyAnimator != null)
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
            Debug.Log("ENEMY 3 AUTO TEST START");

            enemyAnimator.ResetToHidden();
            yield return new WaitForSeconds(hiddenWait);

            if (testWakeUpSequence)
            {
                Debug.Log("ENEMY 3 AUTO TEST: WakeUp -> FirstAttack -> FlyUp -> Walk");
                enemyAnimator.WakeUp();
                yield return new WaitForSeconds(wakeUpSequenceWait);
            }
            else
            {
                Debug.Log("ENEMY 3 AUTO TEST: Skipping WakeUp, jumping to WalkBase");
                enemyAnimator.ResetToWalkBase();
                yield return new WaitForSeconds(0.2f);
            }

            if (setSpeedDuringWalk)
            {
                Debug.Log("ENEMY 3 AUTO TEST: Set Speed during Walk/Base");
                enemyAnimator.SetSpeed(walkSpeed);
                yield return new WaitForSeconds(0.5f);
                enemyAnimator.SetSpeed(0f);
            }

            if (testSecondAttack)
            {
                Debug.Log("ENEMY 3 AUTO TEST: SecondAttack -> Hover");
                enemyAnimator.PlaySecondAttack();
                yield return new WaitForSeconds(secondAttackWait);
            }

            if (testGrabAttack)
            {
                Debug.Log("ENEMY 3 AUTO TEST: GrabAttack -> Hover");
                enemyAnimator.PlayGrabAttack();
                yield return new WaitForSeconds(grabAttackWait);
            }

            if (testLowHpEscape)
            {
                Debug.Log("ENEMY 3 AUTO TEST: Low HP Escape -> FlyDown -> DiggingAndDown -> HiddenArmed");
                enemyAnimator.PlayFlyDown();
                yield return new WaitForSeconds(lowHpEscapeWait);
            }

            if (testDirectDigDown)
            {
                Debug.Log("ENEMY 3 AUTO TEST: Direct DigDown");
                enemyAnimator.PlayDigDown();
                yield return new WaitForSeconds(directDigDownWait);
            }

            if (testDeathFlying)
            {
                Debug.Log("ENEMY 3 AUTO TEST: DeathFlying");
                enemyAnimator.PlayDeathFlying();
                yield return new WaitForSeconds(deathWait);
            }

            if (testDeathDigging)
            {
                Debug.Log("ENEMY 3 AUTO TEST: DeathDigging");
                enemyAnimator.PlayDeathDigging();
                yield return new WaitForSeconds(deathWait);
            }

            Debug.Log("ENEMY 3 AUTO TEST FINISHED");

            if (loopTest)
            {
                Debug.Log("ENEMY 3 AUTO TEST LOOP: Reset and restart.");
                enemyAnimator.ResetToHidden();
                yield return new WaitForSeconds(delayBetweenLoops);
            }

        } while (loopTest);

        testRoutine = null;
    }
}