using System;
using System.Collections.Generic;
using System.Linq;
#if !UNITY_6000_3_OR_NEWER
using System.Reflection;
#endif
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Acp;
using Unity.AI.Assistant.Editor.Settings;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils;

using Unity.Relay.Editor.Acp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;
using TextField = UnityEngine.UIElements.TextField;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    partial class AssistantTextField : ManagedTemplate
    {
        const string k_ChatFocusClass = "mui-mft-input-focused";
        const string k_ChatHoverClass = "mui-mft-input-hovered";
        const string k_ChatActionEnabledClass = "mui-submit-enabled";
        const string k_HasInteractionClass = "mui-mtf-has-interaction";
        const string k_PlanReviseModeClass = "mui-mtf-plan-revise-mode";
        const string k_InteractionBarRoundedClass = "interaction-bar-rounded";

        static readonly string k_PromptLimitWarningTooltip =
            $"Prompt is at the {AssistantMessageSizeConstraints.PromptLimit}-character limit. Additional input is not accepted.";

        const string k_SubmitImage = "arrow-up";
        const string k_StopImage = "stop-square";

        const string k_ActionButtonToolTipSend = "Send prompt";
        const string k_ActionButtonToolTipStop = "Stop response";
        const string k_ActionButtonToolTipNoPrompt = "No prompt entered";
        const string k_ActionButtonToolTipRevise = "Send revision feedback";

        static readonly string k_ImageAttachmentSizeExceeded =
            $"Total image size exceeds {AssistantConstants.MaxTotalAttachmentSizeMB}MB.\n" +
             "Images may use more memory than their file size on disk. Try removing or resizing an image.";

        const string k_PlaceholderAsk = "Ask about Unity";
        const string k_PlaceholderAgent = "Build with Unity";
        const string k_PlaceholderPlan = "Plan with Unity";
        const string k_PlaceholderRevise = "Add notes or changes to the plan";
        const string k_PlaceholderInitializing = "Initializing session...";
        const string k_PlaceholderError = "Session failed to initialize";

        // Debounce window for committing prompt edits into the Unity Undo stack.
        // Per-keystroke undo entries feel broken to users; 500 ms groups natural typing bursts
        // into a single undo entry (matches the TestBindingWindow reference pattern).
        const long k_PromptUndoDebounceMs = 500;

        VisualElement m_Root;
        UserInteractionBar m_InteractionBar;
        BaseEventSubscriptionTicket m_InteractionQueueSubscription;
        AssistantWindowUiContainer m_UiContainer;

        Button m_ActionButton;
        AssistantImage m_SubmitButtonImage;

        TextField m_ChatInput;
        TextElement m_TextElement; // inner text element, cached for visual-row caret queries
        Label m_Placeholder;
        VisualElement m_PlaceholderContent;
        VisualElement m_ActionRow;

        VisualElement m_ContextLimitWarning;
        VisualElement m_ImageSizeLimitWarning;
        VisualElement m_PromptLimitWarning;
        ContextUsageView m_ContextUsageView;

        readonly PromptHistoryNavigator m_HistoryNavigator = new();
        Label m_HistoryLabel;

        // Fallback for when the active conversation's UI model is transiently rebuilt as an empty shell
        // during a reload (see AssistantUIAPIInterpreter.ConvertConversationToModel). Single slot because
        // only the active conversation is ever read back.
        AssistantConversationId m_CachedHistoryConversationId;
        IReadOnlyList<string> m_CachedHistory;

        Button m_AddContextButton;
        Button m_SettingsButton;
        SettingsPopup m_SettingsPopup;
        PopupTracker m_SettingPopupTracker;

        VisualElement m_PopupRoot;

        // Null until a provider is chosen; treated as the Unity provider by IsUnityProvider.
        string m_SelectedProviderId;
        readonly List<(string id, string displayName)> m_CommandItems = new();
        readonly List<(string id, string displayName)> m_ModelItems = new();
        string m_SelectedModelId;

        bool m_TextHasFocus;
        bool m_ShowPlaceholder;
        bool m_HighlightFocus;
        bool m_EditContextEnabled;
        bool m_ImageSizeExceeded;

        IVisualElementScheduledItem m_PromptUndoDebounceTask;

        internal int CursorIndex
        {
            get => m_ChatInput.cursorIndex;
            set => m_ChatInput.cursorIndex = value;
        }

        internal VisualElement AttachmentRow { get; private set; }
        internal ScrollView AttachmentStrip { get; private set; }
        internal Button ClearAllButton { get; private set; }

        internal PromptHistoryNavigator HistoryNavigator => m_HistoryNavigator;
        internal Label HistoryLabel => m_HistoryLabel;

        /// <summary>
        /// True when the caret sits on the first newline-separated line of <paramref name="text"/>, i.e.
        /// there is no '\n' between the start of the text and the caret (UI Toolkit may wrap a single
        /// logical line across several visual rows; this is about logical lines). Pure so the Up/Down
        /// navigation decision can be unit-tested without a laid-out text field.
        /// </summary>
        internal static bool IsCaretOnFirstLine(string text, int caretIndex)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            caretIndex = Mathf.Clamp(caretIndex, 0, text.Length);
            // IndexOf with a count avoids the Substring allocation on this key-down hot path.
            return text.IndexOf('\n', 0, caretIndex) < 0;
        }

        /// <summary>
        /// True when the caret sits on the last newline-separated line of <paramref name="text"/>, i.e.
        /// there is no '\n' between the caret and the end of the text.
        /// </summary>
        internal static bool IsCaretOnLastLine(string text, int caretIndex)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            caretIndex = Mathf.Clamp(caretIndex, 0, text.Length);
            // IndexOf from the caret avoids the Substring allocation on this key-down hot path.
            return text.IndexOf('\n', caretIndex) < 0;
        }

        /// <summary>
        /// Caret on the first visual row. Being on the first logical line is a necessary condition; when the
        /// field is laid out this is refined to the first soft-wrapped row of that line. Falls back to the
        /// plain logical-line check when caret positions aren't available.
        /// </summary>
        bool IsCaretOnFirstVisualRow(string text, int caret)
        {
            if (!IsCaretOnFirstLine(text, caret))
                return false;
            return !TryGetCaretOnVisualEdge(caret, wantFirst: true, out var onEdge) || onEdge;
        }

        /// <summary>
        /// Caret on the last visual row. Being on the last logical line is a necessary condition (a later '\n'
        /// means later visual rows); when laid out this is refined to the last soft-wrapped row of that line.
        /// </summary>
        bool IsCaretOnLastVisualRow(string text, int caret)
        {
            if (!IsCaretOnLastLine(text, caret))
                return false;
            return !TryGetCaretOnVisualEdge(caret, wantFirst: false, out var onEdge) || onEdge;
        }

        /// <summary>
        /// The caret is on the top (or bottom) visual row when its Y matches the first (or last) character's Y.
        /// Reads caret positions from the text handle's cursor-position API, which generates the mesh on demand,
        /// so — unlike GetLineNumber — it never logs "Failed to perform selection on text" on an ungenerated
        /// field. Returns false (caller falls back to the logical-line check) when the field isn't laid out or
        /// positions can't be read.
        /// </summary>
        bool TryGetCaretOnVisualEdge(int caret, bool wantFirst, out bool onEdge)
        {
            onEdge = false;
            // Visual rows require a laid-out field: the caret-position APIs read (and generate) the text mesh,
            // which errors or returns junk when the field isn't laid out yet — e.g. attached to a panel but
            // never rendered, as in unit tests. Until layout has produced a content rect, fall back to the
            // logical-line check. (contentRect.width is 0/NaN before the first layout pass.)
            if (m_TextElement?.panel == null || !(m_TextElement.contentRect.width > 0f))
                return false;

            var textLength = Text.Length;
            if (!TryGetCursorY(Mathf.Clamp(caret, 0, textLength), out var caretY)
                || !TryGetCursorY(wantFirst ? 0 : textLength, out var edgeY))
                return false;

            onEdge = Mathf.Approximately(caretY, edgeY);
            return true;
        }

#if UNITY_6000_3_OR_NEWER
        // 6.3+ exposes the per-index caret position publicly.
        bool TryGetCursorY(int index, out float y)
        {
            y = m_TextElement.selection.GetCursorPositionFromStringIndex(index).y;
            return true;
        }
#else
        // 6.0-6.2 lack the public per-index API, so reach the internal method it wraps
        // (GetCursorPositionFromStringIndexUsingLineHeight — public on the base TextHandle, so no hierarchy
        // walk). It generates the mesh first, and Advanced Text isn't the default before 6.5, so the standard
        // path is taken and the selection error never applies. Latches a removed API to the logical fallback.
        static MethodInfo s_CursorPosMethod;
        static bool s_CursorPosReflectionFailed;
        readonly object[] m_CursorPosArgs = { 0, /*useXAdvance*/ false, /*inverseYAxis*/ true };

        bool TryGetCursorY(int index, out float y)
        {
            y = 0f;
            if (s_CursorPosReflectionFailed)
                return false;

            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var handle = typeof(TextElement).GetProperty("uitkTextHandle", flags)?.GetValue(m_TextElement);
                if (handle == null) { s_CursorPosReflectionFailed = true; return false; }

                s_CursorPosMethod ??= handle.GetType().GetMethod("GetCursorPositionFromStringIndexUsingLineHeight",
                    flags, null, new[] { typeof(int), typeof(bool), typeof(bool) }, null);
                if (s_CursorPosMethod == null) { s_CursorPosReflectionFailed = true; return false; }

                m_CursorPosArgs[0] = index;
                y = ((Vector2)s_CursorPosMethod.Invoke(handle, m_CursorPosArgs)).y;
                return true;
            }
            catch
            {
                return false;
            }
        }
#endif

        public AssistantTextField()
            : base(AssistantUIConstants.UIModulePath)
        {
            m_EditContextEnabled = false;

            RegisterAttachEvents(OnAttachToPanel, OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            AssistantEditorPreferences.AiGatewayDisclaimerAcceptedChanged += RefreshProviderItems;
            AssistantEditorPreferences.ProviderEnabledChanged += RefreshProviderItems;
            AcpProvidersRegistry.OnProvidersChanged += RefreshProviderItems;
            GatewayPreferenceService.Instance.Preferences.OnChange += RefreshProviderItems;
            Context.ConversationLoader.ConversationsLoaded += RefreshProviderItems;
            m_InteractionQueueSubscription = AssistantEvents.Subscribe<EventInteractionQueueChanged>(OnInteractionQueueChanged);
            Context.API.ConversationChanged += OnConversationChanged;
            Context.API.ConversationReload += OnConversationChanged;
            Context.API.ConversationDeleted += OnConversationDeleted;
            Context.Blackboard.ActiveConversationChanged += OnActiveConversationChanged;
            Undo.undoRedoPerformed += OnPromptUndoRedo;

            if (m_UiContainer != null)
            {
                m_UiContainer.PlanApproved += OnPlanApproved;
                m_UiContainer.PlanModeRequested += OnEnterPlanModeRequested;
            }

            UpdateInteractionBarStyle();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssistantEditorPreferences.AiGatewayDisclaimerAcceptedChanged -= RefreshProviderItems;
            AssistantEditorPreferences.ProviderEnabledChanged -= RefreshProviderItems;
            AcpProvidersRegistry.OnProvidersChanged -= RefreshProviderItems;
            GatewayPreferenceService.Instance.Preferences.OnChange -= RefreshProviderItems;
            Context.ConversationLoader.ConversationsLoaded -= RefreshProviderItems;
            AssistantEvents.Unsubscribe(ref m_InteractionQueueSubscription);
            Context.Blackboard.ActiveConversationChanged -= OnActiveConversationChanged;
            Context.API.ConversationChanged -= OnConversationChanged;
            Context.API.ConversationReload -= OnConversationChanged;
            Context.API.ConversationDeleted -= OnConversationDeleted;
            Undo.undoRedoPerformed -= OnPromptUndoRedo;

            if (m_UiContainer != null)
            {
                m_UiContainer.PlanApproved -= OnPlanApproved;
                m_UiContainer.PlanModeRequested -= OnEnterPlanModeRequested;
            }

            // Keep history lifecycle symmetric with attach: a re-attached field starts with no active history.
            m_HistoryNavigator.Exit();
            UpdateHistoryLabel();

            // Flush any pending debounced commit so in-flight edits still land in the undo stack.
            m_PromptUndoDebounceTask?.Pause();
            m_PromptUndoDebounceTask = null;
            CommitPromptToUndo();

            // Stop any in-flight provider-selector flash so the scheduler doesn't tick a detached element.
            ClearProviderSelectorHighlight();
        }

        void OnInteractionQueueChanged(EventInteractionQueueChanged evt)
        {
            UpdateInteractionBarStyle();
        }

        void OnActiveConversationChanged(AssistantConversationId previousConversationId, AssistantConversationId currentConversationId)
        {
            // History is per-conversation; leaving the conversation ends any in-progress navigation.
            m_HistoryNavigator.Exit();
            UpdateHistoryLabel();

            OnConversationChanged(currentConversationId);
        }

        internal void OnConversationDeleted(AssistantConversationId conversationId)
        {
            // Drop the cached history so it cannot be served for a deleted conversation.
            if (m_CachedHistoryConversationId == conversationId)
            {
                m_CachedHistoryConversationId = default;
                m_CachedHistory = null;
            }
        }

        void OnConversationChanged(AssistantConversationId conversationId)
        {
            if (conversationId != Context.Blackboard.ActiveConversationId)
                return;

            var conversation = Context.Blackboard.GetConversation(conversationId);
            if (conversation != null && conversation.ContextUsageMaxTokens > 0)
            {
                m_ContextUsageView.SetUsage(conversation.ContextUsageUsedTokens, conversation.ContextUsageMaxTokens);
            }
            else
            {
                m_ContextUsageView.ResetUsage();
            }
        }

        void UpdateInteractionBarStyle()
        {
            var hasInteraction = Context?.InteractionQueue?.HasPending ?? false;
            var hideMainInput = hasInteraction && (Context.InteractionQueue.Current?.HideMainInput ?? false);

            m_Root.EnableInClassList(k_HasInteractionClass, hasInteraction && !hideMainInput);
            m_Root.EnableInClassList(k_PlanReviseModeClass, IsPlanReviseMode);
            m_Root.SetDisplay(!hideMainInput);
            m_InteractionBar.EnableInClassList(k_InteractionBarRoundedClass, hideMainInput);

            UpdatePlaceholderText();
            RefreshUI();
        }

        bool IsPlanReviseMode => Context?.InteractionQueue?.Current?.ContentView is PlanApprovalFooterContent;

        public bool ShowPlaceholder
        {
            get => m_ShowPlaceholder;
            set
            {
                if (m_ShowPlaceholder == value)
                {
                    return;
                }

                m_ShowPlaceholder = value;
                RefreshUI();
            }
        }

        public bool HighlightFocus
        {
            get => m_HighlightFocus;
            set
            {
                if (m_HighlightFocus == value)
                {
                    return;
                }

                m_HighlightFocus = value;
                RefreshUI();
            }
        }

        internal string Text => m_ChatInput?.value ?? string.Empty;

        public event Action<string> SubmitRequest;
        public event Action CancelRequest;
        public event Action<string, string> OnProviderChanged;
        public event Action<string> OnCommandSelected;
        public event Action<string> OnModelSelected;
        public event Action MentionTriggered;

        public Button ContextButton => m_AddContextButton;
        public string SelectedProviderId => m_SelectedProviderId;

        /// <summary>
        /// Programmatically set the selected provider.
        /// </summary>
        /// <param name="providerId">The provider ID to select.</param>
        /// <param name="triggerEvent">Whether to fire the OnProviderChanged event.</param>
        public void SetProvider(string providerId, bool triggerEvent = true)
        {
            if (m_SelectedProviderId == providerId)
                return;

            var oldProvider = m_SelectedProviderId;
            m_SelectedProviderId = providerId;
            m_ProviderMenu?.SetSelectedId(providerId);
            UpdateProviderLabel();
            UpdateProviderTooltip();
            // Ensure UI state stays in sync even when we skip provider-change events (e.g., domain reload restore).
            UpdateCommandSelectorVisibility();
            UpdateCommandSelectorEnabled();

            if (triggerEvent)
            {
                OnProviderChangedHandler(oldProvider, providerId);
            }
        }

        public void BindUiContainer(AssistantWindowUiContainer container)
        {
            if (m_UiContainer == container) return;

            if (m_UiContainer != null && panel != null)
            {
                m_UiContainer.PlanApproved -= OnPlanApproved;
                m_UiContainer.PlanModeRequested -= OnEnterPlanModeRequested;
            }

            m_UiContainer = container;

            if (m_UiContainer != null && panel != null)
            {
                m_UiContainer.PlanApproved += OnPlanApproved;
                m_UiContainer.PlanModeRequested += OnEnterPlanModeRequested;
            }
        }

        public void SetHost(VisualElement popupRoot)
        {
            m_PopupRoot = popupRoot;
            m_EditContextEnabled = true;

            InitializeSettingsPopup();
            InitializeDropdownMenus(m_PopupRoot);

            m_AddContextButton.SetDisplay(m_EditContextEnabled);
        }

        public void ClearText()
        {
            m_ChatInput.value = "";
        }

        public void SetText(string text)
        {
            m_ChatInput.value = text;
            m_ChatInput.Focus();
        }

        public void InsertTextAtCursor(string text)
        {
            m_ChatInput.value = m_ChatInput.value.Insert(CursorIndex, text);
            CursorIndex += text.Length;
            m_ChatInput.selectIndex = CursorIndex;
        }

        public void RemoveCharBeforeCursor()
        {
            if (CursorIndex <= 0) return;

            m_ChatInput.value = m_ChatInput.value.Remove(CursorIndex - 1, 1);
            CursorIndex--;
            m_ChatInput.selectIndex = CursorIndex;
        }

        public void RemoveCharBeforeCursorIfMatch(char c)
        {
            if (CursorIndex <= 0 || m_ChatInput.value[CursorIndex - 1] != c) return;

            RemoveCharBeforeCursor();
        }

        public void FocusInput()
        {
            m_ChatInput.Focus();
        }

        public void Enable()
        {
            m_ChatInput.SetEnabled(true);
            RefreshUI();
        }

        public void Disable(string reason = "")
        {
            m_Placeholder.text = reason;
            m_ChatInput.SetEnabled(false);
            RefreshUI();
        }

        public void ToggleContextLimitWarning(bool enabled)
        {
            m_ContextLimitWarning.SetDisplay(enabled);
        }

        public void ToggleImageSizeLimitWarning(bool enabled)
        {
            m_ImageSizeExceeded = enabled;
            RefreshUI();
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Root = view.Q<VisualElement>("museTextFieldRoot");
            AttachmentRow = view.Q<VisualElement>("attachmentRow");
            AttachmentStrip = view.Q<ScrollView>("attachmentStrip");
            ClearAllButton = view.Q<Button>("clearAllButton");

            m_InteractionBar = new UserInteractionBar();
            m_InteractionBar.Initialize(Context, autoShowControl: false);
            view.Insert(view.IndexOf(m_Root), m_InteractionBar);

            m_AddContextButton = view.Q<Button>("addContextButton");
            m_AddContextButton.SetDisplay(m_EditContextEnabled);

            m_SettingsButton = view.Q<Button>("settingsButton");
            m_SettingsButton.clicked += OnSettingsButtonClicked;

            InitializeDropdowns(view);

            // Subscribe to mode changes for settings popup auto-run update
            m_ModeProvider.ModeChanged += OnModeChanged;

            UpdateSettingsPopupAutoRun();

            m_ActionButton = view.Q<Button>("actionButton");
            m_ActionButton.RegisterCallback<PointerUpEvent>(_ => OnSubmit());

            m_SubmitButtonImage = view.SetupImage("actionButtonImage", k_SubmitImage);

            m_PlaceholderContent = view.Q<VisualElement>("placeholderContent");
            m_Placeholder = view.Q<Label>("placeholderText");

            m_ChatInput = view.Q<TextField>("input");
            m_ChatInput.maxLength = AssistantMessageSizeConstraints.PromptLimit;
            m_ChatInput.multiline = true;
            m_ChatInput.verticalScrollerVisibility = ScrollerVisibility.Auto;
            m_ChatInput.selectAllOnFocus = false;
            m_ChatInput.selectAllOnMouseUp = false;
            m_ChatInput.RegisterCallback<ClickEvent>(_ => m_ChatInput.Focus());
            m_ChatInput.RegisterCallback<KeyUpEvent>(OnChatKeyUpEvent);
            // TrickleDown.TrickleDown is a workaround for registering KeyDownEvent type with Unity 6
            m_ChatInput.RegisterCallback<KeyDownEvent>(OnChatKeyDownEvent, TrickleDown.TrickleDown);
            m_ChatInput.RegisterValueChangedCallback(OnTextFieldValueChanged);
            m_ChatInput.RegisterCallback<FocusInEvent>(_ => SetTextFocused(true));
            m_ChatInput.RegisterCallback<FocusOutEvent>(_ => SetTextFocused(false));
            m_ChatInput.RegisterCallback<PointerLeaveEvent>(_ => m_ActionButton.RemoveFromClassList(k_ChatHoverClass));

            m_ActionRow = view.Q<VisualElement>("museTextFieldActionRow");

            m_Root.RegisterCallback<ClickEvent>(e =>
            {
                // Focus the input when clicking anywhere in the root, except on focusable elements
                // (focusable elements are interactive controls like buttons, textfields, dropdowns, etc.)
                if (e.target is VisualElement target && !target.focusable)
                {
                    m_ChatInput.Focus();
                }
            });

            m_TextElement = m_ChatInput.Q<TextElement>();
            m_TextElement.enableRichText = false;

            m_PlaceholderContent = view.Q<VisualElement>("placeholderContent");
            m_Placeholder = view.Q<Label>("placeholderText");

            UpdatePlaceholderText();

            m_ContextLimitWarning = view.Q<VisualElement>("contextLimitWarning");
            m_ImageSizeLimitWarning = view.Q<VisualElement>("imageSizeLimitWarning");
            m_PromptLimitWarning = view.Q<VisualElement>("promptLimitWarning");
            m_PromptLimitWarning.tooltip = k_PromptLimitWarningTooltip;
            m_PromptLimitWarning.SetDisplay(false);

            m_HistoryLabel = view.Q<Label>("historyLabel");
            m_HistoryLabel?.SetDisplay(false);

            var contextUsageContainer = view.Q<VisualElement>("contextUsageContainer");
            m_ContextUsageView = new ContextUsageView();
            m_ContextUsageView.Initialize(Context);
            contextUsageContainer.Add(m_ContextUsageView);

            Context.API.APIStateChanged += OnAPIStateChanged;

            ShowPlaceholder = true;
            HighlightFocus = true;

            // SetValueWithoutNotify avoids firing the change callback during construction,
            // which would otherwise schedule a spurious debounced undo commit on every window open.
            m_ChatInput.SetValueWithoutNotify(AssistantUISessionState.instance.Prompt ?? "");
            UpdatePlaceholderText();
            RefreshUI();

            // Apply granular status tracking to disableable elements, keeping ProviderSelector always enabled
            view.Q<VisualElement>(className: "mui-mtf-warning-area")?.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);
            view.Q<VisualElement>(className: "mui-mtf-input-root")?.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);
            view.Q<VisualElement>(className: "mui-left-actions")?.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);
            view.Q<Button>("actionButton")?.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider);

            // Subscribe to provider ready state changes for placeholder updates
            ProviderStateObserver.OnReadyStateChanged += OnProviderReadyStateChanged;
        }

        void OnAPIStateChanged()
        {
            RefreshUI();
        }

        void OnTextFieldValueChanged(ChangeEvent<string> evt)
        {
            OnChatValueChanged();

            // A genuine user edit of a loaded history entry exits history mode. Programmatic loads use
            // SetValueWithoutNotify and never reach here.
            m_HistoryNavigator.NotifyTextChanged(evt.newValue);
            UpdateHistoryLabel();

            if (CursorIndex <= 0 || CursorIndex > evt.newValue.Length || evt.newValue[CursorIndex - 1] != '@') return;
            if (CursorIndex == 1 || char.IsWhiteSpace(evt.newValue[CursorIndex - 2])) MentionTriggered?.Invoke();
        }

        void OnSubmit()
        {
            if (!IsPlanReviseMode)
            {
                if (Context.Blackboard.IsAPIWorking)
                {
                    CancelRequest?.Invoke();
                    return;
                }

                if (!Context.Blackboard.IsAPIReadyForPrompt) return;
            }

            // if button is disabled do not submit
            if (!m_ActionButton.enabledSelf)
            {
                return;
            }

            // Submitting ends any in-progress history navigation. The plan-revise path clears the field
            // with SetValueWithoutNotify (which bypasses the change-event exit), so do it explicitly here.
            m_HistoryNavigator.Exit();
            UpdateHistoryLabel();

            // Flush any pending debounced commit so a submit that races the 500 ms window
            // still sees the latest text and produces a matching undo entry.
            m_PromptUndoDebounceTask?.Pause();
            m_PromptUndoDebounceTask = null;
            CommitPromptToUndo();

            var prompt = Text;
            if (IsPlanReviseMode)
            {
                if (string.IsNullOrEmpty(prompt)) return;

                m_ChatInput.SetValueWithoutNotify("");
                CommitPromptToUndo();
                (Context.InteractionQueue.Current?.ContentView as PlanApprovalFooterContent)?.InlineElement.OnRevise(prompt);
                return;
            }

            // Keep the history cache current so a transient empty-model fallback (common while the agent
            // is responding) serves the just-sent prompt rather than a stale pre-submission snapshot.
            CacheSubmittedPrompt(prompt);

            SubmitRequest?.Invoke(prompt);
        }

        void CacheSubmittedPrompt(string prompt)
        {
            var trimmed = prompt?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return;

            if (Context?.Blackboard is not { } blackboard || !blackboard.ActiveConversationId.IsValid)
                return;

            var activeId = blackboard.ActiveConversationId;
            var updated = m_CachedHistory != null && m_CachedHistoryConversationId == activeId
                ? new List<string>(m_CachedHistory)
                : new List<string>();
            updated.Add(trimmed);

            m_CachedHistoryConversationId = activeId;
            m_CachedHistory = updated;
        }

        void SetTextFocused(bool state)
        {
            m_TextHasFocus = state;
            RefreshUI();
        }

        internal void RefreshUI()
        {
            RefreshPromptLimitWarning();

            m_ImageSizeLimitWarning.tooltip = k_ImageAttachmentSizeExceeded;
            m_ImageSizeLimitWarning.SetDisplay(m_ImageSizeExceeded);

            var actionButtonEnabled = IsPlanReviseMode
                ? !string.IsNullOrEmpty(Text) && !m_ImageSizeExceeded && m_ChatInput.enabledSelf
                : Context.Blackboard.IsAPIWorking ||
                  m_ChatInput.enabledSelf &&
                  !string.IsNullOrEmpty(Text) &&
                  Context.Blackboard.IsAPIReadyForPrompt &&
                  !m_ImageSizeExceeded;

            m_ActionButton.EnableInClassList(k_ChatActionEnabledClass, actionButtonEnabled);

            bool showPlaceholder;
            if (IsPlanReviseMode)
            {
                m_Placeholder.text = string.IsNullOrEmpty(Text) ? k_PlaceholderRevise : string.Empty;
                showPlaceholder = ShowPlaceholder;
            }
            else
            {
                showPlaceholder = ShowPlaceholder && !m_TextHasFocus && string.IsNullOrEmpty(Text);
            }
            m_PlaceholderContent.SetDisplay(showPlaceholder);

            m_Root.EnableInClassList(k_ChatFocusClass, m_TextHasFocus && m_HighlightFocus);

            m_SubmitButtonImage.SetIconClassName(Context.Blackboard.IsAPIWorking ? k_StopImage : k_SubmitImage);

            if (actionButtonEnabled)
            {
                m_ActionButton.tooltip = IsPlanReviseMode
                    ? k_ActionButtonToolTipRevise
                    : Context.Blackboard.IsAPIWorking ? k_ActionButtonToolTipStop : k_ActionButtonToolTipSend;
            }
            else
            {
                m_ActionButton.tooltip =
                    m_ImageSizeExceeded ? k_ImageAttachmentSizeExceeded : k_ActionButtonToolTipNoPrompt;
            }
            m_ActionButton.SetEnabled(actionButtonEnabled);
        }

        void OnChatValueChanged()
        {
            // Refresh the UI immediately (placeholder visibility, char count, submit-button enabled state).
            // The actual SessionState/Undo commit is debounced so that a burst of keystrokes
            // coalesces into a single undo entry.
            RefreshUI();
            SchedulePromptUndoCommit();
        }

        void SchedulePromptUndoCommit()
        {
            m_PromptUndoDebounceTask?.Pause();
            m_PromptUndoDebounceTask = schedule.Execute(CommitPromptToUndo).StartingIn(k_PromptUndoDebounceMs);
        }

        void CommitPromptToUndo()
        {
            if (m_ChatInput == null)
                return;

            var state = AssistantUISessionState.instance;
            var current = Text;
            if (current == state.Prompt)
                return;

            // RecordObject snapshots the current m_Prompt before we overwrite it, creating a discrete undo entry.
            Undo.RecordObject(state, "Edit Assistant Prompt");
            state.Prompt = current;
        }

        void OnPromptUndoRedo()
        {
            if (m_ChatInput == null)
                return;

            var restored = AssistantUISessionState.instance.Prompt ?? string.Empty;
            if (m_ChatInput.value != restored)
                m_ChatInput.SetValueWithoutNotify(restored);

            RefreshUI();
        }

        void RefreshPromptLimitWarning()
        {
            if (m_PromptLimitWarning == null)
                return;

            m_PromptLimitWarning.SetDisplay(Text.Length >= AssistantMessageSizeConstraints.PromptLimit);
        }

        void OnProviderChangedHandler(string oldProvider, string newProvider)
        {
            UpdateCommandSelectorVisibility();

            // Clear models and commands when switching providers
            m_ModelItems.Clear();
            m_CommandItems.Clear();
            m_SelectedModelId = null;
            RefreshCommandSelectorItems();

            OnProviderChanged?.Invoke(oldProvider, newProvider);
        }

        string GetProviderDisplayName(string providerId)
        {
            if (AssistantProviderFactory.IsUnityProvider(providerId))
            {
                var profiles = Context.AvailableUnityModelProfiles;
                if (profiles != null)
                {
                    foreach (var (id, displayName, _) in profiles)
                    {
                        if (id == providerId && !string.IsNullOrEmpty(displayName))
                            return displayName;
                    }
                }
                return providerId;
            }

            var match = AcpProvidersRegistry.Providers.FirstOrDefault(p => p.Id == providerId);
            if (match != null && !string.IsNullOrEmpty(match.DisplayName))
                return match.DisplayName;

            return providerId;
        }

        public void SetAvailableCommands(IReadOnlyList<(string name, string description)> commands)
        {
            m_CommandItems.Clear();
            if (commands != null)
            {
                m_CommandItems.AddRange(commands.Select(c => (c.name, "/" + c.name)));
            }

            RefreshCommandSelectorItems();
        }

        void OnChatKeyUpEvent(KeyUpEvent evt)
        {
            RefreshPromptLimitWarning();
        }

        /// <summary>
        /// macOS: Cmd+Z. Windows/Linux: Ctrl+Z.
        /// </summary>
        static bool IsUndoShortcut(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Z || evt.altKey || evt.shiftKey)
                return false;

            return Application.platform == RuntimePlatform.OSXEditor
                ? evt.commandKey && !evt.ctrlKey
                : evt.ctrlKey && !evt.commandKey;
        }

        /// <summary>
        /// macOS: Cmd+Shift+Z. Windows/Linux: Ctrl+Y or Ctrl+Shift+Z.
        /// </summary>
        static bool IsRedoShortcut(KeyDownEvent evt)
        {
            if (evt.altKey)
                return false;

            if (Application.platform == RuntimePlatform.OSXEditor)
                return evt.commandKey && !evt.ctrlKey && evt.shiftKey && evt.keyCode == KeyCode.Z;

            if (!evt.ctrlKey || evt.commandKey)
                return false;

            return (evt.keyCode == KeyCode.Y && !evt.shiftKey)
                || (evt.keyCode == KeyCode.Z && evt.shiftKey);
        }

        /// <summary>
        /// Flush pending debounced undo commits before Unity's global Undo processes the shortcut.
        /// Without this, fast typing followed by immediate undo would skip the uncommitted edits
        /// entirely (no undo entry exists yet) and revert to a much earlier state.
        /// This handler runs in TrickleDown phase, so it executes before Unity's Undo.PerformUndo —
        /// the flush creates the undo entry just in time for the undo system to revert it.
        /// </summary>
        internal void OnChatKeyDownEvent(KeyDownEvent evt)
        {
            // Runs in TrickleDown, before the engine's BubbleUp preview generation. Clamp the engine's
            // IME composition cursor so it can't overshoot the committed text and trigger the
            // GeneratePreviewString error while composing CJK (e.g. Korean) input.
            ImeCompositionErrorWorkaround.ClampCompositionCursor(m_ChatInput);

            if (m_PromptUndoDebounceTask != null && (IsUndoShortcut(evt) || IsRedoShortcut(evt)))
            {
                m_PromptUndoDebounceTask.Pause();
                m_PromptUndoDebounceTask = null;
                CommitPromptToUndo();
            }

            if (evt.keyCode == KeyCode.V)
            {
                bool isPasteShortcut;

                if (Application.platform == RuntimePlatform.OSXEditor)
                {
                    isPasteShortcut = evt.commandKey && !evt.altKey && !evt.shiftKey && !evt.ctrlKey;
                }
                else
                {
                    isPasteShortcut = evt.ctrlKey && !evt.altKey && !evt.shiftKey && !evt.commandKey;
                }

                if (isPasteShortcut)
                {
                    HandlePaste();
                    evt.StopPropagation();
#pragma warning disable CS0618 // Type or member is obsolete
                    evt.PreventDefault();
#pragma warning restore CS0618 // Type or member is obsolete
                    return;
                }
            }

            // UI Toolkit's built-in Escape "revert edit" both wipes freshly typed text and restores text
            // just deleted from a committed value. Consume it so Escape never mutates the prompt. Only
            // runs while the field has focus; overlays take their own focus and handle Escape first.
            if (evt.keyCode == KeyCode.Escape)
            {
                evt.StopImmediatePropagation();
#if !UNITY_2023_1_OR_NEWER
                evt.PreventDefault();
#endif
                return;
            }

            // Prompt history navigation (Up/Down without modifiers). Runs before the empty-field
            // swallow below so an empty field can still enter history.
            if (!evt.altKey && !evt.shiftKey && !evt.ctrlKey && !evt.commandKey
                && evt.keyCode is KeyCode.UpArrow or KeyCode.DownArrow
                && TryHandleHistoryNavigation(evt))
            {
                return;
            }

            // Newline handling: swallow stray Up/Down/Return on an empty field so the editor does not
            // move its selection/focus when there is nothing to navigate.
            if (string.IsNullOrEmpty(Text) &&
                evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.UpArrow or KeyCode.DownArrow)
            {
                evt.StopImmediatePropagation();
            }

            if (evt.character == '\n')
            {
                if (evt.shiftKey)
                {
                    string previousText = m_ChatInput.value;
                    var isAtEnd = CursorIndex == previousText.Length;

                    string newText = m_ChatInput.value.Insert(CursorIndex, "\n");
                    SetText(newText);

                    CursorIndex++;

                    if (isAtEnd)
                    {
                        m_ChatInput.selectIndex = CursorIndex + 1;
                    }
                    else
                    {
                        m_ChatInput.selectIndex = CursorIndex;
                    }

                    evt.StopImmediatePropagation();
                    return;
                }

                evt.StopPropagation();
#if !UNITY_2023_1_OR_NEWER
                evt.PreventDefault();
#endif
            }

            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (Text.Trim().Length == 0)
                        return;
                    break;

                default:
                    return;
            }

            if (evt.altKey || evt.shiftKey)
                return;

            bool useModifier = AssistantEditorPreferences.UseModifierKeyToSendPrompt;
            bool hasModifier = Application.platform == RuntimePlatform.OSXEditor ? evt.commandKey : evt.ctrlKey;
            if (hasModifier != useModifier)
                return;

            evt.StopPropagation();

            if (IsPlanReviseMode || (!Context.Blackboard.IsAPIWorking && Context.Blackboard.IsAPIReadyForPrompt))
                OnSubmit();
        }

        /// <summary>
        /// Handles Up/Down as prompt-history navigation. Returns true when the keystroke was consumed
        /// (history advanced); false to let the text field process the key normally (e.g. caret movement
        /// within a multiline entry, or no history available).
        /// </summary>
        bool TryHandleHistoryNavigation(KeyDownEvent evt)
        {
            var text = Text;
            var caret = Mathf.Clamp(CursorIndex, 0, text.Length);

            if (evt.keyCode == KeyCode.UpArrow)
            {
                // Let the field move the caret up until it reaches the first visual row; only then enter history.
                if (!IsCaretOnFirstVisualRow(text, caret))
                    return false;

                // Only enter history from an empty field; never interrupt an unsent draft.
                if (!m_HistoryNavigator.IsActive && !string.IsNullOrEmpty(text))
                    return false;

                // Already at the oldest entry: swallow Up so the caret stays put, instead of re-applying the
                // same text and jumping the caret back to the end.
                if (m_HistoryNavigator.IsActive && m_HistoryNavigator.Index == 0)
                {
                    ConsumeNavigation(evt);
                    return true;
                }

                // The history source is only consulted on activation; once active the navigator walks
                // its frozen snapshot, so skip the conversation scan/allocation while active.
                var history = m_HistoryNavigator.IsActive ? Array.Empty<string>() : GetPromptHistory();
                if (!m_HistoryNavigator.TryNavigateOlder(text, history, out var older))
                    return false;

                ApplyHistoryText(older);
                ConsumeNavigation(evt);
                return true;
            }

            // DownArrow: only meaningful while active, and only once the caret reaches the last visual row.
            if (!m_HistoryNavigator.IsActive || !IsCaretOnLastVisualRow(text, caret))
                return false;

            if (!m_HistoryNavigator.TryNavigateNewer(out var newer))
                return false;

            ApplyHistoryText(newer);
            ConsumeNavigation(evt);
            return true;
        }

        void ApplyHistoryText(string text)
        {
            // SetValueWithoutNotify so loading a history entry is not mistaken for a user edit
            // (which would immediately exit history via OnTextFieldValueChanged).
            m_ChatInput.SetValueWithoutNotify(text ?? string.Empty);

            var end = m_ChatInput.value.Length;
            m_ChatInput.cursorIndex = end;
            m_ChatInput.selectIndex = end;

            UpdateHistoryLabel();
            RefreshUI();
        }

        void UpdateHistoryLabel()
        {
            if (m_HistoryLabel == null)
                return;

            m_HistoryLabel.text = m_HistoryNavigator.Label;
            m_HistoryLabel.SetDisplay(m_HistoryNavigator.IsActive);
        }

        IReadOnlyList<string> GetPromptHistory()
        {
            // Never look up a conversation with a default/invalid id — risks serving another's prompts.
            if (Context?.Blackboard is not { } blackboard || !blackboard.ActiveConversationId.IsValid)
                return Array.Empty<string>();

            var activeId = blackboard.ActiveConversationId;

            // Prefer the rendered DisplayedConversation: the blackboard can hold an empty shell for a
            // loaded/restored conversation. Mirrors ChatElementCheckpoint.TryGetConversationAndMessageIndex.
            var displayed = Context.DisplayedConversation;
            var conversation = displayed != null && displayed.Id == activeId
                ? displayed
                : blackboard.GetConversation(activeId);

            // Lazily allocated only when there is at least one prompt to add (avoids an empty-list
            // allocation when the conversation is null or has no user messages).
            List<string> prompts = null;
            if (conversation != null)
            {
                foreach (var message in conversation.Messages)
                {
                    if (message.Role != MessageModelRole.User || message.Blocks == null)
                        continue;

                    // Allocation-free equivalent of Blocks.OfType<PromptBlockModel>().FirstOrDefault().
                    foreach (var block in message.Blocks)
                    {
                        if (block is PromptBlockModel prompt)
                        {
                            var content = prompt.Content?.Trim();
                            if (!string.IsNullOrEmpty(content))
                                (prompts ??= new List<string>()).Add(content);
                            break;
                        }
                    }
                }
            }

            if (prompts != null)
            {
                // Live read succeeded — refresh the single-slot cache (picks up newly submitted prompts).
                m_CachedHistoryConversationId = activeId;
                m_CachedHistory = prompts;
                return prompts;
            }

            // Live source empty (model rebuilt as a shell) — fall back to this conversation's cached history.
            if (m_CachedHistory != null && m_CachedHistoryConversationId == activeId)
            {
                InternalLog.Log($"Prompt history served from cache ({m_CachedHistory.Count} entries) for " +
                    $"conversation {activeId.Value}; the live conversation model had no messages.");
                return m_CachedHistory;
            }

            return Array.Empty<string>();
        }

        static void ConsumeNavigation(KeyDownEvent evt)
        {
            evt.StopImmediatePropagation();
#if !UNITY_2023_1_OR_NEWER
            evt.PreventDefault();
#endif
        }

        void InitializeSettingsPopup()
        {
            if (m_SettingsPopup != null)
                return;

            m_SettingsPopup = new SettingsPopup();
            m_SettingsPopup.Initialize(Context, autoShowControl: false);
            m_PopupRoot.Add(m_SettingsPopup);
        }

        void OnSettingsButtonClicked()
        {
            if (m_SettingsPopup.IsShown)
                HideSettingsPopup();
            else
                ShowSettingsPopup();
        }

        void OnModeChanged(string modeId)
        {
            UpdateSettingsPopupAutoRun();
            UpdatePlaceholderText();
        }

        void UpdatePlaceholderText()
        {
            if (m_Placeholder == null || IsPlanReviseMode) return;

            if (!ProviderStateObserver.IsUnityProvider)
            {
                UpdatePlaceholderFromReadyState();
                return;
            }
            m_Placeholder.text = GetPlaceholderForMode(Context.Blackboard.ActiveMode);
        }

        void OnProviderReadyStateChanged(ProviderStateObserver.ProviderReadyState _, string __)
        {
            UpdatePlaceholderFromReadyState();
        }

        void UpdatePlaceholderFromReadyState()
        {
            if (m_Placeholder == null || IsPlanReviseMode) return;

            if (ProviderStateObserver.IsUnityProvider)
            {
                m_Placeholder.text = GetPlaceholderForMode(Context.Blackboard.ActiveMode);
                return;
            }

            switch (ProviderStateObserver.ReadyState)
            {
                case ProviderStateObserver.ProviderReadyState.Initializing:
                    m_Placeholder.text = k_PlaceholderInitializing;
                    break;
                case ProviderStateObserver.ProviderReadyState.Error:
                    // Show simple message - full error details are shown in chat area
                    m_Placeholder.text = k_PlaceholderError;
                    break;
                case ProviderStateObserver.ProviderReadyState.Ready:
                    m_Placeholder.text = k_PlaceholderAgent;
                    break;
            }
        }

        static string GetPlaceholderForMode(AssistantMode mode)
        {
            return mode switch
            {
                AssistantMode.Plan => k_PlaceholderPlan,
                AssistantMode.Agent => k_PlaceholderAgent,
                _ => k_PlaceholderAsk
            };
        }

        void UpdateSettingsPopupAutoRun() => m_SettingsPopup?.SetAutoRunEnabled(Context.Blackboard.ActiveMode.SupportsAutoRun());

        /// <summary>
        /// Bind the mode provider to an IAssistantProvider.
        /// Call this when switching providers.
        /// </summary>
        public void BindModeProvider(IAssistantProvider provider)
        {
            m_ModeProvider.BindProvider(provider);
        }

        /// <summary>
        /// Set available models for the provider from session/initialized data.
        /// </summary>
        public void SetModels(IEnumerable<(string modelId, string name, string description)> models, string currentModelId)
        {
            m_ModelItems.Clear();

            if (models != null)
            {
                foreach (var (modelId, name, description) in models)
                {
                    m_ModelItems.Add((modelId, name));
                }
            }

            m_SelectedModelId = currentModelId;
            RefreshCommandSelectorItems();
        }

        /// <summary>
        /// Clear models when switching providers or resetting session.
        /// </summary>
        public void ClearModels()
        {
            m_ModelItems.Clear();
            m_SelectedModelId = null;
            RefreshCommandSelectorItems();
        }

        void ShowSettingsPopup()
        {
            using var listPoolHandle = ListPool<IToolPermissions.TemporaryPermission>.Get(out var permissions);
            Context.API.Provider.ToolPermissions.GetTemporaryPermissions(permissions);

            m_SettingsPopup.ShowWithPermissions(permissions);
            m_SettingPopupTracker = new PopupTracker(m_SettingsPopup, m_SettingsButton, new Vector2Int(0, 54), m_SettingsButton);
            m_SettingPopupTracker.Dismiss += HideSettingsPopup;
        }

        void HideSettingsPopup()
        {
            if (m_SettingPopupTracker == null)
                return;

            m_SettingPopupTracker.Dismiss -= HideSettingsPopup;
            m_SettingPopupTracker.Dispose();
            m_SettingPopupTracker = null;

            m_SettingsPopup.Hide();
        }
    }
}
