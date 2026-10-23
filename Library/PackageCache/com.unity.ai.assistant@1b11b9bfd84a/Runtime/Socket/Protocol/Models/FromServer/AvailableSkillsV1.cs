using System.CodeDom.Compiler;
using System.Collections.Generic;
using Newtonsoft.Json;
using Unity.AI.Assistant.Skills;

namespace Unity.AI.Assistant.Socket.Protocol.Models.FromServer
{
    #pragma warning disable // Disable all warnings

    /// <summary>
    /// Server-&gt;client event emitted once per session with the list of skills
    /// currently registered on the backend. The Unity Editor uses this to
    /// populate developer-tools UI for per-skill enable/disable A/B testing.
    ///
    /// Clients that don't need it may ignore the message — no acknowledgement.
    /// </summary>
    [GeneratedCode("NJsonSchema", "11.1.0.0 (Newtonsoft.Json v13.0.0.0)")]
    class AvailableSkillsV1 : IModel
    {
        [JsonProperty("$type")]
        public const string Type = "AVAILABLE_SKILLS_V1";

        public string GetModelType() => Type;

        [JsonProperty("skills", Required = Required.Always)]
        public List<SkillMetaData> Skills { get; set; } = new();
    }
}
