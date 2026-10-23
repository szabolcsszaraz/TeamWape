using System;
using Unity.InferenceEngine.Editor.Visualizer.Views;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Extensions
{
    static class GraphViewExtensions
    {
        const string k_ViewportContentClass = "appui-canvas__viewport-container";

        public static VisualElement GetViewportContent(this GraphView graphView)
        {
            if (graphView == null)
                throw new ArgumentNullException(nameof(graphView));

            var content = graphView.Q(classes: k_ViewportContentClass);
            if (content == null)
                throw new InvalidOperationException($"Cannot find viewport content in {nameof(GraphView)}.");

            return content;
        }
    }
}
