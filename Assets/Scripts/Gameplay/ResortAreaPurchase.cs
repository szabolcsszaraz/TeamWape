using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(ResortAreaRequirements), typeof(ResortArea))]
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
    private ResortArea area;
    private bool hasStarted;
    private bool purchaseInProgress;

    private void Awake()
    {
        requirements = GetComponent<ResortAreaRequirements>();
        area = GetComponent<ResortArea>();
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

        requirements.OnRequirementsChanged += RefreshPurchaseState;
        coinsManager.OnCoinsChanged += HandleCoinsChanged;
        buyButton.onClick.AddListener(HandleBuyClicked);

        requirementsText.text = BuildRequirementsText();
        buyButtonText.text = $"Buy for {purchaseCost} Coins";

        if (hasStarted)
        {
            ApplyAreaState();
            RefreshPurchaseState();
        }
    }

    private void Start()
    {
        hasStarted = true;
        ApplyAreaState();
        RefreshPurchaseState();
    }

    private void OnDisable()
    {
        if (requirements != null)
            requirements.OnRequirementsChanged -= RefreshPurchaseState;

        if (coinsManager != null)
            coinsManager.OnCoinsChanged -= HandleCoinsChanged;

        if (buyButton != null)
            buyButton.onClick.RemoveListener(HandleBuyClicked);
    }

    private void HandleCoinsChanged(int balance)
    {
        RefreshPurchaseState();
    }

    private void RefreshPurchaseState()
    {
        if (!hasStarted || purchaseInProgress)
            return;

        area.UpdatePurchaseState(
            requirements.RequirementsMet,
            coinsManager.CurrentCoins >= purchaseCost
        );

        RefreshPanel();
    }

    private void HandleBuyClicked()
    {
        if (!hasStarted ||
            purchaseInProgress ||
            area.CurrentState != AreaState.Purchasable)
        {
            return;
        }

        purchaseInProgress = true;

        try
        {
            if (coinsManager.TrySpendCoins(purchaseCost))
            {
                area.MarkPurchased();
                ApplyAreaState();
            }
        }
        finally
        {
            purchaseInProgress = false;
            RefreshPurchaseState();
        }
    }

    private void ApplyAreaState()
    {
        bool isUnlocked = area.CurrentState == AreaState.Unlocked;

        if (areaTerrainCollider != null)
            areaTerrainCollider.enabled = isUnlocked;

        unlockedContent.SetActive(isUnlocked);
    }

    private void RefreshPanel()
    {
        AreaState state = area.CurrentState;

        purchasePanel.SetActive(state != AreaState.Unlocked);

        if (state == AreaState.Unlocked)
            return;

        requirementsText.gameObject.SetActive(state == AreaState.Locked);
        buyButton.gameObject.SetActive(
            state == AreaState.Available ||
            state == AreaState.Purchasable
        );
        buyButton.interactable = state == AreaState.Purchasable;
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