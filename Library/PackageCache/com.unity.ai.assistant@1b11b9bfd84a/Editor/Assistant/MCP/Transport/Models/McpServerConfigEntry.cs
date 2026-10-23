using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Unity.AI.Assistant.Editor.Mcp.Transport.Models
{
    /// <summary>
    /// Server configuration entry as stored in the mcp.json config file.
    /// The server name is the dictionary key, not a property of this class.
    /// </summary>
#if !UNITY_6000_5_OR_NEWER
    [Serializable]
#endif
    class McpServerConfigEntry
    {
        /// <summary>
        /// Transport type: "stdio" or "http"
        /// </summary>
        [JsonProperty("type")]
        public string Type = "stdio";

        /// <summary>
        /// Executable command (stdio only)
        /// </summary>
        [JsonProperty("command", NullValueHandling = NullValueHandling.Ignore)]
        public string Command;

        /// <summary>
        /// Command arguments (stdio only, optional)
        /// </summary>
        [JsonProperty("args", NullValueHandling = NullValueHandling.Ignore)]
        public string[] Args = Array.Empty<string>();

        /// <summary>
        /// Environment variables (stdio only, optional)
        /// </summary>
        [JsonProperty("env", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> Env = new();

        /// <summary>
        /// Server URL (http only, required when Type == "http")
        /// </summary>
        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public string Url;

        /// <summary>
        /// HTTP request headers sent on every request (http only, optional).
        /// Used for static-headers auth (Authorization, X-API-Key, etc.).
        /// </summary>
        [JsonProperty("headers", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> Headers = new();

        // Transport-specific fields are optional; omit empty collections so a
        // stdio entry doesn't write empty "headers"/"url" and an http entry
        // doesn't write empty "args"/"env". NullValueHandling covers the null
        // case; these cover the non-null-but-empty case (Newtonsoft's
        // DefaultValueHandling.Ignore does not omit empty collections).
        public bool ShouldSerializeArgs() => Args is { Length: > 0 };

        public bool ShouldSerializeEnv() => Env is { Count: > 0 };

        public bool ShouldSerializeHeaders() => Headers is { Count: > 0 };
    }
}
