#if SENTIS_ANALYTICS_ENABLED
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.InferenceEngine.Editor.Onnx;

namespace Unity.InferenceEngine.Editor.Analytics.Import
{
    /// <summary>
    /// ONNX-specific analytics capture functionality.
    /// </summary>
    static class OnnxModelImportAnalyticsHelper
    {
        /// <summary>
        /// Captures source ONNX model metrics and graph structure before conversion.
        /// </summary>
        public static void CaptureSourceModel(ModelProto onnxModel, ModelImportAnalytics.ImportData importReport)
        {
            CaptureGraphStructure(onnxModel, importReport);
        }

        static void CaptureGraphStructure(ModelProto onnxModel, ModelImportAnalytics.ImportData importReport)
        {
            var registeredTensors = new HashSet<string>();
            var tensorMetadata = new Dictionary<string, (string dataType, int rank, string[] shape)>();

            RegisterInitializers(onnxModel, importReport, registeredTensors, tensorMetadata);
            RegisterInputTensors(onnxModel, importReport, registeredTensors, tensorMetadata);
            RegisterValueInfo(onnxModel, importReport, registeredTensors, tensorMetadata);
            ProcessNodes(onnxModel, importReport, registeredTensors, tensorMetadata);
            RegisterOutputTensors(onnxModel, importReport, registeredTensors, tensorMetadata);
            CreateTensorDescriptors(tensorMetadata, importReport.sourceModel);
            SetModelCounts(onnxModel, importReport);
            PopulateDynamicDimConfigs(tensorMetadata, importReport.sourceModel);

            importReport.sourceModel.graphStructure.BuildBinaryRepresentation();
        }

        static void RegisterInitializers(ModelProto onnxModel, ModelImportAnalytics.ImportData importReport, HashSet<string> registeredTensors, Dictionary<string, (string dataType, int rank, string[] shape)> tensorMetadata)
        {
            foreach (var initializer in onnxModel.Graph.Initializer)
            {
                var tensorName = initializer.Name;
                if (string.IsNullOrEmpty(tensorName))
                    continue;

                if (!registeredTensors.Add(tensorName))
                    continue;

                var onnxDataType = (TensorProto.Types.DataType)initializer.DataType;
                var shape = new string[initializer.Dims.Count];
                long paramCount = 1;
                for (var i = 0; i < initializer.Dims.Count; i++)
                {
                    shape[i] = initializer.Dims[i].ToString();
                    paramCount *= initializer.Dims[i];
                }

                importReport.sourceModel.totalParams += paramCount;

                tensorMetadata[tensorName] = (
                    onnxDataType.ToString(),
                    initializer.Dims.Count,
                    shape
                );

                importReport.sourceModel.AddDataType(onnxDataType.ToString());
            }
        }

        static void RegisterInputTensors(ModelProto onnxModel, ModelImportAnalytics.ImportData importReport, HashSet<string> registeredTensors, Dictionary<string, (string dataType, int rank, string[] shape)> tensorMetadata)
        {
            foreach (var input in onnxModel.Graph.Input)
            {
                var tensorName = input.Name;

                if (!registeredTensors.Add(tensorName))
                    continue;

                if (!input.Type?.TensorType?.Shape?.Dim?.Any() ?? true)
                    continue;

                var onnxShape = input.Type.TensorType.Shape;
                var shape = CreateShapeArray(onnxShape);

                var onnxInputDataType = (TensorProto.Types.DataType)input.Type.TensorType.ElemType;
                tensorMetadata[tensorName] = (
                    onnxInputDataType.ToString(),
                    onnxShape.Dim.Count,
                    shape
                );

                importReport.sourceModel.graphStructure.inputId.Add(tensorName);
                importReport.sourceModel.AddDataType(onnxInputDataType.ToString());
            }
        }

        static void RegisterValueInfo(ModelProto onnxModel, ModelImportAnalytics.ImportData importReport, HashSet<string> registeredTensors, Dictionary<string, (string dataType, int rank, string[] shape)> tensorMetadata)
        {
            // ValueInfo contains type information for intermediate tensors in the graph
            foreach (var valueInfo in onnxModel.Graph.ValueInfo)
            {
                var tensorName = valueInfo.Name;

                // Skip if already registered (inputs/initializers take precedence)
                if (!registeredTensors.Add(tensorName))
                    continue;

                if (!valueInfo.Type?.TensorType?.Shape?.Dim?.Any() ?? true)
                    continue;

                var onnxShape = valueInfo.Type.TensorType.Shape;
                var shape = CreateShapeArray(onnxShape);

                var onnxDataType = (TensorProto.Types.DataType)valueInfo.Type.TensorType.ElemType;
                tensorMetadata[tensorName] = (
                    onnxDataType.ToString(),
                    onnxShape.Dim.Count,
                    shape
                );

                importReport.sourceModel.AddDataType(onnxDataType.ToString());
            }
        }

        static string[] CreateShapeArray(TensorShapeProto onnxShape)
        {
            var shape = new string[onnxShape.Dim.Count];
            for (var i = 0; i < onnxShape.Dim.Count; i++)
            {
                var dim = onnxShape.Dim[i];
                if (dim.DimValue > 0)
                    shape[i] = dim.DimValue.ToString();
                else if (!string.IsNullOrEmpty(dim.DimParam))
                    shape[i] = dim.DimParam;
                else
                    shape[i] = "";
            }

            return shape;
        }

        static void ProcessNodes(ModelProto onnxModel, ModelImportAnalytics.ImportData importReport, HashSet<string> registeredTensors, Dictionary<string, (string dataType, int rank, string[] shape)> tensorMetadata)
        {
            foreach (var node in onnxModel.Graph.Node)
            {
                var layerInputs = CollectNodeInputs(node, registeredTensors, tensorMetadata);
                var layerOutputs = CollectNodeOutputs(node, registeredTensors, tensorMetadata);

                importReport.sourceModel.graphStructure.layers.Add(new ModelImportAnalytics.GraphNode
                {
                    opType = node.OpType,
                    inputIndices = layerInputs.ToArray(),
                    outputIndices = layerOutputs.ToArray()
                });

                importReport.sourceModel.AddOperator(node.OpType);

                // Count params from Constant nodes
                if (node.OpType == "Constant")
                {
                    var valueAttr = node.Attribute.FirstOrDefault(a => a.Name == "value");
                    if (valueAttr?.T != null)
                    {
                        long paramCount = 1;
                        foreach (var dim in valueAttr.T.Dims)
                        {
                            paramCount *= dim;
                        }

                        importReport.sourceModel.totalParams += paramCount;
                    }
                }
            }
        }

        static List<string> CollectNodeInputs(NodeProto node, HashSet<string> registeredTensors, Dictionary<string, (string dataType, int rank, string[] shape)> tensorMetadata)
        {
            var layerInputs = new List<string>();
            foreach (var inputName in node.Input)
            {
                if (string.IsNullOrEmpty(inputName))
                    continue;

                if (registeredTensors.Add(inputName) && !tensorMetadata.ContainsKey(inputName))
                    tensorMetadata[inputName] = ("Unknown", 0, Array.Empty<string>());

                layerInputs.Add(inputName);
            }

            return layerInputs;
        }

        static List<string> CollectNodeOutputs(NodeProto node, HashSet<string> registeredTensors, Dictionary<string, (string dataType, int rank, string[] shape)> tensorMetadata)
        {
            var layerOutputs = new List<string>();
            foreach (var outputName in node.Output)
            {
                if (string.IsNullOrEmpty(outputName))
                    continue;

                if (registeredTensors.Add(outputName) && !tensorMetadata.ContainsKey(outputName))
                    tensorMetadata[outputName] = ("Unknown", 0, Array.Empty<string>());

                layerOutputs.Add(outputName);
            }

            return layerOutputs;
        }

        static void RegisterOutputTensors(ModelProto onnxModel, ModelImportAnalytics.ImportData importReport, HashSet<string> registeredTensors, Dictionary<string, (string dataType, int rank, string[] shape)> tensorMetadata)
        {
            foreach (var output in onnxModel.Graph.Output)
            {
                var tensorName = output.Name;

                if (registeredTensors.Contains(tensorName))
                {
                    importReport.sourceModel.graphStructure.outputId.Add(tensorName);

                    // If output tensor doesn't have type info yet, or has Unknown type, extract it from the Output ValueInfoProto
                    var hasUnknownType = tensorMetadata.ContainsKey(tensorName) && tensorMetadata[tensorName].dataType == "Unknown";
                    if ((!tensorMetadata.ContainsKey(tensorName) || hasUnknownType) && output.Type?.TensorType?.ElemType > 0 && (output.Type?.TensorType?.Shape?.Dim?.Any() ?? false))
                    {
                        var onnxShape = output.Type.TensorType.Shape;
                        var shape = CreateShapeArray(onnxShape);
                        var onnxDataType = (TensorProto.Types.DataType)output.Type.TensorType.ElemType;
                        tensorMetadata[tensorName] = (
                            onnxDataType.ToString(),
                            onnxShape.Dim.Count,
                            shape
                        );
                        importReport.sourceModel.AddDataType(onnxDataType.ToString());
                    }
                }
            }
        }

        static void CreateTensorDescriptors(Dictionary<string, (string dataType, int rank, string[] shape)> tensorMetadata, ModelImportAnalytics.ModelData modelData)
        {
            foreach (var kvp in tensorMetadata.OrderBy(x => x.Key))
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

        static void SetModelCounts(ModelProto onnxModel, ModelImportAnalytics.ImportData importReport)
        {
            importReport.sourceModel.layerCount = onnxModel.Graph.Node.Count;
            importReport.sourceModel.initializerCount = onnxModel.Graph.Initializer.Count;

            var constantNodeCount = onnxModel.Graph.Node.Count(n => n.OpType == "Constant");
            importReport.sourceModel.constantCount = importReport.sourceModel.initializerCount + constantNodeCount;

            importReport.sourceModel.inputCount = importReport.sourceModel.graphStructure.inputId.Count;
            importReport.sourceModel.outputCount = importReport.sourceModel.graphStructure.outputId.Count;
        }

        static void PopulateDynamicDimConfigs(Dictionary<string, (string dataType, int rank, string[] shape)> tensorMetadata, ModelImportAnalytics.ModelData modelData)
        {
            var dynamicDimNames = new HashSet<string>();

            foreach (var kvp in tensorMetadata.Values)
            {
                foreach (var dim in kvp.shape)
                {
                    if (!string.IsNullOrEmpty(dim) && !int.TryParse(dim, out _))
                        dynamicDimNames.Add(dim);
                }
            }

            foreach (var dimName in dynamicDimNames.OrderBy(n => n))
            {
                modelData.dynamicDimConfigs.Add(new ModelImportAnalytics.DynamicDimConfigAnalytics(
                    dimName,
                    -1
                ));
            }
        }
    }
}
#endif
