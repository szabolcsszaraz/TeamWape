using System;

namespace Unity.AI.Assistant.Editor.Acp
{
    /// <summary>
    /// Outcome of a gateway capacity probe. <see cref="CanAcquire"/> is <c>true</c>
    /// when the caller may proceed; otherwise the count/cap fields describe the
    /// saturated pool (for building an accurate tier-aware message).
    /// </summary>
    readonly struct GatewayCapacityCheck
    {
        public readonly bool CanAcquire;
        public readonly int GatewayCount;
        public readonly int GatewayCap;

        public GatewayCapacityCheck(bool canAcquire, int gatewayCount, int gatewayCap)
        {
            CanAcquire = canAcquire;
            GatewayCount = gatewayCount;
            GatewayCap = gatewayCap;
        }

        /// <summary>A permissive "unlimited" result used as the default when no wiring is in place.</summary>
        public static GatewayCapacityCheck Unlimited => new(
            canAcquire: true,
            gatewayCount: 0, gatewayCap: -1);
    }

    /// <summary>
    /// Single indirection so <see cref="AcpSessionRegistry"/> can consult the live
    /// capacity state (owned by <c>ConnectionCensus</c> in Unity.AI.MCP.Editor)
    /// without taking a build-time dependency on that assembly.
    /// </summary>
    /// <remarks>
    /// Wired at editor load by <c>AcpEntitlementWiring</c> in the MCP assembly.
    /// When unwired (headless tests, package not loaded) the default treats
    /// capacity as unlimited.
    /// </remarks>
    static class GatewayCapacityGuard
    {
        /// <summary>
        /// Probe whether a new gateway session can be opened, and retrieve the
        /// live counts needed to describe a refusal.
        /// </summary>
        public static Func<GatewayCapacityCheck> Check = () => GatewayCapacityCheck.Unlimited;
    }
}
