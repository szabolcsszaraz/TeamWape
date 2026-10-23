using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.SplitDelimiterBehaviors
{
    /// <summary>
    /// Removes the delimiter splits, only the content splits only.
    /// </summary>
    public class SplitDelimiterRemove : ISplitDelimiterBehavior
    {
        /// <summary>
        /// Gets a shared instance of the <see cref="SplitDelimiterRemove"/>.
        /// </summary>
        public static SplitDelimiterRemove Instance { get; } = new();

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

            for (var i = 0; i < splits.Count; i++)
            {
                var (offsets, isContent) = splits[i];
                if (!isContent)
                    output.Add(source[offsets]);
            }
        }
    }
}
