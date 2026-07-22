using UnityEngine;

public class ShellSpinnerAnimationEvents : MonoBehaviour
{
    [SerializeField] private ShellSpinnerEnemy shellSpinnerEnemy;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        ResolveEnemy();
    }

    public void OnFootstep()
    {
        LogEvent("Footstep");
    }

    public void OnIdleBreakFinished()
    {
        LogEvent("IdleBreak animation finished");
    }

    public void OnDeathFinished()
    {
        LogEvent("Death animation finished");

        if (ResolveEnemy())
            shellSpinnerEnemy.OnDeathAnimationFinished();
    }

    public void OnDeathHit()
    {
        LogEvent("Death hit frame reached");
    }

    public void OnProjectileAttackStart()
    {
        LogEvent("Projectile Attack started");
    }

    public void OnProjectileAttackCharge()
    {
        LogEvent("Projectile Attack charge frame reached");
    }

    public void OnProjectileAttackShoot()
    {
        LogEvent("Projectile Attack shoot frame reached");

        if (ResolveEnemy())
            shellSpinnerEnemy.OnProjectileAttackShootFrame();
    }

    public void OnProjectileAttackHit()
    {
        LogEvent("Projectile Attack hit frame reached");

        if (ResolveEnemy())
            shellSpinnerEnemy.OnProjectileAttackHitFrame();
    }

    public void OnProjectilAttackHit()
    {
        LogEvent("Projectile Attack hit frame reached. Note: Event name uses typo Projectil.");

        if (ResolveEnemy())
            shellSpinnerEnemy.OnProjectileAttackHitFrame();
    }

    public void OnProjectileAttackEnd()
    {
        LogEvent("Projectile Attack ended");

        if (ResolveEnemy())
            shellSpinnerEnemy.OnProjectileAttackEndFrame();
    }

    public void OnProjectileTurnWalkStart()
    {
        LogEvent("Projectile optional turn/walk started");
    }

    public void OnProjectileTurnWalkEnd()
    {
        LogEvent("Projectile optional turn/walk ended");
    }

    public void OnSpinAttackStart()
    {
        LogEvent("Spin Attack started");

        if (ResolveEnemy())
            shellSpinnerEnemy.OnSpinAttackStartFrame();
    }

    public void OnSpinAttackLoop()
    {
        LogEvent("Spin Attack loop / ContinueSpinning part");
    }

    public void OnSpinAttackHit()
    {
        LogEvent("Spin Attack hit frame reached");

        if (ResolveEnemy())
            shellSpinnerEnemy.OnSpinAttackHitFrame();
    }

    public void OnSpinAttackEnd()
    {
        LogEvent("Spin Attack ended");

        if (ResolveEnemy())
            shellSpinnerEnemy.OnSpinAttackEndFrame();
    }

    private bool ResolveEnemy()
    {
        if (shellSpinnerEnemy == null)
            shellSpinnerEnemy = GetComponentInParent<ShellSpinnerEnemy>();

        if (shellSpinnerEnemy == null)
        {
            Debug.LogError("[ShellSpinnerAnimationEvents:" + name + "] No ShellSpinnerEnemy found in parents.", this);
            return false;
        }

        return true;
    }

    private void LogEvent(string message)
    {
        if (!showDebugLogs) return;

        Debug.Log("[ShellSpinnerAnimationEvents:" + name + "] " + message, this);
    }
}
