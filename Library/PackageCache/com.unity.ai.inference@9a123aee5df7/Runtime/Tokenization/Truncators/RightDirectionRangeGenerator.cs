using System;

namespace Unity.InferenceEngine.Tokenization.Truncators
{
    /// <summary>
    /// Generates a sequence of <see cref="Range" /> starting from the right (the upper bound of
    /// the source).
    /// </summary>
    public class RightDirectionRangeGenerator : RangeGeneratorBase
    {
        /// <summary>
        /// Singleton.
        /// </summary>
        public static IRangeGenerator Instance { get; } = new RightDirectionRangeGenerator();

        /// <inheritdoc />
        protected override int GetRangesInternal(int length, int rangeMaxLength,
            int stride, Output<Range> output)
        {
            var offset = rangeMaxLength - stride;
            var count = 0;

            for (var from = 0; from < length; from += offset)
            {
                var to = Math.Min(from + rangeMaxLength, length);
                output.Add(new(from, to));
                count++;
                if (to == length)
                    break;
            }
            return count;
        }
    }
}
