using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization
{
    interface ISplitDelimiterBehavior
    {
        /// <summary>
        /// Applies the delimiter behavior to the list of splits and stores the result into the
        /// specified <paramref name="output"/>.
        /// </summary>
        /// <param name="source">
        /// The source string of the splits.
        /// </param>
        /// <param name="splits">
        /// The list of splits, indicating of the split if a content, or a delimiter.
        /// </param>
        /// <param name="output">
        /// The target list where updated splits are added.
        /// </param>
        void Apply(
            SubString source,
            [NotNull] IReadOnlyList<(Range offsets, bool isMatch)> splits,
            Output<SubString> output);
    }
}
