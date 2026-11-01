
using System;
using UnityEngine;

public class ServiceQueue : MonoBehaviour
{
    [Header("Queue Settings")]
    [SerializeField, Min(0)]
    private int warningLimit = 5;

    public int WarningLimit => warningLimit;

    public int WaitingGuestCount { get; private set; }

    public bool IsOverLimit => WaitingGuestCount > warningLimit;

    public event Action<int> OnQueueCountChanged;

    public event Action<bool> OnQueueWarningChanged;

    private bool wasOverLimit = false;

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

        CheckQueueLimit();
    }

    private void CheckQueueLimit()
    {
        bool isOverLimit = IsOverLimit;

        if (isOverLimit == wasOverLimit)
        {
            return;
        }

        wasOverLimit = isOverLimit;

        OnQueueWarningChanged?.Invoke(isOverLimit);

        if (isOverLimit)
        {
            Debug.LogWarning(
                $"{gameObject.name}: Queue warning! " +
                $"Waiting guests: {WaitingGuestCount}, Limit: {warningLimit}"
            );
        }
        else
        {
            Debug.Log(
                $"{gameObject.name}: Queue is back within the limit."
            );
        }
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
