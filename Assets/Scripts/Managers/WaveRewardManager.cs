
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

    [Header("Last Wave Reward")]
    [SerializeField, Min(0)]
    private int lastCalculatedReward;

    [SerializeField, Min(0)]
    private int lastSuccessfulServices;

    [SerializeField, Range(0f, 100f)]
    private float lastWavePerformance;

    [SerializeField]
    private int lastProcessedWave = -1;

    private ServicePerformanceTracker performanceTracker;

    public int CoinsPerSuccessfulService =>
        coinsPerSuccessfulService;

    public int LastCalculatedReward =>
        lastCalculatedReward;

    public int LastSuccessfulServices =>
        lastSuccessfulServices;

    public float LastWavePerformance =>
        lastWavePerformance;

    private void Start()
    {
        performanceTracker =
            FindFirstObjectByType<ServicePerformanceTracker>();

        if (performanceTracker == null)
        {
            Debug.LogError(
                "WaveRewardManager: ServicePerformanceTracker not found!"
            );

            enabled = false;
            return;
        }

        performanceTracker.OnWaveResultStored += HandleWaveResultStored;

        Debug.Log(
            "WaveRewardManager: Connected to ServicePerformanceTracker."
        );
    }

    private void HandleWaveResultStored(
        ServicePerformanceTracker.WaveResult result)
    {
        if (result == null || result.waveNumber <= lastProcessedWave)
            return;

        lastProcessedWave = result.waveNumber;

        lastSuccessfulServices = 0;
        lastWavePerformance = 0f;
        lastCalculatedReward = 0;

        if (result.serviceResults == null)
            return;

        float weightedPerformance = 0f;
        int totalRequests = 0;

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

        if (totalRequests > 0)
        {
            lastWavePerformance = Mathf.Clamp(
                weightedPerformance / totalRequests,
                0f,
                100f
            );
        }

        // SCRUM-204: Calculate reward without Resort Rating multiplier.
        lastCalculatedReward = CalculateBaseReward(
            lastSuccessfulServices,
            lastWavePerformance
        );

        Debug.Log(
            $"WaveRewardManager: Wave {result.waveNumber} " +
            $"successful services={lastSuccessfulServices}, " +
            $"performance={lastWavePerformance:F1}%, " +
            $"base reward={lastCalculatedReward} coins"
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

    private void OnDestroy()
    {
        if (performanceTracker != null)
        {
            performanceTracker.OnWaveResultStored -= HandleWaveResultStored;
        }
    }
}
