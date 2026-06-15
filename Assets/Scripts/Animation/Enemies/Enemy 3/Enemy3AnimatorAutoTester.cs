using System.Collections;
using UnityEngine;

public class Enemy3AnimatorAutoTester : MonoBehaviour
{
    private enum TestScenario
    {
        WakeUpToWalk,
        SecondAttackToHover,
        GrabAttackFromHover,
        LowHpEscapeFromWalk,
        LowHpEscapeFromHover,
        FullCycleNoLowHp,
        DeathFlying,
        DeathDigging
    }

    [Header("References")]
    [SerializeField] private Enemy3AnimatorBridge enemyAnimator;

    [Header("Transform Reset")]
    [Tooltip("Das Objekt, das auf den Ursprungspunkt zurückgesetzt werden soll. Leer lassen = dieses GameObject.")]
    [SerializeField] private Transform objectToReset;

    [Tooltip("Speichert beim Start die aktuelle Position/Rotation/Scale als Ursprungspunkt.")]
    [SerializeField] private bool captureOriginOnStart = true;

    [Tooltip("Setzt die Position vor jedem automatischen Test zurück.")]
    [SerializeField] private bool resetTransformBeforeAutoTest = true;

    private Vector3 originPosition;
    private Quaternion originRotation;
    private Vector3 originScale;
    private bool originCaptured;

    [Header("Auto Test Settings")]
    [SerializeField] private bool autoRunOnStart = true;
    [SerializeField] private bool loopTest = false;
    [SerializeField] private TestScenario scenario = TestScenario.FullCycleNoLowHp;

    [Header("Timing")]
    [SerializeField] private float hiddenWait = 1.0f;
    [SerializeField] private float wakeUpSequenceWait = 3.0f;
    [SerializeField] private float secondAttackWait = 2.0f;
    [SerializeField] private float grabAttackWait = 2.0f;
    [SerializeField] private float lowHpEscapeWait = 3.0f;
    [SerializeField] private float deathWait = 2.0f;
    [SerializeField] private float delayBetweenLoops = 1.0f;

    [Header("Optional Speed Test")]
    [SerializeField] private bool setSpeedDuringWalk = false;
    [SerializeField] private float walkSpeed = 1.0f;
    [SerializeField] private float walkSpeedWait = 0.5f;

    private Coroutine testRoutine;

    private void Awake()
    {
        if (enemyAnimator == null)
        {
            enemyAnimator = GetComponent<Enemy3AnimatorBridge>();
        }

        if (objectToReset == null)
        {
            objectToReset = transform;
        }

        if (enemyAnimator == null)
        {
            Debug.LogError("Enemy3AnimatorAutoTester: Keine Enemy3AnimatorBridge gefunden. Bitte beide Scripts auf dasselbe GameObject legen.");
        }

        if (captureOriginOnStart)
        {
            CaptureCurrentTransformAsOrigin();
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

    [ContextMenu("TEST / Capture Current Transform As Origin")]
    public void CaptureCurrentTransformAsOrigin()
    {
        if (objectToReset == null)
        {
            objectToReset = transform;
        }

        originPosition = objectToReset.position;
        originRotation = objectToReset.rotation;
        originScale = objectToReset.localScale;
        originCaptured = true;

        Debug.Log("Enemy3 Tester: Current transform captured as origin.");
    }

    [ContextMenu("TEST / Reset Transform To Origin")]
    public void ResetTransformToOrigin()
    {
        if (!originCaptured)
        {
            CaptureCurrentTransformAsOrigin();
        }

        objectToReset.position = originPosition;
        objectToReset.rotation = originRotation;
        objectToReset.localScale = originScale;

        Debug.Log("Enemy3 Tester: Transform reset to origin.");
    }

    [ContextMenu("TEST / Reset Animation To Hidden At Origin")]
    public void ResetAnimationToHiddenAtOrigin()
    {
        StopAutoTest();
        ResetTransformToOrigin();

        if (enemyAnimator != null)
        {
            enemyAnimator.ResetToHidden();
        }

        Debug.Log("Enemy3 Tester: Reset to Hidden at origin.");
    }

    [ContextMenu("TEST / Reset Animation To Walk At Origin")]
    public void ResetAnimationToWalkAtOrigin()
    {
        StopAutoTest();
        ResetTransformToOrigin();

        if (enemyAnimator != null)
        {
            enemyAnimator.ResetToWalkBase();
        }

        Debug.Log("Enemy3 Tester: Reset to Walk/Base at origin.");
    }

    [ContextMenu("TEST / Reset Animation To Hover At Origin")]
    public void ResetAnimationToHoverAtOrigin()
    {
        StopAutoTest();
        ResetTransformToOrigin();

        if (enemyAnimator != null)
        {
            enemyAnimator.ResetToHover();
        }

        Debug.Log("Enemy3 Tester: Reset to Hover at origin.");
    }

    private IEnumerator AutoTestRoutine()
    {
        do
        {
            Debug.Log("ENEMY 3 AUTO TEST START: " + scenario);

            if (resetTransformBeforeAutoTest)
            {
                ResetTransformToOrigin();
            }

            switch (scenario)
            {
                case TestScenario.WakeUpToWalk:
                    yield return TestWakeUpToWalk();
                    break;

                case TestScenario.SecondAttackToHover:
                    yield return TestSecondAttackToHover();
                    break;

                case TestScenario.GrabAttackFromHover:
                    yield return TestGrabAttackFromHover();
                    break;

                case TestScenario.LowHpEscapeFromWalk:
                    yield return TestLowHpEscapeFromWalk();
                    break;

                case TestScenario.LowHpEscapeFromHover:
                    yield return TestLowHpEscapeFromHover();
                    break;

                case TestScenario.FullCycleNoLowHp:
                    yield return TestFullCycleNoLowHp();
                    break;

                case TestScenario.DeathFlying:
                    yield return TestDeathFlying();
                    break;

                case TestScenario.DeathDigging:
                    yield return TestDeathDigging();
                    break;
            }

            Debug.Log("ENEMY 3 AUTO TEST FINISHED: " + scenario);

            if (loopTest)
            {
                yield return new WaitForSeconds(delayBetweenLoops);
            }

        } while (loopTest);

        testRoutine = null;
    }

    private IEnumerator TestWakeUpToWalk()
    {
        enemyAnimator.ResetToHidden();
        yield return new WaitForSeconds(hiddenWait);

        enemyAnimator.WakeUp();
        yield return new WaitForSeconds(wakeUpSequenceWait);

        yield return OptionalWalkSpeedTest();
    }

    private IEnumerator TestSecondAttackToHover()
    {
        enemyAnimator.ResetToWalkBase();
        yield return new WaitForSeconds(0.3f);

        yield return OptionalWalkSpeedTest();

        enemyAnimator.PlaySecondAttack();
        yield return new WaitForSeconds(secondAttackWait);
    }

    private IEnumerator TestGrabAttackFromHover()
    {
        enemyAnimator.ResetToHover();
        yield return new WaitForSeconds(0.3f);

        enemyAnimator.PlayGrabAttack();
        yield return new WaitForSeconds(grabAttackWait);
    }

    private IEnumerator TestLowHpEscapeFromWalk()
    {
        enemyAnimator.ResetToWalkBase();
        yield return new WaitForSeconds(0.5f);

        enemyAnimator.PlayFlyDown();
        yield return new WaitForSeconds(lowHpEscapeWait);
    }

    private IEnumerator TestLowHpEscapeFromHover()
    {
        enemyAnimator.ResetToHover();
        yield return new WaitForSeconds(0.5f);

        enemyAnimator.PlayFlyDown();
        yield return new WaitForSeconds(lowHpEscapeWait);
    }

    private IEnumerator TestFullCycleNoLowHp()
    {
        enemyAnimator.ResetToHidden();
        yield return new WaitForSeconds(hiddenWait);

        enemyAnimator.WakeUp();
        yield return new WaitForSeconds(wakeUpSequenceWait);

        yield return OptionalWalkSpeedTest();

        enemyAnimator.PlaySecondAttack();
        yield return new WaitForSeconds(secondAttackWait);

        enemyAnimator.PlayGrabAttack();
        yield return new WaitForSeconds(grabAttackWait);
    }

    private IEnumerator TestDeathFlying()
    {
        enemyAnimator.ResetToHover();
        yield return new WaitForSeconds(0.5f);

        enemyAnimator.PlayDeathFlying();
        yield return new WaitForSeconds(deathWait);
    }

    private IEnumerator TestDeathDigging()
    {
        enemyAnimator.ResetToHidden();
        yield return new WaitForSeconds(0.5f);

        enemyAnimator.PlayDeathDigging();
        yield return new WaitForSeconds(deathWait);
    }

    private IEnumerator OptionalWalkSpeedTest()
    {
        if (!setSpeedDuringWalk)
        {
            yield break;
        }

        enemyAnimator.SetSpeed(walkSpeed);
        yield return new WaitForSeconds(walkSpeedWait);
        enemyAnimator.SetSpeed(0f);
    }
}