using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.Padding
{
    /// <summary>
    /// Gives a fixed padding length.
    /// </summary>
    public class FixedPaddingSizeProvider : IPaddingSizeProvider
    {
        /// <summary>
        /// The target padding size.
        /// </summary>
        readonly int m_Size;

        /// <summary>
        /// Initializes a new instance of the <see cref="FixedPaddingSizeProvider" /> type.
        /// </summary>
        /// <param name="size">
        /// The target padding size.
        /// </param>
        public FixedPaddingSizeProvider(int size) => m_Size = size;

        /// <inheritdoc />
        public int GetPaddingSize(IReadOnlyList<int> _) => m_Size;
    }
}
