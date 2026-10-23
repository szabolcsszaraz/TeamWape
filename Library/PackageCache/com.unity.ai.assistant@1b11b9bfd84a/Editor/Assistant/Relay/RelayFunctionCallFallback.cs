using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Socket.Protocol.Models;
using Unity.AI.Assistant.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Socket.Protocol.Models.FromServer;
using Unity.AI.Assistant.Socket.Workflows.Chat;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Unity.Relay.Editor
{
    /// <summary>
    /// Runs a <c>FUNCTION_CALL_REQUEST_V1</c> that arrives from the relay with no chat workflow to
    /// consume it, and answers the relay directly.
    /// <para>
    /// The only path from <see cref="WebSocketRelayClient.OnAssistantMessage"/> to the function
    /// caller is a chat workflow's <c>RelayWebSocketAdapter</c>, and that adapter is subscribed only
    /// while a workflow is attached to <em>this</em> client. After a domain reload the editor can
    /// end up connected on a fresh client with nothing subscribed to it — the workflow that recovery
    /// created is holding the previous, dead client. The relay's auto-recovery net then redelivers
    /// the pending call, the send succeeds at the socket level, and the message is dropped inside
    /// the editor without a trace; the cloud times the turn out ~600s later. That is exactly what
    /// happened in muse-editor#2705, twice in the same session.
    /// </para>
    /// <para>
    /// This subscriber is attached to every client <see cref="RelayService"/> creates, so a pending
    /// call always has a consumer of last resort. Function execution itself is stateless — the
    /// request carries everything the tool needs — so a call can be run and answered without a chat
    /// session behind it.
    /// </para>
    /// <para>
    /// It is deliberately a plain listener rather than a workflow: it never handshakes, never
    /// becomes the backend's active workflow, and never touches the protocol state machine.
    /// </para>
    /// </summary>
    static class RelayFunctionCallFallback
    {
        /// <summary>
        /// How long a call is left for a chat workflow to claim before the fallback takes it.
        /// <para>
        /// A workflow attached to this client claims the call synchronously, inside the same
        /// <see cref="WebSocketRelayClient.OnAssistantMessage"/> invocation, so on the healthy path
        /// the grace has already elapsed in spirit before it starts. The wait covers the case where
        /// a workflow is mid-attach (recovery racing the relay's replay) and would claim the call a
        /// moment later. It also has to be comfortably shorter than the relay's own delivery check
        /// (<c>AUTO_RECOVERY_VERIFY_MS</c>, 30s) so that a call the fallback answers is no longer
        /// pending by the time the relay looks.
        /// </para>
        /// </summary>
        internal const int WorkflowGraceMs = 10000;

        /// <summary>
        /// SessionState key holding the conversation the UI last had open. Duplicated from
        /// <c>AssistantUISessionState.LastActiveConversationId</c>, which lives in the UI assembly
        /// this one cannot reference (<c>AcpProvider</c> reads it the same way).
        /// </summary>
        const string k_LastActiveConversationIdKey = "AssistantUserSession_LastActiveConversationId";

        /// <summary>
        /// The function caller the fallback runs calls through, published by <c>Assistant</c> when it
        /// configures itself.
        /// <para>
        /// It is deliberately the very same <see cref="IFunctionCaller"/> the chat workflow would
        /// have used, so a fallback execution goes through the identical permission path: the same
        /// <c>IToolPermissions</c>, the same policy provider, the same persisted allow/deny state,
        /// and the same approval prompt for a tool whose policy is <c>Ask</c>. The fallback grants
        /// nothing on its own, and when no caller has been published it refuses to run rather than
        /// substituting a permissive one.
        /// </para>
        /// </summary>
        internal static IFunctionCaller FunctionCaller { get; set; }

        /// <summary>
        /// Subscribes the fallback to a newly created relay client. Called for every client, so the
        /// coverage does not depend on whether a workflow ever attaches to it.
        /// </summary>
        internal static void Attach(WebSocketRelayClient client)
        {
            if (client == null)
                return;

            client.OnAssistantMessage += message => HandleAssistantMessage(client, message);
        }

        /// <summary>
        /// Runs on the relay client's receive thread for every assistant message. Returns
        /// immediately for anything that is not a function call request.
        /// </summary>
        static void HandleAssistantMessage(WebSocketRelayClient client, string messageText)
        {
            // Cheap reject first: this runs for every message on the socket, including every chat
            // response fragment, and only function call requests are of any interest here.
            if (string.IsNullOrEmpty(messageText) || !messageText.Contains(FunctionCallRequestV1.Type))
                return;

            FunctionCallRequestV1 request = null;
            try
            {
                request = AssistantJsonHelper.Deserialize<IModel>(messageText, new ServerMessageJsonConverter())
                    as FunctionCallRequestV1;
            }
            catch (Exception)
            {
                // A message this client cannot parse is the attached workflow's problem to report —
                // it disconnects with ServerSentUnknownMessage. The fallback only acts on calls it
                // can positively identify.
                return;
            }

            // Guid.Empty is not a call id the backend can produce, and it cannot be coordinated
            // through the dispatch registry, so there is no way to guarantee the tool would not also
            // run somewhere else. Leave it to whoever else received it.
            if (request == null || request.CallId == Guid.Empty)
                return;

            _ = ClaimAfterGraceAsync(client, request);
        }

        static async Task ClaimAfterGraceAsync(WebSocketRelayClient client, FunctionCallRequestV1 request)
        {
            try
            {
                // Give a chat workflow attached to this client the chance to claim (and answer) the
                // call first; Execute decides what remains to be done once the grace has elapsed.
                await Task.Delay(WorkflowGraceMs).ConfigureAwait(false);

                MainThread.DispatchAndForget(() => Execute(client, request));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// The single entry point for handling a redelivered call after its grace has elapsed:
        /// re-send a cached result, stay out of a still-running claim, or run the call standalone.
        /// <para>
        /// Internal so tests can drive it directly — <see cref="WebSocketRelayClient.OnAssistantMessage"/>
        /// can only be raised inside the client, so the public receive path is not reachable from a
        /// test. Runs on the main thread.
        /// </para>
        /// </summary>
        internal static void Execute(WebSocketRelayClient client, FunctionCallRequestV1 request)
        {
            // Already ran, but its response never reached the relay (the owning workflow closed
            // mid-flight, or a send was rejected) so the call is still pending. Re-send the cached
            // result rather than running the tool again — the claim is never released, so this is the
            // only way a redelivery can answer it (muse-editor#2705).
            if (FunctionCallDispatchRegistry.TryGetResult(request.CallId, out var cachedResult))
            {
                ResendCachedResult(client, request, cachedResult);
                return;
            }

            // Claimed but not answered yet: the owner is still executing it. The existence of a
            // workflow object proves nothing here — in muse-editor#2705 one existed the whole time,
            // bound to a dead client — but a claim does, so stay out of its way.
            if (FunctionCallDispatchRegistry.IsClaimed(request.CallId))
                return;

            var caller = FunctionCaller;
            if (caller == null)
            {
                Debug.LogWarning(
                    $"[RelayFunctionCallFallback] Function call {request.CallId} ({request.FunctionId}) was delivered " +
                    "with no chat workflow to run it and no function caller registered, so it cannot be answered " +
                    "without bypassing the tool permission path. The cloud will time this turn out (muse-editor#2705).");
                return;
            }

            if (!client.IsConnected)
            {
                Debug.LogWarning(
                    $"[RelayFunctionCallFallback] Function call {request.CallId} ({request.FunctionId}) was not consumed " +
                    "by any chat workflow, and the relay client it arrived on has since disconnected — there is nowhere " +
                    "to send the response, so it is not being executed (muse-editor#2705).");
                return;
            }

            // Claim before executing. Losing the race means a workflow attached during the grace and
            // took the call after the checks above.
            if (!FunctionCallDispatchRegistry.TryClaim(request.CallId))
                return;

            var conversationId = ResolveConversationId();

            Debug.LogWarning(
                $"[RelayFunctionCallFallback] No chat workflow consumed function call {request.CallId} " +
                $"({request.FunctionId}) within {WorkflowGraceMs}ms — executing it standalone and answering the relay " +
                "directly. This is the recovery path for a redelivered call after a domain reload (muse-editor#2705).");

            InternalLog.LogToFile(
                conversationId,
                ("event", "standalone function call fallback"),
                ("call_id", request.CallId.ToString()),
                ("function_id", request.FunctionId));

            var responder = new StandaloneCallResponder(client, conversationId, caller);
            caller.CallByLLM(responder, request.FunctionId, request.FunctionParameters, request.CallId, CancellationToken.None);
        }

        /// <summary>
        /// Delivers, on the live relay client, a function-call response whose owning workflow could
        /// not send because it had already closed — the window closed mid-tool and
        /// <c>RelayChatWorkflow</c> returned from its <c>State.Closed</c> send path after recording
        /// the result (muse-editor#2705).
        /// <para>
        /// The relay client is a <see cref="RelayService"/> singleton that outlives any single
        /// workflow and is disposed only at <c>EditorApplication.quitting</c>, so closing the window
        /// reconnects nothing: neither redelivery trigger — the relay's auto-recovery grace timer,
        /// armed on client connect, nor a <c>RELAY_RECOVER_MESSAGES</c> — ever fires, and the recorded
        /// result is never consulted, so the turn would time out ~600s later. Re-sending the recorded
        /// result on the still-live client here answers the pending call immediately, exactly as
        /// <see cref="ResendCachedResult"/> does on a redelivery. The recorded result stays the
        /// backstop for the genuine reconnect case (a domain reload, which does replace the client and
        /// replay the pending calls).
        /// </para>
        /// <para>
        /// The result is read back from <see cref="FunctionCallDispatchRegistry.TryGetResult"/> rather
        /// than the passed response, so the same single conversion is used and only a call the registry
        /// still holds as pending is ever re-sent. Internal so a test can drive it with a mock client;
        /// runs on the main thread.
        /// </para>
        /// </summary>
        /// <returns>
        /// True when the recorded result was handed to a connected client; false when the message is
        /// not a function-call response, nothing is recorded for the call, or there is no connected
        /// client to answer on — in which case the recorded result (if any) is the only recourse until
        /// a reconnect.
        /// </returns>
        internal static bool TryDeliverRecordedResponseOnLiveClient(WebSocketRelayClient client, object message)
        {
            if (message is not FunctionCallResponseV1 response)
                return false;

            if (!FunctionCallDispatchRegistry.TryGetResult(response.CallId, out var result))
                return false;

            if (client == null || !client.IsConnected)
                return false;

            var conversationId = ResolveConversationId();

            Debug.LogWarning(
                $"[RelayFunctionCallFallback] Delivering the response for function call {response.CallId} on the live " +
                "relay client because its workflow closed before the send completed (window closed mid-tool); a window " +
                "close reconnects nothing, so no redelivery would ever answer it from cache (muse-editor#2705).");

            InternalLog.LogToFile(
                conversationId,
                ("event", "closed-workflow function call response delivered on live client"),
                ("call_id", response.CallId.ToString()));

            var responder = new StandaloneCallResponder(client, conversationId, FunctionCaller);
            responder.SendFunctionCallResponse(result, response.CallId);
            return true;
        }

        /// <summary>
        /// Re-sends the cached response for a claimed call whose original delivery was dropped, so
        /// the still-pending call is answered without re-running the tool. The caller is not needed
        /// here — only the socket — so this works even after the window that published it has closed.
        /// </summary>
        static void ResendCachedResult(WebSocketRelayClient client, FunctionCallRequestV1 request, FunctionCallResult result)
        {
            if (!client.IsConnected)
            {
                Debug.LogWarning(
                    $"[RelayFunctionCallFallback] Function call {request.CallId} ({request.FunctionId}) already ran but its " +
                    "response was dropped, and the relay client it was redelivered on has since disconnected — the cached " +
                    "result cannot be re-sent (muse-editor#2705).");
                return;
            }

            var conversationId = ResolveConversationId();

            Debug.LogWarning(
                $"[RelayFunctionCallFallback] Re-sending the cached response for redelivered function call {request.CallId} " +
                $"({request.FunctionId}); it already ran but its first response was not delivered (muse-editor#2705).");

            InternalLog.LogToFile(
                conversationId,
                ("event", "standalone function call response resend"),
                ("call_id", request.CallId.ToString()),
                ("function_id", request.FunctionId));

            var responder = new StandaloneCallResponder(client, conversationId, FunctionCaller);
            responder.SendFunctionCallResponse(result, request.CallId);
        }

        /// <summary>
        /// Best-effort conversation id for the executing call. The request itself does not carry one,
        /// and after a domain reload there is no live workflow to ask, so this falls back to the
        /// conversation the UI last had open — SessionState survives the reload. It only scopes the
        /// tool's <c>PersistentStorage</c> and the trace file; a null id costs neither correctness
        /// nor the response.
        /// </summary>
        static string ResolveConversationId()
        {
            var conversationId = SessionState.GetString(k_LastActiveConversationIdKey, null);
            return string.IsNullOrEmpty(conversationId) ? null : conversationId;
        }

        /// <summary>
        /// Carries the result of a standalone execution back to the relay.
        /// <para>
        /// <see cref="IFunctionCaller.CallByLLM"/> reports through an <see cref="IChatWorkflow"/>, so
        /// the fallback needs one to hand it. Deriving from <see cref="BaseChatWorkflow"/> rather
        /// than reimplementing the interface keeps the <c>FUNCTION_CALL_RESPONSE_V1</c> the fallback
        /// sends identical to the one a real workflow sends — one definition, no second copy to drift.
        /// </para>
        /// <para>
        /// This is not a chat session: it is never started, never registered as the backend's active
        /// workflow, and never subscribes to the transport, so it neither receives messages nor
        /// competes with a real workflow. Its only job is the one send.
        /// </para>
        /// </summary>
        sealed class StandaloneCallResponder : BaseChatWorkflow
        {
            readonly WebSocketRelayClient m_Client;

            internal StandaloneCallResponder(WebSocketRelayClient client, string conversationId, IFunctionCaller functionCaller)
                : base(conversationId, functionCaller)
            {
                m_Client = client;
            }

            protected override Task StartConnectionInternal(ICredentialsContext credentialsContext, bool skipInitialization)
                => throw new NotSupportedException(
                    "The standalone function-call responder never opens a chat session; it only sends a response.");

            protected override async Task SendMessageInternal(object message, CancellationToken cancellationToken)
            {
                if (message is not IModel model)
                {
                    Debug.LogError(
                        "[RelayFunctionCallFallback] Cannot send a message that is not an IModel " +
                        $"(actual type: {message?.GetType().FullName ?? "<null>"}); it will be dropped.");
                    return;
                }

                var sent = await m_Client.SendRawMessageAsync(AssistantJsonHelper.Serialize(model), cancellationToken);
                if (!sent)
                {
                    // The call ran but the relay rejected its response, so it stays pending.
                    // Remember the result so a redelivery re-sends it instead of re-running the tool.
                    RememberUndeliveredResponse(model);
                    Debug.LogWarning(
                        $"[RelayFunctionCallFallback] The relay did not accept the {model.GetModelType()} for the " +
                        "standalone function call; the cloud will time this turn out (muse-editor#2705).");
                }
            }

            // The relay client belongs to RelayService and outlives this responder.
            protected override void DisposeTransport() { }

            // Nothing to subscribe to: this responder is write-only by construction.
            protected override void SubscribeToTransportEvents() { }

            protected override void UnsubscribeFromTransportEvents() { }
        }
    }
}
