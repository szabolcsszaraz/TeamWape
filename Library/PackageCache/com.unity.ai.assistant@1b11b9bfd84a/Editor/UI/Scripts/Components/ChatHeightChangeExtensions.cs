using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    static class ChatHeightChangeExtensions
    {
        /// <summary>
        /// Tells the nearest <see cref="IChatHeightChangeHost"/> above this element that a user gesture
        /// changed how much room the element needs. There is no host outside the virtualized
        /// conversation, where flex reflow still absorbs the change, so the report is a no-op there.
        /// </summary>
        /// <param name="element">The element whose displayed size just changed.</param>
        /// <param name="expanded">
        /// The new expansion state, when the gesture also prunes or restores content the host lays out
        /// below this element; left null when only this element's own height changed.
        /// </param>
        public static void ReportHeightChange(this VisualElement element, bool? expanded = null)
            => element.GetFirstAncestorOfType<IChatHeightChangeHost>()?.OnContentHeightChanged(expanded);
    }
}
