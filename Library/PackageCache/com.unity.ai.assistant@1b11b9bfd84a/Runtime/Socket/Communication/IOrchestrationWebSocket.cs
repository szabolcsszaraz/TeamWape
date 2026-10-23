using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Socket.Protocol.Models;

namespace Unity.AI.Assistant.Socket.Communication
{
    interface IOrchestrationWebSocket : IDisposable
    {
        class Options
        {
            public Dictionary<string, string> Headers = new();
            public Dictionary<string, string> QueryParameters = new();

            public void ApplyHeaders(IClientWebSocket websocket)
            {
                foreach (var keyValuePair in Headers)
                    websocket.SetHeader(keyValuePair.Key, keyValuePair.Value);
            }

            public string ConstructUri(string urlWithoutQueryParameters)
            {
                if (QueryParameters == null || QueryParameters.Count == 0)
                    return urlWithoutQueryParameters;

                string parameters = string.Join('&', QueryParameters.Select(kv => $"{kv.Key}={kv.Value}"));
                return $"{urlWithoutQueryParameters}?{parameters}";
            }
        }

        event Action<ReceiveResult> OnMessageReceived;

        /// <summary>
        /// Fired when the underlying WebSocket transport closes. Carries the close status (when
        /// available) and the description string the peer sent in its close frame so subscribers
        /// can distinguish e.g. an authentication failure (close code 1008) from a generic drop.
        /// Both arguments may be null/empty if the close happened before close-frame negotiation.
        /// </summary>
        event Action<WebSocketCloseStatus?, string> OnClose;

        /// <summary>
        /// True when the underlying transport is currently connected and able to accept sends.
        /// Callers (e.g. workflow Send paths) should check this before attempting to send so a
        /// dropped transport surfaces as a clean failure rather than a hung "thinking" state.
        /// </summary>
        bool IsConnected { get; }

        Task<ConnectResult> Connect(Options args, CancellationToken ct);
        Task<SendResult> Send(IModel model, CancellationToken ct);
    }
}
