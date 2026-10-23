using Unity.AI.Toolkit.Accounts.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    static class WhatsNewMenuLink
    {
        static void OpenUrl()
        {
            Application.OpenURL(AssistantUIConstants.WhatsNewUrl);
        }

        [InitializeOnLoadMethod]
        static void Init() => DropdownExtension.RegisterMainMenuExtension(container => container.Add(new AssistantToolbarMenuItem()), 0);

        class AssistantToolbarMenuItem : VisualElement
        {
            public AssistantToolbarMenuItem()
            {
                AddToClassList("label-button");
                AddToClassList("text-menu-item");
                AddToClassList("dropdown-item-with-margin");

                var label = new Label("See What's New");
                label.AddManipulator(new Clickable(OpenUrl));
                Add(label);
            }
        }
    }
}
