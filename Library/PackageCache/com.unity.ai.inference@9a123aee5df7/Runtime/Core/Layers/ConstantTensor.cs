using System;
using System.Runtime.InteropServices;
using Unity.Jobs;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents a constant tensor at compile time.
    /// </summary>
    class ConstantTensor
    {
        public TensorShape shape;
        public DataType dataType;
        public ArraySegment<byte> array;

        public ConstantTensor(TensorShape shape, DataType dataType, ArraySegment<byte> array)
        {
            this.shape = shape;
            this.dataType = dataType;
            this.array = array;
        }

        public ConstantTensor(Tensor tensor)
        {
            shape = tensor.shape;
            dataType = tensor.dataType;
            var dataArray = (tensor.dataOnBackend as CPUTensorData).array;
            array = dataArray == null ? ArraySegment<byte>.Empty : dataArray.ToArray<byte>(sizeof(float) * dataArray.Length);
        }

        public static ConstantTensor FloatFromFloat16(TensorShape shape, byte[] data)
        {
            Assert.IsTrue(sizeof(ushort) * shape.length == data.Length);
            var dstData = new byte[sizeof(float) * shape.length];

            if (data.Length > 0)
            {
                unsafe
                {
                    fixed (void* dataPtr = &data[0], dstPtr = &dstData[0])
                    {
                        var job = new BurstJobsCastTensor.Float16BytesAsFloatJob
                        {
                            src = (ushort*)dataPtr,
                            dst = (float*)dstPtr
                        };
                        var jobHandle = job.Schedule(shape.length, 32);
                        jobHandle.Complete();
                    }
                }
            }

            return new ConstantTensor(shape, DataType.Float, dstData);
        }

        public static ConstantTensor FloatFromFloat64(TensorShape shape, byte[] data)
        {
            Assert.IsTrue(sizeof(long) * shape.length == data.Length);
            var dstData = new byte[sizeof(float) * shape.length];

            if (data.Length > 0)
            {
                unsafe
                {
                    fixed (void* dataPtr = &data[0], dstPtr = &dstData[0])
                    {
                        var job = new BurstJobsCastTensor.Float64BytesAsFloatJob
                        {
                            src = (long*)dataPtr,
                            dst = (float*)dstPtr
                        };
                        var jobHandle = job.Schedule(shape.length, 32);
                        jobHandle.Complete();
                    }
                }
            }

            return new ConstantTensor(shape, DataType.Float, dstData);
        }

        public static ConstantTensor IntFromInt64(TensorShape shape, byte[] data)
        {
            Assert.IsTrue(sizeof(long) * shape.length == data.Length);
            var dstData = new byte[sizeof(int) * shape.length];

            if (data.Length > 0)
            {
                unsafe
                {
                    fixed (void* dataPtr = &data[0], dstPtr = &dstData[0])
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
            }

            return new ConstantTensor(shape, DataType.Int, dstData);
        }

        public static ConstantTensor IntFromUint64(TensorShape shape, byte[] data)
        {
            Assert.IsTrue(sizeof(ulong) * shape.length == data.Length);
            var dstData = new byte[sizeof(int) * shape.length];

            if (data.Length > 0)
            {
                unsafe
                {
                    fixed (void* dataPtr = &data[0], dstPtr = &dstData[0])
                    {
                        var job = new BurstJobsCastTensor.Uint64BytesAsIntJob
                        {
                            src = (ulong*)dataPtr,
                            dst = (int*)dstPtr
                        };
                        var jobHandle = job.Schedule(shape.length, 32);
                        jobHandle.Complete();
                    }
                }
            }

            return new ConstantTensor(shape, DataType.Int, dstData);
        }

        public static ConstantTensor IntFromUint32(TensorShape shape, byte[] data)
        {
            Assert.IsTrue(sizeof(uint) * shape.length == data.Length);
            var dstData = new byte[sizeof(int) * shape.length];

            if (data.Length > 0)
            {
                unsafe
                {
                    fixed (void* dataPtr = &data[0], dstPtr = &dstData[0])
                    {
                        var job = new BurstJobsCastTensor.Uint32BytesAsIntJob
                        {
                            src = (uint*)dataPtr,
                            dst = (int*)dstPtr
                        };
                        var jobHandle = job.Schedule(shape.length, 32);
                        jobHandle.Complete();
                    }
                }
            }

            return new ConstantTensor(shape, DataType.Int, dstData);
        }

        public static ConstantTensor IntFromBool(TensorShape shape, byte[] data)
        {
            Assert.IsTrue(sizeof(bool) * shape.length == data.Length);
            var dstData = new byte[sizeof(int) * shape.length];

            if (data.Length > 0)
            {
                unsafe
                {
                    fixed (void* dataPtr = &data[0], dstPtr = &dstData[0])
                    {
                        var job = new BurstJobsCastTensor.BoolBytesAsIntJob
                        {
                            src = (bool*)dataPtr,
                            dst = (int*)dstPtr
                        };
                        var jobHandle = job.Schedule(shape.length, 32);
                        jobHandle.Complete();
                    }
                }
            }

            return new ConstantTensor(shape, DataType.Int, dstData);
        }

        public static ConstantTensor IntFromUint16(TensorShape shape, byte[] data)
        {
            Assert.IsTrue(sizeof(ushort) * shape.length == data.Length);
            var dstData = new byte[sizeof(int) * shape.length];

            if (data.Length > 0)
            {
                unsafe
                {
                    fixed (void* dataPtr = &data[0], dstPtr = &dstData[0])
                    {
                        var job = new BurstJobsCastTensor.Uint16BytesAsIntJob
                        {
                            src = (ushort*)dataPtr,
                            dst = (int*)dstPtr
                        };
                        var jobHandle = job.Schedule(shape.length, 32);
                        jobHandle.Complete();
                    }
                }
            }

            return new ConstantTensor(shape, DataType.Int, dstData);
        }

        public static ConstantTensor IntFromInt16(TensorShape shape, byte[] data)
        {
            Assert.IsTrue(sizeof(short) * shape.length == data.Length);
            var dstData = new byte[sizeof(int) * shape.length];

            if (data.Length > 0)
            {
                unsafe
                {
                    fixed (void* dataPtr = &data[0], dstPtr = &dstData[0])
                    {
                        var job = new BurstJobsCastTensor.Int16BytesAsIntJob
                        {
                            src = (short*)dataPtr,
                            dst = (int*)dstPtr
                        };
                        var jobHandle = job.Schedule(shape.length, 32);
                        jobHandle.Complete();
                    }
                }
            }

            return new ConstantTensor(shape, DataType.Int, dstData);
        }

        public static ConstantTensor IntFromUint8(TensorShape shape, byte[] data)
        {
            Assert.IsTrue(sizeof(byte) * shape.length == data.Length);
            var dstData = new byte[sizeof(int) * shape.length];

            if (data.Length > 0)
            {
                unsafe
                {
                    fixed (void* dataPtr = &data[0], dstPtr = &dstData[0])
                    {
                        var job = new BurstJobsCastTensor.Uint8BytesAsIntJob
                        {
                            src = (byte*)dataPtr,
                            dst = (int*)dstPtr
                        };
                        var jobHandle = job.Schedule(shape.length, 32);
                        jobHandle.Complete();
                    }
                }
            }

            return new ConstantTensor(shape, DataType.Int, dstData);
        }

        public static ConstantTensor IntFromInt8(TensorShape shape, byte[] data)
        {
            Assert.IsTrue(sizeof(sbyte) * shape.length == data.Length);
            var dstData = new byte[sizeof(int) * shape.length];

            if (data.Length > 0)
            {
                unsafe
                {
                    fixed (void* dataPtr = &data[0], dstPtr = &dstData[0])
                    {
                        var job = new BurstJobsCastTensor.Int8BytesAsIntJob
                        {
                            src = (sbyte*)dataPtr,
                            dst = (int*)dstPtr
                        };
                        var jobHandle = job.Schedule(shape.length, 32);
                        jobHandle.Complete();
                    }
                }
            }

            return new ConstantTensor(shape, DataType.Int, dstData);
        }

        public ConstantTensor(TensorShape shape, float[] value)
        {
            this.shape = shape;
            dataType = DataType.Float;
            array = MemoryMarshal.AsBytes(value.AsSpan()).ToArray();
        }

        public ConstantTensor(TensorShape shape, int[] value)
        {
            this.shape = shape;
            dataType = DataType.Int;
            array = MemoryMarshal.AsBytes(value.AsSpan()).ToArray();
        }

        public PartialTensor GetPartialTensor()
        {
            return dataType switch
            {
                DataType.Float => PartialTensor<float>.FromValues(shape, AsSpan<float>()),
                DataType.Int => PartialTensor<int>.FromValues(shape, AsSpan<int>()),
                DataType.Short => PartialTensor<short>.FromValues(shape, AsSpan<short>()),
                DataType.Byte => PartialTensor<byte>.FromValues(shape, array.AsSpan()),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        internal Span<T> AsSpan<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(array.AsSpan());
        }

        public Tensor ToTensor()
        {
            var tensor = AllocatorUtils.AllocTensor(dataType, shape, null);
            var nativeTensorArray = shape.length == 0 ? null : new NativeTensorArrayFromManagedArray(array, shape.length);
            tensor.dataOnBackend = new CPUTensorData(nativeTensorArray);
            return tensor;
        }
    }
}
