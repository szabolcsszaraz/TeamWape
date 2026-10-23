using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    /// <summary>
    /// Interface for function call renderers that supply action buttons to be placed
    /// in the parent function call header. The parent is responsible for placement;
    /// the renderer does not need to traverse the hierarchy.
    /// </summary>
    interface IInlineHeaderActionsProvider
    {
        /// <summary>
        /// Returns the element containing the inline header action buttons.
        /// </summary>
        VisualElement GetInlineHeaderActions();
    }
}
