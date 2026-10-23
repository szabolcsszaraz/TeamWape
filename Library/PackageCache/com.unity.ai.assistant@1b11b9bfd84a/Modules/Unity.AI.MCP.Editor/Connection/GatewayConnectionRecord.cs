using System;
using Unity.AI.MCP.Editor.Models;
using Unity.AI.MCP.Editor.Security;

namespace Unity.AI.MCP.Editor
{
    /// <summary>
    /// Record for an AI Gateway MCP connection (ephemeral, non-persisted).
    /// Similar to ConnectionRecord but includes session tracking for cleanup.
    /// </summary>
    record GatewayConnection
    {
        /// <summary>Connection information</summary>
        public ConnectionInfo Info;

        /// <summary>Validation status</summary>
        public ValidationStatus Status;

        /// <summary>Reason for the validation decision</summary>
        public string ValidationReason;

        /// <summary>Connection identity for matching</summary>
        public ConnectionIdentity Identity;

        /// <summary>AI Gateway session ID for cleanup tracking</summary>
        public string SessionId;

        /// <summary>Provider name (e.g., "claude-code", "gemini", "cursor")</summary>
        public string Provider;

        /// <summary>Timestamp when the connection was recorded</summary>
        public DateTime ConnectedAt;

        /// <summary>
        /// Logical client key in the <see cref="ConnectionCensus"/>.
        /// Set when ProcessInfoCollector.CollectConnectionInfo runs on the gateway fast path
        /// so AcpSessions can be merged with the agent's actual MCP transports for cap dedup.
        /// </summary>
        public string LogicalClientKey;
    }
}
