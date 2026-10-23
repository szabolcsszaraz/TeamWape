using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.SplitDelimiterBehaviors
{
    /// <summary>
    /// Merges each single delimiter split with its preceding content split.
    /// </summary>
    public class SplitDelimiterMergeWithNext : ISplitDelimiterBehavior
    {
        /// <summary>
        /// Gets a shared instance of the <see cref="SplitDelimiterMergeWithNext"/>.
        /// </summary>
        public static SplitDelimiterMergeWithNext Instance { get; } = new();

        /// <inheritdoc />
        public void Apply(
            SubString source,
            IReadOnlyList<(Range offsets, bool isMatch)> splits,
            Output<SubString> output)
        {
            if(source.IsNull)
                throw new ArgumentNullException(nameof(source));

            if (splits == null)
                throw new ArgumentNullException(nameof(splits));

            Range? delimOffsets = null;
            for (var i = 0; i < splits.Count; i++)
            {
                var (offsets, isMatch) = splits[i];
                if (isMatch)
                {
                    if (delimOffsets.HasValue)
                        output.Add(source[delimOffsets.Value]);

                    delimOffsets = offsets;
                }
                else if (delimOffsets.HasValue)
                {
                    var mergesOffsets = new Range(delimOffsets.Value.Start, offsets.End);
                    output.Add(source[mergesOffsets]);
                    delimOffsets = null;
                }
                else
                    output.Add(source[offsets]);
            }

            if(delimOffsets.HasValue)
                output.Add(source[delimOffsets.Value]);
        }
    }
}
