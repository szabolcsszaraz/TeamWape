using System;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Toolkit.Accounts.Services;
using UnityEditor;
using UnityEngine.Analytics;

namespace Unity.AI.Assistant.Editor.Analytics
{
    internal enum UITriggerBackendEventSubType
    {
        FavoriteConversation,
        DeleteConversation,
        RenameConversation,
        LoadConversation,
        CancelRequest,
        CreateNewConversation
    }

    internal enum ContextSubType
    {
        PingAttachedContextObjectFromFlyout,
        ClearAllAttachedContext,
        RemoveSingleAttachedContext,
        DragDropAttachedContext,
        DragDropImageFileAttachedContext,
        ChooseContextFromFlyout,
        ScreenshotAttachedContext,
        AnnotationAttachedContext,
        UploadImageAttachedContext,
        ClipboardImageAttachedContext
    }

    internal enum UITriggerLocalEventSubType
    {
        OpenReferenceUrl,
        SaveCode,
        CopyCode,
        CopyResponse,
        ExpandCommandLogic,
        PermissionRequested,
        PermissionResponse,
        WindowClosed,
        WindowOpened,
        PermissionSettingChanged,
        AutoRunSettingChanged,
        NewChatSuggestionsShown,
        SuggestionCategorySelected,
        SuggestionPromptSelected,
        ModeSwitched,
        PlanReviewApproved,
        PlanReviewDenied,
        EnterPlanModeApproved,
        EnterPlanModeDeclined,
        ClarifyingQuestionSubmitted,
        ClarifyingQuestionCancelled,
        RetryRelayConnection,
        ConversationRestored,
        ExpandUserMessageContext,
        ToggleChatHistory,
        ScrollToBottom,
        SwitchRunCommandTab,
        ExpandFunctionCallResult,
        OpenAssistantSettings,
        CollapseReasoningSettingChanged,
        GenerateContent,
        ChangeGeneratorMode,
        ErrorDisplayed,
        OpenedViaIntegration,
        AIDropdownOpened,
        AIInstallAccepted, // For reference, used in Engine code.
        // EARLY_ACCESS_PROMO_START
        DesktopEarlyAccessSignup,
        // EARLY_ACCESS_PROMO_END
    }

    internal static partial class AIAssistantAnalytics
    {
        #region Remote UI Events

        internal const string k_UITriggerBackendEvent = "AIAssistantUITriggerBackendEvent";

        [Serializable]
        internal class UITriggerBackendEventData : IAnalytic.IData
        {
            public UITriggerBackendEventData(UITriggerBackendEventSubType subType) => SubType = subType.ToString();

            public string SubType;
            public string ConversationId;
            public string MessageId;
            public string ResponseMessage;
            public string ConversationTitle;
            public string IsFavorite;
        }

        [AnalyticInfo(eventName: k_UITriggerBackendEvent, vendorKey: k_VendorKey)]
        class UITriggerBackendEvent : IAnalytic
        {
            private readonly UITriggerBackendEventData m_Data;

            public UITriggerBackendEvent(UITriggerBackendEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = m_Data;
                return true;
            }
        }

        static void ReportUITriggerBackendEvent(UITriggerBackendEventData data)
        {
            SendGatedEditorAnalytic(new UITriggerBackendEvent(data));
        }

        internal static void ReportUITriggerBackendCreateNewConversationEvent()
        {
            ReportUITriggerBackendEvent(new UITriggerBackendEventData(UITriggerBackendEventSubType.CreateNewConversation));
        }

        internal static void ReportUITriggerBackendCancelRequestEvent(AssistantConversationId conversationId)
        {
            ReportUITriggerBackendEvent(new UITriggerBackendEventData(UITriggerBackendEventSubType.CancelRequest)
            {
                ConversationId = conversationId.Value,
            });
        }

        internal static void ReportUITriggerBackendLoadConversationEvent(AssistantConversationId conversationId, string title)
        {
            ReportUITriggerBackendEvent(new UITriggerBackendEventData(UITriggerBackendEventSubType.LoadConversation)
            {
                ConversationId = conversationId.Value,
                ConversationTitle = title,
            });
        }

        internal static void ReportUITriggerBackendFavoriteConversationEvent(AssistantConversationId conversationId, string title, bool isFavorited)
        {
            ReportUITriggerBackendEvent(new UITriggerBackendEventData(UITriggerBackendEventSubType.FavoriteConversation)
            {
                ConversationId = conversationId.Value,
                ConversationTitle = title,
                IsFavorite = isFavorited.ToString(),
            });
        }

        internal static void ReportUITriggerBackendDeleteConversationEvent(AssistantConversationId conversationId, string title)
        {
            ReportUITriggerBackendEvent(new UITriggerBackendEventData(UITriggerBackendEventSubType.DeleteConversation)
            {
                ConversationId = conversationId.Value,
                ConversationTitle = title,
            });
        }

        internal static void ReportUITriggerBackendRenameConversationEvent(AssistantConversationId conversationId, string newTitle)
        {
            ReportUITriggerBackendEvent(new UITriggerBackendEventData(UITriggerBackendEventSubType.RenameConversation)
            {
                ConversationId = conversationId.Value,
                ConversationTitle = newTitle,
            });
        }

        #endregion

        #region Context Events

        internal const string k_ContextEvent = "AIAssistantContextEvent";

        [Serializable]
        internal class ContextEventData : IAnalytic.IData
        {
            public ContextEventData(ContextSubType subType)
            {
                SubType = subType.ToString();
                Timestamp = (long)(EditorApplication.timeSinceStartup * 1_000_000_000L); // In Microseconds like analytics. See SecondsToMicroSeconds in PerformanceReporting.h
            }

            public string SubType;
            public string ContextContent;
            public string ContextType;
            public string IsSuccessful;
            public string MessageId;
            public string ConversationId;
            public long Timestamp;

            internal void StampMessageId(AssistantMessageId messageId)
            {
                MessageId = messageId.FragmentId;
                ConversationId = ConversationIdOrNull(messageId.ConversationId);
            }
        }

        /// <summary>
        /// Accumulates context attachment analytics events until the message is acknowledged by the
        /// backend, at which point all pending events are flushed with the real backend message ID.
        /// </summary>
        internal class ContextAnalyticsCache
        {
            readonly System.Collections.Generic.List<ContextEventData> m_PendingAttachEvents = new();

            internal void AddAttachEvent(ContextEventData data) => m_PendingAttachEvents.Add(data);

            /// <summary>
            /// Sends all pending attach events stamped with <paramref name="messageId"/>, then clears
            /// the cache.
            /// </summary>
            internal void FlushAll(AssistantMessageId messageId)
            {
                foreach (var data in m_PendingAttachEvents)
                {
                    data.StampMessageId(messageId);
                    ReportContextEvent(data);
                }
                m_PendingAttachEvents.Clear();
            }

            /// <summary>
            /// Finds the most recent pending attach event whose SubType and ContextContent match
            /// <paramref name="contextEntry"/>, sends it stamped with <paramref name="messageId"/>,
            /// and removes it from the cache. Used for remove-single so the corresponding attach
            /// event is paired and sent together with the remove event.
            /// </summary>
            internal void FlushMatchingAttachEvent(AssistantContextEntry contextEntry, AssistantMessageId messageId)
            {
                var entryType = contextEntry.EntryType.ToString();
                var displayValue = contextEntry.DisplayValue;

                for (int i = m_PendingAttachEvents.Count - 1; i >= 0; i--)
                {
                    var pending = m_PendingAttachEvents[i];
                    if (pending.ContextType == entryType && pending.ContextContent == displayValue)
                    {
                        pending.StampMessageId(messageId);
                        ReportContextEvent(pending);
                        m_PendingAttachEvents.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        // Context Group
        [AnalyticInfo(eventName: k_ContextEvent, vendorKey: k_VendorKey)]
        class ContextEvent : IAnalytic
        {
            private readonly ContextEventData m_Data;

            public ContextEvent(ContextEventData data)
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

        static void ReportContextEvent(ContextEventData data)
        {
            SendGatedEditorAnalytic(new ContextEvent(data));
        }

        internal static void CacheContextDragDropAttachedContextEvent(ContextAnalyticsCache cache, UnityEngine.Object obj)
        {
            cache.AddAttachEvent(new ContextEventData(ContextSubType.DragDropAttachedContext)
            {
                ContextContent = obj.name,
                ContextType = obj.GetType().Name,
                IsSuccessful = "false",
            });
        }

        internal static void CacheContextDragDropAttachedContextEvent(ContextAnalyticsCache cache, AssistantContextEntry contextEntry)
        {
            cache.AddAttachEvent(new ContextEventData(ContextSubType.DragDropAttachedContext)
            {
                ContextContent = contextEntry.DisplayValue,
                ContextType = contextEntry.ValueType,
                IsSuccessful = "true",
            });
        }

        internal static void CacheContextDragDropImageFileAttachedContextEvent(ContextAnalyticsCache cache, string fileName, string fileExtension)
        {
            cache.AddAttachEvent(new ContextEventData(ContextSubType.DragDropImageFileAttachedContext)
            {
                ContextContent = fileName,
                ContextType = fileExtension,
                IsSuccessful = "true",
            });
        }

        internal static void CacheContextChooseContextFromFlyoutEvent(ContextAnalyticsCache cache, LogData logData)
        {
            cache.AddAttachEvent(new ContextEventData(ContextSubType.ChooseContextFromFlyout)
            {
                ContextContent = logData.Message,
                ContextType = "LogData",
            });
        }

        internal static void CacheContextChooseContextFromFlyoutEvent(ContextAnalyticsCache cache, UnityEngine.Object obj)
        {
            cache.AddAttachEvent(new ContextEventData(ContextSubType.ChooseContextFromFlyout)
            {
                ContextContent = obj.name,
                ContextType = obj.GetType().ToString(),
            });
        }

        internal static void CacheContextPingAttachedContextObjectFromFlyoutEvent(ContextAnalyticsCache cache, UnityEngine.Object obj)
        {
            cache.AddAttachEvent(new ContextEventData(ContextSubType.PingAttachedContextObjectFromFlyout)
            {
                ContextContent = obj.name,
                ContextType = obj.GetType().ToString(),
            });
        }

        internal static void CacheContextScreenshotAttachedContextEvent(ContextAnalyticsCache cache, string displayName)
        {
            cache.AddAttachEvent(new ContextEventData(ContextSubType.ScreenshotAttachedContext)
            {
                ContextContent = displayName,
                ContextType = "Image",
            });
        }

        internal static void CacheContextAnnotationAttachedContextEvent(ContextAnalyticsCache cache, string displayName)
        {
            cache.AddAttachEvent(new ContextEventData(ContextSubType.AnnotationAttachedContext)
            {
                ContextContent = displayName,
                ContextType = "Image",
            });
        }

        internal static void CacheContextUploadImageAttachedContextEvent(ContextAnalyticsCache cache, string displayName, string fileExtension)
        {
            cache.AddAttachEvent(new ContextEventData(ContextSubType.UploadImageAttachedContext)
            {
                ContextContent = displayName,
                ContextType = fileExtension,
            });
        }

        internal static void CacheContextClipboardImageAttachedContextEvent(ContextAnalyticsCache cache)
        {
            cache.AddAttachEvent(new ContextEventData(ContextSubType.ClipboardImageAttachedContext)
            {
                ContextType = "Image",
            });
        }

        /// <summary>
        /// Flushes all pending attach events from <paramref name="cache"/> stamped with
        /// <paramref name="conversationId"/>, then sends a ClearAllAttachedContext event.
        /// Call this when context is cleared without a message being sent (new conversation,
        /// window close, or user manually clearing all context).
        /// </summary>
        internal static void ReportContextClearAllAttachedContextEvent(ContextAnalyticsCache cache, AssistantConversationId conversationId)
        {
            var messageId = new AssistantMessageId(conversationId, null, AssistantMessageIdType.Internal);
            cache.FlushAll(messageId);
            ReportContextEvent(new ContextEventData(ContextSubType.ClearAllAttachedContext)
            {
                ConversationId = ConversationIdOrNull(conversationId),
            });
        }

        /// <summary>
        /// Flushes the matching pending attach event for <paramref name="contextEntry"/> from
        /// <paramref name="cache"/> stamped with <paramref name="conversationId"/>, then sends a
        /// RemoveSingleAttachedContext event. Call this when the user removes a single context item.
        /// </summary>
        internal static void ReportContextRemoveSingleAttachedContextEvent(ContextAnalyticsCache cache, AssistantConversationId conversationId, AssistantContextEntry contextEntry)
        {
            var messageId = new AssistantMessageId(conversationId, null, AssistantMessageIdType.Internal);
            cache.FlushMatchingAttachEvent(contextEntry, messageId);
            ReportContextEvent(new ContextEventData(ContextSubType.RemoveSingleAttachedContext)
            {
                ContextContent = contextEntry.DisplayValue,
                ContextType = contextEntry.EntryType.ToString(),
                ConversationId = ConversationIdOrNull(conversationId),
            });
        }

        #endregion

        #region Local UI Events

        internal const string k_UITriggerLocalEvent = "AIAssistantUITriggerLocalEvent";

        [Serializable]
        internal class UITriggerLocalEventData : IAnalytic.IData
        {
            public UITriggerLocalEventData(UITriggerLocalEventSubType subType) => SubType = subType.ToString();

            public string SubType;
            public string UsedInspirationalPrompt;
            public string SuggestionCategory;
            public string ChosenMode;
            public string ReferenceUrl;
            public string ConversationId;
            public string MessageId;
            public string ResponseMessage;
            public string PreviewParameter;
            public string FunctionId;
            public string UserAnswer;
            public string PermissionType;
            public long DurationMs;
            public string ContextItemCount;
            public string GeneratorType;
            public string Prompt;
            public string NegativePrompt;
            public string IsAIRunning;
            public string ActionValue;
            public string ErrorType;
        }

        [AnalyticInfo(eventName: k_UITriggerLocalEvent, vendorKey: k_VendorKey)]
        class UITriggerLocalEvent : IAnalytic
        {
            private readonly UITriggerLocalEventData m_Data;

            public UITriggerLocalEvent(UITriggerLocalEventData data)
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

        static void ReportUITriggerLocalEvent(UITriggerLocalEventData data)
        {
            SendGatedEditorAnalytic(new UITriggerLocalEvent(data));
        }

        internal static void ReportUITriggerLocalAIDropdownOpenedEvent()
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.AIDropdownOpened));
        }

        internal static void ReportUITriggerLocalCopyResponseEvent(AssistantMessageId messageId, string responseMessage)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.CopyResponse)
            {
                ConversationId = ConversationIdOrNull(messageId.ConversationId),
                MessageId = messageId.FragmentId,
                ResponseMessage = responseMessage,
            });
        }

        internal static void ReportUITriggerLocalCopyCodeEvent(AssistantConversationId conversationId, string responseMessage)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.CopyCode)
            {
                ConversationId = conversationId.Value,
                ResponseMessage = responseMessage,
            });
        }

        internal static void ReportUITriggerLocalSaveCodeEvent(AssistantConversationId conversationId, string responseMessage)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.SaveCode)
            {
                ConversationId = conversationId.Value,
                ResponseMessage = responseMessage,
            });
        }

        internal static void ReportUITriggerLocalExpandCommandLogicEvent()
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.ExpandCommandLogic));
        }

        internal static void ReportUITriggerLocalPermissionRequestedEvent(AssistantConversationId conversationId, ToolExecutionContext.CallInfo callInfo, ToolPermissions.PermissionType permissionType)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.PermissionRequested)
            {
                ConversationId = conversationId.Value,
                FunctionId = callInfo.FunctionId,
                PermissionType = permissionType.ToString(),
            });
        }

        internal static void ReportUITriggerLocalPermissionResponseEvent(AssistantConversationId conversationId, ToolExecutionContext.CallInfo callInfo, PermissionUserAnswer answer, ToolPermissions.PermissionType permissionType)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.PermissionResponse)
            {
                ConversationId = conversationId.Value,
                FunctionId = callInfo.FunctionId,
                UserAnswer = answer.ToString(),
                PermissionType = permissionType.ToString(),
            });
        }

        internal static void ReportUITriggerLocalOpenReferenceUrlEvent(string referenceUrl)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.OpenReferenceUrl)
            {
                ReferenceUrl = referenceUrl,
            });
        }

        /// <summary>
        /// Fired when the user closes the AI Assistant window.
        /// </summary>
        /// <param name="conversationId">The active conversation when the window was closed.</param>
        /// <param name="isAIRunning">Whether the AI was actively processing a response.</param>
        internal static void ReportUITriggerLocalWindowClosedEvent(AssistantConversationId conversationId, bool isAIRunning)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.WindowClosed)
            {
                ConversationId = ConversationIdOrNull(conversationId),
                IsAIRunning = isAIRunning.ToString(),
            });
        }

        internal static void ReportUITriggerLocalWindowOpenedEvent()
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.WindowOpened));
        }

        /// <summary>
        /// Fired when the user submits a prompt via an integration popup (e.g. Profiler, Project Auditor), just before the Assistant window opens.
        /// </summary>
        /// <param name="integrationName">String name of the calling integration (e.g. "CpuProfiler", "ProjectAuditor").</param>
        /// <param name="prompt">The user-submitted prompt text.</param>
        internal static void ReportUITriggerLocalOpenedViaIntegrationEvent(string integrationName, string prompt)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.OpenedViaIntegration)
            {
                ActionValue = integrationName,
                Prompt = prompt,
            });
        }

        /// <summary>
        /// Fired when the user changes a permission policy setting in the settings window.
        /// </summary>
        /// <param name="permissionSetting">The name of the permission setting that changed (e.g. "FileSystemReadProject")</param>
        /// <param name="newPolicy">The new policy value selected by the user</param>
        internal static void ReportUITriggerLocalPermissionSettingChangedEvent(string permissionSetting, IPermissionsPolicyProvider.PermissionPolicy newPolicy)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.PermissionSettingChanged)
            {
                PermissionType = permissionSetting,
                UserAnswer = newPolicy.ToString(),
            });
        }

        /// <summary>
        /// Fired when the user explicitly switches mode via the dropdown (Agent, Ask, Plan, etc.).
        /// </summary>
        /// <param name="conversationId">The active conversation at the time of the switch, if any.</param>
        /// <param name="modeId">The ID of the mode the user switched to (e.g. "Agent", "Ask", "Plan").</param>
        internal static void ReportUITriggerLocalModeSwitchedEvent(AssistantConversationId conversationId, string modeId)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.ModeSwitched)
            {
                ConversationId = ConversationIdOrNull(conversationId),
                ChosenMode = modeId,
            });
        }

        /// <summary>
        /// Fired when the user toggles the Auto-Run setting on or off.
        /// </summary>
        /// <param name="enabled">The new value of the Auto-Run setting</param>
        internal static void ReportUITriggerLocalAutoRunSettingChangedEvent(bool enabled)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.AutoRunSettingChanged)
            {
                UserAnswer = enabled.ToString(),
            });
        }

        /// <summary>
        /// Fired every time the new-conversation screen with suggestion prompts becomes visible.
        /// </summary>
        internal static void ReportUITriggerLocalNewChatSuggestionsShownEvent() =>
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.NewChatSuggestionsShown));

        /// <summary>
        /// Fired when the user clicks a suggestion category chip (e.g. "Troubleshoot", "Explore").
        /// </summary>
        /// <param name="categoryLabel">The label of the selected category chip.</param>
        internal static void ReportUITriggerLocalSuggestionCategorySelectedEvent(string categoryLabel)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.SuggestionCategorySelected)
            {
                SuggestionCategory = categoryLabel,
            });
        }

        /// <summary>
        /// Fired when the user approves the implementation plan in the plan review panel.
        /// </summary>
        /// <param name="conversationId">The active conversation.</param>
        /// <param name="planPath">Asset-relative path of the plan file being reviewed.</param>
        internal static void ReportUITriggerLocalPlanReviewApprovedEvent(AssistantConversationId conversationId, string planPath)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.PlanReviewApproved)
            {
                ConversationId = ConversationIdOrNull(conversationId),
                ResponseMessage = planPath,
            });
        }

        /// <summary>
        /// Fired when the user clicks a specific prompt within a suggestion category.
        /// </summary>
        /// <param name="categoryLabel">The category the prompt belongs to.</param>
        /// <param name="promptText">The text of the prompt that was clicked.</param>
        internal static void ReportUITriggerLocalSuggestionPromptSelectedEvent(string categoryLabel, string promptText)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.SuggestionPromptSelected)
            {
                SuggestionCategory = categoryLabel,
                UsedInspirationalPrompt = promptText,
            });
        }

        internal static void ReportUITriggerLocalRetryRelayConnectionEvent(AssistantConversationId conversationId = default)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.RetryRelayConnection)
            {
                ConversationId = conversationId.Value,
            });
        }

        internal static void ReportUITriggerLocalConversationRestoredEvent(AssistantConversationId conversationId, long durationMs)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.ConversationRestored)
            {
                ConversationId = conversationId.Value,
                DurationMs = durationMs,
            });
        }

        /// <summary>
        /// Fired when the user denies the implementation plan review.
        /// </summary>
        /// <param name="conversationId">The active conversation.</param>
        /// <param name="planPath">Asset-relative path of the plan file being reviewed.</param>
        internal static void ReportUITriggerLocalPlanReviewDeniedEvent(AssistantConversationId conversationId, string planPath)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.PlanReviewDenied)
            {
                ConversationId = ConversationIdOrNull(conversationId),
                ResponseMessage = planPath,
            });
        }

        /// <summary>
        /// Fired when the user approves the prompt to switch into Plan mode (clicks "Enter Plan mode").
        /// </summary>
        /// <param name="conversationId">The active conversation.</param>
        internal static void ReportUITriggerLocalEnterPlanModeApprovedEvent(AssistantConversationId conversationId)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.EnterPlanModeApproved)
            {
                ConversationId = ConversationIdOrNull(conversationId),
            });
        }

        /// <summary>
        /// Fired when the user declines the prompt to switch into Plan mode (clicks "Stay in Agent mode").
        /// </summary>
        /// <param name="conversationId">The active conversation.</param>
        internal static void ReportUITriggerLocalEnterPlanModeDeclinedEvent(AssistantConversationId conversationId)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.EnterPlanModeDeclined)
            {
                ConversationId = ConversationIdOrNull(conversationId),
            });
        }

        /// <summary>
        /// Fired when the user submits answers to the clarifying questions dialog.
        /// </summary>
        /// <param name="conversationId">The active conversation.</param>
        /// <param name="questionCount">Total number of questions presented.</param>
        internal static void ReportUITriggerLocalClarifyingQuestionSubmittedEvent(AssistantConversationId conversationId, int questionCount)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.ClarifyingQuestionSubmitted)
            {
                ConversationId = ConversationIdOrNull(conversationId),
                ResponseMessage = questionCount.ToString(),
            });
        }

        /// <summary>
        /// Fired when the user cancels the clarifying questions dialog without submitting.
        /// </summary>
        /// <param name="conversationId">The active conversation.</param>
        /// <param name="questionCount">Total number of questions that were presented.</param>
        internal static void ReportUITriggerLocalClarifyingQuestionCancelledEvent(AssistantConversationId conversationId, int questionCount)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.ClarifyingQuestionCancelled)
            {
                ConversationId = ConversationIdOrNull(conversationId),
                ResponseMessage = questionCount.ToString(),
            });
        }

        /// <summary>
        /// Fired when the user expands the Context accordion on a sent user message.
        /// </summary>
        /// <param name="messageId">The ID of the user message whose context was expanded.</param>
        /// <param name="contextItemCount">The number of context items attached to the message.</param>
        internal static void ReportUITriggerLocalExpandUserMessageContextEvent(AssistantMessageId messageId, int contextItemCount)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.ExpandUserMessageContext)
            {
                ConversationId = ConversationIdOrNull(messageId.ConversationId),
                MessageId = messageId.FragmentId,
                ContextItemCount = contextItemCount.ToString(),
            });
        }

        /// <summary>
        /// Fired when the user clicks the Chats button to toggle the history panel.
        /// </summary>
        /// <param name="isOpened">Whether the panel is now open or closed.</param>
        internal static void ReportUITriggerLocalToggleChatHistoryEvent(bool isOpened)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.ToggleChatHistory)
            {
                ActionValue = isOpened.ToString(),
            });
        }

        /// <summary>
        /// Fired when the user clicks the More button to scroll to the bottom of the conversation.
        /// </summary>
        internal static void ReportUITriggerLocalScrollToBottomEvent()
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.ScrollToBottom));
        }

        /// <summary>
        /// Fired when the user switches between the Code and Output tabs in a Run Command result.
        /// </summary>
        /// <param name="conversationId">The conversation the run command belongs to.</param>
        /// <param name="tabName">"Code" or "Output".</param>
        internal static void ReportUITriggerLocalSwitchRunCommandTabEvent(AssistantConversationId conversationId, string tabName)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.SwitchRunCommandTab)
            {
                ConversationId = ConversationIdOrNull(conversationId),
                ActionValue = tabName,
            });
        }

        /// <summary>
        /// Fired when the user clicks to expand or collapse a function call result details section.
        /// </summary>
        /// <param name="conversationId">The conversation the function call belongs to.</param>
        /// <param name="functionId">The tool/function name that was called.</param>
        /// <param name="expanded">Whether the section is now expanded or collapsed.</param>
        internal static void ReportUITriggerLocalExpandFunctionCallResultEvent(AssistantConversationId conversationId, string functionId, bool expanded)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.ExpandFunctionCallResult)
            {
                ConversationId = ConversationIdOrNull(conversationId),
                FunctionId = functionId,
                ActionValue = expanded.ToString(),
            });
        }

        /// <summary>
        /// Fired when the user clicks "Open Assistant Settings" from the settings popup.
        /// </summary>
        internal static void ReportUITriggerLocalOpenAssistantSettingsEvent()
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.OpenAssistantSettings));
        }

        /// <summary>
        /// Fired when the user toggles the "Collapse Reasoning when complete" setting.
        /// </summary>
        /// <param name="enabled">The new value of the setting.</param>
        internal static void ReportUITriggerLocalCollapseReasoningSettingChangedEvent(bool enabled, string chosenMode)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.CollapseReasoningSettingChanged)
            {
                ActionValue = enabled.ToString(),
                ChosenMode = chosenMode,
            });
        }

        /// <summary>
        /// Fired when the user clicks the Generate button in a generator (Animate, Material, Sound).
        /// </summary>
        /// <param name="generatorType">The generator type: "Animate", "Material", or "Sound".</param>
        /// <param name="prompt">The user's prompt text.</param>
        /// <param name="negativePrompt">The user's negative prompt text.</param>
        internal static void ReportUITriggerLocalGenerateContentEvent(string generatorType, string prompt, string negativePrompt)
        {
            var data = new UITriggerLocalEventData(UITriggerLocalEventSubType.GenerateContent)
            {
                GeneratorType = generatorType,
                Prompt = prompt,
                NegativePrompt = negativePrompt,
            };
            EditorAnalytics.SendAnalytic(new UITriggerLocalEvent(data));
        }

        /// <summary>
        /// Fired when the user switches tabs in the Image generator (e.g. Generate, Remove BG, Spritesheet).
        /// </summary>
        /// <param name="modeName">The display label of the selected tab.</param>
        internal static void ReportUITriggerLocalChangeGeneratorModeEvent(string modeName)
        {
            var data = new UITriggerLocalEventData(UITriggerLocalEventSubType.ChangeGeneratorMode)
            {
                ActionValue = modeName,
            };
            EditorAnalytics.SendAnalytic(new UITriggerLocalEvent(data));
        }

        /// <summary>
        /// Fired when an error message becomes visible to the user. Fires once per show transition,
        /// not per render, so a banner that stays visible across refreshes does not re-fire.
        /// </summary>
        /// <param name="errorType">Discriminator for the error surface (e.g. "relay_stopped", "acp_credential_error", "chat_error_block").</param>
        /// <param name="conversationId">The active conversation when the error was shown, if any.</param>
        /// <param name="errorMessage">Optional human-readable error message for surfaces that produce variable text (e.g. chat error blocks).</param>
        internal static void ReportUITriggerLocalErrorDisplayedEvent(string errorType, AssistantConversationId conversationId = default, string errorMessage = null)
        {
            ReportUITriggerLocalEvent(new UITriggerLocalEventData(UITriggerLocalEventSubType.ErrorDisplayed)
            {
                ErrorType = errorType,
                ConversationId = ConversationIdOrNull(conversationId),
                ResponseMessage = errorMessage,
            });
        }

        // EARLY_ACCESS_PROMO_START
        internal static void ReportEarlyAccessSignup(string email)
        {
            const string source = "empty_state_promo_banner";
            var data = new UITriggerLocalEventData(UITriggerLocalEventSubType.DesktopEarlyAccessSignup)
            {
                UserAnswer = email,
                ResponseMessage = Account.sessionStatus.IsUsable ? CloudProjectSettings.organizationKey : string.Empty,
                ActionValue = source,
            };
            EditorAnalytics.SendAnalytic(new UITriggerLocalEvent(data));
        }
        // EARLY_ACCESS_PROMO_END

        #endregion
    }
}
