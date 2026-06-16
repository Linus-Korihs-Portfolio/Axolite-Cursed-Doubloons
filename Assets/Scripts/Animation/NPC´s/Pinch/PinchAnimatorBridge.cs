using UnityEngine;

public class PinchAnimatorBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Animator State Names")]
    [SerializeField] private string idleStateName = "Pinch_Idle";
    [SerializeField] private string dialogStateName = "Pinch_Dialog";
    [SerializeField] private string idleBreakStateName = "Pinch_IdleBreak";

    [Header("Animator Trigger Names")]
    [SerializeField] private string dialogTriggerName = "Dialog";
    [SerializeField] private string idleBreakTriggerName = "IdleBreak";

    [Header("Settings")]
    [SerializeField] private int layerIndex = 0;
    [SerializeField] private float transitionDuration = 0.1f;
    [SerializeField] private bool useAnimatorParameters = true;

    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;
    private Vector3 startLocalScale;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        CacheStartTransform();
    }

    private void CacheStartTransform()
    {
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;
        startLocalScale = transform.localScale;
    }

    public void PlayIdle()
    {
        if (animator == null) return;

        ResetAllTriggers();
        PlayStateDirectly(idleStateName);
    }

    public void PlayDialog()
    {
        PlayByTriggerOrDirectState(dialogTriggerName, dialogStateName);
    }

    public void PlayIdleBreak()
    {
        PlayByTriggerOrDirectState(idleBreakTriggerName, idleBreakStateName);
    }

    public void ResetToIdle()
    {
        if (animator == null) return;

        ResetAllTriggers();

        animator.Rebind();
        animator.Update(0f);

        animator.Play(idleStateName, layerIndex, 0f);
        animator.Update(0f);
    }

    public void ResetToStartAndIdle()
    {
        transform.localPosition = startLocalPosition;
        transform.localRotation = startLocalRotation;
        transform.localScale = startLocalScale;

        ResetToIdle();
    }

    private void PlayByTriggerOrDirectState(string triggerName, string stateName)
    {
        if (animator == null) return;

        ResetAllTriggers();

        if (useAnimatorParameters && HasParameter(triggerName, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(triggerName);
        }
        else
        {
            PlayStateDirectly(stateName);
        }
    }

    private void PlayStateDirectly(string stateName)
    {
        if (animator == null) return;
        if (string.IsNullOrWhiteSpace(stateName)) return;

        animator.CrossFadeInFixedTime(stateName, transitionDuration, layerIndex, 0f);
    }

    private void ResetAllTriggers()
    {
        if (animator == null) return;

        if (HasParameter(dialogTriggerName, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(dialogTriggerName);

        if (HasParameter(idleBreakTriggerName, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(idleBreakTriggerName);
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null) return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
                return true;
        }

        return false;
    }

    // Kann von Animation Events benutzt werden
    public void AnimationEvent_ReturnToIdle()
    {
        PlayIdle();
    }
}