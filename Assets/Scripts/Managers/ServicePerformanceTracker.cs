
using System;
using System.Collections.Generic;
using UnityEngine;

public class ServicePerformanceTracker : MonoBehaviour
{
    [Serializable]
    public class ServiceUsageData
    {
        public GuestNeedType serviceType;

        [Header("Requests")]
        public int totalRequests;
        public int successfulRequests;
        public int failedRequests;
        public int pendingRequests;

        [Header("Waiting Statistics")]
        public float averageWaitTime;
        [Range(0f, 100f)]
        public float waitingSatisfaction;

        [HideInInspector]
        public float totalWaitTime;

        [HideInInspector]
        public float totalWaitingScore;
    }

    [Header("Current Wave Statistics")]
    [SerializeField]
    private List<ServiceUsageData> serviceUsage = new();

    private readonly Dictionary<GuestNeedType, int>
        previousDemand = new();

    private readonly List<ServiceBuilding>
        trackedBuildings = new();

    private GuestNeedsManager needsManager;
    private WaveManager waveManager;
    private DayStateManager dayStateManager;
    private GuestSatisfactionManager satisfactionManager;

    private bool isTrackingWave;

    public IReadOnlyList<ServiceUsageData> ServiceUsage
        => serviceUsage;

    private void Start()
    {
        needsManager = GuestNeedsManager.Instance;
        waveManager = WaveManager.Instance;
        dayStateManager = DayStateManager.Instance;
        satisfactionManager = GuestSatisfactionManager.Instance;

        if (needsManager == null ||
            waveManager == null ||
            dayStateManager == null)
        {
            Debug.LogError(
                "ServicePerformanceTracker: Required manager missing!"
            );

            enabled = false;
            return;
        }

        if (satisfactionManager == null)
        {
            Debug.LogWarning(
                "ServicePerformanceTracker: " +
                "GuestSatisfactionManager not found. " +
                "Waiting satisfaction will not be calculated."
            );
        }

        InitializeStatistics();
        SubscribeToBuildings();

        needsManager.OnDemandAdded += HandleDemandAdded;
        needsManager.OnNeedsReset += HandleNeedsReset;

        waveManager.OnWaveStarted += HandleWaveStarted;
        dayStateManager.OnPhaseChanged += HandlePhaseChanged;

        isTrackingWave = waveManager.IsWaveActive;
    }

    private void InitializeStatistics()
    {
        serviceUsage.Clear();
        previousDemand.Clear();

        foreach (GuestNeed need in needsManager.Needs)
        {
            serviceUsage.Add(new ServiceUsageData
            {
                serviceType = need.Type
            });

            previousDemand[need.Type] = need.TotalDemand;
        }
    }

    private void SubscribeToBuildings()
    {
        ServiceBuilding[] buildings =
            FindObjectsByType<ServiceBuilding>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (ServiceBuilding building in buildings)
        {
            if (building == null)
                continue;

            building.OnUnitServed += HandleUnitServed;
            trackedBuildings.Add(building);
        }
    }

    private void HandleWaveStarted(int waveNumber)
    {
        isTrackingWave = true;

        foreach (ServiceUsageData data in serviceUsage)
        {
            data.totalRequests = 0;
            data.successfulRequests = 0;
            data.failedRequests = 0;
            data.pendingRequests = 0;

            data.averageWaitTime = 0f;
            data.waitingSatisfaction = 0f;

            data.totalWaitTime = 0f;
            data.totalWaitingScore = 0f;

            previousDemand[data.serviceType] = 0;
        }

        Debug.Log(
            $"ServicePerformanceTracker: Wave {waveNumber} started."
        );
    }

    private void HandleNeedsReset()
    {
        // Synchronize with GuestNeedsManager's wave reset.
        foreach (ServiceUsageData data in serviceUsage)
        {
            GuestNeed need =
                needsManager.GetNeed(data.serviceType);

            previousDemand[data.serviceType] =
                need != null ? need.TotalDemand : 0;
        }
    }

    private void HandleDemandAdded(GuestNeed need)
    {
        if (!isTrackingWave || need == null)
            return;

        int previous = previousDemand.TryGetValue(
            need.Type,
            out int value
        ) ? value : 0;

        int newRequests = Mathf.Max(
            0,
            need.TotalDemand - previous
        );

        previousDemand[need.Type] = need.TotalDemand;

        ServiceUsageData data = GetServiceData(need.Type);

        if (data == null)
            return;

        data.totalRequests += newRequests;
        UpdatePendingRequests(data);
    }

    private void HandleUnitServed(
        ServiceBuilding building,
        float waitTime)
    {
        if (!isTrackingWave || building == null)
            return;

        ServiceUsageData data =
            GetServiceData(building.ServiceNeed);

        if (data == null)
            return;

        data.successfulRequests++;

        // Waiting time until the guest received a service seat.
        float safeWaitTime = Mathf.Max(0f, waitTime);
        data.totalWaitTime += safeWaitTime;

        data.averageWaitTime =
            data.totalWaitTime / data.successfulRequests;

        // Reuse the game's existing waiting score formula.
        if (satisfactionManager != null)
        {
            float waitScore =
                satisfactionManager.CalculateWaitScore(
                    safeWaitTime
                );

            data.totalWaitingScore += waitScore;

            data.waitingSatisfaction =
                data.totalWaitingScore /
                data.successfulRequests * 100f;
        }

        UpdatePendingRequests(data);
    }

    private void UpdatePendingRequests(ServiceUsageData data)
    {
        data.pendingRequests = Mathf.Max(
            0,
            data.totalRequests - data.successfulRequests
        );
    }

    private void HandlePhaseChanged(DayPhase phase)
    {
        if (phase != DayPhase.Passive || !isTrackingWave)
            return;

        isTrackingWave = false;

        foreach (ServiceUsageData data in serviceUsage)
        {
            UpdatePendingRequests(data);

            data.failedRequests = data.pendingRequests;
            data.pendingRequests = 0;

            Debug.Log(
                $"{data.serviceType} Wave Result: " +
                $"Total={data.totalRequests}, " +
                $"Successful={data.successfulRequests}, " +
                $"Failed={data.failedRequests}, " +
                $"AverageWait={data.averageWaitTime:0.0}s, " +
                $"WaitingSatisfaction=" +
                $"{data.waitingSatisfaction:0.0}%"
            );
        }
    }

    private ServiceUsageData GetServiceData(GuestNeedType type)
    {
        foreach (ServiceUsageData data in serviceUsage)
        {
            if (data.serviceType == type)
                return data;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (needsManager != null)
        {
            needsManager.OnDemandAdded -= HandleDemandAdded;
            needsManager.OnNeedsReset -= HandleNeedsReset;
        }

        if (waveManager != null)
        {
            waveManager.OnWaveStarted -= HandleWaveStarted;
        }

        if (dayStateManager != null)
        {
            dayStateManager.OnPhaseChanged -= HandlePhaseChanged;
        }

        foreach (ServiceBuilding building in trackedBuildings)
        {
            if (building != null)
            {
                building.OnUnitServed -= HandleUnitServed;
            }
        }

        trackedBuildings.Clear();
    }
}
