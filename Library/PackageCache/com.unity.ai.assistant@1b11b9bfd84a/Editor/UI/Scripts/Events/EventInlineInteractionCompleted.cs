using System;
using Unity.AI.Assistant.Editor.Utils.Event;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Events
{
    class EventInlineInteractionCompleted : IAssistantEvent
    {
        public Guid CallId { get; }

        public EventInlineInteractionCompleted(Guid callId)
        {
            CallId = callId;
        }
    }
}
