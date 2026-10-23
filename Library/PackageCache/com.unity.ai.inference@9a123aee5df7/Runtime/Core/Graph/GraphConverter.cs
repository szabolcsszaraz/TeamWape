using System.Collections.Generic;

namespace Unity.InferenceEngine.Graph
{
    /// <summary>
    /// Represents a static class with methods for transforming between the GraphModule and Model representations.
    /// </summary>
    static partial class GraphConverter
    {
        /// <summary>
        /// Converts a GraphModule to a Model, certain info such as node names and partial tensors will be lost.
        /// </summary>
        public static Model GraphToModel(GraphModule gm)
        {
            var model = new Model();
            var indexCount = 0;
            var indexes = new Dictionary<Node, int>();
            var multiOutputLayers = new Dictionary<Node, Layer>();
            var getAttrNodes = new Dictionary<string, Node>();

            foreach (var node in gm.graph.Nodes())
            {
                switch (node.op) // inputs
                {
                    case Node.kOpPlaceholder:
                    {
                        var index = indexCount++;
                        indexes[node] = index;
                        var name = node.name;
                        var dataType = node.partialTensor.dataType;
                        var shape = node.partialTensor.shape;
                        var input = new Model.Input(name, index, dataType, shape);
                        model.inputs.Add(input);
                        break;
                    }
                    case Node.kOpGetAttr: // constant
                    {
                        // check if constant is already accessed by a Node.kOpGetAttr node, if so remap to that one
                        if (getAttrNodes.TryGetValue(node.target, out var existingNode))
                        {
                            indexes[node] = indexes[existingNode];
                            break;
                        }

                        getAttrNodes[node.target] = node;

                        var index = indexCount++;
                        indexes[node] = index;

                        var constantTensor = gm.attributes[node.target];
                        var constant = new Constant(index, constantTensor);
                        model.constants.Add(constant);
                        break;
                    }
                    case Node.kOpCallFunction:
                    {
                        if (node.target == "getitem")
                        {
                            var argNode = (Node)node.args[0];
                            var argIndex = (int)node.args[1];
                            var layer = multiOutputLayers[argNode];
                            indexes[node] = layer.outputs[argIndex];
                        }
                        else
                        {
                            var layer = NodeToLayer(node, argNode => argNode is null ? -1 : indexes[argNode]);
                            layer.outputs = new int[layer.OutputCount];

                            if (layer.IsOutputList)
                            {
                                for (var i = 0; i < layer.outputs.Length; i++)
                                    layer.outputs[i] = indexCount++;

                                multiOutputLayers[node] = layer;
                            }
                            else
                            {
                                var index = indexCount++;
                                indexes[node] = index;
                                layer.outputs[0] = index;
                            }

                            model.layers.Add(layer);
                        }
                        break;
                    }
                    case Node.kOpOutput:
                    {
                        var outputNames = (string[])node.meta["output_names"];
                        var nodes = node.args[0].AsArguments;
                        for (var i = 0; i < nodes.Length; i++)
                        {
                            var index = indexes[nodes[i].AsNode];
                            var output = new Model.Output(outputNames[i], index);
                            model.outputs.Add(output);
                        }

                        break;
                    }
                }
            }

            return model;
        }

        /// <summary>
        /// Converts a Model to a GraphModule.
        /// </summary>
        public static GraphModule ModelToGraphModule(Model model)
        {
            var tensors = new Dictionary<int, Node>();
            var gm = new GraphModule();

            foreach (var constant in model.constants)
            {
                var constantTensor = new ConstantTensor(constant.shape, constant.dataType, constant.array);
                tensors[constant.index] = gm.Constant(constantTensor);
            }

            foreach (var input in model.inputs)
            {
                tensors[input.index] = gm.Input(input.name, input.dataType, input.shape);
            }

            foreach (var layer in model.layers)
            {
                var target = layer.opName;
                var layerInputs = new Node[layer.inputs.Length];
                for (var i = 0; i < layerInputs.Length; i++)
                    layerInputs[i] = layer.inputs[i] == -1 ? null : tensors[layer.inputs[i]];

                var args = LayerToArgs(layer, layerInputs);
                var outputs = gm.Layer(target, args);
                for (var i = 0; i < layer.outputs.Length; i++)
                {
                    if (layer.outputs[i] == -1)
                        continue;
                    tensors[layer.outputs[i]] = outputs[i];
                }
            }

            var outputNames = new string[model.outputs.Count];
            var outputNodes = new Node[model.outputs.Count];
            for (var i = 0; i < model.outputs.Count; i++)
            {
                outputNames[i] = model.outputs[i].name;
                outputNodes[i] = tensors[model.outputs[i].index];
            }

            gm.Outputs(outputNames, outputNodes);
            return gm;
        }
    }
}
