
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

    private WorkerAssignment selectedBuilding;

    private void Start()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (buildingWorkersUI == null &&
            buildingWorkersPanel != null)
        {
            buildingWorkersUI =
                buildingWorkersPanel.GetComponent<BuildingWorkersUI>();
        }

        if (buildingWorkersPanel != null)
            buildingWorkersPanel.SetActive(false);
    }

    private void Update()
    {
        if (!TryGetPointerDown(out Vector2 screenPosition))
            return;

        if (IsPointerOverUI(screenPosition))
            return;

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
            return false;

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
            return;

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
        if (building == null ||
            buildingWorkersPanel == null ||
            buildingWorkersUI == null)
        {
            return;
        }

        selectedBuilding = building;

        buildingWorkersPanel.SetActive(true);
        buildingWorkersUI.SetBuilding(building);
    }

    public void ClosePanel()
    {
        selectedBuilding = null;

        if (buildingWorkersPanel != null)
            buildingWorkersPanel.SetActive(false);
    }
}
