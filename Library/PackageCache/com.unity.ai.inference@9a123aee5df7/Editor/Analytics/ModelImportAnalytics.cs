using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.InferenceEngine.Editor.DynamicDims;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.Analytics;

#if SENTIS_ANALYTICS_ENABLED

namespace Unity.InferenceEngine.Editor.Analytics.Import
{
    // More information on how it interacts with the server can be found here: https://docs.editor-data.unity3d.com/Contribute/EditorAnalytics/cs_guide/
    // How to debug: https://docs.editor-data.unity3d.com/Contribute/EditorAnalytics/debugger_guide/
    // Make sure to update the version if you duplicate the schema instead of updating it.
    [AnalyticInfo(k_EventName, k_VendorKey, 7)]
    partial class ModelImportAnalytics : IAnalytic
    {
        const string k_EventName = "sentisModelImport";
        const string k_VendorKey = "unity.sentis";

        ImportData m_ImportData;

        public bool TryGatherData(out IAnalytic.IData data, out Exception error)
        {
            error = null;
            data = m_ImportData;
            return data != null;
        }

        /// <summary>
        /// Test hook: Subscribe to capture ImportData before it's sent.
        /// Only use in tests - set to null in production.
        /// </summary>
        internal static Action<ImportData> OnImportDataCaptured;

        public static void SendEvent(ImportData importData)
        {
            // Capture for testing
            OnImportDataCaptured?.Invoke(importData);

            var analytic = new ModelImportAnalytics();
            analytic.m_ImportData = importData;
            EditorAnalytics.SendAnalytic(analytic);
        }

        /// <summary>
        /// Computes xxHash3-64 from a byte array. Much faster than MD5 (~100x) for large files.
        /// xxHash3 provides excellent collision resistance for file identification.
        /// </summary>
        public static unsafe uint2 ComputeFileHash(byte[] data)
        {
            if (data == null || data.Length == 0)
                return default;

            fixed (byte* ptr = data)
            {
                return xxHash3.Hash64(ptr, data.Length);
            }
        }

        /// <summary>
        /// Computes xxHash3-64 from multiple byte arrays (for model description + weight chunks).
        /// More efficient than concatenating arrays first.
        /// </summary>
        public static unsafe uint2 ComputeFileHash(params byte[][] dataChunks)
        {
            if (dataChunks == null || dataChunks.Length == 0)
                return default;

            // Use streaming state for incremental hashing
            var hasher = new xxHash3.StreamingState(isHash64: true);
            foreach (var chunk in dataChunks)
            {
                if (chunk is { Length: > 0 })
                {
                    fixed (byte* ptr = chunk)
                    {
                        hasher.Update(ptr, chunk.Length);
                    }
                }
            }

            return hasher.DigestHash64();
        }

        /// <summary>
        /// Computes xxHash3-64 from a file path using streaming.
        /// Avoids loading the entire file into memory, supports files larger than 2GB.
        /// Also returns the file size.
        /// </summary>
        public static unsafe (uint2 hash, long fileSize) ComputeFileHashStreaming(string filePath)
        {
            const int bufferSize = 81920; // 80KB buffer
            var hasher = new xxHash3.StreamingState(isHash64: true);
            var buffer = new byte[bufferSize];
            long totalBytes = 0;

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, bufferSize)) > 0)
                {
                    fixed (byte* ptr = buffer)
                    {
                        hasher.Update(ptr, bytesRead);
                    }
                    totalBytes += bytesRead;
                }
            }

            return (hasher.DigestHash64(), totalBytes);
        }

        /// <summary>
        /// Converts DynamicDimConfig array to DynamicDimConfigAnalytics array for analytics.
        /// Preserves dimension names and configured sizes for tracking dynamic dimension usage patterns.
        /// </summary>
        public static List<DynamicDimConfigAnalytics> ConvertDynamicDimConfigs(DynamicDimConfig[] configs)
        {
            if (configs == null || configs.Length == 0)
                return new List<DynamicDimConfigAnalytics>();

            var analyticsConfigs = new List<DynamicDimConfigAnalytics>();
            foreach (var config in configs)
            {
                analyticsConfigs.Add(new DynamicDimConfigAnalytics(config.name, config.size));
            }

            return analyticsConfigs;
        }

        // This class is used to store the data that will be sent to the server. It must match the data that the server expects. Make sure you can read the data in the BigQuery.
        // More information on how to update the data: https://docs.dp.unity3d.com/Schema_Management/schemata_ui/
        [Serializable]
        internal class ImportData : IAnalytic.IData
        {
            /// <summary>
            /// Source model format type (e.g., "ONNX", "LiteRT", "Torch", "Sentis")
            /// </summary>
            public string modelType;

            // === Asset identification ===
            public string assetGuid; // Unity asset GUID for tracking

            // === Import status ===
            public bool importSucceeded;
            public string failureReason;
            public List<ModelConverterBase.ImporterWarning> importWarnings = new();

            // === Source model metrics (pre-optimization) ===
            public ModelData sourceModel = new();

            // === Imported model metrics (post-optimization) ===
            public ModelData importedModel = new();
        }

        /// <summary>
        /// Represents source model metrics before Sentis conversion/optimization,
        /// or imported model metrics after conversion.
        /// </summary>
        [Serializable]
        internal class ModelData
        {
            /// <summary>
            /// Number of computational operations/layers in the model.
            /// - ONNX: Count of nodes in the graph (Graph.Node.Count)
            /// - LiteRT: Count of operators in the subgraph (subGraph.OperatorsLength)
            /// - Torch: Count of nodes in the graph (graph.nodes().size())
            /// - Sentis: Count of layers in the model (model.layers.Count)
            /// </summary>
            public int layerCount;

            /// <summary>
            /// Total number of constant tensors stored in the model.
            /// - ONNX: initializerCount + constant nodes (nodes with OpType="Constant")
            ///   - Includes both learned parameters and fixed constant values
            /// - LiteRT: Count of tensors with non-empty buffer data (buffer.DataLength > 0)
            ///   - Includes weights, biases, and other constant data
            /// - Torch: Count of parameters (model.state_dict().size()) + prim::Constant nodes
            ///   - Includes weights, biases, and constant values in the graph
            /// - Sentis: Count of constant tensors (model.constants.Count)
            /// </summary>
            public int constantCount;

            /// <summary>
            /// Number of initializers (learned parameters like weights and biases).
            /// - ONNX: Count of initializers in graph (Graph.Initializer.Count)
            ///   - Subset of constantCount (initializers are always constants, but not all constants are initializers)
            /// - LiteRT: Not applicable (-1, LiteRT doesn't distinguish initializers from other constants)
            /// - Torch: Count of named parameters (len(model.state_dict()))
            ///   - Includes all trainable parameters from the model state
            /// - Sentis: Not applicable (-1, tracked via constantCount)
            /// Default: -1 (not set or not applicable for the format)
            /// </summary>
            public int initializerCount = -1;

            /// <summary>
            /// Total count of learned parameters (weights/biases) in the model.
            /// Calculated by summing the product of all dimensions for each initializer tensor.
            /// - ONNX: Sum of (dims[0] * dims[1] * ... * dims[n]) for each Graph.Initializer
            /// - LiteRT: Not captured (would require distinguishing trainable vs fixed constants)
            /// - Torch: Sum of parameter.numel() for all parameters in state_dict()
            /// - Sentis: Not captured (Sentis constants don't distinguish learned vs fixed)
            /// This is the standard "model size" metric used in ML (e.g., "ResNet-50 has 25.6M parameters").
            /// </summary>
            public long totalParams;

            /// <summary>
            /// Number of input tensors the model accepts.
            /// - ONNX: Count of graph inputs excluding initializers
            /// - LiteRT: subGraph.InputsLength
            /// - Torch: Count of inputs to the forward method (graph.inputs().size())
            /// - Sentis: model.inputs.Count
            /// </summary>
            public int inputCount;

            /// <summary>
            /// Number of output tensors the model produces.
            /// - ONNX: Graph.Output.Count
            /// - LiteRT: subGraph.OutputsLength
            /// - Torch: Count of outputs from the forward method (graph.outputs().size())
            /// - Sentis: model.outputs.Count
            /// </summary>
            public int outputCount;

            /// <summary>
            /// Configurations for dynamic (variable) dimensions in input tensors.
            /// Each config maps a dimension name to its configured size.
            /// - Stores the actual dimension parameter name (e.g., "batch", "sequence_length")
            /// - Populated for source models with dynamic dimensions:
            ///   - ONNX: Extracted from dimension parameters (dim_param) in value_info
            ///   - Torch: Extracted from dynamic_axes metadata or inferred from -1 dimensions
            /// - In shape arrays, these dimensions appear as the dimension name string
            /// Example: A model with dynamic dims ["batch", "sequence_length"] stores both names with their sizes
            /// Empty for formats without dynamic dimension support or models with fully static shapes.
            /// </summary>
            public List<DynamicDimConfigAnalytics> dynamicDimConfigs = new();

            /// <summary>
            /// List of all unique operator types used in the model.
            /// - ONNX: Node.OpType values (e.g., "Conv", "MatMul", "Add")
            /// - LiteRT: BuiltinOperator enum values as strings (e.g., "CONV_2D", "FULLY_CONNECTED")
            /// - Torch: Node.kind() values (e.g., "aten::conv2d", "aten::linear", "aten::relu")
            /// - Sentis: Layer.opName values (e.g., "Dense", "Conv2D", "Relu")
            /// Used to understand model architecture patterns and operation coverage.
            /// </summary>
            public List<string> allOperators = new();

            /// <summary>
            /// List of operator types that are not supported by Sentis or failed to import.
            /// Subset of allOperators. Empty list indicates full operator support.
            /// Populated during import when:
            /// - ONNX: Operator cannot be mapped to a Sentis equivalent
            /// - LiteRT: Operator is not in the supported BuiltinOperator list
            /// - Torch: ATen operator cannot be converted to a Sentis layer
            /// Used to identify gaps in Sentis operator coverage.
            /// </summary>
            public List<string> unsupportedOperators = new();

            /// <summary>
            /// List of unique data types used across all tensors in the model.
            /// - ONNX: TensorProto.DataType enum values as strings (e.g., "FLOAT", "INT64", "FLOAT16")
            /// - LiteRT: TensorType enum values as strings (e.g., "FLOAT32", "INT8", "UINT8")
            /// - Torch: ScalarType enum values as strings (e.g., "Float", "Long", "Half", "Int")
            /// - Sentis: Formatted DataType values (e.g., "FLOAT32", "INT32", "UINT8")
            /// Includes types for inputs, outputs, initializers, and intermediate tensors.
            /// "Unknown" is excluded (used only as a placeholder for tensors with unavailable metadata).
            /// </summary>
            public List<string> dataTypesUsed = new();

            /// <summary>
            /// List of data types that are not supported by Sentis.
            /// Subset of dataTypesUsed. Empty list indicates full data type support.
            /// - ONNX: Types like "COMPLEX64", "COMPLEX128", or future additions
            /// - LiteRT: Types not supported by IsDataTypeSupported() check
            /// - Torch: Types like "ComplexFloat", "ComplexDouble", or specialized quantized types
            /// - Sentis: Not populated (imported models only use supported types)
            /// Used to identify quantization formats and precision requirements not yet supported.
            /// </summary>
            public List<string> unsupportedDataTypes = new();

            /// <summary>
            /// Size of the source model file in bytes.
            /// - ONNX: Size of .onnx file (includes both structure and weights)
            /// - LiteRT: Size of .tflite file (includes both structure and weights)
            /// - Torch: Size of .pt or .pth file (includes both structure and weights)
            /// - Sentis: Size of .sentis file after export
            /// Used to track model size distribution and compression effectiveness.
            /// </summary>
            public long fileSizeBytes;

            /// <summary>
            /// xxHash3-64 hash of the entire source model file (structure + weights).
            /// Used for exact model matching and deduplication in analytics.
            /// - 64-bit hash provides excellent collision resistance
            /// - Much faster to compute than MD5/SHA (~100x faster for large files)
            /// - Deterministic: same file always produces same hash
            /// Allows tracking of:
            /// - How many unique models are being imported
            /// - Which specific model versions are causing issues
            /// - Popular pre-trained models (by matching against known hashes)
            /// </summary>
            public Hash2 fileHash;

            /// <summary>
            /// Complete graph structure including tensors, layers/operations, and connectivity.
            /// Captures the computational graph topology without proprietary data (weights/hyperparameters).
            /// Format: compressed binary representation (GZip + base64) of:
            /// - Tensor descriptors (ID, data type, rank, shape)
            /// - Layer nodes (operation type, input IDs, output IDs)
            /// - Input/output tensor IDs
            ///
            /// Captured for both successful and failed imports to enable debugging and coverage analysis.
            ///
            /// Tensor IDs:
            /// - ONNX: Original tensor names as strings (e.g., "input", "conv1/weight", "output")
            /// - LiteRT: Tensor indices as strings (e.g., "0", "1", "42")
            /// - Torch: Value names from the graph (e.g., "input.1", "conv1.weight", "output.1")
            /// - Sentis: Tensor indices as strings (e.g., "0", "1", "42")
            ///
            /// Shape arrays store dimensions as strings:
            /// - Numeric strings: static dimension size (e.g., "224", "1", "512")
            /// - Non-numeric strings: dynamic dimension names (e.g., "batch", "sequence_length")
            /// - Empty string: unconfigured/unknown dynamic dimension
            ///
            /// Example shape ["batch_size", "3", "224", "224"] (NCHW format):
            /// - Batch: "batch_size" (dynamic, references a DynamicDimConfig by name)
            /// - Channels: "3" (static)
            /// - Height: "224" (static)
            /// - Width: "224" (static)
            /// </summary>
            public ModelGraphStructure graphStructure = new();

            public void AddDataType(string dataType)
            {
                if (string.IsNullOrEmpty(dataType) || dataTypesUsed.Contains(dataType))
                    return;

                dataTypesUsed.Add(dataType);
            }

            public void AddUnsupportedDataType(string dataType)
            {
                if (string.IsNullOrEmpty(dataType) || unsupportedDataTypes.Contains(dataType))
                    return;

                unsupportedDataTypes.Add(dataType);
            }

            public void AddOperator(string operatorName)
            {
                if (allOperators.Contains(operatorName))
                    return;

                allOperators.Add(operatorName);
            }

            public void AddUnsupportedOperator(string operatorName)
            {
                if (unsupportedOperators.Contains(operatorName))
                    return;

                unsupportedOperators.Add(operatorName);
            }
        }

        /// <summary>
        /// Describes a tensor in the graph (input, intermediate, or output).
        /// </summary>
        [Serializable]
        internal class TensorDescriptor
        {
            public string id;
            public string dataType;
            public int rank;
            /// <summary>
            /// Shape array where each element is a string:
            /// - Numeric strings = static dimensions (e.g., "224", "1", "512")
            /// - Non-numeric strings = dynamic dimension names (e.g., "batch_size", "sequence_length")
            /// - Empty string = unknown/unconfigured dimension
            /// Example: ["batch_size", "3", "224", "224"] where "batch_size" is a dynamic dimension
            /// </summary>
            public string[] shape;
        }

        /// <summary>
        /// Represents an operator/layer in the model graph structure.
        /// </summary>
        [Serializable]
        internal class GraphNode
        {
            public string opType;
            public string[] inputIndices; // Changed from int[] to string[]
            public string[] outputIndices; // Changed from int[] to string[]
        }

        /// <summary>
        /// Represents the model graph structure without proprietary data (weights/hyperparameters).
        /// Format: tensors[], layers[], inputIndices[], outputIndices[]
        /// Uses a compressed binary format encoded as base64 for efficient transmission.
        /// </summary>
        [Serializable]
        internal class ModelGraphStructure
        {
            /// <summary>
            /// Compressed binary representation of the graph structure, base64 encoded.
            /// Format: tensors[] + layers[] + inputIndices[] + outputIndices[]
            /// </summary>
            public string compressedData;

            /// <summary>
            /// xxHash3-64 of the graph structure for fingerprinting.
            /// </summary>
            public Hash2 structureHash;

            // Temporary storage used during graph capture (not serialized)
            [NonSerialized]
            internal List<TensorDescriptor> tensors = new();
            [NonSerialized]
            internal List<GraphNode> layers = new();
            [NonSerialized]
            internal List<string> inputId = new();
            [NonSerialized]
            internal List<string> outputId = new();

            /// <summary>
            /// Builds the compressed binary representation and computes the structure hash.
            /// </summary>
            public void BuildBinaryRepresentation()
            {
                using var memoryStream = new MemoryStream();
                using var binaryWriter = new BinaryWriter(memoryStream);

                // Write tensors (sorted by ID for stability)
                var sortedTensors = tensors.OrderBy(t => t.id).ToList();
                WriteVarInt(binaryWriter, sortedTensors.Count);
                foreach (var tensor in sortedTensors)
                {
                    WriteString(binaryWriter, tensor.id);
                    WriteString(binaryWriter, tensor.dataType);
                    WriteVarInt(binaryWriter, tensor.rank);

                    // Write shape (now string array)
                    if (tensor.shape is { Length: > 0 })
                    {
                        WriteVarInt(binaryWriter, tensor.shape.Length);
                        foreach (var dim in tensor.shape)
                        {
                            WriteString(binaryWriter, dim ?? ""); // Write each dimension as string
                        }
                    }
                    else
                        WriteVarInt(binaryWriter, 0);
                }

                // Write layers (in topological order)
                WriteVarInt(binaryWriter, layers.Count);
                foreach (var layer in layers)
                {
                    WriteString(binaryWriter, layer.opType);

                    // Write input tensor IDs
                    var inputs = layer.inputIndices ?? Array.Empty<string>();
                    WriteVarInt(binaryWriter, inputs.Length);
                    foreach (var idx in inputs)
                    {
                        WriteString(binaryWriter, idx); // Write as string
                    }

                    // Write output tensor IDs
                    var outputs = layer.outputIndices ?? Array.Empty<string>();
                    WriteVarInt(binaryWriter, outputs.Length);
                    foreach (var idx in outputs)
                    {
                        WriteString(binaryWriter, idx); // Write as string
                    }
                }

                // Write input tensor indices (sorted for stability)
                var sortedInputs = inputId.OrderBy(i => i).ToList();
                WriteVarInt(binaryWriter, sortedInputs.Count);
                foreach (var idx in sortedInputs)
                {
                    WriteString(binaryWriter, idx); // Write as string
                }

                // Write output tensor indices (sorted for stability)
                var sortedOutputs = outputId.OrderBy(i => i).ToList();
                WriteVarInt(binaryWriter, sortedOutputs.Count);
                foreach (var idx in sortedOutputs)
                {
                    WriteString(binaryWriter, idx); // Write as string
                }

                binaryWriter.Flush();
                var uncompressedData = memoryStream.ToArray();

                // Compute hash from uncompressed data (using fast xxHash64)
                structureHash = ComputeFileHash(uncompressedData);

                // Compress with GZip
                using var compressedStream = new MemoryStream();
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    gzipStream.Write(uncompressedData, 0, uncompressedData.Length);
                }

                var compressedBytes = compressedStream.ToArray();

                // Base64 encode
                compressedData = Convert.ToBase64String(compressedBytes);

                // Clear temporary lists to free memory
                tensors = null;
                layers = null;
                inputId = null;
                outputId = null;
            }

            /// <summary>
            /// Writes a variable-length integer (supports negative values).
            /// </summary>
            static void WriteVarInt(BinaryWriter writer, int value)
            {
                // ZigZag encoding for negative values: (n << 1) ^ (n >> 31)
                var zigzag = (uint)((value << 1) ^ (value >> 31));

                while (zigzag >= 0x80)
                {
                    writer.Write((byte)(zigzag | 0x80));
                    zigzag >>= 7;
                }

                writer.Write((byte)zigzag);
            }

            /// <summary>
            /// Writes a UTF-8 string with length prefix.
            /// </summary>
            static void WriteString(BinaryWriter writer, string value)
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                WriteVarInt(writer, bytes.Length);
                writer.Write(bytes);
            }

            /// <summary>
            /// Deserializes a compressed base64 string back into a complete ModelGraphStructure.
            /// </summary>
            /// <param name="base64Data">Base64 encoded, GZip compressed binary data</param>
            /// <returns>Deserialized ModelGraphStructure with tensors, layers, inputs, and outputs populated</returns>
            public static ModelGraphStructure Deserialize(string base64Data)
            {
                // Decode from base64
                var compressedBytes = Convert.FromBase64String(base64Data);

                // Decompress with GZip
                using var compressedStream = new MemoryStream(compressedBytes);
                using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                using var decompressedStream = new MemoryStream();
                gzipStream.CopyTo(decompressedStream);
                decompressedStream.Position = 0;

                using var reader = new BinaryReader(decompressedStream);

                // Read tensors
                var tensorCount = ReadVarInt(reader);
                var tensors = new List<TensorDescriptor>(tensorCount);
                for (var i = 0; i < tensorCount; i++)
                {
                    var id = ReadString(reader);
                    var dataType = ReadString(reader);
                    var rank = ReadVarInt(reader);
                    var shapeLen = ReadVarInt(reader);
                    var shape = new string[shapeLen]; // Changed to string[]
                    for (var j = 0; j < shapeLen; j++)
                    {
                        shape[j] = ReadString(reader); // Read each dimension as string
                    }

                    tensors.Add(new TensorDescriptor
                    {
                        id = id,
                        dataType = dataType,
                        rank = rank,
                        shape = shape
                    });
                }

                // Read layers
                var layerCount = ReadVarInt(reader);
                var layers = new List<GraphNode>(layerCount);
                for (var i = 0; i < layerCount; i++)
                {
                    var opType = ReadString(reader);
                    var inputCount = ReadVarInt(reader);
                    var inputs = new string[inputCount]; // Changed to string[]
                    for (var j = 0; j < inputCount; j++)
                    {
                        inputs[j] = ReadString(reader); // Read as string
                    }

                    var outputCount = ReadVarInt(reader);
                    var outputs = new string[outputCount]; // Changed to string[]
                    for (var j = 0; j < outputCount; j++)
                    {
                        outputs[j] = ReadString(reader); // Read as string
                    }

                    layers.Add(new GraphNode
                    {
                        opType = opType,
                        inputIndices = inputs,
                        outputIndices = outputs
                    });
                }

                // Read input tensor IDs
                var inputIdCount = ReadVarInt(reader);
                var inputIds = new List<string>(inputIdCount);
                for (var i = 0; i < inputIdCount; i++)
                {
                    inputIds.Add(ReadString(reader));
                }

                // Read output tensor IDs
                var outputIdCount = ReadVarInt(reader);
                var outputIds = new List<string>(outputIdCount);
                for (var i = 0; i < outputIdCount; i++)
                {
                    outputIds.Add(ReadString(reader));
                }

                // Create and populate a new ModelGraphStructure
                var graphStructure = new ModelGraphStructure();
                graphStructure.tensors = tensors;
                graphStructure.layers = layers;
                graphStructure.inputId = inputIds;
                graphStructure.outputId = outputIds;
                return graphStructure;
            }

            /// <summary>
            /// Reads a variable-length integer (supports negative values).
            /// </summary>
            static int ReadVarInt(BinaryReader reader)
            {
                uint zigzag = 0;
                var shift = 0;

                while (true)
                {
                    var b = reader.ReadByte();
                    zigzag |= (uint)(b & 0x7F) << shift;
                    if ((b & 0x80) == 0)
                        break;

                    shift += 7;
                }

                // ZigZag decode: (n >> 1) ^ -(n & 1)
                return (int)(zigzag >> 1) ^ -(int)(zigzag & 1);
            }

            /// <summary>
            /// Reads a UTF-8 string with length prefix.
            /// </summary>
            static string ReadString(BinaryReader reader)
            {
                var length = ReadVarInt(reader);
                var bytes = reader.ReadBytes(length);
                return Encoding.UTF8.GetString(bytes);
            }
        }

        /// <summary>
        /// Analytics representation of a dynamic dimension configuration.
        /// Note: name is the actual dimension parameter name from the source model
        /// (ONNX dim_param or Torch dynamic_axes key).
        /// </summary>
        [Serializable]
        internal class DynamicDimConfigAnalytics
        {
            public string name; // Changed from int id to string name - the actual dimension parameter name
            public int size; // Configured size or -1 for unconfigured

            public DynamicDimConfigAnalytics(string name, int size)
            {
                this.name = name;
                this.size = size;
            }
        }

        /// <summary>
        /// Created a custom record as bq doesn't handle uint2 well.
        /// </summary>
        [Serializable]
        public record Hash2
        {
#pragma warning disable CS0414 // Field is assigned but its value is never used
            public long x;
#pragma warning restore CS0414 // Field is assigned but its value is never used
#pragma warning disable CS0414 // Field is assigned but its value is never used
            public long y;
#pragma warning restore CS0414 // Field is assigned but its value is never used

            public static implicit operator Hash2(uint2 value)
            {
                return new Hash2 { x = value.x, y = value.y };
            }
        }
    }
}
#endif
