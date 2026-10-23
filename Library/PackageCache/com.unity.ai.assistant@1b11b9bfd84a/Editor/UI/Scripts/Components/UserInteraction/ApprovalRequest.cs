using System;
using Unity.AI.Assistant.FunctionCalling;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction
{
    /// <summary>
    /// Parameters for enqueuing an approval interaction in a tool UI container.
    /// Groups the approval's labels, callbacks, and optional rich "View" content so
    /// containers don't have to thread many positional arguments.
    /// </summary>
    class ApprovalRequest
    {
        public string Action { get; set; }
        public string Detail { get; set; }
        public string AllowLabel { get; set; }
        public string DenyLabel { get; set; }
        public bool ShowScope { get; set; } = true;

        public Action<PermissionUserAnswer> OnRespond { get; set; }
        public Action OnCancel { get; set; }

        /// <summary>
        /// Optional factory for rich "View" content (e.g. the code to be executed). When null,
        /// containers fall back to a plain-text view built from <see cref="Action"/> and
        /// <see cref="Detail"/>.
        /// </summary>
        public Func<VisualElement> ExpandedContentFactory { get; set; }
    }
}
