using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.InferenceEngine.Tokenization.Truncators
{
    /// <summary>
    /// This truncation strategy truncates the longest sequence of tokens first.
    /// In case a secondary sequence of tokens is not provided, it doesn't remove any token.
    /// </summary>
    [Obsolete("Use GenericTruncator instead", false)]
    public class LongestFirstTruncator : StrategicTruncator
    {
        readonly Pool<List<Range>> m_ListOfRangesPool = new(() => new(), list => list.Clear());

        /// <inheritdoc cref="StrategicTruncator(IRangeGenerator, int, int)" />
        /// <summary>
        /// Initializes a new instance of the <see cref="LongestFirstTruncator" /> type.
        /// </summary>
        public LongestFirstTruncator(IRangeGenerator rangeGenerator, int maxLength, int stride)
            : base(rangeGenerator, maxLength, stride)
        {
        }

        /// <inheritdoc />
        protected override void Truncate(
            IReadOnlyList<Token> tokensA,
            IReadOnlyList<Token> tokensB,
            int maxLength,
            int toRemove,
            Output<IEnumerable<Token>> outputA,
            Output<IEnumerable<Token>> outputB)
        {
            using var rangesHandle = m_ListOfRangesPool.Get(out var ranges);

            if (tokensB.Count == 0)
            {
                GetRanges(tokensA.Count, tokensA.Count - toRemove, ranges.AsOutput());
                for (var i = 0; i < ranges.Count; i++)
                {
                    var range = ranges[i];
                    var (offset, length) = range.GetOffsetAndLength(tokensA.Count);
                    outputA.Add(tokensA.Skip(offset).Take(length));
                }
                ranges.Clear();

                return;
            }

            var (n1, n2) = (tokensA.Count, tokensB.Count);
            var swap = n1 > n2;
            if (swap) n1 = n2;

            n2 = n1 > maxLength
                ? n1
                : Math.Max(n1, maxLength - n1);

            if (n1 + n2 > maxLength)
            {
                n1 = maxLength / 2;
                n2 = n1 + maxLength % 2;
            }

            if (swap)
                (n1, n2) = (n2, n1);

            GetRanges(tokensA.Count, n1, ranges.AsOutput());
            for (var i = 0; i < ranges.Count; i++)
            {
                var range = ranges[i];
                var (offset, length) = range.GetOffsetAndLength(tokensA.Count);
                outputA.Add(tokensA.Skip(offset).Take(length));
            }
            ranges.Clear();

            GetRanges(tokensB.Count, n2, ranges.AsOutput());
            for (var i = 0; i < ranges.Count; i++)
            {
                var range = ranges[i];
                var (offset, length) = range.GetOffsetAndLength(tokensB.Count);
                outputB.Add(tokensB.Skip(offset).Take(length));
            }
            ranges.Clear();
        }
    }
}
