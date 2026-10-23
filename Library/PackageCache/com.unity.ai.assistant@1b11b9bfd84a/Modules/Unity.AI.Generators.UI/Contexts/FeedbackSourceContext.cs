using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.UI.Actions;
using UnityEngine.UIElements;

namespace Unity.AI.Generators.UI
{
    /// <summary>
    /// Context that identifies where feedback is being submitted from.
    /// Provided by the parent element to propagate feedback source to child GenerationTile components.
    /// </summary>
    record FeedbackSourceContext(FeedbackSource feedbackSource)
    {
        public FeedbackSource feedbackSource { get; } = feedbackSource;
    }

    static class FeedbackSourceContextExtensions
    {
        /// <summary>
        /// Gets the feedback source from the context hierarchy, defaulting to Generators.
        /// </summary>
        public static FeedbackSource SelectFeedbackSource(this VisualElement element) =>
            element.GetContext<FeedbackSourceContext>()?.feedbackSource ?? FeedbackSource.Generators;
    }
}
