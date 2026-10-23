using System;
using System.Collections.Generic;
using System.Linq;
using Unity.InferenceEngine.Editor.LiteRT;
#if SENTIS_ANALYTICS_ENABLED

namespace Unity.InferenceEngine.Editor.Analytics.Import
{
    /// <summary>
    /// LiteRT-specific analytics capture functionality.
    /// </summary>
    static class LiteRTModelImportAnalyticsHelper
    {
        /// <summary>
        /// Captures source LiteRT model metrics and graph structure before conversion.
        /// </summary>
        public static void CaptureSourceModel(LiteRT.Model liteModel, ModelImportAnalytics.ImportData importReport)
        {
            if (!liteModel.Subgraphs(0).HasValue)
                return;

            var subGraph = liteModel.Subgraphs(0).Value;
            importReport.sourceModel.layerCount = subGraph.OperatorsLength;

            CountConstants(liteModel, subGraph, importReport);
            CaptureGraphStructure(liteModel, subGraph, importReport);
        }

        static void CountConstants(LiteRT.Model liteModel, SubGraph subGraph, ModelImportAnalytics.ImportData importReport)
        {
            var constantCount = 0;
            long totalParams = 0;

            for (var i = 0; i < subGraph.TensorsLength; i++)
            {
                if (!subGraph.Tensors(i).HasValue)
                    continue;

                var tensor = subGraph.Tensors(i).Value;

                if (!liteModel.Buffers((int)tensor.Buffer).HasValue)
                    continue;

                var buffer = liteModel.Buffers((int)tensor.Buffer).Value;
                if (buffer.DataLength > 0)
                {
                    constantCount++;

                    // Calculate element count for this constant tensor
                    long elementCount = 1;
                    for (var j = 0; j < tensor.ShapeLength; j++)
                    {
                        elementCount *= tensor.Shape(j);
                    }

                    totalParams += elementCount;
                }

                var tensorType = tensor.Type;

                if (!tensorType.IsDataTypeSupported())
                    importReport.sourceModel.AddUnsupportedDataType(tensorType.ToString());
            }

            importReport.sourceModel.constantCount = constantCount;
            importReport.sourceModel.totalParams = totalParams;
        }

        static void CaptureGraphStructure(LiteRT.Model liteModel, SubGraph subGraph, ModelImportAnalytics.ImportData importReport)
        {
            var tensorMetadata = CollectTensorMetadata(subGraph, importReport);
            RegisterInputOutputTensors(subGraph, importReport);
            ProcessOperators(liteModel, subGraph, importReport);
            CreateTensorDescriptors(tensorMetadata, importReport.sourceModel);

            importReport.sourceModel.inputCount = subGraph.InputsLength;
            importReport.sourceModel.outputCount = subGraph.OutputsLength;

            importReport.sourceModel.graphStructure.BuildBinaryRepresentation();
        }

        static Dictionary<string, (string dataType, int rank, string[] shape)> CollectTensorMetadata(SubGraph subGraph, ModelImportAnalytics.ImportData importReport)
        {
            var tensorMetadata = new Dictionary<string, (string dataType, int rank, string[] shape)>();

            for (var i = 0; i < subGraph.TensorsLength; i++)
            {
                if (!subGraph.Tensors(i).HasValue)
                    continue;

                var tensor = subGraph.Tensors(i).Value;
                var tensorId = i.ToString();
                var intShape = tensor.Shape();
                var stringShape = intShape.Select(d => d.ToString()).ToArray();

                tensorMetadata[tensorId] = (
                    tensor.Type.ToString(),
                    tensor.ShapeLength,
                    stringShape
                );

                importReport.sourceModel.AddDataType(tensor.Type.ToString());
            }

            return tensorMetadata;
        }

        static void RegisterInputOutputTensors(SubGraph subGraph, ModelImportAnalytics.ImportData importReport)
        {
            for (var i = 0; i < subGraph.InputsLength; i++)
            {
                var tensorIndex = subGraph.Inputs(i);
                importReport.sourceModel.graphStructure.inputId.Add(tensorIndex.ToString());
            }

            for (var i = 0; i < subGraph.OutputsLength; i++)
            {
                var tensorIndex = subGraph.Outputs(i);
                importReport.sourceModel.graphStructure.outputId.Add(tensorIndex.ToString());
            }
        }

        static void ProcessOperators(LiteRT.Model liteModel, SubGraph subGraph, ModelImportAnalytics.ImportData importReport)
        {
            for (var opIndex = 0; opIndex < subGraph.OperatorsLength; opIndex++)
            {
                if (!subGraph.Operators(opIndex).HasValue)
                    continue;

                var op = subGraph.Operators(opIndex).Value;

                if (!liteModel.OperatorCodes((int)op.OpcodeIndex).HasValue)
                    continue;

                var operatorCode = liteModel.OperatorCodes((int)op.OpcodeIndex).Value;
                var builtinCode = GetBuiltinCode(operatorCode);

                var inputIndices = CollectInputIndices(op);
                var outputIndices = CollectOutputIndices(op);

                var opTypeStr = builtinCode.ToString();

                importReport.sourceModel.graphStructure.layers.Add(new ModelImportAnalytics.GraphNode
                {
                    opType = opTypeStr,
                    inputIndices = inputIndices,
                    outputIndices = outputIndices
                });

                importReport.sourceModel.AddOperator(opTypeStr);
            }
        }

        static BuiltinOperator GetBuiltinCode(OperatorCode operatorCode)
        {
            return operatorCode.BuiltinCode > BuiltinOperator.PLACEHOLDER_FOR_GREATER_OP_CODES
                ? operatorCode.BuiltinCode
                : (BuiltinOperator)operatorCode.DeprecatedBuiltinCode;
        }

        static string[] CollectInputIndices(Operator op)
        {
            var inputIndices = new string[op.InputsLength];
            for (var j = 0; j < op.InputsLength; j++)
            {
                inputIndices[j] = op.Inputs(j).ToString();
            }

            return inputIndices;
        }

        static string[] CollectOutputIndices(Operator op)
        {
            var outputIndices = new string[op.OutputsLength];
            for (var j = 0; j < op.OutputsLength; j++)
            {
                outputIndices[j] = op.Outputs(j).ToString();
            }

            return outputIndices;
        }

        static void CreateTensorDescriptors(Dictionary<string, (string dataType, int rank, string[] shape)> tensorMetadata, ModelImportAnalytics.ModelData modelData)
        {
            foreach (var kvp in tensorMetadata.OrderBy(x => int.Parse(x.Key)))
            {
                modelData.graphStructure.tensors.Add(new ModelImportAnalytics.TensorDescriptor
                {
                    id = kvp.Key,
                    dataType = kvp.Value.dataType,
                    rank = kvp.Value.rank,
                    shape = kvp.Value.shape
                });
            }
        }
    }
}
#endif
