using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ResortAreaRequirements : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private ResortUnlockManager unlockManager;

    [Header("Requirements")]
    [SerializeField, Min(0)] private int minimumCompletedWaves;
    [SerializeField, Min(1)] private int minimumResortLevel = 1;

    [Tooltip("Set to 0 to omit the Resort Rating requirement.")]
    [SerializeField, Range(0f, 5f)] private float minimumRatingStars;

    [Header("Runtime State")]
    [SerializeField] private bool requirementsMet;

    public int MinimumCompletedWaves => minimumCompletedWaves;
    public int MinimumResortLevel => minimumResortLevel;
    public float MinimumRatingStars => minimumRatingStars;
    public bool RequirementsMet => requirementsMet;

    public event Action OnRequirementsChanged;

    private void OnEnable()
    {
        if (unlockManager == null)
        {
            SetRequirementsMet(false);
            Debug.LogError(
                "ResortAreaRequirements: Assign an Unlock Manager in the Inspector.",
                this
            );
            return;
        }

        unlockManager.OnUnlockProgressChanged += RefreshRequirements;
        RefreshRequirements();
    }

    private void OnDisable()
    {
        if (unlockManager != null)
        {
            unlockManager.OnUnlockProgressChanged -= RefreshRequirements;
        }

        SetRequirementsMet(false);
    }

    private void RefreshRequirements()
    {
        SetRequirementsMet(
            unlockManager.LastCompletedWave >= minimumCompletedWaves &&
            unlockManager.CurrentResortLevel >= minimumResortLevel &&
            unlockManager.MeetsRatingRequirement(minimumRatingStars)
        );
    }

    private void SetRequirementsMet(bool value)
    {
        if (requirementsMet == value)
            return;

        requirementsMet = value;
        OnRequirementsChanged?.Invoke();
    }
}