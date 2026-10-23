using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.ServerCompatibility;
using Unity.AI.Assistant.Editor;
using Unity.AI.Toolkit.Accounts.Components;
using Unity.AI.Toolkit.Accounts.Services;
using Unity.Relay.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.Editor.SessionBanner
{
    /// <summary>
    /// Session top banners.
    ///
    /// Acts as a state machine where `CurrentView` return the view that should currently be showed.
    /// </summary>
    [UxmlElement]
    partial class SessionBanner : AssistantSessionStatusBanner
    {
        AssistantInsufficientPointsBanner m_InsufficientPointsBanner;
        AcpSessionStatusBannerProvider m_AcpBannerProvider;
        UpdateAvailableBanner m_UpdateAvailableBanner;
        BasicBannerContent m_RelayStoppedBanner;
        BasicBannerContent m_RelayReconnectingBanner;
        bool m_IsAttached;
        bool m_RelayWasConnected;
        string m_LastFiredErrorType;

        // {0} = the model that hit capacity, {1} = the model we switched to (live profile display names).
        const string k_CapacitySwitchedMessageFormat =
            "{0} is experiencing high demand, so Assistant has switched to {1} to keep things running. Switch back later from the model picker.";
        const string k_CapacityReachedMessage =
            "Unity is experiencing high demand right now. Please try again in a few moments.";
        // {0} = the model we switched to (live profile display name).
        const string k_CapacityResendLabelFormat = "Resend on {0}";

        BasicBannerContent m_CapacityBanner;
        bool m_ShowCapacityBanner;
        string m_CapacityMessage;
        // When set, the capacity banner shows an opt-in "Resend on <model>" action. Null = info-only.
        Action m_CapacityResendAction;
        string m_CapacityResendLabel;

        /// <summary>Invoked when the capacity banner is dismissed (used to clear the picker highlight).</summary>
        public Action CapacityBannerDismissed;

        public Func<AssistantConversationId> GetActiveConversationId;

        /// <summary>Raised after the displayed banner changes (shown, hidden, or swapped).</summary>
        public event Action BannerChanged;

        /// <summary>The legal agreement banner blocks all usage until accepted, so it must overlay the window rather than be clipped.</summary>
        public bool IsBlockingBannerActive => m_Current is AIDisabledLegalBanner;

        public SessionBanner()
        {
            NotificationsState.instance.hideCompatibility = false;
            this.AddManipulator(new ServerCompatibilityChanges(Refresh));
            this.AddManipulator(new PointsBalanceChanges(Refresh));
            this.AddManipulator(new ProviderChanges(OnProviderChanged));
            this.AddManipulator(new PackageUpdateStateChanges(OnPackageUpdateStateChanged));
            AssistantEditorPreferences.ShowPackageUpdateBannerChanged += OnShowPackageUpdateBannerChanged;
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_IsAttached = true;

            // Subscribe directly to ready state changes to ensure banner updates
            ProviderStateObserver.OnReadyStateChanged += OnReadyStateChanged;
            RelayService.Instance.StateChanged += OnRelayStateChanged;
            m_RelayWasConnected = RelayService.Instance.IsConnected;

            if (!ProviderStateObserver.IsUnityProvider)
            {
                m_AcpBannerProvider ??= new AcpSessionStatusBannerProvider();
                m_AcpBannerProvider.Attach();
                m_AcpBannerProvider.OnChange += Refresh;
            }
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_IsAttached = false;

            ProviderStateObserver.OnReadyStateChanged -= OnReadyStateChanged;
            RelayService.Instance.StateChanged -= OnRelayStateChanged;

            if (m_AcpBannerProvider != null)
            {
                m_AcpBannerProvider.OnChange -= Refresh;
                m_AcpBannerProvider.Detach();
            }
        }

        void OnReadyStateChanged(ProviderStateObserver.ProviderReadyState state, string error)
        {
            Refresh();
        }

        void OnShowPackageUpdateBannerChanged(bool newValue)
        {
            Refresh();
        }

        void OnPackageUpdateStateChanged()
        {
            // Clear the cached banner so it gets recreated with updated state
            m_UpdateAvailableBanner = null;
            Refresh();
        }

        protected override void Refresh()
        {
            base.Refresh();

            BannerChanged?.Invoke();

            var errorType = m_Current == null ? null : GetErrorTypeForBanner(m_Current);
            if (errorType == null)
            {
                m_LastFiredErrorType = null;
                return;
            }

            if (errorType == m_LastFiredErrorType) return;

            m_LastFiredErrorType = errorType;
            AIAssistantAnalytics.ReportUITriggerLocalErrorDisplayedEvent(errorType, GetActiveConversationId?.Invoke() ?? default);
        }

        string GetErrorTypeForBanner(VisualElement banner)
        {
            if (banner == m_RelayStoppedBanner) return AIAssistantErrorType.k_RelayStopped;
            if (banner == m_RelayReconnectingBanner) return null;
            if (banner == m_UpdateAvailableBanner) return null;
            if (banner == m_CapacityBanner) return AIAssistantErrorType.k_NoCapacity;

            if (!ProviderStateObserver.IsUnityProvider
                && m_AcpBannerProvider != null
                && !string.IsNullOrEmpty(m_AcpBannerProvider.CurrentErrorType))
            {
                return m_AcpBannerProvider.CurrentErrorType;
            }

            return banner switch
            {
                ServerCompatibilityNotSupportedBanner => AIAssistantErrorType.k_ServerIncompatible,
                _ => null
            };
        }

        protected override VisualElement CurrentView()
        {
            // Relay is required for all providers (chat routes through relay)
            var relayStatus = RelayService.Instance.State.Status;
            if (relayStatus == RelayStatus.Stopped || relayStatus == RelayStatus.Failed)
            {
                EnableInClassList("empty", false);
                return m_RelayStoppedBanner ??= BuildRelayStoppedBanner();
            }
            if (relayStatus == RelayStatus.Connecting && m_RelayWasConnected)
            {
                EnableInClassList("empty", false);
                return m_RelayReconnectingBanner ??= BuildRelayReconnectingBanner();
            }

            if (m_ShowCapacityBanner)
            {
                EnableInClassList("empty", false);
                return m_CapacityBanner ??= BuildCapacityBanner();
            }

            var view = ProviderStateObserver.IsUnityProvider
                ? GetUnityProviderBanner()
                : GetAcpProviderBanner();

            // General banners apply regardless of provider
            view ??= GetGeneralBanner();

            EnableInClassList("empty", view == null);
            return view;
        }

        VisualElement GetUnityProviderBanner()
        {
            var view = base.CurrentView();

            if (view == null)
                return GetAssistantBanner();

            if (view is LowPointsBanner && Account.settings.CanSpendPoints && !Account.pointsBalance.CanAfford(AssistantConstants.ChatPreAuthorizePoints))
                return m_InsufficientPointsBanner ??= new AssistantInsufficientPointsBanner();

            return view;
        }

        VisualElement GetAcpProviderBanner()
        {
            return m_AcpBannerProvider?.GetCurrentView();
        }

        VisualElement GetAssistantBanner()
        {
            if (ServerCompatibility.ServerCompatibility.Status == ServerCompatibility.ServerCompatibility.CompatibilityStatus.Unsupported)
                return new ServerCompatibilityNotSupportedBanner();

            if (ServerCompatibility.ServerCompatibility.Status == ServerCompatibility.ServerCompatibility.CompatibilityStatus.Deprecated &&
                !NotificationsState.instance.hideCompatibility)
                return new ServerCompatibilityDeprecatedNotificationView(Dismiss);

            if (Account.settings.CanSpendPoints && !Account.pointsBalance.CanAfford(AssistantConstants.ChatPreAuthorizePoints))
                return m_InsufficientPointsBanner ??= new AssistantInsufficientPointsBanner();

            return null;
        }

        VisualElement GetGeneralBanner()
        {
            if (PackageUpdateState.instance.updateAvailable && !PackageUpdateState.instance.dismissed)
            {
                var current = PackageUpdateState.instance.currentVersion;
                var latest = PackageUpdateState.instance.latestVersion;
                return m_UpdateAvailableBanner ??= new UpdateAvailableBanner(current, latest,
                    () => {
                        if (!string.IsNullOrEmpty(latest))
                            _ = AssistantPackageAutoUpdater.UpdatePackage(latest);
                    });
            }

            return null;
        }

        void OnProviderChanged()
        {
            if (m_IsAttached)
            {
                // Detach old provider if it was attached
                if (m_AcpBannerProvider != null)
                {
                    m_AcpBannerProvider.OnChange -= Refresh;
                    m_AcpBannerProvider.Detach();
                }

                // Attach new provider if switching to ACP
                if (!ProviderStateObserver.IsUnityProvider)
                {
                    m_AcpBannerProvider ??= new AcpSessionStatusBannerProvider();
                    m_AcpBannerProvider.Attach();
                    m_AcpBannerProvider.OnChange += Refresh;
                }
            }

            Refresh();
        }

        void OnRelayStateChanged()
        {
            var status = RelayService.Instance.State.Status;
            if (status == RelayStatus.Running)
            {
                m_RelayWasConnected = true;
            }
            else if (status is RelayStatus.Stopped or RelayStatus.Failed)
            {
                var conversationId = GetActiveConversationId?.Invoke() ?? default;
                TracesUploader.UploadTraces(conversationId.Value, "reconnect-relay");
            }

            m_RelayStoppedBanner = null;
            m_RelayReconnectingBanner = null;
            Refresh();
        }

        BasicBannerContent BuildRelayStoppedBanner()
        {
            var message = "Chat is unavailable — relay connection was lost.\n<link=reconnect-relay><color=#7BAEFA>Try reconnecting</color></link>";
            var links = new List<LabelLink>
            {
                new LabelLink("reconnect-relay", () =>
                {
                    AIAssistantAnalytics.ReportUITriggerLocalRetryRelayConnectionEvent(GetActiveConversationId?.Invoke() ?? default);
                    _ = RelayService.Instance.StartAsync();
                })
            };
            return new BasicBannerContent(message, links);
        }

        static BasicBannerContent BuildRelayReconnectingBanner()
        {
            var message = "Chat is unavailable — reconnecting...";
            return new BasicBannerContent(message, links: null, loadingMessage: message);
        }

        /// <summary>
        /// Show the "switched models" banner after an automatic capacity fallback. The copy and the
        /// resend button use the live profile display names (<paramref name="fromModelName"/> /
        /// <paramref name="toModelName"/>). Offers an opt-in <paramref name="onResend"/> action so the
        /// user — not the client — decides whether to spend tokens re-running the prompt on the fallback.
        /// </summary>
        public void ShowCapacitySwitchedBanner(string fromModelName, string toModelName, Action onResend)
        {
            m_CapacityResendAction = onResend;
            m_CapacityResendLabel = string.Format(k_CapacityResendLabelFormat, toModelName);
            ShowCapacityBanner(string.Format(k_CapacitySwitchedMessageFormat, fromModelName, toModelName));
        }

        /// <summary>Show the "high demand, try again" banner when there is no lower fallback target.</summary>
        public void ShowCapacityReachedBanner()
        {
            m_CapacityResendAction = null;
            ShowCapacityBanner(k_CapacityReachedMessage);
        }

        void ShowCapacityBanner(string message)
        {
            m_ShowCapacityBanner = true;
            m_CapacityMessage = message;
            m_CapacityBanner = null; // rebuild with the current message/action
            Refresh();
        }

        BasicBannerContent BuildCapacityBanner()
        {
            // With a resend action (we switched to the default profile): a "Resend on <model>" button
            // plus a Dismiss link. Without one (already on the default profile — nothing to resend): an
            // informational banner with a Dismiss button.
            if (m_CapacityResendAction != null)
            {
                return new BasicBannerContent(
                    m_CapacityMessage, m_CapacityResendLabel, OnCapacityResendClicked, "Dismiss", DismissCapacityBanner);
            }

            return new BasicBannerContent(m_CapacityMessage, "Dismiss", DismissCapacityBanner, useInfoIcon: true);
        }

        void OnCapacityResendClicked()
        {
            var resend = m_CapacityResendAction;
            DismissCapacityBanner(); // hide the banner + clear the picker highlight
            resend?.Invoke();
        }

        /// <summary>Hide the capacity banner programmatically (e.g. the user changed the model).</summary>
        public void HideCapacityBanner()
        {
            if (!m_ShowCapacityBanner)
                return;

            m_ShowCapacityBanner = false;
            m_CapacityBanner = null;
            m_CapacityResendAction = null;
            m_CapacityResendLabel = null;
            Refresh();
        }

        void DismissCapacityBanner()
        {
            HideCapacityBanner();
            CapacityBannerDismissed?.Invoke();
        }

        void Dismiss()
        {
            Clear();
            Refresh();
        }
    }
}
