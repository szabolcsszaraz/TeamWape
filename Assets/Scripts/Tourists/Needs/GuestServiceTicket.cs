
using System;

// One guest unit waiting for / receiving a service. Lets a tourist
// know when the building has actually served it.
public class GuestServiceTicket
{
    public GuestNeedType NeedType { get; }
    public float ArrivalTime { get; }
    public float WaitTime { get; private set; }

    public bool IsInService { get; private set; }
    public bool IsServed { get; private set; }
    public bool IsCancelled { get; private set; }
    public bool IsFinished => IsServed || IsCancelled;

    public event Action<GuestServiceTicket> OnServiceStarted;
    public event Action<GuestServiceTicket> OnServed;
    public event Action<GuestServiceTicket> OnCancelled;

    public GuestServiceTicket(GuestNeedType needType, float arrivalTime)
    {
        NeedType = needType;
        ArrivalTime = arrivalTime;
    }

    public void StartService(float currentTime)
    {
        if (IsFinished || IsInService)
        {
            return;
        }

        WaitTime = Math.Max(0f, currentTime - ArrivalTime);
        IsInService = true;

        OnServiceStarted?.Invoke(this);
    }

    public void CompleteService()
    {
        if (IsFinished)
        {
            return;
        }

        IsInService = false;
        IsServed = true;

        OnServed?.Invoke(this);
    }

    public void Cancel()
    {
        if (IsFinished)
        {
            return;
        }

        IsInService = false;
        IsCancelled = true;

        OnCancelled?.Invoke(this);
    }
}
