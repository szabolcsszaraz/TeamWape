using System;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using Unity.InferenceEngine.Editor.Visualizer.Views.Edges;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views
{
    class EdgeView : VisualElement
    {
        public EdgeData edgeData { get; }

        public EdgeView(GraphStoreManager storeManager, EdgeData edgeData)
        {
            this.edgeData = edgeData;

            var drawManipulator = new EdgeDrawManipulator(edgeData, storeManager);
            this.AddManipulator(drawManipulator);
        }

        public void UpdateState()
        {
            MarkDirtyRepaint();
        }
    }
}
