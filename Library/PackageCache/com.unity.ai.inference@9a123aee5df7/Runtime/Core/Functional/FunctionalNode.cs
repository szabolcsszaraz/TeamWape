using System;
using Unity.InferenceEngine.Graph;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents a functional tensor in a GraphModule graph, the functional tensor contains all the graph connections
    /// rather than them being in the args like a standard node.
    /// </summary>
    class FakeNode : Node
    {
        public FunctionalTensor functionalTensor;

        public FakeNode(FunctionalTensor functionalTensor)
        {
            this.functionalTensor = functionalTensor;
            partialTensor = functionalTensor.partialTensor;
        }
    }

    abstract class FunctionalNode { }

    class InputNode : FunctionalNode
    {
        public DataType dataType;
        public DynamicTensorShape shape;
        public string name;

        public InputNode(DataType dataType, DynamicTensorShape shape, string name)
        {
            this.dataType = dataType;
            this.shape = shape;
            this.name = name;
        }
    }

    class LayerNode : FunctionalNode
    {
        public string target;
        public Argument[] args;

        public LayerNode(string target, Argument[] args)
        {
            this.target = target;
            this.args = args;
        }
    }

    class IndexerNode : FunctionalNode
    {
        public FunctionalNode layerNode;
        public int index;

        public IndexerNode(FunctionalNode layerNode, int index)
        {
            this.layerNode = layerNode;
            this.index = index;
        }
    }

    class ConstantNode : FunctionalNode
    {
        public ConstantTensor constant;

        public ConstantNode(ConstantTensor constant)
        {
            this.constant = constant;
        }
    }
}
