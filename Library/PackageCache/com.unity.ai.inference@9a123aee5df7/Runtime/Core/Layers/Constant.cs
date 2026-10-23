using System;
using System.Runtime.InteropServices;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents a constant in a model.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    public class Constant
    {
        /// <summary>
        /// The index of the constant.
        /// </summary>
        public int index;

        /// <summary>
        /// The shape of the constant as a `TensorShape`.
        /// </summary>
        public TensorShape shape;

        /// <summary>
        /// The size of the constant in bytes.
        /// </summary>
        public int lengthBytes;

        /// <summary>
        /// The data type of the constant as a `DataType`.
        /// </summary>
        public DataType dataType;

        internal ArraySegment<byte> array;

        internal Constant(int index, ConstantTensor constantTensor)
        {
            this.index = index;
            shape = constantTensor.shape;
            lengthBytes = constantTensor.array.Count;
            dataType = constantTensor.dataType;
            array = constantTensor.array;
        }

        internal Constant(int index, TensorShape shape, int lengthBytes, DataType dataType)
        {
            this.index = index;
            this.shape = shape;
            this.lengthBytes = lengthBytes;
            this.dataType = dataType;
        }

        /// <summary>
        /// Returns a string that represents the `Constant`.
        /// </summary>
        /// <returns>A string representation of the `Constant`.</returns>
        public override string ToString()
        {
            return $"Constant{dataType.ToString()} - index: {index}, shape: {shape}, dataType: {dataType}";
        }

        internal PartialTensor GetPartialTensor()
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

        // Below are legacy methods needed to not break API

        /// <summary>
        /// The elements of the constant as a `NativeTensorArrayFromManagedArray`.
        /// </summary>
        public NativeTensorArray weights
        {
            [Obsolete("Getting constant weights is deprecated.", false)]
            get => shape.length == 0 ? null : new NativeTensorArrayFromManagedArray(array, shape.length);
            [Obsolete("Setting constant weights is deprecated.", false)]
            set => throw new NotSupportedException("Cannot assign weights to constant.");
        }

        /// <summary>
        /// Initializes and returns a vector `Constant` from a given index, shape and float array.
        /// </summary>
        /// <param name="index">The index to use for the constant.</param>
        /// <param name="shape">The shape to use for the constant.</param>
        /// <param name="value">The float array of values.</param>
        public Constant(int index, TensorShape shape, float[] value)
            : this(index, new ConstantTensor(shape, value)) { }
    }
}
