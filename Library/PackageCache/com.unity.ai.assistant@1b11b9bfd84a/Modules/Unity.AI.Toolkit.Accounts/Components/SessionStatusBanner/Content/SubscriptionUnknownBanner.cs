using Unity.AI.Toolkit.Accounts.Services.Core;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    /// <summary>
    /// The backend could not determine the org's subscription. Shown instead of guessing, because
    /// guessing "no subscription" would tell a paying customer their plan is missing. The dashboard
    /// has the authoritative answer, so we say what we know and send them there.
    /// </summary>
    [UxmlElement]
    partial class SubscriptionUnknownBanner : BasicBannerContent
    {
        const string k_Unknown =
            "We could not confirm your organization's AI subscription. You can check it on the dashboard.";

        const string k_UnknownAndNoSeat =
            "We could not confirm your organization's AI subscription, and your account does not have a seat. " +
            "Seats are assigned by an organization admin. You can check both on the dashboard.";

        public SubscriptionUnknownBanner() : this(true) { }

        public SubscriptionUnknownBanner(bool hasSeat)
            : base(hasSeat ? k_Unknown : k_UnknownAndNoSeat,
                "Manage AI access", AccountLinks.ManageCredits)
        {
            ExplainDashboardDestination();
        }
    }
}
