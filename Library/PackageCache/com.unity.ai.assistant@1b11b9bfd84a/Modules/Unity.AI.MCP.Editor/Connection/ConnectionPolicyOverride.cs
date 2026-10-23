using UnityEditor;

namespace Unity.AI.MCP.Editor.Connection
{
    /// <summary>
    /// Dev-tool override for <see cref="ConnectionCensus.Policy"/>, persisted in
    /// <see cref="SessionState"/> so it survives the domain reload that fires on
    /// Edit→Play. Without this persistence the simulator value would be reset
    /// every time the editor entered play mode, while Play→Edit (no reload)
    /// preserved it — the asymmetry was the bug fixed by this class (UUM-141585).
    /// </summary>
    /// <remarks>
    /// SessionState is intentional: it survives domain reloads but is cleared on
    /// editor restart, so a dev who forgets to reset the simulator does not ship
    /// or test against a stale override the next day. Three primitive int keys
    /// are used instead of a JSON blob because <see cref="ConnectionPolicy"/> is
    /// a record (no JsonUtility round-trip) and pulling Newtonsoft in to
    /// serialize two ints would be overweight.
    /// </remarks>
    static class ConnectionPolicyOverride
    {
        internal const string k_HasOverrideKey = "ConnectionPolicyOverride.HasOverride";
        internal const string k_MaxDirectKey = "ConnectionPolicyOverride.MaxDirect";
        internal const string k_MaxGatewayKey = "ConnectionPolicyOverride.MaxGateway";

        internal static bool IsActive => SessionState.GetInt(k_HasOverrideKey, 0) != 0;

        internal static ConnectionPolicy Value => new(
            MaxDirect: SessionState.GetInt(k_MaxDirectKey, -1),
            MaxGateway: SessionState.GetInt(k_MaxGatewayKey, -1));

        internal static void Set(ConnectionPolicy policy)
        {
            SessionState.SetInt(k_MaxDirectKey, policy.MaxDirect);
            SessionState.SetInt(k_MaxGatewayKey, policy.MaxGateway);
            SessionState.SetInt(k_HasOverrideKey, 1);
            ConnectionCensus.SetPolicy(policy);
        }

        internal static void Clear()
        {
            SessionState.EraseInt(k_HasOverrideKey);
            SessionState.EraseInt(k_MaxDirectKey);
            SessionState.EraseInt(k_MaxGatewayKey);
            AcpEntitlementWiring.Apply();
        }
    }
}
