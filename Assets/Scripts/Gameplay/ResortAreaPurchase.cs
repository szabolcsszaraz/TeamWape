using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(ResortAreaRequirements))]
public class ResortAreaPurchase : MonoBehaviour
{
    [Header("Purchase")]
    [SerializeField] private PlayerCoinsManager coinsManager;
    [SerializeField, Min(0)] private int purchaseCost = 1500;

    [Header("Area")]
    [SerializeField] private GameObject unlockedContent;

    [Tooltip("Optional. The terrain stays visible and gains collision after purchase.")]
    [SerializeField] private TerrainCollider areaTerrainCollider;

    [Header("World Space UI")]
    [SerializeField] private GameObject purchasePanel;
    [SerializeField] private TMP_Text requirementsText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonText;

    private ResortAreaRequirements requirements;
    private bool isPurchased;
    private bool purchaseInProgress;

    private void Awake()
    {
        requirements = GetComponent<ResortAreaRequirements>();
    }

    private void OnEnable()
    {
        if (coinsManager == null ||
            unlockedContent == null ||
            purchasePanel == null ||
            requirementsText == null ||
            buyButton == null ||
            buyButtonText == null)
        {
            Debug.LogError(
                "ResortAreaPurchase: Assign the Coins Manager, unlocked content, and all UI references.",
                this
            );
            enabled = false;
            return;
        }

        requirements.OnRequirementsChanged += RefreshPanel;
        coinsManager.OnCoinsChanged += HandleCoinsChanged;
        buyButton.onClick.AddListener(HandleBuyClicked);

        requirementsText.text = BuildRequirementsText();
        buyButtonText.text = $"Buy for {purchaseCost} Coins";

        ApplyAreaState();
        RefreshPanel();
    }

    private void OnDisable()
    {
        if (requirements != null)
            requirements.OnRequirementsChanged -= RefreshPanel;

        if (coinsManager != null)
            coinsManager.OnCoinsChanged -= HandleCoinsChanged;

        if (buyButton != null)
            buyButton.onClick.RemoveListener(HandleBuyClicked);
    }

    private void HandleCoinsChanged(int balance)
    {
        RefreshPanel();
    }

    private void HandleBuyClicked()
    {
        if (isPurchased || purchaseInProgress || !requirements.RequirementsMet)
            return;

        purchaseInProgress = true;

        try
        {
            if (coinsManager.TrySpendCoins(purchaseCost))
            {
                isPurchased = true;
                ApplyAreaState();
            }
        }
        finally
        {
            purchaseInProgress = false;
            RefreshPanel();
        }
    }

    private void ApplyAreaState()
    {
        if (areaTerrainCollider != null)
            areaTerrainCollider.enabled = isPurchased;

        unlockedContent.SetActive(isPurchased);
    }

    private void RefreshPanel()
    {
        purchasePanel.SetActive(!isPurchased);

        if (isPurchased)
            return;

        bool canBuyArea = requirements.RequirementsMet;

        requirementsText.gameObject.SetActive(!canBuyArea);
        buyButton.gameObject.SetActive(canBuyArea);
        buyButton.interactable =
            canBuyArea &&
            !purchaseInProgress &&
            coinsManager.CurrentCoins >= purchaseCost;
    }

    private string BuildRequirementsText()
    {
        var lines = new List<string> { "Unlock requirements" };

        if (requirements.MinimumCompletedWaves > 0)
            lines.Add($"Complete {requirements.MinimumCompletedWaves} waves");

        if (requirements.MinimumResortLevel > 1)
            lines.Add($"Resort Level {requirements.MinimumResortLevel}");

        if (requirements.MinimumRatingStars > 0f)
            lines.Add($"Resort Rating {requirements.MinimumRatingStars:0.##}/5");

        return string.Join("\n", lines);
    }
}