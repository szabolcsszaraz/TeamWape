using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Acp;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Checkpoint.Events;
using Unity.AI.Assistant.Editor.SessionBanner;
using Unity.AI.Assistant.Editor.Settings;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.History;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.PromptSuggestions;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.WhatsNew;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils;
using Unity.AI.Assistant.Utils.Perf;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Accounts.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using Debug = UnityEngine.Debug;
using Trace = Unity.AI.Tracing.Trace;
using TraceEventOptions = Unity.AI.Tracing.TraceEventOptions;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    partial class AssistantView : ManagedTemplate
    {
        static readonly char[] k_MessageTrimChars = { ' ', '\n', '\r', '\t' };

        const string k_HistoryOpenClass = "mui-chat-history-open";
        const string k_ExpandedPanelActiveClass = "mui-expanded-panel-active";
        const string k_NewConversationTitle = "New conversation";

        readonly IAssistantHostWindow k_HostWindow;

        static CancellationTokenSource s_NewChatActiveTokenSource;

        VisualElement m_RootMain;
        VisualElement m_RootPanel;

        Button m_NewChatButton;
        Button m_HistoryButton;
        Button m_ExpandedPanelBackButton;

        Label m_ConversationName;

        AssistantConversationPanel m_AssistantConversationPanel;

        VisualElement m_HistoryPanelRoot;
        VisualElement m_ProgressElementRoot;
        ProgressElement m_ProgressElement;

        HistoryPanel m_HistoryPanel;
        Rect m_HistoryPanelWorldBounds;

        VisualElement m_HeaderRow;
        Label m_ExpandedPanelTitle;
        VisualElement m_ActiveHeaderActions;
        VisualElement m_FooterRoot;

        VisualElement m_ChatInputRoot;
        AssistantTextField m_ChatInput;

        VisualElement m_PopupRoot;
        SelectionPopup m_SelectionPopup;
        PopupTracker m_SelectionPopupTracker;
        bool m_PopupOpenedByMention;
        PopupTracker m_ContextDropdownTracker;
        VisualElement m_ContextDropdownButton;
        Label m_DropdownToggleLabel;
        readonly string k_ContextDropdownToggleFocused = "mui-context-dropdown-toggle-focused";

        Button m_ClearContextButton;

        ContextDropdown m_SelectedContextDropdown;

        NewChatTopicBar m_NewChatTopicBar;
        WhatsNewBanner m_WhatsNewBanner;
        VisualElement m_WhatsNewBannerRoot;
        CheckpointDiscoveryBanner m_CheckpointDiscoveryBanner;
        VisualElement m_CheckpointDiscoveryBannerRoot;
        // EARLY_ACCESS_PROMO_START
        EarlyAccessPromoBanner m_EarlyAccessPromoBanner;
        VisualElement m_EarlyAccessPromoBannerRoot;
        // EARLY_ACCESS_PROMO_END
        VisualElement m_PlanPromptBannerRoot;
        VisualElement m_EmptyStateRoot;
        VisualElement m_EmptyStateLoading;
        VisualElement m_EmptyStateContent;
        bool m_IsEmptyState;
        LoadingSpinner m_EmptyStateSpinner;
        int m_SelectedConsoleMessageNum;
        string m_SelectedConsoleMessageContent;
        string m_SelectedGameObjectName;

        bool m_WaitingForConversationChange;
        bool m_EmptyStateVisible;
        bool m_SuggestionsVisible;

        SessionBanner m_SessionBanner;
        VisualElement m_SessionNotificationsArea;
        VisualElement m_HeaderRoot;
        VisualElement m_SessionBannerOverlay;

        BaseEventSubscriptionTicket m_ConversationSelectedEventTicket;
        BaseEventSubscriptionTicket m_CheckpointEnableStateChangedEventTicket;
        BaseEventSubscriptionTicket m_ExpandedPanelOpenedEventTicket;
        BaseEventSubscriptionTicket m_ExpandedPanelClosedEventTicket;
        BaseEventSubscriptionTicket m_RestoreUnsentPromptEventTicket;
        BaseEventSubscriptionTicket m_ResubmitPromptEventTicket;
        BaseEventSubscriptionTicket m_CapacityReachedEventTicket;

        // Provider switching state
        bool m_IsSwitchingProvider;
        bool m_IsRecoveringFromCredentialError;

        PlanPromptBanner m_PlanPromptBanner;
        string m_PlanPromptContent;
        Stopwatch m_ConversationReloadStopwatch;

        VisualElement m_NewSkillsNotificationRoot;
        NewSkillsNotificationPanel m_NewSkillsNotificationPanel;

        /// <summary>
        /// Constructor for the MuseChatView.
        /// </summary>
        public AssistantView()
            : this(null)
        {
        }

        public AssistantView(IAssistantHostWindow hostWindow)
            : base(AssistantUIConstants.UIModulePath)
        {
            k_HostWindow = hostWindow;

            RegisterAttachEvents(OnAttachToPanel, OnDetachFromPanel);
        }

        public void InitializeThemeAndStyle()
        {
            LoadStyle(m_RootPanel, EditorGUIUtility.isProSkin ? AssistantUIConstants.AssistantSharedStyleDark : AssistantUIConstants.AssistantSharedStyleLight);
            LoadStyle(m_RootPanel, AssistantUIConstants.AssistantBaseStyle, true);
        }

        /// <summary>
        /// Provide access to the currently active context to Debug and Internal tools
        /// Note: Do not use this for public facing operations
        /// </summary>
        internal AssistantUIContext ActiveUIContext => Context;

        public void BindUiContainer(AssistantWindowUiContainer container) => m_ChatInput?.BindUiContainer(container);

        /// <summary>
        /// Initialize the view and its component, called by the managed template
        /// </summary>
        /// <param name="view">the template container of the current element</param>
        protected override void InitializeView(TemplateContainer view)
        {
            // Suspend any saving of state during initialization until the state was restored (RestoreState)
            Context.Blackboard.SuspendStateSave();

            style.flexGrow = 1;
            view.style.flexGrow = 1;

            m_HeaderRow = view.Q<VisualElement>("headerRow");
            m_ExpandedPanelTitle = view.Q<Label>("expandedPanelTitle");

            m_RootMain = view.Q<VisualElement>("root-main");
            m_RootMain.RegisterCallback<DragEnterEvent>(OnMainDragEnter);
            m_RootMain.RegisterCallback<DragLeaveEvent>(OnMainDragLeave);
            m_RootMain.RegisterCallback<DragExitedEvent>(OnMainDragExit);

            m_RootPanel = view.Q<VisualElement>("root-panel");

            m_NewChatButton = view.SetupButton("newChatButton", OnNewChatClicked);
            m_NewChatButton.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider, enableOnProviderError: true);
            m_HistoryButton = view.SetupButton("historyButton", OnHistoryClicked);
            m_HistoryButton.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider, enableOnProviderError: true);
            m_ExpandedPanelBackButton = view.SetupButton("expandedPanelBackButton", _ => m_AssistantConversationPanel.CloseExpandedPanel());

            m_ConversationName = view.Q<Label>("conversationNameLabel");
            m_ConversationName.enableRichText = false;

            var panelRoot = view.Q<VisualElement>("chatPanelRoot");
            m_AssistantConversationPanel = new AssistantConversationPanel();
            m_AssistantConversationPanel.Initialize(Context);
            m_AssistantConversationPanel.RegisterCallback<MouseUpEvent>(OnConversationPanelClicked);
            panelRoot.Add(m_AssistantConversationPanel);

            m_HistoryPanelRoot = view.Q<VisualElement>("historyPanelRoot");
            m_HistoryPanel = new HistoryPanel();
            m_HistoryPanel.Initialize(Context);
            m_HistoryPanelRoot.Add(m_HistoryPanel);
            RegisterCallback<ClickEvent>(CheckHistoryPanelClick);
            m_HistoryPanelRoot.style.display = AssistantUISessionState.instance.IsHistoryOpen ? DisplayStyle.Flex : DisplayStyle.None;

            m_ProgressElementRoot = view.Q<VisualElement>("progressElementContainer");
            m_ProgressElement = new ProgressElement();
            m_ProgressElement.Initialize(Context);
            m_ProgressElement.Hide();
            m_ProgressElementRoot.Add(m_ProgressElement);

            view.AddSessionRefreshManipulators(Context.API.Provider);

            m_FooterRoot = view.Q<VisualElement>("footerRoot");

            // Note: Status tracking is applied granularly to footer children in AssistantTextField
            // to keep ProviderSelector always enabled. contextRoot is tracked here.
            view.Q<VisualElement>("contextRoot")?.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);

            m_ClearContextButton = view.Q<Button>("clearContextButton");

            m_ChatInputRoot = view.Q<VisualElement>("chatTextFieldRoot");

            m_PopupRoot = view.Q<VisualElement>("chatModalPopupRoot");
            InitializeSelectionPopup();

            m_ContextDropdownButton = view.Q<VisualElement>("dropdownToggle");
            InitializeContextDropdown();
            m_DropdownToggleLabel = view.Q<Label>("dropdownToggleLabel");

            m_ChatInput = new AssistantTextField();
            m_ChatInput.Initialize(Context);
            // Set host (and create dropdown menus) before setting provider so provider label/items are populated.
            m_ChatInput.SetHost(m_PopupRoot);
            // Restore the saved selection if any; otherwise profiles load picks the first one.
            var lastProviderId = AssistantUISessionState.instance.LastActiveProviderId;
            if (!string.IsNullOrEmpty(lastProviderId))
                m_ChatInput.SetProvider(lastProviderId, triggerEvent: false);
            m_ChatInput.SubmitRequest += OnRequestSubmit;
            m_ChatInput.CancelRequest += OnActiveProgressCancelRequested;
            m_ChatInput.OnProviderChanged += OnProviderChanged;
            m_ChatInput.OnCommandSelected += OnCommandSelected;
            m_ChatInput.ContextButton.RegisterCallback<PointerUpEvent>(_ => ToggleSelectionPopup());
            m_ChatInput.MentionTriggered += OnMentionTriggered;
            m_ContextDropdownButton.RegisterCallback<PointerUpEvent>(_ => ToggleContextDropdown());
            m_ChatInputRoot.Add(m_ChatInput);

            m_EmptyStateRoot = view.Q<VisualElement>("emptyStateRoot");
            m_EmptyStateLoading = view.Q<VisualElement>("emptyStateLoading");
            m_EmptyStateContent = view.Q<VisualElement>("emptyStateContent");
            m_EmptyStateSpinner = new LoadingSpinner();
            m_EmptyStateSpinner.Hide();
            m_EmptyStateLoading.Add(m_EmptyStateSpinner);

            m_CheckpointDiscoveryBannerRoot = view.Q<VisualElement>("checkpointDiscoveryBannerRoot");
            m_CheckpointDiscoveryBanner = new CheckpointDiscoveryBanner();
            m_CheckpointDiscoveryBanner.Initialize(Context, autoShowControl: false);
            // Checkpoint discovery takes priority over the new-skills notification; re-arbitrate whenever its visibility flips.
            m_CheckpointDiscoveryBanner.ShownStateChanged += UpdateNewSkillsNotificationVisibility;
            m_CheckpointDiscoveryBannerRoot.Add(m_CheckpointDiscoveryBanner);
            m_CheckpointDiscoveryBanner.Refresh();

            m_WhatsNewBannerRoot = view.Q<VisualElement>("whatsNewBannerRoot");
            m_WhatsNewBanner = new WhatsNewBanner();
            m_WhatsNewBanner.Initialize(Context, autoShowControl: false);
            m_WhatsNewBannerRoot.Add(m_WhatsNewBanner);

            // EARLY_ACCESS_PROMO_START
            m_EarlyAccessPromoBannerRoot = view.Q<VisualElement>("earlyAccessPromoBannerRoot");
            m_EarlyAccessPromoBanner = new EarlyAccessPromoBanner();
            m_EarlyAccessPromoBanner.Initialize(Context, autoShowControl: false);
            m_EarlyAccessPromoBanner.ShownStateChanged += ApplyEarlyAccessPromoCoexistence;
            m_EarlyAccessPromoBannerRoot.Add(m_EarlyAccessPromoBanner);
            // EARLY_ACCESS_PROMO_END

            var contentPolicyLink = view.Q<Label>("contentPolicyLink");
            contentPolicyLink.text = AssistantUIConstants.ReportContentRichText;
            contentPolicyLink.RegisterCallback<PointerDownLinkTagEvent>(evt =>
            {
                switch (evt.linkID)
                {
                    case AssistantUIConstants.ReportContentLinkIdReport:
                        Application.OpenURL(AssistantUIConstants.ReportUnacceptableContentUrl);
                        break;
                    case AssistantUIConstants.ReportContentLinkIdLearn:
                        Application.OpenURL(AssistantUIConstants.ContentTransparencyPolicyUrl);
                        break;
                }
            });
            contentPolicyLink.RegisterCallback<PointerOverLinkTagEvent>(_ => contentPolicyLink.AddToClassList(AssistantUIConstants.RichTextLinkHoverClass));
            contentPolicyLink.RegisterCallback<PointerOutLinkTagEvent>(_ => contentPolicyLink.RemoveFromClassList(AssistantUIConstants.RichTextLinkHoverClass));

            var newChatTopicBarRoot = view.Q<VisualElement>("newChatTopicBarRoot");
            m_NewChatTopicBar = new NewChatTopicBar();
            m_NewChatTopicBar.Initialize(Context);
            m_NewChatTopicBar.PromptSelected += OnPromptSuggestionSelected;
            newChatTopicBarRoot.Add(m_NewChatTopicBar);

            m_PlanPromptBannerRoot = view.Q<VisualElement>("planPromptBannerRoot");
            m_PlanPromptBanner = new PlanPromptBanner(OnPlanPromptYesClicked, OnPlanPromptNoClicked);
            m_PlanPromptBanner.Initialize(Context, autoShowControl: true);
            m_PlanPromptBannerRoot.Add(m_PlanPromptBanner);
            Context.API.APIStateChanged += OnPlanPromptApiStateChanged;

            m_NewSkillsNotificationRoot = view.Q<VisualElement>("newSkillsNotificationRoot");
            m_NewSkillsNotificationPanel = new NewSkillsNotificationPanel();
            m_NewSkillsNotificationPanel.Initialize(Context, autoShowControl: false);
            m_NewSkillsNotificationRoot.Add(m_NewSkillsNotificationPanel);
            UpdateNewSkillsNotificationVisibility();

            m_SessionBanner = view.Q<SessionBanner>("session-notifications");
            if (m_SessionBanner != null)
            {
                m_SessionBanner.GetActiveConversationId = () => Context.Blackboard.ActiveConversationId;
                // Dismissing the capacity banner clears the held blue highlight on the model picker.
                m_SessionBanner.CapacityBannerDismissed = () => m_ChatInput?.ClearProviderSelectorHighlight();
                m_SessionBanner.BannerChanged += UpdateBlockingBannerPlacement;
                // EARLY_ACCESS_PROMO_START
                m_SessionBanner.BannerChanged += ApplyEarlyAccessPromoCoexistence;
                // EARLY_ACCESS_PROMO_END
            }

            m_HeaderRoot = view.Q<VisualElement>("headerRoot");
            m_SessionNotificationsArea = view.Q<VisualElement>("session-notifications-area");
            SetUpSessionBannerOverlay();

            // EARLY_ACCESS_PROMO_START
            ApplyEarlyAccessPromoCoexistence();
            // EARLY_ACCESS_PROMO_END

            UpdateAssistantEditorDriverContext();
            UpdateWarnings();

            EditorApplication.hierarchyChanged += OnHierarchChanged;

            ClearChat();

            m_DropZoneRoot = view.Q<VisualElement>("dropZoneRoot");
            m_DropZone = new ChatDropZone();
            m_DropZone.Initialize(Context);
            m_DropZoneRoot.Add(m_DropZone);
            m_DropZoneOverlay = view.Q<VisualElement>("dropZoneOverlay");
            m_DropZone.SetupDragDrop(m_DropZoneOverlay, OnDropped);

            m_DropZone.SetDropZoneActive(false);

            view.AddManipulator(Context.SearchHelper);
            view.AddManipulator(new PointsBalanceChanges(OnPointsBalanceChanged));

            view.RegisterCallback<GeometryChangedEvent>(OnViewGeometryChanged);

            Context.Initialize();

            UpdateContextSelectionElements();

            Context.API.ConversationReload += OnConversationReload;
            Context.API.ConversationLoadFailed += OnConversationLoadFailed;
            Context.API.ConversationChanged += OnConversationChanged;
            Context.API.ConversationDeleted += OnConversationDeleted;

            ScheduleConversationRefresh();
            RefreshPlanPromptBannerVisibility();

            Context.ConversationRenamed += OnConversationRenamed;
            Context.ProviderSwitched += OnProviderSwitched;
            GatewayPreferenceService.Instance.Preferences.OnChange += OnGatewayPreferencesChanged;
            // Subscribe to capability events (forwarded from any provider that supports them)
            Context.API.ModelsAvailable += OnModelsAvailable;
            Context.API.AvailableCommandsChanged += OnAvailableCommandsChanged;
            m_ChatInput.OnModelSelected += OnModelSelected;
            Context.API.ReplayCachedAvailableCommands();

            // Bind mode provider to current assistant provider
            m_ChatInput.BindModeProvider(Context.API.Provider);

            RegisterContextCallbacks();

            RefreshPlanPromptYesEnabled();
        }

        private void OnPointsBalanceChanged()
        {
            if(CanEnableChat()){
                m_ChatInput.Enable();
            }else{
                m_ChatInput.Disable();
            }

            RefreshPlanPromptYesEnabled();
        }

        public void InitializeState()
        {
            RestoreConversationState();
        }

        void ScheduleConversationRefresh()
        {
            Context.API.RefreshConversations();

            // Schedule another history update in 5 minutes.
            schedule.Execute(ScheduleConversationRefresh).StartingIn(1000 * 60 * 5);
        }

        void CheckHistoryPanelClick(ClickEvent e)
        {
            var clickOfHistoryButton = m_HistoryButton.worldBound.Contains(e.position);
            var clickWithinHistoryPanel = m_HistoryPanel.worldBound.Contains(e.position);

            if (!clickWithinHistoryPanel && AssistantUISessionState.instance.IsHistoryOpen && !clickOfHistoryButton)
            {
                SetHistoryDisplay(false);
            }
        }

        void OnConversationPanelClicked(MouseUpEvent evt)
        {
            SetHistoryDisplay(false);
        }

        public void Deinit()
        {
            if (m_PlanPromptBanner != null)
                Context.API.APIStateChanged -= OnPlanPromptApiStateChanged;

            if (m_SessionBanner != null)
                m_SessionBanner.BannerChanged -= UpdateBlockingBannerPlacement;

            // EARLY_ACCESS_PROMO_START
            if (m_SessionBanner != null)
                m_SessionBanner.BannerChanged -= ApplyEarlyAccessPromoCoexistence;
            if (m_EarlyAccessPromoBanner != null)
                m_EarlyAccessPromoBanner.ShownStateChanged -= ApplyEarlyAccessPromoCoexistence;
            // EARLY_ACCESS_PROMO_END

            Context.Deinitialize();

            UnregisterContextCallbacks();

            // Unsubscribe from capability events
            Context.API.ModelsAvailable -= OnModelsAvailable;
            Context.API.AvailableCommandsChanged -= OnAvailableCommandsChanged;
            Context.ProviderSwitched -= OnProviderSwitched;
            m_ChatInput.OnModelSelected -= OnModelSelected;
        }

        async void RestoreConversationState()
        {
            // New restore, reset conversation reload tracking.
            m_AssistantConversationPanel.Populated -= OnConversationPanelPopulated;
            m_ConversationReloadStopwatch = Stopwatch.StartNew();

            var lastMode = AssistantUISessionState.instance.LastActiveMode;
            Context.Blackboard.ActiveMode = lastMode;

            var lastConvId = AssistantUISessionState.instance.LastActiveConversationId;
            if (string.IsNullOrEmpty(lastConvId))
            {
                // No conversation to restore - but ensure the provider is switched
                // (e.g., after domain reload with a non-Unity provider on a new conversation)
                await RestoreProviderIfNeeded();
                await RestoreSavedModeAsync(lastMode);
                RestoreUIState(default);
                return;
            }

            m_WaitingForConversationChange = true;
            UpdateEmptyStateDisplay();

            var id = new AssistantConversationId(lastConvId);

            // Use ConversationReloadManager to load the conversation
            // This will automatically determine the provider, switch to it if needed,
            // and set the active conversation in the blackboard
            try
            {
                await Context.ConversationReloadManager.LoadConversationAsync(id);

                // After successful reload, update the UI to reflect the current provider
                // This is needed because ConversationReloadManager may have switched providers
                UpdateUIForCurrentProvider();
                await RestoreSavedModeAsync(lastMode);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to restore conversation {lastConvId}: {ex.Message}");
                // Conversation load failed - but ensure the provider is switched
                // so the user can start a new conversation with the same provider
                m_WaitingForConversationChange = false;
                UpdateEmptyStateDisplay();
                await RestoreProviderIfNeeded();
                await RestoreSavedModeAsync(lastMode);
                RestoreUIState(default);
                return;
            }

        }

        /// <summary>
        /// Ensures the saved provider is active after domain reload.
        /// When there's no conversation to restore (new conversation or failed load),
        /// we still need to switch to the saved provider so its session is created.
        /// Without this, the UI shows the provider name but no AcpProvider/session exists,
        /// leaving the relay with no session and models unavailable.
        /// </summary>
        async Task RestoreProviderIfNeeded()
        {
            var lastProviderId = AssistantUISessionState.instance.LastActiveProviderId;
            if (string.IsNullOrEmpty(lastProviderId))
            {
                // No saved provider; profiles load auto-selects the first one.
                m_ChatInput.BindModeProvider(Context.API.Provider);
                return;
            }

            try
            {
                await Context.SwitchProviderAsync(lastProviderId);
                m_ChatInput.BindModeProvider(Context.API.Provider);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AssistantView] Failed to restore provider '{lastProviderId}': {ex.Message}");
            }
        }

        /// <summary>
        /// Restores the saved mode (Ask/Agent) after domain reload.
        /// BindModeProvider resets the mode to the provider's default (Agent) because
        /// the provider is recreated with a hardcoded default after domain reload.
        /// This method re-applies the saved mode through SetModeAsync, which syncs
        /// the provider, ModeProvider, blackboard, and UI dropdown.
        /// </summary>
        async Task RestoreSavedModeAsync(AssistantMode mode)
        {
            if (mode == AssistantMode.Undefined)
                return;

            try
            {
                await Context.API.Provider.SetModeAsync(mode.ToString());
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[AssistantView] Failed to restore mode '{mode}': {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the UI components to reflect the current provider after a provider switch.
        /// </summary>
        void UpdateUIForCurrentProvider()
        {
            var currentProviderId = Context.CurrentProviderId;

            // Update the provider dropdown UI (without triggering event since provider is already switched)
            m_ChatInput.SetProvider(currentProviderId, triggerEvent: false);

            // Bind mode provider to update available modes
            m_ChatInput.BindModeProvider(Context.API.Provider);

            // Notify provider state observer
            ProviderStateObserver.SetProvider(currentProviderId);
        }

        void OnProviderSwitched()
        {
            // Update UI to reflect the newly switched provider
            UpdateUIForCurrentProvider();
            // Re-evaluate points-based input state for the new provider
            OnPointsBalanceChanged();
            Context.API.ReplayCachedAvailableCommands();
        }

        void RestoreUIState(AssistantConversationId conversationId)
        {
            Trace.Event("ui.restore.begin", new TraceEventOptions
            {
                Level = "debug",
                Data = new { conversation = conversationId.Value, isWorking = Context.Blackboard.IsAPIWorking, isReady = Context.Blackboard.IsAPIReadyForPrompt }
            });

            if (m_WaitingForConversationChange)
            {
                if (Application.isBatchMode)
                {
                    EditorTask.delayCall += () => RestoreUIState(conversationId);
                }
                // In non-batch mode, OnConversationReload will call RestoreUIState after Populate() completes
                return;
            }

            // Check for incomplete message to recover
            string incompleteId = AssistantUISessionState.instance.IncompleteMessageId;
            Trace.Event("ui.restore.incomplete", new TraceEventOptions
            {
                Level = "debug",
                Data = new { incompleteId = incompleteId ?? "(none)" }
            });
            // No explicit "stale marker" clear runs here anymore. A marker whose prompt reached the
            // conversation-creating handoff now carries a synchronous handoff flag (Assistant.Prompt.cs),
            // so TryGetPendingPromptRecovery already reports it as not-lost and the queued clear tidies
            // it away — it can never re-drive on a later reload. Conversely, a marker *without* that flag
            // is a genuinely lost pre-conversation prompt: clearing it here (the previous behaviour) would
            // eat exactly the submission this fix exists to recover, so it is left armed for the re-drive
            // branch below (or a later restore once the incomplete id clears) instead.

            if (conversationId != default && !string.IsNullOrEmpty(incompleteId))
            {
                // Set working state so UI shows stop button instead of send
                Context.API.SetWorkingState(true);
                // Recover incomplete message now that conversation is loaded
                Context.API.RecoverIncompleteMessage(conversationId);
            }
            else if (string.IsNullOrEmpty(incompleteId)
                     && global::Unity.AI.Assistant.Editor.Assistant.TryGetPendingPromptRecovery(out var pendingPromptText, out var pendingPromptMode, out var pendingPromptContext))
            {
                // Pre-conversation prompt loss: a domain reload hit between prompt
                // submission and IncompleteMessageStarted (no conversation/session
                // existed yet), so the IncompleteMessageId path has nothing to
                // recover and the prompt would silently never start. Re-drive it.
                // TryGetPendingPromptRecovery only reports markers armed by a *dead* domain, so
                // a live in-flight prompt (e.g. this restore is a plain window re-open) is never
                // re-driven here. Claim rather than clear: the send below is only *scheduled*,
                // so erasing now would leave a frame where the prompt lives solely in a delayCall
                // and a reload would lose it. Claiming makes it one-shot for this domain
                // (a second restore sees a matching stamp) while keeping it armed until
                // ProcessPrompt re-arms it for real.
                global::Unity.AI.Assistant.Editor.Assistant.ClaimPendingPromptRecovery();
                InternalLog.Log("Re-driving prompt lost to a pre-conversation domain reload");
                Context.API.SetWorkingState(true);

                // Re-attach the context captured at submit time. The send path clears the selected
                // context and persists the now-empty session context the moment it hands the prompt
                // off, so the snapshot stored with the marker is the only surviving copy — without
                // this the re-drive would rebuild the turn with no object, virtual, console or
                // screenshot attachments. Writing it into session state (rather than into
                // k_SelectedContext directly) lets the normal restore below rehydrate both the
                // selection and the blackboard attachments that BuildPrompt actually reads.
                // Skipped when context has been attached since the send, so a re-drive never
                // clobbers a fresh selection — same rule as RestorePendingContext.
                var currentContextJson = AssistantUISessionState.instance.Context;
                var currentContext = string.IsNullOrEmpty(currentContextJson)
                    ? null
                    : JsonUtility.FromJson<AssistantContextList>(currentContextJson);
                if (!string.IsNullOrEmpty(pendingPromptContext) && !(currentContext?.m_ContextList.Count > 0))
                {
                    AssistantUISessionState.instance.Context = pendingPromptContext;
                }

                // The persisted mode is authoritative; fall back to the restored active mode
                // rather than submitting Undefined if it could not be read back.
                var redriveMode = pendingPromptMode == AssistantMode.Undefined
                    ? Context.Blackboard.ActiveMode
                    : pendingPromptMode;
                EditorTask.delayCall += () => Context.API.SendPrompt(pendingPromptText, redriveMode);
            }
            else if (string.IsNullOrEmpty(incompleteId)
                     && global::Unity.AI.Assistant.Editor.Assistant.TryConsumePendingAutoContinue(
                         conversationId, out var autoContinueText, out var autoContinueMode, out _))
            {
                // A transport death mid-turn scheduled a delayed re-drive, and a domain reload
                // landed inside that delay — taking the delayed task with it. The close path
                // already released the incomplete marker, so no other recovery covers this and
                // the turn would stay stranded. Consume the record (it also charges the attempt,
                // and is a no-op unless it belongs to this conversation) and re-drive here.
                // The model tier is discarded (out _): this arm re-sends through SendPrompt, which
                // fills ModelConfiguration from the live UI selection restored alongside the reload.
                InternalLog.Log("Re-driving auto-continue lost to a domain reload during its backoff");
                Context.API.SetWorkingState(true);
                EditorTask.delayCall += () => Context.API.SendPrompt(autoContinueText, autoContinueMode);
            }
            else if (!string.IsNullOrEmpty(incompleteId)
                && string.IsNullOrEmpty(AssistantUISessionState.instance.LastActiveConversationId))
            {
                // Stale marker with no saved conversation to recover into (a transiently failed
                // load keeps its conversation id, so its marker survives for a later retry).
                // Write session state directly: the blackboard helper is a no-op while state
                // saving is suspended.
                AssistantUISessionState.instance.IncompleteMessageId = null;
            }

            m_ChatInput.SetText(AssistantUISessionState.instance.Prompt);
            var serializableContextList = JsonUtility.FromJson<AssistantContextList>(AssistantUISessionState.instance.Context);
            k_SelectedContext.Clear();
            if (serializableContextList?.m_ContextList.Count > 0)
            {
                RestoreContextSelection(serializableContextList.m_ContextList);
                UpdateContextSelectionElements();
            }

            Trace.Event("ui.restore.complete", new TraceEventOptions
            {
                Level = "debug",
                Data = new { isWorking = Context.Blackboard.IsAPIWorking, isReady = Context.Blackboard.IsAPIReadyForPrompt }
            });
            Context.Blackboard.ResumeStateSave();
        }

        void OnConversationDeleted(AssistantConversationId conversationId)
        {
            if (!Context.Blackboard.ActiveConversationId.IsValid)
            {
                // Clear the chat, in case we deleted our active conversation
                ClearChat();
            }
        }

        void OnConversationRenamed(AssistantConversationId id)
        {
            if (Context.Blackboard.ActiveConversationId == id)
            {
                UpdateConversationTitle(id);
            }
        }

        void OnConversationChanged(AssistantConversationId conversationId)
        {
            UpdateConversationTitle(conversationId);

            // Hide empty state when conversation has content
            var conversation = Context.Blackboard.GetConversation(conversationId);
            if (conversation != null)
            {
                SetEmptyState(conversation.Messages.Count == 0);
            }

            RefreshPlanPromptYesEnabled();
        }

        void UpdateConversationTitle(AssistantConversationId conversationId)
        {
            var conversation = Context.Blackboard.GetConversation(conversationId);
            if (conversation == null)
            {
                // We have not received this conversation data yet
                return;
            }

            m_ConversationName.text = conversation.Title;
        }

        void OnConversationReload(AssistantConversationId conversationId)
        {
            InternalLogUtils.PerformAndSetupDomainReloadLog(conversationId, Context);

            // Cancel stale permission requests on conversation reload.
            // Persistent entries (e.g. the todo progress panel) are left intact.
            Context.InteractionQueue.CancelTransient();
            m_AssistantConversationPanel.Populated -= OnConversationPanelPopulated;

            // If this conversation is not active, we don't display it
            if (Context.Blackboard.ActiveConversationId != conversationId)
            {
                return;
            }

            var conversation = Context.Blackboard.GetConversation(conversationId);
            if (conversation == null)
            {
                // Data not arrived yet: don't clear the panel or consume the pending restore —
                // the reload event that delivers the data completes them.
                return;
            }

            var wasWaitingForRestore = m_WaitingForConversationChange;
            if (wasWaitingForRestore)
            {
                m_AssistantConversationPanel.Populated += OnConversationPanelPopulated;
            }

            ClearChat(false);

            m_WaitingForConversationChange = false;
            UpdateEmptyStateDisplay();

            var sw = new Stopwatch();
            sw.Start();

            AssistantPerf.SetGauge(PerfGauge.ConversationMessages, conversation.Messages.Count);

            try
            {
                m_ConversationName.text = conversation.Title;

                using (AssistantPerf.Measure(PerfProbe.ConversationReload))
                {
                    m_AssistantConversationPanel.Populate(conversation);
                }

                SetEmptyState(conversation.Messages.Count == 0);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Failed to populate conversation panel: " + e.Message);
            }
            finally
            {
                sw.Stop();

                InternalLog.Log($"PopulateConversation took {sw.ElapsedMilliseconds}ms ({conversation.Messages.Count} Messages)");
            }

            // After Populate, trigger UI state restoration if this was a domain reload restore.
            // In non-batch mode, call RestoreUIState directly instead of using delayCall
            // which was vulnerable to being wiped by subsequent domain reloads.
            // In batch mode, use the original delayCall approach.
            if (wasWaitingForRestore)
            {
                if (Application.isBatchMode)
                {
                    EditorTask.delayCall += () => RestoreUIState(conversationId);
                }
                else
                {
                    RestoreUIState(conversationId);
                }
            }
        }

        void OnConversationLoadFailed(AssistantConversationId conversationId)
        {
            if (!m_WaitingForConversationChange)
                return;

            if (Context.Blackboard.ActiveConversationId.IsValid && conversationId != Context.Blackboard.ActiveConversationId)
                return;

            // The failed load never raises a reload event, so finish the restore here:
            // unblock the loading state and resume session-state saving via RestoreUIState.
            m_WaitingForConversationChange = false;
            UpdateEmptyStateDisplay();
            RestoreUIState(default);
        }

        void OnConversationPanelPopulated()
        {
            m_AssistantConversationPanel.Populated -= OnConversationPanelPopulated;

            if (m_ConversationReloadStopwatch == null)
                return;

            m_ConversationReloadStopwatch.Stop();
            AIAssistantAnalytics.ReportUITriggerLocalConversationRestoredEvent(Context.Blackboard.ActiveConversationId, m_ConversationReloadStopwatch.ElapsedMilliseconds);
            m_ConversationReloadStopwatch = null;
        }

        void ClearChat(bool clearInput = true)
        {
            m_ConversationName.text = k_NewConversationTitle;

            if (clearInput)
            {
                m_ChatInput.ClearText();
            }

            m_AssistantConversationPanel.ClearConversation();
            SetEmptyState(true);
        }

        /// <summary>
        /// Updates the empty state root to show either a loading spinner (when waiting for conversation after domain reload)
        /// or the empty state content (See what's new + disclaimer) when loaded.
        /// </summary>
        void UpdateEmptyStateDisplay()
        {
            bool showCenteredLayout = m_IsEmptyState && !m_WaitingForConversationChange;

            m_RootMain.UnregisterCallback<GeometryChangedEvent>(OnEmptyStateGeometryChanged);
            m_FooterRoot.UnregisterCallback<GeometryChangedEvent>(OnEmptyStateGeometryChanged);

            if (showCenteredLayout)
            {
                m_RootMain.AddToClassList("mui-root-empty");
                m_RootMain.RegisterCallback<GeometryChangedEvent>(OnEmptyStateGeometryChanged);
                m_FooterRoot.RegisterCallback<GeometryChangedEvent>(OnEmptyStateGeometryChanged);
            }
            else
            {
                m_RootMain.RemoveFromClassList("mui-root-empty");
                m_WhatsNewBannerRoot.style.maxHeight = StyleKeyword.Null;
            }

            if (m_WaitingForConversationChange)
            {
                m_EmptyStateLoading.SetDisplay(true);
                m_EmptyStateContent.SetDisplay(false);
                m_EmptyStateSpinner.Show();
            }
            else
            {
                m_EmptyStateLoading.SetDisplay(false);
                m_EmptyStateContent.SetDisplay(true);
                m_EmptyStateSpinner.Hide();
                m_WhatsNewBanner.Collapse();
            }

            FireSuggestionsShownEventIfNeeded();
            RefreshPlanPromptYesEnabled();

            // EARLY_ACCESS_PROMO_START
            ApplyEarlyAccessPromoCoexistence();
            // EARLY_ACCESS_PROMO_END
        }

        void OnEmptyStateGeometryChanged(GeometryChangedEvent evt)
        {
            if (!m_IsEmptyState || m_FooterRoot.worldBound.width == 0 || m_WhatsNewBannerRoot.worldBound.width == 0) return;

            var footerTop = m_FooterRoot.worldBound.yMin;
            var bannerTop = m_WhatsNewBannerRoot.worldBound.yMin;
            var availableHeight = footerTop - bannerTop;
            m_WhatsNewBannerRoot.style.maxHeight = Mathf.Max(0f, availableHeight);
        }

        void SetUpSessionBannerOverlay()
        {
            if (m_SessionNotificationsArea == null || m_RootMain == null || m_HeaderRoot == null)
                return;

            m_SessionBannerOverlay = new VisualElement { name = "session-notifications-overlay", pickingMode = PickingMode.Ignore };
            m_SessionBannerOverlay.AddToClassList("mui-session-banner-overlay");
            m_RootMain.Add(m_SessionBannerOverlay);

            UpdateBlockingBannerPlacement();
        }

        // A blocking banner (legal agreement) must stay reachable even when the empty-state footer is
        // centered over the header, so it is hosted in an overlay that paints last; other banners stay
        // in the header flow and push content down as usual.
        void UpdateBlockingBannerPlacement()
        {
            if (m_SessionNotificationsArea == null || m_SessionBannerOverlay == null || m_HeaderRoot == null)
                return;

            var target = m_SessionBanner is { IsBlockingBannerActive: true } ? m_SessionBannerOverlay : m_HeaderRoot;
            if (m_SessionNotificationsArea.parent == target)
                return;

            m_SessionNotificationsArea.RemoveFromHierarchy();
            target.Add(m_SessionNotificationsArea);
        }

        void FireSuggestionsShownEventIfNeeded()
        {
            var shouldBeVisible = m_EmptyStateVisible && !m_WaitingForConversationChange;
            if (shouldBeVisible && !m_SuggestionsVisible)
                AIAssistantAnalytics.ReportUITriggerLocalNewChatSuggestionsShownEvent();
            m_SuggestionsVisible = shouldBeVisible;
        }

        void SetEmptyState(bool isEmpty)
        {
            m_IsEmptyState = isEmpty;
            m_EmptyStateRoot.SetDisplay(isEmpty);
            if (isEmpty) m_NewChatTopicBar.Collapse();
            m_EmptyStateVisible = isEmpty;
            UpdateEmptyStateDisplay();
        }

        void OnHistoryClicked(PointerUpEvent evt)
        {
            Context.API.RefreshConversations();

            bool status = !(m_HistoryPanelRoot.style.display == DisplayStyle.Flex);
            SetHistoryDisplay(status);
            AIAssistantAnalytics.ReportUITriggerLocalToggleChatHistoryEvent(status);
        }

        void SetHistoryDisplay(bool isVisible)
        {
            m_HistoryPanelRoot.style.display = isVisible
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            m_HistoryButton.EnableInClassList(k_HistoryOpenClass, isVisible);

            AssistantUISessionState.instance.IsHistoryOpen = isVisible;

            if (isVisible)
            {
                m_HistoryPanel.FocusSearch();
            }
        }

        void OnHistoryEntrySelected(EventHistoryConversationSelected eventData)
        {
            SetHistoryDisplay(false);
        }

        void OnExpandedPanelOpened(EventExpandedPanelOpened eventData)
        {
            m_RootMain.AddToClassList(k_ExpandedPanelActiveClass);
            Context.SearchHelper.HideSearchBar();

            if (eventData.TitleText != null)
            {
                m_ExpandedPanelTitle.text = eventData.TitleText;
                m_ExpandedPanelTitle.SetDisplay(true);
                m_ConversationName.SetDisplay(false);
            }

            if (eventData.HeaderActions != null)
            {
                m_HeaderRow.Insert(m_HeaderRow.IndexOf(m_ExpandedPanelBackButton), eventData.HeaderActions);
                m_ActiveHeaderActions = eventData.HeaderActions;
            }
        }

        void OnExpandedPanelClosed(EventExpandedPanelClosed eventData)
        {
            m_RootMain.RemoveFromClassList(k_ExpandedPanelActiveClass);

            m_ExpandedPanelTitle.SetDisplay(false);
            m_ConversationName.SetDisplay(true);

            m_ActiveHeaderActions?.RemoveFromHierarchy();
            m_ActiveHeaderActions = null;

            var activeConversation = Context.Blackboard.ActiveConversation;
            m_ConversationName.text = activeConversation != null ? activeConversation.Title : k_NewConversationTitle;
        }

        void ResetConversation(string providerId)
        {
            // Single path - API.CancelPrompt works for all providers
            Context.API.CancelPrompt();

            Context.Blackboard.ClearActiveConversation(true);
            AssistantUISessionState.instance.LastActiveConversationId = null;
            ClearChat();
            ClearContext(null);
            m_ProgressElement.Stop();

            Context.API.Reset();

            m_NewChatButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, true);
            TimerUtils.DelayedAction(ref s_NewChatActiveTokenSource, () =>
            {
                m_NewChatButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, false);
            });
        }

        /// <summary>
        /// When gateway preferences change while a non-Unity provider is in error state,
        /// auto-create a new conversation so updated credentials/settings take effect.
        /// </summary>
        async void OnGatewayPreferencesChanged()
        {
            if (m_IsRecoveringFromCredentialError)
                return;

            if (ProviderStateObserver.IsUnityProvider)
                return;

            if (ProviderStateObserver.ReadyState != ProviderStateObserver.ProviderReadyState.Error)
                return;

            m_IsRecoveringFromCredentialError = true;
            try
            {
                try
                {
                    await Context.API.EndActiveSessionAsync();
                }
                catch (Exception ex)
                {
                    InternalLog.LogWarning($"[AssistantView] End session failed during credential recovery: {ex.Message}");
                }

                var providerId = m_ChatInput.SelectedProviderId;
                ResetConversation(providerId);

                if (!AssistantProviderFactory.IsUnityProvider(providerId))
                {
                    await Context.SwitchProviderAsync(providerId);
                    m_ChatInput.BindModeProvider(Context.API.Provider);
                }
            }
            finally
            {
                m_IsRecoveringFromCredentialError = false;
            }
        }

        internal async void OnNewChatClicked(PointerUpEvent evt)
        {
            InternalLog.Log("[AssistantView] New conversation button clicked");
            await StartNewConversationAsync();
        }

        async Task StartNewConversationAsync()
        {
            try
            {
                await Context.API.EndActiveSessionAsync();
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[AssistantView] End session failed: {ex.Message}");
            }

            ResetConversation(m_ChatInput.SelectedProviderId);

            // For non-Unity providers, create a new session by switching to the same provider
            if (!AssistantProviderFactory.IsUnityProvider(m_ChatInput.SelectedProviderId))
            {
                await Context.SwitchProviderAsync(m_ChatInput.SelectedProviderId);
                m_ChatInput.BindModeProvider(Context.API.Provider);
            }

            AIAssistantAnalytics.ReportUITriggerBackendCreateNewConversationEvent();
        }

        void OnHierarchChanged()
        {
            UpdateContextSelectionElements();
            RefreshPlanPromptBannerVisibility();
        }

        void OnAssetDeletes(string[] paths)
        {
            CheckContextForDeletedAssets(paths);
            RefreshPlanPromptBannerVisibility();
        }

        async void OnProviderChanged(string oldProviderId, string newProviderId)
        {
            // A user-initiated model change supersedes the capacity notice — dismiss the banner.
            // (The automatic Ultra->Lite fallback switches via SetProvider(triggerEvent:false), which
            // does not raise this event, so the banner shown during the fallback is preserved.)
            m_SessionBanner?.HideCapacityBanner();

            // Notify the provider state observer FIRST so status tracking updates immediately
            ProviderStateObserver.SetProvider(newProviderId);

            // Prevent re-entrancy when users toggle providers quickly
            if (m_IsSwitchingProvider)
                return;

            m_IsSwitchingProvider = true;
            try
            {
                await SwitchProviderCoreAsync(oldProviderId, newProviderId);
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[AssistantView] Provider switch failed: {ex.Message}");
            }
            finally
            {
                m_IsSwitchingProvider = false;
            }
        }

        async Task SwitchProviderCoreAsync(string oldProviderId, string newProviderId)
        {
            // Switching between Unity Max and Fast: same backend, only model settings change — keep conversation.
            if (AssistantProviderFactory.IsUnityProvider(oldProviderId) && AssistantProviderFactory.IsUnityProvider(newProviderId))
            {
                await Context.SwitchProviderAsync(newProviderId);
                m_ChatInput.BindModeProvider(Context.API.Provider);
                return;
            }

            // End active session before switching — ensures cancel completes
            // and session is released before the old provider is disposed.
            try
            {
                await Context.API.EndActiveSessionAsync();
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[AssistantView] End session failed during provider switch: {ex.Message}");
            }

            // Clear conversation panel
            m_AssistantConversationPanel.ClearConversation();

            // Clear model selector - new provider will fire events if it supports them
            m_ChatInput.ClearModels();

            // Don't wipe the blackboard here — the history panel is intentionally cross-provider
            // (see ConversationLoader.LoadAdditionalProviderConversations). Wiping forces every
            // switch to wait for the slow Unity REST refetch before cloud convos reappear.
            // Per-provider eviction in ConversationLoader handles staleness as each provider
            // refreshes.

            ResetConversation(oldProviderId);

            if (AssistantProviderFactory.IsUnityProvider(oldProviderId) && !AssistantProviderFactory.IsUnityProvider(newProviderId))
            {
                Context.API.DisconnectWorkflow();
            }

            // Switch provider via the context (factory handles session lifecycle)
            await Context.SwitchProviderAsync(newProviderId);

            // Bind mode provider to the new assistant provider
            m_ChatInput.BindModeProvider(Context.API.Provider);
        }

        void OnModelsAvailable((string modelId, string name, string description)[] models, string currentModelId)
        {
            var providerId = m_ChatInput.SelectedProviderId;
            var preferredModelId = AssistantEditorPreferences.GetSelectedModel(providerId);
            var selectedModelId = currentModelId;

            if (!string.IsNullOrEmpty(preferredModelId) && models != null)
            {
                var preferredAvailable = false;
                foreach (var (modelId, _, _) in models)
                {
                    if (modelId == preferredModelId)
                    {
                        preferredAvailable = true;
                        break;
                    }
                }

                if (preferredAvailable)
                {
                    selectedModelId = preferredModelId;

                    if (!string.IsNullOrEmpty(currentModelId) && currentModelId != preferredModelId)
                    {
                        // Re-apply the preferred model after reload if session reports a different model.
                        Context.API.SetModel(preferredModelId);
                    }
                }
            }

            m_ChatInput.SetModels(models, selectedModelId);
        }

        void OnAvailableCommandsChanged((string name, string description)[] commands)
        {
            m_ChatInput.SetAvailableCommands(commands);
        }

        void OnCommandSelected(string commandName)
        {
            // Send the command as a user message with "/" prefix
            OnRequestSubmit("/" + commandName);
        }

        void OnModelSelected(string modelId)
        {
            // Save the model preference
            AssistantEditorPreferences.SetSelectedModel(m_ChatInput.SelectedProviderId, modelId);

            // Send the model change via the provider
            Context.API.SetModel(modelId);
        }

        void OnActiveProgressCancelRequested()
        {
            // Cancel transient interactions (e.g. permission panels) but preserve persistent
            // entries like the todo progress panel, which should survive a stop/cancel.
            Context.InteractionQueue?.CancelTransient();

            if (!Context.Blackboard.IsAPIWorking)
            {
                return;
            }

            AIAssistantAnalytics.ReportUITriggerBackendCancelRequestEvent(Context.Blackboard.ActiveConversationId);

            // Single path - works for all providers
            Context.API.CancelAssistant(Context.Blackboard.ActiveConversationId);
        }

        void OnPromptSuggestionSelected(string prompt) => m_ChatInput.SetText(prompt);

        void OnRequestSubmit(string message)
        {
            message = message.Trim(k_MessageTrimChars);
            if (string.IsNullOrEmpty(message))
            {
                m_ChatInput.ClearText();
                return;
            }

            // Single code path for all providers
            if (Context.Blackboard.IsAPIWorking)
            {
                Context.API.CancelAssistant(Context.Blackboard.ActiveConversationId);
                m_ChatInput.ClearText();
                return;
            }

            SendPromptFromUser(message);
        }

        /// <summary>
        /// Sends a trimmed non-empty prompt after the same preconditions as <see cref="OnRequestSubmit"/> (not working).
        /// </summary>
        void SendPromptFromUser(string trimmedMessage)
        {
            Context.Blackboard.UnlockConversationChange();
            m_ChatInput.ClearText();
            SetEmptyState(false);

            // Capture the attached context before it is cleared, so a send that drops before the server
            // acknowledges can restore it with the prompt (see OnRestoreUnsentPrompt).
            k_PendingContextSnapshot.Clear();
            k_PendingContextSnapshot.AddRange(k_SelectedContext);

            Context.API.SendPrompt(trimmedMessage, Context.Blackboard.ActiveMode);

            // Clear screenshot attachments (notifies EditScreenCaptureWindow) then clear all remaining
            ClearScreenshotContextEntries();
            k_SelectedContext.Clear();
            UpdateContextSelectionElements();
        }

        void OnPlanPromptApiStateChanged() => RefreshPlanPromptYesEnabled();

        void RefreshPlanPromptBannerVisibility()
        {
            if (m_PlanPromptBannerRoot == null || m_PlanPromptBanner == null)
                return;

            if (AssistantProjectPreferences.PlanExecutionPromptDismissed)
            {
                m_PlanPromptContent = null;
                m_PlanPromptBannerRoot.SetDisplay(false);
                return;
            }

            var planPromptFile = ServiceRegistry.GetService<IPlanPromptFile>();
            if (planPromptFile == null)
            {
                InternalLog.LogError($"[ContextEntry] {nameof(IPlanPromptFile)} service not registered - is {nameof(AssistantWindow)} open?");
                return;
            }
            
            if (!planPromptFile.TryReadTruncated(out var content))
            {
                m_PlanPromptContent = null;
                m_PlanPromptBannerRoot.SetDisplay(false);
                return;
            }

            m_PlanPromptContent = content;
            // Let USS (.mui-root-empty #planPromptBannerRoot) control visibility when a plan exists.
            m_PlanPromptBannerRoot.style.display = StyleKeyword.Null;
        }

        void RefreshPlanPromptYesEnabled()
        {
            if (m_PlanPromptBanner == null || m_PlanPromptBannerRoot == null)
                return;

            if (string.IsNullOrEmpty(m_PlanPromptContent) || !m_IsEmptyState || m_WaitingForConversationChange)
                return;

            m_PlanPromptBanner.SetYesButtonEnabled(CanEnableChat());
        }

        void OnPlanPromptNoClicked()
        {
            var planPromptFile = ServiceRegistry.GetService<IPlanPromptFile>();
            if (planPromptFile == null)
            {
                InternalLog.LogError($"[ContextEntry] {nameof(IPlanPromptFile)} service not registered - is {nameof(AssistantWindow)} open?");
                return;
            }
            
            AssistantProjectPreferences.PlanExecutionPromptDismissed = true;
            RefreshPlanPromptBannerVisibility();
        }

        async void OnPlanPromptYesClicked()
        {
            var message = m_PlanPromptContent?.Trim(k_MessageTrimChars) ?? string.Empty;
            if (string.IsNullOrEmpty(message))
            {
                RefreshPlanPromptBannerVisibility();
                return;
            }

            if (Context.Blackboard.IsAPIWorking)
            {
                Context.API.CancelAssistant(Context.Blackboard.ActiveConversationId);
                m_ChatInput.ClearText();
                return;
            }
            
            var planPromptFile = ServiceRegistry.GetService<IPlanPromptFile>();
            if (planPromptFile == null)
            {
                InternalLog.LogError($"[ContextEntry] {nameof(IPlanPromptFile)} service not registered - is {nameof(AssistantWindow)} open?");
                return;
            }
            
            AssistantProjectPreferences.PlanExecutionPromptDismissed = true;
            RefreshPlanPromptBannerVisibility();

            try
            {
                await StartNewConversationAsync();
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[AssistantView] Plan prompt: new conversation failed: {ex.Message}");
                return;
            }

            if (Context.Blackboard.IsAPIWorking)
                return;

            SendPromptFromUser(message);
        }

        void OnViewGeometryChanged(GeometryChangedEvent evt)
        {
            bool isCompactView = evt.newRect.width < AssistantUIConstants.CompactWindowThreshold;

            m_HistoryButton.EnableInClassList(AssistantUIConstants.CompactStyle, isCompactView);
            m_NewChatButton.EnableInClassList(AssistantUIConstants.CompactStyle, isCompactView);

            m_ConversationName.EnableInClassList(AssistantUIConstants.CompactStyle, isCompactView);

            m_FooterRoot.EnableInClassList(AssistantUIConstants.CompactStyle, isCompactView);
        }

        void AddItemsNumberToLabel(int numItems)
        {
            m_DropdownToggleLabel.text = $"Attached items ({numItems})";
        }

        void ToggleContextDropdown()
        {
            if (m_SelectedContextDropdown.IsShown)
            {
                m_ContextDropdownButton.RemoveFromClassList(k_ContextDropdownToggleFocused);
                HideContextPopup();
            }
            else
            {
                m_ContextDropdownButton.AddToClassList(k_ContextDropdownToggleFocused);
                ShowContextPopup();
            }
        }

        void ToggleSelectionPopup()
        {
            if (m_SelectionPopup.IsShown)
            {
                HideSelectionPopup();
            }
            else
            {
                m_PopupOpenedByMention = false;
                ShowSelectionPopup();
            }
        }

        void ShowContextPopup()
        {
            m_SelectedContextDropdown.Show();

            m_ContextDropdownTracker = new PopupTracker(
                m_SelectedContextDropdown,
                m_ContextDropdownButton,
                new Vector2Int(-1, 47),
                m_ContextDropdownButton
            );
            m_ContextDropdownTracker.Dismiss += HideContextPopup;
        }

        void HideContextPopup()
        {
            if (m_ContextDropdownTracker == null)
            {
                // Popup is not active
                return;
            }

            m_ContextDropdownTracker.Dismiss -= HideContextPopup;
            m_ContextDropdownTracker.Dispose();
            m_ContextDropdownTracker = null;

            m_SelectedContextDropdown.Hide();
        }

        void OnMentionTriggered()
        {
            m_PopupOpenedByMention = true;
            if (!m_SelectionPopup.IsShown) ShowSelectionPopup();
        }

        void OnSelectionPopupDismissRequested()
        {
            HideSelectionPopup();
            m_ChatInput.FocusInput();
        }

        void OnContextObjectAddedFromPopup(UnityEngine.Object obj)
        {
            if (m_PopupOpenedByMention)
            {
                m_ChatInput.RemoveCharBeforeCursorIfMatch('@');
                m_ChatInput.InsertTextAtCursor(obj.name + " ");
            }
        }

        void ShowSelectionPopup()
        {
            // Restore previous context selection
            m_SelectionPopup.SetSelectionFromContext(k_SelectedContext);

            m_SelectionPopup.ShowPopup();

            m_SelectionPopupTracker = new PopupTracker(m_SelectionPopup, m_ChatInput.ContextButton);
            m_SelectionPopupTracker.Dismiss += HideSelectionPopup;
        }

        void HideSelectionPopup()
        {
            if (m_SelectionPopupTracker == null)
            {
                // Popup is not active
                return;
            }

            m_SelectionPopupTracker.Dismiss -= HideSelectionPopup;
            m_SelectionPopupTracker.Dispose();
            m_SelectionPopupTracker = null;

            m_SelectionPopup.Hide();
            m_PopupOpenedByMention = false;
        }

        void InitializeContextDropdown()
        {
            m_SelectedContextDropdown = new ContextDropdown();
            m_SelectedContextDropdown.Initialize(Context);
            m_SelectedContextDropdown.Hide();

            m_PopupRoot.Add(m_SelectedContextDropdown);

            if (k_HostWindow != null)
            {
                k_HostWindow.FocusLost += HideContextPopup;
            }
        }

        void InitializeSelectionPopup()
        {
            m_SelectionPopup = new SelectionPopup();
            m_SelectionPopup.Initialize(Context);
            m_SelectionPopup.Hide();
            m_SelectionPopup.OnSelectionChanged += () =>
            {
                // Memorize current context selection
                SyncContextSelection(m_SelectionPopup.ObjectSelection, m_SelectionPopup.ConsoleSelection);

                UpdateContextSelectionElements();
            };
            m_SelectionPopup.OnContextObjectAdded += OnContextObjectAddedFromPopup;
            m_SelectionPopup.OnAnnotationRequested += OnAnnotationRequested;
            m_SelectionPopup.OnDismissRequested += OnSelectionPopupDismissRequested;

            m_ChatInputRoot.Insert(0, m_SelectionPopup);

            if (k_HostWindow != null)
            {
                k_HostWindow.FocusLost += HideSelectionPopup;
            }
        }

        void OnAnnotationRequested()
        {
            // Open the Edit Screen Capture window for annotations
            EditScreenCaptureWindow.Open();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            GatewayPreferenceService.Instance.Preferences.OnChange -= OnGatewayPreferencesChanged;
            AssistantAssetModificationDelegates.AssetDeletes -= OnAssetDeletes;

            AssistantEvents.Unsubscribe(ref m_ConversationSelectedEventTicket);
            AssistantEvents.Unsubscribe(ref m_CheckpointEnableStateChangedEventTicket);
            AssistantEvents.Unsubscribe(ref m_ExpandedPanelOpenedEventTicket);
            AssistantEvents.Unsubscribe(ref m_ExpandedPanelClosedEventTicket);
            AssistantEvents.Unsubscribe(ref m_RestoreUnsentPromptEventTicket);
            AssistantEvents.Unsubscribe(ref m_ResubmitPromptEventTicket);

            AssistantEditorPreferences.NewSkillsAwaitingNotificationChanged -= OnNewSkillsAwaitingNotificationChanged;
            AssistantEvents.Unsubscribe(ref m_CapacityReachedEventTicket);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssistantAssetModificationDelegates.AssetDeletes += OnAssetDeletes;

            m_ConversationSelectedEventTicket = AssistantEvents.Subscribe<EventHistoryConversationSelected>(OnHistoryEntrySelected);
            m_CheckpointEnableStateChangedEventTicket = AssistantEvents.Subscribe<EventCheckpointEnableStateChanged>(OnCheckpointEnabledChanged);
            m_ExpandedPanelOpenedEventTicket = AssistantEvents.Subscribe<EventExpandedPanelOpened>(OnExpandedPanelOpened);
            m_ExpandedPanelClosedEventTicket = AssistantEvents.Subscribe<EventExpandedPanelClosed>(OnExpandedPanelClosed);
            m_RestoreUnsentPromptEventTicket = AssistantEvents.Subscribe<EventRestoreUnsentPrompt>(OnRestoreUnsentPrompt);
            m_ResubmitPromptEventTicket = AssistantEvents.Subscribe<EventResubmitPrompt>(OnResubmitPrompt);

            AssistantEditorPreferences.NewSkillsAwaitingNotificationChanged += OnNewSkillsAwaitingNotificationChanged;
            m_CapacityReachedEventTicket = AssistantEvents.Subscribe<EventCapacityReached>(OnCapacityReached);
        }

        void OnRestoreUnsentPrompt(EventRestoreUnsentPrompt evt)
        {
            // Only restore if the user has not already started typing something else.
            if (m_ChatInput == null || !string.IsNullOrEmpty(m_ChatInput.Text))
                return;

            m_ChatInput.SetText(evt.Prompt);

            // Restore the attachments captured at send time so the retry resends the full request, not just text.
            RestorePendingContext();
        }

        void OnResubmitPrompt(EventResubmitPrompt evt)
        {
            if (m_ChatInput != null)
                OnRequestSubmit(m_ChatInput.Text);
        }

        void OnNewSkillsAwaitingNotificationChanged()
        {
            UpdateNewSkillsNotificationVisibility();
        }

        // Checkpoint discovery banner takes priority (ASST-3401): hide the new-skills notification while
        // the checkpoint banner is visible; otherwise let the panel show itself if there are pending skills.
        void UpdateNewSkillsNotificationVisibility()
        {
            if (m_NewSkillsNotificationPanel == null)
                return;

            // EARLY_ACCESS_PROMO_START
            if (m_EarlyAccessPromoBanner != null
                && m_EarlyAccessPromoBanner.IsShowing
                && m_IsEmptyState
                && !m_WaitingForConversationChange)
            {
                m_NewSkillsNotificationPanel.SetDisplay(false);
                return;
            }
            // EARLY_ACCESS_PROMO_END

            if (m_CheckpointDiscoveryBanner != null && m_CheckpointDiscoveryBanner.IsShowing)
                m_NewSkillsNotificationPanel.SetDisplay(false);
            else
                m_NewSkillsNotificationPanel.Refresh();
        }

        // EARLY_ACCESS_PROMO_START
        void ApplyEarlyAccessPromoCoexistence()
        {
            if (m_EarlyAccessPromoBanner == null)
                return;

            var blocked = m_SessionBanner is { IsBlockingBannerActive: true };
            m_EarlyAccessPromoBanner.SetBlocked(blocked);

            var takeoverActive = m_EarlyAccessPromoBanner.IsShowing && m_IsEmptyState && !m_WaitingForConversationChange;
            m_EarlyAccessPromoBannerRoot?.SetDisplay(takeoverActive);
            m_WhatsNewBannerRoot?.SetDisplay(!takeoverActive);
            m_CheckpointDiscoveryBannerRoot?.SetDisplay(!takeoverActive);

            m_RootMain?.EnableInClassList("mui-early-access-promo-active", takeoverActive);

            UpdateNewSkillsNotificationVisibility();
        }
        // EARLY_ACCESS_PROMO_END

        /// <summary>
        /// Ensures the specified provider is selected. Used by AssistantApi to reset state.
        /// </summary>
        /// <param name="providerId">The provider ID to ensure is active. Defaults to the default Unity profile.</param>
        public async Task EnsureProviderAsync(string providerId = null)
        {
            providerId ??= AssistantProviderFactory.DefaultProvider.ProfileId;
            if (m_ChatInput.SelectedProviderId == providerId)
                return;

            var oldProviderId = m_ChatInput.SelectedProviderId;

            // Update the provider selector UI (without triggering event since we handle manually)
            m_ChatInput.SetProvider(providerId, triggerEvent: false);

            await SwitchProviderCoreAsync(oldProviderId, providerId);
        }

        void OnCheckpointEnabledChanged(EventCheckpointEnableStateChanged eventData)
        {
            var activeConversationId = Context.Blackboard.ActiveConversationId;

            // Checkpoint init fires this after every domain reload, typically before the active
            // conversation has been fetched; re-rendering without data would be a no-op wipe.
            if (activeConversationId != AssistantConversationId.Invalid
                && Context.Blackboard.GetConversation(activeConversationId) != null)
            {
                OnConversationReload(activeConversationId);
            }
        }

        async void OnCapacityReached(EventCapacityReached evt)
        {
            // Ignore stale events: the picker may be gone, or the user may have switched to a different
            // conversation between sending the prompt and the capacity disconnect arriving.
            if (m_ChatInput == null || evt.ConversationId != Context.Blackboard.ActiveConversationId)
                return;

            var action = CapacityFallbackPolicy.Resolve(m_ChatInput.SelectedProviderId);

            switch (action)
            {
                case CapacityFallbackPolicy.Action.Ignore:
                    return;

                case CapacityFallbackPolicy.Action.ShowReachedBanner:
                    m_SessionBanner?.ShowCapacityReachedBanner();
                    return;

                case CapacityFallbackPolicy.Action.SwitchToDefaultAndOfferResend:
                    // Capture the prompt to (optionally) resend, and the display name of the model that
                    // hit capacity, BEFORE switching providers (the selection changes on the switch).
                    var lastPrompt = AssistantUIContext.GetLastUserPromptText(Context.Blackboard.ActiveConversation)
                        ?? Context.Blackboard.PendingPrompt;
                    var fromModelName = ResolveModelDisplayName(m_ChatInput.SelectedProviderId);

                    // If the prompt can't be recovered there is nothing to resend on the fallback
                    // model, so show the informational banner instead of silently switching.
                    if (string.IsNullOrEmpty(lastPrompt))
                    {
                        m_SessionBanner?.ShowCapacityReachedBanner();
                        return;
                    }

                    try
                    {
                        // Switch the picker to the default provider profile + flash. No tokens are
                        // spent here — only the model selection changes.
                        await EnsureProviderAsync(AssistantProviderFactory.DefaultProvider.ProfileId);
                        m_ChatInput.FlashProviderSelector();

                        // Offer an opt-in resend instead of auto-spending the user's tokens on the
                        // fallback model. On click, resend the prompt on the now-current default profile.
                        var toModelName = ResolveModelDisplayName(AssistantProviderFactory.DefaultProvider.ProfileId);
                        var promptToResend = lastPrompt;
                        m_SessionBanner?.ShowCapacitySwitchedBanner(fromModelName, toModelName, () =>
                        {
                            // Restore the attachments captured at send time so the fallback resend carries the full
                            // request, matching the retry-card path.
                            RestorePendingContext();
                            Context.API.SendPrompt(promptToResend, Context.Blackboard.ActiveMode);
                        });
                    }
                    catch (Exception ex)
                    {
                        InternalLog.LogError($"[AssistantView] Capacity fallback failed: {ex.Message}");
                        m_SessionBanner?.ShowCapacityReachedBanner();
                    }
                    return;
            }
        }

        // Resolve a Unity provider profile's live display name (e.g. "Unity Max") from the model list
        // fetched via GET /v1/assistant/models. Falls back to the provider id if the list isn't ready.
        string ResolveModelDisplayName(string providerId)
        {
            var profiles = Context.AvailableUnityModelProfiles;
            if (profiles != null)
            {
                foreach (var profile in profiles)
                {
                    if (profile.ProviderId == providerId && !string.IsNullOrEmpty(profile.ProfileName))
                        return profile.ProfileName;
                }
            }

            return providerId;
        }

        bool CanEnableChat() => !ProviderStateObserver.IsUnityProvider
                                || Account.pointsBalance.CanAfford(AssistantConstants.ChatPreAuthorizePoints);
    }
}
