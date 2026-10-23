using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Integrations.Profiler.Editor
{
    class PickSessionInteraction : IInteractionSource<SessionProvider.ProfilerSessionInfo>, IApprovalInteraction
    {
        public string Action => "Select a Capture to Analyze";
        public string Detail { get; }
        public string AllowLabel => "Analyze";
        public string DenyLabel => "Cancel";
        public bool ShowScope => false;

        readonly List<SessionProvider.ProfilerSessionInfo> m_Sessions;

        public event Action<SessionProvider.ProfilerSessionInfo> OnCompleted;
        public TaskCompletionSource<SessionProvider.ProfilerSessionInfo> TaskCompletionSource { get; } = new();

        public PickSessionInteraction(List<SessionProvider.ProfilerSessionInfo> sessions)
        {
            m_Sessions = sessions;
            Detail = sessions.Count > 0 ? sessions[0].FileName : "";
        }

        public void Respond(PermissionUserAnswer answer)
        {
            if (answer == PermissionUserAnswer.AllowOnce || answer == PermissionUserAnswer.AllowAlways)
            {
                var session = m_Sessions.Count > 0 ? m_Sessions[0] : null;
                TaskCompletionSource.TrySetResult(session);
                OnCompleted?.Invoke(session);
            }
            else
            {
                CancelInteraction();
            }
        }

        public void CancelInteraction()
        {
            TaskCompletionSource.TrySetCanceled();
        }
    }
}
