using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class RuntimeNavMeshBuilder : MonoBehaviour
{
    [Header("Surface")]
    [SerializeField] private NavMeshSurface surface;
    [SerializeField] private bool createSurfaceOnGeneratedRoot = true;
    [SerializeField] private bool buildChildSurfaces;

    [Header("Collection")]
    [SerializeField] private bool useGeneratedLayers = true;
    [SerializeField] private LayerMask layerMask = ~0;
    [SerializeField] private NavMeshCollectGeometry geometry = NavMeshCollectGeometry.PhysicsColliders;
    [SerializeField] private int defaultArea;

    [Header("Debug")]
    [SerializeField] private bool log;

    public void Build(Transform generatedRoot)
    {
        if (generatedRoot == null)
        {
            Debug.LogWarning($"{name}: cannot build NavMesh without generated root.", this);
            return;
        }

        Physics.SyncTransforms();

        int builtCount = 0;

        if (buildChildSurfaces)
        {
            builtCount += BuildChildSurfaces(generatedRoot);
        }
        else
        {
            NavMeshSurface targetSurface = ResolveSurface(generatedRoot);
            if (targetSurface == null)
            {
                Debug.LogWarning($"{name}: no NavMeshSurface available for runtime build.", this);
                return;
            }

            ConfigureSurface(targetSurface);
            targetSurface.BuildNavMesh();
            builtCount++;
        }

        if (log)
        {
            Debug.Log($"[PCG NavMesh] Built {builtCount} NavMeshSurface(s) under {generatedRoot.name}.", this);
        }
    }

    private int BuildChildSurfaces(Transform generatedRoot)
    {
        NavMeshSurface[] surfaces = generatedRoot.GetComponentsInChildren<NavMeshSurface>(true);
        int builtCount = 0;

        for (int i = 0; i < surfaces.Length; i++)
        {
            NavMeshSurface childSurface = surfaces[i];
            if (childSurface == null || !childSurface.gameObject.activeInHierarchy) continue;

            childSurface.BuildNavMesh();
            builtCount++;
        }

        if (builtCount == 0)
        {
            Debug.LogWarning($"{name}: buildChildSurfaces is enabled, but no active child NavMeshSurface was found under {generatedRoot.name}.", this);
        }

        return builtCount;
    }

    private NavMeshSurface ResolveSurface(Transform generatedRoot)
    {
        if (surface != null) return surface;

        NavMeshSurface existing = generatedRoot.GetComponent<NavMeshSurface>();
        if (existing != null)
        {
            surface = existing;
            return surface;
        }

        if (!createSurfaceOnGeneratedRoot) return null;

        surface = generatedRoot.gameObject.AddComponent<NavMeshSurface>();
        return surface;
    }

    private void ConfigureSurface(NavMeshSurface targetSurface)
    {
        targetSurface.collectObjects = CollectObjects.Children;
        targetSurface.useGeometry = geometry;
        targetSurface.defaultArea = defaultArea;
        targetSurface.layerMask = useGeneratedLayers ? ResolveGeneratedLayerMask() : layerMask;
    }

    private LayerMask ResolveGeneratedLayerMask()
    {
        int generated = LayerMask.NameToLayer("Generated");
        int capGenerated = LayerMask.NameToLayer("Cap(Generated)");

        int mask = 0;
        if (generated >= 0) mask |= 1 << generated;
        if (capGenerated >= 0) mask |= 1 << capGenerated;

        return mask != 0 ? mask : layerMask;
    }
}
