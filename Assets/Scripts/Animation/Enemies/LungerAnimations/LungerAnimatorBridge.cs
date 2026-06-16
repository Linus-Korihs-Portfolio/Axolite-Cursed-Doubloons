using UnityEngine;

[RequireComponent(typeof(Animator))]
public class LungerAnimatorBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Animator Parameter Names")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string mainAttackTrigger = "MainAttack";
    [SerializeField] private string lungeAttackTrigger = "LungeAttack";
    [SerializeField] private string idleBreakTrigger = "IdleBreak";
    [SerializeField] private string isDeadParameter = "IsDead";

    private int speedHash;
    private int mainAttackHash;
    private int lungeAttackHash;
    private int idleBreakHash;
    private int isDeadHash;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        CacheAnimatorHashes();
    }

    private void OnValidate()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void CacheAnimatorHashes()
    {
        speedHash = Animator.StringToHash(speedParameter);
        mainAttackHash = Animator.StringToHash(mainAttackTrigger);
        lungeAttackHash = Animator.StringToHash(lungeAttackTrigger);
        idleBreakHash = Animator.StringToHash(idleBreakTrigger);
        isDeadHash = Animator.StringToHash(isDeadParameter);
    }

    public void SetSpeed(float speed)
    {
        animator.SetFloat(speedHash, speed);
    }

    public void PlayMainAttack()
    {
        animator.SetTrigger(mainAttackHash);
    }

    public void PlayLungeAttack()
    {
        animator.SetTrigger(lungeAttackHash);
    }

    public void PlayIdleBreak()
    {
        animator.SetTrigger(idleBreakHash);
    }

    public void SetDead(bool isDead)
    {
        animator.SetBool(isDeadHash, isDead);
    }

    public void ResetToIdle()
    {
        animator.SetFloat(speedHash, 0f);
        animator.SetBool(isDeadHash, false);
    }
}