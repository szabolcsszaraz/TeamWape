using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class ResortAreaHover : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private BoxCollider hoverZone;
    [SerializeField] private CanvasGroup purchasePanel;

    [SerializeField, Min(0f)]
    private float hideDelay = 0.6f;

    private RectTransform panelRect;
    private float hideAt;

    private void Awake()
    {
        if (worldCamera == null || hoverZone == null || purchasePanel == null)
        {
            Debug.LogError(
                "ResortAreaHover: Assign the camera, hover zone, and panel Canvas Group.",
                this
            );
            enabled = false;
            return;
        }

        panelRect = purchasePanel.GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        hideAt = float.NegativeInfinity;
        SetPanelVisible(false);
    }

    private void OnDisable()
    {
        if (purchasePanel != null)
            SetPanelVisible(false);
    }

    private void Update()
    {
        if (!purchasePanel.gameObject.activeInHierarchy ||
            !TryGetMousePosition(out Vector2 mousePosition) ||
            !worldCamera.pixelRect.Contains(mousePosition))
        {
            hideAt = float.NegativeInfinity;
            SetPanelVisible(false);
            return;
        }

        Ray mouseRay = worldCamera.ScreenPointToRay(mousePosition);
        bool isOverIsland = hoverZone.bounds.IntersectRay(mouseRay);
        bool isOverVisiblePanel = purchasePanel.alpha > 0f &&
            RectTransformUtility.RectangleContainsScreenPoint(
                panelRect,
                mousePosition,
                worldCamera
            );

        if (isOverIsland || isOverVisiblePanel)
        {
            hideAt = Time.unscaledTime + hideDelay;
            SetPanelVisible(true);
            return;
        }

        if (Time.unscaledTime >= hideAt)
            SetPanelVisible(false);
    }

    private void SetPanelVisible(bool visible)
    {
        purchasePanel.alpha = visible ? 1f : 0f;
        purchasePanel.interactable = visible;
        purchasePanel.blocksRaycasts = visible;
    }

    private static bool TryGetMousePosition(out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.mousePresent)
        {
            position = Input.mousePosition;
            return true;
        }
#endif

        position = Vector2.zero;
        return false;
    }
}