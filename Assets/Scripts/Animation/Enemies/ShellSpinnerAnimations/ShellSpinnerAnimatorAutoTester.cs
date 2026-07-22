using System.Collections;
using UnityEngine;

public class ShellSpinnerAnimatorAutoTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShellSpinnerAnimatorBridge shellSpinnerAnimator;

    [Header("Auto Test Settings")]
    [SerializeField] private bool autoRunOnStart = true;

    [Tooltip("Wenn aktiv, wird die komplette Testsequenz immer wiederholt.")]
    [SerializeField] private bool loopTest = false;

    [Header("Which Animations Should Be Tested?")]
    [SerializeField] private bool testIdle = true;
    [SerializeField] private bool testWalk = true;
    [SerializeField] private bool testProjectileAttackWithoutTurn = true;
    [SerializeField] private bool testProjectileAttackWithTurn = true;
    [SerializeField] private bool testProjectileAttackWithTurnAndRepeatShot = false;
    [SerializeField] private bool testSpinAttackNormal = true;
    [SerializeField] private bool testSpinAttackLong = true;
    [SerializeField] private bool testIdleBreak = true;
    [SerializeField] private bool testDeath = true;

    [Header("Timing")]
    [SerializeField] private float idleWait = 1.5f;
    [SerializeField] private float walkDuration = 2.0f;
    [SerializeField] private float projectileAttackWithoutTurnWait = 2.5f;
    [SerializeField] private float projectileAttackWithTurnWait = 3.2f;
    [SerializeField] private float projectileAttackRepeatWait = 4.0f;
    [SerializeField] private float spinAttackNormalWait = 4.0f;

    [Tooltip("Wie lange SpinAttack_2_4 länger laufen soll, bevor ContinueSpinning auf false gesetzt wird.")]
    [SerializeField] private float longSpinHoldTime = 3.0f;

    [Tooltip("Wartezeit nach dem Stoppen des langen Spins, damit SpinAttack_3_4 und 4_4 fertig ablaufen.")]
    [SerializeField] private float spinAttackFinishWait = 2.0f;

    [SerializeField] private float idleBreakWait = 1.5f;
    [SerializeField] private float deathWait = 2.0f;
    [SerializeField] private float delayBetweenLoops = 1.0f;

    [Header("Movement Test")]
    [SerializeField] private float walkSpeed = 1.0f;

    private Coroutine testRoutine;

    private void Awake()
    {
        if (shellSpinnerAnimator == null)
        {
            shellSpinnerAnimator = GetComponent<ShellSpinnerAnimatorBridge>();
        }

        if (shellSpinnerAnimator == null)
        {
            Debug.LogError("ShellSpinnerAnimatorAutoTester: Keine ShellSpinnerAnimatorBridge gefunden. Bitte beide Scripts auf dasselbe GameObject legen.");
        }
    }

    private void Start()
    {
        if (autoRunOnStart && shellSpinnerAnimator != null)
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
            Debug.Log("SHELL SPINNER AUTO TEST START");

            shellSpinnerAnimator.ResetToIdle();
            yield return new WaitForSeconds(0.2f);

            if (testIdle)
            {
                Debug.Log("SHELL SPINNER AUTO TEST: Idle");
                shellSpinnerAnimator.SetSpeed(0f);
                yield return new WaitForSeconds(idleWait);
            }

            if (testWalk)
            {
                Debug.Log("SHELL SPINNER AUTO TEST: Walk");
                shellSpinnerAnimator.SetSpeed(walkSpeed);
                yield return new WaitForSeconds(walkDuration);

                Debug.Log("SHELL SPINNER AUTO TEST: Back to Idle");
                shellSpinnerAnimator.SetSpeed(0f);
                yield return new WaitForSeconds(idleWait);
            }

            if (testProjectileAttackWithoutTurn)
            {
                Debug.Log("SHELL SPINNER AUTO TEST: ProjectileAttack WITHOUT turn/walk");
                shellSpinnerAnimator.PlayProjectileAttack(false, false);
                yield return new WaitForSeconds(projectileAttackWithoutTurnWait);
            }

            if (testProjectileAttackWithTurn)
            {
                Debug.Log("SHELL SPINNER AUTO TEST: ProjectileAttack WITH turn/walk");
                shellSpinnerAnimator.PlayProjectileAttack(true, false);
                yield return new WaitForSeconds(projectileAttackWithTurnWait);
            }

            if (testProjectileAttackWithTurnAndRepeatShot)
            {
                Debug.Log("SHELL SPINNER AUTO TEST: ProjectileAttack WITH turn/walk AND repeat shot");
                shellSpinnerAnimator.PlayProjectileAttack(true, true);
                yield return new WaitForSeconds(projectileAttackRepeatWait);

                shellSpinnerAnimator.SetContinueShooting(false);
                yield return new WaitForSeconds(1.0f);
            }

            if (testSpinAttackNormal)
            {
                Debug.Log("SHELL SPINNER AUTO TEST: SpinAttack normal");
                shellSpinnerAnimator.PlaySpinAttack(false);
                yield return new WaitForSeconds(spinAttackNormalWait);
            }

            if (testSpinAttackLong)
            {
                Debug.Log("SHELL SPINNER AUTO TEST: SpinAttack LONG / ContinueSpinning true");
                shellSpinnerAnimator.PlaySpinAttack(true);

                yield return new WaitForSeconds(longSpinHoldTime);

                Debug.Log("SHELL SPINNER AUTO TEST: Stop ContinueSpinning");
                shellSpinnerAnimator.StopSpinning();

                yield return new WaitForSeconds(spinAttackFinishWait);
            }

            if (testIdleBreak)
            {
                Debug.Log("SHELL SPINNER AUTO TEST: IdleBreak");
                shellSpinnerAnimator.PlayIdleBreak();
                yield return new WaitForSeconds(idleBreakWait);
            }

            if (testDeath)
            {
                Debug.Log("SHELL SPINNER AUTO TEST: Death");
                shellSpinnerAnimator.SetDead(true);
                yield return new WaitForSeconds(deathWait);
            }

            Debug.Log("SHELL SPINNER AUTO TEST FINISHED");

            if (loopTest)
            {
                Debug.Log("SHELL SPINNER AUTO TEST LOOP: Reset and restart.");
                shellSpinnerAnimator.ResetToIdle();
                yield return new WaitForSeconds(delayBetweenLoops);
            }

        } while (loopTest);

        testRoutine = null;
    }
}