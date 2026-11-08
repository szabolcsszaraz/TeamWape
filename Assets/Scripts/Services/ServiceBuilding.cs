
using System;
using System.Collections.Generic;
using UnityEngine;

public enum ServiceLoadState
{
    Normal,
    Overloaded,
    Inactive
}

public class ServiceBuilding : MonoBehaviour
{
    [Header("Service")]
    [SerializeField]
    private GuestNeedType serviceNeed = GuestNeedType.Rooms;

    [Tooltip("How many units can be served at the same time.")]
    [SerializeField, Min(1)]
    private int seats = 3;

    [Tooltip("Seconds to serve one unit.")]
    [SerializeField, Min(0.1f)]
    private float serviceTime = 12f;

    [Header("Quality")]
    [SerializeField, Range(1, 5)]
    private int qualityStars = 2;

    [Header("Worker Requirements")]
    [Tooltip("Requires at least one assigned worker to operate.")]
    [SerializeField]
    private bool requiresWorkers = true;

    [Header("Worker - Legacy Settings")]
    [SerializeField]
    private bool hasWorker = true;

    [SerializeField, Range(0.01f, 1f)]
    private float noWorkerUtilization = 0.33f;

    [Header("Queue (optional)")]
    [SerializeField]
    private ServiceQueue serviceQueue;

    [Header("Debug")]
    [SerializeField] private int occupiedSeats;
    [SerializeField] private int servedThisWave;

    [Header("Load Debug")]
    [SerializeField] private int waitingRequests;
    [SerializeField] private int totalLoad;
    [SerializeField] private float loadRatio;

    [Header("Overload Debug")]
    [SerializeField] private bool isOverloaded;

    [Header("Service State")]
    [SerializeField]
    private ServiceLoadState currentState =
        ServiceLoadState.Normal;

    [Header("Worker Requirement Debug")]
    [SerializeField] private int assignedWorkerCount;
    [SerializeField] private bool isMissingRequiredWorkers;
    [SerializeField] private bool canOperate;

    private class ActiveService
    {
        public float RemainingTime;
        public float WaitTime;
    }

    private readonly List<ActiveService> activeServices = new();

    private GuestNeedsManager needsManager;
    private WorkerAssignment workerAssignment;

    public GuestNeedType ServiceNeed => serviceNeed;
    public int Seats => seats;
    public float ServiceTime => serviceTime;
    public int QualityStars => qualityStars;

    public bool RequiresWorkers => requiresWorkers;
    public bool HasWorker => hasWorker;

    public int AssignedWorkerCount => assignedWorkerCount;
    public bool IsMissingRequiredWorkers =>
        isMissingRequiredWorkers;

    public bool CanOperate => canOperate;

    public int OccupiedSeats => activeServices.Count;
    public int ServedThisWave => servedThisWave;

    public int WaitingRequests => waitingRequests;
    public int TotalLoad => totalLoad;
    public float LoadRatio => loadRatio;

    public bool IsOverloaded => isOverloaded;
    public ServiceLoadState CurrentState => currentState;

    // Retained for compatibility with existing service logic.
    public float Utilization =>
        hasWorker ? 1f : noWorkerUtilization;

    public event Action<ServiceBuilding, float> OnUnitServed;

    public event Action<ServiceBuilding, ServiceLoadState>
        OnServiceStateChanged;

    private void Awake()
    {
        if (serviceQueue == null)
        {
            serviceQueue = GetComponent<ServiceQueue>();
        }

        workerAssignment = GetComponent<WorkerAssignment>();

        if (requiresWorkers && workerAssignment == null)
        {
            Debug.LogWarning(
                $"{name}: Requires Workers is enabled, " +
                "but WorkerAssignment is missing!",
                this
            );
        }

        RefreshWorkerRequirement();
    }

    private void OnEnable()
    {
        if (workerAssignment == null)
        {
            workerAssignment = GetComponent<WorkerAssignment>();
        }

        if (workerAssignment != null)
        {
            workerAssignment.OnWorkersChanged -= HandleWorkersChanged;
            workerAssignment.OnWorkersChanged += HandleWorkersChanged;
        }

        RefreshWorkerRequirement();
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

        RefreshWorkerRequirement();
    }

    // SCRUM-23: Check whether workers are available.
    private void RefreshWorkerRequirement()
    {
        bool wasOperating = canOperate;

        assignedWorkerCount = workerAssignment != null
            ? workerAssignment.AssignedWorkerCount
            : 0;

        isMissingRequiredWorkers =
            requiresWorkers && assignedWorkerCount == 0;

        canOperate = !isMissingRequiredWorkers;

        // Immediately update the visual state when workers change.
        UpdateCurrentServiceState();

        if (wasOperating != canOperate)
        {
            Debug.Log(
                $"{name}: Service operation " +
                (canOperate ? "RESUMED" : "PAUSED") +
                $" (Workers: {assignedWorkerCount})",
                this
            );
        }
    }

    private void Start()
    {
        needsManager = GuestNeedsManager.Instance;

        if (needsManager == null)
        {
            Debug.LogError(
                $"{name}: GuestNeedsManager not found!",
                this
            );

            enabled = false;
            return;
        }

        needsManager.UnlockNeed(serviceNeed);
        needsManager.OnNeedsReset += HandleNeedsReset;

        UpdateCurrentServiceState();

        Debug.Log(
            $"{name}: serves {serviceNeed.ToDisplayName()} " +
            $"({seats} seats, {qualityStars} stars, " +
            $"workers: {assignedWorkerCount}, " +
            $"can operate: {canOperate})",
            this
        );
    }

    private void Update()
    {
        if (needsManager == null)
            return;

        GuestNeed need = needsManager.GetNeed(serviceNeed);

        // Do not process services if required workers are missing.
        if (canOperate)
        {
            UpdateActiveServices(need);
            SeatWaitingUnits(need);
        }

        // Keep queue information updated even while inactive.
        SyncServiceQueue(need);

        occupiedSeats = activeServices.Count;

        CalculateServiceLoad(need);
        UpdateCurrentServiceState();
    }

    private void CalculateServiceLoad(GuestNeed need)
    {
        waitingRequests = need.WaitingCount;
        totalLoad = occupiedSeats + waitingRequests;

        loadRatio = seats > 0
            ? (float)totalLoad / seats
            : 0f;
    }

    // SCRUM-23: Determine the state by priority.
    private void UpdateCurrentServiceState()
    {
        ServiceLoadState newState;

        if (!canOperate)
        {
            // Inactive takes priority over overloaded.
            newState = ServiceLoadState.Inactive;
        }
        else if (totalLoad > seats)
        {
            newState = ServiceLoadState.Overloaded;
        }
        else
        {
            newState = ServiceLoadState.Normal;
        }

        UpdateServiceState(newState);
    }

    private void UpdateServiceState(ServiceLoadState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;

        isOverloaded =
            currentState == ServiceLoadState.Overloaded;

        OnServiceStateChanged?.Invoke(this, currentState);

        Debug.Log(
            $"{name}: Service state changed to {currentState}. " +
            $"Load: {totalLoad}, Capacity: {seats}, " +
            $"Workers: {assignedWorkerCount}",
            this
        );
    }

    private void UpdateActiveServices(GuestNeed need)
    {
        float progress = Time.deltaTime * Utilization;

        for (int i = activeServices.Count - 1; i >= 0; i--)
        {
            ActiveService service = activeServices[i];
            service.RemainingTime -= progress;

            if (service.RemainingTime > 0f)
                continue;

            activeServices.RemoveAt(i);

            if (!need.CompleteService())
                continue;

            servedThisWave++;

            Debug.Log(
                $"{name}: served 1 {serviceNeed.ToDisplayName()} " +
                $"({need.ServedCount}/{need.TotalDemand}, " +
                $"waited {service.WaitTime:0.0}s)",
                this
            );

            GuestSatisfactionManager.Instance?.RecordService(
                serviceNeed,
                service.WaitTime,
                qualityStars
            );

            OnUnitServed?.Invoke(this, service.WaitTime);
        }
    }

    private void SeatWaitingUnits(GuestNeed need)
    {
        while (activeServices.Count < seats &&
               need.TryStartService(Time.time, out float waitTime))
        {
            activeServices.Add(new ActiveService
            {
                RemainingTime = serviceTime,
                WaitTime = waitTime
            });
        }
    }

    private void SyncServiceQueue(GuestNeed need)
    {
        if (serviceQueue == null)
            return;

        while (serviceQueue.WaitingGuestCount < need.WaitingCount)
        {
            serviceQueue.AddGuest();
        }

        while (serviceQueue.WaitingGuestCount > need.WaitingCount)
        {
            if (!serviceQueue.RemoveGuest())
                break;
        }
    }

    private void HandleNeedsReset()
    {
        activeServices.Clear();

        occupiedSeats = 0;
        servedThisWave = 0;

        waitingRequests = 0;
        totalLoad = 0;
        loadRatio = 0f;

        // Preserve Inactive if the building has no workers.
        UpdateCurrentServiceState();
    }

    private void OnDestroy()
    {
        if (needsManager != null)
        {
            needsManager.OnNeedsReset -= HandleNeedsReset;
        }
    }

    [ContextMenu("Test / Toggle Worker")]
    private void TestToggleWorker()
    {
        hasWorker = !hasWorker;

        Debug.Log(
            $"{name}: legacy worker flag " +
            $"{(hasWorker ? "enabled" : "disabled")}, " +
            $"speed x{Utilization:0.00}",
            this
        );
    }
}
