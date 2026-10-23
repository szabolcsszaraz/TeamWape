using Unity.AI.Assistant.Editor.Utils.Event;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Events
{
    class EventRetryableErrorOccurred : IAssistantEvent
    {
        public EventRetryableErrorOccurred(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        public string ErrorMessage { get; }
    }
}
