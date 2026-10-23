using System;
using System.Threading.Tasks;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Tools
{
    class UnsafeCommandApprovalInteraction : IInteractionSource<bool>, IApprovalInteraction
    {
        public string Action { get; }
        public string Detail { get; }
        public string AllowLabel => "Proceed";
        public string DenyLabel => "Cancel";
        public bool ShowScope => false;

        public event Action<bool> OnCompleted;
        public TaskCompletionSource<bool> TaskCompletionSource { get; } = new();

        public UnsafeCommandApprovalInteraction()
        {
            Action = "Run unsafe command";
            Detail = "This command performs non-revertable actions.";
        }

        public void Respond(PermissionUserAnswer answer)
        {
            var approved = answer == PermissionUserAnswer.AllowOnce
                || answer == PermissionUserAnswer.AllowAlways;
            Complete(approved);
        }

        public void Complete(bool approved)
        {
            TaskCompletionSource.TrySetResult(approved);
            OnCompleted?.Invoke(approved);
        }

        public void CancelInteraction()
        {
            TaskCompletionSource.TrySetCanceled();
        }
    }
}
