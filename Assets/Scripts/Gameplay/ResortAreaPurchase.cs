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

    // SCRUM-324: Coin cost of unlocking this area.
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

    [Header("Insufficient Coins (SCRUM-43)")]
    [Tooltip("Optional. Shows the missing Coins. If empty, the Requirements Text is reused.")]
    [SerializeField] private TMP_Text insufficientCoinsText;

    [SerializeField] private Color missingCoinsColor =
        new Color(0.91f, 0.35f, 0.33f);

    private ResortAreaRequirements requirements;
    private ResortArea area;
    private bool hasStarted;
    private bool purchaseInProgress;
    private bool showFailedAttempt;
    private Color requirementsTextColor = Color.white;

    public int PurchaseCost => purchaseCost;

    // SCRUM-326: Coins still needed to unlock this area.
    public int MissingCoins =>
        coinsManager != null ? coinsManager.GetMissingCoins(purchaseCost) : purchaseCost;

    // SCRUM-325
    public bool HasEnoughCoins =>
        coinsManager != null && coinsManager.HasEnoughCoins(purchaseCost);

    private TMP_Text MessageText =>
        insufficientCoinsText != null ? insufficientCoinsText : requirementsText;

    private void Awake()
    {
        requirements = GetComponent<ResortAreaRequirements>();
        area = GetComponent<ResortArea>();

        if (requirementsText != null)
            requirementsTextColor = requirementsText.color;
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
        // The "not enough" warning only lasts until the balance changes.
        showFailedAttempt = false;
        RefreshPurchaseState();
    }

    private void RefreshPurchaseState()
    {
        if (!hasStarted || purchaseInProgress)
            return;

        area.UpdatePurchaseState(
            requirements.RequirementsMet,
            HasEnoughCoins
        );

        RefreshPanel();
    }

    private void HandleBuyClicked()
    {
        if (!hasStarted || purchaseInProgress)
            return;

        // SCRUM-43: The player tried to buy without enough Coins.
        if (area.CurrentState == AreaState.Available)
        {
            showFailedAttempt = true;

            Debug.Log(
                $"{name}: Not enough Coins to unlock. " +
                $"Cost: {purchaseCost}, Balance: {coinsManager.CurrentCoins}, " +
                $"Missing: {MissingCoins}.",
                this
            );

            RefreshPanel();
            return;
        }

        if (area.CurrentState != AreaState.Purchasable)
            return;

        purchaseInProgress = true;

        try
        {
            // TrySpendCoins checks the balance again before paying.
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

        bool lacksCoins = state == AreaState.Available;

        // Requirements text: the unlock requirements while Locked.
        requirementsText.text = BuildRequirementsText();
        requirementsText.color = requirementsTextColor;
        requirementsText.gameObject.SetActive(state == AreaState.Locked);

        // SCRUM-327 / SCRUM-328: Show the missing Coins.
        if (insufficientCoinsText != null)
            insufficientCoinsText.gameObject.SetActive(false);

        if (lacksCoins)
        {
            TMP_Text message = MessageText;
            message.text = BuildInsufficientCoinsText();
            message.color = missingCoinsColor;
            message.gameObject.SetActive(true);
        }

        buyButton.gameObject.SetActive(
            state == AreaState.Available ||
            state == AreaState.Purchasable
        );

        // Clickable while Available so the attempt can show the message.
        buyButton.interactable =
            state == AreaState.Available ||
            state == AreaState.Purchasable;
    }

    // SCRUM-327: Insufficient Coins message.
    private string BuildInsufficientCoinsText()
    {
        int missing = MissingCoins;

        if (showFailedAttempt)
        {
            return
                "Not enough Coins!\n" +
                $"You need {missing} more Coins\n" +
                $"({coinsManager.CurrentCoins} / {purchaseCost})";
        }

        return
            $"You need {missing} more Coins\n" +
            $"({coinsManager.CurrentCoins} / {purchaseCost})";
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

    [ContextMenu("Test / Log Coin Check")]
    private void TestLogCoinCheck()
    {
        if (coinsManager == null)
        {
            Debug.LogWarning($"{name}: No Coins Manager assigned.", this);
            return;
        }

        Debug.Log(
            $"{name}: Cost {purchaseCost}, Balance {coinsManager.CurrentCoins}, " +
            $"Enough: {HasEnoughCoins}, Missing: {MissingCoins}, " +
            $"State: {(area != null ? area.CurrentState.ToString() : "?")}",
            this
        );
    }
}
