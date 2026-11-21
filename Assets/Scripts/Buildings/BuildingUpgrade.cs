using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildingUpgrade : MonoBehaviour
{
    [Serializable]
    private class UpgradeLevel
    {
        [Tooltip("Model shown at this level.")]
        public GameObject version;

        [Min(0)] public int cost;
        [Min(0)] public int xpReward;

        [Header("Building Statistics")]
        [Min(1)] public int capacity = 3;
        [Range(1, 5)] public int qualityStars = 2;
        [Min(0.1f)] public float serviceTime = 12f;

        [Header("Upgrade Timer")]
        [Tooltip("Seconds required to upgrade TO this level.")]
        [Min(0f)] public float upgradeDuration = 30f;
    }

    [Header("Levels (first = starting level)")]
    [SerializeField] private List<UpgradeLevel> levels = new();

    [Header("State")]
    [SerializeField, Min(1)] private int currentLevel = 1;

    [Header("Upgrade Timer State (Runtime)")]
    [SerializeField] private bool isUpgrading;
    [SerializeField, Min(0f)] private float upgradeRemainingTime;
    [SerializeField, Min(0f)] private float upgradeTotalDuration;
    [SerializeField, Min(0)] private int pendingUpgradeXP;

    [Header("Gem Skip Settings")]
    [Tooltip("How many seconds of upgrade time one Gem covers.")]
    [SerializeField, Min(0.1f)] private float secondsPerGem = 10f;

    private PlayerCoinsManager coinsManager;
    private ResortProgressionManager progressionManager;
    private BuildingRepair buildingRepair;
    private ServiceBuilding serviceBuilding;
    private BuildingLevelRequirements levelRequirements;

    public int CurrentLevel => currentLevel;
    public int MaxLevel => levels.Count;
    public bool IsUpgrading => isUpgrading;

    public float UpgradeRemainingTime =>
        isUpgrading ? Mathf.Max(0f, upgradeRemainingTime) : 0f;

    public float UpgradeTotalDuration =>
        isUpgrading ? upgradeTotalDuration : 0f;

    public float UpgradeProgress
    {
        get
        {
            if (!isUpgrading)
                return 0f;

            if (upgradeTotalDuration <= 0f)
                return 1f;

            return Mathf.Clamp01(
                1f - upgradeRemainingTime / upgradeTotalDuration
            );
        }
    }

    public int UpgradingToLevel =>
        isUpgrading ? Mathf.Min(currentLevel + 1, MaxLevel) : currentLevel;

    public int SkipUpgradeGemCost =>
        isUpgrading ? CalculateGemCost(UpgradeRemainingTime) : 0;

    public float SecondsPerGem => secondsPerGem;

    public int MinimumRequiredResortLevel =>
        levelRequirements != null
            ? levelRequirements.GetMinimumUpgradeResortLevel(currentLevel + 1)
            : 1;

    public bool MeetsResortLevelRequirement =>
        MinimumRequiredResortLevel <= 1 ||
        (progressionManager != null &&
         progressionManager.CurrentLevel >= MinimumRequiredResortLevel);

    // The action panel uses this to keep an existing upgrade visible.
    public bool CanUpgrade =>
        !isUpgrading &&
        currentLevel < MaxLevel &&
        (buildingRepair == null || buildingRepair.IsRepaired);

    public int NextLevelCost =>
        CanUpgrade ? levels[currentLevel].cost : 0;

    public int NextLevelXP =>
        CanUpgrade ? levels[currentLevel].xpReward : 0;

    public int NextLevelCapacity =>
        CanUpgrade ? levels[currentLevel].capacity : 0;

    public float NextLevelUpgradeDuration =>
        CanUpgrade ? levels[currentLevel].upgradeDuration : 0f;

    public bool CanAfford =>
        CanUpgrade &&
        MeetsResortLevelRequirement &&
        coinsManager != null &&
        coinsManager.CurrentCoins >= NextLevelCost;

    public event Action<BuildingUpgrade, int> OnUpgraded;
    public event Action<BuildingUpgrade> OnUpgradeStarted;
    public event Action<BuildingUpgrade> OnUpgradeTimerChanged;
    public event Action<BuildingUpgrade> OnUpgradeCompleted;

    private void Awake()
    {
        coinsManager = FindFirstObjectByType<PlayerCoinsManager>();
        progressionManager = FindFirstObjectByType<ResortProgressionManager>();

        buildingRepair = GetComponent<BuildingRepair>();
        serviceBuilding = GetComponent<ServiceBuilding>();
        levelRequirements = GetComponent<BuildingLevelRequirements>();

        if (serviceBuilding == null)
            serviceBuilding = GetComponentInChildren<ServiceBuilding>(true);

        currentLevel = Mathf.Clamp(currentLevel, 1, Mathf.Max(1, MaxLevel));
        secondsPerGem = Mathf.Max(0.1f, secondsPerGem);

        // Timed upgrades are runtime-only.
        isUpgrading = false;
        upgradeRemainingTime = 0f;
        upgradeTotalDuration = 0f;
        pendingUpgradeXP = 0;
    }

    private void OnValidate()
    {
        secondsPerGem = Mathf.Max(0.1f, secondsPerGem);
    }

    private void Start()
    {
        if (buildingRepair == null || buildingRepair.IsRepaired)
        {
            ApplyVersion();
            ApplyLevelStats();
        }

        if (buildingRepair != null)
            buildingRepair.OnRepaired += HandleRepaired;
    }

    private void Update()
    {
        if (!isUpgrading)
            return;

        upgradeRemainingTime = Mathf.Max(
            0f,
            upgradeRemainingTime - Time.deltaTime
        );

        OnUpgradeTimerChanged?.Invoke(this);

        if (upgradeRemainingTime <= 0f)
            CompleteUpgrade();
    }

    private void OnDestroy()
    {
        if (buildingRepair != null)
            buildingRepair.OnRepaired -= HandleRepaired;
    }

    public int CalculateGemCost(float secondsToSkip)
    {
        if (secondsToSkip <= 0f)
            return 0;

        double requiredGems = Math.Ceiling(
            secondsToSkip / Mathf.Max(0.1f, secondsPerGem)
        );

        return requiredGems >= int.MaxValue
            ? int.MaxValue
            : (int)requiredGems;
    }

    public bool TryUpgrade()
    {
        if (!MeetsResortLevelRequirement)
        {
            Debug.LogWarning(
                $"{name}: Upgrade requires Resort Level {MinimumRequiredResortLevel}.",
                this
            );
            return false;
        }

        if (!CanUpgrade)
        {
            Debug.Log($"{name}: Upgrade unavailable or already in progress.", this);
            return false;
        }

        if (coinsManager == null)
        {
            Debug.LogError($"{name}: PlayerCoinsManager not found!", this);
            return false;
        }

        int targetLevel = currentLevel + 1;
        int cost = NextLevelCost;
        int xp = NextLevelXP;
        float duration = Mathf.Max(0f, NextLevelUpgradeDuration);

        if (!coinsManager.TrySpendCoins(cost))
        {
            Debug.Log(
                $"{name}: Not enough coins to upgrade " +
                $"({coinsManager.CurrentCoins}/{cost}).",
                this
            );
            return false;
        }

        isUpgrading = true;
        upgradeTotalDuration = duration;
        upgradeRemainingTime = duration;
        pendingUpgradeXP = xp;

        Debug.Log(
            $"{name}: Started upgrade from level {currentLevel} " +
            $"to level {targetLevel}. Cost: {cost} Coins. " +
            $"Duration: {duration:0.0}s. " +
            $"Initial skip cost: {SkipUpgradeGemCost} Gems.",
            this
        );

        OnUpgradeStarted?.Invoke(this);
        OnUpgradeTimerChanged?.Invoke(this);

        if (upgradeRemainingTime <= 0f)
            CompleteUpgrade();

        return true;
    }

    public bool TrySkipUpgradeWithGems(PlayerGemsManager gemsManager)
    {
        if (!isUpgrading || !isActiveAndEnabled)
        {
            Debug.LogWarning($"{name}: Cannot skip - no active upgrade.", this);
            return false;
        }

        if (gemsManager == null)
        {
            Debug.LogError($"{name}: Cannot skip - PlayerGemsManager missing.", this);
            return false;
        }

        // Recalculate the price instead of using a price displayed earlier.
        int gemCost = SkipUpgradeGemCost;

        if (gemCost <= 0)
        {
            Debug.LogWarning(
                $"{name}: Cannot skip - upgrade is already completing.",
                this
            );
            return false;
        }

        if (!gemsManager.HasEnoughGems(gemCost))
        {
            Debug.LogWarning(
                $"{name}: Not enough Gems. Available: {gemsManager.CurrentGems}, " +
                $"required: {gemCost}.",
                this
            );
            return false;
        }

        if (!gemsManager.TrySpendGems(gemCost))
            return false;

        Debug.Log(
            $"{name}: Skipping the remaining {upgradeRemainingTime:0.0}s " +
            $"for {gemCost} Gems.",
            this
        );

        upgradeRemainingTime = 0f;
        OnUpgradeTimerChanged?.Invoke(this);

        CompleteUpgrade();
        return true;
    }

    private void CompleteUpgrade()
    {
        if (!isUpgrading)
            return;

        int completedXP = pendingUpgradeXP;

        isUpgrading = false;
        upgradeRemainingTime = 0f;
        upgradeTotalDuration = 0f;
        pendingUpgradeXP = 0;

        currentLevel++;
        ApplyVersion();
        ApplyLevelStats();

        if (progressionManager != null)
            progressionManager.AddXP(completedXP);

        Debug.Log(
            $"{name}: Upgrade completed! New level: {currentLevel}. +{completedXP} XP.",
            this
        );

        OnUpgradeTimerChanged?.Invoke(this);
        OnUpgraded?.Invoke(this, currentLevel);
        OnUpgradeCompleted?.Invoke(this);
    }

    private void HandleRepaired(BuildingRepair repair)
    {
        ApplyVersion();
    }

    private void ApplyVersion()
    {
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].version != null)
                levels[i].version.SetActive(i == currentLevel - 1);
        }
    }

    private void ApplyLevelStats()
    {
        if (serviceBuilding == null || levels.Count == 0)
            return;

        UpgradeLevel level = levels[currentLevel - 1];

        serviceBuilding.ApplyUpgradeStats(
            level.capacity,
            level.qualityStars,
            level.serviceTime
        );

        Debug.Log(
            $"{name}: Level {currentLevel}, " +
            $"Capacity: {level.capacity}, " +
            $"Quality: {level.qualityStars}, " +
            $"Service time: {level.serviceTime}s",
            this
        );
    }

    [ContextMenu("Test / Upgrade")]
    private void TestUpgrade()
    {
        TryUpgrade();
    }

    [ContextMenu("Test / Show Upgrade Gem Cost")]
    private void TestShowUpgradeGemCost()
    {
        if (!isUpgrading)
        {
            Debug.Log($"{name}: No upgrade is currently running.", this);
            return;
        }

        Debug.Log(
            $"{name}: Remaining upgrade time: {UpgradeRemainingTime:0.0}s. " +
            $"Skip cost: {SkipUpgradeGemCost} Gems.",
            this
        );
    }
}