using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Socket.Communication;
using Unity.AI.Assistant.Socket.Protocol.Models;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Utils;
using Unity.AI.Assistant.Editor.RelayClient;
using Unity.Relay.Editor;
using TaskUtils = Unity.AI.Assistant.Editor.Utils.TaskUtils;

namespace Unity.AI.Assistant.Socket.Workflows.Chat
{
    /// <summary>
    /// RelayChatWorkflow manages AI Assistant chat communication through WebSocketRelayClient
    /// instead of directly handling WebSocket connections. This eliminates duplication and
    /// ensures consistent connection management across all relay functionality.
    /// </summary>
    class RelayChatWorkflow : BaseChatWorkflow
    {
        RelayWebSocketAdapter m_RelayAdapter;

        public RelayChatWorkflow(string conversationId = null, IFunctionCaller functionCaller = null)
            : base(conversationId, functionCaller)
        {
        }

        /// <summary>
        /// Start the workflow by connecting to the local relay server via RelayWebSocketAdapter
        /// </summary>
        /// <param name="credentialsContext">Credentials for the cloud backend connection</param>
        /// <param name="skipInitialization"></param>
        /// <exception cref="InvalidOperationException">Workflows can only be started once</exception>
        public async Task Start(ICredentialsContext credentialsContext, bool skipInitialization = false)
        {
            try
            {
                await StartConnectionInternal(credentialsContext, skipInitialization);
                MarkStarted();
            }
            catch (Exception ex)
            {
                MarkStartFailed(ex);
                throw;
            }
        }

        protected override async Task StartConnectionInternal(ICredentialsContext credentialsContext, bool skipInitialization)
        {
            InternalLog.Log($"[RelayChatWorkflow] Starting relay workflow (skipInit: {skipInitialization})...");

            if (WorkflowState != State.NotStarted)
                throw new InvalidOperationException("The workflow has already been started");

            // Create and connect the relay adapter
            m_RelayAdapter = new RelayWebSocketAdapter();
            SubscribeToTransportEvents();

            ConnectResult connectResult;

            if (skipInitialization)
            {
                // Recovery mode: connect without server initialization, initialize stream hook
                m_ActiveStreamStatusHook = new(ConversationId);
                connectResult = await m_RelayAdapter.ConnectForRecovery(m_InternalCancellationTokenSource.Token);

                if (!connectResult.IsConnectedSuccessfully)
                {
                    m_RelayAdapter.Dispose();
                    throw new Exception($"Failed to connect for recovery: {connectResult.Exception?.Message}");
                }

                MessagesSent = true;
                WorkflowState = State.AwaitingChatResponse;
                InternalLog.Log("[RelayChatWorkflow] Recovery setup complete - ready to process replayed messages");
                return;
            }

            // Normal mode: connect with credentials and wait for server initialization
            WorkflowState = State.AwaitingDiscussionInitialization;

            var options = new IOrchestrationWebSocket.Options
            {
                Headers = credentialsContext.Headers,
                QueryParameters = !string.IsNullOrEmpty(ConversationId)
                    ? new System.Collections.Generic.Dictionary<string, string> { ["conversation_id"] = ConversationId }
                    : null
            };

            connectResult = await m_RelayAdapter.Connect(options, m_InternalCancellationTokenSource.Token);

            if (!connectResult.IsConnectedSuccessfully)
            {
                if (IsCancelled)
                {
                    InternalLog.Log("[RelayChatWorkflow] Workflow ignores non-successful connection. Workflow was already cancelled.");
                    return;
                }

                m_RelayAdapter.Dispose();
                TriggerOnClose(new CloseReason()
                {
                    Reason = CloseReason.ReasonType.CouldNotConnect,
                    Info = $"Failed to connect to relay: {connectResult.Exception?.Message}"
                });

                m_InternalCancellationTokenSource.Cancel();
                return;
            }

            // Start the cloud session
            var sessionResult = await m_RelayAdapter.StartCloudSession(options, m_InternalCancellationTokenSource.Token);
            if (!sessionResult.IsConnectedSuccessfully)
            {
                if (IsCancelled)
                {
                    InternalLog.Log("[RelayChatWorkflow] Workflow ignores cloud session start failure. Workflow was already cancelled.");
                    return;
                }

                m_RelayAdapter.Dispose();
                TriggerOnClose(new CloseReason()
                {
                    Reason = CloseReason.ReasonType.CouldNotConnect,
                    Info = $"Failed to start cloud session: {sessionResult.Exception?.Message}"
                });

                m_InternalCancellationTokenSource.Cancel();
                return;
            }

            InternalLog.Log("[RelayChatWorkflow] Cloud session started successfully!");

            CancellationTokenSource discussionInitTimeout = new(TimeSpan.FromMilliseconds(DiscussionInitializationTimeoutMillis));

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            TaskUtils.WithExceptionLogging(() => WaitForDiscussionInit());
#pragma warning restore CS4014

            async Task WaitForDiscussionInit()
            {
                while (!CheckWorkflowIsOneOfStates(State.Idle, State.AwaitingChatAcknowledgement, State.AwaitingChatResponse, State.ProcessingStream, State.Closed))
                {
                    if (discussionInitTimeout.IsCancellationRequested)
                    {
                        InternalLog.LogWarning($"[RelayChatWorkflow] Discussion initialization timed out after {DiscussionInitializationTimeoutMillis}ms. " +
                            $"State: {WorkflowState}. Cloud session may have disconnected without notification.");
                        await TaskUtils.WithExceptionLogging(() => DisconnectFromServer(new CloseReason
                        {
                            Reason = CloseReason.ReasonType.DiscussionInitializationTimeout,
                        }));

                        return;
                    }

                    await DelayUtility.ReasonableResponsiveDelay();
                }
            }
        }

        protected override async Task SendMessageInternal(object message, CancellationToken cancellationToken)
        {
            if (WorkflowState == State.Closed)
            {
                // A closed workflow silently drops the send — this is exactly how a function
                // response is lost when the window closes mid-tool (muse-editor#2705). Record it so a
                // redelivery of the still-pending call is answered from cache, not re-run.
                RememberUndeliveredResponse(message);

                // Caching alone is not enough here: closing the window reconnects nothing — the relay
                // client is a RelayService singleton that outlives this workflow and is torn down only
                // at editor quit — so neither redelivery trigger (the relay's auto-recovery grace
                // timer, armed on client connect, nor RELAY_RECOVER_MESSAGES) ever fires, and the
                // cache is never consulted; the turn would time out ~600s later. Deliver the recorded
                // response on the still-live client now, the way the standalone fallback answers a
                // redelivery. TryGetResult stays the backstop for the genuine reconnect case (a domain
                // reload replaces the client and replays pending calls). A no-op when there is no live
                // client, leaving the cache as the only recourse.
                RelayFunctionCallFallback.TryDeliverRecordedResponseOnLiveClient(RelayService.Instance.Client, message);
                return;
            }

            if (m_RelayAdapter == null || !m_RelayAdapter.IsConnected)
            {
                var reason = new CloseReason
                {
                    Reason = CloseReason.ReasonType.UnderlyingWebSocketWasClosed,
                    Info = "Not connected to chat service. Please try again."
                };
                var adapterState = m_RelayAdapter == null ? "adapter is null" : "adapter reports IsConnected=false";
                InternalLog.LogError($"[RelayChatWorkflow] SendMessageInternal called but {adapterState}; " +
                                     $"cannot deliver message of type {message?.GetType().Name ?? "<null>"}. " +
                                     $"Tearing down workflow with CloseReason={reason.Reason}.");

                RememberUndeliveredResponse(message);
                await DisconnectFromServer(reason);
                return;
            }

            if (message is not IModel model)
            {
                InternalLog.LogError($"[RelayChatWorkflow] SendMessageInternal received an object that does not implement IModel " +
                                     $"(actual type: {message?.GetType().FullName ?? "<null>"}). " +
                                     "This indicates a programming error in the caller; the message will be dropped.");
                return;
            }

            try
            {
                var result = await m_RelayAdapter.Send(model, cancellationToken);
                if (result.IsSendSuccessful)
                    return;

                // Send rejected by the adapter (e.g. relay dropped between IsConnected check and SendAsync).
                var exceptionDetail = result.Exception?.Message;
                var info = string.IsNullOrEmpty(exceptionDetail)
                    ? "Failed to send your message. Please try again."
                    : $"Failed to send your message. Please try again. Details: {exceptionDetail}";
                var sendFailureReason = new CloseReason
                {
                    Reason = CloseReason.ReasonType.UnderlyingWebSocketWasClosed,
                    Info = info,
                    Exception = result.Exception
                };
                InternalLog.LogError($"[RelayChatWorkflow] Send rejected by adapter: {exceptionDetail ?? "no exception"}");
                RememberUndeliveredResponse(message);
                await DisconnectFromServer(sendFailureReason);
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[RelayChatWorkflow] Error sending message: {ex.Message}");
                throw;
            }
        }

        protected override void SubscribeToTransportEvents()
        {
            if (m_RelayAdapter != null)
            {
                m_RelayAdapter.OnMessageReceived += ProcessReceiveResult;
                m_RelayAdapter.OnClose += HandleWebsocketClosed;
            }
        }

        protected override void UnsubscribeFromTransportEvents()
        {
            if (m_RelayAdapter != null)
            {
                m_RelayAdapter.OnMessageReceived -= ProcessReceiveResult;
                m_RelayAdapter.OnClose -= HandleWebsocketClosed;
            }
        }

        protected override void DisposeTransport()
        {
            if (m_RelayAdapter != null)
            {
                m_RelayAdapter.OnMessageReceived -= ProcessReceiveResult;
                m_RelayAdapter.OnClose -= HandleWebsocketClosed;
                m_RelayAdapter.Dispose();
                m_RelayAdapter = null;
                InternalLog.Log("[RelayChatWorkflow] Disposed relay adapter");
            }
        }

        void HandleWebsocketClosed(WebSocketCloseStatus? closeStatus, string closeDescription)
        {
            InternalLog.Log($"[RelayChatWorkflow] WebSocket disconnected (closeStatus={closeStatus}, description={closeDescription})");
            Dispose();
            TriggerOnClose(CloseReason.FromTransportClose(closeStatus, closeDescription));
        }
    }
}
