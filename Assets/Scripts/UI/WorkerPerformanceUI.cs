
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorkerPerformanceUI : MonoBehaviour
{
    [Header("Worker")]
    [SerializeField] private WorkerData worker;

    [Header("Upgrade Cost")]
    [SerializeField] private PlayerCoinsManager coinsManager;
    [SerializeField, Min(1)] private int upgradeCoinCost = 100;

    [Header("UI References")]
    [SerializeField] private TMP_Text workerNameText;
    [SerializeField] private TMP_Text performanceLevelText;
    [SerializeField] private TMP_Text performanceMultiplierText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradeButtonText;

    private int UpgradeCoinCost => Mathf.Max(1, upgradeCoinCost);

    private void OnEnable()
    {
        if (coinsManager != null)
        {
            coinsManager.OnCoinsChanged += HandleCoinsChanged;
        }

        if (worker != null)
        {
            worker.OnPerformanceLevelChanged += HandleLevelChanged;
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(UpgradeWorker);
        }

        RefreshUI();
    }

    private void OnDisable()
    {
        if (coinsManager != null)
        {
            coinsManager.OnCoinsChanged -= HandleCoinsChanged;
        }

        if (worker != null)
        {
            worker.OnPerformanceLevelChanged -= HandleLevelChanged;
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(UpgradeWorker);
        }
    }

    private void HandleLevelChanged(WorkerData changedWorker, int newLevel)
    {
        if (changedWorker == worker)
        {
            RefreshUI();
        }
    }

    public void SetWorker(WorkerData newWorker)
    {
        if (worker != null && isActiveAndEnabled)
        {
            worker.OnPerformanceLevelChanged -= HandleLevelChanged;
        }

        worker = newWorker;

        if (worker != null && isActiveAndEnabled)
        {
            worker.OnPerformanceLevelChanged += HandleLevelChanged;
        }

        RefreshUI();
    }

    public void UpgradeWorker()
    {
        WorkerData workerToUpgrade = worker;
        PlayerCoinsManager wallet = coinsManager;
        int cost = UpgradeCoinCost;

        if (workerToUpgrade == null || wallet == null || !workerToUpgrade.CanUpgrade())
        {
            return;
        }

        if (!wallet.TrySpendCoins(cost))
        {
            RefreshUpgradeButton();
            return;
        }

        if (workerToUpgrade == null || !workerToUpgrade.UpgradePerformance())
        {
            wallet.AddCoins(cost);
        }
    }

    private void HandleCoinsChanged(int currentCoins)
    {
        RefreshUpgradeButton();
    }

    private void RefreshUI()
    {
        if (worker == null)
        {
            if (workerNameText != null)
                workerNameText.text = "No Worker Selected";

            if (performanceLevelText != null)
                performanceLevelText.text = "Level -";

            if (performanceMultiplierText != null)
                performanceMultiplierText.text = "Performance: -";

            RefreshUpgradeButton();

            return;
        }

        if (workerNameText != null)
        {
            workerNameText.text = worker.WorkerName;
        }

        if (performanceLevelText != null)
        {
            performanceLevelText.text =
                $"Level {worker.PerformanceLevel} / " +
                $"{WorkerData.MaxPerformanceLevel}";
        }

        if (performanceMultiplierText != null)
        {
            performanceMultiplierText.text =
                $"Performance: {worker.PerformanceMultiplier * 100f:0}%";
        }

        RefreshUpgradeButton();
    }

    private void RefreshUpgradeButton()
    {
        int cost = UpgradeCoinCost;
        bool canUpgrade = worker != null && worker.CanUpgrade();

        if (upgradeButton != null)
        {
            upgradeButton.interactable =
                canUpgrade && coinsManager != null && coinsManager.HasEnoughCoins(cost);
        }

        if (upgradeButtonText != null)
        {
            upgradeButtonText.text = worker != null && worker.IsMaxLevel
                ? "Max Level"
                : $"Upgrade ({cost} Coins)";
        }
    }
}
