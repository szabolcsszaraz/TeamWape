using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.AI.Assistant.Socket.Communication
{
    class WrappedClientWebSocket : IClientWebSocket
    {
        // Staging ClientWebSocket backs Options + the no-proxy path (unchanged behavior).
        ClientWebSocket m_Staging = new();
        // Buffered headers (also applied to m_Staging) for the tunnel path.
        readonly Dictionary<string, string> m_Headers = new();
        WebSocket m_WebSocket; // resolved at ConnectAsync (staging direct, or tunnel)

        // Before ConnectAsync, m_WebSocket is null; fall back to the staging socket so
        // status members don't throw pre-connect. After a tunnel connect, m_Staging is
        // null and m_WebSocket is the tunnel.
        WebSocket Active => m_WebSocket ?? m_Staging;

        public WrappedClientWebSocket() { }

        public WebSocketCloseStatus? CloseStatus => Active.CloseStatus;
        public string CloseStatusDescription => Active.CloseStatusDescription;

        /// <summary>
        /// Request options for the socket. Valid only before <see cref="ConnectAsync"/>
        /// (used to stage request headers); the backing options are released after a
        /// proxy-tunnel connect.
        /// </summary>
        public ClientWebSocketOptions Options => m_Staging.Options;
        public WebSocketState State => Active.State;
        public string SubProtocol => Active.SubProtocol;

        public void Abort()
        {
            Active.Abort();
        }

        public void SetHeader(string key, string value)
        {
            m_Headers[key] = value;
            m_Staging.Options.SetRequestHeader(key, value);
        }

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            return Active.CloseAsync(closeStatus, statusDescription, cancellationToken);
        }

        public Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            return Active.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
        }

        public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (ProxyResolver.TryResolveProxy(uri, out var proxyUri))
            {
                m_WebSocket = await ProxyTunnel.ConnectAsync(
                    uri, proxyUri, m_Headers, null,
                    TimeSpan.FromSeconds(30), cancellationToken);
                m_Staging.Dispose();
                m_Staging = null;
            }
            else
            {
                await m_Staging.ConnectAsync(uri, cancellationToken);
                m_WebSocket = m_Staging;
            }
        }

        public void Dispose()
        {
            m_WebSocket?.Dispose();
            m_Staging?.Dispose();
        }

        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            return Active.ReceiveAsync(buffer, cancellationToken);
        }

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            return Active.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }

        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            return Active.ReceiveAsync(buffer, cancellationToken);
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            return Active.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }
    }
}
