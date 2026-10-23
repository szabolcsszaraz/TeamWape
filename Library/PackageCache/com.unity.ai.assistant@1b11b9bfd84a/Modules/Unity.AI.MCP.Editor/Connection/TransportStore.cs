using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.MCP.Editor.Connection;
using Unity.AI.MCP.Editor.Helpers;
using Unity.AI.MCP.Editor.Models;
using Unity.AI.MCP.Editor.Security;
using UnityEngine;

namespace Unity.AI.MCP.Editor
{
    /// <summary>
    /// Per-transport connection approval state.
    /// </summary>
    enum ConnectionApprovalState
    {
        Unknown,           // Just connected, validation not started
        Validating,        // Background validation in progress
        AwaitingApproval,  // Validation done, waiting for user
        Approved,          // Tool calls allowed
        Denied,            // Tool calls rejected
        GatewayApproved    // ACP fast-path, tool calls allowed
    }

    /// <summary>
    /// Consolidated state for a single MCP transport connection.
    /// All per-transport data lives here instead of scattered across multiple dictionaries.
    /// </summary>
    class TransportState
    {
        public readonly IConnectionTransport Transport;

        /// <summary>
        /// Identity key. Starts as "pending-{ConnectionId}", updated to real
        /// CombinedIdentityKey after validation completes.
        /// </summary>
        public string IdentityKey;

        public ConnectionApprovalState ApprovalState;
        public ValidationDecision ValidationDecision;

        /// <summary>
        /// ACP token consumed during validation to determine gateway vs direct.
        /// </summary>
        public string PendingAcpToken;

        /// <summary>
        /// Persistent ACP token that survives validation consumption.
        /// Used by late gateway upgrade when relay session registration
        /// arrives after the MCP server has already connected.
        /// </summary>
        public string PersistentAcpToken;

        /// <summary>
        /// MCP client info received via set_client_info command.
        /// Typically arrives before validation completes.
        /// </summary>
        public ClientInfo ClientInfo;

        public bool IsGateway;

        public TransportState(IConnectionTransport transport, string initialIdentityKey)
        {
            Transport = transport;
            IdentityKey = initialIdentityKey;
        }
    }

    /// <summary>
    /// Thread-safe runtime store for MCP transport state.
    /// Mirrors <see cref="ConnectionStore"/> pattern — static, encapsulates all transport lifecycle data.
    ///
    /// Supports multiple transports per identity key (e.g., multiple Claude Code instances
    /// running the same executable). Approval is per-identity (approve once per executable),
    /// but each physical connection is tracked separately for display and capacity enforcement.
    /// </summary>
    static class TransportStore
    {
        /// <summary>Primary store: transport → state.</summary>
        static readonly ConcurrentDictionary<IConnectionTransport, TransportState> States = new();

        /// <summary>
        /// Reverse index: identity key → set of transports.
        /// Multiple transports can share the same identity key (same executable, different processes).
        /// Inner ConcurrentDictionary used as a thread-safe HashSet.
        /// </summary>
        static readonly ConcurrentDictionary<string, ConcurrentDictionary<IConnectionTransport, byte>> IdentityToTransports = new();

        /// <summary>
        /// Gateway identity keys (exempt from capacity limit).
        /// Uses ConcurrentDictionary as a thread-safe HashSet.
        /// </summary>
        static readonly ConcurrentDictionary<string, byte> GatewayIdentityKeys = new();

        /// <summary>
        /// Lock for compound identity key updates that span multiple maps.
        /// Individual reads use ConcurrentDictionary's built-in thread safety.
        /// </summary>
        static readonly object IdentityLock = new();

        // ──────────────────────────────────────────────
        //  Registration
        // ──────────────────────────────────────────────

        /// <summary>
        /// Register a new transport with an initial identity key.
        /// Called when a client first connects, before validation.
        /// </summary>
        public static TransportState Register(IConnectionTransport transport, string initialIdentityKey, bool isGateway = false)
        {
            var state = new TransportState(transport, initialIdentityKey) { IsGateway = isGateway };
            States[transport] = state;

            var set = IdentityToTransports.GetOrAdd(initialIdentityKey, _ => new ConcurrentDictionary<IConnectionTransport, byte>());
            set[transport] = 0;

            if (isGateway)
                GatewayIdentityKeys[initialIdentityKey] = 0;

            return state;
        }

        // ──────────────────────────────────────────────
        //  Identity key lifecycle
        // ──────────────────────────────────────────────

        /// <summary>
        /// Update a transport's identity key after validation resolves the real identity.
        /// Multiple transports can share the same identity key — each is tracked separately.
        /// </summary>
        public static void UpdateIdentityKey(IConnectionTransport transport, string newKey, bool isGateway)
        {
            if (!States.TryGetValue(transport, out var state))
                return;

            lock (IdentityLock)
            {
                // Remove from old key's set
                var oldKey = state.IdentityKey;
                if (oldKey != null && IdentityToTransports.TryGetValue(oldKey, out var oldSet))
                {
                    oldSet.TryRemove(transport, out _);
                    // Clean up empty key
                    if (oldSet.IsEmpty)
                    {
                        IdentityToTransports.TryRemove(oldKey, out _);
                        GatewayIdentityKeys.TryRemove(oldKey, out _);
                    }
                }

                // Add to new key's set
                var newSet = IdentityToTransports.GetOrAdd(newKey, _ => new ConcurrentDictionary<IConnectionTransport, byte>());
                newSet[transport] = 0;

                state.IdentityKey = newKey;
                state.IsGateway = isGateway;
                if (isGateway)
                    GatewayIdentityKeys[newKey] = 0;
            }
        }

        /// <summary>
        /// Mark an existing identity key as a gateway key (exempt from capacity limit).
        /// Used when a connection is upgraded to gateway after initial registration.
        /// </summary>
        public static void MarkAsGateway(string identityKey)
        {
            GatewayIdentityKeys[identityKey] = 0;
        }

        // ──────────────────────────────────────────────
        //  Lookups
        // ──────────────────────────────────────────────

        /// <summary>
        /// Get the state for a transport. Returns null if not registered.
        /// </summary>
        public static TransportState GetState(IConnectionTransport transport)
        {
            States.TryGetValue(transport, out var state);
            return state;
        }

        /// <summary>
        /// Get any transport for an identity key. Returns null if not found.
        /// When multiple transports share a key, returns an arbitrary one.
        /// </summary>
        public static IConnectionTransport GetTransportByIdentity(string identityKey)
        {
            if (IdentityToTransports.TryGetValue(identityKey, out var set))
                return set.Keys.FirstOrDefault();
            return null;
        }

        /// <summary>
        /// Get all transports for an identity key. Returns empty list if not found.
        /// Used by DisconnectConnectionByIdentity to close all instances.
        /// </summary>
        public static IReadOnlyList<IConnectionTransport> GetAllTransportsByIdentity(string identityKey)
        {
            if (IdentityToTransports.TryGetValue(identityKey, out var set))
                return set.Keys.ToList();
            return Array.Empty<IConnectionTransport>();
        }

        /// <summary>
        /// Find the transport that holds a specific persistent ACP token.
        /// Used for late gateway upgrade when the relay session registration
        /// arrives after the MCP server has already connected.
        /// </summary>
        public static IConnectionTransport FindTransportByAcpToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            foreach (var state in States.Values)
            {
                if (state.PersistentAcpToken == token)
                    return state.Transport;
            }
            return null;
        }

        // ──────────────────────────────────────────────
        //  Queries
        // ──────────────────────────────────────────────

        /// <summary>
        /// Get all active identity keys (snapshot).
        /// </summary>
        public static List<string> GetActiveIdentityKeys()
        {
            return IdentityToTransports.Keys.ToList();
        }

        /// <summary>
        /// Get the total number of connected transports.
        /// Counts actual transports, not unique identity keys.
        /// </summary>
        public static int CountConnections()
        {
            int count = 0;
            foreach (var set in IdentityToTransports.Values)
                count += set.Count;
            return count;
        }

        /// <summary>
        /// Get the number of direct (non-gateway) connections.
        /// Counts actual transports, not unique identity keys. For cap enforcement
        /// and logical-client deduping, use <see cref="ConnectionCensus.DirectCount"/>.
        /// </summary>
        public static int CountDirectConnections()
        {
            int count = 0;
            foreach (var kvp in IdentityToTransports)
            {
                if (!GatewayIdentityKeys.ContainsKey(kvp.Key))
                    count += kvp.Value.Count;
            }
            return count;
        }

        /// <summary>
        /// Check if an identity key is a gateway connection.
        /// </summary>
        public static bool IsGatewayKey(string identityKey)
        {
            return GatewayIdentityKeys.ContainsKey(identityKey);
        }

        /// <summary>
        /// Get all direct (non-gateway, non-pending) transports.
        /// Used for closing direct connections when a gateway connection arrives.
        /// </summary>
        public static IReadOnlyList<IConnectionTransport> GetDirectTransports()
        {
            var result = new List<IConnectionTransport>();
            foreach (var kvp in IdentityToTransports)
            {
                if (!GatewayIdentityKeys.ContainsKey(kvp.Key) && !kvp.Key.StartsWith("pending-"))
                {
                    result.AddRange(kvp.Value.Keys);
                }
            }
            return result;
        }

        /// <summary>
        /// Get all active (non-pending, connected) transport states.
        /// Returns one entry per physical connection — used by the settings UI
        /// to display each connection separately even when they share an identity.
        /// </summary>
        public static List<TransportState> GetActiveTransportStates()
        {
            var result = new List<TransportState>();
            foreach (var state in States.Values)
            {
                if (state.IdentityKey != null &&
                    !state.IdentityKey.StartsWith("pending-") &&
                    !state.IdentityKey.StartsWith("gateway-") &&
                    state.Transport.IsConnected)
                {
                    result.Add(state);
                }
            }
            return result;
        }

        // ──────────────────────────────────────────────
        //  Removal and cleanup
        // ──────────────────────────────────────────────

        /// <summary>
        /// Remove a transport and clean up all associated state.
        /// Returns the removed state for caller cleanup, or null if not found.
        /// </summary>
        public static TransportState Remove(IConnectionTransport transport)
        {
            if (!States.TryRemove(transport, out var state))
                return null;

            lock (IdentityLock)
            {
                var identityKey = state.IdentityKey;
                if (identityKey == null)
                    return state;

                if (IdentityToTransports.TryGetValue(identityKey, out var set))
                {
                    set.TryRemove(transport, out _);
                    // Clean up empty key
                    if (set.IsEmpty)
                    {
                        IdentityToTransports.TryRemove(identityKey, out _);
                        GatewayIdentityKeys.TryRemove(identityKey, out _);
                    }
                }
            }

            return state;
        }

        /// <summary>
        /// Remove all transports and return them for closing.
        /// Called when the Bridge stops.
        /// </summary>
        public static IConnectionTransport[] Clear()
        {
            // Collect from both States and reverse index to catch everything
            var toClose = States.Keys
                .Concat(IdentityToTransports.Values.SelectMany(set => set.Keys))
                .Distinct()
                .ToArray();

            States.Clear();
            IdentityToTransports.Clear();
            GatewayIdentityKeys.Clear();

            return toClose;
        }
    }
}
