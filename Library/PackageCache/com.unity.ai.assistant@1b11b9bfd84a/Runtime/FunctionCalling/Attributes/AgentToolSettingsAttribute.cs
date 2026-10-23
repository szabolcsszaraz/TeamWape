using System;
using Unity.AI.Assistant.Data;

namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    /// Optional companion to <see cref="AgentToolAttribute"/> that configures internal runtime behaviour.
    /// When absent, defaults apply (AssistantMode.Agent, EditMode|PlayMode, no MCP, default tag).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    class AgentToolSettingsAttribute : Attribute
    {
        /// <summary> A list of tags associated with the tool method. Tags can be used to categorize and filter tools. </summary>
        internal readonly string[] Tags;

        /// <summary> Specifies the editor mode requirements for this agent tool (flags). </summary>
        internal readonly ToolCallEnvironment ToolCallEnvironment;

        /// <summary> Specifies the assistant mode requirements for this agent tool (flags). </summary>
        internal readonly AssistantMode AssistantMode;

        /// <summary>
        /// Controls whether and how this tool is exposed via MCP.
        /// <see cref="McpAvailability.None"/>: not registered (default).
        /// <see cref="McpAvailability.Available"/>: registered but disabled by default.
        /// <see cref="McpAvailability.Default"/>: registered and enabled by default.
        /// </summary>
        internal McpAvailability Mcp { get; set; }

        internal AgentToolSettingsAttribute(
            AssistantMode assistantMode = AssistantMode.Agent,
            ToolCallEnvironment toolCallEnvironment = ToolCallEnvironment.PlayMode | ToolCallEnvironment.EditMode,
            McpAvailability mcp = McpAvailability.None,
            params string[] tags)
        {
            AssistantMode = assistantMode;
            ToolCallEnvironment = toolCallEnvironment;
            Mcp = mcp;
            Tags = tags.Length == 0 ? new[] { FunctionCallingUtilities.k_AgentToolTag } : tags;
        }
    }

    /// <summary>
    /// Controls whether and how an <see cref="AgentToolAttribute"/>-marked tool is exposed via MCP.
    /// </summary>
    enum McpAvailability
    {
        /// <summary>Not registered in MCP (default).</summary>
        None = 0,

        /// <summary>Registered in MCP but disabled by default in settings.</summary>
        Available = 1,

        /// <summary>Registered in MCP and enabled by default (curated set).</summary>
        Default = 2
    }

    /// <summary>
    /// Specifies the editor mode requirements for an agent tool.
    /// </summary>
    [Flags]
    enum ToolCallEnvironment
    {
        /// <summary>Tool is available in the Unity Runtime (aka no Editor present)</summary>
        Runtime = 1,

        /// <summary>Tool is available in Unity's Play Mode.</summary>
        PlayMode = 2,

        /// <summary>Tool is available in Unity's Edit Mode.</summary>
        EditMode = 4,
    }
}
