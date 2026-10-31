
using System;
using UnityEngine;

public enum DayPhase
{
    Active,
    Passive
}

public class DayStateManager : MonoBehaviour
{
    public static DayStateManager Instance { get; private set; }

    public DayPhase CurrentPhase { get; private set; }
    public int CurrentDay { get; private set; }

    public bool IsActivePhase => CurrentPhase == DayPhase.Active;
    public bool IsPassivePhase => CurrentPhase == DayPhase.Passive;

    public event Action<DayPhase> OnPhaseChanged;
    public event Action<int> OnDayChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        CurrentDay = 1;
        StartActivePhase();
    }

    private void StartActivePhase()
    {
        CurrentPhase = DayPhase.Active;

        Debug.Log($"Day {CurrentDay}: Active Phase started!");

        OnDayChanged?.Invoke(CurrentDay);
        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    [ContextMenu("Test Passive Phase")]
    public void StartPassivePhase()
    {
        if (!IsActivePhase)
        {
            Debug.LogWarning("Cannot start Passive Phase!");
            return;
        }

        CurrentPhase = DayPhase.Passive;

        Debug.Log($"Day {CurrentDay}: Passive Phase started!");

        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    [ContextMenu("Test Next Day")]
    public void StartNextDay()
    {
        if (!IsPassivePhase)
        {
            Debug.LogWarning("Cannot start next day during Active Phase!");
            return;
        }

        CurrentDay++;
        StartActivePhase();
    }
}
