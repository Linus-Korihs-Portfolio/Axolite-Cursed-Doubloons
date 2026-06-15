using UnityEngine;

public sealed class PCGGeneratedContentMarker : MonoBehaviour
{
    [SerializeField, HideInInspector] private LevelContentSpawner owner;

    public LevelContentSpawner Owner => owner;

    public void Initialize(LevelContentSpawner contentOwner)
    {
        owner = contentOwner;
    }
}
