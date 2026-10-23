using System;
using System.Text;

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents a tensor during PartialInference, when input data and shapes are not fully defined.
    /// </summary>
    abstract class PartialTensor
    {
        protected readonly DataType m_DataType;
        protected readonly DynamicTensorShape m_Shape;

        /// <summary>
        /// The data type of the partial tensor as a DataType.
        /// </summary>
        public DataType dataType => m_DataType;

        /// <summary>
        /// The shape of the partial tensor as a DynamicTensorShape.
        /// </summary>
        public DynamicTensorShape shape => m_Shape;

        protected PartialTensor(DataType dataType, DynamicTensorShape shape)
        {
            m_DataType = dataType;
            m_Shape = shape;
        }

        /// <summary>
        /// Whether partial elements are stored.
        /// </summary>
        public abstract bool isPartiallyKnown { get; }

        /// <summary>
        /// The number of partial elements stored, is equal to the length of the tensor if partial elements are stored.
        /// </summary>
        public abstract int length { get; }

        public PartialTensorElement<T> Get<T>(int index = 0) where T : unmanaged
        {
            return (this as PartialTensor<T>)[index];
        }

        public void Set<T>(int index, PartialTensorElement<T> element) where T : unmanaged
        {
            (this as PartialTensor<T>)[index] = element;
        }

        public static PartialTensor Create(DataType dataType) => Create(dataType, DynamicTensorShape.DynamicRank);

        public static PartialTensor Create(DataType dataType, DynamicTensorShape shape)
        {
            return dataType switch
            {
                DataType.Float => new PartialTensor<float>(shape),
                DataType.Int => new PartialTensor<int>(shape),
                DataType.Byte => new PartialTensor<byte>(shape),
                DataType.Short => new PartialTensor<short>(shape),
                _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
            };
        }

        public static PartialTensor FromTensor(Tensor t)
        {
            return t.dataType switch
            {
                DataType.Float => PartialTensor<float>.FromValues(t.shape, (t as Tensor<float>).DownloadToArray()),
                DataType.Int => PartialTensor<int>.FromValues(t.shape, (t as Tensor<int>).DownloadToArray()),
                DataType.Byte => PartialTensor<byte>.FromValues(t.shape, (t as Tensor<byte>).DownloadToArray()),
                DataType.Short => PartialTensor<short>.FromValues(t.shape, (t as Tensor<short>).DownloadToArray()),
                _ => throw new ArgumentOutOfRangeException(nameof(t.dataType), t.dataType, null)
            };
        }

        /// <summary>
        /// Creates and returns an integer partial tensor with a given 'shape' and elements all ones if the length is small enough.
        /// </summary>
        public static PartialTensor<int> Ones(DynamicTensorShape shape)
        {
            var partialTensor = new PartialTensor<int>(shape);
            if (!partialTensor.isPartiallyKnown)
                return partialTensor;
            for (var i = 0; i < partialTensor.length; i++)
            {
                partialTensor[i] = PartialTensorElement<int>.Value(1);
            }

            return partialTensor;
        }

        /// <summary>
        /// Creates and returns an integer partial tensor with a given 'shape' and elements from an range if the length is small enough.
        /// </summary>
        public static PartialTensor<int> Range(int start, int end)
        {
            var partialTensor = new PartialTensor<int>(new DynamicTensorShape(DynamicTensorDim.Int(end - start)));
            if (!partialTensor.isPartiallyKnown)
                return partialTensor;
            for (var i = 0; i < partialTensor.length; i++)
            {
                partialTensor[i] = PartialTensorElement<int>.Value(start + i);
            }

            return partialTensor;
        }

        /// <summary>
        /// Whether two partial tensors are identical.
        /// note this can return false even when the partial tensors are compatible and represent the same tensor.
        /// </summary>
        public static bool IsEquivalent(PartialTensor a, PartialTensor b)
        {
            if (a is null)
                return b is null;
            return a.IsEquivalent(b);
        }

        protected abstract bool IsEquivalent(PartialTensor other);

        public static bool IsEqual(PartialTensor a, PartialTensor b)
        {
            if (a is null)
                return b is null;
            return a.IsEqual(b);
        }

        protected abstract bool IsEqual(PartialTensor other);

        /// <summary>
        /// Whether the partial tensor is fully known, i.e. the shape is known and all the partial elements are known.
        /// If so this partial tensor can be converted to a tensor.
        /// </summary>
        public abstract bool IsStatic();

        /// <summary>
        /// Returns a tensor represented by this partial tensor.
        /// If this partial tensor is not fully known returns 'null'.
        /// </summary>
        public abstract Tensor ToTensor();

        /// <summary>
        /// Returns a copy of a partial tensor.
        /// </summary>
        public abstract PartialTensor Copy();

        /// <summary>
        /// Returns a new partial tensor resulting from reshaping this partial tensor with a given dynamic shape.
        /// A single unknown output dimension can be inferred when the input shape is fully known.
        /// If 'allowZeroLength' is false then this method assumes no dimensions are 0, which allows for more general
        /// inference.
        /// </summary>
        public abstract PartialTensor Reshape(DynamicTensorShape newShape, bool allowZeroLength = true);

        /// <summary>
        /// Returns a partial tensor that is the most defined of two partial tensors known to represent equal tensors.
        /// e.g. if one of the input partial tensors has a fully defined dim or element then this will be used in the
        /// return partial tensor of this method.
        /// </summary>
        public static PartialTensor MaxDefinedPartialTensor(PartialTensor a, PartialTensor b)
        {
            return a is null ? b : a.MaxDefinedPartialTensor(b);
        }

        protected abstract PartialTensor MaxDefinedPartialTensor(PartialTensor other);

        public abstract void CopyElement(int dstIndex, PartialTensor src, int srcIndex);

        public static PartialTensor Activation(PartialTensor input)
        {
            return Create(input.dataType, input.shape);
        }

        public static PartialTensor<T3> Broadcast<T1, T2, T3>(PartialTensor<T1> a, PartialTensor<T2> b, Func<PartialTensorElement<T1>, PartialTensorElement<T2>, PartialTensorElement<T3>> inferPartial) where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        {
            var shapeOut = a.shape.Broadcast(b.shape);
            var tensorOut = new PartialTensor<T3>(shapeOut);

            if (shapeOut.IsStatic() && shapeOut.rank <= 1 && a.isPartiallyKnown && b.isPartiallyKnown)
            {
                for (var i = 0; i < tensorOut.length; i++)
                    tensorOut[i] = inferPartial(a[a.length > 1 ? i : 0], b[b.length > 1 ? i : 0]);
            }

            return tensorOut;
        }

        public static PartialTensor Unary<T1, T2>(PartialTensor<T1> a, Func<PartialTensorElement<T1>, PartialTensorElement<T2>> inferPartial) where T1 : unmanaged where T2 : unmanaged
        {
            var output = new PartialTensor<T2>(a.shape);
            if (output.isPartiallyKnown)
            {
                for (var i = 0; i < output.length; i++)
                    output[i] = inferPartial(a[i]);
            }

            return output;
        }

        public static PartialTensor Reduce(PartialTensor data, PartialTensor axes, bool keepdims, bool noopWithEmptyAxes)
        {
            var dataType = data.dataType;
            var shapeData = data.shape;
            var shapeAxes = axes?.shape ?? new DynamicTensorShape(DynamicTensorDim.Zero);
            if (axes != null && axes.isPartiallyKnown && axes.length != 0)
            {
                var reducedShape = new DynamicTensorShape(shapeData);
                if (!axes.IsStatic() && reducedShape.hasRank)
                {
                    // replace any non 1 dims with unknown (1 stays the same whether reduced or not)
                    for (var i = 0; i < reducedShape.rank; i++)
                    {
                        if (reducedShape[i] == 1)
                            continue;
                        reducedShape[i] = DynamicTensorDim.Unknown;
                    }
                }

                for (var i = 0; i < axes.length; i++)
                {
                    if (!axes.Get<int>(i).isValue)
                        continue;
                    var axis = axes.Get<int>(i).value;
                    reducedShape[axis] = DynamicTensorDim.One;
                }

                var tensorOut = Create(dataType, reducedShape);
                if (!keepdims)
                {
                    tensorOut = tensorOut.Reshape(!axes.IsStatic() ? DynamicTensorShape.DynamicOfRank(tensorOut.shape.rank - axes.length) : tensorOut.shape.Squeeze(axes as PartialTensor<int>));
                }

                return tensorOut;
            }

            if (shapeAxes.IsStatic())
            {
                if (shapeAxes[0].value != 0)
                    return Create(dataType, keepdims ? DynamicTensorShape.DynamicOfRankLike(shapeData) : DynamicTensorShape.DynamicRank);
                if (noopWithEmptyAxes)
                    return Create(dataType, shapeData);
                return Create(dataType, keepdims ? DynamicTensorShape.OnesLike(shapeData) : new DynamicTensorShape());
            }

            return Create(dataType, keepdims && !noopWithEmptyAxes ? DynamicTensorShape.DynamicOfRankLike(shapeData) : DynamicTensorShape.DynamicRank);
        }

        public static PartialTensor ArgReduce(PartialTensor input, int axis, bool keepdims)
        {
            var shapeInput = input.shape;
            if (!shapeInput.hasRank)
                return new PartialTensor<int>();

            var reducedShape = new DynamicTensorShape(shapeInput);

            // reducing on a zero axis will result in a zero rather than a one
            if (shapeInput[axis].isValue)
                reducedShape[axis] = shapeInput[axis].value == 0 ? DynamicTensorDim.Zero : DynamicTensorDim.One;
            else
                reducedShape[axis] = DynamicTensorDim.Unknown;

            var shapeOut = !keepdims ? reducedShape.Squeeze(axis) : reducedShape;
            return new PartialTensor<int>(shapeOut);
        }

        public static PartialTensor LocalPool(PartialTensor input, int[] kernelShape, int[] strides, int[] pads, Layers.AutoPad autopad)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            shapeInput.DeclareRank(2 + kernelShape.Length);

            Logger.AssertIsTrue(strides == null || shapeInput.rank - 2 == strides.Length, "Pool.InputError: strides must have same number of values as spatial dimensions or be null");
            Logger.AssertIsTrue(pads == null || (shapeInput.rank - 2) * 2 == pads.Length, "Pool.InputError: padding must have twice the number of values as spatial dimensions or be null");

            var shapeOut = new DynamicTensorShape(shapeInput);

            for (var i = 2; i < shapeOut.rank; i++)
            {
                var s = strides == null ? 1 : strides[i - 2];
                var p = (pads == null || autopad != Layers.AutoPad.NotSet) ? 0 : (pads[i - 2] + pads[i - 2 + (shapeInput.rank - 2)]);
                shapeOut[i] = shapeInput[i].Pool(kernelShape[i - 2], s, p, 1, false, autopad);
            }

            return Create(dataType, shapeOut);
        }

        public static PartialTensor GlobalPool(PartialTensor input)
        {
            var dataType = input.dataType;
            var shapeInput = input.shape;
            if (!shapeInput.hasRank)
                return Create(dataType);

            Logger.AssertIsTrue(shapeInput.hasRank ? shapeInput.rank >= 3 : true, "RankError: incorrect rank, expecting at least {0}, got {1}", 3, shapeInput.rank);

            var shapeOut = new DynamicTensorShape(shapeInput);

            for (var i = 2; i < shapeOut.rank; i++)
            {
                shapeOut[i] = DynamicTensorDim.One;
            }

            return Create(dataType, shapeOut);
        }

        public abstract PartialTensor Cast<T1>() where T1 : unmanaged;

        /// <summary>
        /// Returns a string that represents the `PartialTensor`.
        /// </summary>
        /// <returns>The string representation of the `PartialTensor`.</returns>
        public override string ToString()
        {
            return ToString(p => "d" + p);
        }

        internal abstract string ToString(Func<byte, string> GetParamName);
    }

    /// <summary>
    /// Represents a PartialTensor with a given type T
    /// </summary>
    class PartialTensor<T> : PartialTensor where T : unmanaged
    {
        const int k_MaxLength = TensorShape.maxRank * 2;
        readonly PartialTensorElement<T>[] m_Elements;

        public override int length => m_Elements.Length;

        public override bool isPartiallyKnown => m_Elements != null;

        /// <summary>
        /// Initializes and returns a partial tensor with the specified `dataType` and 'shape'.
        /// If the shape is small enough unknown partial tensor elements are tracked and 'isPartiallyKnown' returns 'true'.
        /// </summary>
        public PartialTensor(DynamicTensorShape shape)
            : base(AllocatorUtils.ToDataType<T>(), shape)
        {
            if (shape.IsStatic() && shape.Length() <= k_MaxLength)
                m_Elements = new PartialTensorElement<T>[shape.Length().value];
        }

        /// <summary>
        /// Initializes and returns a partial tensor with the specified `dataType` and unknown shape.
        /// </summary>
        public PartialTensor()
            : this(DynamicTensorShape.DynamicRank) { }

        /// <summary>
        /// Initializes and returns a partial tensor from a given 'tensor'. The data type shape and potential partial
        /// tensor elements are inferred from the given 'tensor'.
        /// </summary>
        public static PartialTensor<T> FromValues(TensorShape shape, ReadOnlySpan<T> values)
        {
            var partialTensor = new PartialTensor<T>(new DynamicTensorShape(shape));
            if (!partialTensor.isPartiallyKnown)
                return partialTensor;

            for (var i = 0; i < partialTensor.length; i++)
                partialTensor[i] = PartialTensorElement<T>.Value(values[i]);

            return partialTensor;
        }

        /// <inheritdoc/>
        public override PartialTensor Copy()
        {
            var ret = new PartialTensor<T>(m_Shape);
            if (m_Elements is null)
                return ret;
            for (var i = 0; i < m_Elements.Length; i++)
                ret.m_Elements[i] = m_Elements[i];
            return ret;
        }

        /// <inheritdoc/>
        protected override PartialTensor MaxDefinedPartialTensor(PartialTensor other)
        {
            if (other == null)
                return this;
            if (!isPartiallyKnown && !other.isPartiallyKnown)
                return new PartialTensor<T>(DynamicTensorShape.MaxDefinedShape(shape, other.shape));
            if (!isPartiallyKnown)
                return other;
            if (!other.isPartiallyKnown)
                return this;
            Logger.AssertIsTrue(length == other.length, "InputError: incompatible tensors");
            var maxDefinedPartialTensor = new PartialTensor<T>(shape);
            for (var i = 0; i < maxDefinedPartialTensor.length; i++)
            {
                maxDefinedPartialTensor[i] = PartialTensorElement<T>.MaxDefinedElement(this[i], (other as PartialTensor<T>)[i]);
            }

            return maxDefinedPartialTensor;
        }

        /// <inheritdoc/>
        public override PartialTensor Reshape(DynamicTensorShape newShape, bool allowZeroLength = true)
        {
            if (!newShape.hasRank)
                return new PartialTensor<T>(newShape);

            if (!newShape.IsStatic())
            {
                DynamicTensorShape.ReduceCommonFactors(shape, newShape, out var reducedPrev, out var reducedNew, !allowZeroLength);
                var reducedPrevLength = reducedPrev.Length();

                if (!reducedPrevLength.isUnknown)
                {
                    var reducedPrevLengthNonZero = DynamicTensorDim.One;
                    for (var i = 0; i < reducedPrev.rank; i++)
                    {
                        if (!(reducedPrev[i] == DynamicTensorDim.Zero))
                            reducedPrevLengthNonZero *= reducedPrev[i];
                    }

                    var isZero = false;
                    var cumNonZeroValue = 1;
                    var numUnknowns = 0;
                    var unknownIndex = 0;
                    for (var i = 0; i < reducedNew.rank; i++)
                    {
                        if (reducedNew[i].isValue)
                        {
                            if (reducedNew[i].value == 0)
                                isZero = true;
                            else
                                cumNonZeroValue *= reducedNew[i].value;
                            continue;
                        }

                        numUnknowns++;
                        unknownIndex = i;
                    }

                    if (cumNonZeroValue == 1 && numUnknowns == 1)
                    {
                        if (reducedPrevLength == DynamicTensorDim.Zero && !isZero)
                            newShape[unknownIndex] = DynamicTensorDim.Zero;
                        else
                            newShape[unknownIndex] = reducedPrevLengthNonZero;
                    }
                }
            }

            var reshapedPartialTensor = new PartialTensor<T>(newShape);
            if (!reshapedPartialTensor.isPartiallyKnown || !isPartiallyKnown)
                return reshapedPartialTensor;

            for (var i = 0; i < reshapedPartialTensor.length; i++)
                reshapedPartialTensor.m_Elements[i] = m_Elements[i];

            return reshapedPartialTensor;
        }

        public override PartialTensor Cast<T1>()
        {
            var castPartialTensor = new PartialTensor<T1>(shape);
            if (!isPartiallyKnown)
                return castPartialTensor;

            for (var i = 0; i < length; i++)
                castPartialTensor.Set(i, m_Elements[i].Cast<T1>());

            return castPartialTensor;
        }

        /// <inheritdoc/>
        public override bool IsStatic()
        {
            if (!isPartiallyKnown)
                return false;

            for (var i = 0; i < m_Elements.Length; i++)
            {
                if (!m_Elements[i].isValue)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Gets or sets the element of a given index from the flattened array of partial tensor elements.
        /// </summary>
        public PartialTensorElement<T> this[int d0]
        {
            get
            {
                if (m_Elements == null)
                    return PartialTensorElement<T>.Unknown;
                Logger.AssertIsTrue(d0 < m_Elements.Length, "InputError: index out of bounds");
                return m_Elements[d0];
            }
            set
            {
                if (m_Elements != null)
                    m_Elements[d0] = value;
            }
        }

        public PartialTensorElement<T> this[PartialTensorElement<int> d0] => !d0.isValue ? PartialTensorElement<T>.Unknown : this[d0.value];

        /// <inheritdoc/>
        public override Tensor ToTensor()
        {
            return new Tensor<T>(shape.ToTensorShape(), ToArray());
        }

        /// <summary>
        /// Returns a array represented by this partial tensor.
        /// If this partial tensor is not fully known returns 'null'.
        /// </summary>
        public T[] ToArray()
        {
            if (!IsStatic())
                return null;

            var values = new T[length];
            for (var i = 0; i < length; i++)
                values[i] = (T)Convert.ChangeType(m_Elements[i].value, typeof(T));
            return values;
        }

        /// <inheritdoc/>
        public override void CopyElement(int dstIndex, PartialTensor src, int srcIndex)
        {
            Set(dstIndex, src.Get<T>(srcIndex));
        }

        /// <inheritdoc/>
        protected override bool IsEquivalent(PartialTensor other)
        {
            if (other == null)
                return false;
            if (dataType != other.dataType)
                return false;
            if (!shape.hasRank && !other.shape.hasRank)
                return true;
            if (shape != other.shape)
                return false;
            if (!isPartiallyKnown && !other.isPartiallyKnown)
                return true;
            for (var i = 0; i < length; i++)
            {
                if (!this[i].Equals((other as PartialTensor<T>)[i]))
                    return false;
            }

            return true;
        }

        protected override bool IsEqual(PartialTensor other)
        {
            if (other == null)
                return false;
            if (dataType != other.dataType)
                return false;
            if (!isPartiallyKnown || !other.isPartiallyKnown)
                return false;
            if (length != other.length)
                return false;
            for (var i = 0; i < length; i++)
            {
                if (!(this[i] == (other as PartialTensor<T>)[i]))
                    return false;
            }

            return true;
        }

        internal override string ToString(Func<byte, string> GetParamName)
        {
            var sb = new StringBuilder();
            if (isPartiallyKnown)
            {
                if (m_Shape.rank > 0)
                    sb.Append("[");
                for (var i = 0; i < m_Elements.Length; i++)
                {
                    if (i != 0)
                        sb.Append(", ");
                    var element = this[i];
                    sb.Append(element.ToString(GetParamName));
                }
                if (m_Shape.rank > 0)
                    sb.Append("]");
            }
            else
            {
                sb.Append(dataType.ToString().ToLower());
                sb.Append(m_Shape.ToString(GetParamName));
            }
            return sb.ToString();
        }

        public override int GetHashCode()
        {
            var hashcode = HashCode.Combine(m_DataType, m_Shape);
            if (m_Elements == null)
                return hashcode;
            for (var i = 0; i < length; i++)
                hashcode = HashCode.Combine(hashcode, m_Elements[i].GetHashCode());
            return hashcode;
        }
    }
}
