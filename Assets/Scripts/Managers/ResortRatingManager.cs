
using System;
using UnityEngine;

public class ResortRatingManager : MonoBehaviour
{
    [Header("Rating Rules")]
    [SerializeField, Range(0f, 100f)]
    private float startingRating = 50f;

    [SerializeField, Range(0f, 1f)]
    private float waveWeight = 0.3f;

    [Header("Current Resort Rating")]
    [SerializeField, Range(0f, 100f)]
    private float currentRating = 50f;

    [Header("Last Wave Performance")]
    [SerializeField]
    private int lastProcessedWave = -1;

    [SerializeField, Range(0f, 100f)]
    private float lastWavePerformance;

    [Header("Calculated Rating Change")]
    [SerializeField, Range(0f, 100f)]
    private float calculatedNewRating = 50f;

    [SerializeField]
    private float ratingChange;

    private ServicePerformanceTracker performanceTracker;

    public float CurrentRating => currentRating;
    public float LastWavePerformance => lastWavePerformance;
    public float CalculatedNewRating => calculatedNewRating;
    public float RatingChange => ratingChange;

    public event Action<float> OnRatingChanged;

    private void Awake()
    {
        currentRating = Mathf.Clamp(startingRating, 0f, 100f);
        calculatedNewRating = currentRating;
    }

    private void Start()
    {
        performanceTracker =
            FindFirstObjectByType<ServicePerformanceTracker>();

        if (performanceTracker == null)
        {
            Debug.LogError(
                "ResortRatingManager: ServicePerformanceTracker not found!"
            );

            enabled = false;
            return;
        }

        performanceTracker.OnWaveResultStored += HandleWaveResultStored;

        Debug.Log(
            "ResortRatingManager: Connected to ServicePerformanceTracker."
        );
    }

    private void HandleWaveResultStored(
        ServicePerformanceTracker.WaveResult result)
    {
        if (result == null || result.waveNumber <= lastProcessedWave)
            return;

        lastProcessedWave = result.waveNumber;

        float performance = CalculateWavePerformance(result);

        if (performance < 0f)
        {
            lastWavePerformance = 0f;
            calculatedNewRating = currentRating;
            ratingChange = 0f;

            Debug.Log(
                $"ResortRatingManager: Wave {result.waveNumber} " +
                "has no requests. Rating unchanged."
            );
            return;
        }

        lastWavePerformance = performance;

        calculatedNewRating = CalculateNewRating(performance);
        ratingChange = calculatedNewRating - currentRating;

        // SCRUM-201: Update the actual Resort Rating.
        currentRating = calculatedNewRating;

        // Notify other systems (HUD, rewards, etc.).
        OnRatingChanged?.Invoke(currentRating);

        Debug.Log(
            $"ResortRatingManager: Wave {result.waveNumber} " +
            $"performance={lastWavePerformance:F1}%, " +
            $"rating change={ratingChange:+0.0;-0.0;0.0} points, " +
            $"new resort rating={currentRating:F1}%"
        );
    }

    public float CalculateWavePerformance(
        ServicePerformanceTracker.WaveResult result)
    {
        if (result == null || result.serviceResults == null)
            return -1f;

        float weightedScore = 0f;
        int totalRequests = 0;

        foreach (var service in result.serviceResults)
        {
            if (service == null || service.totalRequests <= 0)
                continue;

            weightedScore +=
                Mathf.Clamp(service.performanceScore, 0f, 100f)
                * service.totalRequests;

            totalRequests += service.totalRequests;
        }

        if (totalRequests == 0)
            return -1f;

        return Mathf.Clamp(
            weightedScore / totalRequests,
            0f,
            100f
        );
    }

    public float CalculateNewRating(float wavePerformance)
    {
        if (wavePerformance < 0f)
            return currentRating;

        float weight = Mathf.Clamp01(waveWeight);

        return Mathf.Clamp(
            currentRating * (1f - weight)
            + Mathf.Clamp(wavePerformance, 0f, 100f) * weight,
            0f,
            100f
        );
    }

    private void OnDestroy()
    {
        if (performanceTracker != null)
        {
            performanceTracker.OnWaveResultStored -= HandleWaveResultStored;
        }
    }
}
