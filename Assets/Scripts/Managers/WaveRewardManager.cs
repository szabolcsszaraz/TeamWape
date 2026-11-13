
using System;
using UnityEngine;

public class WaveRewardManager : MonoBehaviour
{
    [Header("Reward Rules")]
    [SerializeField, Min(0)]
    private int coinsPerSuccessfulService = 10;

    [SerializeField, Range(0f, 1f)]
    private float minimumRatingMultiplier = 0.5f;

    [SerializeField, Range(0f, 2f)]
    private float maximumRatingMultiplier = 1.5f;

    [Header("Minimum Reward Rules")]
    [SerializeField, Min(0)]
    private int minimumWaveReward = 10;

    [Header("Last Wave Reward")]
    [SerializeField, Min(0)]
    private int lastCalculatedReward;

    [SerializeField, Min(0)]
    private int lastSuccessfulServices;

    [SerializeField, Range(0f, 100f)]
    private float lastWavePerformance;

    [SerializeField, Range(0f, 100f)]
    private float lastResortRating;

    [SerializeField]
    private float lastRatingMultiplier = 1f;

    [SerializeField]
    private int lastProcessedWave = -1;

    private ServicePerformanceTracker performanceTracker;
    private ResortRatingManager ratingManager;

    public int CoinsPerSuccessfulService =>
        coinsPerSuccessfulService;

    public int LastCalculatedReward =>
        lastCalculatedReward;

    public int LastSuccessfulServices =>
        lastSuccessfulServices;

    public float LastWavePerformance =>
        lastWavePerformance;

    public float LastResortRating =>
        lastResortRating;

    public float LastRatingMultiplier =>
        lastRatingMultiplier;

    public event Action<int, int> OnRewardCalculated;

    private void Start()
    {
        performanceTracker =
            FindFirstObjectByType<ServicePerformanceTracker>();

        ratingManager =
            FindFirstObjectByType<ResortRatingManager>();

        if (performanceTracker == null || ratingManager == null)
        {
            Debug.LogError(
                "WaveRewardManager: Required manager missing!"
            );

            enabled = false;
            return;
        }

        performanceTracker.OnWaveResultStored += HandleWaveResultStored;
        ratingManager.OnRatingChanged += HandleRatingChanged;

        Debug.Log(
            "WaveRewardManager: Connected to performance and rating managers."
        );
    }

    private void HandleWaveResultStored(
        ServicePerformanceTracker.WaveResult result)
    {
        if (result == null || result.waveNumber <= lastProcessedWave)
            return;

        int totalRequests = 0;

        if (result.serviceResults != null)
        {
            foreach (var service in result.serviceResults)
            {
                if (service != null)
                    totalRequests += Mathf.Max(0, service.totalRequests);
            }
        }

        // No requests: rating does not change.
        if (totalRequests == 0)
        {
            ProcessWaveReward(result, ratingManager.CurrentRating);
        }

        // Otherwise wait for the updated Resort Rating event.
    }

    private void HandleRatingChanged(float newRating)
    {
        if (performanceTracker == null)
            return;

        var result = performanceTracker.LastWaveResult;

        if (result == null || result.waveNumber <= lastProcessedWave)
            return;

        ProcessWaveReward(result, newRating);
    }

    private void ProcessWaveReward(
        ServicePerformanceTracker.WaveResult result,
        float resortRating)
    {
        if (result == null || result.waveNumber <= lastProcessedWave)
            return;

        lastProcessedWave = result.waveNumber;

        lastSuccessfulServices = 0;
        lastWavePerformance = 0f;

        lastResortRating =
            Mathf.Clamp(resortRating, 0f, 100f);

        lastRatingMultiplier =
            CalculateRatingMultiplier(lastResortRating);

        float weightedPerformance = 0f;
        int totalRequests = 0;

        if (result.serviceResults != null)
        {
            foreach (var service in result.serviceResults)
            {
                if (service == null)
                    continue;

                lastSuccessfulServices +=
                    Mathf.Max(0, service.successfulRequests);

                if (service.totalRequests <= 0)
                    continue;

                weightedPerformance +=
                    Mathf.Clamp(service.performanceScore, 0f, 100f)
                    * service.totalRequests;

                totalRequests += service.totalRequests;
            }
        }

        if (totalRequests > 0)
        {
            lastWavePerformance = Mathf.Clamp(
                weightedPerformance / totalRequests,
                0f,
                100f
            );
        }

        // Normal reward calculation.
        int normalReward = CalculateReward(
            lastSuccessfulServices,
            lastWavePerformance,
            lastResortRating
        );

        // SCRUM-220: Apply minimum reward before crediting Coins.
        lastCalculatedReward = ApplyMinimumReward(
            normalReward,
            totalRequests
        );

        Debug.Log(
            $"WaveRewardManager: Wave {result.waveNumber} " +
            $"successful services={lastSuccessfulServices}, " +
            $"performance={lastWavePerformance:F1}%, " +
            $"resort rating={lastResortRating:F1}%, " +
            $"multiplier={lastRatingMultiplier:F2}x, " +
            $"calculated reward={normalReward}, " +
            $"final reward={lastCalculatedReward} coins"
        );

        OnRewardCalculated?.Invoke(
            result.waveNumber,
            lastCalculatedReward
        );
    }

    public int CalculateBaseReward(
        int successfulServices,
        float wavePerformance)
    {
        int safeServices = Mathf.Max(0, successfulServices);

        float performanceFactor =
            Mathf.Clamp(wavePerformance, 0f, 100f) / 100f;

        float reward =
            safeServices *
            coinsPerSuccessfulService *
            performanceFactor;

        return Mathf.Max(0, Mathf.RoundToInt(reward));
    }

    public float CalculateRatingMultiplier(float resortRating)
    {
        float normalizedRating =
            Mathf.Clamp(resortRating, 0f, 100f) / 100f;

        return Mathf.Lerp(
            minimumRatingMultiplier,
            maximumRatingMultiplier,
            normalizedRating
        );
    }

    public int CalculateReward(
        int successfulServices,
        float wavePerformance,
        float resortRating)
    {
        int safeServices = Mathf.Max(0, successfulServices);

        float performanceFactor =
            Mathf.Clamp(wavePerformance, 0f, 100f) / 100f;

        float ratingMultiplier =
            CalculateRatingMultiplier(resortRating);

        float reward =
            safeServices *
            coinsPerSuccessfulService *
            performanceFactor *
            ratingMultiplier;

        return Mathf.Max(0, Mathf.RoundToInt(reward));
    }

    public bool IsBelowMinimumReward(
        int calculatedReward,
        int totalRequests)
    {
        if (totalRequests <= 0)
            return false;

        return calculatedReward < minimumWaveReward;
    }

    public int ApplyMinimumReward(
        int calculatedReward,
        int totalRequests)
    {
        if (totalRequests <= 0)
            return 0;

        return Mathf.Max(
            Mathf.Max(0, calculatedReward),
            minimumWaveReward
        );
    }

    private void OnDestroy()
    {
        if (performanceTracker != null)
        {
            performanceTracker.OnWaveResultStored -= HandleWaveResultStored;
        }

        if (ratingManager != null)
        {
            ratingManager.OnRatingChanged -= HandleRatingChanged;
        }
    }
}
