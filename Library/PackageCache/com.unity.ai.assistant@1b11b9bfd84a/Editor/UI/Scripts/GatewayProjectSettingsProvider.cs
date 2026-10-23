using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;

namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// Registers the AI Gateway settings panel in Project Settings > AI > Gateway.
    /// This panel allows configuration of per-provider working directories.
    /// </summary>
    static class GatewayProjectSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateGatewayProjectSettingsProvider()
        {
            var provider = new SettingsProvider("Project/AI/Gateway", SettingsScope.Project)
            {
                label = "Gateway",
                activateHandler = (searchContext, rootElement) =>
                {
                    var page = new GatewayProjectSettingsPage();
                    page.Initialize(null);
                    rootElement.Add(page);
                }
            };

            provider.hasSearchInterestHandler =
                SettingsProviderSearchHelper.CreateSearchHandler(provider.activateHandler, "AI", "Gateway");

            return provider;
        }
    }
}
