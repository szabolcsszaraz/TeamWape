
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

    [Tooltip("Random spread of each service's time.")]
    [SerializeField, Range(0f, 0.9f)]
    private float serviceTimeVariance = 0.33f;

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

    [Header("Energy Boost")]
    [SerializeField, Min(0.1f)]
    private float energyBoostDuration = 30f;

    [SerializeField, Min(1f)]
    private float energyBoostMultiplier = 1.5f;

    [Header("Energy Boost Gem Cost")]
    [Tooltip("How many seconds of Energy Boost duration one Gem pays for.")]
    [SerializeField, Min(0.1f)]
    private float energyBoostSecondsPerGem = 10f;

    [Header("Energy Boost Debug")]
    [SerializeField]
    private bool isEnergyBoostActive;

    [SerializeField]
    private float energyBoostRemainingTime;

    // SCRUM-27: Track the last day Energy Boost was used.
    [Header("Daily Energy Boost Usage")]
    [SerializeField]
    private int lastEnergyBoostUsedDay = -1;

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
        public GuestServiceTicket Ticket;
    }

    private readonly List<ActiveService> activeServices = new();

    private readonly HashSet<WorkerData> observedWorkers = new();

    private GuestNeedsManager needsManager;
    private BuildingRepair buildingRepair;
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

    public bool IsEnergyBoostActive => isEnergyBoostActive;
    public float EnergyBoostDuration => energyBoostDuration;
    public float EnergyBoostMultiplier => energyBoostMultiplier;

    public int EnergyBoostGemCost
    {
        get
        {
            double cost = Math.Ceiling(
                (double)energyBoostDuration /
                Math.Max(0.1f, energyBoostSecondsPerGem)
            );
            return cost >= int.MaxValue ? int.MaxValue : (int)cost;
        }
    }

    public bool CanActivateEnergyBoost =>
        isActiveAndEnabled &&
        canOperate &&
        !isEnergyBoostActive &&
        DayStateManager.Instance != null &&
        DayStateManager.Instance.CurrentDay > 0 &&
        !HasUsedEnergyBoostToday;

    public float EnergyBoostRemainingTime =>
        energyBoostRemainingTime;

    // SCRUM-27: Daily usage information.
    public int LastEnergyBoostUsedDay =>
        lastEnergyBoostUsedDay;

    public bool HasUsedEnergyBoostToday
    {
        get
        {
            DayStateManager dayManager =
                DayStateManager.Instance;

            if (dayManager == null || dayManager.CurrentDay < 1)
                return false;

            return lastEnergyBoostUsedDay ==
                   dayManager.CurrentDay;
        }
    }

    public event Action<ServiceBuilding, float> OnUnitServed;

    public event Action<ServiceBuilding> OnBuildingStatsChanged;

    public event Action<ServiceBuilding, ServiceLoadState>
        OnServiceStateChanged;

    public event Action<ServiceBuilding, float>
        OnPerformanceChanged;

    public event Action<ServiceBuilding, bool>
        OnEnergyBoostStateChanged;

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

        isEnergyBoostActive = false;
        energyBoostRemainingTime = 0f;

        // No boost has been used in this game session.
        lastEnergyBoostUsedDay = -1;

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

        StopEnergyBoost();
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

    private void RecalculateBuildingPerformance()
    {
        float previousMultiplier = buildingPerformanceMultiplier;
        float baseWorkerMultiplier = 0f;

        if (!canOperate)
        {
            baseWorkerMultiplier = 0f;
        }
        else if (assignedWorkerCount == 0)
        {
            baseWorkerMultiplier =
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
                baseWorkerMultiplier = 0f;
            }
            else
            {
                float workerCountBonus =
                    Mathf.Max(0, validWorkerCount - 1) *
                    additionalWorkerBonus;

                baseWorkerMultiplier =
                    1f + workerCountBonus + performanceBonus;
            }
        }

        float activeBoostMultiplier = isEnergyBoostActive
            ? energyBoostMultiplier
            : 1f;

        buildingPerformanceMultiplier =
            baseWorkerMultiplier * activeBoostMultiplier;

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
                $"Energy Boost: {(isEnergyBoostActive ? "ON" : "OFF")}, " +
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

        // A ruined building does not offer its need until it is repaired
        // (e.g. no Relax guests until the Pool is repaired).
        buildingRepair = GetComponentInParent<BuildingRepair>();

        if (buildingRepair != null && !buildingRepair.IsRepaired)
        {
            buildingRepair.OnRepaired += HandleBuildingRepaired;

            Debug.Log(
                $"{name}: {serviceNeed.ToDisplayName()} need stays locked " +
                "until the building is repaired.",
                this
            );
        }
        else
        {
            needsManager.UnlockNeed(serviceNeed);
        }

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
        UpdateEnergyBoost();

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

    // Compatibility entry point: do not allow free activation.
    // Update BuildingWorkersUI to call the paid method after confirmation.
    public bool TryActivateEnergyBoost()
    {
        Debug.LogWarning(
            $"{name}: Free Energy Boost activation is disabled. " +
            "Use TryActivateEnergyBoostWithGems instead.",
            this
        );
        return false;
    }

    // The only gameplay entry point for Energy Boost activation.
    // Validates eligibility and balance BEFORE spending Gems.
    public bool TryActivateEnergyBoostWithGems(PlayerGemsManager gemsManager)
    {
        if (!CanActivateEnergyBoost)
        {
            Debug.LogWarning(
                $"{name}: Energy Boost is unavailable or already used today.",
                this
            );
            return false;
        }

        if (gemsManager == null)
        {
            Debug.LogError($"{name}: PlayerGemsManager missing!", this);
            return false;
        }

        int cost = EnergyBoostGemCost;
        if (!gemsManager.HasEnoughGems(cost))
        {
            Debug.LogWarning(
                $"{name}: Insufficient Gems for Energy Boost " +
                $"({gemsManager.CurrentGems}/{cost}).",
                this
            );
            return false;
        }

        if (!gemsManager.TrySpendGems(cost))
            return false;

        // No normal failure path remains after validated payment.
        bool activated = ActivateEnergyBoostAfterPayment();
        if (!activated)
        {
            // Defensive fallback in case eligibility changed unexpectedly.
            gemsManager.AddGems(cost);
            Debug.LogError(
                $"{name}: Energy Boost failed after payment; refunded {cost} Gems.",
                this
            );
            return false;
        }

        Debug.Log($"{name}: Energy Boost purchased for {cost} Gems.", this);
        return true;
    }

    private bool ActivateEnergyBoostAfterPayment()
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning(
                $"{name}: Energy Boost cannot start " +
                "because the building is disabled.",
                this
            );

            return false;
        }

        if (!canOperate)
        {
            Debug.LogWarning(
                $"{name}: Energy Boost cannot start " +
                "because the building is inactive.",
                this
            );

            return false;
        }

        if (isEnergyBoostActive)
        {
            Debug.LogWarning(
                $"{name}: Energy Boost is already active.",
                this
            );

            return false;
        }

        DayStateManager dayManager =
            DayStateManager.Instance;

        if (dayManager == null || dayManager.CurrentDay < 1)
        {
            Debug.LogWarning(
                $"{name}: Cannot activate Energy Boost. " +
                "A valid game day is not available.",
                this
            );

            return false;
        }

        // SCRUM-27 SUBTASK 2:
        // Reject a second activation on the same game day.
        if (HasUsedEnergyBoostToday)
        {
            Debug.LogWarning(
                $"{name}: Energy Boost already used on " +
                $"Day {dayManager.CurrentDay}. " +
                "Available again on the next game day.",
                this
            );

            return false;
        }

        // SCRUM-27: Record the successful activation day.
        lastEnergyBoostUsedDay = dayManager.CurrentDay;

        Debug.Log(
            $"{name}: Energy Boost usage recorded for " +
            $"Day {lastEnergyBoostUsedDay}.",
            this
        );

        isEnergyBoostActive = true;
        energyBoostRemainingTime = energyBoostDuration;

        RecalculateBuildingPerformance();

        OnEnergyBoostStateChanged?.Invoke(this, true);

        Debug.Log(
            $"{name}: Energy Boost activated! " +
            $"Multiplier: x{energyBoostMultiplier:0.00}, " +
            $"Duration: {energyBoostDuration:0.0}s",
            this
        );

        return true;
    }

    private void UpdateEnergyBoost()
    {
        if (!isEnergyBoostActive)
            return;

        energyBoostRemainingTime -= Time.deltaTime;

        if (energyBoostRemainingTime > 0f)
            return;

        StopEnergyBoost();
    }

    private void StopEnergyBoost()
    {
        if (!isEnergyBoostActive)
        {
            energyBoostRemainingTime = 0f;
            return;
        }

        isEnergyBoostActive = false;
        energyBoostRemainingTime = 0f;

        RecalculateBuildingPerformance();

        OnEnergyBoostStateChanged?.Invoke(this, false);

        Debug.Log(
            $"{name}: Energy Boost finished. " +
            $"Normal speed restored: " +
            $"x{buildingPerformanceMultiplier:0.00}",
            this
        );
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

            if (!need.CompleteService(service.Ticket))
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
               need.TryStartService(
                   Time.time,
                   out GuestServiceTicket ticket))
        {
            float variance = UnityEngine.Random.Range(
                1f - serviceTimeVariance,
                1f + serviceTimeVariance
            );

            activeServices.Add(new ActiveService
            {
                RemainingTime = serviceTime * variance,
                WaitTime = ticket.WaitTime,
                Ticket = ticket
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

    private void HandleBuildingRepaired(BuildingRepair repair)
    {
        repair.OnRepaired -= HandleBuildingRepaired;

        if (needsManager != null)
        {
            needsManager.UnlockNeed(serviceNeed);
        }
    }

    // False while the building is still a ruin.
    public bool IsOpenForGuests =>
        buildingRepair == null || buildingRepair.IsRepaired;

    private void OnDestroy()
    {
        if (needsManager != null)
        {
            needsManager.OnNeedsReset -= HandleNeedsReset;
        }

        if (buildingRepair != null)
        {
            buildingRepair.OnRepaired -= HandleBuildingRepaired;
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

    [ContextMenu("Test / Activate Energy Boost")]
    private void TestActivateEnergyBoost()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Energy Boost can only be tested in Play Mode.",
                this
            );

            return;
        }

        TryActivateEnergyBoost();
    }

    public void ApplyUpgradeStats(
        int newCapacity,
        int newQualityStars,
        float newServiceTime)
    {
        seats = Mathf.Max(1, newCapacity);

        qualityStars = Mathf.Clamp(newQualityStars, 1, 5);

        serviceTime = Mathf.Max(0.1f, newServiceTime);

        RecalculateBuildingPerformance();

        if (needsManager != null)
        {
            GuestNeed need = needsManager.GetNeed(serviceNeed);

            if (need != null)
            {
                occupiedSeats = activeServices.Count;
                CalculateServiceLoad(need);
            }
        }

        UpdateCurrentServiceState();

        OnBuildingStatsChanged?.Invoke(this);

        Debug.Log(
            $"{name}: Building statistics updated. " +
            $"Capacity: {seats}, " +
            $"Quality: {qualityStars}, " +
            $"Service time: {serviceTime}s"
        );
    }

    private void OnValidate()
    {
        energyBoostDuration = Mathf.Max(
            0.1f,
            energyBoostDuration
        );

        energyBoostMultiplier = Mathf.Max(
            1f,
            energyBoostMultiplier
        );

        energyBoostSecondsPerGem = Mathf.Max(
            0.1f,
            energyBoostSecondsPerGem
        );
    }
}
