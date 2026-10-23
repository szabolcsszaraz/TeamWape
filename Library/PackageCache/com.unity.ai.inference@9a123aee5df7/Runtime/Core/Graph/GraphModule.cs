using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Graph
{
    /// <summary>
    /// Represents a model as a graph and set of attributes.
    /// see https://github.com/pytorch/pytorch/blob/main/torch/fx/graph_module.py
    /// </summary>
    class GraphModule
    {
        public Graph graph;
        public Dictionary<string, ConstantTensor> attributes;

        public GraphModule()
        {
            graph = new Graph(this);
            attributes = new Dictionary<string, ConstantTensor>();
        }
    }
}
