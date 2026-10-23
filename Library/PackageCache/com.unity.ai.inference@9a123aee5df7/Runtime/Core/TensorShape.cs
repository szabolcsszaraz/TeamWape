using UnityEngine.Assertions;
using System;
using System.Text;
using UnityEngine;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.InferenceEngine.RuntimeTests")]
[assembly: InternalsVisibleTo("Unity.InferenceEngine.EditorTests")]
[assembly: InternalsVisibleTo("Unity.InferenceEngine.CPUBackend")]

namespace Unity.InferenceEngine
{
    /// <summary>
    /// Represents the shape of a tensor.
    /// </summary>
    /// <remarks>
    /// A `TensorShape` describes the shape of a <see cref="Tensor"/> or <see cref="FunctionalTensor"/>.
    ///
    /// `TensorShape` supports rank up to <see cref="maxRank"/> (`8`).
    ///
    /// Create a `TensorShape` using one of the constructors, or by
    /// passing an array of integers.
    ///
    /// `TensorShape` provides methods for common shape transformations such as <see cref="Squeeze()"/> and <see cref="Squeeze(int)"/>,
    /// <see cref="Unsqueeze"/>, <see cref="Flatten()"/>, <see cref="Broadcast"/>, and <see cref="Reduce"/>. These methods return new
    /// `TensorShapes` without modifying the original.
    ///
    /// The <see cref="rank"/> property returns the number of dimensions, and the <see cref="length"/>
    /// property returns the total number of elements (the product of all dimensions).
    ///
    /// **Additional resources:**
    ///
    /// - <see cref="Tensor"/>
    /// - <see cref="FunctionalTensor"/>
    /// - <see cref="DynamicTensorShape"/>
    /// </remarks>
    /// <example>
    /// <para>Create `TensorShapes` and get their properties</para>
    /// <code lang="cs"><![CDATA[
    /// using Unity.InferenceEngine;
    ///
    /// // Create TensorShape
    /// var shape = new TensorShape(3, 4);       // Shape: (3, 4), rank: 2
    ///
    /// // Create shape from an array
    /// var shape2 = new TensorShape(new[] { 2, 3, 4, 5 });
    /// // Shape: (2, 3, 4, 5), rank: 4
    ///
    /// // Shape properties
    /// shape.rank;    // Returns 2
    /// shape.length;  // Returns 12 (3 * 4)
    /// shape[0];      // Returns 3
    /// shape[-1];     // Returns 4 (last dimension)
    /// ]]></code>
    /// </example>
    [UnityEngine.Scripting.APIUpdating.MovedFrom("Unity.Sentis")]
    [Serializable]
    public unsafe struct TensorShape
    {
        /// <summary>
        /// The maximum rank (number of dimensions) a `TensorShape` can have.
        /// </summary>
        /// <remarks>
        /// This constant defines the upper limit for `rank` in Unity Inference Engine.
        /// </remarks>
        public const int maxRank = 8;

#pragma warning disable CS0649

        // disable warning: Field 'field' is never assigned to, and will always have its default value 'value'
        // fields are accessed using unsafe code (ptr)
        // TensorShapes are 0 padded from right to left to match TensorDim slices
        // Ex:
        // (5)       -> 0,0,0,5
        // (3,5)     -> 0,0,3,5
        // (7,3,5)   -> 0,7,3,5
        // (6,7,3,5) -> 6,7,3,5
        // ...
        int m_D7;
        int m_D6;
        int m_D5;
        int m_D4;
        int m_D3;
        int m_D2;
        int m_D1;
        int m_D0;
#pragma warning restore CS0649

        int m_Rank;

        /// <summary>
        /// Gets the number of dimensions in the `TensorShape`.
        /// </summary>
        /// <remarks>
        /// The rank indicates how many dimensions the shape has. For example:
        /// - A scalar has rank `0`
        /// - A 1D tensor (vector) has rank `1`
        /// - A 2D tensor (matrix) has rank `2`
        /// - A 3D tensor has rank `3`, and so on
        ///
        /// The maximum rank is <see cref="maxRank"/>, which is `8`.
        /// </remarks>
        /// <example>
        /// <para>Get the rank of a `TensorShape`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(4, 3, 2, 4);
        /// shape.rank;  // Returns 4
        /// ]]></code>
        /// </example>
        public int rank => m_Rank;

        int m_Length;

        /// <summary>
        /// Gets the total number of elements in a tensor with this shape.
        /// </summary>
        /// <remarks>
        /// The length is the product of all dimensions.
        /// A scalar tensor (rank `0`) has a length of `1`. A shape with any dimension `0`
        /// has a length of `0`.
        /// </remarks>
        /// <example>
        /// <para>Get the total number of elements in a `TensorShape`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(4, 3, 2);
        /// shape.length;  // Returns 24
        /// ]]></code>
        /// </example>
        public int length => rank == 0 ? 1 : m_Length;

        /// <summary>
        /// Initializes and returns an instance of `TensorShape` with a rank of `8`.
        /// </summary>
        /// <param name="d0">Length of axis `0`.</param>
        /// <param name="d1">Length of axis `1`.</param>
        /// <param name="d2">Length of axis `2`.</param>
        /// <param name="d3">Length of axis `3`.</param>
        /// <param name="d4">Length of axis `4`.</param>
        /// <param name="d5">Length of axis `5`.</param>
        /// <param name="d6">Length of axis `6`.</param>
        /// <param name="d7">Length of axis `7`.</param>
        /// <example>
        /// <para>Create a `TensorShape` of rank `8`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(2, 3, 4, 5, 6, 7, 8, 9);
        /// // Result: shape of rank 8
        /// ]]></code>
        /// </example>
        public TensorShape(int d0, int d1, int d2, int d3, int d4, int d5, int d6, int d7)
        {
            m_D7 = d0 >= 0 ? d0 : 0;
            m_D6 = d1 >= 0 ? d1 : 0;
            m_D5 = d2 >= 0 ? d2 : 0;
            m_D4 = d3 >= 0 ? d3 : 0;
            m_D3 = d4 >= 0 ? d4 : 0;
            m_D2 = d5 >= 0 ? d5 : 0;
            m_D1 = d6 >= 0 ? d6 : 0;
            m_D0 = d7 >= 0 ? d7 : 0;

            m_Rank = 8;
            m_Length = d7 * d6 * d5 * d4 * d3 * d2 * d1 * d0;
        }

        /// <summary>
        /// Initializes and returns an instance of `TensorShape` with a rank of `7`.
        /// </summary>
        /// <param name="d0">Length of axis `0`.</param>
        /// <param name="d1">Length of axis `1`.</param>
        /// <param name="d2">Length of axis `2`.</param>
        /// <param name="d3">Length of axis `3`.</param>
        /// <param name="d4">Length of axis `4`.</param>
        /// <param name="d5">Length of axis `5`.</param>
        /// <param name="d6">Length of axis `6`.</param>
        /// <example>
        /// <para>Create a `TensorShape` of rank `7`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(3, 3, 4, 4, 5, 5, 6);
        /// // Result: shape of rank 7
        /// ]]></code>
        /// </example>
        public TensorShape(int d0, int d1, int d2, int d3, int d4, int d5, int d6)
        {
            m_D7 = 0;
            m_D6 = d0 >= 0 ? d0 : 0;
            m_D5 = d1 >= 0 ? d1 : 0;
            m_D4 = d2 >= 0 ? d2 : 0;
            m_D3 = d3 >= 0 ? d3 : 0;
            m_D2 = d4 >= 0 ? d4 : 0;
            m_D1 = d5 >= 0 ? d5 : 0;
            m_D0 = d6 >= 0 ? d6 : 0;

            m_Rank = 7;
            m_Length = d6 * d5 * d4 * d3 * d2 * d1 * d0;
        }

        /// <summary>
        /// Initializes and returns an instance of `TensorShape` with a rank of `6`.
        /// </summary>
        /// <param name="d0">Length of axis `0`.</param>
        /// <param name="d1">Length of axis `1`.</param>
        /// <param name="d2">Length of axis `2`.</param>
        /// <param name="d3">Length of axis `3`.</param>
        /// <param name="d4">Length of axis `4`.</param>
        /// <param name="d5">Length of axis `5`.</param>
        /// <example>
        /// <para>Create a `TensorShape` of rank `6`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(4, 5, 6, 7, 8, 9);
        /// // Result: shape of rank 6
        /// ]]></code>
        /// </example>
        public TensorShape(int d0, int d1, int d2, int d3, int d4, int d5)
        {
            m_D7 = 0;
            m_D6 = 0;
            m_D5 = d0 >= 0 ? d0 : 0;
            m_D4 = d1 >= 0 ? d1 : 0;
            m_D3 = d2 >= 0 ? d2 : 0;
            m_D2 = d3 >= 0 ? d3 : 0;
            m_D1 = d4 >= 0 ? d4 : 0;
            m_D0 = d5 >= 0 ? d5 : 0;

            m_Rank = 6;
            m_Length = d5 * d4 * d3 * d2 * d1 * d0;
        }

        /// <summary>
        /// Initializes and returns an instance of `TensorShape` with a rank of `5`.
        /// </summary>
        /// <param name="d0">Length of axis `0`.</param>
        /// <param name="d1">Length of axis `1`.</param>
        /// <param name="d2">Length of axis `2`.</param>
        /// <param name="d3">Length of axis `3`.</param>
        /// <param name="d4">Length of axis `4`.</param>
        /// <example>
        /// <para>Create a `TensorShape` of rank `5`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(5, 6, 7, 8, 9);
        /// // Result: shape of rank 5
        /// ]]></code>
        /// </example>
        public TensorShape(int d0, int d1, int d2, int d3, int d4)
        {
            m_D7 = 0;
            m_D6 = 0;
            m_D5 = 0;
            m_D4 = d0 >= 0 ? d0 : 0;
            m_D3 = d1 >= 0 ? d1 : 0;
            m_D2 = d2 >= 0 ? d2 : 0;
            m_D1 = d3 >= 0 ? d3 : 0;
            m_D0 = d4 >= 0 ? d4 : 0;

            m_Rank = 5;
            m_Length = d4 * d3 * d2 * d1 * d0;
        }

        /// <summary>
        /// Initializes and returns an instance of `TensorShape` with a rank of `4`.
        /// </summary>
        /// <param name="d0">Length of axis `0`.</param>
        /// <param name="d1">Length of axis `1`.</param>
        /// <param name="d2">Length of axis `2`.</param>
        /// <param name="d3">Length of axis `3`.</param>
        /// <example>
        /// <para>Create a `TensorShape` of rank `4`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(6, 7, 8, 9);
        /// // Result: shape of rank 4
        /// ]]></code>
        /// </example>
        public TensorShape(int d0, int d1, int d2, int d3)
        {
            m_D7 = 0;
            m_D6 = 0;
            m_D5 = 0;
            m_D4 = 0;
            m_D3 = d0 >= 0 ? d0 : 0;
            m_D2 = d1 >= 0 ? d1 : 0;
            m_D1 = d2 >= 0 ? d2 : 0;
            m_D0 = d3 >= 0 ? d3 : 0;

            m_Rank = 4;
            m_Length = d3 * d2 * d1 * d0;
        }

        /// <summary>
        /// Initializes and returns an instance of `TensorShape` with a rank of `3`.
        /// </summary>
        /// <param name="d0">Length of axis `0`.</param>
        /// <param name="d1">Length of axis `1`.</param>
        /// <param name="d2">Length of axis `2`.</param>
        /// <example>
        /// <para>Create a `TensorShape` of rank `3`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(2, 3, 4);
        /// // Result: shape of rank 3
        /// ]]></code>
        /// </example>
        public TensorShape(int d0, int d1, int d2)
        {
            m_D7 = 0;
            m_D6 = 0;
            m_D5 = 0;
            m_D4 = 0;
            m_D3 = 0;
            m_D2 = d0 >= 0 ? d0 : 0;
            m_D1 = d1 >= 0 ? d1 : 0;
            m_D0 = d2 >= 0 ? d2 : 0;

            m_Rank = 3;
            m_Length = d2 * d1 * d0;
        }

        /// <summary>
        /// Initializes and returns an instance of `TensorShape` with a rank of `2` (matrix).
        /// </summary>
        /// <param name="d0">Length of axis `0`.</param>
        /// <param name="d1">Length of axis `1`.</param>
        /// <example>
        /// <para>Create a `TensorShape` of rank `2` (matrix)</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(8, 2);
        /// // Result: shape of rank 2
        /// ]]></code>
        /// </example>
        public TensorShape(int d0, int d1)
        {
            m_D7 = 0;
            m_D6 = 0;
            m_D5 = 0;
            m_D4 = 0;
            m_D3 = 0;
            m_D2 = 0;
            m_D1 = d0 >= 0 ? d0 : 0;
            m_D0 = d1 >= 0 ? d1 : 0;

            m_Rank = 2;
            m_Length = d1 * d0;
        }

        /// <summary>
        /// Initializes and returns an instance of `TensorShape` with a rank of `1` (vector).
        /// </summary>
        /// <param name="d0">Length of axis `0`.</param>
        /// <example>
        /// <para>Create a `TensorShape` of rank `1` (vector)</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(42);
        /// // Result: shape of rank 1
        /// ]]></code>
        /// </example>
        public TensorShape(int d0)
        {
            m_D7 = 0;
            m_D6 = 0;
            m_D5 = 0;
            m_D4 = 0;
            m_D3 = 0;
            m_D2 = 0;
            m_D1 = 0;
            m_D0 = d0 >= 0 ? d0 : 0;

            m_Rank = 1;
            m_Length = d0;
        }

        /// <summary>
        /// Initializes a `TensorShape` from a span of integers.
        /// </summary>
        /// <remarks>
        /// This constructor creates a `TensorShape` from a span of integers, where each
        /// element specifies a dimension.
        /// The maximum rank is <see cref="maxRank"/> (`8` dimensions).
        /// </remarks>
        /// <param name="shape">A span of integers representing each dimension.</param>
        /// <example>
        /// <para>Create a `TensorShape` from an array of integers</para>
        /// <code lang="cs"><![CDATA[
        /// // Create shape from an array
        /// var shape = new TensorShape(new[] { 3, 4, 5, 6 });
        /// // Result: (3, 4, 5, 6)
        /// ]]></code>
        /// </example>
        public TensorShape(ReadOnlySpan<int> shape)
            : this()
        {
            Logger.AssertIsTrue(shape.Length <= maxRank, "ValueError: TensorShape are capped to rank=8, cannot create TensorShape of rank {0}", shape.Length);

            m_Rank = shape.Length;

            if (shape.Length == 0)
                return;

            fixed (int* dst = &m_D7, src = &shape[0])
            {
                Buffer.MemoryCopy(src, dst + (maxRank - rank), shape.Length * sizeof(int), shape.Length * sizeof(int));
            }

            m_Length = 1;
            for (int i = 0; i < rank; i++)
            {
                m_Length *= shape[i];
            }
        }

        /// <summary>
        /// Returns a copy of another `TensorShape`.
        /// </summary>
        /// <param name="shape">The shape to copy.</param>
        internal TensorShape(TensorShape shape)
        {
            m_Rank = shape.rank;
            m_Length = shape.length;

            m_D7 = shape.m_D7;
            m_D6 = shape.m_D6;
            m_D5 = shape.m_D5;
            m_D4 = shape.m_D4;
            m_D3 = shape.m_D3;
            m_D2 = shape.m_D2;
            m_D1 = shape.m_D1;
            m_D0 = shape.m_D0;
        }

        /// <summary>
        /// Accesses internal shape layout:
        /// shape (3,4,5,6), rank 4,  (internally) = ( 0, 0, 0, 0, 3, 4, 5, 6), rank 8
        ///                                        = (d7,d6,d5,d4,d3,d2,d1,d0)
        /// axis: 0 => d7
        ///       1 => d6
        ///       2 => d5
        ///       3 => d4
        ///       4 => d3
        ///       5 => d2
        ///       6 => d1
        ///       7 => d0
        /// </summary>
        internal int UnsafeGet(int axis)
        {
            fixed (int* shape = &m_D7)
            {
                return shape[axis];
            }
        }

        /// <summary>
        /// Returns rank-sized shape int*
        /// </summary>
        internal unsafe int* UnsafeGetPtr(int axis)
        {
            fixed (int* shape = &m_D7)
            {
                return &shape[axis];
            }
        }

        /// <summary>
        /// Update TensorShape Length
        /// shape (3,4,5,6) => length = 3*4*5*6
        /// </summary>
        void RecomputeLength()
        {
            m_Length = 1;
            fixed (int* srcA = &m_D7)
            {
                for (int i = 0; i < rank; i++)
                {
                    m_Length *= srcA[maxRank - 1 - i];
                }
            }
        }

        /// <summary>
        /// Gets or sets the dimension of the `TensorShape` at a given `axis`.
        /// </summary>
        /// <remarks>
        /// This indexer provides access to individual dimensions. Axes are indexed from `0` to
        /// `rank-1`. Negative indices count backwards from the last dimension.
        /// </remarks>
        /// <param name="axis">The axis to get or set. Must be in the range `[-rank, rank-1]`.</param>
        /// <example>
        /// <para>Access and modify dimensions of a `TensorShape`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(2, 3, 4);
        /// // Access a dimension
        /// shape[1];   // Returns 3
        /// shape[-1];  // Returns 4
        ///
        /// // Modify a dimension
        /// shape[1] = 5;
        /// // shape is now (2, 5, 4)
        /// ]]></code>
        /// </example>
        public int this[int axis]
        {
            get
            {
                axis = Axis(axis);

                fixed (int* shape = &m_D7)
                {
                    return shape[(maxRank - rank) + axis];
                }
            }

            set
            {
                axis = Axis(axis);

                fixed (int* shape = &m_D7)
                {
                    shape[(maxRank - rank) + axis] = value;
                }

                RecomputeLength();
            }
        }

        /// <summary>
        /// Checks whether the shape has any dimension equal to `0`.
        /// </summary>
        /// <remarks>
        /// A shape with any dimension equal to `0` has a <see cref="length"/> of `0`, even if other
        /// dimensions are non-zero.
        /// This method returns `false` for scalar tensors (rank `0`).
        /// </remarks>
        /// <returns>`true` if the shape has rank greater than `0` and at least one dimension is `0`; otherwise, `false`.</returns>
        /// <example>
        /// <para>Check if a `TensorShape` has any dimension equal to `0`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape1 = new TensorShape(3, 4, 5);
        /// shape1.HasZeroDims();  // Returns false
        /// var shape2 = new TensorShape(3, 0, 5);
        /// shape2.HasZeroDims();  // Returns true
        /// ]]></code>
        /// </example>
        public bool HasZeroDims()
        {
            return length == 0 && rank > 0;
        }

        /// <summary>
        /// Returns a string representation of the `TensorShape`.
        /// </summary>
        /// <remarks>
        /// The string format displays all dimensions in parentheses, separated by commas.
        /// </remarks>
        /// <returns>A string showing all dimensions in the format `"(d0, d1, ..., dn)"`.</returns>
        /// <example>
        /// <para>Get a string representation of a `TensorShape`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(2, 3, 4);
        /// shape.ToString();  // Returns "(2, 3, 4)"
        /// ]]></code>
        /// </example>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("(");
            for (int i = 0; i < rank; i++)
            {
                if (i != 0)
                    sb.Append(", ");
                sb.Append(this[i]);
            }

            sb.Append(")");
            return sb.ToString();
        }

        /// <summary>
        /// Returns the number of elements from a given `start` axis to the end.
        /// </summary>
        /// <remarks>
        /// This method calculates the product of dimensions from the `start` axis through
        /// the last axis. Negative `start` values count backwards from the innermost dimension.
        /// If `start` is beyond the shape's rank, returns `1`.
        /// </remarks>
        /// <param name="start">The first axis to count from. Negative values count from the end.</param>
        /// <returns>The product of dimensions from `start` to the last axis.</returns>
        /// <example>
        /// <para>Calculate the number of elements from a given axis to the end</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(2, 3, 4, 5);
        /// shape.Length(1);   // Returns 60 (3 * 4 * 5)
        /// shape.Length(-2);  // Returns 20 (4 * 5)
        /// ]]></code>
        /// </example>
        public int Length(int start)
        {
            if (start >= rank)
                return 1;
            if (start < -rank)
                return length;

            start = Axis(start);
            int l = 1;
            fixed (int* shape = &m_D7)
            {
                for (int i = start; i < rank; i++)
                {
                    l *= shape[(maxRank - rank) + i];
                }
            }

            return l;
        }

        /// <summary>
        /// Returns the number of elements within a range of axes.
        /// </summary>
        /// <remarks>
        /// This method calculates the product of the dimensions from the `start` axis up to,
        /// but not including, the `end` axis. Negative axis values count backwards from the
        /// innermost dimension. If the range is out of bounds or empty, returns `1`.
        /// </remarks>
        /// <param name="start">The first axis to count from (inclusive). Negative values count from the end.</param>
        /// <param name="end">The final axis to count to (exclusive). Negative values count from the end.</param>
        /// <returns>The product of dimensions in the range [start, end).</returns>
        /// <example>
        /// <para>Calculate the number of elements within a range of axes</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(2, 3, 4, 5);
        /// shape.Length(1, 3);   // Returns 12 (3 * 4)
        /// shape.Length(-2, -1); // Returns 4
        /// shape.Length(1, 1);   // Returns 1
        /// ]]></code>
        /// </example>
        public int Length(int start, int end)
        {
            if (start >= rank || end < -rank)
                return 1;
            if (start < -rank)
                start = 0;
            if (end > rank)
                end = rank;

            start = Axis(start);
            if (end < rank)
                end = Axis(end);

            int l = 1;
            fixed (int* shape = &m_D7)
            {
                for (int i = start; i < end; i++)
                {
                    l *= shape[(maxRank - rank) + i];
                }
            }

            return l;
        }

        /// <summary>
        /// Returns the positive axis corresponding to a given axis. Negative axes counts backwards from the inner dimension.
        /// </summary>
        /// <param name="axis">The axis to wrap.</param>
        /// <returns>The positive axis.</returns>
        internal int Axis(int axis)
        {
            Logger.AssertIsTrue(axis >= -rank && axis < rank, "IndexError: axis {0} is out of bounds shape of rank, {1}", axis, rank);
            return axis >= 0 ? axis : rank + axis;
        }

        /// <summary>
        /// Returns the product of the dimensions of the tensor shape after a given axis. Negative axes counts backwards from the inner dimension.
        ///
        /// The strides of a tensor tell us how many elements we have to skip in flattened memory to move to the next position along a given index.
        /// </summary>
        /// <param name="axis">The axis to calculate the stride length at.</param>
        /// <returns>The stride length at the axis.</returns>
        internal int Strides(int axis)
        {
            axis = Axis(axis);

            int trailingLength = 1;
            fixed (int* shape = &m_D7)
            {
                for (int i = (rank - 1); i > axis; i--)
                {
                    trailingLength *= shape[(maxRank - rank) + i];
                }
            }

            return trailingLength;
        }

        /// <summary>
        /// Returns the `TensorShape` as an array of integers.
        /// </summary>
        /// <remarks>
        /// This method creates a new integer array representing the `TensorShape`.
        /// </remarks>
        /// <returns>An integer array where each element is the corresponding dimension.</returns>
        /// <example>
        /// <para>Get a representation of a `TensorShape` as an array of integers</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(2, 3, 4);
        /// int[] array = shape.ToArray();
        /// // array is [2, 3, 4]
        /// ]]></code>
        /// </example>
        public int[] ToArray()
        {
            var shape = new int[rank];
            if (rank > 0)
            {
                fixed (int* dst = &shape[0], src = &m_D7)
                {
                    Buffer.MemoryCopy(src + (maxRank - rank), dst, shape.Length * sizeof(int), shape.Length * sizeof(int));
                }
            }

            return shape;
        }

        /// <summary>
        /// Removes all dimensions equal to `1` from a `TensorShape`.
        /// </summary>
        /// <remarks>
        /// This method returns a new `TensorShape` with all dimensions `1` removed.
        /// The original shape is not modified.
        ///
        /// This method cannot be called on scalar tensors (rank `0`).
        /// </remarks>
        /// <returns>A new `TensorShape` with all dimensions `1` removed.</returns>
        /// <example>
        /// <para>Remove all dimensions equal to `1` from a `TensorShape`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(5, 1, 3, 1);
        /// var squeezed = shape.Squeeze();
        /// // Result: (5, 3)
        /// ]]></code>
        /// </example>
        public TensorShape Squeeze()
        {
            Logger.AssertIsTrue(rank != 0, "ValueError: cannot squeeze scalar tensor {0}", this);

            var squeezed = new TensorShape();
            int* dst = &squeezed.m_D7;

            int squeezedDim = 0;
            fixed (int* src = &m_D7)
            {
                for (int i = (rank - 1); i >= 0; i--)
                {
                    int dim = src[(maxRank - rank) + i];
                    if (dim == 1)
                        continue;

                    dst[(maxRank - 1) - squeezedDim] = dim;
                    squeezedDim++;
                }
            }

            squeezed.m_Rank = squeezedDim;
            squeezed.m_Length = length;

            return squeezed;
        }

        /// <summary>
        /// Removes a specific dimension, equal to `1`, from a `TensorShape`.
        /// </summary>
        /// <remarks>
        /// This method returns a new `TensorShape` with the specified `axis` removed. The `axis` must
        /// have size `1`. Negative `axis` values count backwards from the
        /// last dimension. The original shape is not modified.
        ///
        /// This method cannot be called on scalar tensors (rank `0`).
        /// </remarks>
        /// <param name="axis">The axis to remove. Must be a dimension equal to `1` Negative values count from the end.</param>
        /// <returns>A new `TensorShape` with the specified dimension removed.</returns>
        /// <example>
        /// <para>Remove a dimension equal to `1` from a `TensorShape`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(5, 1, 3, 1);
        ///
        /// var squeezed1 = shape.Squeeze(1);
        /// // Result: (5, 3, 1)
        /// var squeezed2 = shape.Squeeze(-1);
        /// // Result: (5, 1, 3)
        /// ]]></code>
        /// </example>
        public TensorShape Squeeze(int axis)
        {
            Logger.AssertIsTrue(rank != 0, "ValueError: cannot squeeze scalar tensor {0}", this);

            var squeezed = new TensorShape();

            if (rank == 1)
                return squeezed;

            squeezed.m_Rank = rank - 1;

            axis = Axis(axis);

            int* dst = &squeezed.m_D7;

            int squeezedDim = 0;
            fixed (int* src = &m_D7)
            {
                Logger.AssertAreEqual(1, src[(maxRank - rank) + axis], "ValueError: squeezing non unit dimension {0}, {1}", this, axis);
                for (int i = (rank - 1); i >= 0; i--)
                {
                    int dim = src[(maxRank - rank) + i];
                    if (i == axis)
                        continue;

                    dst[(maxRank - 1) - squeezedDim] = dim;

                    squeezedDim++;
                }
            }

            squeezed.m_Length = length;

            return squeezed;
        }

        /// <summary>
        /// Creates a `TensorShape` by duplicating `this` and removing the given axes of size 1. For example, if `this` is (5, 1, 3, 1), and `axes` is {1, -1}, the method returns (5, 3).
        /// </summary>
        /// <param name="axes">The axes to squeeze.</param>
        /// <returns>The squeezed tensor shape.</returns>
        internal TensorShape Squeeze(ReadOnlySpan<int> axes)
        {
            if (axes == null || axes.Length == 0)
                return Squeeze();

            uint axesBitMask = 0;
            for (int i = 0; i < axes.Length; i++)
                axesBitMask |= 1U << Axis(axes[i]);

            Logger.AssertIsTrue(rank - axes.Length >= 0, "ValueError: squeeze axes  {0} larger than current rank {1}. Cannot unsqueeze scalar tensor", rank, axes.Length);

            var squeezed = new TensorShape();
            squeezed.m_Rank = Math.Max(rank - axes.Length, 0);

            int* dst = &squeezed.m_D7;

            int squeezedDim = 0;
            fixed (int* src = &m_D7)
            {
                for (int i = (rank - 1); i >= 0; i--)
                {
                    uint baxis = (axesBitMask >> i) & 1U;
                    if (baxis == 1)
                    {
                        Logger.AssertAreEqual(1, src[(maxRank - rank) + i], "ValueError: squeezing non unit dimension {0}, {1}", this, i);
                        continue;
                    }

                    dst[(maxRank - 1) - squeezedDim] = src[(maxRank - rank) + i];
                    squeezedDim++;
                }
            }

            squeezed.m_Length = length;

            return squeezed;
        }

        /// <summary>
        /// Inserts a dimension `1` in a `TensorShape` at a specified `axis`.
        /// </summary>
        /// <remarks>
        /// This method returns a new `TensorShape` with a dimension `1` inserted at the
        /// specified `axis`.
        /// Negative `axis` values are interpreted in the context of the new, expanded rank.
        ///
        /// The maximum rank is `8`, so this method cannot be called on a tensor of rank `8`.
        /// </remarks>
        /// <param name="axis">The position to insert the new dimension. Negative values count from the end in the new rank.</param>
        /// <returns>A new `TensorShape` with an additional dimension `1`.</returns>
        /// <example>
        /// <para>Insert a dimension of `1` at a specified axis</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(3, 4);
        /// var unsqueezed0 = shape.Unsqueeze(0);
        /// // Result: (1, 3, 4)
        /// var unsqueezed1 = shape.Unsqueeze(-1);
        /// // Result: (3, 4, 1)
        /// ]]></code>
        /// </example>
        public TensorShape Unsqueeze(int axis)
        {
            if (rank == 0)
                return new TensorShape(1);

            Logger.AssertIsTrue(rank != maxRank, "ValueError: TensorShape are capped to rank=8, cannot unsqueeze rank 8 TensorShape {0}", this);

            int unsqueezedRank = rank + 1;
            var unsqueezed = new TensorShape();
            unsqueezed.m_Rank = unsqueezedRank;
            unsqueezed.m_Length = length;

            axis = unsqueezed.Axis(axis);

            int* dst = &unsqueezed.m_D7;

            dst[(maxRank - unsqueezedRank) + axis] = 1;

            int shiftDim = 0;
            fixed (int* src = &m_D7)
            {
                for (int i = 0; i < unsqueezedRank; i++)
                {
                    if (i == axis)
                    {
                        shiftDim = 1;
                        continue;
                    }

                    dst[(maxRank - unsqueezedRank) + i] = src[(maxRank - rank) + i - shiftDim];
                }
            }

            return unsqueezed;
        }

        /// <summary>
        /// Creates a `TensorShape` by duplicating `this` and inserting a dimension of size one at a given axis. For example if `this` is (2), and the value of `axis` is 0, the method returns (1, 2).
        /// </summary>
        /// <param name="axes">The axes at which to unsqueeze.</param>
        /// <returns>The unsqueezed tensor shape.</returns>
        internal TensorShape Unsqueeze(ReadOnlySpan<int> axes)
        {
            Logger.AssertIsTrue(rank + axes.Length <= maxRank, "ValueError: TensorShape are capped to rank=8, cannot unsqueeze TensorShape {0} to rank greater than 8", this);

            int unsqueezedRank = rank + axes.Length;
            var unsqueezed = new TensorShape();
            unsqueezed.m_Rank = unsqueezedRank;
            unsqueezed.m_Length = length;

            uint axesBitMask = 0;
            for (int i = 0; i < axes.Length; i++)
                axesBitMask |= 1U << unsqueezed.Axis(axes[i]);

            int* dst = &unsqueezed.m_D7;

            int shiftDim = 0;
            fixed (int* src = &m_D7)
            {
                for (int i = (unsqueezedRank - 1); i >= 0; i--)
                {
                    uint baxis = (axesBitMask >> i) & 1U;
                    if (baxis == 1)
                    {
                        dst[(maxRank - unsqueezedRank) + i] = 1;
                        continue;
                    }

                    dst[(maxRank - unsqueezedRank) + i] = src[(maxRank - 1) - shiftDim];
                    shiftDim++;
                }
            }

            return unsqueezed;
        }

        /// <summary>
        /// Creates a `TensorShape` by duplicating `this` and reshaping the dimensions to those given.
        ///
        /// If a dimension in the shape array is -1, Sentis infers the value from the size of the `TensorShape` and the remaining dimensions. Only one dimension can be -1.
        /// </summary>
        /// <param name="shape">The new shape as a span of integers.</param>
        /// <param name="allowZero">When the value is `true`, Sentis sets a dimension to zero if the new shape includes a zero. Otherwise Sentis retains the corresponding size at that axis from the original shape.</param>
        /// <returns>The reshape tensor shape.</returns>
        internal TensorShape Reshape(ReadOnlySpan<int> shape, bool allowZero = false)
        {
            Logger.AssertIsTrue(shape.Length <= maxRank, "ValueError: TensorShape are capped to rank=8, cannot create TensorShape of rank {0}", shape.Length);

            var reshapedRank = shape.Length;
            var reshaped = new TensorShape();
            reshaped.m_Rank = reshapedRank;
            reshaped.m_Length = length;

            int* dst = &reshaped.m_D7;

            var unknownDim = -1;
            var newLength = 1;
            var nonZeroLength = 1;
            var newNonZeroLength = 1;

            fixed (int* src = &m_D7)
            {
                for (var i = 0; i < m_Rank; i++)
                    nonZeroLength *= Mathf.Max(1, src[(maxRank - rank) + i]);

                for (var i = 0; i < shape.Length; i++)
                {
                    var shapeValue = shape[i];
                    if (shapeValue == -1)
                    {
                        Logger.AssertIsTrue(unknownDim == -1, "ValueError: at most one dimension of new shape can be -1", this);
                        unknownDim = i;
                    }
                    else
                    {
                        if (!allowZero && shapeValue == 0)
                        {
                            Logger.AssertIsTrue(i < rank, "ValueError: current index {0} out of bounds of the source shape {1}", i, this);
                            shapeValue = src[(maxRank - rank) + i];
                        }

                        dst[(maxRank - reshapedRank) + i] = shapeValue;
                        newLength *= shapeValue;
                        newNonZeroLength *= Mathf.Max(1, shapeValue);
                    }
                }
            }

            if (newLength != 0 && length == 0)
            {
                Logger.AssertIsTrue(unknownDim >= 0, "ValueError: reshaped length does not match with input length");
                dst[(maxRank - reshapedRank) + unknownDim] = 0;
                newLength = 0;
            }
            else if (unknownDim >= 0)
            {
                Logger.AssertIsTrue(nonZeroLength % newNonZeroLength == 0, "ValueError: reshaped length does not match with input length");
                var size = nonZeroLength / newNonZeroLength;
                dst[(maxRank - reshapedRank) + unknownDim] = size;
                newLength *= size;
            }

            Logger.AssertIsTrue(newLength == length, "ValueError: reshaped length does not match with input length");

            return reshaped;
        }

        /// <summary>
        /// Creates a `TensorShape` by duplicating `this` and reshaping to a 2D matrix. The dimensions before `axis` are collapsed into the outer dimension and the remaining axes are collapsed into the inner dimension.
        ///
        /// For example, if `this` is (2, 3, 4), and the value of `axis` is 2, the method returns (2 * 3, 4).
        /// </summary>
        /// <param name="axis">The axis at which to flatten.</param>
        /// <returns>The flattened tensor shape.</returns>
        internal TensorShape Flatten(int axis)
        {
            axis = axis >= 0 ? axis : rank + axis;
            var flatten = new TensorShape();

            int* dst = &flatten.m_D7;

            flatten.m_Rank = 2;
            flatten.m_Length = length;

            dst[maxRank - 1] = 1;
            dst[maxRank - 2] = 1;

            fixed (int* src = &m_D7)
            {
                for (int i = 0; i < axis; i++)
                {
                    dst[maxRank - 2] *= src[(maxRank - rank) + i];
                }

                for (int i = axis; i < rank; i++)
                {
                    dst[(maxRank - 2) + 1] *= src[(maxRank - rank) + i];
                }
            }

            return flatten;
        }

        /// <summary>
        /// Gets a 1D `TensorShape` (vector) representation of this `TensorShape`.
        /// </summary>
        /// <remarks>
        /// This method returns a new `TensorShape` of rank `1` (vector).
        /// The flattened shape has a single dimension equal to the original <see cref="length"/>.
        /// </remarks>
        /// <returns>A new `TensorShape` of rank `1` with <see cref="length"/> equal to the original length.</returns>
        /// <example>
        /// <para>Flatten a `TensorShape` to a vector shape</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(2, 3, 4);
        /// var flattened = shape.Flatten();
        /// // Result: (24)
        /// ]]></code>
        /// </example>
        public TensorShape Flatten()
        {
            return new TensorShape(length);
        }

        /// <summary>
        /// Broadcasts this `TensorShape` with another `TensorShape`.
        /// </summary>
        /// <remarks>
        /// This method computes the broadcasted shape following NumPy broadcasting rules:
        /// - Shapes are aligned from the innermost (rightmost) dimension
        /// - Dimensions are compatible if they're equal, or one of them is `1`
        /// - The result shape has the maximum of compatible dimensions
        /// - The result rank is the maximum of the two input ranks
        ///
        /// An error occurs if dimensions are incompatible (neither equal nor `1`).
        /// </remarks>
        /// <param name="other">The other `TensorShape` to broadcast with.</param>
        /// <returns>A new `TensorShape` representing the broadcasted result.</returns>
        /// <example>
        /// <para>Broadcast two `TensorShapes` together</para>
        /// <code lang="cs"><![CDATA[
        /// var shape1 = new TensorShape(1, 3, 4);
        /// var shape2 = new TensorShape(2, 1, 4);
        /// var broadcast = shape1.Broadcast(shape2);
        /// // Result: (2, 3, 4)
        /// var shape3 = new TensorShape(3, 4);
        /// var broadcast2 = shape2.Broadcast(shape3);
        /// // Result: (2, 3, 4)
        /// ]]></code>
        /// </example>
        public TensorShape Broadcast(TensorShape other)
        {
            TensorShape broadcast = new TensorShape();
            broadcast.m_Rank = Math.Max(rank, other.rank);

            int* srcB = &other.m_D7;
            int* dst = &broadcast.m_D7;
            fixed (int* srcA = &m_D7)
            {
                dst[0] = !(rank > 7) || (srcA[0] == 1 && other.rank > 7) ? srcB[0] : srcA[0]; // srcA[0] == 1 && shape.rank > 7 ? srcB[0] : this.rank > 7 ? srcA[0] : srcB[0];
                dst[1] = !(rank > 6) || (srcA[1] == 1 && other.rank > 6) ? srcB[1] : srcA[1]; // srcA[1] == 1 && shape.rank > 6 ? srcB[1] : this.rank > 6 ? srcA[1] : srcB[1];
                dst[2] = !(rank > 5) || (srcA[2] == 1 && other.rank > 5) ? srcB[2] : srcA[2]; // srcA[2] == 1 && shape.rank > 5 ? srcB[2] : this.rank > 5 ? srcA[2] : srcB[2];
                dst[3] = !(rank > 4) || (srcA[3] == 1 && other.rank > 4) ? srcB[3] : srcA[3]; // srcA[3] == 1 && shape.rank > 4 ? srcB[3] : this.rank > 4 ? srcA[3] : srcB[3];
                dst[4] = !(rank > 3) || (srcA[4] == 1 && other.rank > 3) ? srcB[4] : srcA[4]; // srcA[4] == 1 && shape.rank > 3 ? srcB[4] : this.rank > 3 ? srcA[4] : srcB[4];
                dst[5] = !(rank > 2) || (srcA[5] == 1 && other.rank > 2) ? srcB[5] : srcA[5]; // srcA[5] == 1 && shape.rank > 2 ? srcB[5] : this.rank > 2 ? srcA[5] : srcB[5];
                dst[6] = !(rank > 1) || (srcA[6] == 1 && other.rank > 1) ? srcB[6] : srcA[6]; // srcA[6] == 1 && shape.rank > 1 ? srcB[6] : this.rank > 1 ? srcA[6] : srcB[6];
                dst[7] = !(rank > 0) || (srcA[7] == 1 && other.rank > 0) ? srcB[7] : srcA[7]; // srcA[7] == 1 && shape.rank > 0 ? srcB[7] : this.rank > 0 ? srcA[7] : srcB[7];

                Logger.AssertIsTrue((srcA[0] == srcB[0]) || (srcA[0] == 1) || (srcB[0] == 1) || (srcA[0] == 0 && srcB[0] != 0) || (srcB[0] == 0 && srcA[0] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
                Logger.AssertIsTrue((srcA[1] == srcB[1]) || (srcA[1] == 1) || (srcB[1] == 1) || (srcA[1] == 0 && srcB[1] != 0) || (srcB[1] == 0 && srcA[1] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
                Logger.AssertIsTrue((srcA[2] == srcB[2]) || (srcA[2] == 1) || (srcB[2] == 1) || (srcA[2] == 0 && srcB[2] != 0) || (srcB[2] == 0 && srcA[2] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
                Logger.AssertIsTrue((srcA[3] == srcB[3]) || (srcA[3] == 1) || (srcB[3] == 1) || (srcA[3] == 0 && srcB[3] != 0) || (srcB[3] == 0 && srcA[3] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
                Logger.AssertIsTrue((srcA[4] == srcB[4]) || (srcA[4] == 1) || (srcB[4] == 1) || (srcA[4] == 0 && srcB[4] != 0) || (srcB[4] == 0 && srcA[4] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
                Logger.AssertIsTrue((srcA[5] == srcB[5]) || (srcA[5] == 1) || (srcB[5] == 1) || (srcA[5] == 0 && srcB[5] != 0) || (srcB[5] == 0 && srcA[5] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
                Logger.AssertIsTrue((srcA[6] == srcB[6]) || (srcA[6] == 1) || (srcB[6] == 1) || (srcA[6] == 0 && srcB[6] != 0) || (srcB[6] == 0 && srcA[6] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
                Logger.AssertIsTrue((srcA[7] == srcB[7]) || (srcA[7] == 1) || (srcB[7] == 1) || (srcA[7] == 0 && srcB[7] != 0) || (srcB[7] == 0 && srcA[7] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
            }

            broadcast.RecomputeLength();

            return broadcast;
        }

        /// <summary>
        /// Creates a `TensorShape` with all dimensions equal to `1`.
        /// </summary>
        /// <remarks>
        /// This static method creates a new `TensorShape` with the specified `rank` where every
        /// dimension is `1`. The resulting shape has a <see cref="length"/> of `1`.
        /// The maximum rank is `8`.
        /// </remarks>
        /// <param name="rank">The number of dimensions in the shape. Must be between `0` and `8`.</param>
        /// <returns>A new `TensorShape` with all dimensions equal to `1`.</returns>
        /// <example>
        /// <para>Create a `TensorShape` with all dimensions equal to `1`</para>
        /// <code lang="cs"><![CDATA[
        /// var ones = TensorShape.Ones(3);
        /// // Result: (1, 1, 1)
        /// ]]></code>
        /// </example>
        public static TensorShape Ones(int rank)
        {
            Logger.AssertIsTrue(rank <= maxRank, "ValueError: TensorShape are capped to rank=8, cannot create empty shape of rank {0}", rank);
            var empty = new TensorShape();
            empty.m_Rank = rank;
            empty.m_Length = 1;

            int* dst = &empty.m_D7;
            for (int i = 0; i < rank; i++)
            {
                dst[(maxRank - rank) + i] = 1;
            }

            return empty;
        }

        /// <summary>
        /// Creates a `TensorShape` of a given rank and with the same inner dimensions by adding outer dimensions of size 1 when needed.
        ///
        /// For example, if the `TensorShape` is (256, 256, 3), and the value of `rank` is 5, the method returns (1, 1, 256, 256, 3).
        /// </summary>
        /// <param name="rank">The rank to which to broadcast to.</param>
        /// <returns>The broadcast tensor shape.</returns>
        internal TensorShape BroadcastToRank(int rank)
        {
            Logger.AssertIsTrue(rank >= this.rank, "ValueError: broadcasting to lower rank tensor {0}, {1}", this.rank, rank);
            Logger.AssertIsTrue(rank <= maxRank, "ValueError: TensorShape are capped to rank=8, cannot broadcast shape {0} to rank > 8", this);

            var broadcast = new TensorShape();
            broadcast.m_Rank = rank;
            broadcast.m_Length = length;

            int* dst = &broadcast.m_D7;

            for (int i = 0; i < (rank - this.rank); i++)
            {
                dst[(maxRank - rank) + i] = 1;
            }

            fixed (int* src = &m_D7)
            {
                for (int i = 0; i < this.rank; i++)
                {
                    dst[(maxRank - rank) + (i + (rank - this.rank))] = src[(maxRank - this.rank) + i];
                }
            }

            return broadcast;
        }

        /// <summary>
        /// Creates a `TensorShape` by repeating this `TensorShape` a number of times along each axis.
        /// </summary>
        /// <param name="repeats">The repeat counts along each axis as a span of integers.</param>
        /// <returns>The tiled tensor shape.</returns>
        internal TensorShape Tile(ReadOnlySpan<int> repeats)
        {
            TensorShape mul = new TensorShape();
            mul.m_Rank = Math.Max(rank, repeats.Length);

            int* dst = &mul.m_D7;
            fixed (int* srcA = &m_D7)
            {
                for (int i = 0; i < mul.m_Rank; i++)
                {
                    dst[(maxRank - 1) - i] = (i < rank ? srcA[(maxRank - 1) - i] : 1) * (i < repeats.Length ? repeats[(repeats.Length - 1) - i] : 1);
                }
            }

            mul.RecomputeLength();

            return mul;
        }

        /// <summary>
        /// Creates a `TensorShape` by concatenating this `TensorShape` with `other` along a given axis. The dimensions of the shapes must be equal, except at `axis`.
        ///
        /// For example if `this` is (2, 3, 4, 5), `other` is (2, 2, 4, 5), and the value of `axis` is 1, the method returns (2, 5, 4, 5).
        /// </summary>
        /// <param name="other">The other tensor shape with which to concatenate.</param>
        /// <param name="axis">The axis along which to concatenate.</param>
        /// <returns>The concatenated tensor shape.</returns>
        internal TensorShape Concat(TensorShape other, int axis)
        {
            TensorShape concat = new TensorShape(this);
            axis = other.Axis(axis);

            int* src = &other.m_D7;
            int* dst = &concat.m_D7;
            dst[(maxRank - rank) + axis] += src[(maxRank - rank) + axis];

#if (UNITY_ASSERTIONS)
            for (int r = 0; r < Math.Max(rank, other.rank); r++)
            {
                if (r == axis)
                    continue;
                if (src[(maxRank - rank) + r] == 0 || dst[(maxRank - rank) + r] == 0)
                    continue;
                Logger.AssertAreEqual(src[(maxRank - rank) + r], dst[(maxRank - rank) + r], "ValueError: all input shapes for Concat must be equal {0}, {1} except on axis ({2}) dim", this, other, axis);
            }
#endif

            concat.RecomputeLength();

            return concat;
        }

        /// <summary>
        /// Reduces a `TensorShape`'s dimension along `axis`.
        /// </summary>
        /// <remarks>
        /// This method returns a new `TensorShape` that represents the result of a reduction
        /// operation along the specified `axis`. If `keepDim` is `true`, the dimension is
        /// replaced with `1`. If `keepDim` is `false`, the dimension is removed entirely.
        ///
        /// For scalar tensors (rank `0`), returns the same shape if `keepDim` is `true`.
        ///
        /// This method does not allow reducing over dimensions equal to `0` when `keepDim` is `false`.
        /// </remarks>
        /// <param name="axis">The axis along which to reduce. Negative values count from the end.</param>
        /// <param name="keepDim">When `true`, the reduced dimension becomes `1`. When `false`, the dimension is removed.</param>
        /// <returns>A new `TensorShape` with the dimension `axis` reduced.</returns>
        /// <example>
        /// <para>Reduce a dimension along a specified axis</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(2, 3, 4, 5);
        ///
        /// var reduced1 = shape.Reduce(1, keepDim: true);
        /// // Result: (2, 1, 4, 5)
        ///
        /// var reduced2 = shape.Reduce(1, keepDim: false);
        /// // Result: (2, 4, 5)
        ///
        /// var reduced3 = shape.Reduce(-1, keepDim: true);
        /// // Result: (2, 3, 4, 1) - reduced last dimension
        /// ]]></code>
        /// </example>
        public TensorShape Reduce(int axis, bool keepDim = true)
        {
            TensorShape reducedShape = new TensorShape(this);
            if (rank == 0)
            {
                if (keepDim == false)
                    Logger.AssertIsTrue(rank >= 1, "ValueError: cannot squeeze scalar tensor {0}", this);
                return reducedShape;
            }

            if (this[axis] == 0)
            {
                Logger.AssertIsTrue(keepDim, "ValueError: cannot reduce on dim {0} with value of 0 if 'keepDim' is false", axis);
                reducedShape[axis] = 0;
                return reducedShape;
            }

            reducedShape[axis] = 1;

            if (!keepDim)
                reducedShape = reducedShape.Squeeze(axis);

            return reducedShape;
        }

        /// <summary>
        /// Creates a `TensorShape` by removing the dimensions at `axes`. For example, if `this` is (2, 3, 4, 5), `axis` is {1, 2} and the value of `keepDim` is `true`, the method returns (2, 1, 1, 5).
        ///
        /// This version of the op allows reducing over length 0 dims.
        /// </summary>
        /// <param name="axes">The axes along which to reduce.</param>
        /// <param name="keepDim">When the value is `true`, Sentis replaces the reduced axes with 1. Otherwise Sentis removes the reduced axes.</param>
        /// <returns>The reduced tensor shape.</returns>
        internal TensorShape Reduce(ReadOnlySpan<int> axes, bool keepDim)
        {
            if (axes == null || axes.Length == 0)
                return keepDim ? Ones(rank) : new TensorShape();

            TensorShape reducedShape = new TensorShape(this);

            for (int i = 0; i < axes.Length; i++)
            {
                int axis = Axis(axes[i]);
                reducedShape[axis] = 1;
            }

            if (!keepDim)
                reducedShape = reducedShape.Squeeze(axes);

            return reducedShape;
        }

        /// <summary>
        /// Creates a `TensorShape` by permuting axes. For example, if `this` is (6, 7, 8, 9), and `permutations` is {3, 0, 1, 2}, the method returns (9, 6, 7, 8).
        /// </summary>
        /// <param name="permutations">An array indexing the new tensor axis from the old ones.</param>
        /// <returns>The transposed tensor shape.</returns>
        internal TensorShape Transpose(ReadOnlySpan<int> permutations)
        {
            Logger.AssertAreEqual(rank, permutations.Length, "ValueError: shape ranks and permutations length do not match {0}, {1}", this, permutations.Length);

            TensorShape transposed = new TensorShape();
            transposed.m_Rank = rank;
            transposed.m_Length = length;

            int* dst = &transposed.m_D7;
            int newLength = 1;
            fixed (int* src = &m_D7)
            {
                for (var i = 0; i < permutations.Length; ++i)
                {
                    Logger.AssertIsTrue(permutations[i] >= 0 && permutations[i] < rank, "ValueError: permutation index out of range");
                    dst[(maxRank - rank) + i] = src[(maxRank - rank) + permutations[i]];
                    newLength *= dst[(maxRank - rank) + i];
                }
            }

            Logger.AssertIsTrue(newLength == length, "ValueError: incorrect permutations");

            return transposed;
        }

        /// <summary>
        /// Creates a `TensorShape` by reversing axes. For example, if `this` is (6, 7, 8, 9), the method returns (9, 8, 7, 6).
        /// </summary>
        /// <returns>The transposed tensor shape.</returns>
        internal TensorShape Transpose()
        {
            TensorShape transposed = new TensorShape();
            transposed.m_Rank = rank;
            transposed.m_Length = length;

            int* dst = &transposed.m_D7;
            fixed (int* src = &m_D7)
            {
                for (var i = 0; i < rank; ++i)
                    dst[(maxRank - rank) + i] = src[(maxRank - rank) + ((rank - 1) - i)];
            }

            return transposed;
        }

        /// <summary>
        /// Creates a `TensorShape` by padding axes. For example, if `this` is (1, 2, 3), and `pads` is {0, 0, 1, 0, 2, 2}, the method returns (1, 3, 7).
        /// </summary>
        /// <param name="pads">The lower and upper padding values for each dimension. For example [pad_left, pad_right] for 1D, or [pad_top, pad_bottom, pad_left, pad_right] for 2D.</param>
        /// <returns>The padded tensor shape.</returns>
        internal TensorShape Pad(ReadOnlySpan<int> pads)
        {
            Logger.AssertAreEqual(rank * 2, pads.Length, "ValueError: shape ranks and pad length do not match {0}, {1}", this, pads.Length);

            TensorShape padded = new TensorShape(this);

            int* dst = &padded.m_D7;
            for (int i = 0; i < padded.rank; i++)
                dst[(maxRank - rank) + i] += pads[i] + pads[i + padded.rank];

            padded.RecomputeLength();

            return padded;
        }

        /// <summary>
        /// Creates a `TensorShape` that results from performing a matrix multiplication between `this` and `other` with numpy-style broadcasting. For example, if `this` is (5, 2, 3), and `other` is (1, 3, 4), the method returns (5, 2, 4).
        /// </summary>
        /// <param name="other">The right hand tensor shape for the MatMul.</param>
        /// <returns>The resultant tensor shape.</returns>
        internal TensorShape MatMul(TensorShape other)
        {
            if (other.rank == 1)
                return MatMul(new TensorShape(other[0], 1)).Squeeze(-1);
            if (rank == 1)
                return new TensorShape(1, this[0]).MatMul(other).Squeeze(-2);

            Logger.AssertIsTrue(this[-1] == other[-2], "ValueError: dimension does not match for matmul {0}, {1}", this, other);

            TensorShape matmul = new TensorShape();
            matmul.m_Rank = Math.Max(rank, other.rank);

            int* srcB = &other.m_D7;
            int* dst = &matmul.m_D7;
            fixed (int* srcA = &m_D7)
            {
                dst[0] = !(rank > 7) || (srcA[0] == 1 && other.rank > 7) ? srcB[0] : srcA[0]; // srcA[0] == 1 && shape.rank > 7 ? srcB[0] : this.rank > 7 ? srcA[0] : srcB[0];
                dst[1] = !(rank > 6) || (srcA[1] == 1 && other.rank > 6) ? srcB[1] : srcA[1]; // srcA[1] == 1 && shape.rank > 6 ? srcB[1] : this.rank > 6 ? srcA[1] : srcB[1];
                dst[2] = !(rank > 5) || (srcA[2] == 1 && other.rank > 5) ? srcB[2] : srcA[2]; // srcA[2] == 1 && shape.rank > 5 ? srcB[2] : this.rank > 5 ? srcA[2] : srcB[2];
                dst[3] = !(rank > 4) || (srcA[3] == 1 && other.rank > 4) ? srcB[3] : srcA[3]; // srcA[3] == 1 && shape.rank > 4 ? srcB[3] : this.rank > 4 ? srcA[3] : srcB[3];
                dst[4] = !(rank > 3) || (srcA[4] == 1 && other.rank > 3) ? srcB[4] : srcA[4]; // srcA[4] == 1 && shape.rank > 3 ? srcB[4] : this.rank > 3 ? srcA[4] : srcB[4];
                dst[5] = !(rank > 2) || (srcA[5] == 1 && other.rank > 2) ? srcB[5] : srcA[5]; // srcA[5] == 1 && shape.rank > 2 ? srcB[5] : this.rank > 2 ? srcA[5] : srcB[5];
                dst[6] = srcA[6];
                dst[7] = srcB[7];

                Logger.AssertIsTrue((srcA[0] == srcB[0]) || (srcA[0] == 1) || (srcB[0] == 1) || (srcA[0] == 0 && srcB[0] != 0) || (srcB[0] == 0 && srcA[0] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
                Logger.AssertIsTrue((srcA[1] == srcB[1]) || (srcA[1] == 1) || (srcB[1] == 1) || (srcA[1] == 0 && srcB[1] != 0) || (srcB[1] == 0 && srcA[1] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
                Logger.AssertIsTrue((srcA[2] == srcB[2]) || (srcA[2] == 1) || (srcB[2] == 1) || (srcA[2] == 0 && srcB[2] != 0) || (srcB[2] == 0 && srcA[2] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
                Logger.AssertIsTrue((srcA[3] == srcB[3]) || (srcA[3] == 1) || (srcB[3] == 1) || (srcA[3] == 0 && srcB[3] != 0) || (srcB[3] == 0 && srcA[3] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
                Logger.AssertIsTrue((srcA[4] == srcB[4]) || (srcA[4] == 1) || (srcB[4] == 1) || (srcA[4] == 0 && srcB[4] != 0) || (srcB[4] == 0 && srcA[4] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
                Logger.AssertIsTrue((srcA[5] == srcB[5]) || (srcA[5] == 1) || (srcB[5] == 1) || (srcA[5] == 0 && srcB[5] != 0) || (srcB[5] == 0 && srcA[5] != 0), "TensorShape.ValueError: operands could not be broadcast together with shapes {0}, {1}", this, other);
            }

            matmul.RecomputeLength();

            return matmul;
        }

        /// <summary>
        /// Creates a `TensorShape` that results from slicing `this` along given axes with given starts, ends, and steps.
        /// </summary>
        /// <param name="starts">The start indices along each of the `axes`.</param>
        /// <param name="ends">The end indices along each of the `axes`.</param>
        /// <param name="axes">The axes along which to slice.</param>
        /// <param name="steps">The step sizes for each of the `axes`.</param>
        /// <returns>The sliced tensor shape.</returns>
        internal TensorShape Slice(ReadOnlySpan<int> starts, ReadOnlySpan<int> ends, ReadOnlySpan<int> axes, ReadOnlySpan<int> steps)
        {
            Logger.AssertAreEqual(starts.Length, ends.Length, "ValueError: starts and ends length do not match {0}, {1}", starts.Length, ends.Length);
            Logger.AssertAreEqual(starts.Length, axes.Length, "ValueError: starts and axes length do not match {0}, {1}", starts.Length, axes.Length);
            Logger.AssertAreEqual(starts.Length, steps.Length, "ValueError: starts and steps length do not match {0}, {1}", starts.Length, steps.Length);

            TensorShape strided = new TensorShape(this);

            int* dst = &strided.m_D7;

            for (int i = 0; i < starts.Length; i++)
            {
                int outputDim = (int)Math.Ceiling((double)(ends[i] - starts[i]) / (double)steps[i]);
                dst[(maxRank - rank) + axes[i]] = Mathf.Max(outputDim, 0);
            }

            strided.RecomputeLength();

            return strided;
        }

        internal TensorShape Split(int axis, int start, int end)
        {
            Assert.IsTrue(0 <= start && start <= end && end <= this[axis], "Split.InputError: start and end must obey 0 <= start <= end <= dim");

            var strided = new TensorShape(this);

            axis = Axis(axis);

            var dst = &strided.m_D7;
            dst[(maxRank - rank) + axis] = end - start;

            strided.RecomputeLength();
            return strided;
        }

        internal TensorShape Diagonal(int offset, int dim1, int dim2, ref Span<int> strides, out int storageOffset)
        {
            var rankO = rank - 1;
            var diagonal = Ones(rankO);

            dim1 = Axis(dim1);
            dim2 = Axis(dim2);

            Logger.AssertIsTrue(dim1 != dim2, "DimError: dim1 {0} cannot equal dim2 {1} for diagonal", dim1, dim2);
            fixed (int* src = &m_D7)
            {
                var dst = &diagonal.m_D7;

                var j = rankO - 2;
                var runningStride = 1;
                strides[maxRank - 1] = 0;
                var size1 = src[(maxRank - rank) + dim1];
                var size2 = src[(maxRank - rank) + dim2];
                dst[maxRank - 1] = Mathf.Max(0, Mathf.Min(size1 + Mathf.Min(0, offset), size2 - Mathf.Max(0, offset)));
                storageOffset = 0;
                for (var i = rank - 1; i >= 0; i--)
                {
                    if (i == dim1)
                    {
                        if (offset < 0)
                            storageOffset = -offset * runningStride;
                        strides[maxRank - 1] += runningStride;
                    }
                    else if (i == dim2)
                    {
                        if (offset >= 0)
                            storageOffset = offset * runningStride;
                        strides[maxRank - 1] += runningStride;
                    }
                    else
                    {
                        dst[(maxRank - rankO) + j] = src[(maxRank - rank) + i];
                        strides[(maxRank - rankO) + j] = runningStride;
                        j--;
                    }

                    runningStride *= src[(maxRank - rank) + i];
                }
            }

            diagonal.RecomputeLength();
            return diagonal;
        }

        /// <summary>
        /// Compares two `TensorShape` objects. Returns `true` if the two shapes have equal sized trailing "tailLength" innermost dimensions.
        /// Assumes both shapes have at least rank >= tailLength
        /// </summary>
        /// <param name="a">The first shape to compare.</param>
        /// <param name="b">The second shape to compare.</param>
        /// <param name="tailLength">The number of innermost dimensions to compare.</param>
        /// <param name="innermostNonMatchingDimension">Output the innermost dimension number that didn't match, starting at -1 (-tailLength-1 if all match).</param>
        /// <returns>
        /// True if tailLength == 0.
        /// Otherwise true if the two shapes are equal along the tailLength innermost dimensions.
        /// </returns>
        internal static bool InnermostEqual(TensorShape a, TensorShape b, int tailLength, out int innermostNonMatchingDimension)
        {
            innermostNonMatchingDimension = -1 - tailLength;

            if (tailLength > a.rank || tailLength > b.rank)
                return false;

            if (tailLength <= 0)
                return true;

            for (var i = 0; i < tailLength; i++)
            {
                if (a[a.rank - 1 - i] != b[b.rank - 1 - i])
                {
                    innermostNonMatchingDimension = -1 - i;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Compares two `TensorShape` objects. Returns `true` if the two shapes have equal sized heading "headLength" outermost dimensions.
        /// Assumes both shapes have at least rank >= headLength
        /// </summary>
        /// <param name="a">The first shape to compare.</param>
        /// <param name="b">The second shape to compare.</param>
        /// <param name="headLength">The number of outermost dimensions to compare.</param>
        /// <param name="outermostNonMatchingDimension">Output the outermost dimension number that didn't match headLength (headLength if all match).</param>
        /// <returns>
        /// True if headLength == 0.
        /// Otherwise true if the two shapes are equal along the headLength outermost dimensions.
        /// </returns>
        internal static bool OutermostEqual(TensorShape a, TensorShape b, int headLength, out int outermostNonMatchingDimension)
        {
            outermostNonMatchingDimension = headLength;

            if (headLength > a.rank || headLength > b.rank)
                return false;

            if (headLength <= 0)
                return true;

            for (var i = 0; i < headLength; i++)
            {
                if (a[i] != b[i])
                {
                    outermostNonMatchingDimension = i;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Compares two `TensorShapes` for equality.
        /// </summary>
        /// <remarks>
        /// Two `TensorShapes` are equal if they have the same <see cref="rank"/> and all
        /// corresponding dimensions are equal. The comparison checks both rank and length first
        /// for efficiency before comparing individual dimensions.
        /// </remarks>
        /// <param name="a">The first `TensorShape`.</param>
        /// <param name="b">The second `TensorShape`.</param>
        /// <returns>`true` if both shapes have the same rank and all dimensions are equal; otherwise, `false`.</returns>
        /// <example>
        /// <para>Compare two `TensorShapes`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape1 = new TensorShape(2, 3, 4);
        /// var shape2 = new TensorShape(2, 3, 5);
        ///
        /// shape1 == shape1;  // Returns true
        /// shape1 == shape2;  // Returns false
        /// ]]></code>
        /// </example>
        public static bool operator ==(TensorShape a, TensorShape b)
        {
            if (a.rank != b.rank)
                return false;

            if (a.length != b.length)
                return false;

            for (var i = 0; i < a.rank; ++i)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Compares two `TensorShapes` for inequality.
        /// </summary>
        /// <remarks>
        /// Two `TensorShapes` are not equal if they have different ranks or any
        /// corresponding dimensions differ.
        /// </remarks>
        /// <param name="a">The first `TensorShape`.</param>
        /// <param name="b">The second `TensorShape`.</param>
        /// <returns>`true` if the shapes differ in rank or any dimension; otherwise, `false`.</returns>
        /// <example>
        /// <para>Compare two `TensorShapes` for inequality</para>
        /// <code lang="cs"><![CDATA[
        /// var shape1 = new TensorShape(2, 3, 4);
        /// var shape2 = new TensorShape(2, 3, 5);
        ///
        /// shape1 != shape2;  // Returns true
        /// shape1 != shape1;  // Returns false
        /// ]]></code>
        /// </example>
        public static bool operator !=(TensorShape a, TensorShape b)
        {
            return !(a == b);
        }

        /// <summary>
        /// Determines whether this `TensorShape` is equal to the specified object.
        /// </summary>
        /// <remarks>
        /// This method checks if `obj` is a `TensorShape` with the same rank and dimensions.
        /// Returns `false` if `obj` is null or not a `TensorShape`.
        /// </remarks>
        /// <param name="obj">The <see cref="object"/> to compare.</param>
        /// <returns>`true` if `obj` is a `TensorShape` equal to this shape; otherwise, `false`.</returns>
        /// <example>
        /// <para>Check if a `TensorShape` equals another object</para>
        /// <code lang="cs"><![CDATA[
        /// var shape1 = new TensorShape(2, 3, 4);
        /// var shape2 = new TensorShape(2, 3, 4);
        /// var shape3 = new TensorShape(2, 3, 5);
        /// var notShape = "not a shape";
        ///
        /// shape1.Equals(shape2);    // Returns true
        /// shape1.Equals(shape3);    // Returns false
        /// shape1.Equals(notShape);  // Returns false
        /// ]]></code>
        /// </example>
        public override bool Equals(object obj)
        {
            // Check for null values and compare run-time types.
            if (obj == null || GetType() != obj.GetType())
                return false;

            return this == (TensorShape)obj;
        }

        /// <summary>
        /// Returns a hash code for this `TensorShape`.
        /// </summary>
        /// <remarks>
        /// The hash code is computed from the rank and all dimension values. TensorShapes
        /// that are <see cref="Equals"/> produce the same hash code.
        /// </remarks>
        /// <returns>A hash code value for this `TensorShape`.</returns>
        /// <example>
        /// <para>Get the hash code for a `TensorShape`</para>
        /// <code lang="cs"><![CDATA[
        /// var shape = new TensorShape(2, 3, 4);
        /// shape.GetHashCode();  // Hash code specific to shape (2, 3, 4)
        /// ]]></code>
        /// </example>
        public override int GetHashCode()
        {
            return HashCode.Combine(m_Rank, HashCode.Combine(m_D7, m_D6, m_D5, m_D4, m_D3, m_D2, m_D1, m_D0));
        }

        internal int RavelIndex(int d7, int d6, int d5, int d4, int d3, int d2, int d1, int d0)
        {
            return (((((((d7 * m_D6 + d6) * m_D5 + d5) * m_D4 + d4) * m_D3 + d3) * m_D2 + d2) * m_D1 + d1) * m_D0) + d0;
        }

        internal int RavelIndex(int d6, int d5, int d4, int d3, int d2, int d1, int d0)
        {
            return ((((((d6 * m_D5 + d5) * m_D4 + d4) * m_D3 + d3) * m_D2 + d2) * m_D1 + d1) * m_D0) + d0;
        }

        internal int RavelIndex(int d5, int d4, int d3, int d2, int d1, int d0)
        {
            return ((((d5 * m_D4 + d4) * m_D3 + d3) * m_D2 + d2) * m_D1 + d1) * m_D0 + d0;
        }

        internal int RavelIndex(int d4, int d3, int d2, int d1, int d0)
        {
            return (((d4 * m_D3 + d3) * m_D2 + d2) * m_D1 + d1) * m_D0 + d0;
        }

        internal int RavelIndex(int d3, int d2, int d1, int d0)
        {
            return ((d3 * m_D2 + d2) * m_D1 + d1) * m_D0 + d0;
        }

        internal int RavelIndex(int d2, int d1, int d0)
        {
            return (d2 * m_D1 + d1) * m_D0 + d0;
        }

        internal int RavelIndex(int d1, int d0)
        {
            return d1 * m_D0 + d0;
        }
    }
}

