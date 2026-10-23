using Unity.AI.Toolkit.Accounts.Services.Core;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    /// <summary>
    /// The org can use AI but this account has no seat. Who assigns a seat is stated as a fact
    /// rather than as an instruction to go and ask, so it stays true when the reader is the admin
    /// who can assign it themselves.
    /// </summary>
    [UxmlElement]
    partial class NoSeatBanner : BasicBannerContent
    {
        public NoSeatBanner() : base(
            "Using AI requires a seat, and your account does not have one. Seats are assigned by an organization admin.",
            "Manage AI access", AccountLinks.ManageCredits)
        {
            ExplainDashboardDestination();
        }
    }
}
