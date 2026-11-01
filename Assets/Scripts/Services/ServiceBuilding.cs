
using System;
using System.Collections.Generic;
using UnityEngine;

public class ServiceBuilding : MonoBehaviour
{
    [Header("Service")]
    [SerializeField] private GuestNeedType serviceNeed = GuestNeedType.Rooms;

    [Tooltip("How many units can be served at the same time.")]
    [SerializeField, Min(1)] private int seats = 3;

    [Tooltip("Seconds to serve one unit with a worker assigned.")]
    [SerializeField, Min(0.1f)] private float serviceTime = 12f;

    [Header("Quality")]
    [Tooltip("Service quality (e.g. chef stars). Used by the rating later.")]
    [SerializeField, Range(1, 5)] private int qualityStars = 2;

    [Header("Worker")]
    [SerializeField] private bool hasWorker = true;

    [Tooltip("Service speed multiplier without a worker.")]
    [SerializeField, Range(0.01f, 1f)] private float noWorkerUtilization = 0.33f;

    [Header("Queue (optional)")]
    [Tooltip("Mirrors this need's waiting count, so QueueWarningUI works.")]
    [SerializeField] private ServiceQueue serviceQueue;

    [Header("Debug")]
    [SerializeField] private int occupiedSeats;
    [SerializeField] private int servedThisWave;

    private class ActiveService
    {
        public float RemainingTime;
        public float WaitTime;
    }

    private readonly List<ActiveService> activeServices = new();

    private GuestNeedsManager needsManager;

    public GuestNeedType ServiceNeed => serviceNeed;
    public int Seats => seats;
    public float ServiceTime => serviceTime;
    public int QualityStars => qualityStars;
    public bool HasWorker => hasWorker;
    public int OccupiedSeats => activeServices.Count;
    public int ServedThisWave => servedThisWave;

    public float Utilization => hasWorker ? 1f : noWorkerUtilization;

    // float = how long the served unit waited for a seat.
    public event Action<ServiceBuilding, float> OnUnitServed;

    private void Awake()
    {
        if (serviceQueue == null)
        {
            serviceQueue = GetComponent<ServiceQueue>();
        }
    }

    private void Start()
    {
        needsManager = GuestNeedsManager.Instance;

        if (needsManager == null)
        {
            Debug.LogError($"{name}: GuestNeedsManager not found!");
            enabled = false;
            return;
        }

        needsManager.UnlockNeed(serviceNeed);
        needsManager.OnNeedsReset += HandleNeedsReset;

        Debug.Log(
            $"{name}: serves {serviceNeed.ToDisplayName()} " +
            $"({seats} seats, {qualityStars} stars, " +
            $"worker: {(hasWorker ? "yes" : "no")})"
        );
    }

    private void Update()
    {
        GuestNeed need = needsManager.GetNeed(serviceNeed);

        UpdateActiveServices(need);
        SeatWaitingUnits(need);
        SyncServiceQueue(need);

        occupiedSeats = activeServices.Count;
    }

    private void UpdateActiveServices(GuestNeed need)
    {
        float progress = Time.deltaTime * Utilization;

        for (int i = activeServices.Count - 1; i >= 0; i--)
        {
            ActiveService service = activeServices[i];
            service.RemainingTime -= progress;

            if (service.RemainingTime > 0f)
            {
                continue;
            }

            activeServices.RemoveAt(i);

            if (!need.CompleteService())
            {
                continue;
            }

            servedThisWave++;

            Debug.Log(
                $"{name}: served 1 {serviceNeed.ToDisplayName()} " +
                $"({need.ServedCount}/{need.TotalDemand}, " +
                $"waited {service.WaitTime:0.0}s)"
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
        {
            return;
        }

        while (serviceQueue.WaitingGuestCount < need.WaitingCount)
        {
            serviceQueue.AddGuest();
        }

        while (serviceQueue.WaitingGuestCount > need.WaitingCount)
        {
            if (!serviceQueue.RemoveGuest())
            {
                break;
            }
        }
    }

    private void HandleNeedsReset()
    {
        activeServices.Clear();
        occupiedSeats = 0;
        servedThisWave = 0;
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
            $"{name}: worker {(hasWorker ? "assigned" : "removed")}, " +
            $"speed x{Utilization:0.00}"
        );
    }
}
