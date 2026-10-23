#if SENTIS_ANALYTICS_ENABLED
using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.InferenceEngine.Editor.Analytics.Import
{
    /// <summary>
    /// Sentis-specific analytics capture functionality for imported models.
    /// </summary>
    static class SentisModelImportAnalyticsHelper
    {
        /// <summary>
        /// Captures source Sentis model metrics from the loaded .sentis file.
        /// Since .sentis files are already compiled models, we treat the loaded
        /// model as the "source" before any dynamic dimension transformations.
        /// </summary>
        public static void CaptureSourceModel(Model model, ModelImportAnalytics.ImportData importReport)
        {
            importReport.sourceModel.layerCount = model.layers.Count;
            importReport.sourceModel.inputCount = model.inputs.Count;
            importReport.sourceModel.outputCount = model.outputs.Count;

            // Mark as N/A since Sentis doesn't distinguish learned parameters from constants
            importReport.sourceModel.initializerCount = -1;

            // Calculate constantCount and totalParams in one pass
            long totalParams = 0;
            foreach (var constant in model.constants)
            {
                totalParams += constant.shape.length;
            }

            importReport.sourceModel.constantCount = model.constants.Count;
            importReport.sourceModel.totalParams = totalParams;

            // Capture operators
            foreach (var layer in model.layers)
            {
                importReport.sourceModel.AddOperator(layer.opName);
            }

            // Capture data types from inputs
            foreach (var input in model.inputs)
            {
                importReport.sourceModel.AddDataType(FormatDataType(input.dataType));
            }

            // Note: Dynamic dimensions are captured in CaptureSourceModelStructure via RegisterInputOutputTensors
        }

        /// <summary>
        /// Captures source Sentis model graph structure.
        /// Reuses the same logic as CaptureImportedModelStructure but for sourceModel.
        /// </summary>
        public static void CaptureSourceModelStructure(Model model, ModelImportAnalytics.ImportData importReport)
        {
            // Infer data types and shapes for all tensors
            model.InferDataTypesShapes();

            var tensorMetadata = new Dictionary<string, (string dataType, int rank, string[] shape)>();

            RegisterInputOutputTensors(model, importReport.sourceModel, tensorMetadata);
            ProcessLayers(model, importReport.sourceModel, tensorMetadata);
            CreateTensorDescriptors(tensorMetadata, importReport.sourceModel);

            importReport.sourceModel.graphStructure.BuildBinaryRepresentation();
        }

        /// <summary>
        /// Captures imported/optimized Sentis model metrics.
        /// </summary>
        public static void CaptureImportedModel(Model model, ModelImportAnalytics.ImportData importReport)
        {
            importReport.importedModel.layerCount = model.layers.Count;

            // Calculate constantCount and totalParams in one pass
            long totalParams = 0;
            foreach (var constant in model.constants)
            {
                totalParams += constant.shape.length;
            }

            importReport.importedModel.constantCount = model.constants.Count;
            importReport.importedModel.totalParams = totalParams;

            CaptureImportedModelStructure(model, importReport);
        }

        /// <summary>
        /// Captures imported Sentis model graph structure (ops and connectivity).
        /// </summary>
        public static void CaptureImportedModelStructure(Model model, ModelImportAnalytics.ImportData importReport)
        {
            var tensorMetadata = new Dictionary<string, (string dataType, int rank, string[] shape)>();

            RegisterInputOutputTensors(model, importReport.importedModel, tensorMetadata);
            ProcessLayers(model, importReport.importedModel, tensorMetadata);
            CreateTensorDescriptors(tensorMetadata, importReport.importedModel);

            importReport.importedModel.inputCount = model.inputs.Count;
            importReport.importedModel.outputCount = model.outputs.Count;

            importReport.importedModel.graphStructure.BuildBinaryRepresentation();
        }

        static void RegisterInputOutputTensors(Model model, ModelImportAnalytics.ModelData modelData, Dictionary<string, (string dataType, int rank, string[] shape)> tensorMetadata)
        {
            var dynamicDimNames = new HashSet<string>();

            foreach (var input in model.inputs)
            {
                var tensorId = input.index.ToString();
                var shapeArray = input.shape.ToIntArray();
                var shapeStrings = shapeArray != null
                    ? shapeArray.Select(d => d.ToString()).ToArray()
                    : Array.Empty<string>();

                tensorMetadata[tensorId] = (
                    FormatDataType(input.dataType),
                    input.shape.rank,
                    shapeStrings
                );
                modelData.graphStructure.inputId.Add(tensorId);

                // Capture dynamic dimensions from input shapes
                for (var i = 0; i < input.shape.rank; i++)
                {
                    var dim = input.shape[i];
                    if (dim.dimType == DimType.Param)
                    {
                        // Use symbolic name if available, otherwise fall back to param index
                        var dimName = dim.param < (model.symbolicDimNames?.Length ?? 0)
                            ? model.symbolicDimNames[dim.param]
                            : dim.param.ToString();
                        dynamicDimNames.Add(dimName);
                    }
                }
            }

            // Add dynamic dimensions to model data
            foreach (var dimName in dynamicDimNames.OrderBy(n => n))
            {
                modelData.dynamicDimConfigs.Add(
                    new ModelImportAnalytics.DynamicDimConfigAnalytics(
                        dimName,
                        -1
                    ));
            }

            foreach (var output in model.outputs)
            {
                var tensorId = output.index.ToString();
                if (!tensorMetadata.ContainsKey(tensorId))
                    tensorMetadata[tensorId] = GetTensorMetadataFromModel(model, output.index);

                modelData.graphStructure.outputId.Add(tensorId);
            }
        }

        static void ProcessLayers(Model model, ModelImportAnalytics.ModelData modelData, Dictionary<string, (string dataType, int rank, string[] shape)> tensorMetadata)
        {
            foreach (var layer in model.layers)
            {
                foreach (var inputIdx in layer.inputs ?? Array.Empty<int>())
                {
                    var tensorId = inputIdx.ToString();
                    if (!tensorMetadata.ContainsKey(tensorId))
                        tensorMetadata[tensorId] = GetTensorMetadataFromModel(model, inputIdx);
                }

                foreach (var outputIdx in layer.outputs ?? Array.Empty<int>())
                {
                    var tensorId = outputIdx.ToString();
                    if (!tensorMetadata.ContainsKey(tensorId))
                        tensorMetadata[tensorId] = GetTensorMetadataFromModel(model, outputIdx);
                }

                modelData.graphStructure.layers.Add(new ModelImportAnalytics.GraphNode
                {
                    opType = layer.opName,
                    inputIndices = layer.inputs?.Select(i => i.ToString()).ToArray() ?? Array.Empty<string>(),
                    outputIndices = layer.outputs?.Select(i => i.ToString()).ToArray() ?? Array.Empty<string>()
                });

                modelData.AddOperator(layer.opName);
            }
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
                modelData.AddDataType(kvp.Value.dataType);
            }
        }

        /// <summary>
        /// Gets tensor metadata from the model's inferred data types and shapes.
        /// Falls back to "Unknown" if type information is not available.
        /// </summary>
        static (string dataType, int rank, string[] shape) GetTensorMetadataFromModel(Model model, int tensorIndex)
        {
            var dataType = model.GetDataType(tensorIndex);
            var shape = model.GetShape(tensorIndex);

            if (dataType.HasValue && shape.HasValue)
            {
                var shapeValue = shape.Value;
                var shapeArray = shapeValue.ToIntArray();
                var shapeStrings = shapeArray != null
                    ? shapeArray.Select(d => d.ToString()).ToArray()
                    : Array.Empty<string>();

                return (FormatDataType(dataType.Value), shapeValue.rank, shapeStrings);
            }

            return ("Unknown", 0, Array.Empty<string>());
        }

        /// <summary>
        /// Converts DataType enum values to standardized format with bit widths.
        /// This ensures consistency between source and imported models' data types in analytics.
        /// </summary>
        static string FormatDataType(DataType dataType)
        {
            return dataType switch
            {
                DataType.Float => "FLOAT32",
                DataType.Int => "INT32",
                DataType.Short => "INT16",
                DataType.Byte => "UINT8",
                DataType.Custom => "CUSTOM",
                _ => dataType.ToString()
            };
        }
    }
}
#endif
