using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Acp;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.ConversationSearch;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    class AssistantUIContext
    {
        readonly IAssistantProvider m_UnityProvider;
        IAssistantProvider m_CurrentProvider;
        string m_LastProviderId;
        BaseEventSubscriptionTicket m_RetryableErrorTicket;

        public AssistantUIContext(IAssistantProvider assistant)
        {
            // NOTE: For now we just default to the previous singleton, later we will divert into separate `Assistant` instances for open windows
            Blackboard = new AssistantBlackboard();
            m_UnityProvider = assistant;
            m_CurrentProvider = assistant;
            m_LastProviderId = AssistantProviderFactory.IsUnityProvider(AssistantUISessionState.instance?.LastActiveProviderId)
                ? AssistantUISessionState.instance.LastActiveProviderId
                : AssistantProviderFactory.DefaultProvider.ProfileId;

            if (m_UnityProvider is Assistant.Editor.Assistant unityAssistant)
                unityAssistant.SetCurrentProviderId(m_LastProviderId);

            // ConversationLoader is the single source of truth for populating conversations
            ConversationLoader = new ConversationLoader(Blackboard, m_UnityProvider);

            // Only set current provider if assistant is not null
            if (assistant != null)
            {
                ConversationLoader.SetCurrentProvider(assistant);
            }

            API = new AssistantUIAPIInterpreter(assistant, Blackboard, InteractionQueue, () => AssistantProviderFactory.CreateModelConfigurationForProvider(CurrentProviderId));
            ConversationReloadManager = new ConversationReloadManager(this, Blackboard);
        }

        public readonly AssistantBlackboard Blackboard;
        public readonly AssistantUIAPIInterpreter API;
        public readonly ConversationReloadManager ConversationReloadManager;
        public readonly ConversationLoader ConversationLoader;

        /// <summary>
        /// The current provider ID (UI id for Unity: unity-max or unity-fast).
        /// </summary>
        public string CurrentProviderId => AssistantProviderFactory.IsUnityProvider(m_CurrentProvider?.ProviderId)
            ? m_LastProviderId
            : (m_CurrentProvider?.ProviderId ?? AssistantProviderFactory.DefaultProvider.ProfileId);

        /// <summary>
        /// Whether the current provider is the Unity provider.
        /// </summary>
        public bool IsUnityProvider => AssistantProviderFactory.IsUnityProvider(CurrentProviderId);

        /// <summary>
        /// Cached Unity model profiles from GET /v1/assistant/models (provider id, display name, optional tooltip). Updated when conversations are refreshed. May be null or empty until first refresh.
        /// </summary>
        public IReadOnlyList<ModelProfile> AvailableUnityModelProfiles
            => m_UnityProvider is Assistant.Editor.Assistant unity ? unity.AvailableUnityModelProfiles : null;

        public Action ConversationScrollToEndRequested;
        public Action<AssistantConversationId> ConversationRenamed;
        public Action<VirtualAttachment> VirtualAttachmentAdded;
        public Action ProviderSwitched;

        public Func<bool> WindowDockingState;

        public readonly UserInteractionQueue InteractionQueue = new();
        public readonly Dictionary<Guid, InteractionContentView> PendingInlineInteractions = new();

        public AssistantViewSearchHelper SearchHelper;

        /// <summary>
        /// The conversation currently rendered in the panel; authoritative source of message IDs for
        /// UI-layer operations (e.g. checkpoint validation). The blackboard cache may hold a shell model
        /// with no messages (added by ConversationLoader before full data arrives, or after a cache
        /// refresh), so this is used as the primary lookup. Only <see cref="AssistantConversationPanel"/>
        /// should write to this property.
        /// </summary>
        public ConversationModel DisplayedConversation { get; set; }

        public void Initialize()
        {
            API.Initialize();
            m_RetryableErrorTicket = AssistantEvents.Subscribe<EventRetryableErrorOccurred>(OnRetryableErrorOccurred);
            Blackboard.ClearActiveConversation();

            // Connect ConversationLoader to API's ConversationsRefreshed event
            ConversationLoader.ConversationsLoaded += API.TriggerConversationsRefreshed;
        }

        public void Deinitialize()
        {
            // Disconnect ConversationLoader event
            ConversationLoader.ConversationsLoaded -= API.TriggerConversationsRefreshed;

            AssistantEvents.Unsubscribe(ref m_RetryableErrorTicket);
            API.Deinitialize();
            ConversationLoader.Dispose();
            InteractionQueue.CancelAll();

            // Dispose current provider if it's disposable (and not the Unity provider)
            if (m_CurrentProvider != m_UnityProvider && m_CurrentProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Switch to a different provider by ID.
        /// </summary>
        public Task SwitchProviderAsync(string providerId)
            => SwitchProviderAsync(new ConversationContext(providerId));

        /// <summary>
        /// Switch to a different provider with optional resume context.
        /// If context includes a conversation ID, loads that conversation after switching.
        /// </summary>
        public async Task SwitchProviderAsync(ConversationContext context)
        {
            // Dispose current provider if it's disposable (and not the Unity provider)
            if (m_CurrentProvider != m_UnityProvider && m_CurrentProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // Create new provider via factory (no session created yet)
            m_CurrentProvider = await AssistantProviderFactory.CreateProviderAsync(
                context.ProviderId,
                m_UnityProvider);

            m_LastProviderId = context.ProviderId;

            if (m_CurrentProvider == m_UnityProvider && m_UnityProvider is Assistant.Editor.Assistant unityAssistant)
                unityAssistant.SetCurrentProviderId(context.ProviderId);

            // Switch the interpreter to use the new provider
            API.SwitchProvider(m_CurrentProvider);

            // Update the conversation loader with the new provider
            ConversationLoader.SetCurrentProvider(m_CurrentProvider);

            // Save the provider ID for domain reload restoration
            AssistantUISessionState.instance.LastActiveProviderId = context.ProviderId;

            // Update ProviderStateObserver BEFORE session initialization so that
            // ConversationLoad/EnsureSession can set Initializing state without it
            // being overwritten by SetProvider's Ready reset.
            ProviderStateObserver.SetProvider(context.ProviderId);

            // Now that events are wired, initialize the session:
            // - If resuming a conversation, load it (creates session with resume)
            // - Otherwise, create a fresh session (for modes/models)
            if (context.HasConversation)
            {
                await m_CurrentProvider.ConversationLoad(context.ConversationId);
            }
            else if (m_CurrentProvider is AcpProvider acpProvider)
            {
                acpProvider.EnsureSession();
            }

            // Notify listeners that the provider has been switched
            ProviderSwitched?.Invoke();
        }

        public void SendScrollToEndRequest()
        {
            ConversationScrollToEndRequested?.Invoke();
        }

        public void SendConversationRenamed(AssistantConversationId id)
        {
            ConversationRenamed?.Invoke(id);
        }

        /// <summary>
        /// Returns the text of the most recent user prompt in the conversation, or null when there is
        /// no user message or it carries no prompt block.
        /// </summary>
        public static string GetLastUserPromptText(ConversationModel conversation)
        {
            if (conversation == null)
                return null;

            var lastUserMessage = conversation.Messages.LastOrDefault(m => m.Role == MessageModelRole.User);
            return lastUserMessage.Role == MessageModelRole.User
                ? lastUserMessage.Blocks.OfType<PromptBlockModel>().FirstOrDefault()?.Content
                : null;
        }

        void OnRetryableErrorOccurred(EventRetryableErrorOccurred evt)
        {
            var echoedPrompt = GetLastUserPromptText(Blackboard.ActiveConversation);
            var lastPromptText = echoedPrompt ?? Blackboard.PendingPrompt;
            if (string.IsNullOrEmpty(lastPromptText))
                return;

            var content = new RetryInteractionContent();

            if (echoedPrompt == null)
            {
                // New chat: prompt was never echoed into a conversation, so restore it to the box and retry from there.
                AssistantEvents.Send(new EventRestoreUnsentPrompt(lastPromptText));
                content.SetRetryData(() => AssistantEvents.Send(new EventResubmitPrompt()));
            }
            else
            {
                content.SetRetryData(() => API.SendPrompt(lastPromptText, Blackboard.ActiveMode));
            }

            InteractionQueue.Enqueue(new UserInteractionEntry
            {
                Title = "Something went wrong",
                TitleIcon = "error",
                Detail = evt.ErrorMessage,
                ContentView = content,
            });
        }
    }
}
