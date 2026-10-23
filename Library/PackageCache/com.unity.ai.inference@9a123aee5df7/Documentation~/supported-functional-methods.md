---
uid: sentis-supported-functional-methods
---
# Supported functional methods

Sentis provides a set of operators and functional methods to work with tensors. This page lists the supported operators and functional methods, and provides usage examples.

## Supported operators

You can directly apply the operators to the tensors.

### Overloaded operators

You might use binary operators between functional tensors and scalar float, integer, and Boolean values. For example, 'x + 1', 'x % 2.5f', 'False ^ x'.

| Operator   | Sentis equivalent |
|------------|-------------------|
| -x         | Neg               |
| x + y      | Add               |
| x - y      | Sub               |
| x * y      | Mul               |
| x / y      | Div               |
| x % y      | Mod               |
| x > y      | Greater           |
| x >= y     | GreaterOrEqual    |
| x < y      | Less              |
| x <= y     | LessOrEqual       |
| x &#38; y  | BitwiseAnd        |
| x &#124; y | BitwiseOr         |
| x ^ y      | BitwiseXor        |
| ~x         | BitwiseNot        |

### Indexers

Sentis lets you index functional tensors with C# square bracket indexer notation. These map to the `Slice` and `SetSlice` operators.

The following table shows how you can use indexes and ranges. In these examples, x and y are functional tensors, while i and j are integers.

| Operator          | Description           |
|-------------------|-----------------------|
| x[i]              | Slice first dimension at index i and remove sliced dimension. |
| x[^i]             | Slice first dimension at index i from the end and remove sliced dimension. |
| x[i..j]           | Slice first dimension from index i inclusive to j exclusive. |
| x[i..]            | Slice first dimension from index i inclusive to end. |
| x[..^j]           | Slice first dimension from start to index j from end exclusive. |
| x[i, j]           | Slice first dimension at index i and second dimension at index j and remove sliced dimensions.|
| x[.., i]          | Slice second dimension at index i and remove sliced dimension. |
| x[i] = y          | Set slice i of x to be equal to y, the shape of y must be broadcastable to the slice shape of x. |
| x[i..j] = y[i..j] | Set slice of x to be equal to slice of y, the slice shape of y must be broadcastable to the slice shape of x. |

Sentis doesn't support setting a slice of a functional tensor directly as a scalar float or integer value with this method. Use the `Functional.Constant(value)` method to create a scalar functional tensor.

## Functional methods

Sentis supports many functional methods modelled after the PyTorch library. Each method maps to one or more Sentis layers.

The input parameters and outputs don't exactly match the PyTorch version. Check the API reference for more information.

| Operator          | PyTorch equivalent  | Sentis equivalent          |
|-------------------|-----------|------------------------|
| AsStrided         | as_strided          | AsStrided                  |
| Zeros             | zeros               | ConstantOfShape            |
| ZerosLike         | zeros_like          | Shape, ConstantOfShape     |
| Ones              | ones                | ConstantOfShape            |
| OnesLike          | ones_like           | Shape, ConstantOfShape     |
| ARange            | arange              | Range                      |
| LinSpace          | linspace            | Range                      |
| LogSpace          | logspace            | Pow, Range                 |
| Full              | full                | ConstantOfShape            |
| FullLike          | full_like           | Shape, ConstantOfShape     |
| Concat            | concat              | Concat                     |
| Gather            | gather              | GatherElements             |
| IndexSelect       | index_select        | Gather                     |
| MoveDim           | movedim             | MoveDim                    |
| Narrow            | narrow              | Narrow                     |
| NonZero           | nonzero             | NonZero                    |
| Permute           | permute             | Transpose                  |
| Reshape           | reshape             | Reshape                    |
| Select            | select              | Select                     |
| Scatter           | scatter             | ScatterElements            |
| SelectScatter     | select_scatter      | SliceSet, Unsqueeze        |
| SliceScatter      | slice_scatter       | SliceSet                   |
| ScatterAdd        | scatter_add         | ScatterElements            |
| Split             | split               | Split                      |
| Squeeze           | squeeze             | Squeeze                    |
| Stack             | stack               | Concat, Unsqueeze          |
| Take              | take                | Reshape, Gather            |
| Tile              | tile                | Tile                       |
| Transpose         | transpose           | MoveDim                    |
| Unsqueeze         | unsqueeze           | Unsqueeze                  |
| Where             | where               | Where                      |
| Bernoulli         | bernoulli           | Bernoulli                  |
| Multinomial       | multinomial         | Multinomial                |
| Normal            | normal              | RandomNormal               |
| NormalLike        | normal_like         | RandomNormalLike           |
| Rand              | rand                | RandomUniform              |
| RandLike          | rand_like           | RandomUniformLike          |
| RandInt           | rand_int            | Floor, RandomUniform       |
| RandIntLike       | randint_like        | Floor, RandomUniformLike   |
| RandN             | randn               | RandomNormal               |
| RandNLike         | randn_like          | RandomNormalLike           |
| RandomChoice      | numpy.random.choice | RandomChoice               |
| Abs               | abs                 | Abs                        |
| Acos              | acos                | Acos                       |
| Acosh             | acosh               | Acosh                      |
| Add               | add                 | Add                        |
| Asin              | asin                | Asin                       |
| Asinh             | asinh               | Asinh                      |
| Atan              | atan                | Atan                       |
| Atan2             | atan2               | Atan2                      |
| Atanh             | atanh               | Atanh                      |
| BitwiseAnd        | bitwise_and         | BitwiseAnd                 |
| BitwiseNot        | bitwise_not         | BitwiseNot                 |
| BitwiseOr         | bitwise_or          | BitwiseOr                  |
| BitwiseXor        | bitwise_xor         | BitwiseXor                 |
| Ceil              | ceil                | Ceil                       |
| Clamp             | clamp               | Clamp                      |
| Cos               | cos                 | Cos                        |
| Cosh              | cosh                | Cosh                       |
| Deg2Rad           | deg2rad             | ScalarMad                  |
| Div               | div                 | Div                        |
| Erf               | erf                 | Erf                        |
| Exp               | exp                 | Exp                        |
| Expm1             | expm1               | Expm1                      |
| FloatPower        | float_power         | Pow                        |
| Floor             | floor               | Floor                      |
| FloorDivide       | floor_divide        | Floor, Div                 |
| FMod              | fmod                | Mod                        |
| Frac              | frac                | Sub, Floor, Abs, Mul, Sign |
| Lerp              | lerp                | Add, ScalarMad, Sub        |
| Log               | log                 | Log                        |
| Log10             | log10               | Log10                      |
| Log1P             | log1p               | Log1p                      |
| Log2              | log2                | Log2                       |
| LogAddExp         | logaddexp           | Log, Add, Exp              |
| LogicalAnd        | logical_and         | And                        |
| LogicalNot        | logical_not         | Not                        |
| LogicalOr         | logical_or          | Or                         |
| LogicalXor        | logical_xor         | Xor                        |
| Mul               | mul                 | Mul                        |
| Neg               | neg                 | Neg                        |
| Positive          | positive            | -                          |
| Pow               | pow                 | Pow                        |
| Rad2Deg           | rad2deg             | ScalarMad                  |
| Reciprocal        | reciprocal          | Reciprocal                 |
| Remainder         | remainder           | Mod                        |
| Round             | round               | Round                      |
| RSqrt             | rsqrt               | Rsqrt                      |
| Sign              | sign                | Sign                       |
| Sin               | sin                 | Sin                        |
| Sinh              | sinh                | Sinh                       |
| Sqrt              | sqrt                | Sqrt                       |
| Square            | square              | Square                     |
| Tan               | tan                 | Tan                        |
| Tanh              | tanh                | Tanh                       |
| Trunc             | trunc               | Trunc                      |
| ArgMax            | argmax              | ArgMax                     |
| ArgMin            | argmin              | ArgMin                     |
| ReduceMax         | amax                | ReduceMax                  |
| ReduceMin         | amin                | ReduceMin                  |
| ReduceL1          |                     | ReduceL1                   |
| ReduceL2          |                     | ReduceL2                   |
| ReduceLogSumExp   | logsumexp           | ReduceLogSumExp            |
| ReduceMean        | mean                | ReduceMean                 |
| ReduceProd        | prod                | ReduceProd                 |
| ReduceSum         | sum                 | ReduceSum                  |
| ReduceSumSquare   |                     | ReduceSumSquare            |
| ReduceVariance    | var                 | ReduceVariance             |
| STFT              | stft                | STFT                       |
| BlackmanWindow    | blackman_window     | BlackmanWindow             |
| HammingWindow     | hamming_window      | HammingWindow              |
| HannWindow        | hann_window         | HannWindow                 |
| Equal             | eq                  | Equal                      |
| GreaterEqual      | greater_equal       | GreaterOrEqual             |
| Greater           | greater             | Greater                    |
| IsFinite          | isfinite            | Not, Or, IsInf, IsNaN      |
| IsInf             | isinf               | IsInf                      |
| IsNaN             | isnan               | IsNaN                      |
| LessEqual         | less_equal          | LessOrEqual                |
| Less              | less                | Less                       |
| Max               | maximum             | Max                        |
| Min               | minimum             | Min                        |
| NotEqual          | not_equal           | Not, Equal                 |
| TopK              | topk                | TopK                       |
| AtLeast1D         | atleast_1d          | Expand                     |
| AtLeast2D         | atleast_2d          | Expand                     |
| AtLeast3D         | atleast_3d          | Expand                     |
| BroadcastTo       | broadcast_to        | Expand                     |
| Clone             | clone               | Identity                   |
| CumSum            | cumsum              | CumSum                     |
| Diagonal          | diagonal            | Diagonal                   |
| Einsum            | einsum              | Einsum                     |
| Flip              | flip                | Slice                      |
| FlipLR            | fliplr              | Slice                      |
| FlipUD            | flipud              | Slice                      |
| Ravel             | ravel               | Reshape                    |
| TriL              | tril                | Trilu                      |
| TriU              | triu                | Trilu                      |
| MatMul            | matmul              | MatMul                     |
| Conv1D            | conv1d              | Conv                       |
| Conv2D            | conv2d              | Conv                       |
| Conv3D            | conv3d              | Conv                       |
| Conv1Transpose1D  | conv_transpose1d    | ConvTranspose              |
| Conv1Transpose2D  | conv_transpose2d    | ConvTranspose              |
| Conv1Transpose3D  | conv_transpose3d    | ConvTranspose              |
| AvgPool1D         | avg_pool1d          | AveragePool                |
| AvgPool2D         | avg_pool2d          | AveragePool                |
| AvgPool3D         | avg_pool3d          | AveragePool                |
| MaxPool1D         | max_pool1d          | MaxPool                    |
| MaxPool2D         | max_pool2d          | MaxPool                    |
| MaxPool3D         | max_pool3d          | MaxPool                    |
| Relu              | relu                | Relu                       |
| HardSwish         | hardswish           | HardSwish                  |
| Relu6             | relu6               | Relu6                      |
| Elu               | elu                 | Elu                        |
| Selu              | selu                | Selu                       |
| Celu              | celu                | Celu                       |
| LeakyRelu         | leaky_relu          | LeakyRelu                  |
| PRelu             | prelu               | PRelu                      |
| Gelu              | gelu                | Gelu                       |
| Mish              | mish                | Mish                       |
| Softsign          | softsign            | Softsign                   |
| Softplus          | softplus            | Softplus                   |
| Softmax           | softmax             | Softmax                    |
| LogSoftmax        | log_softmax         | LogSoftmax                 |
| Sigmoid           | sigmoid             | Sigmoid                    |
| HardSigmoid       | hardsigmoid         | HardSigmoid                |
| BatchNorm         | batch_norm          | BatchNormalization         |
| InstanceNorm      | instance_norm       | InstanceNormalization      |
| LocalResponseNorm | local_response_norm | LRN                        |
| OneHot            | one_hot             | OneHot                     |
| PixelShuffle      | pixel_shuffle       | DepthToSpace               |
| PixelUnshuffle    | pixel_unshuffle     | SpaceToDepth               |
| Interpolate       | interpolate         | Resize                     |
| GridSample        | grid_sample         | GridSample                 |
| NMS               | nms                 | NonMaxSuppression          |

## Additional resources

* [Supported ONNX operators](xref:sentis-supported-operators)
* [Import settings for ONNX models](xref:sentis-onnx-import-settings)
