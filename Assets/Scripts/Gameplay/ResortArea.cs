using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ResortArea : MonoBehaviour
{
    [Tooltip("Enable for areas the player can access from the start.")]
    [SerializeField] private bool startsUnlocked;

    [Header("Runtime State")]
    [SerializeField] private AreaState currentState = AreaState.Locked;

    public AreaState CurrentState => currentState;

    public event Action<AreaState> OnStateChanged;

    private void Awake()
    {
        currentState = startsUnlocked
            ? AreaState.Unlocked
            : AreaState.Locked;
    }

    public void UpdatePurchaseState(bool requirementsMet, bool canAfford)
    {
        if (currentState == AreaState.Unlocked)
            return;

        if (!requirementsMet)
        {
            SetState(AreaState.Locked);
            return;
        }

        SetState(canAfford ? AreaState.Purchasable : AreaState.Available);
    }

    public void MarkPurchased()
    {
        SetState(AreaState.Unlocked);
    }

    private void SetState(AreaState nextState)
    {
        if (currentState == nextState)
            return;

        currentState = nextState;
        OnStateChanged?.Invoke(currentState);
    }
}