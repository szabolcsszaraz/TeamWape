using System;
using Unity.Multiplayer.Center.Common;
using UnityEngine;

namespace Unity.Multiplayer.Center.Editor
{
    /// <summary>
    /// Provides a list of <see cref="GettingStartedSample"/>s for each <see cref="GameGenre"/>.
    /// </summary>
    /// <seealso cref="GettingStartedSampleSection"/>
    class GettingStartedSamplesList : EnumBasedDescription<GameGenre, GettingStartedSample>
    {
        protected override GettingStartedSample CreateNewDescription(GameGenre type)
        {
            return new GettingStartedSample
            {
                Genre = type
            };
        }
    }

    [Serializable]
    struct GettingStartedSample : IEnumDescription<GameGenre>
    {
        [ReadOnlyInspector]
        public GameGenre Genre;

        public string Name;
        public Texture2D Icon;
        public Texture2D LightIcon;

        [TextArea]
        public string Description;

        [Tooltip("A string representing a unique identifier of a multiplayer sample." +
                 "\nSamples will be resolved from the QuickStarts package. " +
                 "Add a local reference to the package for development, " +
                 "otherwise the released package version will be used to resolve samples from.")]
        public string SampleId;

        public GameGenre Type => Genre;
    }
}
