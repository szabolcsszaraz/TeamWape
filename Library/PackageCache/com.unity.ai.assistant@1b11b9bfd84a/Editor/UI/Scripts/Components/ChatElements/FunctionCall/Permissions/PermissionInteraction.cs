using System;
using System.Threading.Tasks;
using Unity.AI.Assistant.FunctionCalling;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class PermissionInteraction : IInteractionSource<PermissionUserAnswer>, IApprovalInteraction
    {
        public string Action { get; }
        public string Detail { get; }
        public string AllowLabel => null;
        public string DenyLabel => null;
        public bool ShowScope => true;

        public Func<PermissionUserAnswer?> TryAutoResolve { get; set; }

        // Optional factory for rich "View" content (e.g. a code block). When set, containers
        // use this instead of the plain-text fallback so the user can inspect what will run.
        public Func<VisualElement> ExpandedContentFactory { get; set; }

        public event Action<PermissionUserAnswer> OnCompleted;
        public TaskCompletionSource<PermissionUserAnswer> TaskCompletionSource { get; } = new();

        public PermissionInteraction(string action, string detail = null)
        {
            Action = action;
            Detail = detail;
        }

        public void Respond(PermissionUserAnswer answer) => Complete(answer);

        public void Complete(PermissionUserAnswer answer)
        {
            TaskCompletionSource.TrySetResult(answer);
            OnCompleted?.Invoke(answer);
        }

        public void CancelInteraction()
        {
            TaskCompletionSource.TrySetCanceled();
        }
    }
}
