using System;

namespace Unity.InferenceEngine.Tokenization.Truncators
{
    /// <summary>
    /// Generates a sequence of <see cref="Range" /> starting from the left (<c>0</c>, the lower
    /// bound of the source).
    /// </summary>
    public class LeftDirectionRangeGenerator : RangeGeneratorBase
    {
        /// <summary>
        /// Singleton.
        /// </summary>
        public static IRangeGenerator Instance { get; } = new LeftDirectionRangeGenerator();

        /// <inheritdoc />
        protected override int GetRangesInternal(int length, int rangeMaxLength,
            int stride, Output<Range> output)
        {
            var offset = rangeMaxLength - stride;
            var count = 0;
            for (var to = length; to > 0; to -= offset)
            {
                var from = Math.Max(to - rangeMaxLength, 0);
                output.Add(new(from, to));
                count++;
                if (from == 0)
                    break;
            }
            return count;
        }
    }
}
