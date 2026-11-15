
using System;
using UnityEngine;

// Goes on the building's parent object (e.g. "Pool"). Shows the ruins
// until the player pays to repair it, then swaps to the working
// version. The working version holds the ServiceBuilding, so the
// service (and its need) only starts once the building is repaired.
public class BuildingRepair : MonoBehaviour
{
    [Header("Versions")]
    [Tooltip("Visible while the building is ruined.")]
    [SerializeField] private GameObject ruinsVersion;

    [Tooltip("Visible after the repair (holds the ServiceBuilding).")]
    [SerializeField] private GameObject repairedVersion;

    [Tooltip("Hidden while ruined, e.g. the building's ServiceStateCanvas.")]
    [SerializeField] private GameObject hideWhileRuined;

    [Header("Repair")]
    [SerializeField, Min(0)] private int repairCost = 100;
    [SerializeField, Min(0)] private int xpReward = 100;

    [Tooltip("Tick for buildings that start already repaired.")]
    [SerializeField] private bool isRepaired;

    private PlayerCoinsManager coinsManager;
    private ResortProgressionManager progressionManager;

    public int RepairCost => repairCost;
    public int XPReward => xpReward;
    public bool IsRepaired => isRepaired;

    public string BuildingName => repairedVersion != null
        ? repairedVersion.name
        : name;

    // Worker list of the working version (for the building panel).
    // Either on the working model itself (Pool) or on the parent (Hotel).
    public WorkerAssignment RepairedWorkerAssignment
    {
        get
        {
            WorkerAssignment assignment = repairedVersion != null
                ? repairedVersion.GetComponentInChildren<WorkerAssignment>(true)
                : null;

            return assignment != null
                ? assignment
                : GetComponentInParent<WorkerAssignment>();
        }
    }

    public bool CanAfford =>
        coinsManager != null && coinsManager.CurrentCoins >= repairCost;

    public event Action<BuildingRepair> OnRepaired;

    private void Awake()
    {
        coinsManager = FindFirstObjectByType<PlayerCoinsManager>();
        progressionManager = FindFirstObjectByType<ResortProgressionManager>();

        ApplyVersion();
    }

    public bool TryRepair()
    {
        if (isRepaired)
        {
            return false;
        }

        if (coinsManager == null)
        {
            Debug.LogError($"{name}: PlayerCoinsManager not found!", this);
            return false;
        }

        if (!coinsManager.TrySpendCoins(repairCost))
        {
            Debug.Log(
                $"{name}: Not enough coins to repair " +
                $"({coinsManager.CurrentCoins}/{repairCost}).",
                this
            );
            return false;
        }

        isRepaired = true;
        ApplyVersion();

        if (progressionManager != null)
        {
            progressionManager.AddXP(xpReward);
        }

        Debug.Log(
            $"{BuildingName} repaired for {repairCost} coins, " +
            $"+{xpReward} XP.",
            this
        );

        OnRepaired?.Invoke(this);
        return true;
    }

    private void ApplyVersion()
    {
        if (ruinsVersion != null)
        {
            ruinsVersion.SetActive(!isRepaired);
        }

        if (repairedVersion != null)
        {
            repairedVersion.SetActive(isRepaired);
        }

        if (hideWhileRuined != null)
        {
            hideWhileRuined.SetActive(isRepaired);
        }
    }

    [ContextMenu("Test / Repair")]
    private void TestRepair()
    {
        TryRepair();
    }
}
