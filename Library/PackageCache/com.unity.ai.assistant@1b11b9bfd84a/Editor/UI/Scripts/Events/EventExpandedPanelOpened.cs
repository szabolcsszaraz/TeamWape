using Unity.AI.Assistant.Editor.Utils.Event;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Events
{
    class EventExpandedPanelOpened : IAssistantEvent
    {
        public string TitleText { get; }
        public VisualElement HeaderActions { get; }

        public EventExpandedPanelOpened(string titleText, VisualElement headerActions = null)
        {
            TitleText = titleText;
            HeaderActions = headerActions;
        }
    }
}
