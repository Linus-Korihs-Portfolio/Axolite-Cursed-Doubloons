using UnityEngine;

public class BurrowerAnimationEvents : MonoBehaviour
{
    [SerializeField] private BurrowerEnemy burrowerEnemy;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        ResolveEnemy();
    }

    // WakeUp / First Attack

    public void OnWakeUpStarted()
    {
        LogEvent("WakeUp started");
    }

    public void OnWakeUp()
    {
        LogEvent("WakeUp event reached");
    }

    public void OnFirstAttackBite()
    {
        LogEvent("FirstAttack bite / snap frame reached");

        if (ResolveEnemy())
            burrowerEnemy.OnFirstAttackHitFrame();
    }

    public void OnFirstAttackHit()
    {
        LogEvent("FirstAttack hit frame reached");

        if (ResolveEnemy())
            burrowerEnemy.OnFirstAttackHitFrame();
    }

    public void OnFirstAttackEnd()
    {
        LogEvent("FirstAttack ended");

        if (ResolveEnemy())
            burrowerEnemy.OnFirstAttackEndFrame();
    }

    // Fly Up

    public void OnFlyUpStart()
    {
        LogEvent("FlyUp started");
    }

    public void OnFlyUpEnd()
    {
        LogEvent("FlyUp ended");
    }

    // Walk / Base

    public void OnWalkBaseLoop()
    {
        LogEvent("Walk/Base loop event");
    }

    public void OnFootstep()
    {
        LogEvent("Footstep");
    }

    // Second Attack

    public void OnSecondAttackStart()
    {
        LogEvent("SecondAttack started");
    }

    public void OnSecondAttackHit()
    {
        LogEvent("SecondAttack hit frame reached");

        if (ResolveEnemy())
            burrowerEnemy.OnSecondAttackHitFrame();
    }

    public void OnSecondAttackEnd()
    {
        LogEvent("SecondAttack ended");

        if (ResolveEnemy())
            burrowerEnemy.OnSecondAttackEndFrame();
    }

    // Hover

    public void OnHoverStart()
    {
        LogEvent("Hover started");
    }

    public void OnHoverLoop()
    {
        LogEvent("Hover loop event");
    }

    public void OnHoverEnd()
    {
        LogEvent("Hover ended");
    }

    // Grab Attack

    public void OnGrabAttackStart()
    {
        LogEvent("GrabAttack started");
    }

    public void OnGrabAttackGrab()
    {
        LogEvent("GrabAttack grab frame reached");

        if (ResolveEnemy())
            burrowerEnemy.OnGrabAttackGrabFrame();
    }

    public void OnGrabAttackHit()
    {
        LogEvent("GrabAttack hit frame reached");

        if (ResolveEnemy())
            burrowerEnemy.OnGrabAttackHitFrame();
    }

    public void OnGrabAttackLift()
    {
        LogEvent("GrabAttack lift frame reached");
    }

    public void OnGrabAttackEnd()
    {
        LogEvent("GrabAttack ended");

        if (ResolveEnemy())
            burrowerEnemy.OnGrabAttackEndFrame();
    }

    // Fly Down

    public void OnFlyDownStart()
    {
        LogEvent("FlyDown started");
    }

    public void OnFlyDownLanded()
    {
        LogEvent("FlyDown landed / ground contact reached");
    }

    public void OnFlyDownEnd()
    {
        LogEvent("FlyDown ended");
    }

    // Digging

    public void OnDigDownStart()
    {
        LogEvent("DigDown started");
    }

    public void OnDiggingStarted()
    {
        LogEvent("DiggingAndDown started");
    }

    public void OnDiggingHidden()
    {
        LogEvent("Burrower is hidden in ground");
    }

    public void OnDiggingFinished()
    {
        LogEvent("DiggingAndDown finished");
    }

    // Death Events

    public void OnDeathFlyingStart()
    {
        LogEvent("DeathWhileFlying started");
    }

    public void OnDeathFlyingFinished()
    {
        LogEvent("DeathWhileFlying finished");

        if (ResolveEnemy())
            burrowerEnemy.OnDeathAnimationFinished();
    }

    public void OnDeathDiggingStart()
    {
        LogEvent("DeathWhileDigging started");
    }

    public void OnDeathDiggingFinished()
    {
        LogEvent("DeathWhileDigging finished");

        if (ResolveEnemy())
            burrowerEnemy.OnDeathAnimationFinished();
    }

    public void OnDeathHit()
    {
        LogEvent("Death hit frame reached");
    }

    public void OnDeathFinished()
    {
        LogEvent("Death animation finished");

        if (ResolveEnemy())
            burrowerEnemy.OnDeathAnimationFinished();
    }

    // Aliases for current Animation Event names

    public void OnDiggingDeathFinished()
    {
        LogEvent("Digging death animation finished");

        if (ResolveEnemy())
            burrowerEnemy.OnDeathAnimationFinished();
    }

    public void OnFlyingDeathFinished()
    {
        LogEvent("Flying death animation finished");

        if (ResolveEnemy())
            burrowerEnemy.OnDeathAnimationFinished();
    }

    private bool ResolveEnemy()
    {
        if (burrowerEnemy == null)
            burrowerEnemy = GetComponentInParent<BurrowerEnemy>();

        if (burrowerEnemy == null)
        {
            Debug.LogError("[BurrowerAnimationEvents:" + name + "] No BurrowerEnemy found in parents.", this);
            return false;
        }

        return true;
    }

    private void LogEvent(string message)
    {
        if (!showDebugLogs) return;

        Debug.Log("[BurrowerAnimationEvents:" + name + "] " + message, this);
    }
}
