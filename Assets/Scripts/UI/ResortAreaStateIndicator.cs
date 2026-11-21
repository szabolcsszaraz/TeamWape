using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public class ResortAreaStateIndicator : MonoBehaviour
{
    [SerializeField] private ResortArea area;

    [Header("State Colors")]
    [SerializeField] private Color lockedColor = new Color(0.55f, 0.15f, 0.15f);
    [SerializeField] private Color availableColor = new Color(0.47f, 0.29f, 0.06f);
    [SerializeField] private Color purchasableColor = new Color(0.12f, 0.32f, 0.55f);
    [SerializeField] private Color unlockedColor = new Color(0.12f, 0.38f, 0.25f);

    private TextMeshProUGUI stateText;

    private void Awake()
    {
        stateText = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        if (area == null)
        {
            Debug.LogError(
                "ResortAreaStateIndicator: Assign an Area in the Inspector.",
                this
            );
            enabled = false;
            return;
        }

        area.OnStateChanged += ShowState;
        ShowState(area.CurrentState);
    }

    private void Start()
    {
        ShowState(area.CurrentState);
    }

    private void OnDisable()
    {
        if (area != null)
            area.OnStateChanged -= ShowState;
    }

    private void ShowState(AreaState state)
    {
        stateText.text = state.ToString();
        stateText.color = state switch
        {
            AreaState.Locked => lockedColor,
            AreaState.Available => availableColor,
            AreaState.Purchasable => purchasableColor,
            AreaState.Unlocked => unlockedColor,
            _ => lockedColor
        };
    }
}