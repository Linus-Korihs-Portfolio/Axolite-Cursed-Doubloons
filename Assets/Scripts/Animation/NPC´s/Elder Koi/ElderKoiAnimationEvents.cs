using UnityEngine;

public class ElderKoiAnimationEvents : MonoBehaviour
{
    [SerializeField] private ElderKoiAnimatorBridge bridge;

    private void Awake()
    {
        if (bridge == null)
            bridge = GetComponentInParent<ElderKoiAnimatorBridge>();

        if (bridge == null)
            bridge = GetComponentInChildren<ElderKoiAnimatorBridge>();
    }

    public void AnimationEvent_DialogFinished()
    {
        if (bridge != null)
            bridge.PlayIdle();
    }

    public void AnimationEvent_IdleBreakFinished()
    {
        if (bridge != null)
            bridge.PlayIdle();
    }

    public void AnimationEvent_ReturnToIdle()
    {
        if (bridge != null)
            bridge.PlayIdle();
    }

    public void AnimationEvent_DebugMessage(string message)
    {
        Debug.Log("[Elder Koi Animation Event] " + message, this);
    }
}