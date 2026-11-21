using System;
using UnityEngine;

public class ResortUnlockManager : MonoBehaviour
{
    private const float RatingPercentPerStar = 20f;

    [Header("References")]
    [SerializeField] private ResortProgressionManager progressionManager;
    [SerializeField] private ServicePerformanceTracker performanceTracker;
    [SerializeField] private ResortRatingManager ratingManager;

    [Header("Current Unlock Progress")]
    [SerializeField] private int currentResortLevel = 1;
    [SerializeField] private int lastCompletedWave;
    [SerializeField] private float currentResortRatingPercent;

    private bool refreshPending;

    public int CurrentResortLevel => currentResortLevel;
    public int LastCompletedWave => lastCompletedWave;
    public float CurrentResortRatingStars =>
        currentResortRatingPercent / RatingPercentPerStar;

    public event Action OnUnlockProgressChanged;

    public bool MeetsRatingRequirement(float minimumRatingStars)
    {
        return CurrentResortRatingStars >= minimumRatingStars;
    }

    private void Start()
    {
        if (progressionManager == null ||
            performanceTracker == null ||
            ratingManager == null)
        {
            Debug.LogError(
                "ResortUnlockManager: Assign all three manager references in the Inspector.",
                this
            );
            enabled = false;
            return;
        }

        progressionManager.OnLevelUp += HandleLevelUp;
        performanceTracker.OnWaveResultStored += HandleWaveResultStored;

        RefreshProgress();
    }

    private void HandleLevelUp(int newLevel)
    {
        refreshPending = true;
    }

    private void HandleWaveResultStored(ServicePerformanceTracker.WaveResult result)
    {
        refreshPending = true;
    }

    private void LateUpdate()
    {
        if (!refreshPending)
            return;

        refreshPending = false;
        RefreshProgress();
    }

    private void RefreshProgress()
    {
        int resortLevel = progressionManager.CurrentLevel;
        int completedWave = performanceTracker.LastWaveResult?.waveNumber ?? 0;
        float resortRatingPercent = ratingManager.CurrentRating;

        if (resortLevel == currentResortLevel &&
            completedWave == lastCompletedWave &&
            resortRatingPercent == currentResortRatingPercent)
        {
            return;
        }

        currentResortLevel = resortLevel;
        lastCompletedWave = completedWave;
        currentResortRatingPercent = resortRatingPercent;

        OnUnlockProgressChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (progressionManager != null)
            progressionManager.OnLevelUp -= HandleLevelUp;

        if (performanceTracker != null)
            performanceTracker.OnWaveResultStored -= HandleWaveResultStored;
    }
}