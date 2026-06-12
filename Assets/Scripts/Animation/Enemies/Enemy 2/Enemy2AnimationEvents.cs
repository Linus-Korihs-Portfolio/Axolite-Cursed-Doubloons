using UnityEngine;

public class Enemy2AnimationEvents : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // -------------------------
    // Generic Enemy 2 Events
    // -------------------------

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

    // -------------------------
    // Projectile Attack Events
    // -------------------------

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

    public void OnProjectileAttackEnd()
    {
        LogEvent("Projectile Attack ended");
    }

    // -------------------------
    // Spin Attack Events
    // -------------------------

    public void OnSpinAttackStart()
    {
        LogEvent("Spin Attack started");
    }

    public void OnSpinAttackHit()
    {
        LogEvent("Spin Attack hit frame reached");
    }

    public void OnSpinAttackEnd()
    {
        LogEvent("Spin Attack ended");
    }

    // -------------------------
    // Helper
    // -------------------------

    private void LogEvent(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log("[Enemy 2 Animation Event] " + gameObject.name + ": " + message);
    }
}