
using System;
using UnityEngine;

public class ResortProgressionManager : MonoBehaviour
{
    [Header("Starting Progression")]
    [SerializeField, Min(1)]
    private int startingLevel = 1;

    [SerializeField, Min(0)]
    private int startingXP = 0;

    [Header("XP Calculation Rules")]
    [SerializeField, Min(1)]
    private int baseXPRequired = 100;

    [SerializeField, Min(0)]
    private int xpIncreasePerLevel = 50;

    [Header("Current Resort Progression")]
    [SerializeField, Min(1)]
    private int currentLevel = 1;

    [SerializeField, Min(0)]
    private int currentXP = 0;

    [SerializeField, Min(0)]
    private int totalXP = 0;

    public int CurrentLevel => currentLevel;
    public int CurrentXP => currentXP;
    public int TotalXP => totalXP;

    public int XPRequiredForNextLevel =>
        GetRequiredXPForLevel(currentLevel);

    public float LevelProgress =>
        Mathf.Clamp01(
            (float)currentXP / XPRequiredForNextLevel
        );

    public event Action<int, int> OnProgressionChanged;
    public event Action<int> OnLevelUp;

    private void Awake()
    {
        currentLevel = Mathf.Max(1, startingLevel);
        currentXP = Mathf.Max(0, startingXP);
        totalXP = currentXP;
    }

    public int GetRequiredXPForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);

        long requiredXP =
            (long)Mathf.Max(1, baseXPRequired) +
            ((long)safeLevel - 1) *
            Mathf.Max(0, xpIncreasePerLevel);

        return (int)Math.Min(requiredXP, int.MaxValue);
    }

    public bool CanLevelUp()
    {
        return currentLevel < int.MaxValue &&
               currentXP >= XPRequiredForNextLevel;
    }
}
