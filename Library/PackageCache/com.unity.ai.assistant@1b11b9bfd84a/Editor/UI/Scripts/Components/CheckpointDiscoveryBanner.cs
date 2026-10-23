using System.IO;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Checkpoint;
using Unity.AI.Assistant.Editor.Checkpoint.Events;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class CheckpointDiscoveryBanner : ManagedTemplate
    {
        // Shared key (see AssistantCheckpoints) so this banner and the debug menu can't drift apart.
        const string k_InitializationFailedSessionKey = AssistantCheckpoints.DiscoveryInitializationFailedSessionKey;
        const string k_BannerExpandedSessionKey = "CheckpointDiscoveryBannerExpanded";
        const string k_BannerProgressSessionKey = AssistantCheckpoints.DiscoveryProgressSessionKey;
        const string k_SyncStartTimeKey = AssistantCheckpoints.DiscoverySyncStartTimeSessionKey;
        const string k_EstimateSecondsKey = "CheckpointDiscoverySyncEstimateSeconds";
        const string k_ProjectSizeGbKey = "CheckpointDiscoveryProjectSizeGb";

        const float k_SecondsPerGb = 120f;     // rough heuristic: ~2 min per GB
        const float k_MinEstimateSeconds = 10f;
        const float k_ProgressCap = 0.95f;     // never show 100% from the time estimate; real completion snaps to 1
        const float k_FinalizeAnchor = 0.85f;  // phase progress at which the status reads "Finalizing setup..."
        const float k_LargeProjectGb = 1f;     // at/above this size the tooltip warns the sync will take longer

        VisualElement m_CollapsedRow;
        VisualElement m_ExpandedContent;
        VisualElement m_ActiveRow;
        VisualElement m_FailedRow;
        VisualElement m_DisabledRow;
        VisualElement m_ProgressFill;
        VisualElement m_ProgressTrack;
        Label m_ExpandedTimeEstimate;
        Label m_CollapsedTimeEstimate;

        bool m_Ticking;
        bool m_EstimateComputeStarted;

        // Tick runs on EditorApplication.update (100+/sec); these cache the last applied values
        // so we only touch the labels / progress width when something visibly changes.
        float m_LastProgress = -1f;
        int m_LastRemainingMinutes = -1;
        bool m_LastFinalizing;
        string m_SizeTooltip;
        Vector2 m_PointerWorld;
        Vector2 m_TooltipAnchor;
        bool m_TooltipCaptured;

        BannerState m_State;

        BaseEventSubscriptionTicket m_InitStartedTicket;
        BaseEventSubscriptionTicket m_InitProgressTicket;
        BaseEventSubscriptionTicket m_EnableStateChangedTicket;
        BaseEventSubscriptionTicket m_RefreshRequestedTicket;

        enum BannerState { Hidden, SyncingCollapsed, SyncingExpanded, Active, Failed, Disabled }

        /// <summary>True while the banner is visible in any state (i.e. not Hidden).</summary>
        public bool IsShowing => m_State != BannerState.Hidden;

        /// <summary>Raised when the banner's visibility (shown vs hidden) changes.</summary>
        public event System.Action ShownStateChanged;

        public CheckpointDiscoveryBanner()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_CollapsedRow = view.Q<VisualElement>("collapsedRow");
            m_ExpandedContent = view.Q<VisualElement>("expandedContent");
            m_ActiveRow = view.Q<VisualElement>("activeRow");
            m_FailedRow = view.Q<VisualElement>("failedRow");
            m_DisabledRow = view.Q<VisualElement>("disabledRow");
            m_ProgressFill = view.Q<VisualElement>("progressFill");
            m_ProgressTrack = view.Q<VisualElement>("progressTrack");
            m_ExpandedTimeEstimate = view.Q<Label>("expandedTimeEstimate");
            m_CollapsedTimeEstimate = view.Q<Label>("collapsedTimeEstimate");

            m_CollapsedRow.RegisterCallback<PointerUpEvent>(_ => OnCollapsedRowClicked());
            // The progress bar sits above the collapsed row, so route its clicks to the same toggle.
            m_ProgressTrack.RegisterCallback<PointerUpEvent>(_ => OnCollapsedRowClicked());

            // Size tooltip shows only over the progress bar and the time-remaining text (see OnSyncTooltip).
            RegisterSizeTooltip(m_ProgressTrack);
            RegisterSizeTooltip(m_ExpandedTimeEstimate);
            RegisterSizeTooltip(m_CollapsedTimeEstimate);

            view.SetupButton("disableButton", _ => OnDisableClicked());
            view.SetupButton("gotItButton", _ => OnGotItClicked());
            view.SetupButton("dismissButton", _ => OnDismissClicked());
            view.SetupButton("openSettingsButton", _ => SettingsService.OpenUserPreferences("Preferences/AI/Assistant"));
            view.SetupButton("failedSettingsButton", _ => SettingsService.OpenUserPreferences("Preferences/AI/Assistant"));
            view.SetupButton("failedDismissButton", _ => OnDismissClicked());
            view.SetupButton("disabledDismissButton", _ => OnDismissClicked());

            RegisterAttachEvents(OnAttachToPanel, OnDetachFromPanel);

            Show();
            ApplyState(BannerState.Hidden);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_InitStartedTicket = AssistantEvents.Subscribe<EventCheckpointInitializationStarted>(OnInitializationStarted);
            m_InitProgressTicket = AssistantEvents.Subscribe<EventCheckpointInitializationProgress>(OnInitializationProgress);
            m_EnableStateChangedTicket = AssistantEvents.Subscribe<EventCheckpointEnableStateChanged>(OnEnableStateChanged);
            m_RefreshRequestedTicket = AssistantEvents.Subscribe<EventCheckpointDiscoveryBannerRefreshRequested>(_ => Refresh());
            
            // Skip Refresh during active init. Events drive state. IsInitializingSession covers the domain-reload case.
            if (!AssistantCheckpoints.IsInitializing && !AssistantCheckpoints.IsInitializingSession)
                Refresh();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            StopTicking();
            AssistantEvents.Unsubscribe(ref m_InitStartedTicket);
            AssistantEvents.Unsubscribe(ref m_InitProgressTicket);
            AssistantEvents.Unsubscribe(ref m_EnableStateChangedTicket);
            AssistantEvents.Unsubscribe(ref m_RefreshRequestedTicket);
        }

        public void Refresh()
        {
            var dismissed = AssistantProjectPreferences.CheckpointDiscoveryBannerDismissed;
            if (dismissed)
            {
                ApplyState(BannerState.Hidden);
                return;
            }

            AssistantCheckpoints.EnsureInitializingAsync();

            if (AssistantCheckpoints.IsInitializing)
            {
                var syncState = SessionState.GetBool(k_BannerExpandedSessionKey, false)
                    ? BannerState.SyncingExpanded
                    : BannerState.SyncingCollapsed;
                ApplyState(syncState);
                return;
            }

            // Still initializing per session state (e.g. resumed after domain reload)
            if (AssistantCheckpoints.IsInitializingSession)
            {
                var syncState = SessionState.GetBool(k_BannerExpandedSessionKey, false)
                    ? BannerState.SyncingExpanded
                    : BannerState.SyncingCollapsed;
                ApplyState(syncState);
                return;
            }

            // Only show terminal states to users who went through the auto-init flow; otherwise everyone
            // with checkpoints already enabled would get an uninvited "setup successful" banner.
            if (!AssistantProjectPreferences.CheckpointDiscoveryFlowStarted)
            {
                ApplyState(BannerState.Hidden);
                return;
            }

            if (SessionState.GetBool(k_InitializationFailedSessionKey, false))
            {
                ApplyState(BannerState.Failed);
                return;
            }

            // Show Disabled only when the user turned checkpoints off (via the banner or Preferences)
            // and they remain off. Preferences re-enable clears UserDisabled, so this won't linger.
            if (!AssistantProjectPreferences.CheckpointEnabled && AssistantProjectPreferences.CheckpointDiscoveryUserDisabled)
            {
                ApplyState(BannerState.Disabled);
                return;
            }

            if (AssistantCheckpoints.IsInitialized)
            {
                ApplyState(BannerState.Active);
                return;
            }

            ApplyState(BannerState.Hidden);
        }

        void ApplyState(BannerState state)
        {
            var wasShowing = m_State != BannerState.Hidden;
            m_State = state;

            if (state != BannerState.SyncingCollapsed && state != BannerState.SyncingExpanded)
                SessionState.SetBool(k_BannerExpandedSessionKey, false);

            m_CollapsedRow.SetDisplay(state == BannerState.SyncingCollapsed);
            m_ExpandedContent.SetDisplay(state == BannerState.SyncingExpanded);
            m_ActiveRow.SetDisplay(state == BannerState.Active);
            m_FailedRow.SetDisplay(state == BannerState.Failed);
            m_DisabledRow.SetDisplay(state == BannerState.Disabled);

            var syncing = state == BannerState.SyncingCollapsed || state == BannerState.SyncingExpanded;
            m_ProgressTrack.SetDisplay(syncing);
            if (syncing)
            {
                // Anchor the sync clock if it isn't set (e.g. a debug reset erased it) so the bar starts
                // at 0 and the time estimate has a reference point right away. A resumed sync keeps its
                // original start time.
                if (SessionState.GetFloat(k_SyncStartTimeKey, -1f) < 0f)
                    SessionState.SetFloat(k_SyncStartTimeKey, (float)EditorApplication.timeSinceStartup);
                // Show the latest real anchor immediately, then let the tick smooth it via the time estimate.
                SetProgress(SessionState.GetFloat(k_BannerProgressSessionKey, 0f));
                StartTicking();
            }
            else
            {
                StopTicking();
            }

            if (state == BannerState.Hidden)
                Hide();
            else
                Show();

            // Let the host (AssistantView) re-arbitrate other empty-state banners when our visibility
            // flips - checkpoint discovery takes priority over the new-skills notification.
            if ((state != BannerState.Hidden) != wasShowing)
                ShownStateChanged?.Invoke();
        }

        void OnCollapsedRowClicked()
        {
            if (m_State == BannerState.SyncingCollapsed)
            {
                SessionState.SetBool(k_BannerExpandedSessionKey, true);
                ApplyState(BannerState.SyncingExpanded);
            }
            else if (m_State == BannerState.SyncingExpanded)
            {
                SessionState.SetBool(k_BannerExpandedSessionKey, false);
                ApplyState(BannerState.SyncingCollapsed);
            }
        }

        void OnDisableClicked()
        {
            // Record the opt-out but don't cancel an in-flight init (cancelling mid-git corrupts the
            // repo). The init finishes and its completion handler honors UserDisabled, leaving it off.
            AssistantProjectPreferences.CheckpointEnabled = false;
            AssistantProjectPreferences.CheckpointDiscoveryUserDisabled = true;
            ApplyState(BannerState.Disabled);

            // Notify the Preferences page (CheckpointEnabled was already false during sync, so its
            // setter fires nothing). UserDisabled is set above, so the handler resolves to Disabled.
            AssistantEvents.Send(new EventCheckpointEnableStateChanged(false));
        }

        void OnGotItClicked()
        {
            // "Got it" just folds the card back to the collapsed row; only the X dismisses the banner.
            SessionState.SetBool(k_BannerExpandedSessionKey, false);
            ApplyState(BannerState.SyncingCollapsed);
        }

        void OnDismissClicked()
        {
            AssistantProjectPreferences.CheckpointDiscoveryBannerDismissed = true;
            ApplyState(BannerState.Hidden);
        }

        void OnInitializationStarted(EventCheckpointInitializationStarted evt)
        {
            if (AssistantProjectPreferences.CheckpointDiscoveryBannerDismissed)
                return;

            SessionState.SetBool(k_InitializationFailedSessionKey, false);
            SessionState.SetFloat(k_BannerProgressSessionKey, 0f);
            // Reset the sync clock for this fresh init attempt (persisted so elapsed survives reloads).
            SessionState.SetFloat(k_SyncStartTimeKey, (float)EditorApplication.timeSinceStartup);
            SetProgress(0f);
            ApplyState(BannerState.SyncingCollapsed);
        }

        void OnInitializationProgress(EventCheckpointInitializationProgress evt)
        {
            // Real phase anchor - a monotonic floor for the bar; the tick smooths between anchors.
            var anchor = Mathf.Max(SessionState.GetFloat(k_BannerProgressSessionKey, 0f), evt.Progress);
            SessionState.SetFloat(k_BannerProgressSessionKey, anchor);
            if (!m_Ticking)
                SetProgress(anchor);
        }

        void StartTicking()
        {
            EnsureEstimateAsync();
            if (m_Ticking)
                return;
            // Reset the caches so the first tick of a fresh sync always writes.
            m_LastProgress = -1f;
            m_LastRemainingMinutes = -1;
            m_LastFinalizing = false;
            m_Ticking = true;
            EditorApplication.update += Tick;
        }

        void StopTicking()
        {
            if (!m_Ticking)
                return;
            m_Ticking = false;
            EditorApplication.update -= Tick;
        }

        // Blends the real phase anchor (floor) with a time-based estimate so the bar keeps moving during
        // the long commit phase, and updates the "~N min remaining" label and the syncing/finalizing title.
        void Tick()
        {
            var anchor = SessionState.GetFloat(k_BannerProgressSessionKey, 0f);
            var estimate = SessionState.GetFloat(k_EstimateSecondsKey, 0f);
            var start = SessionState.GetFloat(k_SyncStartTimeKey, -1f);

            var displayed = anchor;
            var finalizing = anchor >= k_FinalizeAnchor;
            var remainingSeconds = -1f;
            if (estimate > 0f && start >= 0f)
            {
                var elapsed = (float)EditorApplication.timeSinceStartup - start;
                displayed = Mathf.Max(Mathf.Clamp01(elapsed / estimate) * k_ProgressCap, anchor);
                remainingSeconds = estimate - elapsed;
                if (remainingSeconds <= 0f)
                    finalizing = true; // ran past the estimate - we're in the finalizing stretch
            }

            SetProgress(displayed);

            // The status slot (right of the title) shows the time remaining, then "Finalizing setup..."
            // for the last step. The title itself stays "Syncing Checkpoints...". Only rebuild and reassign
            // the text when the displayed minute (or the finalizing flag) actually changes - otherwise every
            // tick would allocate a new string and dirty the layout for no visible difference.
            var remainingMinutes = !finalizing && remainingSeconds > 0f ? Mathf.CeilToInt(remainingSeconds / 60f) : -1;
            if (finalizing != m_LastFinalizing || remainingMinutes != m_LastRemainingMinutes)
            {
                m_LastFinalizing = finalizing;
                m_LastRemainingMinutes = remainingMinutes;

                string status;
                if (finalizing)
                    status = "Finalizing setup...";
                else if (remainingMinutes > 0)
                    status = $"~{remainingMinutes} min remaining";
                else
                    status = string.Empty;

                SetTimeText(status);
            }
        }

        void SetProgress(float progress)
        {
            var clamped = progress < 0f ? 0f : (progress > 1f ? 1f : progress);
            if (m_ProgressFill == null)
                return;
            // Ignore sub-0.1% moves so we don't re-invalidate the layout on every editor tick.
            if (m_LastProgress >= 0f && Mathf.Abs(clamped - m_LastProgress) < 0.001f)
                return;
            m_LastProgress = clamped;
            m_ProgressFill.style.width = Length.Percent(clamped * 100f);
        }

        void SetTimeText(string text)
        {
            if (m_ExpandedTimeEstimate != null)
                m_ExpandedTimeEstimate.text = text;
            if (m_CollapsedTimeEstimate != null)
                m_CollapsedTimeEstimate.text = text;
        }

        // Estimates total sync time from project (Assets) size on a background thread, once per session.
        // Drives both the time-remaining label and the size tooltip.
        async void EnsureEstimateAsync()
        {
            if (m_EstimateComputeStarted || SessionState.GetFloat(k_EstimateSecondsKey, -1f) >= 0f)
            {
                ApplyTooltip();
                return;
            }
            m_EstimateComputeStarted = true;

            var assetsPath = Application.dataPath;
            long bytes = 0;
            try
            {
                bytes = await Task.Run(() => GetDirectorySize(assetsPath));
            }
            catch
            {
                // Size unknown - leave the estimate unset; the bar still tracks the real phase anchors.
            }

            MainThread.DispatchAndForget(() =>
            {
                var gb = bytes / (1024f * 1024f * 1024f);
                SessionState.SetFloat(k_ProjectSizeGbKey, gb);
                SessionState.SetFloat(k_EstimateSecondsKey, Mathf.Max(k_MinEstimateSeconds, gb * k_SecondsPerGb));
                ApplyTooltip();
            });
        }

        void ApplyTooltip()
        {
            var gb = SessionState.GetFloat(k_ProjectSizeGbKey, -1f);
            if (gb < 0f)
                return;

            var sizeText = gb >= 1f ? $"~{gb:0.#} GB" : $"~{Mathf.CeilToInt(gb * 1024f)} MB";
            m_SizeTooltip = gb >= k_LargeProjectGb
                ? $"Your project is {sizeText}, so this first sync will take more time."
                : $"Your project is {sizeText}, so setup should complete momentarily.";

            // Only the progress bar and the time-remaining text carry the tooltip; OnSyncTooltip
            // repositions it above the cursor.
            if (m_ProgressTrack != null)
                m_ProgressTrack.tooltip = m_SizeTooltip;
            if (m_ExpandedTimeEstimate != null)
                m_ExpandedTimeEstimate.tooltip = m_SizeTooltip;
            if (m_CollapsedTimeEstimate != null)
                m_CollapsedTimeEstimate.tooltip = m_SizeTooltip;
        }

        void RegisterSizeTooltip(VisualElement element)
        {
            if (element == null)
                return;

            element.RegisterCallback<PointerMoveEvent>(OnSyncPointerMove);
            element.RegisterCallback<PointerLeaveEvent>(OnSyncPointerLeave);
            element.RegisterCallback<TooltipEvent>(OnSyncTooltip);
        }

        void OnSyncPointerMove(PointerMoveEvent evt)
        {
            m_PointerWorld = new Vector2(evt.position.x, evt.position.y);
        }

        void OnSyncPointerLeave(PointerLeaveEvent evt)
        {
            // Next hover captures a fresh anchor position.
            m_TooltipCaptured = false;
        }

        void OnSyncTooltip(TooltipEvent evt)
        {
            if (string.IsNullOrEmpty(m_SizeTooltip))
                return;

            // Capture the cursor position the first time the tooltip shows for this hover and keep it
            // fixed, so the tooltip stays put instead of following the mouse.
            if (!m_TooltipCaptured)
            {
                m_TooltipAnchor = m_PointerWorld;
                m_TooltipCaptured = true;
            }

            evt.tooltip = m_SizeTooltip;
            // A tall anchor rect starting at the captured point leaves no room below, so UI Toolkit
            // places the tooltip above it.
            evt.rect = new Rect(m_TooltipAnchor.x, m_TooltipAnchor.y, 1f, 600f);
            evt.StopImmediatePropagation();
        }

        static long GetDirectorySize(string path)
        {
            long total = 0;
            try
            {
                var dir = new DirectoryInfo(path);
                foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try { total += file.Length; }
                    catch { /* skip unreadable files */ }
                }
            }
            catch
            {
                // ignore - return whatever we accumulated
            }
            return total;
        }

        void OnEnableStateChanged(EventCheckpointEnableStateChanged evt)
        {
            if (AssistantProjectPreferences.CheckpointDiscoveryBannerDismissed)
                return;

            if (evt.Enabled)
            {
                SessionState.SetBool(k_InitializationFailedSessionKey, false);
                ApplyState(BannerState.Active);
            }
            else if (AssistantProjectPreferences.CheckpointDiscoveryUserDisabled)
            {
                // Explicit opt-out (from this banner or from Preferences) - show Disabled, never a failure.
                SessionState.SetBool(k_InitializationFailedSessionKey, false);
                ApplyState(BannerState.Disabled);
            }
            else if (m_State == BannerState.SyncingCollapsed || m_State == BannerState.SyncingExpanded)
            {
                // Disabled while syncing without a user opt-out means initialization failed.
                SessionState.SetBool(k_InitializationFailedSessionKey, true);
                ApplyState(BannerState.Failed);
            }
            else if (m_State != BannerState.Hidden)
            {
                ApplyState(BannerState.Disabled);
            }
        }
    }
}
