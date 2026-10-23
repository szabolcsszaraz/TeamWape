using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    internal static class AssistantSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateAISettingsProvider()
        {
            var provider = new SettingsProvider("Preferences/AI/Assistant", SettingsScope.User)
            {
                label = "Assistant",
                activateHandler = (searchContext, rootElement) =>
                {
                    var page = new AssistantSettingsPage();
                    page.Initialize(null);

                    rootElement.Add(page);
                }
            };

            provider.hasSearchInterestHandler =
                SettingsProviderSearchHelper.CreateSearchHandler(provider.activateHandler, "AI", "Assistant");

            return provider;
        }
    }
}
