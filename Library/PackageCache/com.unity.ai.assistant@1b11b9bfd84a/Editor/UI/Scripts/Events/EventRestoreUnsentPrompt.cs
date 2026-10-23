using Unity.AI.Assistant.Editor.Utils.Event;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Events
{
    class EventRestoreUnsentPrompt : IAssistantEvent
    {
        public EventRestoreUnsentPrompt(string prompt)
        {
            Prompt = prompt;
        }

        public string Prompt { get; }
    }
}
