// TEST ONLY (SCRUM-43): delete this file when Coin testing is finished.
// P = add Coins, O = remove Coins. Editor and Development Builds only.
// Adds itself to the PlayerCoinsManager object at runtime, no scene setup needed.

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class DevCoinsCheat : MonoBehaviour
{
    [SerializeField, Min(1)] private int amountPerPress = 500;

    private PlayerCoinsManager coinsManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        PlayerCoinsManager manager = FindFirstObjectByType<PlayerCoinsManager>();

        if (manager == null || manager.GetComponent<DevCoinsCheat>() != null)
            return;

        manager.gameObject.AddComponent<DevCoinsCheat>();
    }

    private void Awake()
    {
        coinsManager = GetComponent<PlayerCoinsManager>();
    }

    private void Update()
    {
        if (coinsManager == null)
            return;

        if (WasPressed(true))
        {
            coinsManager.AddCoins(amountPerPress);

            Debug.Log(
                $"[DEV CHEAT] +{amountPerPress} Coins. " +
                $"Current Coins: {coinsManager.CurrentCoins}",
                this
            );
        }
        else if (WasPressed(false))
        {
            int removed = Mathf.Min(amountPerPress, coinsManager.CurrentCoins);

            if (removed > 0)
                coinsManager.TrySpendCoins(removed);

            Debug.Log(
                $"[DEV CHEAT] -{removed} Coins. " +
                $"Current Coins: {coinsManager.CurrentCoins}",
                this
            );
        }
    }

    private static bool WasPressed(bool add)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            return false;

        return add
            ? Keyboard.current.pKey.wasPressedThisFrame
            : Keyboard.current.oKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(add ? KeyCode.P : KeyCode.O);
#else
        return false;
#endif
    }
}

#endif
