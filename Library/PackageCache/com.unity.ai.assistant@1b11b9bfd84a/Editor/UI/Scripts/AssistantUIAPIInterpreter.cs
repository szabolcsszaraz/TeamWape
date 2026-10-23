using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Agents;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Acp;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.Editor.CodeBlock;
using Unity.AI.Assistant.Editor.Context;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Editor.Checkpoint;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using Unity.AI.Assistant.Utils;
using Unity.AI.Assistant.Utils.Perf;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Accounts;
using AccountService = Unity.AI.Toolkit.Accounts.Services.Account;
using Unity.AI.Tracing;
using ErrorInfo = Unity.AI.Assistant.Editor.ErrorInfo;
using TaskUtils = Unity.AI.Assistant.Editor.Utils.TaskUtils;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    internal class AssistantUIAPIInterpreter
    {
        const int k_PointsBalanceRefreshDelayMs = 5000;
        const int k_PointsBalanceRefreshRetryDelayMs = 10000;

        static readonly object k_PointsBalanceRefreshLock = new();
        static CancellationTokenSource s_PointsBalanceRefreshCts;

        readonly AssistantBlackboard m_Blackboard;
        readonly Func<ModelConfiguration> m_GetModelConfiguration;
        readonly UserInteractionQueue m_InteractionQueue;

        public AssistantUIAPIInterpreter(IAssistantProvider provider, AssistantBlackboard blackboard, UserInteractionQueue interactionQueue, Func<ModelConfiguration> getModelConfiguration = null)
        {
            m_Blackboard = blackboard;
            m_InteractionQueue = interactionQueue;
            Provider = provider;
            m_GetModelConfiguration = getModelConfiguration;
        }

        /// <summary>
        /// The current Assistant provider in use, generally try to avoid using this directly, the interpreter functions should suffice
        /// </summary>
        public IAssistantProvider Provider { get; private set; }

        public void Initialize()
        {
            Provider.ConversationLoaded += OnConversationLoaded;
            Provider.ConversationCreated += OnConversationCreated;
            Provider.ConversationChanged += OnConversationChanged;
            Provider.ConversationDeleted += OnConversationDeleted;
            Provider.ConversationsRefreshed += OnConversationsRefreshed;
            Provider.ConversationErrorOccured += OnConversationErrorOccured;
            Provider.CapacityReached += OnCapacityReached;

            Provider.PromptStateChanged += OnPromptStateChanged;

            Provider.FeedbackLoaded += OnFeedbackLoaded;
            Provider.FeedbackSent += OnFeedbackSent;
            Provider.MessageCostReceived += OnMessageCostReceived;

            Provider.IncompleteMessageStarted += OnIncompleteMessageStarted;
            Provider.IncompleteMessageCompleted += OnIncompleteMessageCompleted;

            // Capability events
            Provider.ModelsAvailable += OnModelsAvailable;
            Provider.AvailableCommandsChanged += OnAvailableCommandsChanged;

            if (AssistantProjectPreferences.CheckpointEnabled && AssistantCheckpoints.IsInitialized)
            {
                _ = AssistantCheckpoints.RefreshTagsCacheAsync();
            }
        }

        public event Action<AssistantConversationId> ConversationReload;
        public event Action<AssistantConversationId> ConversationLoadFailed;
        public event Action<AssistantConversationId> ConversationChanged;
        public event Action<AssistantConversationId> ConversationDeleted;
        public event Action ConversationsRefreshed;
        public event Action APIStateChanged;

        public event Action<AssistantMessageId, FeedbackData?> FeedbackLoaded;
        public event Action<AssistantMessageId, bool> FeedbackSent;
        public event Action<AssistantMessageId, int?> MessageCostReceived;

        // Read only from ResponseFeedbackQueue.Update; GetInvocationList allocates and must never be called on a hot path.
        internal int FeedbackLoadedSubscriberCount => FeedbackLoaded?.GetInvocationList().Length ?? 0;

        // Capability events - forwarded from providers that support them
        public event Action<(string modelId, string name, string description)[], string> ModelsAvailable;
        public event Action<(string name, string description)[]> AvailableCommandsChanged;

        /// <summary>
        /// Manually triggers the ConversationsRefreshed event.
        /// Used by ConversationLoader to notify UI after loading conversations.
        /// </summary>
        public void TriggerConversationsRefreshed()
        {
            MainThread.DispatchIfNeeded(() => ConversationsRefreshed?.Invoke());
        }

        void OnConversationChanged(AssistantConversation data)
        {
            AssistantPerf.Count(PerfProbe.ConversationChangedRequested);

            DispatchUtility.DispatchWithOverride(data.Id.Value, () =>
            {
                AssistantPerf.Count(PerfProbe.ConversationChangedDispatched);

                var model = ConvertConversationToModel(data);

                if (!m_Blackboard.ActiveConversationId.IsValid)
                {
                    m_Blackboard.SetActiveConversation(data.Id);
                }

                ConversationChanged?.Invoke(model.Id);
            });
        }

        void NotifyAPIStateChanged(AssistantConversationId conversationId)
        {
            if (m_Blackboard.ActiveConversationId.IsValid && conversationId != m_Blackboard.ActiveConversationId)
                return;

            MainThread.DispatchAndForget(() => APIStateChanged?.Invoke());
        }

        void OnFeedbackLoaded(AssistantMessageId messageId, FeedbackData? feedback)
        {
            MainThread.DispatchAndForget(() => FeedbackLoaded?.Invoke(messageId, feedback));
        }

        void OnFeedbackSent(AssistantMessageId messageId, bool success)
        {
            MainThread.DispatchIfNeeded(() => FeedbackSent?.Invoke(messageId, success));
        }

        void OnMessageCostReceived(AssistantMessageId assistantMessageId, int? cost, bool isNewMessage)
        {
            MainThread.DispatchAndForget(() =>
            {
                if (cost.HasValue && isNewMessage)
                    AIToolbarButton.ShowPointsCostNotification(cost.Value);

                MessageCostReceived?.Invoke(assistantMessageId, cost);
            });
        }

        void OnIncompleteMessageStarted(AssistantConversationId conversationId, string messageId)
        {
            // Must run on main thread because EditorPrefs can only be accessed from main thread
            MainThread.DispatchAndForget(() =>
            {
                m_Blackboard.SetIncompleteMessageId(messageId);

                // The blackboard helper returns early while state saving is suspended
                // (AssistantView.InitializeView until the end of RestoreUIState — a window that can
                // span frames, because RestoreUIState defers itself while waiting for a conversation
                // change). Assistant.ProcessPromptInternal releases the pre-conversation recovery
                // marker on the dispatch queued immediately behind this one, so a dropped write here
                // would leave the turn with neither state set and a reload in that gap would lose the
                // prompt. Persist directly instead, the same escape hatch RestoreUIState's stale-id
                // mop-up uses.
                if (AssistantUISessionState.instance.IncompleteMessageId != messageId)
                {
                    AssistantUISessionState.instance.IncompleteMessageId = messageId;
                }

                // Ensure UI shows stop button when there's an incomplete message
                SetWorkingState(true);
            });
        }

        void OnIncompleteMessageCompleted(AssistantConversationId conversationId)
        {
            // Must run on main thread because EditorPrefs can only be accessed from main thread
            MainThread.DispatchAndForget(() =>
            {
                m_Blackboard.ClearIncompleteMessageId();
                SchedulePointsBalanceRefresh();
            });
        }

        void OnConversationDeleted(AssistantConversationId conversationId)
        {
            if (m_Blackboard.RemoveConversation(conversationId))
            {
                if (m_Blackboard.ActiveConversationId == conversationId)
                    m_Blackboard.SetActiveConversation(AssistantConversationId.Invalid);

                MainThread.DispatchAndForget(() => ConversationDeleted?.Invoke(conversationId));
            }
        }

        void OnConversationsRefreshed(IEnumerable<AssistantConversationInfo> infos)
        {
            // ConversationLoader handles all conversation population.
            // This just forwards the event to the UI.
            MainThread.DispatchAndForget(() => ConversationsRefreshed?.Invoke());
        }

        void OnPromptStateChanged(AssistantConversationId conversationId, Assistant.Editor.Assistant.PromptState newState)
        {
            MainThread.DispatchIfNeeded(() => HandlePromptStateSync(conversationId, newState));
        }

        void HandlePromptStateSync(AssistantConversationId conversationId, Assistant.Editor.Assistant.PromptState newState)
        {
            Trace.Event("ui.prompt_state", new TraceEventOptions
            {
                Level = "debug",
                Data = new { conversation = conversationId.Value, newState = newState.ToString(), wasWorking = m_Blackboard.IsAPIWorking }
            });

            if (conversationId != m_Blackboard.ActiveConversationId)
            {
                InternalLog.Log($"Ignoring state request change for non-active conversation: incoming={conversationId.Value}, active={m_Blackboard.ActiveConversationId.Value}");
                return;
            }

            m_Blackboard.IsAPIStreaming = false;
            m_Blackboard.IsAPIRepairing = false;
            m_Blackboard.IsAPIReadyForPrompt = false;
            m_Blackboard.IsAPICanceling = false;

            switch (newState)
            {
                case Assistant.Editor.Assistant.PromptState.NotConnected:
                {
                    SetWorkingState(false);
                    m_Blackboard.IsAPIReadyForPrompt = true;
                    break;
                }

                case Assistant.Editor.Assistant.PromptState.Connected:
                {
                    SetWorkingState(false);
                    m_Blackboard.IsAPIReadyForPrompt = true;
                    m_Blackboard.PendingPrompt = null;
                    break;
                }
                case Assistant.Editor.Assistant.PromptState.Connecting:
                case Assistant.Editor.Assistant.PromptState.AwaitingServer:
                case Assistant.Editor.Assistant.PromptState.AwaitingClient:
                {
                    SetWorkingState(true);
                    m_Blackboard.IsAPIStreaming = true;
                    break;
                }

                case Assistant.Editor.Assistant.PromptState.Canceling:
                {
                    SetWorkingState(false);
                    m_Blackboard.IsAPICanceling = true;
                    m_Blackboard.IsAPIReadyForPrompt = false;
                    // Clear incomplete message so we don't recover it after domain reload
                    m_Blackboard.ClearIncompleteMessageId();
                    break;
                }
            }

            NotifyAPIStateChanged(conversationId);
        }

        static void SchedulePointsBalanceRefresh()
        {
            var refreshCts = new CancellationTokenSource();
            // Capture the token before publishing refreshCts to s_PointsBalanceRefreshCts. Once it's
            // published, a concurrent call can pick it up as its previousCts and dispose it, after which
            // refreshCts.Token would throw ObjectDisposedException.
            var token = refreshCts.Token;
            CancellationTokenSource previousCts;

            lock (k_PointsBalanceRefreshLock)
            {
                previousCts = s_PointsBalanceRefreshCts;
                s_PointsBalanceRefreshCts = refreshCts;
            }

            CancelAndDisposePointsBalanceRefresh(previousCts);

            TaskUtils.WithExceptionLogging(async () =>
            {
                try
                {
                    await EditorTask.Delay(k_PointsBalanceRefreshDelayMs, token);
                    await AccountService.pointsBalance.RefreshPointsBalance();

                    // Points commits can take a few seconds to become visible to the account service.
                    // Retry once so the UI does not remain stale until the next editor restart.
                    await EditorTask.Delay(k_PointsBalanceRefreshRetryDelayMs, token);
                    await AccountService.pointsBalance.RefreshPointsBalance();
                }
                catch (OperationCanceledException)
                {
                    // A newer terminal event scheduled a fresher balance refresh.
                }
                finally
                {
                    var shouldDispose = false;
                    lock (k_PointsBalanceRefreshLock)
                    {
                        if (ReferenceEquals(s_PointsBalanceRefreshCts, refreshCts))
                        {
                            s_PointsBalanceRefreshCts = null;
                            shouldDispose = true;
                        }
                    }

                    if (shouldDispose)
                        refreshCts.Dispose();
                }
            });
        }

        static void CancelAndDisposePointsBalanceRefresh(CancellationTokenSource cts)
        {
            if (cts == null)
                return;

            try
            {
                cts.Cancel();
            }
            finally
            {
                cts.Dispose();
            }
        }

        void OnConversationErrorOccured(AssistantConversationId conversationId, ErrorInfo info)
        {
            var isActiveConversation = m_Blackboard.ActiveConversationId == conversationId || !m_Blackboard.ActiveConversationId.IsValid;

            try
            {
                var conversation = m_Blackboard.GetConversation(conversationId);

                if (conversation == null)
                {
                    ErrorHandlingUtility.PublicLogError(info);
                    SetWorkingState(false);

                    // No conversation data means no reload event will follow; let listeners
                    // (e.g. the post-domain-reload restore) unblock instead of waiting forever.
                    MainThread.DispatchAndForget(() => ConversationLoadFailed?.Invoke(conversationId));
                }
                else
                {
                    if (isActiveConversation)
                    {
                        ErrorHandlingUtility.InternalLogError(info);
                        conversation.Messages.Add(new MessageModel()
                        {
                            Role = MessageModelRole.Error,
                            IsComplete = true,
                            Blocks = new List<IMessageBlockModel> { new ErrorBlockModel { Error = info.PublicMessage } },
                        });

                        MainThread.DispatchAndForget(() => ConversationChanged?.Invoke(conversation.Id));
                    }

                    Provider.AbortPrompt(conversationId);

                    if (m_Blackboard.IsAPIWorking)
                    {
                        SetWorkingState(false);
                    }
                }
            }
            finally
            {
                if (AssistantProviderFactory.IsUnityProvider(Provider.ProviderId))
                    TracesUploader.UploadTraces(conversationId.Value, "conversation-error");
            }

            SchedulePointsBalanceRefresh();

            if (isActiveConversation)
            {
                var message = info.PublicMessage;
                MainThread.DispatchIfNeeded(() => AssistantEvents.Send(new EventRetryableErrorOccurred(message)));
            }
        }

        void OnCapacityReached(AssistantConversationId conversationId)
        {
            MainThread.DispatchIfNeeded(() => AssistantEvents.Send(new EventCapacityReached(conversationId)));
        }

        public void Deinitialize()
        {
            Provider.ConversationsRefreshed -= OnConversationsRefreshed;
            Provider.ConversationErrorOccured -= OnConversationErrorOccured;
            Provider.CapacityReached -= OnCapacityReached;
            Provider.PromptStateChanged -= OnPromptStateChanged;
            Provider.ConversationLoaded -= OnConversationLoaded;
            Provider.ConversationCreated -= OnConversationCreated;
            Provider.ConversationChanged -= OnConversationChanged;
            Provider.ConversationDeleted -= OnConversationDeleted;
            Provider.FeedbackLoaded -= OnFeedbackLoaded;
            Provider.FeedbackSent -= OnFeedbackSent;
            Provider.MessageCostReceived -= OnMessageCostReceived;
            Provider.IncompleteMessageStarted -= OnIncompleteMessageStarted;
            Provider.IncompleteMessageCompleted -= OnIncompleteMessageCompleted;

            // Capability events
            Provider.ModelsAvailable -= OnModelsAvailable;
            Provider.AvailableCommandsChanged -= OnAvailableCommandsChanged;
        }

        /// <summary>
        /// Switch to a different provider. Handles event rebinding.
        /// </summary>
        public void SwitchProvider(IAssistantProvider newProvider)
        {
            if (Provider != null)
            {
                Deinitialize();
            }

            Provider = newProvider;
            Initialize();

            // Notify the provider state observer so UI components can react
            // For ACP providers, this sets state to Initializing and triggers banner attachment
            ProviderStateObserver.SetProvider(newProvider?.ProviderId);
        }

        void OnConversationCreated(AssistantConversation conversation)
        {
            MainThread.DispatchAndForget(() =>
            {
                var model = ConvertConversationToModel(conversation);

                // Only set as active if no other conversation is active
                if (!m_Blackboard.ActiveConversationId.IsValid)
                {
                    m_Blackboard.SetActiveConversation(conversation.Id);
                }

                ConversationReload?.Invoke(model.Id);
            });
        }

        void OnConversationLoaded(AssistantConversation conversation)
        {
            MainThread.DispatchAndForget(() =>
            {
                var model = ConvertConversationToModel(conversation);
                HandlePromptStateSync(m_Blackboard.ActiveConversationId, Assistant.Editor.Assistant.PromptState.NotConnected);
                ConversationReload?.Invoke(model.Id);
            });
        }

        internal ConversationModel ConvertConversationToModel(AssistantConversation conversation)
        {
            using var perfScope = AssistantPerf.Measure(PerfProbe.ConvertConversationToModel);

            var model = m_Blackboard.GetConversation(conversation.Id);
            if (model == null)
            {
                model = new ConversationModel
                {
                    Id = conversation.Id,
                    ProviderId = Provider.ProviderId
                };
                m_Blackboard.UpdateConversation(model.Id, model);
            }

            model.Title = conversation.Title;
            model.ContextUsageUsedTokens = conversation.ContextUsageUsedTokens;
            model.ContextUsageMaxTokens = conversation.ContextUsageMaxTokens;
            model.Messages.Clear();

            foreach (AssistantMessage message in conversation.Messages)
            {
                var messageModel = ConvertMessageToModel(message);
                model.Messages.Add(messageModel);
            }

            AssistantPerf.SetGauge(PerfGauge.ConversationMessages, model.Messages.Count);
            return model;
        }

        public void ConversationLoad(AssistantConversationId conversationId)
        {
            TaskUtils.WithExceptionLogging(() => Provider.ConversationLoad(conversationId));
        }

        public void RecoverIncompleteMessage(AssistantConversationId conversationId)
        {
            Assistant.Utils.TaskUtils.WithExceptionLogging(Provider.RecoverIncompleteMessage(conversationId));
        }

        public void SetFavorite(AssistantConversationId conversationId, bool isFavorited)
        {
            Provider.ConversationFavoriteToggle(conversationId, isFavorited);

            // Set the local caches so we are in sync until the next server data
            var conversation = m_Blackboard.GetConversation(conversationId);
            if (conversation != null)
            {
                conversation.IsFavorite = isFavorited;
            }

            m_Blackboard.SetFavorite(conversationId, isFavorited);

            MainThread.DispatchAndForget(() => ConversationsRefreshed?.Invoke());
        }

        public void ConversationDelete(AssistantConversationId conversationId)
        {
            TaskUtils.WithExceptionLogging(() => Provider.ConversationDeleteAsync(conversationId));
        }

        public void ConversationRename(AssistantConversationId conversationId, string newName)
        {
            TaskUtils.WithExceptionLogging(() => Provider.ConversationRename(conversationId, newName));
        }

        public void SuspendConversationRefresh()
        {
            Provider.SuspendConversationRefresh();
        }

        public void ResumeConversationRefresh()
        {
            Provider.ResumeConversationRefresh();
        }

        public void RefreshConversations()
        {
            TaskUtils.WithExceptionLogging(() => Provider.RefreshConversationsAsync(enforceCooldown: true));
        }

        public void CancelAssistant(AssistantConversationId conversationId)
        {
            Provider.AbortPrompt(conversationId);
            SchedulePointsBalanceRefresh();
            // Provider prompt state changes drive working state once cancellation completes.
        }

        /// <summary>
        /// Note: This function is assumed to be called from the main thread, calling this outside of that will cause errors
        ///       In the Prompt State handling
        /// </summary>
        public void Reset()
        {
            HandlePromptStateSync(m_Blackboard.ActiveConversationId, Assistant.Editor.Assistant.PromptState.NotConnected);
        }

        public void CancelPrompt()
        {
            if (m_Blackboard.IsAPIStreaming)
            {
                Provider.AbortPrompt(m_Blackboard.ActiveConversationId);
                SchedulePointsBalanceRefresh();
            }
        }

        public async Task EndActiveSessionAsync()
        {
            try
            {
                await Provider.EndSessionAsync(m_Blackboard.ActiveConversationId);
            }
            finally
            {
                SchedulePointsBalanceRefresh();
            }
        }

        public void SendPrompt(string stringPrompt, AssistantMode assistantMode, IAgent agent = null, CancellationToken ct = default)
        {
            if (!m_Blackboard.IsAPIWorking)
            {
                SetWorkingState(true);
            }

            // A new submission supersedes any incomplete message left over from an earlier turn, so
            // drop the id now rather than letting this send's own handoff overwrite it later. If a
            // leftover id survives into this submit and a reload then lands in this prompt's
            // pre-conversation window, AssistantView.RestoreUIState sees a non-empty incomplete id and
            // takes the "recover incomplete message" branch — so the re-drive branch, which requires an
            // empty incomplete id, never runs and this prompt is lost with no log line. Leftovers are
            // reachable — HandlePromptStateSync's NotConnected transition returns the UI to accepting
            // prompts without clearing the id (only Canceling clears), and the blackboard's clear
            // no-ops while state saving is suspended. Discarding it here is safe because every path
            // that reaches SendPrompt has already established the assistant is not working. Written
            // directly, as RestoreUIState's stale-id mop-up is, for that same suspension reason.
            AssistantUISessionState.instance.IncompleteMessageId = null;

            RemoveErrorFromCurrentConversation();
            m_Blackboard.PendingPrompt = stringPrompt;
            var prompt = BuildPrompt(stringPrompt, assistantMode);
            prompt.ModelConfiguration = m_GetModelConfiguration?.Invoke();
            prompt.ContextAnalyticsCache = m_Blackboard.ContextAnalyticsCache;

            // Arm pre-conversation recovery synchronously, inside the submit frame. Provider.ProcessPrompt
            // re-arms it too, but only after WithExceptionLogging's Task.Yield defers ProcessPrompt's
            // synchronous section to a later editor tick — a domain reload in that one-tick gap (first
            // prompt of a session, no prior marker) would otherwise leave no marker and no incomplete id,
            // and the prompt would be lost with no log line. Arming here closes that residual window; the
            // ProcessPrompt arm remains as an idempotent re-arm. The release side
            // (Assistant.ProcessPromptInternal) is Unity-provider-only, so only arm for a Unity provider —
            // a non-Unity provider never reaches that release path and would strand the marker.
            if (AssistantProviderFactory.IsUnityProvider(Provider.ProviderId))
                global::Unity.AI.Assistant.Editor.Assistant.SetPendingPromptRecovery(prompt);

            TaskUtils.WithExceptionLogging(() => Provider.ProcessPrompt(m_Blackboard.ActiveConversationId, prompt, agent, ct));
        }

        void RemoveErrorFromCurrentConversation()
        {
            // Remove any error messages in the active conversation
            if (m_Blackboard.ActiveConversationId.IsValid)
            {
                var conversation = m_Blackboard.GetConversation(m_Blackboard.ActiveConversationId);

                // Conversation may not exist yet if provider was just switched
                if (conversation == null)
                    return;

                var removed = conversation.Messages.RemoveAll(
                    m => m.Role == MessageModelRole.Error || m.Role == MessageModelRole.Info);

                if (removed > 0)
                    MainThread.DispatchAndForget(() => ConversationChanged?.Invoke(conversation.Id));
            }
        }

        AssistantPrompt BuildPrompt(string stringPrompt, AssistantMode assistantMode)
        {
            var prompt = new AssistantPrompt(stringPrompt, assistantMode);
            prompt.ObjectAttachments.AddRange(m_Blackboard.ObjectAttachments);
            prompt.VirtualAttachments.AddRange(m_Blackboard.VirtualAttachments);
            prompt.ConsoleAttachments.AddRange(m_Blackboard.ConsoleAttachments);

            return prompt;
        }

        public void SendFeedback(AssistantMessageId messageId, bool flagMessage, string feedbackText, bool upVote)
        {
            TaskUtils.WithExceptionLogging(() => Provider.SendFeedback(messageId, flagMessage, feedbackText, upVote));
        }

        public void LoadFeedback(AssistantMessageId messageId)
        {
            TaskUtils.WithExceptionLogging(() => Provider.LoadFeedback(messageId));
        }

        public void FetchMessageCost(AssistantMessageId messageId)
        {
            TaskUtils.WithExceptionLogging(() => Provider.FetchMessageCost(messageId));
        }

        public async Task RevertMessage(AssistantMessageId messageId)
        {
            var task = TaskUtils.WithExceptionLogging(() => Provider.RevertMessage(messageId));
            await task;
        }

        public void RefreshConversation()
        {
            Provider.ConversationRefresh(m_Blackboard.ActiveConversationId);
        }

        public bool ValidateCode(string code, out string localFixedCode, out CompilationErrors compilationErrors)
        {
            return CodeBlockValidatorUtils.ValidateCode(code, out localFixedCode, out compilationErrors);
        }

        public int GetAttachedContextLength()
        {
            var contextBuilder = new ContextBuilder();
            PromptUtils.GetAttachedContextString(BuildPrompt(string.Empty, AssistantMode.Undefined), ref contextBuilder, true);
            return contextBuilder.PredictedLength;
        }

        IMessageBlockModel ConvertAssistantBlockToBlockModel(IAssistantMessageBlock block)
        {
            return block switch
            {
                PromptBlock b => new PromptBlockModel { Content = b.Content },
                AnswerBlock b => new AnswerBlockModel { Content = b.Content, IsComplete = b.IsComplete},
                ThoughtBlock b => new ThoughtBlockModel { Content = b.Content },
                FunctionCallBlock b => new FunctionCallBlockModel{ Call = b.Call },
                ErrorBlock b => new ErrorBlockModel{ Error = b.Error },
                InfoBlock b => new InfoBlockModel { Message = b.Message },
                AcpToolCallBlock b => new AcpToolCallBlockModel
                {
                    CallInfo = b.CallInfo,
                    LatestUpdate = b.LatestUpdate,
                    PendingPermission = b.PendingPermission,
                    PermissionResponse = b.PermissionResponse,
                    IsReasoning = b.IsReasoning,
                    RawInput = b.RawInput
                },
                AcpPlanBlock b => new AcpPlanBlockModel { Entries = b.Entries },
                AcpToolCallStorageBlock storageBlock => ConvertStorageBlockToModel(storageBlock),
                AcpPlanStorageBlock planStorageBlock => ConvertPlanStorageBlockToModel(planStorageBlock),
                _ => throw new InvalidDataException("Unknown block type: " + block.GetType())
            };
        }

        IMessageBlockModel ConvertStorageBlockToModel(AcpToolCallStorageBlock storageBlock)
        {
            // Convert storage block to UI block, then to model
            // This is a fallback for cases where conversion didn't happen during load
            var toolCallBlock = AcpToolCallBlock.FromStorageBlock(storageBlock);
            return new AcpToolCallBlockModel
            {
                CallInfo = toolCallBlock.CallInfo,
                LatestUpdate = toolCallBlock.LatestUpdate,
                PendingPermission = toolCallBlock.PendingPermission,
                PermissionResponse = toolCallBlock.PermissionResponse,
                IsReasoning = toolCallBlock.IsReasoning,
                RawInput = toolCallBlock.RawInput
            };
        }

        IMessageBlockModel ConvertPlanStorageBlockToModel(AcpPlanStorageBlock storageBlock)
        {
            var storageData = storageBlock?.PlanData?.ToObject<AcpPlanStorageData>();
            var entries = storageData?.Entries ?? new List<AcpPlanStorageEntry>();

            var planEntries = new List<AcpPlanEntry>(entries.Count);
            foreach (var entry in entries)
            {
                planEntries.Add(new AcpPlanEntry
                {
                    Content = entry.Content ?? "",
                    Status = string.IsNullOrEmpty(entry.Status) ? "pending" : entry.Status,
                    Priority = entry.Priority ?? ""
                });
            }

            return new AcpPlanBlockModel { Entries = planEntries };
        }

        // Strip out context entries that are technical metadata for backend tools and
        // should never appear in the conversation history UI (e.g. AttachedImageReference).
        internal static AssistantContextEntry[] FilterUiVisibleContext(AssistantContextEntry[] context)
        {
            return context?
                .Where(c => c.ValueType != ImageReferenceImporter.k_ValueType)
                .ToArray();
        }

        MessageModel ConvertMessageToModel(AssistantMessage message)
        {
            var result = new MessageModel
            {
                Id = message.Id,
                Blocks = new List<IMessageBlockModel>(),
                IsComplete = message.IsComplete,
                Context = FilterUiVisibleContext(message.Context),
                Timestamp = message.Timestamp,
                HasCheckpoint = AssistantCheckpoints.HasCheckpointForMessage(message.Id.ConversationId, message.Id.FragmentId),
				RevertedTimeStamp = message.RevertedTimeStamp,
            };

            foreach (var block in message.Blocks)
            {
                var blockModel = ConvertAssistantBlockToBlockModel(block);
                result.Blocks.Add(blockModel);
            }

            if (message.IsError)
            {
                result.Role = MessageModelRole.Error;
                result.IsComplete = true;
            }
            else if (message.IsInformational)
            {
                result.Role = MessageModelRole.Info;
                result.IsComplete = true;
            }
            else
            {
                switch (message.Role.ToLower())
                {
                    case Assistant.Editor.Assistant.k_AssistantRole:
                    {
                        result.Role = MessageModelRole.Assistant;
                        break;
                    }

                    case Assistant.Editor.Assistant.k_UserRole:
                    {
                        result.Role = MessageModelRole.User;
                        break;
                    }

                    case Assistant.Editor.Assistant.k_SystemRole:
                    {
                        result.Role = MessageModelRole.System;
                        break;
                    }

                    default:
                    {
                        throw new InvalidDataException("Unknown message role: " + message.Role);
                    }
                }
            }

            return result;
        }

        public void SetWorkingState(bool isWorking)
        {
            if (m_Blackboard.IsAPIWorking == isWorking)
            {
                Trace.Event("ui.working_state.unchanged", new TraceEventOptions { Level = "debug", Data = new { isWorking } });
                return;
            }

            Trace.Event("ui.working_state.change", new TraceEventOptions
            {
                Level = "debug",
                Data = new { from = m_Blackboard.IsAPIWorking, to = isWorking, conversation = m_Blackboard.ActiveConversationId.Value }
            });

            m_Blackboard.IsAPIWorking = isWorking;
            if (isWorking && m_Blackboard.ActiveConversation != null)
            {
                m_Blackboard.ActiveConversation.StartTime = 0;
            }

            // When the assistant stops working, clear any pending transient interactions
            // (e.g. permission panels) — they are stale at this point. Runs atomically with
            // the state update and event because OnPromptStateChanged is always dispatched
            // to the main thread at the subscription site.
            if (!isWorking)
            {
                m_InteractionQueue.CancelTransient();
            }

            APIStateChanged?.Invoke();
        }

        public void DisconnectWorkflow()
        {
            Provider.DisconnectWorkflow();
        }

        // === Capability event handlers ===

        void OnModelsAvailable((string modelId, string name, string description)[] models, string currentModelId)
        {
            MainThread.DispatchAndForget(() => ModelsAvailable?.Invoke(models, currentModelId));
        }

        void OnAvailableCommandsChanged((string name, string description)[] commands)
        {
            MainThread.DispatchAndForget(() =>
            {
                AssistantUISessionState.instance.SetAvailableCommands(Provider?.ProviderId, commands);
                AvailableCommandsChanged?.Invoke(commands);
            });
        }

        public void ReplayCachedAvailableCommands()
        {
            var cached = AssistantUISessionState.instance.GetAvailableCommands(Provider?.ProviderId);
            if (cached == null || cached.Length == 0)
                return;

            MainThread.DispatchAndForget(() => AvailableCommandsChanged?.Invoke(cached));
        }

        // === Capability convenience methods ===

        public void SetModel(string modelId)
        {
            TaskUtils.WithExceptionLogging(() => Provider.SetModelAsync(modelId));
        }

        /// <summary>
        /// Respond to a pending permission request for a tool call.
        /// </summary>
        /// <param name="toolCallId">The tool call ID that has a pending permission request.</param>
        /// <param name="answer">The user's answer to the permission request.</param>
        public void RespondToPermission(string toolCallId, FunctionCalling.PermissionUserAnswer answer)
        {
            TaskUtils.WithExceptionLogging(() => Provider.RespondToPermissionAsync(toolCallId, answer));
        }
    }
}
