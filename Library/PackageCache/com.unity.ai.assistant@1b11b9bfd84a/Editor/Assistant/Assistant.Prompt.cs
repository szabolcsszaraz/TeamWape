using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Agents;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Backend.Socket;
using Unity.AI.Assistant.Editor.Context;
using Unity.AI.Assistant.Editor.RunCommand;
using Unity.AI.Assistant.Editor.Utils;
using UnityEditor;
using UnityEngine;
using Unity.AI.Assistant.Socket.ErrorHandling;
using Unity.AI.Assistant.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Socket.Workflows.Chat;
using Unity.AI.Assistant.Utils;
using Unity.AI.Assistant.Utils.Perf;
using Unity.AI.Assistant.Editor.Checkpoint;
using OrchestrationDataUtilities = Unity.AI.Assistant.Socket.Utilities.OrchestrationDataUtilities;
using TaskUtils = Unity.AI.Assistant.Editor.Utils.TaskUtils;

namespace Unity.AI.Assistant.Editor
{
    delegate void ChangePromptStateDelegate(AssistantConversationId conversationId, Assistant.PromptState newState, string message, bool force = false);
    
    /// <summary>
    /// Encapsulates workflow event handling logic for Assistant conversations.
    /// Handles chat responses, function calls, and workflow state changes.
    /// </summary>
    class WorkflowEventHandler
    {
        readonly IChatWorkflow m_Workflow;
        readonly AssistantConversation m_Conversation;
        readonly AssistantMessage m_AssistantMessage;
        readonly StringBuilder m_ResponseBuilder;
        readonly CancellationToken m_CancellationToken;
        readonly bool m_IsNewConversation;
        readonly ChangePromptStateDelegate m_ChangePromptState;
        readonly Action<AssistantConversationId, ErrorInfo> m_ConversationErrorOccured;
        readonly Action<AssistantConversationId> m_CapacityReached;
        readonly Action<AssistantConversation> m_NotifyConversationChange;
        readonly Action<AssistantConversationId> m_IncompleteMessageCompleted;
        readonly Action<AssistantConversationId, Assistant.AutoContinueReason, CancellationToken> m_ConnectionLostMidTurn;
        readonly Action<AssistantConversationId, string, string> m_ReportTurnEnded;

        long m_PromptSentAt;
        bool m_FirstChunkSeen;
        int m_TurnEndReported;
        int m_AutoContinueDispatched;
        int m_CloseHandled;
        int m_IncompleteMessageReleased;

        /// <summary>
        /// Stamp the time at which the user prompt was sent to the backend. Used to compute
        /// client-side Time To First Chunk (TTFT) when the first response fragment arrives.
        /// Must be called synchronously before awaiting <c>SendChatRequest</c>. Not called on
        /// the resume path, in which case TTFT reporting is skipped (sentinel-out at 0).
        /// </summary>
        public void SetPromptSentAt(long unixMs)
        {
            m_PromptSentAt = unixMs;
        }

        void ReportTurnEnded(string outcome, string failureReason)
        {
            if (Interlocked.Exchange(ref m_TurnEndReported, 1) == 1)
                return;

            // EditorAnalytics must run on the main thread; HandleChatResponse/HandleClose do not.
            var conversationId = m_Conversation.Id.IsValid ? m_Conversation.Id.Value : null;
            MainThread.DispatchAndForget(() =>
            {
                AIAssistantAnalytics.ReportGatewayTurnEndedEvent(conversationId, outcome, failureReason);

                // Surface the same authoritative outcome to in-process consumers (e.g. the
                // automation harness), which otherwise have to reverse-engineer "did the turn
                // finish?" from message state — unreliable, because recovery mutates it
                // (RemoveConnectionLossErrors) and a mid-turn close marks its partial complete.
                // Main-thread for the same reason as the analytics call: consumers touch editor state.
                if (conversationId != null)
                    m_ReportTurnEnded?.Invoke(new AssistantConversationId(conversationId), outcome, failureReason);
            });
        }

        // Maps a close reason to the coarse failureReason sub-code carried on the gateway
        // TurnEnded event/analytic, so pre-handler (ProcessPromptInternal early-out) and
        // post-handler (HandleClose) failures share one vocabulary. internal static so the
        // Assistant early-outs can reuse it.
        internal static string MapCloseReasonToFailureSubCode(CloseReason reason)
        {
            if (reason.Reason == CloseReason.ReasonType.AuthenticationFailed)
                return "auth";
            if (reason.Reason == CloseReason.ReasonType.CouldNotConnect
                || reason.Reason == CloseReason.ReasonType.UnderlyingWebSocketWasClosed
                || reason.Reason == CloseReason.ReasonType.ChatResponseTimeout
                || reason.Reason == CloseReason.ReasonType.DiscussionInitializationTimeout)
                return "connection_lost";
            if (reason.Reason == CloseReason.ReasonType.ServerDisconnected
                || reason.Reason == CloseReason.ReasonType.ServerSentUnknownMessage
                || reason.Reason == CloseReason.ReasonType.ServerSentMessageAtWrongTime)
                return "server_disconnect";
            return "unknown";
        }

        /// <summary>
        /// Release the incomplete-message marker exactly once for this turn (UUM-151091).
        /// The completion event's UI handler clears the SessionState marker that
        /// domain-reload recovery keys on; EVERY release site in this class — the
        /// last-fragment path, the parse-error path, and the close paths — funnels
        /// through here, and the latch (same pattern as
        /// <see cref="ReportTurnEnded"/>) makes races between them harmless: a
        /// close racing the completion tail must not release twice, because the
        /// second release can erase a NEWER turn's marker (the key is global).
        /// </summary>
        void ReleaseIncompleteMessage()
        {
            if (Interlocked.Exchange(ref m_IncompleteMessageReleased, 1) == 1)
                return;

            m_IncompleteMessageCompleted?.Invoke(new AssistantConversationId(m_Conversation.Id.Value));
        }

        public WorkflowEventHandler(
            IChatWorkflow workflow,
            AssistantConversation conversation,
            AssistantMessage assistantMessage,
            StringBuilder responseBuilder,
            CancellationToken cancellationToken,
            bool isNewConversation,
            ChangePromptStateDelegate changePromptState,
            Action<AssistantConversationId, ErrorInfo> conversationErrorOccured,
            Action<AssistantConversationId> capacityReached,
            Action<AssistantConversation> notifyConversationChange,
            Action<AssistantConversationId> incompleteMessageCompleted = null,
            Action<AssistantConversationId, Assistant.AutoContinueReason, CancellationToken> connectionLostMidTurn = null,
            Action<AssistantConversationId, string, string> reportTurnEnded = null)
        {
            m_Workflow = workflow;
            m_Conversation = conversation;
            m_AssistantMessage = assistantMessage;
            m_ResponseBuilder = responseBuilder;
            m_CancellationToken = cancellationToken;
            m_IsNewConversation = isNewConversation;
            m_ChangePromptState = changePromptState;
            m_ConversationErrorOccured = conversationErrorOccured;
            m_CapacityReached = capacityReached;
            m_NotifyConversationChange = notifyConversationChange;
            m_IncompleteMessageCompleted = incompleteMessageCompleted;
            m_ConnectionLostMidTurn = connectionLostMidTurn;
            m_ReportTurnEnded = reportTurnEnded;
        }

        public void Subscribe()
        {
            m_Workflow.OnChatResponse -= HandleChatResponse;
            m_Workflow.OnChatResponse += HandleChatResponse;

            m_Workflow.OnClose -= HandleClose;
            m_Workflow.OnClose += HandleClose;
            m_Workflow.OnWorkflowStateChanged -= OnWorkflowStateChange;
            m_Workflow.OnWorkflowStateChanged += OnWorkflowStateChange;
        }

        public void Unsubscribe()
        {
            m_Workflow.OnClose -= HandleClose;
            m_Workflow.OnChatResponse -= HandleChatResponse;
            m_Workflow.OnWorkflowStateChanged -= OnWorkflowStateChange;
        }

        void HandleClose(CloseReason reason)
        {
            // Terminal closes can race (e.g. the chat-response timeout task vs the receive
            // pump); handle only the first.
            if (Interlocked.Exchange(ref m_CloseHandled, 1) == 1)
                return;

            if (reason.Reason == CloseReason.ReasonType.ClientCanceled
                || m_CancellationToken.IsCancellationRequested)
            {
                ReportTurnEnded("cancelled", null);
                return;
            }

            if (reason.Reason == CloseReason.ReasonType.ServerDisconnectedGracefully)
            {
                // A graceful close can only reach this handler mid-turn: on a completed turn the
                // last fragment unsubscribes before the close arrives. The backend also closes
                // with the "graceful" WebSocket code for idle/workflow timeouts (UUM-147368), so
                // surface the interruption instead of letting the turn end looking complete.
                Unsubscribe();

                // The conversation is actively rendered while streaming; mutate it on the main
                // thread so UI enumeration can't observe a mid-change collection.
                MainThread.DispatchIfNeeded(() =>
                {
                    var interruptedMessageId = new AssistantMessageId(
                        m_Conversation.Id,
                        Guid.NewGuid().ToString(),
                        AssistantMessageIdType.Internal);

                    m_Conversation.Messages.Add(AssistantMessage.AsInformational(
                        interruptedMessageId,
                        "The session ended before the response was finished. Send a new message to continue."));

                    // A close before the first fragment leaves an empty placeholder; remove it
                    // instead of rendering a blank bubble (same reasoning as the capacity branch).
                    if (m_AssistantMessage.Blocks.Count == 0)
                        m_Conversation.Messages.Remove(m_AssistantMessage);
                    else
                        m_AssistantMessage.IsComplete = true;

                    m_NotifyConversationChange?.Invoke(m_Conversation);
                });

                // Release the incomplete-message tracking so domain-reload recovery doesn't try to
                // resume a session that no longer exists.
                ReleaseIncompleteMessage();

                ReportTurnEnded("session_ended", null);
                return;
            }

            if (reason.Reason == CloseReason.ReasonType.ServerNoCapacity)
            {
                // The in-flight assistant message never received content (the server disconnected for
                // capacity). Remove the empty placeholder so the UI doesn't render a blank response
                // bubble; the UI layer orchestrates the capacity fallback (and resend) instead.
                m_Conversation.Messages.Remove(m_AssistantMessage);
                m_NotifyConversationChange?.Invoke(m_Conversation);
                Unsubscribe();
                // The placeholder is removed rather than completed normally, so release the
                // incomplete-message tracking — otherwise domain-reload recovery would try to resume a
                // message that no longer exists and the UI's "incomplete" state would stay set.
                ReleaseIncompleteMessage();
                MainThread.DispatchAndForget(() => m_CapacityReached?.Invoke(m_Conversation.Id));
                ReportTurnEnded("error", "no_capacity");
                return;
            }

            bool isInformational = reason.Reason == CloseReason.ReasonType.ServerDisconnectedInformational;
            bool isTransportOrNetwork =
                reason.Reason == CloseReason.ReasonType.CouldNotConnect
                || reason.Reason == CloseReason.ReasonType.UnderlyingWebSocketWasClosed
                || reason.Reason == CloseReason.ReasonType.ChatResponseTimeout
                || reason.Reason == CloseReason.ReasonType.DiscussionInitializationTimeout;

            string message = isTransportOrNetwork
                ? $"The connection to the AI Assistant was lost. {ErrorHandlingUtility.ErrorMessageNetworkedSuffix}"
                : !string.IsNullOrEmpty(reason.Info)
                    ? reason.Info
                    : $"Something went wrong. {ErrorHandlingUtility.ErrorMessageNetworkedSuffix}";

            var messageId = new AssistantMessageId(
                m_Conversation.Id,
                Guid.NewGuid().ToString(),
                AssistantMessageIdType.Internal);

            var closeMessage = isInformational
                ? AssistantMessage.AsInformational(messageId, message)
                : AssistantMessage.AsError(messageId, message);

            m_Conversation.Messages.Add(closeMessage);

            // Mark the current assistant message as complete since we're closing
            m_AssistantMessage.IsComplete = true;

            // Release the incomplete-message tracking.
            ReleaseIncompleteMessage();

            if (isInformational)
            {
                ReportTurnEnded("session_ended", null);
            }
            else
            {
                ReportTurnEnded("error", MapCloseReasonToFailureSubCode(reason));

                // Transport-class deaths mid-turn are resumable: the backend stored
                // the accumulated partial before cancelling its workflow, so a
                // continuation prompt on the same conversation picks the task back
                // up. Auth/unknown failures are not retried.
                //
                // Latched for the same reason ReportTurnEnded is: DisconnectFromServer
                // has no re-entrancy guard and awaits SendMessageInternal before raising
                // OnClose, so one dying turn can surface two closes (e.g. a chat-response
                // timeout racing a server disconnect). Without the latch that spends two
                // of the three retries on one failure and schedules two overlapping
                // re-drives, the later of which lands inside the turn the earlier started.
                if (Assistant.IsResumableMidTurnClose(reason.Reason)
                    && Interlocked.Exchange(ref m_AutoContinueDispatched, 1) == 0)
                    MainThread.DispatchAndForget(() => m_ConnectionLostMidTurn?.Invoke(
                        m_Conversation.Id, Assistant.AutoContinueReason.ConnectionLost, m_CancellationToken));
            }

            // Notify UI of the change
            m_NotifyConversationChange?.Invoke(m_Conversation);

            // Don't upload "bad-close" traces for informational disconnects (e.g. server graceful
            // shutdown for maintenance) — those are an expected, non-error condition.
            if (!isInformational)
            {
                TracesUploader.UploadTraces(m_Conversation.Id.Value, "bad-close");
            }
        }

        void HandleChatResponse(ChatResponseFragment fragment)
        {
            if (!m_FirstChunkSeen && m_PromptSentAt > 0)
            {
                m_FirstChunkSeen = true;
                var ttftMs = Math.Max(0L, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - m_PromptSentAt);
                // Snapshot ids before dispatching to the main thread to avoid racing with
                // any later mutation of m_AssistantMessage.
                var conversationIdSnapshot = m_Conversation.Id;
                var messageIdSnapshot = m_AssistantMessage.Id;
                MainThread.DispatchAndForget(() =>
                    AIAssistantAnalytics.ReportUserMessageTtftEvent(conversationIdSnapshot, messageIdSnapshot, ttftMs));
            }

            try
            {
                fragment.Parse(m_Conversation.Id, m_AssistantMessage, m_ResponseBuilder);

                if (fragment.UsedTokens.HasValue)
                    m_Conversation.ContextUsageUsedTokens = fragment.UsedTokens.Value;
                if (fragment.MaxTokens.HasValue)
                    m_Conversation.ContextUsageMaxTokens = fragment.MaxTokens.Value;
                if (fragment.IsLastFragment && (m_Conversation.ContextUsageUsedTokens > 0 || m_Conversation.ContextUsageMaxTokens > 0))
                    Assistant.SaveContextUsage(m_Conversation.Id.Value, m_Conversation.ContextUsageUsedTokens, m_Conversation.ContextUsageMaxTokens);
            }
            catch (Exception e)
            {
                InternalLog.LogError($"[HandleChatResponse] Error parsing fragment during message recovery: {e}");

                if (fragment.IsLastFragment)
                {
                    m_AssistantMessage.IsComplete = true;
                    Unsubscribe();
                    ReleaseIncompleteMessage();
                    ReportTurnEnded("error", "client_parse_error");
                }

                m_NotifyConversationChange?.Invoke(m_Conversation);
                TracesUploader.UploadTraces(m_Conversation.Id.Value, "parse-fragment-exception");
                return;
            }

            if (fragment.IsLastFragment)
            {
                var lastSnippet = fragment.Fragment?.Length > 200 ? fragment.Fragment[..200] + "..." : fragment.Fragment;
                InternalLog.Log($"<color=orange>[HandleChatResponse]</color> <color=#CC3333>LastFragment</color> ({fragment.Fragment?.Length} chars): {lastSnippet}");
                m_AssistantMessage.IsComplete = true;
                m_AssistantMessage.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                try
                {
                    // The backend's stall watchdog cancels a wedged turn and delivers a
                    // terminal control frame carrying its notice — a clean last message, so
                    // the transport-close auto-continue trigger never fires. Detected
                    // structurally (terminal frame + the backend's exact injection envelope),
                    // not by a text search of the response. The turn is as resumable as a
                    // transport death (partial stored server-side), so re-drive it through the
                    // same budgeted path.
                    var stalledByServer = IsServerStalledTurnFrame(fragment);

                    // Never re-drive a turn the user cancelled: AbortPrompt cancels this
                    // token before disconnecting the workflow, so a terminal frame already
                    // delivered or queued can still land here afterwards (HandleClose has
                    // the same guard). Re-checked inside the dispatch because the cancel
                    // can land between the check and the main-thread callback.
                    if (stalledByServer
                        && !m_CancellationToken.IsCancellationRequested
                        && Interlocked.Exchange(ref m_AutoContinueDispatched, 1) == 0)
                    {
                        InternalLog.LogWarning(
                            "[Assistant] Server stall watchdog cancelled the turn — dispatching auto-continue.");
                        var stalledConversationId = m_Conversation.Id;
                        MainThread.DispatchAndForget(() =>
                        {
                            if (m_CancellationToken.IsCancellationRequested)
                            {
                                InternalLog.LogWarning(
                                    "[Assistant] Auto-continue after server stall skipped — the prompt was cancelled.");
                                return;
                            }

                            m_ConnectionLostMidTurn?.Invoke(
                                stalledConversationId, Assistant.AutoContinueReason.ServerStalled, m_CancellationToken);
                        });
                    }

                    // A user-cancelled turn's queued last-fragment must not win the
                    // m_TurnEndReported latch as "completed" before HandleClose's "cancelled"
                    // branch runs — AbortPrompt cancels this token before ForceDisconnectWorkflow,
                    // and a terminal frame already delivered/queued can still land here afterwards
                    // (same guard as the auto-continue check above).
                    string turnOutcome;
                    string turnReason = null;
                    if (m_CancellationToken.IsCancellationRequested)
                    {
                        turnOutcome = "cancelled";
                    }
                    else if (stalledByServer)
                    {
                        turnOutcome = "error";
                        turnReason = "turn_stalled";
                    }
                    else
                    {
                        turnOutcome = "completed";
                    }
                    ReportTurnEnded(turnOutcome, turnReason);

                    // A turn whose final fragment is CLEAN succeeded, so restore the
                    // auto-continue budget: unrelated blips over a long session must not
                    // permanently disable recovery on a conversation whose every prior
                    // turn completed cleanly. Two constraints, both load-bearing
                    // (UUM-151091):
                    //  - Guarded on !stalledByServer: a server-stalled turn also lands
                    //    here (its notice is a terminal frame), just after dispatching its
                    //    own BUDGETED re-drive above — resetting the budget for it would
                    //    let a persistently stalling backend re-drive forever.
                    //  - Dispatched to the main thread: this handler runs on the websocket
                    //    receive thread and SessionState is main-thread-only. The previous
                    //    un-dispatched call threw UnityException here on every completed
                    //    turn, skipping the marker release below — the next domain reload
                    //    then falsely "recovered" the completed turn. A one-tick delay on
                    //    a budget reset is strictly conservative.
                    if (!stalledByServer)
                    {
                        var completedConversationId = m_Conversation.Id;
                        MainThread.DispatchAndForget(
                            () => Assistant.ClearAutoContinueAttempts(completedConversationId));
                    }
                }
                finally
                {
                    // Detach and release the incomplete-message marker (domain-reload
                    // tracking) — unconditionally. A throw above must not leave the
                    // persisted marker armed (the next reload would falsely "recover"
                    // this completed turn), nor leave this handler subscribed to a
                    // reused workflow (it would parse the NEXT turn's fragments into
                    // this completed message). Unsubscribe runs FIRST: it is pure
                    // delegate removal and cannot throw, while the release invokes an
                    // external handler — a throwing subscriber must not skip the very
                    // detach this block exists to guarantee (review).
                    Unsubscribe();
                    ReleaseIncompleteMessage();
                }

                if (m_IsNewConversation)
                {
                    // TODO: Remove this dispatch when REST is replaced or changed to HttpClient that can be in background threads.
                    MainThread.DispatchAndForget(() =>
                    {
                        m_NotifyConversationChange?.Invoke(m_Conversation);
                    });
                }
            }
            else
            {
                var snippet = fragment.Fragment?.Length > 300 ? fragment.Fragment[..300] + "..." : fragment.Fragment;
                InternalLog.Log($"<color=orange>[HandleChatResponse]</color> Fragment ({fragment.Fragment?.Length} chars): {snippet}");
            }

            AssistantPerf.SetGauge(PerfGauge.ConversationMessages, m_Conversation.Messages.Count);
            AssistantPerf.AddGauge(PerfGauge.StreamedCharsTotal, fragment.Fragment?.Length ?? 0);

            using (AssistantPerf.Measure(PerfProbe.StreamChunkNotify))
            {
                m_NotifyConversationChange?.Invoke(m_Conversation);
            }
        }

        /// <summary>
        /// True when this frame IS the backend stall watchdog's terminal control frame,
        /// rather than assistant prose that mentions or reproduces its notice. The signal
        /// is structural, not a text search: the frame must be the terminal fragment
        /// (<see cref="ChatResponseFragment.IsLastFragment"/> — the server-set last_message
        /// flag) AND its whole per-frame delta must equal the backend's exact injection
        /// envelope (<see cref="Assistant.k_ServerStalledTurnFrameMarkdown"/> — the notice
        /// with the fixed "\n\n" prefix the emitter prepends, unity/muse#2701). An ordinary
        /// answer that merely quotes the sentence is neither a last_message frame nor
        /// prefixed with that envelope, so it can no longer be read as the control signal.
        /// The backend carries no typed stall discriminator to the client (verified against
        /// unity/muse#2701 — the flag is server-internal), so this envelope is the strongest
        /// available structural key; a typed terminal-reason field would supersede it.
        /// </summary>
        internal static bool IsServerStalledTurnFrame(ChatResponseFragment fragment) =>
            fragment.IsLastFragment
            && string.Equals(
                fragment.Fragment,
                Assistant.k_ServerStalledTurnFrameMarkdown,
                StringComparison.Ordinal);

        void OnWorkflowStateChange(State newState)
        {
            var conversationID = new AssistantConversationId(m_Workflow.ConversationId);
            switch (newState)
            {
                case State.NotStarted:
                    m_ChangePromptState?.Invoke(conversationID, Assistant.PromptState.NotConnected, $"Conversation {conversationID} has not yet started");
                    break;
                case State.AwaitingDiscussionInitialization:
                    m_ChangePromptState?.Invoke(conversationID, Assistant.PromptState.Connecting, $"Conversation {conversationID} is awaiting discussion initialization");
                    break;
                case State.Idle:
                    if (!m_Workflow.MessagesSent)
                        m_ChangePromptState?.Invoke(conversationID, Assistant.PromptState.AwaitingServer, $"Conversation {conversationID} is waiting for the server to reply to a prompt.");
                    else
                        m_ChangePromptState?.Invoke(conversationID, Assistant.PromptState.Connected, $"Conversation {conversationID} is connected and ready.");
                    break;
                case State.AwaitingChatAcknowledgement:
                    m_ChangePromptState?.Invoke(conversationID, Assistant.PromptState.AwaitingServer, $"Conversation {conversationID} is waiting for the server to reply to a prompt.");
                    break;
                case State.AwaitingChatResponse:
                    m_ChangePromptState?.Invoke(conversationID, Assistant.PromptState.AwaitingClient, $"Conversation {conversationID} is constructing context with the server.");
                    break;
                case State.ProcessingStream:
                    m_ChangePromptState?.Invoke(conversationID, Assistant.PromptState.AwaitingServer, $"Conversation {conversationID} is streaming a message from the server.");
                    break;
                case State.Canceling:
                    m_ChangePromptState?.Invoke(conversationID, Assistant.PromptState.Canceling, $"User elected to cancel request on conversation {conversationID}");
                    break;
                case State.Closed:
                    m_ChangePromptState?.Invoke(conversationID, Assistant.PromptState.NotConnected, $"Conversation {conversationID}'s websocket has closed.  A new websocket must be created.");
                    break;
            }
        }
    }

    internal partial class Assistant
    {
        readonly IDictionary<AssistantConversationId, AssistantConversation> m_ConversationCache =
            new Dictionary<AssistantConversationId, AssistantConversation>();

        public enum PromptState
        {
            NotConnected,
            Connecting,
            Connected,
            AwaitingServer,
            AwaitingClient,
            Canceling
        }

        internal PromptState CurrentPromptState { get; private set; }

        public event Action<AssistantConversationId, PromptState> PromptStateChanged;

        CancellationTokenSource m_ConnectionCancelToken;

        class PromptContext
        {
            public CredentialsContext Credentials;

            public AssistantContextEntry[] Asset;

            public List<ChatRequestV1.AttachedContextModel> Attached;
        }

        void ChangePromptState(AssistantConversationId conversationId, PromptState newState, string message, bool force = false)
        {
            if (CurrentPromptState == newState && !force)
            {
                return;
            }
            
            InternalLog.Log($"Changing state from {CurrentPromptState} to {newState} because {message}");
            CurrentPromptState = newState;
            PromptStateChanged?.Invoke(conversationId, newState);
            
            if (newState == PromptState.Canceling && 
                m_ConversationCache[conversationId]?.Messages.Count > 0 && 
                m_ConversationCache[conversationId]?.Messages[^1].Role == "assistant")
            {
                m_ConversationCache[conversationId].Messages[^1].IsComplete = true;
            }
        }

        public void AbortPrompt(AssistantConversationId conversationId)
        {
            // The user stopping the turn makes the conversation ineligible for an auto-continue.
            // Before the early-out below, not after: a transport close leaves the state at
            // NotConnected, which is exactly the state that takes that early return — and is
            // exactly when a re-drive is pending.
            SupersedePendingAutoContinue();

            if (CurrentPromptState is PromptState.Canceling or PromptState.NotConnected)
            {
                InternalLog.LogWarning($"AbortPrompt: Ignored in state {CurrentPromptState}");
                ChangePromptState(conversationId, PromptState.NotConnected, "Enforcing Not Connected on Abort", true);
                return;
            }

            m_ConnectionCancelToken?.Cancel();

            // Orchestration uses workflows to manage the connection to the backend rather than the stream object.
            // When orchestration is the only system, the stream objects will be removed.
            if (Backend is BaseWebSocketBackend webSocketBackend)
            {
                var workflow = webSocketBackend.ActiveWorkflow;
                if (workflow != null && workflow.ConversationId == conversationId.Value)
                    workflow.CancelCurrentChatRequest();

                webSocketBackend.ForceDisconnectWorkflow(conversationId.Value);
                ChangePromptState(conversationId, PromptState.NotConnected, "User cancelled the prompt. Disconnected workflow instantly.");
            }
        }

        public void DisconnectWorkflow()
        {
            if (Backend is BaseWebSocketBackend webSocketBackend)
            {
                webSocketBackend.ActiveWorkflow?.LocalDisconnect();
            }
        }

        // --- Pre-conversation prompt recovery -------------------------------------
        // A domain reload between prompt submission and IncompleteMessageStarted
        // (i.e. before the conversation/session exists) vaporizes the prompt: the
        // async ProcessPrompt continuation is lost with the AppDomain, no
        // conversation id was ever assigned, so the IncompleteMessageId recovery
        // path has nothing to recover and the turn silently never starts.
        // Persist the prompt in SessionState (survives domain reloads, cleared on
        // editor restart) for the window between submission and the
        // IncompleteMessageStarted handoff; the UI re-drives it after a reload
        // (see AssistantView.RestoreUIState).
        const string k_PendingPromptTextKey = "AI.Assistant.PendingPromptText";
        const string k_PendingPromptModeKey = "AI.Assistant.PendingPromptMode";
        const string k_PendingPromptContextKey = "AI.Assistant.PendingPromptContext";
        const string k_PendingPromptDomainKey = "AI.Assistant.PendingPromptDomain";

        // Set synchronously the instant the conversation + incomplete message exist (the
        // IncompleteMessageStarted handoff), before either callback that handoff queues can run. Its
        // presence flips the marker from "re-drive me" to "the IncompleteMessageId path owns this
        // turn now": TryGetPendingPromptRecovery reports no lost prompt while it is set. That is what
        // makes a reload which discards *both* queued callbacks — the IncompleteMessageId write and
        // the marker release — degrade to an unrecoverable incomplete message instead of re-driving a
        // turn the backend already received (a second send that, in Agent mode, re-applies edits and
        // bills another turn). The dispatch ordering alone cannot cover a dispatcher that never pumps;
        // this synchronous flag can.
        const string k_PendingPromptHandoffKey = "AI.Assistant.PendingPromptHandoff";

        // Identifies the AppDomain that armed the marker. Statics are re-created by a domain
        // reload, so this value is stable within a domain and always differs across one.
        // The marker means "the prompt was lost", but SessionState alone cannot say that: it
        // only says "a ProcessPrompt has not reached its handoff yet", which is equally true
        // while that ProcessPrompt is still running. Restores happen outside domain reloads
        // too (AssistantWindow.CreateGUI -> InitializeState runs on every window open), so
        // without this stamp merely re-opening or re-docking the Assistant window during the
        // pre-conversation window re-sends a prompt that is still in flight.
        static readonly string k_DomainId = Guid.NewGuid().ToString("N");

        internal static void SetPendingPromptRecovery(AssistantPrompt prompt)
        {
            if (string.IsNullOrEmpty(prompt?.Value))
                return;

            // Snapshot the submitted attachments, not just the text: the UI clears its selected
            // context and persists the now-empty session context as soon as this send returns, so by
            // the time a re-drive runs this is the only surviving copy of what was attached. Without
            // it the re-drive rebuilds the turn with no object/virtual/console/screenshot context.
            // Serialized here (synchronously, on the main thread, while the prompt still holds the
            // attachments) for the same reason ProcessPrompt serializes its own copy on the main
            // thread — the entries do AssetDatabase lookups.
            string contextJson = null;
            try
            {
                contextJson = JsonUtility.ToJson(ContextSerializationHelper.BuildPromptSelectionContext(
                    prompt.ObjectAttachments, prompt.VirtualAttachments, prompt.ConsoleAttachments));
            }
            catch (Exception e)
            {
                // A context-serialization failure must never cost us the prompt text itself, which is
                // the part that cannot be reconstructed from anywhere else after the reload.
                InternalLog.LogException(e);
            }

            SessionState.SetString(k_PendingPromptTextKey, prompt.Value);
            SessionState.SetString(k_PendingPromptModeKey, prompt.Mode.ToString());
            SessionState.SetString(k_PendingPromptContextKey, contextJson ?? string.Empty);
            SessionState.SetString(k_PendingPromptDomainKey, k_DomainId);
            // A freshly armed marker has not reached its handoff yet; clear any stale handoff flag
            // left by an earlier turn so this prompt stays re-drivable until it actually hands off.
            SessionState.EraseString(k_PendingPromptHandoffKey);
        }

        internal static void ClearPendingPromptRecovery()
        {
            SessionState.EraseString(k_PendingPromptTextKey);
            SessionState.EraseString(k_PendingPromptModeKey);
            SessionState.EraseString(k_PendingPromptContextKey);
            SessionState.EraseString(k_PendingPromptDomainKey);
            SessionState.EraseString(k_PendingPromptHandoffKey);
        }

        /// <summary>
        /// Re-stamps an armed marker as belonging to the current domain, without discarding the
        /// prompt it holds. This is what a re-drive should do instead of clearing: the re-drive
        /// only *schedules* the prompt (delayCall, plus the Task.Yield inside
        /// WithExceptionLogging before ProcessPrompt re-arms), so erasing up front leaves an
        /// editor frame in which the prompt exists solely inside an EditorApplication.update
        /// delegate — which a domain reload discards, losing the prompt for good. Re-stamping
        /// keeps the text armed across that gap while making the marker invisible to any further
        /// TryGetPendingPromptRecovery in this domain, so a second restore cannot double-send.
        /// </summary>
        internal static void ClaimPendingPromptRecovery()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(k_PendingPromptTextKey, null)))
                return;
            SessionState.SetString(k_PendingPromptDomainKey, k_DomainId);
        }

        internal static bool TryGetPendingPromptRecovery(out string promptText, out AssistantMode mode, out string contextJson)
        {
            promptText = SessionState.GetString(k_PendingPromptTextKey, null);
            var modeText = SessionState.GetString(k_PendingPromptModeKey, null);
            var domainId = SessionState.GetString(k_PendingPromptDomainKey, null);
            mode = default;
            contextJson = null;
            if (string.IsNullOrEmpty(promptText))
                return false;
            // The prompt already reached the IncompleteMessageStarted handoff, so the conversation
            // and its incomplete message exist and the IncompleteMessageId path owns reload recovery
            // from here. Re-driving would send a turn the backend already has, so the marker is no
            // longer a lost-prompt signal — only a leftover for the queued clear (or the next arm) to
            // tidy up. Checked before the domain gate: it must veto a re-drive even across a reload,
            // where the stamp is from a dead domain.
            if (!string.IsNullOrEmpty(SessionState.GetString(k_PendingPromptHandoffKey, null)))
                return false;
            // Armed by the domain we are still running in: the ProcessPrompt that armed it is
            // alive and owns the prompt, so nothing was lost and re-driving would duplicate
            // the turn. Only a stamp from a dead domain proves the continuation died with it.
            if (string.Equals(domainId, k_DomainId, StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrEmpty(modeText) && Enum.TryParse<AssistantMode>(modeText, out var parsed))
                mode = parsed;
            contextJson = SessionState.GetString(k_PendingPromptContextKey, null);
            return true;
        }
        // ---------------------------------------------------------------------------

        // --- Mid-turn connection-loss auto-continue --------------------------------
        // When the cloud WebSocket dies mid-turn (gateway endpoint update, silent
        // half-open cut by the relay's dead-peer watchdog, plain network blip), the
        // backend cancels the in-flight workflow but STORES the accumulated partial
        // assistant message — every completed tool call/result is in the
        // conversation history, and the project edits are already on disk. The turn
        // is therefore resumable: re-driving a continuation prompt on the same
        // conversation starts a fresh agent loop that reads the history and
        // finishes the remaining work, instead of the case/user dead-ending on
        // "connection lost". Budgeted per conversation so a persistently failing
        // backend can't loop; state lives in SessionState so it survives the
        // domain reloads that are routine mid-turn.
        const int k_MaxAutoContinuesPerConversation = 3;
        // Escalating per-attempt delay. Gateway dataplane reconciles (the dominant
        // drop source) reset connections for a WINDOW of ~1-2 minutes, not an
        // instant: a fixed 3s delay burned every attempt inside the same window
        // (observed 2026-08-18: two cases auto-continued 3s after a reconcile-burst
        // 1006 and the re-driven turn died in the same window). Attempt 1 stays
        // fast for blips; attempts 2-3 back off far enough to outlive a window.
        static readonly double[] k_AutoContinueDelaysSeconds = { 3.0, 20.0, 60.0 };
        internal const string k_AutoContinuePromptText =
            "The connection dropped while you were working. Review the conversation " +
            "history and the current project state to see what is already done, then " +
            "continue and finish the remaining work.";
        // Stall counterpart of k_AutoContinuePromptText. A server-stalled turn was
        // cancelled server-side with NOTHING dropped, so it must not tell the model
        // (or show the user) that the connection was lost — that is a false premise
        // that misleads both, and ProcessPromptInternal renders this text as a real
        // user bubble that returns as a PromptBlock on reload. Same resumable shape
        // (partial stored server-side), so the continuation instruction is otherwise
        // identical.
        internal const string k_StalledContinuePromptText =
            "The previous turn was cancelled by the server before it finished. Review " +
            "the conversation history and the current project state to see what is " +
            "already done, then continue and finish the remaining work.";
        // The backend's TURN_STALLED_USER_MESSAGE, verbatim (unity/muse turn-stall
        // watchdog, unity/muse#2701). The watchdog cancels a wedged turn and ends it
        // as a NORMAL last message — no transport close — so the connection-loss
        // trigger below never sees it. Same resumable shape as a transport death (the
        // backend stored the accumulated partial before cancelling), so a continuation
        // on the same conversation finishes the remaining work under the same
        // per-conversation budget.
        //
        // The backend carries NO typed stall discriminator to the client: the stall
        // status lives only on the server-internal CancelEvent(stalled=True), and the
        // wire frame (CHAT_RESPONSE_V1) has no field for it — verified against
        // unity/muse#2701 head, both emitters. So detection keys on the STRUCTURE of
        // that control frame rather than on assistant-authored text (see
        // IsServerStalledTurnFrame): it must be the terminal frame (last_message=true)
        // AND its whole delta must equal the exact injection envelope below — the
        // notice with the fixed "\n\n" prefix the emitter prepends. Assistant prose
        // that merely quotes (or is asked to reproduce) the sentence is not that
        // envelope, so it can no longer masquerade as the control signal. The durable
        // fix is a typed terminal-reason field on the frame; this client should prefer
        // it once the backend adds one. If the backend reworks the wording meanwhile
        // this stops matching and the turn simply ends as it did before this feature —
        // the safe direction to fail.
        internal const string k_ServerStalledTurnNotice =
            "The assistant stopped making progress and the request was cancelled by the " +
            "server. Please continue.";
        // The exact terminal-frame markdown both backend emitters send:
        // markdown = "\n\n" + TURN_STALLED_USER_MESSAGE, last_message=true. Matching
        // this envelope (not the bare sentence) is what makes the trigger structural
        // rather than a text search of the response.
        internal const string k_ServerStalledTurnFrameMarkdown =
            "\n\n" + k_ServerStalledTurnNotice;

        // A scheduled re-drive is a PROMISE to continue a turn nobody else is driving,
        // and the promise cannot live only in the delayed task's closure:
        //   * the delay runs up to a minute, and a domain reload inside it takes the
        //     AppDomain — and the closure — with it. The close path has already
        //     released the incomplete-message marker, so nothing else would ever
        //     restart the turn: the exact stranding this feature exists to prevent.
        //   * the close also flips the UI to NotConnected, which re-enables the input
        //     field. A user prompt (or a resume) landing inside the delay means the
        //     turn IS being driven, and the scheduled re-drive becomes an unsolicited
        //     duplicate prompt on the same conversation.
        // Both are handled by keeping the promise in SessionState, which survives
        // reloads and is visible to everyone: supersession is just erasing the key
        // (see SupersedePendingAutoContinue, called whenever a prompt starts or the
        // user aborts), and a reload is recovered by re-driving the record after the
        // restore (see AssistantView.RestoreUIState). The delayed task itself is left
        // to expire; it re-checks the record when it wakes and no-ops if it is gone.
        const string k_PendingAutoContinueKey = "AI.Assistant.PendingAutoContinue";
        // Wall-clock (unix ms) the pending record was armed at, so a record left behind by a
        // reload that never reached its consumer can be aged out (see TryConsumePendingAutoContinue).
        const string k_PendingAutoContinueArmedAtKey = "AI.Assistant.PendingAutoContinueArmedAt";

        // The SessionState key the UI persists the active conversation under
        // (AssistantUISessionState.LastActiveConversationId). Read raw because the UI assembly is
        // not referenced from here — same reason AcpProvider writes this key directly.
        const string k_ActiveConversationIdKey = "AssistantUserSession_LastActiveConversationId";

        // Longest backoff plus a generous domain-reload+recompile allowance: a reload that lands at
        // the very end of the backoff must still be recoverable when the post-restore consumer runs,
        // but a record much older than this can only be a leftover from a turn that has since ended —
        // re-driving it would resume a turn that finished long ago.
        static readonly long k_PendingAutoContinueMaxAgeMs =
            (long)(k_AutoContinueDelaysSeconds[^1] * 1000) + 180_000;

        static string AutoContinueAttemptsKey(string conversationId) =>
            $"AI.Assistant.AutoContinueAttempts.{conversationId}";
        static string LastPromptModeKey(string conversationId) =>
            $"AI.Assistant.LastPromptMode.{conversationId}";
        static string LastPromptModelKey(string conversationId) =>
            $"AI.Assistant.LastPromptModel.{conversationId}";

        static void RecordLastPromptMode(AssistantConversationId conversationId, AssistantMode mode)
        {
            if (conversationId.IsValid)
                SessionState.SetString(LastPromptModeKey(conversationId.Value), mode.ToString());
        }

        // Why a turn is being auto-continued. Selects the continuation prompt text and
        // the log label so the stall path never surfaces the connection-loss premise
        // (nothing dropped on a server stall) — see k_StalledContinuePromptText.
        internal enum AutoContinueReason
        {
            ConnectionLost,
            ServerStalled
        }

        // Persist the turn's model tier alongside the mode so the connection-loss re-drive replays
        // it. A UI-originated prompt has ModelConfiguration filled by AssistantUIAPIInterpreter; the
        // in-session re-drive builds AssistantPrompt directly and would otherwise fall back to the
        // backend default (e.g. silently dropping unity-max). Cleared when a later prompt carries no
        // model, so a stale tier is never replayed.
        static void RecordLastPromptModel(AssistantConversationId conversationId, string modelName)
        {
            if (!conversationId.IsValid)
                return;
            if (string.IsNullOrEmpty(modelName))
                SessionState.EraseString(LastPromptModelKey(conversationId.Value));
            else
                SessionState.SetString(LastPromptModelKey(conversationId.Value), modelName);
        }

        /// <summary>
        /// Restore the per-conversation auto-continue budget after a turn completes cleanly. Keeps the
        /// anti-loop property (a persistently failing backend never reaches a final fragment, so its
        /// budget still decays to exhaustion) while stopping unrelated blips over a long session from
        /// permanently disabling auto-continue on a conversation whose every prior recovery succeeded.
        /// </summary>
        internal static void ClearAutoContinueAttempts(AssistantConversationId conversationId)
        {
            if (conversationId.IsValid)
                SessionState.EraseInt(AutoContinueAttemptsKey(conversationId.Value));
        }

        /// <summary>
        /// True for close reasons that leave a mid-turn conversation resumable: transport/network
        /// drops and server-initiated disconnects. Auth failures, capacity, graceful/informational
        /// closes, and unknown reasons are NOT resumable and must never trigger an auto-continue.
        /// </summary>
        internal static bool IsResumableMidTurnClose(CloseReason.ReasonType reason) =>
            reason == CloseReason.ReasonType.CouldNotConnect
            || reason == CloseReason.ReasonType.UnderlyingWebSocketWasClosed
            || reason == CloseReason.ReasonType.ChatResponseTimeout
            || reason == CloseReason.ReasonType.DiscussionInitializationTimeout
            || reason == CloseReason.ReasonType.ServerDisconnected
            || reason == CloseReason.ReasonType.ServerSentUnknownMessage
            || reason == CloseReason.ReasonType.ServerSentMessageAtWrongTime;

        /// <summary>
        /// Drop any pending auto-continue re-drive, because the conversation has stopped being
        /// eligible for one: a prompt has started (someone else is driving the turn now) or the
        /// user aborted. Safe to call when nothing is pending.
        /// </summary>
        internal static void SupersedePendingAutoContinue()
        {
            SessionState.EraseString(k_PendingAutoContinueKey);
            SessionState.EraseString(k_PendingAutoContinueArmedAtKey);
        }

        /// <summary>
        /// Claim the pending auto-continue for <paramref name="conversationId"/> and charge one
        /// attempt against the budget. Returns false — with nothing to launch — when there is no
        /// pending record, it belongs to a different conversation, it has aged out, the recorded
        /// prompt mode is unreadable, or the budget is spent. On success <paramref name="modelName"/>
        /// carries the turn's recorded model tier (null = backend default). One-shot: a matching
        /// record is retired either way, so this is also the supersession check for the delayed task.
        /// </summary>
        /// <remarks>
        /// The attempt is charged HERE, at the moment a re-drive actually launches, rather than
        /// when one is scheduled: a promise lost to a domain reload (or superseded by the user
        /// re-sending by hand) must not burn budget it never spent.
        /// </remarks>
        internal static bool TryConsumePendingAutoContinue(
            AssistantConversationId conversationId,
            out string promptText,
            out AssistantMode mode,
            out string modelName)
        {
            promptText = null;
            mode = default;
            modelName = null;

            if (!conversationId.IsValid)
                return false;

            var pending = SessionState.GetString(k_PendingAutoContinueKey, null);
            if (string.IsNullOrEmpty(pending) || pending != conversationId.Value)
                return false;

            // The record is ours to resolve, so retire it (and its metadata) whatever we decide below.
            SessionState.EraseString(k_PendingAutoContinueKey);
            var armedAtText = SessionState.GetString(k_PendingAutoContinueArmedAtKey, null);
            SessionState.EraseString(k_PendingAutoContinueArmedAtKey);

            // Age out a record armed long ago but never consumed — the user switched away before a
            // reload, or an incomplete-message marker pre-empted the restore consumer — so a much
            // later reload can't re-drive a turn that ended long ago. Wall-clock because the stamp
            // must survive domain reloads without an editor-lifetime clock; SessionState is cleared
            // on editor restart so it can't outlive one. NOTE (UUM-151091 audit): this comment used
            // to claim the consume "can run off the main thread from the delayed task" — false, and
            // a trap: both dispatch sites go through MainThread.DispatchAndForget and the delayed
            // task's continuation resumes on Unity's main-thread sync context, which the raw
            // SessionState reads above REQUIRE (off-main they throw UnityException).
            if (long.TryParse(armedAtText, out var armedAtMs)
                && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - armedAtMs > k_PendingAutoContinueMaxAgeMs)
            {
                InternalLog.LogWarning(
                    $"[Assistant] Pending auto-continue on {conversationId.Value} is older than " +
                    $"{k_PendingAutoContinueMaxAgeMs / 1000}s — retiring the stale re-drive.");
                return false;
            }

            var modeText = SessionState.GetString(LastPromptModeKey(conversationId.Value), null);
            if (string.IsNullOrEmpty(modeText) || !Enum.TryParse<AssistantMode>(modeText, out mode))
            {
                InternalLog.LogWarning(
                    $"[Assistant] Pending auto-continue on {conversationId.Value} has no readable " +
                    "prompt mode — dropping it.");
                return false;
            }

            // Optional: absent means the turn ran on the backend default, which the re-drive keeps.
            var modelText = SessionState.GetString(LastPromptModelKey(conversationId.Value), null);
            if (!string.IsNullOrEmpty(modelText))
                modelName = modelText;

            var attemptsKey = AutoContinueAttemptsKey(conversationId.Value);
            var attempts = SessionState.GetInt(attemptsKey, 0);
            if (attempts >= k_MaxAutoContinuesPerConversation)
            {
                InternalLog.LogWarning(
                    $"[Assistant] Auto-continue budget exhausted on {conversationId.Value} " +
                    $"({attempts}/{k_MaxAutoContinuesPerConversation}) — surfacing the error.");
                return false;
            }

            SessionState.SetInt(attemptsKey, attempts + 1);
            InternalLog.LogWarning(
                $"[Assistant] Re-driving auto-continue on {conversationId.Value} " +
                $"(attempt {attempts + 1}/{k_MaxAutoContinuesPerConversation}, mode {mode}).");

            promptText = k_AutoContinuePromptText;
            return true;
        }

        internal void TryAutoContinueAfterConnectionLoss(
            AssistantConversationId conversationId,
            AutoContinueReason reason = AutoContinueReason.ConnectionLost,
            CancellationToken cancellationToken = default)
        {
            if (!conversationId.IsValid)
                return;

            // Stall vs connection-loss: choose the log label here and (on re-drive) the continuation
            // prompt text after TryConsumePendingAutoContinue. A server-stalled turn dropped nothing,
            // so it must never surface the connection-loss premise — see k_StalledContinuePromptText.
            var reasonLabel = reason == AutoContinueReason.ServerStalled
                ? "Server stalled the turn"
                : "Connection lost mid-turn";

            var modeText = SessionState.GetString(LastPromptModeKey(conversationId.Value), null);
            if (string.IsNullOrEmpty(modeText) || !Enum.TryParse<AssistantMode>(modeText, out var mode))
            {
                InternalLog.LogWarning(
                    $"[Assistant] {reasonLabel} on {conversationId.Value} but no recorded " +
                    "prompt mode — skipping auto-continue.");
                return;
            }

            // Peek at the budget so a spent one is reported (and nothing armed) at the point of
            // failure rather than a minute later; it is re-checked, and only then charged, by
            // TryConsumePendingAutoContinue when the re-drive actually launches.
            var attempts = SessionState.GetInt(AutoContinueAttemptsKey(conversationId.Value), 0);
            if (attempts >= k_MaxAutoContinuesPerConversation)
            {
                InternalLog.LogWarning(
                    $"[Assistant] {reasonLabel} on {conversationId.Value}; auto-continue " +
                    $"budget exhausted ({attempts}/{k_MaxAutoContinuesPerConversation}) — surfacing the error.");
                return;
            }

            // Arm the promise BEFORE the delay, so a domain reload inside the delay is recovered
            // by the post-restore re-drive instead of stranding the turn. Stamp the arm time so a
            // record that never reaches a consumer (see TryConsumePendingAutoContinue) can be aged out.
            SessionState.SetString(k_PendingAutoContinueKey, conversationId.Value);
            SessionState.SetString(
                k_PendingAutoContinueArmedAtKey,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

            var delaySeconds = k_AutoContinueDelaysSeconds[
                Math.Min(attempts, k_AutoContinueDelaysSeconds.Length - 1)];

            InternalLog.LogWarning(
                $"[Assistant] {reasonLabel} on {conversationId.Value} — auto-continuing " +
                $"(attempt {attempts + 1}/{k_MaxAutoContinuesPerConversation}, mode {mode}) in " +
                $"{delaySeconds:0}s.");

            _ = TaskUtils.WithExceptionLogging(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                // A cancel can land anywhere in this backoff (up to a minute): AbortPrompt
                // cancels the stalled/dead turn's token before disconnecting the workflow,
                // and CancelAssistant reaches AbortPrompt with no IsAPIStreaming gate. The
                // dispatch-time guard cannot see a cancel that arrives DURING the wait, so
                // re-check the same token here. Inert on the happy path (the token is only
                // cancelled by AbortPrompt); without it the wait swallows the cancel and we
                // re-drive a turn the user abandoned.
                if (cancellationToken.IsCancellationRequested)
                {
                    InternalLog.LogWarning(
                        $"[Assistant] Auto-continue on {conversationId.Value} skipped after backoff — " +
                        "the prompt was cancelled.");
                    return;
                }

                // The backoff is long enough (up to a minute) that the work may already
                // have been picked up by the time it elapses — the user re-sent by hand,
                // or an earlier attempt's re-drive is now streaming. Re-driving into a
                // live workflow is destructive: ProcessPromptInternal appends a phantom
                // prompt plus an assistant message that never completes, repoints the
                // incomplete-message recovery marker at that orphan, and subscribes a
                // SECOND event handler onto the running turn (so its fragments are parsed
                // twice) before SendChatRequest finally throws for not being State.Idle.
                // Worse, if the live workflow belongs to a different conversation,
                // GetOrCreateWorkflow LocalDisconnects it — killing that turn. If anything
                // is already in flight the turn is being driven, so stand down.
                var active = Backend?.ActiveWorkflow;
                var workflowBusy = active != null
                    && active.WorkflowState != State.Idle
                    && active.WorkflowState != State.Closed;
                if (workflowBusy || CurrentPromptState == PromptState.Connecting)
                {
                    InternalLog.LogWarning(
                        $"[Assistant] Skipping auto-continue on {conversationId.Value}: work is already " +
                        $"in flight (workflow state {(active == null ? "none" : active.WorkflowState.ToString())}, " +
                        $"prompt state {CurrentPromptState}).");
                    // Someone else owns the turn, so the promise is stale — retire it rather than
                    // leave it for a later restore to re-drive out of nowhere.
                    SupersedePendingAutoContinue();
                    return;
                }

                // Switching conversations during the backoff is not a supersession event
                // (ConversationReloadManager.LoadConversationAsync only calls SetActiveConversation),
                // so the record for the dead conversation can still be here while a different one is
                // on screen. Re-driving it now would stream a turn into the background: the UI drops
                // ChangePromptState for a non-active conversation, so no working state and no Stop
                // button appear. Retire the record as stale when the UI is KNOWN to be on a different
                // conversation. Guarded on a non-empty active id on purpose: a headless/automation
                // driver that never populates LastActiveConversationId must still recover (that is the
                // environment this feature was validated in), so an unknown active conversation proceeds.
                var activeConversationId = SessionState.GetString(k_ActiveConversationIdKey, null);
                if (!string.IsNullOrEmpty(activeConversationId) && activeConversationId != conversationId.Value)
                {
                    InternalLog.LogWarning(
                        $"[Assistant] Auto-continue on {conversationId.Value} is no longer the active " +
                        $"conversation (active is {activeConversationId}) — retiring the stale re-drive.");
                    SupersedePendingAutoContinue();
                    return;
                }

                // Supersession check and budget charge in one step: a prompt that started during
                // the delay erased the record, and a turn that already finished is not ours to
                // continue. Nothing to do if the promise is gone.
                if (!TryConsumePendingAutoContinue(conversationId, out var promptText, out var promptMode, out var promptModel))
                {
                    InternalLog.LogWarning(
                        $"[Assistant] Auto-continue on {conversationId.Value} was superseded during its " +
                        "backoff — standing down.");
                    return;
                }

                // A server-stalled turn dropped nothing, so replay the stall continuation text rather
                // than the connection-loss premise TryConsumePendingAutoContinue returns by default.
                // The reason is captured from this in-session dispatch; a re-drive recovered after a
                // domain reload (via AssistantView.RestoreUIState) has no reason and keeps the default.
                if (reason == AutoContinueReason.ServerStalled)
                    promptText = k_StalledContinuePromptText;

                // Clear the connection-loss error the close path left in the conversation, mirroring
                // AssistantUIAPIInterpreter.SendPrompt.RemoveErrorFromCurrentConversation — otherwise
                // "The connection to the AI Assistant was lost." sits above the actively streaming
                // continuation. (The reload-recovery arm goes through SendPrompt and already clears it.)
                RemoveConnectionLossErrors(conversationId);

                // Replay the turn's model tier so the continuation runs on the same model the user
                // chose, not the backend default (a UI prompt has this filled by SendPrompt).
                var prompt = new AssistantPrompt(promptText, promptMode);
                if (!string.IsNullOrEmpty(promptModel))
                    prompt.ModelConfiguration = new ModelConfiguration { Name = promptModel };

                // isAutoContinue: true so that if this re-drive itself fails to reconnect, the pre-init
                // close path in ProcessPromptInternal escalates to the next backoff attempt. A fresh user
                // prompt reaches DoProcessPrompt (via the public ProcessPrompt) without this flag and
                // therefore never escalates.
                await DoProcessPrompt(conversationId, prompt, isAutoContinue: true);
            });
        }

        /// <summary>
        /// Remove any error/informational messages from the cached conversation. Mirrors the UI's
        /// <c>AssistantUIAPIInterpreter.RemoveErrorFromCurrentConversation</c> so the in-session
        /// auto-continue re-drive (which calls <see cref="ProcessPrompt"/> directly, bypassing the UI
        /// send path) doesn't leave the connection-loss error hanging above the streaming continuation.
        /// </summary>
        void RemoveConnectionLossErrors(AssistantConversationId conversationId)
        {
            if (!conversationId.IsValid || !m_ConversationCache.TryGetValue(conversationId, out var conversation))
                return;

            var removed = conversation.Messages.RemoveAll(m => m.IsError || m.IsInformational);
            if (removed > 0)
                NotifyConversationChange(conversation);
        }
        // ---------------------------------------------------------------------------

        // IAssistantProvider entry point. A prompt submitted through the public API is always a
        // fresh user turn — never the connection-loss auto-continue re-driving itself — so
        // isAutoContinue is false here and such a prompt can never escalate an auto-continue on a
        // transient connect failure (see the pre-init close branch in ProcessPromptInternal). The
        // 4-parameter signature is the interface member; the flag lives only on the internal
        // DoProcessPrompt the in-session re-drive calls directly.
        public Task ProcessPrompt(
            AssistantConversationId conversationId,
            AssistantPrompt prompt,
            IAgent agent = null,
            CancellationToken ct = default)
            => DoProcessPrompt(conversationId, prompt, agent, ct, isAutoContinue: false);

        async Task DoProcessPrompt(
            AssistantConversationId conversationId,
            AssistantPrompt prompt,
            IAgent agent = null,
            CancellationToken ct = default,
            bool isAutoContinue = false)
        {
            // A prompt starting supersedes any pending auto-continue: this turn is being
            // driven now, so a re-drive scheduled by an earlier transport death would be an
            // unsolicited duplicate. Runs before the first await AND before the Connecting
            // state change, so a re-drive waking mid-submission sees the promise already gone
            // whichever side of the state change it looks at.
            SupersedePendingAutoContinue();

            try
            {
                // Re-arm pre-conversation recovery before the first await — from here until
                // IncompleteMessageStarted a domain reload would otherwise lose the prompt. The Unity
                // submit site (AssistantUIAPIInterpreter.SendPrompt) already armed it synchronously in
                // the submit frame; this is an idempotent re-arm that also covers callers reaching
                // ProcessPrompt directly. Inside the try so that a throw while arming (context
                // serialization touches the asset db) cannot leave a half-written marker behind.
                SetPendingPromptRecovery(prompt);

                // Warm up ScriptableSingleton from main thread, or it
                // will throw exceptions later when we access it, and it initializes itself from a thread later on:
                var _ = AssistantEnvironment.WebSocketApiUrl;

                // It's possible that here the conversationId won't be valid because this is a new prompt. It doesn't
                // matter. The current prompt needs to be considered connecting the moment that processing begins to start
                // connecting it to the backend. Otherwise, there are timing gaps where features don't work, because they
                // are not aware that a prompt has started processing.
                ChangePromptState(
                    conversationId,
                    PromptState.Connecting,
                    "Connecting");

                var promptContext = new PromptContext { Credentials = await CredentialsProvider.GetCredentialsContext(ct) };
                TracesUploader.CacheCredentials(promptContext.Credentials);

                // Prepare serialized context, this needs to be on the main thread for asset db checks:
                promptContext.Asset = ContextSerializationHelper
                    .BuildPromptSelectionContext(prompt.ObjectAttachments, prompt.VirtualAttachments, prompt.ConsoleAttachments).m_ContextList
                    .ToArray();

                // Ensure the prompt adheres to the size constraints
                if (prompt.Value.Length > AssistantMessageSizeConstraints.PromptLimit)
                {
                    prompt.Value = prompt.Value.Substring(0, AssistantMessageSizeConstraints.PromptLimit);
                }

                var attachedContext = PromptUtils.GetContextModel(AssistantMessageSizeConstraints.ContextLimit, prompt);
                promptContext.Attached = OrchestrationDataUtilities.FromEditorContextReport(attachedContext);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                // ProcessPromptInternal owns the marker from here (it clears on every early-out
                // and at the IncompleteMessageStarted handoff), so its *failures* must release it
                // too — otherwise a faulted turn leaves the marker armed for the rest of the
                // editor session and the next unrelated domain reload replays a dead prompt.
                TaskUtils.WithExceptionLogging(
                    () => ProcessPromptInternal(conversationId, prompt, promptContext, agent, ct, isAutoContinue),
                    processPromptException => ClearPendingPromptRecovery());
#pragma warning restore CS4014
            }
            catch
            {
                // Thrown before ProcessPromptInternal took ownership (cancelled token, credential
                // failure, context serialization). WithExceptionLogging around this call only logs,
                // so without this the marker would outlive the prompt it describes.
                ClearPendingPromptRecovery();
                throw;
            }
        }

        public async Task RevertMessage(AssistantMessageId messageId)
        {
            if (messageId.ConversationId.IsValid)
            {
                var workflow = Backend.GetOrCreateWorkflow(await CredentialsProvider.GetCredentialsContext(), FunctionCaller, messageId.ConversationId);

                if (workflow != null)
                {
                    // Wait for the discussion to be initialized before sending the revert request
                    // This ensures the relay has established the cloud backend session
                    InternalLog.Log("[RevertMessage] Waiting for discussion initialization before sending revert request");
                    var isInitialized = await workflow.AwaitDiscussionInitialization();
                    if (!isInitialized)
                    {
                        InternalLog.LogError($"[RevertMessage] Failed to initialize workflow. {workflow.CloseReason}");
                        return;
                    }

                    workflow.RevertMessageRequest(messageId.FragmentId);
                }
            }
        }

        // Report a terminal from a ProcessPromptInternal early-out — before the
        // WorkflowEventHandler (and its ReportTurnEnded) exists. Mirrors that handler: fire the
        // gateway analytic ALWAYS, and the consumer event only when a conversation id exists.
        // No once-latch (these early-outs are mutually exclusive returns) and no main-thread
        // dispatch (this post-await continuation already runs on the main thread, like the
        // adjacent CapacityReached/ConversationErrorOccured/ChangePromptState calls).
        void ReportEarlyTurnEnd(AssistantConversationId conversationId, string outcome, string failureReason)
        {
            var idValue = conversationId.IsValid ? conversationId.Value : null;
            AIAssistantAnalytics.ReportGatewayTurnEndedEvent(idValue, outcome, failureReason);
            if (idValue != null)
                TurnEnded?.Invoke(new AssistantConversationId(idValue), outcome, failureReason);
        }

        async Task ProcessPromptInternal(
            AssistantConversationId conversationId,
            AssistantPrompt prompt,
            PromptContext promptContext,
            IAgent agent = null,
            CancellationToken ct = default,
            bool isAutoContinue = false)
        {
            m_ConnectionCancelToken = new();
            var connectionCancelToken = m_ConnectionCancelToken.Token;

            // get the appropriate workflow
            var isNewConversation = !conversationId.IsValid;

            var workflow = Backend.GetOrCreateWorkflow(promptContext.Credentials, FunctionCaller, conversationId);

            await workflow.AwaitDiscussionInitialization();

            // If the user has cancelled the prompt, then treat this as an early-out
            if (CurrentPromptState == PromptState.Canceling)
            {
                InternalLog.LogWarning("ProcessPrompt: Early out due to user cancellation");
                ClearPendingPromptRecovery();
                ReportEarlyTurnEnd(conversationId, "cancelled", null);
                return;
            }

            // A capacity close routes to the capacity-fallback UI (banner + provider switch), matching HandleClose,
            // rather than the generic error below.
            if (workflow.WorkflowState == State.Closed
                && workflow.CloseReason.Reason == CloseReason.ReasonType.ServerNoCapacity)
            {
                CapacityReached?.Invoke(conversationId);
                ChangePromptState(conversationId, PromptState.NotConnected, "The AI Assistant server is at capacity.");
                ClearPendingPromptRecovery();
                ReportEarlyTurnEnd(conversationId, "error", "no_capacity");
                return;
            }

            // Pre-init there is no conversation yet, so any close other than an intentional client cancel means the
            // prompt never reached the server — surface it to recover the pending prompt. (Unlike HandleClose, which
            // treats graceful/informational closes as benign because it runs once a conversation exists.)
            if (workflow.WorkflowState == State.Closed
                && workflow.CloseReason.Reason != CloseReason.ReasonType.ClientCanceled)
            {
                ConversationErrorOccured?.Invoke(conversationId, new($"We were unable to establish communication with the AI Assistant server. {ErrorHandlingUtility.ErrorMessageNetworkedSuffix}", workflow.CloseReason.ToString()));
                ChangePromptState(conversationId, PromptState.NotConnected, "Unable to establish communication with the AI Assistant server.");
                ClearPendingPromptRecovery();

                // A failed (re)connect returns here BEFORE any WorkflowEventHandler is subscribed, so
                // HandleClose can never escalate the auto-continue for it. Without this, an auto-continue
                // re-drive that can't reconnect — the expected outcome a few seconds into a 1-2 minute
                // gateway reconcile window — strands the turn with the attempt counter stuck at 1, and the
                // 20s/60s attempts never happen.
                //
                // Gate on isAutoContinue: this branch also runs for a FRESH user prompt whose initial
                // connect merely blipped, and a transport-class close there must NOT arm a re-drive. With a
                // prior completed turn the conversation still has LastPromptMode recorded and the budget
                // reset, so an ungated escalation on e.g. a CouldNotConnect would supersede the user's real
                // request with the generic continuation text ("...continue and finish the remaining work")
                // and run an unsolicited agent turn. Only escalate when this invocation is itself the
                // re-drive continuing an already-in-flight turn. auth/unknown are still excluded by the
                // predicate; the budget check inside bounds the loop, and a Connecting-state or
                // busy-workflow re-drive stands itself down.
                if (isAutoContinue && IsResumableMidTurnClose(workflow.CloseReason.Reason))
                    TryAutoContinueAfterConnectionLoss(conversationId);
                // Report this attempt's terminal; an armed auto-continue re-drive is a fresh attempt
                // that will report its own outcome (a later "completed" supersedes this for consumers).
                ReportEarlyTurnEnd(conversationId, "error", WorkflowEventHandler.MapCloseReasonToFailureSubCode(workflow.CloseReason));
                return;
            }

            if (workflow.IsCancelled)
            {
                InternalLog.Log("ProcessPrompt: Early out due to workflow cancellation");
                ClearPendingPromptRecovery();
                ReportEarlyTurnEnd(conversationId, "cancelled", null);
                return;
            }

            ChangePromptState(
                new AssistantConversationId(workflow.ConversationId),
                PromptState.Connected,
                "Connected");

            InternalLog.LogToFile(
                workflow.ConversationId,
                ("event", "processing prompt"),
                ("env", AssistantEnvironment.ApiUrl)
            );

            // Create the objects used by the UI code to render the conversation
            conversationId = new AssistantConversationId(workflow.ConversationId);

            if (!m_ConversationCache.TryGetValue(conversationId, out var conversation))
            {
                conversation = new AssistantConversation
                {
                    Title = AssistantConstants.DefaultConversationTitle,
                    Id = conversationId
                };

                m_ConversationCache.Add(conversationId, conversation);
            }

            // We should probably remove the need for the frontend to control this altogether, but as of right now
            // the frontend indicates when the title should be generated. It makes most sense to do this immediately
            // when the conversation id is available. This will result in eventually getting a title on the frontend.
            MainThread.DispatchAndForgetAsync(async () =>
            {
                var result = await Backend.ConversationGenerateTitle(
                    await CredentialsProvider.GetCredentialsContext(connectionCancelToken),
                    workflow.ConversationId, connectionCancelToken);

                if (!connectionCancelToken.IsCancellationRequested && result.Status == BackendResult.ResultStatus.Success && conversation != null)
                {
                    conversation.Title = result.Value;
                    NotifyConversationChange(conversation);
                }
            });

            // Add the messages needed to start rendering the response
            var promptMessage = AddInternalMessage(conversation, prompt.Value, role: k_UserRole, sendUpdate: true);
            promptMessage.Context = promptContext.Asset;

            var assistantMessage = AddIncompleteMessage(conversation, string.Empty, k_AssistantRole, sendUpdate: true);

            // Create checkpoint before any tool operations (if enabled)
            // Store as pending - will be tagged with real fragment ID when server responds
            if (AssistantProjectPreferences.CheckpointEnabled && AssistantCheckpoints.IsInitialized)
            {
                try
                {
                    var checkpointMessage = $"Before prompt: {TruncateForCheckpoint(prompt.Value)}";
                    var checkpointResult = await AssistantCheckpoints.CreateCheckpointAsync(checkpointMessage);
                    if (checkpointResult.Success)
                    {
                        AssistantCheckpoints.SetPendingCheckpoint(
                            assistantMessage.Id.ConversationId,
                            assistantMessage.Id.FragmentId,
                            checkpointResult.Value);
                    }
                }
                catch (Exception ex)
                {
                    InternalLog.LogWarning($"[Checkpoint] Failed to create checkpoint: {ex.Message}");
                }
            }

            // Track incomplete message for domain reload recovery
            IncompleteMessageStarted?.Invoke(conversationId, assistantMessage.Id.FragmentId);

            // Record the handoff synchronously, before either callback the line above queued can run.
            // The conversation + incomplete message now exist, so the IncompleteMessageId path owns
            // reload survival from here and the pre-conversation marker must never re-drive this turn
            // again. Writing the flag inline (not on the dispatch queue) is what guarantees that even
            // a reload which discards *both* queued callbacks — the IncompleteMessageId write and the
            // marker release below — cannot re-send: TryGetPendingPromptRecovery sees the flag and
            // reports no lost prompt, so the turn degrades to an unrecoverable incomplete message
            // rather than a second send that re-applies edits and bills another turn.
            SessionState.SetString(k_PendingPromptHandoffKey, k_DomainId);

            // Release the rest of the pre-conversation marker. Dispatched rather than cleared inline
            // because the IncompleteMessageId write this hands off to is itself queued:
            // OnIncompleteMessageStarted wraps it in MainThread.DispatchAndForget, which always
            // enqueues (never runs inline, even on the main thread). Queuing behind it means this clear
            // runs after that write, never before it — so a normal (non-reload) turn ends with both the
            // handoff flag and the marker keys erased. With the synchronous flag above already vetoing
            // any re-drive, this queued clear now only tidies SessionState up.
            //
            // The id write must also actually land: AssistantBlackboard.SetIncompleteMessageId returns
            // early while state saving is suspended (AssistantView.InitializeView until the end of
            // RestoreUIState, a window that can span frames), which would drop it. OnIncompleteMessageStarted
            // therefore persists the id directly when the blackboard helper no-ops — see the read-back there.
            MainThread.DispatchAndForget(ClearPendingPromptRecovery);

            // Remember the turn's mode and model tier for the connection-loss auto-continue
            // (SessionState: must survive the domain reloads routine mid-turn).
            RecordLastPromptMode(conversation.Id, prompt.Mode);
            RecordLastPromptModel(conversation.Id, prompt.ModelConfiguration?.Name);

            ToolInteractionAndPermissionBridge.ResetIgnoredObjects();
            if (isNewConversation)
            {
                ToolInteractionAndPermissionBridge.ResetTemporaryPermissions();
                ConversationCreated?.Invoke(conversation);
            }

            // Setup event handler
            StringBuilder assistantResponseStringBuilder = new();
            var eventHandler = new WorkflowEventHandler(
                workflow,
                conversation,
                assistantMessage,
                assistantResponseStringBuilder,
                connectionCancelToken,
                isNewConversation,
                ChangePromptState,
                ConversationErrorOccured,
                CapacityReached,
                NotifyConversationChange,
                (convId) => IncompleteMessageCompleted?.Invoke(convId),
                TryAutoContinueAfterConnectionLoss,
                (convId, outcome, reason) => TurnEnded?.Invoke(convId, outcome, reason));

            eventHandler.Subscribe();

            var originalPrompt = prompt.Value;
            var contextAnalyticsCache = prompt.ContextAnalyticsCache;
            var promptMode = prompt.Mode;

            workflow.OnAcknowledgeChat -= HandleChatAcknowledgment;
            workflow.OnAcknowledgeChat += HandleChatAcknowledgment;

            // Stamp t0 for client-side TTFT (prompt sent → first fragment arrival). Must happen
            // synchronously before the await so the timestamp reflects the actual send moment.
            eventHandler.SetPromptSentAt(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            await TaskUtils.WithExceptionLogging(() => workflow.SendChatRequest(prompt.Value, promptContext.Attached, agent, prompt.Mode, prompt.ModelConfiguration, ct));

            return;

            void HandleChatAcknowledgment(AcknowledgePromptInfo info)
            {
                workflow.OnAcknowledgeChat -= HandleChatAcknowledgment;

                promptMessage.Id = new AssistantMessageId(conversation.Id, info.Id, AssistantMessageIdType.External);
                promptMessage.Context = MergeContext(promptMessage.Context, info.Context);

                if (promptMessage.Blocks.Count != 1)
                    throw new Exception("Prompt message is expected to have a single block");

                if (promptMessage.Blocks[^1] is not PromptBlock promptBlock)
                    throw new Exception("Last block in prompt message is not a prompt block and should be during acknowledgment.");

                promptBlock.Content = info.Content;
                NotifyConversationChange(conversation);

                PendingCostUserMessageId = promptMessage.Id;

                // Report send event and flush all pending context attach events with the real backend message ID
                MainThread.DispatchAndForget(() =>
                {
                    contextAnalyticsCache?.FlushAll(promptMessage.Id);
                    AIAssistantAnalytics.ReportUserMessageSentEvent(originalPrompt, promptMessage.Id, promptMode);
                });
            }

            static AssistantContextEntry[] MergeContext(AssistantContextEntry[] localContext, AssistantContextEntry[] ackContext)
            {
                if ((localContext == null || localContext.Length == 0) &&
                    (ackContext == null || ackContext.Length == 0))
                    return Array.Empty<AssistantContextEntry>();

                if (localContext == null || localContext.Length == 0)
                    return ackContext;

                if (ackContext == null || ackContext.Length == 0)
                    return localContext;

                var merged = ackContext.ToList();
                foreach (var localEntry in localContext)
                {
                    if (!merged.Contains(localEntry))
                        merged.Add(localEntry);
                }

                return merged.ToArray();
            }
        }

        /// <summary>
        /// Resume an incomplete message after domain reload. Handles lifecycle of replayed and new streaming messages.
        /// </summary>
        void ResumeIncompleteMessage(
            IChatWorkflow workflow,
            AssistantConversation conversation,
            AssistantMessage assistantMessage,
            CancellationToken ct = default)
        {
            InternalLog.LogToFile(conversation.Id.ToString(), ("event", "resuming incomplete message"), ("blocks", assistantMessage.Blocks.Count.ToString()), ("isComplete", assistantMessage.IsComplete.ToString()));

            m_ConnectionCancelToken = new();
            var connectionCancelToken = m_ConnectionCancelToken.Token;

            var content = string.Empty;
            if (assistantMessage.Blocks.Count > 0 && assistantMessage.Blocks[^1] is AnswerBlock { IsComplete: false } responseBlock)
                content  = responseBlock.Content;

            // Initialize StringBuilder with existing content
            StringBuilder assistantResponseStringBuilder = new(content);

            // Setup event handler (no new conversation, no credentials for title generation)
            var eventHandler = new WorkflowEventHandler(
                workflow,
                conversation,
                assistantMessage,
                assistantResponseStringBuilder,
                connectionCancelToken,
                isNewConversation: false, // Resume means conversation already exists
                ChangePromptState,
                ConversationErrorOccured,
                CapacityReached,
                NotifyConversationChange,
                (convId) => IncompleteMessageCompleted?.Invoke(convId),
                TryAutoContinueAfterConnectionLoss,
                (convId, outcome, reason) => TurnEnded?.Invoke(convId, outcome, reason));

            eventHandler.Subscribe();

            // Don't send any request - just listen for replayed/streamed messages
        }

        static string TruncateForCheckpoint(string text, int maxLength = 50)
        {
            if (string.IsNullOrEmpty(text)) return "(empty)";
            text = text.Replace("\n", " ").Replace("\r", "");
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }
    }
}
