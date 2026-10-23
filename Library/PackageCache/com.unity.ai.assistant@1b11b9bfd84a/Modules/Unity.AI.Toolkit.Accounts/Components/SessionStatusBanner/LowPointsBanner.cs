using System;
using Unity.AI.Toolkit.Accounts.Services.Core;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    [UxmlElement]
    partial class LowPointsBanner : BasicBannerContent
    {
        const string k_Tooltip = "Only 10% of your organization's credits remain. Request a credits top-up to continue generating content without interruption.";
        internal const string k_DismissedPrefKey = "Unity.AI.Toolkit.LowPointsBannerDismissed";

        public static bool IsDismissed => EditorPrefs.GetBool(k_DismissedPrefKey, false);

        public static void ResetDismissed() => EditorPrefs.DeleteKey(k_DismissedPrefKey);

        public LowPointsBanner() : this(null) { }

        public LowPointsBanner(Action onDismiss)
            : base(
                "Only 10% of your org's credits remain. Credits refresh automatically at the start of the month.",
                "Manage credits", AccountLinks.ManageCredits,
                "Dismiss", () =>
                {
                    EditorPrefs.SetBool(k_DismissedPrefKey, true);
                    onDismiss?.Invoke();
                })
        {
            this.Query<VisualElement>().ForEach(ve => ve.tooltip = k_Tooltip);
        }
    }
}
