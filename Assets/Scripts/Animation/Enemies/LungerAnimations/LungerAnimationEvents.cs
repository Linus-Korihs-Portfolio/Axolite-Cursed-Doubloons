using UnityEngine;

public class LungerAnimationEvents : MonoBehaviour
{
    [SerializeField] private LungerEnemy lungerEnemy;
    [SerializeField] private bool logEvents = true;

    private void Awake()
    {
        ResolveEnemy();
    }

    public void OnMainAttackHit()
    {
        Log("Main Attack Hit Frame reached.");

        if (ResolveEnemy())
            lungerEnemy.OnMainAttackHitFrame();
    }

    public void OnLungeAttackStart()
    {
        Log("Lunge Attack started.");
    }

    public void OnLungeAttackHit()
    {
        Log("Lunge Attack Hit Frame reached.");

        if (ResolveEnemy())
            lungerEnemy.OnLungeAttackHitFrame();
    }

    public void OnLungeAttackEnd()
    {
        Log("Lunge Attack ended.");
    }

    public void OnFootstep()
    {
        Log("Footstep.");
    }

    public void OnDeathFinished()
    {
        Log("Death animation finished.");

        if (ResolveEnemy())
            lungerEnemy.OnDeathAnimationFinished();
    }

    private bool ResolveEnemy()
    {
        if (lungerEnemy == null)
            lungerEnemy = GetComponentInParent<LungerEnemy>();

        if (lungerEnemy == null)
        {
            Debug.LogError("[LungerAnimationEvents:" + name + "] No LungerEnemy found in parents.", this);
            return false;
        }

        return true;
    }

    private void Log(string message)
    {
        if (logEvents)
            Debug.Log("[LungerAnimationEvents:" + name + "] " + message, this);
    }
}
