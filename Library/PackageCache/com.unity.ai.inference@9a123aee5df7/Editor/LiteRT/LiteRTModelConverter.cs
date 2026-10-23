using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.InferenceEngine.Google.FlatBuffers;
using Unity.InferenceEngine.Graph;
using Unity.InferenceEngine.Layers;
using UnityEngine;
using UnityEngine.Assertions;
using ScaleMode = Unity.InferenceEngine.Layers.ScaleMode;

namespace Unity.InferenceEngine.Editor.LiteRT
{
    /// <summary>
    /// Represents a converter from an LiteRT model to Sentis format.
    /// </summary>
    class LiteRTModelConverter : ModelConverterBase
    {
        public string[] signatureKeys;
        public string signatureKey;
        internal event Action<Model> OnLiteRTModelLoaded;
        internal event Action<string> OnLiteRTOperator;
        internal event Action<string> OnLiteRTOperatorUnsupported;
        internal event Action<string> OnLiteRTDataType;
        internal event Action<string> OnLiteRTDataTypeUnsupported;

        /// <summary>
        /// Initializes and returns an instance of `LiteRTModelConverter`.
        /// </summary>
        public LiteRTModelConverter(string filePath, string signatureKey)
            : base(filePath)
        {
            this.signatureKey = signatureKey;
        }

        /// <summary>
        /// Converts an LiteRT model to a Sentis `Model` object.
        /// </summary>
        /// <returns>The converted Sentis model.</returns>
        public override InferenceEngine.Model Convert()
        {
            // Read file into memory once - used for both parsing and hash computation
            var data = File.ReadAllBytes(m_FilePath);

            var bb = new ByteBuffer(data, 0);
            var liteModel = Model.GetRootAsModel(bb);
            var gm = new GraphModule();
            var model = new InferenceEngine.Model();

            if (liteModel.SubgraphsLength > 1)
                Debug.LogWarning("Multiple subgraphs are not supported. Using the first subgraph.");
            else if (liteModel.SubgraphsLength <= 0)
                throw new LiteRTImportException("Model contains no subgraphs.");
            var subGraph =  liteModel.Subgraphs(0).Value;
            var tensors = new PermutedFunctionalTensor[subGraph.TensorsLength];

            OnLiteRTModelLoaded?.Invoke(liteModel);

            ValidateSubGraph(subGraph, liteModel);

            signatureKeys = new string[liteModel.SignatureDefsLength];

            for (var i = 0; i < liteModel.SignatureDefsLength; i++)
                signatureKeys[i] = liteModel.SignatureDefs(i).Value.SignatureKey;

            if (signatureKey is null)
                signatureKey = signatureKeys.Length > 0 ? signatureKeys[0] : string.Empty;

            SignatureDef? signatureDef = null;

            for (var i = 0; i < liteModel.SignatureDefsLength; i++)
                if (liteModel.SignatureDefs(i).Value.SignatureKey == signatureKey)
                    signatureDef = liteModel.SignatureDefs(i).Value;

            // constants
            for (var i = 0; i < subGraph.TensorsLength; i++)
            {
                var tensor = subGraph.Tensors(i).Value;
                var buffer = liteModel.Buffers((int)tensor.Buffer).Value;
                var shape = tensor.Shape();
                if (shape != null && (shape.Contains(0) || buffer.DataLength > 0))
                    tensors[i] = new PermutedFunctionalTensor(gm.Constant(tensor.GetConstant(buffer)), isConstant: true);
            }

            PermutedFunctionalTensor AddPermutedInputTensor(int index, string name = null)
            {
                AssertNotNull(subGraph.Tensors(index), (name == null) ? "Input tensor not found" : $"Input tensor \"{name}\" not found");
                var tensor = subGraph.Tensors(index).Value;
                var dataType = tensor.GetDataType();
                name ??= tensor.Name;

                var inputNode = gm.Input(name, dataType, tensor.DynamicShape());
                return new PermutedFunctionalTensor(inputNode);
            }

            // inputs
            if (signatureDef.HasValue)
            {
                for (var i = 0; i < signatureDef.Value.InputsLength; i++)
                {
                    var tensorMap = signatureDef.Value.Inputs(i).Value;
                    var index = (int)tensorMap.TensorIndex;
                    tensors[index] = AddPermutedInputTensor(index, tensorMap.Name);
                }
            }
            else
            {
                for (var i = 0; i < subGraph.InputsLength; i++)
                {
                    var index = subGraph.Inputs(i);
                    tensors[index] = AddPermutedInputTensor(index);
                }
            }

            if (ImportWarnings.Any(w => w.messageSeverity == WarningType.Error))
            {
                var errorMessage = "Could not import model due to errors with inputs";
                Warn(WarningType.Error, errorMessage);
                return model;
            }

            // operators
            for (var opIndex = 0; opIndex < subGraph.OperatorsLength; opIndex++)
            {
                var op = subGraph.Operators(opIndex).Value;
                var operatorCode = liteModel.OperatorCodes((int)op.OpcodeIndex).Value;
                var builtinCode = operatorCode.BuiltinCode > BuiltinOperator.PLACEHOLDER_FOR_GREATER_OP_CODES ? operatorCode.BuiltinCode : (BuiltinOperator)operatorCode.DeprecatedBuiltinCode;

                // Return op input as a permuted functional tensor.
                PermutedFunctionalTensor GetPermutedInput(int index)
                {
                    if (index >= op.InputsLength)
                        return null;
                    var input = op.Inputs(index);
                    if (input == -1)
                        return null;
                    return tensors[input];
                }

                // Return op input as a non-permuted functional tensor with a given permutation if required (e.g. for conv).
                Node GetInput(int index, Permutation? permutation = null)
                {
                    return GetPermutedInput(index)?.GetTensor(permutation);
                }

                // Return the rank of the shape of the op input at an index.
                int GetInputRank(int index)
                {
                    if (index < 0)
                        throw new LiteRTLayerImportException(builtinCode, $"GetInputRank called with negative index {index}");
                    var tensorIndex = op.Inputs(index);
                    if (!subGraph.Tensors(tensorIndex).HasValue)
                        throw new LiteRTLayerImportException(builtinCode, $"Input tensor at index {index} not found");
                    return subGraph.Tensors(tensorIndex).Value.ShapeLength;
                }

                // Return the shape of the op input at an index as an int array, negative 1 as a dim means dynamic.
                int[] GetInputShape(int index)
                {
                    if (index < 0)
                        throw new LiteRTLayerImportException(builtinCode, $"GetInputShape called with negative index {index}");
                    var tensorIndex = op.Inputs(index);
                    if (!subGraph.Tensors(tensorIndex).HasValue)
                        throw new LiteRTLayerImportException(builtinCode, $"Input tensor at index {index} not found");
                    return subGraph.Tensors(tensorIndex).Value.Shape();
                }

                // Return the elements of a 1d functional tensor as an array, this is useful if onnx only supports constant values that correspond to tensors in liteRT.
                T[] GetArray<T>(Node tensor, string name) where T : unmanaged
                {
                    var partialTensor = tensor.partialTensor as PartialTensor<T>;
                    AssertTrue(partialTensor != null, $"\"{name}\" must be of type {typeof(T).Name}");
                    AssertTrue(partialTensor.IsStatic(), $"Dynamic \"{name}\" input is not supported.");
                    return partialTensor.ToArray();
                }

                // Return the single element of a scalar or single-element 1d functional tensor, this is useful if onnx only supports constant values that correspond to tensors in liteRT.
                T GetValue<T>(Node tensor, string name) where T : unmanaged
                {
                    AssertTrue(tensor.partialTensor.shape.rank <= 1, $"\"{name}\" must have a rank of 0 or 1");
                    var tensorArray = GetArray<T>(tensor, name);
                    return tensorArray[0];
                }

                // Return the shape of the op output at an index as an int array, negative 1 as a dim means dynamic.
                int[] GetOutputShape(int index)
                {
                    if (index < 0)
                        throw new LiteRTLayerImportException(builtinCode, $"GetOutputShape called with negative index {index}");
                    var tensorIndex = op.Outputs(index);
                    if (!subGraph.Tensors(tensorIndex).HasValue)
                        throw new LiteRTLayerImportException(builtinCode, $"Output tensor at index {index} not found");
                    return subGraph.Tensors(tensorIndex).Value.Shape();
                }

                // Return the data type of the op output at an index.
                DataType GetOutputDataType(int index)
                {
                    if (index < 0)
                        throw new LiteRTLayerImportException(builtinCode, $"GetOutputDataType called with negative index {index}");
                    var tensorIndex = op.Outputs(index);
                    if (!subGraph.Tensors(tensorIndex).HasValue)
                        throw new LiteRTLayerImportException(builtinCode, $"Output tensor at index {index} not found");
                    var tensor = subGraph.Tensors(tensorIndex).Value;
                    return tensor.GetDataType();
                }

                // Set the output of an op as a permuted tensor.
                void SetPermutedOutput(PermutedFunctionalTensor output, int index = 0)
                {
                    tensors[op.Outputs(index)] = output;
                }

                // Set the output of an op as a non-permuted tensor.
                void SetOutput(Node output, int index = 0)
                {
                    tensors[op.Outputs(index)] = new PermutedFunctionalTensor(output);
                }

                void WarnOpNotImplemented()
                {
                    Warn(WarningType.Error, $"Unsupported LiteRT Operator: {builtinCode}");
                }

                void AssertType(DataType inputType, DataType type)
                {
                    if (inputType != type)
                    {
                        throw new LiteRTLayerImportException(builtinCode, $"LiteRT Operator is not supported with type {inputType}. Expected type: {type}");
                    }
                }

                void AssertValue<T>(T value, T expectedValue, string name) where T : IEquatable<T>
                {
                    if (!value.Equals(expectedValue))
                    {
                        throw new LiteRTLayerImportException(builtinCode, $"Value \"{value}\" is not supported for \"{name}\". Expected value: \"{expectedValue}\"");
                    }
                }

                void AssertTrue(bool value, string msg)
                {
                    if (!value)
                    {
                        throw new LiteRTLayerImportException(builtinCode, msg);
                    }
                }

                void AssertValueGreaterEqual(int value, int minValue, string name)
                {
                    if (value < minValue)
                    {
                        throw new LiteRTLayerImportException(builtinCode, $"\"{name}\" is {value}, must be greater than or equal to {minValue}");
                    }
                }

                int? GetSeed(RandomOptions options)
                {
                    var seed = options.Seed;
                    var seed2 = options.Seed2;
                    return (seed == 0 && seed2 == 0) ? null : HashCode.Combine(seed, seed2);
                }

                switch (builtinCode)
                {
                    case BuiltinOperator.ADD:
                    {
                        var options = op.BuiltinOptionsAsAddOptions();
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast((x, y) => gm.Add(x, y), a, b);
                        output = PermutedActivation(output, options.FusedActivationFunction);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.AVERAGE_POOL_2D:
                    {
                        var options = op.BuiltinOptionsAsPool2DOptions();
                        var input = GetInput(0, new Permutation(0, 3, 1, 2));
                        var kernelShape = new[] { options.FilterHeight, options.FilterWidth };
                        var dilations = new[] { 1, 1 };
                        var strides = new[] { options.StrideH, options.StrideW };
                        var autoPad = options.Padding switch
                        {
                            Padding.SAME => AutoPad.SameUpper,
                            Padding.VALID => AutoPad.Valid,
                            _ => throw new LiteRTLayerImportException(builtinCode, $"Value \"{options.Padding}\" is not supported for attribute \"Padding\"")
                        };
                        int[] pads = null;
                        if (options.Padding == Padding.SAME)
                        {
                            var inputShape = GetInputShape(0)[1..^1];
                            var outputShape = GetOutputShape(0);
                            outputShape = outputShape.Length > 2 ? outputShape[1..^1] : new[] { -1, -1 };
                            pads = GetPads(inputShape, kernelShape, outputShape, dilations, strides);
                            if (pads != null)
                                autoPad = AutoPad.NotSet;
                        }

                        pads ??= new int[4];

                        var output = new PermutedFunctionalTensor(gm.AveragePool(input, kernelShape, strides, pads, autoPad), new Permutation(0, 3, 1, 2));
                        output = PermutedActivation(output, options.FusedActivationFunction);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.CONCATENATION:
                    {
                        var options = op.BuiltinOptionsAsConcatenationOptions();
                        var axis = options.Axis;
                        var inputs = new PermutedFunctionalTensor[op.InputsLength];
                        for (var j = 0; j < inputs.Length; j++)
                            inputs[j] = GetPermutedInput(j);

                        Permutation? permutation = null;
                        if (IsCommonPermutation(inputs, out var commonPermutation))
                            permutation = commonPermutation;

                        var output = new PermutedFunctionalTensor(gm.Concat(inputs.Select(input => input.GetTensor(permutation)).ToArray(), permutation.HasValue ? permutation.Value.Inverse()[axis] : axis), permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.CONV_2D:
                    {
                        var options = op.BuiltinOptionsAsConv2DOptions();
                        var X = GetInput(0, new Permutation(0, 3, 1, 2));
                        var W = GetInput(1, new Permutation(0, 3, 1, 2));
                        var B = GetInput(2);
                        var inputChannels = GetInputShape(0)[^1];
                        var kernelChannels = GetInputShape(1)[^1];
                        var group = (inputChannels != -1 && kernelChannels != -1) ? inputChannels / kernelChannels : 1;
                        var strides = new[] { options.StrideH, options.StrideW };
                        var dilations = new[] { options.DilationHFactor, options.DilationWFactor };
                        int[] pads = null;
                        var autoPad = AutoPad.NotSet;
                        if (options.Padding == Padding.SAME)
                        {
                            var inputShape = GetInputShape(0)[1..^1];
                            var kernelShape = GetInputShape(1)[1..^1];
                            var outputShape = GetOutputShape(0);
                            outputShape = outputShape.Length > 2 ? outputShape[1..^1] : new[] { -1, -1 };
                            pads = GetPads(inputShape, kernelShape, outputShape, dilations, strides);
                            if (pads == null)
                                autoPad = AutoPad.SameUpper;
                        }

                        pads ??= new int[4];

                        var output = new PermutedFunctionalTensor(gm.Conv(X, W, B, autoPad, dilations, group, pads, strides, null, FusableActivation.None), new Permutation(0, 3, 1, 2));
                        output = PermutedActivation(output, options.FusedActivationFunction);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.DEPTHWISE_CONV_2D:
                    {
                        var options = op.BuiltinOptionsAsDepthwiseConv2DOptions();
                        var X = GetInput(0, new Permutation(0, 3, 1, 2));
                        var W = GetInput(1, new Permutation(3, 0, 1, 2));
                        var B = GetInput(2);
                        var inputChannels = GetInputShape(0)[^1];
                        var kernelChannels = GetInputShape(1)[0];
                        var group = (inputChannels != -1 && kernelChannels != -1) ? inputChannels / kernelChannels : 1;
                        var strides = new[] { options.StrideH, options.StrideW };
                        var dilations = new[] { options.DilationHFactor, options.DilationWFactor };
                        int[] pads = null;
                        var autoPad = AutoPad.NotSet;
                        if (options.Padding == Padding.SAME)
                        {
                            var inputShape = GetInputShape(0)[1..^1];
                            var kernelShape = GetInputShape(1)[1..^1];
                            var outputShape = GetOutputShape(0);
                            outputShape = outputShape.Length > 2 ? outputShape[1..^1] : new[] { -1, -1 };
                            pads = GetPads(inputShape, kernelShape, outputShape, dilations, strides);
                            if (pads == null)
                                autoPad = AutoPad.SameUpper;
                        }

                        pads ??= new int[4];

                        var output = new PermutedFunctionalTensor(gm.Conv(X, W, B, autoPad, dilations, group, pads, strides, null, FusableActivation.None), new Permutation(0, 3, 1, 2));
                        output = PermutedActivation(output, options.FusedActivationFunction);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.DEPTH_TO_SPACE:
                    {
                        var options = op.BuiltinOptionsAsDepthToSpaceOptions();
                        var blocksize = options.BlockSize;
                        var mode = DepthToSpaceMode.DepthColumnRow;
                        var rank = GetInputRank(0);
                        var permutation = Permutation.ChannelFirst(rank);
                        var input = GetInput(0, permutation);
                        var output = new PermutedFunctionalTensor(gm.DepthToSpace(input, blocksize, mode), permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.DEQUANTIZE:
                    {
                        var input = GetPermutedInput(0);
                        AssertType(input.permutedTensor.partialTensor.dataType, DataType.Float);
                        var output = new PermutedFunctionalTensor(gm.Identity(GetInput(0)), input.permutation, input.isConstant);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.EMBEDDING_LOOKUP:
                    {
                        var axis = 0;
                        var output = gm.Gather(GetInput(1), GetInput(0), axis);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.FLOOR:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Floor, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.FULLY_CONNECTED:
                    {
                        var options = op.BuiltinOptionsAsFullyConnectedOptions();
                        var input = GetInput(0);
                        var weights = GetInput(1);
                        var bias = GetInput(2);
                        AssertTrue(options.WeightsFormat == FullyConnectedOptionsWeightsFormat.DEFAULT, $"Value \"{options.WeightsFormat}\" is not supported for attribute \"WeightsFormat\"");
                        AssertType(weights.partialTensor.dataType, DataType.Float);
                        AssertValue(weights.partialTensor.shape.rank, 2, "filter rank");

                        if (!options.KeepNumDims && input.partialTensor.shape.rank != 2)
                        {
                            // Extra dims must be compressed
                            AssertTrue(weights.partialTensor.shape.IsStatic(), "Filter shape must be static");
                            var weightsShape = weights.partialTensor.shape.ToIntArray()[1];
                            var shape = gm.Constant(new[] { -1, weightsShape });
                            input = gm.Reshape(input, shape, false);
                        }

                        weights = gm.Transpose(weights, new[] { 1, 0 });

                        var output = gm.MatMul(input, weights);
                        if (bias != null)
                            output = gm.Add(output, bias);
                        output = Activation(output, options.FusedActivationFunction);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.HASHTABLE_LOOKUP:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.L2_NORMALIZATION:
                    {
                        // ReduceSumSquare + Sqrt + Div
                        var options = op.BuiltinOptionsAsL2NormOptions();
                        var input = GetInput(0);
                        var dim = gm.Constant(new[] { -1 });
                        var output = gm.ReduceSumSquare(input, dim, keepdims: true, noopWithEmptyAxes: true);
                        output = gm.Sqrt(output);
                        output = gm.Div(input, output);
                        output = Activation(output, options.FusedActivationFunction);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.L2_POOL_2D:
                    {
                        var options = op.BuiltinOptionsAsPool2DOptions();
                        var input = GetInput(0, new Permutation(0, 3, 1, 2));
                        input = gm.Square(input);

                        var kernelShape = new[] { options.FilterHeight, options.FilterWidth };
                        var dilations = new[] { 1, 1 };
                        var strides = new[] { options.StrideH, options.StrideW };
                        var autoPad = options.Padding switch
                        {
                            Padding.SAME => AutoPad.SameUpper,
                            Padding.VALID => AutoPad.Valid,
                            _ => throw new LiteRTLayerImportException(builtinCode, $"Value \"{options.Padding}\" is not supported for attribute \"Padding\"")
                        };
                        int[] pads = null;
                        if (options.Padding == Padding.SAME)
                        {
                            var inputShape = GetInputShape(0)[1..^1];
                            var outputShape = GetOutputShape(0);
                            outputShape = outputShape.Length > 2 ? outputShape[1..^1] : new[] { -1, -1 };
                            pads = GetPads(inputShape, kernelShape, outputShape, dilations, strides);
                            if (pads != null)
                                autoPad = AutoPad.NotSet;
                        }

                        pads ??= new int[4];

                        var output = new PermutedFunctionalTensor(gm.AveragePool(input, kernelShape, strides, pads, autoPad), new Permutation(0, 3, 1, 2));
                        output = PermutedUnary(gm.Sqrt, output);
                        output = PermutedActivation(output, options.FusedActivationFunction);

                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.LOCAL_RESPONSE_NORMALIZATION:
                    {
                        var options = op.BuiltinOptionsAsLocalResponseNormalizationOptions();
                        var input = GetInput(0, new Permutation(0, 3, 1, 2));
                        var beta = options.Beta;
                        var bias = options.Bias;
                        var size = options.Radius * 2 + 1;
                        var alpha = size * options.Alpha;
                        var output = new PermutedFunctionalTensor(gm.LRN(input, alpha, beta, bias, size), new Permutation(0, 3, 1, 2));
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.LOGISTIC:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Sigmoid, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.LSH_PROJECTION:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.LSTM:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.MAX_POOL_2D:
                    {
                        var options = op.BuiltinOptionsAsPool2DOptions();
                        var input = GetInput(0, new Permutation(0, 3, 1, 2));
                        var kernelShape = new[] { options.FilterHeight, options.FilterWidth };
                        var dilations = new[] { 1, 1 };
                        var strides = new[] { options.StrideH, options.StrideW };
                        var autoPad = options.Padding switch
                        {
                            Padding.SAME => AutoPad.SameUpper,
                            Padding.VALID => AutoPad.Valid,
                            _ => throw new LiteRTLayerImportException(builtinCode, $"Value \"{options.Padding}\" is not supported for attribute \"Padding\"")
                        };
                        int[] pads = null;
                        if (options.Padding == Padding.SAME)
                        {
                            var inputShape = GetInputShape(0)[1..^1];
                            var outputShape = GetOutputShape(0);
                            outputShape = outputShape.Length > 2 ? outputShape[1..^1] : new[] { -1, -1 };
                            pads = GetPads(inputShape, kernelShape, outputShape, dilations, strides);
                            if (pads != null)
                                autoPad = AutoPad.NotSet;
                        }

                        pads ??= new int[4];

                        var output = new PermutedFunctionalTensor(gm.MaxPool(input, kernelShape, strides, pads, autoPad), new Permutation(0, 3, 1, 2));
                        output = PermutedActivation(output, options.FusedActivationFunction);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.MUL:
                    {
                        var options = op.BuiltinOptionsAsMulOptions();
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.Mul, a, b);
                        output = PermutedActivation(output, options.FusedActivationFunction);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.RELU:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Relu, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.RELU_N1_TO_1:
                    {
                        var input = GetPermutedInput(0);
                        var output = new PermutedFunctionalTensor(gm.Clip(input.permutedTensor, gm.Constant(-1.0f), gm.Constant(1.0f)), input.permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.RELU6:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Relu6, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.RESHAPE:
                    {
                        var input = GetInput(0);
                        var shape = op.InputsLength > 1 ? GetInput(1) : gm.Constant(op.BuiltinOptionsAsReshapeOptions().GetNewShapeArray());
                        var output = gm.Reshape(input, shape, true);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.RESIZE_BILINEAR:
                    {
                        var options = op.BuiltinOptionsAsResizeBilinearOptions();
                        var rank = GetInputRank(0);
                        var permutation = Permutation.ChannelFirst(rank);
                        var input = GetInput(0, permutation);
                        var scaleMode = ScaleMode.Sizes;
                        var coordTransformMode = CoordTransformMode.Asymmetric;
                        if (options.HalfPixelCenters)
                            coordTransformMode = CoordTransformMode.HalfPixel;
                        if (options.AlignCorners)
                            coordTransformMode = CoordTransformMode.AlignCorners;
                        var interpolationMode = InterpolationMode.Linear;
                        var nearestMode = NearestMode.RoundPreferFloor;
                        var sizes = gm.Concat(new Node[] { gm.Shape(input, 0, 2), GetInput(1) }, -1);
                        var output = new PermutedFunctionalTensor(gm.Resize(input, sizes, scaleMode, coordTransformMode, interpolationMode, nearestMode, null), permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.RNN:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.SOFTMAX:
                    {
                        var options = op.BuiltinOptionsAsSoftmaxOptions();
                        var beta = options.Beta;
                        var input = gm.ScalarMad(GetInput(0), DataType.Float, beta, 0, 0, 0);
                        var output = gm.Softmax(input, -1);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.SPACE_TO_DEPTH:
                    {
                        var options = op.BuiltinOptionsAsSpaceToDepthOptions();
                        var blocksize = options.BlockSize;
                        var rank = GetInputRank(0);
                        var permutation = Permutation.ChannelFirst(rank);
                        var input = GetInput(0, permutation);
                        var output = new PermutedFunctionalTensor(gm.SpaceToDepth(input, blocksize), permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.SVDF:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.TANH:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Tanh, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.CONCAT_EMBEDDINGS:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.SKIP_GRAM:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.CALL:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.CUSTOM:
                        Warn(WarningType.Error, $"{operatorCode.CustomCode} is a custom operator. Custom operators are not supported.");
                        break;
                    case BuiltinOperator.EMBEDDING_LOOKUP_SPARSE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.PAD:
                    {
                        var input = GetPermutedInput(0);
                        var pads = GetInput(1);
                        pads = gm.Gather(pads, gm.Constant(input.permutation.ToArray()), 0);
                        pads = gm.Transpose(pads, new[] { 1, 0 });
                        pads = gm.Reshape(pads, gm.Constant(new[] { -1 }), false);
                        var output = new PermutedFunctionalTensor(gm.Pad(input.permutedTensor, pads, null, null, PadMode.Constant), input.permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.UNIDIRECTIONAL_SEQUENCE_RNN:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.GATHER:
                    {
                        var options = op.BuiltinOptionsAsGatherOptions();
                        if (options.BatchDims > 0)
                        {
                            var axis = options.Axis;
                            var input = GetInput(0);
                            if (axis != options.BatchDims)
                                input = MoveDim(input, axis, options.BatchDims);
                            var indices = GetInput(1);
                            var indicesRank = indices.partialTensor.shape.rank;
                            indices = gm.Unsqueeze(indices, gm.Constant(new[] { -1 }));
                            var output = gm.GatherND(input, indices, options.BatchDims);
                            if (axis != options.BatchDims)
                                output = MoveDim(output, indicesRank, options.BatchDims);
                            SetOutput(output);
                        }
                        else
                        {
                            var axis = options.Axis;
                            var input = GetInput(0);
                            var indices = GetInput(1);
                            var output = gm.Gather(input, indices, axis);
                            SetOutput(output);
                        }
                        break;
                    }
                    case BuiltinOperator.BATCH_TO_SPACE_ND:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.SPACE_TO_BATCH_ND:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.TRANSPOSE:
                    {
                        var input = GetPermutedInput(0);
                        var permutations = GetArray<int>(GetInput(1), "permutations");

                        // LiteRT allows negative values in a transpose permutation, make these positive
                        for (var j = 0; j < permutations.Length; j++)
                        {
                            if (permutations[j] < 0)
                                permutations[j] += permutations.Length;
                        }

                        var permutation = new Permutation(permutations);
                        var output = new PermutedFunctionalTensor(input.permutedTensor, input.permutation.Compound(permutation.Inverse()), input.isConstant);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.MEAN:
                    {
                        var options = op.BuiltinOptionsAsReducerOptions();
                        var input = GetPermutedInput(0);
                        AssertType(input.permutedTensor.partialTensor.dataType, DataType.Float);
                        var axes = GetInput(1);
                        var keepDims = options.KeepDims;
                        var output = PermutedReduction(gm.ReduceMean, input, axes, keepDims, true);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.SUB:
                    {
                        var options = op.BuiltinOptionsAsSubOptions();
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.Sub, a, b);
                        output = PermutedActivation(output, options.FusedActivationFunction);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.DIV:
                    {
                        var options = op.BuiltinOptionsAsDivOptions();
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.Div, a, b);
                        output = PermutedActivation(output, options.FusedActivationFunction);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.SQUEEZE:
                    {
                        var options = op.BuiltinOptionsAsSqueezeOptions();
                        var axes = options.SqueezeDimsLength > 0 ? gm.Constant(options.GetSqueezeDimsArray()) : null;
                        var output = gm.Squeeze(GetInput(0), axes);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.UNIDIRECTIONAL_SEQUENCE_LSTM:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.STRIDED_SLICE:
                    {
                        var options = op.BuiltinOptionsAsStridedSliceOptions();
                        var beginMask = options.BeginMask;
                        var endMask = options.EndMask;
                        AssertValue(options.EllipsisMask, 0, "EllipsisMask");
                        var newAxisMask = options.NewAxisMask;
                        var shrinkAxisMask = options.ShrinkAxisMask;
                        AssertValue(options.Offset, false, "Offset");
                        var inputPermuted = GetPermutedInput(0);

                        var begin = GetInput(1);
                        var end = GetInput(2);
                        var strides = GetInput(3);
                        if (beginMask != 0 || endMask != 0 || shrinkAxisMask != 0)
                        {
                            var beginArray = GetArray<int>(begin, "begin");
                            var endArray = GetArray<int>(end, "end");
                            var strideArray = GetArray<int>(strides, "strides");
                            for (var j = 0; j < beginArray.Length; j++)
                            {
                                if (((beginMask >> j) & 1) != 0)
                                    beginArray[j] = strideArray[j] > 0 ? 0 : -1;
                                if (((endMask >> j) & 1) != 0)
                                    endArray[j] = strideArray[j] > 0 ? int.MaxValue : int.MinValue;
                                if (((shrinkAxisMask >> j) & 1) != 0)
                                {
                                    endArray[j] = beginArray[j] + 1;
                                    strideArray[j] = 1;
                                }
                            }

                            begin = gm.Constant(beginArray);
                            end = gm.Constant(endArray);
                            strides = gm.Constant(strideArray);
                        }

                        if (!inputPermuted.permutation.IsIdentity() && newAxisMask == 0 && shrinkAxisMask == 0)
                        {
                            // use permuted strided slice
                            var indices = gm.Constant(inputPermuted.permutation.ToArray());
                            var beginPermuted = gm.Gather(begin, indices, 0);
                            var endPermuted = gm.Gather(end, indices, 0);
                            var stridesPermuted = gm.Gather(strides, indices, 0);
                            var output = new PermutedFunctionalTensor(gm.Slice(inputPermuted.permutedTensor, beginPermuted, endPermuted, null, stridesPermuted), inputPermuted.permutation);
                            SetPermutedOutput(output);
                        }
                        else
                        {
                            var input = inputPermuted.GetTensor();

                            if (newAxisMask != 0)
                            {
                                var axesList = new List<int>();
                                for (var j = 0; j < input.partialTensor.shape.rank; j++)
                                {
                                    if (((newAxisMask >> j) & 1) != 0)
                                        axesList.Add(j);
                                }

                                var unsqueezeAxes = gm.Constant(axesList.ToArray());
                                input = gm.Unsqueeze(input, unsqueezeAxes);
                            }

                            var output = gm.Slice(input, begin, end, null, strides);
                            if (shrinkAxisMask != 0)
                            {
                                var axesList = new List<int>();
                                for (var j = 0; j < input.partialTensor.shape.rank; j++)
                                {
                                    if (((shrinkAxisMask >> j) & 1) != 0)
                                        axesList.Add(j);
                                }

                                var squeezeAxes = gm.Constant(axesList.ToArray());
                                output = gm.Squeeze(output, squeezeAxes);
                            }

                            SetOutput(output);
                        }
                        break;
                    }
                    case BuiltinOperator.BIDIRECTIONAL_SEQUENCE_RNN:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.EXP:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Exp, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.TOPK_V2:
                    {
                        var input = GetInput(0);
                        var k = GetInput(1);
                        k = gm.Reshape(k, gm.Constant(new[] { 1 }), false);
                        var outputs = gm.TopK(input, k, -1, true, true);
                        SetOutput(outputs[0], 0);
                        SetOutput(outputs[1], 1);
                        break;
                    }
                    case BuiltinOperator.SPLIT:
                    {
                        var options = op.BuiltinOptionsAsSplitOptions();
                        var input = GetPermutedInput(1);
                        var numSplits = options.NumSplits;
                        var axis = GetValue<int>(GetInput(0), "axis");
                        axis = input.permutation.Inverse()[axis];
                        var outputs = gm.Split(input.permutedTensor, null, axis, numSplits);
                        for (var j = 0; j < numSplits; j++)
                            SetPermutedOutput(new PermutedFunctionalTensor(outputs[j], input.permutation), j);
                        break;
                    }
                    case BuiltinOperator.LOG_SOFTMAX:
                    {
                        var output = gm.LogSoftmax(GetInput(0), -1);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.DELEGATE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.BIDIRECTIONAL_SEQUENCE_LSTM:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.CAST:
                    {
                        var input = GetPermutedInput(0);
                        var dataType = GetOutputDataType(0);
                        var output = new PermutedFunctionalTensor(gm.Cast(input.permutedTensor, dataType), input.permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.PRELU:
                    {
                        var input = GetPermutedInput(0);
                        var slope = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.PRelu, input, slope);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.MAXIMUM:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.Max, a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.MINIMUM:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.Min, a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.LESS:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.Less, a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.NEG:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Neg, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.PADV2:
                    {
                        var input = GetPermutedInput(0);
                        var pads = GetInput(1);
                        pads = gm.Gather(pads, gm.Constant(input.permutation.ToArray()), 0);
                        pads = gm.Transpose(pads, new[] { 1, 0 });
                        pads = gm.Reshape(pads, gm.Constant(new[] { -1 }), false);
                        var output = new PermutedFunctionalTensor(gm.Pad(input.permutedTensor, pads, GetInput(2), null, PadMode.Constant), input.permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.GREATER:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.Greater, a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.GREATER_EQUAL:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.GreaterOrEqual, a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.LESS_EQUAL:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.LessOrEqual, a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.SELECT:
                    {
                        var condition = GetInput(0);
                        var x = GetInput(1);
                        var y = GetInput(2);
                        condition = BroadcastToRank(condition, x.partialTensor.shape.rank, append: true);
                        var output = gm.Where(condition, x, y);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.SLICE:
                    {
                        var input = GetPermutedInput(0);
                        var beginArray = GetArray<int>(GetInput(1), "begin");
                        var sizeArray = GetArray<int>(GetInput(2), "size");
                        var startsArray = new int[beginArray.Length];
                        var endsArray = new int[beginArray.Length];
                        for (var j = 0; j < endsArray.Length; j++)
                        {
                            var axis = input.permutation[j];
                            startsArray[j] = beginArray[axis];
                            endsArray[j] = sizeArray[axis] == -1 ? int.MaxValue : beginArray[axis] + sizeArray[axis];
                        }

                        var starts = gm.Constant(startsArray);
                        var ends = gm.Constant(endsArray);
                        var output = gm.Slice(input.permutedTensor, starts, ends, null, null);
                        SetPermutedOutput(new PermutedFunctionalTensor(output, input.permutation));
                        break;
                    }
                    case BuiltinOperator.SIN:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Sin, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.TRANSPOSE_CONV:
                    {
                        var options = op.BuiltinOptionsAsTransposeConvOptions();
                        var X = GetInput(2, new Permutation(0, 3, 1, 2));
                        var W = GetInput(1, new Permutation(3, 0, 1, 2));
                        var B = GetInput(3);
                        var inputShape = GetInputShape(2);
                        var kernelShape = GetInputShape(1);
                        var outputShape = GetArray<int>(GetInput(0), "outputShape");
                        var outputChannels = outputShape[^1];
                        var kernelChannels = kernelShape[0];
                        var group = (outputChannels != -1 && kernelChannels != -1) ? outputChannels / kernelChannels : 1;
                        AssertValue(group, 1, "attribute \"group\"");
                        var strides = new[] { options.StrideH, options.StrideW };
                        var dilations = new[] { 1, 1 };
                        var outputPadding = new int[2];
                        var autoPad = AutoPad.NotSet;
                        var pads = GetPadsTranspose(inputShape[1..^1], kernelShape[1..^1], outputShape[1..^1], dilations, strides);
                        if (pads == null)
                            autoPad = AutoPad.SameUpper;

                        pads ??= new int[4];

                        var outputPermuted = gm.ConvTranspose(X, W, B, autoPad, dilations, 1, outputPadding, pads, strides, null, FusableActivation.None);
                        var output = new PermutedFunctionalTensor(outputPermuted, new Permutation(0, 3, 1, 2));
                        output = PermutedActivation(output, options.FusedActivationFunction);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.SPARSE_TO_DENSE:
                    {
                        var input = GetInput(3);
                        var shape = GetInput(1);
                        var indices = GetInput(0);
                        var updates = GetInput(2);

                        AssertValueGreaterEqual(indices.partialTensor.shape.rank, 1, "sparse_indices rank");

                        if (indices.partialTensor.shape.rank == 1)
                        {
                            indices = gm.Unsqueeze(indices, gm.Constant(new[] { 1 }));
                        }

                        if (updates.partialTensor.shape.rank == 0)
                        {
                            var indicesShape = gm.Shape(indices, 0, 1);
                            updates = gm.Expand(updates, indicesShape);
                        }

                        input = gm.Expand(input, shape);
                        var output = gm.ScatterND(input, indices, updates, ScatterReductionMode.None);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.TILE:
                    {
                        var input = GetPermutedInput(0);
                        var multiples = gm.Gather(GetInput(1), gm.Constant(input.permutation.ToArray()), 0);
                        var output = new PermutedFunctionalTensor(gm.Tile(input.permutedTensor, multiples), input.permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.EXPAND_DIMS:
                    {
                        var output = gm.Unsqueeze(GetInput(0), GetInput(1));
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.EQUAL:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.Equal, a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.NOT_EQUAL:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.NotEqual, a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.LOG:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Log, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.SUM:
                    {
                        var options = op.BuiltinOptionsAsReducerOptions();
                        var input = GetPermutedInput(0);
                        var axes = GetInput(1);
                        var keepDims = options.KeepDims;
                        var output = PermutedReduction(gm.ReduceSum, input, axes, keepDims, true);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.SQRT:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Sqrt, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.RSQRT:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Rsqrt, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.SHAPE:
                    {
                        var input = GetPermutedInput(0);
                        var output = gm.Shape(input.permutedTensor, 0, TensorShape.maxRank);
                        output = gm.Gather(output, gm.Constant(input.permutation.Inverse().ToArray()), 0);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.POW:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.Pow, a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.ARG_MIN:
                    {
                        var dim = GetValue<int>(GetInput(1), "dim");
                        var rank = GetInputRank(0);
                        var output = gm.ArgMin(GetInput(0, Permutation.Identity(rank)), dim, false, false);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.ARG_MAX:
                    {
                        var dim = GetValue<int>(GetInput(1), "dim");
                        var rank = GetInputRank(0);
                        var output = gm.ArgMax(GetInput(0, Permutation.Identity(rank)), dim, false, false);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.FAKE_QUANT:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.REDUCE_PROD:
                    {
                        var options = op.BuiltinOptionsAsReducerOptions();
                        var input = GetPermutedInput(0);
                        var axes = GetInput(1);
                        var keepDims = options.KeepDims;
                        var output = PermutedReduction(gm.ReduceProd, input, axes, keepDims, true);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.REDUCE_MAX:
                    {
                        var options = op.BuiltinOptionsAsReducerOptions();
                        var input = GetPermutedInput(0);
                        var axes = GetInput(1);
                        var keepDims = options.KeepDims;
                        var output = PermutedReduction(gm.ReduceMax, input, axes, keepDims, true);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.PACK:
                    {
                        var options = op.BuiltinOptionsAsPackOptions();
                        var valuesCount = options.ValuesCount;
                        var axis = options.Axis;
                        var axes = gm.Constant(new[] { axis });
                        var inputs = new Node[valuesCount];
                        for (var j = 0; j < valuesCount; j++)
                        {
                            var rank = GetInputRank(j);
                            inputs[j] = gm.Unsqueeze(GetInput(j, Permutation.Identity(rank)), axes);
                        }
                        var output = gm.Concat(inputs, axis);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.LOGICAL_OR:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.Or, a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.ONE_HOT:
                    {
                        var options = op.BuiltinOptionsAsOneHotOptions();
                        var axis = options.Axis;
                        var indices = GetInput(0);
                        var depth = GetInput(1);
                        var onValue = GetInput(2);
                        var offValue = GetInput(3);
                        var allowNegativeIndexes = false;
                        onValue = gm.Reshape(onValue, gm.Constant(new[] { 1 }), false);
                        offValue = gm.Reshape(offValue, gm.Constant(new[] { 1 }), false);
                        var values = gm.Concat(new[] { offValue, onValue }, 0);
                        var output = gm.OneHot(indices, depth, values, axis, allowNegativeIndexes);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.LOGICAL_AND:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.And, a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.LOGICAL_NOT:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Not, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.UNPACK:
                    {
                        var options = op.BuiltinOptionsAsUnpackOptions();
                        var num = options.Num;
                        var axis = options.Axis;
                        var input = GetPermutedInput(0);
                        axis = input.permutation.Inverse()[axis];
                        var dim = gm.Constant(new[] { axis });
                        for (var j = 0; j < num; j++)
                        {
                            var index = gm.Constant(new[] { j });
                            SetOutput(gm.Select(input.permutedTensor, dim, index), j);
                        }
                        break;
                    }
                    case BuiltinOperator.REDUCE_MIN:
                    {
                        var options = op.BuiltinOptionsAsReducerOptions();
                        var input = GetPermutedInput(0);
                        var axes = GetInput(1);
                        var keepDims = options.KeepDims;
                        var output = PermutedReduction(gm.ReduceMin, input, axes, keepDims, true);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.FLOOR_DIV:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.FloorDiv, a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.REDUCE_ANY:
                    {
                        var options = op.BuiltinOptionsAsReducerOptions();
                        var input = GetPermutedInput(0);
                        var axes = GetInput(1);
                        var keepDims = options.KeepDims;
                        var output = PermutedReduction(gm.ReduceMax, input, axes, keepDims, true);
                        var dataType = GetOutputDataType(0);
                        var zero = gm.ConstantOfShape(gm.Constant(Array.Empty<int>()), dataType, 0f, 0);
                        output = new PermutedFunctionalTensor(gm.Max(output.permutedTensor, zero), output.permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.SQUARE:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Square, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.ZEROS_LIKE:
                    {
                        var input = GetInput(0);
                        var shape = gm.Shape(input, 0, TensorShape.maxRank);
                        var output = gm.ConstantOfShape(shape, input.partialTensor.dataType, 0f, 0);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.FILL:
                    {
                        var output = gm.Expand(GetInput(1), GetInput(0));
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.FLOOR_MOD:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast((a, b) => gm.Mod(a, b, false), a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.RANGE:
                    {
                        var output = gm.Range(GetInput(0), GetInput(1), GetInput(2));
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.RESIZE_NEAREST_NEIGHBOR:
                    {
                        var options = op.BuiltinOptionsAsResizeNearestNeighborOptions();
                        var rank = GetInputRank(0);
                        var permutation = Permutation.ChannelFirst(rank);
                        var input = GetInput(0, permutation);
                        var scaleMode = ScaleMode.Sizes;
                        var nearestMode = NearestMode.Floor;
                        var coordTransformMode = CoordTransformMode.Asymmetric;
                        if (options.HalfPixelCenters)
                            coordTransformMode = CoordTransformMode.HalfPixel;
                        if (options.AlignCorners)
                        {
                            coordTransformMode = CoordTransformMode.AlignCorners;
                            nearestMode = NearestMode.RoundPreferCeil;
                        }

                        var interpolationMode = InterpolationMode.Nearest;
                        var sizes = gm.Concat(new[] { gm.Shape(input, 0, 2), GetInput(1) }, -1);
                        var output = new PermutedFunctionalTensor(gm.Resize(input, sizes, scaleMode, coordTransformMode, interpolationMode, nearestMode, null), permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.LEAKY_RELU:
                    {
                        var options = op.BuiltinOptionsAsLeakyReluOptions();
                        var alpha = options.Alpha;
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(t => gm.LeakyRelu(t, alpha), input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.SQUARED_DIFFERENCE:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast(gm.Sub, a, b);
                        output = PermutedUnary(gm.Square, output);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.MIRROR_PAD:
                    {
                        var options = op.BuiltinOptionsAsMirrorPadOptions();

                        var input = GetPermutedInput(0);
                        var pads = GetInput(1);
                        pads = gm.Gather(pads, gm.Constant(input.permutation.ToArray()), 0);
                        pads = gm.Transpose(pads, new[] { 1, 0 });
                        pads = gm.Reshape(pads, gm.Constant(new[] { -1 }), false);
                        var mode = options.Mode switch
                        {
                            MirrorPadMode.REFLECT => PadMode.Reflect,
                            MirrorPadMode.SYMMETRIC => PadMode.Symmetric,
                            _ => throw new LiteRTLayerImportException(builtinCode, $"Value \"{options.Mode}\" is not supported for attribute \"Mode\"")
                        };
                        var output = new PermutedFunctionalTensor(gm.Pad(input.permutedTensor, pads, null, null, mode), input.permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.ABS:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Abs, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.SPLIT_V:
                    {
                        var options = op.BuiltinOptionsAsSplitVOptions();
                        var numSplits = options.NumSplits;
                        var input = GetPermutedInput(0);

                        var splitArray = GetArray<int>(GetInput(1), "split");
                        var axis = GetValue<int>(GetInput(2), "axis");
                        axis = input.permutation.Inverse()[axis];
                        var dimLength = input.permutedTensor.partialTensor.shape[axis].value;
                        var negativeAxis = -1;
                        var nonNegativeSum = 0;
                        for (var j = 0; j < numSplits; j++)
                        {
                            if (splitArray[j] < 0)
                                negativeAxis = j;
                            else
                                nonNegativeSum += splitArray[j];
                        }

                        if (negativeAxis >= 0)
                            splitArray[negativeAxis] = dimLength - nonNegativeSum;

                        var outputs = gm.Split(input.permutedTensor, gm.Constant(splitArray), axis, numSplits);
                        for (var j = 0; j < numSplits; j++)
                            SetPermutedOutput(new PermutedFunctionalTensor(outputs[j], input.permutation), j);
                        break;
                    }
                    case BuiltinOperator.UNIQUE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.CEIL:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Ceil, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.REVERSE_V2:
                    {
                        var input = GetInput(0);
                        var axes = GetInput(1);
                        var numAxes = GetInputShape(1)[0];
                        var startsArray = new int[numAxes];
                        var endsArray = new int[numAxes];
                        var stepsArray = new int[numAxes];
                        for (var j = 0; j < numAxes; j++)
                        {
                            startsArray[j] = -1;
                            endsArray[j] = int.MinValue;
                            stepsArray[j] = -1;
                        }

                        var output = gm.Slice(input, gm.Constant(startsArray), gm.Constant(endsArray), axes, gm.Constant(stepsArray));
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.ADD_N:
                    {
                        var output = GetPermutedInput(0);

                        for (var inputIndex = 1; inputIndex < op.InputsLength; inputIndex++)
                        {
                            output = PermutedBroadcast(gm.Add, output, GetPermutedInput(inputIndex));
                        }

                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.GATHER_ND:
                    {
                        var output = gm.GatherND(GetInput(0), GetInput(1), 0);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.COS:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Cos, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.WHERE:
                    {
                        var output = gm.NonZero(GetInput(0));
                        output = gm.Transpose(output, new[] { 1, 0 });
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.RANK:
                        SetOutput(gm.Constant(GetInputRank(0)));
                        break;
                    case BuiltinOperator.ELU:
                    {
                        var input = GetPermutedInput(0);
                        var output = new PermutedFunctionalTensor(gm.Elu(input.permutedTensor, 1), input.permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.REVERSE_SEQUENCE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.MATRIX_DIAG:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.QUANTIZE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.MATRIX_SET_DIAG:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.ROUND:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Round, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.HARD_SWISH:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.HardSwish, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.IF:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.WHILE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.NON_MAX_SUPPRESSION_V4:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.NON_MAX_SUPPRESSION_V5:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.SCATTER_ND:
                    {
                        var indices = GetInput(0);
                        var updates = GetInput(1);
                        var shape = GetInput(2);
                        var input = gm.ConstantOfShape(shape, updates.partialTensor.dataType, 0f, 0);
                        var output = gm.ScatterND(input, indices, updates, ScatterReductionMode.None);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.SELECT_V2:
                    {
                        var condition = GetInput(0);
                        var x = GetInput(1);
                        var y = GetInput(2);
                        var output = gm.Where(condition, x, y);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.DENSIFY:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.SEGMENT_SUM:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.BATCH_MATMUL:
                    {
                        var options = op.BuiltinOptionsAsBatchMatMulOptions();
                        var input0 = GetInput(0, options.AdjX ? Permutation.Transpose(GetInputRank(0)) : null);
                        var input1 = GetInput(1, options.AdjY ? Permutation.Transpose(GetInputRank(1)) : null);
                        var output = gm.MatMul(input0, input1);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.PLACEHOLDER_FOR_GREATER_OP_CODES:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.CUMSUM:
                    {
                        var options = op.BuiltinOptionsAsCumsumOptions();
                        var reverse = options.Reverse;
                        var exclusive = options.Exclusive;
                        var output = gm.CumSum(GetInput(0), GetInput(1), reverse, exclusive);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.CALL_ONCE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.BROADCAST_TO:
                    {
                        var output = gm.Expand(GetInput(0), GetInput(1));
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.RFFT2D:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.CONV_3D:
                    {
                        var options = op.BuiltinOptionsAsConv3DOptions();
                        var X = GetInput(0, new Permutation(0, 4, 1, 2, 3));
                        var W = GetInput(1, new Permutation(4, 3, 0, 1, 2));
                        var B = GetInput(2);
                        var inputChannels = GetInputShape(0)[^1];
                        var kernelChannels = GetInputShape(1)[^1];
                        var group = (inputChannels != -1 && kernelChannels != -1) ? inputChannels / kernelChannels : 1;
                        var strides = new[] { options.StrideD, options.StrideH, options.StrideW };
                        var dilations = new[] { options.DilationDFactor, options.DilationHFactor, options.DilationWFactor };
                        int[] pads = null;
                        var autoPad = AutoPad.NotSet;
                        if (options.Padding == Padding.SAME)
                        {
                            var inputShape = GetInputShape(0)[1..^1];
                            var kernelShape = GetInputShape(1)[..3];
                            var outputShape = GetOutputShape(0);
                            outputShape = outputShape.Length > 2 ? outputShape[1..^1] : new[] { -1, -1 };
                            pads = GetPads(inputShape, kernelShape, outputShape, dilations, strides);
                            if (pads == null)
                                autoPad = AutoPad.SameUpper;
                        }

                        pads ??= new int[6];

                        var outputPermuted = gm.Conv(X, W, B, autoPad, dilations, group, pads, strides, null, FusableActivation.None);
                        var output = new PermutedFunctionalTensor(outputPermuted, new Permutation(0, 4, 1, 2, 3));
                        output = PermutedActivation(output, options.FusedActivationFunction);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.IMAG:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.REAL:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.COMPLEX_ABS:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.HASHTABLE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.HASHTABLE_FIND:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.HASHTABLE_IMPORT:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.HASHTABLE_SIZE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.REDUCE_ALL:
                    {
                        var options = op.BuiltinOptionsAsReducerOptions();
                        var input = GetPermutedInput(0);
                        var axes = GetInput(1);
                        var keepDims = options.KeepDims;
                        var output = PermutedReduction(gm.ReduceMin, input, axes, keepDims, true);
                        var one = gm.Constant(1);
                        output = new PermutedFunctionalTensor(gm.Min(output.permutedTensor, one), output.permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.CONV_3D_TRANSPOSE:
                    {
                        var options = op.BuiltinOptionsAsConv3DOptions();
                        var X = GetInput(2, new Permutation(0, 4, 1, 2, 3));
                        var W = GetInput(1, new Permutation(4, 3, 0, 1, 2));
                        var B = GetInput(3);
                        var outputShape = GetArray<int>(GetInput(0), "outputShape");
                        var outputChannels = outputShape[^1];
                        var kernelChannels = GetInputShape(1)[3];
                        var group = (outputChannels != -1 && kernelChannels != -1) ? outputChannels / kernelChannels : 1;
                        AssertValue(group, 1, "attribute \"group\"");
                        var strides = new[] { options.StrideD, options.StrideH, options.StrideW };
                        var dilations = new[] { options.DilationDFactor, options.DilationHFactor, options.DilationWFactor };
                        var outputPadding = new int[3];
                        int[] pads = null;
                        var autoPad = AutoPad.NotSet;
                        if (options.Padding == Padding.SAME)
                        {
                            var inputShape = GetInputShape(2);
                            var kernelShape = GetInputShape(1);
                            pads = GetPadsTranspose(inputShape[1..^1], kernelShape[..3], outputShape[1..^1], dilations, strides);
                            if (pads == null)
                                autoPad = AutoPad.SameUpper;
                        }

                        pads ??= new int[6];

                        var outputPermuted = gm.ConvTranspose(X, W, B, autoPad, dilations, 1, outputPadding, pads, strides, null, FusableActivation.None);
                        var output = new PermutedFunctionalTensor(outputPermuted, new Permutation(0, 4, 1, 2, 3));
                        output = PermutedActivation(output, options.FusedActivationFunction);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.VAR_HANDLE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.READ_VARIABLE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.ASSIGN_VARIABLE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.BROADCAST_ARGS:
                    {
                        var a = GetInput(0);
                        var b = GetInput(1);
                        var output = gm.BroadcastArgs(a, b);
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.RANDOM_STANDARD_NORMAL:
                    {
                        var options = op.BuiltinOptionsAsRandomOptions();
                        var seed = GetSeed(options);
                        var mean = 0.0f;
                        var stdDev = 1.0f;
                        var shape = GetInput(0);
                        var input = gm.ConstantOfShape(shape, DataType.Int, 0f, 0);
                        var output = gm.RandomNormalLike(input, mean, stdDev, seed.HasValue, seed.GetValueOrDefault(0));
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.BUCKETIZE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.RANDOM_UNIFORM:
                    {
                        var options = op.BuiltinOptionsAsRandomOptions();
                        var seed = GetSeed(options);
                        var low = 0.0f;
                        var high = 1.0f;
                        var shape = GetInput(0);
                        var input = gm.ConstantOfShape(shape, DataType.Int, 0f, 0);
                        var output = gm.RandomUniformLike(input, low, high, seed.HasValue, seed.GetValueOrDefault(0));
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.MULTINOMIAL:
                    {
                        var options = op.BuiltinOptionsAsRandomOptions();
                        var seed = GetSeed(options);
                        var input = GetInput(0);
                        var count = GetValue<int>(GetInput(1), "count");
                        var output = gm.Multinomial(input, count, seed.HasValue, seed.GetValueOrDefault(0));
                        SetOutput(output);
                        break;
                    }
                    case BuiltinOperator.GELU:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Gelu, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.DYNAMIC_UPDATE_SLICE:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.RELU_0_TO_1:
                    {
                        var input = GetPermutedInput(0);
                        var output = new PermutedFunctionalTensor(gm.Clip(input.permutedTensor, gm.Constant(0f), gm.Constant(1.0f)), input.permutation);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.UNSORTED_SEGMENT_PROD:
                    case BuiltinOperator.UNSORTED_SEGMENT_MIN:
                    case BuiltinOperator.UNSORTED_SEGMENT_MAX:
                    case BuiltinOperator.UNSORTED_SEGMENT_SUM:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.ATAN2:
                    {
                        var y = GetPermutedInput(0);
                        var x = GetPermutedInput(1);
                        var output = PermutedBroadcast((a, b) => gm.Atan2(a, b), y, x);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.SIGN:
                    {
                        var input = GetPermutedInput(0);
                        var output = PermutedUnary(gm.Sign, input);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.BITCAST:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.BITWISE_XOR:
                    {
                        var a = GetPermutedInput(0);
                        var b = GetPermutedInput(1);
                        var output = PermutedBroadcast((x, y) => gm.BitwiseXor(x, y), a, b);
                        SetPermutedOutput(output);
                        break;
                    }
                    case BuiltinOperator.RIGHT_SHIFT:
                        WarnOpNotImplemented();
                        break;
                    case BuiltinOperator.STABLEHLO_LOGISTIC:
                    case BuiltinOperator.STABLEHLO_ADD:
                    case BuiltinOperator.STABLEHLO_DIVIDE:
                    case BuiltinOperator.STABLEHLO_MULTIPLY:
                    case BuiltinOperator.STABLEHLO_MAXIMUM:
                    case BuiltinOperator.STABLEHLO_RESHAPE:
                    case BuiltinOperator.STABLEHLO_CLAMP:
                    case BuiltinOperator.STABLEHLO_CONCATENATE:
                    case BuiltinOperator.STABLEHLO_BROADCAST_IN_DIM:
                    case BuiltinOperator.STABLEHLO_CONVOLUTION:
                    case BuiltinOperator.STABLEHLO_SLICE:
                    case BuiltinOperator.STABLEHLO_CUSTOM_CALL:
                    case BuiltinOperator.STABLEHLO_REDUCE:
                    case BuiltinOperator.STABLEHLO_ABS:
                    case BuiltinOperator.STABLEHLO_AND:
                    case BuiltinOperator.STABLEHLO_COSINE:
                    case BuiltinOperator.STABLEHLO_EXPONENTIAL:
                    case BuiltinOperator.STABLEHLO_FLOOR:
                    case BuiltinOperator.STABLEHLO_LOG:
                    case BuiltinOperator.STABLEHLO_MINIMUM:
                    case BuiltinOperator.STABLEHLO_NEGATE:
                    case BuiltinOperator.STABLEHLO_OR:
                    case BuiltinOperator.STABLEHLO_POWER:
                    case BuiltinOperator.STABLEHLO_REMAINDER:
                    case BuiltinOperator.STABLEHLO_RSQRT:
                    case BuiltinOperator.STABLEHLO_SELECT:
                    case BuiltinOperator.STABLEHLO_SUBTRACT:
                    case BuiltinOperator.STABLEHLO_TANH:
                    case BuiltinOperator.STABLEHLO_SCATTER:
                    case BuiltinOperator.STABLEHLO_COMPARE:
                    case BuiltinOperator.STABLEHLO_CONVERT:
                    case BuiltinOperator.STABLEHLO_DYNAMIC_SLICE:
                    case BuiltinOperator.STABLEHLO_DYNAMIC_UPDATE_SLICE:
                    case BuiltinOperator.STABLEHLO_PAD:
                    case BuiltinOperator.STABLEHLO_IOTA:
                    case BuiltinOperator.STABLEHLO_DOT_GENERAL:
                    case BuiltinOperator.STABLEHLO_REDUCE_WINDOW:
                    case BuiltinOperator.STABLEHLO_SORT:
                    case BuiltinOperator.STABLEHLO_WHILE:
                    case BuiltinOperator.STABLEHLO_GATHER:
                    case BuiltinOperator.STABLEHLO_TRANSPOSE:
                    case BuiltinOperator.DILATE:
                    case BuiltinOperator.STABLEHLO_RNG_BIT_GENERATOR:
                    case BuiltinOperator.REDUCE_WINDOW:
                    case BuiltinOperator.STABLEHLO_COMPOSITE:
                    case BuiltinOperator.STABLEHLO_SHIFT_LEFT:
                    case BuiltinOperator.STABLEHLO_CBRT:
                    case BuiltinOperator.STABLEHLO_CASE:
                    default:
                        WarnOpNotImplemented();
                        break;
                }
            }

            if (ImportWarnings.Any(w => w.messageSeverity == WarningType.Error))
            {
                throw new LiteRTImportException($"Error importing model: {ImportWarnings.Last(w => w.messageSeverity == WarningType.Error).message}");
            }

            Node GetOutputTensor(int index, string name)
            {
                var tensor = tensors[index]?.GetTensor();
                AssertNotNull(tensor, $"Output tensor \"{name}\" not found");
                return tensor;
            }

            var outputNodes = new List<Node>();
            var outputNames = new List<string>();
            // outputs
            if (signatureDef.HasValue)
            {
                for (var i = 0; i < signatureDef.Value.OutputsLength; i++)
                {
                    var tensorMap = signatureDef.Value.Outputs(i).Value;
                    var tensor = GetOutputTensor((int)tensorMap.TensorIndex, tensorMap.Name);
                    outputNodes.Add(tensor);
                    outputNames.Add(tensorMap.Name);
                }
            }
            else
            {
                for (var i = 0; i < subGraph.OutputsLength; i++)
                {
                    var index = subGraph.Outputs(i);
                    var name = subGraph.Tensors(index).Value.Name;
                    var tensor = GetOutputTensor(index, name);
                    outputNodes.Add(tensor);
                    outputNames.Add(name);
                }
            }

            gm.Outputs(outputNames.ToArray(), outputNodes.ToArray());

            if (ImportWarnings.Any(w => w.messageSeverity == WarningType.Error))
            {
                var failureMessage = ImportWarnings.Last(w => w.messageSeverity == WarningType.Error).message;
                throw new LiteRTImportException($"Could not import model due to errors with outputs: {failureMessage}");
            }

            ModelOptimizer.OptimizeGraph(gm);
            model = GraphConverter.GraphToModel(gm);

            return model;
        }

        void ValidateSubGraph(SubGraph subGraph, Model liteModel)
        {
            // Track unsupported operators and datatypes for error handling
            var unsupportedOperators = new HashSet<string>();
            var unsupportedDataTypes = new HashSet<string>();

            for (var i = 0; i < subGraph.TensorsLength; i++)
            {
                var tensor = subGraph.Tensors(i).Value;
                var tensorType = tensor.Type;
                var typeStr = tensorType.ToString();
                if (!tensor.Type.IsDataTypeSupported())
                {
                    unsupportedDataTypes.Add(typeStr);
                    OnLiteRTDataTypeUnsupported?.Invoke(typeStr);
                }

                OnLiteRTDataType?.Invoke(typeStr);
            }

            for (var opIndex = 0; opIndex < subGraph.OperatorsLength; opIndex++)
            {
                var op = subGraph.Operators(opIndex).Value;
                var operatorCode = liteModel.OperatorCodes((int)op.OpcodeIndex).Value;
                var builtinCode = operatorCode.BuiltinCode > BuiltinOperator.PLACEHOLDER_FOR_GREATER_OP_CODES ? operatorCode.BuiltinCode : (BuiltinOperator)operatorCode.DeprecatedBuiltinCode;

                var builtinStr = builtinCode.ToString();
                if (!builtinCode.IsOperatorSupported())
                {
                    unsupportedOperators.Add(builtinStr);
                    OnLiteRTOperatorUnsupported?.Invoke(builtinStr);
                }
                OnLiteRTOperator?.Invoke(builtinStr);
            }

            if (unsupportedOperators.Count > 0)
            {
                Warn(WarningType.Error, $"Model contains unsupported operator(s): {string.Join(", ", unsupportedOperators)}");
            }

            if (unsupportedDataTypes.Count > 0)
            {
                Warn(WarningType.Error, $"Model contains unsupported data type(s): {string.Join(", ", unsupportedDataTypes)}");
            }

            if (unsupportedDataTypes.Count > 0 || unsupportedOperators.Count > 0)
            {
                throw new LiteRTImportException("Model contains unsupported operators or data types. See errors for details.");
            }
        }

        // Determine whether a set of permuted functional tensors share a common permutation
        static bool IsCommonPermutation(PermutedFunctionalTensor[] inputs, out Permutation permutation)
        {
            permutation = default;

            var rank = inputs[0].permutedTensor.partialTensor.shape.rank;
            for (var i = 1; i < inputs.Length; i++)
            {
                if (inputs[i].permutedTensor.partialTensor.shape.rank != rank)
                    return false;
            }

            var isSetPermutation = false;
            foreach (var input in inputs)
            {
                // if the functional tensor is a constant then the permutation will be baked into the graph
                if (input.isConstant)
                    continue;

                if (!isSetPermutation)
                {
                    permutation = input.permutation;
                    isSetPermutation = true;
                    continue;
                }

                if (input.permutation != permutation)
                    return false;
            }

            return true;
        }

        // Returns a permuted tensor with an activation applied.
        // This works because x.Activation().Transpose(perm) == x.Transpose(perm).Activation()
        PermutedFunctionalTensor PermutedActivation(PermutedFunctionalTensor output, ActivationFunctionType activationType)
        {
            return new PermutedFunctionalTensor(Activation(output.permutedTensor, activationType), output.permutation);
        }

        // Returns a permuted tensor with a unary (elementwise) op applied.
        // This works because x.Unary().Transpose(perm) == x.Transpose(perm).Unary()
        PermutedFunctionalTensor PermutedUnary(Func<Node, Node> unary, PermutedFunctionalTensor input)
        {
            return new PermutedFunctionalTensor(unary(input.permutedTensor), input.permutation, input.isConstant);
        }

        // Returns a permuted tensor as the result of doing a numpy-style broadcast op with two permuted input tensors.
        // This is sometimes possible without applying extra transposes, e.g. if both inputs have the same permutation or one of the inputs is constant.
        PermutedFunctionalTensor PermutedBroadcast(Func<Node, Node, Node> broadcast, PermutedFunctionalTensor a, PermutedFunctionalTensor b)
        {
            if (a.isConstant && a.permutedTensor.partialTensor.shape.rank < b.permutedTensor.partialTensor.shape.rank)
                a = a.GetPermutedTensorForBroadcastOp(b.permutation);

            if (b.isConstant && b.permutedTensor.partialTensor.shape.rank < a.permutedTensor.partialTensor.shape.rank)
                b = b.GetPermutedTensorForBroadcastOp(a.permutation);

            if (IsCommonPermutation(new[] { a, b }, out var permutation))
                return new PermutedFunctionalTensor(broadcast(a.GetTensor(permutation), b.GetTensor(permutation)), permutation);

            return new PermutedFunctionalTensor(broadcast(a.GetTensor(), b.GetTensor()));
        }

        // Returns a permuted tensor as the result of doing a reduction along one or more dimensions.
        PermutedFunctionalTensor PermutedReduction(Func<Node, Node, bool, bool, Node> reduction, PermutedFunctionalTensor input, Node axes, bool keepdims, bool noopWithEmptyAxes)
        {
            var gm = axes.graph.owningModule;
            axes = BroadcastToRank(axes, 1);

            if (keepdims && !input.permutation.IsIdentity())
            {
                axes = gm.Gather(gm.Constant(input.permutation.Inverse().ToArray()), axes, 0);
                return new PermutedFunctionalTensor(reduction(input.permutedTensor, axes, keepdims, noopWithEmptyAxes), input.permutation);
            }

            // if keepdims is false then the output permutation won't be easy to calculate so we just do the transpose before the reduction
            return new PermutedFunctionalTensor(reduction(input.GetTensor(), axes, keepdims, noopWithEmptyAxes));
        }

        Node Activation(Node output, ActivationFunctionType activationType)
        {
            var gm = output.graph.owningModule;
            switch (activationType)
            {
                case ActivationFunctionType.NONE:
                    break;
                case ActivationFunctionType.RELU:
                    output = gm.Relu(output);
                    break;
                case ActivationFunctionType.RELU6:
                    output = gm.Relu6(output);
                    break;
                case ActivationFunctionType.TANH:
                    output = gm.Tanh(output);
                    break;
                case ActivationFunctionType.RELU_N1_TO_1:
                    output = gm.Clip(output, gm.Constant(-1f), gm.Constant(1f));
                    break;
                default:
                    // SIGN_BIT
                    Warn(WarningType.Warning, $"Activation function {activationType} not supported");
                    break;
            }

            return output;
        }

        public static Node BroadcastToRank(Node tensor, int rank, bool append = false)
        {
            var gm = tensor.graph.owningModule;
            var numNewDims = rank - tensor.partialTensor.shape.rank;
            Assert.IsTrue(numNewDims >= 0);
            if (numNewDims == 0)
                return tensor;
            int[] unsqueezeAxes;
            if (append)
            {
                var r = rank - 1;
                unsqueezeAxes = numNewDims switch
                {
                    1 => new[] { r },
                    2 => new[] { r, r - 1 },
                    3 => new[] { r, r - 1, r - 2 },
                    4 => new[] { r, r - 1, r - 2, r - 3 },
                    5 => new[] { r, r - 1, r - 2, r - 3, r - 4 },
                    6 => new[] { r, r - 1, r - 2, r - 3, r - 4, r - 5 },
                    7 => new[] { r, r - 1, r - 2, r - 3, r - 4, r - 5, r - 6 },
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
            else
            {
                unsqueezeAxes = numNewDims switch
                {
                    1 => new[] { 0 },
                    2 => new[] { 0, 1 },
                    3 => new[] { 0, 1, 2 },
                    4 => new[] { 0, 1, 2, 3 },
                    5 => new[] { 0, 1, 2, 3, 4 },
                    6 => new[] { 0, 1, 2, 3, 4, 5 },
                    7 => new[] { 0, 1, 2, 3, 4, 5, 6 },
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            return gm.Unsqueeze(tensor, gm.Constant(unsqueezeAxes));
        }

        static Node MoveDim(Node tensor, int from, int to)
        {
            var gm = tensor.graph.owningModule;
            var perm = Enumerable.Range(0, tensor.partialTensor.shape.rank).ToList();
            from = tensor.partialTensor.shape.Axis(from);
            to = tensor.partialTensor.shape.Axis(to);
            var x = perm[from];
            perm.RemoveAt(from);
            perm.Insert(to, x);
            return gm.Transpose(tensor, perm.ToArray());
        }

        static int[] GetPads(int[] inputShape, int[] kernelShape, int[] outputShape, int[] dilations, int[] strides)
        {
            var spatial = inputShape.Length;
            var pads = new int[2 * spatial];
            for (var i = 0; i < spatial; i++)
            {
                if (inputShape[i] < 0 || kernelShape[i] < 0 || outputShape[i] < 0)
                    return null;
                var effectiveKernelSize = dilations[i] * (kernelShape[i] - 1) + 1;
                var pad = (outputShape[i] - 1) * strides[i] + effectiveKernelSize - inputShape[i];
                pad = Mathf.Max(pad, 0);

                pads[i] = pad / 2;
                pads[i + spatial] = pad - pad / 2;
            }

            return pads;
        }

        static int[] GetPadsTranspose(int[] inputShape, int[] kernelShape, int[] outputShape, int[] dilations, int[] strides)
        {
            var spatial = inputShape.Length;
            var pads = new int[2 * spatial];
            for (var i = 0; i < spatial; i++)
            {
                if (inputShape[i] < 0 || kernelShape[i] < 0 || outputShape[i] < 0)
                    return null;
                var effectiveKernelSize = dilations[i] * (kernelShape[i] - 1) + 1;
                var pad = strides[i] * (inputShape[i] - 1) + effectiveKernelSize - outputShape[i];
                pads[i] = pad / 2;
                pads[i + spatial] = pad - pad / 2;
            }

            return pads;
        }

        void AssertNotNull(object obj, string msg)
        {
            if (obj == null)
            {
                throw new LiteRTImportException(msg);
            }
        }
    }

    /// <summary>
    /// Represents an exception during the import of a LiteRT model.
    /// </summary>
    class LiteRTImportException : ImportException
    {
        /// <inheritdoc cref="ImportException"/>
        public LiteRTImportException(string message)
            : base(message) { }
    }

    /// <summary>
    /// Represents an exception during the import of a LiteRT layer.
    /// </summary>
    class LiteRTLayerImportException : LayerImportException
    {
        /// <inheritdoc cref="LayerImportException"/>
        public LiteRTLayerImportException(BuiltinOperator builtinCode, string message)
            : base($"{builtinCode}: {message}") { }
    }
}
