
using System;
using UnityEngine;

public class ResortProgressionManager : MonoBehaviour
{
    [Header("Starting Progression")]
    [SerializeField, Min(1)]
    private int startingLevel = 1;

    [SerializeField, Min(0)]
    private int startingXP = 0;

    [Header("XP Calculation Rules")]
    [SerializeField, Min(1)]
    private int baseXPRequired = 100;

    [SerializeField, Min(0)]
    private int xpIncreasePerLevel = 50;

    [Header("Wave XP Rewards")]
    [SerializeField, Min(0)]
    private int xpPerCompletedWave = 50;

    [SerializeField, Min(0)]
    private int xpPerSuccessfulService = 5;

    [Header("Current Resort Progression")]
    [SerializeField, Min(1)]
    private int currentLevel = 1;

    [SerializeField, Min(0)]
    private int currentXP = 0;

    [SerializeField, Min(0)]
    private int totalXP = 0;

    [Header("Last Wave XP")]
    [SerializeField, Min(0)]
    private int lastCalculatedXP;

    [SerializeField, Min(0)]
    private int lastSuccessfulServices;

    [SerializeField]
    private int lastProcessedWave = -1;

    private ServicePerformanceTracker performanceTracker;

    public int CurrentLevel => currentLevel;
    public int CurrentXP => currentXP;
    public int TotalXP => totalXP;

    public int LastCalculatedXP => lastCalculatedXP;
    public int LastSuccessfulServices => lastSuccessfulServices;

    public int XPRequiredForNextLevel =>
        GetRequiredXPForLevel(currentLevel);

    public float LevelProgress =>
        Mathf.Clamp01(
            (float)currentXP / XPRequiredForNextLevel
        );

    public event Action<int, int> OnProgressionChanged;
    public event Action<int> OnLevelUp;

    private void Awake()
    {
        currentLevel = Mathf.Max(1, startingLevel);
        currentXP = Mathf.Max(0, startingXP);
        totalXP = currentXP;
    }

    private void Start()
    {
        performanceTracker =
            FindFirstObjectByType<ServicePerformanceTracker>();

        if (performanceTracker == null)
        {
            Debug.LogError(
                "ResortProgressionManager: " +
                "ServicePerformanceTracker not found!"
            );

            enabled = false;
            return;
        }

        performanceTracker.OnWaveResultStored += HandleWaveResultStored;

        Debug.Log(
            "ResortProgressionManager: Connected to ServicePerformanceTracker."
        );
    }

    private void HandleWaveResultStored(
        ServicePerformanceTracker.WaveResult result)
    {
        if (result == null ||
            result.waveNumber <= lastProcessedWave)
            return;

        lastProcessedWave = result.waveNumber;
        lastSuccessfulServices = 0;

        if (result.serviceResults != null)
        {
            foreach (var service in result.serviceResults)
            {
                if (service == null)
                    continue;

                lastSuccessfulServices +=
                    Mathf.Max(0, service.successfulRequests);
            }
        }

        lastCalculatedXP =
            CalculateWaveXP(lastSuccessfulServices);

        Debug.Log(
            $"ResortProgressionManager: Wave {result.waveNumber} " +
            $"successful services={lastSuccessfulServices}, " +
            $"earned XP={lastCalculatedXP}"
        );
    }

    public int CalculateWaveXP(int successfulServices)
    {
        int safeServices = Mathf.Max(0, successfulServices);

        long earnedXP =
            (long)Mathf.Max(0, xpPerCompletedWave) +
            (long)safeServices * Mathf.Max(0, xpPerSuccessfulService);

        return (int)Math.Min(earnedXP, int.MaxValue);
    }

    public int GetRequiredXPForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);

        long requiredXP =
            (long)Mathf.Max(1, baseXPRequired) +
            ((long)safeLevel - 1) *
            Mathf.Max(0, xpIncreasePerLevel);

        return (int)Math.Min(requiredXP, int.MaxValue);
    }

    public bool CanLevelUp()
    {
        return currentLevel < int.MaxValue &&
               currentXP >= XPRequiredForNextLevel;
    }

    private void OnDestroy()
    {
        if (performanceTracker != null)
        {
            performanceTracker.OnWaveResultStored -= HandleWaveResultStored;
        }
    }
}
