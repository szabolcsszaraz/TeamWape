
using System;
using UnityEngine;

public class GuestSatisfactionManager : MonoBehaviour
{
    public static GuestSatisfactionManager Instance { get; private set; }

    [Header("Weights")]
    [Tooltip("How much service quality (building stars) counts.")]
    [SerializeField, Min(0f)] private float qualityWeight = 0.8f;

    [Tooltip("How much waiting time counts.")]
    [SerializeField, Min(0f)] private float waitWeight = 0.2f;

    [Header("Waiting Time")]
    [Tooltip("Waits up to this many seconds still give a full wait score.")]
    [SerializeField, Min(0f)] private float waitGraceSeconds = 10f;

    [Tooltip("At this many seconds the wait score reaches its minimum.")]
    [SerializeField, Min(0f)] private float waitWorstSeconds = 30f;

    [Tooltip("Lowest wait score (0-1), even for very long waits.")]
    [SerializeField, Range(0f, 1f)] private float minWaitScore = 0.25f;

    [Header("Current Wave (read only)")]
    [SerializeField, Range(0f, 100f)] private float satisfaction;
    [SerializeField, Range(0, 5)] private int rating;
    [SerializeField] private int servedUnits;
    [SerializeField] private float averageWaitTime;
    [SerializeField] private float averageQualityStars;

    private float totalScore;
    private float totalWaitTime;
    private float totalQualityStars;

    private WaveManager waveManager;
    private bool waveCompleteReported;

    // 0-100 %, average of every served unit this wave.
    public float Satisfaction => satisfaction;

    // 1-5 stars derived from Satisfaction (0 = nothing served yet).
    public int Rating => rating;

    public int ServedUnits => servedUnits;
    public float AverageWaitTime => averageWaitTime;
    public float AverageQualityStars => averageQualityStars;

    public event Action<float> OnSatisfactionChanged;

    // int = wave number, float = final satisfaction.
    public event Action<int, float> OnWaveSatisfactionCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        waveManager = WaveManager.Instance;

        if (waveManager != null)
        {
            waveManager.OnWaveStarted += HandleWaveStarted;
        }
        else
        {
            Debug.LogError("GuestSatisfactionManager: WaveManager not found!");
        }
    }

    private void Update()
    {
        CheckWaveComplete();
    }

    public void RecordService(
        GuestNeedType needType,
        float waitTime,
        int qualityStars)
    {
        float unitScore = CalculateUnitScore(waitTime, qualityStars);

        servedUnits++;
        totalScore += unitScore;
        totalWaitTime += waitTime;
        totalQualityStars += qualityStars;

        satisfaction = totalScore / servedUnits * 100f;
        rating = CalculateRating(satisfaction);
        averageWaitTime = totalWaitTime / servedUnits;
        averageQualityStars = totalQualityStars / servedUnits;

        Debug.Log(
            $"Satisfaction: {needType.ToDisplayName()} unit scored " +
            $"{unitScore * 100f:0}% (waited {waitTime:0.0}s, " +
            $"{qualityStars} stars). Wave: {satisfaction:0}%"
        );

        OnSatisfactionChanged?.Invoke(satisfaction);
    }

    // Returns 0-1.
    public float CalculateUnitScore(float waitTime, int qualityStars)
    {
        float qualityScore = Mathf.Clamp(qualityStars, 1, 5) / 5f;
        float waitScore = CalculateWaitScore(waitTime);

        float weightSum = qualityWeight + waitWeight;

        if (weightSum <= 0f)
        {
            return 0f;
        }

        return (qualityScore * qualityWeight + waitScore * waitWeight)
               / weightSum;
    }

    // Returns minWaitScore-1. Full score inside the grace time,
    // then drops linearly until waitWorstSeconds.
    public float CalculateWaitScore(float waitTime)
    {
        if (waitTime <= waitGraceSeconds)
        {
            return 1f;
        }

        float t = Mathf.InverseLerp(
            waitGraceSeconds,
            waitWorstSeconds,
            waitTime
        );

        return Mathf.Lerp(1f, minWaitScore, t);
    }

    private int CalculateRating(float satisfactionPercent)
    {
        return Mathf.Clamp(
            Mathf.CeilToInt(satisfactionPercent / 20f),
            1,
            5
        );
    }

    // A wave is complete when all tourists arrived and every
    // need with demand has been fully served.
    private void CheckWaveComplete()
    {
        if (waveCompleteReported ||
            waveManager == null ||
            !waveManager.IsWaveActive ||
            !waveManager.IsArrivalComplete ||
            servedUnits == 0)
        {
            return;
        }

        GuestNeedsManager needsManager = GuestNeedsManager.Instance;

        if (needsManager == null)
        {
            return;
        }

        foreach (GuestNeed need in needsManager.Needs)
        {
            if (need.HasDemand && !need.IsFullyServed)
            {
                return;
            }
        }

        waveCompleteReported = true;

        Debug.Log(
            $"Wave {waveManager.CurrentWave} complete! " +
            $"Satisfaction: {satisfaction:0}% ({rating} stars), " +
            $"avg wait {averageWaitTime:0.0}s, " +
            $"avg quality {averageQualityStars:0.0} stars"
        );

        OnWaveSatisfactionCompleted?.Invoke(
            waveManager.CurrentWave,
            satisfaction
        );
    }

    private void HandleWaveStarted(int waveNumber)
    {
        servedUnits = 0;
        totalScore = 0f;
        totalWaitTime = 0f;
        totalQualityStars = 0f;

        satisfaction = 0f;
        rating = 0;
        averageWaitTime = 0f;
        averageQualityStars = 0f;

        waveCompleteReported = false;

        OnSatisfactionChanged?.Invoke(satisfaction);
    }

    private void OnValidate()
    {
        if (waitWorstSeconds < waitGraceSeconds)
        {
            waitWorstSeconds = waitGraceSeconds;
        }
    }

    private void OnDestroy()
    {
        if (waveManager != null)
        {
            waveManager.OnWaveStarted -= HandleWaveStarted;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    [ContextMenu("Test / Log Score Examples")]
    private void TestLogScoreExamples()
    {
        int[] starOptions = { 1, 2, 3, 5 };
        float[] waitOptions = { 0f, 15f, 30f };

        foreach (int stars in starOptions)
        {
            foreach (float wait in waitOptions)
            {
                Debug.Log(
                    $"{stars} stars, waited {wait:0}s -> " +
                    $"{CalculateUnitScore(wait, stars) * 100f:0}%"
                );
            }
        }
    }
}
