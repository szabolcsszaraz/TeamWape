#if SENTIS_ANALYTICS_ENABLED
using System;
using System.Collections.Generic;
using System.Linq;
using TorchPt2;

namespace Unity.InferenceEngine.Editor.Analytics.Import
{
    /// <summary>
    /// PyTorch-specific analytics capture functionality.
    /// </summary>
    static class TorchModelImportAnalyticsHelper
    {
        /// <summary>
        /// Captures source PyTorch model metrics and graph structure before conversion.
        /// </summary>
        public static void CaptureSourceModel(ExportedProgram exportedProgram,
            ModelImportAnalytics.ImportData importReport)
        {
            var graphModule = exportedProgram.graph_module;
            var signature = graphModule.signature;
            var graph = graphModule.graph;

            // Count inputs/outputs/constants
            int userInputCount = 0;
            int userOutputCount = 0;
            int constantCount = 0;
            long totalParams = 0;

            foreach (var inputSpec in signature.input_specs)
            {
                switch (inputSpec.kind)
                {
                    case "user_input":
                        userInputCount++;
                        break;
                    case "parameter":
                        constantCount++;
                        // Calculate total params for parameters
                        if (inputSpec.value is InputToParameterSpec paramSpec &&
                            graph.tensor_values.TryGetValue(paramSpec.arg.name, out var paramMeta))
                        {
                            totalParams += CalculateElementCount(paramMeta);
                        }
                        break;
                    case "tensor_constant":
                        constantCount++;
                        // Calculate total params for tensor constants
                        if (inputSpec.value is InputToTensorConstantSpec constSpec &&
                            graph.tensor_values.TryGetValue(constSpec.arg.name, out var constMeta))
                        {
                            totalParams += CalculateElementCount(constMeta);
                        }
                        break;
                }
            }

            foreach (var outputSpec in signature.output_specs)
            {
                if (outputSpec.kind == "user_output")
                    userOutputCount++;
            }

            // Set model metrics
            importReport.sourceModel.inputCount = userInputCount;
            importReport.sourceModel.outputCount = userOutputCount;
            importReport.sourceModel.constantCount = constantCount;
            importReport.sourceModel.totalParams = totalParams;
            importReport.sourceModel.initializerCount = -1; // N/A for Torch

            // Capture graph structure
            CaptureGraphStructure(exportedProgram, importReport);
        }

        static void CaptureGraphStructure(ExportedProgram exportedProgram,
            ModelImportAnalytics.ImportData importReport)
        {
            var graphModule = exportedProgram.graph_module;
            var graph = graphModule.graph;
            var signature = graphModule.signature;

            var tensorMetadata = new Dictionary<string, (string dataType, int rank, string[] shape)>();
            var dynamicDimNames = new HashSet<string>();

            // Register tensors from tensor_values
            foreach (var kvp in graph.tensor_values)
            {
                var tensorName = kvp.Key;
                var meta = kvp.Value;
                var shape = new List<string>();

                foreach (var dim in meta.sizes)
                {
                    if (dim.kind == "as_int")
                    {
                        shape.Add(dim.value.ToString());
                    }
                    else if (dim.kind == "as_expr")
                    {
                        var symExpr = dim.value as SymExpr;
                        var dimName = symExpr?.expr_str ?? "";
                        shape.Add(dimName);
                        if (!string.IsNullOrEmpty(dimName))
                            dynamicDimNames.Add(dimName);
                    }
                }

                tensorMetadata[tensorName] = (
                    meta.dtype.ToString(),
                    meta.sizes.Count,
                    shape.ToArray()
                );
            }

            // Register input tensors
            foreach (var inputSpec in signature.input_specs)
            {
                if (inputSpec.kind == "user_input" && inputSpec.value is UserInputSpec userInput)
                {
                    if (userInput.arg?.value is TensorArgument tensorArg)
                        importReport.sourceModel.graphStructure.inputId.Add(tensorArg.name);
                }
            }

            // Register output tensors
            foreach (var outputSpec in signature.output_specs)
            {
                if (outputSpec.kind == "user_output" && outputSpec.value is UserOutputSpec userOutput)
                {
                    if (userOutput.arg?.value is TensorArgument tensorArg)
                        importReport.sourceModel.graphStructure.outputId.Add(tensorArg.name);
                }
            }

            // Process nodes (layers)
            foreach (var node in graph.nodes)
            {
                var inputIndices = new List<string>();
                var outputIndices = new List<string>();

                // Collect input tensor names
                if (node.inputs != null)
                {
                    foreach (var input in node.inputs)
                    {
                        if (input.arg?.value is TensorArgument tensorArg)
                            inputIndices.Add(tensorArg.name);
                    }
                }

                // Collect output tensor names
                if (node.outputs != null)
                {
                    foreach (var output in node.outputs)
                    {
                        if (output.value is TensorArgument tensorArg)
                            outputIndices.Add(tensorArg.name);
                    }
                }

                importReport.sourceModel.graphStructure.layers.Add(
                    new ModelImportAnalytics.GraphNode
                    {
                        opType = node.target,
                        inputIndices = inputIndices.ToArray(),
                        outputIndices = outputIndices.ToArray()
                    });
            }

            // Create tensor descriptors
            foreach (var kvp in tensorMetadata.OrderBy(x => x.Key))
            {
                importReport.sourceModel.graphStructure.tensors.Add(
                    new ModelImportAnalytics.TensorDescriptor
                    {
                        id = kvp.Key,
                        dataType = kvp.Value.dataType,
                        rank = kvp.Value.rank,
                        shape = kvp.Value.shape
                    });
            }

            // Populate dynamic dim configs
            foreach (var dimName in dynamicDimNames.OrderBy(n => n))
            {
                importReport.sourceModel.dynamicDimConfigs.Add(
                    new ModelImportAnalytics.DynamicDimConfigAnalytics(
                        name: dimName,
                        size: -1
                    ));
            }

            importReport.sourceModel.graphStructure.BuildBinaryRepresentation();
        }

        static long CalculateElementCount(TensorMeta meta)
        {
            long count = 1;
            foreach (var dim in meta.sizes)
            {
                if (dim.kind == "as_int")
                    count *= Convert.ToInt64(dim.value);
            }
            return count;
        }
    }
}
#endif
