using JetBrains.Annotations;
using Unity.Multiplayer.Center.Common;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Multiplayer.Center.Editor
{
    /// <summary>
    /// Provides a placeholder UI element, currently not included anywhere.
    /// Use it for pages under development only.
    /// </summary>
    [UsedImplicitly]
    class PlaceholderSection : OnboardingGUIProvider<PlaceholderSection>
    {
        public override (OnboardingSectionCategory, int)[] Categories => new[]
        {
            (OnboardingSectionCategory.Other, 0),
        };

        public override VisualElement CreateGUI()
        {
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unity.multiplayer.center/Editor/MultiplayerCenterWindow/UI/Recommendations.uss");

            var placeHodler =AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.multiplayer.center/Editor/MultiplayerCenterWindow/UI/PlaceHolder.uxml");

            var root = new VisualElement() {
                style = { flexGrow = 1,
                    alignContent = Align.Center, paddingTop = 100, paddingBottom = 100} };
            root.styleSheets.Add(style);

            placeHodler.CloneTree(root);

            root.hierarchy[0].style.alignSelf = Align.Center;

            root.AddToClassList("recommendation-box");

            return root;
        }
    }
}
