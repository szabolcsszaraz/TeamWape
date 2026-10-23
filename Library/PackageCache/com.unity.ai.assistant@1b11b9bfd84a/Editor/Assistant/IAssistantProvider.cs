using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.RunCommand;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Agents;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Editor
{
    internal interface IAssistantProvider
    {
        // Dependencies
        IToolPermissions ToolPermissions { get; }

        /// <summary>
        /// The provider ID (e.g., "unity", "claude-code").
        /// Used for display/logging only, not for branching logic.
        /// </summary>
        string ProviderId { get; }

        // Callbacks
        event Action<IEnumerable<AssistantConversationInfo>> ConversationsRefreshed;
        event Action<AssistantConversationId, Assistant.PromptState> PromptStateChanged;
        event Action<AssistantConversation> ConversationLoaded;
        event Action<AssistantConversation> ConversationChanged;
        event Action<AssistantConversation> ConversationCreated;
        event Action<AssistantConversationId> ConversationDeleted;

        /// <summary>
        /// Invoked when an error occurs during an active conversation. If this is invoked and the conversation is
        /// active, this error indicates that conversation has stopped. All errors are critical errors and the
        /// conversation will cease to perform work.
        /// </summary>
        event Action<AssistantConversationId, ErrorInfo> ConversationErrorOccured;

        /// <summary>
        /// Raised when the server disconnected because the model is out of capacity (NO_CAPACITY).
        /// The UI layer reacts per CapacityFallbackPolicy, falling back to the default provider profile
        /// when a non-default profile is active. Only Unity providers raise this.
        /// </summary>
        event Action<AssistantConversationId> CapacityReached;

        event Action<AssistantMessageId, FeedbackData?> FeedbackLoaded;
        event Action<AssistantMessageId, bool> FeedbackSent;

        /// <summary>
        /// Invoked when message cost is received.
        /// </summary>
        event Action<AssistantMessageId, int?, bool> MessageCostReceived;

        /// <summary>
        /// Invoked when an incomplete message starts streaming
        /// </summary>
        event Action<AssistantConversationId, string> IncompleteMessageStarted;

        /// <summary>
        /// Invoked when an incomplete message is completed
        /// </summary>
        event Action<AssistantConversationId> IncompleteMessageCompleted;

        /// <summary>
        /// Invoked when a turn reaches a terminal state, carrying an <c>outcome</c> string:
        /// <c>"completed"</c> (a clean terminal fragment — the only genuine success),
        /// <c>"error"</c>, <c>"session_ended"</c> (a mid-turn/graceful close whose partial was
        /// finalized client-side), or <c>"cancelled"</c>; <c>failureReason</c> is a sub-code for the
        /// error case (e.g. <c>"turn_stalled"</c>) and null otherwise. Consumers that need to know
        /// whether a turn genuinely finished should key on this rather than re-reading message
        /// state, which recovery mutates (e.g. RemoveConnectionLossErrors) and which marks a
        /// client-finalized partial complete.
        /// <para>
        /// Contract by implementor: the gateway provider (<see cref="Assistant"/>) fires this
        /// exactly once per turn, mirroring the outcome it reports to gateway analytics, across
        /// all four values above. <c>AcpProvider</c> currently emits a best-effort subset —
        /// <c>"completed"</c> or <c>"error"</c> — with no exactly-once guarantee, and does not yet
        /// distinguish <c>"session_ended"</c> or <c>"cancelled"</c> (tracked separately).
        /// </para>
        /// </summary>
        event Action<AssistantConversationId, string, string> TurnEnded;

        bool SessionStatusTrackingEnabled { get; }

        /// <summary>
        /// Whether the Auto-Run setting is supported and should be shown in the UI.
        /// </summary>
        bool AutoRunSettingAvailable { get; }

        // Methods
        Task ConversationLoad(AssistantConversationId conversationId, CancellationToken ct = default);
        void ConversationRefresh(AssistantConversationId conversationId);
        Task RecoverIncompleteMessage(AssistantConversationId conversationId);
        Task ConversationFavoriteToggle(AssistantConversationId conversationId, bool isFavorite);
        Task ConversationDeleteAsync(AssistantConversationId conversationId, CancellationToken ct = default);
        Task ConversationRename(AssistantConversationId conversationId, string newName, CancellationToken ct = default);
        Task RefreshConversationsAsync(CancellationToken ct = default, bool enforceCooldown = false);

        Task ProcessPrompt(AssistantConversationId conversationId, AssistantPrompt prompt, IAgent agent = null, CancellationToken ct = default);
        Task SendFeedback(AssistantMessageId messageId, bool flagMessage, string feedbackText, bool upVote);
        Task<FeedbackData?> LoadFeedback(AssistantMessageId messageId, CancellationToken ct = default);
        Task<int?> FetchMessageCost(AssistantMessageId messageId, CancellationToken ct = default);
        Task RevertMessage(AssistantMessageId messageId);

        void SuspendConversationRefresh();
        void ResumeConversationRefresh();

        void AbortPrompt(AssistantConversationId conversationId);

        /// <summary>
        /// End the active provider session for the given conversation.
        /// Providers that don't use sessions should no-op.
        /// </summary>
        Task EndSessionAsync(AssistantConversationId conversationId);

        /// <summary>
        /// Disconnects the active workflow, unsubscribing from relay events.
        /// Use when switching away from Unity provider to prevent orphaned subscriptions.
        /// </summary>
        void DisconnectWorkflow();

        // Function Calling
        IFunctionCaller FunctionCaller { get; }

        Task RefreshProjectOverview(CancellationToken cancellationToken = default);

        // === Capability-based additions ===
        // These methods and events support optional provider capabilities.
        // Providers that don't support a feature return no-op results or never fire the event.

        /// <summary>
        /// Request a mode change. No-op for providers that don't support dynamic modes.
        /// </summary>
        Task SetModeAsync(string modeId);

        /// <summary>
        /// Request a model change. No-op for providers that don't support dynamic models.
        /// </summary>
        Task SetModelAsync(string modelId);


        /// <summary>
        /// Fired when available modes are received from the provider.
        /// Parameters: modes array (id, name, description), current mode ID.
        /// </summary>
        event Action<(string id, string name, string desc)[], string> ModesAvailable;

        /// <summary>
        /// Fired when available models are received from the provider.
        /// Parameters: models array (modelId, name, description), current model ID.
        /// </summary>
        event Action<(string modelId, string name, string description)[], string> ModelsAvailable;

        /// <summary>
        /// Fired when the current mode changes.
        /// </summary>
        event Action<string> ModeChanged;

        /// <summary>
        /// Fired when available commands are updated.
        /// Parameters: array of (name, description) tuples for each available command.
        /// </summary>
        event Action<(string name, string description)[]> AvailableCommandsChanged;

        /// <summary>
        /// Respond to a pending permission request for a tool call.
        /// AcpProvider: sends response to agent via AcpSession.
        /// Unity provider: no-op (permissions handled via IToolPermissions).
        /// </summary>
        /// <param name="toolCallId">The tool call ID that has a pending permission request.</param>
        /// <param name="answer">The user's answer to the permission request.</param>
        Task RespondToPermissionAsync(string toolCallId, PermissionUserAnswer answer);
    }
}
