---
uid: sentis-supported-operators
---
# Supported ONNX operators

When you import a model, each Open Neural Network Exchange (ONNX) operator in the model graph becomes a Sentis layer. A Sentis layer has the same name as the ONNX operator, unless the table shows the operator maps to a different layer.

For more information, refer to [How Sentis optimizes a model](xref:sentis-models-concept#how-sentis-optimizes-a-model).

## Supported ONNX operators

The following table lists the ONNX operators that Sentis supports and the data types supported for each [backend type](xref:sentis-create-an-engine#backend-types).

|Name|Supported data types with [`BackendType.CPU`](xref:Unity.InferenceEngine.BackendType.CPU)|Supported data types with [`BackendType.GPUCompute`](xref:Unity.InferenceEngine.BackendType.GPUCompute)|Supported data types with [`BackendType.GPUPixel`](xref:Unity.InferenceEngine.BackendType.GPUPixel)|Notes|
|-|-|-|-|-|
|[Abs](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Abs) | float, int | float, int | float, int | |
|[Acos](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Acos) | float | float | float | |
|[Acosh](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Acosh) | float | float | float | |
|[Add](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Add) | float, int | float, int | float, int | |
|[And](https://github.com/onnx/onnx/blob/main/docs/Operators.md#And) | int | int | int | |
|[ArgMax](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ArgMax) | float, int | float, int | float, int | |
|[ArgMin](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ArgMin) | float, int | float, int | float, int | |
|[Asin](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Asin) | float | float | float | |
|[Asinh](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Asinh) | float | float | float | |
|[Atan](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Atan) | float | float | float | |
|[Atanh](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Atanh) | float | float | float | |
|[AveragePool](https://github.com/onnx/onnx/blob/main/docs/Operators.md#AveragePool) | float | float (1D and 2D only) | float (1D and 2D only) | The `ceil_mode` and `count_include_pad` parameters aren't supported. |
|[BatchNormalization](https://github.com/onnx/onnx/blob/main/docs/Operators.md#BatchNormalization) | float | float | float | The `momentum`, `spatial` and `training_mode` parameters aren't supported. |
|[Bernoulli](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Bernoulli) | float, int | float, int | float, int | |
|[BitwiseAnd](https://github.com/onnx/onnx/blob/main/docs/Operators.md#BitwiseAnd) | int | int | int | |
|[BitwiseNot](https://github.com/onnx/onnx/blob/main/docs/Operators.md#BitwiseNot) | int | int | int | |
|[BitwiseOr](https://github.com/onnx/onnx/blob/main/docs/Operators.md#BitwiseOr) | int | int | int | |
|[BitwiseXor](https://github.com/onnx/onnx/blob/main/docs/Operators.md#BitwiseXor) | int | int | int | |
|[BlackmanWindow](https://github.com/onnx/onnx/blob/main/docs/Operators.md#BlackmanWindow) | float | float | float | |
|[Cast](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Cast) | float, int, short | float, int, short | float, int, short | |
|[CastLike](https://github.com/onnx/onnx/blob/main/docs/Operators.md#CastLike) | float, int, short | float, int, short | float, int, short | |
|[Ceil](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Ceil) | float | float | float | |
|[Celu](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Celu) | float | float | float | |
|[Clip](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Clip) | float, int | float, int | float, int | |
|[Compress](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Compress) | float, int | Not supported | Not supported | |
|[Concat](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Concat) | float, int | float, int | float, int | |
|[Constant](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Constant) | - | - | - | The `sparse_value`, `value_string` and `value_strings` parameters aren't supported. |
|[ConstantOfShape](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ConstantOfShape) | float, int | float, int | float, int | |
|[Conv](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Conv) | float | float* | float | Supports 1D, 2D or 3D convolutions. |
|[ConvTranspose](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ConvTranspose) | float | float | float | Supports 1D, 2D or 3D convolutions. The `output_shape` parameter isn't supported. |
|[Cos](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Cos) | float | float | float | |
|[Cosh](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Cosh) | float | float | float | |
|[CumSum](https://github.com/onnx/onnx/blob/main/docs/Operators.md#CumSum) | float, int | float, int | float, int | |
|[DepthToSpace](https://github.com/onnx/onnx/blob/main/docs/Operators.md#DepthToSpace) | float, int | float, int | float, int | |
|[DFT](https://github.com/onnx/onnx/blob/main/docs/Operators.md#DFT) | float | float | float | |
|[Div](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Div) | float, int | float, int | float, int | |
|[Dropout](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Dropout) | - | - | - | The operator maps to the Sentis layer `Identity`|
|[Einsum](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Einsum) | float | float (1 or 2 inputs only) | Not supported | |
|[Elu](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Elu) | float | float | float | |
|[Equal](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Equal) | float, int | float, int | float, int | |
|[Erf](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Erf) | float | float | float | |
|[Exp](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Exp) | float | float | float | |
|[Expand](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Expand) | float, int | float, int | float, int | |
|[Flatten](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Flatten) | float, int | float, int | float, int | |
|[Floor](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Floor) | float | float | float | |
|[Gather](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Gather) | float, int | float, int | float, int | |
|[GatherElements](https://github.com/onnx/onnx/blob/main/docs/Operators.md#GatherElements) | float, int | float, int | float, int | |
|[GatherND](https://github.com/onnx/onnx/blob/main/docs/Operators.md#GatherND) | float, int | float, int | float, int | |
|[Gemm](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Gemm) | float | float | float | |
|[GlobalAveragePool](https://github.com/onnx/onnx/blob/main/docs/Operators.md#GlobalAveragePool) | float | float | float | |
|[GlobalMaxPool](https://github.com/onnx/onnx/blob/main/docs/Operators.md#GlobalMaxPool) | float | float | float | |
|[Greater](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Greater) | float, int | float, int | float, int | |
|[GreaterOrEqual](https://github.com/onnx/onnx/blob/main/docs/Operators.md#GreaterOrEqual) | float, int | float, int | float, int | |
|[GridSample](https://github.com/onnx/onnx/blob/main/docs/Operators.md#GridSample) | float | float | float | |
|[HammingWindow](https://github.com/onnx/onnx/blob/main/docs/Operators.md#HammingWindow) | float | float | float | |
|[HannWindow](https://github.com/onnx/onnx/blob/main/docs/Operators.md#HannWindow) | float | float | float | |
|[Hardmax](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Hardmax) | float | float | float | |
|[HardSigmoid](https://github.com/onnx/onnx/blob/main/docs/Operators.md#HardSigmoid) | float | float | float | |
|[HardSwish](https://github.com/onnx/onnx/blob/main/docs/Operators.md#HardSwish) | float | float | float | |
|[Identity](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Identity) | float, int | float, int | float, int | |
|[InstanceNormalization](https://github.com/onnx/onnx/blob/main/docs/Operators.md#InstanceNormalization) | float | float | float | |
|[IsInf](https://github.com/onnx/onnx/blob/main/docs/Operators.md#IsInf) | float | float | float (Infs not supported) | |
|[IsNaN](https://github.com/onnx/onnx/blob/main/docs/Operators.md#IsNaN) | float | float | float (NaNs not supported) | |
|[LayerNormalization](https://github.com/onnx/onnx/blob/main/docs/Operators.md#LayerNormalization) | float | float | float | |
|[LeakyRelu](https://github.com/onnx/onnx/blob/main/docs/Operators.md#LeakyRelu) | float | float | float | |
|[Less](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Less) | float, int | float, int | float, int | |
|[LessOrEqual](https://github.com/onnx/onnx/blob/main/docs/Operators.md#LessOrEqual) | float, int | float, int | float, int | |
|[Log](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Log) | float | float | float | |
|[LogSoftmax](https://github.com/onnx/onnx/blob/main/docs/Operators.md#LogSoftmax) | float | float | float | |
|[LRN](https://github.com/onnx/onnx/blob/main/docs/Operators.md#LRN) | float | Not supported | Not supported | |
|[LSTM](https://github.com/onnx/onnx/blob/main/docs/Operators.md#LSTM) | float | float | Not supported | |
|[MatMul](https://github.com/onnx/onnx/blob/main/docs/Operators.md#MatMul) | float | float* | float | |
|[Max](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Max) | float, int | float, int | float, int | |
|[MaxPool](https://github.com/onnx/onnx/blob/main/docs/Operators.md#MaxPool) | float | float (1D and 2D only) | float (1D and 2D only) | The `ceil_mode`, `dilations` and `storage_order` parameters aren't supported. |
|[Mean](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Mean) | float | float | float | The operator maps to the Sentis layers `Add` and `ScalarMad`. |
|[MelWeightMatrix](https://github.com/onnx/onnx/blob/main/docs/Operators.md#MelWeightMatrix) | float | float | float | |
|[Min](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Min) | float, int | float, int | float, int | |
|[Mod](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Mod) | float, int | float, int | float, int | |
|[Mish](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Mish) | float | float | float | |
|[Mul](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Mul) | float, int | float, int | float, int | |
|[Multinomial](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Multinomial) | float | float | float | |
|[Neg](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Neg) | float, int | float, int | float, int | |
|[NonMaxSuppression](https://github.com/onnx/onnx/blob/main/docs/Operators.md#NonMaxSuppression) | float | float | Not supported | |
|[NonZero](https://github.com/onnx/onnx/blob/main/docs/Operators.md#NonZero) | float, int | Not supported | Not supported | |
|[Not](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Not) | int | int | int | |
|[OneHot](https://github.com/onnx/onnx/blob/main/docs/Operators.md#OneHot) | float, int | float, int | float, int | |
|[Or](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Or) | int | int | int | |
|[Pad](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Pad) | float, int | float, int | float, int | |
|[Pow](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Pow) | float, int | float, int | float, int | |
|[PRelu](https://github.com/onnx/onnx/blob/main/docs/Operators.md#PRelu) | float | float | float | |
|[RandomNormal](https://github.com/onnx/onnx/blob/main/docs/Operators.md#RandomNormal) | float | float | float | |
|[RandomNormalLike](https://github.com/onnx/onnx/blob/main/docs/Operators.md#RandomNormalLike) | float | float | float | |
|[RandomUniform](https://github.com/onnx/onnx/blob/main/docs/Operators.md#RandomUniform) | float | float | float | |
|[RandomUniformLike](https://github.com/onnx/onnx/blob/main/docs/Operators.md#RandomUniformLike) | float | float | float | |
|[Range](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Range) | float, int | float, int | float, int | |
|[Reciprocal](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Reciprocal) | float | float | float | |
|[ReduceL1](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ReduceL1) | float, int | float*, int* | float, int | |
|[ReduceL2](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ReduceL2) | float | float* | float | |
|[ReduceLogSum](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ReduceLogSum) | float | float* | float | |
|[ReduceLogSumExp](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ReduceLogSumExp) | float | float | float | |
|[ReduceMax](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ReduceMax) | float, int | float*, int* | float, int | |
|[ReduceMean](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ReduceMean) | float | float* | float | |
|[ReduceMin](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ReduceMin) | float, int | float*, int* | float, int | |
|[ReduceProd](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ReduceProd) | float, int | float*, int*| float, int | |
|[ReduceSum](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ReduceSum) | float, int | float*, int* | float, int | |
|[ReduceSumSquare](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ReduceSumSquare) | float, int | float*, int* | float, int | |
|[Relu](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Relu) | float | float | float | |
|[Reshape](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Reshape) | float, int | float, int | float, int | |
|[Resize](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Resize) | float | float | float | The `cubic_coeff_a`, `exclude_outside`, `extrapolation_value` and `roi`  parameters aren't supported. The `half_pixel_symmetric` option for `coordinate_transform_mode` is not supported.  |
|[RMSNormalization](https://github.com/onnx/onnx/blob/main/docs/Operators.md#RmsNormalization) | float | float | float |
|[RoiAlign](https://github.com/onnx/onnx/blob/main/docs/Operators.md#RoiAlign) | float | float | float | |
|[Round](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Round) | float | float | float | |
|[Scatter (deprecated)](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Scatter) | float, int | float, int | float, int | The operator maps to the Sentis layer `ScatterElements`. |
|[ScatterElements](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ScatterElements) | float, int | float, int (no ScatterReductionMode) | float, int | |
|[ScatterND](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ScatterND) | float, int | float, int | float, int | |
|[Selu](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Selu) | float | float | float | |
|[Shape](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Shape) | - | - | - | The operator returns a CPU tensor without downloading the input tensor. |
|[Shrink](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Shrink) | float | float | float | |
|[Sigmoid](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Sigmoid) | float | float | float | |
|[Sign](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Sign) | float, int | float, int | float, int | |
|[Sin](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Sin) | float | float | float | |
|[Sinh](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Sinh) | float | float | float | |
|[Size](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Size) | - | - | - | The operator returns a CPU tensor without downloading the input tensor. |
|[Slice](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Slice) | float, int | float, int | float, int | |
|[Softmax](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Softmax) | float | float | float | |
|[Softplus](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Softplus) | float | float | float | |
|[Softsign](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Softsign) | float | float | float | |
|[SpaceToDepth](https://github.com/onnx/onnx/blob/main/docs/Operators.md#SpaceToDepth) | float, int | float, int | float, int | |
|[Split](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Split) | float, int | float, int | float, int | |
|[Sqrt](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Sqrt) | float | float | float | |
|[Squeeze](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Squeeze) | float, int | float, int | float, int | |
|[STFT](https://github.com/onnx/onnx/blob/main/docs/Operators.md#STFT) | float | float | float | |
|[Sub](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Sub) | float, int | float, int | float, int | |
|[Sum](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Sum) | float, int | float, int | float, int | The operator maps to the Sentis layer `Add`. |
|[Swish](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Swish) | float | float | float |
|[Tan](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Tan) | float | float | float | |
|[Tanh](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Tanh) | float | float | float | |
|[ThresholdedRelu](https://github.com/onnx/onnx/blob/main/docs/Operators.md#ThresholdedRelu) | float | float | float | |
|[Tile](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Tile) | float, int | float, int | float, int | |
|[TopK](https://github.com/onnx/onnx/blob/main/docs/Operators.md#TopK) | float, int | float, int | Not supported | |
|[Transpose](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Transpose) | float, int | float, int | float, int | |
|[Trilu](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Trilu) | float, int | float, int | float, int | |
|[Unsqueeze](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Unsqueeze) | float, int | float, int | float, int | |
|[Upsample (deprecated)](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Upsample) | float | float | float | The operator maps to the Sentis layer `Resize`. |
|[Where](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Where) | float, int | float, int | float, int | |
|[Xor](https://github.com/onnx/onnx/blob/main/docs/Operators.md#Xor) | int | int | int | |

\* Sentis uses [DirectML](https://learn.microsoft.com/en-us/windows/ai/directml/dml) to accelerate these operators on supported hardware.

### Sentis-only layers

Sentis might create the following layers when it [optimizes the model](xref:sentis-models-concept).

|Name|Supported data types with [`BackendType.CPU`](xref:Unity.InferenceEngine.BackendType.CPU)|Supported data types with [`BackendType.GPUCompute`](xref:Unity.InferenceEngine.BackendType.GPUCompute)|Supported data types with [`BackendType.GPUPixel`](xref:Unity.InferenceEngine.BackendType.GPUPixel)|
|-|-|-|-|
|Atan2 | float | float | float |
|BroadcastArgs | int | - | - |
|Dense | float | float* | float |
|DequantizeUint8 | byte | byte | byte |
|FloorDiv | float, int | float, int | float, int |
|Gelu | float | float | float |
|GeluFast | float | float | float |
|MatMul2D | float | float* | float |
|MoveDim | float, int | float, int | float, int |
|Narrow | float, int | float, int | float, int |
|NotEqual | float, int | float, int | float, int |
|RandomChoice | float, int | float, int | float, int |
|Relu6 | float | float | float |
|ScalarMad | float, int | float, int | float, int |
|Select | float, int | float, int | float, int |
|SliceSet | float, int | float, int | float, int |
|Square | float, int | float, int | float, int |
|TrueDiv | float, int | float, int | float, int |
|ScaleBias | float | float | float |

\* Sentis uses [DirectML](https://learn.microsoft.com/en-us/windows/ai/directml/dml) to accelerate these operators on supported hardware.

## Unsupported operators

The following ONNX operators aren't supported in the current version of Sentis.

- AffineGrid
- Attention
- BitShift
- CenterCropPad
- Col2Im
- ConcatFromSequence
- ConvInteger
- DeformConv
- DequantizeLinear
- Det
- DynamicQuantizeLinear
- EyeLike
- GlobalLpPool
- GroupNormalization
- GRU
- If
- ImageDecoder
- Loop
- LpNormalization
- LpPool
- MatMulInteger
- MaxRoiPool
- MaxUnpool
- MeanVarianceNormalization
- NegativeLogLikelihoodLoss
- Optional
- OptionalGetElement
- OptionalHasElement
- QLinearConv
- QLinearMatMul
- QuantizeLinear
- RegexFullMatch
- ReverseSequence
- RNN
- RotaryEmbedding
- Scan
- SequenceAt
- SequenceConstruct
- SequenceEmpty
- SequenceErase
- SequenceInsert
- SequenceLength
- SequenceMap
- SoftmaxCrossEntropyLoss
- SplitToSequence
- StringConcat
- StringNormalizer
- StringSplit
- TensorScatter
- TfIdfVectorizer
- Unique

## Additional resources

- [ONNX operator schemas](https://github.com/onnx/onnx/blob/main/docs/Operators.md)
- [Export an ONNX file from a machine learning framework](xref:sentis-export-convert-onnx)
- [Profile a model](xref:sentis-profile-a-model)
- [Supported functional methods](xref:sentis-supported-functional-methods)
