using System;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace
{
    /// <summary>
    /// Identifies the parser for an implementation of <see cref="Unity.InferenceEngine.Tokenization.Truncators.ITruncator"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class HfTruncationAttribute : HfAttribute
    {
        /// <summary>
        /// The strategy of padding.
        /// </summary>
        public readonly string Strategy;

        /// <summary>
        /// Initializes a new instance of the <see cref="HfTruncationAttribute"/> type.
        /// </summary>
        /// <param name="strategy">
        /// The type name of the decoder.
        /// It corresponds to the name in the Hugging Face JSON configuration.
        /// </param>
        public HfTruncationAttribute([NotNull] string strategy) =>
            Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
    }
}
