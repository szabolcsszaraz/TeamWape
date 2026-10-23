using System;
using UnityEditor;
using UnityEngine.Analytics;

namespace Unity.AI.Assistant.Editor.Analytics
{
    internal enum GatewayEventSubType
    {
        TurnCompleted,
        TurnEnded,
        CompileResult,
        GenerationResult,
        StreamRecovery
    }

    internal sealed class AssetGenerationBusyException : Exception
    {
        public AssetGenerationBusyException(string message) : base(message) { }
    }

    internal static partial class AIAssistantAnalytics
    {
        const string k_GatewayEvent = "AIAssistantGatewayEvent";
        const string k_GatewayTtftEvent = "AIAssistantGatewayTtftEvent";

        [Serializable]
        internal class GatewayEventData : IAnalytic.IData
        {
            public GatewayEventData(GatewayEventSubType subType) => SubType = subType.ToString();

            public string SubType;
            public string ConversationId;
            public string Provider;
            public int TurnCount;
            public long StartedAt;
            public long EndedAt;
            public string Messages;
        }

        [AnalyticInfo(eventName: k_GatewayEvent, vendorKey: k_VendorKey)]
        class GatewayEvent : IAnalytic
        {
            readonly GatewayEventData m_Data;

            public GatewayEvent(GatewayEventData data)
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

        /// <summary>
        /// Fired when an ACP agent turn completes. Captures conversation metadata
        /// and full message history for the gateway team's usage analytics.
        /// </summary>
        /// <param name="conversationId">The agent session ID.</param>
        /// <param name="provider">Provider ID (e.g. "claude", "gemini").</param>
        /// <param name="turnCount">Number of turns in this conversation so far.</param>
        /// <param name="startedAt">Unix ms timestamp when the conversation started.</param>
        /// <param name="endedAt">Unix ms timestamp when this turn completed.</param>
        /// <param name="messages">Full conversation history as a JSON string.</param>
        internal static void ReportGatewayTurnCompletedEvent(
            string conversationId,
            string provider,
            int turnCount,
            long startedAt,
            long endedAt,
            string messages)
        {
            var data = new GatewayEventData(GatewayEventSubType.TurnCompleted)
            {
                ConversationId = conversationId,
                Provider = provider,
                TurnCount = turnCount,
                StartedAt = startedAt,
                EndedAt = endedAt,
                Messages = messages,
            };
            EditorAnalytics.SendAnalytic(new GatewayEvent(data));
        }

        [Serializable]
        internal class TurnOutcomePayload
        {
            public string outcome;
            public string failure_reason;
        }

        internal static void ReportGatewayTurnEndedEvent(
            string conversationId,
            string outcome,
            string failureReason)
        {
            var payload = UnityEngine.JsonUtility.ToJson(new TurnOutcomePayload
            {
                outcome = outcome,
                failure_reason = failureReason,
            });
            var data = new GatewayEventData(GatewayEventSubType.TurnEnded)
            {
                ConversationId = conversationId,
                Messages = payload,
            };
            EditorAnalytics.SendAnalytic(new GatewayEvent(data));
        }

        [Serializable]
        internal class CompileResultPayload
        {
            public string result;
            public string file_type;
            public int error_count;
        }

        internal static void ReportGatewayCompileResultEvent(string result, string fileType, int errorCount)
        {
            var payload = UnityEngine.JsonUtility.ToJson(new CompileResultPayload
            {
                result = result,
                file_type = fileType,
                error_count = errorCount,
            });
            var data = new GatewayEventData(GatewayEventSubType.CompileResult)
            {
                Messages = payload,
            };
            EditorAnalytics.SendAnalytic(new GatewayEvent(data));
        }

        [Serializable]
        internal class GenerationResultPayload
        {
            public string asset_type;
            public string result;
            public string failure_reason;
        }

        internal static string ClassifyGenerationFailure(Exception ex)
        {
            switch (ex)
            {
                case TimeoutException _:
                    return "timeout";
                case OperationCanceledException _:
                    return "cancelled";
                case ArgumentException _:
                case System.IO.FileNotFoundException _:
                    return "invalid_args";
                case AssetGenerationBusyException _:
                    return "asset_busy";
                default:
                    return "generation_failed";
            }
        }

        internal static void ReportGatewayGenerationResultEvent(string assetType, string result, string failureReason)
        {
            var payload = UnityEngine.JsonUtility.ToJson(new GenerationResultPayload
            {
                asset_type = assetType,
                result = result,
                failure_reason = failureReason,
            });
            var data = new GatewayEventData(GatewayEventSubType.GenerationResult)
            {
                Messages = payload,
            };
            EditorAnalytics.SendAnalytic(new GatewayEvent(data));
        }

        [Serializable]
        internal class StreamRecoveryPayload
        {
            public string phase;
            public string result;
        }

        internal static void ReportGatewayStreamRecoveryEvent(string phase, string result)
        {
            var payload = UnityEngine.JsonUtility.ToJson(new StreamRecoveryPayload
            {
                phase = phase,
                result = result,
            });
            var data = new GatewayEventData(GatewayEventSubType.StreamRecovery)
            {
                Messages = payload,
            };
            EditorAnalytics.SendAnalytic(new GatewayEvent(data));
        }

        [Serializable]
        internal class GatewayTtftEventData : IAnalytic.IData
        {
            public string ConversationId;
            public string Provider;
            public int TurnCount;
            public long TtftMs;
        }

        [AnalyticInfo(eventName: k_GatewayTtftEvent, vendorKey: k_VendorKey)]
        class GatewayTtftEvent : IAnalytic
        {
            readonly GatewayTtftEventData m_Data;

            public GatewayTtftEvent(GatewayTtftEventData data)
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

        /// <summary>
        /// Reports client-side Time To First Chunk (TTFT) for the Gateway (ACP) pipeline.
        /// Fires exactly once per turn when the first chunk arrives. Bypasses
        /// <see cref="SendGatedEditorAnalytic"/> so the event is delivered for users who are not signed in.
        /// </summary>
        /// <param name="conversationId">The agent session ID.</param>
        /// <param name="provider">Provider ID (e.g. "claude", "gemini").</param>
        /// <param name="turnCount">Number of turns in this conversation so far.</param>
        /// <param name="ttftMs">Elapsed milliseconds from prompt dispatch to first chunk arrival.</param>
        internal static void ReportGatewayTtftEvent(
            string conversationId,
            string provider,
            int turnCount,
            long ttftMs)
        {
            var data = new GatewayTtftEventData
            {
                ConversationId = conversationId,
                Provider = provider,
                TurnCount = turnCount,
                TtftMs = ttftMs,
            };
            EditorAnalytics.SendAnalytic(new GatewayTtftEvent(data));
        }
    }
}
