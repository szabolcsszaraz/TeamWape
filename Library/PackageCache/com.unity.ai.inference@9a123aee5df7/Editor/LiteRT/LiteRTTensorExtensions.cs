using System;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine.Editor.LiteRT
{
    static class LiteRTTensorExtensions
    {
        public static int[] Shape(this Tensor tensor)
        {
            var shapeSignatureArray = tensor.GetShapeSignatureArray();
            var shapeArray = tensor.GetShapeArray();
            shapeArray ??= Array.Empty<int>();
            return shapeSignatureArray ?? shapeArray;
        }

        public static DynamicTensorShape DynamicShape(this Tensor tensor)
        {
            var shapeSignatureArray = tensor.GetShapeSignatureArray();
            var shapeArray = tensor.GetShapeArray();
            return new DynamicTensorShape(shapeSignatureArray ?? shapeArray);
        }

        public static DataType GetDataType(this Tensor tensor)
        {
            return ToDataType(tensor.Type);
        }

        static DataType ToDataType(this TensorType tensorType)
        {
            return tensorType switch
            {
                TensorType.FLOAT32 or TensorType.FLOAT16 or TensorType.FLOAT64 or TensorType.BFLOAT16 => DataType.Float,
                TensorType.INT32 or TensorType.UINT8 or TensorType.INT64 or TensorType.BOOL or TensorType.INT16 or TensorType.INT8 or TensorType.UINT64 or TensorType.UINT32 or TensorType.UINT16 or TensorType.INT4 => DataType.Int,
                TensorType.STRING or TensorType.COMPLEX64 or TensorType.COMPLEX128 or TensorType.RESOURCE or TensorType.VARIANT => throw new LiteRTImportException($"Tensor type {tensorType} is not supported"),
                _ => throw new LiteRTImportException($"Tensor type {tensorType} is not supported")
            };
        }

        /// <summary>
        /// Checks if a LiteRT tensor type is supported by Sentis.
        /// </summary>
        public static bool IsDataTypeSupported(this TensorType tensorType)
        {
            try
            {
                _ = tensorType.ToDataType();
                return true;
            }
            catch (LiteRTImportException)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a LiteRT operator is supported by Sentis.
        /// </summary>
        /// <param name="builtinCode">The LiteRT builtin operator code.</param>
        /// <returns>True if the operator is supported, false otherwise.</returns>
        public static bool IsOperatorSupported(this BuiltinOperator builtinCode)
        {
            return builtinCode switch
            {
                // Supported operators (those with implementations in LiteRTModelConverter)
                BuiltinOperator.ADD => true,
                BuiltinOperator.AVERAGE_POOL_2D => true,
                BuiltinOperator.CONCATENATION => true,
                BuiltinOperator.CONV_2D => true,
                BuiltinOperator.DEPTHWISE_CONV_2D => true,
                BuiltinOperator.DEPTH_TO_SPACE => true,
                BuiltinOperator.DEQUANTIZE => true,
                BuiltinOperator.EMBEDDING_LOOKUP => true,
                BuiltinOperator.FLOOR => true,
                BuiltinOperator.FULLY_CONNECTED => true,
                BuiltinOperator.L2_NORMALIZATION => true,
                BuiltinOperator.L2_POOL_2D => true,
                BuiltinOperator.LOCAL_RESPONSE_NORMALIZATION => true,
                BuiltinOperator.LOGISTIC => true,
                BuiltinOperator.MAX_POOL_2D => true,
                BuiltinOperator.MUL => true,
                BuiltinOperator.RELU => true,
                BuiltinOperator.RELU_N1_TO_1 => true,
                BuiltinOperator.RELU6 => true,
                BuiltinOperator.RESHAPE => true,
                BuiltinOperator.RESIZE_BILINEAR => true,
                BuiltinOperator.SOFTMAX => true,
                BuiltinOperator.SPACE_TO_DEPTH => true,
                BuiltinOperator.TANH => true,
                BuiltinOperator.PAD => true,
                BuiltinOperator.GATHER => true,
                BuiltinOperator.TRANSPOSE => true,
                BuiltinOperator.MEAN => true,
                BuiltinOperator.SUB => true,
                BuiltinOperator.DIV => true,
                BuiltinOperator.SQUEEZE => true,
                BuiltinOperator.STRIDED_SLICE => true,
                BuiltinOperator.EXP => true,
                BuiltinOperator.TOPK_V2 => true,
                BuiltinOperator.SPLIT => true,
                BuiltinOperator.LOG_SOFTMAX => true,
                BuiltinOperator.CAST => true,
                BuiltinOperator.PRELU => true,
                BuiltinOperator.MAXIMUM => true,
                BuiltinOperator.MINIMUM => true,
                BuiltinOperator.LESS => true,
                BuiltinOperator.NEG => true,
                BuiltinOperator.PADV2 => true,
                BuiltinOperator.GREATER => true,
                BuiltinOperator.GREATER_EQUAL => true,
                BuiltinOperator.LESS_EQUAL => true,
                BuiltinOperator.SELECT => true,
                BuiltinOperator.SLICE => true,
                BuiltinOperator.SIN => true,
                BuiltinOperator.TRANSPOSE_CONV => true,
                BuiltinOperator.SPARSE_TO_DENSE => true,
                BuiltinOperator.TILE => true,
                BuiltinOperator.EXPAND_DIMS => true,
                BuiltinOperator.EQUAL => true,
                BuiltinOperator.NOT_EQUAL => true,
                BuiltinOperator.LOG => true,
                BuiltinOperator.SUM => true,
                BuiltinOperator.SQRT => true,
                BuiltinOperator.RSQRT => true,
                BuiltinOperator.SHAPE => true,
                BuiltinOperator.POW => true,
                BuiltinOperator.ARG_MIN => true,
                BuiltinOperator.ARG_MAX => true,
                BuiltinOperator.REDUCE_PROD => true,
                BuiltinOperator.REDUCE_MAX => true,
                BuiltinOperator.PACK => true,
                BuiltinOperator.LOGICAL_OR => true,
                BuiltinOperator.ONE_HOT => true,
                BuiltinOperator.LOGICAL_AND => true,
                BuiltinOperator.LOGICAL_NOT => true,
                BuiltinOperator.UNPACK => true,
                BuiltinOperator.REDUCE_MIN => true,
                BuiltinOperator.FLOOR_DIV => true,
                BuiltinOperator.REDUCE_ANY => true,
                BuiltinOperator.SQUARE => true,
                BuiltinOperator.ZEROS_LIKE => true,
                BuiltinOperator.FILL => true,
                BuiltinOperator.FLOOR_MOD => true,
                BuiltinOperator.RANGE => true,
                BuiltinOperator.RESIZE_NEAREST_NEIGHBOR => true,
                BuiltinOperator.LEAKY_RELU => true,
                BuiltinOperator.SQUARED_DIFFERENCE => true,
                BuiltinOperator.MIRROR_PAD => true,
                BuiltinOperator.ABS => true,
                BuiltinOperator.SPLIT_V => true,
                BuiltinOperator.CEIL => true,
                BuiltinOperator.REVERSE_V2 => true,
                BuiltinOperator.ADD_N => true,
                BuiltinOperator.GATHER_ND => true,
                BuiltinOperator.COS => true,
                BuiltinOperator.WHERE => true,
                BuiltinOperator.RANK => true,
                BuiltinOperator.ELU => true,
                BuiltinOperator.ROUND => true,
                BuiltinOperator.HARD_SWISH => true,
                BuiltinOperator.SCATTER_ND => true,
                BuiltinOperator.SELECT_V2 => true,
                BuiltinOperator.BATCH_MATMUL => true,
                BuiltinOperator.CUMSUM => true,
                BuiltinOperator.BROADCAST_TO => true,
                BuiltinOperator.CONV_3D => true,
                BuiltinOperator.REDUCE_ALL => true,
                BuiltinOperator.CONV_3D_TRANSPOSE => true,
                BuiltinOperator.BROADCAST_ARGS => true,
                BuiltinOperator.RANDOM_STANDARD_NORMAL => true,
                BuiltinOperator.RANDOM_UNIFORM => true,
                BuiltinOperator.MULTINOMIAL => true,
                BuiltinOperator.GELU => true,
                BuiltinOperator.RELU_0_TO_1 => true,
                BuiltinOperator.ATAN2 => true,
                BuiltinOperator.SIGN => true,
                BuiltinOperator.BITWISE_XOR => true,
                // All other operators are unsupported
                _ => false
            };
        }

        public static ConstantTensor GetConstant(this Tensor tensor, Buffer buffer)
        {
            if (tensor.Sparsity.HasValue)
                throw new LiteRTImportException("Sentis does not support sparse tensors");
            var shape = new TensorShape(tensor.GetShapeArray());
            var data = shape.length == 0 ? Array.Empty<byte>() : buffer.GetDataArray();

            switch (tensor.Type)
            {
                case TensorType.FLOAT16:
                {
                    return ConstantTensor.FloatFromFloat16(shape, data);
                }
                case TensorType.FLOAT32:
                {
                    return new ConstantTensor(shape, DataType.Float, data);
                }
                case TensorType.FLOAT64:
                {
                    return ConstantTensor.FloatFromFloat64(shape, data);
                }
                case TensorType.BOOL:
                {
                    return ConstantTensor.IntFromBool(shape, data);
                }
                case TensorType.UINT8:
                {
                    return ConstantTensor.IntFromUint8(shape, data);
                }
                case TensorType.UINT16:
                {
                    return ConstantTensor.IntFromUint16(shape, data);
                }
                case TensorType.UINT32:
                {
                    return ConstantTensor.IntFromUint32(shape, data);
                }
                case TensorType.UINT64:
                {
                    return ConstantTensor.IntFromUint64(shape, data);
                }
                case TensorType.INT8:
                {
                    return ConstantTensor.IntFromInt8(shape, data);
                }
                case TensorType.INT16:
                {
                    return ConstantTensor.IntFromInt16(shape, data);
                }
                case TensorType.INT32:
                {
                    return new ConstantTensor(shape, DataType.Int, data);
                }
                case TensorType.INT64:
                {
                    return ConstantTensor.IntFromInt64(shape, data);
                }
                case TensorType.STRING:
                case TensorType.COMPLEX64:
                case TensorType.COMPLEX128:
                case TensorType.RESOURCE:
                case TensorType.VARIANT:
                case TensorType.INT4:
                case TensorType.BFLOAT16:
                default:
                    throw new LiteRTImportException($"Input constant tensor \"{tensor.Name}\": type {tensor.Type} is not supported");
            }
        }
    }
}
