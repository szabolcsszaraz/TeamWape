using System;
using Microsoft.Msagl.Core.Layout;

namespace Unity.InferenceEngine.Editor.Visualizer.GraphData
{
    class EdgeData
    {
        public Edge Edge { get; }
        public NodeData Source { get; }
        public NodeData Target { get; }
        public int TensorIndex { get; }

        public EdgeData(Edge edge, NodeData source, NodeData target, int tensorIndex)
        {
            Edge = edge;
            Source = source;
            Target = target;
            TensorIndex = tensorIndex;
        }
    }
}
