using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.SplitDelimiterBehaviors
{
    /// <summary>
    /// Keeps both the content splits and the delimiter splits, separated.
    /// </summary>
    public class SplitDelimiterIsolate : ISplitDelimiterBehavior
    {
        /// <summary>
        /// Gets a shared instance of the <see cref="SplitDelimiterIsolate"/>.
        /// </summary>
        public static SplitDelimiterIsolate Instance { get; } = new();

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
                output.Add(source[splits[i].offsets]);
        }
    }
}
