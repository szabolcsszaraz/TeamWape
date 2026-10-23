using System;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Mappers
{
    /// <summary>
    /// Represents a mergeable pair of token values used in Byte Pair Encoding (BPE) tokenization.
    /// Each pair consists of two consecutive token strings that can be merged into a single token
    /// during the BPE encoding process. See <see cref="BpeMapper"/>.
    /// </summary>
    public readonly struct MergePair : IEquatable<MergePair>
    {
        /// <summary>
        /// The first token value of the mergeable pair.
        /// </summary>
        public readonly string First;

        /// <summary>
        /// The second token value of the mergeable pair.
        /// </summary>
        public readonly string Second;

        /// <summary>
        /// The cached hash code for this merge pair to improve performance in hash-based collections.
        /// </summary>
        readonly int m_HashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="MergePair"/> struct with the specified token values.
        /// </summary>
        /// <param name="first">The first token value of the mergeable pair. Cannot be null.</param>
        /// <param name="second">The second token value of the mergeable pair. Cannot be null.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="first"/> or <paramref name="second"/> is null or empty.
        /// </exception>
        public MergePair([NotNull] string first, [NotNull] string second)
        {
            if(string.IsNullOrEmpty(first))
                throw new ArgumentException("Cannot be null or empty", nameof(first));

            if(string.IsNullOrEmpty(second))
                throw new ArgumentException("Cannot be null or empty", nameof(second));

            First = first;
            Second = second;
            m_HashCode = HashCode.Combine(First, Second);
        }

        /// <summary>
        /// Deconstructs this merge pair into its constituent token values.
        /// </summary>
        /// <param name="first">When this method returns, contains the first token value.</param>
        /// <param name="second">When this method returns, contains the second token value.</param>
        public void Deconstruct(out string first, out string second)
        {
            first = First;
            second = Second;
        }

        /// <summary>
        /// Returns a string representation of this merge pair in the format "({First},{Second})".
        /// </summary>
        /// <returns>A string representation of the merge pair.</returns>
        public override string ToString() => $"({First},{Second})";

        /// <summary>
        /// Returns the hash code for this merge pair.
        /// </summary>
        /// <returns>A hash code based on both token values.</returns>
        public override int GetHashCode() => m_HashCode;

        /// <summary>
        /// Determines whether this merge pair is equal to another merge pair.
        /// </summary>
        /// <param name="other">The merge pair to compare with this instance.</param>
        /// <returns>
        /// <see langword="true"/> if both the <see cref="First"/> and <see cref="Second"/>
        /// values are equal; otherwise, <see langword="false"/>.
        /// </returns>
        public bool Equals(MergePair other) => First == other.First && Second == other.Second;

        /// <summary>
        /// Determines whether this merge pair is equal to the specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="obj"/> is a <see cref="MergePair"/>
        /// and is equal to this instance; otherwise, <see langword="false"/>.
        /// </returns>
        public override bool Equals(object obj) => obj is MergePair other && Equals(other);
    }
}
