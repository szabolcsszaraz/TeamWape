using Unity.AI.MCP.Editor.ToolRegistry;

namespace Unity.AI.MCP.Editor.Tools.Parameters
{
    /// <summary>
    /// Parameters for the Unity.RunCommand tool.
    /// </summary>
    public record RunCommandParams
    {
        /// <summary>
        /// Gets or sets the C# script code to compile and execute.
        /// </summary>
        [McpDescription("The C# script code to compile and execute. Should implement IRunCommand interface or be a valid C# script.", Required = true)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional title for the execution command.
        /// </summary>
        [McpDescription("Optional title for the execution command", Required = false)]
        public string Title { get; set; } = string.Empty;
    }
}
