using System;
using System.Collections.Generic;
using Unity.InferenceEngine.Graph;
using UnityEngine;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    /// <summary>
    /// Calculates partial tensor inference and full tensor inference on the graph.
    /// Tensors that can be fully known at runtime are baked into the graph as attributes rather than calculated.
    /// </summary>
    class FuseConstantsPass : GraphPass
    {
        public override void Run(GraphModule gm)
        {
            var tensorIndex = 0;

            var partialTensorArrays = new Dictionary<Node, PartialTensor[]>();
            var tensorArrays = new Dictionary<Node, Tensor[]>();
            var constantTensors = new Dictionary<Node, Tensor>();
            var calculatedTensors = new Dictionary<Node, Tensor>();
            var storageInstancesToDispose = new List<ModelStorage>();

            foreach (var node in gm.graph.Nodes())
            {
                switch (node.op)
                {
                    case Node.kOpPlaceholder:
                    {
                        break;
                    }
                    case Node.kOpGetAttr:
                    {
                        var constantTensor = gm.attributes[node.target];
                        constantTensors[node] = constantTensor.ToTensor();
                        node.partialTensor = constantTensor.GetPartialTensor();
                        break;
                    }
                    case Node.kOpCallFunction:
                    {
                        if (node.target == "getitem")
                        {
                            if (tensorArrays.TryGetValue((Node)node.args[0], out var tensors))
                            {
                                var tensor = tensors[(int)node.args[1]];
                                calculatedTensors[node] = tensor;
                                node.partialTensor = PartialTensor.FromTensor(tensor);
                            }
                            else
                            {
                                node.partialTensor = partialTensorArrays[(Node)node.args[0]][(int)node.args[1]];
                                if (node.partialTensor.IsStatic())
                                {
                                    var tensor = node.partialTensor.ToTensor();
                                    calculatedTensors[node] = tensor;
                                }
                            }
                        }
                        else
                        {
                            var layer = GraphConverter.NodeToLayer(node, _ => 0);

                            var isDeterministic = layer is not Layers.RandomLayer;
                            var inputArgs = node.args;
                            // TODO better determination of variadic input
                            if (inputArgs[0] != null && inputArgs[0].IsArguments && inputArgs[0].AsArguments[0].IsNode)
                                inputArgs = inputArgs[0].AsArguments;
                            for (var i = 0; i < layer.inputs.Length && isDeterministic; i++)
                            {
                                isDeterministic &= inputArgs[i] is null || calculatedTensors.ContainsKey((Node)inputArgs[i]) || constantTensors.ContainsKey((Node)inputArgs[i]);
                            }

                            if (!isDeterministic)
                            {
                                // partial tensor inference
                                var output = FunctionalLayer.InferPartial(node.target, node.args);
                                if (output is PartialTensor partialTensor)
                                {
                                    node.partialTensor = partialTensor;
                                    if (node.partialTensor.IsStatic())
                                        calculatedTensors[node] = node.partialTensor.ToTensor();
                                }
                                else if (output is PartialTensor[] outputPartialTensors)
                                {
                                    partialTensorArrays[node] = outputPartialTensors;
                                }
                                continue;
                            }

                            using var backend = new CPUBackend();
                            var vars = new ModelStorage();
                            storageInstancesToDispose.Add(vars);
                            var executionContext = new ExecutionContext
                            {
                                backend = backend,
                                cpuBackend = backend,
                                storage = vars
                            };

                            for (var i = 0; i < layer.inputs.Length; i++)
                            {
                                var inputNode = (Node)inputArgs[i];
                                if (inputArgs[i] is null)
                                {
                                    layer.inputs[i] = -1;
                                    continue;
                                }
                                Tensor tensor;
                                if (calculatedTensors.TryGetValue(inputNode, out var calculatedInputTensor))
                                    tensor = calculatedInputTensor;
                                else
                                    tensor = constantTensors[inputNode];

                                layer.inputs[i] = tensorIndex++;
                                executionContext.storage.SetInput(layer.inputs[i], tensor);
                            }

                            layer.outputs = new int[layer.OutputCount];
                            for (var i = 0; i < layer.outputs.Length; i++)
                                layer.outputs[i] = tensorIndex++;

                            // full inference
                            layer.Execute(executionContext);

                            if (layer.IsOutputList)
                            {
                                var tensors = new Tensor[layer.outputs.Length];
                                for (var i = 0; i < layer.outputs.Length; i++)
                                {
                                    var outputTensor = executionContext.storage.TakeTensorOwnership(layer.outputs[i]);
                                    outputTensor.CompleteAllPendingOperations();
                                    tensors[i] = outputTensor;
                                }

                                tensorArrays[node] = tensors;
                            }
                            else
                            {
                                var outputTensor = executionContext.storage.TakeTensorOwnership(layer.outputs[0]);
                                outputTensor.CompleteAllPendingOperations();
                                calculatedTensors[node] = outputTensor;
                                node.partialTensor = PartialTensor.FromTensor(outputTensor);
                            }
                        }
                        break;
                    }
                }
            }

            foreach (var (node, tensor) in calculatedTensors)
            {
                var constantNode = GraphPassUtil.AddConstant(gm, node, new ConstantTensor(tensor));
                tensor.Dispose();
                node.ReplaceAllUsesWith(constantNode);
                gm.graph.EraseNode(node);
            }

            try
            {
                gm.graph.EliminateDeadCode();
            }
            finally
            {
                // Dispose all ModelStorage instances after tensors have been processed
                foreach (var storage in storageInstancesToDispose)
                {
                    storage?.Dispose();
                }
            }
        }
    }
}
