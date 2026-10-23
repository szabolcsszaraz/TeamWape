using System;
using System.Net.WebSockets;

namespace Unity.AI.Assistant.Socket.Workflows.Chat
{
    struct CloseReason
    {
        /// <summary>Default <see cref="Info"/> for a NO_CAPACITY disconnect that carries no server message.</summary>
        public const string NoCapacityFallbackInfo = "No Capacity";

        public ReasonType Reason;
        public string Info;
        public Exception Exception;

        /// <summary>
        /// The raw WebSocket close status, if the underlying transport closed (typically
        /// captured from <see cref="System.Net.WebSockets.WebSocket.CloseStatus"/>).
        /// May be null for application-level closures or when the transport never connected.
        /// </summary>
        public WebSocketCloseStatus? CloseStatus;

        /// <summary>
        /// The raw WebSocket close-frame reason string (the up-to-123-byte description sent by
        /// the relay alongside the close code). May be null/empty if the relay did not provide one.
        /// </summary>
        public string CloseDescription;

        public override string ToString() => Exception == null
            ? $"CloseReason [Reason: {Reason}, Info:{Info}, CloseStatus:{CloseStatus}, CloseDescription:{CloseDescription}]"
            : $"CloseReason [Reason: {Reason}, Info:{Info}, CloseStatus:{CloseStatus}, CloseDescription:{CloseDescription}, Exception:{Exception}]";

        /// <summary>
        /// Translates a raw transport-level WebSocket close (status + description) into a
        /// <see cref="CloseReason"/> with an appropriate <see cref="ReasonType"/>.
        ///
        /// In particular, <see cref="WebSocketCloseStatus.PolicyViolation"/> (close code 1008) is
        /// surfaced as <see cref="ReasonType.AuthenticationFailed"/> with the description string
        /// preserved as <see cref="Info"/>; everything else falls back to the existing
        /// <see cref="ReasonType.UnderlyingWebSocketWasClosed"/> path.
        /// </summary>
        public static CloseReason FromTransportClose(WebSocketCloseStatus? closeStatus, string closeDescription)
        {
            // Authentication failures surface as PolicyViolation (1008) per the server contract.
            // Promote those to AuthenticationFailed so the UI can show actionable copy ("you're
            // not signed in") rather than the generic "relay connection lost" banner.
            if (closeStatus == WebSocketCloseStatus.PolicyViolation)
            {
                return new CloseReason
                {
                    Reason = ReasonType.AuthenticationFailed,
                    Info = string.IsNullOrEmpty(closeDescription) ? "Authentication failed. Please sign in again." : closeDescription,
                    CloseStatus = closeStatus,
                    CloseDescription = closeDescription
                };
            }

            return new CloseReason
            {
                Reason = ReasonType.UnderlyingWebSocketWasClosed,
                Info = $"Status reported by underlying websocket: {closeStatus}{(string.IsNullOrEmpty(closeDescription) ? string.Empty : $" ({closeDescription})")}",
                CloseStatus = closeStatus,
                CloseDescription = closeDescription
            };
        }

        public enum ReasonType
        {
            /// <summary>
            /// The websocket was unable to connect to the server
            /// </summary>
            CouldNotConnect,

            /// <summary>
            /// The server sent a message that was not deserializable and could not be processed by the workflow.
            /// </summary>
            ServerSentUnknownMessage,

            /// <summary>
            /// The server sent a message at a point in the <see cref="ChatWorkflow.WorkflowState"/> that was not
            /// valid at that point in time.
            /// </summary>
            ServerSentMessageAtWrongTime,

            /// <summary>
            /// The underlying web socket closed for some reason. This is the case where the workflow does not
            /// decide to close, rather the websocket has been closed for some other reason. This likely means some
            /// critical failure has occured, like loss of internet access.
            /// </summary>
            UnderlyingWebSocketWasClosed,

            /// <summary>
            /// The server provides a timeout that should be the maximum amount of time between a
            /// <see cref="ChatRequestV1"/> and the first token of a <see cref="ChatResponseV1"/>. If this is
            /// exceeded, the socket will be closed because of timeout.
            /// </summary>
            ChatResponseTimeout,

            /// <summary>
            /// The server should send <see cref="DiscussionInitializationV1"/> immeditately after connection. If this
            /// is not received, then disconnect.
            /// </summary>
            DiscussionInitializationTimeout,

            /// <summary>
            /// The server sent a disconnection packet and has disconnected the websocket
            /// </summary>
            ServerDisconnected,

            /// <summary>
            /// The server sent a disconnection packet and has disconnected the websocket (happy path)
            /// </summary>
            ServerDisconnectedGracefully,

            /// <summary>
            /// The client canceled the operation
            /// </summary>
            ClientCanceled,

            /// <summary>
            /// The server sent an informational disconnect (e.g., maintenance restart). This is not an error and
            /// the user should see the accompanying message but it should not be styled as a critical failure.
            /// </summary>
            ServerDisconnectedInformational,

            /// <summary>
            /// The server closed the WebSocket because authentication failed (e.g. close code 1008
            /// "Authentication failure"). This is distinct from a generic transport drop because the
            /// user must take action (sign in / refresh credentials) before retrying.
            /// </summary>
            AuthenticationFailed,

            /// <summary>
            /// The server disconnected because the requested model is out of capacity / throttled
            /// (NO_CAPACITY). Distinct from a generic <see cref="ServerDisconnected"/> so the client
            /// can auto-fall back to the default provider profile rather than surfacing a generic error.
            /// </summary>
            ServerNoCapacity
        }
    }
}
