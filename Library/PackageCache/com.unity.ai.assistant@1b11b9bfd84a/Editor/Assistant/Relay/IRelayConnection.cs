using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Relay.Editor
{
    /// <summary>
    /// Abstraction over relay connection state and client access.
    /// Allows components like AcpClient to be tested without a live relay.
    /// </summary>
    interface IRelayConnection
    {
        bool IsConnected { get; }
        WebSocketRelayClient Client { get; }
        event Action Connected;
        event Action Disconnected;

        /// <summary>
        /// Acquire the live relay client, waiting until it is ready (or throwing on failure).
        /// Lets a consumer fetch a not-yet-connected connection's client through the injected
        /// seam instead of the RelayService singleton, so the disconnected path is testable.
        /// </summary>
        Task<WebSocketRelayClient> GetClientAsync(CancellationToken ct);
    }
}
