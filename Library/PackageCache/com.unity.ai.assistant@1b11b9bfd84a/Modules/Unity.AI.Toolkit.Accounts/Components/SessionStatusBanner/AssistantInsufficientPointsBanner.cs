using Unity.AI.Toolkit.Accounts.Services.Core;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    /// <summary>
    /// The org has run out of credits. The trial note is unconditional because the editor cannot
    /// tell a trial from a paid subscription: both report the same subscription signal, so the note
    /// is phrased as a condition the reader can check rather than a claim about their plan.
    /// </summary>
    [UxmlElement]
    partial class AssistantInsufficientPointsBanner : BasicBannerContent
    {
        public AssistantInsufficientPointsBanner()
            : base(
                "No credits remaining. Credits refresh automatically at the start of the month, or you can purchase a top-up. " +
                "If you are on an AI trial, you will need a subscription before you can buy more credits.",
                "Manage credits", AccountLinks.ManageCredits) { }
    }
}
