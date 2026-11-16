
using System;
using System.Collections.Generic;
using UnityEngine;

// Goes on the building's parent object (e.g. "Hotel", which holds the
// ServiceBuilding and WorkerAssignment). Each level is a model stacked
// on the same spot (hotel1, Hotel2, ...); only the current one is
// visible. Workers and service stay on the parent, so they are kept.
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

    [Range(1, 5)]
    public int qualityStars = 2;

    [Min(0.1f)]
    public float serviceTime = 12f;
}

    [Header("Levels (first = starting level)")]
    [SerializeField] private List<UpgradeLevel> levels = new();

    [Header("State")]
    [SerializeField, Min(1)] private int currentLevel = 1;

    private PlayerCoinsManager coinsManager;
    private ResortProgressionManager progressionManager;
    private BuildingRepair buildingRepair;
    private ServiceBuilding serviceBuilding;

    public int CurrentLevel => currentLevel;
    public int MaxLevel => levels.Count;

    // Ruined buildings must be repaired before upgrading.
    public bool CanUpgrade =>
        currentLevel < MaxLevel &&
        (buildingRepair == null || buildingRepair.IsRepaired);

    public int NextLevelCost => CanUpgrade ? levels[currentLevel].cost : 0;
    public int NextLevelXP => CanUpgrade ? levels[currentLevel].xpReward : 0;
    public int NextLevelCapacity =>
        CanUpgrade ? levels[currentLevel].capacity : 0;

    public bool CanAfford =>
        CanUpgrade &&
        coinsManager != null &&
        coinsManager.CurrentCoins >= NextLevelCost;

    public event Action<BuildingUpgrade, int> OnUpgraded;

    private void Awake()
    {
        coinsManager = FindFirstObjectByType<PlayerCoinsManager>();
        progressionManager = FindFirstObjectByType<ResortProgressionManager>();
        buildingRepair = GetComponent<BuildingRepair>();
        serviceBuilding = GetComponent<ServiceBuilding>();

        if (serviceBuilding == null)
        {
            serviceBuilding =
                GetComponentInChildren<ServiceBuilding>(true);
        }

        currentLevel = Mathf.Clamp(currentLevel, 1, Mathf.Max(1, MaxLevel));
    }

    private void Start()
    {
        // After BuildingRepair.Awake, so the right model wins.
        if (buildingRepair == null || buildingRepair.IsRepaired)
        {
            ApplyVersion();
            ApplyLevelStats();
        }

        if (buildingRepair != null)
        {
            buildingRepair.OnRepaired += HandleRepaired;
        }
    }

    private void OnDestroy()
    {
        if (buildingRepair != null)
        {
            buildingRepair.OnRepaired -= HandleRepaired;
        }
    }

    public bool TryUpgrade()
    {
        if (!CanUpgrade)
        {
            return false;
        }

        if (coinsManager == null)
        {
            Debug.LogError($"{name}: PlayerCoinsManager not found!", this);
            return false;
        }

        int cost = NextLevelCost;
        int xp = NextLevelXP;

        if (!coinsManager.TrySpendCoins(cost))
        {
            Debug.Log(
                $"{name}: Not enough coins to upgrade " +
                $"({coinsManager.CurrentCoins}/{cost}).",
                this
            );
            return false;
        }

        currentLevel++;
        ApplyVersion();
        ApplyLevelStats();

        if (progressionManager != null)
        {
            progressionManager.AddXP(xp);
        }

        Debug.Log(
            $"{name} upgraded to level {currentLevel} for {cost} coins, " +
            $"+{xp} XP.",
            this
        );

        OnUpgraded?.Invoke(this, currentLevel);
        return true;
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
            {
                levels[i].version.SetActive(i == currentLevel - 1);
            }
        }
    }

    [ContextMenu("Test / Upgrade")]
    private void TestUpgrade()
    {
        TryUpgrade();
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
        $"Service time: {level.serviceTime}s"
    );
}
}
