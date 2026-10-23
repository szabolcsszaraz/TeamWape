using System;
using System.Linq;
using Newtonsoft.Json.Linq;
#if UNITY_7000_0_OR_NEWER
using UnityEditor.Licensing;
#else
using UnityEditor.Experimental.Licensing;
#endif
using UnityEngine;

namespace Unity.AI.Toolkit.Accounts.Services
{
    class EditorConnectionLimitProvider : IConnectionLimitProvider
    {
        const string k_EditorAiEntitlement = "com.unity.editor.ai";
        static readonly string[] k_EditorAiEntitlements = { k_EditorAiEntitlement };

        public static EditorConnectionLimitProvider Instance { get; } = new();

        public (int? GatewayConnections, int? McpConnections)? LicensingConnectionLimits
        {
            get
            {
                try
                {
                    var entitlements = LicensingUtility.HasEntitlementsExtended(k_EditorAiEntitlements, true);
                    return ExtractConnectionLimits(entitlements);
                }
                catch
                {
                    // Missing or unavailable licensing data should fall through to the Pro fallback.
                    return null;
                }
            }
        }

        public bool HasProLicense => Application.HasProLicense();

        static (int? GatewayConnections, int? McpConnections)? ExtractConnectionLimits(EntitlementInfo[] entitlements)
        {
            var entitlement = entitlements?.FirstOrDefault(e => e.EntitlementId == k_EditorAiEntitlement);
            if (entitlement == null)
                return null;

            TryExtractCustomDataCounts(entitlement.CustomData, out var gatewayCount, out var mcpCount, out var sharedCount);
            return (
                GatewayConnections: gatewayCount ?? sharedCount ?? 1,
                McpConnections: mcpCount ?? sharedCount ?? 1);
        }

        static void TryExtractCustomDataCounts(string customData, out int? gatewayCount, out int? mcpCount, out int? sharedCount)
        {
            gatewayCount = null;
            mcpCount = null;
            sharedCount = null;
            if (string.IsNullOrEmpty(customData))
                return;

            try
            {
                var json = JObject.Parse(customData);
                gatewayCount = ReadValidCap(json, "AIGatewayConnectionCount");
                mcpCount = ReadValidCap(json, "MCPConnectionCount");
                sharedCount = ReadValidCap(json, "Count");
            }
            catch
            {
                // Malformed licensing custom data should fall through to the default entitlement cap.
            }
        }

        static int? ReadValidCap(JObject json, string key)
        {
            var value = json.Value<int?>(key);
            return value == -1 || value >= 0 ? value : null;
        }
    }
}
