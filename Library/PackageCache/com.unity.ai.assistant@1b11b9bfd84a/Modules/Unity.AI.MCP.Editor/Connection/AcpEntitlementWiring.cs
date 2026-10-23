using Unity.AI.Assistant.Editor.Acp;
using Unity.AI.Toolkit.Accounts.Services;
using UnityEditor;

namespace Unity.AI.MCP.Editor.Connection
{
    /// <summary>
    /// Installs the <see cref="ConnectionCensus"/> policy (which the Bridge reads
    /// directly) and points <see cref="GatewayCapacityGuard"/> at the census so the
    /// assistant-side <c>AcpSessionRegistry</c> can consult it without a
    /// build-time dependency on this assembly.
    /// <para>
    /// Entitlement-driven connection limits are intentionally not enforced (see
    /// <see cref="Apply"/>): every account gets unlimited connections and the
    /// <c>0</c> entitlement gate is bypassed, unless a dev-tool
    /// <see cref="ConnectionPolicyOverride"/> is active.
    /// </para>
    /// </summary>
    static class AcpEntitlementWiring
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            GatewayCapacityGuard.Check = Probe;
            Account.settings.OnChange += Apply;
            Apply();
        }

        /// <summary>
        /// Re-installs the census policy. Absent a dev-tool override this is
        /// always <see cref="ConnectionPolicy.Unlimited"/> — entitlement limits
        /// (including the <c>0</c> gate) are not enforced. Called when
        /// <see cref="Account.settings"/> changes, and exposed to dev tools so
        /// the "Reset to entitlement" button can undo a tier-simulator override.
        /// </summary>
        /// <remarks>
        /// When a <see cref="ConnectionPolicyOverride"/> is active (a dev-tool
        /// tier simulation persisted in SessionState) entitled values are
        /// suppressed and the persisted override is re-applied instead. This is
        /// what restores the override into <see cref="ConnectionCensus"/> after
        /// the domain reload on Edit→Play — the static <c>s_Policy</c> field
        /// resets to <see cref="ConnectionPolicy.Unlimited"/> on reload, so
        /// without this re-apply the override would be silently lost (UUM-141585).
        /// </remarks>
        internal static void Apply()
        {
            if (ConnectionPolicyOverride.IsActive)
            {
                ConnectionCensus.SetPolicy(ConnectionPolicyOverride.Value);
                return;
            }

            // Connection limits are intentionally not enforced: every account
            // gets unlimited direct MCP and gateway connections regardless of
            // entitlement, unless a dev-tool ConnectionPolicyOverride is active
            // (handled above). This deliberately bypasses the entitlement gate as
            // well as the concurrency ceiling — a resolved cap of 0 ("plan does
            // not include this") no longer denies, so unentitled accounts can
            // open connections instead of getting the upgrade/banner flow. The
            // entitlement values are still resolved and surfaced on
            // Account.settings for diagnostics; they just don't gate anything.
            ConnectionCensus.SetPolicy(ConnectionPolicy.Unlimited);
        }

        /// <summary>
        /// Translate a census pre-check into the assistant-side capacity struct.
        /// Kept allocation-free so it can be called on every acquire.
        /// </summary>
        static GatewayCapacityCheck Probe()
        {
            var r = ConnectionCensus.TryReserveGatewaySlot();
            return new GatewayCapacityCheck(
                canAcquire: r.Allowed,
                gatewayCount: r.PoolCount,
                gatewayCap: r.PoolCap);
        }
    }
}
