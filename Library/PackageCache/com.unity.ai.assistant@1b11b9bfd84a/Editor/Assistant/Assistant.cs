using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.ApplicationModels;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Acp;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Backend.Socket;
using Unity.AI.Assistant.Editor.Config;
using Unity.AI.Assistant.Editor.Config.Credentials;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Socket.ErrorHandling;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEngine.Pool;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant : IAssistantProvider
    {
        public const string k_UserRole = "user";
        public const string k_AssistantRole = "assistant";
        public const string k_SystemRole = "system";

        static float s_LastRefreshTokenTime;

        public IAssistantBackend Backend { get; private set; }
        public IFunctionCaller FunctionCaller { get; private set; }

        public ICredentialsProvider CredentialsProvider { get; private set; }

        public IToolPermissions ToolPermissions => ToolInteractionAndPermissionBridge.ToolPermissions;
        public IToolInteractions ToolInteractions => ToolInteractionAndPermissionBridge.ToolInteractions;

        string m_ProviderId = AssistantProviderFactory.DefaultProvider.ProfileId;
        IReadOnlyList<ModelProfile> m_AvailableUnityModelProfiles;

        /// <summary>
        /// Current Unity profile id (e.g. unity-max, unity-fast).
        /// </summary>
        public string ProviderId => m_ProviderId;

        /// <summary>
        /// Cached Unity model profiles from GET /v1/assistant/models. Updated when the backend raises AvailableModelProfilesUpdated. Exposed for UI context.
        /// </summary>
        internal IReadOnlyList<ModelProfile> AvailableUnityModelProfiles => m_AvailableUnityModelProfiles;

        /// <summary>
        /// Sets the provider id when the user switches between Unity Max and Fast. Called by the UI context.
        /// </summary>
        internal void SetCurrentProviderId(string providerId)
        {
            if (AssistantProviderFactory.IsUnityProvider(providerId))
                m_ProviderId = providerId;
        }

        public ToolInteractionAndPermissionBridge ToolInteractionAndPermissionBridge { get; private set; }

        public Assistant(AssistantConfiguration configuration = null)
        {
            Reconfigure(configuration);
        }

        internal void Reconfigure(AssistantConfiguration configuration = null)
        {
            Backend = configuration?.Backend ?? new AssistantRelayBackend();

            ToolInteractionAndPermissionBridge = configuration?.Bridge ?? new ToolInteractionAndPermissionBridge(
                new AllowAllToolPermissions(),
                new AllowAllToolInteractions());

            // TODO: Why is IFunctionCaller an interface but not configurable
            FunctionCaller = new AIAssistantFunctionCaller(ToolInteractionAndPermissionBridge, ToolInteractionAndPermissionBridge);

            // The relay executes a redelivered function call itself when no chat workflow is
            // attached to consume it (muse-editor#2705). Publish the caller the workflow would have
            // used so that path runs the tool through the same permission gate, rather than a
            // permissive caller of its own.
            //
            // Only publish when a real Bridge was supplied. A bridgeless Reconfigure — e.g. the
            // window's first construct-before-the-bridge-exists pass, or any headless
            // construction — falls back to AllowAllToolPermissions above; publishing that to the
            // process-wide static would leave a permissive caller in place last-writer-wins, which
            // is exactly what the fallback promises never to do. The window re-runs Reconfigure
            // with its EditorToolPermissions bridge moments later, and that publishes.
            if (configuration?.Bridge != null)
                Relay.Editor.RelayFunctionCallFallback.FunctionCaller = FunctionCaller;

			CredentialsProvider = configuration?.CredentialsProvider ?? new EditorCredentialsProvider();
        }

        public event Action<AssistantMessageId, FeedbackData?> FeedbackLoaded;
        public event Action<AssistantMessageId, bool> FeedbackSent;

        public bool SessionStatusTrackingEnabled => Backend == null || Backend.SessionStatusTrackingEnabled;
        public bool AutoRunSettingAvailable => true;

        AssistantMessage AddInternalMessage(AssistantConversation conversation, string text, string role = null, bool sendUpdate = true, int indexOverride = -1)
        {
            var message = new AssistantMessage
            {
                Id = AssistantMessageId.GetNextInternalId(conversation.Id),
                IsComplete = true,
                Role = role,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            message.AddMessageForRole(role, text, message.IsComplete);

            if (indexOverride > conversation.Messages.Count)
            {
                InternalLog.LogError($"Index override {indexOverride} is out of bounds for conversation with {conversation.Messages.Count} messages.");
                TracesUploader.UploadTraces(conversation.Id.Value, "index-override");

                indexOverride = -1; // Fallback to adding at the end
            }

            if (indexOverride < 0)
            {
                conversation.Messages.Add(message);
            }
            else
            {
                conversation.Messages.Insert(indexOverride, message);
            }

            if (sendUpdate)
            {
                NotifyConversationChange(conversation);
            }

            return message;
        }

        AssistantMessage AddIncompleteMessage(AssistantConversation conversation, string text, string role = null, bool sendUpdate = true)
        {
            var message = new AssistantMessage
            {
                Id = AssistantMessageId.GetNextIncompleteId(conversation.Id),
                IsComplete = false,
                Role = role,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            if (!string.IsNullOrEmpty(text))
                message.AddMessageForRole(role, text, message.IsComplete);

            conversation.Messages.Add(message);
            if (sendUpdate)
            {
                NotifyConversationChange(conversation);
            }

            return message;
        }

        public async Task SendFeedback(AssistantMessageId messageId, bool flagMessage, string feedbackText, bool upVote)
        {
            var feedback = new MessageFeedback
            {
                MessageId = messageId,
                FlagInappropriate = flagMessage,
                Type = Category.ResponseQuality,
                Message = feedbackText,
                Sentiment = upVote ? Sentiment.Positive : Sentiment.Negative
            };

            try
            {
                // Failing to send feedback is non-critical. Surface completion through FeedbackSent event.
                var result = await Backend.SendFeedback(await CredentialsProvider.GetCredentialsContext(), messageId.ConversationId.Value, feedback);
                if (result.Status != BackendResult.ResultStatus.Success)
                {
                    ErrorHandlingUtility.InternalLogBackendResult(result);
                    FeedbackSent?.Invoke(messageId, false);
                    return;
                }

                FeedbackSent?.Invoke(messageId, true);
            }
            catch (Exception ex)
            {
                InternalLog.LogException(ex);
                FeedbackSent?.Invoke(messageId, false);
            }
        }

        public async Task<FeedbackData?> LoadFeedback(AssistantMessageId messageId, CancellationToken ct = default)
        {
            if (!messageId.ConversationId.IsValid || messageId.Type != AssistantMessageIdType.External)
            {
                // Whatever we are asking for is not valid to be asked for
                return null;
            }

            var result =  await Backend.LoadFeedback(await CredentialsProvider.GetCredentialsContext(ct), messageId, ct);

            if (result.Status != BackendResult.ResultStatus.Success)
            {
#if ASSISTANT_INTERNAL_VERBOSE
                // if feedback fails to load, silently fail
                ErrorHandlingUtility.InternalLogBackendResult(result);
#endif
                return null;
            }

            FeedbackLoaded?.Invoke(messageId, result.Value);

            return result.Value;
        }
  
        /// <summary>
        /// Recover incomplete message from relay server cache after domain reload
        /// </summary>
        public async Task RecoverIncompleteMessage(AssistantConversationId conversationId)
        {
            // The message THIS recovery adopted (assigned right after adoption below).
            // Give-up paths hand it to SetCompleteWithError so the finalize is gated
            // on IDENTITY — recovery is a detached task, and by give-up time a NEW
            // turn's placeholder can be the conversation's last message (C1).
            AssistantMessage recoveredMessage = null;
            try
            {
                InternalLog.Log($"Attempting to recover incomplete message for conversation: {conversationId}");

                AIAssistantAnalytics.ReportGatewayStreamRecoveryEvent("requested", null);

                // Wait for relay connection to be ready
                try
                {
                    await Relay.Editor.RelayService.Instance.GetClientAsync(TimeSpan.FromSeconds(5));
                }
                catch (Relay.Editor.RelayConnectionException)
                {
                    var error = "Relay connection not available, cannot recover message";
                    InternalLog.LogWarning(error);
                    SetCompleteWithError(conversationId, error, recoveredMessage);
                    return;
                }

                InternalLog.Log("Relay connected, initiating incomplete message recovery");

                // Get conversation from cache (usually loaded by the time recovery runs)
                if (!m_ConversationCache.TryGetValue(conversationId, out var conversation))
                {
                    // Post-domain-reload race: RestoreUIState can trigger recovery before the
                    // async conversation reload has repopulated the cache. Bailing out here
                    // permanently abandons the relay's cached pending FUNCTION_CALL_REQUESTs
                    // (they are only replayed via RELAY_RECOVER_MESSAGES), so the in-flight
                    // turn dies on the backend's ~600s client-response timeout. Self-serve the
                    // load instead of depending on UI load ordering (ConversationLoad populates
                    // m_ConversationCache on success).
                    InternalLog.Log("Conversation not in cache yet - loading it before recovery");
                    // Retry: the load runs seconds after a domain reload, when the backend
                    // driver/credentials can be transiently unavailable — a one-shot load
                    // was observed failing silently right in that window, abandoning
                    // recovery while the relay still held replayable pending calls.
                    // This deliberately does not wait on the restore's own in-flight load: that
                    // load may never complete, and recovery is racing the backend's ~600s
                    // client-response timeout. What ConversationLoadInternal's ordering guard buys
                    // is narrower than "overlapping loads are safe": it publishes by request order,
                    // so a load requested BEFORE this one cannot answer late and replace the
                    // instance captured below. A load requested AFTER it still publishes normally
                    // and replaces the cache entry while recovery keeps streaming into the captured
                    // instance — ChatElementCheckpoint's post-RevertMessage load and AssistantApi's
                    // resumeConversationId load can both do that inside this window. That residual
                    // race predates this retry loop and is partly intentional (a checkpoint restore
                    // is meant to supersede the message being recovered), so it is left alone here.
                    const int maxLoadAttempts = 3;
                    for (var attempt = 1; attempt <= maxLoadAttempts; attempt++)
                    {
                        try
                        {
                            // This budget has to cover BOTH phases of ConversationLoadInternal, not
                            // just the backend call: it first awaits
                            // ApiAccessibleState.WaitForCloudProjectSettings(), which takes no
                            // CancellationToken and caps itself at 30s. At a 30s budget the
                            // readiness wait alone could spend the whole thing, expiring the token
                            // before GetCredentialsContext/Backend.ConversationLoad were even
                            // reached — three attempts and not one backend call, against a backend
                            // that may well be reachable. 60s leaves a full load window behind a
                            // worst-case readiness wait. Worst case overall is 3*60s + 2*3s = 186s,
                            // still comfortably inside the ~600s client-response timeout recovery
                            // is racing.
                            using var loadCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                            // Deliberately the non-event-raising overload: a failed attempt here is
                            // internal retry bookkeeping. The UI's ConversationErrorOccured handler
                            // responds with AbortPrompt (cancel chat request + force-disconnect the
                            // workflow), which would tear down the in-flight turn this recovery is
                            // trying to rescue, and would surface one error bubble per attempt.
                            await ConversationLoadWithoutErrorEvent(conversationId, loadCts.Token);
                        }
                        catch (Exception ex)
                        {
                            InternalLog.LogWarning($"Conversation load during recovery failed (attempt {attempt}/{maxLoadAttempts}): {ex.Message}");
                        }

                        if (m_ConversationCache.ContainsKey(conversationId))
                            break;

                        if (attempt < maxLoadAttempts)
                            await Task.Delay(TimeSpan.FromSeconds(3));
                    }

                    if (!m_ConversationCache.TryGetValue(conversationId, out conversation))
                    {
                        InternalLog.LogWarning("Conversation not in cache yet, cannot recover incomplete message");
                        // Cache miss — no conversation to add an error to, but the give-up
                        // must still end the working state RestoreUIState armed directly
                        // (review: releasing the marker alone leaves the "Preparing…"
                        // wedge). SetCompleteWithError also reports the failed-recovery
                        // analytic, so no local report here.
                        SetCompleteWithError(conversationId);
                        return;
                    }
                }

                // Check conversation state for recovery
                var lastConversationMessage = conversation.Messages.LastOrDefault();
                if (lastConversationMessage == null)
                {
                    // Nothing to recover — no error bubble wanted, but the give-up must
                    // still end the working state, not just release the marker (review).
                    SetCompleteWithError(conversationId);
                    return;
                }

                AssistantMessage message;
                if (lastConversationMessage.Role.ToLower() == k_AssistantRole)
                {
                    // Always attempt recovery — IncompleteMessageId guarantees recovery is needed.
                    // Clear blocks (replay rebuilds from raw fragments) and reset IsComplete
                    // (ConvertConversation hardcodes it to true) so the completion flow works.
                    message = lastConversationMessage;
                    message.Blocks.Clear();
                    message.IsComplete = false;
                    InternalLog.Log("Recovering existing assistant message (clearing blocks for replay)");
                }
                else if (lastConversationMessage.Role.ToLower() == k_UserRole)
                {
                    // User message without answer — add a new incomplete message
                    message = AddIncompleteMessage(conversation, string.Empty, k_AssistantRole, sendUpdate: true);
                }
                else
                {
                    // Unexpected role — bail safely (no error bubble wanted, but the
                    // give-up must still end the working state, not just the marker).
                    InternalLog.Log($"Last message has unexpected role '{lastConversationMessage.Role}', skipping recovery");
                    SetCompleteWithError(conversationId);
                    return;
                }

                recoveredMessage = message;

                // Create workflow in recovery mode (skip initialization, just set up message handlers)
                var credentialsContext = await CredentialsProvider.GetCredentialsContext(CancellationToken.None);
                var workflow = Backend.GetOrCreateWorkflow(
                    credentialsContext,
                    FunctionCaller,
                    conversationId,
                    skipInitialization: true);

                if (workflow == null)
                {
                    var error = "Failed to create workflow for recovery";
                    InternalLog.LogError(error);
                    SetCompleteWithError(conversationId, error, recoveredMessage);
                    return;
                }

                // Backend.GetOrCreateWorkflow starts the workflow fire-and-forget. Await Started here so the transport is subscribed before replay
                // begins — otherwise replayed messages (notably FUNCTION_CALL_REQUEST_V1) can race past ProcessReceiveResult wiring and be dropped.
                // The 5s ceiling caps recovery time if Start never completes.
                const int startupTimeoutMs = 5000;
                using var delayCts = new CancellationTokenSource();
                var delayTask = Task.Delay(startupTimeoutMs, delayCts.Token);
                var completed = await Task.WhenAny(workflow.Started, delayTask);
                delayCts.Cancel();

                if (completed != workflow.Started)
                {
                    InternalLog.LogWarning($"Workflow startup did not complete within {startupTimeoutMs}ms — proceeding with replay anyway");
                }
                else if (workflow.Started.IsFaulted)
                {
                    var startupError = $"Workflow startup failed: {workflow.Started.Exception?.GetBaseException().Message}";
                    InternalLog.LogError(startupError);
                    SetCompleteWithError(conversationId, startupError, recoveredMessage);
                    return;
                }
                else if (workflow.Started.IsCanceled)
                {
                    const string startupCancelled = "Workflow was disposed before startup completed";
                    InternalLog.LogWarning(startupCancelled);
                    SetCompleteWithError(conversationId, startupCancelled, recoveredMessage);
                    return;
                }

                // Start listening to workflow events (handles both replayed and new streaming messages)
                ResumeIncompleteMessage(workflow, conversation, message, CancellationToken.None);

                // Request replay - messages will flow through the workflow to ResumeIncompleteMessage handlers.
                //
                // Waiting for the relay to be up is the GetClientAsync(5s) gate above (line ~216), NOT this
                // loop: the gate is what recovery spends its connection-wait budget on, and both its exits
                // (RelayConnectionException / the 5s TaskCanceledException) end recovery before we get here.
                // The case-094 shape (2026-08-19) — an unresponsive relay after a domain reload — is now
                // fixed by the connect-side fail-fast in this PR: TryConnectAsync no longer parks the service
                // in Running on a dead transport and it frees the half-open socket immediately, so the relay's
                // single-client slot is reclaimed and the connection comes back through the connect loop. Post-
                // fix, if the relay is genuinely still down at 5s, recovery fails fast at that gate rather than
                // wedging; it does not reach this replay.
                //
                // This short retry only covers the narrow window where the gate passed (client connected) but
                // the connection flaps between the gate and the replay call, so ReplayIncompleteMessageAsync
                // momentarily reports "not connected". A couple of quick retries let a same-client blip settle;
                // a real drop instead tears the recovery workflow down (see the WorkflowState check below) and
                // we stop. The budget is deliberately small: a genuine reconnect is now handled by
                // RelayWebSocketAdapter following RelayService's client swap (#2705) — it re-subscribes to the
                // new client and re-requests the cached replay — so this retry only needs to cover a brief
                // same-client flap, not a full reconnect.
                const int replayAttempts = 3;
                const int replayRetryDelayMs = 2000;
                bool replayStarted = false;
                for (var replayAttempt = 1; replayAttempt <= replayAttempts; replayAttempt++)
                {
                    // Retry only while the workflow subscribed above can still receive the
                    // replay. A relay disconnect that lands after that subscription tears this
                    // workflow down (RelayChatWorkflow.HandleWebsocketClosed -> Dispose ->
                    // State.Closed), disposing the adapter with it. A later attempt would then
                    // "succeed" against the relay with this workflow already gone: the replayed
                    // fragments reach nobody while recovery is reported as initiated. Stop and report
                    // the failure. (A swap that does NOT tear the workflow down is instead followed by
                    // RelayWebSocketAdapter, which re-subscribes to the new client and re-requests the
                    // replay — so recovery still lands.)
                    if (workflow.WorkflowState == Socket.Workflows.Chat.State.Closed)
                    {
                        InternalLog.LogWarning(
                            $"Recovery workflow closed before replay started (attempt {replayAttempt}/{replayAttempts}) - abandoning replay");
                        break;
                    }

                    replayStarted = await Relay.Editor.RelayService.Instance.ReplayIncompleteMessageAsync();
                    if (replayStarted)
                        break;
                    InternalLog.LogWarning($"Replay request failed (attempt {replayAttempt}/{replayAttempts}) - relay not connected yet, retrying");
                    if (replayAttempt < replayAttempts)
                        await Task.Delay(replayRetryDelayMs);
                }
                InternalLog.LogToFile(conversationId.Value, ("event", "recovery_replay_requested"), ("workflowState", workflow.WorkflowState.ToString()), ("started", replayStarted.ToString()));

                if (!replayStarted)
                {
                    InternalLog.LogWarning("Failed to initiate replay");
                    // Give-up parity with every other recovery failure path (UUM-151091):
                    // this was the ONE branch that neither completed the message nor
                    // released the incomplete-message marker, so the UI kept waiting on a
                    // replay that would never come — the same "Preparing…" wedge as the
                    // stale-marker case, from a second cause. SetCompleteWithError also
                    // reports the "failed" recovery event, so no separate report here.
                    SetCompleteWithError(
                        conversationId,
                        "The Assistant could not resume this conversation after the reload. Send a new message to continue.",
                        recoveredMessage);
                }
                else
                {
                    AIAssistantAnalytics.ReportGatewayStreamRecoveryEvent("initiated", null);
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to recover incomplete message: {ex.Message}");
                SetCompleteWithError(conversationId, ex.Message, recoveredMessage);
            }
        }

        void SetCompleteWithError(AssistantConversationId conversationId, string errorMessage = null, AssistantMessage recoveredMessage = null)
        {
            AIAssistantAnalytics.ReportGatewayStreamRecoveryEvent("failed", null);

            // Ownership check (review): recovery is a detached task, so this give-up
            // can land after the user stopped the false "Preparing…" and started a
            // NEW turn on the same conversation (a new prompt LocalDisconnects the
            // recovery workflow — the give-up is then guaranteed to follow). The
            // UI's HandlePromptStateSync gates only on the ACTIVE CONVERSATION id,
            // which is turn-blind: an unconditional raise below would clear the
            // live turn's working state and reopen the prompt box mid-stream, and
            // the completion event would erase the live turn's incomplete marker.
            // A newer turn shows up as a trailing message that is NOT the one this
            // recovery adopted: its streaming placeholder (incomplete) or its
            // just-submitted user prompt. Computed BEFORE the finalize/error edits
            // below so those cannot perturb it.
            var newerTurnActive = false;
            if (m_ConversationCache.TryGetValue(conversationId, out var conversation))
            {
                var lastMessage = conversation.Messages.LastOrDefault();
                newerTurnActive = lastMessage != null
                    && !ReferenceEquals(lastMessage, recoveredMessage)
                    && (!lastMessage.IsComplete || lastMessage.Role?.ToLower() == k_UserRole);

                // Finalize the message THIS recovery adopted so the UI stops
                // rendering a streaming bubble (UUM-151091 review P1): recovery
                // resets it to IsComplete=false (and clears its blocks) BEFORE the
                // failure that lands here. Gated on the adopted instance, never on
                // the conversation's LAST message: recovery is a detached task,
                // and by give-up time a NEW turn's placeholder can be last — a
                // positional finalize could complete or even delete a live turn's
                // message (self-review C1). Remove is reference-based, so it
                // no-ops if a newer flow already dropped the adopted message.
                if (recoveredMessage != null && !recoveredMessage.IsComplete)
                {
                    if (recoveredMessage.Blocks.Count == 0)
                        conversation.Messages.Remove(recoveredMessage);
                    else
                        recoveredMessage.IsComplete = true;
                }

                // No error bubble when a newer turn owns the conversation: "send a
                // new message to continue" while one already streams is noise.
                if (errorMessage != null && !newerTurnActive)
                {
                    conversation.Messages.Add(AssistantMessage.AsError(AssistantMessageId.GetNextInternalId(conversationId), errorMessage));
                    TracesUploader.UploadTraces(conversationId.Value, "complete-with-error");
                }

                NotifyConversationChange(conversation);
            }

            // A newer live turn now owns the working state AND the incomplete
            // marker — this stale give-up must touch neither (the finalize above
            // already cleaned up the abandoned placeholder, identity-gated).
            if (newerTurnActive)
                return;

            // End the UI's working state (UUM-151091 review P1): the post-reload
            // restore arms it DIRECTLY (SetWorkingState(true)), not through a
            // prompt-state transition, so releasing the marker below is not
            // enough — the panel would stay on "Preparing…" and refuse new
            // prompts for the rest of the session. Raise the UI sync event ONLY
            // — deliberately NOT ChangePromptState (self-review C2): that stamps
            // the GLOBAL CurrentPromptState field, and a detached give-up landing
            // while a newer turn streams would turn AbortPrompt/Stop into a
            // silent no-op for that turn. The event half is staleness-safe (the
            // UI's HandlePromptStateSync ignores non-active conversations), and
            // its NotConnected arm clears the working state and reopens the
            // prompt box.
            PromptStateChanged?.Invoke(conversationId, PromptState.NotConnected);

            IncompleteMessageCompleted?.Invoke(conversationId);
        }

        public async Task RefreshProjectOverview(CancellationToken cancellationToken = default)
        {
            await ProjectOverview.RefreshProjectOverview(cancellationToken);
        }

        // === Mode support ===
        // Unity provider supports Agent/Ask/Plan modes

        static readonly (string id, string name, string desc)[] s_UnityModes =
        {
            ("Agent", "Agent", "Can perform actions"),
            ("Plan", "Plan", "Plan with Unity"),
            ("Ask", "Ask", "Read-only tools only")
        };

        string m_CurrentModeId = "Agent";

        // ReSharper disable once EventNeverSubscribedTo.Local - Backing field needed for unsubscribe; modes list is static so event is never raised
#pragma warning disable CS0067
        event Action<(string id, string name, string desc)[], string> m_ModesAvailable;
#pragma warning restore CS0067
        public event Action<(string id, string name, string desc)[], string> ModesAvailable
        {
            add
            {
                m_ModesAvailable += value;
                // Immediately notify new subscriber of available modes
                value?.Invoke(s_UnityModes, m_CurrentModeId);
            }
            remove => m_ModesAvailable -= value;
        }

        event Action<string> m_ModeChanged;
        public event Action<string> ModeChanged
        {
            add => m_ModeChanged += value;
            remove => m_ModeChanged -= value;
        }

        public Task SetModeAsync(string modeId)
        {
            if (modeId != m_CurrentModeId)
            {
                m_CurrentModeId = modeId;
                m_ModeChanged?.Invoke(modeId);
            }
            return Task.CompletedTask;
        }

        public Task SetModelAsync(string modelId) => Task.CompletedTask;

        // Unity provider handles permissions via IToolPermissions, not via this method
        public Task RespondToPermissionAsync(string toolCallId, PermissionUserAnswer answer) => Task.CompletedTask;

        public Task EndSessionAsync(AssistantConversationId conversationId) => Task.CompletedTask;

        // Events that Unity provider never fires (empty add/remove to satisfy interface)
        public event Action<(string modelId, string name, string description)[], string> ModelsAvailable { add { } remove { } }
        public event Action<(string name, string description)[]> AvailableCommandsChanged { add { } remove { } }
    }
}
