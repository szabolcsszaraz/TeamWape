
using System;
using System.Collections.Generic;
using UnityEngine;

public class GuestNeedsManager : MonoBehaviour
{
    public static GuestNeedsManager Instance { get; private set; }

    // Filled by ServiceBuildings in the scene (a need is only
    // unlocked when a building can serve it).
    [Header("Unlocked Needs")]
    [SerializeField]
    private List<GuestNeedType> unlockedNeedTypes = new();

    [Header("Need Status")]
    [Tooltip("Need becomes a bottleneck when MORE than this many units are waiting.")]
    [SerializeField, Min(0)] private int bottleneckThreshold = 4;

    [Tooltip("Seconds the queue must stay over/under the threshold before the status changes.")]
    [SerializeField, Min(0f)] private float bottleneckHysteresis = 3f;

    [Header("Wave Needs")]
    [SerializeField]
    private List<GuestNeed> needs = new();

    public IReadOnlyList<GuestNeed> Needs => needs;

    public IReadOnlyList<GuestNeedType> UnlockedNeedTypes
        => unlockedNeedTypes;

    public event Action<GuestNeed> OnDemandAdded;
    public event Action<GuestNeed> OnNeedStatusChanged;
    public event Action OnNeedsReset;

    private WaveManager waveManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        CreateNeeds();
    }

    private void Start()
    {
        waveManager = WaveManager.Instance;

        if (waveManager != null)
        {
            waveManager.OnWaveStarted += HandleWaveStarted;
        }
        else
        {
            Debug.LogError("GuestNeedsManager: WaveManager not found!");
        }
    }

    private void CreateNeeds()
    {
        needs.Clear();

        GuestNeedType[] needTypes =
            (GuestNeedType[])Enum.GetValues(typeof(GuestNeedType));

        foreach (GuestNeedType needType in needTypes)
        {
            GuestNeed need = new GuestNeed(needType);

            // Set the starting status silently (no event).
            need.UpdateStatus(
                IsUnlocked(needType),
                Time.time,
                bottleneckThreshold,
                bottleneckHysteresis
            );

            needs.Add(need);
        }
    }

    private void Update()
    {
        foreach (GuestNeed need in needs)
        {
            bool changed = need.UpdateStatus(
                IsUnlocked(need.Type),
                Time.time,
                bottleneckThreshold,
                bottleneckHysteresis
            );

            if (changed)
            {
                Debug.Log(
                    $"{need.Type.ToDisplayName()} status: {need.Status} " +
                    $"(waiting {need.WaitingCount})"
                );

                OnNeedStatusChanged?.Invoke(need);
            }
        }
    }

    public GuestNeedStatus GetStatus(GuestNeedType type)
    {
        return GetNeed(type).Status;
    }

    public GuestNeed GetNeed(GuestNeedType type)
    {
        return needs.Find(need => need.Type == type);
    }

    public bool IsUnlocked(GuestNeedType type)
    {
        return unlockedNeedTypes.Contains(type);
    }

    public void UnlockNeed(GuestNeedType type)
    {
        if (IsUnlocked(type))
        {
            return;
        }

        unlockedNeedTypes.Add(type);

        Debug.Log($"{type.ToDisplayName()} need unlocked!");
    }

    public void AddDemand(GuestNeedType type, int units)
    {
        if (!IsUnlocked(type))
        {
            Debug.LogWarning(
                $"Cannot add demand for locked need: {type.ToDisplayName()}"
            );
            return;
        }

        GuestNeed need = GetNeed(type);
        need.AddDemand(units, Time.time);

        OnDemandAdded?.Invoke(need);
    }

    private void HandleWaveStarted(int waveNumber)
    {
        foreach (GuestNeed need in needs)
        {
            need.ResetForNewWave();
        }

        Debug.Log($"Wave {waveNumber}: Guest needs reset.");

        OnNeedsReset?.Invoke();
    }

    private void OnDestroy()
    {
        if (waveManager != null)
        {
            waveManager.OnWaveStarted -= HandleWaveStarted;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    [ContextMenu("Test / Log Needs")]
    private void TestLogNeeds()
    {
        foreach (GuestNeed need in needs)
        {
            Debug.Log(need.ToString());
        }
    }

    // Temporary helpers until SCRUM-88 buildings serve needs.
    [ContextMenu("Test / Add 5 Demand To Unlocked Needs")]
    private void TestAddDemand()
    {
        foreach (GuestNeedType needType in unlockedNeedTypes)
        {
            AddDemand(needType, 5);
        }

        TestLogNeeds();
    }

    [ContextMenu("Test / Serve 3 Units Per Need")]
    private void TestServeUnits()
    {
        foreach (GuestNeed need in needs)
        {
            for (int i = 0; i < 3; i++)
            {
                if (!need.TryStartService(Time.time))
                {
                    break;
                }

                need.CompleteService();
            }
        }

        TestLogNeeds();
    }
}
