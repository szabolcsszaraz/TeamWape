using System;
using System.Collections.Generic;
using Unity.Multiplayer.Center.Common;

namespace Unity.Multiplayer.Center.Editor
{
    /// <summary>
    /// Provides a list of <see cref="PackageRecommendation"/>s for each <see cref="GameGenre"/>.
    /// </summary>
    /// <see cref="GettingStartedRecommendationPackages"/>
    class Recommendations : EnumBasedDescription<GameGenre, PackageRecommendation>
    {
        protected override PackageRecommendation CreateNewDescription(GameGenre type)
        {
            return new PackageRecommendation
            {
                Genre = type,
            };
        }
    }

    [Serializable]
    struct PackageRecommendation : IEnumDescription<GameGenre>
    {
        [ReadOnlyInspector]
        public GameGenre Genre;

        public List<string> CorePackages;
        public List<string> RecommendedPackages;
        public List<string> AdditionalPackages;

        public GameGenre Type => Genre;
    }
}
