using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.AI.MCP.Editor.Connection;
using Unity.AI.MCP.Editor.Helpers;
using Unity.AI.MCP.Editor.Models;
using Unity.AI.MCP.Editor.Security;
using Unity.AI.MCP.Editor.Settings;
using Unity.AI.MCP.Editor.ToolRegistry;
using Unity.AI.MCP.Editor.UI;
using Unity.AI.Assistant.Editor.Acp;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Toolkit;
using Trace = Unity.AI.Tracing.Trace;
using Unity.Relay;
using Unity.Relay.Editor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using ClientInfo = Unity.AI.MCP.Editor.Models.ClientInfo;

namespace Unity.AI.MCP.Editor
{
    /// <summary>
    /// Unity MCP Bridge - Uses named pipes (Windows) / Unix sockets (Mac/Linux)
    /// Cleanly separates connection management from messaging
    /// </summary>
    class Bridge : IDisposable
    {
        // Connection layer
        IConnectionListener listener;
        volatile bool isRunning;
        readonly object startStopLock = new();
        bool isBatchMode; // captured on main thread in Start(), safe to read from background threads
        CancellationTokenSource cts;
        Task listenerTask;

        // Focus-independent dispatch pump (keeps MCP responsive while editor unfocused)
        MainThreadCommandPump commandPump;
        volatile System.Threading.SynchronizationContext mainThreadContext;
        const int k_PumpIntervalMs = 100;

        // Command processing
        int processingCommands;
        readonly ConcurrentDictionary<string, (Command command, TaskCompletionSource<string> tcs, IConnectionTransport client)> commandQueue = new();

        // Lifecycle
        bool initScheduled;
        bool ensureUpdateHooked;
        bool isStarting;
        double nextStartAt;
        // Tools
        string s_CurrentToolsHash;
        McpToolInfo[] s_ToolsSnapshot;

        // Connection info
        string currentConnectionPath;

        // Security validation
        ValidationConfig validationConfig;

        // Per-transport state is managed by TransportStore (static, thread-safe).
        // Per-identity approval tracking — one TCS per identity, stays in Bridge.
        static readonly Dictionary<string, TaskCompletionSource<bool>> pendingApprovalsByIdentity = new();
        static readonly object pendingApprovalsLock = new();

        // Command deduplication
        readonly ConcurrentDictionary<string, Task<string>> inFlightCommands = new();
        readonly ConcurrentDictionary<string, (string result, DateTime expiry)> completedCommands = new();
        static readonly TimeSpan ResultCacheDuration = TimeSpan.FromMinutes(5);
        double nextCacheCleanupAt;

        // Write serialization — multiple async responses and heartbeats may complete concurrently
        readonly SemaphoreSlim transportWriteLock = new(1, 1);

        // Per-connection analytics tracking
        readonly ConcurrentDictionary<IConnectionTransport, McpSessionTracker> transportSessionTrackers = new();

        /// <summary>
        /// Tracks per-connection analytics state for MCP session and tool call metrics.
        /// </summary>
        class McpSessionTracker
        {
            public readonly Stopwatch SessionTimer = Stopwatch.StartNew();
            public readonly Stopwatch TimeToFirstSuccess = Stopwatch.StartNew();
            public int ToolCallCount;
            public string LastToolName;
            public string ClientName;
            public int HadFirstSuccess; // 0 = false, 1 = true; int for Interlocked.CompareExchange
        }

        /// <summary>
        /// Event fired when a client connects or disconnects.
        /// This event is always invoked on the main thread via EditorTask.delayCall.
        /// </summary>
        public static event Action OnClientConnectionChanged;

        /// <summary>
        /// Diagnostic events for testing and observability.
        /// Most events fire immediately (synchronously) from background threads.
        /// OnDialogShown fires on main thread via EditorTask.delayCall.
        /// Event handlers should be thread-safe or marshal to main thread if needed.
        /// </summary>
        public static event Action<string> OnConnectionAttempt;  // Fired immediately when AcceptClientAsync returns (connectionId)
        public static event Action<string, ValidationStatus> OnValidationComplete;  // Fired immediately after validation (connectionId, status)
        public static event Action<string> OnDialogShown;  // Fired on main thread after dialog opens (connectionId)


        /// <summary>
        /// The currently shown approval dialog, if any.
        /// Used by tests to access the actual dialog instance.
        /// </summary>
        public static ConnectionApprovalDialog CurrentApprovalDialog { get; internal set; }

        public bool IsRunning => isRunning;
        public string CurrentConnectionPath => currentConnectionPath;

        /// <summary>
        /// Get the set of currently active identity keys.
        /// Used by ConnectionStore to filter active connections.
        /// </summary>
        public IEnumerable<string> GetActiveIdentityKeys()
        {
            return TransportStore.GetActiveIdentityKeys();
        }

        public string GetClientInfo()
        {
            return ConnectionStore.GetClientInfo(GetActiveIdentityKeys());
        }

        /// <summary>
        /// Disconnect any active connections matching the given identity.
        /// Used when revoking a previously-approved connection from settings.
        /// Server will see connection loss and attempt to reconnect, at which point
        /// it will receive approval_denied during the handshake if status is Rejected.
        /// </summary>
        public void DisconnectConnectionByIdentity(ConnectionIdentity identity)
        {
            if (identity == null || string.IsNullOrEmpty(identity.CombinedIdentityKey))
                return;

            var transports = TransportStore.GetAllTransportsByIdentity(identity.CombinedIdentityKey);
            if (transports.Count > 0)
            {
                McpLog.LogDelayed($"Disconnecting {transports.Count} connection(s) with identity: {identity.CombinedIdentityKey}");
                foreach (var transport in transports)
                {
                    try
                    {
                        transport.Dispose();
                    }
                    catch (Exception ex)
                    {
                        McpLog.LogDelayed($"Error disconnecting transport: {ex.Message}", LogType.Warning);
                    }
                }
            }
            else
            {
                McpLog.LogDelayed($"No active connection found for identity: {identity.CombinedIdentityKey}");
            }
        }

        /// <summary>
        /// Disconnect all active connections.
        /// Used when removing all connections from settings.
        /// </summary>
        public void DisconnectAll()
        {
            var keys = TransportStore.GetActiveIdentityKeys();
            McpLog.LogDelayed($"Disconnecting all connections ({keys.Count} active)");
            var toClose = keys.Select(k => TransportStore.GetTransportByIdentity(k)).Where(t => t != null);
            foreach (var transport in toClose)
            {
                try
                {
                    transport.Dispose();
                }
                catch (Exception ex)
                {
                    McpLog.LogDelayed($"Error disconnecting transport: {ex.Message}", LogType.Warning);
                }
            }
        }

        /// <summary>
        /// Complete a pending approval for a connection with the given identity.
        /// Called from settings UI when user accepts/denies a pending connection.
        /// </summary>
        /// <param name="identityKey">The combined identity key (server+client)</param>
        /// <param name="approved">True to approve, false to deny</param>
        public static void CompletePendingApproval(string identityKey, bool approved)
        {
            if (string.IsNullOrEmpty(identityKey))
                return;

            lock (pendingApprovalsLock)
            {
                if (pendingApprovalsByIdentity.TryGetValue(identityKey, out var tcs))
                {
                    McpLog.LogDelayed($"Completing pending approval for identity {identityKey}: {(approved ? "approved" : "denied")}");
                    tcs.TrySetResult(approved);
                    pendingApprovalsByIdentity.Remove(identityKey);
                }
                else
                {
                    McpLog.LogDelayed($"No pending approval found for identity {identityKey}");
                }
            }
        }

        public Bridge(bool autoScheduleStart = true)
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += _ => ScheduleInitRetry();
            McpToolRegistry.ToolsChanged += OnToolsChanged;

            // Subscribe to MCP session events from Relay for auto-approval
            RelayService.Instance.OnMcpSessionRegister += OnMcpSessionRegister;
            RelayService.Instance.OnMcpSessionUnregister += OnMcpSessionUnregister;

            // Catch any registrations that happened before we subscribed (race condition fix)
            // McpSessionBuffer subscribes via [InitializeOnLoadMethod] and buffers all registrations
            foreach (var registration in McpSessionBuffer.GetAll())
            {
                OnMcpSessionRegister(registration);
            }

            // Load validation configuration
            validationConfig = ValidatedConfigs.Unity;

            if (autoScheduleStart)
            {
                // Defer start until the editor is idle and not compiling
                ScheduleInitRetry();

                // Add a safety net update hook in case delayCall is missed during reload churn
                if (!ensureUpdateHooked)
                {
                    ensureUpdateHooked = true;
                    EditorApplication.update += EnsureStartedOnEditorIdle;
                }
            }
        }

        public void Start()
        {
            lock (startStopLock)
            {
                if (isRunning && listener != null)
                {
                    McpLog.Log($"UnityMCPBridge already running on {currentConnectionPath}");
                    return;
                }

                Stop();

                // Reload validation configuration (settings may have changed)
                validationConfig = ValidatedConfigs.Unity;

                try
                {
                    // Create platform-specific listener
                    listener = ConnectionFactory.CreateListener();

                    // Get connection path for this project
                    currentConnectionPath = ServerDiscovery.GetConnectionPath();

                    LogBreadcrumb("Start");

                    // Start listening
                    listener.Start(currentConnectionPath);

                    isRunning = true;
                    isBatchMode = Application.isBatchMode;
                    string connectionType = ConnectionFactory.GetConnectionTypeName();
                    string platform = Application.platform.ToString();
                    McpLog.Log($"MCP Bridge V2 started using {connectionType} at {currentConnectionPath} (OS={platform})");

                    // Once the listener is up, the bridge is functional. Any failure in the
                    // remaining steps (e.g. broken third-party Newtonsoft converters
                    // registered globally via JsonConvert.DefaultSettings) must not tear
                    // the bridge down — otherwise EnsureStartedOnEditorIdle will retry in
                    // a tight loop and spam the editor with errors.

                    // Save discovery file (written once; deleted on shutdown)
                    try { ServerDiscovery.SaveConnectionInfo(currentConnectionPath); }
                    catch (Exception ex) { Debug.LogWarning($"MCP Bridge: failed to save discovery file: {ex}"); }

                    // Pre-warm tools cache so handshake can include tools immediately
                    try { ComputeToolsSnapshotAndHash(); }
                    catch (Exception ex) { Debug.LogWarning($"MCP Bridge: failed to pre-warm tools cache: {ex}"); }

                    // Start background listener with cooperative cancellation
                    cts = new CancellationTokenSource();
                    listenerTask = Task.Run(() => ListenerLoopAsync(cts.Token));
                    EditorApplication.update += ProcessCommands;

                    // Capture the main-thread sync context so the background pump can
                    // marshal the drain back to the main thread (Unity APIs require it).
                    mainThreadContext = System.Threading.SynchronizationContext.Current;

                    // Drive the drain off a focus-independent timer so tool calls stay
                    // responsive when EditorApplication.update is throttled (editor unfocused).
                    commandPump?.Dispose();
                    commandPump = new MainThreadCommandPump(
                        hasPendingWork: () => isRunning && !commandQueue.IsEmpty,
                        requestDrain: RequestDrainOnMainThread,
                        intervalMs: k_PumpIntervalMs);
                    commandPump.Start();

                    // Ensure lifecycle events are (re)subscribed
                    try { AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload; } catch { }
                    try { AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload; } catch { }
                    try { AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload; } catch { }
                    try { AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload; } catch { }
                    try { EditorApplication.quitting -= Stop; } catch { }
                    try { EditorApplication.quitting += Stop; } catch { }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to start MCP Bridge: {ex.Message}");
                    isRunning = false;
                }
            }
        }

        public void Stop()
        {
            Task toWait = null;
            lock (startStopLock)
            {
                if (!isRunning)
                    return;

                try
                {
                    isRunning = false;

                    // Delete discovery files
                    ServerDiscovery.DeleteDiscoveryFiles();

                    // Clear all gateway connections (they're ephemeral and won't survive anyway)
                    ConnectionStore.ClearAllGatewayConnections();

                    // Quiesce background listener
                    var cancel = cts;
                    cts = null;
                    try { cancel?.Cancel(); } catch { }

                    try { listener?.Stop(); } catch { }
                    try { listener?.Dispose(); } catch { }
                    listener = null;

                    toWait = listenerTask;
                    listenerTask = null;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error stopping UnityMCPBridge: {ex.Message}");
                }
            }

            // Close all active clients (including displaced ones — their OS-level
            // sockets must be closed so the MCP server detects the disconnect)
            var toClose = TransportStore.Clear();
            ConnectionCensus.Clear();
            McpLog.ClearOnceKeys();
            foreach (var c in toClose)
            {
                try { c.Close(); c.Dispose(); } catch { }
            }

            // Unblock any pending command waiters since ProcessCommands won't run after Stop()
            foreach (var kvp in commandQueue.Values)
            {
                kvp.tcs.TrySetResult(JsonConvert.SerializeObject(new
                {
                    status = "error",
                    error = "Bridge stopped"
                }));
            }
            commandQueue.Clear();

            if (toWait != null)
            {
                // Wait for listener task to complete (increased timeout for slower CI machines)
                // The listener task should complete quickly after cancellation, but on Linux
                // the accept() call may take a moment to return after the socket is closed
                try { toWait.Wait(1000); } catch { }
            }

            try { commandPump?.Dispose(); } catch { }
            commandPump = null;
            mainThreadContext = null;
            try { EditorApplication.update -= ProcessCommands; } catch { }
            McpLog.Log("UnityMCPBridge stopped.");
        }

        public void Dispose()
        {
            try { Stop(); } catch { }
            try { EditorApplication.update -= EnsureStartedOnEditorIdle; } catch { }
            try { EditorApplication.update -= ProcessCommands; } catch { }
            try { EditorApplication.quitting -= Stop; } catch { }
            try { AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload; } catch { }
            try { AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload; } catch { }
            try { McpToolRegistry.ToolsChanged -= OnToolsChanged; } catch { }
            try { transportWriteLock.Dispose(); } catch { }
        }

        void ScheduleInitRetry()
        {
            if (initScheduled) return;
            initScheduled = true;
            nextStartAt = EditorApplication.timeSinceStartup + 0.20f;
            if (!ensureUpdateHooked)
            {
                ensureUpdateHooked = true;
                EditorApplication.update += EnsureStartedOnEditorIdle;
            }
            EditorTask.delayCall += InitializeAfterCompilation;
        }

        // Safety net: ensure the bridge starts shortly after domain reload when editor is idle
        void EnsureStartedOnEditorIdle()
        {
            // Do nothing while compiling
            if (IsCompiling()) return;

            // If already running, remove the hook
            if (isRunning)
            {
                EditorApplication.update -= EnsureStartedOnEditorIdle;
                ensureUpdateHooked = false;
                return;
            }
            // Debounced start: wait until the scheduled time
            if (nextStartAt > 0 && EditorApplication.timeSinceStartup < nextStartAt) return;
            if (isStarting) return;

            isStarting = true;
            // Attempt start; if it succeeds, remove the hook to avoid overhead
            try { Start(); }
            finally { isStarting = false; }

            if (isRunning)
            {
                EditorApplication.update -= EnsureStartedOnEditorIdle;
                ensureUpdateHooked = false;
            }
        }

        /// <summary>
        /// Initialize the MCP bridge after Unity is fully loaded and compilation is complete.
        /// This prevents repeated restarts during script compilation that cause port hopping.
        /// </summary>
        void InitializeAfterCompilation()
        {
            initScheduled = false;

            // Play-mode friendly: allow starting in play mode; only defer while compiling
            if (IsCompiling())
            {
                ScheduleInitRetry();
                return;
            }

            if (!isRunning)
            {
                Start();
                // If a race prevented start, retry later
                if (!isRunning) ScheduleInitRetry();
            }
        }

        async Task ListenerLoopAsync(CancellationToken token)
        {
            while (isRunning && !token.IsCancellationRequested)
            {
                try
                {
                    IConnectionTransport clientTransport = await listener.AcceptClientAsync(token);

                    // Capture peer PID immediately while the socket is still connected.
                    // Deferring this to the background thread risks ENOTCONN if the peer disconnects.
                    clientTransport.CacheClientProcessId();

                    // Fire diagnostic event immediately (not via delayCall)
                    // Event handlers can marshal to main thread if needed, but event fires synchronously
                    var connId = clientTransport.ConnectionId;
                    OnConnectionAttempt?.Invoke(connId);

                    // Fire and forget each client connection
                    _ = Task.Run(() => HandleClientAsync(clientTransport, token), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (isRunning && !token.IsCancellationRequested)
                    {
                        McpLog.LogDelayed($"Listener error: {ex.Message}", LogType.Error);
                    }
                }
            }
        }

        void SetApprovalState(IConnectionTransport transport, ConnectionApprovalState state)
        {
            var ts = TransportStore.GetState(transport);
            if (ts != null)
                ts.ApprovalState = state;
        }

        ConnectionApprovalState GetApprovalState(IConnectionTransport transport)
        {
            return TransportStore.GetState(transport)?.ApprovalState ?? ConnectionApprovalState.Unknown;
        }

        /// <summary>
        /// Apply any ClientInfo received via set_client_info (which may arrive before
        /// validation completes) to the ConnectionStore record.
        /// Called after RecordConnection so the record exists to update.
        /// </summary>
        void ApplyTransportClientInfo(IConnectionTransport transport, string identityKey)
        {
            var clientInfo = TransportStore.GetState(transport)?.ClientInfo;
            if (clientInfo != null)
            {
                ConnectionStore.UpdateClientInfo(identityKey, clientInfo);
            }
        }

        /// <summary>
        /// Send a duplicate_connection notification to a transport before closing it.
        /// The MCP server handles this as a non-retryable error and clears its tool cache.
        /// </summary>
        async Task SendDuplicateNotificationAsync(IConnectionTransport transport, string reason, CancellationToken ct)
        {
            try
            {
                string message = JsonConvert.SerializeObject(new
                {
                    type = "duplicate_connection",
                    reason
                });
                await WriteWithLockAsync(transport, message, ct);
            }
            catch (Exception ex)
            {
                McpLog.LogDelayed($"Failed to send duplicate notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Close all active direct (non-gateway) connections.
        /// Called when a gateway connection registers — gateway takes precedence.
        /// Sends a duplicate_connection notification so the relay treats this as non-retryable.
        /// Does NOT dispose the transports — the relay closes the pipe from its side after
        /// reading the notification, and HandleClientAsync's using block disposes naturally.
        /// Disposing here would RST the socket, destroying the buffered notification before
        /// the relay can read it, causing the relay to treat it as a retryable error and
        /// reconnect in a tight loop.
        /// </summary>
        // TODO: Deduplication disabled — direct connections coexist with gateway.
        // The current approach (close all direct on gateway connect) races with
        // reconnecting MCP servers: they reconnect after CloseDirectConnections
        // fires and end up as orphaned duplicates anyway. Needs a proper solution
        // (e.g., scope by identity key, or continuous enforcement).
        Task CloseDirectConnectionsAsync(CancellationToken ct) => Task.CompletedTask;

#if false // Disabled — see TODO above
        async Task CloseDirectConnectionsAsync_Dedup(CancellationToken ct)
        {
            var directTransports = TransportStore.GetDirectTransports();

            if (directTransports.Count == 0)
                return;

            McpLog.LogDelayed($"Closing {directTransports.Count} direct connection(s) in favor of gateway");

            foreach (var transport in directTransports)
            {
                try
                {
                    await SendDuplicateNotificationAsync(transport, "Gateway connection established for this editor", ct);
                }
                catch { }
            }
        }
#endif

        async Task HandleClientAsync(IConnectionTransport transport, CancellationToken token)
        {
            using (transport)
            {
                // Per-transport CTS: cancelled in finally when this client disconnects.
                // Linked with the listener token so it also cancels if the listener stops.
                // Used to cancel background validation/approval for this specific transport.
                using var transportCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                var transportToken = transportCts.Token;

                transport.OnDisconnected += () =>
                {
                    var state = TransportStore.GetState(transport);
                    if (state != null)
                    {
                        var identityKey = state.IdentityKey;
                        var record = ConnectionStore.GetConnectionByIdentity(identityKey);
                        var clientInfo = record?.Info?.ClientInfo ?? state.ClientInfo;
                        if (clientInfo != null)
                        {
                            string displayName = string.IsNullOrEmpty(clientInfo.Title) ? clientInfo.Name : clientInfo.Title;
                            McpLog.LogDelayed($"Client disconnected: {displayName} v{clientInfo.Version}");
                        }
                    }
                };

                Task heartbeatTask = Task.CompletedTask;
                try
                {
                    McpLog.LogDelayed($"Client connected: {transport.ConnectionId}");

                    // Track whether we can skip validation and go straight to handshake
                    bool skipValidation = false;
                    bool isGatewayFastPath = false;

                    // Read ACP token FIRST (before expensive validation)
                    // This allows gateway connections to skip validation entirely
                    string acpToken = await TryReadAcpTokenAsync(transport, token);
                    bool hasAcpToken = !string.IsNullOrEmpty(acpToken);

                    if (hasAcpToken)
                    {
                        McpLog.LogDelayed($"[ACP Token] Received approval token from client");

                        // Persist token for late-upgrade: if the relay session registration arrives
                        // after this connection (domain reload race), TryLateUpgradeToGateway can
                        // find this transport and upgrade it to gateway status.
                        // Note: TransportState not yet created — will be set after Register below.
                        // Store temporarily for the fast-path and approval branches.

                        var tokenResult = McpSessionTokenRegistry.ValidateAndConsume(acpToken);
                        if (tokenResult.IsValid)
                        {
                            var gatewayPolicy = MCPSettingsManager.Settings.connectionPolicies.gateway;

                            if (gatewayPolicy.allowed && !gatewayPolicy.requiresApproval)
                            {
                                // FAST PATH: Valid gateway + auto-approve policy
                                // Skip expensive validation (SHA256, signatures) since the ACP token authenticates this connection
                                McpLog.LogDelayed($"[Gateway Fast Path] Skipping validation for session: {tokenResult.SessionId}, provider: {tokenResult.Provider ?? "unknown"}");

                                // Collect process-tree info so the census can dedup multiple
                                // transports / sessions from the same agent (codex probes,
                                // claude using both gateway + direct MCP, etc.). The walk is
                                // capped at MaxParentChainDepth so the cost stays bounded.
                                ConnectionInfo gatewayConnectionInfo;
                                int? gatewayClientPid = transport.GetClientProcessId();
                                if (gatewayClientPid.HasValue && gatewayClientPid.Value > 0)
                                {
                                    try
                                    {
                                        gatewayConnectionInfo = ProcessInfoCollector.CollectConnectionInfo(gatewayClientPid.Value, ValidatedConfigs.Unity);
                                        gatewayConnectionInfo.ConnectionId = transport.ConnectionId;
                                    }
                                    catch (Exception ex)
                                    {
                                        McpLog.LogDelayed($"[Gateway Fast Path] Failed to collect process info: {ex.Message}", LogType.Warning);
                                        gatewayConnectionInfo = new ConnectionInfo
                                        {
                                            ConnectionId = transport.ConnectionId,
                                            Timestamp = DateTime.UtcNow,
                                            Server = new ProcessInfo
                                            {
                                                ProcessId = gatewayClientPid.Value,
                                                ProcessName = "gateway-connection"
                                            }
                                        };
                                    }
                                }
                                else
                                {
                                    gatewayConnectionInfo = new ConnectionInfo
                                    {
                                        ConnectionId = transport.ConnectionId,
                                        Timestamp = DateTime.UtcNow,
                                        Server = new ProcessInfo
                                        {
                                            ProcessId = 0,
                                            ProcessName = "gateway-connection"
                                        }
                                    };
                                }

                                // Enforce gateway + total caps on the actual agent's logical client.
                                // Race-safe: AcpSessionRegistry.Acquire already pre-checked
                                // by AcpSession count, but a second agent that started in the
                                // same window may have slipped past — confirm here before we
                                // commit the fast-path approval. The census dedupes so an
                                // existing logical client's gateway leg never spuriously fails.
                                var gatewayReservation = ConnectionCensus.TryReserveGateway(gatewayConnectionInfo);
                                if (!gatewayReservation.Allowed)
                                {
                                    string denialReason = BuildCapacityDenialReason(gatewayReservation, TierDenialKind.Gateway);
                                    McpLog.LogDelayed($"Gateway connection rejected: {denialReason}", LogType.Warning);
                                    try
                                    {
                                        string denialMsg = MessageProtocol.CreateApprovalDeniedMessage(denialReason);
                                        byte[] denialBytes = Encoding.UTF8.GetBytes(denialMsg);
                                        await transport.WriteAsync(denialBytes, token);
                                    }
                                    catch (Exception ex)
                                    {
                                        McpLog.LogDelayed($"Failed to send denial message: {ex.Message}", LogType.Warning);
                                    }
                                    return;
                                }
                                var logicalClientKey = gatewayReservation.ClientKey;

                                OnValidationComplete?.Invoke(transport.ConnectionId, ValidationStatus.Accepted);

                                var acceptedDecision = new ValidationDecision
                                {
                                    Status = ValidationStatus.Accepted,
                                    Reason = "Auto-approved via AI Gateway (fast path)",
                                    Connection = gatewayConnectionInfo
                                };

                                var sessionId = tokenResult.SessionId;
                                var provider = tokenResult.Provider;
                                ConnectionStore.RecordGatewayConnection(acceptedDecision, sessionId, provider);

                                ConnectionCensus.RegisterGatewayTransport(transport, gatewayConnectionInfo);

                                // Attach the existing AcpSession (created when Acquire ran) to
                                // the agent's real logical client so it joins the dedup pool.
                                if (!string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(logicalClientKey))
                                {
                                    ConnectionCensus.AttachAcpSessionToClient(
                                        new Unity.AI.Assistant.Data.AssistantConversationId(sessionId),
                                        logicalClientKey);
                                    ConnectionStore.SetGatewayConnectionLogicalClientKey(sessionId, logicalClientKey);
                                }

                                skipValidation = true;
                                isGatewayFastPath = true;
                                SetApprovalState(transport, ConnectionApprovalState.GatewayApproved);
                            }
                            else if (!gatewayPolicy.allowed)
                            {
                                McpLog.LogDelayed($"Connection rejected: Gateway connections not allowed by policy", LogType.Warning);
                                try
                                {
                                    string denialMsg = MessageProtocol.CreateApprovalDeniedMessage(
                                        "Gateway connections are not allowed by current policy");
                                    byte[] denialBytes = Encoding.UTF8.GetBytes(denialMsg);
                                    await transport.WriteAsync(denialBytes, token);
                                }
                                catch (Exception ex)
                                {
                                    McpLog.LogDelayed($"Failed to send denial message: {ex.Message}", LogType.Warning);
                                }
                                return;
                            }
                            else
                            {
                                // Gateway requires approval - token stored on TransportState after Register
                            }
                        }
                        else
                        {
                            // Invalid token - stored on TransportState after Register, fall through to full validation
                        }
                    }

                    // === EAGER HANDSHAKE ===
                    // Send handshake immediately - don't block on validation or approval.
                    // Validation and approval run in the background; tool calls are gated in ExecuteCommandAsync.
                    if (!skipValidation)
                    {
                        SetApprovalState(transport, ConnectionApprovalState.Unknown);
                    }

                    try
                    {
                        // Include pre-warmed tools in handshake to eliminate discovery round trips
                        await MessageProtocol.SendHandshakeAsync(transport, s_ToolsSnapshot, s_CurrentToolsHash);
                        McpLog.LogDelayed($"Sent handshake (unity-mcp protocol v2.0, tools={s_ToolsSnapshot?.Length ?? 0})");
                    }
                    catch (Exception ex)
                    {
                        // errno=32 (EPIPE) = client disconnected before handshake completed (benign race)
                        if (ex.Message.Contains("errno=32"))
                            McpLog.LogDelayed($"Handshake skipped: client disconnected");
                        else
                            McpLog.LogDelayed($"Handshake failed: {ex.Message}", LogType.Warning);
                        return;
                    }

                    // Register with temporary identity key (updated when validation completes)
                    string connectionIdentityKey = isGatewayFastPath
                        ? $"gateway-{transport.ConnectionId}"
                        : $"pending-{transport.ConnectionId}";

                    var transportState = TransportStore.Register(transport, connectionIdentityKey, isGateway: isGatewayFastPath);

                    // Store ACP token on transport state (deferred from above — state didn't exist yet)
                    if (hasAcpToken)
                    {
                        transportState.PersistentAcpToken = acpToken;
                        transportState.PendingAcpToken = acpToken;
                    }

                    // Dedup: if a gateway connection just registered, close existing direct connections
                    // Gateway takes precedence (auto-approval, session tracking, editor targeting)
                    if (isGatewayFastPath)
                    {
                        _ = Task.Run(() => CloseDirectConnectionsAsync(token));
                    }

                    // Notify listeners on main thread that a client connected
                    EditorTask.delayCall += () => OnClientConnectionChanged?.Invoke();

                    // Initialize per-connection analytics tracker
                    var sessionTracker = new McpSessionTracker();
                    transportSessionTrackers[transport] = sessionTracker;

                    // Analytics must be dispatched to the main thread (EditorAnalytics requirement)
                    // NOTE: For non-gateway (direct) connections, connectionIdentityKey is a provisional
                    // "pending-{id}" value here. SessionEnd uses the validated identity key from
                    // TransportStore, so Start/End SessionIds won't match for direct connections.
                    // Currently all production connections go through the relay (gateway), where the
                    // identity key is stable from the start. This will need fixing if cowork reuses
                    // this code path with direct connections that bypass the relay.
                    var startSessionId = connectionIdentityKey;
                    var totalConnections = TransportStore.CountConnections();
                    var directConnections = TransportStore.CountDirectConnections();
                    EditorTask.delayCall += () => AIAssistantAnalytics.ReportMcpSessionStartEvent(
                        startSessionId, totalConnections, directConnections);

                    // Launch background validation + approval (fire-and-forget)
                    // This runs concurrently with the message loop below.
                    // isBatchMode was captured on main thread in Start().
                    if (!skipValidation)
                    {
                        _ = ValidateAndApproveAsync(transport, transportToken, isBatchMode);
                    }

                    // Transport-level heartbeat — sends command_in_progress every 1.5s as a
                    // connection liveness signal. The MCP server (unity-connection.ts) skips
                    // these messages; they never reach external clients (Claude Code, Cursor).
                    // This lets both sides detect stale connections promptly, especially when
                    // long-running tool calls keep the main thread busy.
                    // Scoped to transportToken so it's cancelled when the transport disconnects.
                    heartbeatTask = Task.Run(async () =>
                    {
                        try
                        {
                            while (!transportToken.IsCancellationRequested)
                            {
                                await Task.Delay(1500, transportToken);
                                if (transport.IsConnected)
                                {
                                    string msg = MessageProtocol.CreateCommandInProgressMessage();
                                    await WriteWithLockAsync(transport, msg, transportToken);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        catch (OperationCanceledException) { /* transport disconnected */ }
                        catch { /* transport disposed — stop silently */ }
                    }, transportToken);

                    while (isRunning && !token.IsCancellationRequested && transport.IsConnected)
                    {
                        try
                        {
                            // Read and parse on the I/O thread (Command is a plain POCO — no Unity deps)
                            // No timeout — idle connections are normal (client sends commands sporadically).
                            // The loop exits when the pipe closes or the listener stops.
                            string commandText = await MessageProtocol.ReadMessageAsync(transport, timeoutMs: -1);
                            Command command;
                            try
                            {
                                command = JsonConvert.DeserializeObject<Command>(commandText);
                            }
                            catch
                            {
                                // Malformed JSON — respond with error directly, don't queue
                                string errorResponse = JsonConvert.SerializeObject(new
                                {
                                    status = "error",
                                    error = "Invalid JSON format",
                                    receivedText = commandText.Length > 50 ? commandText[..50] + "..." : commandText
                                });
                                await WriteWithLockAsync(transport, errorResponse, token);
                                continue;
                            }

                            if (command == null || string.IsNullOrEmpty(command.type))
                            {
                                string errorResponse = JsonConvert.SerializeObject(new
                                {
                                    status = "error",
                                    error = command == null ? "Command deserialized to null" : "Command type cannot be empty"
                                });
                                await WriteWithLockAsync(transport, errorResponse, token);
                                continue;
                            }

                            // Fast-path: respond to pings on the I/O thread
                            // without going through the main-thread command queue.
                            if (command.type.Equals("ping", StringComparison.OrdinalIgnoreCase))
                            {
                                string pingResponse = InjectRequestId(
                                    JsonConvert.SerializeObject(new { status = "success", result = new { message = "pong" } }),
                                    command.requestId);
                                await WriteWithLockAsync(transport, pingResponse, token);
                                continue;
                            }

                            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                            string commandId = Guid.NewGuid().ToString();

                            commandQueue[commandId] = (command, tcs, transport);

                            // Fire-and-forget: write response when ready (non-blocking).
                            // The reader loop continues immediately so multiple commands
                            // can be in-flight concurrently (multiplexed by requestId).
                            _ = WriteResponseWhenReadyAsync(tcs.Task, transport, token);
                        }
                        catch (Exception ex)
                        {
                            string msg = ex.Message ?? string.Empty;
                            bool isBenign = msg.Contains("closed", StringComparison.OrdinalIgnoreCase)
                                || msg.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                                || msg.Contains("errno=9", StringComparison.Ordinal)  // EBADF — fd closed while reading
                                || msg.Contains("errno=32", StringComparison.Ordinal) // EPIPE — broken pipe
                                || msg.Contains("errno=38", StringComparison.Ordinal) // ENOTSOCK — fd closed while writing
                                || ex is TimeoutException
                                || ex is ObjectDisposedException;

                            if (isBenign)
                                McpLog.LogDelayed($"Client handler: {msg}");
                            else
                                McpLog.LogDelayed($"Client handler error: {msg}", LogType.Error);
                            break;
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    McpLog.LogDelayed($"HandleClientAsync unhandled exception for {transport.ConnectionId}: {ex}", LogType.Error);
                }
                finally
                {
                    McpLog.LogDelayed($"HandleClientAsync exiting for transport {transport.ConnectionId} [isConnected={transport.IsConnected}]");

                    // Cancel background validation/approval and heartbeat for this transport
                    try { transportCts.Cancel(); } catch { /* best-effort */ }

                    // Wait briefly for heartbeat task to stop
                    try { await Task.WhenAny(heartbeatTask, Task.Delay(500)); } catch { /* best-effort */ }

                    // Remove all per-transport state (identity mappings, ACP tokens,
                    // approval state, validation decisions). TransportStore.Remove handles
                    // displaced transport restoration automatically.
                    var removedState = TransportStore.Remove(transport);
                    bool clientRemoved = removedState != null;

                    // Always unregister from the census so logical-client counts shrink
                    // immediately. Safe to call even if the transport was never registered.
                    ConnectionCensus.UnregisterTransport(transport);

                    // Note: we do NOT cancel or remove pendingApprovalsByIdentity TCS here.
                    // The approval dialog may still be open, and the user can approve/deny
                    // the identity for future connections. ValidateAndApproveAsync registers
                    // a continuation to process the result after it detects disconnection.

                    if (!transportSessionTrackers.TryRemove(transport, out var tracker) || tracker == null)
                    {
                        Trace.Debug("[Analytics] Session tracker was null after TryRemove — tracker may not have been initialized");
                    }
                    else
                    {
                        // Report session end analytics (dispatched to main thread)
                        tracker.SessionTimer.Stop();
                        var endSessionId = removedState?.IdentityKey ?? transport.ConnectionId;
                        var endClientName = tracker.ClientName ?? string.Empty;
                        var endDurationMs = tracker.SessionTimer.ElapsedMilliseconds;
                        var endToolCallCount = tracker.ToolCallCount;
                        var endLastToolName = tracker.LastToolName ?? string.Empty;
                        var endTimeToFirstSuccessMs = tracker.HadFirstSuccess == 1
                            ? tracker.TimeToFirstSuccess.ElapsedMilliseconds
                            : -1L;

                        EditorTask.delayCall += () => AIAssistantAnalytics.ReportMcpSessionEndEvent(
                            endSessionId,
                            endClientName,
                            endDurationMs,
                            endToolCallCount,
                            endLastToolName,
                            endTimeToFirstSuccessMs);
                    }

                    // Notify listeners on main thread that a client disconnected
                    if (clientRemoved)
                    {
                        EditorTask.delayCall += () => OnClientConnectionChanged?.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// Runs validation and approval in the background, concurrently with the message loop.
        /// Updates approval state so that ExecuteCommandAsync can gate tool calls.
        /// </summary>
        async Task ValidateAndApproveAsync(IConnectionTransport transport, CancellationToken token, bool isBatchMode)
        {
            try
            {
                SetApprovalState(transport, ConnectionApprovalState.Validating);

                ValidationDecision decision = null;

                // Run expensive validation (SHA256, signatures) on background thread
                if (validationConfig != null && validationConfig.Enabled && validationConfig.Mode != ValidationMode.Disabled)
                {
                    try
                    {
                        var validationStart = DateTime.Now;
                        decision = await Task.Run(() => ConnectionValidator.ValidateConnection(transport, validationConfig));
                        var validationMs = (DateTime.Now - validationStart).TotalMilliseconds;
                        McpLog.LogDelayed($"[TIMING] Validation took {validationMs:F0}ms");

                        // Exit early if transport disconnected during validation
                        token.ThrowIfCancellationRequested();

                        OnValidationComplete?.Invoke(transport.ConnectionId, decision.Status);
                        LogConnectionDecision(decision);

                        if (!decision.IsAccepted)
                        {
                            McpLog.LogDelayed($"Connection rejected: {decision.Reason}", LogType.Warning);
                            SetApprovalState(transport, ConnectionApprovalState.Denied);
                            return;
                        }

                        if (decision.Status == ValidationStatus.Warning)
                        {
                            McpLog.LogDelayed($"Connection allowed with warning: {decision.Reason}", LogType.Warning);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Connection closed during validation (e.g. brief probe) — nothing to do
                        return;
                    }
                    catch (Exception ex)
                    {
                        McpLog.LogDelayed($"Validation exception: {ex.Message}\n{ex.StackTrace}", LogType.Error);

                        if (validationConfig.Mode == ValidationMode.Strict)
                        {
                            SetApprovalState(transport, ConnectionApprovalState.Denied);
                            return;
                        }

                        McpLog.LogDelayed("Allowing connection despite validation error (LogOnly mode)", LogType.Warning);
                    }
                }
                else
                {
                    McpLog.LogDelayed(
                        "Connection validation is DISABLED - connections will not appear in MCP Settings UI. " +
                        "This should only be used for automated tests.",
                        LogType.Warning);
                    SetApprovalState(transport, ConnectionApprovalState.Approved);
                    return;
                }

                if (decision == null)
                {
                    SetApprovalState(transport, ConnectionApprovalState.Approved);
                    return;
                }

                // Store decision for dialog use
                var transportState = TransportStore.GetState(transport);
                if (transportState != null)
                    transportState.ValidationDecision = decision;

                // Update identity mapping with real identity
                var identity = ConnectionIdentity.FromConnectionInfo(decision.Connection);
                if (identity != null && !string.IsNullOrEmpty(identity.CombinedIdentityKey))
                {
                    TransportStore.UpdateIdentityKey(transport, identity.CombinedIdentityKey, isGateway: false);
                }

                // Exit early if transport disconnected during identity mapping
                token.ThrowIfCancellationRequested();

                // Client process not identifiable — likely a probe connection (e.g. Codex startup).
                // Wait briefly; if it disconnects, clean up silently without showing a dialog.
                if (decision.Connection.Client == null)
                {
                    McpLog.LogDelayed("Client process not identifiable — waiting to determine if probe");
                    try
                    {
                        await Task.Delay(5000, token);
                    }
                    catch (OperationCanceledException)
                    {
                        McpLog.LogDelayed("Probe connection disconnected during grace period — ignoring");
                        return;
                    }
                    McpLog.LogDelayed("Connection persists with unidentifiable client — proceeding with approval");
                }

                // Determine connection origin via stored ACP token
                string storedToken = transportState?.PendingAcpToken;
                if (transportState != null)
                    transportState.PendingAcpToken = null; // consumed

                var tokenResult = McpSessionTokenRegistry.ValidateAndConsume(storedToken);
                bool isGateway = tokenResult.IsValid;

                // Check if this transport was late-upgraded to gateway while validation was running.
                // TryLateUpgradeToGateway may fire concurrently when the relay session registration
                // arrives after the MCP server connected (domain reload race).
                if (GetApprovalState(transport) == ConnectionApprovalState.GatewayApproved)
                    return;

                // Note: direct connections are allowed to coexist with gateway connections.
                // Users may have an external CLI (e.g., Claude Code) with its own MCP server
                // alongside the AI Gateway's MCP server. Both need Unity access.
                // CloseDirectConnectionsAsync handles one-time cleanup when a gateway first
                // connects via the fast path; after that, new direct connections are accepted.

                var policy = isGateway
                    ? MCPSettingsManager.Settings.connectionPolicies.gateway
                    : MCPSettingsManager.Settings.connectionPolicies.direct;

                // Check if origin is allowed
                if (!policy.allowed)
                {
                    string origin = isGateway ? "Gateway" : "Direct MCP";
                    McpLog.LogDelayed($"Connection policy denied: {origin} connections not allowed", LogType.Warning);
                    SetApprovalState(transport, ConnectionApprovalState.Denied);
                    return;
                }

                // Enforce capacity limit (gateway exempt). The census dedupes multiple
                // transports from the same logical client (codex probes, etc.) so they
                // collapse to one slot, and tells us which cap was binding on denial
                // so we can surface a tier-aware message.
                if (!isGateway)
                {
                    var reservation = ConnectionCensus.TryReserveDirect(decision.Connection);
                    if (!reservation.Allowed)
                    {
                        decision.Status = ValidationStatus.CapacityLimit;
                        decision.Reason = BuildCapacityDenialReason(reservation, TierDenialKind.DirectMcp);
                        var capacityDecision = decision;
                        ConnectionStore.RecordConnection(capacityDecision);
                        ApplyTransportClientInfo(transport, identity.CombinedIdentityKey);
                        SetApprovalState(transport, ConnectionApprovalState.Denied);
                        return;
                    }

                    ConnectionCensus.RegisterDirectTransport(transport, decision.Connection);
                }

                // If approval not required, auto-approve
                if (!policy.requiresApproval)
                {
                    var decisionToRecord = decision;
                    ConnectionStore.RecordConnection(decisionToRecord);
                    ApplyTransportClientInfo(transport, identity.CombinedIdentityKey);
                    SetApprovalState(transport, ConnectionApprovalState.Approved);
                    return;
                }

                // Batch mode: auto-approve or deny based on setting (no UI available)
                if (isBatchMode)
                {
                    if (MCPSettingsManager.Settings.autoApproveInBatchMode)
                    {
                        McpLog.LogDelayed("Batch mode: auto-approving connection");
                        var decisionToRecord = decision;
                        ConnectionStore.RecordConnection(decisionToRecord);
                        ApplyTransportClientInfo(transport, identity.CombinedIdentityKey);
                        SetApprovalState(transport, ConnectionApprovalState.Approved);
                    }
                    else
                    {
                        McpLog.LogDelayed("Batch mode: auto-approve disabled, denying connection", LogType.Warning);
                        SetApprovalState(transport, ConnectionApprovalState.Denied);
                    }
                    return;
                }

                // Check existing approval history (exact identity match, then publisher fallback)
                var existingRecord = ConnectionStore.FindMatchingConnection(decision.Connection)
                    ?? ConnectionStore.FindMatchingConnectionByPublisher(decision.Connection);

                if (existingRecord != null &&
                    (existingRecord.Status == ValidationStatus.Accepted ||
                     existingRecord.Status == ValidationStatus.Warning ||
                     existingRecord.Status == ValidationStatus.CapacityLimit))
                {
                    McpLog.LogDelayed("Connection auto-approved: previously accepted by user");
                    var decisionToRecord = new ValidationDecision
                    {
                        Status = ValidationStatus.Accepted,
                        Reason = "Auto-approved: previously accepted",
                        Connection = decision.Connection
                    };
                    ConnectionStore.RecordConnection(decisionToRecord);
                    ApplyTransportClientInfo(transport, identity.CombinedIdentityKey);
                    SetApprovalState(transport, ConnectionApprovalState.Approved);
                    return;
                }

                if (existingRecord != null && existingRecord.Status == ValidationStatus.Rejected)
                {
                    McpLog.LogDelayed("Connection denied: previously rejected by user", LogType.Warning);
                    SetApprovalState(transport, ConnectionApprovalState.Denied);
                    return;
                }

                // Exit early if transport disconnected before recording Pending
                token.ThrowIfCancellationRequested();

                // New connection — needs user approval
                SetApprovalState(transport, ConnectionApprovalState.AwaitingApproval);

                // Record as Pending
                var pendingDecision = new ValidationDecision
                {
                    Status = ValidationStatus.Pending,
                    Reason = "Awaiting user approval",
                    Connection = decision.Connection
                };
                ConnectionStore.RecordConnection(pendingDecision);
                ApplyTransportClientInfo(transport, identity.CombinedIdentityKey);

                // Show dialog proactively
                string identityKey = identity?.CombinedIdentityKey;
                if (string.IsNullOrEmpty(identityKey))
                {
                    McpLog.LogDelayed("Unable to determine identity key for approval", LogType.Warning);
                    SetApprovalState(transport, ConnectionApprovalState.Denied);
                    return;
                }

                TaskCompletionSource<bool> approvalTcs;
                lock (pendingApprovalsLock)
                {
                    if (!pendingApprovalsByIdentity.TryGetValue(identityKey, out approvalTcs) ||
                        approvalTcs.Task.IsCompleted)
                    {
                        approvalTcs = new TaskCompletionSource<bool>();
                        pendingApprovalsByIdentity[identityKey] = approvalTcs;
                    }
                }

                // Check again — TryLateUpgradeToGateway may have fired concurrently
                if (GetApprovalState(transport) == ConnectionApprovalState.GatewayApproved)
                    return;

                // Show dialog on main thread
                ShowApprovalDialogForTransport(transport);

                // Await user decision or transport disconnection
                var cancellationTcs = new TaskCompletionSource<bool>();
                using var reg = token.Register(() => cancellationTcs.TrySetCanceled());

                var completedTask = await Task.WhenAny(approvalTcs.Task, cancellationTcs.Task);

                if (completedTask == cancellationTcs.Task || token.IsCancellationRequested)
                {
                    // Transport disconnected while awaiting approval.
                    // Don't cancel the TCS — the approval dialog may still be open and
                    // the user can approve/deny the identity for future connections.
                    McpLog.LogDelayed("Connection disconnected while awaiting approval");

                    // Update registry reason so settings UI shows the disconnection
                    var disconnectedRecord = ConnectionStore.FindMatchingConnection(identity);
                    if (disconnectedRecord != null && disconnectedRecord.Status == ValidationStatus.Pending)
                    {
                        ConnectionStore.UpdateConnectionStatus(
                            disconnectedRecord.Info.ConnectionId,
                            ValidationStatus.Pending,
                            "Client disconnected \u2014 approve to allow future connections from this client");
                    }

                    // Register continuation so the user's decision (from dialog or settings)
                    // is processed even though this method is returning.
                    _ = approvalTcs.Task.ContinueWith(task =>
                    {
                        if (!task.IsCompletedSuccessfully) return;
                        bool userApproved = task.Result;

                        lock (pendingApprovalsLock)
                        {
                            pendingApprovalsByIdentity.Remove(identityKey);
                        }

                        var record = ConnectionStore.FindMatchingConnection(identity);
                        if (record == null) return;

                        if (userApproved)
                        {
                            McpLog.Log("Connection approved by user (after disconnect)");
                            ConnectionStore.UpdateConnectionStatus(
                                record.Info.ConnectionId,
                                ValidationStatus.Accepted,
                                "Approved by user");
                        }
                        else
                        {
                            McpLog.Warning("Connection denied by user (after disconnect)");
                            ConnectionStore.UpdateConnectionStatus(
                                record.Info.ConnectionId,
                                ValidationStatus.Rejected,
                                "Denied by user");
                        }
                    }, TaskScheduler.Default);

                    return;
                }

                bool approved = await approvalTcs.Task; // Already completed, won't block

                lock (pendingApprovalsLock)
                {
                    pendingApprovalsByIdentity.Remove(identityKey);
                }

                if (approved)
                {
                    McpLog.LogDelayed("Connection approved by user");
                    SetApprovalState(transport, ConnectionApprovalState.Approved);

                    var approvedRecord = ConnectionStore.FindMatchingConnection(identity);
                    if (approvedRecord != null)
                    {
                        ConnectionStore.UpdateConnectionStatus(
                            approvedRecord.Info.ConnectionId,
                            ValidationStatus.Accepted,
                            "Approved by user");
                    }
                }
                else
                {
                    McpLog.LogDelayed("Connection denied by user", LogType.Warning);
                    SetApprovalState(transport, ConnectionApprovalState.Denied);

                    var rejectedRecord = ConnectionStore.FindMatchingConnection(identity);
                    if (rejectedRecord != null)
                    {
                        ConnectionStore.UpdateConnectionStatus(
                            rejectedRecord.Info.ConnectionId,
                            ValidationStatus.Rejected,
                            "Denied by user");
                    }
                    // Do NOT close the connection — tool calls will fail with error message
                }
            }
            catch (OperationCanceledException)
            {
                // Transport disconnected during validation — not an error
                McpLog.LogDelayed("Validation cancelled: client disconnected");
            }
            catch (Exception ex)
            {
                McpLog.LogDelayed($"Background validation/approval error: {ex.Message}", LogType.Error);
                SetApprovalState(transport, ConnectionApprovalState.Denied);
            }
        }

        /// <summary>
        /// Show the approval dialog for a transport that hasn't been approved yet.
        /// Called both proactively (after validation) and reactively (on tool call rejection).
        /// Can re-show the dialog if previously dismissed without a decision.
        /// </summary>
        void ShowApprovalDialogForTransport(IConnectionTransport transport)
        {
            var ts = TransportStore.GetState(transport);
            if (ts == null)
                return;

            // Can only show dialog if validation is complete
            var decision = ts.ValidationDecision;
            if (decision == null)
                return;

            if (ts.ApprovalState != ConnectionApprovalState.AwaitingApproval)
                return;

            var identity = ConnectionIdentity.FromConnectionInfo(decision.Connection);
            string identityKey = identity?.CombinedIdentityKey;
            if (string.IsNullOrEmpty(identityKey)) return;

            TaskCompletionSource<bool> approvalTcs;
            lock (pendingApprovalsLock)
            {
                if (!pendingApprovalsByIdentity.TryGetValue(identityKey, out approvalTcs) ||
                    approvalTcs.Task.IsCompleted)
                {
                    // Create new TCS if completed (e.g. dialog was dismissed and re-triggered)
                    approvalTcs = new TaskCompletionSource<bool>();
                    pendingApprovalsByIdentity[identityKey] = approvalTcs;
                }
            }

            var eventConnId = transport.ConnectionId;
            var tcs = approvalTcs;
            var decisionForDialog = decision;
            EditorTask.delayCall += () =>
            {
                try
                {
                    if (!tcs.Task.IsCompleted)
                    {
                        CurrentApprovalDialog = ConnectionApprovalDialog.ShowApprovalDialog(decisionForDialog, tcs);
                        OnDialogShown?.Invoke(eventConnId);
                    }
                }
                catch (Exception ex)
                {
                    McpLog.LogDelayed($"Error showing approval dialog: {ex.Message}", LogType.Error);
                    tcs.TrySetResult(false);
                }
            };
        }

        // Called from the pump's background timer thread. Marshals the queue drain
        // to the main thread and wakes the (possibly throttled) editor loop so it
        // runs promptly instead of at the unfocused tick rate.
        void RequestDrainOnMainThread()
        {
            var ctx = mainThreadContext;
            if (ctx == null)
                return;

            ctx.Post(_ =>
            {
                ProcessCommands();
                WakeEditorLoop();
            }, null);
        }

        // Forces a player-loop update so the editor ticks again promptly while unfocused.
        // Chosen via the wake-mechanism probe (macOS): QueuePlayerLoopUpdate gave the lowest
        // post→exec latency while unfocused (~154ms vs None ~238ms / RepaintAllViews ~202ms @100ms).
        static void WakeEditorLoop()
        {
            EditorApplication.QueuePlayerLoopUpdate();
        }

        void ProcessCommands()
        {
            if (!isRunning) return;
            // Reentrancy guard
            if (Interlocked.Exchange(ref processingCommands, 1) == 1) return;

            try
            {
                // Cache cleanup for completed commands (every 60 seconds)
                double now = EditorApplication.timeSinceStartup;
                if (now >= nextCacheCleanupAt)
                {
                    CleanExpiredCommandResults();
                    nextCacheCleanupAt = now + 60;
                }

                if (commandQueue.IsEmpty)
                    return;

                // Snapshot commands (already parsed on the I/O thread)
                var work = commandQueue.Select(kvp => (kvp.Key, kvp.Value.command, kvp.Value.tcs, kvp.Value.client)).ToList();

                foreach (var item in work)
                {
                    string id = item.Key;
                    Command command = item.command;
                    TaskCompletionSource<string> tcs = item.tcs;
                    IConnectionTransport client = item.client;

                    try
                    {
                        // Remove from queue BEFORE starting async execution to prevent
                        // re-processing on subsequent Update frames
                        commandQueue.TryRemove(id, out _);

                        // Deduplication: check if this requestId is already being processed or completed
                        if (!string.IsNullOrEmpty(command.requestId))
                        {
                            // Check for in-flight duplicate
                            if (inFlightCommands.TryGetValue(command.requestId, out var existingTask))
                            {
                                // Wait for existing task and return its result
                                _ = WaitForExistingAndComplete(existingTask, tcs, command.requestId);
                                continue;
                            }

                            // Check for completed duplicate
                            if (completedCommands.TryGetValue(command.requestId, out var cached) &&
                                cached.expiry > DateTime.UtcNow)
                            {
                                tcs.SetResult(InjectRequestId(cached.result, command.requestId));
                                continue;
                            }
                        }

                        // Start execution and track the result Task
                        Task<string> resultTask = ExecuteCommandAsync(command, client);

                        // Complete the TCS when execution finishes (bridges to background I/O thread).
                        // Inject requestId into the response so the multiplexed MCP client
                        // can route it to the correct pending promise.
                        string reqIdForResponse = command.requestId; // capture for closure
                        _ = resultTask.ContinueWith(t =>
                        {
                            string response;
                            if (t.IsFaulted)
                                response = JsonConvert.SerializeObject(new { status = "error", error = t.Exception?.InnerException?.Message ?? "Unknown error" });
                            else
                                response = t.Result;

                            tcs.SetResult(InjectRequestId(response, reqIdForResponse));
                        }, TaskScheduler.Default);

                        // Track by requestId for deduplication
                        if (!string.IsNullOrEmpty(command.requestId))
                        {
                            inFlightCommands[command.requestId] = resultTask;

                            // When complete, move to cache (fire-and-forget continuation)
                            string reqId = command.requestId; // Capture for closure
                            _ = resultTask.ContinueWith(t =>
                            {
                                inFlightCommands.TryRemove(reqId, out _);
                                if (t.IsCompletedSuccessfully)
                                {
                                    completedCommands[reqId] = (t.Result, DateTime.UtcNow + ResultCacheDuration);
                                }
                            }, TaskScheduler.Default);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error processing command: {ex.Message}\\n{ex.StackTrace}");
                        tcs.SetResult(JsonConvert.SerializeObject(new
                        {
                            status = "error",
                            error = ex.Message,
                            commandType = command.type ?? "Unknown"
                        }));
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref processingCommands, 0);
            }
        }

        // ========================================================================
        // Multiplexed protocol helpers
        // ========================================================================

        /// <summary>
        /// Inject requestId into a JSON response string for multiplexed routing.
        /// If requestId is null/empty or the response isn't valid JSON, returns the response as-is.
        /// </summary>
        static string InjectRequestId(string response, string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
                return response;

            try
            {
                var jobj = JObject.Parse(response);
                jobj["requestId"] = requestId;
                return jobj.ToString(Formatting.None);
            }
            catch
            {
                return response;
            }
        }

        /// <summary>
        /// Awaits a response task and writes the result to the transport with write serialization.
        /// Used by the listener loop to fire-and-forget response writes.
        /// </summary>
        async Task WriteResponseWhenReadyAsync(Task<string> responseTask, IConnectionTransport transport, CancellationToken ct)
        {
            try
            {
                string response = await responseTask.ConfigureAwait(false);
                await WriteWithLockAsync(transport, response, ct);
            }
            catch (OperationCanceledException) { /* connection closed */ }
            catch (Exception ex)
            {
                McpLog.LogDelayed($"Failed to write response: {ex.Message}");
            }
        }

        /// <summary>
        /// Write a message to the transport, serialized with the write lock to prevent
        /// interleaved writes from concurrent responses and heartbeats.
        /// </summary>
        async Task WriteWithLockAsync(IConnectionTransport transport, string message, CancellationToken ct = default)
        {
            await transportWriteLock.WaitAsync(ct);
            try
            {
                await MessageProtocol.WriteMessageAsync(transport, message, ct);
            }
            finally
            {
                transportWriteLock.Release();
            }
        }

        async Task<string> ExecuteCommandAsync(Command command, IConnectionTransport client, CancellationToken cancellationToken = default)
        {
            string toolSessionId = null;
            string toolClientName = null;
            McpSessionTracker sessionTracker = null;
            Stopwatch toolCallTimer = null;

            try
            {
                if (string.IsNullOrEmpty(command.type))
                {
                    return JsonConvert.SerializeObject(new
                    {
                        status = "error",
                        error = "Command type cannot be empty"
                    });
                }

                if (command.type.Equals("set_client_info", StringComparison.OrdinalIgnoreCase))
                {
                    string name = command.@params?.Value<string>("name") ?? "unknown";
                    string version = command.@params?.Value<string>("version") ?? "unknown";
                    string title = command.@params?.Value<string>("title");

                    var clientInfo = new ClientInfo
                    {
                        Name = name,
                        Version = version,
                        Title = title,
                        ConnectionId = client.ConnectionId
                    };

                    // Store on TransportState (survives the pending→real identity transition)
                    var clientState = TransportStore.GetState(client);
                    string identityKey = null;
                    string oldClientName = null;
                    if (clientState != null)
                    {
                        clientState.ClientInfo = clientInfo;
                        identityKey = clientState.IdentityKey;
                        // Also try to update ConnectionStore — may fail if identity is still pending
                        ConnectionStore.UpdateClientInfo(clientState.IdentityKey, clientInfo);
                    }

                    if (transportSessionTrackers.TryGetValue(client, out var tracker))
                    {
                        oldClientName = tracker.ClientName;
                        tracker.ClientName = name;
                    }

                    var infoSessionId = identityKey ?? client.ConnectionId;
                    var infoName = name;
                    var infoVersion = version;
                    var infoOldClientName = oldClientName ?? string.Empty;
                    EditorTask.delayCall += () => AIAssistantAnalytics.ReportMcpClientInfoReceivedEvent(
                        infoSessionId, infoName, infoVersion, infoOldClientName);

                    string displayName = string.IsNullOrEmpty(title) ? name : title;
                    McpLog.Log($"MCP client info: {displayName} v{version}");

                    return JsonConvert.SerializeObject(new
                    {
                        status = "success",
                        result = new { message = "Client info received" }
                    });
                }

                if (command.type.Equals("get_available_tools", StringComparison.OrdinalIgnoreCase))
                {
                    string requestedHash = command.@params?.Value<string>("hash");

                    // Recompute when hash is missing or differs from request
                    if (string.IsNullOrEmpty(s_CurrentToolsHash) || s_CurrentToolsHash != requestedHash)
                    {
                        ComputeToolsSnapshotAndHash();
                        McpLog.Log($"Tools changed: hash={s_CurrentToolsHash}, count={s_ToolsSnapshot?.Length ?? 0}");
                    }
                    // No logging for unchanged case - it's periodic polling noise

                    if (requestedHash == s_CurrentToolsHash)
                    {
                        return JsonConvert.SerializeObject(new
                        {
                            status = "success",
                            result = new { unchanged = true, hash = s_CurrentToolsHash }
                        });
                    }

                    var response = JsonConvert.SerializeObject(new
                    {
                        status = "success",
                        result = new
                        {
                            hash = s_CurrentToolsHash,
                            tools = s_ToolsSnapshot ?? Array.Empty<McpToolInfo>()
                        }
                    });
                    McpLog.Log($"Sending tools response with {s_ToolsSnapshot?.Length ?? 0} tools");
                    return response;
                }

                // Handle MCP tool approval requests (from Codex via MCP)
                if (command.type.Equals("mcp/request_tool_approval", StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleMcpToolApprovalAsync(command);
                }

                // Approval gate — accept-by-default policy.
                // All commands above (set_client_info, get_available_tools,
                // mcp/request_tool_approval, ping) are exempt.
                // Tool calls are allowed in all states EXCEPT Denied (user explicitly revoked).
                var approvalState = GetApprovalState(client);
                if (approvalState == ConnectionApprovalState.Denied)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        status = "error",
                        error = "Connection revoked. Go to Unity Editor > Project Settings > AI > Unity MCP to change approval.",
                        isError = true
                    });
                }

                // Use JObject for parameters as the handlers expect this
                JObject paramsObject = command.@params ?? new JObject();

                // Gather analytics state before the async tool call
                var toolClientState = TransportStore.GetState(client);
                toolSessionId = toolClientState?.IdentityKey;
                if (transportSessionTrackers.TryGetValue(client, out sessionTracker))
                    toolClientName = sessionTracker.ClientName ?? string.Empty;

                toolSessionId ??= client.ConnectionId;

                toolCallTimer = Stopwatch.StartNew();
                var result = await McpToolRegistry.ExecuteToolAsync(command.type, paramsObject);
                toolCallTimer.Stop();

                if (result == null)
                    result = Response.Success("Operation completed.");

                // Update session tracker fields atomically (accessed from concurrent tool calls)
                if (sessionTracker != null)
                {
                    Interlocked.Increment(ref sessionTracker.ToolCallCount);
                    sessionTracker.LastToolName = command.type;

                    if (Interlocked.CompareExchange(ref sessionTracker.HadFirstSuccess, 1, 0) == 0)
                    {
                        sessionTracker.TimeToFirstSuccess.Stop();
                    }
                }

                var toolLatencyMs = toolCallTimer.ElapsedMilliseconds;
                var toolName = command.type;
                EditorTask.delayCall += () => AIAssistantAnalytics.ReportMcpToolCallCompletedEvent(
                    toolSessionId, toolClientName, toolName,
                    success: true, errorType: null, errorMessage: null,
                    toolLatencyMs);

                // Standard success response format
                return JsonConvert.SerializeObject(new { status = "success", result });
            }
            catch (Exception ex)
            {
                if (sessionTracker != null)
                {
                    Interlocked.Increment(ref sessionTracker.ToolCallCount);
                    sessionTracker.LastToolName = command.type;
                }

                var failToolName = command?.type ?? "Unknown";
                var failErrorType = ex.GetType().Name;
                EditorTask.delayCall += () => AIAssistantAnalytics.ReportMcpToolCallCompletedEvent(
                    toolSessionId, toolClientName, failToolName,
                    success: false, errorType: failErrorType, errorMessage: null,
                    toolCallTimer?.ElapsedMilliseconds ?? 0);

                Debug.LogError($"Error executing command '{command?.type ?? "Unknown"}': {ex.Message}\n{ex.StackTrace}");
                return JsonConvert.SerializeObject(new
                {
                    status = "error",
                    error = ex.Message,
                    command = command?.type ?? "Unknown"
                });
            }
        }

        /// <summary>
        /// Handle MCP tool approval requests from Codex.
        /// Routes through McpToolApprovalHandler for permission UI, or auto-approves.
        /// </summary>
        async Task<string> HandleMcpToolApprovalAsync(Command command)
        {
            var token = command.@params?.Value<string>("token");
            var toolName = command.@params?.Value<string>("toolName");
            var args = command.@params?["args"]?.ToString() ?? "{}";
            var toolCallId = command.@params?.Value<string>("toolCallId") ?? Guid.NewGuid().ToString();

            McpLog.Log($"[MCP Approval] Received tool approval request: {toolName}");

            // Look up session by token
            var sessionInfo = McpSessionTokenRegistry.FindByMcpToken(token);
            if (!sessionInfo.HasValue)
            {
                // No valid session - auto-approve (standalone MCP connection or expired token)
                McpLog.Log($"[MCP Approval] No valid session for token - auto-approving: {toolName}");
                return JsonConvert.SerializeObject(new
                {
                    status = "success",
                    result = new { approved = true, reason = "No active session (auto-approved)" }
                });
            }

            var (sessionId, provider) = sessionInfo.Value;
            McpLog.Log($"[MCP Approval] Session found: {sessionId} (provider: {provider})");

            try
            {
                // Route to the approval handler
                var request = new McpToolApprovalRequest(sessionId, provider, toolName, args, toolCallId);
                var response = await McpToolApprovalHandler.RequestApprovalAsync(request);

                McpLog.Log($"[MCP Approval] Tool {toolName}: {(response.Approved ? "approved" : "rejected")} - {response.Reason}");

                return JsonConvert.SerializeObject(new
                {
                    status = "success",
                    result = new { approved = response.Approved, reason = response.Reason, alwaysAllow = response.AlwaysAllow }
                });
            }
            catch (Exception ex)
            {
                McpLog.Warning($"[MCP Approval] Error processing approval for {toolName}: {ex.Message}");
                return JsonConvert.SerializeObject(new
                {
                    status = "success",
                    result = new { approved = false, reason = $"Approval error: {ex.Message}" }
                });
            }
        }

        void OnBeforeAssemblyReload()
        {
            // Stop cleanly before reload
            try { Stop(); } catch { }
            // Avoid file I/O or heavy work here
        }

        void OnAfterAssemblyReload()
        {
            LogBreadcrumb("Idle");
            // Schedule a safe restart after reload to avoid races during compilation
            ScheduleInitRetry();
        }

        void OnToolsChanged(McpToolRegistry.ToolChangeEventArgs args)
        {
            // Notify connected clients about tool changes
            // This allows MCP clients to refresh their tool list without reconnecting
            if (isRunning && args != null)
            {
                var changeType = args.ChangeType.ToString().ToLowerInvariant();
                var message = args.ChangeType == McpToolRegistry.ToolChangeType.Refreshed
                    ? "Tools were refreshed"
                    : $"Tool '{args.ToolName}' was {changeType}";
                McpLog.Log($"[UnityMCPBridge] {message}");
            }
            // Invalidate cached tools hash/snapshot so it will be recomputed on next request
            s_CurrentToolsHash = null;
            s_ToolsSnapshot = null;
        }

        public void InvalidateToolsCache()
        {
            s_CurrentToolsHash = null;
            s_ToolsSnapshot = null;
        }

        /// <summary>
        /// Wait for an existing in-flight task and complete the TCS with its result.
        /// Used for command deduplication when a duplicate requestId is detected.
        /// </summary>
        async Task WaitForExistingAndComplete(Task<string> existingTask, TaskCompletionSource<string> tcs, string requestId)
        {
            try
            {
                string result = await existingTask;
                tcs.SetResult(InjectRequestId(result, requestId));
            }
            catch (Exception ex)
            {
                McpLog.LogDelayed($"Error waiting for existing command {requestId}: {ex.Message}");
                tcs.SetResult(InjectRequestId(JsonConvert.SerializeObject(new
                {
                    status = "error",
                    error = $"Deduplication error: {ex.Message}"
                }), requestId));
            }
        }

        /// <summary>
        /// Clean expired entries from the completed commands cache.
        /// </summary>
        void CleanExpiredCommandResults()
        {
            var expiredKeys = completedCommands
                .Where(kvp => kvp.Value.expiry <= DateTime.UtcNow)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                completedCommands.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                McpLog.Log($"[Command Cache] Cleaned {expiredKeys.Count} expired entries");
            }
        }

        void ComputeToolsSnapshotAndHash()
        {
            s_ToolsSnapshot = McpToolRegistry.GetAvailableTools();
            var tools = s_ToolsSnapshot ?? Array.Empty<McpToolInfo>();
            var minimal = new object[tools.Length];
            for (int i = 0; i < tools.Length; i++)
            {
                minimal[i] = new { tools[i].name, tools[i].description, tools[i].inputSchema };
            }
            var json = JsonConvert.SerializeObject(minimal, Formatting.None);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
            s_CurrentToolsHash = Convert.ToBase64String(hashBytes);
        }

        void LogConnectionDecision(ValidationDecision decision)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Connection Info ===");

            // Server info
            sb.AppendLine("MCP Server:");
            var server = decision.Connection?.Server;
            if (server != null)
            {
                sb.AppendLine($"  PID: {server.ProcessId}");
                sb.AppendLine($"  Name: {server.ProcessName ?? "unknown"}");
                sb.AppendLine($"  Executable: {server.Identity?.Path ?? "unknown"}");
                if (server.Identity != null)
                {
                    sb.AppendLine($"  Hash: {server.Identity.SHA256Hash?.Substring(0, 16) ?? "unknown"}...");
                    sb.AppendLine($"  Signed: {(server.Identity.IsSigned ? "Yes" : "No")}");
                    if (server.Identity.IsSigned)
                    {
                        sb.AppendLine($"  Publisher: {server.Identity.SignaturePublisher ?? "unknown"}");
                        sb.AppendLine($"  Signature Valid: {(server.Identity.SignatureValid ? "Yes" : "No")}");
                    }
                }
            }
            else
            {
                sb.AppendLine("  Unable to determine server info");
            }

            // Validation status
            sb.AppendLine($"  Validation: {decision.Status}");
            sb.AppendLine($"  Reason: {decision.Reason}");

            // Client info
            sb.AppendLine("MCP Client:");
            var client = decision.Connection?.Client;
            if (client != null)
            {
                sb.AppendLine($"  Name: {client.ProcessName ?? "unknown"}");
                sb.AppendLine($"  PID: {client.ProcessId}");
                sb.AppendLine($"  Executable: {client.Identity?.Path ?? "unknown"}");
                if (decision.Connection.ClientChainDepth > 0)
                {
                    sb.AppendLine($"  Chain depth: {decision.Connection.ClientChainDepth} (walked up {decision.Connection.ClientChainDepth} level{(decision.Connection.ClientChainDepth == 1 ? "" : "s")})");
                }
            }
            else
            {
                sb.AppendLine("  Unable to determine (parent may have exited or permissions denied)");
            }

            sb.Append("======================");

            // Use LogDelayed since this is called from HandleClientAsync background thread
            McpLog.LogDelayed(sb.ToString());
        }

        static bool IsCompiling()
        {
            if (EditorApplication.isCompiling) return true;
            try
            {
                Type pipeline = Type.GetType("UnityEditor.Compilation.CompilationPipeline, UnityEditor");
                var prop = pipeline?.GetProperty("isCompiling", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null) return (bool)prop.GetValue(null);
            }
            catch { }
            return false;
        }

        static void LogBreadcrumb(string stage) => McpLog.Log($"[{stage}]");

        /// <summary>
        /// Handle MCP session token registration from Relay.
        /// Called when an AI Gateway session starts and pre-registers a token for auto-approval.
        /// Also checks for late-upgrade: if an MCP server already connected with this token
        /// (domain reload race — server reconnects before relay re-registers the session),
        /// upgrades the existing connection to gateway status.
        /// </summary>
        void OnMcpSessionRegister(McpSessionRegistration registration)
        {
            McpSessionTokenRegistry.RegisterSession(registration);
            TryLateUpgradeToGateway(registration);
        }

        /// <summary>
        /// Upgrade an existing direct connection to gateway status when the relay session
        /// registration arrives after the MCP server has already connected.
        ///
        /// Domain reload race condition:
        ///   1. Domain reload clears McpSessionTokenRegistry (static, in-memory)
        ///   2. MCP server reconnects and sends ACP token → token not found → classified as direct
        ///   3. ~700ms later, relay sends mcp.session.register → token registered here
        ///   4. This method finds the transport that sent that token and upgrades it to gateway
        /// </summary>
        void TryLateUpgradeToGateway(McpSessionRegistration registration)
        {
            var matchingTransport = TransportStore.FindTransportByAcpToken(registration.Token);
            if (matchingTransport == null)
                return;

            // Don't upgrade if already gateway
            var currentState = GetApprovalState(matchingTransport);
            if (currentState == ConnectionApprovalState.GatewayApproved)
                return;

            // Validate token (should succeed now that it's registered)
            var tokenResult = McpSessionTokenRegistry.ValidateAndConsume(registration.Token);
            if (!tokenResult.IsValid)
                return;

            var gatewayPolicy = MCPSettingsManager.Settings.connectionPolicies.gateway;
            if (!gatewayPolicy.allowed || gatewayPolicy.requiresApproval)
            {
                McpLog.LogDelayed($"[Late Gateway Upgrade] Skipped: gateway policy does not allow auto-approve");
                return;
            }

            McpLog.LogDelayed($"[Late Gateway Upgrade] Upgrading connection to gateway: session={tokenResult.SessionId}, provider={tokenResult.Provider ?? "unknown"}");

            // Upgrade approval state
            SetApprovalState(matchingTransport, ConnectionApprovalState.GatewayApproved);

            // Mark identity key as gateway (exempt from capacity limit)
            var matchingState = TransportStore.GetState(matchingTransport);
            if (matchingState != null)
            {
                matchingState.IsGateway = true;
                TransportStore.MarkAsGateway(matchingState.IdentityKey);
            }

            // Use the validation-supplied connection info if we have it (so the census
            // dedup uses the real ancestor PID / executable), otherwise fall back to a
            // minimal record.
            var validationInfo = matchingState?.ValidationDecision?.Connection;
            var connectionInfo = validationInfo ?? new ConnectionInfo
            {
                ConnectionId = matchingTransport.ConnectionId,
                Timestamp = DateTime.UtcNow,
                Server = new ProcessInfo
                {
                    ProcessId = matchingTransport.GetClientProcessId() ?? 0,
                    ProcessName = "gateway-connection"
                }
            };
            var acceptedDecision = new ValidationDecision
            {
                Status = ValidationStatus.Accepted,
                Reason = "Auto-approved via AI Gateway (late upgrade after domain reload)",
                Connection = connectionInfo
            };
            ConnectionStore.RecordGatewayConnection(acceptedDecision, tokenResult.SessionId, tokenResult.Provider);

            // Move the transport from the direct pool to the gateway pool in the census.
            // Direct enforcement may have already counted this transport — re-registering
            // as gateway (after unregistering) keeps the pool counts accurate.
            ConnectionCensus.UnregisterTransport(matchingTransport);
            ConnectionCensus.RegisterGatewayTransport(matchingTransport, connectionInfo);

            if (!string.IsNullOrEmpty(tokenResult.SessionId))
            {
                var clientKey = ConnectionCensus.ResolveClientKey(connectionInfo);
                if (!string.IsNullOrEmpty(clientKey))
                {
                    ConnectionCensus.AttachAcpSessionToClient(
                        new Unity.AI.Assistant.Data.AssistantConversationId(tokenResult.SessionId),
                        clientKey);
                    ConnectionStore.SetGatewayConnectionLogicalClientKey(tokenResult.SessionId, clientKey);
                }
            }

            // Don't close existing direct connections — they may be from an external CLI
            // (e.g., Claude Code running outside Unity) that needs Unity access alongside
            // the gateway. CloseDirectConnectionsAsync only runs on the initial gateway
            // fast-path connection, not on late upgrades.

            // Notify UI
            EditorTask.delayCall += () => OnClientConnectionChanged?.Invoke();
        }

        /// <summary>
        /// Handle MCP session token unregistration from Relay.
        /// Called when an AI Gateway session ends.
        /// </summary>
        void OnMcpSessionUnregister(string sessionId)
        {
            McpSessionTokenRegistry.UnregisterSession(sessionId);

            // Also clean up any gateway connections for this session
            // This removes them from the non-persisted list (for UI cleanup)
            ConnectionStore.RemoveGatewayConnectionsForSession(sessionId);
        }

        /// <summary>
        /// Try to read an ACP token message from the client with a short timeout.
        /// MCP clients from AI Gateway send this token immediately after connecting.
        /// Returns the token if present, null otherwise.
        /// </summary>
        async Task<string> TryReadAcpTokenAsync(IConnectionTransport transport, CancellationToken ct)
        {
            try
            {
                // Try to read a newline-delimited message with a short timeout
                // If no token is sent within timeout, proceed with normal flow
                const byte newlineDelimiter = 0x0A; // '\n'
                const int maxBytes = 1024;
                const int timeoutMs = 100; // Short timeout - token should arrive quickly

                var messageData = await transport.ReadUntilDelimiterAsync(newlineDelimiter, maxBytes, timeoutMs, ct);
                if (messageData == null || messageData.Length == 0)
                {
                    return null;
                }

                var messageText = Encoding.UTF8.GetString(messageData).Trim();

                // Try to parse as JSON
                try
                {
                    var message = JObject.Parse(messageText);
                    var type = message.Value<string>("type");

                    if (type == "set_acp_token")
                    {
                        var paramsObj = message["params"] as JObject;
                        return paramsObj?.Value<string>("token");
                    }
                }
                catch (JsonException)
                {
                    // Not valid JSON, ignore
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                // Timeout - no token sent, proceed with normal flow
                return null;
            }
            catch (TimeoutException)
            {
                // Timeout - no token sent, proceed with normal flow
                return null;
            }
            catch (Exception ex)
            {
                McpLog.LogDelayed($"[ACP Token] Error reading token: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get information about currently connected clients
        /// </summary>
        public ClientInfo[] GetConnectedClients()
        {
            return TransportStore.GetActiveTransportStates()
                .Select(ts => ts.ClientInfo)
                .Where(ci => ci != null)
                .ToArray();
        }

        /// <summary>
        /// Get the count of currently connected clients
        /// </summary>
        public int GetConnectedClientCount()
        {
            return TransportStore.CountConnections();
        }

        /// <summary>
        /// Build a tier-aware denial message from a failed census reservation.
        /// Copy is owned by <see cref="TierDenial"/> so the Project Settings
        /// row, the Bridge rejection log, and the AI Gateway session banner
        /// (<see cref="GatewayCapReachedException"/>) all speak in one voice.
        /// </summary>
        static string BuildCapacityDenialReason(ReservationResult reservation, TierDenialKind kind) =>
            TierDenial.BuildMessage(kind, reservation.PoolCount, reservation.PoolCap);
    }
}
