using System;
using System.Linq;
using Newtonsoft.Json;
using Unity.AI.MCP.Editor.Connection;
using Unity.AI.MCP.Editor.Models;
using Unity.AI.MCP.Editor.Settings;
using Unity.AI.MCP.Editor.ToolRegistry;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.MCP.Editor
{
    /// <summary>
    /// Main API for controlling the Unity MCP Bridge lifecycle and querying connection status.
    /// The bridge enables MCP clients (like Claude Code) to connect to Unity Editor and invoke registered tools.
    /// </summary>
    /// <remarks>
    /// The bridge operates as an IPC server that:
    /// - Listens for incoming connections from MCP clients via named pipes (Windows) or Unix sockets (Mac/Linux)
    /// - Routes tool invocation requests to the <see cref="McpToolRegistry"/>
    /// - Manages client connections and security validation
    ///
    /// Lifecycle:
    /// - Automatically started at editor load if enabled in project settings
    /// - Can be manually controlled via <see cref="Start"/> and <see cref="Stop"/>
    /// - Persists across domain reloads (script compilation)
    ///
    /// The bridge can be disabled entirely via <see cref="Enabled"/> property or project settings.
    /// In batch mode, the bridge respects the batchModeEnabled setting and UNITY_MCP_DISABLE_BATCH environment variable.
    /// </remarks>
    public static class UnityMCPBridge
    {
        static Bridge s_Instance;
        static bool IsAllowedInBatchMode
        {
            get
            {
                if (Application.isBatchMode)
                    if (!MCPSettingsManager.Settings.batchModeEnabled)
                        return false;
                    else if(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNITY_MCP_DISABLE_BATCH")))
                        return false;
                return true;
            }
        }

        static bool IsAllowed => IsAllowedInBatchMode;

        /// <summary>
        /// Gets whether the MCP bridge is currently running and accepting connections.
        /// </summary>
        /// <remarks>
        /// Returns true when the bridge has successfully started and is listening for connections.
        /// The bridge may stop temporarily during domain reloads (script compilation) and restart automatically.
        /// </remarks>
        public static bool IsRunning => s_Instance?.IsRunning == true;

        /// <summary>
        /// Gets or sets whether the MCP bridge is enabled in project settings.
        /// </summary>
        public static bool Enabled
        {
            get => MCPSettingsManager.Settings.bridgeEnabled;
            set
            {
                if (MCPSettingsManager.Settings.bridgeEnabled == value) return;
                MCPSettingsManager.Settings.bridgeEnabled = value;
                MCPSettingsManager.SaveSettings();
                if (value)
                {
                    EnsureInstance();
                }
                else
                {
                    DisposeInstance();
                }
            }
        }

        /// <summary>
        /// Initializes the MCP bridge at editor load time if enabled in project settings.
        /// This method is called automatically by Unity's InitializeOnLoadMethod mechanism.
        /// </summary>
        [InitializeOnLoadMethod]
        public static void Init()
        {
            // Forward the legacy public MaxDirectConnections API onto the census's
            // PolicyChanged stream so existing subscribers keep receiving events.
            ConnectionCensus.PolicyChanged += NotifyMaxDirectConnectionsPolicyChanged;

            if (!IsAllowed) return;
            if (MCPSettingsManager.Settings.bridgeEnabled)
            {
                EnsureInstance();
            }
        }

        /// <summary>
        /// Manually starts the MCP bridge if it's not already running.
        /// If the bridge is not enabled in settings, this will enable it first.
        /// </summary>
        public static void Start()
        {
            if (!IsAllowed) return;
            if (!Enabled) Enabled = true;
            s_Instance?.Start();
        }

        /// <summary>
        /// Manually stops the MCP bridge, disconnecting all clients.
        /// This does not change the <see cref="Enabled"/> setting.
        /// </summary>
        public static void Stop() => s_Instance?.Stop();

        /// <summary>
        /// Prints all registered MCP tool schemas to the console and copies them to the clipboard.
        /// </summary>
        public static void PrintToolSchemas()
        {
            var tools = McpToolRegistry.GetAvailableTools();
            var prettyJson = JsonConvert.SerializeObject(tools, Formatting.Indented);

            EditorGUIUtility.systemCopyBuffer = prettyJson;
            Debug.Log($"=== MCP Tool Schemas ({tools.Length} tools) ===\n{prettyJson}");
        }

        /// <summary>
        /// Prints information about all currently connected MCP clients to the console.
        /// </summary>
        public static void PrintClientInfo()
        {
            if (s_Instance == null)
            {
                Debug.Log("MCP Bridge not initialized");
                return;
            }

            string clientInfo = s_Instance.GetClientInfo();
            string status = s_Instance.IsRunning ? "running" : "stopped";
            string connectionPath = s_Instance.CurrentConnectionPath ?? "not-started";

            string info = $"=== MCP Client Info ===\n" +
                         $"Bridge Status: {status}\n" +
                         $"Connection Path: {connectionPath}\n" +
                         $"Client: {clientInfo}";

            EditorGUIUtility.systemCopyBuffer = clientInfo;
            Debug.Log(info);
        }

        /// <summary>
        /// Gets information about all currently connected MCP clients.
        /// </summary>
        public static ClientInfo[] GetConnectedClients() =>
            s_Instance?.GetConnectedClients() ?? Array.Empty<ClientInfo>();

        /// <summary>
        /// Gets the number of currently connected MCP clients.
        /// </summary>
        public static int GetConnectedClientCount() =>
            s_Instance?.GetConnectedClientCount() ?? 0;

        /// <summary>
        /// Gets the identity keys of all currently connected clients.
        /// </summary>
        public static string[] GetActiveIdentityKeys() =>
            s_Instance?.GetActiveIdentityKeys()?.ToArray() ?? Array.Empty<string>();

        /// <summary>
        /// Disconnects any active client connections matching the specified identity.
        /// </summary>
        public static void DisconnectConnectionByIdentity(ConnectionIdentity identity) =>
            s_Instance?.DisconnectConnectionByIdentity(identity);

        /// <summary>
        /// Disconnects all active client connections.
        /// </summary>
        public static void DisconnectAll() => s_Instance?.DisconnectAll();

        // ──────────────────────────────────────────────
        //  Legacy MaxDirectConnections policy facade
        // ──────────────────────────────────────────────
        //
        // The authoritative policy lives on ConnectionCensus.Policy. These
        // members are preserved for back-compat with existing consumers that
        // read the direct cap via the Bridge facade. Setting the resolver
        // writes through to the census policy; the event mirrors
        // ConnectionCensus.PolicyChanged for convenience.

        /// <summary>
        /// Resolver for the maximum number of concurrent direct (non-gateway) connections.
        /// Return -1 for unlimited, or a positive integer for a hard cap.
        /// Writing this resolver mutates <see cref="ConnectionCensus.Policy"/>.
        /// </summary>
        public static Func<int> MaxDirectConnectionsResolver
        {
            get => () => ConnectionCensus.Policy.MaxDirect;
            set
            {
                var policy = ConnectionCensus.Policy;
                int next = value != null ? value() : -1;
                ConnectionCensus.SetPolicy(new ConnectionPolicy(next, policy.MaxGateway));
            }
        }

        /// <summary>
        /// Raised when the effective max-direct-connections policy may have changed.
        /// Mirrors <see cref="ConnectionCensus.PolicyChanged"/> for consumers that
        /// historically subscribed here.
        /// </summary>
        public static event Action MaxDirectConnectionsPolicyChanged;

        /// <summary>
        /// Resets the direct policy by delegating to the census, which
        /// <c>AcpEntitlementWiring.Apply</c> re-installs as unlimited (entitlement
        /// limits are not enforced), so this does not round-trip back to an
        /// entitlement-derived cap. Always fires
        /// <see cref="MaxDirectConnectionsPolicyChanged"/> for back-compat,
        /// matching the pre-census contract ("reset notifies regardless of
        /// whether the value actually changed").
        /// </summary>
        public static void ResetMaxDirectConnectionsResolver()
        {
            // Re-applying account settings restores the full policy to its
            // entitlement-driven values. If the policy actually changes, the
            // census fires PolicyChanged which our Init() forwarder translates
            // to MaxDirectConnectionsPolicyChanged; if it's already at the
            // entitlement values, SetPolicy is a no-op, so we fire explicitly.
            var before = ConnectionCensus.Policy;
            Connection.AcpEntitlementWiring.Apply();
            if (ConnectionCensus.Policy.Equals(before))
                NotifyMaxDirectConnectionsPolicyChanged();
        }

        /// <summary>
        /// Notify subscribers that the effective direct cap may have changed.
        /// Called automatically when <see cref="ConnectionCensus.PolicyChanged"/>
        /// fires; may also be invoked explicitly by tests or dev tools.
        /// </summary>
        public static void NotifyMaxDirectConnectionsPolicyChanged()
        {
            MaxDirectConnectionsPolicyChanged?.Invoke();
        }

        static void EnsureInstance() => s_Instance ??= new Bridge(autoScheduleStart: true);

        static void DisposeInstance()
        {
            if (s_Instance == null) return;
            try { s_Instance.Dispose(); } catch { }
            s_Instance = null;
        }
    }
}
