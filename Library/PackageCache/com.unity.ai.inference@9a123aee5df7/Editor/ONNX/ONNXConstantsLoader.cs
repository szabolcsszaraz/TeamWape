using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine.Assertions;
using Unity.Jobs;

namespace Unity.InferenceEngine.Editor.Onnx
{
    static class ONNXConstantsLoader
    {
        public static ConstantTensor LoadConstant(TensorProto tensorProto, FileStream weightStream)
        {
            var shape = GetShape(tensorProto);

            Assert.AreEqual(tensorProto.DataLocation, TensorProto.Types.DataLocation.External);

            var length = (int)weightStream.Length;

            foreach (var externalData in tensorProto.ExternalData)
            {
                if (externalData.Key != "length")
                    continue;
                length = int.Parse(externalData.Value);
            }

            byte[] byteArray = new byte[length];
            weightStream.Read(byteArray, 0, length);

            return GetTensorData(byteArray, length, shape, (TensorProto.Types.DataType)tensorProto.DataType);
        }

        public static ConstantTensor LoadConstant(TensorProto tensorProto, string directoryPath = null)
        {
            if (tensorProto.ExternalData != null)
            {
                foreach (var externalData in tensorProto.ExternalData)
                {
                    if (externalData.Key != "location")
                        continue;
                    var name = externalData.Value;
                    using var weightStream = File.OpenRead(Path.Combine(directoryPath, name));
                    return LoadConstant(tensorProto, weightStream);
                }
            }

            var shape = GetShape(tensorProto);

            if (tensorProto.RawData != null && (shape.length == 0 || tensorProto.RawData.Length != 0))
            {
                return GetTensorData(tensorProto.RawData.ToByteArray(), tensorProto.RawData.Length, shape, (TensorProto.Types.DataType)tensorProto.DataType);
            }
            // Float
            if (tensorProto.DataType == (int)TensorProto.Types.DataType.Float)
            {
                Assert.IsTrue(shape.length == tensorProto.FloatData.Count);
                var arrayData = new float[shape.length];
                tensorProto.FloatData.CopyTo(arrayData, 0);
                return new ConstantTensor(shape, DataType.Float, MemoryMarshal.AsBytes(arrayData.AsSpan()).ToArray());
            }
            // Int32
            if (tensorProto.DataType == (int)TensorProto.Types.DataType.Int32)
            {
                Assert.IsTrue(shape.length == tensorProto.Int32Data.Count);
                var arrayData = new int[shape.length];
                tensorProto.Int32Data.CopyTo(arrayData, 0);
                return new ConstantTensor(shape, DataType.Int, MemoryMarshal.AsBytes(arrayData.AsSpan()).ToArray());
            }
            // Double
            if (tensorProto.DataType == (int)TensorProto.Types.DataType.Double)
            {
                Assert.IsTrue(shape.length == tensorProto.DoubleData.Count);
                var arrayData = new double[shape.length];
                tensorProto.DoubleData.CopyTo(arrayData, 0);
                var floatArrayData = new byte[sizeof(int) * shape.length];
                unsafe
                {
                    fixed (void* dataPtr = &arrayData[0], dstPtr = &floatArrayData[0])
                    {
                        var job = new BurstJobsCastTensor.Float64BytesAsFloatJob()
                        {
                            src = (long*)dataPtr,
                            dst = (float*)dstPtr
                        };
                        var jobHandle = job.Schedule(shape.length, 32);
                        jobHandle.Complete();
                    }
                }
                return new ConstantTensor(shape, DataType.Float, floatArrayData);
            }
            // Int64
            if (tensorProto.DataType == (int)TensorProto.Types.DataType.Int64)
            {
                Assert.IsTrue(shape.length == tensorProto.Int64Data.Count);
                var arrayData = new long[shape.length];
                tensorProto.Int64Data.CopyTo(arrayData, 0);
                var intArrayData = new byte[sizeof(int) * shape.length];
                unsafe
                {
                    fixed (void* dataPtr = &arrayData[0], dstPtr = &intArrayData[0])
                    {
                        var job = new BurstJobsCastTensor.Int64BytesAsIntJob
                        {
                            src = (long*)dataPtr,
                            dst = (int*)dstPtr
                        };
                        var jobHandle = job.Schedule(shape.length, 32);
                        jobHandle.Complete();
                    }
                }
                return new ConstantTensor(shape, DataType.Int, intArrayData);
            }

            throw new OnnxLayerImportException("Could not read tensor data for constant tensor.");
        }

        static TensorShape GetShape(TensorProto tensorProto)
        {
            var onnxShape = new int[tensorProto.Dims.Count];
            for (var i = 0; i < onnxShape.Length; i++)
            {
                var v = tensorProto.Dims[i];
                onnxShape[i] = v < int.MinValue ? int.MinValue : v > int.MaxValue ? int.MaxValue : (int)v;
            }

            return new TensorShape(onnxShape);
        }

        static DataType GetDataType(TensorProto tensorProto)
        {
            return ONNXNodeWrapper.DataTypeFromOnnxDataType((TensorProto.Types.DataType)tensorProto.DataType);
        }

        static ConstantTensor GetTensorData(byte[] byteArray, int length, TensorShape shape, TensorProto.Types.DataType dataType)
        {
            // Double
            if (dataType == TensorProto.Types.DataType.Double)
            {
                return ConstantTensor.FloatFromFloat64(shape, byteArray);
            }
            // Float32
            if (dataType == TensorProto.Types.DataType.Float)
            {
                Assert.IsTrue(sizeof(float) * shape.length == length);
                return new ConstantTensor(shape, DataType.Float, byteArray);
            }
            // Float16
            if (dataType == TensorProto.Types.DataType.Float16)
            {
                return ConstantTensor.FloatFromFloat16(shape, byteArray);
            }
            // Int32
            if (dataType == TensorProto.Types.DataType.Int32)
            {
                Assert.IsTrue(sizeof(int) * shape.length == length);
                return new ConstantTensor(shape, DataType.Int, byteArray);
            }
            // Int64
            if (dataType == TensorProto.Types.DataType.Int64)
            {
                return ConstantTensor.IntFromInt64(shape, byteArray);
            }
            // Bool
            if (dataType == TensorProto.Types.DataType.Bool)
            {
                return ConstantTensor.IntFromBool(shape, byteArray);
            }
            // Uint8
            if (dataType == TensorProto.Types.DataType.Uint8)
            {
                return ConstantTensor.IntFromUint8(shape, byteArray);
            }
            // Int8
            if (dataType == TensorProto.Types.DataType.Int8)
            {
                return ConstantTensor.IntFromInt8(shape, byteArray);
            }

            throw new OnnxLayerImportException($"Tensor data type {dataType} is not supported.");
        }
    }
}
