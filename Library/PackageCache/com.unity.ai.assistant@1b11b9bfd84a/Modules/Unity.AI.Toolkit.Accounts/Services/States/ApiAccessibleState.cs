using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Toolkit.Accounts.Services.States
{
    class ApiAccessibleState
    {
        static bool s_HasLoggedWarning;

        // How often, while waiting, to proactively re-derive the gating account
        // states so IsAccessible can flip without the editor regaining focus.
        const double k_RefreshIntervalSeconds = 1.0;

        public static bool IsAccessible => Account.network.IsAvailable && Account.signIn.IsSignedIn && Account.cloudConnected.IsConnected;

        /// <summary>
        /// Asynchronously waits for the API to become accessible. This method is safe to call
        /// even when the Unity Editor is not in focus. It uses an EditorApplication.update
        /// subscription to poll for the required state, which keeps the async context alive.
        /// </summary>
        /// <returns>A Task that completes when the API is accessible or a timeout is reached.</returns>
        public static async Task<bool> WaitForCloudProjectSettings()
        {
            if (Application.isBatchMode)
                return false;

            if (IsAccessible)
                return true;

            // While unfocused, Unity Connect callbacks may not fire, so the gating
            // states (signIn / cloudConnected) never re-derive and IsAccessible stays
            // frozen. Proactively refresh them on a throttled cadence while we wait.
            var poller = new ThrottledAccessibilityPoller(
                () => IsAccessible,
                RefreshAccessibilityInputs,
                k_RefreshIntervalSeconds);

            var result = await EditorTask.WaitForCondition(
                () => poller.Poll(EditorApplication.timeSinceStartup),
                TimeSpan.FromSeconds(30));

            if (!result && !s_HasLoggedWarning)
            {
                Debug.LogWarning("Account API did not become accessible within 30 seconds. This may be due to network issues or editor focus.");
                s_HasLoggedWarning = true;
            }
            else if (result)
            {
                s_HasLoggedWarning = false;
            }

            return result;
        }

        // Re-reads cached Unity Connect state for the three gating signals. These
        // are local, synchronous, and require no editor focus. No focus is forced.
        static void RefreshAccessibilityInputs()
        {
            Account.network.Refresh();
            Account.signIn.Refresh();
            Account.cloudConnected.Refresh();
        }

        public event Action OnChange
        {
            add
            {
                Account.network.OnChange += value;
                Account.signIn.OnChange += value;
                Account.cloudConnected.OnChange += value;
            }
            remove
            {
                Account.network.OnChange -= value;
                Account.signIn.OnChange -= value;
                Account.cloudConnected.OnChange -= value;
            }
        }
    }
}
