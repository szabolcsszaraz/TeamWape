using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.InferenceEngine.Tokenization.Padding
{
    /// <summary>
    /// Gets the size of the biggest sequence.
    /// </summary>
    public class BatchLongestSizeProvider : IPaddingSizeProvider
    {
        /// <inheritdoc />
        public int GetPaddingSize(IReadOnlyList<int> sizes) =>
            sizes?.Max() ?? throw new ArgumentNullException(nameof(sizes));
    }
}
