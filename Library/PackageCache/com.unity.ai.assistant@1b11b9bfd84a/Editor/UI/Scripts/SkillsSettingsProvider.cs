using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;

namespace Unity.AI.Assistant.Editor
{
    static class SkillsSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateAISettingsProvider()
        {
            var provider = new SettingsProvider("Preferences/AI/Skills", SettingsScope.User)
            {
                label = "Skills",
                activateHandler = (searchContext, rootElement) =>
                {
                    var page = new AssistantSkillsSettingsView();
                    page.Initialize(null);
                    rootElement.Add(page);
                }
            };

            provider.hasSearchInterestHandler =
                SettingsProviderSearchHelper.CreateSearchHandler(provider.activateHandler, "AI", "Skills");

            return provider;
        }
    }
}
