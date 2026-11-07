
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorkerPerformanceUI : MonoBehaviour
{
    [Header("Worker")]
    [SerializeField] private WorkerData worker;

    [Header("UI References")]
    [SerializeField] private TMP_Text workerNameText;
    [SerializeField] private TMP_Text performanceLevelText;
    [SerializeField] private TMP_Text performanceMultiplierText;
    [SerializeField] private Button upgradeButton;

    private void OnEnable()
    {
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
        if (worker == null)
        {
            return;
        }

        worker.UpgradePerformance();
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

            if (upgradeButton != null)
                upgradeButton.interactable = false;

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

        if (upgradeButton != null)
        {
            upgradeButton.interactable = worker.CanUpgrade();
        }
    }
}
