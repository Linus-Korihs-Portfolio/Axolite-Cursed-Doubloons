using UnityEngine;

public class PinchAnimationEvents : MonoBehaviour
{
    [SerializeField] private PinchAnimatorBridge bridge;

    private void Awake()
    {
        if (bridge == null)
            bridge = GetComponentInParent<PinchAnimatorBridge>();

        if (bridge == null)
            bridge = GetComponentInChildren<PinchAnimatorBridge>();
    }

    public void AnimationEvent_DialogFinished()
    {
        if (bridge != null)
        {
            bridge.SetCoinVisible(false);
            bridge.PlayIdle();
        }
    }

    public void AnimationEvent_IdleBreakFinished()
    {
        if (bridge != null)
        {
            bridge.SetCoinVisible(false);
            bridge.PlayIdle();
        }
    }

    public void AnimationEvent_ReturnToIdle()
    {
        if (bridge != null)
        {
            bridge.SetCoinVisible(false);
            bridge.PlayIdle();
        }
    }

    public void AnimationEvent_ShowCoin()
    {
        if (bridge != null)
            bridge.SetCoinVisible(true);
    }

    public void AnimationEvent_HideCoin()
    {
        if (bridge != null)
            bridge.SetCoinVisible(false);
    }

    public void AnimationEvent_DebugMessage(string message)
    {
        Debug.Log("[Pinch Animation Event] " + message, this);
    }
}