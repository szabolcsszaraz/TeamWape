
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BuildingWorkersUI : MonoBehaviour
{
    [Header("Building")]
    [SerializeField] private WorkerAssignment workerAssignment;

    [Header("UI References")]
    [SerializeField] private TMP_Text buildingNameText;
    [SerializeField] private TMP_Text workerCountText;
    [SerializeField] private Transform workersList;

    [Header("Prefab")]
    [SerializeField] private GameObject workerListItemPrefab;

    private WorkerData selectedWorker;

    private readonly List<WorkerListItemUI> displayedItems = new();

    public WorkerData SelectedWorker => selectedWorker;

    public event Action<WorkerData> OnWorkerSelected;

    private void OnEnable()
    {
        if (workerAssignment != null)
        {
            workerAssignment.OnWorkersChanged += HandleWorkersChanged;
        }

        RefreshUI();
    }

    private void OnDisable()
    {
        if (workerAssignment != null)
        {
            workerAssignment.OnWorkersChanged -= HandleWorkersChanged;
        }
    }

    private void HandleWorkersChanged(WorkerAssignment assignment)
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

    public void SetBuilding(WorkerAssignment newAssignment)
    {
        if (workerAssignment != null && isActiveAndEnabled)
        {
            workerAssignment.OnWorkersChanged -= HandleWorkersChanged;
        }

        if (workerAssignment != newAssignment)
        {
            selectedWorker = null;
        }

        workerAssignment = newAssignment;

        if (workerAssignment != null && isActiveAndEnabled)
        {
            workerAssignment.OnWorkersChanged += HandleWorkersChanged;
        }

        RefreshUI();
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

        if (workerAssignment == null)
        {
            if (buildingNameText != null)
                buildingNameText.text = "No Building Selected";

            if (workerCountText != null)
                workerCountText.text = "Workers: -";

            return;
        }

        if (buildingNameText != null)
        {
            buildingNameText.text =
                $"{workerAssignment.gameObject.name} Workers";
        }

        if (workerCountText != null)
        {
            workerCountText.text =
                $"Workers: {workerAssignment.AssignedWorkerCount} / " +
                $"{workerAssignment.MaxWorkerSlots}";
        }

        if (workersList == null || workerListItemPrefab == null)
            return;

        foreach (WorkerData worker in workerAssignment.AssignedWorkers)
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
                    "WorkerListItem prefab is missing WorkerListItemUI!",
                    itemObject
                );

                Destroy(itemObject);
                continue;
            }

            item.Setup(worker, HandleMoveClicked);
            displayedItems.Add(item);
        }

        UpdateSelectionVisuals();
    }
}
