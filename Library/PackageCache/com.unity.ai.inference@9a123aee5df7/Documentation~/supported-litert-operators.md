---
uid: sentis-supported-litert-operators
---
# Supported LiteRT operators

When you import a model, each [LiteRT (formerly TensorFlow Lite)](https://ai.google.dev/edge/litert) operator in the model graph becomes one or more Sentis layers. For more information, refer to [How Sentis optimizes a model](xref:sentis-models-concept#how-sentis-optimizes-a-model).

## Supported LiteRT operators

The following table shows the LiteRT operators that Sentis supports. It also outlines the data types that Sentis supports for each [backend type](xref:sentis-create-an-engine#backend-types).

|Name|Sentis operators|Supported data types with [`BackendType.CPU`](xref:Unity.InferenceEngine.BackendType.CPU)|Supported data types with [`BackendType.GPUCompute`](xref:Unity.InferenceEngine.BackendType.GPUCompute)|Supported data types with [`BackendType.GPUPixel`](xref:Unity.InferenceEngine.BackendType.GPUPixel)| Notes |
|-|-|-|-|-|--|
|[abs](https://www.tensorflow.org/mlir/tfl_ops#tflabs_tflabsop) | `Abs` | float, int | float, int | float, int | |
|[add](https://www.tensorflow.org/mlir/tfl_ops#tfladd_tfladdop) | `Add` | float, int | float, int | float, int | |
|[add_n](https://www.tensorflow.org/mlir/tfl_ops#tfladd_n_tfladdnop) | `Add` | float, int | float, int | float, int | |
|[arg_max](https://www.tensorflow.org/mlir/tfl_ops#tflarg_max_tflargmaxop) | `ArgMax` | float, int | float, int | float, int | Dynamic `dim` input tensor is not supported |
|[arg_min](https://www.tensorflow.org/mlir/tfl_ops#tflarg_min_tflargminop) | `ArgMin` | float, int | float, int | float, int | Dynamic `dim` input tensor is not supported |
|[atan2](https://www.tensorflow.org/mlir/tfl_ops#tflatan2_tflatan2op) | `Atan2` | float | float | float | |
|[average_pool_2d](https://www.tensorflow.org/mlir/tfl_ops#tflaverage_pool_2d_tflaveragepool2dop) | `AveragePool` | float | float | float | |
|[batch_matmul](https://www.tensorflow.org/mlir/tfl_ops#tflbatch_matmul_tflbatchmatmulop) | `MatMul` | float | float* | float | |
|[bitwise_xor](https://www.tensorflow.org/mlir/tfl_ops#tflbitwise_xor_tflbitwisexorop) | `BitwiseXor` | int | int | int | |
|[broadcast_args](https://www.tensorflow.org/mlir/tfl_ops#tflbroadcast_args_tflbroadcastargsop) | `BroadcastArgs` | float, int | float, int | float, int | |
|[broadcast_to](https://www.tensorflow.org/mlir/tfl_ops#tflbroadcast_to_tflbroadcasttoop) | `Expand` | float, int | float, int | float, int | |
|[cast](https://www.tensorflow.org/mlir/tfl_ops#tflcast_tflcastop) | `Cast` | float, int | float, int | float, int | |
|[ceil](https://www.tensorflow.org/mlir/tfl_ops#tflceil_tflceilop) | `Ceil` | float | float | float | |
|[concatenation](https://www.tensorflow.org/mlir/tfl_ops#tflconcatenation_tflconcatenationop) | `Concat` | float, int | float, int | float, int | |
|[conv_2d](https://www.tensorflow.org/mlir/tfl_ops#tflconv_2d_tflconv2dop) | `Conv` | float | float* | float | |
|[conv_3d](https://www.tensorflow.org/mlir/tfl_ops#tflconv_3d_tflconv3dop) | `Conv` | float | float* | float | |
|[conv_3d_transpose](https://www.tensorflow.org/mlir/tfl_ops#tflconv_3d_transpose_tflconv3dtransposeop) | `ConvTranspose` | float | float | float | Dynamic `output_shape` input tensor is not supported. |
|[cos](https://www.tensorflow.org/mlir/tfl_ops#tflcos_tflcosop) | `Cos` | float | float | float | |
|[cumsum](https://www.tensorflow.org/mlir/tfl_ops#tflcumsum_tflcumsumop) | `CumSum` | float, int | float, int | float, int | |
|[depth_to_space](https://www.tensorflow.org/mlir/tfl_ops#tfldepth_to_space_tfldepthtospaceop) | `DepthToSpace` | float, int | float, int | float, int | |
|[depthwise_conv_2d](https://www.tensorflow.org/mlir/tfl_ops#tfldepthwise_conv_2d_tfldepthwiseconv2dop) | `Conv` | float | float* | float | |
|[dequantize](https://www.tensorflow.org/mlir/tfl_ops#tfldequantize_tfldequantize) | `Identity` | float | float | float | `int` type is not supported |
|[div](https://www.tensorflow.org/mlir/tfl_ops#tfldiv_tfldivop) | `Div` | float, int | float, int | float, int | |
|[elu](https://www.tensorflow.org/mlir/tfl_ops#tflelu_tfleluop) | `Elu` | float | float | float | |
|[embedding_lookup](https://www.tensorflow.org/mlir/tfl_ops#tflembedding_lookup_tflembeddinglookup) | `Gather` | float, int | float, int | float, int | |
|[equal](https://www.tensorflow.org/mlir/tfl_ops#tflequal_tflequalop) | `Equal` | float, int | float, int | float, int | |
|[exp](https://www.tensorflow.org/mlir/tfl_ops#tflexp_tflexpop) | `Exp` | float | float | float | |
|[expand_dims](https://www.tensorflow.org/mlir/tfl_ops#tflexpand_dims_tflexpanddimsop) | `Unsqueeze` | float, int | float, int | float, int | |
|[fill](https://www.tensorflow.org/mlir/tfl_ops#tflfill_tflfillop) | `Expand` | float, int | float, int | float, int | |
|[floor](https://www.tensorflow.org/mlir/tfl_ops#tflfloor_tflfloorop) | `Floor` | float | float | float | |
|[floor_div](https://www.tensorflow.org/mlir/tfl_ops#tflfloor_div_tflfloordivop) | `FloorDiv` | float, int | float, int | float, int | |
|[floor_mod](https://www.tensorflow.org/mlir/tfl_ops#tflfloor_mod_tflfloormodop) | `Mod` | float | float | float | |
|[fully_connected](https://www.tensorflow.org/mlir/tfl_ops#tflfully_connected_tflfullyconnectedop) | `MatMul` | float | float* | float |
|[gather](https://www.tensorflow.org/mlir/tfl_ops#tflgather_tflgatherop) | `Gather` | float, int | float, int | float, int | |
|[gather_nd](https://www.tensorflow.org/mlir/tfl_ops#tflgather_nd_tflgatherndop) | `GatherND` | float, int | float, int | float, int | |
|[gelu](https://www.tensorflow.org/mlir/tfl_ops#tflgelu_tflgeluop) | `Gelu` | float | float | float | |
|[greater](https://www.tensorflow.org/mlir/tfl_ops#tflgreater_tflgreaterop) | `Greater` | float, int | float, int | float, int | |
|[greater_equal](https://www.tensorflow.org/mlir/tfl_ops#tflgreater_equal_tflgreaterequalop) | `GreaterOrEqual` | float, int | float, int | float, int | |
|[hard_swish](https://www.tensorflow.org/mlir/tfl_ops#tflhard_swish_tflhardswishop) | `HardSwish` | float | float | float | |
|[l2_normalization](https://www.tensorflow.org/mlir/tfl_ops#tfll2_normalization_tfll2normalizationop) | `ReduceSumSquare` + `Sqrt` + `Div` | float | float* | float | |
|l2_pool_2d | `Square` + `AveragePool` + `Sqrt` | float | float | float | |
|[leaky_relu](https://www.tensorflow.org/mlir/tfl_ops#tflleaky_relu_tflleakyreluop) | `LeakyRelu` | float | float | float | |
|[less](https://www.tensorflow.org/mlir/tfl_ops#tflless_tfllessop) | `Less` | float, int | float, int | float, int | |
|[less_equal](https://www.tensorflow.org/mlir/tfl_ops#tflless_equal_tfllessequalop) | `LessOrEqual` | float, int | float, int | float, int | |
|[local_response_normalization](https://www.tensorflow.org/mlir/tfl_ops#tfllocal_response_normalization_tfllocalresponsenormalizationop) | `LRN` | float | Not supported | Not supported | |
|[log](https://www.tensorflow.org/mlir/tfl_ops#tfllog_tfllogop) | `Log` | float | float | float | |
|[log_softmax](https://www.tensorflow.org/mlir/tfl_ops#tfllog_softmax_tfllogsoftmaxop) | `LogSoftmax` | float | float | float | |
|[logical_and](https://www.tensorflow.org/mlir/tfl_ops#tfllogical_and_tfllogicalandop) | `LogicalAnd` | int | int | int | |
|[logical_not](https://www.tensorflow.org/mlir/tfl_ops#tfllogical_not_tfllogicalnotop) | `LogicalNot` | int | int | int | |
|[logical_or](https://www.tensorflow.org/mlir/tfl_ops#tfllogical_or_tfllogicalorop) | `LogicalOr` | int | int | int | |
|[logistic](https://www.tensorflow.org/mlir/tfl_ops#tfllogistic_tfllogisticop) | `Sigmoid` | float | float | float | |
|[max_pool_2d](https://www.tensorflow.org/mlir/tfl_ops#tflmax_pool_2d_tflmaxpool2dop) | `MaxPool` | float | float | float | |
|[maximum](https://www.tensorflow.org/mlir/tfl_ops#tflmaximum_tflmaximumop) | `Max` | float, int | float, int | float, int | |
|[mean](https://www.tensorflow.org/mlir/tfl_ops#tflmean_tflmeanop) | `ReduceMean` | float | float | float | Reduction over an axis of length 0 is not supported. `int` type is not supported. |
|[minimum](https://www.tensorflow.org/mlir/tfl_ops#tflminimum_tflminimumop) | `Min` | float, int | float, int | float, int | |
|[mirror_pad](https://www.tensorflow.org/mlir/tfl_ops#tflmirror_pad_tflmirrorpadop) | `Pad` | float, int | float, int | float, int | |
|[mul](https://www.tensorflow.org/mlir/tfl_ops#tflmul_tflmulop) | `Mul` | float, int | float, int | float, int | |
|[multinomial](https://www.tensorflow.org/mlir/tfl_ops#tflmultinomial_tflmultinomialop) | `Multinomial` | float | float | float | Dynamic `num_samples` input tensor is not supported |
|[neg](https://www.tensorflow.org/mlir/tfl_ops#tflneg_tflnegop) | `Neg` | float, int | float, int | float, int | |
|[not_equal](https://www.tensorflow.org/mlir/tfl_ops#tflnot_equal_tflnotequalop) | `NotEqual` | float, int | float, int | float, int | |
|[one_hot](https://www.tensorflow.org/mlir/tfl_ops#tflone_hot_tflonehotop) | `OneHot` | float, int | float, int | float, int | |
|[pack](https://www.tensorflow.org/mlir/tfl_ops#tflpack_tflpackop) | `Unsqueeze` + `Concat` | float, int | float, int | float, int | |
|[pad](https://www.tensorflow.org/mlir/tfl_ops#tflpad_tflpadop) | `Pad` | float, int | float, int | float, int | |
|[padv2](https://www.tensorflow.org/mlir/tfl_ops#tflpadv2_tflpadv2op) | `Pad` | float, int | float, int | float, int | |
|[pow](https://www.tensorflow.org/mlir/tfl_ops#tflpow_tflpowop) | `Pow` | float, int | float, int | float, int | |
|[prelu](https://www.tensorflow.org/mlir/tfl_ops#tflprelu_tflpreluop) | `PRelu` | float | float | float | |
|[random_standard_normal](https://www.tensorflow.org/mlir/tfl_ops#tflrandom_standard_normal_tflrandomstandardnormalop) | `RandomNormal` | float | float | float | |
|[random_uniform](https://www.tensorflow.org/mlir/tfl_ops#tflrandom_uniform_tflrandomuniformop) | `RandomUniform` | float | float | float | |
|[range](https://www.tensorflow.org/mlir/tfl_ops#tflrange_tflrangeop) | `Range` | float, int | float, int | float, int | |
|[rank](https://www.tensorflow.org/mlir/tfl_ops#tflrank_tflrankop) | `Shape` + `Size` | - | - | - | The operator returns a CPU tensor without downloading the input tensor |
|[reduce_all](https://www.tensorflow.org/mlir/tfl_ops#tflreduce_all_tflreduceallop) | `ReduceMin` | float, int | float*, int* | float, int | |
|[reduce_any](https://www.tensorflow.org/mlir/tfl_ops#tflreduce_any_tflreduceanyop) | `ReduceMax` | float, int | float*, int* | float, int | |
|[reduce_max](https://www.tensorflow.org/mlir/tfl_ops#tflreduce_max_tflreducemaxop) | `ReduceMax` | float, int | float*, int* | float, int | |
|[reduce_min](https://www.tensorflow.org/mlir/tfl_ops#tflreduce_min_tflreduceminop) | `ReduceMin` | float, int | float*, int* | float, int | |
|[reduce_prod](https://www.tensorflow.org/mlir/tfl_ops#tflreduce_prod_tflreduceprodop) | `ReduceProd` | float, int | float*, int* | float, int | |
|[relu](https://www.tensorflow.org/mlir/tfl_ops#tflrelu_tflreluop) | `Relu` | float | float | float | |
|[relu6](https://www.tensorflow.org/mlir/tfl_ops#tflrelu6_tflrelu6op) | `Relu6` | float | float | float | |
|[relu_0_to_1](https://www.tensorflow.org/mlir/tfl_ops#tflrelu_0_to_1_tflrelu0to1op) | `Clip` | float | float | float | |
|[relu_n1_to_1](https://www.tensorflow.org/mlir/tfl_ops#tflrelu_n1_to_1_tflrelun1to1op) | `Clip` | float | float | float | |
|[reshape](https://www.tensorflow.org/mlir/tfl_ops#tflreshape_tflreshapeop) | `Reshape` | float, int | float, int | float, int | |
|[resize_bilinear](https://www.tensorflow.org/mlir/tfl_ops#tflresize_bilinear_tflresizebilinearop) | `Resize` | float | float | float | |
|[resize_nearest_neighbor](https://www.tensorflow.org/mlir/tfl_ops#tflresize_nearest_neighbor_tflresizenearestneighborop) | `Resize` | float | float | float | |
|[reverse_v2](https://www.tensorflow.org/mlir/tfl_ops#tflreverse_v2_tflreversev2op) | `Slice` | float, int | float, int | float, int | |
|[round](https://www.tensorflow.org/mlir/tfl_ops#tflround_tflroundop) | `Round` | float | float | float | |
|[rsqrt](https://www.tensorflow.org/mlir/tfl_ops#tflrsqrt_tflrsqrtop) | `Rsqrt` | float | float | float | |
|[scatter_nd](https://www.tensorflow.org/mlir/tfl_ops#tflscatter_nd_tflscatterndop) | `ScatterND` | float, int | float, int | float, int | |
|[select](https://www.tensorflow.org/mlir/tfl_ops#tflselect_tflselectop) | `Where` | float, int | float, int | float, int |
|[select_v2](https://www.tensorflow.org/mlir/tfl_ops#tflselect_v2_tflselectv2op) | `Where` | float, int | float, int | float, int |
|[shape](https://www.tensorflow.org/mlir/tfl_ops#tflshape_tflshapeop) | `Shape` | - | - | - | The operator returns a CPU tensor without downloading the input tensor |
|[sign](https://www.tensorflow.org/mlir/tfl_ops#tflsign_tflsignop) | `Sign` | float, int | float, int | float, int | |
|[sin](https://www.tensorflow.org/mlir/tfl_ops#tflsin_tflsinop) | `Sin` | float | float | float | |
|[slice](https://www.tensorflow.org/mlir/tfl_ops#tflslice_tflsliceop) | `Slice` | float, int | float, int | float, int | Dynamic `begin` and `size` input tensors are not supported |
|[softmax](https://www.tensorflow.org/mlir/tfl_ops#tflsoftmax_tflsoftmaxop) | `Softmax` | float | float | float | |
|[space_to_depth](https://www.tensorflow.org/mlir/tfl_ops#tflspace_to_depth_tflspacetodepthop) | `SpaceToDepth` | float, int | float, int | float, int | |
|[sparse_to_dense](https://www.tensorflow.org/mlir/tfl_ops#tflsparse_to_dense_tflsparsetodenseop) | `Expand` + `ScatterND` | float, int | float, int | float, int | `indices` input tensor must have a rank greater than 0 |
|[split](https://www.tensorflow.org/mlir/tfl_ops#tflsplit_tflsplitop) | `Split` | float, int | float, int | float, int | Dynamic `split_dim` input tensor is not supported |
|[split_v](https://www.tensorflow.org/mlir/tfl_ops#tflsplit_v_tflsplitvop) | `Split` | float, int | float, int | float, int | Dynamic `size_split` and `split_dim` input tensors are not supported |
|[sqrt](https://www.tensorflow.org/mlir/tfl_ops#tflsqrt_tflsqrtop) | `Sqrt` | float | float | float | |
|[square](https://www.tensorflow.org/mlir/tfl_ops#tflsquare_tflsquareop) | `Square` | float, int | float, int | float, int |
|[squared_difference](https://www.tensorflow.org/mlir/tfl_ops#tflsquared_difference_tflsquareddifferenceop) | `Sub` + `Square` | float, int | float, int | float, int |
|[squeeze](https://www.tensorflow.org/mlir/tfl_ops#tflsqueeze_tflsqueezeop) | `Squeeze` | float, int | float, int | float, int | |
|[strided_slice](https://www.tensorflow.org/mlir/tfl_ops#tflstrided_slice_tflstridedsliceop) | `Slice` | float, int | float, int | float, int |  The `offset` and `ellipsis_mask` attributes are not supported. Dynamic `begin`, `end`, and `strides` input tensors are not supported for masked slices. |
|[sub](https://www.tensorflow.org/mlir/tfl_ops#tflsub_tflsubop) | `Sub` | float, int | float, int | float, int | |
|[sum](https://www.tensorflow.org/mlir/tfl_ops#tflsum_tflsumop) | `ReduceSum` | float, int | float, int | float, int | |
|[tanh](https://www.tensorflow.org/mlir/tfl_ops#tfltanh_tfltanhop) | `Tanh` | float | float | float | |
|[tile](https://www.tensorflow.org/mlir/tfl_ops#tfltile_tfltileop) | `Tile` | float, int | float, int | float, int | |
|[topk_v2](https://www.tensorflow.org/mlir/tfl_ops#tfltopk_v2_tfltopkv2op) | `TopK` | float, int | float, int | Not supported | |
|[transpose](https://www.tensorflow.org/mlir/tfl_ops#tfltranspose_tfltransposeop) | `Transpose` | float, int | float, int | float, int | Dynamic `perm` input tensor is not supported |
|[transpose_conv](https://www.tensorflow.org/mlir/tfl_ops#tfltranspose_conv_tfltransposeconvop) | `ConvTranspose` | float | float* | float | Supports 1D, 2D or 3D convolutions. Dynamic `output_shape` input tensor is not supported. |
|[unpack](https://www.tensorflow.org/mlir/tfl_ops#tflunpack_tflunpackop) | `Select` | float, int | float, int | float, int | |
|[where](https://www.tensorflow.org/mlir/tfl_ops#tflwhere_tflwhereop) | `NonZero` + `Transpose` | float, int | float, int | float, int |
|[zeros_like](https://www.tensorflow.org/mlir/tfl_ops#tflzeros_like_tflzeroslikeop) | `Shape` + `ConstantOfShape` | float, int | float, int | float, int ||

\* Sentis uses [DirectML](https://learn.microsoft.com/en-us/windows/ai/directml/dml) to accelerate these operators on supported hardware.

### Additional layers
LiteRT uses NHWC (batch size, height, width, channels) layout for image operations, such as convolution and pooling. Sentis uses [NCHW (batch size, channels, height, width)](xref:sentis-tensor-fundamentals#format) internally, and might insert `Transpose` operators where needed in the model graph. The Sentis importer tries to minimize the number of transposes used for intermediate tensor operations. Consequently, intermediate tensors in the Sentis model may use a different layout than in the original LiteRT model. Input and output tensor shapes remain unchanged.

Sentis might create additional layers when it [optimizes the model](xref:sentis-models-concept). A full list of Sentis-only layers is available [here](xref:sentis-supported-operators#sentis-only-layers).

## Unsupported features

Sentis currently does not support the following LiteRT features:
* **Multiple subgraphs**: If the model contains multiple subgraphs, only the first one is used. All others are ignored.
* **SIGN_BIT activation**: Operators using the `fused_activation_function` attribute with the value `SIGN_BIT` are not supported.
* **Custom operators**: Custom LiteRT operators are not supported.
* **Sparse tensors**: Sparse tensor formats are not supported.

### Unsupported tensor types

The following tensor data types are not supported in any context:

* `string`
* `complex64`
* `complex128`
* `resource`
* `variant`

In addition, the following types are not supported for constant tensors:

* `int4`
* `bfloat16`

### Unsupported operators

The following LiteRT operators are not supported in the current version of Sentis.

- assign_variable
- basic_lstm
- batch_to_space_nd
- bidirectional_sequence_lstm
- bitcast
- bucketize
- call_once
- complex_abs
- control_node
- custom
- custom_tf
- densify
- dilate
- dynamic_update_slice
- external_const
- fake_quant
- hashtable
- hashtable_find
- hashtable_import
- hashtable_size
- if
- imag
- lstm
- matrix_diag
- matrix_set_diag
- no_value
- non_max_suppression_v4
- non_max_suppression_v5
- NumericVerify
- poly_call
- pseudo_const
- pseudo_qconst
- pseudo_sparse_const
- pseudo_sparse_qconst
- quantize
- read_variable
- real
- reverse_sequence
- rfft2d
- right_shift
- segment_sum
- space_to_batch_nd
- StableHLO - All operators
- svdf
- unidirectional_sequence_lstm
- unidirectional_sequence_rnn
- unique
- unsorted_segment_max
- unsorted_segment_min
- unsorted_segment_prod
- unsorted_segment_sum
- var_handle
- while
- yield

## Additional resources

- ['tfl' Dialect](https://www.tensorflow.org/mlir/tfl_ops)
- [Profile a model](xref:sentis-profile-a-model)
- [Supported functional methods](xref:sentis-supported-functional-methods)
- [Supported ONNX operators](xref:sentis-supported-operators)
