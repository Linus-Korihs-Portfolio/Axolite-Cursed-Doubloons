using UnityEngine;

public class RunSetupData : MonoBehaviour
{
    public static RunSetupData Instance;

    public int typeA;
    public int typeB;
    public int typeC;

    public int maxTotal = 15;

    private void Awake()
    {
        Instance = this;
    }
}