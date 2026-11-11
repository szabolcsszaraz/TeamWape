
using System;
using TMPro;
using UnityEngine;

public class PlayerCoinsManager : MonoBehaviour
{
    [Header("Player Coins")]
    [SerializeField, Min(0)]
    private int startingCoins = 2500;

    [SerializeField, Min(0)]
    private int currentCoins;

    [Header("UI References")]
    [SerializeField]
    private TMP_Text coinsText;

    [Header("Last Reward")]
    [SerializeField]
    private int lastRewardedWave = -1;

    [SerializeField, Min(0)]
    private int lastEarnedCoins;

    private WaveRewardManager rewardManager;

    public int CurrentCoins => currentCoins;
    public int LastEarnedCoins => lastEarnedCoins;

    public event Action<int> OnCoinsChanged;

    private void Awake()
    {
        currentCoins = Mathf.Max(0, startingCoins);
        UpdateCoinsUI();
    }

    private void Start()
    {
        rewardManager =
            FindFirstObjectByType<WaveRewardManager>();

        if (rewardManager == null)
        {
            Debug.LogError(
                "PlayerCoinsManager: WaveRewardManager not found!"
            );

            enabled = false;
            return;
        }

        rewardManager.OnRewardCalculated += HandleRewardCalculated;

        UpdateCoinsUI();

        Debug.Log(
            $"PlayerCoinsManager: Starting balance = {currentCoins}"
        );
    }

    private void HandleRewardCalculated(
        int waveNumber,
        int earnedCoins)
    {
        if (waveNumber <= lastRewardedWave)
            return;

        lastRewardedWave = waveNumber;
        lastEarnedCoins = Mathf.Max(0, earnedCoins);

        AddCoins(lastEarnedCoins);

        Debug.Log(
            $"PlayerCoinsManager: Wave {waveNumber} " +
            $"reward +{lastEarnedCoins} coins. " +
            $"Current balance = {currentCoins}"
        );
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        currentCoins = (int)Mathf.Min(
            (long)currentCoins + amount,
            int.MaxValue
        );

        UpdateCoinsUI();
        OnCoinsChanged?.Invoke(currentCoins);
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount < 0 || currentCoins < amount)
            return false;

        if (amount == 0)
            return true;

        currentCoins -= amount;

        UpdateCoinsUI();
        OnCoinsChanged?.Invoke(currentCoins);

        return true;
    }

    private void UpdateCoinsUI()
    {
        if (coinsText == null)
            return;

        coinsText.text =
            currentCoins.ToString("N0",
                System.Globalization.CultureInfo.InvariantCulture)
                .Replace(",", " ");
    }

    private void OnDestroy()
    {
        if (rewardManager != null)
        {
            rewardManager.OnRewardCalculated -=
                HandleRewardCalculated;
        }
    }
}
