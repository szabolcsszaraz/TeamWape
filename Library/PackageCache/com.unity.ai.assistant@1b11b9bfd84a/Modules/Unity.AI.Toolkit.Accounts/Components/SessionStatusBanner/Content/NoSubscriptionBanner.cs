using Unity.AI.Toolkit.Accounts.Services.Core;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    /// <summary>
    /// The org has no plan covering AI. When the user also has no seat, both facts are stated at
    /// once so they do not sort out a subscription only to come back and find they still cannot
    /// use AI.
    /// </summary>
    [UxmlElement]
    partial class NoSubscriptionBanner : BasicBannerContent
    {
        const string k_NoSubscription =
            "Your organization does not have a subscription that includes AI.";

        const string k_NoSubscriptionAndNoSeat =
            "Your organization does not have a subscription that includes AI, and your account does not have a seat. " +
            "Both are required to use AI, and seats are assigned by an organization admin.";

        public NoSubscriptionBanner() : this(true) { }

        public NoSubscriptionBanner(bool hasSeat)
            : base(hasSeat ? k_NoSubscription : k_NoSubscriptionAndNoSeat,
                "Manage subscription", AccountLinks.ManageCredits)
        {
            ExplainDashboardDestination();
        }
    }
}
