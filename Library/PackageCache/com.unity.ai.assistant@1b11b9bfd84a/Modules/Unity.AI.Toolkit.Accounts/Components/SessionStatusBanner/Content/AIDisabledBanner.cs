using System;
using Unity.AI.Toolkit.Accounts.Services.Core;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    [UxmlElement]
    partial class AIDisabledBanner : BasicBannerContent
    {
        public AIDisabledBanner() : base(
            "AI features are not currently enabled for your organization. Please contact your manager or administrator for access. <link=manageaccount><color=#7BAEFA>Manage account</color></link>.",
            new LabelLink("manageaccount", AccountLinks.ManageAccount)
        ) { }
    }
}
