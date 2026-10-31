
using System.Collections;
using TMPro;
using UnityEngine;

public class WaveArrivalNotificationUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Notification Settings")]
    [SerializeField, Min(0f)]
    private float displayDuration = 4f;

    private Coroutine hideCoroutine;

    private WaveManager waveManager;
    private DayStateManager dayStateManager;

    private void Awake()
    {
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(false);
        }
    }

    private void Start()
    {
        waveManager = WaveManager.Instance;
        dayStateManager = DayStateManager.Instance;

        if (waveManager != null)
        {
            waveManager.OnWaveStarted += HandleWaveStarted;

            waveManager.OnWaveArrivalCompleted +=
                HandleArrivalCompleted;
        }
        else
        {
            Debug.LogError(
                "WaveArrivalNotificationUI: WaveManager not found!"
            );
        }

        if (dayStateManager != null)
        {
            dayStateManager.OnPhaseChanged += HandlePhaseChanged;
        }
        else
        {
            Debug.LogError(
                "WaveArrivalNotificationUI: DayStateManager not found!"
            );
        }
    }

    private void HandleWaveStarted(int waveNumber)
    {
        ShowNotification(
            $"Wave {waveNumber} Started!",
            "A new wave of tourists is arriving at the resort."
        );
    }

    private void HandleArrivalCompleted(int waveNumber)
    {
        ShowNotification(
            "All Tourists Have Arrived!",
            $"Wave {waveNumber} - All tourists have arrived."
        );
    }

    private void HandlePhaseChanged(DayPhase phase)
    {
        if (phase == DayPhase.Passive)
        {
            int currentDay = dayStateManager.CurrentDay;

            ShowNotification(
                $"Day {currentDay} Completed!",
                "Passive Phase started. Review your daily results."
            );
        }
        else if (phase == DayPhase.Active)
        {
            int currentDay = dayStateManager.CurrentDay;

            ShowNotification(
                $"Day {currentDay} Started!",
                "Active Phase started. Prepare for new tourists!"
            );
        }
    }

    private void ShowNotification(string title, string description)
    {
        if (notificationPanel == null)
        {
            Debug.LogWarning(
                "WaveArrivalNotificationUI: Notification Panel missing!"
            );
            return;
        }

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        if (titleText != null)
        {
            titleText.text = title;
        }

        if (descriptionText != null)
        {
            descriptionText.text = description;
        }

        notificationPanel.SetActive(true);

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(displayDuration);

        if (notificationPanel != null)
        {
            notificationPanel.SetActive(false);
        }

        hideCoroutine = null;
    }

    private void OnDestroy()
    {
        if (waveManager != null)
        {
            waveManager.OnWaveStarted -= HandleWaveStarted;

            waveManager.OnWaveArrivalCompleted -=
                HandleArrivalCompleted;
        }

        if (dayStateManager != null)
        {
            dayStateManager.OnPhaseChanged -= HandlePhaseChanged;
        }
    }
}
