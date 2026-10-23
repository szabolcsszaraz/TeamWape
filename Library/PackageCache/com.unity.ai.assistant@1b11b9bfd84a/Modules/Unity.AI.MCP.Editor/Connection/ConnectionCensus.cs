using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Acp;
using Unity.AI.MCP.Editor.Models;
using Unity.AI.MCP.Editor.Security;
using Unity.AI.Toolkit;

namespace Unity.AI.MCP.Editor.Connection
{
    /// <summary>
    /// Identifies which cap caused a <see cref="ConnectionCensus"/> reservation to fail.
    /// </summary>
    enum CapKind
    {
        None = 0,
        /// <summary>A per-pool cap (direct or gateway) was hit.</summary>
        Pool,
    }

    /// <summary>
    /// Immutable policy snapshot. Measured in distinct logical clients. The
    /// gateway pool is canonical: a client present in both pools consumes a
    /// gateway slot only — its direct transports are "free" so a single agent
    /// using the AI Gateway plus its own MCP probe can't crowd a separate
    /// third-party MCP client out of the direct pool. -1 means "unlimited".
    /// </summary>
    record ConnectionPolicy(int MaxDirect, int MaxGateway)
    {
        public static ConnectionPolicy Unlimited { get; } = new(-1, -1);
    }

    /// <summary>
    /// Outcome of a reservation attempt. Carries the live count and cap so
    /// callers can build an accurate denial message without re-reading state.
    /// <see cref="ClientKey"/> is set when the attempt provided a
    /// <c>ConnectionInfo</c>; <c>null</c> for the "no info" pre-check path.
    /// </summary>
    record ReservationResult(
        CapKind RejectedBy,
        int PoolCount,
        int PoolCap,
        string ClientKey)
    {
        public bool Allowed => RejectedBy == CapKind.None;
    }

    /// <summary>Snapshot view of a logical client used by developer tools.</summary>
    record LogicalClientSnapshot(
        string ClientKey,
        int? RootPid,
        string ExecutableKey,
        string DisplayName,
        int DirectTransportCount,
        int GatewayTransportCount,
        int AcpSessionCount)
    {
        public bool HasGateway => GatewayTransportCount > 0 || AcpSessionCount > 0;
        public bool HasDirect => DirectTransportCount > 0;
    }

    /// <summary>
    /// Single authoritative source of truth for active MCP / AI Gateway connection
    /// state. Owns the <see cref="ConnectionPolicy"/> (entitlement-driven caps) and
    /// the live logical-client table, and exposes one reservation entry point per
    /// pool that returns a self-describing <see cref="ReservationResult"/>.
    /// </summary>
    /// <remarks>
    /// Logical clients are deduped by deepest non-shell ancestor PID AND by
    /// <c>ExecutableIdentityComparer</c> key, so the same external agent shows
    /// up once even if it opens many transports or appears in both pools.
    /// Cap accounting is asymmetric: the gateway pool is canonical, so a client
    /// already in the gateway pool is excluded from the direct-pool count. This
    /// matches the user-visible "N agents" model — a single agent using both
    /// transports (e.g. Claude over the AI Gateway plus its own configured MCP
    /// probe) consumes one gateway slot and zero direct slots, leaving the
    /// direct pool free for a separate third-party client.
    /// </remarks>
    static class ConnectionCensus
    {
        /// <summary>Internal record of a logical client (an external application instance).</summary>
        class LogicalClient
        {
            public string ClientKey;
            public int? RootPid;
            public string ExecutableKey;
            public string DisplayName;
            // Monotonic registration sequence number used for oldest-first
            // eviction when the installed policy tightens. Stored explicitly
            // rather than relying on Dictionary<> iteration order, which is
            // not preserved across Add/Remove cycles.
            public long RegistrationSequence;
            public readonly HashSet<IConnectionTransport> DirectTransports = new();
            public readonly HashSet<IConnectionTransport> GatewayTransports = new();
            public readonly HashSet<AssistantConversationId> AcpSessions = new();

            public bool HasGateway => GatewayTransports.Count > 0 || AcpSessions.Count > 0;
            public bool HasDirect => DirectTransports.Count > 0;
            public bool IsEmpty => !HasGateway && !HasDirect;

            // Whether this client counts toward the per-pool gateway cap. An
            // ACP-only client (AcpSession tracked but no transport yet) is a
            // placeholder produced by AcpSessionRegistry.Acquire before the
            // real agent process has connected. It must stay visible in the
            // total tally (so concurrent acquires and total-cap enforcement
            // see it) but must NOT consume the one per-pool slot it is about
            // to unlock — otherwise at tier=1 the real gateway transport is
            // denied and the session it represents deadlocks. After Bridge
            // calls AttachAcpSessionToClient, the AcpSession moves onto the
            // process-identified client whose GatewayTransports.Count > 0, so
            // the post-merge client correctly counts toward the pool again.
            public bool IsGatewayPooled => GatewayTransports.Count > 0;
        }

        static readonly Dictionary<string, LogicalClient> s_Clients = new();
        static readonly ConcurrentDictionary<IConnectionTransport, string> s_TransportToClient = new();
        static readonly ConcurrentDictionary<AssistantConversationId, string> s_SessionToClient = new();

        // Secondary index used by LookupClientLocked. Two aliases per client:
        //   "pid:{RootPid}"   — indexed whenever the client has a known PID.
        //   "exe:{ExeKey}"    — indexed ONLY when the client has no known PID.
        //
        // The exe alias is a fallback for cases where FindMcpClient failed
        // to walk the parent chain (so info.Client is null / ProcessId is
        // zero). In the common case, every connection has a resolved parent
        // PID and only the pid alias is published — that way two independent
        // claude.exe processes at different PIDs are counted as two distinct
        // logical clients, matching the "per-connection" billing model.
        // If a client registers first without a PID (exe alias published)
        // and later an incoming connection on that same exe DOES carry a
        // PID, GetOrCreateClientLocked upgrades the record and
        // IndexAliasesLocked swaps in the pid alias / removes the exe alias
        // so subsequent different-PID connections with the same exe are
        // correctly treated as new clients.
        static readonly Dictionary<string, string> s_KeyAliases = new();

        static readonly object s_Lock = new();
        static bool s_AcpListenerAttached;
        static ConnectionPolicy s_Policy = ConnectionPolicy.Unlimited;
        static long s_NextRegistrationSequence;

        /// <summary>Raised on any logical-client registration / unregistration. Always marshalled via EditorTask.delayCall.</summary>
        public static event Action Changed;

        /// <summary>Raised when <see cref="Policy"/> changes. Subscribers read <see cref="Policy"/> for the new values.</summary>
        public static event Action PolicyChanged;

        /// <summary>Current entitlement-driven policy. Read-only; mutate via <see cref="SetPolicy"/>.</summary>
        public static ConnectionPolicy Policy => s_Policy;

        /// <summary>
        /// Install a new policy. Equal values are a no-op (no event fired).
        /// If the cap for either pool tightens below the live count (e.g. the
        /// user signed out of a Pro org and the new account allows fewer
        /// concurrent sessions), the oldest connections in that pool are
        /// evicted until the count fits. Eviction is dispatched via
        /// <c>EditorTask.delayCall</c> so <see cref="SetPolicy"/> remains
        /// synchronous and the lock isn't held while tearing down transports
        /// (which would reenter the census via <see cref="UnregisterTransport"/>).
        /// </summary>
        public static void SetPolicy(ConnectionPolicy policy)
        {
            ConnectionPolicy previous;
            lock (s_Lock)
            {
                if (s_Policy.Equals(policy)) return;
                previous = s_Policy;
                s_Policy = policy;
            }
            PolicyChanged?.Invoke();

            bool gatewayTightened = Tightened(previous.MaxGateway, policy.MaxGateway);
            bool directTightened = Tightened(previous.MaxDirect, policy.MaxDirect);
            if (gatewayTightened || directTightened)
                EditorTask.delayCall += EnforcePolicyFireAndForget;
        }

        // Fire-and-forget wrapper so EditorTask.delayCall (Action event) can
        // invoke the async enforcement path. async void is intentional here:
        // the caller is an editor update tick and nothing awaits this —
        // exceptions are swallowed inside EvictOverflowAsync so an unhandled
        // throw cannot crash the editor.
        static async void EnforcePolicyFireAndForget()
        {
            try { await EnforcePolicyAsync(); }
            catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
        }

        /// <summary>
        /// Revokes connections that no longer fit the installed policy.
        /// Called after a tightening <see cref="SetPolicy"/>; exposed as
        /// <c>internal</c> so tests can observe it synchronously.
        /// Gateway is enforced before direct because the gateway pool is
        /// canonical (see <see cref="CountLocked"/>): evicting a gateway
        /// client may move it into the direct-pool count and change the
        /// overflow picture.
        /// </summary>
        internal static async Task EnforcePolicyAsync()
        {
            await EvictOverflowAsync(isGateway: true);
            await EvictOverflowAsync(isGateway: false);
        }

        static async Task EvictOverflowAsync(bool isGateway)
        {
            // Collect the overflow under the lock, then run tear-down outside
            // it so the lock isn't held across await boundaries (session
            // EndAsync can take tens of ms on slow agents).
            List<LogicalClient> victims;
            lock (s_Lock)
            {
                int cap = PoolCap(isGateway);
                if (cap < 0) return; // unlimited
                int count = CountLocked(isGateway);
                int overflow = count - cap;
                if (overflow <= 0) return;

                // Evict the oldest clients first so a user who just opened a
                // session mid-org-switch isn't the one whose conversation
                // gets killed. Sort by the monotonic sequence stamped at
                // registration time — Dictionary<> iteration order is not
                // preserved across Add/Remove cycles, so we can't use it.
                var candidates = s_Clients.Values
                    .Where(c => isGateway
                        ? c.IsGatewayPooled
                        : (c.HasDirect && !c.IsGatewayPooled))
                    .OrderBy(c => c.RegistrationSequence)
                    .Take(overflow);
                victims = candidates.ToList();
            }

            foreach (var client in victims)
            {
                // Evict the whole logical client: when an agent loses its
                // gateway slot it should also lose any direct transports it
                // owned (see RegisterGatewayThenDirect_SameExe_GatewayAbsorbsDirectSlot)
                // otherwise we'd leave a detached direct-only zombie the
                // census silently reassigns to the direct pool. For direct-
                // pool eviction the converse is already true by construction:
                // a client in the direct pool has no gateway transports or
                // sessions (CountLocked's `!IsGatewayPooled` guard).
                //
                // Snapshot under lock so we don't enumerate a mutating
                // collection when the tear-down call-back reenters the census.
                List<IConnectionTransport> transports;
                List<AssistantConversationId> sessions;
                lock (s_Lock)
                {
                    transports = new List<IConnectionTransport>(client.DirectTransports.Count + client.GatewayTransports.Count);
                    transports.AddRange(client.DirectTransports);
                    transports.AddRange(client.GatewayTransports);
                    sessions = new List<AssistantConversationId>(client.AcpSessions);
                }

                foreach (var transport in transports)
                {
                    try { transport.Dispose(); }
                    catch { /* best-effort — UnregisterTransport is called from the transport's own cleanup */ }
                }

                foreach (var sessionId in sessions)
                {
                    try { await AcpSessionRegistry.RemoveAsync(sessionId); }
                    catch { /* best-effort — the census still converges via OnSessionRemoved */ }
                }
            }
        }

        /// <summary>
        /// True when <paramref name="next"/> represents a strictly-smaller cap
        /// than <paramref name="prev"/>. Unlimited (-1) is treated as larger
        /// than any finite cap, and unchanged values don't count.
        /// </summary>
        static bool Tightened(int prev, int next)
        {
            if (prev == next) return false;
            if (prev < 0) return true;     // prev unlimited → next finite = tighter
            if (next < 0) return false;    // next unlimited = looser
            return next < prev;
        }

        /// <summary>Distinct logical clients with at least one gateway transport or AcpSession.</summary>
        public static int GatewayCount { get { lock (s_Lock) return CountLocked(gateway: true); } }

        /// <summary>Distinct logical clients with at least one direct (non-gateway) transport.</summary>
        public static int DirectCount { get { lock (s_Lock) return CountLocked(gateway: false); } }

        /// <summary>Total distinct logical clients (any active connection).</summary>
        public static int LogicalClientCount { get { lock (s_Lock) return s_Clients.Count; } }

        /// <summary>
        /// Resolve the logical-client key for a ConnectionInfo. Prefers the
        /// stored ClientKey on any existing client the info maps to (so callers
        /// passing this key to <see cref="AttachAcpSessionToClient"/> hit the
        /// table), falling back to a freshly-built key only when no matching
        /// client exists yet.
        /// </summary>
        public static string ResolveClientKey(ConnectionInfo info)
        {
            if (info == null) return null;
            var (rootPid, exeKey) = Identify(info);
            lock (s_Lock)
            {
                var existing = LookupClientLocked(rootPid, exeKey);
                if (existing != null) return existing.ClientKey;
            }
            return BuildClientKey(rootPid, exeKey, info.DisplayName);
        }

        /// <summary>
        /// Try to reserve a slot in the direct MCP pool. Returns an
        /// <see cref="ReservationResult"/> describing the outcome:
        /// <list type="bullet">
        /// <item><c>Allowed=true</c> if the connection fits within the direct cap
        /// OR maps to an existing logical client (already counted).</item>
        /// <item><c>RejectedBy=Pool</c> if a NEW logical client would exceed <see cref="Policy"/>.MaxDirect.</item>
        /// </list>
        /// Note: this does NOT register the transport — call
        /// <see cref="RegisterDirectTransport"/> after the bridge has finalized approval.
        /// </summary>
        public static ReservationResult TryReserveDirect(ConnectionInfo info) =>
            EvaluateLocked(info, isGateway: false);

        /// <summary>
        /// Try to reserve a slot in the gateway pool. Honors logical-client dedup:
        /// an agent that already exists (in the same or the other pool) only hits
        /// the gateway cap when it's joining that pool for the first time.
        /// </summary>
        public static ReservationResult TryReserveGateway(ConnectionInfo info) =>
            EvaluateLocked(info, isGateway: true);

        /// <summary>
        /// Optimistic gateway pre-check used before any agent has connected (e.g.
        /// <c>AcpSessionRegistry.Acquire</c>). Conservatively treats the request
        /// as a brand-new logical client. The Bridge's gateway fast path runs a
        /// second, dedup-aware check via <see cref="TryReserveGateway"/> once the
        /// agent actually connects, so a re-using same-process client is not
        /// rejected by this pre-check.
        /// </summary>
        public static ReservationResult TryReserveGatewaySlot()
        {
            lock (s_Lock) return EvaluateNewClientLocked(isGateway: true, clientKey: null);
        }

        /// <summary>Register a direct MCP transport with the census after approval.</summary>
        public static void RegisterDirectTransport(IConnectionTransport transport, ConnectionInfo info) =>
            RegisterTransport(transport, info, isGateway: false, fallbackName: "direct");

        /// <summary>Register a gateway-fast-path MCP transport with the census.</summary>
        public static void RegisterGatewayTransport(IConnectionTransport transport, ConnectionInfo info) =>
            RegisterTransport(transport, info, isGateway: true, fallbackName: "gateway");

        /// <summary>
        /// Remove a transport from whatever pool it was registered in, cleaning
        /// up the logical client when no connections remain.
        /// </summary>
        public static void UnregisterTransport(IConnectionTransport transport)
        {
            if (transport == null) return;
            if (!s_TransportToClient.TryRemove(transport, out var clientKey)) return;

            bool changed = false;
            lock (s_Lock)
            {
                if (s_Clients.TryGetValue(clientKey, out var client))
                {
                    if (client.DirectTransports.Remove(transport) | client.GatewayTransports.Remove(transport))
                        changed = true;
                    if (client.IsEmpty)
                        RemoveClientLocked(client);
                }
            }

            if (changed) NotifyChanged();
        }

        /// <summary>
        /// Register an AcpSession. Used as an optimistic pre-check when the
        /// gateway agent process hasn't connected yet (so we have no
        /// <see cref="ConnectionInfo"/>); the session is later attached to a
        /// logical client via <see cref="AttachAcpSessionToClient"/>.
        /// </summary>
        public static void RegisterAcpSession(AssistantConversationId sessionId, string providerHint)
        {
            EnsureAcpListenerAttached();

            lock (s_Lock)
            {
                var clientKey = $"acp:{sessionId.Value}";
                if (!s_Clients.TryGetValue(clientKey, out var client))
                {
                    client = new LogicalClient
                    {
                        ClientKey = clientKey,
                        DisplayName = providerHint ?? "gateway-session",
                        RegistrationSequence = s_NextRegistrationSequence++
                    };
                    s_Clients[clientKey] = client;
                }
                client.AcpSessions.Add(sessionId);
                s_SessionToClient[sessionId] = clientKey;
            }

            NotifyChanged();
        }

        /// <summary>Remove an AcpSession from its logical client.</summary>
        public static void UnregisterAcpSession(AssistantConversationId sessionId)
        {
            if (!s_SessionToClient.TryRemove(sessionId, out var clientKey)) return;

            bool changed = false;
            lock (s_Lock)
            {
                if (s_Clients.TryGetValue(clientKey, out var client))
                {
                    if (client.AcpSessions.Remove(sessionId))
                        changed = true;
                    if (client.IsEmpty)
                        RemoveClientLocked(client);
                }
            }

            if (changed) NotifyChanged();
        }

        /// <summary>
        /// Attach an existing AcpSession to a logical client (e.g. once the
        /// gateway fast path has correlated the agent's process to the session
        /// token). Merges the placeholder "acp:{sessionId}" client into the real one.
        /// </summary>
        public static void AttachAcpSessionToClient(AssistantConversationId sessionId, string targetClientKey)
        {
            if (string.IsNullOrEmpty(targetClientKey)) return;
            bool changed = false;

            lock (s_Lock)
            {
                if (!s_SessionToClient.TryGetValue(sessionId, out var currentKey)) return;
                if (currentKey == targetClientKey) return;
                if (!s_Clients.TryGetValue(currentKey, out var current)) return;
                if (!s_Clients.TryGetValue(targetClientKey, out var target)) return;

                current.AcpSessions.Remove(sessionId);
                target.AcpSessions.Add(sessionId);
                s_SessionToClient[sessionId] = targetClientKey;
                changed = true;

                if (current.IsEmpty)
                    RemoveClientLocked(current);
            }

            if (changed) NotifyChanged();
        }

        /// <summary>Get a snapshot of all logical clients for developer-tool display.</summary>
        public static IReadOnlyList<LogicalClientSnapshot> Snapshot()
        {
            lock (s_Lock)
            {
                return s_Clients.Values
                    .Select(c => new LogicalClientSnapshot(
                        c.ClientKey, c.RootPid, c.ExecutableKey, c.DisplayName,
                        c.DirectTransports.Count, c.GatewayTransports.Count, c.AcpSessions.Count))
                    .ToList();
            }
        }

        /// <summary>Reset all state. Test-only / Bridge.Stop helper. Does NOT clear the installed <see cref="Policy"/>.</summary>
        internal static void Clear()
        {
            lock (s_Lock)
            {
                s_Clients.Clear();
                s_KeyAliases.Clear();
                s_NextRegistrationSequence = 0;
            }
            s_TransportToClient.Clear();
            s_SessionToClient.Clear();
            NotifyChanged();
        }

        // ──────────────────────────────────────────────
        //  Private helpers
        // ──────────────────────────────────────────────

        static ReservationResult EvaluateLocked(ConnectionInfo info, bool isGateway)
        {
            var (rootPid, exeKey) = Identify(info);

            lock (s_Lock)
            {
                var existing = LookupClientLocked(rootPid, exeKey);

                // Already counted in this pool: free. Return the stored ClientKey
                // (not a freshly-built one from `info`), because a client registered
                // earlier with partial identity keeps its original key even after
                // GetOrCreateClientLocked upgrades its RootPid/ExecutableKey fields.
                // Returning the rebuilt key here would cause callers doing
                // `AttachAcpSessionToClient(sessionId, result.ClientKey)` to miss
                // the lookup and silently no-op, breaking the ACP→gateway merge.
                bool alreadyInPool = existing != null
                    && (isGateway ? existing.HasGateway : existing.HasDirect);
                if (alreadyInPool)
                    return BuildResult(CapKind.None, isGateway, existing.ClientKey);

                string clientKey = BuildClientKey(rootPid, exeKey, info?.DisplayName);
                return EvaluateNewClientLocked(isGateway, clientKey);
            }
        }

        static ReservationResult EvaluateNewClientLocked(bool isGateway, string clientKey)
        {
            int poolCount = CountLocked(isGateway);
            int poolCap = PoolCap(isGateway);
            return poolCap >= 0 && poolCount >= poolCap
                ? BuildResult(CapKind.Pool, isGateway, clientKey)
                : BuildResult(CapKind.None, isGateway, clientKey);
        }

        static ReservationResult BuildResult(CapKind kind, bool isGateway, string clientKey) =>
            new(kind,
                PoolCount: CountLocked(isGateway),
                PoolCap: PoolCap(isGateway),
                ClientKey: clientKey);

        static int PoolCap(bool isGateway) => isGateway ? s_Policy.MaxGateway : s_Policy.MaxDirect;

        // Per-pool count used for cap enforcement.
        //
        // Gateway: count clients with a real gateway transport (IsGatewayPooled),
        // not just HasGateway (transport OR AcpSession). The ACP-only
        // placeholder created by AcpSessionRegistry.Acquire must not pre-claim
        // the one slot it is trying to unlock — see LogicalClient.IsGatewayPooled
        // for the full rationale (placeholder + tier=1 + real transport arriving
        // later would otherwise deadlock).
        //
        // Direct: count clients with a direct transport AND not already in the
        // gateway pool. The gateway pool is canonical: an agent using both
        // transports consumes one gateway slot, not also one direct slot. This
        // is what lets a Claude that has a gateway connection plus its own MCP
        // probe coexist with a separate Cursor at tier=1.
        static int CountLocked(bool gateway)
        {
            int count = 0;
            foreach (var client in s_Clients.Values)
            {
                if (gateway)
                {
                    if (client.IsGatewayPooled) count++;
                }
                else
                {
                    if (client.HasDirect && !client.IsGatewayPooled) count++;
                }
            }
            return count;
        }

        static void RegisterTransport(IConnectionTransport transport, ConnectionInfo info, bool isGateway, string fallbackName)
        {
            if (transport == null) return;
            EnsureAcpListenerAttached();

            lock (s_Lock)
            {
                var client = GetOrCreateClientLocked(info, fallbackName);
                (isGateway ? client.GatewayTransports : client.DirectTransports).Add(transport);
                s_TransportToClient[transport] = client.ClientKey;
            }

            NotifyChanged();
        }

        static (int? rootPid, string exeKey) Identify(ConnectionInfo info)
        {
            if (info == null) return (null, null);

            int? rootPid = info.Client?.ProcessId > 0
                ? info.Client.ProcessId
                : (info.Server?.ProcessId > 0 ? info.Server.ProcessId : (int?)null);

            string exeKey = info.Client?.Identity != null
                ? ExecutableIdentityComparer.GetIdentityKey(info.Client.Identity)
                : (info.Server?.Identity != null
                    ? ExecutableIdentityComparer.GetIdentityKey(info.Server.Identity)
                    : null);

            return (rootPid, exeKey);
        }

        static string BuildClientKey(int? rootPid, string exeKey, string fallbackName)
        {
            if (!rootPid.HasValue && string.IsNullOrEmpty(exeKey))
                return $"unknown:{fallbackName ?? Guid.NewGuid().ToString("N")}";
            return $"pid:{(rootPid?.ToString() ?? "?")}|exe:{exeKey ?? "?"}";
        }

        // When the incoming connection carries a resolved PID (the common
        // case, because FindMcpClient walks up the parent chain to the real
        // agent process), match only by that PID. Distinct PIDs = distinct
        // logical clients, even when the executable identity is the same —
        // this is what turns "two independent claude.exe processes" into
        // two billable connections instead of one.
        //
        // Only when the incoming connection has NO resolved PID do we fall
        // back to matching by executable identity. That path exists to
        // preserve probe-sharing semantics in the degenerate case where
        // FindMcpClient couldn't identify a parent: we still merge those
        // anonymous connections under whatever same-exe record happens to
        // exist, if any. Any client with a known PID is never published in
        // the exe alias table (see IndexAliasesLocked), so this fallback
        // can't accidentally merge a rootless connection into a properly
        // identified one and vice-versa.
        static LogicalClient LookupClientLocked(int? rootPid, string exeKey)
        {
            if (rootPid.HasValue)
            {
                return s_KeyAliases.TryGetValue($"pid:{rootPid.Value}", out var byPid)
                       && s_Clients.TryGetValue(byPid, out var pidClient)
                    ? pidClient
                    : null;
            }

            if (!string.IsNullOrEmpty(exeKey)
                && s_KeyAliases.TryGetValue($"exe:{exeKey}", out var byExe)
                && s_Clients.TryGetValue(byExe, out var exeClient))
                return exeClient;

            return null;
        }

        static LogicalClient GetOrCreateClientLocked(ConnectionInfo info, string fallbackName)
        {
            var (rootPid, exeKey) = Identify(info);
            string displayName = info?.DisplayName ?? fallbackName;

            var existing = LookupClientLocked(rootPid, exeKey);
            if (existing != null)
            {
                bool gainedPid = !existing.RootPid.HasValue && rootPid.HasValue;
                if (gainedPid) existing.RootPid = rootPid;
                if (string.IsNullOrEmpty(existing.ExecutableKey) && !string.IsNullOrEmpty(exeKey))
                    existing.ExecutableKey = exeKey;
                if (string.IsNullOrEmpty(existing.DisplayName) && !string.IsNullOrEmpty(displayName))
                    existing.DisplayName = displayName;
                // When the client just gained a PID, drop the exe alias
                // that previously pointed at it — otherwise a later
                // connection from a different PID but the same exe would
                // match the stale exe alias and incorrectly merge into
                // this client. After this call the record is only
                // reachable by its pid alias.
                if (gainedPid)
                    RemoveStaleAliasesLocked(existing);
                IndexAliasesLocked(existing);
                return existing;
            }

            string clientKey = BuildClientKey(rootPid, exeKey, displayName);
            if (!s_Clients.TryGetValue(clientKey, out var client))
            {
                client = new LogicalClient
                {
                    ClientKey = clientKey,
                    RootPid = rootPid,
                    ExecutableKey = exeKey,
                    DisplayName = displayName,
                    RegistrationSequence = s_NextRegistrationSequence++
                };
                s_Clients[clientKey] = client;
            }

            IndexAliasesLocked(client);
            return client;
        }

        // Enumerates every alias the client is *currently* reachable by.
        // When RootPid is known, we ONLY publish the pid alias — the exe
        // alias becomes shadow-only (used for display, not for lookup). See
        // the s_KeyAliases comment for the full rationale.
        static IEnumerable<string> AliasKeys(LogicalClient client)
        {
            if (client.RootPid.HasValue)
            {
                yield return $"pid:{client.RootPid.Value}";
                yield break;
            }
            if (!string.IsNullOrEmpty(client.ExecutableKey))
                yield return $"exe:{client.ExecutableKey}";
        }

        // Writes the current AliasKeys into the lookup table. Callers that
        // are upgrading a previously-rootless client to a known PID should
        // first run RemoveStaleAliasesLocked so the old exe alias doesn't
        // linger and accidentally match a later different-PID-same-exe
        // connection.
        static void IndexAliasesLocked(LogicalClient client)
        {
            foreach (var alias in AliasKeys(client))
                s_KeyAliases[alias] = client.ClientKey;
        }

        // Drops any alias that currently maps to this client but is NOT in
        // its current alias set. Used when a client gains a PID after being
        // registered exe-only: the exe alias must stop pointing at this
        // client so a later different-PID same-exe connection creates a new
        // logical client rather than merging.
        static void RemoveStaleAliasesLocked(LogicalClient client)
        {
            var current = new HashSet<string>(AliasKeys(client));
            var toRemove = new List<string>();
            foreach (var kv in s_KeyAliases)
            {
                if (kv.Value == client.ClientKey && !current.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }
            foreach (var alias in toRemove)
                s_KeyAliases.Remove(alias);
        }

        static void RemoveClientLocked(LogicalClient client)
        {
            s_Clients.Remove(client.ClientKey);
            foreach (var alias in AliasKeys(client))
            {
                if (s_KeyAliases.TryGetValue(alias, out var mapped) && mapped == client.ClientKey)
                    s_KeyAliases.Remove(alias);
            }
        }

        static void EnsureAcpListenerAttached()
        {
            if (s_AcpListenerAttached) return;
            s_AcpListenerAttached = true;
            AcpSessionRegistry.OnSessionAdded += OnAcpSessionAdded;
            AcpSessionRegistry.OnSessionRemoved += OnAcpSessionRemoved;
        }

        static void OnAcpSessionAdded(AssistantConversationId sessionId)
        {
            if (s_SessionToClient.ContainsKey(sessionId)) return;
            RegisterAcpSession(sessionId, providerHint: null);
        }

        static void OnAcpSessionRemoved(AssistantConversationId sessionId) => UnregisterAcpSession(sessionId);

        static void NotifyChanged() => EditorTask.delayCall += () => Changed?.Invoke();
    }
}
