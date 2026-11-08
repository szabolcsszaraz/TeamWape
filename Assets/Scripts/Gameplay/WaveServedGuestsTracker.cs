
using System;
using System.Collections.Generic;
using UnityEngine;

public class WaveServedGuestsTracker : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private int totalServedThisWave;

    [SerializeField] private int finalServedGuests;

    private readonly List<ServiceBuilding> observedBuildings = new();

    private WaveManager waveManager;
    private DayStateManager dayStateManager;

    public int TotalServedThisWave => totalServedThisWave;
    public int FinalServedGuests => finalServedGuests;

    public event Action<int> OnTotalServedChanged;
    public event Action<int> OnWaveResultFinalized;

    private void Start()
    {
        SubscribeToServiceBuildings();

        waveManager = WaveManager.Instance;
        dayStateManager = DayStateManager.Instance;

        if (waveManager != null)
        {
            waveManager.OnWaveStarted += HandleWaveStarted;
        }
        else
        {
            Debug.LogWarning(
                "WaveServedGuestsTracker: WaveManager not found!",
                this
            );
        }

        if (dayStateManager != null)
        {
            dayStateManager.OnPhaseChanged += HandlePhaseChanged;
        }
        else
        {
            Debug.LogWarning(
                "WaveServedGuestsTracker: DayStateManager not found!",
                this
            );
        }
    }

    private void SubscribeToServiceBuildings()
    {
        ServiceBuilding[] buildings =
            FindObjectsByType<ServiceBuilding>(
                FindObjectsSortMode.None
            );

        foreach (ServiceBuilding building in buildings)
        {
            if (building == null)
                continue;

            if (observedBuildings.Contains(building))
                continue;

            observedBuildings.Add(building);
            building.OnUnitServed += HandleUnitServed;
        }

        Debug.Log(
            $"WaveServedGuestsTracker: Tracking " +
            $"{observedBuildings.Count} service buildings.",
            this
        );
    }

    private void HandleWaveStarted(int waveNumber)
    {
        ResetCounter();

        Debug.Log(
            $"Wave {waveNumber}: Served guests counter reset.",
            this
        );
    }

    private void HandleUnitServed(
        ServiceBuilding building,
        float waitTime)
    {
        if (waveManager == null || !waveManager.IsWaveActive)
            return;

        totalServedThisWave++;

        OnTotalServedChanged?.Invoke(totalServedThisWave);
    }

    private void HandlePhaseChanged(DayPhase phase)
    {
        if (phase != DayPhase.Passive)
            return;

        // Finalize the wave result when the day ends.
        FinalizeWaveResult();
    }

    private void FinalizeWaveResult()
    {
        finalServedGuests = totalServedThisWave;

        OnWaveResultFinalized?.Invoke(finalServedGuests);

        Debug.Log(
            $"Wave {waveManager?.CurrentWave}: " +
            $"Final served guests = {finalServedGuests}",
            this
        );
    }

    public void ResetCounter()
    {
        totalServedThisWave = 0;
        finalServedGuests = 0;

        OnTotalServedChanged?.Invoke(totalServedThisWave);
    }

    private void OnDestroy()
    {
        foreach (ServiceBuilding building in observedBuildings)
        {
            if (building != null)
            {
                building.OnUnitServed -= HandleUnitServed;
            }
        }

        observedBuildings.Clear();

        if (waveManager != null)
        {
            waveManager.OnWaveStarted -= HandleWaveStarted;
        }

        if (dayStateManager != null)
        {
            dayStateManager.OnPhaseChanged -= HandlePhaseChanged;
        }
    }
}
