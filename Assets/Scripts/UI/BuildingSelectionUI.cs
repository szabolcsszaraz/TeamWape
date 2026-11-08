
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BuildingSelectionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private GameObject buildingWorkersPanel;
    [SerializeField] private BuildingWorkersUI buildingWorkersUI;

    [Header("Selection")]
    [SerializeField] private LayerMask buildingRaycastMask = ~0;
    [SerializeField] private float maxRayDistance = 1000f;

    [Header("Worker Transfer Debug")]
    [SerializeField] private WorkerData selectedWorker;
    [SerializeField] private WorkerAssignment sourceBuilding;
    [SerializeField] private WorkerAssignment targetBuilding;

    private WorkerAssignment selectedBuilding;

    private void Start()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (buildingWorkersUI == null &&
            buildingWorkersPanel != null)
        {
            buildingWorkersUI =
                buildingWorkersPanel.GetComponent<BuildingWorkersUI>();
        }

        if (buildingWorkersUI != null)
        {
            buildingWorkersUI.OnWorkerSelected +=
                HandleWorkerSelected;
        }

        if (buildingWorkersPanel != null)
        {
            buildingWorkersPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (buildingWorkersUI != null)
        {
            buildingWorkersUI.OnWorkerSelected -=
                HandleWorkerSelected;
        }
    }

    private void Update()
    {
        if (!TryGetPointerDown(out Vector2 screenPosition))
        {
            return;
        }

        if (IsPointerOverUI(screenPosition))
        {
            return;
        }

        SelectBuildingAt(screenPosition);
    }

    private bool TryGetPointerDown(out Vector2 position)
    {
        position = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            position =
                Touchscreen.current.primaryTouch.position.ReadValue();

            return true;
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            position = Mouse.current.position.ReadValue();

            return true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0 &&
            Input.GetTouch(0).phase == TouchPhase.Began)
        {
            position = Input.GetTouch(0).position;
            return true;
        }

        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            return true;
        }
#endif

        return false;
    }

    private bool IsPointerOverUI(Vector2 position)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerData =
            new PointerEventData(EventSystem.current)
            {
                position = position
            };

        List<RaycastResult> results = new();

        EventSystem.current.RaycastAll(pointerData, results);

        return results.Count > 0;
    }

    private void SelectBuildingAt(Vector2 position)
    {
        if (worldCamera == null)
        {
            return;
        }

        Ray ray = worldCamera.ScreenPointToRay(position);

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            maxRayDistance,
            buildingRaycastMask))
        {
            WorkerAssignment building =
                hit.collider.GetComponentInParent<WorkerAssignment>();

            if (building != null)
            {
                SelectBuilding(building);
                return;
            }
        }

        ClosePanel();
    }

    public void SelectBuilding(WorkerAssignment building)
    {
        if (building == null)
        {
            return;
        }

        // SCRUM-19: Transfer selected worker
        // when a destination building is clicked.
        if (selectedWorker != null)
        {
            SelectTransferTarget(building);
            return;
        }

        ShowBuildingPanel(building);
    }

    private void ShowBuildingPanel(WorkerAssignment building)
    {
        selectedBuilding = building;

        if (buildingWorkersPanel != null)
        {
            buildingWorkersPanel.SetActive(true);
        }

        if (buildingWorkersUI != null)
        {
            buildingWorkersUI.SetBuilding(building);
        }
    }

    private void HandleWorkerSelected(WorkerData worker)
    {
        if (worker == null)
        {
            return;
        }

        WorkerAssignment currentBuilding =
            WorkerAssignment.GetWorkerBuilding(worker);

        if (currentBuilding == null)
        {
            Debug.LogWarning(
                $"{worker.WorkerName}: Source building not found!",
                this
            );

            CancelTransfer();
            return;
        }

        selectedWorker = worker;
        sourceBuilding = currentBuilding;
        targetBuilding = null;

        Debug.Log(
            $"Transfer started: {worker.WorkerName} " +
            $"from {sourceBuilding.name}. " +
            "Select a destination building.",
            this
        );
    }

    private void SelectTransferTarget(WorkerAssignment building)
    {
        if (selectedWorker == null || sourceBuilding == null)
        {
            CancelTransfer();
            return;
        }

        if (building == sourceBuilding)
        {
            Debug.LogWarning(
                "Cannot transfer a worker to the same building.",
                this
            );

            return;
        }

        targetBuilding = building;

        TransferSelectedWorker();
    }

    // SCRUM-19: Transfer worker between buildings.
    private void TransferSelectedWorker()
    {
        if (selectedWorker == null ||
            sourceBuilding == null ||
            targetBuilding == null)
        {
            return;
        }

        // Verify that the worker is still assigned
        // to the original building.
        if (!sourceBuilding.ContainsWorker(selectedWorker))
        {
            Debug.LogWarning(
                $"{selectedWorker.WorkerName} is no longer " +
                $"assigned to {sourceBuilding.name}.",
                this
            );

            CancelTransfer();
            return;
        }

        // Do not remove the worker if the target is full.
        if (!targetBuilding.HasAvailableSlot)
        {
            Debug.LogWarning(
                $"{targetBuilding.name}: No available worker slots! " +
                $"({targetBuilding.AssignedWorkerCount}/" +
                $"{targetBuilding.MaxWorkerSlots})",
                this
            );

            targetBuilding = null;
            return;
        }

        WorkerData workerToMove = selectedWorker;

        WorkerAssignment originalBuilding = sourceBuilding;
        WorkerAssignment destinationBuilding = targetBuilding;

        // Remove worker from the original building.
        bool removed = originalBuilding.RemoveWorker(workerToMove);

        if (!removed)
        {
            Debug.LogWarning(
                $"Failed to remove {workerToMove.WorkerName} " +
                $"from {originalBuilding.name}.",
                this
            );

            CancelTransfer();
            return;
        }

        // Add worker to the destination building.
        bool added = destinationBuilding.AddWorker(workerToMove);

        if (!added)
        {
            // Restore the original assignment
            // if the destination rejected the worker.
            bool restored = originalBuilding.AddWorker(workerToMove);

            if (!restored)
            {
                Debug.LogError(
                    $"Transfer failed and could not restore " +
                    $"{workerToMove.WorkerName} to " +
                    $"{originalBuilding.name}!",
                    this
                );
            }

            CancelTransfer();
            return;
        }

        Debug.Log(
            $"Worker transferred successfully: " +
            $"{workerToMove.WorkerName} " +
            $"from {originalBuilding.name} " +
            $"to {destinationBuilding.name}.",
            this
        );

        // Finish the transfer.
        CancelTransfer();

        // Automatically display workers
        // assigned to the destination building.
        ShowBuildingPanel(destinationBuilding);
    }

    public void CancelTransfer()
    {
        selectedWorker = null;
        sourceBuilding = null;
        targetBuilding = null;
    }

    public void ClosePanel()
    {
        CancelTransfer();

        selectedBuilding = null;

        if (buildingWorkersPanel != null)
        {
            buildingWorkersPanel.SetActive(false);
        }
    }
}
