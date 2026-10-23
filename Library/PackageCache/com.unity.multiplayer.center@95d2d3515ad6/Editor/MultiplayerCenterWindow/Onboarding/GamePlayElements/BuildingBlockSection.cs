using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Multiplayer.Center.Common;
using Unity.Multiplayer.Center.Editor.Models;
using Unity.Properties;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Multiplayer.Center.Editor.Onboarding
{
    /// <summary>
    /// Provides a list of UI elements in the <see cref="OnboardingSectionCategory.Intro"/> section
    /// to show recommended building blocks for the selected <see cref="GameGenre"/>.
    /// </summary>
    [UsedImplicitly]
    class BuildingBlockSection : OnboardingGUIProvider<BuildingBlockSection>, IDataSourceViewHashProvider
    {
        const string k_AssetPath =
            "Packages/com.unity.multiplayer.center/Editor/MultiplayerCenterWindow/BuildingBlockList.asset";

        long m_Version;

        readonly Dictionary<GameGenre, int> m_GenreToIndexMap = new();
        GenreAssociatedBuildingBlocks m_BlocksPerGenre;
        VisualElement m_Root = new VisualElement();
        [CreateProperty]
        public BuildingBlockList Blocks
        {
            get
            {
                var genre = MultiplayerCenterSettings.instance.SelectedGameGenre;
                if (m_GenreToIndexMap.TryGetValue(genre, out var index) && index < m_BlocksPerGenre.Descriptions.Count)
                    return m_BlocksPerGenre.Descriptions[index];
                return default;
            }
        }

        public override (OnboardingSectionCategory, int)[] Categories =>
            new[] { (OnboardingSectionCategory.Intro, 0) };

        public override VisualElement CreateGUI()
        {
            m_BlocksPerGenre = AssetDatabase
                .LoadAssetAtPath<GenreAssociatedBuildingBlocks>(k_AssetPath);

            m_GenreToIndexMap.Clear();
            for (var i = 0; i < m_BlocksPerGenre.Descriptions.Count; i++)
            {
                m_GenreToIndexMap[m_BlocksPerGenre.Descriptions[i].m_Genre] = i;
            }

            m_Root.SetBinding(nameof(m_Root.dataSource),
                new DataBinding()
                {
                    bindingMode = BindingMode.ToTarget,
                    dataSource = this,
                    dataSourcePath = PropertyPath.FromName(nameof(Blocks))
                });

            var title = new Label(L10n.Tr("Add Gameplay Elements"));
            title.AddToClassList(StyleClasses.SectionHeadline);
            m_Root.Add(title);

            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.unity.multiplayer.center/Editor/MultiplayerCenterWindow/UI/BuildingBlockTemplate.uxml");

            for (var i = 0; i < Blocks.Length; i++)
            {
                var item = template.CloneTree();
                item.dataSourcePath = PropertyPath.AppendIndex(PropertyPath.FromName("m_" + nameof(Blocks)), i);

                m_Root.Add(item);
            }

            return m_Root;
        }

        public long GetViewHashCode()
        {
            if (m_Version != MultiplayerCenterSettings.instance.SelectedGameGenre.GetHashCode())
            {
                m_Version = MultiplayerCenterSettings.instance.SelectedGameGenre.GetHashCode();

                m_Root.Clear();
                CreateGUI();
            }

            return m_Version;
        }
    }
}
