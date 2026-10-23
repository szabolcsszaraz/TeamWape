using System;
using Unity.AI.MCP.Editor.ToolRegistry;

namespace Unity.AI.MCP.Editor.Tools.Parameters
{
    /// <summary>
    /// Parameters for the CaptureGameView tool.
    /// </summary>
    public record CaptureGameViewParams
    {
        /// <summary>
        /// Optional name of the current scene (for confirmation/logging purposes only)
        /// </summary>
        [McpDescription("Name of the current scene (for confirmation/logging only)", Required = false)]
        public string SceneName { get; set; }
    }
}
