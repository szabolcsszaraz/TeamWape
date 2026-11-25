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

    private Button rowButton;
    private WorkerData worker;
    private Action<WorkerData> onMoveClicked;
    private Action<WorkerData> onDetailsClicked;

    public WorkerData Worker => worker;

    private void Awake()
    {
        rowButton = GetComponent<Button>();

        if (rowButton != null)
        {
            rowButton.onClick.AddListener(HandleDetailsClick);
        }

        if (moveButton != null)
        {
            moveButton.onClick.AddListener(HandleMoveClick);
        }
    }

    public void Setup(
        WorkerData newWorker,
        Action<WorkerData> moveCallback,
        Action<WorkerData> detailsCallback = null)
    {
        worker = newWorker;
        onMoveClicked = moveCallback;
        onDetailsClicked = detailsCallback;

        if (worker == null)
        {
            return;
        }

        if (workerNameText != null)
        {
            workerNameText.text = worker.WorkerName;
        }

        if (workerLevelText != null)
        {
            workerLevelText.text = $"Lv. {worker.PerformanceLevel}";
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (moveButtonText != null)
        {
            moveButtonText.text = selected ? "Selected" : "Move";
        }
    }

    private void HandleDetailsClick()
    {
        if (worker != null)
        {
            onDetailsClicked?.Invoke(worker);
        }
    }

    private void HandleMoveClick()
    {
        if (worker != null)
        {
            onMoveClicked?.Invoke(worker);
        }
    }

    private void OnDestroy()
    {
        if (rowButton != null)
        {
            rowButton.onClick.RemoveListener(HandleDetailsClick);
        }

        if (moveButton != null)
        {
            moveButton.onClick.RemoveListener(HandleMoveClick);
        }
    }
}