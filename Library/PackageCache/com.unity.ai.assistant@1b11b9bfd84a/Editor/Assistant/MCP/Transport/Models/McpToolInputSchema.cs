using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unity.AI.Assistant.Editor.Mcp.Transport.Models
{
    /// <summary>
    /// MCP Tool input schema
    /// </summary>
#if !UNITY_6000_5_OR_NEWER
    [Serializable]
#endif
    class McpToolInputSchema
    {
        [JsonProperty("type")]
        public string Type;

        [JsonProperty("properties")]
        public JObject Properties;

        [JsonProperty("required")]
        public string[] Required;
    }
}
