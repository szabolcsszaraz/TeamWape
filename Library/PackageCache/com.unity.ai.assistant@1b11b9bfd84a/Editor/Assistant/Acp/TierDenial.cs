namespace Unity.AI.Assistant.Editor.Acp
{
    /// <summary>
    /// Identifies which pool surfaced a cap-reached denial so <see cref="TierDenial"/>
    /// can pick the right noun ("AI Gateway session" vs "MCP connection") without
    /// any copy drift between the session banner and the Project Settings row.
    /// </summary>
    enum TierDenialKind
    {
        /// <summary>Gateway pool: AI Gateway / ACP session cap.</summary>
        Gateway,
        /// <summary>Direct pool: third-party MCP connection cap.</summary>
        DirectMcp,
    }

    /// <summary>
    /// Single source of truth for cap-reached user-facing copy. Both the AI
    /// Gateway session banner (via <see cref="GatewayCapReachedException"/>) and
    /// the Project Settings connection row (via <c>Bridge.BuildCapacityDenialReason</c>)
    /// render the same strings, so the wording across the editor stays consistent.
    /// </summary>
    /// <remarks>
    /// We intentionally omit the numeric "(current/cap)" tuple when
    /// <paramref name="cap"/> is <c>0</c>. Under the post-2026-04 business
    /// model <c>cap == 0</c> is a legitimate entitlement (the user's plan
    /// simply doesn't include this pool), so showing "(0/0)" reads as a
    /// puzzling "full of nothing" rather than "your plan doesn't cover this."
    /// When <paramref name="cap"/> is finite and &gt; 0 we keep the tuple so
    /// power users can see how close they are to the limit.
    /// </remarks>
    static class TierDenial
    {
        /// <summary>
        /// Build the primary sentence and CTA for a cap-reached refusal.
        /// The CTA is returned separately so callers can render it with a
        /// link / button without having to string-parse our output.
        /// </summary>
        /// <param name="kind">Pool that refused the request.</param>
        /// <param name="current">Live per-pool count at denial time.</param>
        /// <param name="cap">Installed per-pool cap (0 means "not entitled").</param>
        public static (string Primary, string Cta) Build(TierDenialKind kind, int current, int cap)
        {
            string noun = kind == TierDenialKind.Gateway ? "AI Gateway sessions" : "MCP connections";

            string primary;
            if (cap <= 0)
            {
                // Plan-gated case: the user's entitlement doesn't include
                // this pool at all. Don't surface counts — the "(0/0)" view
                // is factually correct but reads as a bug to most users.
                primary = $"Your Unity plan doesn't include {noun}.";
            }
            else
            {
                // Saturated case: the user is entitled but has hit the
                // concurrent limit. Show the tuple so they can tell whether
                // a single stale agent is holding the slot.
                primary = $"Your {noun} limit is reached ({current}/{cap}).";
            }

            return (primary, "Upgrade your Unity plan to add more.");
        }

        /// <summary>
        /// Convenience wrapper that returns the joined message for surfaces
        /// that render a single plain-text string (e.g. the connection-row
        /// validation-reason label, which has no link-handling affordance).
        /// </summary>
        public static string BuildMessage(TierDenialKind kind, int current, int cap)
        {
            var (primary, cta) = Build(kind, current, cap);
            return $"{primary} {cta}";
        }
    }
}
