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
    [SerializeField] private GameObject repairSection;
    [SerializeField] private Button repairButton;
    [SerializeField] private TMP_Text repairButtonText;
    [SerializeField] private ResortUnlockManager unlockManager;

    [Header("Building Upgrade Statistics")]
    [SerializeField] private TMP_Text capacityText;
    [SerializeField] private TMP_Text qualityText;

    [Header("Building Upgrade Timer UI")]
    [SerializeField] private GameObject upgradeTimerSection;
    [SerializeField] private TMP_Text upgradeTimerTitle;
    [SerializeField] private TMP_Text upgradeTimerText;
    [SerializeField] private Image upgradeProgressFill;

    [Header("Skip Upgrade UI")]
    [SerializeField] private Button skipUpgradeButton;
    [SerializeField] private TMP_Text skipUpgradeButtonText;

    // SCRUM-21: Confirmation dialog references.
    [Header("Skip Upgrade Confirmation UI")]
    [SerializeField] private GameObject skipUpgradeConfirmPanel;
    [SerializeField] private TMP_Text confirmTitleText;
    [SerializeField] private TMP_Text confirmDescriptionText;
    [SerializeField] private TMP_Text confirmCostText;
    [SerializeField] private TMP_Text confirmBalanceText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;

    [Header("Energy Boost UI")]
    [SerializeField] private GameObject energyBoostSection;
    [SerializeField] private Button energyBoostButton;
    [SerializeField] private TMP_Text energyBoostButtonText;

    [Header("Energy Boost Timer UI")]
    [SerializeField] private GameObject energyBoostTimerPanel;
    [SerializeField] private TMP_Text energyBoostTimerText;
    [SerializeField] private Image energyBoostTimerFill;

    [Header("Energy Boost Timer Colors")]
    [SerializeField] private Color timerHighColor =
        new Color(0.20f, 0.75f, 0.40f);

    [SerializeField] private Color timerMediumColor =
        new Color(0.90f, 0.72f, 0.18f);

    [SerializeField] private Color timerLowColor =
        new Color(0.90f, 0.30f, 0.30f);

    [Header("Energy Boost Timer Color Thresholds")]
    [SerializeField, Range(0f, 1f)]
    private float lowTimeThreshold = 0.33f;

    [SerializeField, Range(0f, 1f)]
    private float highTimeThreshold = 0.66f;

    private BuildingRepair ruinedBuilding;
    private BuildingUpgrade buildingUpgrade;
    private PlayerCoinsManager coinsManager;
    private PlayerGemsManager gemsManager;
    private DayStateManager dayStateManager;

    // The building being confirmed is tracked separately
    // so switching to another building cannot spend Gems
    // on the wrong upgrade.
    private BuildingUpgrade pendingSkipUpgrade;

    // Timed Repair uses the same timer and confirmation dialog.
    private BuildingRepair pendingSkipRepair;

    // Energy Boost uses the same confirmation dialog as Skip Upgrade.
    private ServiceBuilding pendingEnergyBoost;

    private WorkerData selectedWorker;

    private readonly List<WorkerListItemUI> displayedItems = new();

    public WorkerData SelectedWorker => selectedWorker;

    public event Action<WorkerData> OnWorkerSelected;
    public event Action<WorkerData> OnWorkerDetailsRequested;

    private void Awake()
    {
        coinsManager =
            FindFirstObjectByType<PlayerCoinsManager>();

        gemsManager =
            FindFirstObjectByType<PlayerGemsManager>();

        dayStateManager = DayStateManager.Instance;

        if (repairButton != null)
        {
            repairButton.onClick.AddListener(
                HandleRepairClicked
            );
        }

        if (energyBoostButton != null)
        {
            energyBoostButton.onClick.AddListener(
                HandleEnergyBoostClicked
            );
        }

        if (skipUpgradeButton != null)
        {
            skipUpgradeButton.onClick.AddListener(
                HandleSkipUpgradeClicked
            );
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(
                HandleCancelSkipClicked
            );
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(
                HandleConfirmSkipClicked
            );
        }

        // Hide the dialog at startup.
        CloseSkipConfirmation();
    }

    private void OnEnable()
    {
        SubscribeToBuildingEvents();
        SubscribeToUpgradeEvents();
        SubscribeToRepairEvents();
        SubscribeToDayEvents();

        if (unlockManager != null)
        {
            unlockManager.OnUnlockProgressChanged += RefreshRepairSection;
        }

        if (coinsManager == null)
        {
            coinsManager =
                FindFirstObjectByType<PlayerCoinsManager>();
        }

        if (coinsManager != null)
        {
            coinsManager.OnCoinsChanged -= HandleCoinsChanged;
            coinsManager.OnCoinsChanged += HandleCoinsChanged;
        }

        if (gemsManager == null)
        {
            gemsManager =
                FindFirstObjectByType<PlayerGemsManager>();
        }

        if (gemsManager != null)
        {
            gemsManager.OnGemsChanged -= HandleGemsChanged;
            gemsManager.OnGemsChanged += HandleGemsChanged;
        }
        else
        {
            Debug.LogWarning(
                "BuildingWorkersUI: PlayerGemsManager not found. " +
                "Skip Upgrade will remain unavailable.",
                this
            );
        }

        RefreshUI();
    }

    private void OnDisable()
    {
        CloseSkipConfirmation();

        UnsubscribeFromBuildingEvents();
        UnsubscribeFromUpgradeEvents();
        UnsubscribeFromRepairEvents();
        UnsubscribeFromDayEvents();

        if (unlockManager != null)
        {
            unlockManager.OnUnlockProgressChanged -= RefreshRepairSection;
        }

        if (coinsManager != null)
        {
            coinsManager.OnCoinsChanged -= HandleCoinsChanged;
        }

        if (gemsManager != null)
        {
            gemsManager.OnGemsChanged -= HandleGemsChanged;
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

        if (skipUpgradeButton != null)
        {
            skipUpgradeButton.onClick.RemoveListener(
                HandleSkipUpgradeClicked
            );
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(
                HandleCancelSkipClicked
            );
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(
                HandleConfirmSkipClicked
            );
        }
    }

    private void Update()
    {
        if ((buildingUpgrade != null && buildingUpgrade.IsUpgrading) ||
            (ruinedBuilding != null && ruinedBuilding.IsRepairing))
        {
            RefreshUpgradeTimerUI();
        }

        if (pendingSkipUpgrade != null ||
            pendingSkipRepair != null ||
            pendingEnergyBoost != null)
        {
            RefreshSkipConfirmation();
        }

        if (serviceBuilding != null &&
            ruinedBuilding == null &&
            serviceBuilding.IsEnergyBoostActive)
        {
            RefreshEnergyBoostUI();
        }
    }

    private void OnValidate()
    {
        lowTimeThreshold = Mathf.Clamp01(lowTimeThreshold);
        highTimeThreshold = Mathf.Clamp01(highTimeThreshold);

        if (highTimeThreshold < lowTimeThreshold)
        {
            highTimeThreshold = lowTimeThreshold;
        }
    }

    // --------------------------------------------------
    // DAY EVENTS
    // --------------------------------------------------

    private void SubscribeToDayEvents()
    {
        if (dayStateManager == null)
        {
            dayStateManager = DayStateManager.Instance;
        }

        if (dayStateManager == null)
        {
            dayStateManager =
                FindFirstObjectByType<DayStateManager>();
        }

        if (dayStateManager != null)
        {
            dayStateManager.OnDayChanged -= HandleDayChanged;
            dayStateManager.OnDayChanged += HandleDayChanged;
        }
    }

    private void UnsubscribeFromDayEvents()
    {
        if (dayStateManager != null)
        {
            dayStateManager.OnDayChanged -= HandleDayChanged;
        }
    }

    private void HandleDayChanged(int newDay)
    {
        Debug.Log(
            $"BuildingWorkersUI: Day {newDay} started. " +
            "Refreshing Energy Boost availability.",
            this
        );

        RefreshEnergyBoostUI();
    }

    // --------------------------------------------------
    // REPAIR / UPGRADE ACTIONS
    // --------------------------------------------------

    public void SetRuinedBuilding(BuildingRepair repair)
    {
        CloseSkipConfirmation();
        SetBuilding(null);

        ruinedBuilding = repair;

        if (isActiveAndEnabled)
        {
            SubscribeToRepairEvents();
        }

        RefreshUI();
    }

    private void HandleRepairClicked()
    {
        if (ruinedBuilding != null)
        {
            if (ruinedBuilding.IsRepairing)
            {
                RefreshRepairSection();
                return;
            }

            BuildingRepair repair = ruinedBuilding;
            if (!repair.TryRepair())
            {
                RefreshUI();
                return;
            }

            // A successful TryRepair starts the timer, not the model swap.
            // A zero-duration repair can complete in the same call.
            if (repair.IsRepaired)
            {
                HandleRepairCompleted(repair);
            }
            else
            {
                RefreshUI();
            }
            return;
        }

        if (buildingUpgrade != null)
        {
            bool started = buildingUpgrade.TryUpgrade();

            if (started)
            {
                Debug.Log(
                    $"{buildingUpgrade.name}: Timed upgrade started.",
                    this
                );
            }

            RefreshRepairSection();
            RefreshUpgradeTimerUI();
        }
    }

    private void HandleCoinsChanged(int coins)
    {
        RefreshRepairSection();
    }

    private void HandleGemsChanged(int newBalance)
    {
        RefreshUpgradeTimerUI();

        if (pendingSkipUpgrade != null ||
            pendingSkipRepair != null ||
            pendingEnergyBoost != null)
        {
            RefreshSkipConfirmation();
        }
    }

    // --------------------------------------------------
    // SCRUM-21: SKIP UPGRADE CONFIRMATION
    // --------------------------------------------------

    private void HandleSkipUpgradeClicked()
    {
        // The same button skips either a timed Repair or an Upgrade.
        if (ruinedBuilding != null)
        {
            OpenSkipRepairConfirmation();
            return;
        }

        if (buildingUpgrade == null ||
            !buildingUpgrade.IsUpgrading ||
            gemsManager == null)
        {
            return;
        }

        int gemCost = buildingUpgrade.SkipUpgradeGemCost;
        if (gemCost <= 0 ||
            !gemsManager.HasEnoughGems(gemCost))
        {
            RefreshUpgradeTimerUI();
            return;
        }

        CloseSkipConfirmation();
        pendingSkipUpgrade = buildingUpgrade;
        OpenSharedConfirmation();
    }

    private void OpenSkipRepairConfirmation()
    {
        if (ruinedBuilding == null ||
            !ruinedBuilding.IsRepairing ||
            gemsManager == null)
        {
            return;
        }

        int gemCost = ruinedBuilding.SkipRepairGemCost;
        if (gemCost <= 0 ||
            !gemsManager.HasEnoughGems(gemCost))
        {
            RefreshUpgradeTimerUI();
            return;
        }

        CloseSkipConfirmation();
        pendingSkipRepair = ruinedBuilding;
        OpenSharedConfirmation();
    }

    private void OpenSharedConfirmation()
    {
        if (skipUpgradeConfirmPanel == null)
        {
            Debug.LogError(
                "BuildingWorkersUI: Shared confirmation panel is missing!",
                this
            );
            CloseSkipConfirmation();
            return;
        }

        skipUpgradeConfirmPanel.SetActive(true);
        skipUpgradeConfirmPanel.transform.SetAsLastSibling();
        RefreshSkipConfirmation();
    }

    // Reuse the existing confirmation panel for paid Energy Boost.
    private void OpenEnergyBoostConfirmation()
    {
        if (ruinedBuilding != null ||
            serviceBuilding == null ||
            gemsManager == null ||
            !serviceBuilding.CanActivateEnergyBoost)
        {
            RefreshEnergyBoostUI();
            return;
        }

        int gemCost = serviceBuilding.EnergyBoostGemCost;
        if (gemCost <= 0 || !gemsManager.HasEnoughGems(gemCost))
        {
            RefreshEnergyBoostUI();
            return;
        }

        CloseSkipConfirmation();
        pendingEnergyBoost = serviceBuilding;
        OpenSharedConfirmation();
    }

    private void HandleCancelSkipClicked()
    {
        CloseSkipConfirmation();
    }

    private void HandleConfirmSkipClicked()
    {
        // The same Confirm button can purchase Energy Boost
        // or immediately complete an ongoing building upgrade.
        if (pendingEnergyBoost != null)
        {
            if (pendingEnergyBoost != serviceBuilding ||
                ruinedBuilding != null ||
                gemsManager == null ||
                !pendingEnergyBoost.CanActivateEnergyBoost)
            {
                CloseSkipConfirmation();
                RefreshEnergyBoostUI();
                return;
            }

            int gemCost = pendingEnergyBoost.EnergyBoostGemCost;
            if (gemCost <= 0 || !gemsManager.HasEnoughGems(gemCost))
            {
                RefreshSkipConfirmation();
                return;
            }

            ServiceBuilding selectedBoost = pendingEnergyBoost;
            bool activated = selectedBoost.TryActivateEnergyBoostWithGems(
                gemsManager
            );

            CloseSkipConfirmation();
            RefreshEnergyBoostUI();
            RefreshServiceInformation();

            if (activated)
            {
                Debug.Log(
                    $"BuildingWorkersUI: Purchased Energy Boost for " +
                    $"{selectedBoost.name} ({gemCost} Gems).",
                    this
                );
            }
            return;
        }

        if (pendingSkipRepair != null)
        {
            BuildingRepair repair = pendingSkipRepair;

            if (repair != ruinedBuilding ||
                !repair.IsRepairing ||
                gemsManager == null)
            {
                CloseSkipConfirmation();
                return;
            }

            int cost = repair.SkipRepairGemCost;
            if (cost <= 0)
            {
                CloseSkipConfirmation();
                return;
            }

            if (!gemsManager.HasEnoughGems(cost))
            {
                RefreshSkipConfirmation();
                RefreshUpgradeTimerUI();
                return;
            }

            bool repairCompleted = repair.TrySkipRepairWithGems(gemsManager);

            // OnRepairCompleted may already have switched the panel.
            CloseSkipConfirmation();
            if (repairCompleted && ruinedBuilding == repair && repair.IsRepaired)
            {
                HandleRepairCompleted(repair);
            }

            RefreshUI();
            return;
        }

        if (pendingSkipUpgrade == null ||
            pendingSkipUpgrade != buildingUpgrade ||
            !pendingSkipUpgrade.IsUpgrading ||
            gemsManager == null)
        {
            CloseSkipConfirmation();
            return;
        }

        int currentCost = pendingSkipUpgrade.SkipUpgradeGemCost;
        if (currentCost <= 0)
        {
            CloseSkipConfirmation();
            return;
        }

        if (!gemsManager.HasEnoughGems(currentCost))
        {
            Debug.LogWarning(
                "Not enough Gems to confirm Skip Upgrade.", this
            );
            RefreshSkipConfirmation();
            RefreshUpgradeTimerUI();
            return;
        }

        bool completed = pendingSkipUpgrade.TrySkipUpgradeWithGems(
            gemsManager
        );

        CloseSkipConfirmation();
        if (completed)
        {
            Debug.Log(
                "BuildingWorkersUI: Skip Upgrade completed.", this
            );
        }
        RefreshUI();
    }

    private void RefreshSkipConfirmation()
    {
        // Paid Energy Boost confirmation mode.
        if (pendingEnergyBoost != null)
        {
            if (pendingEnergyBoost != serviceBuilding ||
                ruinedBuilding != null ||
                !pendingEnergyBoost.CanActivateEnergyBoost)
            {
                CloseSkipConfirmation();
                return;
            }

            int cost = pendingEnergyBoost.EnergyBoostGemCost;
            int balance = gemsManager != null
                ? gemsManager.CurrentGems : 0;

            if (confirmTitleText != null)
                confirmTitleText.text = "Activate Energy Boost?";

            if (confirmDescriptionText != null)
            {
                confirmDescriptionText.text =
                    $"Boost service speed x" +
                    $"{pendingEnergyBoost.EnergyBoostMultiplier:0.00} for " +
                    $"{pendingEnergyBoost.EnergyBoostDuration:0} seconds?";
            }

            if (confirmCostText != null)
                confirmCostText.text = $"Cost: {cost} " +
                    (cost == 1 ? "Gem" : "Gems");

            if (confirmBalanceText != null)
                confirmBalanceText.text = $"Your Gems: {balance}";

            if (confirmButton != null)
                confirmButton.interactable = cost > 0 &&
                    gemsManager != null && gemsManager.HasEnoughGems(cost);
            return;
        }

        // Repair uses the same confirmation panel with different text.
        if (pendingSkipRepair != null)
        {
            if (pendingSkipRepair != ruinedBuilding ||
                !pendingSkipRepair.IsRepairing)
            {
                CloseSkipConfirmation();
                return;
            }

            int cost = pendingSkipRepair.SkipRepairGemCost;
            int balance = gemsManager != null
                ? gemsManager.CurrentGems : 0;

            if (confirmTitleText != null)
                confirmTitleText.text = "Skip Building Repair?";

            if (confirmDescriptionText != null)
                confirmDescriptionText.text =
                    $"Complete the repair of {pendingSkipRepair.BuildingName} immediately?";

            if (confirmCostText != null)
                confirmCostText.text = $"Cost: {cost} " +
                    (cost == 1 ? "Gem" : "Gems");

            if (confirmBalanceText != null)
                confirmBalanceText.text = $"Your Gems: {balance}";

            if (confirmButton != null)
                confirmButton.interactable = cost > 0 &&
                    gemsManager != null && gemsManager.HasEnoughGems(cost);
            return;
        }

        // Existing Skip Upgrade confirmation mode.
        if (pendingSkipUpgrade == null ||
            pendingSkipUpgrade != buildingUpgrade ||
            !pendingSkipUpgrade.IsUpgrading ||
            ruinedBuilding != null)
        {
            CloseSkipConfirmation();
            return;
        }

        int gemCost = pendingSkipUpgrade.SkipUpgradeGemCost;
        int currentBalance = gemsManager != null
            ? gemsManager.CurrentGems : 0;

        bool canConfirm = gemsManager != null &&
            gemCost > 0 && gemsManager.HasEnoughGems(gemCost);

        if (confirmTitleText != null)
            confirmTitleText.text = "Skip Building Upgrade?";

        if (confirmDescriptionText != null)
            confirmDescriptionText.text =
                $"Complete the upgrade to Level " +
                $"{pendingSkipUpgrade.UpgradingToLevel} immediately?";

        if (confirmCostText != null)
            confirmCostText.text = $"Cost: {gemCost} " +
                (gemCost == 1 ? "Gem" : "Gems");

        if (confirmBalanceText != null)
            confirmBalanceText.text = $"Your Gems: {currentBalance}";

        if (confirmButton != null)
            confirmButton.interactable = canConfirm;
    }

    private void CloseSkipConfirmation()
    {
        pendingSkipUpgrade = null;
        pendingSkipRepair = null;
        pendingEnergyBoost = null;

        if (skipUpgradeConfirmPanel != null)
            skipUpgradeConfirmPanel.SetActive(false);
    }

    // --------------------------------------------------
    // BUILDING UPGRADE EVENTS
    // --------------------------------------------------

    private void SubscribeToUpgradeEvents()
    {
        if (buildingUpgrade == null)
            return;

        buildingUpgrade.OnUpgradeStarted -=
            HandleUpgradeStarted;
        buildingUpgrade.OnUpgradeStarted +=
            HandleUpgradeStarted;

        buildingUpgrade.OnUpgradeCompleted -=
            HandleUpgradeCompleted;
        buildingUpgrade.OnUpgradeCompleted +=
            HandleUpgradeCompleted;
    }

    private void UnsubscribeFromUpgradeEvents()
    {
        if (buildingUpgrade == null)
            return;

        buildingUpgrade.OnUpgradeStarted -=
            HandleUpgradeStarted;

        buildingUpgrade.OnUpgradeCompleted -=
            HandleUpgradeCompleted;
    }

    private void HandleUpgradeStarted(
        BuildingUpgrade upgrade)
    {
        if (upgrade != buildingUpgrade)
            return;

        RefreshRepairSection();
        RefreshUpgradeTimerUI();
    }

    private void HandleUpgradeCompleted(
        BuildingUpgrade upgrade)
    {
        if (upgrade != buildingUpgrade)
            return;

        if (pendingSkipUpgrade == upgrade)
        {
            CloseSkipConfirmation();
        }

        RefreshUI();
    }

    // --------------------------------------------------
    // BUILDING REPAIR EVENTS
    // --------------------------------------------------

    private void SubscribeToRepairEvents()
    {
        if (ruinedBuilding == null)
            return;

        ruinedBuilding.OnRepairStarted -= HandleRepairStarted;
        ruinedBuilding.OnRepairStarted += HandleRepairStarted;
        ruinedBuilding.OnRepairCompleted -= HandleRepairCompleted;
        ruinedBuilding.OnRepairCompleted += HandleRepairCompleted;
    }

    private void UnsubscribeFromRepairEvents()
    {
        if (ruinedBuilding == null)
            return;

        ruinedBuilding.OnRepairStarted -= HandleRepairStarted;
        ruinedBuilding.OnRepairCompleted -= HandleRepairCompleted;
    }

    private void HandleRepairStarted(BuildingRepair repair)
    {
        if (repair != ruinedBuilding)
            return;

        RefreshRepairSection();
    }

    private void HandleRepairCompleted(BuildingRepair repair)
    {
        if (repair != ruinedBuilding || !repair.IsRepaired)
            return;

        // The repair event fires after OnRepaired, so models are ready.
        CloseSkipConfirmation();
        SetBuilding(repair.RepairedWorkerAssignment);
    }

    // --------------------------------------------------
    // REPAIR / UPGRADE PANEL VISIBILITY
    // --------------------------------------------------

    private void RefreshRepairSection()
    {
        bool hasRuinedBuilding = ruinedBuilding != null &&
            !ruinedBuilding.IsRepaired;

        bool isRepairing = hasRuinedBuilding &&
            ruinedBuilding.IsRepairing;

        bool showRepairButton = hasRuinedBuilding && !isRepairing;

        bool isUpgrading = !hasRuinedBuilding &&
            buildingUpgrade != null &&
            buildingUpgrade.IsUpgrading;

        bool showUpgradeButton = !hasRuinedBuilding &&
            !isUpgrading &&
            buildingUpgrade != null &&
            buildingUpgrade.CanUpgrade;

        bool showTimer = isRepairing || isUpgrading;

        if (repairSection != null)
            repairSection.SetActive(
                showRepairButton || showUpgradeButton || showTimer
            );

        if (repairButton != null)
            repairButton.gameObject.SetActive(
                showRepairButton || showUpgradeButton
            );

        if (upgradeTimerSection != null)
            upgradeTimerSection.SetActive(showTimer);

        if (showRepairButton)
        {
            if (repairButton != null)
                repairButton.interactable = ruinedBuilding.CanAfford;

            if (repairButtonText != null)
            {
                string repairLabel = $"Repair ({ruinedBuilding.RepairCost}$)";

                if (!ruinedBuilding.MeetsResortLevelRequirement)
                {
                    repairButtonText.text =
                        $"{repairLabel}\n" +
                        $"Requires Resort Level {ruinedBuilding.MinimumRequiredResortLevel}";
                }
                else
                {
                    repairButtonText.text =
                        repairLabel +
                        BuildMissingCoinsLine(ruinedBuilding.RepairCost);
                }
            }
        }
        else if (showUpgradeButton)
        {
            if (repairButton != null)
                repairButton.interactable = buildingUpgrade.CanAfford;

            if (repairButtonText != null)
            {
                string upgradeLabel =
                    $"Upgrade Lv. {buildingUpgrade.CurrentLevel + 1}";
                string costLabel = $"{buildingUpgrade.NextLevelCost} Coins";

                if (buildingUpgrade.MeetsResortLevelRequirement)
                {
                    int currentCapacity = serviceBuilding != null
                        ? serviceBuilding.Seats : 0;

                    repairButtonText.text =
                        $"{upgradeLabel}\n" +
                        $"Capacity: {currentCapacity} → " +
                        $"{buildingUpgrade.NextLevelCapacity}\n" +
                        costLabel +
                        BuildMissingCoinsLine(buildingUpgrade.NextLevelCost);
                }
                else
                {
                    repairButtonText.text =
                        $"{upgradeLabel}\n" +
                        $"Requires Resort Level {buildingUpgrade.MinimumRequiredResortLevel}\n" +
                        costLabel;
                }
            }
        }

        RefreshUpgradeTimerUI();
    }

    // SCRUM-43: Extra button line with the Coins still needed.
    // Empty when the player can afford the cost.
    private string BuildMissingCoinsLine(int cost)
    {
        if (coinsManager == null)
            return string.Empty;

        int missing = coinsManager.GetMissingCoins(cost);

        return missing > 0
            ? $"\n<color=#E85A54>Need {missing} more Coins</color>"
            : string.Empty;
    }

    // --------------------------------------------------
    // UPGRADE TIMER AND SKIP COST
    // --------------------------------------------------

    // Shared timer: the same serialized references work for Repair and Upgrade.
    private void RefreshUpgradeTimerUI()
    {
        bool isRepairing = ruinedBuilding != null &&
            ruinedBuilding.IsRepairing;

        bool isUpgrading = !isRepairing &&
            ruinedBuilding == null &&
            buildingUpgrade != null &&
            buildingUpgrade.IsUpgrading;

        bool shouldShow = isRepairing || isUpgrading;

        if (upgradeTimerSection != null)
            upgradeTimerSection.SetActive(shouldShow);

        if (!shouldShow)
        {
            if (upgradeTimerText != null)
                upgradeTimerText.text = "Remaining 00:00";

            if (upgradeProgressFill != null)
                upgradeProgressFill.fillAmount = 0f;

            if (skipUpgradeButton != null)
            {
                skipUpgradeButton.interactable = false;
                skipUpgradeButton.gameObject.SetActive(false);
            }
            return;
        }

        float remaining;
        float progress;
        int gemCost;
        string actionLabel;

        if (isRepairing)
        {
            if (upgradeTimerTitle != null)
                upgradeTimerTitle.text = "Repairing Building";

            remaining = ruinedBuilding.RepairRemainingTime;
            progress = ruinedBuilding.RepairProgress;
            gemCost = ruinedBuilding.SkipRepairGemCost;
            actionLabel = "Skip Repair";
        }
        else
        {
            if (upgradeTimerTitle != null)
                upgradeTimerTitle.text =
                    $"Upgrading to Level {buildingUpgrade.UpgradingToLevel}";

            remaining = buildingUpgrade.UpgradeRemainingTime;
            progress = buildingUpgrade.UpgradeProgress;
            gemCost = buildingUpgrade.SkipUpgradeGemCost;
            actionLabel = "Skip Upgrade";
        }

        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        if (upgradeTimerText != null)
            upgradeTimerText.text =
                $"Remaining {minutes:00}:{seconds:00}";

        if (upgradeProgressFill != null)
            upgradeProgressFill.fillAmount = progress;

        if (skipUpgradeButtonText != null)
            skipUpgradeButtonText.text =
                $"{actionLabel} - {gemCost} " +
                (gemCost == 1 ? "Gem" : "Gems");

        if (skipUpgradeButton != null)
        {
            skipUpgradeButton.gameObject.SetActive(true);
            skipUpgradeButton.interactable =
                gemsManager != null &&
                gemCost > 0 &&
                gemsManager.HasEnoughGems(gemCost);
        }
    }

    // --------------------------------------------------
    // ENERGY BOOST
    // --------------------------------------------------

    private void HandleEnergyBoostClicked()
    {
        // No free activation: opening the shared confirmation
        // is the only action performed by this button.
        OpenEnergyBoostConfirmation();
    }

    private void HandleEnergyBoostStateChanged(
        ServiceBuilding building,
        bool isActive)
    {
        if (building != serviceBuilding)
            return;

        RefreshEnergyBoostUI();
        RefreshServiceInformation();
    }

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

            RefreshEnergyBoostTimerUI();
            return;
        }

        bool isBoosted =
            serviceBuilding.IsEnergyBoostActive;

        bool usedToday =
            serviceBuilding.HasUsedEnergyBoostToday;

        DayStateManager currentDayManager =
            DayStateManager.Instance;

        bool hasValidDay =
            currentDayManager != null &&
            currentDayManager.CurrentDay > 0;

        bool canActivate =
            serviceBuilding.CanActivateEnergyBoost &&
            gemsManager != null &&
            gemsManager.HasEnoughGems(serviceBuilding.EnergyBoostGemCost);

        if (energyBoostButton != null)
        {
            energyBoostButton.interactable = canActivate;
        }

        if (energyBoostButtonText != null)
        {
            if (isBoosted)
            {
                float remaining = Mathf.Max(
                    0f,
                    serviceBuilding.EnergyBoostRemainingTime
                );

                energyBoostButtonText.text =
                    $"Energy Boost Active ({remaining:0.0}s)";
            }
            else if (usedToday)
            {
                energyBoostButtonText.text =
                    "Energy Boost Used Today";
            }
            else if (!serviceBuilding.isActiveAndEnabled ||
                     !serviceBuilding.CanOperate ||
                     !hasValidDay)
            {
                energyBoostButtonText.text =
                    "Energy Boost Unavailable";
            }
            else
            {
                int cost = serviceBuilding.EnergyBoostGemCost;
                energyBoostButtonText.text =
                    $"Energy Boost " +
                    $"(x{serviceBuilding.EnergyBoostMultiplier:0.00}, " +
                    $"{serviceBuilding.EnergyBoostDuration:0}s) - " +
                    $"{cost} {(cost == 1 ? "Gem" : "Gems")}";
            }
        }

        RefreshEnergyBoostTimerUI();
    }

    private void RefreshEnergyBoostTimerUI()
    {
        bool shouldShowTimer =
            ruinedBuilding == null &&
            serviceBuilding != null &&
            serviceBuilding.IsEnergyBoostActive;

        if (energyBoostTimerPanel != null)
        {
            energyBoostTimerPanel.SetActive(
                shouldShowTimer
            );
        }

        if (!shouldShowTimer)
        {
            if (energyBoostTimerText != null)
            {
                energyBoostTimerText.text = "00:00";
            }

            if (energyBoostTimerFill != null)
            {
                energyBoostTimerFill.fillAmount = 0f;
                energyBoostTimerFill.color = timerHighColor;
            }

            return;
        }

        float remaining = Mathf.Max(
            0f,
            serviceBuilding.EnergyBoostRemainingTime
        );

        float duration = Mathf.Max(
            0.1f,
            serviceBuilding.EnergyBoostDuration
        );

        int remainingSeconds = Mathf.CeilToInt(remaining);
        int minutes = remainingSeconds / 60;
        int seconds = remainingSeconds % 60;

        if (energyBoostTimerText != null)
        {
            energyBoostTimerText.text =
                $"{minutes:00}:{seconds:00}";
        }

        float progress = Mathf.Clamp01(
            remaining / duration
        );

        if (energyBoostTimerFill != null)
        {
            energyBoostTimerFill.fillAmount = progress;

            if (progress > highTimeThreshold)
            {
                energyBoostTimerFill.color =
                    timerHighColor;
            }
            else if (progress > lowTimeThreshold)
            {
                energyBoostTimerFill.color =
                    timerMediumColor;
            }
            else
            {
                energyBoostTimerFill.color =
                    timerLowColor;
            }
        }
    }

    // --------------------------------------------------
    // BUILDING EVENTS
    // --------------------------------------------------

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

    // --------------------------------------------------
    // BUILDING SELECTION
    // --------------------------------------------------

    public void SetBuilding(WorkerAssignment newAssignment)
    {
        CloseSkipConfirmation();

        if (isActiveAndEnabled)
        {
            UnsubscribeFromBuildingEvents();
            UnsubscribeFromUpgradeEvents();
            UnsubscribeFromRepairEvents();
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
            SubscribeToUpgradeEvents();
        }

        RefreshUI();
    }

    private void HandleWorkerDetailsClicked(WorkerData worker)
    {
        selectedWorker = null;
        UpdateSelectionVisuals();
        OnWorkerDetailsRequested?.Invoke(worker);
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

            item.SetSelected(
                item.Worker == selectedWorker
            );
        }
    }

    // --------------------------------------------------
    // MAIN PANEL REFRESH
    // --------------------------------------------------

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
                serviceStatusText.text = ruinedBuilding.IsRepairing
                    ? "Status: Repairing" : "Status: Ruined";

                serviceStatusText.color =
                    new Color(0.55f, 0.58f, 0.61f);
            }

            if (serviceSpeedText != null)
            {
                serviceSpeedText.text = ruinedBuilding.IsRepairing
                    ? "Repair in progress" : "Repair to open";
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

        if (workersList == null ||
            workerListItemPrefab == null)
        {
            return;
        }

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

            item.Setup(worker, HandleMoveClicked, HandleWorkerDetailsClicked);
            displayedItems.Add(item);
        }

        UpdateSelectionVisuals();
    }

    // --------------------------------------------------
    // BUILDING STATISTICS
    // --------------------------------------------------

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
                serviceSpeedText.text = "Speed: 0.00x";
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