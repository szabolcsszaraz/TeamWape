using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Utils.Event;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Events
{
    /// <summary>
    /// Raised when the active provider reported NO_CAPACITY. AssistantView reacts per
    /// CapacityFallbackPolicy: when a non-default Unity provider profile is active it switches to the
    /// default profile (banner + flash + auto-resend); otherwise it shows an informational banner.
    /// </summary>
    class EventCapacityReached : IAssistantEvent
    {
        public EventCapacityReached(AssistantConversationId conversationId)
        {
            ConversationId = conversationId;
        }

        public AssistantConversationId ConversationId { get; }
    }
}
