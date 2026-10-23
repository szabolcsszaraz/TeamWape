using System;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Backend;

namespace Unity.AI.Assistant.Data
{
    [Serializable]
    struct AssistantFunctionCall : IEquatable<AssistantFunctionCall>
    {
        public string FunctionId;
#if UNITY_6000_5_OR_NEWER
        [NonSerialized]
#endif
        public Guid CallId;
#if UNITY_6000_5_OR_NEWER
        [NonSerialized]
#endif
        public JObject Parameters;
#if UNITY_6000_5_OR_NEWER
        [NonSerialized]
#endif
        public FunctionCallResult Result;
        // No [NonSerialized] needed — string and bool round-trip safely through Unity's serializer,
        // unlike Guid/JObject/FunctionCallResult above which require the UNITY_6000_5_OR_NEWER guard.
        /// <summary>
        /// Name of the agent that made this tool call (e.g., "Subagent-explorer-1").
        /// Null for calls made directly by CoreAgent.
        /// </summary>
        public string Agent;
        /// <summary>
        /// True when the conversation has active sub-agents. Set by the backend
        /// on every TOOL_CALL/TOOL_RESULT payload when the agent definition
        /// includes sub-agents. Defaults to false for solo-agent conversations
        /// and older backends that don't send this field.
        /// </summary>
        public bool SubAgentsActive;

        public override int GetHashCode() => HashCode.Combine(FunctionId, CallId, Parameters, Result, SubAgentsActive, Agent);
        public override bool Equals(object obj) => obj is AssistantFunctionCall other && Equals(other);
        public bool Equals(AssistantFunctionCall other)
        {
            return FunctionId == other.FunctionId && CallId.Equals(other.CallId) && Equals(Parameters, other.Parameters) && Result.Equals(other.Result) && SubAgentsActive == other.SubAgentsActive && Agent == other.Agent;
        }
        
        internal void GetCodeEditParameters(out string filePath, out string newCode, out string oldCode)
        {
            filePath = Parameters?["filePath"]?.ToString();
            newCode = Parameters?["newString"]?.ToString();
            oldCode = Parameters?["oldString"]?.ToString();
        }
    }
}

