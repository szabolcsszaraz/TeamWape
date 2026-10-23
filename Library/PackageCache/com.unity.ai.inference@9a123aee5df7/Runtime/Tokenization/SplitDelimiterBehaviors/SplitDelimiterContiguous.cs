using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.SplitDelimiterBehaviors
{
    /// <summary>
    /// Aggregates all the successive delimiters into a single split and keeps all the content
    /// splits as is.
    /// </summary>
    public class SplitDelimiterContiguous : ISplitDelimiterBehavior
    {
        /// <summary>
        /// Gets a shared instance of the <see cref="SplitDelimiterContiguous"/>.
        /// </summary>
        public static SplitDelimiterContiguous Instance { get; } = new();

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
                    delimOffsets = delimOffsets.HasValue
                        ? new(delimOffsets.Value.Start, offsets.End)
                        : offsets;
                }
                else
                {
                    if (delimOffsets.HasValue)
                    {
                        output.Add(source[delimOffsets.Value]);
                        delimOffsets = null;
                    }
                    output.Add(source[offsets]);
                }
            }

            if(delimOffsets.HasValue)
                output.Add(source[delimOffsets.Value]);
        }
    }
}
