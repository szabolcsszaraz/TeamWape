using Unity.AI.Assistant.Editor.Utils.Event;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Events
{
    class EventExpandedViewRequested : IAssistantEvent
    {
        public string TitleText { get; }
        public VisualElement ExpandedElement { get; }
        public VisualElement HeaderActions { get; }
        public ScrollViewMode ScrollMode { get; }

        /// <summary>Whether the element scrolls itself and wants the panel's height rather than a place
        /// inside the panel's scroll view. <see cref="ScrollMode"/> is ignored when this is true, because
        /// the panel's scroll view is the thing the element replaces.</summary>
        public bool FillsPanel { get; }

        public EventExpandedViewRequested(string titleText, VisualElement expandedElement, VisualElement headerActions = null,
            ScrollViewMode scrollMode = ScrollViewMode.VerticalAndHorizontal, bool fillsPanel = false)
        {
            TitleText = titleText;
            ExpandedElement = expandedElement;
            HeaderActions = headerActions;
            ScrollMode = scrollMode;
            FillsPanel = fillsPanel;
        }
    }
}
