
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DevGameControlsUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button startWaveButton;
    [SerializeField] private Button endDayButton;
    [SerializeField] private Button nextDayButton;

    [Header("Optional Status Text")]
    [SerializeField] private TMP_Text statusText;

    private DayStateManager dayManager;
    private WaveManager waveManager;

    private void Start()
    {
        dayManager = DayStateManager.Instance;
        waveManager = WaveManager.Instance;

        if (dayManager == null || waveManager == null)
        {
            Debug.LogError(
                "DevGameControlsUI: Missing manager!"
            );
            enabled = false;
            return;
        }

        if (startWaveButton != null)
            startWaveButton.onClick.AddListener(StartWave);

        if (endDayButton != null)
            endDayButton.onClick.AddListener(EndDay);

        if (nextDayButton != null)
            nextDayButton.onClick.AddListener(StartNextDay);

        dayManager.OnPhaseChanged += HandlePhaseChanged;
        dayManager.OnDayChanged += HandleDayChanged;

        waveManager.OnWaveStarted += HandleWaveStarted;

        UpdateUI();
    }

    private void StartWave()
    {
        if (waveManager == null)
            return;

        waveManager.StartWave();
        UpdateUI();
    }

    private void EndDay()
    {
        if (dayManager == null)
            return;

        dayManager.StartPassivePhase();
        UpdateUI();
    }

    private void StartNextDay()
    {
        if (dayManager == null)
            return;

        dayManager.StartNextDay();
        UpdateUI();
    }

    private void HandlePhaseChanged(DayPhase phase)
    {
        UpdateUI();
    }

    private void HandleDayChanged(int day)
    {
        UpdateUI();
    }

    private void HandleWaveStarted(int wave)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (dayManager == null || waveManager == null)
            return;

        bool isActive = dayManager.IsActivePhase;
        bool isPassive = dayManager.IsPassivePhase;

        bool canStartWave =
            isActive &&
            !waveManager.IsWaveActive;

        if (startWaveButton != null)
        {
            startWaveButton.interactable = canStartWave;
        }

        if (endDayButton != null)
        {
            endDayButton.interactable = isActive;
        }

        if (nextDayButton != null)
        {
            nextDayButton.interactable = isPassive;
        }

        if (statusText != null)
        {
            statusText.text =
                $"Day: {dayManager.CurrentDay}\n" +
                $"Phase: {dayManager.CurrentPhase}\n" +
                $"Wave: {waveManager.CurrentWave}";
        }
    }

    private void OnDestroy()
    {
        if (startWaveButton != null)
            startWaveButton.onClick.RemoveListener(StartWave);

        if (endDayButton != null)
            endDayButton.onClick.RemoveListener(EndDay);

        if (nextDayButton != null)
            nextDayButton.onClick.RemoveListener(StartNextDay);

        if (dayManager != null)
        {
            dayManager.OnPhaseChanged -= HandlePhaseChanged;
            dayManager.OnDayChanged -= HandleDayChanged;
        }

        if (waveManager != null)
        {
            waveManager.OnWaveStarted -= HandleWaveStarted;
        }
    }
}
