namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    /// <summary>
    /// Implemented by the container a chat element is rendered in, when that container positions the
    /// element itself instead of letting flex reflow do it. A user-driven size change reaches the host
    /// through <see cref="ChatHeightChangeExtensions.ReportHeightChange"/>, which finds the nearest
    /// host above the element that changed.
    /// </summary>
    interface IChatHeightChangeHost
    {
        /// <param name="expanded">
        /// The new expansion state, when the toggle also prunes or restores what the host displays below
        /// the element; null when nothing but the element's own height changed.
        /// </param>
        void OnContentHeightChanged(bool? expanded);
    }
}
