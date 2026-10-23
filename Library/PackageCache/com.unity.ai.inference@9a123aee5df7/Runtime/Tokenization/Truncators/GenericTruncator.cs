using System;
using System.Collections.Generic;
using Unity.InferenceEngine.Tokenization.Truncators.Strategies;

namespace Unity.InferenceEngine.Tokenization.Truncators
{
    /// <summary>
    /// General implementation of truncation, replacing the obsolete
    /// <see cref="StrategicTruncator"/> and <see cref="LongestFirstStrategy"/> while supporting
    /// more options.
    /// </summary>
    public class GenericTruncator : ITruncator
    {
        readonly Pool<List<Range>> m_ListOfRangesPool = new(() => new(), list => list.Clear());
        readonly Pool<List<Token>> m_ListOfTokenPool = new(() => new(), list => list.Clear());

        readonly ITruncationStrategy m_Strategy;
        readonly IRangeGenerator m_Direction;
        readonly int m_MaxLength;
        readonly int m_Stride;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTruncator"/> type.
        /// </summary>
        /// <param name="strategy">The strategy of truncation.
        /// <see cref="LongestFirstStrategy"/>, <see cref="OnlyFirstStrategy"/> and
        /// <see cref="OnlySecondStrategy"/> are the built-in implementations.</param>
        /// <param name="direction">The truncation direction, either Left with
        /// <see cref="LeftDirectionRangeGenerator"/> or Right with
        /// <see cref="RightDirectionRangeGenerator"/>.</param>
        /// <param name="maxLength">The maximum length of each truncated sequence.</param>
        /// <param name="stride">How to go along the sequence of tokens.</param>
        public GenericTruncator(ITruncationStrategy strategy, IRangeGenerator direction,
            int maxLength, int stride)
        {
            m_Strategy = strategy;
            m_Direction = direction;
            m_MaxLength = maxLength;
            m_Stride = stride;
        }

        /// <inheritdoc/>
        public void Truncate(
            IReadOnlyList<Token> inputA,
            IReadOnlyList<Token> inputB,
            int numAddedTokens,
            Output<IEnumerable<Token>> outputA,
            Output<IEnumerable<Token>> outputB)
        {
            if (inputA is null)
                throw new ArgumentNullException(nameof(inputA));

            var adjustedMaxLength = m_MaxLength - numAddedTokens;
            if (adjustedMaxLength == 0)
            {
                Truncate(inputA, 0, outputA);
                if (inputB is not null)
                    Truncate(inputB, 0, outputB);
                return;
            }

            var totalLength = inputA.Count + (inputB?.Count ?? 0);
            if (totalLength < adjustedMaxLength)
            {
                outputA.Add(inputA);
                if (inputB is not null)
                    outputB.Add(inputB);
                return;
            }

            var (lengthA, lengthB) =
                m_Strategy.GetTruncationLength(adjustedMaxLength, inputA, inputB);
            var toRemove = totalLength - adjustedMaxLength;

            Truncate(inputA, lengthA, outputA);
            if (inputB is not null)
                Truncate(inputB, lengthB, outputB);
        }

        void Truncate(IReadOnlyList<Token> tokens, int adjustedMaxLength,
            Output<IEnumerable<Token>> output)
        {
            if (adjustedMaxLength >= tokens.Count)
            {
                output.Add(tokens);
                return;
            }

            if (adjustedMaxLength == 0)
                return;

            if (m_Stride >= adjustedMaxLength)
                throw new("The stride must be strictly less than the adjusted max length");

            using var rangesHandle = m_ListOfRangesPool.Get(out var ranges);
            m_Direction.GetRanges(tokens.Count, adjustedMaxLength, m_Stride, ranges.AsOutput());

            using var truncatedHandle = m_ListOfTokenPool.Get(out var truncated);
            for (var i = 0; i < ranges.Count; i++)
            {
                var (first, length) = ranges[i].GetOffsetAndLength(tokens.Count);
                var stop = first + length;
                for (var j = first; j < stop; j++)
                    truncated.Add(tokens[j]);

                output.Add(truncated);
                truncated.Clear();
            }
        }
    }
}
