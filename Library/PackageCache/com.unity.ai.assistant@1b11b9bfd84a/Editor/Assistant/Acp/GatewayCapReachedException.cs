using System;

namespace Unity.AI.Assistant.Editor.Acp
{
    /// <summary>
    /// Thrown when an AI Gateway session cannot be acquired because the per-pool
    /// gateway cap has been reached, driven by the user's Unity entitlement tier.
    /// UI layers catch this to display a tier-aware "upgrade for more agents" prompt.
    /// </summary>
    class GatewayCapReachedException : Exception
    {
        /// <summary>Current number of distinct logical clients holding a gateway slot.</summary>
        public int CurrentCount { get; }

        /// <summary>Per-pool gateway cap (-1 means unlimited; not thrown in that case).</summary>
        public int Cap { get; }

        public GatewayCapReachedException(GatewayCapacityCheck check)
            : base(TierDenial.BuildMessage(TierDenialKind.Gateway, check.GatewayCount, check.GatewayCap))
        {
            CurrentCount = check.GatewayCount;
            Cap = check.GatewayCap;
        }
    }
}
