using UnityEngine;

public class LungerAnimationEvents : MonoBehaviour
{
    public void OnMainAttackHit()
    {
        Debug.Log("Main Attack Hit Frame reached.");
    }

    public void OnLungeAttackStart()
    {
        Debug.Log("Lunge Attack started.");
    }

    public void OnLungeAttackHit()
    {
        Debug.Log("Lunge Attack Hit Frame reached.");
    }

    public void OnLungeAttackEnd()
    {
        Debug.Log("Lunge Attack ended.");
    }

    public void OnFootstep()
    {
        Debug.Log("Footstep.");
    }

    public void OnDeathFinished()
    {
        Debug.Log("Death animation finished.");
    }
}