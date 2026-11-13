
using System;

[Serializable]
public class WaveRewardsData
{
    public int waveNumber;
    public int earnedCoins;
    public int earnedXP;

    public float resortRating;
    public int resortLevel;

    public WaveRewardsData(
        int waveNumber,
        int earnedCoins,
        int earnedXP,
        float resortRating,
        int resortLevel)
    {
        this.waveNumber = waveNumber;
        this.earnedCoins = earnedCoins;
        this.earnedXP = earnedXP;
        this.resortRating = resortRating;
        this.resortLevel = resortLevel;
    }
}
