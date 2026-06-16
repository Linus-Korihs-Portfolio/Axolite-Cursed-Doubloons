using UnityEngine;

public class BurrowerAnimationEvents : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

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
    }

    public void OnFirstAttackHit()
    {
        LogEvent("FirstAttack hit frame reached");
    }

    public void OnFirstAttackEnd()
    {
        LogEvent("FirstAttack ended");
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
    }

    public void OnSecondAttackEnd()
    {
        LogEvent("SecondAttack ended");
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
    }

    public void OnGrabAttackHit()
    {
        LogEvent("GrabAttack hit frame reached");
    }

    public void OnGrabAttackLift()
    {
        LogEvent("GrabAttack lift frame reached");
    }

    public void OnGrabAttackEnd()
    {
        LogEvent("GrabAttack ended");
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
    }

    public void OnDeathDiggingStart()
    {
        LogEvent("DeathWhileDigging started");
    }

    public void OnDeathDiggingFinished()
    {
        LogEvent("DeathWhileDigging finished");
    }

    public void OnDeathHit()
    {
        LogEvent("Death hit frame reached");
    }

    public void OnDeathFinished()
    {
        LogEvent("Death animation finished");
    }

    // Aliases for current Animation Event names

    public void OnDiggingDeathFinished()
    {
        LogEvent("Digging death animation finished");
    }

    public void OnFlyingDeathFinished()
    {
        LogEvent("Flying death animation finished");
    }

    private void LogEvent(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log("[Burrower Animation Event] " + gameObject.name + ": " + message);
    }
}