using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Tracing;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.Utils;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Trace = Unity.AI.Tracing.Trace;

namespace Unity.Relay.Editor
{

    /// <summary>
    /// Status of the relay service lifecycle.
    /// </summary>
    enum RelayStatus
    {
        /// <summary>Initial state, no relay process exists.</summary>
        NotStarted,
        /// <summary>Finding port, launching process.</summary>
        Starting,
        /// <summary>Process running, WebSocket connecting.</summary>
        Connecting,
        /// <summary>Fully operational.</summary>
        Running,
        /// <summary>Stop requested, cleanup in progress.</summary>
        Stopping,
        /// <summary>Error state with diagnostic info.</summary>
        Failed,
        /// <summary>Clean shutdown complete.</summary>
        Stopped
    }

    /// <summary>
    /// Immutable snapshot of relay state.
    /// </summary>
    record RelaySnapshot(
        RelayStatus Status,
        int Port = 0,
        int ProcessId = 0,
        string ErrorMessage = null,
        DateTime LastStateChange = default
    );

    /// <summary>
    /// Delegate for starting a relay process (for testing/custom scenarios).
    /// </summary>
    delegate Process RelayStartDelegate(int port, int mcpClientPort, int editorPid, int shutdownDelaySeconds);

    /// <summary>
    /// Unified service for relay lifecycle management.
    /// Thread-safe, survives domain reloads.
    /// </summary>
    class RelayService : IRelayConnection
    {
        const int k_StartPort = 9001;
        const int k_MaxPort = 9100;
        const int k_AutoShutdownDelaySeconds = 180;
        const float k_ReconnectIntervalSeconds = 5.0f;
        const int k_DefaultTimeoutSeconds = 30;
        const int k_HealthCheckTimeoutMs = 100;
        const int k_VersionValidationTimeoutMs = 2000;
        const int k_PortScanCount = 10;
        const string k_RelayPortPrefix = "RELAY-PORT";
        const string k_McpClientPortPrefix = "MCP-CLIENT-PORT";
        const string k_RelayProcessIdPrefix = "RELAY-PID";

        static readonly string k_RelayPath = Path.GetFullPath("Packages/com.unity.ai.assistant/RelayApp~");

        /// <summary>
        /// Builds the CLI arguments for starting the relay server.
        /// </summary>
        /// <param name="port">WebSocket port</param>
        /// <param name="mcpClientPort">MCP client REST API port</param>
        /// <param name="editorPid">Unity Editor process ID</param>
        /// <param name="shutdownDelaySeconds">Auto-shutdown delay in seconds</param>
        /// <returns>Formatted CLI arguments string</returns>
        public static string BuildRelayArguments(int port, int mcpClientPort, int editorPid, int shutdownDelaySeconds)
        {
            var args = $"--relay --port {port} --mcp-client-port {mcpClientPort} --editor-pid {editorPid} --shutdown-delay {shutdownDelaySeconds}";

            // Pass the centralized log directory so relay and Unity write to the same location
            var logDir = TraceLogDir.LogDir;
            args += $" --log-dir \"{logDir}\"";

            return args;
        }

        /// <summary>
        /// Apply the environment every relay process needs, whichever path starts it.
        /// Each start path owns its own <see cref="ProcessStartInfo"/>, so both must call
        /// this; keeping it here means the variables and the reason for them live in one
        /// place.
        /// </summary>
        /// <param name="startInfo">Start info for the relay process, mutated in place.</param>
        public static void ApplyRelayEnvironment(ProcessStartInfo startInfo)
        {
            // Trust the OS certificate store. Without this the relay fails the cloud TLS
            // handshake behind a TLS-inspection proxy, which re-signs traffic with a
            // private root that exists only in that store (UUM-142529).
            startInfo.Environment["NODE_USE_SYSTEM_CA"] = "1";
        }

        static RelayService s_Instance;
        static readonly object s_InstanceLock = new();

        static bool s_AutoStartSuppressed;

        [InitializeOnLoadMethod]
        static void AutoStart()
        {
            // Defer one tick so other [InitializeOnLoadMethod] callers (e.g. test bootstrap)
            // have a chance to call SuppressAutoStart() before we initialize.
            EditorApplication.delayCall += () =>
            {
                if (s_AutoStartSuppressed)
                {
                    InternalLog.Log("[RelayService] Auto-start suppressed; relay will lazy-init on first use.");
                    return;
                }

                InternalLog.Log("[RelayService] Initializing persistent Relay connection...");
                Instance.Initialize();
            };
        }

        /// <summary>
        /// Suppress the eager auto-start of the relay process at editor load. Lazy initialization
        /// on first <see cref="GetClientAsync"/> call still works. Intended for test bootstraps
        /// that don't want the relay subprocess running during unrelated tests (a relay crash
        /// can otherwise poison strict LogAssert checks in any concurrently running test).
        /// Must be called from an [InitializeOnLoadMethod] so it runs before the deferred
        /// auto-start callback fires.
        /// </summary>
        public static void SuppressAutoStart()
        {
            s_AutoStartSuppressed = true;
        }

        public static RelayService Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    lock (s_InstanceLock)
                    {
                        s_Instance ??= new RelayService();
                    }
                }
                return s_Instance;
            }
        }

        readonly object m_StateLock = new();
        readonly object m_StartLock = new();
        RelaySnapshot m_State;
        Process m_ProcessHandle;
        WebSocketRelayClient m_Client;
        RelayBus m_Bus;
        float m_LastConnectionAttemptTime;
        bool m_IsReconnecting;
        bool m_IsConnectedToExternalServer;
        Task m_StartTask;
        readonly List<TaskCompletionSource<WebSocketRelayClient>> m_WaitingClients = new();
        readonly int m_EditorProcessId;
        int m_Port;
        int m_McpPort;
        int m_ProcessId;
        string[] m_Capabilities = Array.Empty<string>();
        string m_RelayVersion;
        string m_VersionMismatchError;

        /// <summary>
        /// Current state snapshot (thread-safe read).
        /// </summary>
        public RelaySnapshot State => m_State;

        /// <summary>
        /// Whether the relay is fully connected and operational.
        /// The state machine is the single source of truth - when WebSocket disconnects,
        /// OnDisconnected fires and transitions state to Connecting.
        /// </summary>
        public bool IsConnected => m_State.Status == RelayStatus.Running;

        /// <summary>
        /// The underlying WebSocket client. May be null if not connected.
        /// Prefer using GetClientAsync() for safe access.
        /// </summary>
        public WebSocketRelayClient Client => m_Client;

        /// <summary>
        /// The relay bus for typed event/method communication.
        /// Always non-null. Check <see cref="RelayBus.IsAttached"/> for connection state.
        /// </summary>
        public RelayBus Bus => m_Bus;

        /// <summary>
        /// Custom relay start handler. When set, used instead of the default binary.
        /// </summary>
        public RelayStartDelegate CustomStartHandler { get; set; }

        /// <summary>
        /// When enabled, connects to an already-running relay instead of starting a new one.
        /// Set by developer tools for debugging or sharing between editors.
        /// </summary>
        public bool UseRunningServer { get; set; }

        /// <summary>
        /// Fixed port for the relay server. When set to a value > 0, the relay will use this port
        /// instead of auto-discovering an available one. Set to 0 for default behavior.
        /// </summary>
        public int FixedPort { get; set; }

        /// <summary>
        /// Fixed port for the MCP client REST endpoint. When set to a value > 0, the relay will
        /// use this port instead of auto-discovering an available one. Set to 0 for default behavior.
        /// Mirrors <see cref="FixedPort"/>; without this, concurrent relay instances TOCTOU-race
        /// on the same scanned MCP port and one of them dies with EADDRINUSE during startup.
        /// </summary>
        /// <remarks>
        /// Like <see cref="FixedPort"/>, this lives in memory on a static singleton and is reset by
        /// every domain reload; callers must re-apply it (e.g. from an [InitializeOnLoad] hook) or the
        /// next relay start silently falls back to auto-discovery. Pin <see cref="FixedPort"/> as well:
        /// the WebSocket port is selected first, and when it is left at 0 its scan can claim this port,
        /// which then forces MCP selection off the pin.
        /// </remarks>
        public int FixedMcpPort { get; set; }

        /// <summary>
        /// The current WebSocket port the relay is using.
        /// </summary>
        public int Port => m_Port;

        /// <summary>
        /// The current MCP client REST API port the relay is using.
        /// </summary>
        public int McpClientPort => m_McpPort;

        /// <summary>
        /// True if connected to a relay we didn't start (external server via UseRunningServer).
        /// False if we started the relay process ourselves.
        /// </summary>
        public bool IsConnectedToExternalServer => m_IsConnectedToExternalServer;

        /// <summary>
        /// The version of the connected relay, if available.
        /// </summary>
        public string RelayVersion => m_RelayVersion;

        /// <summary>
        /// The capabilities reported by the connected relay.
        /// </summary>
        public IReadOnlyList<string> Capabilities => m_Capabilities;

        /// <summary>
        /// Error message if relay version is incompatible, null otherwise.
        /// </summary>
        public string VersionMismatchError => m_VersionMismatchError;

        /// <summary>
        /// Checks if the relay has a specific capability.
        /// </summary>
        /// <param name="capability">The capability to check for (e.g., "acp", "replay").</param>
        /// <returns>True if the relay has the capability, false otherwise.</returns>
        public bool HasCapability(string capability)
        {
            return m_Capabilities != null && Array.Exists(m_Capabilities, c => c == capability);
        }

        /// <summary>
        /// Gets the executable path of the running relay process.
        /// Returns null if no process is running or path cannot be determined.
        /// </summary>
        public string ProcessExecutablePath
        {
            get
            {
                try
                {
                    if (m_ProcessHandle == null || m_ProcessHandle.HasExited)
                        return null;

                    return m_ProcessHandle.MainModule?.FileName;
                }
                catch
                {
                    // MainModule access may throw on some platforms/permissions
                    return null;
                }
            }
        }

        /// <summary>Fired when state changes (on main thread). Listeners should read State property for current state.</summary>
        public event Action StateChanged;

        /// <summary>Fired when connection is established.</summary>
        public event Action Connected;

        /// <summary>Fired when connection is lost.</summary>
        public event Action Disconnected;

        /// <summary>Fired when MCP session token registration is received from relay (for auto-approval).</summary>
        public event Action<McpSessionRegistration> OnMcpSessionRegister;

        /// <summary>Fired when MCP session token unregistration is received from relay.</summary>
        public event Action<string> OnMcpSessionUnregister;  // sessionId

        RelayService()
        {
            m_EditorProcessId = Process.GetCurrentProcess().Id;
            // Initialize in-memory fields from EditorPrefs (read once at startup)
            m_Port = GetPersistedPort();
            m_McpPort = GetPersistedMcpPort();
            m_ProcessId = GetPersistedProcessId();
            m_State = new RelaySnapshot(
                RelayStatus.NotStarted,
                m_Port,
                m_ProcessId,
                LastStateChange: DateTime.UtcNow
            );

            // Bus is long-lived — created once, transport swapped on connect/disconnect
            m_Bus = new RelayBus();
            SetupBusHandlers();
        }

        /// <summary>
        /// Initialize the relay service. Called from RelayAutoStart.
        /// </summary>
        public void Initialize()
        {
            ProjectScriptCompilation.OnBeforeReload += SendWaitingDomainReloadMessage;
            ProjectScriptCompilation.OnRequestReload += SendWaitingDomainReloadMessage;
            EditorApplication.update += Update;
            EditorApplication.quitting += OnEditorQuitting;

            if (Application.isBatchMode)
            {
                EditorTask.delayCall += () => _ = StartAsync();
            }
            else
            {
                _ = StartAsync();
            }
        }

        /// <summary>
        /// Start the relay service. Safe to call multiple times.
        /// Thread-safe: concurrent callers all await the same startup task.
        /// </summary>
        public Task StartAsync()
        {
            lock (m_StartLock)
            {
                // If startup is already in progress, all callers await the same task
                if (m_StartTask is {IsCompleted: false})
                    return m_StartTask;

                var currentStatus = m_State.Status;
                if (currentStatus == RelayStatus.Starting ||
                    currentStatus == RelayStatus.Connecting ||
                    currentStatus == RelayStatus.Running)
                {
                    return Task.CompletedTask; // Already started or in progress
                }

                m_StartTask = StartAsyncCore();
                return m_StartTask;
            }
        }

        /// <summary>
        /// Core startup logic. Called only from StartAsync() which ensures single execution.
        /// </summary>
        async Task StartAsyncCore()
        {
            try
            {
                // Ensure we're subscribed to events (may have been removed by StopAsync)
                EditorApplication.update -= Update;
                EditorApplication.update += Update;
                EditorApplication.quitting -= OnEditorQuitting;
                EditorApplication.quitting += OnEditorQuitting;

                // Try to recover from persisted state first
                // This is event-driven: we check if we have persisted state, then attempt
                // the actual connection. The connection result determines if we need a new relay.
                if (TryRecoverFromPersistedState())
                {
                    // Attempt to connect to the persisted relay
                    // ConnectWebSocketAsync has robust retry logic (10 attempts, 500ms each)
                    bool connected = await TryConnectToExistingRelayAsync();
                    if (connected)
                    {
                        return; // Successfully recovered
                    }

                    // Connection failed - the relay is not actually running
                    // Clear persisted state and start fresh
                    InternalLog.Log("[RelayService] Failed to connect to persisted relay - starting new relay");
                    ClearPersistedState();
                }

                // Start fresh relay
                await StartRelayProcessAsync();
            }
            finally
            {
                lock (m_StartLock)
                {
                    m_StartTask = null;
                }
            }
        }

        /// <summary>
        /// Force reconnect the WebSocket client.
        /// </summary>
        public async Task ReconnectAsync()
        {
            if (m_State.Status == RelayStatus.NotStarted || m_State.Status == RelayStatus.Stopped)
            {
                await StartAsync();
                return;
            }

            // Early-out if a connect loop is already running. TryConnectAsync (invoked by
            // ConnectWebSocketAsync) owns the m_IsReconnecting flag for its whole duration,
            // so this both blocks a re-entrant ReconnectAsync and is what Update() checks
            // before firing us. The check-then-call is synchronous up to TryConnectAsync
            // setting the flag, so Update() (main thread) cannot slip a second loop in.
            if (m_IsReconnecting) return;

            await ConnectWebSocketAsync();
        }

        /// <summary>
        /// Stop the relay service gracefully.
        /// </summary>
        public async Task StopAsync()
        {
            if (m_State.Status == RelayStatus.Stopped ||
                m_State.Status == RelayStatus.NotStarted ||
                m_State.Status == RelayStatus.Stopping)
                return;

            // Transition to Stopping immediately - this is the source of truth
            // All auto-reconnect logic will see this state and bail out
            TransitionTo(RelayStatus.Stopping);

            // Remove Update handler to prevent reconnection attempts during shutdown
            EditorApplication.update -= Update;

            // Send shutdown signal via WebSocket first (while still connected)
            if (m_Client?.IsConnected == true)
            {
                try
                {
                    await m_Client.ShutdownServerAsync();
                    // Brief pause to let server begin shutdown
                    await Task.Delay(100);
                }
                catch
                {
                    // Ignore errors during shutdown
                }
            }

            // Kill the process tree if it's still running
            if (m_ProcessHandle != null)
            {
                try
                {
                    KillProcessTree(m_ProcessHandle);
                }
                catch
                {
                    // Ignore errors during process termination
                }
                finally
                {
                    try { m_ProcessHandle.Dispose(); } catch { }
                    m_ProcessHandle = null;
                }

                // Give OS time to release the port
                await Task.Delay(200);
            }

            // Clear persisted state BEFORE transitioning so the snapshot has cleared values
            ClearPersistedState();
            Cleanup();

            // Transition to stopped AFTER cleanup so state reflects cleared port/PID
            TransitionTo(RelayStatus.Stopped);
        }

        /// <summary>
        /// Check if we have persisted relay state that we can attempt to recover.
        /// This method does NOT validate the relay is running - it only checks if we have
        /// persisted state to try. The actual validation happens via the WebSocket connection
        /// attempt in ConnectWebSocketAsync, which has robust retry logic.
        ///
        /// This event-driven approach is more reliable than timeout-based validation because:
        /// 1. The WebSocket connection attempt has proper retry logic (10 retries, 500ms each)
        /// 2. We don't rely on arbitrary timeouts that may fail during busy periods
        /// 3. The actual connection result (success/failure) determines next steps
        /// </summary>
        bool TryRecoverFromPersistedState()
        {
            int persistedProcessId = GetPersistedProcessId();
            int persistedPort = GetPersistedPort();

            if (persistedProcessId == 0 || persistedPort == 0)
            {
                InternalLog.Log("[RelayService] No persisted relay state found");
                return false;
            }

            InternalLog.Log($"[RelayService] Found persisted relay state (port: {persistedPort}, PID: {persistedProcessId}) - will attempt connection");

            // Try to recover process handle if possible (for process monitoring)
            // This is optional - if it fails, we can still connect to the relay
            try
            {
                var existingProcess = Process.GetProcessById(persistedProcessId);
                if (!existingProcess.HasExited)
                {
                    // Try to validate executable path for full process management
                    string exePath = null;
                    try { exePath = existingProcess.MainModule?.FileName; } catch { /* ignore access errors */ }

                    var expectedRelayPath = Path.GetFullPath(GetRelayExecutablePath());
                    if (!string.IsNullOrEmpty(exePath) &&
                        string.Equals(exePath, expectedRelayPath, StringComparison.OrdinalIgnoreCase))
                    {
                        m_ProcessHandle = existingProcess;
                        SetupProcessMonitoring();
                        InternalLog.Log($"[RelayService] Recovered relay process handle (PID: {persistedProcessId})");
                    }
                    // If path doesn't match (e.g., Node.js dev mode), we can still connect
                    // The relay is running, we just don't have process management
                }
            }
            catch
            {
                // Process doesn't exist or can't be accessed - that's OK
                // We'll find out if the relay is actually running when we try to connect
            }

            return true;
        }

        async Task StartRelayProcessAsync()
        {
            TransitionTo(RelayStatus.Starting);

            try
            {
                // If UseRunningServer enabled, try to find existing relay first
                if (UseRunningServer)
                {
                    int existingPort = await FindExistingRelayPortAsync();
                    if (existingPort > 0)
                    {
                        InternalLog.Log($"[RelayService] Found existing relay on port {existingPort}");
                        m_IsConnectedToExternalServer = true;
                        SetPersistedPort(existingPort);
                        await ConnectWebSocketAsync();
                        return;
                    }

                    InternalLog.Log("[RelayService] UseRunningServer enabled but no existing relay found, starting new one");
                }

                m_IsConnectedToExternalServer = false;
                await StartNewRelayWithPortRetry();
            }
            catch (Exception ex)
            {
                TransitionTo(RelayStatus.Failed, $"Error starting relay: {ex.Message}");
            }
        }

        /// <summary>
        /// Find available ports and start the relay process.
        /// Retries with different ports if the process exits immediately (e.g., EADDRINUSE).
        /// </summary>
        async Task StartNewRelayWithPortRetry()
        {
            const int maxStartAttempts = 3;
            var excludePorts = new HashSet<int>();

            for (int startAttempt = 0; startAttempt < maxStartAttempts; startAttempt++)
            {
                // Find port for relay WebSocket server
                int port = await FindAvailablePortAsync(
                    FixedPort > 0 ? FixedPort : null,
                    GetPersistedPort() > 0 ? GetPersistedPort() : null,
                    excludePorts);
                if (port == 0)
                {
                    TransitionTo(RelayStatus.Failed, "No available ports found in range 9001-9100");
                    return;
                }

                // Find port for MCP client REST API (exclude the relay port and previously failed ports)
                var mcpExclude = new HashSet<int>(excludePorts) { port };
                int mcpClientPort = await FindAvailablePortAsync(
                    FixedMcpPort > 0 ? FixedMcpPort : null,
                    GetPersistedMcpPort() > 0 ? GetPersistedMcpPort() : null,
                    mcpExclude);
                if (mcpClientPort == 0)
                {
                    TransitionTo(RelayStatus.Failed, "No available ports found for MCP client in range 9001-9100");
                    return;
                }

                SetPersistedPort(port);
                SetPersistedMcpPort(mcpClientPort);

                m_ProcessHandle = CustomStartHandler != null
                    ? CustomStartHandler(port, mcpClientPort, m_EditorProcessId, k_AutoShutdownDelaySeconds)
                    : StartDefaultRelay(port, mcpClientPort);

                if (m_ProcessHandle == null)
                {
                    TransitionTo(RelayStatus.Failed, "Failed to start relay process");
                    return;
                }

                SetPersistedProcessId(m_ProcessHandle.Id);
                SetupProcessMonitoring();

                // Try to connect — TryConnectAsync will bail early if the process exits
                // (e.g., EADDRINUSE crash), so no artificial delay needed here.
                var (success, error) = await TryConnectAsync(
                    k_MaxConnectionRetries,
                    TimeSpan.FromSeconds(k_StartupConnectionRetrySeconds));
                if (success)
                {
                    return;
                }

                // Connection failed — if process crashed, retry with different ports
                if (m_ProcessHandle.HasExited)
                {
                    InternalLog.LogWarning($"[RelayService] Relay exited on ports {port}/{mcpClientPort} (attempt {startAttempt + 1}/{maxStartAttempts}), retrying with different ports...");
                    excludePorts.Add(port);
                    excludePorts.Add(mcpClientPort);
                    continue;
                }

                // Process is alive but connection failed for another reason — don't retry
                TransitionTo(RelayStatus.Failed, error ?? "Connection failed");
                return;
            }

            TransitionTo(RelayStatus.Failed, "Relay process failed to start after multiple port attempts");
        }

        const int k_MaxConnectionRetries = 10;
        const int k_ConnectionTimeoutMs = 500;
        const int k_ConnectionRetryDelayMs = 200;
        const int k_StartupConnectionRetrySeconds = 30;
        const int k_RecoveryConnectionRetries = 3;  // Fewer retries for recovery attempts

        /// <summary>
        /// Attempt to connect to an existing relay using persisted state.
        /// Uses fewer retries since we need to fall back to starting a new relay quickly.
        /// </summary>
        /// <returns>True if connection succeeds, false if it fails.</returns>
        async Task<bool> TryConnectToExistingRelayAsync()
        {
            var (success, _) = await TryConnectAsync(k_RecoveryConnectionRetries);
            if (!success)
            {
                // Reset state so caller can start fresh
                TransitionTo(RelayStatus.NotStarted);
            }
            return success;
        }

        /// <summary>
        /// Connect to relay, transitioning to Failed state if connection fails.
        /// Used after starting a new relay process.
        /// </summary>
        async Task ConnectWebSocketAsync()
        {
            var (success, error) = await TryConnectAsync(k_MaxConnectionRetries);
            if (!success)
            {
                TransitionTo(RelayStatus.Failed, error ?? "Connection failed");
            }
        }

        /// <summary>
        /// Core connection logic. Attempts to connect to the relay with retry logic.
        /// Returns success status and error message (if any).
        /// On success, transitions to Running state.
        /// On failure, cleans up client but does NOT transition state - caller decides.
        /// </summary>
        async Task<(bool success, string error)> TryConnectAsync(
            int maxRetries,
            TimeSpan minimumRetryDuration = default)
        {
            // Single-flight guard. Update()'s 5s auto-reconnect and ReconnectAsync both gate
            // on m_IsReconnecting, so holding it for the WHOLE attempt stops a second
            // TryConnectAsync from running concurrently against the shared m_Client/m_Bus.
            // This became reachable once bus-validation failures were made retryable: the
            // connect loop can now run past the 5s reconnect throttle, so Update() would
            // otherwise fire ReconnectAsync mid-attempt and the two overlapping loops would
            // tear down each other's client — parking the service in Running with a null
            // m_Client, or (with the new DetachTransport) detaching the bus out from under
            // the sibling loop.
            //
            // The two direct callers (StartNewRelayWithPortRetry, TryConnectToExistingRelayAsync)
            // do NOT pre-check the flag the way ReconnectAsync/Update() do, so the check must live
            // here to make the method genuinely single-flight: check-and-set at entry, with no
            // await between the check and the set, so Update()/ReconnectAsync (main thread) cannot
            // slip a second loop in during this method's own awaits. A second concurrent call is
            // refused rather than allowed to run a second loop against the shared m_Client/m_Bus.
            // The flag is set BEFORE TryConnectCoreAsync transitions to Connecting, so Update()
            // never observes Connecting-and-not-reconnecting during an attempt.
            if (!CanStartConnectAttempt(m_IsReconnecting))
                return (false, "Connect already in progress");

            m_IsReconnecting = true;
            try
            {
                return await TryConnectCoreAsync(maxRetries, minimumRetryDuration);
            }
            finally
            {
                m_IsReconnecting = false;
            }
        }

        /// <summary>
        /// Single-flight gate for <see cref="TryConnectAsync"/>: a connect attempt may start only
        /// when no other attempt is already in flight. Pure so the re-entry rule is unit-testable
        /// without a live relay/editor (the connect loop itself is editor-bound and cannot be).
        /// </summary>
        /// <param name="attemptInProgress">Current value of the in-flight flag (m_IsReconnecting).</param>
        /// <returns>True if the caller may begin a new attempt; false if one is already running.</returns>
        internal static bool CanStartConnectAttempt(bool attemptInProgress) => !attemptInProgress;

        // The connect loop itself. Always invoked through TryConnectAsync, which owns the
        // single-flight m_IsReconnecting guard around it.
        async Task<(bool success, string error)> TryConnectCoreAsync(
            int maxRetries,
            TimeSpan minimumRetryDuration)
        {
            int port = GetPersistedPort();
            if (port == 0)
            {
                return (false, "No port configured");
            }

            int processId = m_ProcessHandle?.Id ?? GetPersistedProcessId();
            bool processAlive = false;
            try
            {
                if (processId > 0)
                {
                    var proc = m_ProcessHandle ?? Process.GetProcessById(processId);
                    processAlive = !proc.HasExited;
                }
            }
            catch { /* process doesn't exist or can't be accessed */ }

            Trace.Event("connection.attempt_start", new TraceEventOptions
            {
                Data = new Dictionary<string, object>
                {
                    { "port", port },
                    { "maxRetries", maxRetries },
                    { "processAlive", processAlive },
                    { "processId", processId },
                }
            });

            TransitionTo(RelayStatus.Connecting);
            string serverAddress = $"ws://127.0.0.1:{port}";
            string lastError = null;
            m_LastConnectionAttemptTime = (float)EditorApplication.timeSinceStartup;
            var retryStopwatch = Stopwatch.StartNew();
            int attempt = 0;

            while (true)
            {
                attempt++;

                // Set only by a deterministic, non-retryable failure (WrongEditor) to leave
                // the loop after this iteration's cleanup instead of retrying.
                bool abortWithoutRetry = false;

                if (m_State.Status == RelayStatus.Stopping || m_State.Status == RelayStatus.Stopped)
                    return (false, "Stop requested");

                // Early exit if the relay process has already crashed (e.g., EADDRINUSE)
                if (m_ProcessHandle is { HasExited: true })
                    return (false, $"Relay process exited (exit code {m_ProcessHandle.ExitCode})");

                try
                {
                    // Detach before dropping the client, matching every other teardown site
                    // (detach-before-null). The transport installed below is
                    // `json => m_Client.SendRawMessageAsync(json)`, which reads the FIELD at call
                    // time, so a background bus caller landing between the null and the SetTransport
                    // a few instructions later would hit the same NRE this PR is closing. Detaching
                    // first makes the bus report disconnected in that window instead. DetachTransport
                    // is idempotent, so this is a no-op on the first iteration when nothing is attached.
                    m_Bus.DetachTransport();
                    if (m_Client != null)
                    {
                        m_Client.Dispose();
                        m_Client = null;
                    }

                    m_Client = new WebSocketRelayClient();
                    m_Bus.SetTransport(json => m_Client.SendRawMessageAsync(json));
                    m_Client.Bus = m_Bus;
                    SetupClientEvents();

                    var (connected, connectError) = await m_Client.ConnectAsync(serverAddress, k_ConnectionTimeoutMs).ConfigureAwait(false);

                    if (connected)
                    {
                        if (m_State.Status == RelayStatus.Stopping || m_State.Status == RelayStatus.Stopped)
                        {
                            // Same reason as the validation-failure path below: the attached
                            // transport dereferences m_Client at call time, so it must go
                            // before the field does.
                            m_Bus.DetachTransport();
                            m_Client?.Dispose();
                            m_Client = null;
                            return (false, "Stop requested");
                        }

                        // Validate relay version via bus (over existing connection, no second WebSocket)
                        var validation = await ValidateRelayViaBusAsync();
                        var outcome = ClassifyValidation(validation);
                        if (outcome == ConnectionValidationOutcome.Accept)
                        {
                            if (validation == BusValidationResult.VersionMismatch)
                            {
                                var warningMsg = m_VersionMismatchError ??
                                    "Relay protocol version mismatch. Some features may not work correctly.";
                                Debug.LogWarning($"[RelayService] {warningMsg}");
                                // The relay answered and serves this editor — an old
                                // protocol is degraded, not unusable.
                            }

                            TransitionTo(RelayStatus.Running);
                            InternalLog.Log($"[RelayService] Connection established (relay v{m_RelayVersion ?? "unknown"}, capabilities: {string.Join(", ", m_Capabilities)})");
                            return (true, null);
                        }

                        // Not a usable connection: either the relay never answered (socket
                        // likely half-open or already closed by the peer, e.g. a 1008
                        // single-client rejection) or it belongs to a different editor.
                        // Returning success here would park the service in Running on a dead
                        // transport, and keeping the client alive leaves a zombie socket
                        // occupying a relay's single-client slot so every subsequent attempt
                        // is rejected — the case-094 reconnect wedge. Free the socket NOW,
                        // before any retry delay.
                        lastError = m_VersionMismatchError ?? "Bus validation failed";
                        InternalLog.LogWarning($"[RelayService] {lastError} — treating connection attempt as failed");
                        // Detach before dropping the client. The transport installed at the
                        // top of this loop is `json => m_Client.SendRawMessageAsync(json)`,
                        // which reads the FIELD at call time — so leaving it attached over a
                        // null m_Client means a bus caller (e.g. the ACP credential
                        // reveal/set path) passes RelayBus.IsAttached during the retry delay
                        // and NREs instead of getting a normal disconnected/cancelled
                        // result. DetachTransport also cancels the calls already in flight,
                        // which this dead socket will never answer.
                        m_Bus.DetachTransport();
                        m_Client?.Dispose();
                        m_Client = null;

                        if (outcome == ConnectionValidationOutcome.Abort)
                        {
                            // WrongEditor is deterministic: `port` is read once (above) and
                            // no new relay is started in this loop, so every retry would
                            // re-connect to the SAME foreign relay and re-read the SAME PID —
                            // it can never resolve, and each attempt just grabs another
                            // editor's single-client slot again. Stop immediately rather than
                            // retry; what happens after the abort depends on the caller:
                            //   - TryConnectToExistingRelayAsync (recovery from persisted state):
                            //     StartAsyncCore sees the failure, clears persisted state, and
                            //     starts THIS editor's own relay — the abort self-heals.
                            //   - ConnectWebSocketAsync (Update()'s auto-reconnect via
                            //     ReconnectAsync, and the UseRunningServer path) and
                            //     StartNewRelayWithPortRetry: both transition to Failed, which is
                            //     terminal in both directions (Update() only re-drives Connecting,
                            //     and GetClientAsync throws on Failed), so a WrongEditor reached
                            //     through those paths stays disconnected until a manual reconnect /
                            //     StartAsync. (Converging them on the self-heal is a possible
                            //     follow-up; left out here to keep this a comment-only change.)
                            // (Unresponsive falls through to the retry path below, where a later
                            // attempt can actually succeed.)
                            abortWithoutRetry = true;
                        }
                    }
                    else
                    {
                        lastError = connectError ?? "WebSocket connection failed";
                    }
                }
                catch (Exception ex)
                {
                    lastError = $"Connection error: {ex.GetType().Name}: {ex.Message}";
                }

                if (abortWithoutRetry)
                    break;

                if (!ShouldRetryConnection(
                        attempt,
                        retryStopwatch.Elapsed,
                        maxRetries,
                        minimumRetryDuration))
                    break;

                await Task.Delay(k_ConnectionRetryDelayMs);
            }

            // All retries exhausted - clean up client. Detach first: terminal failure
            // otherwise leaves the bus "attached" to a transport over a null client
            // forever, so every later bus caller NREs instead of seeing a disconnected
            // relay.
            m_Bus.DetachTransport();
            if (m_Client != null)
            {
                m_Client.Dispose();
                m_Client = null;
            }

            return (false, $"{lastError} (after {attempt} attempts over {retryStopwatch.Elapsed.TotalSeconds:F1}s)");
        }

        internal static bool ShouldRetryConnection(
            int completedAttempts,
            TimeSpan elapsed,
            int maxRetries,
            TimeSpan minimumRetryDuration)
        {
            return completedAttempts < maxRetries || elapsed < minimumRetryDuration;
        }

        /// <summary>
        /// How the connect loop should react to a <see cref="BusValidationResult"/>.
        /// </summary>
        internal enum ConnectionValidationOutcome
        {
            /// <summary>Usable connection — transition to Running.</summary>
            Accept,
            /// <summary>Transient failure — dispose the client and retry (the relay may answer next time).</summary>
            Retry,
            /// <summary>Deterministic failure — dispose the client and stop retrying (retrying cannot change the result).</summary>
            Abort,
        }

        /// <summary>
        /// Map a bus-validation result to the connect loop's reaction. Split out so the
        /// retryable/terminal distinction is unit-testable without a live relay:
        /// <see cref="BusValidationResult.Unresponsive"/> is retryable (a half-open or
        /// still-settling socket can answer on a later attempt), but
        /// <see cref="BusValidationResult.WrongEditor"/> is a fixed PID mismatch against a
        /// fixed port — no number of retries reaches a different relay, so it aborts.
        /// </summary>
        internal static ConnectionValidationOutcome ClassifyValidation(BusValidationResult validation)
        {
            switch (validation)
            {
                case BusValidationResult.Valid:
                case BusValidationResult.VersionMismatch:
                    return ConnectionValidationOutcome.Accept;
                case BusValidationResult.WrongEditor:
                    return ConnectionValidationOutcome.Abort;
                case BusValidationResult.Unresponsive:
                default:
                    return ConnectionValidationOutcome.Retry;
            }
        }

        /// <summary>
        /// Get a connected relay client, waiting if necessary.
        /// Uses default 30 second timeout.
        /// </summary>
        /// <returns>Connected client.</returns>
        /// <exception cref="RelayConnectionException">If connection fails or times out.</exception>
        public Task<WebSocketRelayClient> GetClientAsync() => GetClientAsync(TimeSpan.FromSeconds(k_DefaultTimeoutSeconds));

        public Task<WebSocketRelayClient> GetClientAsync(CancellationToken ct) => GetClientAsync(TimeSpan.FromSeconds(k_DefaultTimeoutSeconds), ct);

        /// <summary>
        /// Get a connected relay client, waiting if necessary.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for connection.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Connected client.</returns>
        /// <exception cref="RelayConnectionException">If connection fails or times out.</exception>
        /// <exception cref="OperationCanceledException">If cancelled.</exception>
        public async Task<WebSocketRelayClient> GetClientAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            // Fast path: already connected (state machine is source of truth)
            if (m_State.Status == RelayStatus.Running)
                return m_Client;

            // If not started, start now
            if (m_State.Status == RelayStatus.NotStarted || m_State.Status == RelayStatus.Stopped)
            {
                _ = StartAsync();
            }

            // If failed, throw immediately
            if (m_State.Status == RelayStatus.Failed)
            {
                throw new RelayConnectionException(m_State.ErrorMessage ?? "Relay is in failed state");
            }

            // Wait for running state
            var tcs = new TaskCompletionSource<WebSocketRelayClient>();

            lock (m_WaitingClients)
            {
                m_WaitingClients.Add(tcs);
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);

                using (cts.Token.Register(() => tcs.TrySetCanceled()))
                {
                    // Double-check after registration
                    if (m_State.Status == RelayStatus.Running && m_Client?.IsConnected == true)
                    {
                        tcs.TrySetResult(m_Client);
                    }
                    else if (m_State.Status == RelayStatus.Failed)
                    {
                        tcs.TrySetException(new RelayConnectionException(m_State.ErrorMessage ?? "Relay is in failed state"));
                    }

                    return await tcs.Task;
                }
            }
            finally
            {
                lock (m_WaitingClients)
                {
                    m_WaitingClients.Remove(tcs);
                }
            }
        }

        async Task<int> FindAvailablePortAsync()
        {
            return await FindAvailablePortAsync(null, null, null);
        }

        /// <summary>
        /// Find an available port in the configured range.
        /// </summary>
        /// <param name="fixedPort">If set, prefer this specific port</param>
        /// <param name="persistedPort">If set, check this port first (fast path)</param>
        /// <param name="excludePorts">Ports to exclude from selection</param>
        /// <returns>Available port, or 0 if none found</returns>
        async Task<int> FindAvailablePortAsync(int? fixedPort, int? persistedPort, HashSet<int> excludePorts)
        {
            excludePorts ??= new();

            // Phase 0: If fixed port is configured, use it
            if (fixedPort is > 0)
            {
                if (excludePorts.Contains(fixedPort.Value))
                {
                    // Dropping the pin here is deliberate — the port was already claimed by the relay
                    // WebSocket server, or a previous start attempt failed on it — but log it so an
                    // unpinned port stays explainable from the relay log.
                    InternalLog.LogWarning($"[RelayService] Fixed port {fixedPort.Value} is excluded from this attempt, falling back to auto-discovery");
                }
                else
                {
                    if (await IsServerRunningOnPortAsync(fixedPort.Value))
                        return fixedPort.Value;
                    if (IsPortAvailable(fixedPort.Value))
                        return fixedPort.Value;

                    InternalLog.LogWarning($"[RelayService] Fixed port {fixedPort.Value} is not available, falling back to auto-discovery");
                }
            }

            // Phase 1: Check persisted port first (fast path)
            if (persistedPort is > 0 && !excludePorts.Contains(persistedPort.Value))
            {
                if (await IsServerRunningOnPortAsync(persistedPort.Value))
                    return persistedPort.Value;
                if (IsPortAvailable(persistedPort.Value))
                    return persistedPort.Value;
            }

            // Phase 2: Fast TCP scan to find first available port
            for (int port = k_StartPort; port <= k_MaxPort; port++)
            {
                if (!excludePorts.Contains(port) && IsPortAvailable(port))
                    return port;
            }

            return 0;
        }

        /// <summary>
        /// Scan for an existing relay running on any port in the range.
        /// Uses parallel scanning for faster discovery.
        /// </summary>
        async Task<int> FindExistingRelayPortAsync()
        {
            // Fast path: check known ports first
            int persistedPort = GetPersistedPort();
            if (persistedPort > 0 && await IsServerRunningOnPortAsync(persistedPort))
                return persistedPort;

            if (FixedPort > 0 && await IsServerRunningOnPortAsync(FixedPort))
                return FixedPort;

            // Parallel scan first N ports
            var results = await Task.WhenAll(
                Enumerable.Range(k_StartPort, k_PortScanCount)
                    .Select(async p => (port: p, running: await IsServerRunningOnPortAsync(p))));

            return results.FirstOrDefault(r => r.running).port;
        }

        static bool IsPortAvailable(int port)
        {
            try
            {
                var tcpListener = new TcpListener(IPAddress.Loopback, port);
                tcpListener.Start();
                tcpListener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        async Task<bool> IsServerRunningOnPortAsync(int port, int timeoutMs = -1)
        {
            if (timeoutMs < 0)
                timeoutMs = k_HealthCheckTimeoutMs;

            try
            {
                using var webSocket = new System.Net.WebSockets.ClientWebSocket();
                string testAddress = $"ws://127.0.0.1:{port}?validationCheck=true";
                var uri = new Uri(testAddress);

                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

                await webSocket.ConnectAsync(uri, cts.Token).ConfigureAwait(false);

                var buffer = new byte[1024];
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token).ConfigureAwait(false);

                if (result.MessageType != System.Net.WebSockets.WebSocketMessageType.Text)
                {
                    m_VersionMismatchError = $"Validation response was not text (type: {result.MessageType})";
                    return false;
                }

                var response = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                var testResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerValidationResponse>(response);

                if (testResponse is not { status: "success", serverReady: true })
                {
                    m_VersionMismatchError = $"Relay not ready (status: {testResponse?.status ?? "null"}, serverReady: {testResponse?.serverReady})";
                    return false;
                }

                m_RelayVersion = testResponse.version;
                m_Capabilities = testResponse.capabilities ?? Array.Empty<string>();

                // Check editor PID ownership — reject relays belonging to other Unity instances
                if (!string.IsNullOrEmpty(testResponse.editorPid) &&
                    int.TryParse(testResponse.editorPid, out var relayEditorPid) &&
                    relayEditorPid != m_EditorProcessId)
                {
                    m_VersionMismatchError = $"Relay belongs to a different editor (relay PID: {testResponse.editorPid}, expected: {m_EditorProcessId})";
                    return false;
                }

                // Check protocol version compatibility
                if (!IsProtocolVersionCompatible(testResponse.protocolVersion))
                {
                    var versionDisplay = string.IsNullOrEmpty(testResponse.version) ? "unknown" : testResponse.version;
                    m_VersionMismatchError = $"Relay version {versionDisplay} is incompatible (protocol {testResponse.protocolVersion ?? "unknown"}). " +
                        "Enable 'Development Mode' in Developer Tools > Relay Settings, or rebuild the relay.";
                    return false;
                }

                m_VersionMismatchError = null;
                return true;
            }
            catch (Exception ex)
            {
                m_VersionMismatchError = $"Validation failed: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Validate relay version after connection. Uses a separate validation connection
        /// to get version info from the relay.
        /// </summary>
        async Task<bool> ValidateRelayVersionAsync(int port)
        {
            // Use longer timeout for post-connection validation since we know the server is running
            return await IsServerRunningOnPortAsync(port, k_VersionValidationTimeoutMs);
        }

        /// <summary>
        /// Outcome of the post-connect bus validation. The split matters because the
        /// failure modes need opposite handling: a relay that ANSWERED with an old
        /// protocol is usable (warn and continue), while a relay that never answered
        /// or belongs to another editor must fail the connect attempt — treating
        /// those as "non-fatal mismatch" is how the 2026-08-19 case-094 wedge
        /// happened (Running state on a dead socket, and the half-open client
        /// holding a relay's single-client slot so every retry got rejected with
        /// 1008 "Server dedicated to single client").
        /// </summary>
        internal enum BusValidationResult
        {
            Valid,
            /// <summary>Relay responded but its protocol version is old. Usable.</summary>
            VersionMismatch,
            /// <summary>No InfoResponse (timeout/cancel/null). The socket cannot be trusted.</summary>
            Unresponsive,
            /// <summary>Relay answered but serves a different editor PID. Never usable.</summary>
            WrongEditor,
        }

        /// <summary>
        /// Validate relay version via bus call on the already-connected WebSocket.
        /// Avoids opening a second connection (which can cause disconnection on Windows).
        /// </summary>
        async Task<BusValidationResult> ValidateRelayViaBusAsync()
        {
            try
            {
                var response = await m_Bus.CallAsync(
                    RelayChannels.Info,
                    new InfoRequest(),
                    timeoutMs: k_VersionValidationTimeoutMs);

                if (response == null)
                {
                    m_VersionMismatchError = "Info response was null";
                    return BusValidationResult.Unresponsive;
                }

                m_RelayVersion = response.Version;
                m_Capabilities = response.Capabilities ?? Array.Empty<string>();

                // Check editor PID ownership — reject relays belonging to other Unity instances
                if (!string.IsNullOrEmpty(response.EditorPid) &&
                    int.TryParse(response.EditorPid, out var relayEditorPid) &&
                    relayEditorPid != m_EditorProcessId)
                {
                    m_VersionMismatchError = $"Relay belongs to a different editor (relay PID: {response.EditorPid}, expected: {m_EditorProcessId})";
                    return BusValidationResult.WrongEditor;
                }

                // Check protocol version compatibility
                if (!IsProtocolVersionCompatible(response.ProtocolVersion))
                {
                    var versionDisplay = string.IsNullOrEmpty(response.Version) ? "unknown" : response.Version;
                    m_VersionMismatchError = $"Relay version {versionDisplay} is incompatible (protocol {response.ProtocolVersion ?? "unknown"}). " +
                        "Enable 'Development Mode' in Developer Tools > Relay Settings, or rebuild the relay.";
                    return BusValidationResult.VersionMismatch;
                }

                m_VersionMismatchError = null;
                return BusValidationResult.Valid;
            }
            catch (Exception ex)
            {
                m_VersionMismatchError = $"Bus validation failed: {ex.GetType().Name}: {ex.Message}";
                return BusValidationResult.Unresponsive;
            }
        }

        static bool IsProtocolVersionCompatible(string protocolVersion)
        {
            // If no protocol version is reported, treat as incompatible (old relay)
            if (string.IsNullOrEmpty(protocolVersion))
                return false;

            // Compare version strings - for now, require exact match or compatible range
            // Format: "major.minor"
            if (!Version.TryParse(protocolVersion, out var relayVersion))
                return false;

            if (!Version.TryParse(RelayProtocol.MinimumProtocolVersion, out var minVersion))
                return true; // Fail open if our constant is malformed

            return relayVersion >= minVersion;
        }

        class ServerValidationResponse
        {
            public string type { get; set; }
            public string status { get; set; }
            public bool serverReady { get; set; }
            public string version { get; set; }
            public string protocolVersion { get; set; }
            public string[] capabilities { get; set; }
            public string editorPid { get; set; }
        }

        Process StartDefaultRelay(int port, int mcpClientPort)
        {
            if (string.IsNullOrEmpty(k_RelayPath) || !Directory.Exists(k_RelayPath))
            {
                Debug.LogError($"[RelayService] Server path not found: {k_RelayPath}");
                return null;
            }

            string relayExecutable = GetRelayExecutablePath();

            if (GetCurrentPlatform() == "mac")
                ForceUnpackExecutable();

            if (string.IsNullOrEmpty(relayExecutable) || !File.Exists(relayExecutable))
            {
                Debug.LogError($"[RelayService] Relay executable not found: {relayExecutable}");
                return null;
            }

            // Write trace config file for the relay to read on startup
            TraceConfigFileWriter.WriteTraceConfigFile(TraceLogDir.LogDir);

            var startInfo = new ProcessStartInfo
            {
                FileName = relayExecutable,
                Arguments = BuildRelayArguments(port, mcpClientPort, m_EditorProcessId, k_AutoShutdownDelaySeconds),
                UseShellExecute = false,
                CreateNoWindow = true
            };

            ApplyRelayEnvironment(startInfo);
            return Process.Start(startInfo);
        }


        void SetupProcessMonitoring()
        {
            if (m_ProcessHandle == null) return;

            m_ProcessHandle.EnableRaisingEvents = true;
            m_ProcessHandle.Exited += (sender, e) =>
            {
                // Capture exit code immediately while process handle is still valid
                var exitCode = -1;
                try
                {
                    exitCode = m_ProcessHandle?.ExitCode ?? -1;
                }
                catch
                {
                    // Process may already be disposed
                }

                EditorTask.delayCall += () =>
                {
                    // Expected exit during intentional shutdown
                    if (m_State.Status == RelayStatus.Stopping || m_State.Status == RelayStatus.Stopped)
                        return;

                    TransitionTo(RelayStatus.Failed, $"Process exited unexpectedly. code={exitCode}");
                    Cleanup();
                };
            };
        }

        void SetupClientEvents()
        {
            if (m_Client == null) return;

            // A chat workflow only consumes OnAssistantMessage while it is attached to *this*
            // client, so a reconnect can leave a live client with nothing subscribed to it and a
            // redelivered function call is dropped inside the editor (muse-editor#2705). Attach the
            // standalone fallback to every client so a pending call always has a consumer.
            RelayFunctionCallFallback.Attach(m_Client);

            // Note: Connected/Disconnected events now fire from TransitionTo based on state changes
            // OnConnected from WebSocket doesn't need to do anything - the state transition to Running
            // triggers the Connected event

            m_Client.OnDisconnected += (closeStatus, closeDescription) =>
            {
                InternalLog.LogWarning($"[RelayService] WebSocket disconnected (closeStatus={closeStatus}, description={closeDescription})");
                if (m_State.Status == RelayStatus.Running)
                {
                    TransitionTo(RelayStatus.Connecting);
                }
            };
        }

        /// <summary>
        /// One-time bus handler registration. Called once in constructor.
        /// Bus is long-lived so handlers survive reconnects.
        /// </summary>
        void SetupBusHandlers()
        {
            // Forward MCP session events from bus (for auto-approval)
            m_Bus.On(RelayChannels.McpSessionRegister, (registration) =>
            {
                if (registration.IsValid)
                    OnMcpSessionRegister?.Invoke(registration);
            });

            m_Bus.On(RelayChannels.McpSessionUnregister, (unreg) =>
            {
                if (!string.IsNullOrEmpty(unreg.SessionId))
                    OnMcpSessionUnregister?.Invoke(unreg.SessionId);
            });
        }

        void TransitionTo(RelayStatus newStatus, string error = null)
        {
            RelaySnapshot newState;
            RelaySnapshot oldState;

            lock (m_StateLock)
            {
                oldState = m_State;
                newState = new RelaySnapshot(
                    newStatus,
                    m_Port,      // Use in-memory field (thread-safe)
                    m_ProcessId, // Use in-memory field (thread-safe)
                    error,
                    DateTime.UtcNow
                );
                m_State = newState;
            }

            if (oldState.Status != newStatus)
            {
                InternalLog.Log($"[RelayService] State transition: {oldState.Status} -> {newStatus}");

                Trace.Event("connection.state_change", new TraceEventOptions
                {
                    Level = newStatus == RelayStatus.Failed ? "error" : "info",
                    Data = new Dictionary<string, object>
                    {
                        { "oldState", oldState.Status.ToString() },
                        { "newState", newStatus.ToString() },
                        { "error", error ?? "" },
                    }
                });

                // Fire Connected/Disconnected based on state transitions
                // These are derived from state changes, not independent triggers
                if (newStatus == RelayStatus.Running)
                {
                    Connected?.Invoke();
                }
                else if (oldState.Status == RelayStatus.Running)
                {
                    m_Bus.DetachTransport();
                    Disconnected?.Invoke();
                }
            }

            // Notify waiting clients
            if (newStatus == RelayStatus.Running)
            {
                NotifyWaitingClients(success: true);
            }
            else if (newStatus == RelayStatus.Failed)
            {
                NotifyWaitingClients(success: false, error);
            }

            // Fire event on main thread - use MainThread.DispatchAndForget to ensure
            // the event fires correctly even when TransitionTo is called from a background thread
            MainThread.DispatchIfNeeded(() => StateChanged?.Invoke());
        }

        void NotifyWaitingClients(bool success, string error = null)
        {
            List<TaskCompletionSource<WebSocketRelayClient>> clients;
            lock (m_WaitingClients)
            {
                clients = new List<TaskCompletionSource<WebSocketRelayClient>>(m_WaitingClients);
            }

            foreach (var tcs in clients)
            {
                if (success)
                    tcs.TrySetResult(m_Client);
                else
                    tcs.TrySetException(new RelayConnectionException(error ?? "Connection failed"));
            }
        }

        void Update()
        {
            // Only attempt reconnection when in Connecting state (process running, WebSocket needs connection)
            if (m_State.Status != RelayStatus.Connecting)
                return;

            // Handle automatic reconnection with throttling
            if (!m_IsReconnecting)
            {
                float currentTime = (float)EditorApplication.timeSinceStartup;
                if (currentTime - m_LastConnectionAttemptTime > k_ReconnectIntervalSeconds)
                {
                    _ = ReconnectAsync();
                }
            }
        }

        void OnEditorQuitting()
        {
            // Capture the process handle before Cleanup() nulls it
            var processHandle = m_ProcessHandle;

            if (m_Client?.IsConnected == true)
            {
                try
                {
                    // Send shutdown synchronously, matching SendWaitingDomainReloadMessage pattern.
                    // Task.Run escapes Unity's SynchronizationContext to prevent deadlocks.
                    // 2s timeout is a safety net; on localhost the round-trip is < 5ms.
                    Task.Run(() => m_Client.ShutdownServerAsync()).Wait(2000);
                }
                catch
                {
                    // Ignore errors during shutdown
                }
            }

            // Kill the relay process tree (relay + all its children including ACP subprocesses).
            // Even if the relay's own killProcessTree worked, this ensures nothing survives.
            if (processHandle != null)
            {
                try
                {
                    KillProcessTree(processHandle);
                }
                catch
                {
                    // Ignore errors during process termination
                }
            }

            Cleanup();
            ClearPersistedState();
        }

        void SendWaitingDomainReloadMessage()
        {
            if (m_Client?.IsConnected != true)
                return;

            // Synchronously wait for the relay to acknowledge it has paused its message queues.
            // Task.Run() escapes Unity's SynchronizationContext, preventing deadlocks.
            // On localhost, the round-trip is < 5ms. The 2s timeout is a safety net.
            try
            {
                Task.Run(() => m_Client.SendWaitingDomainReloadWithAckAsync()).Wait(2000);
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[RelayService] Error sending domain reload message: {ex.Message}");
            }
        }

        void Cleanup()
        {
            EditorApplication.update -= Update;
            EditorApplication.quitting -= OnEditorQuitting;

            m_Bus.DetachTransport();

            if (m_Client != null)
            {
                m_Client.Dispose();
                m_Client = null;
            }

            m_ProcessHandle = null;
            m_IsConnectedToExternalServer = false;
            m_IsReconnecting = false;
            m_Capabilities = Array.Empty<string>();
            m_RelayVersion = null;
            m_VersionMismatchError = null;
        }

        void ClearPersistedState()
        {
            // Clear in-memory fields
            m_Port = 0;
            m_McpPort = 0;
            m_ProcessId = 0;

            // Clear EditorPrefs
            string portKey = $"{k_RelayPortPrefix}{m_EditorProcessId}";
            EditorPrefs.DeleteKey(portKey);

            string mcpPortKey = $"{k_McpClientPortPrefix}{m_EditorProcessId}";
            EditorPrefs.DeleteKey(mcpPortKey);

            string pidKey = $"{k_RelayProcessIdPrefix}{m_EditorProcessId}";
            EditorPrefs.DeleteKey(pidKey);
        }

        int GetPersistedPort()
        {
            string key = $"{k_RelayPortPrefix}{m_EditorProcessId}";
            return EditorPrefs.GetInt(key, 0);
        }

        int GetPersistedMcpPort()
        {
            string key = $"{k_McpClientPortPrefix}{m_EditorProcessId}";
            return EditorPrefs.GetInt(key, 0);
        }

        void SetPersistedPort(int port)
        {
            m_Port = port; // Update in-memory field
            string key = $"{k_RelayPortPrefix}{m_EditorProcessId}";
            EditorPrefs.SetInt(key, port);
        }

        void SetPersistedMcpPort(int port)
        {
            m_McpPort = port; // Update in-memory field
            string key = $"{k_McpClientPortPrefix}{m_EditorProcessId}";
            EditorPrefs.SetInt(key, port);
        }

        int GetPersistedProcessId()
        {
            string key = $"{k_RelayProcessIdPrefix}{m_EditorProcessId}";
            return EditorPrefs.GetInt(key, 0);
        }

        void SetPersistedProcessId(int processId)
        {
            m_ProcessId = processId; // Update in-memory field
            string key = $"{k_RelayProcessIdPrefix}{m_EditorProcessId}";
            EditorPrefs.SetInt(key, processId);
        }

        static string GetRelayExecutablePath()
        {
            string platform = GetCurrentPlatform();

            if (platform == "mac")
            {
                if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                    return Path.Combine(k_RelayPath, "relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64");
                if (RuntimeInformation.OSArchitecture == Architecture.X64)
                    return Path.Combine(k_RelayPath, "relay_mac_x64.app/Contents/MacOS/relay_mac_x64");

                throw new Exception($"Could not find relay paths. {RuntimeInformation.OSArchitecture} compatible relay does not exist");
            }

            return Path.Combine(k_RelayPath, $"relay_{platform}");
        }

        static void ForceUnpackExecutable()
        {
            if (GetCurrentPlatform() != "mac")
                return;

            try
            {
                if (!RelayUtility.TryGetMacZipFileName(out string zipName))
                {
                    Trace.Warn("Failed to get a relay zip name for the current platform. This should never happen.");
                    return;
                }
                
                string macosxPath = Path.Combine(k_RelayPath, "__MACOSX");
                if (Directory.Exists(macosxPath))
                    Directory.Delete(macosxPath, true);

                string appPath = Path.Combine(k_RelayPath, $"{zipName}.app");
                if (Directory.Exists(appPath))
                    Directory.Delete(appPath, true);
                
                RelayUtility.UnzipAndSetMacAppPermissions(k_RelayPath, zipName, k_RelayPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[RelayService] Failed to unzip or set permissions\n{exception}");
                throw;
            }
        }

        /// <summary>
        /// Kill a process and its entire child-process tree.
        /// On Windows, Process.Kill() only terminates the direct process.
        /// This helper uses taskkill /T /F on Windows to tear down the full
        /// tree, falling back to Process.Kill() on other platforms or if taskkill fails.
        /// </summary>
        static void KillProcessTree(Process process)
        {
            if (process == null || process.HasExited)
                return;

            var pid = process.Id;

#if UNITY_EDITOR_WIN
            try
            {
                using var taskkill = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/T /F /PID {pid}",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                });
                taskkill?.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[RelayService] taskkill failed to start for PID {pid}: {ex.Message}");
            }

            // Fallback: taskkill may have timed out or failed with a non-zero exit code
            // without throwing. Ensure the process is dead.
            try { if (!process.HasExited) process.Kill(); }
            catch { /* already exited */ }
#else
            try { if (!process.HasExited) process.Kill(); }
            catch { /* already exited */ }
#endif
        }

        static string GetCurrentPlatform()
        {
#if UNITY_EDITOR_WIN
            return "win.exe";
#elif UNITY_EDITOR_OSX
            return "mac";
#elif UNITY_EDITOR_LINUX
            return "linux";
#else
            throw new NotSupportedException("Unsupported platform");
#endif
        }

        /// <summary>
        /// Request relay server to replay incomplete message.
        /// </summary>
        public async Task<bool> ReplayIncompleteMessageAsync()
        {
            if (m_Client?.IsConnected == true)
            {
                try
                {
                    return await m_Client.ReplayIncompleteMessageAsync();
                }
                catch (Exception ex)
                {
                    InternalLog.LogError($"[RelayService] Error replaying incomplete message: {ex.Message}");
                    return false;
                }
            }

            InternalLog.LogWarning("[RelayService] Not connected to relay server");
            return false;
        }

    }
}
