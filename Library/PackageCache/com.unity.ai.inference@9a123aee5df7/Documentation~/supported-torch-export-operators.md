---
uid: sentis-supported-torch-export-operators
---
# Supported PyTorch Export operators

When you import a model, each [Core ATen](https://docs.pytorch.org/docs/stable/torch.compiler_ir.html#core-aten-ir) operator in the model graph becomes one or more Sentis layers. For more information, refer to [How Sentis optimizes a model](xref:sentis-models-concept#how-sentis-optimizes-a-model).

## Supported Core ATen operators

The following table shows the Core ATen operators that Sentis supports. It also outlines the data types that Sentis supports for each [backend type](xref:sentis-create-an-engine#backend-types).

|Name|Sentis operators|Supported data types with [`BackendType.CPU`](xref:Unity.InferenceEngine.BackendType.CPU)|Supported data types with [`BackendType.GPUCompute`](xref:Unity.InferenceEngine.BackendType.GPUCompute)|Supported data types with [`BackendType.GPUPixel`](xref:Unity.InferenceEngine.BackendType.GPUPixel)| Notes |
|-|-|-|-|-|--|
| aten._adaptive_avg_pool2d | `AveragePool` | float | float | float | Window sizes must be the same. |
| aten._adaptive_avg_pool3d | `AveragePool` | float | float | float | Window sizes must be the same. |
| aten._local_scalar_dense | `Identity` | float, int | float, int | float, int | |
| aten._log_softmax | `LogSoftmax` | float | float | float | |
| aten._native_batch_norm_legit | `BatchNormalization` | float | float | float | Only calculates first output. `training` and `momentum` arguments are ignored. |
| aten._native_batch_norm_legit_functional | `BatchNormalization` | float | float | float | Only calculates first output. `training` and `momentum` arguments are ignored. |
| aten._native_batch_norm_legit_no_training | `BatchNormalization` | float | float | float | Only calculates first output. `momentum` argument is ignored. |
| aten._softmax | `Softmax` | float | float | float | |
| aten._to_copy | `Identity` | float, int | float, int | float, int | `dtype`, `layout`, `device`, `pin_memory`, `non_blocking`, and `memory_format` keyword arguments are ignored. Output is the same type as input. |
| aten.abs | `Abs` | float, int | float, int | float, int | |
| aten.acos | `Acos` | float | float | float | |
| aten.acosh | `Acosh` | float | float | float | |
| aten.adaptive_avg_pool1d | `AveragePool` | float | float | float | Window sizes must be the same. |
| aten.add.Scalar | `Add`, `Mul` | float, int | float, int | float, int | |
| aten.add.Tensor | `Add`, `Mul` | float, int | float, int | float, int | |
| aten.addmm | `MatMul`, `Mul`, `Add` | float, int | float*, int* | float, int | Integer inputs are cast to floats. |
| aten.alias | `Identity` | float, int | float, int | float, int | |
| aten.amax | `ReduceMax` | float, int | float*, int* | float, int | |
| aten.amin | `ReduceMin` | float, int | float*, int* | float, int | |
| aten.any | `ReduceMax` | int | int* | int | |
| aten.any.dim | `ReduceMax` | int | int* | int | |
| aten.any.dims | `ReduceMax` | int | int* | int | |
| aten.arange.start_step | `Range` | float, int | float, int | float, int | Float inputs with int `dtype` is not supported. `layout`, `device`, and `pin_memory` keyword arguments are ignored. |
| aten.argmax | `ArgMax` | float, int | float, int | float, int | |
| aten.argmin | `ArgMin` | float, int | float, int | float, int | |
| aten.as_strided | `AsStrided` | float, int | float, int | float, int | |
| aten.asin | `Asin` | float | float | float | |
| aten.asinh | `Asinh` | float | float | float | |
| aten.atan | `Atan` | float | float | float | |
| aten.atan2 | `Atan2` | float, int | float, int | float, int | Integer inputs are cast to floats. |
| aten.atan2.out | `Atan2` | float, int | float, int | float, int | `out` keyword argument is ignored. Integer inputs are cast to floats. |
| aten.atanh | `Atanh` | float | float | float | |
| aten.avg_pool1d | `AveragePool` | float | float | float | `ceil_mode` argument is ignored. |
| aten.avg_pool2d | `AveragePool` | float | float | float | `ceil_mode` and `divisor_override` arguments are ignored. |
| aten.avg_pool3d | `AveragePool` | float | float | float | `ceil_mode` and `divisor_override` arguments are ignored. |
| aten.bitwise_and.Scalar | `BitwiseAnd` | int | int | int | |
| aten.bitwise_and.Tensor | `BitwiseAnd` | int | int | int | |
| aten.bitwise_not | `BitwiseNot` | int | int | int | |
| aten.bitwise_or.Scalar | `BitwiseOr` | int | int | int | |
| aten.bitwise_or.Tensor | `BitwiseOr` | int | int | int | |
| aten.bitwise_xor.Scalar | `BitwiseXor` | int | int | int | |
| aten.bitwise_xor.Tensor | `BitwiseXor` | int | int | int | |
| aten.bmm | `MatMul` | float, int | float*, int* | float, int | Integer inputs are cast to floats. |
| aten.cat | `Concat` | float, int | float, int | float, int | |
| aten.ceil | `Ceil` | float, int | float, int | float, int | |
| aten.clamp | `Clip` | float, int | float, int | float, int | |
| aten.clamp.Tensor | `Min`, `Max` | float, int | float, int | float, int | |
| aten.clone | `Identity` | float, int | float, int | float, int | `memory_format` keyword argument is ignored. |
| aten.constant_pad_nd | `Pad` | float, int | float, int | float, int | |
| aten.convolution | `Conv`, `ConvTranspose` | float, int | float*, int* | float, int | Integer inputs are cast to floats. |
| aten.copy | `CastLike` | float, int | float, int | float, int | `non_blocking` keyword argument is ignored. |
| aten.cos | `Cos` | float | float | float | |
| aten.cosh | `Cosh` | float | float | float | |
| aten.cumsum | `CumSum` | float, int | float, int | float, int | |
| aten.diagonal | `Diagonal` | float, int | float, int | float, int | |
| aten.div.Scalar | `Div` | float, int | float, int | float, int | Integer inputs are cast to floats. |
| aten.div.Scalar_mode | `Div`, `FloorDiv`, `TruncDiv` | float, int | float, int | float, int | Integer inputs are cast to floats if `rounding_mode` is not provided. |
| aten.div.Tensor | `Div` | float, int | float, int | float, int | Integer inputs are cast to floats. |
| aten.div.Tensor_mode | `Div`, `FloorDiv`, `TruncDiv` | float, int | float, int | float, int | Integer inputs are cast to floats  if `rounding_mode` is not provided. |
| aten.elu | `Elu`, `Mul` | float | float | float | |
| aten.embedding | `Gather` | float, int | float, int | float, int | `padding_idx`, `scale_grad_by_freq`, and `sparse` arguments are ignored. |
| aten.empty.memory_format | `ConstantOfShape` | float, int | float, int | float, int | `layout`, `device`, `pin_memory`, and `memory_format` keyword arguments are ignored. |
| aten.empty_strided | `ConstantOfShape` | float, int | float, int | float, int | `stride` argument is ignored. `layout`, `device`, and `pin_memory` keyword arguments are ignored. |
| aten.eq.Scalar | `Equal` | float, int | float, int | float, int | |
| aten.eq.Tensor | `Equal` | float, int | float, int | float, int | |
| aten.erf | `Erf` | float | float | float | |
| aten.exp | `Exp` | float | float | float | |
| aten.expand | `Expand` | float, int | float, int | float, int | `implicit` keyword argument is ignored. |
| aten.expm1 | `Expm1` | float | float | float | |
| aten.fill.Scalar | `ConstantOfShape` | float, int | float, int | float, int | |
| aten.flip | `Slice` | float, int | float, int | float, int | |
| aten.floor | `Floor` | float, int | float, int | float, int | |
| aten.floor_divide | `FloorDiv` | float, int | float, int | float, int | |
| aten.fmod.Scalar | `Mod` | float, int | float, int | float, int | |
| aten.fmod.Tensor | `Mod` | float, int | float, int | float, int | |
| aten.full | `ConstantOfShape` | float, int | float, int | float, int | `layout`, `device`, and `pin_memory` keyword arguments are ignored. |
| aten.full_like | `ConstantOfShape` | float, int | float, int | float, int | `layout`, `device`, `pin_memory`, and `memory_format` keyword arguments are ignored. |
| aten.gather | `GatherElements` | float, int | float, int | float, int | `sparse_grad` keyword argument is ignored. |
| aten.ge.Scalar | `GreaterOrEqual` | float, int | float, int | float, int | |
| aten.ge.Tensor | `GreaterOrEqual` | float, int | float, int | float, int | |
| aten.gelu | `Gelu`, `GeluFast` | float | float | float | `approximate` must be `none` or `tanh` |
| aten.grid_sampler_2d | `GridSample` | float | float | float | Supported interpolation modes: `Linear`, `Nearest`. Supported padding modes: `Zeros`, `Border`, `Reflection` |
| aten.gt.Scalar | `Greater` | float, int | float, int | float, int | |
| aten.gt.Tensor | `Greater` | float, int | float, int | float, int | |
| aten.hardtanh | `HardTanh` | float | float | float | |
| aten.index.Tensor | `GatherND` and transformations | float, int | float, int | float, int | |
| aten.index_put | `ScatterND` and transformations | float, int | float, int | float, int | Boolean `indices` with more than 1 elements are not supported. The first index is used. |
| aten.index_select | `Gather` | float, int | float, int | float, int | |
| aten.isinf | `IsInf` | float | float | float (Infs not supported) | |
| aten.isnan | `IsNaN` | float | float | float (NaNs not supported) | |
| aten.le.Scalar | `LessOrEqual` | float, int | float, int | float, int | |
| aten.le.Tensor | `LessOrEqual` | float, int | float, int | float, int | |
| aten.leaky_relu | `LeakyRelu` | float | float | float | |
| aten.log | `Log` | float, int | float, int | float, int | Integer inputs are cast to floats. |
| aten.log10 | `Log10` | float, int | float, int | float, int | Integer inputs are cast to floats. |
| aten.log1p | `Log1p` | float, int | float, int | float, int | Integer inputs are cast to floats. |
| aten.log2 | `Log2` | float, int | float, int | float, int | Integer inputs are cast to floats. |
| aten.logical_and | `And` | int | int | int | |
| aten.logical_not | `Not` | int | int | int | |
| aten.logical_or | `Or` | int | int | int | |
| aten.logical_xor | `Xor` | int | int | int | |
| aten.lt.Scalar | `Less` | float, int | float, int | float, int | |
| aten.lt.Tensor | `Less` | float, int | float, int | float, int | |
| aten.max.dim | `ArgMax`, `GatherElements` | float, int | float, int | float, int | |
| aten.max_pool2d_with_indices | `MaxPool` | float, int | float, int | float, int | Only calculates first output. `dilation`, and `ceil_mode` arguments are ignored. |
| aten.max_pool3d_with_indices | `MaxPool` | float, int | float, int | float, int | Only calculates first output. `dilation`, and `ceil_mode` arguments are ignored. |
| aten.maximum | `Max` | float, int | float, int | float, int | |
| aten.mean | `ReduceMean` | float | float | float | `dtype` keyword argument is ignored. Output is the same type as input. |
| aten.mean.dim | `ReduceMean` | float | float | float | `dtype` keyword argument is ignored. Output is the same type as input. |
| aten.min.dim | `ArgMin`, `GatherElements` | float, int | float, int | float, int | |
| aten.minimum | `Min` | float, int | float, int | float, int | |
| aten.mm | `MatMul` | float, int | float*, int* | float, int | Integer inputs are cast to floats. |
| aten.mul.Scalar | `Mul` | float, int | float, int | float, int | |
| aten.mul.Tensor | `Mul` | float, int | float, int | float, int | |
| aten.native_dropout | `Identity` | float, int | float, int | float, int | Only calculates first output. `p`, and `train` arguments are ignored. |
| aten.native_group_norm | `InstanceNormalization` | float | float | float | Only calculates first output. |
| aten.native_layer_norm | `LayerNormalization` | float | float | float | Only calculates first output. `normalized_shape` of rank greater than 1 is not supported. `weight`, and `bias` must be provided. |
| aten.ne.Scalar | `NotEqual` | float, int | float, int | float, int | |
| aten.ne.Tensor | `NotEqual` | float, int | float, int | float, int | |
| aten.neg | `Neg` | float, int | float, int | float, int | |
| aten.nonzero | `NonZero` | float, int | float, int | float, int | |
| aten.permute | `Transpose` | float, int | float, int | float, int | |
| aten.pow.Scalar | `Pow` | float, int | float, int | float, int | |
| aten.pow.Tensor_Scalar | `Pow` | float, int | float, int | float, int | |
| aten.pow.Tensor_Tensor | `Pow` | float, int | float, int | float, int | |
| aten.prod | `ReduceProd` | float, int | float, int | float, int | `dtype` keyword argument is ignored. Output is the same type as input. |
| aten.prod.dim_int | `ReduceProd` | float, int | float, int | float, int | `dtype` keyword argument is ignored. Output is the same type as input. |
| aten.rand | `RandomUniform` | float, int | float, int | float, int | `layout`, `device`, and `pin_memory` keyword arguments are ignored. |
| aten.randn | `RandomNormal` | float, int | float, int | float, int | `layout`, `device`, and `pin_memory` keyword arguments are ignored. |
| aten.randperm | `RandomUniform`, `TopK` | float, int | float, int | Not supported | `layout`, `device`, and `pin_memory` keyword arguments are ignored. |
| aten.reciprocal | `Reciprocal` | float, int | float, int | float, int | Integer inputs are cast to floats. |
| aten.reflection_pad1d | `Pad` | float, int | float, int | float, int | |
| aten.reflection_pad2d | `Pad` | float, int | float, int | float, int | |
| aten.reflection_pad3d | `Pad` | float, int | float, int | float, int | |
| aten.relu | `Relu` | float | float | float | |
| aten.remainder.Scalar | `Mod` | float, int | float, int | float, int | |
| aten.remainder.Tensor | `Mod` | float, int | float, int | float, int | |
| aten.repeat | `Tile` | float, int | float, int | float, int | |
| aten.replication_pad2d | `Pad` | float, int | float, int | float, int | |
| aten.replication_pad3d | `Pad` | float, int | float, int | float, int | |
| aten.resize_ | `Reshape` | float, int | float, int | float, int | `memory_format` keyword argument is ignored. |
| aten.round | `Round` | float, int | float, int | float, int | |
| aten.rsqrt | `Rsqrt` | float, int | float, int | float, int | Integer inputs are cast to floats. |
| aten.scalar_tensor | - | float, int | float, int | float, int | `layout`, `device`, and `pin_memory` keyword arguments are ignored. |
| aten.scatter.src | `ScatterElements` | float, int | float, int | float, int | |
| aten.scatter.value | `ScatterElements` | float, int | float, int | float, int | |
| aten.scatter_add | `ScatterElements` | float, int | float, int | float, int | |
| aten.scatter_reduce.two | `ScatterElements` | float, int | float, int | float, int | `mean` reduction is not supported. |
| aten.select.int | `Select` | float, int | float, int | float, int | |
| aten.select_scatter | `ScatterElements` | float, int | float, int | float, int | |
| aten.sigmoid | `Sigmoid` | float | float | float | |
| aten.sign | `Sign` | float, int | float, int | float, int | |
| aten.sin | `Sin` | float | float | float | |
| aten.sinh | `Sinh` | float | float | float | |
| aten.slice.Tensor | `Slice` | float, int | float, int | float, int | |
| aten.slice_scatter | `SliceSet` | float, int | float, int | float, int | |
| aten.sort | `TopK` | float, int | float, int | Not supported | |
| aten.split_with_sizes | `Split` | float, int | float, int | float, int | |
| aten.sqrt | `Sqrt` | float, int | float, int | float, int | Integer inputs are cast to floats. |
| aten.squeeze.dim | `Squeeze` | float, int | float, int | float, int | |
| aten.squeeze.dims | `Squeeze` | float, int | float, int | float, int | |
| aten.sub.Scalar | `Sub`, `Mul` | float, int | float, int | float, int | |
| aten.sub.Tensor | `Sub`, `Mul` | float, int | float, int | float, int | |
| aten.sum.dim_IntList | `ReduceSum` | float, int | float, int | float, int | `dtype` keyword argument is ignored. Output is the same type as input. |
| aten.sym_numel | `Size` | - | - | - | |
| aten.sym_size.int | `Shape`, `Gather` | - | - | - | |
| aten.sym_storage_offset | - | - | - | - | `self` argument is ignored, treated as zero. |
| aten.sym_stride.int | `Shape`, `ReduceProd` | - | - | - | |
| aten.tan | `Tan` | float | float | float | |
| aten.tanh | `Tanh` | float | float | float | |
| aten.topk | `TopK` | float, int | float, int | Not supported | |
| aten.trunc | `Trunc` | float, int | float, int | float, int | |
| aten.unsqueeze | `Unsqueeze` | float, int | float, int | float, int | |
| aten.upsample_bilinear2d.vec | `Resize` | float | float | float | |
| aten.upsample_nearest2d.vec | `Resize` | float | float | float | |
| aten.var.correction | `ReduceVariance` | float | float | float | |
| aten.var.dim | `ReduceVariance` | float | float | float | |
| aten.view | `Reshape` | float, int | float, int | float, int | |
| aten.where.self | `Where` | float, int | float, int | float, int | |

\* Sentis uses [DirectML](https://learn.microsoft.com/en-us/windows/ai/directml/dml) to accelerate these operators on supported hardware.

### Additional layers

Sentis might create additional layers when it [optimizes the model](xref:sentis-models-concept). A full list of Sentis-only layers is available [here](xref:sentis-supported-operators#sentis-only-layers).

## Unsupported operators

The following Core ATen operators are not supported in the current version of Sentis.

- aten._adaptive_avg_pool2d_backward
- aten._cdist_forward
- aten._embedding_bag
- aten._fft_r2c
- aten._native_batch_norm_legit.no_stats
- aten._pdist_forward
- aten.avg_pool2d_backward
- aten.col2im
- aten.convolution_backward
- aten.embedding_dense_backward
- aten.masked_scatter
- aten.max_pool2d_with_indices_backward
- aten.native_group_norm_backward
- aten.native_layer_norm_backward
- aten.sym_is_contiguous

## Tensor types

Integers are promoted to floats when needed for computation.

The following float and integer data types are supported:
- Float data types: `float32`, `float64`, `float16`, `bfloat16`
- Integer data types: `int32`, `int64`, `int16`, `int8`, `uint16`, `uint8`
- Booleans: `bool` are mapped to integer

The following tensor data types are not supported:
- Complex data types: `complex32`, `complex64`, `complex128`
- Quantized data types: `qint32`, `qint8`, `quint2x4`, `quint4x2`, `quint8`
- Larger unsigned ints: `uint32`, `uint64`

## Unsupported features

Sentis currently does not support the following PyTorch Export features:
- **Input kinds**: Inputs of kinds `buffers`, `custom_obj`, and `token` are not supported.
- **Named symbolic ints and floats**: Outputs of kind named symbolic ints and named symbolic floats are not supported.
- **Python side effects**: Mutation of Python tensors during forward is not supported.
- **Memory management**: Memory management is not supported. Keywords for `layout`, `device`, `pin_memory`, `non_blocking`, and `memory_format` are not supported.

## Additional resources

- [Core ATen IR](https://docs.pytorch.org/docs/stable/torch.compiler_ir.html#core-aten-ir)
- [Profile a model](xref:sentis-profile-a-model)
- [Supported functional methods](xref:sentis-supported-functional-methods)
- [Supported ONNX operators](xref:sentis-supported-operators)
