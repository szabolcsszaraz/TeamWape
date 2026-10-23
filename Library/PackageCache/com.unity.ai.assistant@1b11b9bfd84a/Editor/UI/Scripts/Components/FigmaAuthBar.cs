using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class FigmaAuthBar : ManagedTemplate
    {
        const string k_FigmaSettingsUrl = "https://www.figma.com/settings";

        public FigmaAuthBar() : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            var settingsLink = view.Q<Label>("openFigmaSettingsLink");
            settingsLink.AddManipulator(new Clickable(() => Application.OpenURL(k_FigmaSettingsUrl)));
        }
    }
}
