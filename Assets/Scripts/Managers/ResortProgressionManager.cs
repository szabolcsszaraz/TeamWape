
using System;
using UnityEngine;

public class ResortProgressionManager : MonoBehaviour
{
    [Header("Starting Progression")]
    [SerializeField, Min(1)]
    private int startingLevel = 1;

    [SerializeField, Min(0)]
    private int startingXP = 0;

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

    public event Action<int, int> OnProgressionChanged;
    public event Action<int> OnLevelUp;

    private void Awake()
    {
        currentLevel = Mathf.Max(1, startingLevel);
        currentXP = Mathf.Max(0, startingXP);
        totalXP = currentXP;
    }
}
