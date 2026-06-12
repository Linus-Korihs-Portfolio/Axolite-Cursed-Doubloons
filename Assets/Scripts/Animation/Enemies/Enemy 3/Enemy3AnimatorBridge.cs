using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Enemy3AnimatorBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Animator Parameter Names")]
    [SerializeField] private string wakeUpTrigger = "WakeUp";
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string secondAttackTrigger = "SecondAttack";
    [SerializeField] private string grabAttackTrigger = "GrabAttack";
    [SerializeField] private string flyDownTrigger = "FlyDown";
    [SerializeField] private string digDownTrigger = "DigDown";
    [SerializeField] private string deathFlyingTrigger = "DeathFlying";
    [SerializeField] private string deathDiggingTrigger = "DeathDigging";

    [Header("State Names")]
    [SerializeField] private string hiddenStateName = "E3_HiddenArmed";
    [SerializeField] private string walkBaseStateName = "E3_Walk";
    [SerializeField] private string hoverStateName = "E3_Hover";

    private int wakeUpHash;
    private int speedHash;
    private int secondAttackHash;
    private int grabAttackHash;
    private int flyDownHash;
    private int digDownHash;
    private int deathFlyingHash;
    private int deathDiggingHash;

    private bool hasWakeUp;
    private bool hasSpeed;
    private bool hasSecondAttack;
    private bool hasGrabAttack;
    private bool hasFlyDown;
    private bool hasDigDown;
    private bool hasDeathFlying;
    private bool hasDeathDigging;

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
        wakeUpHash = Animator.StringToHash(wakeUpTrigger);
        speedHash = Animator.StringToHash(speedParameter);
        secondAttackHash = Animator.StringToHash(secondAttackTrigger);
        grabAttackHash = Animator.StringToHash(grabAttackTrigger);
        flyDownHash = Animator.StringToHash(flyDownTrigger);
        digDownHash = Animator.StringToHash(digDownTrigger);
        deathFlyingHash = Animator.StringToHash(deathFlyingTrigger);
        deathDiggingHash = Animator.StringToHash(deathDiggingTrigger);
    }

    private void CheckAvailableParameters()
    {
        hasWakeUp = HasParameter(wakeUpTrigger, AnimatorControllerParameterType.Trigger);
        hasSpeed = HasParameter(speedParameter, AnimatorControllerParameterType.Float);
        hasSecondAttack = HasParameter(secondAttackTrigger, AnimatorControllerParameterType.Trigger);
        hasGrabAttack = HasParameter(grabAttackTrigger, AnimatorControllerParameterType.Trigger);
        hasFlyDown = HasParameter(flyDownTrigger, AnimatorControllerParameterType.Trigger);
        hasDigDown = HasParameter(digDownTrigger, AnimatorControllerParameterType.Trigger);
        hasDeathFlying = HasParameter(deathFlyingTrigger, AnimatorControllerParameterType.Trigger);
        hasDeathDigging = HasParameter(deathDiggingTrigger, AnimatorControllerParameterType.Trigger);
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("Enemy3AnimatorBridge: Kein Animator oder kein Animator Controller gefunden.");
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

    public void WakeUp()
    {
        if (!hasWakeUp)
        {
            Debug.LogWarning("Enemy3AnimatorBridge: Animator Trigger fehlt: " + wakeUpTrigger);
            return;
        }

        animator.SetTrigger(wakeUpHash);
    }

    public void SetSpeed(float speed)
    {
        if (!hasSpeed)
        {
            Debug.LogWarning("Enemy3AnimatorBridge: Animator Parameter fehlt oder ist kein Float: " + speedParameter);
            return;
        }

        animator.SetFloat(speedHash, speed);
    }

    public void PlaySecondAttack()
    {
        if (!hasSecondAttack)
        {
            Debug.LogWarning("Enemy3AnimatorBridge: Animator Trigger fehlt: " + secondAttackTrigger);
            return;
        }

        animator.SetTrigger(secondAttackHash);
    }

    public void PlayGrabAttack()
    {
        if (!hasGrabAttack)
        {
            Debug.LogWarning("Enemy3AnimatorBridge: Animator Trigger fehlt: " + grabAttackTrigger);
            return;
        }

        animator.SetTrigger(grabAttackHash);
    }

    public void PlayFlyDown()
    {
        if (!hasFlyDown)
        {
            Debug.LogWarning("Enemy3AnimatorBridge: Animator Trigger fehlt: " + flyDownTrigger);
            return;
        }

        animator.SetTrigger(flyDownHash);
    }

    public void PlayDigDown()
    {
        if (!hasDigDown)
        {
            Debug.LogWarning("Enemy3AnimatorBridge: Animator Trigger fehlt: " + digDownTrigger);
            return;
        }

        animator.SetTrigger(digDownHash);
    }

    public void PlayDeathFlying()
    {
        if (!hasDeathFlying)
        {
            Debug.LogWarning("Enemy3AnimatorBridge: Animator Trigger fehlt: " + deathFlyingTrigger);
            return;
        }

        animator.SetTrigger(deathFlyingHash);
    }

    public void PlayDeathDigging()
    {
        if (!hasDeathDigging)
        {
            Debug.LogWarning("Enemy3AnimatorBridge: Animator Trigger fehlt: " + deathDiggingTrigger);
            return;
        }

        animator.SetTrigger(deathDiggingHash);
    }

    public void ResetToHidden()
    {
        if (hasSpeed)
        {
            animator.SetFloat(speedHash, 0f);
        }

        ResetAllTriggers();

        if (!string.IsNullOrEmpty(hiddenStateName))
        {
            animator.Play(hiddenStateName, 0, 0f);
        }
    }

    public void ResetToWalkBase()
    {
        if (hasSpeed)
        {
            animator.SetFloat(speedHash, 0f);
        }

        ResetAllTriggers();

        if (!string.IsNullOrEmpty(walkBaseStateName))
        {
            animator.Play(walkBaseStateName, 0, 0f);
        }
    }

    public void ResetToHover()
    {
        if (hasSpeed)
        {
            animator.SetFloat(speedHash, 0f);
        }

        ResetAllTriggers();

        if (!string.IsNullOrEmpty(hoverStateName))
        {
            animator.Play(hoverStateName, 0, 0f);
        }
    }

    public void ResetAllTriggers()
    {
        if (hasWakeUp)
        {
            animator.ResetTrigger(wakeUpHash);
        }

        if (hasSecondAttack)
        {
            animator.ResetTrigger(secondAttackHash);
        }

        if (hasGrabAttack)
        {
            animator.ResetTrigger(grabAttackHash);
        }

        if (hasFlyDown)
        {
            animator.ResetTrigger(flyDownHash);
        }

        if (hasDigDown)
        {
            animator.ResetTrigger(digDownHash);
        }

        if (hasDeathFlying)
        {
            animator.ResetTrigger(deathFlyingHash);
        }

        if (hasDeathDigging)
        {
            animator.ResetTrigger(deathDiggingHash);
        }
    }
}