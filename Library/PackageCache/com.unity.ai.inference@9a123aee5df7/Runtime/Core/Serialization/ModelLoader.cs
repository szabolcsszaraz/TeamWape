// #define DEBUG_TIMING
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using SentisFlatBuffer;
using Unity.InferenceEngine.Google.FlatBuffers;

[assembly: InternalsVisibleTo("Unity.InferenceEngine.RuntimeTests")]
[assembly: InternalsVisibleTo("Unity.InferenceEngine.EditorTests")]

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Provides methods for loading models.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public static class ModelLoader
    {
        /// <summary>
        /// Converts a binary `ModelAsset` representation of a neural network to an object-oriented `Model` representation.
        /// </summary>
        /// <param name="modelAsset">The binary `ModelAsset` model</param>
        /// <returns>The loaded `Model`</returns>
        public static Model Load(ModelAsset modelAsset)
        {
            var modelDescriptionBytes = modelAsset.modelAssetData.value;
            var modelWeightsBytes = new byte[modelAsset.modelWeightsChunks.Length][];
            for (var i = 0; i < modelAsset.modelWeightsChunks.Length; i++)
                modelWeightsBytes[i] = modelAsset.modelWeightsChunks[i].value;
            return LoadModel(modelDescriptionBytes, modelWeightsBytes);
        }

        /// <summary>
        /// Loads a model that has been serialized to disk.
        /// </summary>
        /// <param name="path">The path of the binary serialized model</param>
        /// <returns>The loaded `Model`</returns>
        public static Model Load(string path)
        {
            using var fileStream = File.OpenRead(path);
            return Load(fileStream);
        }

        /// <summary>
        /// Loads a model that has been serialized to a stream.
        /// </summary>
        /// <param name="stream">The stream to load the serialized model from.</param>
        /// <returns>The loaded `Model`.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the stream is not readable.</exception>
        /// <exception cref="EndOfStreamException">Thrown when the stream ends unexpectedly while reading model data.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the stream contains invalid format (invalid model description size, invalid weights chunk size).</exception>
        /// <exception cref="Exception">Thrown when model description loading or weights loading fails. This can occur if the model was exported with an incompatible version or contains unsupported operators.</exception>
        public static Model Load(Stream stream)
        {
            try
            {
                if (stream == null)
                    throw new ArgumentNullException(nameof(stream), "Stream cannot be null");

                if (!stream.CanRead)
                    throw new ArgumentException("Stream is not readable", nameof(stream));

                var model = new Model();

                var prefixSizeBytes = new byte[sizeof(int)];

                if (stream.Read(prefixSizeBytes, 0, sizeof(int)) != sizeof(int))
                    throw new EndOfStreamException("Unexpected end of stream while reading model description size.");

                var modelDescriptionSize = BitConverter.ToInt32(prefixSizeBytes);
                if (modelDescriptionSize <= 0)
                    throw new InvalidOperationException("Invalid model description size");

                var modelDescriptionBytes = new byte[modelDescriptionSize + sizeof(int)];
                System.Buffer.BlockCopy(prefixSizeBytes, 0, modelDescriptionBytes, 0, sizeof(int));

                var descriptionBytesRead = 0;
                while (descriptionBytesRead < modelDescriptionSize)
                {
                    var read = stream.Read(modelDescriptionBytes, sizeof(int) + descriptionBytesRead, modelDescriptionSize - descriptionBytesRead);
                    if (read == 0)
                        throw new EndOfStreamException("Unexpected end of stream while reading model description.");
                    descriptionBytesRead += read;
                }

                var weightBuffersConstantsOffsets = LoadModelDescription(modelDescriptionBytes, model);

                for (var i = 0; i < weightBuffersConstantsOffsets.Length; i++)
                {
                    if (stream.Read(prefixSizeBytes, 0, sizeof(int)) != sizeof(int))
                        throw new EndOfStreamException($"Unexpected end of stream while reading weights chunk {i} header.");

                    var modelWeightsChunkSize = BitConverter.ToInt32(prefixSizeBytes);
                    if (modelWeightsChunkSize < 0)
                        throw new InvalidOperationException($"Invalid weights chunk size at index {i}");

                    var modelWeightsBufferBytes = new byte[modelWeightsChunkSize + sizeof(int)];
                    System.Buffer.BlockCopy(prefixSizeBytes, 0, modelWeightsBufferBytes, 0, sizeof(int));

                    var weightsBytesRead = 0;
                    while (weightsBytesRead < modelWeightsChunkSize)
                    {
                        var read = stream.Read(modelWeightsBufferBytes, sizeof(int) + weightsBytesRead, modelWeightsChunkSize - weightsBytesRead);
                        if (read == 0)
                            throw new EndOfStreamException($"Unexpected end of stream while reading weights chunk {i}.");
                        weightsBytesRead += read;
                    }

                    LoadModelWeights(modelWeightsBufferBytes, weightBuffersConstantsOffsets[i], model);
                }

                return model;
            }
            catch (Exception e)
            {
                D.LogError($"Failed to load serialized .sentis model, ensure model was exported with Sentis 1.4 or newer (or Inference Engine). ({e.Message})");
                throw;
            }
        }

        internal static Model LoadModelDescription(byte[] modelDescription)
        {
            var model = new Model();
            LoadModelDescription(modelDescription, model);
            return model;
        }

        internal static Model LoadModel(byte[] modelDescription, byte[][] modelWeights)
        {
            var model = new Model();
            var weightsConstantIndexesOffsets = LoadModelDescription(modelDescription, model);
            for (var i = 0; i < weightsConstantIndexesOffsets.Length; i++)
                LoadModelWeights(modelWeights[i], weightsConstantIndexesOffsets[i], model);
            return model;
        }

        static DynamicTensorShape GetDynamicShape(SentisFlatBuffer.Tensor tensorDesc)
        {
            if (tensorDesc.ShapeDynamism == TensorShapeDynamism.STATIC)
                return new DynamicTensorShape(new TensorShape(tensorDesc.GetFixedSizesArray()));
            if (tensorDesc.HasDynamicRank)
                return DynamicTensorShape.DynamicRank;

            var shape = DynamicTensorShape.DynamicOfRank(tensorDesc.DynamicSizesLength);
            for (var i = 0; i < shape.rank; i++)
            {
                var d = tensorDesc.DynamicSizes(i).Value;
                shape[i] = d.ValType switch
                {
                    SymbolicDim.NONE => DynamicTensorDim.Unknown,
                    SymbolicDim.Int => DynamicTensorDim.Int(d.ValAsInt().IntVal),
                    SymbolicDim.Byte => DynamicTensorDim.Param(d.ValAsByte().ByteVal),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            return shape;
        }

        static List<(int, int)>[] LoadModelDescription(byte[] modelDescription, Model model)
        {
            var bb = new ByteBuffer(modelDescription, sizeof(int));
            var program = Program.GetRootAsProgram(bb);
            var numWeightsBuffers = program.SegmentsLength;
            var weightBuffersConstantsOffsets = new List<(int, int)>[numWeightsBuffers];
            for (var i = 0; i < numWeightsBuffers; i++)
                weightBuffersConstantsOffsets[i] = new List<(int, int)>();

            try
            {
                var originalProgramVersion = program.Version;
                if (originalProgramVersion < ModelWriter.version)
                    program = ModelUpgrader.Upgrade(program);
                if (program.Version < ModelWriter.version)
                    throw new Exception("Model upgrader must upgrade to ModelWriter version");
                if (originalProgramVersion > ModelWriter.version)
                    D.LogWarning("Serialized model was exported in a newer version of Sentis than the current installed version and may not work as expected. Update the Sentis package to ensure compatibility.");
                var executionPlan = program.ExecutionPlan.Value;

                model.symbolicDimNames = new string[executionPlan.SymbolicDimNamesLength];
                for (var i = 0; i < model.symbolicDimNames.Length; i++)
                    model.symbolicDimNames[i] = executionPlan.SymbolicDimNames(i);

                int inputCount = executionPlan.InputsLength;
                for (int i = 0; i < inputCount; i++)
                {
                    int input = executionPlan.Inputs(i);
                    var tensorDesc = executionPlan.Values(input).Value.ValAsTensor();
                    var name = executionPlan.InputsName(i);
                    var dataType = (DataType)tensorDesc.ScalarType;
                    var shape = GetDynamicShape(tensorDesc);
                    model.inputs.Add(new Model.Input(name, input, dataType, shape));
                }
                model.outputs = new List<Model.Output>();
                for (int i = 0; i < executionPlan.OutputsLength; i++)
                {
                    model.outputs.Add(new Model.Output(executionPlan.OutputsName(i), executionPlan.Outputs(i)));
                }

                // load known data types and shapes
                for (var i = 0; i < executionPlan.ValuesLength; i++)
                {
                    var value = executionPlan.Values(i).Value;
                    if (value.ValType != KernelTypes.Tensor)
                        continue;
                    var tensorDesc = executionPlan.Values(i).Value.ValAsTensor();
                    // defaults for tensor desc mean no data type or shape provided
                    if (tensorDesc.ShapeDynamism == TensorShapeDynamism.STATIC && tensorDesc.GetFixedSizesArray() == null)
                        continue;
                    model.dataTypes[i] = (DataType)tensorDesc.ScalarType;
                    model.shapes[i] = GetDynamicShape(tensorDesc);
                }

                HashSet<int> constants = new HashSet<int>();
                for (int i = 0; i < executionPlan.ChainsLength; i++)
                {
                    var chain = executionPlan.Chains(i).Value;

                    for (int k = 0; k < chain.InputsLength; k++)
                    {
                        var input = chain.Inputs(k);
                        if (input == -1)
                            continue;
                        if (constants.Contains(input))
                            continue;
                        var constantTensor = executionPlan.Values(input).Value.ValAsTensor();
                        if (constantTensor.ConstantBufferIdx == 0)
                            continue;
                        int lengthByte = constantTensor.LengthByte;
                        model.constants.Add(new Constant(input, new TensorShape(constantTensor.GetFixedSizesArray()), lengthByte, (DataType)constantTensor.ScalarType));
                        var idx = (int)(constantTensor.ConstantBufferIdx - 1);
                        var offset = constantTensor.StorageOffset;
                        weightBuffersConstantsOffsets[idx].Add((model.constants.Count - 1, offset));
                        constants.Add(input);
                    }

                    if (chain.Instructions(0).Value.InstrArgsType == InstructionArguments.NONE)
                        continue;

                    var kernel = chain.Instructions(0).Value.InstrArgsAsKernelCall();
                    string kernelName = executionPlan.Operators(kernel.OpIndex).Value.Name;
                    var layer = LayerModelLoader.DeserializeLayer(kernelName, chain, executionPlan);

                    if (layer == null)
                        throw new NotImplementedException(kernelName);

                    model.layers.Add(layer);
                }
            }
            catch (Exception e)
            {
                D.LogError($"Failed to load serialized model description. ({e.Message})");
                throw;
            }

            return weightBuffersConstantsOffsets;
        }

        static void LoadModelWeights(byte[] modelWeightsBufferBytes, List<(int, int)> constantIndexesOffsets, Model model)
        {
            try
            {
                var bb = new ByteBuffer(modelWeightsBufferBytes, sizeof(int));
                var weightBuffer = SentisFlatBuffer.Buffer.GetRootAsBuffer(bb);
                var data = weightBuffer.GetStorageArray();
                foreach (var (constantIdx, offset) in constantIndexesOffsets)
                {
                    var constant = model.constants[constantIdx];
                    constant.array = new ArraySegment<byte>(data, offset, constant.lengthBytes);
                }
            }
            catch (InvalidOperationException)
            {
                D.LogError("Failed to load serialized model weights.");
                throw;
            }
        }
    }
}
