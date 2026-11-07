
using System;
using System.Collections.Generic;
using UnityEngine;

public class WorkerAssignment : MonoBehaviour
{
    [Header("Worker Slots")]
    [SerializeField, Min(1)]
    private int maxWorkerSlots = 3;

    [Header("Assigned Workers (Debug)")]
    [SerializeField]
    private List<WorkerData> assignedWorkers = new();

    [Header("Testing")]
    [SerializeField] private WorkerData testWorker;

    public IReadOnlyList<WorkerData> AssignedWorkers =>
        assignedWorkers;

    public int AssignedWorkerCount =>
        assignedWorkers.Count;

    public int MaxWorkerSlots => maxWorkerSlots;

    public int AvailableSlots =>
        Mathf.Max(0, maxWorkerSlots - assignedWorkers.Count);

    public bool HasAvailableSlot =>
        assignedWorkers.Count < maxWorkerSlots;

    public event Action<WorkerAssignment> OnWorkersChanged;

    public bool ContainsWorker(WorkerData worker)
    {
        return worker != null &&
               assignedWorkers.Contains(worker);
    }

    // Finds the building currently assigned to a worker.
    public static WorkerAssignment GetWorkerBuilding(
        WorkerData worker)
    {
        if (worker == null)
            return null;

        WorkerAssignment[] buildings =
            FindObjectsByType<WorkerAssignment>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (WorkerAssignment building in buildings)
        {
            if (building.ContainsWorker(worker))
                return building;
        }

        return null;
    }

    public bool AddWorker(WorkerData worker)
    {
        if (worker == null)
        {
            Debug.LogWarning(
                $"{name}: Cannot assign a null worker.",
                this
            );
            return false;
        }

        if (ContainsWorker(worker))
        {
            Debug.LogWarning(
                $"{name}: {worker.WorkerName} is already assigned here.",
                this
            );
            return false;
        }

        if (!HasAvailableSlot)
        {
            Debug.LogWarning(
                $"{name}: No available worker slots! " +
                $"({AssignedWorkerCount}/{maxWorkerSlots})",
                this
            );
            return false;
        }

        WorkerAssignment currentBuilding =
            GetWorkerBuilding(worker);

        if (currentBuilding != null)
        {
            Debug.LogWarning(
                $"{worker.WorkerName} is already assigned to " +
                $"{currentBuilding.name}. Remove the worker first.",
                this
            );
            return false;
        }

        assignedWorkers.Add(worker);

        OnWorkersChanged?.Invoke(this);

        Debug.Log(
            $"{name}: Assigned {worker.WorkerName}. " +
            $"Workers: {AssignedWorkerCount}/{maxWorkerSlots}",
            this
        );

        return true;
    }

    public bool RemoveWorker(WorkerData worker)
    {
        if (worker == null ||
            !assignedWorkers.Remove(worker))
        {
            return false;
        }

        OnWorkersChanged?.Invoke(this);

        Debug.Log(
            $"{name}: Removed {worker.WorkerName}. " +
            $"Workers: {AssignedWorkerCount}/{maxWorkerSlots}",
            this
        );

        return true;
    }

    public void RemoveAllWorkers()
    {
        if (assignedWorkers.Count == 0)
            return;

        assignedWorkers.Clear();

        OnWorkersChanged?.Invoke(this);

        Debug.Log(
            $"{name}: All workers removed.",
            this
        );
    }

    private void OnValidate()
    {
        maxWorkerSlots = Mathf.Max(1, maxWorkerSlots);
    }

    [ContextMenu("Test / Assign Worker")]
    private void TestAssignWorker()
    {
        AddWorker(testWorker);
    }

    [ContextMenu("Test / Remove Worker")]
    private void TestRemoveWorker()
    {
        RemoveWorker(testWorker);
    }

    [ContextMenu("Test / Remove All Workers")]
    private void TestRemoveAllWorkers()
    {
        RemoveAllWorkers();
    }
}
