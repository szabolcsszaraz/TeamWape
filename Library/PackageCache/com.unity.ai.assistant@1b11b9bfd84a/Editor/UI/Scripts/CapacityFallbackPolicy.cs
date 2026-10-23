using Unity.AI.Assistant.Editor.Acp;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    /// <summary>
    /// Decides how the client reacts to a NO_CAPACITY disconnect, based on the currently selected
    /// provider. Pure and side-effect free so it can be unit-tested without the Editor UI.
    /// </summary>
    static class CapacityFallbackPolicy
    {
        public enum Action
        {
            /// <summary>Not a Unity provider (or none selected) — capacity fallback does not apply.</summary>
            Ignore,

            /// <summary>
            /// On a non-default Unity provider profile: switch the picker to the default profile and
            /// offer the user an opt-in resend (we do not auto-spend their tokens on the fallback model).
            /// </summary>
            SwitchToDefaultAndOfferResend,

            /// <summary>Already on the default Unity provider profile: no fallback target, show an info banner only.</summary>
            ShowReachedBanner
        }

        public static Action Resolve(string currentProviderId)
        {
            // No selection or a non-Unity provider: capacity fallback does not apply.
            if (string.IsNullOrEmpty(currentProviderId) || !AssistantProviderFactory.IsUnityProvider(currentProviderId))
                return Action.Ignore;

            return currentProviderId == AssistantProviderFactory.DefaultProvider.ProfileId
                ? Action.ShowReachedBanner
                : Action.SwitchToDefaultAndOfferResend;
        }
    }
}
