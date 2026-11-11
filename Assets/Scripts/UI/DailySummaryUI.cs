
using System.Text;
using TMPro;
using UnityEngine;

public class DailySummaryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dailySummaryPanel;
    [SerializeField] private TMP_Text summaryTitleText;
    [SerializeField] private TMP_Text servedGuestsValueText;

    [Header("Service Performance UI")]
    [SerializeField] private TMP_Text servicePerformanceText;

    [Header("References")]
    [SerializeField] private WaveServedGuestsTracker servedGuestsTracker;
    [SerializeField] private DayStateManager dayStateManager;
    [SerializeField]
    private ServicePerformanceTracker
        servicePerformanceTracker;

    private void Start()
    {
        if (servedGuestsTracker == null)
        {
            servedGuestsTracker =
                FindFirstObjectByType<WaveServedGuestsTracker>();
        }

        if (dayStateManager == null)
        {
            dayStateManager = DayStateManager.Instance;
        }

        if (servicePerformanceTracker == null)
        {
            servicePerformanceTracker =
                FindFirstObjectByType<ServicePerformanceTracker>();
        }

        if (servedGuestsTracker != null)
        {
            servedGuestsTracker.OnWaveResultFinalized +=
                HandleWaveResultFinalized;
        }
        else
        {
            Debug.LogWarning(
                "DailySummaryUI: WaveServedGuestsTracker not found!",
                this
            );
        }

        if (servicePerformanceTracker != null)
        {
            servicePerformanceTracker.OnWaveResultStored +=
                HandleServiceResultStored;
        }
        else
        {
            Debug.LogWarning(
                "DailySummaryUI: ServicePerformanceTracker not found!",
                this
            );
        }

        if (dayStateManager != null)
        {
            dayStateManager.OnPhaseChanged += HandlePhaseChanged;
        }

        if (servicePerformanceText != null)
        {
            servicePerformanceText.text = "";
        }

        if (dailySummaryPanel != null)
        {
            dailySummaryPanel.SetActive(false);
        }
    }

    private void HandleWaveResultFinalized(int finalServedGuests)
    {
        UpdateSummary(finalServedGuests);
        RefreshServicePerformance();

        if (dailySummaryPanel != null)
        {
            dailySummaryPanel.SetActive(true);
        }

        Debug.Log(
            $"Daily Summary: {finalServedGuests} completed services.",
            this
        );
    }

    private void HandleServiceResultStored(
        ServicePerformanceTracker.WaveResult result)
    {
        UpdateServicePerformance(result);
    }

    private void HandlePhaseChanged(DayPhase phase)
    {
        if (phase == DayPhase.Active)
        {
            if (dailySummaryPanel != null)
            {
                dailySummaryPanel.SetActive(false);
            }
        }
    }

    private void UpdateSummary(int servedGuests)
    {
        if (summaryTitleText != null)
        {
            int currentDay = dayStateManager != null
                ? dayStateManager.CurrentDay
                : 1;

            summaryTitleText.text =
                $"Day {currentDay} Summary";
        }

        if (servedGuestsValueText != null)
        {
            servedGuestsValueText.text = servedGuests.ToString();
        }
    }

    private void RefreshServicePerformance()
    {
        if (servicePerformanceTracker == null)
            return;

        UpdateServicePerformance(
            servicePerformanceTracker.LastWaveResult
        );
    }

    private void UpdateServicePerformance(
        ServicePerformanceTracker.WaveResult result)
    {
        if (servicePerformanceText == null)
            return;

        if (result == null)
        {
            servicePerformanceText.text =
                "No service results available.";

            return;
        }

        StringBuilder builder = new StringBuilder();

        builder.AppendLine("SERVICE PERFORMANCE");
        builder.AppendLine();

        int displayedServices = 0;

        foreach (
            ServicePerformanceTracker.ServiceUsageData data
            in result.serviceResults
        )
        {
            if (data.totalRequests <= 0)
                continue;

            displayedServices++;

            builder.AppendLine(
                $"{data.serviceType.ToDisplayName()}: " +
                $"{data.performanceScore:0}%"
            );

            builder.AppendLine(
                $"Served: {data.successfulRequests}/" +
                $"{data.totalRequests} | " +
                $"Wait: {data.averageWaitTime:0.0}s"
            );

            builder.AppendLine();
        }

        if (displayedServices == 0)
        {
            builder.AppendLine(
                "No service requests this wave."
            );
        }

        servicePerformanceText.text =
            builder.ToString().TrimEnd();
    }

    private void OnDestroy()
    {
        if (servedGuestsTracker != null)
        {
            servedGuestsTracker.OnWaveResultFinalized -=
                HandleWaveResultFinalized;
        }

        if (servicePerformanceTracker != null)
        {
            servicePerformanceTracker.OnWaveResultStored -=
                HandleServiceResultStored;
        }

        if (dayStateManager != null)
        {
            dayStateManager.OnPhaseChanged -= HandlePhaseChanged;
        }
    }
}
