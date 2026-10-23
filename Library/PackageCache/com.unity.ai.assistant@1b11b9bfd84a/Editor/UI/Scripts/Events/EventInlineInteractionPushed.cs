using System;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Events
{
    class EventInlineInteractionPushed : IAssistantEvent
    {
        public Guid CallId { get; }
        public InteractionContentView ContentView { get; }

        public EventInlineInteractionPushed(Guid callId, InteractionContentView contentView)
        {
            CallId = callId;
            ContentView = contentView;
        }
    }
}
