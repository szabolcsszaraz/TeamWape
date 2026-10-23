using System;
using Unity.AI.Toolkit.Connect;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Toolkit.Accounts.Services.Core
{
    static class AccountLinks
    {
        /// <summary>
        /// Opens the dashboard credits page for the org that pays for AI usage. Subscribing, buying
        /// credits and assigning seats all happen there, and it enforces who is allowed to do what,
        /// so the editor sends every billing and access prompt to this one page rather than deciding
        /// anything itself.
        /// </summary>
        public static void ManageCredits()
        {
            var billedOrgId = Account.settings.BilledOrgId;
            Application.OpenURL(string.IsNullOrEmpty(billedOrgId)
                ? "https://cloud.unity.com/home/ai/points"
                : $"https://cloud.unity.com/home/organizations/{billedOrgId}/ai/points");
        }

        public static void ManageAccount()
        {
            var organizationId = UnityConnectProvider.organizationKey;
            if (string.IsNullOrEmpty(organizationId))
                Application.OpenURL("https://cloud.unity.com/home/organizations");
            else
                Application.OpenURL($"https://cloud.unity.com/home/organizations/{organizationId}/ai/settings");
        }

        public static void ViewBundles() => Application.OpenURL("https://unity.com/features/ai");

        public static void ViewDocumentation()
        {
            var versionParts = Application.unityVersion.Split('.');
            Application.OpenURL(versionParts.Length >= 2
                ? $"https://docs.unity3d.com/{versionParts[0]}.{versionParts[1]}/Documentation/Manual/ai-menu.html"
                : "https://docs.unity3d.com/6000.3/Documentation/Manual/ai-menu.html");
        }

        public static void OpenInPackageManager() =>
            UnityEditor.PackageManager.UI.Window.Open("com.unity.ai.assistant");

        public static void OpenAssistantInPackageManager() =>
            UnityEditor.PackageManager.UI.Window.Open("com.unity.ai.assistant");

        public static void OpenGeneratorsInPackageManager() =>
            UnityEditor.PackageManager.UI.Window.Open("com.unity.ai.assistant");

        public static void AssignSeats()
        {
            var organizationId = UnityConnectProvider.organizationKey;
            if (string.IsNullOrEmpty(organizationId))
                Application.OpenURL("https://cloud.unity.com/home/organizations");
            else
                Application.OpenURL($"https://cloud.unity.com/home/organizations/{organizationId}/commerce/subscriptions/");
        }
    }
}
