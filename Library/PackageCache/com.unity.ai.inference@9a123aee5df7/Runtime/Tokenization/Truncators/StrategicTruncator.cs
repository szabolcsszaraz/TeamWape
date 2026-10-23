using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Truncators
{
    /// <summary>
    /// Base implementation for builtin <see cref="ITruncator" /> types using
    /// <see cref="IRangeGenerator" /> to truncate the sequences of tokens.
    /// </summary>
    [Obsolete("Use GenericTruncator instead", false)]
    public abstract class StrategicTruncator : ITruncator
    {
        readonly Pool<List<Token>> m_ListOfTokenPool = new(() => new(), list => list.Clear());

        /// <summary>
        /// Generates the range of each truncated sequence.
        /// </summary>
        readonly IRangeGenerator m_RangeGenerator;

        /// <summary>
        /// The maximum length of the truncated sequence of tokens.
        /// </summary>
        readonly int m_MaxLength;

        /// <summary>
        /// How to go along the sequence of tokens.
        /// </summary>
        readonly int m_Stride;

        /// <summary>
        /// Initializes a new instance of the <see cref="StrategicTruncator" /> type.
        /// </summary>
        /// <param name="rangeGenerator">
        /// Generates the range of each truncated sequence.
        /// </param>
        /// <param name="maxLength">
        /// The maximum length of each truncated sequence.
        /// </param>
        /// <param name="stride">
        /// How to go along the sequence of tokens.
        /// </param>
        protected StrategicTruncator([NotNull] IRangeGenerator rangeGenerator, int maxLength, int stride)
        {
            m_RangeGenerator = rangeGenerator ?? throw new ArgumentNullException(nameof(rangeGenerator));
            m_Stride = stride;
            m_MaxLength = maxLength;
        }

        /// <inheritdoc cref="ITruncator.Truncate" />
        /// <exception cref="ArgumentNullException">
        /// <list type="bullet">
        ///     <item>
        ///         <term>
        ///             <paramref name="inputA" /> is <see langword="null" />.
        ///         </term>
        ///     </item>
        ///     <item>
        ///         <term>
        ///             <paramref name="outputA" /> is <see langword="null" />.
        ///         </term>
        ///     </item>
        ///     <item>
        ///         <term>
        ///             <paramref name="outputB" /> is <see langword="null" /> while
        ///             <paramref name="inputB" /> is not.
        ///         </term>
        ///     </item>
        /// </list>
        /// </exception>
        public void Truncate(
            IReadOnlyList<Token> inputA,
            IReadOnlyList<Token> inputB,
            int numAddedTokens,
            Output<IEnumerable<Token>> outputA,
            Output<IEnumerable<Token>> outputB)
        {
            if (inputA is null)
                throw new ArgumentNullException(nameof(inputA));

            var maxPortionLength = m_MaxLength - numAddedTokens;

            using var handleA = m_ListOfTokenPool.Get(out var tokensA);
            using var handleB = m_ListOfTokenPool.Get(out var tokensB);

            tokensA.AddRange(inputA);

            if (inputB is not null)
                tokensB.AddRange(inputB);

            var totalLength = tokensA.Count + tokensB.Count;

            if (totalLength <= maxPortionLength)
            {
                outputA.Add(tokensA);

                if (inputB is not null)
                    outputB.Add(tokensB);

                return;
            }

            var toRemove = totalLength - maxPortionLength;

            Truncate(tokensA, tokensB, maxPortionLength, toRemove, outputA, outputB);
        }

        /// <summary>
        /// Gets the <see cref="Range" />s to truncate the sequences of tokens.
        /// </summary>
        /// <param name="length">
        /// The length of the sequence of tokens.
        /// </param>
        /// <param name="rangeMaxLength">
        /// The maximum length of each chunk.
        /// </param>
        /// <param name="output">
        /// The sequence of <see cref="Range" />
        /// </param>
        /// <returns>
        /// The number of ranges generated.
        /// </returns>
        protected int GetRanges(int length, int rangeMaxLength, Output<Range> output) =>
            m_RangeGenerator.GetRanges(length, rangeMaxLength, m_Stride, output);

        /// <summary>
        /// Truncates the input sequences of tokens (<paramref name="tokensA" /> and
        /// <paramref name="tokensB" />) and passes the resulting truncated subsequences to
        /// <paramref name="outputA" /> and <paramref name="outputB" />.
        /// </summary>
        /// <param name="tokensA">
        /// The tokens of the primary sequence.
        /// </param>
        /// <param name="tokensB">
        /// The tokens of the secondary sequence.
        /// </param>
        /// <param name="maxLength">
        /// The maximum length of each truncated sequence.
        /// </param>
        /// <param name="toRemove">
        /// The total number of tokens to remove from <paramref name="tokensA" /> and
        /// <paramref name="tokensB" /> in order to get the first truncated sequence.
        /// </param>
        /// <param name="outputA">
        /// The target container for truncated subsequences from <paramref name="tokensA" />.
        /// </param>
        /// <param name="outputB">
        /// The target container for truncated subsequences from <paramref name="tokensB" />.
        /// </param>
        protected abstract void Truncate(
            [NotNull] IReadOnlyList<Token> tokensA,
            [NotNull] IReadOnlyList<Token> tokensB,
            int maxLength,
            int toRemove,
            Output<IEnumerable<Token>> outputA,
            Output<IEnumerable<Token>> outputB);
    }
}
