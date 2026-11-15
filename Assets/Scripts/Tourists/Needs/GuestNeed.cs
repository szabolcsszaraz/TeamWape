
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GuestNeed
{
    [SerializeField] private GuestNeedType type;
    [SerializeField] private GuestNeedStatus status;

    [Header("Wave Progress")]
    [Tooltip("All units this wave (on the way + waiting + in service + done).")]
    [SerializeField] private int totalDemand;

    [Tooltip("Tourists walking towards a building for this need.")]
    [SerializeField] private int onTheWayCount;

    [Tooltip("Tourists inside the building, waiting for a free seat.")]
    [SerializeField] private int waitingCount;

    [SerializeField] private int inServiceCount;
    [SerializeField] private int servedCount;

    [Tooltip("Units that gave up (no reachable building, waited too long).")]
    [SerializeField] private int abandonedCount;

    [Header("Waiting Times (seconds)")]
    [SerializeField] private List<float> waitTimes = new();

    private List<GuestServiceTicket> waitingTickets;
    private List<GuestServiceTicket> inServiceTickets;

    // -1 = timer not running
    private float overThresholdSince = -1f;
    private float underThresholdSince = -1f;

    private List<GuestServiceTicket> WaitingTickets
        => waitingTickets ??= new List<GuestServiceTicket>();

    private List<GuestServiceTicket> InServiceTickets
        => inServiceTickets ??= new List<GuestServiceTicket>();

    public GuestNeedType Type => type;
    public GuestNeedStatus Status => status;
    public int TotalDemand => totalDemand;
    public int OnTheWayCount => onTheWayCount;
    public int WaitingCount => waitingCount;
    public int InServiceCount => inServiceCount;
    public int ServedCount => servedCount;
    public int AbandonedCount => abandonedCount;
    public IReadOnlyList<float> WaitTimes => waitTimes;

    public bool HasDemand => totalDemand > 0;

    // Every unit is either served or gave up.
    public bool IsFullyServed
        => HasDemand && servedCount + abandonedCount >= totalDemand;

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

    // A tourist spawned with this need and is on its way.
    // Counts as demand right away (for rating/performance).
    public void ReserveDemand(int units)
    {
        if (units <= 0)
        {
            return;
        }

        totalDemand += units;
        onTheWayCount += units;
    }

    // A reserved unit will never arrive (no reachable building,
    // tourist removed).
    public void AbandonReserved(int units = 1)
    {
        int abandoned = Mathf.Min(units, onTheWayCount);

        if (abandoned <= 0)
        {
            return;
        }

        onTheWayCount -= abandoned;
        abandonedCount += abandoned;
    }

    // A tourist arrived at the building and joins its queue.
    public GuestServiceTicket ArriveForService(float currentTime)
    {
        if (onTheWayCount > 0)
        {
            onTheWayCount--;
        }
        else
        {
            // Arrived without a reservation (e.g. after a wave reset).
            totalDemand++;
        }

        GuestServiceTicket ticket = new GuestServiceTicket(type, currentTime);
        WaitingTickets.Add(ticket);
        waitingCount = WaitingTickets.Count;

        return ticket;
    }

    // Demand that is already waiting at the building (test helper).
    public void AddDemand(int units, float currentTime)
    {
        if (units <= 0)
        {
            return;
        }

        for (int i = 0; i < units; i++)
        {
            WaitingTickets.Add(new GuestServiceTicket(type, currentTime));
        }

        totalDemand += units;
        waitingCount = WaitingTickets.Count;
    }

    public bool TryStartService(float currentTime)
    {
        return TryStartService(currentTime, out GuestServiceTicket _);
    }

    // waitTime = seconds from arrival until the unit got a seat.
    public bool TryStartService(float currentTime, out float waitTime)
    {
        bool started = TryStartService(currentTime, out GuestServiceTicket ticket);
        waitTime = started ? ticket.WaitTime : 0f;
        return started;
    }

    public bool TryStartService(float currentTime, out GuestServiceTicket ticket)
    {
        ticket = null;

        if (WaitingTickets.Count == 0)
        {
            return false;
        }

        ticket = WaitingTickets[0];
        WaitingTickets.RemoveAt(0);
        waitingCount = WaitingTickets.Count;

        ticket.StartService(currentTime);
        waitTimes.Add(ticket.WaitTime);

        InServiceTickets.Add(ticket);
        inServiceCount = InServiceTickets.Count;
        return true;
    }

    public bool CompleteService(GuestServiceTicket ticket)
    {
        if (ticket == null || !InServiceTickets.Remove(ticket))
        {
            return false;
        }

        inServiceCount = InServiceTickets.Count;
        servedCount++;

        // Notifies the tourist that its need is met.
        ticket.CompleteService();
        return true;
    }

    // Completes the oldest unit in service (test helper).
    public bool CompleteService()
    {
        if (InServiceTickets.Count == 0)
        {
            return false;
        }

        return CompleteService(InServiceTickets[0]);
    }

    // A waiting tourist gives up. Units already in service
    // cannot be cancelled.
    public bool CancelWaiting(GuestServiceTicket ticket)
    {
        if (ticket == null || !WaitingTickets.Remove(ticket))
        {
            return false;
        }

        waitingCount = WaitingTickets.Count;
        abandonedCount++;

        ticket.Cancel();
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
        // Copy first: cancelling notifies tourists, who may react.
        List<GuestServiceTicket> openTickets = new(WaitingTickets);
        openTickets.AddRange(InServiceTickets);

        WaitingTickets.Clear();
        InServiceTickets.Clear();
        waitTimes.Clear();
        ResetStatusTimers();

        totalDemand = 0;
        onTheWayCount = 0;
        waitingCount = 0;
        inServiceCount = 0;
        servedCount = 0;
        abandonedCount = 0;

        foreach (GuestServiceTicket ticket in openTickets)
        {
            ticket.Cancel();
        }
    }

    public override string ToString()
    {
        return $"{type.ToDisplayName()} [{status}]: " +
               $"{servedCount}/{totalDemand} " +
               $"(on the way {onTheWayCount}, waiting {waitingCount}, " +
               $"in service {inServiceCount}, gave up {abandonedCount})";
    }
}
