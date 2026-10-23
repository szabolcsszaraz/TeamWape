using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.AI.Assistant.Socket.Communication
{
    static partial class ProxyTunnel
    {
        const string k_WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        internal static string BuildConnectRequest(string host, int port)
            => $"CONNECT {host}:{port} HTTP/1.1\r\nHost: {host}:{port}\r\n\r\n";

        internal static string BuildUpgradeRequest(Uri target, IReadOnlyDictionary<string, string> headers, string secWebSocketKey, string subProtocol)
        {
            var pathAndQuery = string.IsNullOrEmpty(target.PathAndQuery) ? "/" : target.PathAndQuery;
            var sb = new StringBuilder();
            sb.Append($"GET {pathAndQuery} HTTP/1.1\r\n");
            sb.Append($"Host: {target.Host}\r\n");
            sb.Append("Upgrade: websocket\r\n");
            sb.Append("Connection: Upgrade\r\n");
            sb.Append($"Sec-WebSocket-Key: {secWebSocketKey}\r\n");
            sb.Append("Sec-WebSocket-Version: 13\r\n");
            if (!string.IsNullOrEmpty(subProtocol))
                sb.Append($"Sec-WebSocket-Protocol: {subProtocol}\r\n");
            if (headers != null)
                foreach (var kv in headers)
                    sb.Append($"{kv.Key}: {kv.Value}\r\n");
            sb.Append("\r\n");
            return sb.ToString();
        }

        internal static (int code, string reason) ParseStatusLine(string line)
        {
            var parts = (line ?? string.Empty).Split(new[] { ' ' }, 3);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var code))
                throw new FormatException($"Malformed HTTP status line: '{line}'");
            return (code, parts.Length > 2 ? parts[2] : string.Empty);
        }

        internal static string ComputeAcceptKey(string secWebSocketKey)
        {
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(secWebSocketKey + k_WebSocketGuid));
            return Convert.ToBase64String(hash);
        }

        internal static async Task<WebSocket> ConnectAsync(
            Uri target, Uri proxy, IReadOnlyDictionary<string, string> headers,
            string subProtocol, TimeSpan keepAliveInterval, CancellationToken ct)
        {
            var secure = target.Scheme == "wss" || target.Scheme == "https";
            var targetPort = target.IsDefaultPort ? (secure ? 443 : 80) : target.Port;

            var tcp = new TcpClient();
            SslStream tls = null;
            // Blocking phases below (TCP connect, TLS handshake) have no ct-aware
            // overload in this runtime; closing the socket on cancellation aborts them.
            using var ctReg = ct.Register(() => { try { tcp.Close(); } catch { /* ignore */ } });
            try
            {
                ct.ThrowIfCancellationRequested();
                await tcp.ConnectAsync(proxy.Host, proxy.Port);
                var raw = tcp.GetStream();

                // HTTP CONNECT tunnel to the target host.
                var connectReq = Encoding.ASCII.GetBytes(BuildConnectRequest(target.Host, targetPort));
                await raw.WriteAsync(connectReq, 0, connectReq.Length, ct);
                await raw.FlushAsync(ct);
                var (connectLine, _) = await ReadHeaderBlockAsync(raw, ct);
                var (connectCode, connectReason) = ParseStatusLine(connectLine);
                if (connectCode != 200)
                    throw new WebSocketException($"Proxy CONNECT failed: {connectCode} {connectReason}");

                // For a secure target, TLS to the real host through the tunnel
                // (SNI = target host; default OS-store validation). A plain ws://
                // target speaks WebSocket directly over the raw tunnel.
                Stream stream = raw;
                if (secure)
                {
                    tls = new SslStream(raw, leaveInnerStreamOpen: false);
                    await tls.AuthenticateAsClientAsync(target.Host);
                    stream = tls;
                }

                // WebSocket HTTP upgrade.
                var key = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                var upgradeReq = Encoding.ASCII.GetBytes(BuildUpgradeRequest(target, headers, key, subProtocol));
                await stream.WriteAsync(upgradeReq, 0, upgradeReq.Length, ct);
                await stream.FlushAsync(ct);

                var (statusLine, respHeaders) = await ReadHeaderBlockAsync(stream, ct);
                var (code, reason) = ParseStatusLine(statusLine);
                if (code != 101)
                    throw new WebSocketException($"WebSocket upgrade failed: {code} {reason}");
                respHeaders.TryGetValue("Sec-WebSocket-Accept", out var accept);
                if (accept != ComputeAcceptKey(key))
                    throw new WebSocketException("WebSocket upgrade failed: invalid Sec-WebSocket-Accept");

                return WebSocket.CreateClientWebSocket(
                    stream, subProtocol, 4096, 4096, keepAliveInterval, useZeroMaskingKey: false, ArraySegment<byte>.Empty);
            }
            catch
            {
                tls?.Dispose();
                tcp.Dispose();
                throw;
            }
        }

        // Reads bytes until the CRLFCRLF header terminator. Returns the status line
        // and the parsed headers. Reads one byte at a time so nothing past the
        // terminator is consumed (the first WS frame stays in the stream for
        // CreateClientWebSocket to read).
        static async Task<(string statusLine, Dictionary<string, string> headers)> ReadHeaderBlockAsync(Stream s, CancellationToken ct)
        {
            var sb = new StringBuilder();
            var one = new byte[1];
            int state = 0;
            while (state < 4)
            {
                int n = await s.ReadAsync(one, 0, 1, ct);
                if (n == 0) break;
                char c = (char)one[0];
                sb.Append(c);
                switch (state)
                {
                    case 0: state = c == '\r' ? 1 : 0; break;
                    case 1: state = c == '\n' ? 2 : (c == '\r' ? 1 : 0); break;
                    case 2: state = c == '\r' ? 3 : 0; break;
                    case 3: state = c == '\n' ? 4 : (c == '\r' ? 1 : 0); break;
                }
            }
            var lines = sb.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                int idx = lines[i].IndexOf(':');
                if (idx > 0)
                    headers[lines[i].Substring(0, idx).Trim()] = lines[i].Substring(idx + 1).Trim();
            }
            return (lines.Length > 0 ? lines[0].Trim() : string.Empty, headers);
        }
    }
}
