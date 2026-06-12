using System.Collections;
using UnityEngine;

public class EnemyAnimatorAutoTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyAnimatorBridge enemyAnimator;

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
        if (enemyAnimator == null)
        {
            enemyAnimator = GetComponent<EnemyAnimatorBridge>();
        }

        if (enemyAnimator == null)
        {
            Debug.LogError("EnemyAnimatorAutoTester: Keine EnemyAnimatorBridge gefunden.");
        }
    }

    private IEnumerator Start()
    {
        if (autoRunOnStart && enemyAnimator != null)
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
        enemyAnimator.SetSpeed(0f);
        enemyAnimator.SetDead(false);
        yield return new WaitForSeconds(idleWait);

        // Walk
        Debug.Log("AUTO TEST: Walk");
        enemyAnimator.SetSpeed(walkSpeed);
        yield return new WaitForSeconds(walkDuration);

        // Back to Idle
        Debug.Log("AUTO TEST: Back to Idle");
        enemyAnimator.SetSpeed(0f);
        yield return new WaitForSeconds(idleWait);

        // Main Attack
        Debug.Log("AUTO TEST: MainAttack");
        enemyAnimator.PlayMainAttack();
        yield return new WaitForSeconds(attackWait);

        // Lunge Attack
        Debug.Log("AUTO TEST: LungeAttack");
        enemyAnimator.PlayLungeAttack();
        yield return new WaitForSeconds(lungeWait);

        // Idle Break optional
        Debug.Log("AUTO TEST: IdleBreak");
        enemyAnimator.PlayIdleBreak();
        yield return new WaitForSeconds(idleBreakWait);

        // Death
        Debug.Log("AUTO TEST: Death");
        enemyAnimator.SetDead(true);
        yield return new WaitForSeconds(deathWait);

        Debug.Log("AUTO TEST FINISHED");
    }

    private void ResetAnimatorForNextLoop()
    {
        enemyAnimator.ResetToIdle();
    }
}