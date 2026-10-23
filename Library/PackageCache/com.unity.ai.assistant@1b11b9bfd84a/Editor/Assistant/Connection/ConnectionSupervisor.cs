using System;
using Unity.AI.Assistant.Utils;
using Unity.AI.Toolkit.Accounts.Services.Core;
using UnityEditor;

namespace Unity.AI.Assistant.Editor.Connection
{
    /// <summary>
    /// Watches for events that imply the backend connection went stale — the process being
    /// suspended (system sleep, detected as a wall-clock gap) or the network dropping and
    /// returning — and triggers a recovery action (re-run the conversation/model refresh).
    /// Recovery is debounced so a burst of triggers collapses into a single recovery.
    /// Construct when the Assistant window opens; Dispose when it closes.
    /// </summary>
    sealed class ConnectionSupervisor : IDisposable
    {
        // A wall-clock gap this large between editor updates means the process was suspended
        // (system sleep) — well beyond a normal frame or even a long synchronous editor stall.
        const double k_WakeGapSeconds = 20.0;

        // Collapse a burst of triggers (e.g. a wake immediately followed by network-restored)
        // into a single recovery, while staying responsive.
        const double k_RecoverDebounceSeconds = 5.0;

        readonly Action m_Recover;
        readonly WakeDetector m_WakeDetector = new(k_WakeGapSeconds);
        double m_LastRecoverAt = double.MinValue;
        bool m_WasReachable;
        bool m_Disposed;

        public ConnectionSupervisor(Action recover)
        {
            m_Recover = recover ?? throw new ArgumentNullException(nameof(recover));
            m_WasReachable = NetworkAvailability.IsAvailable;
            EditorApplication.update += OnUpdate;
            NetworkAvailability.OnChanged += OnNetworkChanged;
        }

        void OnUpdate()
        {
            if (m_WakeDetector.Tick(EditorApplication.timeSinceStartup))
                TryRecover("wake");
        }

        void OnNetworkChanged()
        {
            var reachable = NetworkAvailability.IsAvailable;
            var restored = reachable && !m_WasReachable;
            m_WasReachable = reachable;
            if (restored)
                TryRecover("network-restored");
        }

        void TryRecover(string reason)
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - m_LastRecoverAt < k_RecoverDebounceSeconds)
                return;
            m_LastRecoverAt = now;

            // Internal-only trace (no-op in retail builds) to aid future debugging of post-sleep
            // and network-loss recovery.
            InternalLog.Log($"[Recover] ConnectionSupervisor recovering (reason={reason}, networkAvailable={NetworkAvailability.IsAvailable}) — re-running conversation/model refresh.");

            try
            {
                m_Recover();
            }
            catch (Exception e)
            {
                InternalLog.LogWarning($"[ConnectionSupervisor] recovery ({reason}) threw: {e}");
            }
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;
            m_Disposed = true;
            EditorApplication.update -= OnUpdate;
            NetworkAvailability.OnChanged -= OnNetworkChanged;
        }
    }
}
