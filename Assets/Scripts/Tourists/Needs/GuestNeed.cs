
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GuestNeed
{
    [SerializeField] private GuestNeedType type;
    [SerializeField] private GuestNeedStatus status;

    [Header("Wave Progress")]
    [SerializeField] private int totalDemand;
    [SerializeField] private int waitingCount;
    [SerializeField] private int inServiceCount;
    [SerializeField] private int servedCount;

    [Header("Waiting Times (seconds)")]
    [SerializeField] private List<float> waitTimes = new();

    private Queue<float> waitingSince;

    // -1 = timer not running
    private float overThresholdSince = -1f;
    private float underThresholdSince = -1f;

    private Queue<float> WaitingSince
        => waitingSince ??= new Queue<float>();

    public GuestNeedType Type => type;
    public GuestNeedStatus Status => status;
    public int TotalDemand => totalDemand;
    public int WaitingCount => waitingCount;
    public int InServiceCount => inServiceCount;
    public int ServedCount => servedCount;
    public IReadOnlyList<float> WaitTimes => waitTimes;

    public bool HasDemand => totalDemand > 0;

    public bool IsFullyServed
        => HasDemand && servedCount == totalDemand;

    public float AverageWaitTime
    {
        get
        {
            if (waitTimes.Count == 0)
            {
                return 0f;
            }

            float total = 0f;

            foreach (float waitTime in waitTimes)
            {
                total += waitTime;
            }

            return total / waitTimes.Count;
        }
    }

    public GuestNeed(GuestNeedType type)
    {
        this.type = type;
    }

    public void AddDemand(int units, float currentTime)
    {
        if (units <= 0)
        {
            return;
        }

        for (int i = 0; i < units; i++)
        {
            WaitingSince.Enqueue(currentTime);
        }

        totalDemand += units;
        waitingCount = WaitingSince.Count;
    }

    public bool TryStartService(float currentTime)
    {
        return TryStartService(currentTime, out _);
    }

    // waitTime = seconds from arrival until the unit got a seat.
    public bool TryStartService(float currentTime, out float waitTime)
    {
        waitTime = 0f;

        if (WaitingSince.Count == 0)
        {
            return false;
        }

        float arrivalTime = WaitingSince.Dequeue();
        waitTime = Mathf.Max(0f, currentTime - arrivalTime);
        waitTimes.Add(waitTime);

        waitingCount = WaitingSince.Count;
        inServiceCount++;
        return true;
    }

    public bool CompleteService()
    {
        if (inServiceCount <= 0)
        {
            return false;
        }

        inServiceCount--;
        servedCount++;
        return true;
    }

    // Returns true if the status changed.
    public bool UpdateStatus(
        bool isUnlocked,
        float currentTime,
        int bottleneckThreshold,
        float hysteresis)
    {
        GuestNeedStatus newStatus = EvaluateStatus(
            isUnlocked,
            currentTime,
            bottleneckThreshold,
            hysteresis
        );

        if (newStatus == status)
        {
            return false;
        }

        status = newStatus;
        return true;
    }

    private GuestNeedStatus EvaluateStatus(
        bool isUnlocked,
        float currentTime,
        int bottleneckThreshold,
        float hysteresis)
    {
        if (!isUnlocked)
        {
            ResetStatusTimers();
            return GuestNeedStatus.Locked;
        }

        if (IsFullyServed)
        {
            ResetStatusTimers();
            return GuestNeedStatus.Satisfied;
        }

        // Only waiting units count, units in service do not.
        if (waitingCount > bottleneckThreshold)
        {
            underThresholdSince = -1f;

            if (overThresholdSince < 0f)
            {
                overThresholdSince = currentTime;
            }

            if (currentTime - overThresholdSince >= hysteresis)
            {
                return GuestNeedStatus.Bottleneck;
            }

            return status == GuestNeedStatus.Recovering
                ? GuestNeedStatus.Recovering
                : GuestNeedStatus.Ok;
        }

        overThresholdSince = -1f;

        if (status == GuestNeedStatus.Bottleneck)
        {
            underThresholdSince = currentTime;
            return GuestNeedStatus.Recovering;
        }

        if (status == GuestNeedStatus.Recovering)
        {
            if (underThresholdSince < 0f)
            {
                underThresholdSince = currentTime;
            }

            if (currentTime - underThresholdSince >= hysteresis)
            {
                return GuestNeedStatus.Ok;
            }

            return GuestNeedStatus.Recovering;
        }

        return GuestNeedStatus.Ok;
    }

    private void ResetStatusTimers()
    {
        overThresholdSince = -1f;
        underThresholdSince = -1f;
    }

    public void ResetForNewWave()
    {
        WaitingSince.Clear();
        waitTimes.Clear();
        ResetStatusTimers();

        totalDemand = 0;
        waitingCount = 0;
        inServiceCount = 0;
        servedCount = 0;
    }

    public override string ToString()
    {
        return $"{type.ToDisplayName()} [{status}]: " +
               $"{servedCount}/{totalDemand} " +
               $"(waiting {waitingCount}, in service {inServiceCount})";
    }
}
