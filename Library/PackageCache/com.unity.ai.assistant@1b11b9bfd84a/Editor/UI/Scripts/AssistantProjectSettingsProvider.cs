using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;

namespace Unity.AI.Assistant.Editor
{
    static class AssistantProjectSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateAISettingsProvider()
        {
            var provider = new SettingsProvider("Project/AI/Assistant", SettingsScope.Project)
            {
                label = "Assistant",
                activateHandler = (searchContext, rootElement) =>
                {
                    var page = new AssistantProjectSettingsPage();
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
