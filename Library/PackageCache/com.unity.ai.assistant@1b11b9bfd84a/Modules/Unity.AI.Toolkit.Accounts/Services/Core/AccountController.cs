using System;
using System.Threading.Tasks;
using Unity.AI.Toolkit.Accounts.Services.Core;
using Unity.AI.Toolkit.Accounts.Services.States;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Unity.AI.Toolkit.Accounts.Services
{
    static class AccountController
    {
#if !UNITY_6000_3_OR_NEWER
        // EditorPref written by the Unity 6.0 engine's AI dropdown popup when the user clicks
        // "Agree and install Unity AI". On Unity 6.3+ the engine uses AIDropdownConfig.termsAccepted instead.
        const string k_TermsAcceptedEditorPrefKey = "Unity.AI.TermsAccepted";
#endif

        [InitializeOnLoadMethod]
        static void Init()
        {
            VerifyTermsOfServiceAcceptance();
            RefreshAccountInformation();
            Account.network.OnChange += RefreshAccountInformation;
            Account.signIn.OnChange += RefreshAccountInformation;
            Account.cloudConnected.OnChange += RefreshAccountInformation;
        }

        static void VerifyTermsOfServiceAcceptance()
        {
            // Ensure settings have been fetched and that we know the current terms of service setting
            if (Account.settings.Value == null)
            {
                Account.settings.OnChange -= VerifyTermsOfServiceAcceptance;
                Account.settings.OnChange += VerifyTermsOfServiceAcceptance;
                return;
            }

            Account.settings.OnChange -= VerifyTermsOfServiceAcceptance;

            // The user may have already accepted the terms in the engine's AI dropdown install popup before
            // the package was installed. If so, propagate that acceptance to the server.
            if (DidUserAcceptTermsInEnginePopup() && !Account.legalAgreement.IsAgreed)
                _ = SetTermsOfService();
        }

        static bool DidUserAcceptTermsInEnginePopup()
        {
#if UNITY_6000_3_OR_NEWER
            return AIDropdownConfig.instance.termsAccepted;
#else
            return EditorPrefs.GetBool(k_TermsAcceptedEditorPrefKey, false);
#endif
        }

        public static async Task SetTermsOfService()
        {
            var settings = await AccountApi.SetTermsOfServiceAcceptance(true);
            Account.settings.Value = new(settings);
        }

        /// <summary>
        /// Ensure account information gets fetched at least once during an editor session.
        /// </summary>
        static void RefreshAccountInformation()
        {
            Account.apiAccessible.OnChange -= RefreshAccountInformation;
            if (!ApiAccessibleState.IsAccessible)
                Account.apiAccessible.OnChange += RefreshAccountInformation;

            if (Account.settings.Value == null)
                Account.settings.Refresh();
            if (Account.pointsBalance.Value == null)
                Account.pointsBalance.Refresh();
        }
    }
}
