using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.SplitDelimiterBehaviors
{
    /// <summary>
    /// Merges each single delimiter split with its next content split.
    /// </summary>
    public class SplitDelimiterMergeWithPrevious : ISplitDelimiterBehavior
    {
        /// <summary>
        /// Gets a shared instance of the <see cref="SplitDelimiterMergeWithNext"/>.
        /// </summary>
        public static SplitDelimiterMergeWithPrevious Instance { get; } = new();

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

            Range? contentOffsets = null;
            for (var i = 0; i < splits.Count; i++)
            {
                var (offsets, isMatch) = splits[i];
                if (!isMatch)
                {
                    if (contentOffsets.HasValue)
                        output.Add(source[contentOffsets.Value]);

                    contentOffsets = offsets;
                }
                else if (contentOffsets.HasValue)
                {
                    var mergedOffsets = new Range(contentOffsets.Value.Start, offsets.End);
                    output.Add(source[mergedOffsets]);
                    contentOffsets = null;
                }
                else
                    output.Add(source[offsets]);
            }

            if(contentOffsets.HasValue)
                output.Add(source[contentOffsets.Value]);
        }
    }
}
