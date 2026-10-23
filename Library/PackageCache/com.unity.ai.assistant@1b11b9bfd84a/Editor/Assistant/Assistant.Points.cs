using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant
    {
        public event Action<AssistantMessageId, int?, bool> MessageCostReceived;
        
        public AssistantMessageId PendingCostUserMessageId { get; set; }

        public async Task<int?> FetchMessageCost(AssistantMessageId messageId, CancellationToken ct = default)
        {
            if (!messageId.ConversationId.IsValid)
                return null;

            // Get conversation from cache
            if (!m_ConversationCache.TryGetValue(messageId.ConversationId, out var conversation))
                return null;

            var messageIndex = conversation.Messages.FindIndex(m => m.Id == messageId);
            if (messageIndex < 0)
                return null;

            AssistantMessageId? userMessageId = null;
            var correlationMessageId = conversation.Messages[messageIndex].CorrelationMessageId;
            if (!string.IsNullOrEmpty(correlationMessageId))
            {
                userMessageId = new AssistantMessageId(messageId.ConversationId, correlationMessageId, AssistantMessageIdType.External);
            }
            else
            {
                for (int i = messageIndex - 1; i >= 0; i--)
                {
                    if (conversation.Messages[i].Role.Equals(k_UserRole, StringComparison.OrdinalIgnoreCase))
                    {
                        userMessageId = conversation.Messages[i].Id;
                        break;
                    }
                }
            }

            if (!userMessageId.HasValue)
                return null;

            var result = await Backend.FetchMessageCost(await CredentialsProvider.GetCredentialsContext(ct), userMessageId.Value, ct);

            MessageCostReceived?.Invoke(messageId, result.Value, userMessageId == PendingCostUserMessageId);
            
            PendingCostUserMessageId = AssistantMessageId.Invalid;
            
            return result.Value;
        }
    }
}