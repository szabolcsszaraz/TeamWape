
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    public int CurrentWave { get; private set; }
    public bool IsWaveActive { get; private set; }
    public bool IsArrivalComplete { get; private set; }

    private int lastWaveDay = -1;
    private int spawnedTourists = 0;
    private Coroutine spawnCoroutine;

    public event Action<int> OnWaveStarted;
    public event Action<int> OnWaveArrivalCompleted;

    [Header("Tourist Spawn Points")]
    [SerializeField]
    private List<TouristSpawnPoint> spawnPoints = new();

    public IReadOnlyList<TouristSpawnPoint> SpawnPoints
        => spawnPoints;

    [Header("Tourist Settings")]
    [SerializeField] private GameObject touristPrefab;
    [SerializeField, Min(1)] private int touristsPerWave = 6;
    [SerializeField, Min(0f)] private float spawnInterval = 1f;

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
        CurrentWave = 0;
        IsWaveActive = false;
        IsArrivalComplete = false;

        if (DayStateManager.Instance != null)
        {
            DayStateManager.Instance.OnPhaseChanged
                += HandlePhaseChanged;
        }

        Debug.Log("WaveManager initialized successfully!");
    }

    [ContextMenu("Test Start Wave")]
    public void StartWave()
    {
        if (DayStateManager.Instance == null)
        {
            Debug.LogWarning("DayStateManager not found!");
            return;
        }

        if (!DayStateManager.Instance.IsActivePhase)
        {
            Debug.LogWarning(
                "Cannot start wave during Passive Phase!"
            );
            return;
        }

        int currentDay = DayStateManager.Instance.CurrentDay;

        if (lastWaveDay == currentDay)
        {
            Debug.LogWarning("Wave already started today!");
            return;
        }

        if (touristPrefab == null)
        {
            Debug.LogWarning("Tourist Prefab is missing!");
            return;
        }

        if (spawnPoints.Count == 0 ||
            spawnPoints.Exists(point => point == null))
        {
            Debug.LogWarning("Tourist spawn points are missing!");
            return;
        }

        if (touristsPerWave <= 0)
        {
            Debug.LogWarning(
                "Tourists Per Wave must be greater than zero!"
            );
            return;
        }

        CurrentWave++;
        lastWaveDay = currentDay;

        IsWaveActive = true;
        IsArrivalComplete = false;
        spawnedTourists = 0;

        Debug.Log(
            $"Day {currentDay}: Wave {CurrentWave} started!"
        );

        OnWaveStarted?.Invoke(CurrentWave);

        spawnCoroutine = StartCoroutine(SpawnTourists());
    }

    private IEnumerator SpawnTourists()
    {
        for (int i = 0; i < touristsPerWave; i++)
        {
            if (!IsWaveActive)
            {
                spawnCoroutine = null;
                yield break;
            }

            TouristSpawnPoint spawnPoint =
                spawnPoints[i % spawnPoints.Count];

            GameObject tourist = Instantiate(
                touristPrefab,
                spawnPoint.SpawnPosition,
                spawnPoint.SpawnRotation
            );

            tourist.name =
                $"Tourist_{CurrentWave}_{i + 1}";

            spawnedTourists++;

            Debug.Log(
                $"Tourist {spawnedTourists}/{touristsPerWave} " +
                $"spawned at {spawnPoint.name}"
            );

            if (i < touristsPerWave - 1)
            {
                yield return new WaitForSeconds(
                    Mathf.Max(0f, spawnInterval)
                );
            }
        }

        spawnCoroutine = null;

        if (IsWaveActive &&
            !IsArrivalComplete &&
            spawnedTourists == touristsPerWave)
        {
            IsArrivalComplete = true;

            Debug.Log(
                $"Wave {CurrentWave}: " +
                "All tourists have arrived!"
            );

            OnWaveArrivalCompleted?.Invoke(CurrentWave);
        }
    }

    private void HandlePhaseChanged(DayPhase phase)
    {
        if (phase == DayPhase.Passive)
        {
            IsWaveActive = false;

            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            Debug.Log("Wave ended!");
        }
    }

    private void OnDestroy()
    {
        if (DayStateManager.Instance != null)
        {
            DayStateManager.Instance.OnPhaseChanged
                -= HandlePhaseChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
