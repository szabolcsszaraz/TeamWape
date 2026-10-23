using JetBrains.Annotations;
using Unity.Multiplayer.Center.Common;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Multiplayer.Center.Editor
{
    /// <summary>
    /// Provides shortcuts in the <see cref="OnboardingSectionCategory.GettingStarted"/> section
    /// for users to access the community links, such as discord and discussions.
    /// </summary>
    [UsedImplicitly]
    class CommunityHandlesSection : OnboardingGUIProvider<CommunityHandlesSection>
    {
        public override (OnboardingSectionCategory, int)[] Categories => new[]
        {
            (OnboardingSectionCategory.GettingStarted, 160),
            (OnboardingSectionCategory.Other, -100)
        };


        public override VisualElement CreateGUI()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.unity.multiplayer.center/Editor/MultiplayerCenterWindow/UI/Community.uxml");

            var root =new VisualElement();

            asset.CloneTree(root);

            return root;

        }

    }
}
