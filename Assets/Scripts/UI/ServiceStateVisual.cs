
using UnityEngine;
using UnityEngine.UI;

public class ServiceStateVisual : MonoBehaviour
{
    public enum VisualState
    {
        Normal,
        Overloaded,
        Inactive,
        Boosted,
        Recovering
    }

    [Header("References")]
    [SerializeField] private ServiceBuilding serviceBuilding;
    [SerializeField] private Image stateIcon;

    [Header("State Sprites")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite overloadedSprite;
    [SerializeField] private Sprite inactiveSprite;
    [SerializeField] private Sprite boostedSprite;
    [SerializeField] private Sprite recoveringSprite;

    [Header("State Colors")]
    [SerializeField] private Color normalColor =
        new Color(0.27f, 0.73f, 0.46f);

    [SerializeField] private Color overloadedColor =
        new Color(0.91f, 0.35f, 0.33f);

    [SerializeField] private Color inactiveColor =
        new Color(0.55f, 0.58f, 0.61f);

    [SerializeField] private Color boostedColor =
        new Color(0.90f, 0.71f, 0.24f);

    [SerializeField] private Color recoveringColor =
        new Color(0.29f, 0.62f, 0.87f);

    [Header("Current State (Debug)")]
    [SerializeField]
    private VisualState currentState = VisualState.Normal;

    public VisualState CurrentState => currentState;

    private void Awake()
    {
        if (serviceBuilding == null)
        {
            serviceBuilding =
                GetComponentInParent<ServiceBuilding>();
        }

        if (stateIcon == null)
        {
            stateIcon = GetComponentInChildren<Image>(true);
        }

        RefreshVisual();
    }

    private void OnEnable()
    {
        if (serviceBuilding == null)
        {
            serviceBuilding =
                GetComponentInParent<ServiceBuilding>();
        }

        if (serviceBuilding != null)
        {
            serviceBuilding.OnServiceStateChanged -=
                HandleServiceStateChanged;

            serviceBuilding.OnServiceStateChanged +=
                HandleServiceStateChanged;

            SyncWithServiceBuilding();
        }
        else
        {
            Debug.LogWarning(
                $"{name}: ServiceBuilding component not found!",
                this
            );
        }
    }

    private void OnDisable()
    {
        if (serviceBuilding != null)
        {
            serviceBuilding.OnServiceStateChanged -=
                HandleServiceStateChanged;
        }
    }

    private void HandleServiceStateChanged(
        ServiceBuilding building,
        ServiceLoadState newState)
    {
        if (building != serviceBuilding)
            return;

        switch (newState)
        {
            case ServiceLoadState.Normal:
                SetState(VisualState.Normal);
                break;

            case ServiceLoadState.Overloaded:
                SetState(VisualState.Overloaded);
                break;

            case ServiceLoadState.Inactive:
                SetState(VisualState.Inactive);
                break;
        }
    }

    private void SyncWithServiceBuilding()
    {
        if (serviceBuilding == null)
            return;

        HandleServiceStateChanged(
            serviceBuilding,
            serviceBuilding.CurrentState
        );
    }

    public void SetState(VisualState newState)
    {
        currentState = newState;
        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (stateIcon == null)
        {
            Debug.LogWarning(
                $"{name}: StateIcon reference is missing!",
                this
            );

            return;
        }

        Sprite sprite = null;
        Color color = Color.white;

        switch (currentState)
        {
            case VisualState.Normal:
                sprite = normalSprite;
                color = normalColor;
                break;

            case VisualState.Overloaded:
                sprite = overloadedSprite;
                color = overloadedColor;
                break;

            case VisualState.Inactive:
                sprite = inactiveSprite;
                color = inactiveColor;
                break;

            case VisualState.Boosted:
                sprite = boostedSprite;
                color = boostedColor;
                break;

            case VisualState.Recovering:
                sprite = recoveringSprite;
                color = recoveringColor;
                break;
        }

        if (sprite != null)
        {
            stateIcon.sprite = sprite;
        }

        stateIcon.color = color;
    }

    [ContextMenu("Test / Normal")]
    private void TestNormal()
    {
        SetState(VisualState.Normal);
    }

    [ContextMenu("Test / Overloaded")]
    private void TestOverloaded()
    {
        SetState(VisualState.Overloaded);
    }

    [ContextMenu("Test / Inactive")]
    private void TestInactive()
    {
        SetState(VisualState.Inactive);
    }

    [ContextMenu("Test / Boosted")]
    private void TestBoosted()
    {
        SetState(VisualState.Boosted);
    }

    [ContextMenu("Test / Recovering")]
    private void TestRecovering()
    {
        SetState(VisualState.Recovering);
    }
}
