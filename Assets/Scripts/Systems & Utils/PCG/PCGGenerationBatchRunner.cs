using System.Collections.Generic;
using PCG.RoomAssembler.Metrics;
using UnityEngine;

public sealed class PCGGenerationBatchRunner : MonoBehaviour
{
    [Header("Generator")]
    [SerializeField] private RoomAssemblerGenerator generator;

    [Header("Batch")]
    [SerializeField] private string variantId = "Standard";
    [Min(1)] [SerializeField] private int runs = 100;
    [SerializeField] private int startSeed = 10000;
    [SerializeField] private bool incrementSeed = true;
    [SerializeField] private bool runOnStart;

    [Header("CSV Export")]
    [SerializeField] private string outputDirectory = "PCG_Metrics";
    [SerializeField] private string outputFilePrefix = "pcg_metrics";
    [SerializeField] private bool logEachRun;

    public string LastExportPath { get; private set; }
    public IReadOnlyList<PCGGenerationMetrics> LastResults => lastResults;

    private readonly List<PCGGenerationMetrics> lastResults = new();

    private void Start()
    {
        if (runOnStart)
            RunBatchAndExportCsv();
    }

    [ContextMenu("Run Batch And Export CSV")]
    public void RunBatchAndExportCsv()
    {
        if (generator == null)
            generator = GetComponent<RoomAssemblerGenerator>();

        if (generator == null)
        {
            Debug.LogError("[PCG Metrics] No RoomAssemblerGenerator assigned.", this);
            return;
        }

        lastResults.Clear();

        for (int i = 0; i < runs; i++)
        {
            int seed = incrementSeed ? startSeed + i : startSeed;
            PCGGenerationMetrics metrics = generator.GenerateWithMetrics(seed, i);
            metrics.variantId = string.IsNullOrWhiteSpace(variantId) ? "Standard" : variantId.Trim();
            lastResults.Add(metrics);

            if (logEachRun)
            {
                Debug.Log(
                    $"[PCG Metrics] Run {i + 1}/{runs}: Seed={metrics.seed}, " +
                    $"Success={metrics.success}, Rooms={metrics.actualRoomsBeforeCapping}, " +
                    $"OpenBeforeCap={metrics.openSocketsBeforeCapping}, " +
                    $"Path={metrics.startToBossGraphDistance}.",
                    this);
            }
        }

        LastExportPath = PCGGenerationMetricsCsvExporter.Export(
            lastResults,
            outputDirectory,
            outputFilePrefix);

        Debug.Log($"[PCG Metrics] Exported {lastResults.Count} run(s) to {LastExportPath}", this);

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }
}
