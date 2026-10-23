using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Unity.AI.Assistant.Editor.Mcp.Transport.Models
{
    /// <summary>
    /// Individual server entry in project config
    /// </summary>
#if !UNITY_6000_5_OR_NEWER
    [Serializable]
#endif
    class McpServerEntry
    {
        [JsonProperty("name")] public string Name;

        [JsonProperty("command")] public string Command;

        [JsonProperty("args")] public string[] Args = Array.Empty<string>();

        [JsonProperty("transport")] public string Transport = "stdio";

        [JsonProperty("environment")]
        public Dictionary<string, string> Environment = new();

        [JsonProperty("url")] public string Url;

        [JsonProperty("headers")] public Dictionary<string, string> Headers = new();
    }
}