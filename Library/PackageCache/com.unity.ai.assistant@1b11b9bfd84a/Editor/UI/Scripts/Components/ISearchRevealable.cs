using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    /// <summary>
    /// Implemented by a chat element that keeps part of its own content off screen — a folded card, a
    /// pane behind a tab — so that a search jump onto text inside that part can have it shown. The
    /// element is found by walking out from the text the jump landed on, so nothing on the search side
    /// has to know which elements hide anything, or how.
    /// </summary>
    interface ISearchRevealable
    {
        /// <param name="hidden">The descendant a search jump needs on screen.</param>
        /// <returns>
        /// Whether this element was keeping <paramref name="hidden"/> off screen and has shown it, which
        /// moves everything laid out below and settles a pass later.
        /// </returns>
        bool RevealForSearch(VisualElement hidden);
    }
}
