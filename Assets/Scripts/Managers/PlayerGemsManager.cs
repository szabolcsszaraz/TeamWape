
using System;
using TMPro;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerGemsManager : MonoBehaviour
{
    [Header("Player Gems")]
    [SerializeField, Min(0)]
    private int startingGems = 0;

    [SerializeField, Min(0)]
    private int currentGems;

    [Header("UI References")]
    [SerializeField]
    private TMP_Text gemsText;

    [Header("Developer Cheat")]
    [SerializeField, Min(1)]
    private int cheatGemsAmount = 100;

    public int CurrentGems => currentGems;

    public event Action<int> OnGemsChanged;

    private void Awake()
    {
        currentGems = Mathf.Max(0, startingGems);

        UpdateGemsUI();
    }

    private void Start()
    {
        UpdateGemsUI();

        Debug.Log(
            $"PlayerGemsManager: Starting Gems = {currentGems}",
            this
        );
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        if (IsCheatKeyPressed())
        {
            AddGems(cheatGemsAmount);

            Debug.Log(
                $"[DEV CHEAT] +{cheatGemsAmount} Gems. " +
                $"Current Gems: {currentGems}",
                this
            );
        }

#endif
    }

    private bool IsCheatKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.numpad6Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Keypad6);
#else
        return false;
#endif
    }

    public void AddGems(int amount)
    {
        if (amount <= 0)
            return;

        currentGems = (int)Mathf.Min(
            (long)currentGems + amount,
            int.MaxValue
        );

        UpdateGemsUI();

        OnGemsChanged?.Invoke(currentGems);
    }

    public bool HasEnoughGems(int amount)
    {
        return amount >= 0 && currentGems >= amount;
    }

    public bool TrySpendGems(int amount)
    {
        if (!HasEnoughGems(amount))
        {
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        currentGems -= amount;

        UpdateGemsUI();

        OnGemsChanged?.Invoke(currentGems);

        Debug.Log(
            $"PlayerGemsManager: Spent {amount} Gems. " +
            $"Remaining: {currentGems}",
            this
        );

        return true;
    }

    private void UpdateGemsUI()
    {
        if (gemsText == null)
            return;

        gemsText.text =
            currentGems.ToString(
                "N0",
                System.Globalization.CultureInfo.InvariantCulture
            ).Replace(",", " ");
    }
}
