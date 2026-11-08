
using TMPro;
using UnityEngine;

public class DailySummaryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dailySummaryPanel;
    [SerializeField] private TMP_Text summaryTitleText;
    [SerializeField] private TMP_Text servedGuestsValueText;

    [Header("References")]
    [SerializeField] private WaveServedGuestsTracker servedGuestsTracker;
    [SerializeField] private DayStateManager dayStateManager;

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

        if (dayStateManager != null)
        {
            dayStateManager.OnPhaseChanged += HandlePhaseChanged;
        }

        if (dailySummaryPanel != null)
        {
            dailySummaryPanel.SetActive(false);
        }
    }

    private void HandleWaveResultFinalized(int finalServedGuests)
    {
        UpdateSummary(finalServedGuests);

        if (dailySummaryPanel != null)
        {
            dailySummaryPanel.SetActive(true);
        }

        Debug.Log(
            $"Daily Summary: {finalServedGuests} completed services.",
            this
        );
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

    private void OnDestroy()
    {
        if (servedGuestsTracker != null)
        {
            servedGuestsTracker.OnWaveResultFinalized -=
                HandleWaveResultFinalized;
        }

        if (dayStateManager != null)
        {
            dayStateManager.OnPhaseChanged -= HandlePhaseChanged;
        }
    }
}
