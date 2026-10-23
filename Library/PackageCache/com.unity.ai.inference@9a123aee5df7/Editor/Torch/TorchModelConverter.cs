using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using TorchPt2;
using Unity.InferenceEngine.Graph;
using GraphModule = Unity.InferenceEngine.Graph.GraphModule;
#if SENTIS_ANALYTICS_ENABLED
using Unity.InferenceEngine.Editor.Analytics.Import;
#endif

namespace Unity.InferenceEngine.Editor.Torch
{
    /// <summary>
    /// Represents a converter from a Torch model to Sentis format.
    /// </summary>
    class TorchModelConverter : ModelConverterBase
    {
        readonly System.Version m_SupportedTorchVersion = new("2.9.1");

        // Analytics events
        internal event Action<ExportedProgram> OnTorchModelLoaded;
        internal event Action<string> OnTorchOperator;
        internal event Action<string> OnTorchOperatorUnsupported;
        internal event Action<string> OnTorchDataType;
        internal event Action<string> OnTorchDataTypeUnsupported;

        // Supported operators set for validation
        static readonly HashSet<string> k_SupportedOperators = new()
        {
            "torch.ops.aten._adaptive_avg_pool2d.default",
            "torch.ops.aten._adaptive_avg_pool3d.default",
            "torch.ops.aten._local_scalar_dense.default",
            "torch.ops.aten._log_softmax.default",
            "torch.ops.aten._native_batch_norm_legit.default",
            "torch.ops.aten._native_batch_norm_legit_functional.default",
            "torch.ops.aten._native_batch_norm_legit_no_training.default",
            "torch.ops.aten._softmax.default",
            "torch.ops.aten._to_copy.default",
            "torch.ops.aten.abs.default",
            "torch.ops.aten.acos.default",
            "torch.ops.aten.acosh.default",
            "torch.ops.aten.adaptive_avg_pool1d.default",
            "torch.ops.aten.add.Scalar",
            "torch.ops.aten.add.Tensor",
            "torch.ops.aten.addmm.default",
            "torch.ops.aten.alias.default",
            "torch.ops.aten.amax.default",
            "torch.ops.aten.amin.default",
            "torch.ops.aten.any.default",
            "torch.ops.aten.any.dim",
            "torch.ops.aten.any.dims",
            "torch.ops.aten.arange.start_step",
            "torch.ops.aten.argmax.default",
            "torch.ops.aten.argmin.default",
            "torch.ops.aten.as_strided.default",
            "torch.ops.aten.asin.default",
            "torch.ops.aten.asinh.default",
            "torch.ops.aten.atan.default",
            "torch.ops.aten.atan2.default",
            "torch.ops.aten.atan2.out",
            "torch.ops.aten.atanh.default",
            "torch.ops.aten.avg_pool1d.default",
            "torch.ops.aten.avg_pool2d.default",
            "torch.ops.aten.avg_pool3d.default",
            "torch.ops.aten.bitwise_and.Scalar",
            "torch.ops.aten.bitwise_and.Tensor",
            "torch.ops.aten.bitwise_not.default",
            "torch.ops.aten.bitwise_or.Scalar",
            "torch.ops.aten.bitwise_or.Tensor",
            "torch.ops.aten.bitwise_xor.Scalar",
            "torch.ops.aten.bitwise_xor.Tensor",
            "torch.ops.aten.bmm.default",
            "torch.ops.aten.cat.default",
            "torch.ops.aten.ceil.default",
            "torch.ops.aten.clamp.default",
            "torch.ops.aten.clamp.Tensor",
            "torch.ops.aten.clone.default",
            "torch.ops.aten.constant_pad_nd.default",
            "torch.ops.aten.convolution.default",
            "torch.ops.aten.copy.default",
            "torch.ops.aten.cos.default",
            "torch.ops.aten.cosh.default",
            "torch.ops.aten.cumsum.default",
            "torch.ops.aten.diagonal.default",
            "torch.ops.aten.div.Scalar",
            "torch.ops.aten.div.Scalar_mode",
            "torch.ops.aten.div.Tensor",
            "torch.ops.aten.div.Tensor_mode",
            "torch.ops.aten.elu.default",
            "torch.ops.aten.embedding.default",
            "torch.ops.aten.empty.memory_format",
            "torch.ops.aten.empty_strided.default",
            "torch.ops.aten.eq.Scalar",
            "torch.ops.aten.eq.Tensor",
            "torch.ops.aten.erf.default",
            "torch.ops.aten.exp.default",
            "torch.ops.aten.expand.default",
            "torch.ops.aten.expm1.default",
            "torch.ops.aten.fill.Scalar",
            "torch.ops.aten.flip.default",
            "torch.ops.aten.floor.default",
            "torch.ops.aten.floor_divide.default",
            "torch.ops.aten.fmod.Scalar",
            "torch.ops.aten.fmod.Tensor",
            "torch.ops.aten.full.default",
            "torch.ops.aten.full_like.default",
            "torch.ops.aten.gather.default",
            "torch.ops.aten.ge.Scalar",
            "torch.ops.aten.ge.Tensor",
            "torch.ops.aten.gelu.default",
            "torch.ops.aten.grid_sampler_2d.default",
            "torch.ops.aten.gt.Scalar",
            "torch.ops.aten.gt.Tensor",
            "torch.ops.aten.hardtanh.default",
            "torch.ops.aten.index.Tensor",
            "torch.ops.aten.index_put.default",
            "torch.ops.aten.index_select.default",
            "torch.ops.aten.isinf.default",
            "torch.ops.aten.isnan.default",
            "torch.ops.aten.le.Scalar",
            "torch.ops.aten.le.Tensor",
            "torch.ops.aten.leaky_relu.default",
            "torch.ops.aten.log.default",
            "torch.ops.aten.log10.default",
            "torch.ops.aten.log1p.default",
            "torch.ops.aten.log2.default",
            "torch.ops.aten.logical_and.default",
            "torch.ops.aten.logical_not.default",
            "torch.ops.aten.logical_or.default",
            "torch.ops.aten.logical_xor.default",
            "torch.ops.aten.lt.Scalar",
            "torch.ops.aten.lt.Tensor",
            "torch.ops.aten.max.dim",
            "torch.ops.aten.max_pool2d_with_indices.default",
            "torch.ops.aten.max_pool3d_with_indices.default",
            "torch.ops.aten.maximum.default",
            "torch.ops.aten.mean.default",
            "torch.ops.aten.mean.dim",
            "torch.ops.aten.min.dim",
            "torch.ops.aten.minimum.default",
            "torch.ops.aten.mm.default",
            "torch.ops.aten.mul.Scalar",
            "torch.ops.aten.mul.Tensor",
            "torch.ops.aten.native_dropout.default",
            "torch.ops.aten.native_group_norm.default",
            "torch.ops.aten.native_layer_norm.default",
            "torch.ops.aten.ne.Scalar",
            "torch.ops.aten.ne.Tensor",
            "torch.ops.aten.neg.default",
            "torch.ops.aten.nonzero.default",
            "torch.ops.aten.permute.default",
            "torch.ops.aten.pow.Scalar",
            "torch.ops.aten.pow.Tensor_Scalar",
            "torch.ops.aten.pow.Tensor_Tensor",
            "torch.ops.aten.prod.default",
            "torch.ops.aten.prod.dim_int",
            "torch.ops.aten.rand.default",
            "torch.ops.aten.randn.default",
            "torch.ops.aten.randperm.default",
            "torch.ops.aten.reciprocal.default",
            "torch.ops.aten.reflection_pad1d.default",
            "torch.ops.aten.reflection_pad2d.default",
            "torch.ops.aten.reflection_pad3d.default",
            "torch.ops.aten.relu.default",
            "torch.ops.aten.remainder.Scalar",
            "torch.ops.aten.remainder.Tensor",
            "torch.ops.aten.repeat.default",
            "torch.ops.aten.replication_pad2d.default",
            "torch.ops.aten.replication_pad3d.default",
            "torch.ops.aten.resize_.default",
            "torch.ops.aten.round.default",
            "torch.ops.aten.rsqrt.default",
            "torch.ops.aten.scalar_tensor.default",
            "torch.ops.aten.scatter.src",
            "torch.ops.aten.scatter.value",
            "torch.ops.aten.scatter_add.default",
            "torch.ops.aten.scatter_reduce.two",
            "torch.ops.aten.select.int",
            "torch.ops.aten.select_scatter.default",
            "torch.ops.aten.sigmoid.default",
            "torch.ops.aten.sign.default",
            "torch.ops.aten.sin.default",
            "torch.ops.aten.sinh.default",
            "torch.ops.aten.slice.Tensor",
            "torch.ops.aten.slice_scatter.default",
            "torch.ops.aten.sort.default",
            "torch.ops.aten.split_with_sizes.default",
            "torch.ops.aten.sqrt.default",
            "torch.ops.aten.squeeze.dim",
            "torch.ops.aten.squeeze.dims",
            "torch.ops.aten.sub.Scalar",
            "torch.ops.aten.sub.Tensor",
            "torch.ops.aten.sum.dim_IntList",
            "torch.ops.aten.sym_numel.default",
            "torch.ops.aten.sym_size.int",
            "torch.ops.aten.sym_storage_offset.default",
            "torch.ops.aten.sym_stride.int",
            "torch.ops.aten.tan.default",
            "torch.ops.aten.tanh.default",
            "torch.ops.aten.topk.default",
            "torch.ops.aten.trunc.default",
            "torch.ops.aten.unsqueeze.default",
            "torch.ops.aten.upsample_bilinear2d.vec",
            "torch.ops.aten.upsample_nearest2d.vec",
            "torch.ops.aten.var.correction",
            "torch.ops.aten.var.dim",
            "torch.ops.aten.view.default",
            "torch.ops.aten.where.self",
        };

        /// <summary>
        /// Checks if an operator is supported by the converter.
        /// </summary>
        public static bool IsOperatorSupported(string opType)
        {
            return k_SupportedOperators.Contains(opType);
        }

        /// <summary>
        /// Checks if a scalar type is supported by the converter.
        /// </summary>
        public static bool IsDataTypeSupported(ScalarType scalarType)
        {
            return scalarType switch
            {
                ScalarType.BYTE => true,
                ScalarType.CHAR => true,
                ScalarType.SHORT => true,
                ScalarType.INT => true,
                ScalarType.LONG => true,
                ScalarType.HALF => true,
                ScalarType.FLOAT => true,
                ScalarType.DOUBLE => true,
                ScalarType.BOOL => true,
                ScalarType.BFLOAT16 => true,
                ScalarType.UINT16 => true,
                ScalarType.FLOAT8E4M3FN => true,
                ScalarType.FLOAT8E5M2 => true,
                _ => false
            };
        }

        /// <summary>
        /// Initializes and returns an instance of `TorchModelConverter`.
        /// </summary>
        public TorchModelConverter(string filePath)
            : base(filePath) { }

        /// <summary>
        /// Parse a PyTorch version string to extract major and minor version components.
        /// </summary>
        static System.Version ParseTorchVersion(string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
                return new System.Version(0, 0);

            var parts = versionString.Split('.');
            if (parts.Length < 2)
                return new System.Version(0, 0);

            if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
                return new System.Version(0, 0);

            return new System.Version(major, minor);
        }

        void GetTensorMeta(TorchContext ctx, ExportedProgram exportedProgram, string tensorName, out ScalarType dtype, out DynamicTensorShape shape)
        {
            var tensorMeta = exportedProgram.graph_module.graph.tensor_values[tensorName];
            dtype = tensorMeta.dtype;
            shape = DynamicTensorShape.DynamicOfRank(tensorMeta.sizes.Count);
            for (var i = 0; i < tensorMeta.sizes.Count; i++)
            {
                var dim = tensorMeta.sizes[i];
                switch (dim.kind)
                {
                    case "as_int":
                    {
                        shape[i] = DynamicTensorDim.Int((int)dim.value);
                        break;
                    }
                    case "as_expr":
                    {
                        var symExpr = (SymExpr)dim.value;
                        shape[i] = ctx.GetSymbolicDim(symExpr.expr_str);
                        break;
                    }
                }
            }
        }

        internal void OnNode(TorchContext ctx, TorchNode node)
        {
            var gm = ctx.gm;

            void SetOutput(Graph.Node output, ScalarType scalarType, int index = 0)
            {
                var outputName = (node.GetOutput(index).value as TensorArgument)?.name;
                if (string.IsNullOrEmpty(outputName))
                    return;
                ctx.AddTensor(outputName, output, scalarType);
            }

            void SetUnsupportedTensor(string target, string tensorName, bool isInput = false)
            {
                var inputOrOutput = isInput ? "input" : "output";
                ctx.AddUnsupportedTensor(tensorName, $"{target} {inputOrOutput} \"{tensorName}\" is not supported");
            }

            void SetOutputSymInt(Graph.Node output, int index = 0)
            {
                var symIntArg = (SymIntArgument)node.GetOutput(index).value;
                AssertTrue(symIntArg.kind != "as_name", "Named symbolic ints are not supported");
                var outputName = (string)symIntArg.value;
                if (string.IsNullOrEmpty(outputName))
                    return;
                ctx.AddSymInt(outputName, output);
            }

            void SetOutputSymFloat(Graph.Node output, int index = 0)
            {
                var symFloatArg = (SymFloatArgument)node.GetOutput(index).value;
                AssertTrue(symFloatArg.kind != "as_name", "Named symbolic floats are not supported");
                var outputName = (string)symFloatArg.value;
                if (string.IsNullOrEmpty(outputName))
                    return;
                ctx.AddSymFloat(outputName, output);
            }

            void SetOutputs(Graph.Node[] outputs, ScalarType scalarType, int index = 0)
            {
                var tensorArgs = (List<TensorArgument>)node.GetOutput(index).value;

                for (var i = 0; i < tensorArgs.Count; i++)
                {
                    var outputName = tensorArgs[i]?.name;
                    if (string.IsNullOrEmpty(outputName))
                        return;
                    ctx.AddTensor(outputName, outputs[i], scalarType);
                }
            }

            void ExplicitPadding(ref Graph.Node self, int[] padding, ScalarType scalarType)
            {
                var axesList = new System.Collections.Generic.List<int>();
                var padsList = new System.Collections.Generic.List<int>();

                for (var i = 0; i < padding.Length; i++)
                {
                    if (padding[i] != 0)
                        axesList.Add(-padding.Length + i);
                }

                for (var j = 0; j < 2; j++)
                {
                    for (var i = 0; i < padding.Length; i++)
                    {
                        if (padding[i] != 0)
                            padsList.Add(padding[i]);
                    }
                }

                if (axesList.Count > 0)
                {
                    self = gm.Pad(self, gm.Constant(padsList.ToArray()), null, gm.Constant(axesList.ToArray()), Layers.PadMode.Constant);
                    ctx.scalarTypes[self] = scalarType;
                }
            }

            var target = node.target;
            switch (target)
            {
                case "torch.ops.aten._adaptive_avg_pool2d.default": // _adaptive_avg_pool2d(Tensor self, SymInt[2] output_size) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var outputSize = node.Args[1].SymIntsAsInts();
                    var kernelShape = new int[2];
                    var strides = new int[2];
                    var pads = new int[4];
                    for (var i = 0; i < 2; i++)
                    {
                        var inputTensorDim = self.partialTensor.shape[-2 + i];
                        AssertTrue(inputTensorDim.dimType == DimType.Static, $"\"self\" shape is {self.partialTensor.shape}. Dimension {self.partialTensor.shape.rank -2 + i} must be static");
                        var inputSize = inputTensorDim.value;
                        AssertTrue(inputSize % outputSize[i] == 0, $"Expecting inputSize {inputSize} to be divisible by outputSize {outputSize[i]}. Windows of different sizes are not supported");
                        strides[i] = inputSize / outputSize[i];
                        kernelShape[i] = inputSize - (outputSize[i] - 1) * strides[i];
                    }
                    SetOutput(gm.AveragePool(self, kernelShape, strides, pads, Layers.AutoPad.NotSet), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten._adaptive_avg_pool2d_backward.default": // _adaptive_avg_pool2d_backward(Tensor grad_output, Tensor self) -> Tensor
                {
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten._adaptive_avg_pool3d.default": // _adaptive_avg_pool3d(Tensor self, SymInt[3] output_size) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var outputSize = node.Args[1].SymIntsAsInts();
                    var kernelShape = new int[3];
                    var strides = new int[3];
                    var pads = new int[6];
                    for (var i = 0; i < 3; i++)
                    {
                        var inputTensorDim = self.partialTensor.shape[-3 + i];
                        AssertTrue(inputTensorDim.dimType == DimType.Static, $"\"self\" shape is {self.partialTensor.shape}. Dimension {self.partialTensor.shape.rank -3 + i} must be static");
                        var inputSize = inputTensorDim.value;
                        AssertTrue(inputSize % outputSize[i] == 0, $"Expecting inputSize {inputSize} to be divisible by outputSize {outputSize[i]}. Windows of different sizes are not supported");
                        strides[i] = inputSize / outputSize[i];
                        kernelShape[i] = inputSize - (outputSize[i] - 1) * strides[i];
                    }
                    SetOutput(gm.AveragePool(self, kernelShape, strides, pads, Layers.AutoPad.NotSet), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten._cdist_forward.default": // _cdist_forward(Tensor x1, Tensor x2, float p, int? compute_mode) -> Tensor
                {
                    // TODO implement, this should be possible in some cases, torch.onnx can't seem to export it though. SENTIS-1228
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten._embedding_bag.default": // _embedding_bag(Tensor weight, Tensor indices, Tensor offsets, bool scale_grad_by_freq=False, int mode=0, bool sparse=False, Tensor? per_sample_weights=None, bool include_last_offset=False, int padding_idx=-1) -> (Tensor, Tensor, Tensor, Tensor)
                {
                    // TODO implement SENTIS-1229
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten._fft_r2c.default": // _fft_r2c(Tensor self, int[] dim, int normalization, bool onesided) -> Tensor
                {
                    // TODO implement SENTIS-1230
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten._local_scalar_dense.default": // _local_scalar_dense(Tensor self) -> Scalar
                {
                    var self = node.Args[0].Tensor();
                    if (self.partialTensor.dataType == DataType.Float)
                        SetOutputSymFloat(gm.Identity(self));
                    else if (self.partialTensor.dataType == DataType.Int)
                        SetOutputSymInt(gm.Identity(self));
                    break;
                }
                case "torch.ops.aten._log_softmax.default": // _log_softmax(Tensor self, int dim, bool half_to_float) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var halfToFloat = node.Args[2].Bool();
                    var scalarType = ctx.ScalarType(self);
                    if (halfToFloat && scalarType == ScalarType.HALF)
                        scalarType = ScalarType.FLOAT;
                    SetOutput(gm.LogSoftmax(self, dim), scalarType);
                    break;
                }
                case "torch.ops.aten._native_batch_norm_legit.default": // _native_batch_norm_legit(Tensor input, Tensor? weight, Tensor? bias, Tensor(a!) running_mean, Tensor(b!) running_var, bool training, float momentum, float eps) -> (Tensor, Tensor, Tensor)
                {
                    var input = node.Args[0].Tensor();
                    var weight = node.Args[1].Tensor();
                    var bias = node.Args[2].Tensor();
                    var runningMean = node.Args[3].Tensor();
                    var runningVar = node.Args[4].Tensor();
                    var training = node.Args[5].Bool();
                    AssertValue(training, false, "training");
                    WarnArgFloatIgnored(6, "momentum", 0f);
                    var eps = node.Args[7].Float();
                    SetOutput(gm.BatchNormalization(input, weight, bias, runningMean, runningVar, eps), ctx.ScalarType(input));
                    SetUnsupportedTensor(target, "mean");
                    SetUnsupportedTensor(target, "invstd");
                    break;
                }
                case "torch.ops.aten._native_batch_norm_legit_functional.default": // _native_batch_norm_legit_functional(Tensor input, Tensor? weight, Tensor? bias, Tensor(a!) running_mean, Tensor(b!) running_var, bool training, float momentum, float eps) -> (Tensor, Tensor, Tensor, Tensor, Tensor)
                {
                    var input = node.Args[0].Tensor();
                    var weight = node.Args[1].Tensor();
                    var bias = node.Args[2].Tensor();
                    var runningMean = node.Args[3].Tensor();
                    var runningVar = node.Args[4].Tensor();
                    var training = node.Args[5].Bool();
                    AssertValue(training, false, "training");
                    WarnArgFloatIgnored(6, "momentum", 0f);
                    var eps = node.Args[7].Float();
                    SetOutput(gm.BatchNormalization(input, weight, bias, runningMean, runningVar, eps), ctx.ScalarType(input));
                    SetUnsupportedTensor(target, "updated_output");
                    SetUnsupportedTensor(target, "updated_running_mean");
                    SetUnsupportedTensor(target, "updated_running_var");
                    SetUnsupportedTensor(target, "updated_save_stats");
                    break;
                }
                case "torch.ops.aten._native_batch_norm_legit.no_stats": // _native_batch_norm_legit.no_stats(Tensor input, Tensor? weight, Tensor? bias, bool training, float momentum, float eps) -> (Tensor, Tensor, Tensor)
                {
                    // TODO implement SENTIS-1231
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten._native_batch_norm_legit_no_training.default": // _native_batch_norm_legit_no_training(Tensor input, Tensor? weight, Tensor? bias, Tensor running_mean, Tensor running_var, float momentum, float eps) -> (Tensor, Tensor, Tensor)
                {
                    var input = node.Args[0].Tensor();
                    var weight = node.Args[1].Tensor();
                    var bias = node.Args[2].Tensor();
                    var runningMean = node.Args[3].Tensor();
                    var runningVar = node.Args[4].Tensor();
                    WarnArgFloatIgnored(5, "momentum", 0f);
                    var eps = node.Args[6].Float();
                    SetOutput(gm.BatchNormalization(input, weight, bias, runningMean, runningVar, eps), ctx.ScalarType(input));
                    SetUnsupportedTensor(target, "mean");
                    SetUnsupportedTensor(target, "invstd");
                    break;
                }
                case "torch.ops.aten._pdist_forward.default": // _pdist_forward(Tensor self, float p=2) -> Tensor
                {
                    // TODO implement SENTIS-761
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten._softmax.default": // _softmax(Tensor self, int dim, bool half_to_float) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var halfToFloat = node.Args[2].Bool();
                    var scalarType = ctx.ScalarType(self);
                    if (halfToFloat && scalarType == ScalarType.HALF)
                        scalarType = ScalarType.FLOAT;
                    SetOutput(gm.Softmax(self, dim), scalarType);
                    break;
                }
                case "torch.ops.aten._to_copy.default": // _to_copy(Tensor self, *, ScalarType? dtype=None, Layout? layout=None, Device? device=None, bool? pin_memory=None, bool non_blocking=False, MemoryFormat? memory_format=None) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    WarnKeywordArgDtypeIgnored("dtype", ctx.ScalarType(self));
                    WarnKeywordArgIgnored("layout");
                    WarnKeywordArgIgnored("device");
                    WarnKeywordArgBoolIgnored("pin_memory", false);
                    WarnKeywordArgBoolIgnored("non_blocking", false);
                    WarnKeywordArgIgnored("memory_format");
                    SetOutput(gm.Identity(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.abs.default": // abs(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Abs(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.acos.default": // acos(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Acos(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.acosh.default": // acosh(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Acosh(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.adaptive_avg_pool1d.default": // adaptive_avg_pool1d(Tensor self, int[1] output_size) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var outputSize = node.Args[1].SymIntsAsInts();
                    var kernelShape = new int[1];
                    var strides = new int[1];
                    var pads = new int[2];
                    for (var i = 0; i < 1; i++)
                    {
                        var inputTensorDim = self.partialTensor.shape[-1 + i];
                        AssertTrue(inputTensorDim.dimType == DimType.Static, $"\"self\" shape is {self.partialTensor.shape}. Dimension {self.partialTensor.shape.rank -1 + i} must be static");
                        var inputSize = inputTensorDim.value;
                        AssertTrue(inputSize % outputSize[i] == 0, $"Expecting inputSize {inputSize} to be divisible by outputSize {outputSize[i]}. Windows of different sizes are not supported");
                        strides[i] = inputSize / outputSize[i];
                        kernelShape[i] = inputSize - (outputSize[i] - 1) * strides[i];
                    }
                    SetOutput(gm.AveragePool(self, kernelShape, strides, pads, Layers.AutoPad.NotSet), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.add.Scalar": // add.Scalar(Tensor self, Scalar other, Scalar alpha=1) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var alpha = node.Args[2].ScalarAsTensor(1);
                    var (nodes, scalarType) = PromoteTypes(new [] {self, other, alpha});
                    var scaledOther = gm.Mul(nodes[1], nodes[2]);
                    var output = gm.Add(nodes[0], scaledOther);
                    output = ConvertToUint8IfNeeded(scalarType, output);
                    SetOutput(output, scalarType);
                    break;
                }
                case "torch.ops.aten.add.Tensor": // add.Tensor(Tensor self, Tensor other, *, Scalar alpha=1) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var alpha = node.KeywordArgs["alpha"].ScalarAsTensor(1);
                    var (nodes, scalarType) = PromoteTypes(new [] {self, other, alpha});
                    var scaledOther = gm.Mul(nodes[1], nodes[2]);
                    SetOutput(gm.Add(nodes[0], scaledOther), scalarType);
                    break;
                }
                case "torch.ops.aten.addmm.default": // addmm(Tensor self, Tensor mat1, Tensor mat2, *, Scalar beta=1, Scalar alpha=1) -> Tensor
                {
                    // beta * self + alpha * (mat1 @ mat2)
                    var self = node.Args[0].Tensor();
                    var mat1 = node.Args[1].Tensor();
                    var mat2 = node.Args[2].Tensor();
                    var beta = node.KeywordArgs["beta"].ScalarAsTensor(1);
                    var alpha = node.KeywordArgs["alpha"].ScalarAsTensor(1);
                    var matAllInts = AllInts(new[] { self, mat1, mat2 });
                    if (matAllInts)
                    {
                        beta = PromoteTypeInt(beta);
                        alpha = PromoteTypeInt(alpha);
                    }
                    var nodes = PromoteTypesFloat(new[] { self, mat1, mat2, beta, alpha });

                    var scaledSelf = gm.Mul(nodes[3], nodes[0]);
                    var matmul = gm.MatMul(nodes[1], nodes[2]);
                    var scaledMatmul = gm.Mul(nodes[4], matmul);
                    var output = gm.Add(scaledSelf, scaledMatmul);
                    if (matAllInts)
                        output = gm.Cast(output, DataType.Int);
                    SetOutput(output, matAllInts ? ScalarType.LONG : ScalarType.FLOAT);
                    break;
                }
                case "torch.ops.aten.alias.default": // alias(Tensor(a) self) -> Tensor(a)
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Identity(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.amax.default": // amax(Tensor self, int[1] dim=[], bool keepdim=False) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Ints(Array.Empty<int>());
                    var keepdim = node.Args[2].Bool(false);
                    SetOutput(gm.ReduceMax(self, gm.Constant(dim), keepdim, noopWithEmptyAxes: false), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.amin.default": // amin(Tensor self, int[1] dim=[], bool keepdim=False) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Ints(Array.Empty<int>());
                    var keepdim = node.Args[2].Bool(false);
                    SetOutput(gm.ReduceMin(self, gm.Constant(dim), keepdim, noopWithEmptyAxes: false), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.any.default": // any(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    self = PromoteTypeBool(self, true);
                    var output = gm.ReduceMax(self, gm.Constant(Array.Empty<int>()), keepdims: false, noopWithEmptyAxes: false);
                    output = gm.Max(output, gm.Constant(0));
                    SetOutput(output, ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.any.dim": // any.dim(Tensor self, int dim, bool keepdim=False) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var keepdim = node.Args[2].Bool(false);
                    self = PromoteTypeBool(self, true);
                    var output = gm.ReduceMax(self, gm.Constant(new[] { dim }), keepdims: keepdim, noopWithEmptyAxes: false);
                    output = gm.Max(output, gm.Constant(0));
                    SetOutput(output, ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.any.dims": // any.dims(Tensor self, int[]? dim=None, bool keepdim=False) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Ints(Array.Empty<int>());
                    var keepdim = node.Args[2].Bool(false);
                    self = PromoteTypeBool(self, true);
                    var output = gm.ReduceMax(self, gm.Constant(dim), keepdims: keepdim, noopWithEmptyAxes: false);
                    output = gm.Max(output, gm.Constant(0));
                    SetOutput(output, ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.arange.start_step": // arange.start_step(Scalar start, Scalar end, Scalar step=1, *, ScalarType? dtype=None, Layout? layout=None, Device? device=None, bool? pin_memory=None) -> Tensor
                {
                    var dtype = node.KeywordArgs["dtype"].OptionalScalarType();
                    var start = node.Args[0].ScalarAsTensor();
                    var end = node.Args[1].ScalarAsTensor();
                    var step = node.Args[2].ScalarAsTensor(1);
                    var nodes = new[] { start, end, step };
                    var allInts = AllInts(nodes);
                    var scalarType = dtype.GetValueOrDefault(allInts ? ScalarType.LONG : ScalarType.FLOAT);
                    var outputDataType = TorchUtilities.ScalarTypeToDataType(scalarType);
                    // To support float args with int dtype, we would need to add unique operator SENTIS-902
                    AssertTrue(allInts || outputDataType == DataType.Float, $"{target} with floating arguments and integer dtype is not supported");
                    nodes = (outputDataType == DataType.Int) ? PromoteTypesInt(nodes) : PromoteTypesFloat(nodes);
                    WarnKeywordArgIgnored("layout");
                    WarnKeywordArgIgnored("device");
                    WarnKeywordArgBoolIgnored("pin_memory", false);
                    var output = gm.Range(nodes[0], nodes[1], nodes[2]);
                    output = (outputDataType == DataType.Int) ? PromoteTypeInt(output) : PromoteTypeFloat(output);
                    SetOutput(output, scalarType);
                    break;
                }
                case "torch.ops.aten.argmax.default": // argmax(Tensor self, int? dim=None, bool keepdim=False) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].OptionalInt();
                    var keepdim = node.Args[2].Bool(false);
                    if (dim.HasValue)
                    {
                        SetOutput(gm.ArgMax(self, dim.Value, keepdims: keepdim, selectLastIndex: false), ScalarType.LONG);
                    }
                    else
                    {
                        // flatten input
                        var flattenedSelf = gm.Reshape(self, gm.Constant(new[] { -1 }), true);
                        SetOutput(gm.ArgMax(flattenedSelf, 0, keepdims: keepdim, selectLastIndex: false), ScalarType.LONG);
                    }
                    break;
                }
                case "torch.ops.aten.argmin.default": // argmin(Tensor self, int? dim=None, bool keepdim=False) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].OptionalInt();
                    var keepdim = node.Args[2].Bool(false);
                    if (dim.HasValue)
                    {
                        SetOutput(gm.ArgMin(self, dim.Value, keepdims: keepdim, selectLastIndex: false), ScalarType.LONG);
                    }
                    else
                    {
                        // flatten input
                        var flattenedSelf = gm.Reshape(self, gm.Constant(new[] { -1 }), true);
                        SetOutput(gm.ArgMin(flattenedSelf, 0, keepdims: keepdim, selectLastIndex: false), ScalarType.LONG);
                    }
                    break;
                }
                case "torch.ops.aten.as_strided.default": // as_strided(Tensor(a) self, SymInt[] size, SymInt[] stride, SymInt? storage_offset=None) -> Tensor(a)
                {
                    var self = node.Args[0].Tensor();
                    var size = node.Args[1].SymIntsAsTensor();
                    var stride = node.Args[2].SymIntsAsTensor();
                    var storageOffset = node.Args[3].SymIntAsTensor(gm.Constant(0));
                    SetOutput(gm.AsStrided(self, size, stride, storageOffset), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.asin.default": // asin(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Asin(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.asinh.default": // asinh(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Asinh(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.atan.default": // atan(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Atan(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.atan2.default": // atan2(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var nodes = PromoteTypesFloat(new [] {self, other});
                    SetOutput(gm.Atan2(nodes[0], nodes[1]), ScalarType.FLOAT);
                    break;
                }
                case "torch.ops.aten.atan2.out": // atan2.out(Tensor self, Tensor other, *, Tensor(a!) out) -> Tensor(a!)
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    SetUnsupportedTensor(target, "out", isInput: true);
                    var nodes = PromoteTypesFloat(new [] {self, other});
                    SetOutput(gm.Atan2(nodes[0], nodes[1]), ScalarType.FLOAT);
                    break;
                }
                case "torch.ops.aten.atanh.default": // atanh(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Atanh(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.avg_pool1d.default": // avg_pool1d(Tensor self, int[1] kernel_size, int[1] stride=[], int[1] padding=0, bool ceil_mode=False, bool count_include_pad=True) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var kernelSize = node.Args[1].Ints();
                    var stride = node.Args[2].Ints(kernelSize);
                    var padding = node.Args[3].Ints(new[] { 0 });
                    var countIncludePad = node.Args[5].Bool(true);
                    WarnArgBoolIgnored(4, "ceil_mode", false);
                    var scalarType = ctx.ScalarType(self);

                    if (countIncludePad && padding.Any(p => p != 0))
                    {
                        ExplicitPadding(ref self, padding, scalarType);
                        var zeroPads = new int[2 * padding.Length];
                        SetOutput(gm.AveragePool(self, kernelSize, stride, zeroPads, Layers.AutoPad.NotSet), scalarType);
                    }
                    else
                    {
                        // TODO check autopadding stuff and padding being able to be an integer
                        var pads = new int[2 * padding.Length];
                        for (var i = 0; i < padding.Length; i++)
                        {
                            pads[i] = padding[i];
                            pads[padding.Length + i] = padding[i];
                        }
                        SetOutput(gm.AveragePool(self, kernelSize, stride, pads, Layers.AutoPad.NotSet), scalarType);
                    }
                    break;
                }
                case "torch.ops.aten.avg_pool2d.default": // avg_pool2d(Tensor self, int[2] kernel_size, int[2] stride=[], int[2] padding=0, bool ceil_mode=False, bool count_include_pad=True, int? divisor_override=None) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var kernelSize = node.Args[1].Ints();
                    var stride = node.Args[2].Ints(kernelSize);
                    var padding = node.Args[3].Ints(new[] { 0, 0 });
                    var countIncludePad = node.Args[5].Bool(true);
                    WarnArgBoolIgnored(4, "ceil_mode", false);
                    WarnArgIgnored(6, "divisor_override");
                    var scalarType = ctx.ScalarType(self);

                    if (countIncludePad && padding.Any(p => p != 0))
                    {
                        ExplicitPadding(ref self, padding, scalarType);
                        var zeroPads = new int[2 * padding.Length];
                        SetOutput(gm.AveragePool(self, kernelSize, stride, zeroPads, Layers.AutoPad.NotSet), scalarType);
                    }
                    else
                    {
                        // TODO check autopadding stuff and padding being able to be an integer
                        var pads = new int[2 * padding.Length];
                        for (var i = 0; i < padding.Length; i++)
                        {
                            pads[i] = padding[i];
                            pads[padding.Length + i] = padding[i];
                        }
                        SetOutput(gm.AveragePool(self, kernelSize, stride, pads, Layers.AutoPad.NotSet), scalarType);
                    }
                    break;
                }
                case "torch.ops.aten.avg_pool2d_backward.default": // avg_pool2d_backward(Tensor grad_output, Tensor self, int[2] kernel_size, int[2] stride, int[2] padding, bool ceil_mode, bool count_include_pad, int? divisor_override) -> Tensor
                {
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten.avg_pool3d.default": // avg_pool3d(Tensor self, int[3] kernel_size, int[3] stride=[], int[3] padding=0, bool ceil_mode=False, bool count_include_pad=True, int? divisor_override=None) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var kernelSize = node.Args[1].Ints();
                    var stride = node.Args[2].Ints(kernelSize);
                    var padding = node.Args[3].Ints(new[] { 0, 0, 0 });
                    var countIncludePad = node.Args[5].Bool(true);
                    WarnArgBoolIgnored(4, "ceil_mode", false);
                    WarnArgIgnored(6, "divisor_override");
                    var scalarType = ctx.ScalarType(self);

                    if (countIncludePad && padding.Any(p => p != 0))
                    {
                        ExplicitPadding(ref self, padding, scalarType);
                        var zeroPads = new int[2 * padding.Length];
                        SetOutput(gm.AveragePool(self, kernelSize, stride, zeroPads, Layers.AutoPad.NotSet), scalarType);
                    }
                    else
                    {
                        // TODO check autopadding stuff and padding being able to be an integer
                        var pads = new int[2 * padding.Length];
                        for (var i = 0; i < padding.Length; i++)
                        {
                            pads[i] = padding[i];
                            pads[padding.Length + i] = padding[i];
                        }
                        SetOutput(gm.AveragePool(self, kernelSize, stride, pads, Layers.AutoPad.NotSet), scalarType);
                    }
                    break;
                }
                case "torch.ops.aten.bitwise_and.Scalar": // bitwise_and.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    SetOutput(gm.BitwiseAnd(self, other), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.bitwise_and.Tensor": // bitwise_and.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    SetOutput(gm.BitwiseAnd(self, other), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.bitwise_not.default": // bitwise_not(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var scalarType = ctx.ScalarType(self);
                    var output = gm.BitwiseNot(self);
                    output = ConvertToUint8IfNeeded(scalarType, output);
                    SetOutput(output, scalarType);
                    break;
                }
                case "torch.ops.aten.bitwise_or.Scalar": // bitwise_or.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var output = gm.BitwiseOr(self, other);
                    output = ConvertToUint8IfNeeded(GetCommonScalarType(new[] { self, other }), output);
                    SetOutput(output, ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.bitwise_or.Tensor": // bitwise_or.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    SetOutput(gm.BitwiseOr(self, other), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.bitwise_xor.Scalar": // bitwise_xor.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var output = gm.BitwiseXor(self, other);
                    output = ConvertToUint8IfNeeded(GetCommonScalarType(new[] { self, other }), output);
                    SetOutput(output, ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.bitwise_xor.Tensor": // bitwise_xor.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    SetOutput(gm.BitwiseXor(self, other), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.bmm.default": // bmm(Tensor self, Tensor mat2) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var mat2 = node.Args[1].Tensor();
                    var nodes = new[] { self, mat2 };
                    var allInts = AllInts(nodes);
                    nodes = PromoteTypesFloat(nodes);
                    var output = gm.MatMul(nodes[0], nodes[1]);
                    if (allInts)
                        output = PromoteTypeInt(output);
                    SetOutput(output, ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.cat.default": // cat(Tensor[] tensors, int dim=0) -> Tensor
                {
                    var tensors = node.Args[0].Tensors();
                    var dim = node.Args[1].Int(defaultValue: 0);
                    SetOutput(gm.Concat(tensors, dim), ctx.ScalarType(tensors[0]));
                    break;
                }
                case "torch.ops.aten.ceil.default": // ceil(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    if (self.partialTensor.dataType == DataType.Int)
                        SetOutput(gm.Identity(self), ctx.ScalarType(self));
                    else
                        SetOutput(gm.Ceil(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.clamp.default": // clamp(Tensor self, Scalar? min=None, Scalar? max=None) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var min = node.Args[1].ScalarAsTensor();
                    var max = node.Args[2].ScalarAsTensor();
                    var (nodes, scalarType) = PromoteTypes(new[] {self, min, max});
                    SetOutput(gm.Clip(nodes[0], nodes[1], nodes[2]), scalarType);
                    break;
                }
                case "torch.ops.aten.clamp.Tensor": // clamp.Tensor(Tensor self, Tensor? min=None, Tensor? max=None) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var min = node.Args[1].Tensor();
                    var max = node.Args[2].Tensor();
                    var (nodes, scalarType) = PromoteTypes(new[] {self, min, max});
                    if (min == null)
                        SetOutput(gm.Min(nodes[0], nodes[2]), scalarType);
                    else if (max == null)
                        SetOutput(gm.Max(nodes[0], nodes[1]), scalarType);
                    else
                        SetOutput(gm.Min(gm.Max(nodes[0], nodes[1]), nodes[2]), scalarType);
                    break;
                }
                case "torch.ops.aten.clone.default": // clone(Tensor self, *, MemoryFormat? memory_format=None) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    WarnKeywordArgIgnored("memory_format");
                    SetOutput(gm.Identity(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.col2im.default": // col2im(Tensor self, SymInt[2] output_size, int[2] kernel_size, int[2] dilation, int[2] padding, int[2] stride) -> Tensor
                {
                    // TODO implement SENTIS-754
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten.constant_pad_nd.default": // constant_pad_nd(Tensor self, SymInt[] pad, Scalar value=0) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var padding = node.Args[1].Ints();
                    var value = node.Args[2].ScalarAsTensor(0);
                    var numAxes = padding.Length / 2;
                    var pads = new int[padding.Length];
                    var axes = new int[numAxes];
                    for (var i = 0; i < numAxes; i++)
                    {
                        pads[i] = padding[2 * i];
                        pads[i + numAxes] = padding[2 * i + 1];
                        axes[i] = -1 - i;
                    }
                    SetOutput(gm.Pad(self, gm.Constant(pads), value, gm.Constant(axes), Layers.PadMode.Constant), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.convolution.default": // convolution(Tensor input, Tensor weight, Tensor? bias, SymInt[] stride, SymInt[] padding, SymInt[] dilation, bool transposed, SymInt[] output_padding, SymInt groups) -> Tensor
                {
                    var input = node.Args[0].Tensor();
                    var weight = node.Args[1].Tensor();
                    var bias = node.Args[2].Tensor();
                    var promoteToInts = AllInts(new[] {input, weight});
                    if (promoteToInts)
                        bias = PromoteTypeInt(bias);
                    var nodes = PromoteTypesFloat(new[] {input, weight, bias});
                    var stride = node.Args[3].SymIntsAsInts();
                    var padding = node.Args[4].SymIntsAsInts();
                    var pads = new int[2 * padding.Length];
                    for (var i = 0; i < padding.Length; i++)
                    {
                        pads[i] = padding[i];
                        pads[padding.Length + i] = padding[i];
                    }
                    var dilation = node.Args[5].SymIntsAsInts();
                    var transposed = node.Args[6].Bool();
                    var outputPadding = node.Args[7].SymIntsAsInts();
                    var groups = node.Args[8].SymIntAsInt();
                    if (transposed)
                    {
                        var output = gm.ConvTranspose(nodes[0], nodes[1], nodes[2], Layers.AutoPad.NotSet, dilation, groups, outputPadding, pads, stride, null, Layers.FusableActivation.None);
                        if (promoteToInts)
                            output = PromoteTypeInt(output);
                        SetOutput(output, promoteToInts ? ScalarType.LONG : ScalarType.FLOAT);
                    }
                    else
                    {
                        var output = gm.Conv(nodes[0], nodes[1], nodes[2], Layers.AutoPad.NotSet, dilation, groups, pads, stride, null, Layers.FusableActivation.None);
                        if (promoteToInts)
                            output = PromoteTypeInt(output);
                        SetOutput(output, promoteToInts ? ScalarType.LONG : ScalarType.FLOAT);
                    }
                    break;
                }
                case "torch.ops.aten.convolution_backward.default": // convolution_backward(Tensor grad_output, Tensor input, Tensor weight, SymInt[]? bias_sizes, SymInt[] stride, SymInt[] padding, SymInt[] dilation, bool transposed, SymInt[] output_padding, SymInt groups, bool[3] output_mask) -> (Tensor, Tensor, Tensor)
                {
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten.copy.default": // copy(Tensor self, Tensor src, bool non_blocking=False) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var src = node.Args[1].Tensor();
                    WarnKeywordArgBoolIgnored("non_blocking", false);
                    SetOutput(gm.CastLike(src, self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.cos.default": // cos(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Cos(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.cosh.default": // cosh(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Cosh(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.cumsum.default": // cumsum(Tensor self, int dim, *, ScalarType? dtype=None) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var dtype = node.KeywordArgs["dtype"].OptionalScalarType();
                    var scalarType = dtype.GetValueOrDefault(ctx.ScalarType(self));
                    var dataType = TorchUtilities.ScalarTypeToDataType(scalarType);
                    self = (dataType == DataType.Int) ? PromoteTypeInt(self) : PromoteTypeFloat(self);
                    var cumsum = gm.CumSum(self, gm.Constant(dim), reverse: false, exclusive: false);
                    SetOutput(cumsum, scalarType);
                    break;
                }
                case "torch.ops.aten.diagonal.default": // diagonal(Tensor(a) self, int offset=0, int dim1=0, int dim2=1) -> Tensor(a)
                {
                    var self = node.Args[0].Tensor();
                    var offset = node.Args[1].Int(0);
                    var dim1 = node.Args[2].Int(0);
                    var dim2 = node.Args[3].Int(1);
                    SetOutput(gm.Diagonal(self, offset, dim1, dim2), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.div.Scalar": // div.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var nodes = PromoteTypesFloat(new [] {self, other});
                    SetOutput(gm.Div(nodes[0], nodes[1]), ScalarType.FLOAT);
                    break;
                }
                case "torch.ops.aten.div.Scalar_mode": // div.Scalar_mode(Tensor self, Scalar other, *, str? rounding_mode) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var roundingMode = node.KeywordArgs["rounding_mode"].String("");
                    if (roundingMode == "floor")
                    {
                        var (nodes, scalarType) = PromoteTypes(new [] {self, other});
                        SetOutput(gm.FloorDiv(nodes[0], nodes[1]), scalarType);
                    }
                    else if (roundingMode == "trunc")
                    {
                        var (nodes, scalarType) = PromoteTypes(new [] {self, other});
                        SetOutput(gm.TruncDiv(nodes[0], nodes[1]), scalarType);
                    }
                    else
                    {
                        var nodes = PromoteTypesFloat(new[] {self, other});
                        SetOutput(gm.Div(nodes[0], nodes[1]), ScalarType.FLOAT);
                    }
                    break;
                }
                case "torch.ops.aten.div.Tensor": // div.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var nodes = PromoteTypesFloat(new [] {self, other});
                    SetOutput(gm.Div(nodes[0], nodes[1]), ScalarType.FLOAT);
                    break;
                }
                case "torch.ops.aten.div.Tensor_mode": // div.Tensor_mode(Tensor self, Tensor other, *, str? rounding_mode) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var roundingMode = node.KeywordArgs["rounding_mode"].String("");
                    if (roundingMode == "floor")
                    {
                        var (nodes, scalarType) = PromoteTypes(new [] {self, other});
                        SetOutput(gm.FloorDiv(nodes[0], nodes[1]), scalarType);
                    }
                    else if (roundingMode == "trunc")
                    {
                        var (nodes, scalarType) = PromoteTypes(new [] {self, other});
                        SetOutput(gm.TruncDiv(nodes[0], nodes[1]), scalarType);
                    }
                    else
                    {
                        var nodes = PromoteTypesFloat(new [] {self, other});
                        SetOutput(gm.Div(nodes[0], nodes[1]), ScalarType.FLOAT);
                    }
                    break;
                }
                case "torch.ops.aten.elu.default": // elu(Tensor self, Scalar alpha=1, Scalar scale=1, Scalar input_scale=1) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var alpha = node.Args[1].ScalarAsFloat(1);
                    var scale = node.Args[2].ScalarAsTensor(1);
                    var inputScale = node.Args[3].ScalarAsTensor(1);
                    scale = PromoteTypeFloat(scale);
                    inputScale = PromoteTypeFloat(inputScale);
                    var zero = gm.Constant(0f);
                    var condition = gm.GreaterOrEqual(self, zero);
                    var positiveResult = gm.Mul(self, scale);
                    var scaledInput = gm.Mul(self, inputScale);
                    var eluResult = gm.Elu(scaledInput, alpha);
                    var negativeResult = gm.Mul(eluResult, scale);
                    SetOutput(gm.Where(condition, positiveResult, negativeResult), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.embedding.default": // embedding(Tensor weight, Tensor indices, SymInt padding_idx=-1, bool scale_grad_by_freq=False, bool sparse=False) -> Tensor
                {
                    var weight = node.Args[0].Tensor();
                    var indices = node.Args[1].Tensor();
                    WarnArgSymIntIgnored(2, "padding_idx", -1);
                    WarnArgBoolIgnored(3, "scale_grad_by_freq", false);
                    WarnArgBoolIgnored(4, "sparse", false);
                    SetOutput(gm.Gather(weight, indices, 0), ctx.ScalarType(weight));
                    break;
                }
                case "torch.ops.aten.embedding_dense_backward.default": // embedding_dense_backward(Tensor grad_output, Tensor indices, SymInt num_weights, SymInt padding_idx, bool scale_grad_by_freq) -> Tensor
                {
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten.empty.memory_format": // empty.memory_format(SymInt[] size, *, ScalarType? dtype=None, Layout? layout=None, Device? device=None, bool? pin_memory=None, MemoryFormat? memory_format=None) -> Tensor
                {
                    var size = node.Args[0].SymIntsAsTensor();
                    var dtype = node.KeywordArgs["dtype"].OptionalScalarType();
                    var scalarType = dtype.GetValueOrDefault(ScalarType.FLOAT);
                    var dataType = TorchUtilities.ScalarTypeToDataType(scalarType);
                    WarnKeywordArgIgnored("layout");
                    WarnKeywordArgIgnored("device");
                    WarnKeywordArgBoolIgnored("pin_memory", false);
                    WarnKeywordArgIgnored("memory_format");
                    SetOutput(gm.ConstantOfShape(size, dataType, 0f, 0), scalarType);
                    break;
                }
                case "torch.ops.aten.empty_strided.default": // empty_strided(SymInt[] size, SymInt[] stride, *, ScalarType? dtype=None, Layout? layout=None, Device? device=None, bool? pin_memory=None) -> Tensor
                {
                    var size = node.Args[0].SymIntsAsTensor();
                    var dtype = node.KeywordArgs["dtype"].OptionalScalarType();
                    var scalarType = dtype.GetValueOrDefault(ScalarType.FLOAT);
                    var dataType = TorchUtilities.ScalarTypeToDataType(scalarType);
                    WarnArgIgnored(1, "stride");
                    WarnKeywordArgIgnored("layout");
                    WarnKeywordArgIgnored("device");
                    WarnKeywordArgBoolIgnored("pin_memory", false);
                    SetOutput(gm.ConstantOfShape(size, dataType, 0f, 0), scalarType);
                    break;
                }
                case "torch.ops.aten.eq.Scalar": // eq.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var (nodes, _) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.Equal(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.eq.Tensor": // eq.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var (nodes, _) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.Equal(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.erf.default": // erf(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Erf(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.exp.default": // exp(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Exp(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.expand.default": // expand(Tensor(a) self, SymInt[] size, *, bool implicit=False) -> Tensor(a)
                {
                    var self = node.Args[0].Tensor();
                    var sizeInts = node.Args[1].SymIntsAsInts().Select(x => x == -1 ? 1 : x).ToArray();
                    var size = gm.Constant(sizeInts);
                    WarnKeywordArgBoolIgnored("implicit", false);
                    SetOutput(gm.Expand(self, size), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.expm1.default": // expm1(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Expm1(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.fill.Scalar": // fill.Scalar(Tensor self, Scalar value) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var shape = gm.Shape(self, 0, TensorShape.maxRank);
                    var scalarType = ctx.ScalarType(self);
                    var dataType = self.partialTensor.dataType;
                    if (dataType == DataType.Int)
                        SetOutput(gm.ConstantOfShape(shape, dataType, 0f, node.Args[1].ScalarAsIntType(scalarType)), scalarType);
                    else if (dataType == DataType.Float)
                        SetOutput(gm.ConstantOfShape(shape, dataType, node.Args[1].ScalarAsFloat(), 0), scalarType);
                    else
                        throw new TorchLayerImportException(target, $"Type {scalarType} is not supported for \"dtype\"");
                    break;
                }
                case "torch.ops.aten.flip.default": // flip(Tensor self, int[] dims) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dims = node.Args[1].Ints();
                    var starts = new int[dims.Length];
                    var ends = new int[dims.Length];
                    var steps = new int[dims.Length];
                    for (var i = 0; i < dims.Length; i++)
                    {
                        starts[i] = -1;
                        ends[i] = int.MinValue;
                        steps[i] = -1;
                    }
                    SetOutput(gm.Slice(self, gm.Constant(starts), gm.Constant(ends), gm.Constant(dims), gm.Constant(steps)), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.floor.default": // floor(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    if (self.partialTensor.dataType == DataType.Int)
                        SetOutput(gm.Identity(self), ctx.ScalarType(self));
                    else
                        SetOutput(gm.Floor(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.floor_divide.default": // floor_divide(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var (nodes, scalarType) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.FloorDiv(nodes[0], nodes[1]), scalarType);
                    break;
                }
                case "torch.ops.aten.fmod.Scalar": // fmod.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var (nodes, scalarType) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.Mod(nodes[0], nodes[1], true), scalarType);
                    break;
                }
                case "torch.ops.aten.fmod.Tensor": // fmod.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var (nodes, scalarType) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.Mod(nodes[0], nodes[1], true), scalarType);
                    break;
                }
                case "torch.ops.aten.full.default": // full(SymInt[] size, Scalar fill_value, *, ScalarType? dtype=None, Layout? layout=None, Device? device=None, bool? pin_memory=None) -> Tensor
                {
                    var dtype = node.KeywordArgs["dtype"].OptionalScalarType();
                    var scalarType = dtype.GetValueOrDefault(node.Args[1].ScalarType());
                    var dataType = TorchUtilities.ScalarTypeToDataType(scalarType);
                    var size = node.Args[0].SymIntsAsTensor();
                    WarnKeywordArgIgnored("layout");
                    WarnKeywordArgIgnored("device");
                    WarnKeywordArgBoolIgnored("pin_memory", false);
                    if (dataType == DataType.Float)
                        SetOutput(gm.ConstantOfShape(size, dataType, node.Args[1].ScalarAsFloat(), 0), scalarType);
                    else if (dataType == DataType.Int)
                        SetOutput(gm.ConstantOfShape(size, dataType, 0f, node.Args[1].ScalarAsIntType(scalarType)), scalarType);
                    else
                        throw new TorchLayerImportException(target, $"Type {scalarType} is not supported for \"dtype\"");
                    break;
                }
                case "torch.ops.aten.full_like.default": // full_like(Tensor self, Scalar fill_value, *, ScalarType? dtype=None, Layout? layout=None, Device? device=None, bool? pin_memory=None, MemoryFormat? memory_format=None) -> Tensor
                {
                    var dtype = node.KeywordArgs["dtype"].OptionalScalarType();
                    var self = node.Args[0].Tensor();
                    var scalarType = dtype.GetValueOrDefault(ctx.ScalarType(self));
                    var dataType = TorchUtilities.ScalarTypeToDataType(scalarType);
                    var shape = gm.Shape(self, 0, TensorShape.maxRank);
                    WarnKeywordArgIgnored("layout");
                    WarnKeywordArgIgnored("device");
                    WarnKeywordArgBoolIgnored("pin_memory", false);
                    WarnKeywordArgIgnored("memory_format");
                    if (dataType == DataType.Int)
                        SetOutput(gm.ConstantOfShape(shape, dataType, 0f, node.Args[1].ScalarAsIntType(scalarType)), scalarType);
                    else if (dataType == DataType.Float)
                        SetOutput(gm.ConstantOfShape(shape, dataType, node.Args[1].ScalarAsFloat(), 0), scalarType);
                    else
                        throw new TorchLayerImportException(target, $"Type {scalarType} is not supported for \"dtype\"");
                    break;
                }
                case "torch.ops.aten.gather.default": // gather(Tensor self, int dim, Tensor index, *, bool sparse_grad=False) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var index = node.Args[2].Tensor();

                    WarnKeywordArgBoolIgnored("sparse_grad", false);
                    if (index.partialTensor.IsStatic() && index.partialTensor.length == 0)
                    {
                        var shape = gm.Shape(index, 0, TensorShape.maxRank);
                        var scalarType = ctx.ScalarType(self);
                        var dataType = TorchUtilities.ScalarTypeToDataType(scalarType);
                        SetOutput(gm.ConstantOfShape(shape, dataType, 0.0f, 0), scalarType);
                    }
                    else
                        SetOutput(gm.GatherElements(self, index, dim), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.ge.Scalar": // ge.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var (nodes, _) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.GreaterOrEqual(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.ge.Tensor": // ge.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var (nodes, _) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.GreaterOrEqual(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.gelu.default": // gelu(Tensor self, *, str approximate=’none’) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var approximate = node.KeywordArgs["approximate"].String("none");
                    if (approximate == "none")
                        SetOutput(gm.Gelu(self), ctx.ScalarType(self));
                    else if (approximate == "tanh")
                        SetOutput(gm.GeluFast(self), ctx.ScalarType(self));
                    else
                        throw new TorchLayerImportException(target, $"Value {approximate} is not supported for \"approximate\"");
                    break;
                }
                case "torch.ops.aten.grid_sampler_2d.default": // grid_sampler_2d(Tensor input, Tensor grid, int interpolation_mode, int padding_mode, bool align_corners) -> Tensor
                {
                    var input = node.Args[0].Tensor();
                    var grid = node.Args[1].Tensor();
                    var interpolationModeInt = node.Args[2].Int();
                    var paddingModeInt = node.Args[3].Int();
                    // from https://github.com/pytorch/pytorch/blob/main/aten/src/ATen/native/GridSamplerUtils.h
                    var interpolationMode = interpolationModeInt switch
                    {
                        0 => Layers.InterpolationMode.Linear,
                        1 => Layers.InterpolationMode.Nearest,
                        2 => Warn(WarningType.Warning, $"{target} `bicubic` interpolation_mode is not supported, using `linear`.", Layers.InterpolationMode.Linear),
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    var paddingMode = paddingModeInt switch
                    {
                        0 => Layers.PaddingMode.Zeros,
                        1 => Layers.PaddingMode.Border,
                        2 => Layers.PaddingMode.Reflection,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    var alignCorners = node.Args[4].Bool();
                    SetOutput(gm.GridSample(input, grid, interpolationMode, paddingMode, alignCorners), ctx.ScalarType(input));
                    break;
                }
                case "torch.ops.aten.gt.Scalar": // gt.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var (nodes, _) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.Greater(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.gt.Tensor": // gt.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var (nodes, _) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.Greater(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.hardtanh.default": // hardtanh(Tensor self, Scalar min_val=-1, Scalar max_val=1) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var minVal = node.Args[1].ScalarAsFloat(-1);
                    var maxVal = node.Args[2].ScalarAsFloat(1f);
                    SetOutput(gm.HardTanh(self, minVal, maxVal), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.index.Tensor": // index.Tensor(Tensor self, Tensor?[] indices) -> Tensor
                {
                    // see https://github.com/microsoft/onnxscript/blob/main/onnxscript/function_libs/torch_lib/ops/core.py

                    var self = node.Args[0].Tensor();
                    var indices = node.Args[1].Tensors();

                    var indexRanks = new List<int>();
                    var isBoolIndex = false;

                    foreach (var index in indices)
                    {
                        if (index == null)
                            continue;
                        isBoolIndex |= ctx.ScalarType(index) == ScalarType.BOOL;
                        indexRanks.Add(index.partialTensor.shape.rank);
                    }

                    var selfRank = self.partialTensor.shape.rank;
                    AssertTrue(self.partialTensor.shape.isRankDynamic == false, "\"self\" must have a known rank");

                    if (isBoolIndex)
                    {
                        if (indexRanks[0] == 1)
                        {
                            // indices contains scalar only.
                            indices = indices.Select(index => index != null ? gm.Transpose(gm.NonZero(index), new[] { 1, 0 }) : null).ToArray();
                            indices = indices.Select(index => index != null ? gm.Squeeze(index, gm.Constant(new[] { 1 })) : null).ToArray();
                        }
                        else
                        {
                            var transPerm = Enumerable.Range(0, selfRank).ToList();
                            transPerm.Add(transPerm[0]);
                            transPerm.RemoveAt(0);

                            var countOfNone = 0;
                            foreach (var index in indices)
                            {
                                if (index == null)
                                {
                                    self = gm.Transpose(self, transPerm.ToArray());
                                    countOfNone++;
                                }
                                else
                                {
                                    var newIndices = gm.Transpose(gm.NonZero(index), new[] { 1, 0 });
                                    var result = gm.GatherND(self, newIndices, 0);
                                    var finalRank = selfRank - (index.partialTensor.shape.rank - 1);
                                    transPerm = Enumerable.Range(0, finalRank).ToList();
                                    transPerm = transPerm.Skip(finalRank - 1).Concat(transPerm.Take(finalRank - 1)).ToList();

                                    for (var i = 0; i < countOfNone; i++)
                                    {
                                        result = gm.Transpose(result, transPerm.ToArray());
                                    }
                                    SetOutput(result, ctx.ScalarType(self));
                                }
                            }

                            // If all indices are null, just return self?
                            SetOutput(gm.Identity(self), ctx.ScalarType(self));
                            return;
                        }
                    }

                    var advancedIndexingRank = indexRanks.Max();

                    // Compute reordered_positions
                    var reorderedPositions = Enumerable.Range(0, indices.Length)
                        .OrderBy(i => indices[i] == null ? 1 : 0)
                        .ThenBy(i => i)
                        .ToList();

                    // Extend reordered_positions to fill up to self rank
                    reorderedPositions.AddRange(Enumerable.Range(reorderedPositions.Count, selfRank - reorderedPositions.Count));

                    // Transpose `self` according to reordered positions
                    var transposedSelf = gm.Transpose(self, reorderedPositions.ToArray());

                    // Collect non-null indices
                    var notNoneIndices = indices.Where(idx => idx != null).ToArray();

                    // Broadcast all indices to the same shape
                    // TODO better way of doing this without the maximum ops
                    var broadcasted = notNoneIndices[0];
                    for (var i = 1; i < notNoneIndices.Length; i++)
                        broadcasted = gm.Max(broadcasted, notNoneIndices[i]);
                    var broadcastShape = gm.Shape(broadcasted, 0, TensorShape.maxRank);

                    // Expand and unsqueeze each index before concatenation
                    var expanded = new List<Graph.Node>();
                    foreach (var idx in notNoneIndices)
                    {
                        var expandedIdx = gm.Expand(idx, broadcastShape);
                        expanded.Add(gm.Unsqueeze(expandedIdx, gm.Constant(new[] { -1 })));
                    }

                    // Final gather index
                    var finalIndex = gm.Concat(expanded.ToArray(), axis: -1);

                    // Apply GatherND
                    var gatheredSelf = gm.GatherND(transposedSelf, finalIndex, batchDims: 0);

                    bool HasNoneInMiddle(Graph.Node[] idxs)
                    {
                        var foundNonNull = false;
                        var foundNullAfterNonNull = false;

                        foreach (var idx in idxs)
                        {
                            if (idx != null)
                            {
                                if (foundNullAfterNonNull)
                                {
                                    // Found a non-null after encountering a null in the middle
                                    return true;
                                }
                                foundNonNull = true;
                            }
                            else if (foundNonNull)
                            {
                                // Found a null after first non-null element
                                foundNullAfterNonNull = true;
                            }
                        }
                        return false;
                    }

                    // Check if there is a `None` in the middle
                    if (HasNoneInMiddle(indices))
                    {
                        // Leave as is
                        SetOutput(gatheredSelf, ctx.ScalarType(self));
                        break;
                    }

                    // Calculate axis permutation to match PyTorch advanced indexing layout
                    var firstNotNone = reorderedPositions[0];
                    var startOfBackNone = advancedIndexingRank + firstNotNone;
                    var resultRank = selfRank - notNoneIndices.Length + advancedIndexingRank;

                    var perm = new List<int>();

                    // Front None
                    for (var i = advancedIndexingRank; i < startOfBackNone; i++)
                        perm.Add(i);

                    // Broadcasted dimensions
                    for (var i = 0; i < advancedIndexingRank; i++)
                        perm.Add(i);

                    // Back None
                    for (var i = startOfBackNone; i < resultRank; i++)
                        perm.Add(i);

                    SetOutput(gm.Transpose(gatheredSelf, perm.ToArray()), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.index_put.default": // index_put(Tensor self, Tensor?[] indices, Tensor values, bool accumulate=False) -> Tensor
                {
                    // see https://github.com/microsoft/onnxscript/blob/main/onnxscript/function_libs/torch_lib/ops/core.py

                    var self = node.Args[0].Tensor();
                    var indices = node.Args[1].Tensors();
                    var values = node.Args[2].Tensor();
                    var accumulate = node.Args[3].Bool(false);

                    var selfRank = self.partialTensor.shape.rank;
                    var isBoolIndex = false;

                    foreach (var index in indices)
                    {
                        if (index == null)
                            continue;
                        isBoolIndex |= ctx.ScalarType(index) == ScalarType.BOOL;
                    }

                    if (isBoolIndex)
                    {
                        // TODO: Support indices with more than 1 elements SENTIS-1186
                        var index = indices[0];
                        if (indices.Length > 1)
                            Warn(WarningType.Warning, $"{target}: Indices with more than 1 elements are not supported. Using the first index.");

                        AssertValue(accumulate, false, "accumulate");

                        // Reshape indices to broadcast properly
                        var indexRank = index.partialTensor.shape.rank;

                        if (selfRank > indexRank)
                        {
                            var indexShape = gm.Shape(index, 0, TensorShape.maxRank);
                            var padding = gm.Constant(Enumerable.Repeat(1, selfRank - indexRank).ToArray());
                            var paddedShape = gm.Concat(new[] { indexShape, padding }, axis: 0);
                            index = gm.Reshape(index, paddedShape, true);
                        }

                        SetOutput(gm.Where(index, values, self), ctx.ScalarType(self));
                        return;
                    }

                    // Helper: make reshape list broadcastable
                    List<int> MakeReshapeListBroadcastable(List<int> reshapeList, List<int> valuesShape)
                    {
                        // Remove ones until reshapeList length matches valuesShape length
                        while (reshapeList.Count > valuesShape.Count && reshapeList.Contains(1))
                        {
                            reshapeList.Remove(1);
                        }

                        for (var i = 0; i < reshapeList.Count; i++)
                        {
                            var r = reshapeList[i];
                            if (r != 1 && (i >= valuesShape.Count || r != valuesShape[i]))
                            {
                                var valueIndex = valuesShape.IndexOf(r);
                                if (valueIndex >= 0)
                                {
                                    // Swap elements
                                    reshapeList[valueIndex] = r;
                                    reshapeList[i] = 1;
                                }
                            }
                        }
                        return reshapeList;
                    }

                    // Ensure indices length matches self rank
                    var indicesList = indices.ToList();
                    if (indicesList.Count < selfRank)
                    {
                        for (var i = indicesList.Count; i < selfRank; i++)
                            indicesList.Add(null);
                    }

                    var valuesShape = new List<int>(values.partialTensor.shape.ToIntArray());

                    var indexVectors = new List<Graph.Node>();

                    for (var i = 0; i < selfRank; i++)
                    {
                        Graph.Node idx;
                        int reshapeUpdate;

                        if (indicesList[i] == null)
                        {
                            AssertTrue(self.partialTensor.shape.IsStatic(), "\"self\" shape must be static");
                            idx = gm.Range(gm.Constant(0), gm.Constant(self.partialTensor.shape[i].value), gm.Constant(1));
                            reshapeUpdate = self.partialTensor.shape[i].value;
                        }
                        else
                        {
                            idx = indices[i];
                            AssertTrue(idx.partialTensor.shape.IsStatic(), "\"indices\" shapes must be static");
                            reshapeUpdate = idx.partialTensor.shape.ToTensorShape().length;

                            if (idx.partialTensor.shape.rank > 1)
                            {
                                var newValuesShape = new List<int> { reshapeUpdate };
                                for (int k = idx.partialTensor.shape.rank; k < valuesShape.Count; k++)
                                    newValuesShape.Add(valuesShape[k]);
                                valuesShape = newValuesShape;
                            }

                            idx = gm.Reshape(idx, gm.Constant(new[] { -1 }), true);
                        }

                        var reshapeList = new List<int>(new int[indicesList.Count]);
                        for (var j = 0; j < reshapeList.Count; j++)
                            reshapeList[j] = 1;

                        reshapeList[i] = reshapeUpdate;

                        reshapeList = MakeReshapeListBroadcastable(reshapeList, valuesShape);

                        idx = gm.Reshape(idx, gm.Constant(reshapeList.ToArray()), true);
                        idx = gm.Expand(idx, gm.Constant(valuesShape.ToArray()));
                        idx = gm.Reshape(idx, gm.Constant(new[] { -1 }), true);
                        idx = gm.Unsqueeze(idx, gm.Constant(new[] { 1 }));

                        indexVectors.Add(idx);
                    }

                    var newIndex = gm.Concat(indexVectors.ToArray(), axis: 1);
                    var flatValues = gm.Reshape(values, gm.Constant(new[] { -1 }), true);

                    SetOutput(gm.ScatterND(self, newIndex, flatValues, accumulate ? Layers.ScatterReductionMode.Add : Layers.ScatterReductionMode.None), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.index_select.default": // index_select(Tensor self, int dim, Tensor index) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var index = node.Args[2].Tensor();
                    SetOutput(gm.Gather(self, index, dim), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.isinf.default": // isinf(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.IsInf(self, true, true), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.isnan.default": // isnan(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.IsNaN(self), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.le.Scalar": // le.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var (nodes, _) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.LessOrEqual(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.le.Tensor": // le.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var (nodes, _) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.LessOrEqual(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.leaky_relu.default": // leaky_relu(Tensor self, Scalar negative_slope=0.01) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var alpha = node.Args[1].ScalarAsFloat(0.01f);
                    SetOutput(gm.LeakyRelu(self, alpha), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.log.default": // log(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Log(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.log10.default": // log10(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Log10(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.log1p.default": // log1p(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Log1p(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.log2.default": // log2(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Log2(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.logical_and.default": // logical_and(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var nodes = PromoteTypesBool(new[] {self, other}, true);
                    SetOutput(gm.And(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.logical_not.default": // logical_not(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    self = PromoteTypeBool(self, true);
                    SetOutput(gm.Not(self), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.logical_or.default": // logical_or(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var nodes = PromoteTypesBool(new [] {self, other}, true);
                    SetOutput(gm.Or(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.logical_xor.default": // logical_xor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var nodes = PromoteTypesBool(new [] {self, other}, true);
                    SetOutput(gm.Xor(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.lt.Scalar": // lt.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var (nodes, _) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.Less(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.lt.Tensor": // lt.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var (nodes, _) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.Less(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.masked_scatter.default": // masked_scatter(Tensor self, Tensor mask, Tensor source) -> Tensor
                {
                    // TODO implement SENTIS-1187
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten.max.dim": // max.dim(Tensor self, int dim, bool keepdim=False) -> (Tensor values, Tensor indices)
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var keepdim = node.Args[2].Bool(false);
                    var indices = gm.ArgMax(self, dim, keepdims: true, selectLastIndex: false);
                    var values = gm.GatherElements(self, indices, dim);
                    if (!keepdim)
                    {
                        values = gm.Squeeze(values, gm.Constant(new[] { dim }));
                        indices = gm.Squeeze(indices, gm.Constant(new[] { dim }));
                    }
                    SetOutput(values, ctx.ScalarType(self), 0);
                    SetOutput(indices, ScalarType.LONG, 1);
                    break;
                }
                case "torch.ops.aten.max_pool2d_with_indices.default": // max_pool2d_with_indices(Tensor self, int[2] kernel_size, int[2] stride=[], int[2] padding=0, int[2] dilation=1, bool ceil_mode=False) -> (Tensor, Tensor)
                {
                    var self = node.Args[0].Tensor();
                    var kernelSize = node.Args[1].Ints();
                    var stride = node.Args[2].Ints(kernelSize);
                    var padding = node.Args[3].Ints(new[] { 0, 0 });
                    // TODO check autopadding stuff and padding being able to be an integer
                    WarnArgIgnored(4, "dilation");
                    WarnArgIgnored(5, "ceil_mode");
                    var pads = new int[2 * padding.Length];
                    for (var i = 0; i < padding.Length; i++)
                    {
                        pads[i] = padding[i];
                        pads[padding.Length + i] = padding[i];
                    }
                    var dataType = self.partialTensor.dataType;
                    self = PromoteTypeFloat(self);
                    var output = gm.MaxPool(self, kernelSize, stride, pads, Layers.AutoPad.NotSet);
                    if (dataType == DataType.Int)
                        output = PromoteTypeInt(output);
                    SetOutput(output, dataType == DataType.Int ? ScalarType.LONG : ScalarType.FLOAT);
                    SetUnsupportedTensor(target, "indices");
                    break;
                }
                case "torch.ops.aten.max_pool2d_with_indices_backward.default": // max_pool2d_with_indices_backward(Tensor grad_output, Tensor self, int[2] kernel_size, int[2] stride, int[2] padding, int[2] dilation, bool ceil_mode, Tensor indices) -> Tensor
                {
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten.max_pool3d_with_indices.default": // max_pool3d_with_indices(Tensor self, int[3] kernel_size, int[3] stride=[], int[3] padding=0, int[3] dilation=1, bool ceil_mode=False) -> (Tensor, Tensor)
                {
                    var self = node.Args[0].Tensor();
                    var kernelSize = node.Args[1].Ints();
                    var stride = node.Args[2].Ints(kernelSize);
                    var padding = node.Args[3].Ints(new[] { 0, 0, 0 });
                    // TODO check autopadding stuff and padding being able to be an integer
                    WarnArgIgnored(4, "dilation");
                    WarnArgIgnored(5, "ceil_mode");
                    var pads = new int[2 * padding.Length];
                    for (var i = 0; i < padding.Length; i++)
                    {
                        pads[i] = padding[i];
                        pads[padding.Length + i] = padding[i];
                    }
                    var dataType = self.partialTensor.dataType;
                    self = PromoteTypeFloat(self);
                    var output = gm.MaxPool(self, kernelSize, stride, pads, Layers.AutoPad.NotSet);
                    if (dataType == DataType.Int)
                        output = PromoteTypeInt(output);
                    SetOutput(output, dataType == DataType.Int ? ScalarType.LONG : ScalarType.FLOAT);
                    SetUnsupportedTensor(target, "indices");
                    break;
                }
                case "torch.ops.aten.maximum.default": // maximum(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var (nodes, scalarType) = PromoteTypes(new[] { self, other });
                    SetOutput(gm.Max(nodes[0], nodes[1]), scalarType);
                    break;
                }
                case "torch.ops.aten.mean.default": // mean(Tensor self, *, ScalarType? dtype=None) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    WarnKeywordArgDtypeIgnored("dtype", ctx.ScalarType(self));
                    SetOutput(gm.ReduceMean(self, gm.Constant(Array.Empty<int>()), keepdims: false, noopWithEmptyAxes: false), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.mean.dim": // mean.dim(Tensor self, int[1]? dim, bool keepdim=False, *, ScalarType? dtype=None) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Ints(defaultValue: Array.Empty<int>());
                    var keepdim = node.Args[2].Bool(defaultValue: false);
                    WarnKeywordArgDtypeIgnored("dtype", ctx.ScalarType(self));
                    SetOutput(gm.ReduceMean(self, gm.Constant(dim), keepdims: keepdim, noopWithEmptyAxes: false), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.min.dim": // min.dim(Tensor self, int dim, bool keepdim=False) -> (Tensor values, Tensor indices)
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var keepdim = node.Args[2].Bool(false);
                    var indices = gm.ArgMin(self, dim, keepdims: true, selectLastIndex: false);
                    var values = gm.GatherElements(self, indices, dim);
                    if (!keepdim)
                    {
                        values = gm.Squeeze(values, gm.Constant(new[] { dim }));
                        indices = gm.Squeeze(indices, gm.Constant(new[] { dim }));
                    }
                    SetOutput(values, ctx.ScalarType(self), 0);
                    SetOutput(indices, ScalarType.LONG, 1);
                    break;
                }
                case "torch.ops.aten.minimum.default": // minimum(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var (nodes, scalarType) = PromoteTypes(new[] { self, other });
                    SetOutput(gm.Min(nodes[0], nodes[1]), scalarType);
                    break;
                }
                case "torch.ops.aten.mm.default": // mm(Tensor self, Tensor mat2) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var mat2 = node.Args[1].Tensor();
                    var nodes = new[] {self, mat2};
                    var allInts = AllInts(nodes);
                    nodes = PromoteTypesFloat(nodes);
                    var output = gm.MatMul(nodes[0], nodes[1]);
                    if (allInts)
                        output = gm.Cast(output, DataType.Int);
                    SetOutput(output, allInts ? ScalarType.LONG : ScalarType.FLOAT);
                    break;
                }
                case "torch.ops.aten.mul.Scalar": // mul.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var (nodes, scalarType) = PromoteTypes(new [] {self, other});
                    var output = gm.Mul(nodes[0], nodes[1]);
                    output = ConvertToUint8IfNeeded(scalarType, output);
                    SetOutput(output, scalarType);
                    break;
                }
                case "torch.ops.aten.mul.Tensor": // mul.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var (nodes, scalarType) = PromoteTypes(new [] {self, other});
                    var output = gm.Mul(nodes[0], nodes[1]);
                    output = ConvertToUint8IfNeeded(scalarType, output);
                    SetOutput(output, scalarType);
                    break;
                }
                case "torch.ops.aten.native_dropout.default": // native_dropout(Tensor input, float p, bool? train) -> (Tensor, Tensor)
                {
                    var input = node.Args[0].Tensor();
                    WarnArgFloatIgnored(1, "p", 0f);
                    WarnArgBoolIgnored(2, "train", false);
                    SetOutput(gm.Identity(input), ctx.ScalarType(input));
                    SetUnsupportedTensor(target, "mask");
                    break;
                }
                case "torch.ops.aten.native_group_norm.default": // native_group_norm(Tensor input, Tensor? weight, Tensor? bias, SymInt N, SymInt C, SymInt HxW, int group, float eps) -> (Tensor, Tensor, Tensor)
                {
                    var input = node.Args[0].Tensor();
                    var weight = node.Args[1].Tensor();
                    AssertTrue(weight != null, "\"weight\" must be provided");
                    var bias = node.Args[2].Tensor();
                    AssertTrue(bias != null, "\"bias\" must be provided");
                    var N = node.Args[3].SymIntAsInt();
                    var C = node.Args[4].SymIntAsInt();
                    var HxW = node.Args[5].SymIntAsInt();
                    var group = node.Args[6].Int();
                    var eps = node.Args[7].Float();
                    var reshapedInput = gm.Reshape(input, gm.Constant(new[] { N, group, HxW * C / group }), allowZero: true);
                    var ones = gm.ConstantOfShape(gm.Constant(new[] { group }), DataType.Float, 1f, 0);
                    var zeros = gm.ConstantOfShape(gm.Constant(new[] { group }), DataType.Float, 0f, 0);
                    var instanceNorm = gm.Reshape(gm.InstanceNormalization(reshapedInput, ones, zeros, eps), gm.Shape(input, 0, TensorShape.maxRank), allowZero: true);
                    var scaled = gm.Mul(instanceNorm, gm.Unsqueeze(weight, gm.Constant(new[] { 1, 2 })));
                    var biased = gm.Add(scaled, gm.Unsqueeze(bias, gm.Constant(new[] { 1, 2 })));
                    SetOutput(biased, ctx.ScalarType(input));
                    SetUnsupportedTensor(target, "mean");
                    SetUnsupportedTensor(target, "rstd");
                    break;
                }
                case "torch.ops.aten.native_group_norm_backward.default": // native_group_norm_backward(Tensor grad_out, Tensor input, Tensor mean, Tensor rstd, Tensor? weight, SymInt N, SymInt C, SymInt HxW, int group, bool[3] output_mask) -> (Tensor, Tensor, Tensor)
                {
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten.native_layer_norm.default": // native_layer_norm(Tensor input, SymInt[] normalized_shape, Tensor? weight, Tensor? bias, float eps) -> (Tensor, Tensor, Tensor)
                {
                    var input = node.Args[0].Tensor();
                    var normalizedShape = node.Args[1].SymIntsAsInts();
                    AssertTrue(normalizedShape.Length <= 1, $"\"normalized_shape\" is of rank {normalizedShape.Length}. Rank greater than 1 is not supported");
                    var weight = node.Args[2].Tensor();
                    AssertTrue(weight != null, "\"weight\" must be provided");
                    var bias = node.Args[3].Tensor();
                    AssertTrue(bias != null, "\"bias\" must be provided");
                    var eps = node.Args[4].Float();
                    SetOutput(gm.LayerNormalization(input, weight, bias, eps), ctx.ScalarType(input));
                    SetUnsupportedTensor(target, "mean");
                    SetUnsupportedTensor(target, "rstd");
                    break;
                }
                case "torch.ops.aten.native_layer_norm_backward.default": // native_layer_norm_backward(Tensor grad_out, Tensor input, SymInt[] normalized_shape, Tensor mean, Tensor rstd, Tensor? weight, Tensor? bias, bool[3] output_mask) -> (Tensor, Tensor, Tensor)
                {
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten.ne.Scalar": // ne.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var (nodes, _) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.NotEqual(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.ne.Tensor": // ne.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var (nodes, _) = PromoteTypes(new [] {self, other});
                    SetOutput(gm.NotEqual(nodes[0], nodes[1]), ScalarType.BOOL);
                    break;
                }
                case "torch.ops.aten.neg.default": // neg(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Neg(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.nonzero.default": // nonzero(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var nonZero = gm.NonZero(self);
                    SetOutput(gm.Transpose(nonZero, new[] { 1, 0 }), ScalarType.LONG);
                    break;
                }
                case "torch.ops.aten.permute.default": // permute(Tensor(a) self, int[] dims) -> Tensor(a)
                {
                    var self = node.Args[0].Tensor();
                    var dims = node.Args[1].Ints();
                    dims = MakeDimsPositive(dims);
                    SetOutput(gm.Transpose(self, dims), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.pow.Scalar": // pow.Scalar(Scalar self, Tensor exponent) -> Tensor
                {
                    var self = node.Args[0].ScalarAsTensor();
                    var exponent = node.Args[1].Tensor();
                    var (nodes, scalarType) = PromoteTypes(new [] {self, exponent});
                    SetOutput(gm.Pow(nodes[0], nodes[1]), scalarType);
                    break;
                }
                case "torch.ops.aten.pow.Tensor_Scalar": // pow.Tensor_Scalar(Tensor self, Scalar exponent) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var exponent = node.Args[1].ScalarAsTensor();
                    var (nodes, scalarType) = PromoteTypes(new [] {self, exponent});
                    SetOutput(gm.Pow(nodes[0], nodes[1]), scalarType);
                    break;
                }
                case "torch.ops.aten.pow.Tensor_Tensor": // pow.Tensor_Tensor(Tensor self, Tensor exponent) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var exponent = node.Args[1].Tensor();
                    var (nodes, scalarType) = PromoteTypes(new [] {self, exponent});
                    SetOutput(gm.Pow(nodes[0], nodes[1]), scalarType);
                    break;
                }
                case "torch.ops.aten.prod.default": // prod(Tensor self, *, ScalarType? dtype=None) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    WarnKeywordArgDtypeIgnored("dtype", ctx.ScalarType(self));
                    SetOutput(gm.ReduceProd(self, gm.Constant(Array.Empty<int>()), keepdims: false, noopWithEmptyAxes: false), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.prod.dim_int": // prod.dim_int(Tensor self, int dim, bool keepdim=False, *, ScalarType? dtype=None) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var keepdim = node.Args[2].Bool(false);
                    WarnKeywordArgDtypeIgnored("dtype", ctx.ScalarType(self));
                    SetOutput(gm.ReduceProd(self, gm.Constant(new[] { dim }), keepdims: keepdim, noopWithEmptyAxes: false), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.rand.default": // rand(SymInt[] size, *, ScalarType? dtype=None, Layout? layout=None, Device? device=None, bool? pin_memory=None) -> Tensor
                {
                    var size = node.Args[0].SymIntsAsInts();
                    var dtype = node.KeywordArgs["dtype"].OptionalScalarType();
                    var scalarType = dtype.GetValueOrDefault(ScalarType.FLOAT);
                    WarnKeywordArgIgnored("layout");
                    WarnKeywordArgIgnored("device");
                    WarnKeywordArgBoolIgnored("pin_memory", false);
                    SetOutput(gm.RandomUniform(0, 1, size, false, 0), scalarType);
                    break;
                }
                case "torch.ops.aten.randn.default": // randn(SymInt[] size, *, ScalarType? dtype=None, Layout? layout=None, Device? device=None, bool? pin_memory=None) -> Tensor
                {
                    var size = node.Args[0].SymIntsAsInts();
                    var dtype = node.KeywordArgs["dtype"].OptionalScalarType();
                    var scalarType = dtype.GetValueOrDefault(ScalarType.FLOAT);
                    WarnKeywordArgIgnored("layout");
                    WarnKeywordArgIgnored("device");
                    WarnKeywordArgBoolIgnored("pin_memory", false);
                    SetOutput(gm.RandomNormal(0, 1, size, false, 0), scalarType);
                    break;
                }
                case "torch.ops.aten.randperm.default": // randperm(SymInt n, *, ScalarType? dtype=long, Layout? layout=None, Device? device=None, bool? pin_memory=None) -> Tensor
                {
                    var size = node.Args[0].SymIntAsInt();
                    var dtype = node.KeywordArgs["dtype"].OptionalScalarType(ScalarType.LONG);
                    var scalarType = dtype.GetValueOrDefault(ScalarType.LONG);
                    WarnKeywordArgIgnored("layout");
                    WarnKeywordArgIgnored("device");
                    WarnKeywordArgBoolIgnored("pin_memory", false);
                    var shape = new[] { size };
                    var input = gm.RandomUniform(0f, 1f, shape, false, 0);
                    var outputs = gm.TopK(input, gm.Constant(shape), -1, false, true);
                    var output = outputs[1];
                    if (TorchUtilities.ScalarTypeToDataType(scalarType) == DataType.Float)
                        output = PromoteTypeFloat(output);
                    SetOutput(output, scalarType);
                    break;
                }
                case "torch.ops.aten.reciprocal.default": // reciprocal(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    self = PromoteTypeFloat(self);
                    SetOutput(gm.Reciprocal(self), ScalarType.FLOAT);
                    break;
                }
                case "torch.ops.aten.reflection_pad1d.default": // reflection_pad1d(Tensor self, SymInt[2] padding) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var padding = node.Args[1].Ints();
                    SetOutput(gm.Pad(self, gm.Constant(padding), null, gm.Constant(new[] { -1 }), Layers.PadMode.Reflect), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.reflection_pad2d.default": // reflection_pad2d(Tensor self, SymInt[4] padding) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var padding = node.Args[1].Ints();
                    SetOutput(gm.Pad(self, gm.Constant(new[] { padding[0], padding[2], padding[1], padding[3] }), null, gm.Constant(new[] { -1, -2 }), Layers.PadMode.Reflect), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.reflection_pad3d.default": // reflection_pad3d(Tensor self, SymInt[6] padding) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var padding = node.Args[1].Ints();
                    SetOutput(gm.Pad(self, gm.Constant(new[] { padding[0], padding[2], padding[4], padding[1], padding[3], padding[5] }), null, gm.Constant(new[] { -1, -2, -3 }), Layers.PadMode.Reflect), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.relu.default": // relu(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Relu(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.remainder.Scalar": // remainder.Scalar(Tensor self, Scalar other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var (nodes, scalarType) = PromoteTypes(new[] {self, other});
                    SetOutput(gm.Mod(nodes[0], nodes[1], false), scalarType);
                    break;
                }
                case "torch.ops.aten.remainder.Tensor": // remainder.Tensor(Tensor self, Tensor other) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var (nodes, scalarType) = PromoteTypes(new[] {self, other});
                    SetOutput(gm.Mod(nodes[0], nodes[1], false), scalarType);
                    break;
                }
                case "torch.ops.aten.repeat.default": // repeat(Tensor self, SymInt[] repeats) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var repeats = node.Args[1].SymIntsAsTensor();
                    SetOutput(gm.Tile(self, repeats), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.replication_pad2d.default": // replication_pad2d(Tensor self, SymInt[4] padding) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var padding = node.Args[1].Ints();
                    SetOutput(gm.Pad(self, gm.Constant(new[] { padding[0], padding[2], padding[1], padding[3] }), null, gm.Constant(new[] { -1, -2 }), Layers.PadMode.Edge), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.replication_pad3d.default": // replication_pad3d(Tensor self, SymInt[6] padding) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var padding = node.Args[1].Ints();
                    SetOutput(gm.Pad(self, gm.Constant(new[] { padding[0], padding[2], padding[4], padding[1], padding[3], padding[5] }), null, gm.Constant(new[] { -1, -2, -3 }), Layers.PadMode.Edge), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.resize_.default": // resize_(Tensor(a!) self, SymInt[] size, *, MemoryFormat? memory_format=None) -> Tensor(a!)
                {
                    var self = node.Args[0].Tensor();
                    var size = node.Args[1].SymIntsAsTensor();
                    WarnKeywordArgIgnored("memory_format");
                    SetOutput(gm.Reshape(self, size, allowZero: true), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.round.default": // round(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    if (self.partialTensor.dataType == DataType.Int)
                        SetOutput(gm.Identity(self), ctx.ScalarType(self));
                    else
                        SetOutput(gm.Round(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.rsqrt.default": // rsqrt(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    self = PromoteTypeFloat(self);
                    SetOutput(gm.Rsqrt(self), ScalarType.FLOAT);
                    break;
                }
                case "torch.ops.aten.scalar_tensor.default": // scalar_tensor(Scalar s, *, ScalarType? dtype=None, Layout? layout=None, Device? device=None, bool? pin_memory=None) -> Tensor
                {
                    var dtype = node.KeywordArgs["dtype"].OptionalScalarType();
                    var s = node.Args[0].ScalarAsTensor();
                    var scalarType = dtype.GetValueOrDefault(ScalarType.FLOAT);
                    WarnKeywordArgIgnored("layout");
                    WarnKeywordArgIgnored("device");
                    WarnKeywordArgBoolIgnored("pin_memory", false);
                    var dataType = TorchUtilities.ScalarTypeToDataType(scalarType);
                    var output = (dataType == DataType.Int) ? PromoteTypeInt(s) : PromoteTypeFloat(s);
                    SetOutput(output, scalarType);
                    break;
                }
                case "torch.ops.aten.scatter.src": // scatter.src(Tensor self, int dim, Tensor index, Tensor src) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var index = node.Args[2].Tensor();
                    var src = node.Args[3].Tensor();
                    SetOutput(gm.ScatterElements(self, index, src, dim, Layers.ScatterReductionMode.None), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.scatter.value": // scatter.value(Tensor self, int dim, Tensor index, Scalar value) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var index = node.Args[2].Tensor();
                    var value = node.Args[3].ScalarAsTensor();
                    value = (self.partialTensor.dataType == DataType.Int) ? PromoteTypeInt(value) : PromoteTypeFloat(value);
                    var expandedValue = gm.Expand(value, gm.Shape(index, 0, TensorShape.maxRank));
                    SetOutput(gm.ScatterElements(self, index, expandedValue, dim, Layers.ScatterReductionMode.None), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.scatter_add.default": // scatter_add(Tensor self, int dim, Tensor index, Tensor src) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var index = node.Args[2].Tensor();
                    var src = node.Args[3].Tensor();
                    SetOutput(gm.ScatterElements(self, index, src, dim, Layers.ScatterReductionMode.Add), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.scatter_reduce.two": // scatter_reduce.two(Tensor self, int dim, Tensor index, Tensor src, str reduce, *, bool include_self=True) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var index = node.Args[2].Tensor();
                    var src = node.Args[3].Tensor();
                    var reduce = node.Args[4].String();
                    var includeSelf = node.KeywordArgs["include_self"].Bool(true);

                    var reduction = reduce switch
                    {
                        "sum" => Layers.ScatterReductionMode.Add,
                        "prod" => Layers.ScatterReductionMode.Mul,
                        "amax" => Layers.ScatterReductionMode.Max,
                        "amin" => Layers.ScatterReductionMode.Min,
                        "mean" => throw new TorchLayerImportException(target, $"Value {reduce} is not supported for \"reduction\""),
                        _ => throw new TorchLayerImportException(target, $"Value {reduce} is not supported for \"reduction\""),
                    };
                    if (includeSelf)
                    {
                        SetOutput(gm.ScatterElements(self, index, src, dim, reduction), ctx.ScalarType(self));
                    }
                    else
                    {
                        var shape = gm.Shape(index, 0, TensorShape.maxRank);
                        var dataType = self.partialTensor.dataType;
                        var (initialSrc, initialReduction) = reduction switch
                        {
                            Layers.ScatterReductionMode.Add => (dataType == DataType.Int ? gm.ConstantOfShape(shape, DataType.Int, 0f, 0) : gm.ConstantOfShape(shape, DataType.Float, 0f, 0), Layers.ScatterReductionMode.None),
                            Layers.ScatterReductionMode.Mul => (src, Layers.ScatterReductionMode.None),
                            Layers.ScatterReductionMode.Max => (dataType == DataType.Int ? gm.ConstantOfShape(shape, DataType.Int, 0f, int.MinValue) : gm.ConstantOfShape(shape, DataType.Float, float.MinValue, 0), Layers.ScatterReductionMode.Min),
                            Layers.ScatterReductionMode.Min => (dataType == DataType.Int ? gm.ConstantOfShape(shape, DataType.Int, 0f, int.MaxValue) : gm.ConstantOfShape(shape, DataType.Float, float.MaxValue, 0), Layers.ScatterReductionMode.Max),
                            _ => throw new ArgumentOutOfRangeException()
                        };
                        var scatter = gm.ScatterElements(self, index, initialSrc, dim, initialReduction);
                        SetOutput(gm.ScatterElements(scatter, index, src, dim, reduction), ctx.ScalarType(self));
                    }
                    break;
                }
                case "torch.ops.aten.select.int": // select.int(Tensor(a) self, int dim, SymInt index) -> Tensor(a)
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    var index = node.Args[2].SymIntAsTensor();
                    SetOutput(gm.Select(self, gm.Constant(dim), index), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.select_scatter.default": // select_scatter(Tensor self, Tensor src, int dim, SymInt index) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var src = node.Args[1].Tensor();
                    var (nodes, scalarType) = PromoteTypes(new[] { self, src });
                    var dim = node.Args[2].Int();
                    var index = node.Args[3].SymIntAsTensor();
                    var unsqueezedSrc = gm.Unsqueeze(nodes[1], gm.Constant(new[] { dim }));
                    var expandedIndex = gm.Expand(index, gm.Shape(unsqueezedSrc, 0, TensorShape.maxRank));
                    var output = gm.ScatterElements(nodes[0], expandedIndex, unsqueezedSrc, dim, Layers.ScatterReductionMode.None);
                    output = ConvertToUint8IfNeeded(scalarType, output);
                    SetOutput(output, scalarType);
                    break;
                }
                case "torch.ops.aten.sigmoid.default": // sigmoid(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Sigmoid(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.sign.default": // sign(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Sign(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.sin.default": // sin(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Sin(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.sinh.default": // sinh(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Sinh(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.slice.Tensor": // slice.Tensor(Tensor(a) self, int dim=0, SymInt? start=None, SymInt? end=None, SymInt step=1) -> Tensor(a)
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int(0);
                    var step = node.Args[4].SymIntAsTensor(gm.Constant(1));
                    var start = node.Args[2].SymIntAsTensor(gm.Where(gm.Less(step, gm.Constant(0)), gm.Constant(-1), gm.Constant(0)));
                    var end = node.Args[3].SymIntAsTensor(gm.Where(gm.Less(step, gm.Constant(0)), gm.Constant(int.MinValue), gm.Constant(int.MaxValue)));
                    var steps = gm.Reshape(step, gm.Constant(new[] { 1 }), true);
                    var starts = gm.Reshape(start, gm.Constant(new[] { 1 }), true);
                    var ends = gm.Reshape(end, gm.Constant(new[] { 1 }), true);
                    SetOutput(gm.Slice(self, starts, ends, gm.Constant(new[] { dim }), steps), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.slice_scatter.default": // slice_scatter(Tensor self, Tensor src, int dim=0, SymInt? start=None, SymInt? end=None, SymInt step=1) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var src = node.Args[1].Tensor();
                    var dim = node.Args[2].Int(0);
                    var step = node.Args[5].SymIntAsTensor(gm.Constant(1));
                    var start = node.Args[3].SymIntAsTensor(gm.Where(gm.Less(step, gm.Constant(0)), gm.Constant(-1), gm.Constant(0)));
                    var end = node.Args[4].SymIntAsTensor(gm.Where(gm.Less(step, gm.Constant(0)), gm.Constant(int.MinValue), gm.Constant(int.MaxValue)));
                    var steps = gm.Reshape(step, gm.Constant(new[] { 1 }), true);
                    var starts = gm.Reshape(start, gm.Constant(new[] { 1 }), true);
                    var ends = gm.Reshape(end, gm.Constant(new[] { 1 }), true);
                    SetOutput(gm.SliceSet(self, src, starts, ends, gm.Constant(new[] { dim }), steps), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.sort.default": // sort(Tensor self, int dim=-1, bool descending=False) -> (Tensor values, Tensor indices)
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int(-1);
                    var descending = node.Args[2].Bool(false);
                    var shape = gm.Shape(self, 0, TensorShape.maxRank);
                    var k = gm.Gather(shape, gm.Constant(new[] { dim }), 0);
                    var outputs = gm.TopK(self, k, dim, descending, sorted: true);
                    SetOutput(outputs[0], ctx.ScalarType(self), 0);
                    SetOutput(outputs[1], ScalarType.LONG, 1);
                    break;
                }
                case "torch.ops.aten.split_with_sizes.default": // split_with_sizes(Tensor(a -> *) self, SymInt[] split_sizes, int dim=0) -> Tensor(a)[]
                {
                    var self = node.Args[0].Tensor();
                    var splitSizes = node.Args[1].SymIntsAsTensor();
                    var dim = node.Args[2].Int(0);
                    var numOutputs = splitSizes.partialTensor.shape[0].value; // we can always take this because split_sizes comes from a SymInt[]
                    var outputs = gm.Split(self, splitSizes, dim, numOutputs);
                    SetOutputs(outputs, ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.sqrt.default": // sqrt(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    self = PromoteTypeFloat(self);
                    SetOutput(gm.Sqrt(self), ScalarType.FLOAT);
                    break;
                }
                case "torch.ops.aten.squeeze.dim": // squeeze.dim(Tensor(a) self, int dim) -> Tensor(a)
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    SetOutput(gm.Squeeze(self, gm.Constant(new[] { dim })), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.squeeze.dims": // squeeze.dims(Tensor(a) self, int[] dim) -> Tensor(a)
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Ints();
                    SetOutput(gm.Squeeze(self, gm.Constant(dim)), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.sub.Scalar": // sub.Scalar(Tensor self, Scalar other, Scalar alpha=1) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].ScalarAsTensor();
                    var alpha = node.Args[2].ScalarAsTensor(1);
                    var (nodes, scalarType) = PromoteTypes(new [] {self, other, alpha});
                    var scaledOther = gm.Mul(nodes[1], nodes[2]);
                    var output = gm.Sub(nodes[0], scaledOther);
                    output = ConvertToUint8IfNeeded(scalarType, output);
                    SetOutput(output, scalarType);
                    break;
                }
                case "torch.ops.aten.sub.Tensor": // sub.Tensor(Tensor self, Tensor other, *, Scalar alpha=1) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var other = node.Args[1].Tensor();
                    var alpha = node.KeywordArgs["alpha"].ScalarAsTensor(1);
                    var (nodes, scalarType) = PromoteTypes(new [] {self, other, alpha});
                    var scaledOther = gm.Mul(nodes[1], nodes[2]);
                    SetOutput(gm.Sub(nodes[0], scaledOther), scalarType);
                    break;
                }
                case "torch.ops.aten.sum.dim_IntList": // sum.dim_IntList(Tensor self, int[1]? dim, bool keepdim=False, *, ScalarType? dtype=None) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Ints(Array.Empty<int>());
                    var keepdim = node.Args[2].Bool(false);
                    WarnKeywordArgDtypeIgnored("dtype", ctx.ScalarType(self));
                    SetOutput(gm.ReduceSum(self, gm.Constant(dim), keepdims: keepdim, noopWithEmptyAxes: false), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.sym_is_contiguous.default": // sym_is_contiguous(Tensor self, MemoryFormat memory_format=contiguous_format) -> SymBool
                {
                    ErrorOpNotImplemented();
                    break;
                }
                case "torch.ops.aten.sym_numel.default": // sym_numel(Tensor self) -> SymInt
                {
                    var self = node.Args[0].Tensor();
                    SetOutputSymInt(gm.Size(self));
                    break;
                }
                case "torch.ops.aten.sym_size.int": // sym_size.int(Tensor self, int dim) -> SymInt
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    SetOutputSymInt(gm.Gather(gm.Shape(self, 0, TensorShape.maxRank), gm.Constant(dim), 0));
                    break;
                }
                case "torch.ops.aten.sym_storage_offset.default": // sym_storage_offset(Tensor self) -> SymInt
                {
                    WarnArgIgnored(0, "self");
                    SetOutputSymInt(gm.Constant(0));
                    break;
                }
                case "torch.ops.aten.sym_stride.int": // sym_stride.int(Tensor self, int dim) -> SymInt
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    if (dim < 0)
                    {
                        Warn(WarningType.Warning, $"{target} rank unknown");
                        dim += self.partialTensor.shape.rank;
                    }
                    var remainingShape = gm.Shape(self, dim + 1, TensorShape.maxRank);
                    SetOutputSymInt(gm.ReduceProd(remainingShape, gm.Constant(new[] { 0 }), false, false));
                    break;
                }
                case "torch.ops.aten.tan.default": // tan(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Tan(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.tanh.default": // tanh(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    SetOutput(gm.Tanh(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.topk.default": // topk(Tensor self, SymInt k, int dim=-1, bool largest=True, bool sorted=True) -> (Tensor values, Tensor indices)
                {
                    var self = node.Args[0].Tensor();
                    var k = gm.Unsqueeze(node.Args[1].SymIntAsTensor(), gm.Constant(new[] { 0 }));
                    var dim = node.Args[2].Int(-1);
                    var largest = node.Args[3].Bool(true);
                    var sorted = node.Args[4].Bool(true);
                    var outputs = gm.TopK(self, k, dim, largest, sorted);
                    SetOutput(outputs[0], ctx.ScalarType(self), 0);
                    SetOutput(outputs[1], ScalarType.LONG, 1);
                    break;
                }
                case "torch.ops.aten.trunc.default": // trunc(Tensor self) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    // TODO check this is necessary, i.e. does torch support int inputs for this op
                    if (self.partialTensor.dataType == DataType.Int)
                        SetOutput(gm.Identity(self), ctx.ScalarType(self));
                    else
                        SetOutput(gm.Trunc(self), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.unsqueeze.default": // unsqueeze(Tensor(a) self, int dim) -> Tensor(a)
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Int();
                    SetOutput(gm.Unsqueeze(self, gm.Constant(new[] { dim })), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.upsample_bilinear2d.vec": // upsample_bilinear2d.vec(Tensor input, SymInt[]? output_size, bool align_corners, float[]? scale_factors) -> Tensor
                {
                    var input = node.Args[0].Tensor();
                    var outputSize = node.Args[1].SymIntsAsTensor();
                    var alignCorners = node.Args[2].Bool();
                    var scaleFactors = node.Args[3].Floats();

                    var coordTransformMode = alignCorners ? Layers.CoordTransformMode.AlignCorners : Layers.CoordTransformMode.PytorchHalfPixel;

                    if (outputSize is not null)
                        SetOutput(gm.Resize(input, outputSize, Layers.ScaleMode.Sizes, coordTransformMode, Layers.InterpolationMode.Linear, Layers.NearestMode.RoundPreferFloor, new[] { 2, 3 }), ctx.ScalarType(input));
                    else
                        SetOutput(gm.Resize(input, gm.Constant(scaleFactors), Layers.ScaleMode.Scales, coordTransformMode, Layers.InterpolationMode.Linear, Layers.NearestMode.RoundPreferFloor, new[] { 2, 3 }), ctx.ScalarType(input));
                    break;
                }
                case "torch.ops.aten.upsample_nearest2d.vec": // upsample_nearest2d.vec(Tensor input, SymInt[]? output_size, float[]? scale_factors) -> Tensor
                {
                    var input = node.Args[0].Tensor();
                    var outputSize = node.Args[1].SymIntsAsTensor();
                    var scaleFactors = node.Args[2].Floats();

                    if (outputSize is not null)
                        SetOutput(gm.Resize(input, outputSize, Layers.ScaleMode.Sizes, Layers.CoordTransformMode.Asymmetric, Layers.InterpolationMode.Nearest, Layers.NearestMode.Floor, new[] { 2, 3 }), ctx.ScalarType(input));
                    else
                        SetOutput(gm.Resize(input, gm.Constant(scaleFactors), Layers.ScaleMode.Scales, Layers.CoordTransformMode.Asymmetric, Layers.InterpolationMode.Nearest, Layers.NearestMode.Floor, new[] { 2, 3 }), ctx.ScalarType(input));
                    break;
                }
                case "torch.ops.aten.var.correction": // var.correction(Tensor self, int[1]? dim=None, *, Scalar? correction=None, bool keepdim=False) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Ints(Array.Empty<int>());
                    var correction = node.KeywordArgs["correction"].ScalarAsFloat(1f);
                    var keepdim = node.KeywordArgs["keepdim"].Bool(false);
                    var axes = gm.Constant(dim);
                    SetOutput(gm.ReduceVariance(self, axes, keepdim, noopWithEmptyAxes: false, correction), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.var.dim": // var.dim(Tensor self, int[1]? dim, bool unbiased=True, bool keepdim=False) -> Tensor
                {
                    var self = node.Args[0].Tensor();
                    var dim = node.Args[1].Ints(Array.Empty<int>());
                    var unbiased = node.Args[2].Bool(true);
                    var keepdim = node.Args[3].Bool(false);
                    var axes = gm.Constant(dim);
                    SetOutput(gm.ReduceVariance(self, axes, keepdim, noopWithEmptyAxes: false, unbiased ? 1f : 0f), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.view.default": // view(Tensor(a) self, SymInt[] size) -> Tensor(a)
                {
                    var self = node.Args[0].Tensor();
                    var size = node.Args[1].SymIntsAsTensor();
                    SetOutput(gm.Reshape(self, size, allowZero: true), ctx.ScalarType(self));
                    break;
                }
                case "torch.ops.aten.where.self": // where.self(Tensor condition, Tensor self, Tensor other) -> Tensor
                {
                    var condition = node.Args[0].Tensor();
                    condition = PromoteTypeBool(condition);
                    var self = node.Args[1].Tensor();
                    var other = node.Args[2].Tensor();
                    var (nodes, scalarType) = PromoteTypes(new[] { self, other });
                    var output = gm.Where(condition, nodes[0], nodes[1]);
                    output = ConvertToUint8IfNeeded(scalarType, output);
                    SetOutput(output, scalarType);
                    break;
                }
                default:
                {
                    if (node.HasNoOutputs)
                        Warn(WarningType.Warning, $"{target} has no output and will be ignored");
                    else
                        ErrorOpNotImplemented();
                    break;
                }
            }

            int[] MakeDimsPositive(int[] dims)
            {
                for (var j = 0; j < dims.Length; j++)
                {
                    if (dims[j] < 0)
                        dims[j] += dims.Length;
                }
                return dims;
            }

            void ErrorOpNotImplemented()
            {
                Warn(WarningType.Error, $"{target} is not supported");
            }

            void WarnArgIgnored(int index, string name)
            {
                if (!node.Args[index].Ignore)
                    Warn(WarningType.Warning, $"{target} argument \"{name}\" is ignored");
            }

            void WarnArgBoolIgnored(int index, string name, bool defaultValue)
            {
                if (node.Args[index].Ignore)
                    return;
                var value = node.Args[index].OptionalBool();
                if (value != null && value != defaultValue)
                    Warn(WarningType.Warning, $"{target} argument \"{name}\" is ignored");
            }

            void WarnArgFloatIgnored(int index, string name, float defaultValue)
            {
                var value = node.Args[index].Float();
                if (Math.Abs(value - defaultValue) > 1e-6)
                    Warn(WarningType.Warning, $"{target} argument \"{name}\" is ignored");
            }

            void WarnArgSymIntIgnored(int index, string name, int defaultValue)
            {
                if (node.Args[index].Ignore)
                    return;
                var value = node.Args[index].SymIntAsInt();
                if (value != defaultValue)
                    Warn(WarningType.Warning, $"{target} argument \"{name}\" is ignored");
            }

            void WarnKeywordArgIgnored(string name)
            {
                if (!node.KeywordArgs[name].Ignore)
                    Warn(WarningType.Warning, $"{target} keyword argument \"{name}\" is ignored");
            }

            void WarnKeywordArgBoolIgnored(string name, bool defaultValue)
            {
                var value = node.KeywordArgs[name].OptionalBool();
                if (value != null && value != defaultValue)
                    Warn(WarningType.Warning, $"{target} keyword argument \"{name}\" is ignored");
            }

            void WarnKeywordArgDtypeIgnored(string name, ScalarType defaultValue)
            {
                var value = node.KeywordArgs[name].OptionalScalarType();
                if (value != null && value != defaultValue)
                    Warn(WarningType.Warning, $"{target} keyword argument \"{name}\" is ignored");
            }

            void AssertValue<T>(T value, T expectedValue, string name) where T : IEquatable<T>
            {
                if (!value.Equals(expectedValue))
                {
                    throw new TorchLayerImportException(target, $"Value \"{value}\" is not supported for \"{name}\". Expected value: \"{expectedValue}\"");
                }
            }

            void AssertTrue(bool value, string msg)
            {
                if (!value)
                {
                    throw new TorchLayerImportException(target, msg);
                }
            }

            (Graph.Node[], ScalarType) PromoteTypes(Graph.Node[] nodes)
            {
                var scalarType = GetCommonScalarType(nodes);
                return scalarType switch
                {
                    ScalarType.LONG => (PromoteTypesInt(nodes), scalarType),
                    ScalarType.BYTE => (PromoteTypesUInt8(nodes), scalarType),
                    ScalarType.FLOAT or _ => (PromoteTypesFloat(nodes), ScalarType.FLOAT)
                };
            }

            ScalarType GetCommonScalarType(Graph.Node[] nodes)
            {
                var commonScalarType = ScalarType.UNKNOWN;
                foreach (var n in nodes)
                {
                    if (n == null)
                        continue;
                    if (n.partialTensor.dataType == DataType.Float)
                        return ScalarType.FLOAT;
                    if (commonScalarType == ScalarType.LONG)
                        continue;
                    if (ctx.scalarTypes.TryGetValue(n, out var tmpScalarType))
                        commonScalarType = (tmpScalarType == ScalarType.BYTE) ? ScalarType.BYTE : ScalarType.LONG;
                }
                return commonScalarType == ScalarType.UNKNOWN ? ScalarType.LONG : commonScalarType;
            }

            bool AllInts(Graph.Node[] nodes)
            {
                return nodes.All(n => n is null || n.partialTensor.dataType == DataType.Int);
            }

            Graph.Node[] PromoteTypesFloat(Graph.Node[] nodes)
            {
                return nodes.Select(n => PromoteTypeFloat(n)).ToArray();
            }

            Graph.Node PromoteTypeFloat(Graph.Node n)
            {
                return n is null || n.partialTensor.dataType == DataType.Float ? n : gm.Cast(n, DataType.Float);
            }

            Graph.Node[] PromoteTypesInt(Graph.Node[] nodes)
            {
                return nodes.Select(n => PromoteTypeInt(n)).ToArray();
            }

            Graph.Node PromoteTypeInt(Graph.Node n)
            {
                return n is null || n.partialTensor.dataType == DataType.Int ? n : gm.Cast(n, DataType.Int);
            }

            Graph.Node[] PromoteTypesBool(Graph.Node[] nodes, bool checkNodeScalarTypes = false)
            {
                return nodes.Select(n => PromoteTypeBool(n, checkNodeScalarTypes)).ToArray();
            }

            Graph.Node PromoteTypeBool(Graph.Node n, bool checkNodeScalarType = false)
            {
                if (n is null)
                    return null;
                if (checkNodeScalarType && ctx.ScalarType(n) == ScalarType.BOOL)
                    return n;
                return (n.partialTensor.dataType == DataType.Int) ? gm.NotEqual(n, gm.Constant(0)) : gm.Cast(gm.NotEqual(n, gm.Constant(0.0f)), DataType.Int);
            }

            Graph.Node[] PromoteTypesUInt8(Graph.Node[] nodes)
            {
                return nodes.Select(n => PromoteTypeUInt8(n)).ToArray();
            }

            Graph.Node PromoteTypeUInt8(Graph.Node n)
            {
                if (n is null)
                    return null;
                if (ctx.scalarTypes.TryGetValue(n, out var scalarType) && scalarType == ScalarType.BYTE)
                    return n;
                return gm.Mod(n, gm.Constant(256), false);
            }

            Graph.Node ConvertToUint8IfNeeded(ScalarType scalarType, Graph.Node n)
            {
                return (scalarType == ScalarType.BYTE) ? PromoteTypeUInt8(n) : n;
            }
        }

        GraphModule ConvertFromJson(Stream entryStream, Dictionary<string, Tensor> constantTensors)
        {
            using var streamReader = new StreamReader(entryStream);
            var jsonString = streamReader.ReadToEnd();
            var exportedProgram = JsonConvert.DeserializeObject<ExportedProgram>(jsonString);

            // Fire model loaded event for analytics
            OnTorchModelLoaded?.Invoke(exportedProgram);

            // Validate operators and data types before conversion
            ValidateTorchGraph(exportedProgram);

            var ctx = new TorchContext();

            var torchVersion = ParseTorchVersion(exportedProgram.torch_version);
            if (torchVersion.Major != m_SupportedTorchVersion.Major || torchVersion.Minor != m_SupportedTorchVersion.Minor)
                Warn(WarningType.Warning, $"File Torch version {torchVersion} is different from supported Torch version {m_SupportedTorchVersion}. Unexpected behavior may occur.");

            // unsupported input kinds
            foreach (var inputSpec in exportedProgram.graph_module.signature.input_specs)
            {
                switch (inputSpec.kind)
                {
                    case "custom_obj":
                    case "token":
                        Warn(WarningType.Warning, $"Input of kind \"{inputSpec.kind}\" is not supported");
                        break;
                    case "user_input":
                    {
                        // inputs
                        var userInputSpec = inputSpec.value as UserInputSpec;
                        var tensorArgument = userInputSpec.arg.value as TensorArgument;
                        GetTensorMeta(ctx, exportedProgram, tensorArgument.name, out var dtype, out var shape);
                        var dataType = TorchUtilities.ScalarTypeToDataType(dtype);
                        var node = ctx.gm.Input(tensorArgument.name, dataType, shape);
                        ctx.AddTensor(tensorArgument.name, node, dtype);
                        break;
                    }
                    case "parameter":
                    {
                        // constant parameters
                        var parameter = inputSpec.value as InputToParameterSpec;
                        GetTensorMeta(ctx, exportedProgram, parameter.arg.name, out var dtype, out var shape);
                        var dataType = TorchUtilities.ScalarTypeToDataType(dtype);
                        var tensor = constantTensors[parameter.parameter_name];
                        Logger.AssertIsTrue(dataType == tensor.dataType, $"Parameter {parameter.parameter_name} expected type {dataType} but is {tensor.dataType}");
                        Logger.AssertIsTrue(shape.ToTensorShape() == tensor.shape, $"Parameter {parameter.parameter_name} expected shape {shape.ToTensorShape()} but is {tensor.shape}");
                        ctx.gm.attributes[parameter.parameter_name] = new ConstantTensor(tensor);
                        var node = ctx.gm.graph.GetAttr(parameter.parameter_name, parameter.arg.name);
                        node.partialTensor = PartialTensor.FromTensor(tensor);
                        ctx.AddTensor(parameter.arg.name, node, dtype);
                        break;
                    }
                    case "buffer":
                    {
                        // buffers
                        var buffer = inputSpec.value as InputToBufferSpec;
                        GetTensorMeta(ctx, exportedProgram, buffer.arg.name, out var dtype, out var shape);
                        var dataType = TorchUtilities.ScalarTypeToDataType(dtype);
                        var tensor = constantTensors[buffer.buffer_name];
                        Logger.AssertIsTrue(dataType == tensor.dataType, $"Buffer {buffer.buffer_name} expected type {dataType} but is {tensor.dataType}");
                        Logger.AssertIsTrue(shape.ToTensorShape() == tensor.shape, $"Buffer {buffer.buffer_name} expected shape {shape.ToTensorShape()} but is {tensor.shape}");
                        ctx.gm.attributes[buffer.buffer_name] = new ConstantTensor(tensor);
                        var node = ctx.gm.graph.GetAttr(buffer.buffer_name, buffer.arg.name);
                        node.partialTensor = PartialTensor.FromTensor(tensor);
                        ctx.AddTensor(buffer.arg.name, node, dtype);
                        break;
                    }
                    case "tensor_constant":
                    {
                        // tensor constants
                        var tensorConstant = inputSpec.value as InputToTensorConstantSpec;
                        GetTensorMeta(ctx, exportedProgram, tensorConstant.arg.name, out var dtype, out var shape);
                        var dataType = TorchUtilities.ScalarTypeToDataType(dtype);
                        var tensor = constantTensors[tensorConstant.tensor_constant_name];
                        Logger.AssertIsTrue(dataType == tensor.dataType, $"Tensor constant {tensorConstant.tensor_constant_name} expected type {dataType} but is {tensor.dataType}");
                        Logger.AssertIsTrue(shape.ToTensorShape() == tensor.shape, $"Tensor constant {tensorConstant.tensor_constant_name} expected shape {shape.ToTensorShape()} but is {tensor.shape}");
                        ctx.gm.attributes[tensorConstant.tensor_constant_name] = new ConstantTensor(tensor);
                        var node = ctx.gm.graph.GetAttr(tensorConstant.tensor_constant_name, tensorConstant.arg.name);
                        node.partialTensor = PartialTensor.FromTensor(tensor);
                        ctx.AddTensor(tensorConstant.arg.name, node, dtype);
                        break;
                    }
                }
            }

            // nodes
            foreach (var n in exportedProgram.graph_module.graph.nodes)
            {
                var node = new TorchNode(ctx, n);
                OnNode(ctx, node);
            }

            // outputs
            var outputs = new List<Graph.Node>();
            var outputNames = new List<string>();
            foreach (var outputSpec in exportedProgram.graph_module.signature.output_specs)
            {
                if (outputSpec.kind != "user_output")
                    continue;
                var userOutputSpec = outputSpec.value as UserOutputSpec;
                var name = (userOutputSpec.arg.value as TensorArgument).name;
                ctx.tensorNodes.TryGetValue(name, out var outputTensor);
                if (outputTensor == null)
                    continue;
                outputs.Add(outputTensor);
                outputNames.Add(name);
            }

            ctx.gm.Outputs(outputNames.ToArray(), outputs.ToArray());

            return ctx.gm;
        }

        /// <summary>
        /// Converts a Torch model to a Sentis `Model` object.
        /// </summary>
        /// <returns>The converted Sentis model.</returns>
        public override Model Convert()
        {
            GraphModule gm = null;
            var tensors = new Dictionary<string, Tensor>();
            using var zipToOpen = new FileStream(m_FilePath, FileMode.Open);
            using var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read);

            try
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.FullName.EndsWith("data/constants/model_constants_config.json"))
                        continue;

                    var basePath = entry.FullName.Substring(0, entry.FullName.IndexOf("data/constants/")) + "data/constants";

                    using var entryStream = entry.Open();
                    TorchModelConstants.LoadConstantsFromConfig(entryStream, archive, basePath, tensors);
                }

                if (ImportWarnings.Any(w => w.messageSeverity == WarningType.Error))
                {
                    throw new TorchImportException($"Could not import model due to errors when loading constants: {ImportWarnings.Last(w => w.messageSeverity == WarningType.Error).message}");
                }

                foreach (var entry in archive.Entries)
                {
                    if (!entry.FullName.EndsWith("data/weights/model_weights_config.json"))
                        continue;

                    var basePath = entry.FullName.Substring(0, entry.FullName.IndexOf("data/weights/")) + "data/weights";

                    using var entryStream = entry.Open();
                    TorchModelConstants.LoadWeightsFromConfig(entryStream, archive, basePath, tensors);
                }

                if (ImportWarnings.Any(w => w.messageSeverity == WarningType.Error))
                {
                    throw new TorchImportException($"Could not import model due to errors when loading weights: {ImportWarnings.Last(w => w.messageSeverity == WarningType.Error).message}");
                }

                foreach (var entry in archive.Entries)
                {
                    if (!entry.FullName.EndsWith("models/model.json"))
                        continue;
                    using var entryStream = entry.Open();
                    gm = ConvertFromJson(entryStream, tensors);
                }

                AssertNotNull(gm, $"Error importing model. Make sure file {m_FilePath} is a valid Torch .pt2 model exported with Torch version {m_SupportedTorchVersion}.");

                if (ImportWarnings.Any(w => w.messageSeverity == WarningType.Error))
                {
                    throw new TorchImportException($"Error importing model: {ImportWarnings.Last(w => w.messageSeverity == WarningType.Error).message}");
                }

                ModelOptimizer.OptimizeGraph(gm);

                return GraphConverter.GraphToModel(gm);
            }
            finally
            {
                foreach (var tensor in tensors.Values)
                    tensor?.Dispose();
            }
        }

        void ValidateTorchGraph(ExportedProgram exportedProgram)
        {
            // Track unsupported operators and datatypes for error handling
            var unsupportedOperators = new HashSet<string>();
            var unsupportedDataTypes = new HashSet<string>();

            var graph = exportedProgram.graph_module.graph;

            // Validate tensor data types
            foreach (var kvp in graph.tensor_values)
            {
                var tensorMeta = kvp.Value;
                var dtype = tensorMeta.dtype;
                var dataTypeStr = dtype.ToString();

                if (!IsDataTypeSupported(dtype))
                {
                    unsupportedDataTypes.Add(dataTypeStr);
                    OnTorchDataTypeUnsupported?.Invoke(dataTypeStr);
                }

                OnTorchDataType?.Invoke(dataTypeStr);
            }

            // Validate operators
            foreach (var node in graph.nodes)
            {
                var target = node.target;
                var nodeHasOutputs = node.outputs is { Count: > 0 }; //During conversion, we ignore nodes with no outputs, but we still want to track it

                if (!IsOperatorSupported(target) && nodeHasOutputs)
                {
                    unsupportedOperators.Add(target);
                    OnTorchOperatorUnsupported?.Invoke(target);
                }

                OnTorchOperator?.Invoke(target);
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
                throw new TorchImportException("Model contains unsupported operators or data types. See errors for details.");
            }
        }

        void AssertNotNull(object obj, string msg)
        {
            if (obj == null)
            {
                throw new TorchImportException(msg);
            }
        }
    }

    /// <summary>
    /// Represents an exception during the import of a Torch .pt2 model.
    /// </summary>
    class TorchImportException : ImportException
    {
        /// <inheritdoc cref="ImportException"/>
        public TorchImportException(string message)
            : base(message) { }
    }


    /// <summary>
    /// Represents an exception during the import of a Torch layer from a .pt2 model.
    /// </summary>
    class TorchLayerImportException : LayerImportException
    {
        /// <inheritdoc cref="LayerImportException"/>
        public TorchLayerImportException(string target, string message)
            : base($"{target}: {message}") { }
    }

    class TorchArgumentImportException: TorchLayerImportException
    {
        /// <inheritdoc cref="TorchLayerImportException"/>
        public TorchArgumentImportException(string target, string argName, string message)
            : base(target, $"for argument \"{argName}\": {message}") { }
        }
}
