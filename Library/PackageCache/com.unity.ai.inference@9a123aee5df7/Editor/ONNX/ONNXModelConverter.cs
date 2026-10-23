using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Unity.InferenceEngine.Graph;
using Unity.Mathematics;

namespace Unity.InferenceEngine.Editor.Onnx
{

    /// <summary>
    /// Represents a converter from an ONNX model to Sentis format.
    /// </summary>
    class ONNXModelConverter : ModelConverterBase
    {
        /// <summary>
        /// Occurs when the metadata of the ONNX model is loaded.
        /// </summary>
        /// <remarks>
        /// This event is triggered during the conversion of an ONNX model to Sentis format, when
        /// <see cref="Convert"/> is called. The event handler receives an argument of type
        /// <see cref="ONNXModelMetadata"/> containing metadata loaded from ONNX model.
        /// </remarks>
        public event Action<ONNXModelMetadata> MetadataLoaded;

        internal event Action<ModelProto> OnOnnxModelProtoLoaded;
        internal event Action<string> OnOnnxOperator;
        internal event Action<string> OnOnnxOperatorUnsupported;
        internal event Action<string> OnOnnxDataType;
        internal event Action<string> OnOnnxDataTypeUnsupported;

        internal Dictionary<string, int> DynamicDimConfigs = new();

        /// <summary>
        /// Converts an ONNX model to a Sentis `Model` object.
        /// </summary>
        /// <returns>The converted Sentis model.</returns>
        public override Model Convert()
        {
            // Use stream-based parsing to avoid loading entire file into memory
            // This supports models larger than 2GB
            using var readStream = new FileStream(m_FilePath, FileMode.Open, FileAccess.Read);
            using var inputStream = new CodedInputStream(readStream);

            var onnxModel = new ModelProto();
            onnxModel.MergeFrom(inputStream);

            OnOnnxModelProtoLoaded?.Invoke(onnxModel);
            return ConvertOnnxModel(onnxModel);
        }

        /// <summary>
        /// Initializes and returns an instance of `ONNXModelConverter`.
        /// </summary>
        /// <param name="filePath">The path of the asset to convert.</param>
        public ONNXModelConverter(string filePath)
            : base(filePath) { }

        internal static readonly Dictionary<string, ONNXOperatorType> s_OperatorTypeMap = new()
        {
            { "Constant", ONNXOperatorType.Constant },
            { "Celu", ONNXOperatorType.Celu },
            { "Elu", ONNXOperatorType.Elu },
            { "Erf", ONNXOperatorType.Erf },
            { "Gelu", ONNXOperatorType.Gelu },
            { "Hardmax", ONNXOperatorType.Hardmax },
            { "HardSigmoid", ONNXOperatorType.HardSigmoid },
            { "HardSwish", ONNXOperatorType.HardSwish },
            { "LeakyRelu", ONNXOperatorType.LeakyRelu },
            { "Mish", ONNXOperatorType.Mish },
            { "PRelu", ONNXOperatorType.PRelu },
            { "Relu", ONNXOperatorType.Relu },
            { "Selu", ONNXOperatorType.Selu },
            { "Sigmoid", ONNXOperatorType.Sigmoid },
            { "Softplus", ONNXOperatorType.Softplus },
            { "Softsign", ONNXOperatorType.Softsign },
            { "Tanh", ONNXOperatorType.Tanh },
            { "ThresholdedRelu", ONNXOperatorType.ThresholdedRelu },
            { "LogSoftmax", ONNXOperatorType.LogSoftmax },
            { "Softmax", ONNXOperatorType.Softmax },
            { "Conv", ONNXOperatorType.Conv },
            { "ConvTranspose", ONNXOperatorType.ConvTranspose },
            { "Shape", ONNXOperatorType.Shape },
            { "Size", ONNXOperatorType.Size },
            { "ConstantOfShape", ONNXOperatorType.ConstantOfShape },
            { "Range", ONNXOperatorType.Range },
            { "OneHot", ONNXOperatorType.OneHot },
            { "ArgMax", ONNXOperatorType.ArgMax },
            { "ArgMin", ONNXOperatorType.ArgMin },
            { "Gather", ONNXOperatorType.Gather },
            { "GatherElements", ONNXOperatorType.GatherElements },
            { "GatherND", ONNXOperatorType.GatherND },
            { "NonZero", ONNXOperatorType.NonZero },
            { "Scatter", ONNXOperatorType.Scatter },
            { "ScatterElements", ONNXOperatorType.ScatterElements },
            { "ScatterND", ONNXOperatorType.ScatterND },
            { "TopK", ONNXOperatorType.TopK },
            { "And", ONNXOperatorType.And },
            { "Compress", ONNXOperatorType.Compress },
            { "Equal", ONNXOperatorType.Equal },
            { "Greater", ONNXOperatorType.Greater },
            { "GreaterOrEqual", ONNXOperatorType.GreaterOrEqual },
            { "IsInf", ONNXOperatorType.IsInf },
            { "IsNaN", ONNXOperatorType.IsNaN },
            { "Less", ONNXOperatorType.Less },
            { "LessOrEqual", ONNXOperatorType.LessOrEqual },
            { "Not", ONNXOperatorType.Not },
            { "Or", ONNXOperatorType.Or },
            { "Xor", ONNXOperatorType.Xor },
            { "Where", ONNXOperatorType.Where },
            { "Abs", ONNXOperatorType.Abs },
            { "Add", ONNXOperatorType.Add },
            { "BitwiseAnd", ONNXOperatorType.BitwiseAnd },
            { "BitwiseNot", ONNXOperatorType.BitwiseNot },
            { "BitwiseOr", ONNXOperatorType.BitwiseOr },
            { "BitwiseXor", ONNXOperatorType.BitwiseXor },
            { "Ceil", ONNXOperatorType.Ceil },
            { "Clip", ONNXOperatorType.Clip },
            { "CumSum", ONNXOperatorType.CumSum },
            { "Div", ONNXOperatorType.Div },
            { "Einsum", ONNXOperatorType.Einsum },
            { "Exp", ONNXOperatorType.Exp },
            { "Floor", ONNXOperatorType.Floor },
            { "Gemm", ONNXOperatorType.Gemm },
            { "Log", ONNXOperatorType.Log },
            { "MatMul", ONNXOperatorType.MatMul },
            { "Max", ONNXOperatorType.Max },
            { "Mean", ONNXOperatorType.Mean },
            { "Min", ONNXOperatorType.Min },
            { "Mod", ONNXOperatorType.Mod },
            { "Mul", ONNXOperatorType.Mul },
            { "Neg", ONNXOperatorType.Neg },
            { "Pow", ONNXOperatorType.Pow },
            { "Reciprocal", ONNXOperatorType.Reciprocal },
            { "Round", ONNXOperatorType.Round },
            { "Shrink", ONNXOperatorType.Shrink },
            { "Sign", ONNXOperatorType.Sign },
            { "Sqrt", ONNXOperatorType.Sqrt },
            { "Sub", ONNXOperatorType.Sub },
            { "Sum", ONNXOperatorType.Sum },
            { "BatchNormalization", ONNXOperatorType.BatchNormalization },
            { "InstanceNormalization", ONNXOperatorType.InstanceNormalization },
            { "LayerNormalization", ONNXOperatorType.LayerNormalization },
            { "RMSNormalization", ONNXOperatorType.RMSNormalization },
            { "LRN", ONNXOperatorType.LRN },
            { "NonMaxSuppression", ONNXOperatorType.NonMaxSuppression },
            { "RoiAlign", ONNXOperatorType.RoiAlign },
            { "AveragePool", ONNXOperatorType.AveragePool },
            { "GlobalAveragePool", ONNXOperatorType.GlobalAveragePool },
            { "GlobalMaxPool", ONNXOperatorType.GlobalMaxPool },
            { "MaxPool", ONNXOperatorType.MaxPool },
            { "Bernoulli", ONNXOperatorType.Bernoulli },
            { "Multinomial", ONNXOperatorType.Multinomial },
            { "RandomNormal", ONNXOperatorType.RandomNormal },
            { "RandomNormalLike", ONNXOperatorType.RandomNormalLike },
            { "RandomUniform", ONNXOperatorType.RandomUniform },
            { "RandomUniformLike", ONNXOperatorType.RandomUniformLike },
            { "LSTM", ONNXOperatorType.LSTM },
            { "ReduceL1", ONNXOperatorType.ReduceL1 },
            { "ReduceL2", ONNXOperatorType.ReduceL2 },
            { "ReduceLogSum", ONNXOperatorType.ReduceLogSum },
            { "ReduceLogSumExp", ONNXOperatorType.ReduceLogSumExp },
            { "ReduceMax", ONNXOperatorType.ReduceMax },
            { "ReduceMean", ONNXOperatorType.ReduceMean },
            { "ReduceMin", ONNXOperatorType.ReduceMin },
            { "ReduceProd", ONNXOperatorType.ReduceProd },
            { "ReduceSum", ONNXOperatorType.ReduceSum },
            { "ReduceSumSquare", ONNXOperatorType.ReduceSumSquare },
            { "BlackmanWindow", ONNXOperatorType.BlackmanWindow },
            { "DFT", ONNXOperatorType.DFT },
            { "HammingWindow", ONNXOperatorType.HammingWindow },
            { "HannWindow", ONNXOperatorType.HannWindow },
            { "MelWeightMatrix", ONNXOperatorType.MelWeightMatrix },
            { "STFT", ONNXOperatorType.STFT },
            { "Cast", ONNXOperatorType.Cast },
            { "CastLike", ONNXOperatorType.CastLike },
            { "Concat", ONNXOperatorType.Concat },
            { "DepthToSpace", ONNXOperatorType.DepthToSpace },
            { "Expand", ONNXOperatorType.Expand },
            { "Flatten", ONNXOperatorType.Flatten },
            { "GridSample", ONNXOperatorType.GridSample },
            { "Dropout", ONNXOperatorType.Dropout },
            { "Identity", ONNXOperatorType.Identity },
            { "Pad", ONNXOperatorType.Pad },
            { "Reshape", ONNXOperatorType.Reshape },
            { "Resize", ONNXOperatorType.Resize },
            { "Slice", ONNXOperatorType.Slice },
            { "SpaceToDepth", ONNXOperatorType.SpaceToDepth },
            { "Split", ONNXOperatorType.Split },
            { "Squeeze", ONNXOperatorType.Squeeze },
            { "Tile", ONNXOperatorType.Tile },
            { "Transpose", ONNXOperatorType.Transpose },
            { "Trilu", ONNXOperatorType.Trilu },
            { "Upsample", ONNXOperatorType.Upsample },
            { "Unsqueeze", ONNXOperatorType.Unsqueeze },
            { "Acos", ONNXOperatorType.Acos },
            { "Acosh", ONNXOperatorType.Acosh },
            { "Asin", ONNXOperatorType.Asin },
            { "Asinh", ONNXOperatorType.Asinh },
            { "Atan", ONNXOperatorType.Atan },
            { "Atanh", ONNXOperatorType.Atanh },
            { "Cos", ONNXOperatorType.Cos },
            { "Cosh", ONNXOperatorType.Cosh },
            { "Sin", ONNXOperatorType.Sin },
            { "Sinh", ONNXOperatorType.Sinh },
            { "Tan", ONNXOperatorType.Tan },
            { "Swish", ONNXOperatorType.Swish },
            { "ImageScaler", ONNXOperatorType.ImageScaler }
        };

        /// <summary>
        /// Maps a string operator type to its corresponding enum value.
        /// </summary>
        /// <param name="opTypeString">The operator type string from ONNX.</param>
        /// <returns>The corresponding ONNXOperatorType enum value, or Unknown if not found.</returns>
        internal static ONNXOperatorType MapOperatorType(string opTypeString)
        {
            return s_OperatorTypeMap.TryGetValue(opTypeString, out var opType) ? opType : ONNXOperatorType.Unknown;
        }

        /// <summary>
        /// Checks if an ONNX operator is supported by Sentis.
        /// </summary>
        public static bool IsOperatorSupported(string opTypeString)
        {
            return MapOperatorType(opTypeString) != ONNXOperatorType.Unknown;
        }

        void OnNode(GraphModule gm, Dictionary<string, Node> tensors, long defaultOpsetVersion, ONNXNodeWrapper node)
        {
            Node GetInput(int index)
            {
                if (index >= node.InputCount || string.IsNullOrEmpty(node.Inputs[index]))
                    return null;
                return tensors[node.Inputs[index]];
            }

            Node[] GetInputs()
            {
                var inputs = new Node[node.InputCount];
                for (var i = 0; i < node.InputCount; i++)
                    inputs[i] = GetInput(i);
                return inputs;
            }

            void SetOutput(Node output, int index = 0)
            {
                if (index >= node.OutputCount || string.IsNullOrEmpty(node.Outputs[index]))
                    return;
                tensors[node.Outputs[index]] = output;
            }

            void SetOutputs(Node[] outputs)
            {
                for (var i = 0; i < outputs.Length; i++)
                    SetOutput(outputs[i], i);
            }

            var opTypeString = node.OperatorType;
            var opType = MapOperatorType(opTypeString);
            switch (opType)
            {
                case ONNXOperatorType.Constant when node.HasAttribute("value"):
                {
                    var constantTensor = ONNXConstantsLoader.LoadConstant(node.GetRequiredTensor("value"), m_DirectoryPath);
                    var constantNode = gm.Constant(constantTensor);
                    SetOutput(constantNode);
                    break;
                }
                case ONNXOperatorType.Constant when node.HasAttribute("value_float"):
                {
                    var value = node.GetRequiredFloat("value_float");
                    var constantNode = gm.Constant(value);
                    SetOutput(constantNode);
                    break;
                }
                case ONNXOperatorType.Constant when node.HasAttribute("value_floats"):
                {
                    var values = node.GetRequiredFloatArray("value_floats");
                    var constant = gm.Constant(values);
                    SetOutput(constant);
                    break;
                }
                case ONNXOperatorType.Constant when node.HasAttribute("value_int"):
                {
                    var value = node.GetRequiredInt("value_int");
                    var constant = gm.Constant(value);
                    SetOutput(constant);
                    break;
                }
                case ONNXOperatorType.Constant when node.HasAttribute("value_ints"):
                {
                    var values = node.GetRequiredIntArray("value_ints");
                    var constant = gm.Constant(values);
                    SetOutput(constant);
                    break;
                }
                case ONNXOperatorType.Constant:
                    node.UnsupportedAttribute("sparse_value");
                    node.UnsupportedAttribute("value_string");
                    node.UnsupportedAttribute("value_strings");
                    Warn(WarningType.Error, $"<b>{opTypeString}</b>: Required attribute `<b>value</b>`, `<b>value_int(s)</b>` or `<b>value_float(s)</b>`");
                    break;

                // Layer.Activation
                case ONNXOperatorType.Celu:
                {
                    var alpha = node.GetOptionalFloat("alpha", 1f);
                    SetOutput(gm.Celu(GetInput(0), alpha));
                    break;
                }
                case ONNXOperatorType.Elu:
                {
                    var alpha = node.GetOptionalFloat("alpha", 1f);
                    SetOutput(gm.Elu(GetInput(0), alpha));
                    break;
                }
                case ONNXOperatorType.Erf:
                {
                    SetOutput(gm.Erf(GetInput(0)));
                    break;
                }
                case ONNXOperatorType.Gelu:
                {
                    var approximate = node.GetOptionalString("approximate", "none");
                    if (approximate.Equals("tanh"))
                        SetOutput(gm.GeluFast(GetInput(0)));
                    else
                        SetOutput(gm.Gelu(GetInput(0)));
                    break;
                }
                case ONNXOperatorType.Hardmax:
                {
                    var axis = node.GetOptionalInt("axis", -1);
                    SetOutput(gm.Hardmax(GetInput(0), axis));
                    break;
                }
                case ONNXOperatorType.HardSigmoid:
                {
                    var alpha = node.GetOptionalFloat("alpha", 0.2f);
                    var beta = node.GetOptionalFloat("beta", 0.5f);
                    SetOutput(gm.HardSigmoid(GetInput(0), alpha, beta));
                    break;
                }
                case ONNXOperatorType.HardSwish:
                {
                    SetOutput(gm.HardSwish(GetInput(0)));
                    break;
                }
                case ONNXOperatorType.Swish:
                {
                    var alpha = node.GetOptionalFloat("alpha", 1f);
                    SetOutput(gm.Swish(GetInput(0), alpha));
                    break;
                }
                case ONNXOperatorType.LeakyRelu:
                {
                    var alpha = node.GetOptionalFloat("alpha", 0.01f);
                    SetOutput(gm.LeakyRelu(GetInput(0), alpha));
                    break;
                }
                case ONNXOperatorType.Mish:
                {
                    SetOutput(gm.Mish(GetInput(0)));
                    break;
                }
                case ONNXOperatorType.PRelu:
                {
                    SetOutput(gm.PRelu(GetInput(0), GetInput(1)));
                    break;
                }
                case ONNXOperatorType.Relu:
                {
                    SetOutput(gm.Relu(GetInput(0)));
                    break;
                }
                case ONNXOperatorType.Selu:
                {
                    var alpha = node.GetOptionalFloat("alpha", defaultOpsetVersion < 6 ? 1.6732f : 1.67326319f);
                    var gamma = node.GetOptionalFloat("gamma", defaultOpsetVersion < 6 ? 1.0507f : 1.05070102f);
                    SetOutput(gm.Selu(GetInput(0), alpha, gamma));
                    break;
                }
                case ONNXOperatorType.Sigmoid:
                {
                    SetOutput(gm.Sigmoid(GetInput(0)));
                    break;
                }
                case ONNXOperatorType.Softplus:
                {
                    SetOutput(gm.Softplus(GetInput(0)));
                    break;
                }
                case ONNXOperatorType.Softsign:
                {
                    SetOutput(gm.Softsign(GetInput(0)));
                    break;
                }
                case ONNXOperatorType.Tanh:
                {
                    SetOutput(gm.Tanh(GetInput(0)));
                    break;
                }
                case ONNXOperatorType.ThresholdedRelu:
                {
                    var alpha = node.GetOptionalFloat("alpha", 1f);
                    SetOutput(gm.ThresholdedRelu(GetInput(0), alpha));
                    break;
                }

                // Layer.ActivationNonLinear
                case ONNXOperatorType.LogSoftmax:
                {
                    var axis = node.GetOptionalInt("axis", -1);
                    SetOutput(gm.LogSoftmax(GetInput(0), axis));
                    break;
                }
                case ONNXOperatorType.Softmax:
                {
                    var axis = node.GetOptionalInt("axis", -1);
                    SetOutput(gm.Softmax(GetInput(0), axis));
                    break;
                }

                // Layer.Convolution
                case ONNXOperatorType.Conv:
                {
                    // Conv-1, Conv-11

                    var autoPadString = node.GetOptionalString("auto_pad", "NOTSET");
                    var autoPad = autoPadString switch
                    {
                        "NOTSET" => Layers.AutoPad.NotSet,
                        "VALID" => Layers.AutoPad.Valid,
                        "SAME_UPPER" => Layers.AutoPad.SameUpper,
                        "SAME_LOWER" => Layers.AutoPad.SameLower,
                        _ => Warn(WarningType.Warning, $"auto_pad `{autoPadString}` is not supported for Conv, using `NOTSET`.", Layers.AutoPad.NotSet)
                    };
                    var dilations = node.GetOptionalIntArray("dilations", new[] { 1, 1, 1, 1, 1, 1 });
                    var group = node.GetOptionalInt("group", 1);
                    var pads = node.GetOptionalIntArray("pads", new int[12]);
                    var strides = node.GetOptionalIntArray("strides", new[] { 1, 1, 1, 1, 1, 1 });
                    var kernelShape = node.GetOptionalIntArray("kernel_shape", null);

                    SetOutput(gm.Conv(GetInput(0), GetInput(1), GetInput(2), autoPad, dilations, group, pads, strides, kernelShape, Layers.FusableActivation.None));
                    break;
                }
                case ONNXOperatorType.ConvTranspose:
                {
                    // ConvTranspose-1, ConvTranspose-11

                    node.UnsupportedAttribute("output_shape", "null");

                    var outputPadding = node.GetOptionalIntArray("output_padding", new[] { 0, 0, 0, 0, 0, 0 });
                    var autoPadString = node.GetOptionalString("auto_pad", "NOTSET");
                    var autoPad = autoPadString switch
                    {
                        "NOTSET" => Layers.AutoPad.NotSet,
                        "VALID" => Layers.AutoPad.Valid,
                        "SAME_UPPER" => Layers.AutoPad.SameUpper,
                        "SAME_LOWER" => Layers.AutoPad.SameLower,
                        _ => Warn(WarningType.Warning, $"auto_pad `{autoPadString}` is not supported for ConvTranspose, using `NOTSET`.", Layers.AutoPad.NotSet)
                    };
                    var kernelShape = node.GetOptionalIntArray("kernel_shape", null);
                    var dilations = node.GetOptionalIntArray("dilations", new[] { 1, 1, 1, 1, 1, 1 });
                    var group = node.GetOptionalInt("group", 1);
                    var pads = node.GetOptionalIntArray("pads", new int[12]);
                    var strides = node.GetOptionalIntArray("strides", new[] { 1, 1, 1, 1, 1, 1 });

                    SetOutput(gm.ConvTranspose(GetInput(0), GetInput(1), GetInput(2), autoPad, dilations, group, outputPadding, pads, strides, kernelShape, Layers.FusableActivation.None));
                    break;
                }

                // Layer.Dimension
                case ONNXOperatorType.Shape:
                {
                    // Shape-1, Shape-13, Shape-15
                    var start = node.GetOptionalInt("start", 0);
                    var end = node.GetOptionalInt("end", TensorShape.maxRank);
                    SetOutput(gm.Shape(GetInput(0), start, end));
                    break;
                }
                case ONNXOperatorType.Size:
                    // Size-1, Size-13
                    SetOutput(gm.Size(GetInput(0)));
                    break;

                // Layer.Generator
                case ONNXOperatorType.ConstantOfShape:
                {
                    UnityEngine.Debug.Assert(node.InputCount > 0);

                    if (!node.HasAttribute("value"))
                    {
                        SetOutput(gm.ConstantOfShape(GetInput(0), DataType.Float, 0.0f, 0));
                        return;
                    }

                    var constantTensor = ONNXConstantsLoader.LoadConstant(node.GetRequiredTensor("value"), m_DirectoryPath);
                    if (constantTensor.dataType == DataType.Int)
                    {
                        var value = constantTensor.AsSpan<int>()[0];
                        SetOutput(gm.ConstantOfShape(GetInput(0), DataType.Int, 0f, value));
                    }
                    else if (constantTensor.dataType == DataType.Float)
                    {
                        var value = constantTensor.AsSpan<float>()[0];
                        SetOutput(gm.ConstantOfShape(GetInput(0), DataType.Float, value, 0));
                    }

                    break;
                }
                case ONNXOperatorType.Range:
                {
                    SetOutput(gm.Range(GetInput(0), GetInput(1), GetInput(2)));
                    break;
                }
                case ONNXOperatorType.OneHot:
                {
                    // OneHot-9, OneHot-11
                    var axis = node.GetOptionalInt("axis", -1);
                    var allowNegativeIndexes = true;
                    SetOutput(gm.OneHot(GetInput(0), GetInput(1), GetInput(2), axis, allowNegativeIndexes));
                    break;
                }

                // Layer.Indexing
                case ONNXOperatorType.ArgMax:
                {
                    var axis = node.GetOptionalInt("axis", 0);
                    var keepdims = node.GetOptionalInt("keepdims", 1) == 1;
                    var selectLastIndex = node.GetOptionalInt("select_last_index", 0) == 1;
                    SetOutput(gm.ArgMax(GetInput(0), axis, keepdims, selectLastIndex));
                    break;
                }
                case ONNXOperatorType.ArgMin:
                {
                    var axis = node.GetOptionalInt("axis", 0);
                    var keepdims = node.GetOptionalInt("keepdims", 1) == 1;
                    var selectLastIndex = node.GetOptionalInt("select_last_index", 0) == 1;
                    SetOutput(gm.ArgMin(GetInput(0), axis, keepdims, selectLastIndex));
                    break;
                }
                case ONNXOperatorType.Gather:
                {
                    var axis = node.GetOptionalInt("axis", 0);
                    SetOutput(gm.Gather(GetInput(0), GetInput(1), axis));
                    break;
                }
                case ONNXOperatorType.GatherElements:
                {
                    var axis = node.GetOptionalInt("axis", 0);
                    SetOutput(gm.GatherElements(GetInput(0), GetInput(1), axis));
                    break;
                }
                case ONNXOperatorType.GatherND:
                {
                    var batchDims = node.GetOptionalInt("batch_dims", 0);
                    SetOutput(gm.GatherND(GetInput(0), GetInput(1), batchDims));
                    break;
                }
                case ONNXOperatorType.NonZero:
                {
                    SetOutput(gm.NonZero(GetInput(0)));
                    break;
                }
                case ONNXOperatorType.Scatter:
                {
                    // Scatter-9 maps to ScatterElements
                    var axis = node.GetOptionalInt("axis", 0);
                    SetOutput(gm.ScatterElements(GetInput(0), GetInput(1), GetInput(2), axis, Layers.ScatterReductionMode.None));
                    break;
                }
                case ONNXOperatorType.ScatterElements:
                {
                    var axis = node.GetOptionalInt("axis", 0);
                    var reductionString = node.GetOptionalString("reduction", "none");
                    var reduction = reductionString switch
                    {
                        "none" => Layers.ScatterReductionMode.None,
                        "add" => Layers.ScatterReductionMode.Add,
                        "mul" => Layers.ScatterReductionMode.Mul,
                        "max" => Layers.ScatterReductionMode.Max,
                        "min" => Layers.ScatterReductionMode.Min,
                        _ => Warn(WarningType.Warning, $"reduction `{reductionString}` is not supported for ScatterElements, using `none`.", Layers.ScatterReductionMode.None)
                    };

                    SetOutput(gm.ScatterElements(GetInput(0), GetInput(1), GetInput(2), axis, reduction));
                    break;
                }
                case ONNXOperatorType.ScatterND:
                {
                    var reductionString = node.GetOptionalString("reduction", "none");
                    var reduction = reductionString switch
                    {
                        "none" => Layers.ScatterReductionMode.None,
                        "add" => Layers.ScatterReductionMode.Add,
                        "mul" => Layers.ScatterReductionMode.Mul,
                        "max" => Layers.ScatterReductionMode.Max,
                        "min" => Layers.ScatterReductionMode.Min,
                        _ => Warn(WarningType.Warning, $"reduction `{reductionString}` is not supported for ScatterND, using `none`.", Layers.ScatterReductionMode.None)
                    };

                    SetOutput(gm.ScatterND(GetInput(0), GetInput(1), GetInput(2), reduction));
                    break;
                }
                case ONNXOperatorType.TopK:
                {
                    var axis = node.GetOptionalInt("axis", -1);
                    var largest = node.GetOptionalInt("largest", 1) == 1;
                    var sorted = node.GetOptionalInt("sorted", 1) == 1;
                    if (defaultOpsetVersion < 10)
                    {
                        // TopK-1
                        var kValue = node.GetRequiredInt("k");
                        var k = gm.Constant(new[] { kValue });
                        SetOutputs(gm.TopK(GetInput(0), k, axis, largest, sorted));
                    }
                    else
                    {
                        // TopK-10, TopK-11
                        SetOutputs(gm.TopK(GetInput(0), GetInput(1), axis, largest, sorted));
                    }

                    break;
                }

                // Layer.Logical
                case ONNXOperatorType.And:
                {
                    SetOutput(gm.And(GetInput(0), GetInput(1)));
                    break;
                }
                case ONNXOperatorType.Compress:
                {
                    var hasAxis = node.HasAttribute("axis");
                    var axis = node.GetOptionalInt("axis", 0);
                    SetOutput(gm.Compress(GetInput(0), GetInput(1), hasAxis, axis));
                    break;
                }
                case ONNXOperatorType.Equal:
                    SetOutput(gm.Equal(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.Greater:
                    SetOutput(gm.Greater(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.GreaterOrEqual:
                    SetOutput(gm.GreaterOrEqual(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.IsInf:
                {
                    var detectNegative = node.GetOptionalInt("detect_negative", 1) != 0;
                    var detectPositive = node.GetOptionalInt("detect_positive", 1) != 0;
                    SetOutput(gm.IsInf(GetInput(0), detectNegative, detectPositive));
                    break;
                }
                case ONNXOperatorType.IsNaN:
                    SetOutput(gm.IsNaN(GetInput(0)));
                    break;
                case ONNXOperatorType.Less:
                    SetOutput(gm.Less(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.LessOrEqual:
                    SetOutput(gm.LessOrEqual(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.Not:
                    SetOutput(gm.Not(GetInput(0)));
                    break;
                case ONNXOperatorType.Or:
                    SetOutput(gm.Or(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.Xor:
                    SetOutput(gm.Xor(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.Where:
                    SetOutput(gm.Where(GetInput(0), GetInput(1), GetInput(2)));
                    break;

                // Layer.Math
                case ONNXOperatorType.Abs:
                    SetOutput(gm.Abs(GetInput(0)));
                    break;
                case ONNXOperatorType.Add:
                    SetOutput(gm.Add(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.BitwiseAnd:
                    SetOutput(gm.BitwiseAnd(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.BitwiseNot:
                    SetOutput(gm.BitwiseNot(GetInput(0)));
                    break;
                case ONNXOperatorType.BitwiseOr:
                    SetOutput(gm.BitwiseOr(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.BitwiseXor:
                    SetOutput(gm.BitwiseXor(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.Ceil:
                    SetOutput(gm.Ceil(GetInput(0)));
                    break;
                case ONNXOperatorType.Clip when defaultOpsetVersion < 11:
                {
                    // Clip-1, Clip-6
                    var minValue = node.GetOptionalFloat("min", float.MinValue);
                    var min = gm.Constant(minValue);
                    var maxValue = node.GetOptionalFloat("max", float.MaxValue);
                    var max = gm.Constant(maxValue);
                    SetOutput(gm.Clip(GetInput(0), min, max));
                    break;
                }
                case ONNXOperatorType.Clip:
                    // Clip-11, Clip-12, Clip-13 or Clip-1, Clip-6 with no min or max
                    SetOutput(gm.Clip(GetInput(0), GetInput(1), GetInput(2)));
                    break;
                case ONNXOperatorType.CumSum:
                {
                    var reverse = node.GetOptionalInt("reverse", 0) == 1;
                    var exclusive = node.GetOptionalInt("exclusive", 0) == 1;
                    SetOutput(gm.CumSum(GetInput(0), GetInput(1), reverse, exclusive));
                    break;
                }
                case ONNXOperatorType.Div:
                    SetOutput(gm.Div(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.Einsum:
                    SetOutput(gm.Einsum(GetInputs(), node.GetRequiredString("equation")));
                    break;
                case ONNXOperatorType.Exp:
                    SetOutput(gm.Exp(GetInput(0)));
                    break;
                case ONNXOperatorType.Floor:
                    SetOutput(gm.Floor(GetInput(0)));
                    break;
                case ONNXOperatorType.Gemm:
                {
                    var transposeA = node.GetOptionalInt("transA", 0) == 1;
                    var transposeB = node.GetOptionalInt("transB", 0) == 1;

                    var alpha = node.GetOptionalFloat("alpha", 1.0f);
                    var a = GetInput(0);
                    if (alpha != 1f)
                        a = gm.ScalarMad(a, DataType.Float, alpha, 0, 0, 0);

                    var res = gm.MatMul2D(a, GetInput(1), transposeA, transposeB);
                    var c = GetInput(2);
                    if (c is not null)
                    {
                        var beta = node.GetOptionalFloat("beta", 1.0f);
                        if (beta != 1f)
                            c = gm.ScalarMad(c, DataType.Float, beta, 0, 0, 0);
                        res = gm.Add(res, c);
                    }

                    SetOutput(res);
                    break;
                }
                case ONNXOperatorType.Log:
                    SetOutput(gm.Log(GetInput(0)));
                    break;
                case ONNXOperatorType.MatMul:
                    SetOutput(gm.MatMul(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.Max:
                {
                    var prev = GetInput(0);
                    for (var i = 1; i < node.InputCount - 1; i++)
                    {
                        var current = GetInput(i);
                        prev = gm.Max(prev, current);
                    }

                    SetOutput(gm.Max(GetInput(node.InputCount - 1), prev));
                    break;
                }
                case ONNXOperatorType.Mean:
                {
                    var prev = GetInput(0);
                    for (var i = 1; i < node.InputCount; i++)
                    {
                        var current = GetInput(i);
                        prev = gm.Add(prev, current);
                    }

                    SetOutput(gm.ScalarMad(prev, DataType.Float, 1.0f / node.InputCount, 0, 0, 0));
                    break;
                }
                case ONNXOperatorType.Min:
                {
                    var prev = GetInput(0);
                    for (var i = 1; i < node.InputCount - 1; i++)
                    {
                        var current = GetInput(i);
                        prev = gm.Min(prev, current);
                    }

                    SetOutput(gm.Min(GetInput(node.InputCount - 1), prev));
                    break;
                }
                case ONNXOperatorType.Mod:
                {
                    var fmod = node.GetOptionalInt("fmod", 0) != 0;
                    SetOutput(gm.Mod(GetInput(0), GetInput(1), fmod));
                    break;
                }
                case ONNXOperatorType.Mul:
                    SetOutput(gm.Mul(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.Neg:
                    SetOutput(gm.Neg(GetInput(0)));
                    break;
                case ONNXOperatorType.Pow:
                    // Pow-1, Pow-7, Pow-12, Pow-13
                    SetOutput(gm.Pow(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.Reciprocal:
                    SetOutput(gm.Reciprocal(GetInput(0)));
                    break;
                case ONNXOperatorType.Round:
                    SetOutput(gm.Round(GetInput(0)));
                    break;
                case ONNXOperatorType.Shrink:
                {
                    var bias = node.GetOptionalFloat("bias", 0f);
                    var lambd = node.GetOptionalFloat("lambd", 0.5f);
                    SetOutput(gm.Shrink(GetInput(0), bias, lambd));
                    break;
                }
                case ONNXOperatorType.Sign:
                    SetOutput(gm.Sign(GetInput(0)));
                    break;
                case ONNXOperatorType.Sqrt:
                    SetOutput(gm.Sqrt(GetInput(0)));
                    break;
                case ONNXOperatorType.Sub:
                    SetOutput(gm.Sub(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.Sum:
                {
                    var prev = GetInput(0);
                    for (var i = 1; i < node.InputCount - 1; i++)
                    {
                        var current = GetInput(i);
                        prev = gm.Add(prev, current);
                    }

                    SetOutput(gm.Add(GetInput(node.InputCount - 1), prev));
                    break;
                }

                // Layer.Normalization
                case ONNXOperatorType.BatchNormalization:
                {
                    var epsilon = node.GetOptionalFloat("epsilon", 1e-5f);
                    SetOutput(gm.BatchNormalization(GetInput(0), GetInput(1), GetInput(2), GetInput(3), GetInput(4), epsilon));
                    break;
                }
                case ONNXOperatorType.InstanceNormalization:
                {
                    var epsilon = node.GetOptionalFloat("epsilon", 1e-5f);
                    SetOutput(gm.InstanceNormalization(GetInput(0), GetInput(1), GetInput(2), epsilon));
                    break;
                }
                case ONNXOperatorType.LayerNormalization:
                {
                    var epsilon = node.GetOptionalFloat("epsilon", 1e-5f);
                    node.UnsupportedAttribute("axis", -1);
                    SetOutput(gm.LayerNormalization(GetInput(0), GetInput(1), GetInput(2), epsilon));
                    break;
                }
                case ONNXOperatorType.RMSNormalization:
                {
                    var epsilon = node.GetOptionalFloat("epsilon", 1e-5f);
                    node.UnsupportedAttribute("axis", -1);
                    SetOutput(gm.RMSNormalization(GetInput(0), GetInput(1), epsilon));
                    break;
                }
                case ONNXOperatorType.LRN:
                {
                    var alpha = node.GetOptionalFloat("alpha", 0.0001f);
                    var beta = node.GetOptionalFloat("beta", 0.75f);
                    var bias = node.GetOptionalFloat("bias", 1.0f);
                    var size = node.GetRequiredInt("size");
                    SetOutput(gm.LRN(GetInput(0), alpha, beta, bias, size));
                    break;
                }

                // Layer.ObjectDetection
                case ONNXOperatorType.NonMaxSuppression:
                {
                    var centerPointBox = (node.GetOptionalInt("center_point_box", 0) == 0) ? Layers.CenterPointBox.Corners : Layers.CenterPointBox.Center;
                    SetOutput(gm.NonMaxSuppression(GetInput(0), GetInput(1), GetInput(2), GetInput(3), GetInput(4), centerPointBox));
                    break;
                }
                case ONNXOperatorType.RoiAlign:
                {
                    Layers.RoiCoordinateTransformationMode coordinateTransformMode;
                    if (defaultOpsetVersion < 16)
                    {
                        coordinateTransformMode = Layers.RoiCoordinateTransformationMode.OutputHalfPixel;
                    }
                    else
                    {
                        var coordinateTransformModeString = node.GetOptionalString("coordinate_transformation_mode", "half_pixel");
                        coordinateTransformMode = coordinateTransformModeString switch
                        {
                            "output_half_pixel" => Layers.RoiCoordinateTransformationMode.OutputHalfPixel,
                            "half_pixel" => Layers.RoiCoordinateTransformationMode.HalfPixel,
                            _ => Warn(WarningType.Warning, $"coordinate_transformation_mode `{coordinateTransformModeString}` is not supported for RoiAlign, using `half_pixel`.", Layers.RoiCoordinateTransformationMode.HalfPixel)
                        };
                    }

                    var modeString = node.GetOptionalString("mode", "avg");
                    var mode = modeString switch
                    {
                        "avg" => Layers.RoiPoolingMode.Avg,
                        "max" => Layers.RoiPoolingMode.Max,
                        _ => Warn(WarningType.Warning, $"mode `{modeString}` is not supported for RoiAlign, using `avg`.", Layers.RoiPoolingMode.Avg)
                    };
                    var outputHeight = node.GetOptionalInt("output_height", 1);
                    var outputWidth = node.GetOptionalInt("output_width", 1);
                    var samplingRatio = node.GetOptionalInt("sampling_ratio", 0);
                    var spatialScale = node.GetOptionalFloat("spatial_scale", 1.0f);

                    SetOutput(gm.RoiAlign(GetInput(0), GetInput(1), GetInput(2), mode, outputHeight, outputWidth, samplingRatio, spatialScale, coordinateTransformMode));
                    break;
                }

                // Layer.Pooling
                case ONNXOperatorType.AveragePool:
                {
                    node.UnsupportedAttribute("ceil_mode", 0);
                    node.UnsupportedAttribute("dilations", new[] { 1, 1 });
                    node.UnsupportedAttribute("storage_order", 0);
                    node.UnsupportedAttribute("count_include_pad", 0);

                    var autoPadString = node.GetOptionalString("auto_pad", "NOTSET");
                    var autoPad = autoPadString switch
                    {
                        "NOTSET" => Layers.AutoPad.NotSet,
                        "VALID" => Layers.AutoPad.Valid,
                        "SAME_UPPER" => Layers.AutoPad.SameUpper,
                        "SAME_LOWER" => Layers.AutoPad.SameLower,
                        _ => Warn(WarningType.Warning, $"auto_pad `{autoPadString}` is not supported for AveragePool, using `NOTSET`.", Layers.AutoPad.NotSet)
                    };

                    var kernelShape = node.GetRequiredIntArray("kernel_shape");
                    var pads = node.GetOptionalIntArray("pads", new int[2 * kernelShape.Length]);
                    var strides = node.GetOptionalIntArray("strides", null);

                    if (strides == null)
                    {
                        strides = new int[kernelShape.Length];
                        for (var i = 0; i < strides.Length; i++)
                            strides[i] = 1;
                    }

                    SetOutput(gm.AveragePool(GetInput(0), kernelShape, strides, pads, autoPad));
                    break;
                }
                case ONNXOperatorType.GlobalAveragePool:
                    SetOutput(gm.GlobalAveragePool(GetInput(0)));
                    break;
                case ONNXOperatorType.GlobalMaxPool:
                    SetOutput(gm.GlobalMaxPool(GetInput(0)));
                    break;
                case ONNXOperatorType.MaxPool:
                {
                    node.UnsupportedAttribute("ceil_mode", 0);
                    node.UnsupportedAttribute("dilations", new[] { 1, 1 });
                    node.UnsupportedAttribute("storage_order", 0);

                    var autoPadString = node.GetOptionalString("auto_pad", "NOTSET");
                    var autoPad = autoPadString switch
                    {
                        "NOTSET" => Layers.AutoPad.NotSet,
                        "VALID" => Layers.AutoPad.Valid,
                        "SAME_UPPER" => Layers.AutoPad.SameUpper,
                        "SAME_LOWER" => Layers.AutoPad.SameLower,
                        _ => Warn(WarningType.Warning, $"auto_pad `{autoPadString}` is not supported for MaxPool, using `NOTSET`.", Layers.AutoPad.NotSet)
                    };

                    var kernelShape = node.GetRequiredIntArray("kernel_shape");
                    var pads = node.GetOptionalIntArray("pads", new int[2 * kernelShape.Length]);
                    var strides = node.GetOptionalIntArray("strides", null);

                    if (strides == null)
                    {
                        strides = new int[kernelShape.Length];
                        for (var i = 0; i < strides.Length; i++)
                            strides[i] = 1;
                    }

                    SetOutput(gm.MaxPool(GetInput(0), kernelShape, strides, pads, autoPad));
                    break;
                }

                // Layer.Random
                case ONNXOperatorType.Bernoulli:
                {
                    var dataTypeValue = node.GetOptionalInt("dtype", (int)TensorProto.Types.DataType.Float);
                    var dataType = ONNXNodeWrapper.DataTypeFromOnnxDataType((TensorProto.Types.DataType)dataTypeValue, OnUnsupported: () =>
                    {
                        Warn(WarningType.Error, $"Unsupported tensor dataType: {dataTypeValue}.");
                        throw new OnnxImportException(ImportWarnings.Last().message);
                    });
                    var hasSeed = node.HasAttribute("seed");
                    var seed = hasSeed ? math.asint(node.GetRequiredFloat("seed")) : 0;
                    SetOutput(gm.Bernoulli(GetInput(0), dataType, hasSeed, seed));
                    break;
                }
                case ONNXOperatorType.Multinomial:
                {
                    // dtype can only be int32 or int64 which both map to Tensor<int>
                    var samples = node.GetOptionalInt("sample_size", 1);
                    var hasSeed = node.HasAttribute("seed");
                    var seed = hasSeed ? math.asint(node.GetRequiredFloat("seed")) : 0;
                    SetOutput(gm.Multinomial(GetInput(0), samples, hasSeed, seed));
                    break;
                }
                case ONNXOperatorType.RandomNormal:
                {
                    var mean = node.GetOptionalFloat("mean", 0.0f);
                    var scale = node.GetOptionalFloat("scale", 1.0f);
                    var shape = node.GetRequiredIntArray("shape");
                    var hasSeed = node.HasAttribute("seed");
                    var seed = hasSeed ? math.asint(node.GetRequiredFloat("seed")) : 0;
                    SetOutput(gm.RandomNormal(mean, scale, shape, hasSeed, seed));
                    break;
                }
                case ONNXOperatorType.RandomNormalLike:
                {
                    var mean = node.GetOptionalFloat("mean", 0.0f);
                    var scale = node.GetOptionalFloat("scale", 1.0f);
                    var hasSeed = node.HasAttribute("seed");
                    var seed = hasSeed ? math.asint(node.GetRequiredFloat("seed")) : 0;
                    SetOutput(gm.RandomNormalLike(GetInput(0), mean, scale, hasSeed, seed));
                    break;
                }
                case ONNXOperatorType.RandomUniform:
                {
                    var low = node.GetOptionalFloat("low", 0.0f);
                    var high = node.GetOptionalFloat("high", 1.0f);
                    var shape = node.GetRequiredIntArray("shape");
                    var hasSeed = node.HasAttribute("seed");
                    var seed = hasSeed ? math.asint(node.GetRequiredFloat("seed")) : 0;
                    SetOutput(gm.RandomUniform(low, high, shape, hasSeed, seed));
                    break;
                }
                case ONNXOperatorType.RandomUniformLike:
                {
                    var low = node.GetOptionalFloat("low", 0.0f);
                    var high = node.GetOptionalFloat("high", 1.0f);
                    var hasSeed = node.HasAttribute("seed");
                    var seed = hasSeed ? math.asint(node.GetRequiredFloat("seed")) : 0;
                    SetOutput(gm.RandomUniformLike(GetInput(0), low, high, hasSeed, seed));
                    break;
                }

                // Layer.Recurrent
                case ONNXOperatorType.LSTM:
                {
                    var hiddenSize = node.GetRequiredInt("hidden_size");
                    var directionString = node.GetOptionalString("direction", "forward");
                    var direction = directionString switch
                    {
                        "forward" => Layers.RnnDirection.Forward,
                        "reverse" => Layers.RnnDirection.Reverse,
                        "bidirectional" => Layers.RnnDirection.Bidirectional,
                        _ => Warn(WarningType.Warning, $"direction `{directionString}` is not supported for LSTM, using `forward`.", Layers.RnnDirection.Forward)
                    };
                    var numDirections = direction == Layers.RnnDirection.Bidirectional ? 2 : 1;

                    var activationAlphaNode = node.GetOptionalFloatArray("activation_alpha", null);
                    var activationBetaNode = node.GetOptionalFloatArray("activation_beta", null);

                    var activationAlpha = new float[3 * numDirections];
                    var activationBeta = new float[3 * numDirections];

                    var activationsStringArray = node.GetOptionalStringArray("activations", null);
                    var activations = new Layers.RnnActivation[3 * numDirections];
                    for (var i = 0; i < 3 * numDirections; i++)
                    {
                        var defaultActivation = i % 3 == 0 ? Layers.RnnActivation.Sigmoid : Layers.RnnActivation.Tanh;
                        if (activationsStringArray == null)
                        {
                            activations[i] = defaultActivation;
                        }
                        else
                        {
                            activations[i] = activationsStringArray[i] switch
                            {
                                "Relu" => Layers.RnnActivation.Relu,
                                "Tanh" => Layers.RnnActivation.Tanh,
                                "Sigmoid" => Layers.RnnActivation.Sigmoid,
                                "Affine" => Layers.RnnActivation.Affine,
                                "LeakyRelu" => Layers.RnnActivation.LeakyRelu,
                                "ThresholdedRelu" => Layers.RnnActivation.ThresholdedRelu,
                                "ScaledTanh" => Layers.RnnActivation.ScaledTanh,
                                "HardSigmoid" => Layers.RnnActivation.HardSigmoid,
                                "Elu" => Layers.RnnActivation.Elu,
                                "Softsign" => Layers.RnnActivation.Softsign,
                                "Softplus" => Layers.RnnActivation.Softplus,
                                _ => Warn(WarningType.Warning, $"activation `{activationsStringArray[i]}` is not supported for LSTM, using `{defaultActivation}`.", defaultActivation)
                            };
                        }

                        if (activationAlphaNode == null || activationAlphaNode.Length <= i)
                        {
                            activationAlpha[i] = activations[i] switch
                            {
                                Layers.RnnActivation.Affine => 1.0f,
                                Layers.RnnActivation.LeakyRelu => 0.01f,
                                Layers.RnnActivation.ThresholdedRelu => 1.0f,
                                Layers.RnnActivation.ScaledTanh => 1.0f,
                                Layers.RnnActivation.HardSigmoid => 0.2f,
                                Layers.RnnActivation.Elu => 1.0f,
                                _ => 0
                            };
                        }
                        else
                        {
                            activationAlpha[i] = activationAlphaNode[i];
                        }

                        if (activationBetaNode == null || activationBetaNode.Length <= i)
                        {
                            activationBeta[i] = activations[i] switch
                            {
                                Layers.RnnActivation.ScaledTanh => 1.0f,
                                Layers.RnnActivation.HardSigmoid => 0.5f,
                                _ => 0
                            };
                        }
                        else
                        {
                            activationBeta[i] = activationBetaNode[i];
                        }
                    }

                    var clip = node.GetOptionalFloat("clip", float.MaxValue);
                    var inputForget = node.GetOptionalInt("input_forget", 0) != 0;
                    var layoutInt = node.GetOptionalInt("layout", 0);
                    var layout = layoutInt switch
                    {
                        0 => Layers.RnnLayout.SequenceFirst,
                        1 => Layers.RnnLayout.BatchFirst,
                        _ => Warn(WarningType.Warning, $"layout `{layoutInt}` is not supported for LSTM, using `0`.", Layers.RnnLayout.SequenceFirst)
                    };

                    SetOutputs(gm.LSTM(GetInput(0), GetInput(1), GetInput(2), GetInput(3), GetInput(4), GetInput(5), GetInput(6), GetInput(7), hiddenSize, direction, activations, activationAlpha, activationBeta, clip, inputForget, layout));
                    break;
                }

                // Layer.Reduction
                case ONNXOperatorType.ReduceL1:
                {
                    var keepDims = node.GetOptionalInt("keepdims", 1) == 1;
                    var noopWithEmptyAxes = node.GetOptionalInt("noop_with_empty_axes", 0) == 1;
                    var axes = GetInput(1);
                    if (defaultOpsetVersion < 18)
                    {
                        var axesArray = node.GetOptionalIntArray("axes", null);
                        if (axesArray != null)
                        {
                            axes = gm.Constant(axesArray);
                        }
                    }

                    SetOutput(gm.ReduceL1(GetInput(0), axes, keepDims, noopWithEmptyAxes));
                    break;
                }
                case ONNXOperatorType.ReduceL2:
                {
                    var keepDims = node.GetOptionalInt("keepdims", 1) == 1;
                    var noopWithEmptyAxes = node.GetOptionalInt("noop_with_empty_axes", 0) == 1;
                    var axes = GetInput(1);
                    if (defaultOpsetVersion < 18)
                    {
                        var axesArray = node.GetOptionalIntArray("axes", null);
                        if (axesArray != null)
                        {
                            axes = gm.Constant(axesArray);
                        }
                    }

                    SetOutput(gm.ReduceL2(GetInput(0), axes, keepDims, noopWithEmptyAxes));
                    break;
                }
                case ONNXOperatorType.ReduceLogSum:
                {
                    var keepDims = node.GetOptionalInt("keepdims", 1) == 1;
                    var noopWithEmptyAxes = node.GetOptionalInt("noop_with_empty_axes", 0) == 1;
                    var axes = GetInput(1);
                    if (defaultOpsetVersion < 18)
                    {
                        var axesArray = node.GetOptionalIntArray("axes", null);
                        if (axesArray != null)
                        {
                            axes = gm.Constant(axesArray);
                        }
                    }

                    SetOutput(gm.ReduceLogSum(GetInput(0), axes, keepDims, noopWithEmptyAxes));
                    break;
                }
                case ONNXOperatorType.ReduceLogSumExp:
                {
                    var keepDims = node.GetOptionalInt("keepdims", 1) == 1;
                    var noopWithEmptyAxes = node.GetOptionalInt("noop_with_empty_axes", 0) == 1;
                    var axes = GetInput(1);
                    if (defaultOpsetVersion < 18)
                    {
                        var axesArray = node.GetOptionalIntArray("axes", null);
                        if (axesArray != null)
                        {
                            axes = gm.Constant(axesArray);
                        }
                    }

                    SetOutput(gm.ReduceLogSumExp(GetInput(0), axes, keepDims, noopWithEmptyAxes));
                    break;
                }
                case ONNXOperatorType.ReduceMax:
                {
                    var keepDims = node.GetOptionalInt("keepdims", 1) == 1;
                    var noopWithEmptyAxes = node.GetOptionalInt("noop_with_empty_axes", 0) == 1;
                    var axes = GetInput(1);
                    if (defaultOpsetVersion < 18)
                    {
                        var axesArray = node.GetOptionalIntArray("axes", null);
                        if (axesArray != null)
                        {
                            axes = gm.Constant(axesArray);
                        }
                    }

                    SetOutput(gm.ReduceMax(GetInput(0), axes, keepDims, noopWithEmptyAxes));
                    break;
                }
                case ONNXOperatorType.ReduceMean:
                {
                    var keepDims = node.GetOptionalInt("keepdims", 1) == 1;
                    var noopWithEmptyAxes = node.GetOptionalInt("noop_with_empty_axes", 0) == 1;
                    var axes = GetInput(1);
                    if (defaultOpsetVersion < 18)
                    {
                        var axesArray = node.GetOptionalIntArray("axes", null);
                        if (axesArray != null)
                        {
                            axes = gm.Constant(axesArray);
                        }
                    }

                    SetOutput(gm.ReduceMean(GetInput(0), axes, keepDims, noopWithEmptyAxes));
                    break;
                }
                case ONNXOperatorType.ReduceMin:
                {
                    var keepDims = node.GetOptionalInt("keepdims", 1) == 1;
                    var noopWithEmptyAxes = node.GetOptionalInt("noop_with_empty_axes", 0) == 1;
                    var axes = GetInput(1);
                    if (defaultOpsetVersion < 18)
                    {
                        var axesArray = node.GetOptionalIntArray("axes", null);
                        if (axesArray != null)
                        {
                            axes = gm.Constant(axesArray);
                        }
                    }

                    SetOutput(gm.ReduceMin(GetInput(0), axes, keepDims, noopWithEmptyAxes));
                    break;
                }
                case ONNXOperatorType.ReduceProd:
                {
                    var keepDims = node.GetOptionalInt("keepdims", 1) == 1;
                    var noopWithEmptyAxes = node.GetOptionalInt("noop_with_empty_axes", 0) == 1;
                    var axes = GetInput(1);
                    if (defaultOpsetVersion < 18)
                    {
                        var axesArray = node.GetOptionalIntArray("axes", null);
                        if (axesArray != null)
                        {
                            axes = gm.Constant(axesArray);
                        }
                    }
                    else if (node.InputCount > 1)
                    {
                        axes = GetInput(1);
                    }

                    SetOutput(gm.ReduceProd(GetInput(0), axes, keepDims, noopWithEmptyAxes));
                    break;
                }
                case ONNXOperatorType.ReduceSum:
                {
                    var keepDims = node.GetOptionalInt("keepdims", 1) == 1;
                    var noopWithEmptyAxes = node.GetOptionalInt("noop_with_empty_axes", 0) == 1;
                    var axes = GetInput(1);
                    if (defaultOpsetVersion < 13)
                    {
                        var axesArray = node.GetOptionalIntArray("axes", null);
                        if (axesArray != null)
                        {
                            axes = gm.Constant(axesArray);
                        }
                    }
                    else if (node.InputCount > 1)
                    {
                        axes = GetInput(1);
                    }

                    SetOutput(gm.ReduceSum(GetInput(0), axes, keepDims, noopWithEmptyAxes));
                    break;
                }
                case ONNXOperatorType.ReduceSumSquare:
                {
                    var keepDims = node.GetOptionalInt("keepdims", 1) == 1;
                    var noopWithEmptyAxes = node.GetOptionalInt("noop_with_empty_axes", 0) == 1;
                    var axes = GetInput(1);
                    if (defaultOpsetVersion < 18)
                    {
                        var axesArray = node.GetOptionalIntArray("axes", null);
                        if (axesArray != null)
                        {
                            axes = gm.Constant(axesArray);
                        }
                    }
                    else if (node.InputCount > 1)
                    {
                        axes = GetInput(1);
                    }

                    SetOutput(gm.ReduceSumSquare(GetInput(0), axes, keepDims, noopWithEmptyAxes));
                    break;
                }

                // Layer.Spectral
                case ONNXOperatorType.BlackmanWindow:
                {
                    node.UnsupportedAttribute("output_datatype", 1);
                    var periodic = node.GetOptionalInt("periodic", 1) == 1;
                    var size = GetInput(0);
                    SetOutput(gm.BlackmanWindow(size, periodic));
                    break;
                }
                case ONNXOperatorType.DFT:
                {
                    var inverse = node.GetOptionalInt("inverse", 0) == 1;
                    var onesided = node.GetOptionalInt("onesided", 0) == 1;
                    var input = GetInput(0);
                    var dftLength = GetInput(1);
                    var axis = GetInput(2);
                    SetOutput(gm.DFT(input, dftLength, axis, dftMatrix: null, inverse, onesided));
                    break;
                }
                case ONNXOperatorType.HammingWindow:
                {
                    node.UnsupportedAttribute("output_datatype", 1);
                    var periodic = node.GetOptionalInt("periodic", 1) == 1;
                    var size = GetInput(0);
                    SetOutput(gm.HammingWindow(size, periodic));
                    break;
                }
                case ONNXOperatorType.HannWindow:
                {
                    node.UnsupportedAttribute("output_datatype", 1);
                    var periodic = node.GetOptionalInt("periodic", 1) == 1;
                    var size = GetInput(0);
                    SetOutput(gm.HannWindow(size, periodic));
                    break;
                }
                case ONNXOperatorType.MelWeightMatrix:
                {
                    node.UnsupportedAttribute("output_datatype", 1);
                    var numMelBins = GetInput(0);
                    var dftLength = GetInput(1);
                    var sampleRate = GetInput(2);
                    var lowerEdgeHertz = GetInput(3);
                    var upperEdgeHertz = GetInput(4);
                    SetOutput(gm.MelWeightMatrix(numMelBins, dftLength, sampleRate, lowerEdgeHertz, upperEdgeHertz));
                    break;
                }
                case ONNXOperatorType.STFT:
                {
                    var onesided = node.GetOptionalInt("onesided", 1) == 1;
                    var signal = GetInput(0);
                    var frameStep = GetInput(1);
                    var window = GetInput(2);
                    var frameLength = GetInput(3);
                    SetOutput(gm.STFT(signal, frameStep, window, frameLength, windowedDFTMatrix: null, onesided));
                    break;
                }

                // Layer.Transformation
                case ONNXOperatorType.Cast:
                {
                    var toOnnxType = (TensorProto.Types.DataType)node.GetRequiredInt("to");
                    var toDataType = ONNXNodeWrapper.DataTypeFromOnnxDataType(toOnnxType, OnUnsupported: () =>
                    {
                        Warn(WarningType.Error, $"Unsupported tensor dataType: {toOnnxType}.");
                    });
                    SetOutput(gm.Cast(GetInput(0), toDataType));
                    break;
                }
                case ONNXOperatorType.CastLike:
                    SetOutput(gm.CastLike(GetInput(0), GetInput(1)));
                    break;
                case ONNXOperatorType.Concat:
                {
                    var axis = node.GetRequiredInt("axis");
                    SetOutput(gm.Concat(GetInputs(), axis));
                    break;
                }
                case ONNXOperatorType.DepthToSpace:
                {
                    var modeType = node.GetOptionalString("mode", "DCR");
                    var mode = modeType == "DCR" ? Layers.DepthToSpaceMode.DepthColumnRow : Layers.DepthToSpaceMode.ColumnRowDepth;
                    var blocksize = node.GetRequiredInt("blocksize");
                    SetOutput(gm.DepthToSpace(GetInput(0), blocksize, mode));
                    break;
                }
                case ONNXOperatorType.Expand:
                {
                    // Expand-8, Expand-13
                    SetOutput(gm.Expand(GetInput(0), GetInput(1)));
                    break;
                }
                case ONNXOperatorType.Flatten:
                {
                    var axis = node.GetOptionalInt("axis", 1);
                    SetOutput(gm.Flatten(GetInput(0), axis));
                    break;
                }
                case ONNXOperatorType.GridSample:
                {
                    var modeString = node.GetOptionalString("mode", "linear");
                    var mode = modeString switch
                    {
                        "bilinear" => Layers.InterpolationMode.Linear, // for opset 16
                        "linear" => Layers.InterpolationMode.Linear,
                        // SENTIS-1352 support mode 'bicubic' for GridSample
                        "bicubic" => Warn(WarningType.Warning, $"mode `{modeString}` is not supported for GridSample, using `linear`.", Layers.InterpolationMode.Linear),  // for opset 16
                        "cubic" => Warn(WarningType.Warning, $"mode `{modeString}` is not supported for GridSample, using `linear`.", Layers.InterpolationMode.Linear),
                        "nearest" => Layers.InterpolationMode.Nearest,
                        _ => Warn(WarningType.Warning, $"mode `{modeString}` is not supported for GridSample, using `linear`.", Layers.InterpolationMode.Linear)
                    };
                    var paddingModeString = node.GetOptionalString("padding_mode", "zeros");
                    var paddingMode = paddingModeString switch
                    {
                        "zeros" => Layers.PaddingMode.Zeros,
                        "border" => Layers.PaddingMode.Border,
                        "reflection" => Layers.PaddingMode.Reflection,
                        _ => Warn(WarningType.Warning, $"padding_mode `{paddingModeString}` is not supported for GridSample, using `zeros`.", Layers.PaddingMode.Zeros)
                    };
                    var alignCorners = node.GetOptionalInt("align_corners", 0) == 1;
                    SetOutput(gm.GridSample(GetInput(0), GetInput(1), mode, paddingMode, alignCorners));
                    break;
                }
                case ONNXOperatorType.Dropout:
                {
                    SetOutput(gm.Identity(GetInput(0)));
                    break;
                }
                case ONNXOperatorType.Identity:
                {
                    SetOutput(gm.Identity(GetInput(0)));
                    break;
                }
                case ONNXOperatorType.Pad:
                {
                    var modeString = node.GetOptionalString("mode", "constant");
                    var mode = modeString switch
                    {
                        "constant" => Layers.PadMode.Constant,
                        "reflect" => Layers.PadMode.Reflect,
                        "edge" => Layers.PadMode.Edge,
                        "wrap" => Layers.PadMode.Wrap,
                        _ => Warn(WarningType.Warning, $"mode `{modeString}` is not supported for Pad, using `constant`.", Layers.PadMode.Constant)
                    };

                    if (defaultOpsetVersion < 11)
                    {
                        // Pad-1 or Pad-2
                        var padsArray = node.GetRequiredIntArray(node.HasAttribute("pads") ? "pads" : "paddings");
                        var pads = gm.Constant(padsArray);

                        var valueFloat = node.GetOptionalFloat("value", 0f);
                        var value = gm.Constant(valueFloat);

                        SetOutput(gm.Pad(GetInput(0), pads, value, null, mode));
                    }
                    else
                    {
                        // Pad-11, Pad-13, Pad-18
                        SetOutput(gm.Pad(GetInput(0), GetInput(1), GetInput(2), GetInput(3), mode));
                    }

                    break;
                }
                case ONNXOperatorType.Reshape when defaultOpsetVersion < 5:
                {
                    // Reshape-1
                    var shapeArray = node.GetRequiredIntArray("shape");
                    var shape = gm.Constant(shapeArray);
                    SetOutput(gm.Reshape(GetInput(0), shape, false));
                    break;
                }
                case ONNXOperatorType.Reshape:
                {
                    // Reshape-5, Reshape-13, Reshape-14
                    var allowZero = node.GetOptionalInt("allowzero", 0) != 0;
                    SetOutput(gm.Reshape(GetInput(0), GetInput(1), allowZero));
                    break;
                }
                case ONNXOperatorType.Resize:
                {
                    var modeString = node.GetOptionalString("mode", "nearest");
                    var mode = modeString switch
                    {
                        "nearest" => Layers.InterpolationMode.Nearest,
                        "linear" => Layers.InterpolationMode.Linear,
                        _ => Warn(WarningType.Warning, $"mode `{modeString}` is not supported for Resize, using `nearest`.", Layers.InterpolationMode.Nearest)
                    };

                    var axes = node.GetOptionalIntArray("axes", null);
                    if (defaultOpsetVersion < 11)
                    {
                        // Resize-10
                        SetOutput(gm.Resize(GetInput(0), GetInput(1), Layers.ScaleMode.Scales, Layers.CoordTransformMode.Asymmetric, mode, Layers.NearestMode.Floor, axes));
                    }
                    else
                    {
                        node.UnsupportedAttribute("cubic_coeff_a", -0.75f);
                        node.UnsupportedAttribute("exclude_outside", 0);
                        node.UnsupportedAttribute("extrapolation_value", 0);
                        var coordinateTransformModeString = node.GetOptionalString("coordinate_transformation_mode", "half_pixel");
                        var coordinateTransformMode = coordinateTransformModeString switch
                        {
                            "half_pixel" => Layers.CoordTransformMode.HalfPixel,
                            "pytorch_half_pixel" => Layers.CoordTransformMode.PytorchHalfPixel,
                            "align_corners" => Layers.CoordTransformMode.AlignCorners,
                            "asymmetric" => Layers.CoordTransformMode.Asymmetric,
                            _ => Warn(WarningType.Warning, $"coordinate_transformation_mode `{coordinateTransformModeString}` is not supported for Resize, using `half_pixel`.", Layers.CoordTransformMode.HalfPixel)
                        };

                        var nearestModeString = node.GetOptionalString("nearest_mode", "round_prefer_floor");
                        var nearestMode = nearestModeString switch
                        {
                            "round_prefer_floor" => Layers.NearestMode.RoundPreferFloor,
                            "round_prefer_ceil" => Layers.NearestMode.RoundPreferCeil,
                            "floor" => Layers.NearestMode.Floor,
                            "ceil" => Layers.NearestMode.Ceil,
                            _ => Warn(WarningType.Warning, $"nearest_mode `{nearestModeString}` is not supported for Resize, using `round_prefer_floor`.", Layers.NearestMode.RoundPreferFloor)
                        };

                        if (node.InputCount == 3 || string.IsNullOrEmpty(node.Inputs[3]))
                        {
                            // Resize-11, Resize-13, Resize-18 with scales
                            SetOutput(gm.Resize(GetInput(0), GetInput(2), Layers.ScaleMode.Scales, coordinateTransformMode, mode, nearestMode, axes));
                        }
                        else if (node.InputCount == 4)
                        {
                            // Resize-11, Resize-13, Resize-18 with sizes
                            SetOutput(gm.Resize(GetInput(0), GetInput(3), Layers.ScaleMode.Sizes, coordinateTransformMode, mode, nearestMode, axes));
                        }
                    }

                    break;
                }
                case ONNXOperatorType.Slice when defaultOpsetVersion < 10:
                {
                    // Slice-1
                    var startsArray = node.GetRequiredIntArray("starts");
                    var starts = gm.Constant(startsArray);

                    var endsArray = node.GetRequiredIntArray("ends");
                    var ends = gm.Constant(endsArray);

                    if (node.HasAttribute("axes"))
                    {
                        var axesArray = node.GetRequiredIntArray("axes");
                        var axes = gm.Constant(axesArray);
                        SetOutput(gm.Slice(GetInput(0), starts, ends, axes, null));
                    }
                    else
                    {
                        SetOutput(gm.Slice(GetInput(0), starts, ends, null, null));
                    }

                    break;
                }
                case ONNXOperatorType.Slice:
                {

                    // Slice-10, Slice-11, Slice-13
                    SetOutput(gm.Slice(GetInput(0), GetInput(1), GetInput(2), GetInput(3), GetInput(4)));
                    break;
                }
                case ONNXOperatorType.SpaceToDepth:
                {
                    var blocksize = node.GetRequiredInt("blocksize");
                    SetOutput(gm.SpaceToDepth(GetInput(0), blocksize));
                    break;
                }
                case ONNXOperatorType.Split:
                {
                    var axis = node.GetOptionalInt("axis", 0);
                    if (node.HasAttribute("split"))
                    {
                        // Split-1, Split-2, Split-11 with "split" attribute
                        var splitArray = node.GetRequiredIntArray("split");
                        var split = gm.Constant(splitArray);
                        SetOutputs(gm.Split(GetInput(0), split, axis, node.OutputCount));
                    }
                    else
                    {
                        var split = GetInput(1);

                        if (split is null)
                        {
                            // Split-1, Split-2, Split-11, Split-13, Split-18 with num_outputs
                            var numOutputs = node.GetOptionalInt("num_outputs", node.Outputs.Length);
                            SetOutputs(gm.Split(GetInput(0), null, axis, numOutputs));
                        }
                        else
                        {
                            // Split-1, Split-2, Split-11, Split-13, Split-18 with split tensor
                            SetOutputs(gm.Split(GetInput(0), split, axis, node.OutputCount));
                        }
                    }

                    break;
                }
                case ONNXOperatorType.Squeeze when defaultOpsetVersion < 13 && node.HasAttribute("axes"):
                {
                    // Squeeze-1, Squeeze-11 with given axes
                    var axesArray = node.GetRequiredIntArray("axes");
                    var axes = gm.Constant(axesArray);

                    SetOutput(gm.Squeeze(GetInput(0), axes));
                    break;
                }
                case ONNXOperatorType.Squeeze:
                {
                    // Squeeze-13 or Squeeze-1, Squeeze-11 without given axes
                    SetOutput(gm.Squeeze(GetInput(0), GetInput(1)));
                    break;
                }
                case ONNXOperatorType.Tile:
                {
                    SetOutput(gm.Tile(GetInput(0), GetInput(1)));
                    break;
                }
                case ONNXOperatorType.Transpose:
                {
                    var permutations = node.GetOptionalIntArray("perm", null);
                    SetOutput(gm.Transpose(GetInput(0), permutations));
                    break;
                }
                case ONNXOperatorType.Trilu:
                {
                    var upper = node.GetOptionalInt("upper", 1);
                    SetOutput(gm.Trilu(GetInput(0), GetInput(1), (Layers.TriluMode)upper));
                    break;
                }
                case ONNXOperatorType.Upsample:
                {
                    var coordinateTransformMode = Layers.CoordTransformMode.Asymmetric;
                    var modeString = node.GetOptionalString("mode", "nearest");
                    var mode = modeString switch
                    {
                        "nearest" => Layers.InterpolationMode.Nearest,
                        "linear" => Layers.InterpolationMode.Linear,
                        "bilinear" => Layers.InterpolationMode.Linear, // for opset 1
                        _ => Warn(WarningType.Warning, $"mode `{modeString}` is not supported for Resize, using `nearest`.", Layers.InterpolationMode.Nearest)
                    };
                    var nearestMode = Layers.NearestMode.Floor;
                    if (defaultOpsetVersion < 9)
                    {
                        // Upsample-7
                        var scalesArray = node.GetRequiredFloatArray("scales");
                        var scales = gm.Constant(scalesArray);

                        SetOutput(gm.Resize(GetInput(0), scales, Layers.ScaleMode.Scales, coordinateTransformMode, mode, nearestMode, null));
                    }
                    else
                    {
                        // Upsample-9
                        SetOutput(gm.Resize(GetInput(0), GetInput(1), Layers.ScaleMode.Scales, coordinateTransformMode, mode, nearestMode, null));
                    }

                    break;
                }
                case ONNXOperatorType.Unsqueeze when defaultOpsetVersion < 13:
                {
                    // Unsqueeze-1, Unsqueeze-11
                    var axesArray = node.GetRequiredIntArray("axes");
                    var axes = gm.Constant(axesArray);

                    SetOutput(gm.Unsqueeze(GetInput(0), axes));
                    break;
                }
                case ONNXOperatorType.Unsqueeze:
                {
                    SetOutput(gm.Unsqueeze(GetInput(0), GetInput(1)));
                    break;
                }

                // Layer.Trigonometric
                case ONNXOperatorType.Acos:
                    SetOutput(gm.Acos(GetInput(0)));
                    break;
                case ONNXOperatorType.Acosh:
                    SetOutput(gm.Acosh(GetInput(0)));
                    break;
                case ONNXOperatorType.Asin:
                    SetOutput(gm.Asin(GetInput(0)));
                    break;
                case ONNXOperatorType.Asinh:
                    SetOutput(gm.Asinh(GetInput(0)));
                    break;
                case ONNXOperatorType.Atan:
                    SetOutput(gm.Atan(GetInput(0)));
                    break;
                case ONNXOperatorType.Atanh:
                    SetOutput(gm.Atanh(GetInput(0)));
                    break;
                case ONNXOperatorType.Cos:
                    SetOutput(gm.Cos(GetInput(0)));
                    break;
                case ONNXOperatorType.Cosh:
                    SetOutput(gm.Cosh(GetInput(0)));
                    break;
                case ONNXOperatorType.Sin:
                    SetOutput(gm.Sin(GetInput(0)));
                    break;
                case ONNXOperatorType.Sinh:
                    SetOutput(gm.Sinh(GetInput(0)));
                    break;
                case ONNXOperatorType.Tan:
                    SetOutput(gm.Tan(GetInput(0)));
                    break;

                // Non standard ONNX
                case ONNXOperatorType.ImageScaler:
                {
                    var attrBias = node.GetRequiredFloatArray("bias");
                    var maxElements = attrBias.Length;
                    var attrScale = Enumerable.Repeat(node.GetOptionalFloat("scale", 1.0f), maxElements).ToArray();

                    var scale = gm.Constant(attrScale);
                    var bias = gm.Constant(attrBias);
                    SetOutput(gm.ScaleBias(GetInput(0), scale, bias));
                    break;
                }
                default:
                    Warn(WarningType.Error, $"Unsupported ONNX Operator: {opTypeString}");
                    throw new OnnxUnsupportedOpException(opTypeString);
            }
        }

        Model ConvertOnnxModel(ModelProto onnxModel)
        {
            var gm = new GraphModule();
            var tensors = new Dictionary<string, Node>();

            long defaultOpsetVersion = 15;

            // Parse producer meta data
            foreach (var opsetSetIdProto in onnxModel.OpsetImport)
            {
                if (string.IsNullOrEmpty(opsetSetIdProto.Domain))
                    defaultOpsetVersion = opsetSetIdProto.Version;
            }

            // Validate graph structure, datatypes, and operators BEFORE any processing
            ValidateOnnxGraph(onnxModel.Graph);

            // Convert graph inputs & outputs
            var initializersByName = onnxModel.Graph.Initializer.ToDictionary(i => i.Name, i => true);

            var namedDims = new List<string>();
            foreach (var input in onnxModel.Graph.Input)
            {
                // skip input tensors that have initializer data, they are constant tensors not global inputs
                // also skip nodes that should be trimmed
                if (initializersByName.ContainsKey(input.Name))
                    continue;

                var onnxShape = input.Type.TensorType.Shape;
                var inputShape = DynamicTensorShape.DynamicOfRank(onnxShape.Dim.Count);

                for (var i = 0; i < inputShape.rank; i++)
                {
                    var dim = onnxShape.Dim[i];
                    switch (dim.ValueCase)
                    {
                        case TensorShapeProto.Types.Dimension.ValueOneofCase.None:
                            inputShape[i] = DynamicTensorDim.Unknown;
                            break;
                        case TensorShapeProto.Types.Dimension.ValueOneofCase.DimParam:
                            var index = namedDims.IndexOf(dim.DimParam);
                            if (index < 0)
                            {
                                index = namedDims.Count;
                                namedDims.Add(dim.DimParam);
                            }
                            inputShape[i] = DynamicTensorDim.Param((byte)index);
                            if (DynamicDimConfigs.TryGetValue(dim.DimParam, out var dimInt))
                                inputShape[i] = DynamicTensorDim.Int(dimInt);
                            break;
                        case TensorShapeProto.Types.Dimension.ValueOneofCase.DimValue:
                            if (dim.DimValue < 0)
                                Warn(WarningType.Warning, "Tensor shape has negative index, treating as unknown dimension");
                            else
                                inputShape[i] = DynamicTensorDim.Int(dim.DimValue > int.MaxValue ? int.MaxValue : (int)dim.DimValue);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                var onnxInputDataType = (TensorProto.Types.DataType)input.Type.TensorType.ElemType;
                var inputDataType = ONNXNodeWrapper.DataTypeFromOnnxDataType(onnxInputDataType);

                var inputNode = gm.Input(input.Name, inputDataType, inputShape);
                tensors[input.Name] = inputNode;
            }

            var weightsStream = new Dictionary<string, FileStream>();
            // Read constants from initializer list
            foreach (TensorProto initializer in onnxModel.Graph.Initializer)
            {
                if (initializer.DataLocation == TensorProto.Types.DataLocation.External)
                {
                    string name = initializer.ExternalData.Single(x => x.Key == "location").Value;
                    if (!weightsStream.ContainsKey(name))
                    {
                        string filePath = Path.Combine(m_DirectoryPath, name);
                        if (File.Exists(filePath))
                            weightsStream.Add(name, File.OpenRead(Path.Combine(m_DirectoryPath, name)));
                        else
                        {
                            var errorMessage = $"External Weights file not found! Expecting: {filePath}";
                            Warn(WarningType.Error, errorMessage);
                            return null;
                        }
                    }
                    var stream = weightsStream[name];
                    var constantTensor = ONNXConstantsLoader.LoadConstant(initializer, stream);
                    tensors[initializer.Name] = gm.Constant(constantTensor);
                }
                else
                {
                    var constantTensor = ONNXConstantsLoader.LoadConstant(initializer);
                    tensors[initializer.Name] = gm.Constant(constantTensor);
                }

            }
            foreach (var stream in weightsStream.Values)
                stream.Dispose();

            // NOTE: It's questionable whether we should be doing this since the ONNX specification requires the graph to be
            // topologically sorted, but at least one network encountered that was exported from keras2onnx v1.7.0 produced
            // an incorrectly sorted graph. related example: https://github.com/onnx/keras-onnx/issues/184
            var sortedGraph = ONNXModelUtility.StableTopologicalSort(onnxModel.Graph);
            var onnxNodes = sortedGraph.Select(onnxNode => new ONNXNodeWrapper(onnxNode)).ToList();

            // Convert graph nodes
            foreach (var node in onnxNodes)
            {
                OnNode(gm, tensors, defaultOpsetVersion, node);
            }

            // delete unused outputs
            var outputs = new List<Node>();
            var outputNames = new List<string>();
            for (var i = 0; i < onnxModel.Graph.Output.Count; i++)
            {
                var outputName = onnxModel.Graph.Output[i].Name;
                if (!tensors.TryGetValue(outputName, out var outputTensor))
                {
                    Warn(WarningType.Warning, $"Output {outputName} is not connected to any tensor in the graph and will be skipped.");
                    continue;
                }
                outputs.Add(outputTensor);
                outputNames.Add(outputName);
            }

            gm.Outputs(outputNames.ToArray(), outputs.ToArray());

            if (!ImportWarnings.Any(w => w.messageSeverity == WarningType.Error))
            {
                ModelOptimizer.OptimizeGraph(gm);
            }

            var model = GraphConverter.GraphToModel(gm);

            model.ProducerName = onnxModel.ProducerName;
            if (!string.IsNullOrEmpty(onnxModel.ProducerVersion))
                model.ProducerName += $" v{onnxModel.ProducerVersion}";

            // add symbolic names to model
            model.symbolicDimNames = namedDims.ToArray();

            // validate imported model
            if (!ImportWarnings.Any(w => w.messageSeverity == WarningType.Error))
            {
                model = ModelValidator.ValidateModel(model);
            }

            // Invoke metadata handlers
            var propDict = new Dictionary<string, string>();
            foreach (var prop in onnxModel.MetadataProps)
            {
                propDict[prop.Key] = prop.Value;
            }

            MetadataLoaded?.Invoke(new ONNXModelMetadata
            {
                DocString = onnxModel.DocString,
                Domain = onnxModel.Domain,
                IRVersion = onnxModel.IrVersion,
                MetadataProps = propDict,
                ProducerName = onnxModel.ProducerName,
                ProducerVersion = onnxModel.ProducerVersion,
                ModelVersion = onnxModel.ModelVersion,
            });

            return model;
        }

        /// <summary>
        /// Validates the ONNX graph structure for unsupported operators and data types BEFORE any processing.
        /// This validates inputs, outputs, initializers, and nodes to catch invalid datatypes early.
        /// </summary>
        /// <param name="onnxGraph">The ONNX graph to validate</param>
        /// <exception cref="OnnxImportException">Thrown when unsupported operators and/or data types are found.</exception>
        void ValidateOnnxGraph(GraphProto onnxGraph)
        {
            // Track unsupported operators and datatypes for error handling
            var unsupportedOperators = new HashSet<string>();
            var unsupportedDataTypes = new HashSet<string>();

            // Validate input datatypes before processing
            foreach (var input in onnxGraph.Input)
            {
                var onnxInputDataType = (TensorProto.Types.DataType)input.Type.TensorType.ElemType;
                var dataStr = onnxInputDataType.ToString();
                ONNXNodeWrapper.DataTypeFromOnnxDataType(onnxInputDataType, OnUnsupported: () =>
                {
                    OnOnnxDataTypeUnsupported?.Invoke(dataStr);
                    unsupportedDataTypes.Add(dataStr);
                });
                // Always track the datatype (supported or unsupported)
                OnOnnxDataType?.Invoke(dataStr);
            }

            // Validate output datatypes before processing
            foreach (var output in onnxGraph.Output)
            {
                if(output.Type == null) // Outputs types might not be inferred yet
                    continue;

                var onnxOutputDataType = (TensorProto.Types.DataType)output.Type.TensorType.ElemType;
                var dataStr = onnxOutputDataType.ToString();
                ONNXNodeWrapper.DataTypeFromOnnxDataType(onnxOutputDataType, OnUnsupported: () =>
                {
                    OnOnnxDataTypeUnsupported?.Invoke(dataStr);
                    unsupportedDataTypes.Add(dataStr);
                });
                // Always track the datatype (supported or unsupported)
                OnOnnxDataType?.Invoke(dataStr);
            }

            // Validate initializer datatypes before processing
            foreach (var initializer in onnxGraph.Initializer)
            {
                var onnxInitializerDataType = (TensorProto.Types.DataType)initializer.DataType;
                var dataStr = onnxInitializerDataType.ToString();
                ONNXNodeWrapper.DataTypeFromOnnxDataType(onnxInitializerDataType, OnUnsupported: () =>
                {
                    OnOnnxDataTypeUnsupported?.Invoke(dataStr);
                    unsupportedDataTypes.Add(dataStr);
                });
                // Always track the datatype (supported or unsupported)
                OnOnnxDataType?.Invoke(dataStr);
            }

            // Validate node operators and attributes
            foreach (var node in onnxGraph.Node)
            {
                if (!IsOperatorSupported(node.OpType))
                {
                    OnOnnxOperatorUnsupported?.Invoke(node.OpType);
                    unsupportedOperators.Add(node.OpType);
                }

                OnOnnxOperator?.Invoke(node.OpType);

                // Check node attribute datatypes (e.g., dtype attribute in Random operators)
                foreach (var attr in node.Attribute)
                {
                    if (attr.Name == "dtype" && attr.I > 0)
                    {
                        var dataTypeValue = (TensorProto.Types.DataType)attr.I;
                        var dataStr = dataTypeValue.ToString();
                        ONNXNodeWrapper.DataTypeFromOnnxDataType(dataTypeValue, OnUnsupported: () =>
                        {
                            OnOnnxDataTypeUnsupported?.Invoke(dataStr);
                            unsupportedDataTypes.Add(dataStr);
                        });
                        // Always track the datatype (supported or unsupported)
                        OnOnnxDataType?.Invoke(dataStr);
                    }
                }
            }

            if (unsupportedOperators.Count > 0)
            {
                var unsupportedOpsMessage = $"Model contains unsupported operator(s): {string.Join(", ", unsupportedOperators)}";
                Warn(WarningType.Error, unsupportedOpsMessage);
            }

            if (unsupportedDataTypes.Count > 0)
            {
                var unsupportedDataMessage = $"Model contains unsupported data type(s): {string.Join(", ", unsupportedDataTypes)}";
                Warn(WarningType.Error, unsupportedDataMessage);
            }

            if (unsupportedDataTypes.Count > 0 || unsupportedOperators.Count > 0)
            {
                throw new OnnxImportException("Model contains unsupported operators or data types. See errors for details.");
            }
        }

        /// <summary>
        /// Represents ONNX operator types.
        /// </summary>
        internal enum ONNXOperatorType
        {
            Unknown,
            Constant,
            Celu,
            Elu,
            Erf,
            Gelu,
            Hardmax,
            HardSigmoid,
            HardSwish,
            LeakyRelu,
            Mish,
            PRelu,
            Relu,
            Selu,
            Sigmoid,
            Softplus,
            Softsign,
            Tanh,
            ThresholdedRelu,
            LogSoftmax,
            Softmax,
            Conv,
            ConvTranspose,
            Shape,
            Size,
            ConstantOfShape,
            Range,
            OneHot,
            ArgMax,
            ArgMin,
            Gather,
            GatherElements,
            GatherND,
            NonZero,
            Scatter,
            ScatterElements,
            ScatterND,
            TopK,
            And,
            Compress,
            Equal,
            Greater,
            GreaterOrEqual,
            IsInf,
            IsNaN,
            Less,
            LessOrEqual,
            Not,
            Or,
            Xor,
            Where,
            Abs,
            Add,
            BitwiseAnd,
            BitwiseNot,
            BitwiseOr,
            BitwiseXor,
            Ceil,
            Clip,
            CumSum,
            Div,
            Einsum,
            Exp,
            Floor,
            Gemm,
            Log,
            MatMul,
            Max,
            Mean,
            Min,
            Mod,
            Mul,
            Neg,
            Pow,
            Reciprocal,
            Round,
            Shrink,
            Sign,
            Sqrt,
            Sub,
            Sum,
            BatchNormalization,
            InstanceNormalization,
            LayerNormalization,
            RMSNormalization,
            LRN,
            NonMaxSuppression,
            RoiAlign,
            AveragePool,
            GlobalAveragePool,
            GlobalMaxPool,
            MaxPool,
            Bernoulli,
            Multinomial,
            RandomNormal,
            RandomNormalLike,
            RandomUniform,
            RandomUniformLike,
            LSTM,
            ReduceL1,
            ReduceL2,
            ReduceLogSum,
            ReduceLogSumExp,
            ReduceMax,
            ReduceMean,
            ReduceMin,
            ReduceProd,
            ReduceSum,
            ReduceSumSquare,
            BlackmanWindow,
            DFT,
            HammingWindow,
            HannWindow,
            MelWeightMatrix,
            STFT,
            Cast,
            CastLike,
            Concat,
            DepthToSpace,
            Expand,
            Flatten,
            GridSample,
            Dropout,
            Identity,
            Pad,
            Reshape,
            Resize,
            Slice,
            SpaceToDepth,
            Split,
            Squeeze,
            Tile,
            Transpose,
            Trilu,
            Upsample,
            Unsqueeze,
            Acos,
            Acosh,
            Asin,
            Asinh,
            Atan,
            Atanh,
            Cos,
            Cosh,
            Sin,
            Sinh,
            Tan,
            Swish,
            ImageScaler
        }

    }

    /// <summary>
    /// Represents an exception during the import of an ONNX model.
    /// </summary>
    class OnnxImportException : ImportException
    {
        /// <inheritdoc cref="ImportException"/>
        public OnnxImportException(string message) : base(message) { }
    }

    /// <summary>
    /// Represents an exception during the import of a ONNX layer.
    /// </summary>
    class OnnxLayerImportException : LayerImportException
    {
        /// <inheritdoc cref="LayerImportException"/>
        public OnnxLayerImportException(string message) : base(message) { }
    }

    /// <summary>
    /// Represents an exception for unsupported ONNX operators.
    /// </summary>
    class OnnxUnsupportedOpException : OnnxImportException
    {
        public readonly string OpName;

        public OnnxUnsupportedOpException(string op)
            : base($"Unsupported ONNX Operator: {op}")
        {
            OpName = op;
        }
    }
}
