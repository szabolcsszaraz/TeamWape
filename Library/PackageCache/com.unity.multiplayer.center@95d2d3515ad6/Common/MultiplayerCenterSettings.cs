using UnityEditor;
using UnityEngine;

namespace Unity.Multiplayer.Center.Common
{
    /// <summary>
    /// Project Settings for the Multiplayer Center.
    /// </summary>
    /// <remarks>
    /// Use these settings values to refine what to display in a custom <see cref="OnboardingGUIProvider{T}"/>.
    /// </remarks>
    [FilePath("ProjectSettings/Packages/com.unity.multiplayer.center/MultiplayerCenterSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class MultiplayerCenterSettings : ScriptableSingleton<MultiplayerCenterSettings>
    {
        [SerializeField]
        internal int SelectedGenre = -1;

        /// <summary>
        /// Currently selected <see cref="GameGenre"/>.
        /// </summary>
        /// <remarks>
        /// The selected Game Genre is changed by the Multiplayer Services Window on its first panel.<br />
        /// Users can go back to this panel at any point to change their answer, which will be reflected here.
        /// </remarks>
        public GameGenre SelectedGameGenre => SelectedGenre == -1 ? GameGenre.Casual : (GameGenre)SelectedGenre;

        internal void Save()
        {

            base.Save(true);
        }
    }
}
