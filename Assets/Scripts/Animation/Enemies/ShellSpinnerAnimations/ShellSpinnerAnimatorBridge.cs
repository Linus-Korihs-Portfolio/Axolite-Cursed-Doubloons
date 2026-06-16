using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ShellSpinnerAnimatorBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Animator Parameter Names")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string projectileAttackTrigger = "ProjectileAttack";
    [SerializeField] private string spinAttackTrigger = "SpinAttack";
    [SerializeField] private string idleBreakTrigger = "IdleBreak";
    [SerializeField] private string isDeadParameter = "IsDead";
    [SerializeField] private string needsTurnParameter = "NeedsTurn";
    [SerializeField] private string continueShootingParameter = "ContinueShooting";
    [SerializeField] private string continueSpinningParameter = "ContinueSpinning";

    [Header("State Names")]
    [SerializeField] private string idleStateName = "E2_Idle";

    private int speedHash;
    private int projectileAttackHash;
    private int spinAttackHash;
    private int idleBreakHash;
    private int isDeadHash;
    private int needsTurnHash;
    private int continueShootingHash;
    private int continueSpinningHash;

    private bool hasSpeed;
    private bool hasProjectileAttack;
    private bool hasSpinAttack;
    private bool hasIdleBreak;
    private bool hasIsDead;
    private bool hasNeedsTurn;
    private bool hasContinueShooting;
    private bool hasContinueSpinning;

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
        needsTurnHash = Animator.StringToHash(needsTurnParameter);
        continueShootingHash = Animator.StringToHash(continueShootingParameter);
        continueSpinningHash = Animator.StringToHash(continueSpinningParameter);
    }

    private void CheckAvailableParameters()
    {
        hasSpeed = HasParameter(speedParameter, AnimatorControllerParameterType.Float);
        hasProjectileAttack = HasParameter(projectileAttackTrigger, AnimatorControllerParameterType.Trigger);
        hasSpinAttack = HasParameter(spinAttackTrigger, AnimatorControllerParameterType.Trigger);
        hasIdleBreak = HasParameter(idleBreakTrigger, AnimatorControllerParameterType.Trigger);
        hasIsDead = HasParameter(isDeadParameter, AnimatorControllerParameterType.Bool);
        hasNeedsTurn = HasParameter(needsTurnParameter, AnimatorControllerParameterType.Bool);
        hasContinueShooting = HasParameter(continueShootingParameter, AnimatorControllerParameterType.Bool);
        hasContinueSpinning = HasParameter(continueSpinningParameter, AnimatorControllerParameterType.Bool);
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("ShellSpinnerAnimatorBridge: Kein Animator oder kein Animator Controller gefunden.");
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
            Debug.LogWarning("ShellSpinnerAnimatorBridge: Animator Parameter fehlt oder ist kein Float: " + speedParameter);
            return;
        }

        animator.SetFloat(speedHash, speed);
    }

    public void SetNeedsTurn(bool needsTurn)
    {
        if (!hasNeedsTurn)
        {
            Debug.LogWarning("ShellSpinnerAnimatorBridge: Animator Parameter fehlt oder ist kein Bool: " + needsTurnParameter);
            return;
        }

        animator.SetBool(needsTurnHash, needsTurn);
    }

    public void SetContinueShooting(bool continueShooting)
    {
        if (!hasContinueShooting)
        {
            Debug.LogWarning("ShellSpinnerAnimatorBridge: Animator Parameter fehlt oder ist kein Bool: " + continueShootingParameter);
            return;
        }

        animator.SetBool(continueShootingHash, continueShooting);
    }

    public void SetContinueSpinning(bool continueSpinning)
    {
        if (!hasContinueSpinning)
        {
            Debug.LogWarning("ShellSpinnerAnimatorBridge: Animator Parameter fehlt oder ist kein Bool: " + continueSpinningParameter);
            return;
        }

        animator.SetBool(continueSpinningHash, continueSpinning);
    }

    public void PlayProjectileAttack()
    {
        PlayProjectileAttack(false, false);
    }

    public void PlayProjectileAttack(bool needsTurn)
    {
        PlayProjectileAttack(needsTurn, false);
    }

    public void PlayProjectileAttack(bool needsTurn, bool continueShooting)
    {
        if (!hasProjectileAttack)
        {
            Debug.LogWarning("ShellSpinnerAnimatorBridge: Animator Trigger fehlt: " + projectileAttackTrigger);
            return;
        }

        if (hasNeedsTurn)
        {
            animator.SetBool(needsTurnHash, needsTurn);
        }

        if (hasContinueShooting)
        {
            animator.SetBool(continueShootingHash, continueShooting);
        }

        ResetAttackTriggers();
        animator.SetTrigger(projectileAttackHash);
    }

    public void PlaySpinAttack()
    {
        PlaySpinAttack(false);
    }

    public void PlaySpinAttack(bool continueSpinning)
    {
        if (!hasSpinAttack)
        {
            Debug.LogWarning("ShellSpinnerAnimatorBridge: Animator Trigger fehlt: " + spinAttackTrigger);
            return;
        }

        if (hasContinueSpinning)
        {
            animator.SetBool(continueSpinningHash, continueSpinning);
        }

        ResetAttackTriggers();
        animator.SetTrigger(spinAttackHash);
    }

    public void StopSpinning()
    {
        if (hasContinueSpinning)
        {
            animator.SetBool(continueSpinningHash, false);
        }
    }

    public void PlayIdleBreak()
    {
        if (!hasIdleBreak)
        {
            Debug.LogWarning("ShellSpinnerAnimatorBridge: Animator Trigger fehlt: " + idleBreakTrigger);
            return;
        }

        ResetAttackTriggers();
        animator.SetTrigger(idleBreakHash);
    }

    public void SetDead(bool isDead)
    {
        if (!hasIsDead)
        {
            Debug.LogWarning("ShellSpinnerAnimatorBridge: Animator Parameter fehlt oder ist kein Bool: " + isDeadParameter);
            return;
        }

        if (isDead)
        {
            ResetAllTriggers();

            if (hasSpeed)
            {
                animator.SetFloat(speedHash, 0f);
            }

            if (hasNeedsTurn)
            {
                animator.SetBool(needsTurnHash, false);
            }

            if (hasContinueShooting)
            {
                animator.SetBool(continueShootingHash, false);
            }

            if (hasContinueSpinning)
            {
                animator.SetBool(continueSpinningHash, false);
            }
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

        if (hasNeedsTurn)
        {
            animator.SetBool(needsTurnHash, false);
        }

        if (hasContinueShooting)
        {
            animator.SetBool(continueShootingHash, false);
        }

        if (hasContinueSpinning)
        {
            animator.SetBool(continueSpinningHash, false);
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

    private void ResetAttackTriggers()
    {
        if (hasProjectileAttack)
        {
            animator.ResetTrigger(projectileAttackHash);
        }

        if (hasSpinAttack)
        {
            animator.ResetTrigger(spinAttackHash);
        }
    }
}