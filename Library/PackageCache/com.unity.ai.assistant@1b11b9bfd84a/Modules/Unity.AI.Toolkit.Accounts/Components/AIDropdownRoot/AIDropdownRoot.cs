using System;
using Unity.AI.Toolkit.Accounts.Services;
using Unity.AI.Toolkit.Accounts.Services.Data;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    [UxmlElement]
    partial class AIDropdownRoot : VisualElement
    {
        VisualElement m_Content;
        VisualElement m_Current;

        AIDropdown m_Dropdown;
        SessionStatusBanner m_Banner;
        LegalAgreement m_LegalAgreement;
        RegionBanner m_RegionBanner;
        PackagesUnsupportedBanner m_UnsupportedBanner;
        // Cached per seat state: these banners word themselves differently depending on it, so one
        // cached instance would keep showing the old wording after a seat is assigned.
        NoSubscriptionBanner m_NoSubscriptionBanner;
        NoSubscriptionBanner m_NoSubscriptionAndNoSeatBanner;
        SubscriptionUnknownBanner m_SubscriptionUnknownBanner;
        SubscriptionUnknownBanner m_SubscriptionUnknownAndNoSeatBanner;
        NoSeatBanner m_NoSeatBanner;

        public AIDropdownRoot()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.ai.assistant/Modules/Unity.AI.Toolkit.Accounts/Components/AIDropdownRoot/AIDropdownRoot.uxml");
            tree.CloneTree(this);

            if (!EditorGUIUtility.isProSkin)
                AddToClassList("light");

            m_Content = this.Q<VisualElement>("content");

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Refresh();
                Account.session.OnChange += Refresh;
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                Account.session.OnChange -= Refresh;
            });
        }

        void Refresh()
        {
            VisualElement current;
            if (Account.signIn.Value == SignInStatus.NotReady ||
                Account.signIn.IsSignedOut ||
                Account.cloudConnected.Value == ProjectStatus.NotReady ||
                Account.cloudConnected.Value == ProjectStatus.NotConnected)
                current = m_Banner ??= new();
            else if (!Account.settings.RegionAvailable)
                current = m_RegionBanner ??= new();
            else if (!Account.settings.PackagesSupported)
                current = m_UnsupportedBanner ??= new();
            else if (Account.settings.Value == null)
                current = m_Banner ??= new();
            else if (!Account.legalAgreement.Value)
                current = m_LegalAgreement ??= new();
            // Same access rules and destination as the session banner, so the toolbar cannot tell a
            // different story from the chat window. Resolved only once settings have loaded, since
            // before that every entitlement reads as absent. These replace the dropdown outright;
            // credit states are left to the SessionStatusBanner embedded inside the dropdown, which
            // is reached once access is fine and renders them there.
            else if (Account.settings.SubscriptionState == false)
                current = Account.settings.CanSpendPoints
                    ? m_NoSubscriptionBanner ??= new NoSubscriptionBanner(hasSeat: true)
                    : m_NoSubscriptionAndNoSeatBanner ??= new NoSubscriptionBanner(hasSeat: false);
            else if (Account.settings.SubscriptionState == null)
                current = Account.settings.CanSpendPoints
                    ? m_SubscriptionUnknownBanner ??= new SubscriptionUnknownBanner(hasSeat: true)
                    : m_SubscriptionUnknownAndNoSeatBanner ??= new SubscriptionUnknownBanner(hasSeat: false);
            else if (!Account.settings.CanSpendPoints)
                current = m_NoSeatBanner ??= new();
            else
                current = m_Dropdown ??= new();

            if (m_Current != current)
            {
                m_Current = current;
                m_Content.Clear();
                if (m_Current != null)
                    m_Content.Add(m_Current);
            }
        }
    }
}
