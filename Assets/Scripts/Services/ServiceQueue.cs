
using System;
using UnityEngine;

public class ServiceQueue : MonoBehaviour
{
    public int WaitingGuestCount { get; private set; }

    public event Action<int> OnQueueCountChanged;

    public void AddGuest()
    {
        WaitingGuestCount++;
        NotifyQueueChanged();
    }

    public bool RemoveGuest()
    {
        if (WaitingGuestCount <= 0)
        {
            return false;
        }

        WaitingGuestCount--;
        NotifyQueueChanged();
        return true;
    }

    private void NotifyQueueChanged()
    {
        OnQueueCountChanged?.Invoke(WaitingGuestCount);

        Debug.Log(
            $"{gameObject.name}: {WaitingGuestCount} guests waiting."
        );
    }

    [ContextMenu("Test / Add Guest")]
    private void TestAddGuest()
    {
        AddGuest();
    }

    [ContextMenu("Test / Remove Guest")]
    private void TestRemoveGuest()
    {
        RemoveGuest();
    }
}
