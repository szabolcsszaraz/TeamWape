
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

    public float CurrentRating => currentRating;

    public event Action<float> OnRatingChanged;

    private void Awake()
    {
        currentRating = Mathf.Clamp(startingRating, 0f, 100f);
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
}
