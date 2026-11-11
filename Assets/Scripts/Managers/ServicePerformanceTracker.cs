
using System;
using System.Collections.Generic;
using UnityEngine;

public class ServicePerformanceTracker : MonoBehaviour
{
    [Serializable]
    public class ServiceUsageData
    {
        public GuestNeedType serviceType;
        public int totalRequests;
    }

    [Header("Current Wave Statistics")]
    [SerializeField]
    private List<ServiceUsageData> serviceUsage = new();

    private readonly Dictionary<GuestNeedType, int>
        previousDemand = new();

    private GuestNeedsManager needsManager;
    private WaveManager waveManager;

    public IReadOnlyList<ServiceUsageData> ServiceUsage
        => serviceUsage;

    private void Start()
    {
        needsManager = GuestNeedsManager.Instance;
        waveManager = WaveManager.Instance;

        if (needsManager == null || waveManager == null)
        {
            Debug.LogError(
                "ServicePerformanceTracker: " +
                "Required manager not found!"
            );

            enabled = false;
            return;
        }

        InitializeStatistics();

        needsManager.OnDemandAdded += HandleDemandAdded;
        waveManager.OnWaveStarted += HandleWaveStarted;
    }

    private void InitializeStatistics()
    {
        serviceUsage.Clear();
        previousDemand.Clear();

        foreach (GuestNeed need in needsManager.Needs)
        {
            serviceUsage.Add(new ServiceUsageData
            {
                serviceType = need.Type,
                totalRequests = 0
            });

            previousDemand[need.Type] = need.TotalDemand;
        }
    }

    private void HandleWaveStarted(int waveNumber)
    {
        foreach (ServiceUsageData data in serviceUsage)
        {
            data.totalRequests = 0;
            previousDemand[data.serviceType] = 0;
        }

        Debug.Log(
            $"ServicePerformanceTracker: " +
            $"Statistics reset for Wave {waveNumber}."
        );
    }

    private void HandleDemandAdded(GuestNeed need)
    {
        if (waveManager == null ||
            !waveManager.IsWaveActive)
        {
            return;
        }

        int previous = previousDemand.TryGetValue(
            need.Type,
            out int value
        ) ? value : 0;

        int newRequests = Mathf.Max(
            0,
            need.TotalDemand - previous
        );

        previousDemand[need.Type] = need.TotalDemand;

        foreach (ServiceUsageData data in serviceUsage)
        {
            if (data.serviceType != need.Type)
            {
                continue;
            }

            data.totalRequests += newRequests;

            Debug.Log(
                $"{need.Type}: " +
                $"{data.totalRequests} requests this wave."
            );

            break;
        }
    }

    private void OnDestroy()
    {
        if (needsManager != null)
        {
            needsManager.OnDemandAdded -= HandleDemandAdded;
        }

        if (waveManager != null)
        {
            waveManager.OnWaveStarted -= HandleWaveStarted;
        }
    }
}
