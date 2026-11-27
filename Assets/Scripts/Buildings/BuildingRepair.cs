using System;
using UnityEngine;

public class BuildingRepair : MonoBehaviour
{
    [Header("Versions")]

    [Tooltip("Visible while the building is ruined or being repaired.")]
    [SerializeField] private GameObject ruinsVersion;

    [Tooltip("Visible after the repair is completed.")]
    [SerializeField] private GameObject repairedVersion;

    [Tooltip("Hidden while ruined or being repaired.")]
    [SerializeField] private GameObject hideWhileRuined;

    [Header("Repair")]

    [SerializeField, Min(0)] private int repairCost = 100;
    [SerializeField, Min(0)] private int xpReward = 100;

    [Tooltip("Tick for buildings that start already repaired.")]
    [SerializeField] private bool isRepaired;

    [Header("Repair Timer")]

    [Tooltip("Time required to repair this building in seconds.")]
    [SerializeField, Min(0f)] private float repairDuration = 30f;

    [Header("Repair Timer State (Runtime)")]

    [SerializeField] private bool isRepairing;
    [SerializeField, Min(0f)] private float repairRemainingTime;
    [SerializeField, Min(0f)] private float repairTotalDuration;

    [Header("Gem Skip Settings")]

    [Tooltip("How many seconds of repair time one Gem covers.")]
    [SerializeField, Min(0.1f)] private float secondsPerGem = 10f;

    private PlayerCoinsManager coinsManager;
    private ResortProgressionManager progressionManager;
    private BuildingLevelRequirements levelRequirements;
    private ResortUnlockManager unlockManager;

    public int RepairCost => repairCost;
    public int XPReward => xpReward;
    public bool IsRepaired => isRepaired;
    public bool IsRepairing => isRepairing;
    public float RepairDuration => repairDuration;

    public float RepairRemainingTime =>
        isRepairing ? Mathf.Max(0f, repairRemainingTime) : 0f;

    public float RepairTotalDuration =>
        isRepairing ? repairTotalDuration : 0f;

    public float RepairProgress
    {
        get
        {
            if (!isRepairing)
                return 0f;

            if (repairTotalDuration <= 0f)
                return 1f;

            return Mathf.Clamp01(
                1f - repairRemainingTime / repairTotalDuration
            );
        }
    }

    public float SecondsPerGem => secondsPerGem;

    public int SkipRepairGemCost =>
        isRepairing ? CalculateGemCost(RepairRemainingTime) : 0;

    public int MinimumRequiredResortLevel =>
        levelRequirements != null
            ? levelRequirements.MinimumRepairResortLevel
            : 1;

    public bool MeetsResortLevelRequirement =>
        MinimumRequiredResortLevel <= 1 ||
        (progressionManager != null &&
         progressionManager.CurrentLevel >= MinimumRequiredResortLevel);

    // Completed waves required before the repair unlocks.
    public int MinimumRequiredCompletedWaves =>
        levelRequirements != null
            ? levelRequirements.MinimumRepairCompletedWaves
            : 0;

    public int CompletedWaves
    {
        get
        {
            if (unlockManager == null)
                unlockManager = FindFirstObjectByType<ResortUnlockManager>();

            return unlockManager != null ? unlockManager.LastCompletedWave : 0;
        }
    }

    public bool MeetsWaveRequirement =>
        MinimumRequiredCompletedWaves <= 0 ||
        CompletedWaves >= MinimumRequiredCompletedWaves;

    public bool CanRepair =>
        !isRepaired &&
        !isRepairing &&
        isActiveAndEnabled &&
        MeetsResortLevelRequirement &&
        MeetsWaveRequirement;

    public bool CanAfford =>
        CanRepair &&
        coinsManager != null &&
        coinsManager.CurrentCoins >= repairCost;

    public string BuildingName =>
        repairedVersion != null ? repairedVersion.name : name;

    public WorkerAssignment RepairedWorkerAssignment
    {
        get
        {
            WorkerAssignment assignment =
                repairedVersion != null
                    ? repairedVersion.GetComponentInChildren<WorkerAssignment>(true)
                    : null;

            return assignment != null
                ? assignment
                : GetComponentInParent<WorkerAssignment>();
        }
    }

    public event Action<BuildingRepair> OnRepaired;
    public event Action<BuildingRepair> OnRepairStarted;
    public event Action<BuildingRepair> OnRepairTimerChanged;
    public event Action<BuildingRepair> OnRepairCompleted;

    private void Awake()
    {
        coinsManager = FindFirstObjectByType<PlayerCoinsManager>();
        progressionManager = FindFirstObjectByType<ResortProgressionManager>();
        levelRequirements = GetComponent<BuildingLevelRequirements>();
        unlockManager = FindFirstObjectByType<ResortUnlockManager>();

        // Timed repairs are runtime-only.
        isRepairing = false;
        repairRemainingTime = 0f;
        repairTotalDuration = 0f;

        repairDuration = Mathf.Max(0f, repairDuration);
        secondsPerGem = Mathf.Max(0.1f, secondsPerGem);

        ApplyVersion();
    }

    private void Update()
    {
        if (!isRepairing)
            return;

        repairRemainingTime = Mathf.Max(
            0f,
            repairRemainingTime - Time.deltaTime
        );

        OnRepairTimerChanged?.Invoke(this);

        if (repairRemainingTime <= 0f)
            CompleteRepair();
    }

    private void OnValidate()
    {
        repairCost = Mathf.Max(0, repairCost);
        xpReward = Mathf.Max(0, xpReward);

        repairDuration = Mathf.Max(0f, repairDuration);
        secondsPerGem = Mathf.Max(0.1f, secondsPerGem);
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

    public bool TryRepair()
    {
        if (!MeetsResortLevelRequirement)
        {
            Debug.LogWarning(
                $"{name}: Repair requires Resort Level {MinimumRequiredResortLevel}.",
                this
            );
            return false;
        }

        if (!MeetsWaveRequirement)
        {
            Debug.LogWarning(
                $"{name}: Repair unlocks after wave {MinimumRequiredCompletedWaves} " +
                $"({CompletedWaves}/{MinimumRequiredCompletedWaves} completed).",
                this
            );
            return false;
        }

        if (!CanRepair)
        {
            Debug.LogWarning(
                $"{name}: Repair unavailable, " +
                "already in progress, or already completed.",
                this
            );
            return false;
        }

        if (coinsManager == null)
            coinsManager = FindFirstObjectByType<PlayerCoinsManager>();

        if (coinsManager == null)
        {
            Debug.LogError($"{name}: PlayerCoinsManager not found!", this);
            return false;
        }

        if (!coinsManager.TrySpendCoins(repairCost))
        {
            Debug.LogWarning(
                $"{name}: Not enough Coins for repair. " +
                $"Available: {coinsManager.CurrentCoins}, " +
                $"Required: {repairCost}.",
                this
            );
            return false;
        }

        isRepairing = true;
        repairTotalDuration = Mathf.Max(0f, repairDuration);
        repairRemainingTime = repairTotalDuration;

        // Keep the ruined model visible during repair.
        ApplyVersion();

        Debug.Log(
            $"{BuildingName}: Repair started! " +
            $"Cost: {repairCost} Coins. " +
            $"Duration: {repairTotalDuration:0.0}s. " +
            $"Initial skip cost: {SkipRepairGemCost} Gems.",
            this
        );

        OnRepairStarted?.Invoke(this);
        OnRepairTimerChanged?.Invoke(this);

        if (repairRemainingTime <= 0f)
            CompleteRepair();

        return true;
    }

    public bool TrySkipRepairWithGems(PlayerGemsManager gemsManager)
    {
        if (!isRepairing || isRepaired || !isActiveAndEnabled)
        {
            Debug.LogWarning(
                $"{name}: Cannot skip repair. No active repair is available.",
                this
            );
            return false;
        }

        if (gemsManager == null)
        {
            Debug.LogError($"{name}: PlayerGemsManager not found!", this);
            return false;
        }

        // The cost decreases as repair time runs out.
        int gemCost = SkipRepairGemCost;

        if (gemCost <= 0)
        {
            Debug.LogWarning($"{name}: Repair is already completing.", this);
            return false;
        }

        if (!gemsManager.HasEnoughGems(gemCost))
        {
            Debug.LogWarning(
                $"{name}: Not enough Gems to skip repair. " +
                $"Available: {gemsManager.CurrentGems}, Required: {gemCost}.",
                this
            );
            return false;
        }

        if (!gemsManager.TrySpendGems(gemCost))
            return false;

        Debug.Log(
            $"{BuildingName}: Skipping remaining " +
            $"{repairRemainingTime:0.0}s for {gemCost} Gems.",
            this
        );

        repairRemainingTime = 0f;
        OnRepairTimerChanged?.Invoke(this);

        // Payment and completion happen in the same call.
        CompleteRepair();
        return true;
    }

    private void CompleteRepair()
    {
        if (!isRepairing || isRepaired)
            return;

        isRepairing = false;
        repairRemainingTime = 0f;
        repairTotalDuration = 0f;
        isRepaired = true;

        ApplyVersion();

        if (progressionManager != null)
            progressionManager.AddXP(xpReward);

        Debug.Log($"{BuildingName}: Repair completed! +{xpReward} XP.", this);

        OnRepairTimerChanged?.Invoke(this);
        OnRepaired?.Invoke(this);
        OnRepairCompleted?.Invoke(this);
    }

    private void ApplyVersion()
    {
        if (ruinsVersion != null)
            ruinsVersion.SetActive(!isRepaired);

        if (repairedVersion != null)
            repairedVersion.SetActive(isRepaired);

        if (hideWhileRuined != null)
            hideWhileRuined.SetActive(isRepaired);
    }

    [ContextMenu("Test / Repair")]
    private void TestRepair()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Repair can only be tested in Play Mode.", this);
            return;
        }

        TryRepair();
    }

    [ContextMenu("Test / Show Repair Gem Cost")]
    private void TestShowRepairGemCost()
    {
        if (!Application.isPlaying || !isRepairing)
        {
            Debug.Log($"{name}: No repair is currently running.", this);
            return;
        }

        Debug.Log(
            $"{BuildingName}: Remaining repair time: {RepairRemainingTime:0.0}s. " +
            $"Skip cost: {SkipRepairGemCost} Gems.",
            this
        );
    }
}