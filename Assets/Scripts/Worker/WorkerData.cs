
using System;
using UnityEngine;

public class WorkerData : MonoBehaviour
{
    public const int MinPerformanceLevel = 1;
    public const int MaxPerformanceLevel = 5;

    [Header("Worker Information")]
    [SerializeField] private string workerName = "Worker";

    [Header("Performance")]
    [SerializeField, Range(
        MinPerformanceLevel,
        MaxPerformanceLevel)]
    private int performanceLevel = 1;

    public string WorkerName => workerName;
    public int PerformanceLevel => performanceLevel;

    public float PerformanceMultiplier =>
        1f + (performanceLevel - 1) * 0.25f;

    public bool IsMaxLevel =>
        performanceLevel >= MaxPerformanceLevel;

    // Notifies UI and other systems about level changes.
    public event Action<WorkerData, int> OnPerformanceLevelChanged;

    public bool CanUpgrade()
    {
        return performanceLevel < MaxPerformanceLevel;
    }

    public bool UpgradePerformance()
    {
        if (!CanUpgrade())
        {
            Debug.Log(
                $"{workerName}: Already at maximum performance level.",
                this
            );

            return false;
        }

        performanceLevel++;

        OnPerformanceLevelChanged?.Invoke(
            this,
            performanceLevel
        );

        Debug.Log(
            $"{workerName}: Performance upgraded to level " +
            $"{performanceLevel} " +
            $"(x{PerformanceMultiplier:0.00}).",
            this
        );

        return true;
    }

    private void OnValidate()
    {
        performanceLevel = Mathf.Clamp(
            performanceLevel,
            MinPerformanceLevel,
            MaxPerformanceLevel
        );
    }

    [ContextMenu("Test / Upgrade Performance")]
    private void TestUpgradePerformance()
    {
        UpgradePerformance();
    }
}
