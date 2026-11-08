
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorkerListItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text workerNameText;
    [SerializeField] private TMP_Text workerLevelText;
    [SerializeField] private Button moveButton;
    [SerializeField] private TMP_Text moveButtonText;

    private WorkerData worker;
    private Action<WorkerData> onMoveClicked;

    // SCRUM-19: Allows other scripts to identify
    // which worker belongs to this UI item.
    public WorkerData Worker => worker;

    private void Awake()
    {
        if (moveButton != null)
        {
            moveButton.onClick.AddListener(HandleMoveClick);
        }
    }

    public void Setup(
        WorkerData newWorker,
        Action<WorkerData> moveCallback)
    {
        worker = newWorker;
        onMoveClicked = moveCallback;

        if (worker == null)
            return;

        if (workerNameText != null)
        {
            workerNameText.text = worker.WorkerName;
        }

        if (workerLevelText != null)
        {
            workerLevelText.text =
                $"Lv. {worker.PerformanceLevel}";
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (moveButtonText != null)
        {
            moveButtonText.text = selected
                ? "Selected"
                : "Move";
        }
    }

    private void HandleMoveClick()
    {
        if (worker == null)
            return;

        onMoveClicked?.Invoke(worker);
    }

    private void OnDestroy()
    {
        if (moveButton != null)
        {
            moveButton.onClick.RemoveListener(HandleMoveClick);
        }
    }
}
