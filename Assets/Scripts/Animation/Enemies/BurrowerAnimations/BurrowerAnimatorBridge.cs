using UnityEngine;

[RequireComponent(typeof(Animator))]
public class BurrowerAnimatorBridge : MonoBehaviour
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

    [Header("Reset Settings")]
    [SerializeField] private bool updateAnimatorAfterReset = true;

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
            Debug.LogWarning("BurrowerAnimatorBridge: Kein Animator oder kein Animator Controller gefunden.");
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
        FireTrigger(wakeUpHash, hasWakeUp, wakeUpTrigger);
    }

    public void SetSpeed(float speed)
    {
        if (!hasSpeed)
        {
            Debug.LogWarning("BurrowerAnimatorBridge: Animator Parameter fehlt oder ist kein Float: " + speedParameter);
            return;
        }

        animator.SetFloat(speedHash, speed);
    }

    public void PlaySecondAttack()
    {
        FireTrigger(secondAttackHash, hasSecondAttack, secondAttackTrigger);
    }

    public void PlayGrabAttack()
    {
        FireTrigger(grabAttackHash, hasGrabAttack, grabAttackTrigger);
    }

    public void PlayFlyDown()
    {
        FireTrigger(flyDownHash, hasFlyDown, flyDownTrigger);
    }

    public void PlayDigDown()
    {
        FireTrigger(digDownHash, hasDigDown, digDownTrigger);
    }

    public void PlayDeathFlying()
    {
        ResetAllTriggers();
        FireTrigger(deathFlyingHash, hasDeathFlying, deathFlyingTrigger);
    }

    public void PlayDeathDigging()
    {
        ResetAllTriggers();
        FireTrigger(deathDiggingHash, hasDeathDigging, deathDiggingTrigger);
    }

    public void ResetToHidden()
    {
        ResetAllTriggers();

        if (hasSpeed)
        {
            animator.SetFloat(speedHash, 0f);
        }

        PlayState(hiddenStateName);
    }

    public void ResetToWalkBase()
    {
        ResetAllTriggers();

        if (hasSpeed)
        {
            animator.SetFloat(speedHash, 0f);
        }

        PlayState(walkBaseStateName);
    }

    public void ResetToHover()
    {
        ResetAllTriggers();

        if (hasSpeed)
        {
            animator.SetFloat(speedHash, 0f);
        }

        PlayState(hoverStateName);
    }

    public void ResetAllTriggers()
    {
        if (hasWakeUp) animator.ResetTrigger(wakeUpHash);
        if (hasSecondAttack) animator.ResetTrigger(secondAttackHash);
        if (hasGrabAttack) animator.ResetTrigger(grabAttackHash);
        if (hasFlyDown) animator.ResetTrigger(flyDownHash);
        if (hasDigDown) animator.ResetTrigger(digDownHash);
        if (hasDeathFlying) animator.ResetTrigger(deathFlyingHash);
        if (hasDeathDigging) animator.ResetTrigger(deathDiggingHash);
    }

    private void FireTrigger(int triggerHash, bool hasTrigger, string triggerName)
    {
        if (!hasTrigger)
        {
            Debug.LogWarning("BurrowerAnimatorBridge: Animator Trigger fehlt: " + triggerName);
            return;
        }

        animator.ResetTrigger(triggerHash);
        animator.SetTrigger(triggerHash);
    }

    private void PlayState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName))
        {
            Debug.LogWarning("BurrowerAnimatorBridge: State Name ist leer.");
            return;
        }

        animator.Play(stateName, 0, 0f);

        if (updateAnimatorAfterReset)
        {
            animator.Update(0f);
        }
    }
}