using UnityEngine;

public class Enemy2AnimationEvents : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

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
    }

    public void OnProjectileAttackHit()
    {
        LogEvent("Projectile Attack hit frame reached");
    }

    public void OnProjectilAttackHit()
    {
        LogEvent("Projectile Attack hit frame reached. Note: Event name uses typo Projectil.");
    }

    public void OnProjectileAttackEnd()
    {
        LogEvent("Projectile Attack ended");
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
    }

    public void OnSpinAttackLoop()
    {
        LogEvent("Spin Attack loop / ContinueSpinning part");
    }

    public void OnSpinAttackHit()
    {
        LogEvent("Spin Attack hit frame reached");
    }

    public void OnSpinAttackEnd()
    {
        LogEvent("Spin Attack ended");
    }

    private void LogEvent(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log("[Enemy 2 Animation Event] " + gameObject.name + ": " + message);
    }
}