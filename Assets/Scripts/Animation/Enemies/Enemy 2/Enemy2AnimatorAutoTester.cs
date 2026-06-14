using System.Collections;
using UnityEngine;

public class Enemy2AnimatorAutoTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Enemy2AnimatorBridge enemyAnimator;

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
        if (enemyAnimator == null)
        {
            enemyAnimator = GetComponent<Enemy2AnimatorBridge>();
        }

        if (enemyAnimator == null)
        {
            Debug.LogError("Enemy2AnimatorAutoTester: Keine Enemy2AnimatorBridge gefunden. Bitte beide Scripts auf dasselbe GameObject legen.");
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
            Debug.Log("ENEMY 2 AUTO TEST START");

            enemyAnimator.ResetToIdle();
            yield return new WaitForSeconds(0.2f);

            if (testIdle)
            {
                Debug.Log("ENEMY 2 AUTO TEST: Idle");
                enemyAnimator.SetSpeed(0f);
                yield return new WaitForSeconds(idleWait);
            }

            if (testWalk)
            {
                Debug.Log("ENEMY 2 AUTO TEST: Walk");
                enemyAnimator.SetSpeed(walkSpeed);
                yield return new WaitForSeconds(walkDuration);

                Debug.Log("ENEMY 2 AUTO TEST: Back to Idle");
                enemyAnimator.SetSpeed(0f);
                yield return new WaitForSeconds(idleWait);
            }

            if (testProjectileAttackWithoutTurn)
            {
                Debug.Log("ENEMY 2 AUTO TEST: ProjectileAttack WITHOUT turn/walk");
                enemyAnimator.PlayProjectileAttack(false, false);
                yield return new WaitForSeconds(projectileAttackWithoutTurnWait);
            }

            if (testProjectileAttackWithTurn)
            {
                Debug.Log("ENEMY 2 AUTO TEST: ProjectileAttack WITH turn/walk");
                enemyAnimator.PlayProjectileAttack(true, false);
                yield return new WaitForSeconds(projectileAttackWithTurnWait);
            }

            if (testProjectileAttackWithTurnAndRepeatShot)
            {
                Debug.Log("ENEMY 2 AUTO TEST: ProjectileAttack WITH turn/walk AND repeat shot");
                enemyAnimator.PlayProjectileAttack(true, true);
                yield return new WaitForSeconds(projectileAttackRepeatWait);

                enemyAnimator.SetContinueShooting(false);
                yield return new WaitForSeconds(1.0f);
            }

            if (testSpinAttackNormal)
            {
                Debug.Log("ENEMY 2 AUTO TEST: SpinAttack normal");
                enemyAnimator.PlaySpinAttack(false);
                yield return new WaitForSeconds(spinAttackNormalWait);
            }

            if (testSpinAttackLong)
            {
                Debug.Log("ENEMY 2 AUTO TEST: SpinAttack LONG / ContinueSpinning true");
                enemyAnimator.PlaySpinAttack(true);

                yield return new WaitForSeconds(longSpinHoldTime);

                Debug.Log("ENEMY 2 AUTO TEST: Stop ContinueSpinning");
                enemyAnimator.StopSpinning();

                yield return new WaitForSeconds(spinAttackFinishWait);
            }

            if (testIdleBreak)
            {
                Debug.Log("ENEMY 2 AUTO TEST: IdleBreak");
                enemyAnimator.PlayIdleBreak();
                yield return new WaitForSeconds(idleBreakWait);
            }

            if (testDeath)
            {
                Debug.Log("ENEMY 2 AUTO TEST: Death");
                enemyAnimator.SetDead(true);
                yield return new WaitForSeconds(deathWait);
            }

            Debug.Log("ENEMY 2 AUTO TEST FINISHED");

            if (loopTest)
            {
                Debug.Log("ENEMY 2 AUTO TEST LOOP: Reset and restart.");
                enemyAnimator.ResetToIdle();
                yield return new WaitForSeconds(delayBetweenLoops);
            }

        } while (loopTest);

        testRoutine = null;
    }
}