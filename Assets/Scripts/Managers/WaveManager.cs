
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    public int CurrentWave { get; private set; }
    public bool IsWaveActive { get; private set; }

    private int lastWaveDay = -1;
    private Coroutine spawnCoroutine;

    public event Action<int> OnWaveStarted;

    [Header("Tourist Spawn Points")]
    [SerializeField] private List<TouristSpawnPoint> spawnPoints = new();

    public IReadOnlyList<TouristSpawnPoint> SpawnPoints => spawnPoints;

    [Header("Tourist Settings")]
    [SerializeField] private GameObject touristPrefab;
    [SerializeField] private int touristsPerWave = 6;
    [SerializeField] private float spawnInterval = 1f;

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

        if (DayStateManager.Instance != null)
        {
            DayStateManager.Instance.OnPhaseChanged += HandlePhaseChanged;
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
            Debug.LogWarning("Cannot start wave during Passive Phase!");
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

        CurrentWave++;
        lastWaveDay = currentDay;
        IsWaveActive = true;

        Debug.Log($"Day {currentDay}: Wave {CurrentWave} started!");

        OnWaveStarted?.Invoke(CurrentWave);

        spawnCoroutine = StartCoroutine(SpawnTourists());
    }

    private IEnumerator SpawnTourists()
    {
        for (int i = 0; i < touristsPerWave; i++)
        {
            if (!IsWaveActive)
            {
                break;
            }

            TouristSpawnPoint spawnPoint =
                spawnPoints[i % spawnPoints.Count];

            GameObject tourist = Instantiate(
                touristPrefab,
                spawnPoint.SpawnPosition,
                spawnPoint.SpawnRotation
            );

            tourist.name = $"Tourist_{CurrentWave}_{i + 1}";

            Debug.Log(
                $"Tourist {i + 1} spawned at {spawnPoint.name}"
            );

            if (i < touristsPerWave - 1)
            {
                yield return new WaitForSeconds(
                    Mathf.Max(0f, spawnInterval)
                );
            }
        }

        spawnCoroutine = null;

        if (IsWaveActive)
        {
            Debug.Log("All tourists have spawned!");
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
            DayStateManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
