using System;
using UnityEditor;
using UnityEngine.Analytics;

namespace Unity.AI.Assistant.Editor.Analytics
{
    internal enum McpSessionEventSubType
    {
        SessionStart,
        SessionEnd,
        ClientInfoReceived
    }

    internal enum McpToolCallEventSubType
    {
        ToolCallCompleted
    }

    internal static partial class AIAssistantAnalytics
    {
        #region MCP Session Events

        const string k_McpSessionEvent = "AIAssistantMcpSessionEvent";

        [Serializable]
        internal class McpSessionEventData : IAnalytic.IData
        {
            public McpSessionEventData(McpSessionEventSubType subType) => SubType = subType.ToString();

            public string SubType;
            public string ClientName;
            public string ClientVersion;
            public string SessionId;
            public string SessionDurationMs;
            public string TotalToolCalls;
            public string LastToolName;
            public string TimeToFirstSuccessMs;
            public string OldClientName;
            public string SimultaneousConnections;
            public string SimultaneousDirectConnections;
        }

        [AnalyticInfo(eventName: k_McpSessionEvent, vendorKey: k_VendorKey)]
        class McpSessionEvent : IAnalytic
        {
            readonly McpSessionEventData m_Data;

            public McpSessionEvent(McpSessionEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                data = m_Data;
                error = null;
                return true;
            }
        }

        static void ReportMcpSessionEvent(McpSessionEventData data)
        {
            EditorAnalytics.SendAnalytic(new McpSessionEvent(data));
        }

        /// <summary>
        /// Fired when an MCP client connects. The session ID may be a provisional
        /// "pending-{id}" value until validation completes.
        /// </summary>
        /// <param name="sessionId">Identity key for the connection (may be provisional).</param>
        /// <param name="simultaneousConnections">Total number of connected transports at session start.</param>
        /// <param name="simultaneousDirectConnections">Number of direct (non-gateway) transports at session start.</param>
        internal static void ReportMcpSessionStartEvent(string sessionId, int simultaneousConnections, int simultaneousDirectConnections)
        {
            ReportMcpSessionEvent(new McpSessionEventData(McpSessionEventSubType.SessionStart)
            {
                SessionId = sessionId,
                SimultaneousConnections = simultaneousConnections.ToString(),
                SimultaneousDirectConnections = simultaneousDirectConnections.ToString(),
            });
        }

        /// <summary>
        /// Fired when the MCP client sends its identity via set_client_info.
        /// </summary>
        /// <param name="sessionId">Identity key for the connection.</param>
        /// <param name="clientName">Display name of the MCP client (e.g. "Claude Code").</param>
        /// <param name="clientVersion">Version string reported by the client.</param>
        /// <param name="oldClientName">Previous client name if this is an update, empty otherwise.</param>
        internal static void ReportMcpClientInfoReceivedEvent(string sessionId, string clientName, string clientVersion, string oldClientName)
        {
            ReportMcpSessionEvent(new McpSessionEventData(McpSessionEventSubType.ClientInfoReceived)
            {
                SessionId = sessionId,
                ClientName = clientName,
                ClientVersion = clientVersion,
                OldClientName = oldClientName,
            });
        }

        /// <summary>
        /// Fired when an MCP client disconnects. Captures session-level aggregate metrics.
        /// </summary>
        /// <param name="sessionId">Validated identity key for the connection.</param>
        /// <param name="clientName">Display name of the MCP client.</param>
        /// <param name="sessionDurationMs">Total session duration in milliseconds.</param>
        /// <param name="totalToolCalls">Number of tool calls made during the session.</param>
        /// <param name="lastToolName">Name of the last tool invoked, or empty if none.</param>
        /// <param name="timeToFirstSuccessMs">Time to first successful tool call in milliseconds, or -1 if no successful call was made.</param>
        internal static void ReportMcpSessionEndEvent(
            string sessionId,
            string clientName,
            long sessionDurationMs,
            int totalToolCalls,
            string lastToolName,
            long timeToFirstSuccessMs)
        {
            ReportMcpSessionEvent(new McpSessionEventData(McpSessionEventSubType.SessionEnd)
            {
                SessionId = sessionId,
                ClientName = clientName,
                SessionDurationMs = sessionDurationMs.ToString(),
                TotalToolCalls = totalToolCalls.ToString(),
                LastToolName = lastToolName,
                TimeToFirstSuccessMs = timeToFirstSuccessMs >= 0 ? timeToFirstSuccessMs.ToString() : string.Empty,
            });
        }

        #endregion

        #region MCP Tool Call Events

        const string k_McpToolCallEvent = "AIAssistantMcpToolCallEvent";

        [Serializable]
        internal class McpToolCallEventData : IAnalytic.IData
        {
            public McpToolCallEventData(McpToolCallEventSubType subType) => SubType = subType.ToString();

            public string SubType;
            public string ClientName;
            public string ToolName;
            public string Status;
            public string ErrorType;
            public string ErrorMessage;
            public string LatencyMs;
            public string SessionId;
        }

        [AnalyticInfo(eventName: k_McpToolCallEvent, vendorKey: k_VendorKey)]
        class McpToolCallEvent : IAnalytic
        {
            readonly McpToolCallEventData m_Data;

            public McpToolCallEvent(McpToolCallEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                data = m_Data;
                error = null;
                return true;
            }
        }

        static void ReportMcpToolCallEvent(McpToolCallEventData data)
        {
            EditorAnalytics.SendAnalytic(new McpToolCallEvent(data));
        }

        /// <summary>
        /// Fired after each MCP tool invocation completes (success or failure).
        /// </summary>
        /// <param name="sessionId">Identity key for the connection.</param>
        /// <param name="clientName">Display name of the MCP client.</param>
        /// <param name="toolName">Name of the tool that was invoked.</param>
        /// <param name="success">Whether the tool call completed without error.</param>
        /// <param name="errorType">Exception type name on failure, null on success.</param>
        /// <param name="errorMessage">Reserved for future use (currently always null for PII safety).</param>
        /// <param name="latencyMs">Tool execution time in milliseconds.</param>
        internal static void ReportMcpToolCallCompletedEvent(
            string sessionId,
            string clientName,
            string toolName,
            bool success,
            string errorType,
            string errorMessage,
            long latencyMs)
        {
            ReportMcpToolCallEvent(new McpToolCallEventData(McpToolCallEventSubType.ToolCallCompleted)
            {
                SessionId = sessionId,
                ClientName = clientName,
                ToolName = toolName,
                Status = success ? "Success" : "Error",
                ErrorType = errorType ?? string.Empty,
                ErrorMessage = errorMessage ?? string.Empty,
                LatencyMs = latencyMs.ToString(),
            });
        }

        #endregion
    }
}
