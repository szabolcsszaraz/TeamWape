
using UnityEngine;
using TMPro;

public class QueueWarningUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ServiceQueue serviceQueue;
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private TMP_Text warningText;

    private void Start()
    {
        if (serviceQueue == null ||
            warningPanel == null ||
            warningText == null)
        {
            Debug.LogError(
                "QueueWarningUI: Missing Inspector references!"
            );
            return;
        }

        serviceQueue.OnQueueWarningChanged += HandleWarningChanged;
        serviceQueue.OnQueueCountChanged += UpdateWarningText;

        UpdateWarningText(serviceQueue.WaitingGuestCount);
        HandleWarningChanged(serviceQueue.IsOverLimit);
    }

    private void OnDestroy()
    {
        if (serviceQueue == null)
            return;

        serviceQueue.OnQueueWarningChanged -= HandleWarningChanged;
        serviceQueue.OnQueueCountChanged -= UpdateWarningText;
    }

    private void HandleWarningChanged(bool isOverLimit)
    {
        warningPanel.SetActive(isOverLimit);
    }

    private void UpdateWarningText(int waitingGuests)
    {
        warningText.text =
            "QUEUE WARNING!\n" +
            "Restaurant queue limit exceeded!\n" +
            $"Waiting: {waitingGuests} / Limit: {serviceQueue.WarningLimit}";
    }
}
