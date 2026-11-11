
using UnityEngine;

public class WaveRewardManager : MonoBehaviour
{
    [Header("Reward Rules")]
    [SerializeField, Min(0)]
    private int coinsPerSuccessfulService = 10;

    [SerializeField, Range(0f, 1f)]
    private float minimumRatingMultiplier = 0.5f;

    [SerializeField, Range(0f, 2f)]
    private float maximumRatingMultiplier = 1.5f;

    [Header("Last Wave Reward")]
    [SerializeField, Min(0)]
    private int lastCalculatedReward;

    public int CoinsPerSuccessfulService =>
        coinsPerSuccessfulService;

    public int LastCalculatedReward =>
        lastCalculatedReward;

    public float CalculateRatingMultiplier(float resortRating)
    {
        float normalizedRating =
            Mathf.Clamp(resortRating, 0f, 100f) / 100f;

        return Mathf.Lerp(
            minimumRatingMultiplier,
            maximumRatingMultiplier,
            normalizedRating
        );
    }

    public int CalculateReward(
        int successfulServices,
        float wavePerformance,
        float resortRating)
    {
        int safeServices = Mathf.Max(0, successfulServices);

        float performanceFactor =
            Mathf.Clamp(wavePerformance, 0f, 100f) / 100f;

        float ratingMultiplier =
            CalculateRatingMultiplier(resortRating);

        float reward =
            safeServices *
            coinsPerSuccessfulService *
            performanceFactor *
            ratingMultiplier;

        return Mathf.Max(0, Mathf.RoundToInt(reward));
    }
}
