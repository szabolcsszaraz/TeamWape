using System;
using Unity.InferenceEngine.Graph;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    static class GraphPassUtil
    {
        /// <summary>
        /// Creates a node with a constant tensor value, the tensor is added to the attributes and the node to the graph.
        /// </summary>
        public static Node AddConstant(GraphModule gm, Node node, ConstantTensor constant)
        {
            if (constant == null)
                return null;
            // this name has to be unique, this feels like the easiest way of doing this at the moment
            var name = Guid.NewGuid().ToString();
            gm.attributes[name] = constant;
            gm.graph.InsertingBefore(node);
            var newNode = gm.graph.GetAttr(name);
            newNode.partialTensor = constant.GetPartialTensor();
            return newNode;
        }

        public static Node AddConstant(GraphModule gm, Node node, Tensor tensor)
        {
            using var tensorClone = tensor.ReadbackAndClone();
            return AddConstant(gm, node, new ConstantTensor(tensorClone));
        }

        public static Node AddConstant(GraphModule gm, Node node, TensorShape shape, int[] values)
        {
            return AddConstant(gm, node, new ConstantTensor(shape, values));
        }

        public static Node AddConstant(GraphModule gm, Node node, TensorShape shape, float[] values)
        {
            return AddConstant(gm, node, new ConstantTensor(shape, values));
        }

        /// <summary>
        /// Replace a node with a new Node.kOpCallFunction node at the same point in the graph.
        /// </summary>
        public static Node ReplaceNode(Node node, string op, Argument[] args)
        {
            var graph = node.graph;
            graph.InsertingAfter(node);
            var newNode = graph.CallFunction(op, args);
            node.ReplaceAllUsesWith(newNode);
            graph.EraseNode(node);
            return newNode;
        }

        /// <summary>
        /// Replace a pair of consecutive nodes with a new Node.kOpCallFunction node at the same point in the graph.
        /// </summary>
        public static Node ReplaceNodes(Node prev, Node next, string op, Argument[] args)
        {
            var graph = next.graph;
            graph.InsertingAfter(next);
            var merged = graph.CallFunction(op, args);
            next.ReplaceAllUsesWith(merged);
            graph.EraseNode(next);
            graph.EraseNode(prev);
            return merged;
        }

        /// <summary>
        /// Return the constant tensor from a given node and index.
        /// </summary>
        public static Tensor GetConstantInput(GraphModule gm, Node node, int index)
        {
            var argNode = (Node)node.args[index];
            return argNode is null ? null : gm.attributes[argNode.target].ToTensor();
        }
    }
}
