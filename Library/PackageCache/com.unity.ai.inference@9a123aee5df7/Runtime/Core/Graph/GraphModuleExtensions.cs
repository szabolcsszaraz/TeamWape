using System;
using System.Runtime.InteropServices;
using Unity.InferenceEngine.Compiler.Passes.Optimization;

namespace Unity.InferenceEngine.Graph
{
    /// <summary>
    /// Represents extension methods for adding nodes to GraphModules.
    /// These are useful as they deal with the things that are non-standard in torch such as partial tensors and output names.
    /// </summary>
    static partial class GraphModuleExtensions
    {
        public static Node Input(this GraphModule gm, string name, DataType dataType, DynamicTensorShape shape)
        {
            var node = gm.graph.Placeholder(name);
            node.partialTensor = PartialTensor.Create(dataType, shape);
            return node;
        }

        public static Node Constant(this GraphModule gm, ConstantTensor constant)
        {
            return GraphPassUtil.AddConstant(gm, gm.graph.root, constant);
        }

        public static Node Constant<T>(this GraphModule gm, T value) where T : unmanaged
        {
            return Constant(gm, new TensorShape(), new[] { value });
        }

        public static Node Constant<T>(this GraphModule gm, T[] values) where T : unmanaged
        {
            return Constant(gm, new TensorShape(values.Length), values);
        }

        public static Node Constant<T>(this GraphModule gm, TensorShape shape, T[] values) where T : unmanaged
        {
            var dataType = AllocatorUtils.ToDataType<T>();
            var bytes = MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
            var constantTensor = new ConstantTensor(shape, dataType, bytes);
            return GraphPassUtil.AddConstant(gm, gm.graph.root, constantTensor);
        }

        public static Node[] Layer(this GraphModule gm, string target, Argument[] args)
        {
            var layerNode = gm.graph.CallFunction(target, args);
            var output = FunctionalLayer.InferPartial(target, args);

            if (output is PartialTensor partialTensor)
            {
                layerNode.partialTensor = partialTensor;
                return new[] { layerNode };
            }

            if (output is PartialTensor[] partialTensors)
            {
                // output is a list of tensors, insert indexer nodes equivalent to "getitem" functions in the graph
                var outputs = new Node[partialTensors.Length];
                for (var i = 0; i < partialTensors.Length; i++)
                {
                    outputs[i] = gm.graph.CallFunction("getitem", new Argument[] { layerNode, i });
                    outputs[i].partialTensor = partialTensors[i];
                }
                return outputs;
            }

            return null;
        }

        public static Node Outputs(this GraphModule gm, string[] names, Node[] outputs)
        {
            var outputNode = gm.graph.Output(outputs);
            outputNode.meta["output_names"] = names;
            return outputNode;
        }
    }
}
