
using TMPro;
using UnityEngine;

public class ResortProgressionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private ResortProgressionManager progressionManager;

    [SerializeField]
    private TMP_Text levelText;

    [SerializeField]
    private TMP_Text xpValueText;

    [SerializeField]
    private TMP_Text xpRemainingText;

    private void Start()
    {
        if (progressionManager == null)
        {
            progressionManager =
                FindFirstObjectByType<ResortProgressionManager>();
        }

        if (progressionManager == null)
        {
            Debug.LogError(
                "ResortProgressionUI: Progression Manager not found!"
            );

            enabled = false;
            return;
        }

        if (levelText == null ||
            xpValueText == null ||
            xpRemainingText == null)
        {
            Debug.LogError(
                "ResortProgressionUI: Missing text references!"
            );

            enabled = false;
            return;
        }

        progressionManager.OnProgressionChanged += HandleProgressionChanged;

        UpdateUI();
    }

    private void HandleProgressionChanged(int level, int xp)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        int level = progressionManager.CurrentLevel;
        int xp = progressionManager.CurrentXP;
        int requiredXP = progressionManager.XPRequiredForNextLevel;

        levelText.text = $"Resort Level {level}";
        xpValueText.text = $"{xp} / {requiredXP} XP";

        int remainingXP = Mathf.Max(0, requiredXP - xp);
        xpRemainingText.text = $"{remainingXP} XP to next level";
    }

    private void OnDestroy()
    {
        if (progressionManager != null)
        {
            progressionManager.OnProgressionChanged -=
                HandleProgressionChanged;
        }
    }
}
