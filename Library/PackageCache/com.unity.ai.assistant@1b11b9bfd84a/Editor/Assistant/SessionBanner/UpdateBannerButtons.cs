using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.Editor.SessionBanner
{
    class UpdateBannerButtons : VisualElement
    {
        const string k_UxmlPath = "Packages/com.unity.ai.assistant/Editor/Assistant/SessionBanner/UpdateBannerButtons.uxml";

        public UpdateBannerButtons(Action onUpdate)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_UxmlPath);
            tree.CloneTree(this);

            this.Q<Button>("update-button").clicked += () => onUpdate?.Invoke();
            this.Q<Button>("dismiss-button").clicked += PackageUpdateState.instance.Dismiss;
            this.Q<Button>("dont-ask-again-button").clicked += () =>
            {
                AssistantEditorPreferences.EnablePackageAutoUpdate = false;
                PackageUpdateState.instance.Dismiss();
            };
        }

        public static Button BuildAccentButton(string text, Action action)
        {
            var button = new Button(() => action?.Invoke()) { text = text };
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unity.ai.assistant/Editor/Assistant/SessionBanner/UpdateBannerButtons.uss");
            button.styleSheets.Add(styleSheet);
            button.AddToClassList("update-banner-accent-button");
            return button;
        }
    }
}
