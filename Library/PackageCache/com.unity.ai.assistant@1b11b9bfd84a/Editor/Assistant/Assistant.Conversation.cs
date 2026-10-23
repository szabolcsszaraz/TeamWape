using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Unity.AI.Assistant.ApplicationModels;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Socket.ErrorHandling;
using Unity.AI.Assistant.Socket.Workflows.Chat;
using Unity.AI.Assistant.Utils;
using Unity.AI.Toolkit.Accounts.Services.States;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant
    {
        static AssistantContextEntry[] ConvertSelectionContextToInternal(List<SelectedContextMetadataItems> context)
        {
            if (context == null || context.Count == 0)
            {
                return Array.Empty<AssistantContextEntry>();
            }

            var result = new AssistantContextEntry[context.Count];
            for (var i = 0; i < context.Count; i++)
            {
                var entry = context[i];
                if (entry.EntryType == null)
                {
                    // Invalid entry
                    UnityEngine.Debug.LogError("Invalid Selection Context Entry");
                    continue;
                }

                var entryType = (AssistantContextType)entry.EntryType;
                switch (entryType)
                {
                    case AssistantContextType.ConsoleMessage:
                    {
                        result[i] = new AssistantContextEntry
                        {
                            EntryType = AssistantContextType.ConsoleMessage,
                            Value = entry.Value,
                            ValueType = entry.ValueType
                        };

                        break;
                    }

                    default:
                    {
                        result[i] = new()
                        {
                            Value = entry.Value,
                            DisplayValue = entry.DisplayValue,
                            EntryType = entryType,
                            ValueType = entry.ValueType,
                            ValueIndex = entry.ValueIndex ?? 0
                        };

                        break;
                    }
                }
            }

            return result;
        }

        const int k_MaxInternalConversationTitleLength = 30;

        // Context usage tokens are streamed only on ChatResponseV1 fragments and are not part
        // of the conversation history payload, so they would be lost on every domain reload
        // (and editor restart) without a local cache (UUM-140652). PersistentStorage already
        // backs this conversation with a per-id JSON file under Library/AI.Conversations/, so
        // we piggyback on it and restore the values whenever the conversation is reloaded
        // from the backend.
        const string k_ContextUsageStorageKey = "ContextUsage";

        [Serializable]
        internal class ContextUsageState
        {
            public int UsedTokens;
            public int MaxTokens;
        }

        internal static void SaveContextUsage(string conversationId, int usedTokens, int maxTokens)
        {
            if (string.IsNullOrEmpty(conversationId))
                return;

            try
            {
                var storage = new PersistentStorage(conversationId);
                storage.SetState(k_ContextUsageStorageKey, new ContextUsageState
                {
                    UsedTokens = usedTokens,
                    MaxTokens = maxTokens
                });
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[Assistant] Failed to persist context usage for {conversationId}: {ex}");
            }
        }

        static void RestoreContextUsage(AssistantConversation conversation)
        {
            if (conversation == null || !conversation.Id.IsValid)
                return;

            try
            {
                var storage = new PersistentStorage(conversation.Id.Value);
                if (storage.TryGetState<ContextUsageState>(k_ContextUsageStorageKey, out var state)
                    && state != null
                    && state.MaxTokens > 0)
                {
                    conversation.ContextUsageUsedTokens = state.UsedTokens;
                    conversation.ContextUsageMaxTokens = state.MaxTokens;
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[Assistant] Failed to restore context usage for {conversation.Id.Value}: {ex}");
            }
        }

        bool m_ConversationRefreshSuspended;

        // Loads of the SAME conversation can overlap: post-reload recovery self-serves a load on a
        // cache miss (see RecoverIncompleteMessage) while the UI restore's load is still in flight.
        // Publishing was last-to-finish-wins, so an OLDER request could replace the
        // AssistantConversation instance a newer load had already published — including the very
        // instance recovery captured and is streaming replayed fragments into. Everything that
        // reads the cache afterwards (the next prompt in Assistant.Prompt.cs, SetCompleteWithError)
        // would then see the stale object, and its NotifyConversationChange rebuilds the UI model
        // from data that never contained the recovered content. Each load takes a monotonic ticket
        // and refuses to publish once a newer load has already published for the same conversation.
        int m_ConversationLoadTicket;
        readonly Dictionary<AssistantConversationId, int> m_PublishedConversationLoadTicket = new();

        double m_LastConversationRefreshTime = double.MinValue;
        const double k_ConversationRefreshCooldown = 10.0;
        const int k_ConversationRefreshTimeoutMs = 60_000;
        Task m_ConversationRefreshInFlight;
        bool m_ConversationRefreshRerunRequested;

        /// <summary>
        /// Indicates that the conversations have been refreshed
        /// </summary>
        public event Action<IEnumerable<AssistantConversationInfo>> ConversationsRefreshed;

        /// <summary>
        /// The callback when a conversation has been loaded
        /// </summary>
        public event Action<AssistantConversation> ConversationLoaded;

        /// <summary>
        /// The callback when a conversation has changed in any way
        /// TODO: later on we will listen to a change event on the conversation itself, for now this replaces the update queue
        /// </summary>
        public event Action<AssistantConversation> ConversationChanged;

        /// <summary>
        /// Callback when a new conversation has been created
        /// </summary>
        public event Action<AssistantConversation> ConversationCreated;

        /// <summary>
        /// Callback when a conversation has been deleted
        /// </summary>
        public event Action<AssistantConversationId> ConversationDeleted;

        /// <inheritdoc />
        public event Action<AssistantConversationId, ErrorInfo> ConversationErrorOccured;

        /// <inheritdoc />
        public event Action<AssistantConversationId> CapacityReached;

        /// <inheritdoc />
        public event Action<AssistantConversationId, string> IncompleteMessageStarted;

        /// <inheritdoc />
        public event Action<AssistantConversationId> IncompleteMessageCompleted;

        /// <inheritdoc />
        public event Action<AssistantConversationId, string, string> TurnEnded;

        public void SuspendConversationRefresh()
        {
            m_ConversationRefreshSuspended = true;
        }

        public void ResumeConversationRefresh()
        {
            m_ConversationRefreshSuspended = false;
        }

        private void NotifyConversationChange(AssistantConversation conversation)
        {
            ConversationChanged?.Invoke(conversation);
        }

        public async Task RefreshConversationsAsync(CancellationToken ct = default, bool enforceCooldown = false)
        {
            if (m_ConversationRefreshSuspended)
                return;

            // Staleness-tolerant callers stay decoupled from any in-flight refresh:
            // they must never wait on, or rethrow from, a request they did not start.
            if (enforceCooldown && EditorApplication.timeSinceStartup - m_LastConversationRefreshTime < k_ConversationRefreshCooldown)
                return;

            // Callers fire this from repeated events (e.g. every ACP conversation save);
            // join the in-flight refresh instead of stacking a new backend request per event.
            // Mutation-driven callers (no cooldown) must observe state written after the
            // in-flight request started, so they schedule a trailing re-run before joining.
            var inFlight = m_ConversationRefreshInFlight;
            if (inFlight != null && !inFlight.IsCompleted)
            {
                if (!enforceCooldown)
                    m_ConversationRefreshRerunRequested = true;
                await WaitWithCancellation(inFlight, ct);
                return;
            }

            // The shared task runs on its own lifetime; a caller's token governs only
            // that caller's wait. One caller cancelling must not fail the other
            // joiners or kill a pending post-mutation pass.
            var refresh = RunConversationRefreshAsync();
            m_ConversationRefreshInFlight = refresh;
            _ = refresh.ContinueWith(
                _ =>
                {
                    if (ReferenceEquals(m_ConversationRefreshInFlight, refresh))
                        m_ConversationRefreshInFlight = null;
                },
                TaskScheduler.FromCurrentSynchronizationContext());

            await WaitWithCancellation(refresh, ct);
        }

        async Task RunConversationRefreshAsync()
        {
            do
            {
                m_ConversationRefreshRerunRequested = false;
                m_LastConversationRefreshTime = EditorApplication.timeSinceStartup;

                var pass = RefreshConversationsCoreAsync(CancellationToken.None);
                if (await Task.WhenAny(pass, Task.Delay(k_ConversationRefreshTimeoutMs)) != pass)
                {
                    // Give up the in-flight slot so later refreshes make independent
                    // attempts; the abandoned pass keeps running and publishes its
                    // result through ConversationsRefreshed if it ever completes.
                    InternalLog.LogWarning("[Assistant] Conversation refresh timed out; abandoning the attempt.");
                    return;
                }

                await pass;
            }
            while (m_ConversationRefreshRerunRequested && !m_ConversationRefreshSuspended);
        }

        static async Task WaitWithCancellation(Task task, CancellationToken ct)
        {
            if (!ct.CanBeCanceled)
            {
                await task;
                return;
            }

            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => cancelled.TrySetCanceled(ct)))
            {
                await await Task.WhenAny(task, cancelled.Task);
            }
        }

        async Task RefreshConversationsCoreAsync(CancellationToken ct)
        {
            var credentialsContext = await CredentialsProvider.GetCredentialsContext(ct);

            var convosTask = Backend.ConversationRefresh(credentialsContext, ct);
            var profilesTask = Backend.GetAvailableModelProfiles(credentialsContext, ct);
            await Task.WhenAll(convosTask, profilesTask);

            var profilesResult = await profilesTask;
            if (profilesResult.Status == BackendResult.ResultStatus.Success)
                MainThread.DispatchAndForget(() => m_AvailableUnityModelProfiles = profilesResult.Value);

            var infosResult = await convosTask;
            if (infosResult.Status != BackendResult.ResultStatus.Success)
            {
                // A transient transport failure (e.g. right after resuming from sleep) recovers on
                // its own once connectivity returns and is re-driven by ConnectionSupervisor / the
                // scheduled refresh — so don't surface it as a console error. Real errors still log.
                if (BackendFailureClassifier.IsTransientTransportFailure(infosResult))
                    InternalLog.Log($"[Assistant] Conversation refresh skipped (transient transport failure): {infosResult.Exception?.Message}");
                else
                    ErrorHandlingUtility.PublicLogBackendResultError(infosResult);
                return;
            }

            var conversations = infosResult.Value.Select(
                info => new AssistantConversationInfo()
                {
                    Id = new(info.ConversationId),
                    Title = info.Title,
                    LastMessageTimestamp = info.LastMessageTimestamp,
                    IsFavorite = info.IsFavorite != null && info.IsFavorite.Value
                });

            ConversationsRefreshed?.Invoke(conversations);
        }

        public Task ConversationLoad(AssistantConversationId conversationId, CancellationToken ct = default)
            => ConversationLoadInternal(conversationId, raiseErrorEvent: true, ct);

        /// <summary>
        /// Loads a conversation without reporting failures through <see cref="ConversationErrorOccured"/>.
        /// Post-reload recovery loads the conversation itself on a cache miss and retries it; a failed
        /// attempt there is internal bookkeeping, not a user-facing conversation-load failure. The UI's
        /// handler for that event responds with AbortPrompt — cancelling the in-flight chat request and
        /// force-disconnecting the workflow — which would destroy the very turn recovery exists to
        /// rescue, and would append one error bubble per attempt.
        /// Note: the failure is still reported through <see cref="InternalLog"/>, which is
        /// [Conditional("ASSISTANT_INTERNAL")] — so it is visible in internal builds only. A caller
        /// that needs an always-visible signal must emit one itself.
        /// </summary>
        internal Task ConversationLoadWithoutErrorEvent(AssistantConversationId conversationId, CancellationToken ct = default)
            => ConversationLoadInternal(conversationId, raiseErrorEvent: false, ct);

        async Task ConversationLoadInternal(AssistantConversationId conversationId, bool raiseErrorEvent, CancellationToken ct)
        {
            if(!conversationId.IsValid)
                throw new ArgumentException("Invalid conversation id");

            // Taken before the first await so the ticket orders loads by when they were REQUESTED,
            // not by when the backend happened to answer.
            var ticket = Interlocked.Increment(ref m_ConversationLoadTicket);

            // Connection may still be coming up at startup / post-reconnect; loading too early
            // yields a spurious 404. Wait for readiness (no-op when already accessible).
            if (!ApiAccessibleState.IsAccessible)
            {
                InternalLog.Log($"[Assistant] Conversation load waiting for cloud readiness before loading {conversationId.Value}.");
                // This wait takes no CancellationToken and caps itself at 30s, so ct cannot shorten
                // it — callers must size their own budget to cover it plus a real load window (see
                // RecoverIncompleteMessage's loadCts).
                // The result is deliberately NOT used to skip the load: WaitForCloudProjectSettings
                // also returns false in batch mode, where it returns immediately without ever
                // polling, so treating false as "backend unreachable" would skip the backend call on
                // every batchmode load. It is logged instead, so a load that failed behind an
                // unfinished readiness wait is distinguishable from one the backend actually refused.
                if (!await ApiAccessibleState.WaitForCloudProjectSettings())
                    InternalLog.Log($"[Assistant] Cloud readiness did not become accessible for {conversationId.Value}; attempting the load anyway.");
            }

            var result = await Backend.ConversationLoad(await CredentialsProvider.GetCredentialsContext(ct), conversationId.Value, ct);

            if (result.Status != BackendResult.ResultStatus.Success)
            {
                string errorMessage = "Failed to load the conversation.";
                // BackendResult.ToString() dereferences Info, which FailOnCancellation leaves null.
                // The recovery path passes a timeout token, so a cancelled load is a routine outcome
                // here and must not turn this diagnostic into a NullReferenceException.
                var details = result.Status == BackendResult.ResultStatus.FailOnCancellation
                    ? $"BackendResult [Status: {result.Status}]"
                    : result.ToString();
                // Log as well as raising the event: the event is suppressed on the recovery path
                // (see ConversationLoadWithoutErrorEvent), and a silent failure here was untraceable
                // (recovery gave up with no clue why the cache never populated).
                InternalLog.LogWarning($"[Assistant] ConversationLoad({conversationId}) failed: {details}");
                if (raiseErrorEvent)
                    ConversationErrorOccured?.Invoke(conversationId, new ErrorInfo(errorMessage, details));
                return;
            }

            AssistantConversation conversation;
            try
            {
                conversation = ConvertConversation(result.Value);
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[Assistant] Failed to parse conversation {conversationId}: {ex.Message}");
                if (raiseErrorEvent)
                    ConversationErrorOccured?.Invoke(conversationId, new ErrorInfo("Failed to parse conversation history.", ex.Message));
                return;
            }

            RestoreContextUsage(conversation);

            TryPublishLoadedConversation(conversationId, conversation, ticket);
        }

        /// <summary>
        /// Caches <paramref name="conversation"/> and raises <see cref="ConversationLoaded"/>, unless a
        /// load requested LATER than <paramref name="ticket"/> has already published for the same
        /// conversation. Returns true when it published, false when it was discarded as out-of-order.
        /// </summary>
        /// <remarks>
        /// Discarding an out-of-order result matters because overwriting the newer entry would orphan
        /// the object other code is already holding — post-reload recovery streams replayed fragments
        /// into the instance it took out of this cache — and would re-raise ConversationLoaded with
        /// older data, rebuilding the UI model from a snapshot missing everything since. Nothing is
        /// left waiting on a discarded load: the newer one raised ConversationLoaded when it published.
        /// Note the guard is one-directional and per-conversation. It only rejects loads requested
        /// EARLIER than the last publish; a load requested later publishes normally, even over an
        /// instance recovery is streaming into (see the note at RecoverIncompleteMessage's retry loop).
        /// Kept as its own method so that ordering rule is unit-testable without driving a real load.
        /// </remarks>
        internal bool TryPublishLoadedConversation(AssistantConversationId conversationId, AssistantConversation conversation, int ticket)
        {
            if (m_PublishedConversationLoadTicket.TryGetValue(conversationId, out var publishedTicket)
                && publishedTicket > ticket)
            {
                InternalLog.Log($"[Assistant] Discarding out-of-order conversation load for {conversationId} (request {ticket}, cache holds {publishedTicket}).");
                return false;
            }

            m_PublishedConversationLoadTicket[conversationId] = ticket;

            if (!m_ConversationCache.TryAdd(conversationId, conversation))
            {
                m_ConversationCache[conversationId] = conversation;
            }

            ConversationLoaded?.Invoke(conversation);
            return true;
        }

        public void ConversationRefresh(AssistantConversationId conversationId)
        {
            if(!conversationId.IsValid)
                throw new ArgumentException("Invalid conversation id");

            if (m_ConversationCache.TryGetValue(conversationId, out var conversation))
            {
                ConversationLoaded?.Invoke(conversation);
            }
            else
            {
                throw new Exception("Conversation not available.");
            }
        }

        public async Task ConversationFavoriteToggle(AssistantConversationId conversationId, bool isFavorite)
        {
            if(!conversationId.IsValid)
                throw new ArgumentException("Invalid conversation id");

            BackendResult result = await Backend.ConversationFavoriteToggle(await CredentialsProvider.GetCredentialsContext(CancellationToken.None), conversationId.Value, isFavorite);

            if (result.Status != BackendResult.ResultStatus.Success)
            {
                ErrorHandlingUtility.PublicLogBackendResultError(result);
                return;
            }
        }

        public async Task ConversationRename(AssistantConversationId conversationId, [NotNull] string newName, CancellationToken ct = default)
        {
            if (!conversationId.IsValid)
            {
                return;
            }

            BackendResult result = await Backend.ConversationRename(await CredentialsProvider.GetCredentialsContext(ct), conversationId.Value, newName, ct);

            if (result.Status != BackendResult.ResultStatus.Success)
            {
                ErrorHandlingUtility.PublicLogBackendResultError(result);
                return;
            }

            await RefreshConversationsAsync(ct);
        }

        public async Task ConversationDeleteAsync(AssistantConversationId conversationId, CancellationToken ct = default)
        {
            if (!conversationId.IsValid)
            {
                return;
            }

            BackendResult result = await Backend.ConversationDelete(await CredentialsProvider.GetCredentialsContext(ct), conversationId.Value, ct);

            if (result.Status != BackendResult.ResultStatus.Success)
            {
                ErrorHandlingUtility.PublicLogBackendResultError(result);
                return;
            }

            PersistentStorage.Delete(conversationId.Value);

            ConversationDeleted?.Invoke(conversationId);
        }

        internal static AssistantConversation ConvertConversation(ClientConversation remoteConversation)
        {
            var conversationId = new AssistantConversationId(remoteConversation.Id);
            AssistantConversation localConversation = new()
            {
                Id = conversationId,
                Title = string.IsNullOrEmpty(remoteConversation.Title)
                    ? AssistantConstants.DefaultConversationTitle
                    : remoteConversation.Title
            };

            for (var i = 0; i < remoteConversation.History.Count; i++)
            {
                var fragment = remoteConversation.History[i];
                var message = new AssistantMessage
                {
                    Id = new(conversationId, fragment.Id, AssistantMessageIdType.External),
                    IsComplete = true,
                    Role = fragment.Role,
                    RevertedTimeStamp = fragment.RevertedTimeStamp,
                    Timestamp = fragment.Timestamp,
                    Context = ConvertSelectionContextToInternal(fragment.SelectedContextMetadata)
                };

                switch (fragment.Role.ToLower())
                {
                    case k_UserRole:
                        message.Blocks.Add(new PromptBlock{Content = fragment.Content});
                        break;

                    case k_AssistantRole:
                    {
                        var chatResponseFragment = new ChatResponseFragment
                        {
                            Id = fragment.Id,
                            CorrelationMessageId = fragment.CorrelationMessageId,
                            Fragment = fragment.Content,
                            IsLastFragment = true
                        };
                        var responseBuilder = new StringBuilder();
                        chatResponseFragment.Parse(conversationId, message, responseBuilder);
                        break;
                    }

                    default:
                        throw new NotImplementedException($"Role is not supported: {fragment.Role}");
                }

                localConversation.Messages.Add(message);
            }

            return localConversation;
        }
    }
}
