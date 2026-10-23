using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;

namespace Unity.AI.Assistant.Editor
{
    static class AssistantUISettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateAISettingsProvider()
        {
            var provider = new SettingsProvider("Project/AI/UI", SettingsScope.Project)
            {
                label = "UI",
                activateHandler = (searchContext, rootElement) =>
                {
                    var page = new AssistantUISettingsPage();
                    page.Initialize(null);
                    rootElement.Add(page);
                }
            };

            provider.hasSearchInterestHandler =
                SettingsProviderSearchHelper.CreateSearchHandler(provider.activateHandler, "AI", "UI");

            return provider;
        }
    }
}