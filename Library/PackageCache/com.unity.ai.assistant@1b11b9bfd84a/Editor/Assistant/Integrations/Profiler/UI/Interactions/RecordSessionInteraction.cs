using System;
using System.Threading.Tasks;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;

namespace Unity.AI.Assistant.Integrations.Profiler.Editor
{
    class RecordSessionInteraction : IInteractionSource<SessionProvider.ProfilerSessionInfo>, IApprovalInteraction
    {
        public string Action => "Open Profiler";
        public string Detail => "No existing profiler captures. Record a new capture and prompt again.";
        public string AllowLabel => "Open Profiler";
        public string DenyLabel => "Cancel";
        public bool ShowScope => false;

        public event Action<SessionProvider.ProfilerSessionInfo> OnCompleted;
        public TaskCompletionSource<SessionProvider.ProfilerSessionInfo> TaskCompletionSource { get; } = new();

        public void Respond(PermissionUserAnswer answer)
        {
            if (answer == PermissionUserAnswer.AllowOnce || answer == PermissionUserAnswer.AllowAlways)
            {
                EditorWindow.GetWindow<ProfilerWindow>().Show();
            }

            // Always complete so the caller receives the "no sessions found" error
            // rather than a cancellation, regardless of user choice.
            TaskCompletionSource.TrySetResult(null);
            OnCompleted?.Invoke(null);
        }

        public void CancelInteraction()
        {
            TaskCompletionSource.TrySetCanceled();
        }
    }
}
