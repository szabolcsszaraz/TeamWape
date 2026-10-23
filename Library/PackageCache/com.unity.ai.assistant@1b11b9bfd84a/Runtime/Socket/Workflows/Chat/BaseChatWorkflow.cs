using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Agents;
using Unity.AI.Assistant.Socket.Communication;
using Unity.AI.Assistant.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Socket.Protocol.Models.FromServer;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Skills;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Socket.Workflows.Chat
{
    /// <summary>
    /// Base class for chat workflows that manages the protocol state machine and message handling.
    /// Derived classes implement the transport layer (direct WebSocket or relay).
    /// </summary>
    abstract class BaseChatWorkflow : IChatWorkflow
    {
        /// <summary>
        /// If the <see cref="DiscussionInitializationV1"/> is not received after connection within this timeout. Assume
        /// there is a problem and disconnect.
        /// </summary>
        protected internal const float DiscussionInitializationTimeoutMillis = 5000;

        /// <summary>
        /// The conversationId that the workflow is actively working on
        /// </summary>
        public string ConversationId { get; protected set; }

        /// <summary>
        /// The current state that the workflow is in. This effects the messages that are valid for the workflow to
        /// send.
        /// </summary>
        public State WorkflowState
        {
            get => m_WorkflowState;
            protected set
            {
                if (m_WorkflowState != value)
                    OnWorkflowStateChanged?.Invoke(value);

                m_WorkflowState = value;
            }
        }

        public bool IsCancelled => m_InternalCancellationTokenSource.IsCancellationRequested;

        /// <summary>
        /// If the workflow is in the <see cref="State.Closed"/> state, indicates the reason for the closure.
        /// </summary>
        public CloseReason CloseReason { get; protected set; }

        /// <summary>
        /// Invoked when a chat response is sent by the server. The workflow tracks the entire message as it streams in.
        /// This event is invoked with the entire message collected so far.
        /// </summary>
        public event Action<ChatResponseFragment> OnChatResponse;

        /// <summary>
        /// Invoked when the <see cref="ChatAcknowledgmentV1"/> is received. Provides the prompt that the server is
        /// using that will be saved into the database.
        /// </summary>
        public event Action<AcknowledgePromptInfo> OnAcknowledgeChat;

        /// <summary>
        /// Invoked when the <see cref="DiscussionInitializationV1"/> is received. Provides the conversation id provided
        /// by the server.
        /// </summary>
        public event Action<string> OnConversationId;

        /// <summary>
        /// Invoked when the workflow is closed. Generally, the workflow closes when the websocket is closed. It is
        /// possible for both the workflow, or the server to initiate the close.
        /// </summary>
        public event Action<CloseReason> OnClose;

        /// <summary>
        /// True, when the workflow has sent a message at least once
        /// </summary>
        public bool MessagesSent { get; protected set; }

        /// <summary>
        /// Completes once the subclass <c>Start</c> path has finished wiring up transport and state. Faults if startup throws.
        /// See <see cref="IChatWorkflow.Started"/>.
        /// </summary>
        public Task Started => m_StartedTcs.Task;

        readonly TaskCompletionSource<bool> m_StartedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Invoked when the workflow's state changes
        /// </summary>
        public event Action<State> OnWorkflowStateChanged;

        public event Action<string, Guid, JObject> OnFunctionCall;
        public event Action<Guid, FunctionCallResult> OnFunctionCallResult;

        public event Action<string> OnMessageReverted;

        protected IFunctionCaller m_FunctionCaller;
        protected OrchestrationStreamStatusHook m_ActiveStreamStatusHook;
        protected StringBuilder m_ActiveStreamStringBuilder = new();
        protected CancellationTokenSource m_InternalCancellationTokenSource = new();
        protected CancellationTokenSource m_ChatRequestCancellationTokenSource;
        protected float m_ChatTimeoutMilliseconds = 600000; // Default 600 seconds (10 min), will be overridden by server in normal flow

        State m_WorkflowState = State.NotStarted;

        protected BaseChatWorkflow(string conversationId = null, IFunctionCaller functionCaller = null)
        {
            if (!string.IsNullOrEmpty(conversationId))
            {
                ConversationId = conversationId;
            }
            m_FunctionCaller = functionCaller;
        }

        /// <summary>
        /// Derived classes must implement transport-specific connection logic
        /// </summary>
        protected abstract Task StartConnectionInternal(ICredentialsContext credentialsContext, bool skipInitialization);

        /// <summary>
        /// Signals that <c>Start</c> has finished successfully. Safe to call more than once; only the first call transitions <see cref="Started"/>.
        /// Exposed as <c>protected internal</c> so backends that bypass the subclass <c>Start</c> path (e.g. direct WebSocket recovery, which has
        /// nothing to initialize) can release awaiters without stalling <see cref="Started"/>.
        /// </summary>
        protected internal void MarkStarted() => m_StartedTcs.TrySetResult(true);

        /// <summary>
        /// Signals that the workflow's <c>Start</c> path failed. Faults <see cref="Started"/> so awaiters can observe the failure.
        /// </summary>
        protected void MarkStartFailed(Exception ex) => m_StartedTcs.TrySetException(ex);

        /// <summary>
        /// Derived classes must implement transport-specific message sending
        /// </summary>
        protected abstract Task SendMessageInternal(object message, CancellationToken cancellationToken);

        /// <summary>
        /// Derived classes must implement transport-specific disposal
        /// </summary>
        protected abstract void DisposeTransport();

        /// <summary>
        /// Derived classes must subscribe to transport events and call ProcessReceiveResult when messages arrive
        /// </summary>
        protected abstract void SubscribeToTransportEvents();

        /// <summary>
        /// Derived classes must unsubscribe from transport events
        /// </summary>
        protected abstract void UnsubscribeFromTransportEvents();

        /// <summary>
        /// Helper method for derived classes to trigger the OnClose event
        /// </summary>
        protected void TriggerOnClose(CloseReason reason)
        {
            OnClose?.Invoke(reason);
        }

        /// <summary>
        /// Called by derived classes to process incoming messages from the transport layer
        /// </summary>
        protected void ProcessReceiveResult(ReceiveResult result)
        {
            InternalLog.LogToFile(ConversationId ?? "unknown", ("event", "process_receive_result"), ("workflowState", WorkflowState.ToString()), ("deserialized", result.IsDeserializedSuccessfully.ToString()), ("type", result.DeserializedData?.GetType().Name ?? "null"));

            if (!result.IsDeserializedSuccessfully)
            {
                DisconnectBecauseMessageIsUnknown();
                return;
            }

            // A null DeserializedData with IsDeserializedSuccessfully=true means the
            // converter encountered an unknown $type and chose to skip the message
            // (forward-compatibility: backend added a new message type this client
            // version does not understand). Drop it silently — do NOT disconnect.
            if (result.DeserializedData == null)
            {
                return;
            }

            if (result.DeserializedData is ServerDisconnectV1 serverDisconnect)
            {
                HandleServerDisconnect(serverDisconnect);
                return;
            }

            if (m_ChatRequestCancellationTokenSource?.IsCancellationRequested == true)
            {
                // Claim a function call we are about to drop. The user cancelled this turn, so the
                // tool must not run — and the relay still holds the call as pending, so without a
                // claim its auto-recovery redelivery would reach the standalone fallback and run
                // the very tool the cancellation was meant to stop.
                if (result.DeserializedData is FunctionCallRequestV1 cancelledCall)
                    FunctionCallDispatchRegistry.TryClaim(cancelledCall.CallId);

                InternalLog.LogWarning("Skipping message - cancellation requested.");
                DisconnectBecauseMessageSentAtWrongTime();
                return;
            }

            var message = result.DeserializedData;
            switch (WorkflowState)
            {
                // Before the DiscussionInitializationV1 is received, nothing else is valid from the server
                case State.AwaitingDiscussionInitialization:
                {
                    if (message is not DiscussionInitializationV1 discussionInitializationV1)
                        DisconnectBecauseMessageSentAtWrongTime();
                    else
                        HandleDiscussionInitializationV1(discussionInitializationV1);
                    break;
                }
                // Before the user makes a chat request, the server can still make calls
                case State.Idle:
                {
                    Action action = result.DeserializedData switch
                    {
                        CapabilitiesRequestV1 cr => () => HandleCapabilitiesRequestV1(cr).WithExceptionLogging(),
                        SkillsRequestV1 sr => () => HandleSkillsRequestV1(sr).WithExceptionLogging(),
                        AvailableSkillsV1 av => () => HandleAvailableSkillsV1(av),
                        FunctionCallRequestV1 fcr => () => HandleFunctionCallRequestV1(fcr),
                        _ => DisconnectBecauseMessageSentAtWrongTime
                    };

                    action();
                    break;
                }
                // Before a chat has been acknowledged, many things are valid
                case State.AwaitingChatAcknowledgement:
                {
                    Action action = result.DeserializedData switch
                    {
                        CapabilitiesRequestV1 cr => () => HandleCapabilitiesRequestV1(cr).WithExceptionLogging(),
                        SkillsRequestV1 sr => () => HandleSkillsRequestV1(sr).WithExceptionLogging(),
                        AvailableSkillsV1 av => () => HandleAvailableSkillsV1(av),
                        FunctionCallRequestV1 fcr => () => HandleFunctionCallRequestV1(fcr),
                        ChatAcknowledgmentV1 ca => () => HandleChatAcknowledgmentV1(ca).WithExceptionLogging(),
                        _ => DisconnectBecauseMessageSentAtWrongTime
                    };

                    action();
                    break;
                }
                // Before a stream response stream starts coming back from the server, many things are valid
                case State.AwaitingChatResponse:
                {
                    Action action = result.DeserializedData switch
                    {
                        CapabilitiesRequestV1 cr => () => HandleCapabilitiesRequestV1(cr).WithExceptionLogging(),
                        SkillsRequestV1 sr => () => HandleSkillsRequestV1(sr).WithExceptionLogging(),
                        AvailableSkillsV1 av => () => HandleAvailableSkillsV1(av),
                        FunctionCallRequestV1 fcr => () => HandleFunctionCallRequestV1(fcr),
                        ChatResponseV1 crf => () => HandleChatResponseFragmentV1(crf),
                        _ => DisconnectBecauseMessageSentAtWrongTime
                    };

                    action();
                    break;
                }
                // Once a response is being streamed, we only support getting function calls or response fragments
                case State.ProcessingStream:
                {
                    Action action = result.DeserializedData switch
                    {
                        FunctionCallRequestV1 fcr => () => HandleFunctionCallRequestV1(fcr),
                        ChatResponseV1 crf => () => HandleChatResponseFragmentV1(crf),
                        _ => DisconnectBecauseMessageSentAtWrongTime
                    };

                    action();
                    break;
                }
            }

            void DisconnectBecauseMessageSentAtWrongTime()
            {
                DisconnectFromServer(new CloseReason()
                {
                    Reason = CloseReason.ReasonType.ServerSentMessageAtWrongTime,
                    Info = $"State: {WorkflowState}, Model: {result.RawData}, Cancellation: {m_ChatRequestCancellationTokenSource?.IsCancellationRequested}"
                }).WithExceptionLogging();
            }

            void DisconnectBecauseMessageIsUnknown()
            {
                DisconnectFromServer(new CloseReason()
                {
                    Reason = CloseReason.ReasonType.ServerSentUnknownMessage,
                    Info = $"The workflow received unknown message. Raw data: {result.RawData}\nThe " +
                           $"workflow was in the state: {WorkflowState}",
                    Exception = result.Exception
                }).WithExceptionLogging();
            }
        }

        public async Task<IStreamStatusHook> SendChatRequest(string prompt, List<ChatRequestV1.AttachedContextModel> context,
            IAgent agent = null, AssistantMode? assistantMode = null, ModelConfiguration modelConfiguration = null, CancellationToken ct = default)
        {
            if (!CheckWorkflowIsOneOfStates(State.Idle))
                throw new InvalidOperationException("A chat request cannot be made unless the workflow is idle or in the initial connection handshake");

            MessagesSent = true;
            // Currently, the server returns fragments, but the UX expects a cumulative response.
            m_ActiveStreamStringBuilder.Clear();

            WorkflowState = State.AwaitingChatAcknowledgement;

            InternalLog.LogToFile(
                ConversationId,
                ("event", "send prompt"),
                ("prompt", prompt)
            );

            await SendMessageInternal(new ChatRequestV1
            {
                Markdown = prompt,
                AttachedContext = context,
                Agent = agent.ConvertToAgentDefinitionV1(),
                Mode = assistantMode?.ToName(),
                ModelSettings = modelConfiguration,
                PermissionPolicies = CapabilityRegistry.GetPermissionPolicies()
            }, ct);

            m_ChatRequestCancellationTokenSource = new();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            // Setup a parallel task that will cancel the chat request if the user asks for it
            ParallelCancel().WithExceptionLogging();

            // Setup a parallel task that will cancel the workflow if the time to first response token is too long
            ParallelTimeout().WithExceptionLogging();
#pragma warning restore CS4014

            // TODO: IStreamStatusHook is still part of integrating with the legacy system. This will likely change into something more websocket appropriate
            m_ActiveStreamStatusHook = new(ConversationId);
            return m_ActiveStreamStatusHook;

            async Task ParallelTimeout()
            {
                CancellationTokenSource timeoutCancellation = new(TimeSpan.FromMilliseconds(m_ChatTimeoutMilliseconds));
                CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutCancellation.Token,
                    m_InternalCancellationTokenSource.Token,
                    m_ChatRequestCancellationTokenSource.Token
                );

                while (!cancel.IsCancellationRequested && WorkflowState != State.ProcessingStream)
                    await DelayUtility.ReasonableResponsiveDelay();

                // check to see if cancellation was internal (meaning that it was not the timeout, just return)
                if(m_InternalCancellationTokenSource.Token.IsCancellationRequested)
                    return;

                // if it's a chat request cancellation, then no need to deal with timeouts anymore.
                if(m_ChatRequestCancellationTokenSource.Token.IsCancellationRequested)
                    return;

                // if a timeout occurs, this is a reason to disconnect
                if (timeoutCancellation.IsCancellationRequested)
                    await DisconnectFromServer(new CloseReason()
                    {
                        Reason = CloseReason.ReasonType.ChatResponseTimeout,
                        Info = $"Timeout: {m_ChatTimeoutMilliseconds}"
                    }).WithExceptionLogging();
            }

            async Task ParallelCancel()
            {
                CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(
                    m_InternalCancellationTokenSource.Token,
                    m_ChatRequestCancellationTokenSource.Token
                );

                while (!cancel.IsCancellationRequested)
                    await DelayUtility.ReasonableResponsiveDelay();

                // check to see if cancellation was internal (something else happened and waiting for cancellation
                // isn't necessary anymore)
                if(m_InternalCancellationTokenSource.Token.IsCancellationRequested)
                    return;

                // if a cancellation is actually requested, send a cancellation message to the server
                if (m_ChatRequestCancellationTokenSource.IsCancellationRequested)
                {
                    await SendMessageInternal(new CancelChatRequestV1(), m_InternalCancellationTokenSource.Token);
                }
            }
        }

        public void CancelCurrentChatRequest()
        {
            if (!CheckWorkflowIsOneOfStates(State.AwaitingDiscussionInitialization, State.AwaitingChatAcknowledgement, State.AwaitingChatResponse, State.ProcessingStream))
                return;

            WorkflowState = State.Canceling;

            // This should never happen. It's possible that this should be an exception, but let us be permissive until
            // it's a problem.
            if (m_ChatRequestCancellationTokenSource == null)
                return;

            m_ChatRequestCancellationTokenSource.Cancel();
        }

        public async Task<bool> AwaitDiscussionInitialization()
        {
            while (!CheckWorkflowIsOneOfStates(State.Idle, State.AwaitingChatAcknowledgement, State.AwaitingChatResponse, State.ProcessingStream, State.Closed))
            {
                if(m_InternalCancellationTokenSource.IsCancellationRequested)
                    return false;

                if (m_ChatRequestCancellationTokenSource?.IsCancellationRequested == true)
                    return false;

                await DelayUtility.ReasonableResponsiveDelay();
            }

            return !m_InternalCancellationTokenSource.IsCancellationRequested;
        }

        void HandleDiscussionInitializationV1(DiscussionInitializationV1 message)
        {
            // NOTE: DiscussionInitializationV1.MaxMessageSize is a deprecated field and clients are no longer expected
            // to use it.
            
            InternalLog.Log("Discussion initialized with conversation id: " + message.ConversationId);
            
            InternalLog.LogToFile(message.ConversationId, ("event", "discussion initialized"));
            ConversationId = message.ConversationId;
            OnConversationId?.Invoke(message.ConversationId);

            // Note: the function call dispatch guard is deliberately NOT reset here. It is shared
            // across workflows now (see FunctionCallDispatchRegistry), and a new discussion does not
            // mean the previous one's calls are settled — a call the outgoing workflow already ran
            // but could not answer stays pending on the relay and is still a redelivery candidate.
            // Dropping its claim here would let the standalone fallback run that tool a second time.
            // Nothing has to be reset: call ids are GUIDs, so a stale entry can never match a new
            // call, and the registry caps itself.

            m_ChatTimeoutMilliseconds = message.ChatTimeoutSeconds * 1000;
            
            WorkflowState = State.Idle;
        }

        async Task HandleChatAcknowledgmentV1(ChatAcknowledgmentV1 ca)
        {
            InternalLog.LogToFile(ConversationId, ("event", "chat acknowledged"));
            AcknowledgePromptInfo info = new()
            {
                Id = ca.MessageId,
                Content = ca.Markdown,
                Context = ca.AttachedContextMetadata.Select(c =>
                {
                    return new AssistantContextEntry()
                    {
                        DisplayValue = c.DisplayValue,
                        EntryType = (AssistantContextType)c.EntryType,
                        Value = c.Value,
                        ValueIndex = c.ValueIndex,
                        ValueType = c.ValueType
                    };
                }).ToArray()
            };

            WorkflowState = State.AwaitingChatResponse;
            OnAcknowledgeChat?.Invoke(info);
            await Task.CompletedTask;
        }

        void HandleFunctionCallRequestV1(FunctionCallRequestV1 message)
        {
            InternalLog.LogToFile(
                ConversationId,
                ("event", "function call requested"),
                ("call_id", message.CallId.ToString()),
                ("function_id", message.FunctionId),
                ("params", message.FunctionParameters.ToString()));

            if (m_FunctionCaller == null)
            {
                InternalLog.LogError($"Workflow instance does not have a {m_FunctionCaller} set. The recieved" +
                                     $"FunctionCallRequestV1 cannot be handled");
                return;
            }

            // Claimed only once we are actually going to dispatch, so a request we could not
            // handle above does not suppress a later, working delivery of the same call.
            if (!FunctionCallDispatchRegistry.TryClaim(message.CallId))
            {
                InternalLog.LogWarning($"Ignoring duplicate function call request {message.CallId} ({message.FunctionId}) - it was already dispatched");
                InternalLog.LogToFile(
                    ConversationId,
                    ("event", "duplicate function call ignored"),
                    ("call_id", message.CallId.ToString()),
                    ("function_id", message.FunctionId));
                return;
            }

            OnFunctionCall?.Invoke(message.FunctionId, message.CallId, message.FunctionParameters);
            var cancellationToken = m_ChatRequestCancellationTokenSource?.Token ?? CancellationToken.None;
            m_FunctionCaller.CallByLLM(this, message.FunctionId, message.FunctionParameters, message.CallId, cancellationToken);
        }

        public void SendFunctionCallResponse(FunctionCallResult result, Guid callId)
        {
            OnFunctionCallResult?.Invoke(callId, result);
            SendMessageInternal(new FunctionCallResponseV1
            {
                CallId = callId,
                FunctionResult = result.Result,
                Success = result.HasFunctionCallSucceeded
            }, m_InternalCancellationTokenSource.Token).WithExceptionLogging();

            InternalLog.LogToFile(
                ConversationId,
                ("event", "function call response"),
                ("call_id", callId.ToString()),
                ("succeeded", result.HasFunctionCallSucceeded.ToString()),
                ("is_done", result.IsDone.ToString()),
                ("data", result.Result.ToString()));
        }

        /// <summary>
        /// Records a <see cref="FunctionCallResponseV1"/> that a transport could not deliver, so the
        /// standalone relay fallback can answer a redelivery of the still-pending call by re-sending
        /// the cached result rather than running the tool again (muse-editor#2705).
        /// <para>
        /// Call this only from a send path that has failed to reach the relay — a delivered response
        /// clears the call from the relay's pending set, so recording it would make the fallback
        /// re-send a call that was already answered. A no-op for any other message type, and for a
        /// call the dispatch registry no longer tracks.
        /// </para>
        /// </summary>
        protected static void RememberUndeliveredResponse(object message)
        {
            if (message is not FunctionCallResponseV1 response)
                return;

            var result = response.Success
                ? FunctionCallResult.SuccessfulResult(response.FunctionResult)
                : FunctionCallResult.FailedResult(response.FunctionResult?.ToString() ?? string.Empty);
            FunctionCallDispatchRegistry.RecordResult(response.CallId, result);
        }

        public void RevertMessageRequest(string messageId)
        {
            OnMessageReverted?.Invoke(messageId);
            SendMessageInternal(new RevertMessageNotification
            {
                MessageId = messageId,
            }, m_InternalCancellationTokenSource.Token).WithExceptionLogging();
        }

        async Task HandleCapabilitiesRequestV1(CapabilitiesRequestV1 message)
        {
            InternalLog.LogToFile(ConversationId, ("event", "capabilities requested"));

            await SendMessageInternal(new CapabilitiesResponseV1()
            {
                Functions = CapabilityRegistry.GetFunctionCapabilities(),
                Agents = AgentRegistry.GetAgentDefinitions(),
                ProtocolVersion = 1
            }, default);

            InternalLog.LogToFile(ConversationId, ("event", "capabilities response sent"));
        }

        async Task HandleSkillsRequestV1(SkillsRequestV1 message)
        {
            InternalLog.LogToFile(ConversationId, ("event", "skills requested"));

            var totalStopwatch = Stopwatch.StartNew();

            var getSkillsStopwatch = Stopwatch.StartNew();
            var skillDefinitions = SkillsRegistry.GetSkills();
            getSkillsStopwatch.Stop();

            InternalLog.Log($"[SkillsTiming] HandleSkillsRequestV1: GetSkills() {getSkillsStopwatch.ElapsedMilliseconds}ms ({skillDefinitions.Count} skills)");
            
            if (!SkillsRegistry.IsLoadComplete)
                UnityEngine.Debug.LogWarning($"[SkillsRegistry] HandleSkillsRequestV1: skill scan did not finish within {getSkillsStopwatch.ElapsedMilliseconds}ms - sending {skillDefinitions.Count} partially loaded skill(s) to backend. Check skill folder(s) for slow or unreachable drives. If a skill tool call fails, ask the user to retry once scanning completes.");

            var skillMetadata = skillDefinitions.Select(s => s.Value.MetaData).ToList();

            // Append synthetic disable entries for backend skills the developer-tools
            // panel has toggled off. The backend's SkillManager._merge_frontend_skills
            // removes any backend skill whose name matches a frontend skill with
            // enabled=false.
            var disabledBackend = BackendSkillsCache.DisabledSkillsProvider?.Invoke();
            if (disabledBackend != null)
            {
                foreach (var name in disabledBackend)
                {
                    skillMetadata.Add(new SkillMetaData
                    {
                        Name = name,
                        Description = "(disabled by developer-tools)",
                        Enabled = false,
                    });
                }
            }

            await SendMessageInternal(new SkillsResponseV1()
            {
                Skills = skillMetadata
            }, default);

            totalStopwatch.Stop();
            InternalLog.Log($"[SkillsTiming] HandleSkillsRequestV1: response sent, {totalStopwatch.ElapsedMilliseconds}ms total");
            InternalLog.LogToFile(ConversationId, ("event", "skills response sent"));
        }

        void HandleAvailableSkillsV1(AvailableSkillsV1 message)
        {
            InternalLog.LogToFile(ConversationId,
                ("event", "available skills received"),
                ("count", message.Skills.Count.ToString()));
            InternalLog.LogToFile(ConversationId,
                ("event", "available skills names"),
                ("skills", string.Join(", ", message.Skills.Select(s => s.Name))));
            BackendSkillsCache.Update(message.Skills);
        }

        void HandleChatResponseFragmentV1(ChatResponseV1 message)
        {
            InternalLog.LogToFile(
                ConversationId,
                ("event", "chat response fragment received"),
                ("id", message.MessageId),
                ("is_last", message.LastMessage.ToString()),
                ("fragment", message.Markdown)
            );

            m_ActiveStreamStringBuilder.Append(message.Markdown);

            if (message.LastMessage)
                WorkflowState = State.Idle;
            else
                WorkflowState = State.ProcessingStream;

            try
            {
                OnChatResponse?.Invoke(new ChatResponseFragment()
                {
                    Fragment = message.Markdown,
                    Id = message.MessageId,
                    CorrelationMessageId = message.CorrelationMessageId,
                    IsLastFragment = message.LastMessage,
                    UsedTokens = message.ContextUsageUsedTokens,
                    MaxTokens = message.ContextUsageMaxTokens,
                });
            }
            catch (Exception e)
            {
                InternalLog.LogError($"[HandleChatResponseFragmentV1] Error in OnChatResponse handler: {e}");
            }

            m_ActiveStreamStatusHook.ProcessStatusFromResponse(message, m_ActiveStreamStringBuilder.ToString());
        }

        void HandleServerDisconnect(ServerDisconnectV1 serverDisconnect)
        {
            InternalLog.LogToFile(
                ConversationId,
                ("event", "server disconnected"),
                ("reason_is_happy_path", serverDisconnect.DisconnectReason.IsHappyPathModel.ToString()),
                ("reason_is_critical", serverDisconnect.DisconnectReason.IsCriticalError.ToString()),
                ("reason_is_info", serverDisconnect.DisconnectReason.IsInfoDisconnect.ToString()),
                ("reason_is_no_capacity", serverDisconnect.DisconnectReason.IsNoCapacity.ToString())
            );

            if (serverDisconnect.DisconnectReason.IsHappyPathModel)
            {
                CloseReason reason = new() { Reason = CloseReason.ReasonType.ServerDisconnectedGracefully, Info = "Happy Path" };
                DisconnectFromServer(reason).WithExceptionLogging();
            }
            else if (serverDisconnect.DisconnectReason.IsInfoDisconnect)
            {
                // Informational disconnects (e.g., "Server is restarting for maintenance.") are not errors.
                // Surface the user-facing message via a dedicated CloseReason so consumers can render it distinctly from critical-error disconnects.
                var infoMessage = serverDisconnect.DisconnectReason.InfoDisconnect?.UserMessage;
                CloseReason reason = new()
                {
                    Reason = CloseReason.ReasonType.ServerDisconnectedInformational,
                    Info = string.IsNullOrEmpty(infoMessage) ? "The server disconnected for an informational reason." : infoMessage
                };

                DisconnectFromServer(reason).WithExceptionLogging();
            }
            else if (serverDisconnect.DisconnectReason.IsNoCapacity)
            {
                // Distinct capacity signal so the UI can auto-fall back to the default provider profile instead of
                // rendering a generic error. user_message is optional; carry it through for logging.
                var capacityMessage = serverDisconnect.DisconnectReason.NoCapacity?.UserMessage;
                CloseReason reason = new()
                {
                    Reason = CloseReason.ReasonType.ServerNoCapacity,
                    Info = string.IsNullOrEmpty(capacityMessage) ? CloseReason.NoCapacityFallbackInfo : capacityMessage
                };
                DisconnectFromServer(reason).WithExceptionLogging();
            }
            else
            {
                CloseReason errorReason = new() { Reason = CloseReason.ReasonType.ServerDisconnected };

                if (serverDisconnect.DisconnectReason.IsCriticalError)
                    errorReason.Info = serverDisconnect.DisconnectReason.CriticalError.UserMessage;

                DisconnectFromServer(errorReason).WithExceptionLogging();
            }
        }

        public void LocalDisconnect()
        {
            CloseReason reason = new() { Reason = CloseReason.ReasonType.ClientCanceled, Info = "Happy Path" };
            DisconnectFromServer(reason).WithExceptionLogging();
        }

        protected async Task DisconnectFromServer(CloseReason reason)
        {
            InternalLog.Log($"[BaseChatWorkflow] Disconnecting from server. Reason: {reason.Reason}, Info: {reason.Info}");
            CloseReason = reason;

            // Unsubscribe from transport events
            UnsubscribeFromTransportEvents();

            // Determine and send disconnect packet
            switch (reason.Reason)
            {
                case CloseReason.ReasonType.CouldNotConnect:
                    break;
                case CloseReason.ReasonType.UnderlyingWebSocketWasClosed:
                    break;
                case CloseReason.ReasonType.ChatResponseTimeout:
                    var timeoutMessage = new ClientDisconnectV1
                    {
                        DisconnectReason = ClientDisconnectV1.DisconnectReasonOneOf.FromTimeoutModel(
                            new ClientDisconnectV1.DisconnectReasonOneOf.TimeoutModel()
                        )
                    };
                    await SendMessageInternal(timeoutMessage, default);
                    break;
                case CloseReason.ReasonType.ServerSentUnknownMessage:
                    var unknownMessage = new ClientDisconnectV1
                    {
                        DisconnectReason = ClientDisconnectV1.DisconnectReasonOneOf.FromInvalidMessageModel(
                            new ClientDisconnectV1.DisconnectReasonOneOf.InvalidMessageModel()
                            {
                                InvalidMessage = reason.Info
                            }
                        )
                    };
                    await SendMessageInternal(unknownMessage, default);
                    break;
                case CloseReason.ReasonType.ServerSentMessageAtWrongTime:
                    var wrongTimeMessage = new ClientDisconnectV1
                    {
                        DisconnectReason = ClientDisconnectV1.DisconnectReasonOneOf.FromInvalidMessageOrderModel(
                            new ClientDisconnectV1.DisconnectReasonOneOf.InvalidMessageOrderModel() { }
                        )
                    };
                    await SendMessageInternal(wrongTimeMessage, default);
                    break;
                case CloseReason.ReasonType.DiscussionInitializationTimeout:
                    var disInitMessage = new ClientDisconnectV1
                    {
                        DisconnectReason = ClientDisconnectV1.DisconnectReasonOneOf.FromTimeoutModel(
                            new ClientDisconnectV1.DisconnectReasonOneOf.TimeoutModel()
                        )
                    };
                    await SendMessageInternal(disInitMessage, default);
                    break;
                case CloseReason.ReasonType.ServerDisconnected:
                case CloseReason.ReasonType.ServerDisconnectedInformational:
                    // When the server disconnected, don't need to send a disconnect packet
                    break;
                case CloseReason.ReasonType.ServerNoCapacity:
                    // Server already sent the NO_CAPACITY disconnect; no client disconnect packet needed.
                    break;
                case CloseReason.ReasonType.AuthenticationFailed:
                    // Server already closed the WebSocket with a policy-violation close frame
                    // (e.g. close code 1008 "Authentication failure"). The transport is gone,
                    // so don't try to echo a disconnect packet — there's nothing to send to.
                    break;
            }

            // Close the transport
            Dispose();

            // Signal closure
            OnClose?.Invoke(reason);
        }

        internal void HandleCancellationResponse()
        {
            if (m_WorkflowState == State.Canceling)
                m_WorkflowState = State.Idle;
            else
            {
                CloseReason reason = new() { Reason = CloseReason.ReasonType.ServerSentMessageAtWrongTime, Info = "Server acknowledged a cancel when a cancellation was not expected." };
                DisconnectFromServer(reason).WithExceptionLogging();
            }
        }

        public virtual void Dispose()
        {
            WorkflowState = State.Closed;

            // Indicate to all other internal tasks that the workflow is canceled.
            m_InternalCancellationTokenSource.Cancel();

            // Release any awaiters on Started so they don't hang if the workflow is disposed before (or without) Start completing.
            m_StartedTcs.TrySetCanceled();

            DisposeTransport();
        }

        protected bool CheckWorkflowIsOneOfStates(params State[] states) => states.Any(s => WorkflowState == s);
    }
}
