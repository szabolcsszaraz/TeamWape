
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingWorkersUI : MonoBehaviour
{
    [Header("Building")]
    [SerializeField] private WorkerAssignment workerAssignment;

    private ServiceBuilding serviceBuilding;

    [Header("UI References")]
    [SerializeField] private TMP_Text buildingNameText;
    [SerializeField] private TMP_Text workerCountText;
    [SerializeField] private Transform workersList;

    [Header("Service Status UI")]
    [SerializeField] private TMP_Text serviceStatusText;
    [SerializeField] private TMP_Text serviceSpeedText;

    [Header("Prefab")]
    [SerializeField] private GameObject workerListItemPrefab;

    [Header("Repair / Upgrade UI")]
    [Tooltip("Shown for ruined buildings (Repair) and upgradable " +
             "buildings (Upgrade). Holds the action button.")]
    [SerializeField] private GameObject repairSection;
    [SerializeField] private Button repairButton;
    [SerializeField] private TMP_Text repairButtonText;

    [Header("Building Upgrade Statistics")]
    [SerializeField] private TMP_Text capacityText;
    [SerializeField] private TMP_Text qualityText;

    // SCRUM-28: Energy Boost UI references.
    [Header("Energy Boost UI")]
    [SerializeField] private GameObject energyBoostSection;
    [SerializeField] private Button energyBoostButton;
    [SerializeField] private TMP_Text energyBoostButtonText;

    // Set while the panel shows a ruined building.
    private BuildingRepair ruinedBuilding;

    // Upgrade component of the shown building, if any.
    private BuildingUpgrade buildingUpgrade;
    private PlayerCoinsManager coinsManager;

    private WorkerData selectedWorker;

    private readonly List<WorkerListItemUI> displayedItems = new();

    public WorkerData SelectedWorker => selectedWorker;

    public event Action<WorkerData> OnWorkerSelected;

    private void Awake()
    {
        coinsManager = FindFirstObjectByType<PlayerCoinsManager>();

        if (repairButton != null)
        {
            repairButton.onClick.AddListener(HandleRepairClicked);
        }

        // SCRUM-28: Register the Energy Boost button.
        if (energyBoostButton != null)
        {
            energyBoostButton.onClick.AddListener(
                HandleEnergyBoostClicked
            );
        }
    }

    private void OnEnable()
    {
        SubscribeToBuildingEvents();
        RefreshUI();

        if (coinsManager != null)
        {
            coinsManager.OnCoinsChanged += HandleCoinsChanged;
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromBuildingEvents();

        if (coinsManager != null)
        {
            coinsManager.OnCoinsChanged -= HandleCoinsChanged;
        }
    }

    private void OnDestroy()
    {
        if (repairButton != null)
        {
            repairButton.onClick.RemoveListener(
                HandleRepairClicked
            );
        }

        if (energyBoostButton != null)
        {
            energyBoostButton.onClick.RemoveListener(
                HandleEnergyBoostClicked
            );
        }
    }

    // SCRUM-28: Update remaining boost time while
    // the selected building's panel is visible.
    private void Update()
    {
        if (serviceBuilding == null || ruinedBuilding != null)
            return;

        if (serviceBuilding.IsEnergyBoostActive)
        {
            RefreshEnergyBoostUI();
        }
    }

    // Shows a ruined building: status + Repair button, no workers.
    public void SetRuinedBuilding(BuildingRepair repair)
    {
        SetBuilding(null);

        ruinedBuilding = repair;
        RefreshUI();
    }

    // One button: Repair for ruins, Upgrade for working buildings.
    private void HandleRepairClicked()
    {
        if (ruinedBuilding != null)
        {
            BuildingRepair repaired = ruinedBuilding;

            if (!repaired.TryRepair())
            {
                RefreshUI();
                return;
            }

            // Switch the panel to the now working building.
            SetBuilding(repaired.RepairedWorkerAssignment);
            return;
        }

        if (buildingUpgrade != null)
        {
            buildingUpgrade.TryUpgrade();
            RefreshUI();
        }
    }

    // SCRUM-28: Activate boost only for the selected building.
    private void HandleEnergyBoostClicked()
    {
        if (ruinedBuilding != null || serviceBuilding == null)
            return;

        bool activated =
            serviceBuilding.TryActivateEnergyBoost();

        if (activated)
        {
            Debug.Log(
                $"Energy Boost activated for selected building: " +
                $"{serviceBuilding.gameObject.name}",
                this
            );
        }

        RefreshEnergyBoostUI();
        RefreshServiceInformation();
    }

    // SCRUM-28: React to boost activation and expiration.
    private void HandleEnergyBoostStateChanged(
        ServiceBuilding building,
        bool isActive)
    {
        if (building != serviceBuilding)
            return;

        RefreshEnergyBoostUI();
        RefreshServiceInformation();
    }

    private void HandleCoinsChanged(int coins)
    {
        RefreshRepairSection();
    }

    private void RefreshRepairSection()
    {
        bool showRepair = ruinedBuilding != null;

        bool showUpgrade = !showRepair &&
                           buildingUpgrade != null &&
                           buildingUpgrade.CanUpgrade;

        if (repairSection != null)
        {
            repairSection.SetActive(showRepair || showUpgrade);
        }

        if (showRepair)
        {
            if (repairButton != null)
            {
                repairButton.interactable =
                    ruinedBuilding.CanAfford;
            }

            if (repairButtonText != null)
            {
                repairButtonText.text =
                    $"Repair ({ruinedBuilding.RepairCost}$)";
            }
        }
        else if (showUpgrade)
        {
            if (repairButton != null)
            {
                repairButton.interactable =
                    buildingUpgrade.CanAfford;
            }

            if (repairButtonText != null)
            {
                int currentCapacity = serviceBuilding != null
                    ? serviceBuilding.Seats
                    : 0;

                int nextCapacity =
                    buildingUpgrade.NextLevelCapacity;

                repairButtonText.text =
                    $"Upgrade Lv. {buildingUpgrade.CurrentLevel + 1}\n" +
                    $"Capacity: {currentCapacity} → {nextCapacity}\n" +
                    $"{buildingUpgrade.NextLevelCost} Coins";
            }
        }
    }

    // SCRUM-28: Update button visibility, availability
    // and remaining boost time.
    private void RefreshEnergyBoostUI()
    {
        bool hasValidBuilding =
            ruinedBuilding == null &&
            workerAssignment != null &&
            serviceBuilding != null;

        if (energyBoostSection != null)
        {
            energyBoostSection.SetActive(hasValidBuilding);
        }

        if (!hasValidBuilding)
        {
            if (energyBoostButton != null)
            {
                energyBoostButton.interactable = false;
            }

            if (energyBoostButtonText != null)
            {
                energyBoostButtonText.text =
                    "Energy Boost Unavailable";
            }

            return;
        }

        bool isBoosted =
            serviceBuilding.IsEnergyBoostActive;

        bool canActivate =
            serviceBuilding.isActiveAndEnabled &&
            serviceBuilding.CanOperate &&
            !isBoosted;

        if (energyBoostButton != null)
        {
            energyBoostButton.interactable = canActivate;
        }

        if (energyBoostButtonText == null)
            return;

        if (isBoosted)
        {
            float remaining =
                Mathf.Max(
                    0f,
                    serviceBuilding.EnergyBoostRemainingTime
                );

            energyBoostButtonText.text =
                $"Energy Boost Active ({remaining:0.0}s)";
        }
        else if (!serviceBuilding.CanOperate)
        {
            energyBoostButtonText.text =
                "Energy Boost Unavailable";
        }
        else
        {
            energyBoostButtonText.text =
                $"Activate Energy Boost " +
                $"(x{serviceBuilding.EnergyBoostMultiplier:0.00}, " +
                $"{serviceBuilding.EnergyBoostDuration:0}s)";
        }
    }

    private void SubscribeToBuildingEvents()
    {
        if (workerAssignment != null)
        {
            workerAssignment.OnWorkersChanged -=
                HandleWorkersChanged;

            workerAssignment.OnWorkersChanged +=
                HandleWorkersChanged;
        }

        if (serviceBuilding != null)
        {
            serviceBuilding.OnServiceStateChanged -=
                HandleServiceStateChanged;

            serviceBuilding.OnServiceStateChanged +=
                HandleServiceStateChanged;

            serviceBuilding.OnPerformanceChanged -=
                HandlePerformanceChanged;

            serviceBuilding.OnPerformanceChanged +=
                HandlePerformanceChanged;

            // SCRUM-28: Subscribe to Energy Boost changes.
            serviceBuilding.OnEnergyBoostStateChanged -=
                HandleEnergyBoostStateChanged;

            serviceBuilding.OnEnergyBoostStateChanged +=
                HandleEnergyBoostStateChanged;
        }
    }

    private void UnsubscribeFromBuildingEvents()
    {
        if (workerAssignment != null)
        {
            workerAssignment.OnWorkersChanged -=
                HandleWorkersChanged;
        }

        if (serviceBuilding != null)
        {
            serviceBuilding.OnServiceStateChanged -=
                HandleServiceStateChanged;

            serviceBuilding.OnPerformanceChanged -=
                HandlePerformanceChanged;

            // SCRUM-28: Unsubscribe from the old building.
            serviceBuilding.OnEnergyBoostStateChanged -=
                HandleEnergyBoostStateChanged;
        }
    }

    private void HandleWorkersChanged(
        WorkerAssignment assignment)
    {
        if (assignment != workerAssignment)
            return;

        if (selectedWorker != null &&
            !workerAssignment.ContainsWorker(selectedWorker))
        {
            selectedWorker = null;
        }

        RefreshUI();
    }

    private void HandleServiceStateChanged(
        ServiceBuilding building,
        ServiceLoadState newState)
    {
        if (building != serviceBuilding)
            return;

        RefreshServiceInformation();
        RefreshEnergyBoostUI();
    }

    private void HandlePerformanceChanged(
        ServiceBuilding building,
        float newMultiplier)
    {
        if (building != serviceBuilding)
            return;

        RefreshServiceInformation();
        RefreshEnergyBoostUI();
    }

    public void SetBuilding(WorkerAssignment newAssignment)
    {
        if (isActiveAndEnabled)
        {
            UnsubscribeFromBuildingEvents();
        }

        if (workerAssignment != newAssignment)
        {
            selectedWorker = null;
        }

        ruinedBuilding = null;
        workerAssignment = newAssignment;

        buildingUpgrade = workerAssignment != null
            ? workerAssignment.GetComponentInParent<BuildingUpgrade>()
            : null;

        serviceBuilding = workerAssignment != null
            ? workerAssignment.GetComponent<ServiceBuilding>()
            : null;

        if (isActiveAndEnabled)
        {
            SubscribeToBuildingEvents();
        }

        RefreshUI();
    }

    private void HandleMoveClicked(WorkerData worker)
    {
        if (worker == null)
            return;

        selectedWorker = worker;

        UpdateSelectionVisuals();

        Debug.Log(
            $"Worker selected for transfer: {worker.WorkerName}",
            this
        );

        OnWorkerSelected?.Invoke(worker);
    }

    private void UpdateSelectionVisuals()
    {
        foreach (WorkerListItemUI item in displayedItems)
        {
            if (item == null)
                continue;

            item.SetSelected(item.Worker == selectedWorker);
        }
    }

    private void RefreshUI()
    {
        displayedItems.Clear();

        if (workersList != null)
        {
            for (int i = workersList.childCount - 1; i >= 0; i--)
            {
                GameObject oldItem =
                    workersList.GetChild(i).gameObject;

                oldItem.SetActive(false);
                Destroy(oldItem);
            }
        }

        RefreshRepairSection();

        if (ruinedBuilding != null)
        {
            if (buildingNameText != null)
            {
                buildingNameText.text =
                    ruinedBuilding.BuildingName;
            }

            if (workerCountText != null)
            {
                workerCountText.text = "-";
            }

            if (serviceStatusText != null)
            {
                serviceStatusText.text = "Status: Ruined";

                serviceStatusText.color =
                    new Color(0.55f, 0.58f, 0.61f);
            }

            if (serviceSpeedText != null)
            {
                serviceSpeedText.text = "Repair to open";
            }

            RefreshEnergyBoostUI();
            return;
        }

        if (workerAssignment == null)
        {
            if (buildingNameText != null)
            {
                buildingNameText.text =
                    "No Building Selected";
            }

            if (workerCountText != null)
            {
                workerCountText.text = "-";
            }

            RefreshServiceInformation();
            RefreshEnergyBoostUI();
            return;
        }

        if (buildingNameText != null)
        {
            string levelText = buildingUpgrade != null
                ? $" (Lv {buildingUpgrade.CurrentLevel})"
                : "";

            buildingNameText.text =
                $"{workerAssignment.gameObject.name}" +
                $"{levelText} Workers";
        }

        if (workerCountText != null)
        {
            workerCountText.text =
                $"{workerAssignment.AssignedWorkerCount}/" +
                $"{workerAssignment.MaxWorkerSlots}";
        }

        RefreshServiceInformation();
        RefreshEnergyBoostUI();

        if (workersList == null || workerListItemPrefab == null)
            return;

        foreach (WorkerData worker in
                 workerAssignment.AssignedWorkers)
        {
            if (worker == null)
                continue;

            GameObject itemObject = Instantiate(
                workerListItemPrefab,
                workersList
            );

            WorkerListItemUI item =
                itemObject.GetComponent<WorkerListItemUI>();

            if (item == null)
            {
                Debug.LogError(
                    "WorkerListItem prefab is missing " +
                    "WorkerListItemUI!",
                    itemObject
                );

                Destroy(itemObject);
                continue;
            }

            item.Setup(worker, HandleMoveClicked);
            displayedItems.Add(item);
        }

        UpdateSelectionVisuals();
    }

    // SCRUM-24: Update operational state and speed.
    private void RefreshServiceInformation()
    {
        if (capacityText != null)
        {
            capacityText.text = serviceBuilding != null
                ? $"Capacity: {serviceBuilding.Seats}"
                : "Capacity: N/A";
        }

        if (qualityText != null)
        {
            qualityText.text = serviceBuilding != null
                ? $"Quality: {serviceBuilding.QualityStars}/5"
                : "Quality: N/A";
        }

        if (serviceBuilding == null)
        {
            if (serviceStatusText != null)
            {
                serviceStatusText.text = "Status: N/A";
            }

            if (serviceSpeedText != null)
            {
                serviceSpeedText.text = "Speed: N/A";
            }

            return;
        }

        if (serviceStatusText != null)
        {
            serviceStatusText.text =
                $"Status: {serviceBuilding.CurrentState}";

            switch (serviceBuilding.CurrentState)
            {
                case ServiceLoadState.Normal:
                    serviceStatusText.color = new Color(
                        0.27f, 0.73f, 0.46f
                    );
                    break;

                case ServiceLoadState.Overloaded:
                    serviceStatusText.color = new Color(
                        0.91f, 0.35f, 0.33f
                    );
                    break;

                case ServiceLoadState.Inactive:
                    serviceStatusText.color = new Color(
                        0.55f, 0.58f, 0.61f
                    );
                    break;
            }
        }

        if (serviceSpeedText != null)
        {
            if (!serviceBuilding.CanOperate)
            {
                serviceSpeedText.text =
                    "Speed: 0.00x";
            }
            else
            {
                serviceSpeedText.text =
                    $"Speed: " +
                    $"{serviceBuilding.BuildingPerformanceMultiplier:0.00}x";
            }
        }
    }
}
