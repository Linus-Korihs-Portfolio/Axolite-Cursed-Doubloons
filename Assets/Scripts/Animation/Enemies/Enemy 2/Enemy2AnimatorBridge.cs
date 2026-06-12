using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Enemy2AnimatorBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Animator Parameter Names")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string projectileAttackTrigger = "ProjectileAttack";
    [SerializeField] private string spinAttackTrigger = "SpinAttack";
    [SerializeField] private string idleBreakTrigger = "IdleBreak";
    [SerializeField] private string isDeadParameter = "IsDead";

    [Header("State Names")]
    [Tooltip("Name des Idle-States im Animator Controller.")]
    [SerializeField] private string idleStateName = "E2_Idle";

    private int speedHash;
    private int projectileAttackHash;
    private int spinAttackHash;
    private int idleBreakHash;
    private int isDeadHash;

    private bool hasSpeed;
    private bool hasProjectileAttack;
    private bool hasSpinAttack;
    private bool hasIdleBreak;
    private bool hasIsDead;

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
        speedHash = Animator.StringToHash(speedParameter);
        projectileAttackHash = Animator.StringToHash(projectileAttackTrigger);
        spinAttackHash = Animator.StringToHash(spinAttackTrigger);
        idleBreakHash = Animator.StringToHash(idleBreakTrigger);
        isDeadHash = Animator.StringToHash(isDeadParameter);
    }

    private void CheckAvailableParameters()
    {
        hasSpeed = HasParameter(speedParameter, AnimatorControllerParameterType.Float);
        hasProjectileAttack = HasParameter(projectileAttackTrigger, AnimatorControllerParameterType.Trigger);
        hasSpinAttack = HasParameter(spinAttackTrigger, AnimatorControllerParameterType.Trigger);
        hasIdleBreak = HasParameter(idleBreakTrigger, AnimatorControllerParameterType.Trigger);
        hasIsDead = HasParameter(isDeadParameter, AnimatorControllerParameterType.Bool);
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("Enemy2AnimatorBridge: Kein Animator oder kein Animator Controller gefunden.");
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

    public void SetSpeed(float speed)
    {
        if (!hasSpeed)
        {
            Debug.LogWarning("Enemy2AnimatorBridge: Animator Parameter fehlt oder ist kein Float: " + speedParameter);
            return;
        }

        animator.SetFloat(speedHash, speed);
    }

    public void PlayProjectileAttack()
    {
        if (!hasProjectileAttack)
        {
            Debug.LogWarning("Enemy2AnimatorBridge: Animator Trigger fehlt: " + projectileAttackTrigger);
            return;
        }

        animator.SetTrigger(projectileAttackHash);
    }

    public void PlaySpinAttack()
    {
        if (!hasSpinAttack)
        {
            Debug.LogWarning("Enemy2AnimatorBridge: Animator Trigger fehlt: " + spinAttackTrigger);
            return;
        }

        animator.SetTrigger(spinAttackHash);
    }

    public void PlayIdleBreak()
    {
        if (!hasIdleBreak)
        {
            Debug.LogWarning("Enemy2AnimatorBridge: Animator Trigger fehlt: " + idleBreakTrigger);
            return;
        }

        animator.SetTrigger(idleBreakHash);
    }

    public void SetDead(bool isDead)
    {
        if (!hasIsDead)
        {
            Debug.LogWarning("Enemy2AnimatorBridge: Animator Parameter fehlt oder ist kein Bool: " + isDeadParameter);
            return;
        }

        animator.SetBool(isDeadHash, isDead);
    }

    public void ResetToIdle()
    {
        if (hasSpeed)
        {
            animator.SetFloat(speedHash, 0f);
        }

        if (hasIsDead)
        {
            animator.SetBool(isDeadHash, false);
        }

        ResetAllTriggers();

        if (!string.IsNullOrEmpty(idleStateName))
        {
            animator.Play(idleStateName, 0, 0f);
        }
    }

    public void ResetAllTriggers()
    {
        if (hasProjectileAttack)
        {
            animator.ResetTrigger(projectileAttackHash);
        }

        if (hasSpinAttack)
        {
            animator.ResetTrigger(spinAttackHash);
        }

        if (hasIdleBreak)
        {
            animator.ResetTrigger(idleBreakHash);
        }
    }
}