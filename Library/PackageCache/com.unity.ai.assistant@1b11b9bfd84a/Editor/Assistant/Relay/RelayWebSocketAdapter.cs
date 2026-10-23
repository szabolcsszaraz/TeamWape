using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Socket.Communication;
using Unity.AI.Assistant.Socket.Protocol.Models;
using Unity.AI.Assistant.Utils;
using Unity.Relay;
using Unity.Relay.Editor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.RelayClient
{
    /// <summary>
    /// Adapter that makes WebSocketRelayClient compatible with IOrchestrationWebSocket interface.
    /// This allows ChatWorkflow to work with relay connections without code duplication.
    /// </summary>
    class RelayWebSocketAdapter : IOrchestrationWebSocket
    {
        public event Action<ReceiveResult> OnMessageReceived;
        public event Action<WebSocketCloseStatus?, string> OnClose;
        public event Action OnReplayComplete;

        readonly IRelayConnection m_RelayConnection;
        // volatile: written on the RelayService.Connected threadpool thread (OnRelayReconnected)
        // and the client listen thread (HandleDisconnected), read on the main thread
        // (IsConnected/Send/StartCloudSession). volatile gives those reads visibility of the latest
        // write; read-then-use callers ALSO snapshot m_RelayClient into a local so a concurrent swap
        // cannot split their guard and their use across two different clients.
        volatile WebSocketRelayClient m_RelayClient;
        volatile bool m_IsConnected;
        bool m_Disposed;

        // Set only when this adapter fetched its own client from RelayService (the production
        // path). It then follows RelayService across a client swap; when a caller injects an
        // explicit client (tests) we leave RelayService's lifecycle untouched.
        bool m_FollowingReconnects;

        // OnRelayReconnected and Dispose run on DIFFERENT threads and race:
        //  - OnRelayReconnected fires from RelayService.TransitionTo(Running) -> Connected, which
        //    is raised after `await ConnectAsync().ConfigureAwait(false)` and is NOT marshalled to
        //    the main thread (unlike StateChanged), so it runs on a threadpool thread.
        //  - Dispose runs from the client listen thread: the swap disposes the old client, whose
        //    OnDisconnected fires -> HandleDisconnected -> OnClose -> RelayChatWorkflow teardown ->
        //    Dispose().
        // Serialize the two so the disposed-check is atomic with the swap; without it a reconnect
        // that passes the `m_Disposed` check just before Dispose completes would re-subscribe a live
        // client onto a torn-down workflow (zombie delivery) and leak the client's handlers forever.
        readonly object m_SwapLock = new object();

        /// <summary>
        /// True when both the relay-side adapter state and the underlying relay client report
        /// connected. Mirrors the precondition checks used internally by Send/StartCloudSession
        /// so callers (e.g. RelayChatWorkflow.SendMessageInternal) can refuse a send before
        /// invoking the adapter.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                var client = m_RelayClient;
                return m_IsConnected && client?.IsConnected == true;
            }
        }

        public RelayWebSocketAdapter(WebSocketRelayClient relayClient = null)
            : this(RelayService.Instance, relayClient) { }

        // Internal seam mirroring AcpClient(IRelayConnection): lets tests drive client swaps
        // through a MockRelayConnection without a live RelayService.
        internal RelayWebSocketAdapter(IRelayConnection relayConnection, WebSocketRelayClient relayClient = null)
        {
            m_RelayConnection = relayConnection;
            m_RelayClient = relayClient;
        }

        public async Task<ConnectResult> Connect(IOrchestrationWebSocket.Options options, CancellationToken ct)
        {
            try
            {
                // Use RelayService.GetClientAsync() which blocks until ready or throws on failure
                if (m_RelayClient == null)
                {
                    InternalLog.Log("[RelayWebSocketAdapter] Waiting for relay connection...");

                    // A reconnect (bus-validation failure, dropped socket, unresponsive relay) makes
                    // RelayService dispose its old client and swap in a fresh one. This adapter
                    // snapshots a single client and subscribes to it once, so when the workflow ends
                    // up bound to the disposed client the turn's completion frames
                    // (last_message_complete / m_IncompleteMessageCompleted) arrive on the NEW client
                    // and reach nobody — the turn silently stalls to the cloud's timeout (muse-editor
                    // #2705, cases 094/096/100). RelayFunctionCallFallback still answers the
                    // redelivered call so the model keeps producing, which is why the stall is silent.
                    //
                    // Disposing the old client DOES fire its OnDisconnected (WebSocketRelayClient's
                    // listen loop invokes it unconditionally on CTS-cancel), so on such a swap our
                    // HandleDisconnected races RelayService's Connected: if the disconnect wins the
                    // workflow tears down and rebuilds (this follow is then a no-op, guarded in
                    // OnRelayReconnected); if Connected wins — or the disconnect-recovery otherwise
                    // misses the swap, which is the #2705 orphan — following RelayService's live client
                    // re-points us so completion frames keep reaching the session. m_SwapLock makes
                    // both orderings safe. Mirrors AcpClient's IRelayConnection.Connected follow.
                    //
                    // Seam note: the fetch goes through m_RelayConnection (RelayService in production,
                    // the injected mock in tests) for BOTH branches — the already-connected fast path
                    // (.Client) and the not-yet-connected path (GetClientAsync) — so a test driving a
                    // MockRelayConnection covers the full connect flow without the real singleton.
                    m_RelayClient = m_RelayConnection.IsConnected && m_RelayConnection.Client != null
                        ? m_RelayConnection.Client
                        : await m_RelayConnection.GetClientAsync(ct);
                    m_RelayConnection.Connected += OnRelayReconnected;
                    m_FollowingReconnects = true;
                }

                SubscribeToClient(m_RelayClient);

                m_IsConnected = true;

                return new ConnectResult { IsConnectedSuccessfully = true };
            }
            catch (OperationCanceledException)
            {
                return new ConnectResult
                {
                    IsConnectedSuccessfully = false,
                    Exception = new OperationCanceledException("Connection cancelled")
                };
            }
            catch (RelayConnectionException ex)
            {
                InternalLog.LogError($"[RelayWebSocketAdapter] Relay connection failed: {ex.Message}");
                return new ConnectResult
                {
                    IsConnectedSuccessfully = false,
                    Exception = ex
                };
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[RelayWebSocketAdapter] Connection failed: {ex.Message}");
                return new ConnectResult
                {
                    IsConnectedSuccessfully = false,
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Connect for recovery mode - connects to relay but skips cloud session initialization
        /// </summary>
        public async Task<ConnectResult> ConnectForRecovery(CancellationToken ct)
        {
            // For recovery, we only need to connect to relay (no cloud session)
            // The relay will replay cached messages through the normal message pipeline
            return await Connect(new IOrchestrationWebSocket.Options(), ct);
        }

        /// <summary>
        /// Start a cloud session (assumes relay WebSocket is already connected)
        /// </summary>
        public async Task<ConnectResult> StartCloudSession(IOrchestrationWebSocket.Options options, CancellationToken ct)
        {
            try
            {
                if (!IsConnected)
                {
                    return new ConnectResult
                    {
                        IsConnectedSuccessfully = false,
                        Exception = new InvalidOperationException("Must be connected to relay before starting cloud session")
                    };
                }

                InternalLog.Log("[RelayWebSocketAdapter] Starting cloud session...");

                // Send session start message to establish cloud backend connection
                await SendSessionStartMessage(options, ct);

                InternalLog.Log("[RelayWebSocketAdapter] Session start message sent to relay (awaiting cloud connection)");

                return new ConnectResult { IsConnectedSuccessfully = true };
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[RelayWebSocketAdapter] Cloud session start failed: {ex.Message}");
                return new ConnectResult
                {
                    IsConnectedSuccessfully = false,
                    Exception = ex
                };
            }
        }


        public async Task<SendResult> Send(IModel model, CancellationToken ct)
        {
            // Snapshot once so a concurrent client swap cannot let the guard check client A and the
            // send hit client B (fields are volatile; the local pins the target for this call).
            var client = m_RelayClient;
            if (!m_IsConnected || client?.IsConnected != true)
            {
                return new SendResult
                {
                    IsSendSuccessful = false,
                    Exception = new InvalidOperationException("Not connected to relay")
                };
            }

            try
            {
                var json = AssistantJsonHelper.Serialize(model);
                var success = await client.SendRawMessageAsync(json, ct);

                return new SendResult { IsSendSuccessful = success };
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[RelayWebSocketAdapter] Send failed: {ex.Message}");
                return new SendResult
                {
                    IsSendSuccessful = false,
                    Exception = ex
                };
            }
        }

        void HandleAssistantMessage(string messageText)
        {
            InternalLog.LogToFile("recovery", ("event", "adapter_message_received"), ("length", messageText.Length.ToString()));

            var result = new ReceiveResult { RawData = messageText };

            try
            {
                // Use the same converter as OrchestrationWebSocket for AI Assistant protocol messages
                var converter = new ServerMessageJsonConverter();
                result.DeserializedData = AssistantJsonHelper.Deserialize<IModel>(messageText, converter);
                result.IsDeserializedSuccessfully = true;
            }
            catch (Exception e)
            {
                result.IsDeserializedSuccessfully = false;
                result.Exception = e;
                InternalLog.LogError($"[RelayWebSocketAdapter] Deserialization failed: {e.Message}");
            }

            OnMessageReceived?.Invoke(result);
        }

        void HandleDisconnected(WebSocketCloseStatus? closeStatus, string closeDescription)
        {
            m_IsConnected = false;

            // Forward both the close code and the description string so the chat workflow can
            // distinguish e.g. an auth failure (PolicyViolation/1008) from a generic transport drop.
            OnClose?.Invoke(closeStatus, closeDescription);
        }

        void HandleReplayComplete()
        {
            InternalLog.Log("[RelayWebSocketAdapter] Replay complete - forwarding event");
            OnReplayComplete?.Invoke();
        }

        /// <summary>
        /// Re-point this adapter at RelayService's current client after a reconnect swapped it, so
        /// this conversation's completion frames keep reaching the live session instead of the
        /// disposed client (muse-editor #2705). Runs under m_SwapLock because it races Dispose (see
        /// the field comment): if Dispose already ran, the m_Disposed check bails; if we run first we
        /// unsubscribe the old client so a FUTURE OnDisconnected can't tear us down, then bind the new
        /// one. This does NOT close the window on an OnDisconnected already in flight: `-=` cannot
        /// revoke an invocation the listen thread already read, so that interleaving still degrades to
        /// a teardown (HandleDisconnected -> OnClose -> workflow rebuild) — the follow only prevents
        /// disconnects that have not begun dispatching. After binding, it asks the relay to replay
        /// anything cached during the swap gap onto the followed client (see below).
        /// </summary>
        void OnRelayReconnected()
        {
            WebSocketRelayClient followedClient = null;
            lock (m_SwapLock)
            {
                if (m_Disposed)
                    return;

                var freshClient = m_RelayConnection.Client;
                if (freshClient == null || ReferenceEquals(freshClient, m_RelayClient))
                    return; // Connected fired, but our client did not actually change.

                InternalLog.Log("[RelayWebSocketAdapter] RelayService reconnected onto a new client — " +
                    "resubscribing so this conversation's completion events keep reaching the live session.");

                UnsubscribeFromClient(m_RelayClient);
                m_RelayClient = freshClient;
                SubscribeToClient(m_RelayClient);

                // The follow may win the race against the old client's OnDisconnected, so restore the
                // adapter-level flag to match the live client. IsConnected still AND-guards on
                // m_RelayClient.IsConnected.
                m_IsConnected = true;
                followedClient = freshClient;
            }

            // Re-pointing alone does not unblock the relay: its handleClientConnection stays blocked
            // for a turn with cached chat chunks and no pending function call until a
            // RELAY_RECOVER_MESSAGES arrives (it auto-arms replay only when a pending call exists). So
            // ask the new client to replay whatever it cached during the swap gap; without this the
            // completion sits in the relay's block-cache while we sit subscribed to a live-but-silent
            // client. Fire-and-forget outside m_SwapLock (we are on the RelayService.Connected
            // threadpool thread); the bool result is ignored by design (best-effort recovery). Dedup
            // caveat: a redelivered FUNCTION CALL is deduped by the claim registry, but replayed CHAT
            // CHUNKS are not — a rare overlap with RecoverIncompleteMessage's own RELAY_RECOVER_MESSAGES
            // would rely on the relay's replay-once (it should clear the block on
            // RELAY_RECOVER_MESSAGES_COMPLETED), which is not verifiable from this package.
            if (followedClient != null)
                _ = ReplayCachedMessagesAfterSwap(followedClient);
        }

        async Task ReplayCachedMessagesAfterSwap(WebSocketRelayClient client)
        {
            try
            {
                await client.ReplayIncompleteMessageAsync();
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning(
                    $"[RelayWebSocketAdapter] Replay request after client swap failed: {ex.Message}");
            }
        }

        void SubscribeToClient(WebSocketRelayClient client)
        {
            if (client == null)
                return;
            // Idempotent: a repeat Connect (e.g. ConnectForRecovery after Connect on the same client)
            // must not double-subscribe. `-=` before `+=` is a no-op when not already subscribed and
            // collapses a duplicate to a single handler otherwise.
            client.OnAssistantMessage -= HandleAssistantMessage;
            client.OnAssistantMessage += HandleAssistantMessage;
            client.OnDisconnected -= HandleDisconnected;
            client.OnDisconnected += HandleDisconnected;
            client.OnReplayComplete -= HandleReplayComplete;
            client.OnReplayComplete += HandleReplayComplete;
        }

        void UnsubscribeFromClient(WebSocketRelayClient client)
        {
            if (client == null)
                return;
            client.OnAssistantMessage -= HandleAssistantMessage;
            client.OnDisconnected -= HandleDisconnected;
            client.OnReplayComplete -= HandleReplayComplete;
        }

        /// <summary>
        /// Sends the session-start handshake to the relay so it can establish the cloud backend
        /// connection on our behalf.
        /// </summary>
        async Task SendSessionStartMessage(IOrchestrationWebSocket.Options options, CancellationToken ct = default)
        {
            // Retrieve conversation_id if a conversation is already in progress
            var conversationId = string.Empty;
            options.QueryParameters?.TryGetValue("conversation_id", out conversationId);

            var sessionStartMessage = new JObject
            {
                ["type"] = RelayConstants.RELAY_SESSION_START,
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["cloudBackendUri"] = AssistantEnvironment.WebSocketApiUrl,
                ["conversationId"] = conversationId,
                ["credentials"] = new JObject
                {
                    ["headers"] = AssistantJsonHelper.FromObject(options.Headers)
                }
            };

            string messageJson = sessionStartMessage.ToString();
            InternalLog.Log("[RelayWebSocketAdapter] Sending session start message");

            var client = m_RelayClient;
            var sent = await client.SendRawMessageAsync(messageJson, ct);
            if (!sent)
            {
                throw new InvalidOperationException(
                    "Relay did not accept the session-start message (connection lost or send rejected).");
            }

            InternalLog.Log("[RelayWebSocketAdapter] Session start message sent");
        }


        public void Dispose()
        {
            // Under m_SwapLock so the disposed-check and the teardown are atomic against a concurrent
            // OnRelayReconnected (see the field comment) — otherwise a reconnect that already passed
            // its m_Disposed check would re-subscribe a live client after we tore down.
            lock (m_SwapLock)
            {
                if (m_Disposed)
                    return;

                try
                {
                    // Send session end signal
                    if (m_RelayClient?.IsConnected == true)
                    {
                        var sessionEndMessage = new JObject
                        {
                            ["type"] = RelayConstants.RELAY_SESSION_END,
                            ["timestamp"] = DateTime.UtcNow.ToString("O")
                        };

                        _ = m_RelayClient.SendRawMessageAsync(sessionEndMessage.ToString());
                    }

                    // Stop following RelayService's client swaps.
                    if (m_FollowingReconnects)
                    {
                        m_RelayConnection.Connected -= OnRelayReconnected;
                        m_FollowingReconnects = false;
                    }

                    // Unsubscribe from events
                    UnsubscribeFromClient(m_RelayClient);

                    // Don't dispose the relay client itself since it's shared via RelayService
                    m_RelayClient = null;
                    m_IsConnected = false;
                    m_Disposed = true;
                }
                catch (Exception ex)
                {
                    InternalLog.LogError($"[RelayWebSocketAdapter] Error during disposal: {ex.Message}");
                }
            }
        }
    }
}
