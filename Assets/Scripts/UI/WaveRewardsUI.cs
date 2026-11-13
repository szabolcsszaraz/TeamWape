
using System.Collections;
using TMPro;
using UnityEngine;

public class WaveRewardsUI : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField]
    private ServicePerformanceTracker performanceTracker;

    [SerializeField]
    private WaveRewardManager rewardManager;

    [SerializeField]
    private ResortProgressionManager progressionManager;

    [SerializeField]
    private ResortRatingManager ratingManager;

    [Header("Rewards Panel")]
    [SerializeField]
    private GameObject rewardsPanel;

    [Header("Text References")]
    [SerializeField]
    private TMP_Text rewardsTitleText;

    [SerializeField]
    private TMP_Text coinsRewardText;

    [SerializeField]
    private TMP_Text xpRewardText;

    [SerializeField]
    private TMP_Text ratingRewardText;

    [SerializeField]
    private TMP_Text levelRewardText;

    private WaveRewardsData lastRewardsData;
    private int lastProcessedWave = -1;

    public WaveRewardsData LastRewardsData => lastRewardsData;

    private void Start()
    {
        if (performanceTracker == null)
            performanceTracker =
                FindFirstObjectByType<ServicePerformanceTracker>();

        if (rewardManager == null)
            rewardManager =
                FindFirstObjectByType<WaveRewardManager>();

        if (progressionManager == null)
            progressionManager =
                FindFirstObjectByType<ResortProgressionManager>();

        if (ratingManager == null)
            ratingManager =
                FindFirstObjectByType<ResortRatingManager>();

        if (performanceTracker == null ||
            rewardManager == null ||
            progressionManager == null ||
            ratingManager == null)
        {
            Debug.LogError(
                "WaveRewardsUI: One or more managers are missing!"
            );
            enabled = false;
            return;
        }

        if (rewardsPanel == null ||
            rewardsTitleText == null ||
            coinsRewardText == null ||
            xpRewardText == null ||
            ratingRewardText == null ||
            levelRewardText == null)
        {
            Debug.LogError(
                "WaveRewardsUI: Missing UI references!"
            );
            enabled = false;
            return;
        }

        performanceTracker.OnWaveResultStored += HandleWaveResultStored;

        rewardsPanel.SetActive(false);

        Debug.Log("WaveRewardsUI: Initialized.");
    }

    private void HandleWaveResultStored(
        ServicePerformanceTracker.WaveResult result)
    {
        if (result == null ||
            result.waveNumber <= lastProcessedWave)
            return;

        lastProcessedWave = result.waveNumber;

        StartCoroutine(CollectRewards(result.waveNumber));
    }

    private IEnumerator CollectRewards(int waveNumber)
    {
        // Allow all wave-end event handlers to finish.
        yield return new WaitForEndOfFrame();

        lastRewardsData = new WaveRewardsData(
            waveNumber,
            rewardManager.LastCalculatedReward,
            progressionManager.LastCalculatedXP,
            ratingManager.CurrentRating,
            progressionManager.CurrentLevel
        );

        UpdateRewardsText(lastRewardsData);

        Debug.Log(
            $"WaveRewardsUI: Wave {waveNumber} rewards ready. " +
            $"Coins=+{lastRewardsData.earnedCoins}, " +
            $"XP=+{lastRewardsData.earnedXP}, " +
            $"Rating={lastRewardsData.resortRating:F1}%, " +
            $"Level={lastRewardsData.resortLevel}"
        );
    }

    private void UpdateRewardsText(WaveRewardsData data)
    {
        rewardsTitleText.text =
            $"Wave {data.waveNumber} Rewards";

        coinsRewardText.text =
            $"Coins Earned: +{data.earnedCoins}";

        xpRewardText.text =
            $"XP Earned: +{data.earnedXP} XP";

        float stars =
            Mathf.Clamp(data.resortRating, 0f, 100f) / 20f;

        ratingRewardText.text =
            $"Resort Rating: {stars:F1}/5";

        levelRewardText.text =
            $"Resort Level: {data.resortLevel}";
    }

    private void OnDestroy()
    {
        if (performanceTracker != null)
        {
            performanceTracker.OnWaveResultStored -=
                HandleWaveResultStored;
        }
    }
}
