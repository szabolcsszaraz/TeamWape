using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.InferenceEngine
{
    public partial class FunctionalTensor
    {
        void IndexerSet(FunctionalTensor src, IEnumerable<IndexOrRange> indexOrRanges)
        {
            var starts = new List<int>();
            var ends = new List<int>();
            var axes = new List<int>();
            var axis = 0;
            foreach (var indexOrRange in indexOrRanges)
            {
                if (!indexOrRange.IsRangeAll())
                {
                    starts.Add(indexOrRange.Start());
                    ends.Add(indexOrRange.End());
                    axes.Add(axis);
                }

                if (indexOrRange.IsIndex)
                    src = src.Unsqueeze(axis);

                axis++;
            }
            var c = FunctionalLayer.SliceSet(Copy(), src, Functional.Constant(starts.ToArray()), Functional.Constant(ends.ToArray()), Functional.Constant(axes.ToArray()), null);
            m_PartialTensor = c.partialTensor;
            m_Source = c.source;
            m_Name = c.m_Name;
        }

        FunctionalTensor IndexerGet(IEnumerable<IndexOrRange> indexOrRanges)
        {
            var starts = new List<int>();
            var ends = new List<int>();
            var axes = new List<int>();
            var squeezeAxes = new List<int>();
            var axis = 0;
            foreach (var indexOrRange in indexOrRanges)
            {
                if (!indexOrRange.IsRangeAll())
                {
                    starts.Add(indexOrRange.Start());
                    ends.Add(indexOrRange.End());
                    axes.Add(axis);
                    if (indexOrRange.IsIndex)
                        squeezeAxes.Add(axis);
                }

                axis++;
            }

            if (starts.Count == 0)
                return this;
            var startsArray = starts.ToArray();
            var endsArray = ends.ToArray();
            var axesArray = axes.ToArray();
            var slice = FunctionalLayer.Slice(this, Functional.Constant(startsArray), Functional.Constant(endsArray), Functional.Constant(axesArray), null);
            if (squeezeAxes.Count > 0)
                slice = slice.Squeeze(squeezeAxes.ToArray());
            return slice;
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using index positions.
        /// </summary>
        /// <remarks>
        /// This indexer retrieves or assigns values at specific positions in the tensor using <see cref="Index"/> syntax.
        /// Each index specifies a single position along the corresponding dimension.
        /// Supports negative indexing using `^` notation to count from the end (for example, `^1` refers to the last element).
        /// </remarks>
        /// <example>
        /// <para>Access and modify tensor elements using index positions</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(3, 4), new[] {
        ///     1f, 2f, 3f, 4f,
        ///     5f, 6f, 7f, 8f,
        ///     9f, 10f, 11f, 12f
        /// });
        ///
        /// // Get element at position [1, 2]
        /// var element = tensor[1, 2]; // Shape: [] (scalar), value: 7
        ///
        /// // Get row using negative index - ^1 means last row
        /// var lastRow = tensor[^1, ..]; // Shape: [4], values: [9, 10, 11, 12]
        ///
        /// // Set first element to 99
        /// tensor[0, 0] = Functional.Constant(99f);
        /// // Result: [[99, 2, 3, 4], [5, 6, 7, 8], [9, 10, 11, 12]]
        /// ]]></code>
        /// </example>
        /// <param name="indices">The <see cref="Index"/> positions for each dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[params Index[] indices]
        {
            get => IndexerGet(indices.Select(i => new IndexOrRange(i)));
            set => IndexerSet(value, indices.Select(i => new IndexOrRange(i)));
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using range slices.
        /// </summary>
        /// <remarks>
        /// This indexer retrieves or assigns values from continuous ranges along each dimension using <see cref="Range"/> syntax.
        /// Each range selects a slice of elements along the corresponding dimension.
        /// Supports the `..` operator for selecting all elements in a dimension, and range endpoints can use negative indexing with `^`.
        /// </remarks>
        /// <example>
        /// <para>Access and modify tensor elements using ranges</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(3, 4), new[] {
        ///     1f, 2f, 3f, 4f,
        ///     5f, 6f, 7f, 8f,
        ///     9f, 10f, 11f, 12f
        /// });
        ///
        /// // Get first two rows, all columns - Shape: [2, 4]
        /// var subset = tensor[0..2, ..];
        /// // Result: [[1, 2, 3, 4], [5, 6, 7, 8]]
        ///
        /// // Get middle section using ranges - Shape: [2, 2]
        /// var middle = tensor[1..3, 1..3];
        /// // Result: [[6, 7], [10, 11]]
        ///
        /// // Use negative indexing - last 2 rows, first 3 columns
        /// var bottomLeft = tensor[^2.., ..3]; // Shape: [2, 3]
        /// // Result: [[5, 6, 7], [9, 10, 11]]
        ///
        /// // Set a slice
        /// var newValues = Functional.Constant(new TensorShape(1, 4), new[] { 99f, 98f, 97f, 96f });
        /// tensor[0..1, ..] = newValues;
        /// // Result: [[99, 98, 97, 96], [5, 6, 7, 8], [9, 10, 11, 12]]
        /// ]]></code>
        /// </example>
        /// <param name="ranges">The <see cref="Range"/>s for slicing each dimension.</param>
        /// <value>A functional tensor containing the sliced subset.</value>
        public FunctionalTensor this[params Range[] ranges]
        {
            get => IndexerGet(ranges.Select(r => new IndexOrRange(r)));
            set => IndexerSet(value, ranges.Select(r => new IndexOrRange(r)));
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using a specific index for the first dimension and a range for the second dimension.
        /// </summary>
        /// <remarks>
        /// This indexer combines index and range syntax for flexible tensor slicing.
        /// Supports negative indexing using `^` notation for both indices and range endpoints.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using an index for the first dimension and a range for the second</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(3, 4), new[] {
        ///     1f, 2f, 3f, 4f,
        ///     5f, 6f, 7f, 8f,
        ///     9f, 10f, 11f, 12f
        /// });
        ///
        /// // Get first row, columns 1 to 3 - Shape: [2]
        /// var result = tensor[0, 1..3];
        /// // Result: [2, 3]
        ///
        /// // Get last row, all columns - Shape: [4]
        /// var lastRow = tensor[^1, ..];
        /// // Result: [9, 10, 11, 12]
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Index"/> position for the first dimension.</param>
        /// <param name="i1">The <see cref="Range"/> slice for the second dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Index i0, Range i1]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using a range for the first dimension and a specific index for the second dimension.
        /// </summary>
        /// <remarks>
        /// This indexer combines range and index syntax for flexible tensor slicing.
        /// Supports negative indexing using `^` notation for both range endpoints and indices.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using a range for the first dimension and an index for the second</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(3, 4), new[] {
        ///     1f, 2f, 3f, 4f,
        ///     5f, 6f, 7f, 8f,
        ///     9f, 10f, 11f, 12f
        /// });
        ///
        /// // Get first two rows, second column - Shape: [2]
        /// var result = tensor[0..2, 1];
        /// // Result: [2, 6]
        ///
        /// // Get all rows, last column - Shape: [3]
        /// var lastCol = tensor[.., ^1];
        /// // Result: [4, 8, 12]
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Range"/> slice for the first dimension.</param>
        /// <param name="i1">The <see cref="Index"/> position for the second dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Range i0, Index i1]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using indices for the first two dimensions and a range for the third dimension.
        /// </summary>
        /// <remarks>
        /// This indexer selects specific elements along dimensions 0 and 1, and a range along dimension 2.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using indices for the first two dimensions and a range for the third</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,     // [0,0,:]
        ///     5f, 6f, 7f, 8f,     // [0,1,:]
        ///     9f, 10f, 11f, 12f,  // [0,2,:]
        ///     13f, 14f, 15f, 16f, // [1,0,:]
        ///     17f, 18f, 19f, 20f, // [1,1,:]
        ///     21f, 22f, 23f, 24f  // [1,2,:]
        /// });
        /// var slice = tensor[0, 1, 1..3]; // Shape: [2]
        /// // Result: [6, 7] (elements at [0,1,1] and [0,1,2])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Index"/> position for the first dimension.</param>
        /// <param name="i1">The <see cref="Index"/> position for the second dimension.</param>
        /// <param name="i2">The <see cref="Range"/> slice for the third dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Index i0, Index i1, Range i2]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using an index for dimensions 0 and 2, and a range for dimension 1.
        /// </summary>
        /// <remarks>
        /// This indexer selects specific elements along dimensions 0 and 2, and a range along dimension 1.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using an index for dimensions 0 and 2, and a range for dimension 1</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,     // [0,0,:]
        ///     5f, 6f, 7f, 8f,     // [0,1,:]
        ///     9f, 10f, 11f, 12f,  // [0,2,:]
        ///     13f, 14f, 15f, 16f, // [1,0,:]
        ///     17f, 18f, 19f, 20f, // [1,1,:]
        ///     21f, 22f, 23f, 24f  // [1,2,:]
        /// });
        /// var slice = tensor[0, 1..3, 2]; // Shape: [2]
        /// // Result: [7, 11] (elements at [0,1,2] and [0,2,2])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Index"/> position for the first dimension.</param>
        /// <param name="i1">The <see cref="Range"/> slice for the second dimension.</param>
        /// <param name="i2">The <see cref="Index"/> position for the third dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Index i0, Range i1, Index i2]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using an index for the first dimension and ranges for the second and third dimensions.
        /// </summary>
        /// <remarks>
        /// This indexer selects a specific element along dimension 0, and ranges along dimensions 1 and 2.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using an index for the first dimension and ranges for the second and third</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,     // [0,0,:]
        ///     5f, 6f, 7f, 8f,     // [0,1,:]
        ///     9f, 10f, 11f, 12f,  // [0,2,:]
        ///     13f, 14f, 15f, 16f, // [1,0,:]
        ///     17f, 18f, 19f, 20f, // [1,1,:]
        ///     21f, 22f, 23f, 24f  // [1,2,:]
        /// });
        /// var slice = tensor[0, 1..3, 1..3]; // Shape: [2, 2]
        /// // Result: [[6, 7], [10, 11]] (elements at [0,1:3,1:3])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Index"/> position for the first dimension.</param>
        /// <param name="i1">The <see cref="Range"/> slice for the second dimension.</param>
        /// <param name="i2">The <see cref="Range"/> slice for the third dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Index i0, Range i1, Range i2]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using a range for the first dimension and indices for the second and third dimensions.
        /// </summary>
        /// <remarks>
        /// This indexer selects a range along dimension 0, and specific elements along dimensions 1 and 2.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using a range for the first dimension and indices for the second and third</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,     // [0,0,:]
        ///     5f, 6f, 7f, 8f,     // [0,1,:]
        ///     9f, 10f, 11f, 12f,  // [0,2,:]
        ///     13f, 14f, 15f, 16f, // [1,0,:]
        ///     17f, 18f, 19f, 20f, // [1,1,:]
        ///     21f, 22f, 23f, 24f  // [1,2,:]
        /// });
        /// var slice = tensor[0..2, 1, 2]; // Shape: [2]
        /// // Result: [7, 19] (elements at [0,1,2] and [1,1,2])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Range"/> slice for the first dimension.</param>
        /// <param name="i1">The <see cref="Index"/> position for the second dimension.</param>
        /// <param name="i2">The <see cref="Index"/> position for the third dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Range i0, Index i1, Index i2]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using ranges for dimensions 0 and 2, and an index for dimension 1.
        /// </summary>
        /// <remarks>
        /// This indexer selects ranges along dimensions 0 and 2, and a specific element along dimension 1.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using ranges for dimensions 0 and 2, and an index for dimension 1</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,     // [0,0,:]
        ///     5f, 6f, 7f, 8f,     // [0,1,:]
        ///     9f, 10f, 11f, 12f,  // [0,2,:]
        ///     13f, 14f, 15f, 16f, // [1,0,:]
        ///     17f, 18f, 19f, 20f, // [1,1,:]
        ///     21f, 22f, 23f, 24f  // [1,2,:]
        /// });
        /// var slice = tensor[0..2, 1, 1..3]; // Shape: [2, 2]
        /// // Result: [[6, 7], [18, 19]] (elements at [0:2,1,1:3])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Range"/> slice for the first dimension.</param>
        /// <param name="i1">The <see cref="Index"/> position for the second dimension.</param>
        /// <param name="i2">The <see cref="Range"/> slice for the third dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Range i0, Index i1, Range i2]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using ranges for the first two dimensions and an index for the third dimension.
        /// </summary>
        /// <remarks>
        /// This indexer selects ranges along dimensions 0 and 1, and a specific element along dimension 2.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using ranges for the first two dimensions and an index for the third</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,     // [0,0,:]
        ///     5f, 6f, 7f, 8f,     // [0,1,:]
        ///     9f, 10f, 11f, 12f,  // [0,2,:]
        ///     13f, 14f, 15f, 16f, // [1,0,:]
        ///     17f, 18f, 19f, 20f, // [1,1,:]
        ///     21f, 22f, 23f, 24f  // [1,2,:]
        /// });
        /// var slice = tensor[0..2, 1..3, 2]; // Shape: [2, 2]
        /// // Result: [[7, 11], [19, 23]] (elements at [0:2,1:3,2])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Range"/> slice for the first dimension.</param>
        /// <param name="i1">The <see cref="Range"/> slice for the second dimension.</param>
        /// <param name="i2">The <see cref="Index"/> position for the third dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Range i0, Range i1, Index i2]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using indices for the first three dimensions and a range for the fourth dimension.
        /// </summary>
        /// <remarks>
        /// This indexer selects specific elements along dimensions 0, 1, and 2, and a range along dimension 3.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using indices for the first three dimensions and a range for the fourth</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0, 1, 1, 1..3]; // Shape: [2]
        /// // Result: [18, 19] (elements at [0,1,1,1:3])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Index"/> position for the first dimension.</param>
        /// <param name="i1">The <see cref="Index"/> position for the second dimension.</param>
        /// <param name="i2">The <see cref="Index"/> position for the third dimension.</param>
        /// <param name="i3">The <see cref="Range"/> slice for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Index i0, Index i1, Index i2, Range i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using indices for dimensions 0, 1, and 3, and a range for dimension 2.
        /// </summary>
        /// <remarks>
        /// This indexer selects specific elements along dimensions 0, 1, and 3, and a range along dimension 2.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using indices for dimensions 0, 1, and 3, and a range for dimension 2</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0, 1, 1..3, 2]; // Shape: [2]
        /// // Result: [19, 23] (elements at [0,1,1:3,2])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Index"/> position for the first dimension.</param>
        /// <param name="i1">The <see cref="Index"/> position for the second dimension.</param>
        /// <param name="i2">The <see cref="Range"/> slice for the third dimension.</param>
        /// <param name="i3">The <see cref="Index"/> position for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Index i0, Index i1, Range i2, Index i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using indices for the first two dimensions and ranges for the last two dimensions.
        /// </summary>
        /// <remarks>
        /// This indexer selects specific elements along dimensions 0 and 1, and ranges along dimensions 2 and 3.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using indices for the first two dimensions and ranges for the last two</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0, 1, 1..3, 1..3]; // Shape: [2, 2]
        /// // Result: [[18, 19], [22, 23]] (elements at [0,1,1:3,1:3])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Index"/> position for the first dimension.</param>
        /// <param name="i1">The <see cref="Index"/> position for the second dimension.</param>
        /// <param name="i2">The <see cref="Range"/> slice for the third dimension.</param>
        /// <param name="i3">The <see cref="Range"/> slice for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Index i0, Index i1, Range i2, Range i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using an index for dimension 0, a range for dimension 1, and indices for dimensions 2 and 3.
        /// </summary>
        /// <remarks>
        /// This indexer selects a specific element along dimensions 0, 2, and 3, and a range along dimension 1.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using indices for dimensions 0, 2, and 3, and a range for dimension 1</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0, 0..2, 1, 2]; // Shape: [2]
        /// // Result: [7, 19] (elements at [0,0:2,1,2])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Index"/> position for the first dimension.</param>
        /// <param name="i1">The <see cref="Range"/> slice for the second dimension.</param>
        /// <param name="i2">The <see cref="Index"/> position for the third dimension.</param>
        /// <param name="i3">The <see cref="Index"/> position for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Index i0, Range i1, Index i2, Index i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using an index for dimensions 0 and 2, and ranges for dimensions 1 and 3.
        /// </summary>
        /// <remarks>
        /// This indexer selects specific elements along dimensions 0 and 2, and ranges along dimensions 1 and 3.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using indices for dimensions 0 and 2, and ranges for dimensions 1 and 3</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0, 0..2, 1, 1..3]; // Shape: [2, 2]
        /// // Result: [[6, 7], [18, 19]] (elements at [0,0:2,1,1:3])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Index"/> position for the first dimension.</param>
        /// <param name="i1">The <see cref="Range"/> slice for the second dimension.</param>
        /// <param name="i2">The <see cref="Index"/> position for the third dimension.</param>
        /// <param name="i3">The <see cref="Range"/> slice for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Index i0, Range i1, Index i2, Range i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using an index for dimensions 0 and 3, and ranges for dimensions 1 and 2.
        /// </summary>
        /// <remarks>
        /// This indexer selects specific elements along dimensions 0 and 3, and ranges along dimensions 1 and 2.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using indices for dimensions 0 and 3, and ranges for dimensions 1 and 2</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0, 0..2, 1..3, 2]; // Shape: [2, 2]
        /// // Result: [[7, 11], [19, 23]] (elements at [0,0:2,1:3,2])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Index"/> position for the first dimension.</param>
        /// <param name="i1">The <see cref="Range"/> slice for the second dimension.</param>
        /// <param name="i2">The <see cref="Range"/> slice for the third dimension.</param>
        /// <param name="i3">The <see cref="Index"/> position for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Index i0, Range i1, Range i2, Index i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using an index for the first dimension and ranges for the last three dimensions.
        /// </summary>
        /// <remarks>
        /// This indexer selects a specific element along dimension 0, and ranges along dimensions 1, 2, and 3.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using an index for the first dimension and ranges for the last three</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0, 0..2, 1..3, 1..3]; // Shape: [2, 2, 2]
        /// // Result: [[[6, 7], [10, 11]], [[18, 19], [22, 23]]] (elements at [0,0:2,1:3,1:3])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Index"/> position for the first dimension.</param>
        /// <param name="i1">The <see cref="Range"/> slice for the second dimension.</param>
        /// <param name="i2">The <see cref="Range"/> slice for the third dimension.</param>
        /// <param name="i3">The <see cref="Range"/> slice for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Index i0, Range i1, Range i2, Range i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using a range for the first dimension and indices for the last three dimensions.
        /// </summary>
        /// <remarks>
        /// This indexer selects a range along dimension 0, and specific elements along dimensions 1, 2, and 3.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using a range for the first dimension and indices for the last three</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0..2, 1, 1, 2]; // Shape: [2]
        /// // Result: [19, 43] (elements at [0:2,1,1,2])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Range"/> slice for the first dimension.</param>
        /// <param name="i1">The <see cref="Index"/> position for the second dimension.</param>
        /// <param name="i2">The <see cref="Index"/> position for the third dimension.</param>
        /// <param name="i3">The <see cref="Index"/> position for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Range i0, Index i1, Index i2, Index i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using ranges for dimensions 0 and 3, and indices for dimensions 1 and 2.
        /// </summary>
        /// <remarks>
        /// This indexer selects ranges along dimensions 0 and 3, and specific elements along dimensions 1 and 2.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using ranges for dimensions 0 and 3, and indices for dimensions 1 and 2</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0..2, 1, 1, 1..3]; // Shape: [2, 2]
        /// // Result: [[18, 19], [42, 43]] (elements at [0:2,1,1,1:3])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Range"/> slice for the first dimension.</param>
        /// <param name="i1">The <see cref="Index"/> position for the second dimension.</param>
        /// <param name="i2">The <see cref="Index"/> position for the third dimension.</param>
        /// <param name="i3">The <see cref="Range"/> slice for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Range i0, Index i1, Index i2, Range i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using ranges for dimensions 0 and 2, and indices for dimensions 1 and 3.
        /// </summary>
        /// <remarks>
        /// This indexer selects ranges along dimensions 0 and 2, and specific elements along dimensions 1 and 3.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using ranges for dimensions 0 and 2, and indices for dimensions 1 and 3</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0..2, 1, 1..3, 2]; // Shape: [2, 2]
        /// // Result: [[19, 23], [43, 47]] (elements at [0:2,1,1:3,2])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Range"/> slice for the first dimension.</param>
        /// <param name="i1">The <see cref="Index"/> position for the second dimension.</param>
        /// <param name="i2">The <see cref="Range"/> slice for the third dimension.</param>
        /// <param name="i3">The <see cref="Index"/> position for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Range i0, Index i1, Range i2, Index i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using ranges for dimensions 0, 2, and 3, and an index for dimension 1.
        /// </summary>
        /// <remarks>
        /// This indexer selects ranges along dimensions 0, 2, and 3, and a specific element along dimension 1.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using ranges for dimensions 0, 2, and 3, and an index for dimension 1</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0..2, 1, 1..3, 1..3]; // Shape: [2, 2, 2]
        /// // Result: [[[18, 19], [22, 23]], [[42, 43], [46, 47]]] (elements at [0:2,1,1:3,1:3])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Range"/> slice for the first dimension.</param>
        /// <param name="i1">The <see cref="Index"/> position for the second dimension.</param>
        /// <param name="i2">The <see cref="Range"/> slice for the third dimension.</param>
        /// <param name="i3">The <see cref="Range"/> slice for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Range i0, Index i1, Range i2, Range i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using ranges for the first two dimensions and indices for the last two dimensions.
        /// </summary>
        /// <remarks>
        /// This indexer selects ranges along dimensions 0 and 1, and specific elements along dimensions 2 and 3.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using ranges for the first two dimensions and indices for the last two</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0..2, 0..2, 1, 2]; // Shape: [2, 2]
        /// // Result: [[7, 19], [31, 43]] (elements at [0:2,0:2,1,2])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Range"/> slice for the first dimension.</param>
        /// <param name="i1">The <see cref="Range"/> slice for the second dimension.</param>
        /// <param name="i2">The <see cref="Index"/> position for the third dimension.</param>
        /// <param name="i3">The <see cref="Index"/> position for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Range i0, Range i1, Index i2, Index i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using ranges for dimensions 0, 1, and 3, and an index for dimension 2.
        /// </summary>
        /// <remarks>
        /// This indexer selects ranges along dimensions 0, 1, and 3, and a specific element along dimension 2.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using ranges for dimensions 0, 1, and 3, and an index for dimension 2</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0..2, 0..2, 1, 1..3]; // Shape: [2, 2, 2]
        /// // Result: [[[6, 7], [18, 19]], [[30, 31], [42, 43]]] (elements at [0:2,0:2,1,1:3])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Range"/> slice for the first dimension.</param>
        /// <param name="i1">The <see cref="Range"/> slice for the second dimension.</param>
        /// <param name="i2">The <see cref="Index"/> position for the third dimension.</param>
        /// <param name="i3">The <see cref="Range"/> slice for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Range i0, Range i1, Index i2, Range i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }

        /// <summary>
        /// Gets or sets a subset of the tensor using ranges for the first three dimensions and an index for the fourth dimension.
        /// </summary>
        /// <remarks>
        /// This indexer selects ranges along dimensions 0, 1, and 2, and a specific element along dimension 3.
        /// Supports negative indexing using `^` notation.
        /// </remarks>
        /// <example>
        /// <para>Access tensor using ranges for the first three dimensions and an index for the fourth</para>
        /// <code lang="cs"><![CDATA[
        /// var tensor = Functional.Constant(new TensorShape(2, 2, 3, 4), new[] {
        ///     1f, 2f, 3f, 4f,      5f, 6f, 7f, 8f,      9f, 10f, 11f, 12f,     // [0,0,:,:]
        ///     13f, 14f, 15f, 16f,  17f, 18f, 19f, 20f,  21f, 22f, 23f, 24f,    // [0,1,:,:]
        ///     25f, 26f, 27f, 28f,  29f, 30f, 31f, 32f,  33f, 34f, 35f, 36f,    // [1,0,:,:]
        ///     37f, 38f, 39f, 40f,  41f, 42f, 43f, 44f,  45f, 46f, 47f, 48f     // [1,1,:,:]
        /// });
        /// var slice = tensor[0..2, 0..2, 1..3, 2]; // Shape: [2, 2, 2]
        /// // Result: [[[7, 11], [19, 23]], [[31, 35], [43, 47]]] (elements at [0:2,0:2,1:3,2])
        /// ]]></code>
        /// </example>
        /// <param name="i0">The <see cref="Range"/> slice for the first dimension.</param>
        /// <param name="i1">The <see cref="Range"/> slice for the second dimension.</param>
        /// <param name="i2">The <see cref="Range"/> slice for the third dimension.</param>
        /// <param name="i3">The <see cref="Index"/> position for the fourth dimension.</param>
        /// <value>A functional tensor containing the indexed subset.</value>
        public FunctionalTensor this[Range i0, Range i1, Range i2, Index i3]
        {
            get => IndexerGet(new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
            set => IndexerSet(value, new[] { new IndexOrRange(i0), new IndexOrRange(i1), new IndexOrRange(i2), new IndexOrRange(i3) });
        }
    }

    struct IndexOrRange
    {
        enum IndexOrRangeType
        {
            Index,
            Range
        }

        IndexOrRangeType m_Type;
        Index m_Index;
        Range m_Range;

        public IndexOrRange(Index index)
        {
            m_Type = IndexOrRangeType.Index;
            m_Index = index;
            m_Range = default;
        }

        public IndexOrRange(Range range)
        {
            m_Type = IndexOrRangeType.Range;
            m_Index = default;
            m_Range = range;
        }

        public bool IsIndex => m_Type == IndexOrRangeType.Index;

        public int Start()
        {
            return m_Type switch
            {
                IndexOrRangeType.Index => m_Index.IsFromEnd ? m_Index.Value == 0 ? int.MaxValue : -m_Index.Value : m_Index.Value,
                IndexOrRangeType.Range => m_Range.Start.IsFromEnd ? m_Range.Start.Value == 0 ? int.MaxValue : -m_Range.Start.Value : m_Range.Start.Value,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public int End()
        {
            return m_Type switch
            {
                IndexOrRangeType.Index => m_Index.IsFromEnd ? m_Index.Value == 1 ? int.MaxValue : -m_Index.Value + 1 : m_Index.Value + 1,
                IndexOrRangeType.Range => m_Range.End.IsFromEnd ? m_Range.End.Value == 0 ? int.MaxValue : -m_Range.End.Value : m_Range.End.Value,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public bool IsRangeAll()
        {
            return m_Type == IndexOrRangeType.Range && m_Range.Start is { IsFromEnd: false, Value: 0 } && m_Range.End is { IsFromEnd: true, Value: 0 };
        }
    }
}
