
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
        if (assignment == workerAssignment)
        {
            RefreshUI();
        }
    }

    public void SetBuilding(WorkerAssignment newAssignment)
    {
        if (workerAssignment != null && isActiveAndEnabled)
        {
            workerAssignment.OnWorkersChanged -= HandleWorkersChanged;
        }

        workerAssignment = newAssignment;

        if (workerAssignment != null && isActiveAndEnabled)
        {
            workerAssignment.OnWorkersChanged += HandleWorkersChanged;
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        if (workersList != null)
        {
            for (int i = workersList.childCount - 1; i >= 0; i--)
            {
                Destroy(workersList.GetChild(i).gameObject);
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

            GameObject item = Instantiate(
                workerListItemPrefab,
                workersList
            );

            TMP_Text[] texts =
                item.GetComponentsInChildren<TMP_Text>();

            if (texts.Length >= 2)
            {
                texts[0].text = worker.WorkerName;
                texts[1].text = $"Lv. {worker.PerformanceLevel}";
            }
        }
    }
}
