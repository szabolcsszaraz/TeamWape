
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BuildingWorkersUI : MonoBehaviour
{
    [Header("Building")]
    [SerializeField] private WorkerAssignment workerAssignment;

    private ServiceBuilding serviceBuilding;

    [Header("UI References")]
    [SerializeField] private TMP_Text buildingNameText;
    [SerializeField] private TMP_Text workerCountText;
    [SerializeField] private Transform workersList;

    [Header("Service Status UI")]
    [SerializeField] private TMP_Text serviceStatusText;
    [SerializeField] private TMP_Text serviceSpeedText;

    [Header("Prefab")]
    [SerializeField] private GameObject workerListItemPrefab;

    private WorkerData selectedWorker;

    private readonly List<WorkerListItemUI> displayedItems = new();

    public WorkerData SelectedWorker => selectedWorker;

    public event Action<WorkerData> OnWorkerSelected;

    private void OnEnable()
    {
        SubscribeToBuildingEvents();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeFromBuildingEvents();
    }

    private void SubscribeToBuildingEvents()
    {
        if (workerAssignment != null)
        {
            workerAssignment.OnWorkersChanged -= HandleWorkersChanged;
            workerAssignment.OnWorkersChanged += HandleWorkersChanged;
        }

        if (serviceBuilding != null)
        {
            serviceBuilding.OnServiceStateChanged -=
                HandleServiceStateChanged;

            serviceBuilding.OnServiceStateChanged +=
                HandleServiceStateChanged;

            serviceBuilding.OnPerformanceChanged -=
                HandlePerformanceChanged;

            serviceBuilding.OnPerformanceChanged +=
                HandlePerformanceChanged;
        }
    }

    private void UnsubscribeFromBuildingEvents()
    {
        if (workerAssignment != null)
        {
            workerAssignment.OnWorkersChanged -= HandleWorkersChanged;
        }

        if (serviceBuilding != null)
        {
            serviceBuilding.OnServiceStateChanged -=
                HandleServiceStateChanged;

            serviceBuilding.OnPerformanceChanged -=
                HandlePerformanceChanged;
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

    private void HandleServiceStateChanged(
        ServiceBuilding building,
        ServiceLoadState newState)
    {
        if (building != serviceBuilding)
            return;

        RefreshServiceInformation();
    }

    private void HandlePerformanceChanged(
        ServiceBuilding building,
        float newMultiplier)
    {
        if (building != serviceBuilding)
            return;

        RefreshServiceInformation();
    }

    public void SetBuilding(WorkerAssignment newAssignment)
    {
        if (isActiveAndEnabled)
        {
            UnsubscribeFromBuildingEvents();
        }

        if (workerAssignment != newAssignment)
        {
            selectedWorker = null;
        }

        workerAssignment = newAssignment;

        serviceBuilding = workerAssignment != null
            ? workerAssignment.GetComponent<ServiceBuilding>()
            : null;

        if (isActiveAndEnabled)
        {
            SubscribeToBuildingEvents();
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

            item.SetSelected(item.Worker == selectedWorker);
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
                workerCountText.text = "-";

            RefreshServiceInformation();
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
                $"{workerAssignment.AssignedWorkerCount}/{workerAssignment.MaxWorkerSlots}";
        }

        RefreshServiceInformation();

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

    // SCRUM-24: Update operational state and speed.
    private void RefreshServiceInformation()
    {
        if (serviceBuilding == null)
        {
            if (serviceStatusText != null)
                serviceStatusText.text = "Status: N/A";

            if (serviceSpeedText != null)
                serviceSpeedText.text = "Speed: N/A";

            return;
        }

        if (serviceStatusText != null)
        {
            serviceStatusText.text =
                $"Status: {serviceBuilding.CurrentState}";

            switch (serviceBuilding.CurrentState)
            {
                case ServiceLoadState.Normal:
                    serviceStatusText.color = new Color(
                        0.27f, 0.73f, 0.46f
                    );
                    break;

                case ServiceLoadState.Overloaded:
                    serviceStatusText.color = new Color(
                        0.91f, 0.35f, 0.33f
                    );
                    break;

                case ServiceLoadState.Inactive:
                    serviceStatusText.color = new Color(
                        0.55f, 0.58f, 0.61f
                    );
                    break;
            }
        }

        if (serviceSpeedText != null)
        {
            if (!serviceBuilding.CanOperate)
            {
                serviceSpeedText.text = "Speed: 0.00x";
            }
            else
            {
                serviceSpeedText.text =
                    $"Speed: " +
                    $"{serviceBuilding.BuildingPerformanceMultiplier:0.00}x";
            }
        }
    }
}
