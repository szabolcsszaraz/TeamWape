using System;
using Unity.InferenceEngine.Tokenization.PreTokenizers;
using Unity.InferenceEngine.Tokenization.SplitDelimiterBehaviors;

namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// Options for how to deal with the delimiter when splitting the input string.
    /// See <see cref="RegexSplitPreTokenizer"/> and <see cref="StringSplitPreTokenizer"/>.
    /// </summary>
    /// <seealso cref="StringSplitPreTokenizer"/>
    /// <seealso cref="RegexSplitPreTokenizer"/>
    public enum SplitDelimiterBehavior
    {
        /// <summary>
        /// The delimiter is not included in the output tokens at all.
        /// </summary>
        Removed,

        /// <summary>
        /// The delimiter is kept as a separate token.
        /// </summary>
        Isolated,

        /// <summary>
        /// The delimiter is appended to the previous token.
        /// </summary>
        MergedWithPrevious,

        /// <summary>
        /// The delimiter is prepended to the next token.
        /// </summary>
        MergedWithNext,

        /// <summary>
        /// Variation of <see cref="Isolated"/>, but merges the contiguous delimiters.
        /// </summary>
        Contiguous,
    }

    static class SplitDelimiterBehaviorUtility
    {
        public static ISplitDelimiterBehavior GetImplementation(
            this SplitDelimiterBehavior @this)
        {
            return @this switch
            {
                SplitDelimiterBehavior.Removed => SplitDelimiterRemove.Instance,
                SplitDelimiterBehavior.Isolated => SplitDelimiterIsolate.Instance,
                SplitDelimiterBehavior.MergedWithPrevious => SplitDelimiterMergeWithPrevious
                    .Instance,
                SplitDelimiterBehavior.MergedWithNext => SplitDelimiterMergeWithNext.Instance,
                SplitDelimiterBehavior.Contiguous => SplitDelimiterContiguous.Instance,
                _ => throw new ArgumentOutOfRangeException(nameof(@this), @this, null)
            };
        }
    }
}
