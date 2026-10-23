using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Unity.AI.Toolkit.Accounts.Services;
using Unity.AI.Toolkit.Accounts.Services.Core;
using Unity.AI.Toolkit.Accounts.Services.Data;
using Unity.AI.Toolkit.Connect;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace Unity.AI.Toolkit.Accounts.Services.States
{
    class SettingsState : IDisposable
    {
        internal readonly Signal<SettingsRecord> settings;
        internal readonly Signal<bool> regionAvailability;
        internal readonly Signal<bool> packagesSupported;
        ConnectionLimitSnapshot m_ConnectionLimits;

        public event Action OnChange;
        public SettingsRecord Value { get => settings.Value; internal set => settings.Value = value; }
        public bool RegionAvailable { get => regionAvailability.Value; set => regionAvailability.Value = value; }
        public bool PackagesSupported { get => packagesSupported.Value; set => packagesSupported.Value = value; }
        public void Refresh() => settings.Refresh();
        public Task RefreshSettings() => RefreshInternal();
        public bool AiAssistantEnabled => Value?.IsAiAssistantEnabled ?? false;
        public bool AiGeneratorsEnabled => Value?.IsAiGeneratorsEnabled ?? false;
        public bool IsDataSharingEnabled => Value?.IsDataSharingEnabled ?? false;
        public bool IsTermsOfServiceAccepted => Value?.IsTermsOfServiceAccepted ?? false;
        public bool IsMcpProEnabled => Value?.IsMcpProEnabled ?? false;
        public bool CanSpendPoints => Value?.CanSpendPoints ?? false;

        /// <summary>
        /// Whether the org holds a paid plan covering AI: true, false, or null when the backend
        /// could not determine it. Null is not a "no" — a paying customer must never be told their
        /// plan is missing because a lookup failed.
        /// </summary>
        public bool? SubscriptionState => Value?.HasAiSubscription;

        /// <summary>
        /// The org AI usage is billed to: the user's opt-in override when set, otherwise the org the
        /// project belongs to.
        /// </summary>
        public string BilledOrgId =>
            string.IsNullOrEmpty(Value?.OptedInBillingOrgId)
                ? UnityConnectProvider.organizationKey
                : Value.OptedInBillingOrgId;

        public ConnectionLimitSnapshot ConnectionLimits
        {
            get
            {
                if (m_ConnectionLimits == null)
                    m_ConnectionLimits = ResolveConnectionLimits();
                return m_ConnectionLimits;
            }
        }

        public int AllowedGatewayConnections => ConnectionLimits.AllowedGatewayConnections;
        public int AllowedMcpConnections => ConnectionLimits.AllowedMcpConnections;

        Timer m_NetworkPollTimer;
        Task m_PollingTask;
        List<string> m_LastActiveInterfaces;
        volatile bool m_RefreshNeeded;

        public SettingsState()
        {
            settings = new(AccountPersistence.SettingsProxy, () => _ = RefreshInternal(), OnSettingsChanged);
            regionAvailability = new Signal<bool>(AccountPersistence.RegionAvailabilityProxy, () => _ = RefreshInternal(), () => OnChange?.Invoke());
            packagesSupported = new Signal<bool>(AccountPersistence.PackagesSupportedProxy, () => _ = RefreshInternal(), () => OnChange?.Invoke());

            UpdateConnectionLimits();
            Refresh();

            // --- Start a timer that triggers a task-based network poll ---
            m_NetworkPollTimer = new Timer(PollNetworkOnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            EditorApplication.update += CheckForPendingRefresh;

            AIDropdownBridge.ConnectProjectStateChanged(Refresh);
            AIDropdownBridge.ConnectStateChanged(Refresh);
            AIDropdownBridge.UserStateChanged(Refresh);
            if (!Application.isBatchMode)
            {
                EditorApplication.focusChanged += OnEditorFocusChanged;
                EditorApplication.quitting += Dispose;
            }
        }

        // This method is called by the timer on a background thread.
        void PollNetworkOnTimerTick(object state)
        {
            // Re-entrancy guard: If the previous polling task is still running, skip this tick.
            if (m_PollingTask is { IsCompleted: false })
                return;

            // Start the new polling task and store it for the next check.
            m_PollingTask = PollNetworkAsync();
        }

        Task PollNetworkAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    var currentActiveInterfaces = GetActiveNetworkInterfaces();
                    if (m_LastActiveInterfaces == null)
                    {
                        m_LastActiveInterfaces = currentActiveInterfaces;
                        return;
                    }

                    if (!currentActiveInterfaces.SequenceEqual(m_LastActiveInterfaces))
                    {
                        m_LastActiveInterfaces = currentActiveInterfaces;
                        m_RefreshNeeded = true; // Signal the main thread.
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Network polling encountered an error: {ex.Message}");
                }
            });
        }

        void CheckForPendingRefresh()
        {
            if (!m_RefreshNeeded)
                return;

            m_RefreshNeeded = false;
            RefreshSettings();
        }

        List<string> GetActiveNetworkInterfaces()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .Select(n => n.Id)
                    .OrderBy(name => name)
                    .ToList();
            }
            catch (Exception)
            {
                return m_LastActiveInterfaces ?? new List<string>();
            }
        }

        void OnEditorFocusChanged(bool focused)
        {
            if (focused)
                RefreshSettings();
        }

        async Task RefreshInternal()
        {
            RegionAvailable = true; // Assume region is available by default, change to false if there is an error in AccountApi.GetSettings()
            // PackagesSupported is NOT reset here — it is only set to false by AccountApi
            // when the server returns ApiNoLongerSupported, and should stay false until a
            // successful response proves otherwise.

            var result = await AccountApi.GetSettings();
            if (result != null)
                PackagesSupported = true;
            var nextValue = result == null ? null : new SettingsRecord(result);
            bool settingsChanged = !EqualityComparer<SettingsRecord>.Default.Equals(Value, nextValue);

            Value = nextValue;

            if (!settingsChanged && UpdateConnectionLimits())
                OnChange?.Invoke();
        }

        void OnSettingsChanged()
        {
            UpdateConnectionLimits();
            OnChange?.Invoke();
        }

        bool UpdateConnectionLimits()
        {
            var nextConnectionLimits = ResolveConnectionLimits();
            if (Equals(m_ConnectionLimits, nextConnectionLimits))
                return false;

            m_ConnectionLimits = nextConnectionLimits;
            return true;
        }

        ConnectionLimitSnapshot ResolveConnectionLimits() =>
            ConnectionLimitResolver.Resolve(Value, EditorConnectionLimitProvider.Instance);

        public void Dispose()
        {
            EditorApplication.focusChanged -= OnEditorFocusChanged;
            EditorApplication.quitting -= Dispose;
            m_NetworkPollTimer?.Dispose();
            m_NetworkPollTimer = null;
        }
    }
}
