using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    /// <summary>
    /// Interface for function call renderers that support expanded panel mode.
    /// Implementing this allows the renderer to configure itself for the expanded view
    /// and provide custom header actions without requiring explicit type checks.
    /// </summary>
    interface IExpandableRenderer
    {
        /// <summary>
        /// Configures the renderer for expanded panel display mode.
        /// Called before UpdateData so the renderer initializes directly into expanded mode.
        /// </summary>
        void SetExpandedPanelMode();

        /// <summary>
        /// Creates the header actions container for the expanded panel.
        /// </summary>
        /// <returns>A VisualElement containing the header action buttons, or null if none.</returns>
        VisualElement CreateHeaderActions();
    }
}
