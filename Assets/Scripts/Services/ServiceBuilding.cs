
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

    [SerializeField, Min(1)]
    private int seats = 3;

    [SerializeField, Min(0.1f)]
    private float serviceTime = 12f;

    [Header("Quality")]
    [SerializeField, Range(1, 5)]
    private int qualityStars = 2;

    [Header("Worker Requirements")]
    [SerializeField]
    private bool requiresWorkers = true;

    [Header("Worker - Legacy Settings")]
    [SerializeField]
    private bool hasWorker = true;

    [SerializeField, Range(0.01f, 1f)]
    private float noWorkerUtilization = 0.33f;

    [Header("Worker Performance")]
    [SerializeField, Min(0f)]
    private float additionalWorkerBonus = 0.5f;

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

    [Header("Performance Debug")]
    [SerializeField] private float buildingPerformanceMultiplier;
    [SerializeField] private float effectiveServiceTime;

    private class ActiveService
    {
        public float RemainingTime;
        public float WaitTime;
    }

    private readonly List<ActiveService> activeServices = new();

    private readonly HashSet<WorkerData> observedWorkers = new();

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

    public float BuildingPerformanceMultiplier =>
        buildingPerformanceMultiplier;

    public float EffectiveServiceTime => effectiveServiceTime;

    public float Utilization => buildingPerformanceMultiplier;

    public event Action<ServiceBuilding, float> OnUnitServed;

    public event Action<ServiceBuilding, ServiceLoadState>
        OnServiceStateChanged;

    public event Action<ServiceBuilding, float>
        OnPerformanceChanged;

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

        UnsubscribeFromWorkers();
    }

    private void HandleWorkersChanged(WorkerAssignment assignment)
    {
        if (assignment != workerAssignment)
            return;

        RefreshWorkerRequirement();
    }

    private void RefreshWorkerRequirement()
    {
        bool wasOperating = canOperate;

        assignedWorkerCount = workerAssignment != null
            ? workerAssignment.AssignedWorkerCount
            : 0;

        isMissingRequiredWorkers =
            requiresWorkers && assignedWorkerCount == 0;

        canOperate = !isMissingRequiredWorkers;

        RefreshWorkerSubscriptions();
        RecalculateBuildingPerformance();
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

    // SCRUM-24: Subscribe to changes in workers'
    // individual performance levels.
    private void RefreshWorkerSubscriptions()
    {
        UnsubscribeFromWorkers();

        if (workerAssignment == null || !isActiveAndEnabled)
            return;

        foreach (WorkerData worker in workerAssignment.AssignedWorkers)
        {
            if (worker == null)
                continue;

            if (observedWorkers.Add(worker))
            {
                worker.OnPerformanceLevelChanged +=
                    HandleWorkerPerformanceChanged;
            }
        }
    }

    private void UnsubscribeFromWorkers()
    {
        foreach (WorkerData worker in observedWorkers)
        {
            if (worker != null)
            {
                worker.OnPerformanceLevelChanged -=
                    HandleWorkerPerformanceChanged;
            }
        }

        observedWorkers.Clear();
    }

    private void HandleWorkerPerformanceChanged(
        WorkerData worker,
        int newLevel)
    {
        if (workerAssignment == null ||
            !workerAssignment.ContainsWorker(worker))
        {
            return;
        }

        RecalculateBuildingPerformance();
    }

    // SCRUM-24: Recalculate service speed based on
    // assigned workers and their performance levels.
    private void RecalculateBuildingPerformance()
    {
        float previousMultiplier = buildingPerformanceMultiplier;

        if (!canOperate)
        {
            buildingPerformanceMultiplier = 0f;
        }
        else if (assignedWorkerCount == 0)
        {
            // Buildings that do not require assigned workers
            // retain their previous standalone behavior.
            buildingPerformanceMultiplier =
                hasWorker ? 1f : noWorkerUtilization;
        }
        else
        {
            float performanceBonus = 0f;
            int validWorkerCount = 0;

            foreach (WorkerData worker in
                     workerAssignment.AssignedWorkers)
            {
                if (worker == null)
                    continue;

                validWorkerCount++;

                performanceBonus +=
                    worker.PerformanceMultiplier - 1f;
            }

            if (validWorkerCount == 0)
            {
                buildingPerformanceMultiplier = 0f;
            }
            else
            {
                float workerCountBonus =
                    Mathf.Max(0, validWorkerCount - 1) *
                    additionalWorkerBonus;

                buildingPerformanceMultiplier =
                    1f + workerCountBonus + performanceBonus;
            }
        }

        effectiveServiceTime =
            buildingPerformanceMultiplier > 0f
                ? serviceTime / buildingPerformanceMultiplier
                : 0f;

        if (!Mathf.Approximately(
                previousMultiplier,
                buildingPerformanceMultiplier))
        {
            Debug.Log(
                $"{name}: Performance recalculated. " +
                $"Workers: {assignedWorkerCount}, " +
                $"Speed: x{buildingPerformanceMultiplier:0.00}, " +
                $"Service time: {effectiveServiceTime:0.00}s",
                this
            );

            OnPerformanceChanged?.Invoke(
                this,
                buildingPerformanceMultiplier
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
            $"speed: x{buildingPerformanceMultiplier:0.00})",
            this
        );
    }

    private void Update()
    {
        if (needsManager == null)
            return;

        GuestNeed need = needsManager.GetNeed(serviceNeed);

        if (canOperate)
        {
            UpdateActiveServices(need);
            SeatWaitingUnits(need);
        }

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

    private void UpdateCurrentServiceState()
    {
        ServiceLoadState newState;

        if (!canOperate)
        {
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

        UpdateCurrentServiceState();
    }

    private void OnDestroy()
    {
        if (needsManager != null)
        {
            needsManager.OnNeedsReset -= HandleNeedsReset;
        }

        UnsubscribeFromWorkers();
    }

    [ContextMenu("Test / Toggle Worker")]
    private void TestToggleWorker()
    {
        hasWorker = !hasWorker;

        RefreshWorkerRequirement();

        Debug.Log(
            $"{name}: legacy worker flag " +
            $"{(hasWorker ? "enabled" : "disabled")}, " +
            $"speed x{Utilization:0.00}",
            this
        );
    }
}
