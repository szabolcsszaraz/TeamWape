using System;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Utils;
using Unity.AI.Toolkit.Accounts;
using Unity.AI.Toolkit.Accounts.Services;
using UnityEditor;
using UnityEngine.Analytics;

namespace Unity.AI.Assistant.Editor.Analytics
{
    internal static partial class AIAssistantAnalytics
    {
        const string k_VendorKey = "unity.ai.assistant";
        const string k_SendMessageEvent = "AIAssistantSendUserMessageEvent";
        const string k_UserMessageTtftEvent = "AIAssistantUserMessageTtftEvent";

        [InitializeOnLoadMethod]
        static void RegisterDropdownAnalytics()
        {
#if UNITY_6000_3_OR_NEWER
            AIDropdownController.OnDropdownOpened += ReportUITriggerLocalAIDropdownOpenedEvent;
#else
            AIToolbarButtonLegacy.OnDropdownOpened += ReportUITriggerLocalAIDropdownOpenedEvent;
#endif
        }

        /// <summary>
        /// Returns the conversation ID string if valid, or null otherwise.
        /// Centralises the IsValid guard so callers don't repeat it.
        /// </summary>
        static string ConversationIdOrNull(AssistantConversationId id)
            => id.IsValid ? id.Value : null;

        static void SendGatedEditorAnalytic(IAnalytic analytic)
        {
            if (!Account.sessionStatus.IsUsable)
                return;

            EditorAnalytics.SendAnalytic(analytic);
        }

        #region SendMessageEvent

        [Serializable]
        internal class SendUserMessageEventData : IAnalytic.IData
        {
            public string userPrompt;
            public string commandMode;
            public string autoRunEnabled;
            public string conversationId;
            public string messageId;
        }

        [AnalyticInfo(eventName: k_SendMessageEvent, vendorKey: k_VendorKey)]
        class SendUserMessageEvent : IAnalytic
        {
            readonly SendUserMessageEventData m_Data;

            public SendUserMessageEvent(SendUserMessageEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                data = m_Data;
                error = null;
                return true;
            }
        }

        internal static void ReportUserMessageSentEvent(string userPrompt, AssistantMessageId messageId, AssistantMode mode = AssistantMode.Undefined)
        {
            var data = new SendUserMessageEventData
            {
                userPrompt = userPrompt,
                commandMode = mode != AssistantMode.Undefined ? mode.ToString() : string.Empty,
                autoRunEnabled = mode.SupportsAutoRun() ? AssistantEditorPreferences.AutoRun.ToString() : null,
                messageId = messageId.FragmentId,
                conversationId = ConversationIdOrNull(messageId.ConversationId)
            };

            SendGatedEditorAnalytic(new SendUserMessageEvent(data));
        }

        #endregion

        [Serializable]
        internal class UserMessageTtftEventData : IAnalytic.IData
        {
            public string conversationId;
            public string messageId;
            public long ttftMs;
        }

        [AnalyticInfo(eventName: k_UserMessageTtftEvent, vendorKey: k_VendorKey)]
        class UserMessageTtftEvent : IAnalytic
        {
            readonly UserMessageTtftEventData m_Data;

            public UserMessageTtftEvent(UserMessageTtftEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                data = m_Data;
                error = null;
                return true;
            }
        }

        /// <summary>
        /// Reports client-side Time To First Chunk (TTFT): elapsed milliseconds from prompt
        /// dispatch to first response fragment arrival. Fires once per turn when the first
        /// fragment is received.
        /// </summary>
        internal static void ReportUserMessageTtftEvent(
            AssistantConversationId conversationId,
            AssistantMessageId messageId,
            long ttftMs)
        {
            var data = new UserMessageTtftEventData
            {
                conversationId = ConversationIdOrNull(conversationId),
                messageId = messageId.FragmentId,
                ttftMs = ttftMs
            };

            EditorAnalytics.SendAnalytic(new UserMessageTtftEvent(data));
        }
    }
}
