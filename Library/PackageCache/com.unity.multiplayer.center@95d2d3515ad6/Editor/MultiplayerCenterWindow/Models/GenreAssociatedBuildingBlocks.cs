using System;
using Unity.Multiplayer.Center.Common;
using Unity.Properties;
using UnityEditor;
using UnityEngine;

namespace Unity.Multiplayer.Center.Editor.Models
{
    /// <summary>
    /// Provides a list of <see cref="BuildingBlockDescription"/>s for each <see cref="GameGenre"/>.
    /// </summary>
    class GenreAssociatedBuildingBlocks: EnumBasedDescription<GameGenre, BuildingBlockList>
    {
        protected override BuildingBlockList CreateNewDescription(GameGenre type)
        {
            return new BuildingBlockList { m_Genre = type };
        }
    }

    /// <summary>
    /// Contains all the editable properties that describe a building block.
    /// </summary>
    [Serializable]
    struct BuildingBlockDescription
    {
        [SerializeField] private string m_Title;
        [SerializeField] private string m_Description;
        [SerializeField] private string m_AssetStoreURL;
        [SerializeField] private Texture m_Preview;

        [CreateProperty]
        public string Title => L10n.Tr(m_Title);

        [CreateProperty]
        public string Description => L10n.Tr(m_Description);

        [CreateProperty]
        public string AssetStoreURL => L10n.Tr(m_AssetStoreURL);

        [CreateProperty]
        public Texture Preview => m_Preview;
    }

    [Serializable]
    struct BuildingBlockList : IEnumDescription<GameGenre>, IListViewDisplayName
    {
        [ReadOnlyInspector,SerializeField]
        internal GameGenre m_Genre;

        [SerializeField]
        private BuildingBlockDescription[] m_Blocks;

        [CreateProperty]
        public GameGenre Type => m_Genre;

        [CreateProperty]
        public string DisplayName => m_Genre.ToString("G");

        [CreateProperty]
        public int Length => m_Blocks.Length;
    }
}
