using System.Collections.Generic;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Backend
{
    /// <summary>
    /// Registry for capability declarations that bridges Runtime and Editor assemblies
    /// </summary>
    static class CapabilityRegistry
    {
        static List<FunctionsObject> s_RegisteredFunctions = new();

        public static List<FunctionsObject> GetFunctionCapabilities()
        {
            return new List<FunctionsObject>(s_RegisteredFunctions);
        }

        /// <summary>
        /// Registers a function capability (called from Editor assembly during initialization and dynamically added MCP tools
        /// </summary>
        public static void RegisterFunction(FunctionsObject functionCapability)
        {
            if (functionCapability == null)
                return;

            s_RegisteredFunctions.Add(functionCapability);
        }

        /// <summary>
        /// Unregisters a function capability for dynamically removed MCP tools
        /// </summary>
        public static void UnregisterFunction(string functionId)
        {
            if (string.IsNullOrEmpty(functionId))
                return;

            s_RegisteredFunctions.RemoveAll(f => {
                if (f.FunctionId == functionId)
                {
                    InternalLog.Log($"Unregistered function capability: {f.FunctionName} (ID: {f.FunctionId})");
                    return true;
                }
                return false;
            });
        }

        internal static void Clear()
        {
            s_RegisteredFunctions.Clear();
        }

        // Permission token constants — must match the backend schema.
        internal const string PermissionFirstPartyTool = "first_party_tool";
        internal const string PermissionThirdPartyTool = "third_party_tool";
        internal const string PermissionReadExternalFiles = "read_external_files";
        internal const string PermissionReadProject = "read_project";
        internal const string PermissionModifyProject = "modify_project";
        internal const string PermissionPlayMode = "play_mode";
        internal const string PermissionScreenCapture = "screen_capture";
        internal const string PermissionCodeExecution = "code_execution";
        internal const string PermissionAssetGeneration = "asset_generation";

        // volatile: SetPermissionPolicies is called on the main thread;
        // GetPermissionPolicies is read on background network threads.
        static volatile Dictionary<string, string> s_PermissionPolicies = new();

        public static void SetPermissionPolicies(Dictionary<string, string> policies)
        {
            s_PermissionPolicies = policies != null
                ? new Dictionary<string, string>(policies)
                : new Dictionary<string, string>();
        }

        public static Dictionary<string, string> GetPermissionPolicies()
        {
            var policies = s_PermissionPolicies;
            return policies.Count > 0 ? new Dictionary<string, string>(policies) : null;
        }
    }
}
