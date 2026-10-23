using System;
using Newtonsoft.Json;

namespace Unity.AI.Assistant.Editor.Mcp.Transport.Models
{
    /// <summary>
    /// Response from getting server status
    /// Matches the TypeScript MCPServerStatusResponse interface
    /// </summary>
#if !UNITY_6000_5_OR_NEWER
    [Serializable]
#endif
    class McpServerStatusResponse
    {
        [JsonProperty("serverName")]
        public string ServerName;

        [JsonProperty("isProcessRunning")]
        public bool IsProcessRunning;

        [JsonProperty("availableTools")]
        public McpTool[] AvailableTools;
    }
}
